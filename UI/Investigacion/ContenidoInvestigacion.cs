using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Investigacion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Investigacion
{
	/// <summary>
	/// El CONTENIDO del panel de Investigacion: el aviso de Modo Viaje, la barra de progreso
	/// global, el arbol de carpetas de la Libreria y la lista de objetos de la carpeta abierta,
	/// con sus acciones.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Es un <see cref="UIElement"/> corriente que ocupa lo que le den, <b>sin marco, sin titulo y
	/// sin boton de cerrar</b>: todo eso es mecanica de apertura y vive en
	/// <see cref="PanelInvestigacionState"/>. Asi, cuando los seis paneles del mod se fusionen en
	/// uno solo con pestañas, esto se cuelga dentro de una pestaña tal cual, sin tocar ni una
	/// linea de lo de aqui.
	/// </para>
	/// <para>
	/// Todo lo que enseña sale de la API oficial de investigacion del juego a traves de
	/// <see cref="EstadoInvestigacion"/> - ni un diccionario propio - y todo lo que escribe pasa
	/// por el historial de WS7, asi que cualquier accion, incluida "quitar TODA la
	/// investigacion", se deshace con Ctrl+Z.
	/// </para>
	/// </remarks>
	public class ContenidoInvestigacion : UIElement
	{
		private const float AltoCabecera = 88f;
		private const float AltoPie = 42f;

		// La columna del arbol va en PORCENTAJE, no en pixeles fijos: el panel se estira con la
		// pantalla (0.96 del ancho, con tope 1080) y en una ventana pequeña - 800x720 en la
		// maquina de pruebas - una columna fija de 400 px se comia mas de la mitad y dejaba la
		// lista de objetos sin sitio.
		private const float FraccionArbol = 0.4f;
		private const float SeparacionColumnas = 10f;
		private const int MaximoFilasObjeto = 120;
		private const int FotogramasDeConfirmacion = 240;   // 4 segundos a 60 fps

		private UIPanel _cajaArbol;
		private UIPanel _cajaObjetos;
		private UIList _listaCarpetas;
		private UIList _listaObjetos;
		private EtiquetaTk _tituloObjetos;
		private EtiquetaTk _aviso;
		private EtiquetaTk _mensaje;

		private BotonTk _botonCarpeta;
		private BotonTk _botonQuitarCarpeta;
		private BotonTk _botonTodo;
		private BotonTk _botonQuitarTodo;
		private AlternadorTk _soloPendientes;

		private readonly List<FilaCarpetaInvestigacion> _filasCarpeta = new List<FilaCarpetaInvestigacion>();

		private CarpetaInvestigacion _seleccionada;
		private bool _soloFaltantes;
		private int _versionPintada = -1;
		private string _textoMensaje = "";
		private int _fotogramasMensaje;

		// Confirmacion en dos pasos de las dos acciones globales. No hay dialogos modales en esta
		// interfaz, y una accion que toca miles de objetos no puede dispararse con un solo clic.
		private BotonTk _pendienteDeConfirmar;
		private int _fotogramasConfirmacion;

		/// <summary>Carpeta abierta ahora mismo (la que se lista a la derecha).</summary>
		public CarpetaInvestigacion CarpetaSeleccionada => _seleccionada;

		public ContenidoInvestigacion()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirCabecera();
			ConstruirArbol();
			ConstruirObjetos();
			ConstruirPie();

			CatalogoInvestigacion.RefrescarContadores();
			ReconstruirArbol();

			// Se abre por la primera carpeta, para que el panel no arranque con la mitad derecha
			// vacia y haya que adivinar que hay que hacer.
			if (CatalogoInvestigacion.Raices.Count > 0) {
				Seleccionar(CatalogoInvestigacion.Raices[0], true);
			}
		}

		// ------------------------------------------------------------------ construccion

		private void ConstruirCabecera()
		{
			_aviso = new EtiquetaTk(TextoAviso, 0.8f, 700f, 22f);
			_aviso.ColorTexto = EstiloInvestigacion.Peligro;
			_aviso.Top.Set(0f, 0f);
			Append(_aviso);

			EtiquetaTk etiquetaProgreso = new EtiquetaTk(
				() => "Progreso global", 0.85f, 160f, 22f);
			etiquetaProgreso.ColorTexto = EstiloTk.TextoSuave;
			etiquetaProgreso.Top.Set(30f, 0f);
			Append(etiquetaProgreso);

			BarraProgresoTk barra = new BarraProgresoTk(
				() => EstadoInvestigacion.TotalCompletos,
				() => EstadoInvestigacion.TotalInvestigable,
				0f, 22f);
			barra.Width.Set(-450f, 1f);
			barra.Left.Set(150f, 0f);
			barra.Top.Set(30f, 0f);
			Append(barra);

			EtiquetaTk numeros = new EtiquetaTk(TextoProgreso, 0.85f, 290f, 22f);
			numeros.Top.Set(30f, 0f);
			numeros.HAlign = 1f;
			Append(numeros);

			_mensaje = new EtiquetaTk(() => _textoMensaje, 0.78f, 1040f, 20f);
			_mensaje.ColorTexto = EstiloTk.TextoSuave;
			_mensaje.Top.Set(58f, 0f);
			Append(_mensaje);
		}

		private void ConstruirArbol()
		{
			_cajaArbol = new UIPanel();
			_cajaArbol.Width.Set(0f, FraccionArbol);
			_cajaArbol.Top.Set(AltoCabecera, 0f);
			_cajaArbol.Height.Set(-(AltoCabecera + AltoPie), 1f);
			_cajaArbol.BackgroundColor = EstiloTk.FondoCaja;
			_cajaArbol.SetPadding(6f);
			Append(_cajaArbol);

			_listaCarpetas = new UIList();
			_listaCarpetas.Width.Set(-22f, 1f);
			_listaCarpetas.Height.Set(0f, 1f);
			_listaCarpetas.ListPadding = 1f;
			_cajaArbol.Append(_listaCarpetas);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Height.Set(0f, 1f);
			barra.SetView(100f, 1000f);
			_cajaArbol.Append(barra);
			_listaCarpetas.SetScrollbar(barra);
		}

		private void ConstruirObjetos()
		{
			_cajaObjetos = new UIPanel();
			_cajaObjetos.Left.Set(SeparacionColumnas, FraccionArbol);
			_cajaObjetos.Width.Set(-SeparacionColumnas, 1f - FraccionArbol);
			_cajaObjetos.Top.Set(AltoCabecera, 0f);
			_cajaObjetos.Height.Set(-(AltoCabecera + AltoPie), 1f);
			_cajaObjetos.BackgroundColor = EstiloTk.FondoCaja;
			_cajaObjetos.SetPadding(6f);
			Append(_cajaObjetos);

			_tituloObjetos = new EtiquetaTk(TextoTituloCarpeta, 0.8f, 200f, 22f);
			_tituloObjetos.Top.Set(2f, 0f);
			_cajaObjetos.Append(_tituloObjetos);

			_botonCarpeta = new BotonTk("Investigar carpeta", 0.75f);
			_botonCarpeta.Width.Set(-6f, 0.28f);
			_botonCarpeta.Height.Set(28f, 0f);
			_botonCarpeta.Top.Set(0f, 0f);
			_botonCarpeta.Left.Set(0f, 0.44f);
			_botonCarpeta.Ayuda = "Investiga del todo cada objeto de esta carpeta y de las que hay dentro";
			_botonCarpeta.AlPulsar += InvestigarCarpeta;
			_cajaObjetos.Append(_botonCarpeta);

			_botonQuitarCarpeta = new BotonTk("Quitar carpeta", 0.75f);
			_botonQuitarCarpeta.Width.Set(-6f, 0.28f);
			_botonQuitarCarpeta.Height.Set(28f, 0f);
			_botonQuitarCarpeta.Top.Set(0f, 0f);
			_botonQuitarCarpeta.Left.Set(0f, 0.72f);
			_botonQuitarCarpeta.Ayuda = "Deja sin investigar todo lo de esta carpeta (Ctrl+Z lo devuelve)";
			_botonQuitarCarpeta.AlPulsar += QuitarCarpeta;
			_cajaObjetos.Append(_botonQuitarCarpeta);

			_listaObjetos = new UIList();
			_listaObjetos.Width.Set(-22f, 1f);
			_listaObjetos.Top.Set(34f, 0f);
			_listaObjetos.Height.Set(-34f, 1f);
			_listaObjetos.ListPadding = 2f;
			_cajaObjetos.Append(_listaObjetos);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Top.Set(34f, 0f);
			barra.Height.Set(-34f, 1f);
			barra.SetView(100f, 1000f);
			_cajaObjetos.Append(barra);
			_listaObjetos.SetScrollbar(barra);
		}

		private void ConstruirPie()
		{
			_soloPendientes = new AlternadorTk("Solo lo que falta",
				() => _soloFaltantes, valor => { _soloFaltantes = valor; ReconstruirObjetos(); });
			_soloPendientes.Width.Set(220f, 0f);
			_soloPendientes.Height.Set(28f, 0f);
			_soloPendientes.VAlign = 1f;
			_soloPendientes.Left.Set(0f, 0f);
			_soloPendientes.Ayuda = "Esconde de la lista los objetos que ya estan investigados del todo";
			Append(_soloPendientes);

			_botonTodo = new BotonTk("Investigar TODO", 0.8f);
			_botonTodo.Width.Set(200f, 0f);
			_botonTodo.Height.Set(32f, 0f);
			_botonTodo.VAlign = 1f;
			_botonTodo.Left.Set(240f, 0f);
			_botonTodo.Ayuda = "Investiga del todo TODOS los objetos investigables de la partida";
			_botonTodo.AlPulsar += () => Confirmar(_botonTodo, "Investigar TODO", InvestigarTodo);
			Append(_botonTodo);

			_botonQuitarTodo = new BotonTk("Quitar TODA la investigacion", 0.8f);
			_botonQuitarTodo.Width.Set(280f, 0f);
			_botonQuitarTodo.Height.Set(32f, 0f);
			_botonQuitarTodo.VAlign = 1f;
			_botonQuitarTodo.Left.Set(452f, 0f);
			_botonQuitarTodo.Ayuda = "Deja el personaje sin nada investigado (Ctrl+Z lo devuelve)";
			_botonQuitarTodo.AlPulsar += () => Confirmar(_botonQuitarTodo, "Quitar TODA la investigacion", QuitarTodo);
			Append(_botonQuitarTodo);
		}

		// ------------------------------------------------------------------ textos vivos

		private string TextoAviso()
		{
			if (EstadoInvestigacion.ModoViaje) {
				return "";
			}
			// Texto CORTO: el largo ocupaba ~1040 px y se salia del marco por la derecha en una
			// ventana de 800, y partirlo en dos lineas tampoco valia porque la segunda se metia
			// por encima de "Progreso global", que va a 30 px fijos. Las dos cosas se vieron en
			// capturas reales del juego.
			return "AVISO: no es un personaje de Modo Viaje (dificultad " +
				(Main.LocalPlayer != null ? Main.LocalPlayer.difficulty.ToString() : "?") +
				"): se guarda, pero el juego no lo usa.";
		}

		private static string TextoProgreso()
		{
			int hechos = EstadoInvestigacion.TotalCompletos;
			int total = EstadoInvestigacion.TotalInvestigable;
			int porcentaje = total > 0 ? (int)Math.Round(hechos * 100.0 / total) : 0;
			return hechos + " / " + total + " objetos (" + porcentaje + "%)";
		}

		private string TextoTituloCarpeta()
		{
			if (_seleccionada == null) {
				return "Elige una carpeta";
			}
			// 22 caracteres y sin la palabra "investigados": el hueco que queda a la izquierda de
			// los dos botones de la derecha son ~180 px, y con 34 el texto se metia por debajo de
			// ellos (visto en una captura real).
			return EstiloInvestigacion.Acortar(_seleccionada.Nombre, 22) +
				"  " + _seleccionada.Hechos + "/" + _seleccionada.Total;
		}

		// ------------------------------------------------------------------ arbol

		/// <summary>Rehace la lista de filas del arbol con lo que este desplegado ahora.</summary>
		public void ReconstruirArbol()
		{
			_listaCarpetas.Clear();
			_filasCarpeta.Clear();

			foreach (CarpetaInvestigacion raiz in CatalogoInvestigacion.Raices) {
				AnadirFila(raiz);
			}
		}

		private void AnadirFila(CarpetaInvestigacion carpeta)
		{
			FilaCarpetaInvestigacion fila = new FilaCarpetaInvestigacion(carpeta);
			fila.Seleccionada = carpeta == _seleccionada;
			fila.AlPulsar += PulsarCarpeta;
			_filasCarpeta.Add(fila);
			_listaCarpetas.Add(fila);

			if (carpeta.Desplegada) {
				for (int i = 0; i < carpeta.Hijos.Count; i++) {
					AnadirFila(carpeta.Hijos[i]);
				}
			}
		}

		private void PulsarCarpeta(CarpetaInvestigacion carpeta)
		{
			// Una carpeta con hijas se abre y se cierra al pulsarla; una hoja solo se selecciona.
			// Es lo que hace cualquier arbol y evita tener que acertar con un triangulito de 10 px.
			if (!carpeta.EsHoja) {
				carpeta.Desplegada = !carpeta.Desplegada;
			}
			Seleccionar(carpeta, true);
		}

		/// <summary>Selecciona una carpeta y lista sus objetos.</summary>
		public void Seleccionar(CarpetaInvestigacion carpeta, bool rehacerArbol)
		{
			_seleccionada = carpeta;
			if (rehacerArbol) {
				ReconstruirArbol();
			}
			else {
				for (int i = 0; i < _filasCarpeta.Count; i++) {
					_filasCarpeta[i].Seleccionada = _filasCarpeta[i].Carpeta == carpeta;
				}
			}
			ReconstruirObjetos();
		}

		/// <summary>Busca una carpeta por su ruta completa. La usa la autoprueba.</summary>
		public CarpetaInvestigacion BuscarCarpeta(string ruta)
		{
			foreach (CarpetaInvestigacion raiz in CatalogoInvestigacion.Raices) {
				CarpetaInvestigacion encontrada = Buscar(raiz, ruta);
				if (encontrada != null) {
					return encontrada;
				}
			}
			return null;
		}

		private static CarpetaInvestigacion Buscar(CarpetaInvestigacion carpeta, string ruta)
		{
			if (carpeta.Ruta == ruta) {
				return carpeta;
			}
			for (int i = 0; i < carpeta.Hijos.Count; i++) {
				CarpetaInvestigacion encontrada = Buscar(carpeta.Hijos[i], ruta);
				if (encontrada != null) {
					return encontrada;
				}
			}
			return null;
		}

		// ------------------------------------------------------------------ objetos

		/// <summary>Rehace la lista de objetos de la carpeta abierta.</summary>
		public void ReconstruirObjetos()
		{
			_listaObjetos.Clear();
			if (_seleccionada == null) {
				return;
			}

			// Una hoja enseña lo suyo; una carpeta con hijas enseña TODO lo que hay dentro, para
			// que se pueda revisar sin ir carpeta por carpeta, pero con tope: la raiz de vanilla
			// tiene miles de objetos y construir miles de filas no le sirve a nadie.
			int[] tipos = _seleccionada.EsHoja ? _seleccionada.TiposPropios : _seleccionada.Tipos;

			int puestos = 0;
			int escondidos = 0;
			for (int i = 0; i < tipos.Length; i++) {
				if (_soloFaltantes && EstadoInvestigacion.Completo(tipos[i])) {
					escondidos++;
					continue;
				}
				if (puestos >= MaximoFilasObjeto) {
					break;
				}

				FilaObjetoInvestigacion fila = new FilaObjetoInvestigacion(tipos[i]);
				fila.AlPulsar += PulsarObjeto;
				_listaObjetos.Add(fila);
				puestos++;
			}

			int sinListar = Math.Max(0, tipos.Length - escondidos - puestos);
			if (sinListar > 0) {
				EtiquetaTk pie = new EtiquetaTk(
					() => "y " + sinListar + " objetos mas en las carpetas de dentro. Los botones de " +
						"carpeta si actuan sobre todos.", 0.72f, 500f, 22f);
				pie.ColorTexto = EstiloTk.TextoSuave;
				_listaObjetos.Add(pie);
			}
			else if (puestos == 0) {
				EtiquetaTk vacio = new EtiquetaTk(
					() => _soloFaltantes
						? "Nada pendiente aqui: todo esta investigado."
						: "Esta carpeta no tiene objetos investigables.",
					0.8f, 500f, 22f);
				vacio.ColorTexto = EstiloTk.TextoSuave;
				_listaObjetos.Add(vacio);
			}
		}

		private void PulsarObjeto(int tipo, bool investigar)
		{
			int[] uno = new int[] { tipo };
			string nombre = EstadoInvestigacion.NombreObjeto(tipo);

			ResultadoInvestigacion resultado = investigar
				? EstadoInvestigacion.Investigar("Investigar " + nombre, uno)
				: EstadoInvestigacion.Quitar("Quitar la investigacion de " + nombre, uno);

			Anunciar((investigar ? "Investigado: " : "Sin investigar: ") + nombre +
				" -> " + EstadoInvestigacion.Describir(tipo), resultado);
		}

		// ------------------------------------------------------------------ acciones

		/// <summary>Investiga del todo la carpeta abierta (y todo lo que cuelgue de ella).</summary>
		public void InvestigarCarpeta()
		{
			if (_seleccionada == null) {
				return;
			}
			ResultadoInvestigacion resultado = EstadoInvestigacion.Investigar(
				"Investigar la carpeta \"" + _seleccionada.Nombre + "\"", _seleccionada.Tipos);
			Anunciar("Carpeta \"" + _seleccionada.Nombre + "\": " + resultado.Resumen, resultado);
		}

		/// <summary>Quita la investigacion de toda la carpeta abierta.</summary>
		public void QuitarCarpeta()
		{
			if (_seleccionada == null) {
				return;
			}
			ResultadoInvestigacion resultado = EstadoInvestigacion.Quitar(
				"Quitar la investigacion de la carpeta \"" + _seleccionada.Nombre + "\"", _seleccionada.Tipos);
			Anunciar("Carpeta \"" + _seleccionada.Nombre + "\" sin investigar: " + resultado.Resumen, resultado);
		}

		/// <summary>Investiga TODO lo investigable de la partida.</summary>
		public void InvestigarTodo()
		{
			List<int> todos = new List<int>(EstadoInvestigacion.TiposInvestigables);
			ResultadoInvestigacion resultado = EstadoInvestigacion.Investigar("Investigar todo", todos);
			Anunciar("Investigar todo: " + resultado.Resumen, resultado);
		}

		/// <summary>Deja el personaje sin nada investigado.</summary>
		public void QuitarTodo()
		{
			ResultadoInvestigacion resultado = EstadoInvestigacion.QuitarTodo("Quitar toda la investigacion");
			Anunciar("Quitada toda la investigacion: " + resultado.Resumen, resultado);
		}

		/// <summary>
		/// Confirmacion en dos pasos de las acciones globales: el primer clic cambia el texto del
		/// boton y espera; el segundo, dentro de unos segundos, ejecuta. Un solo clic no puede
		/// rehacer miles de objetos de golpe, por muy deshacible que sea despues.
		/// </summary>
		private void Confirmar(BotonTk boton, string textoOriginal, Action accion)
		{
			if (_pendienteDeConfirmar == boton) {
				_pendienteDeConfirmar = null;
				boton.FijarTexto(textoOriginal);
				accion();
				return;
			}

			CancelarConfirmacion();
			_pendienteDeConfirmar = boton;
			_fotogramasConfirmacion = FotogramasDeConfirmacion;
			boton.FijarTexto("Seguro? Pulsa otra vez");
			boton.Activo = true;
		}

		private void CancelarConfirmacion()
		{
			if (_pendienteDeConfirmar == null) {
				return;
			}
			_pendienteDeConfirmar.Activo = false;
			_pendienteDeConfirmar.FijarTexto(_pendienteDeConfirmar == _botonTodo
				? "Investigar TODO"
				: "Quitar TODA la investigacion");
			_pendienteDeConfirmar = null;
		}

		private void Anunciar(string texto, ResultadoInvestigacion resultado)
		{
			_textoMensaje = texto;
			_fotogramasMensaje = 480;
			_mensaje.ColorTexto = resultado != null && resultado.HuboCambios
				? EstiloInvestigacion.Hecho
				: EstiloTk.TextoSuave;

			RegistroInvestigacion.Linea($"{Terrakeep.LogTag} Investigacion: {texto}");

			// Mismo canal de aviso que usa el deshacer/rehacer de WS7: el chat del juego.
			if (!Main.dedServ) {
				Main.NewText("Terrakeep: " + texto, EstiloInvestigacion.Hecho);
			}

			CatalogoInvestigacion.RefrescarContadores();
			ReconstruirObjetos();
		}

		// ------------------------------------------------------------------ ciclo de vida

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Los contadores se recalculan solo cuando el estado de investigacion ha cambiado de
			// verdad (LastEditId del tracker oficial), no en cada fotograma: son decenas de miles
			// de consultas. Y cambia tambien si el jugador sacrifica objetos en el menu del propio
			// juego con este panel abierto, asi que hay que mirarlo, no basta con recalcular
			// despues de nuestras propias acciones.
			if (CatalogoInvestigacion.RefrescarContadoresSiHaceFalta() && _versionPintada >= 0) {
				ReconstruirObjetos();
			}
			_versionPintada = EstadoInvestigacion.VersionDeEstado;

			if (_fotogramasMensaje > 0 && --_fotogramasMensaje == 0) {
				_textoMensaje = "";
			}

			if (_pendienteDeConfirmar != null && --_fotogramasConfirmacion <= 0) {
				CancelarConfirmacion();
			}

			bool hayCarpeta = _seleccionada != null && _seleccionada.Total > 0;
			_botonCarpeta.Habilitado = hayCarpeta;
			_botonQuitarCarpeta.Habilitado = hayCarpeta;
		}

		// ------------------------------------------------------------------ para el arnes

		/// <summary>
		/// Pulsa DE VERDAD el boton de la fila de un objeto (su <c>OnLeftClick</c> real, el mismo
		/// camino que un clic de raton), y devuelve el texto que tenia el boton. null si ese
		/// objeto no esta listado ahora mismo.
		/// </summary>
		public string PulsarBotonDeObjeto(int tipo)
		{
			foreach (UIElement hijo in _listaObjetos._items) {
				FilaObjetoInvestigacion fila = hijo as FilaObjetoInvestigacion;
				if (fila != null && fila.Tipo == tipo) {
					return fila.Pulsar();
				}
			}
			return null;
		}

		/// <summary>Pulsa de verdad "Investigar carpeta" o "Quitar carpeta".</summary>
		public string PulsarBotonDeCarpeta(bool investigar)
		{
			BotonTk boton = investigar ? _botonCarpeta : _botonQuitarCarpeta;
			string texto = boton.Texto;
			boton.LeftClick(new UIMouseEvent(boton, boton.GetDimensions().Center()));
			return texto;
		}

		/// <summary>
		/// Pulsa de verdad uno de los dos botones globales. Devuelve el texto que muestra el boton
		/// DESPUES del clic, que es lo que demuestra que la confirmacion en dos pasos existe: el
		/// primer clic lo deja en "Seguro?..." sin ejecutar nada.
		/// </summary>
		public string PulsarBotonGlobal(bool investigar)
		{
			BotonTk boton = investigar ? _botonTodo : _botonQuitarTodo;
			boton.LeftClick(new UIMouseEvent(boton, boton.GetDimensions().Center()));
			return boton.Texto;
		}

		/// <summary>true si hay una confirmacion global esperando el segundo clic.</summary>
		public bool EsperandoConfirmacion => _pendienteDeConfirmar != null;

		/// <summary>Texto que muestra ahora mismo uno de los dos botones globales.</summary>
		public string TextoBotonGlobal(bool investigar)
		{
			return (investigar ? _botonTodo : _botonQuitarTodo).Texto;
		}

		/// <summary>Numero de filas de carpeta visibles en el arbol ahora mismo.</summary>
		public int FilasDeCarpeta => _filasCarpeta.Count;

		/// <summary>El texto del aviso de Modo Viaje tal cual se esta pintando ahora mismo (vacio
		/// si el personaje SI es de Modo Viaje y por tanto no hay nada que avisar).</summary>
		public string AvisoVisible => TextoAviso();

		/// <summary>Recuento y medidas REALES de lo que hay puesto ahora mismo, ya recalculado.
		/// Es la evidencia de que el panel ocupa sitio de verdad en la pantalla del juego y no
		/// solo esta construido en memoria (mismo criterio de prueba que WS0/WS1/WS4).</summary>
		public string Informe()
		{
			int elementos = 0;
			int slots = 0;
			CalculatedStyle primerSlot = new CalculatedStyle();

			ExecuteRecursively(elemento => {
				elementos++;
				if (elemento is SlotMuestraInvestigacion) {
					if (slots == 0) {
						primerSlot = elemento.GetDimensions();
					}
					slots++;
				}
			});

			CalculatedStyle arbol = _cajaArbol.GetDimensions();
			CalculatedStyle objetos = _cajaObjetos.GetDimensions();

			return $"{elementos} elementos, {_filasCarpeta.Count} filas de carpeta y {slots} slots de objeto" +
				(slots > 0
					? $" (el 1o en x={(int)primerSlot.X} y={(int)primerSlot.Y} " +
						$"{(int)primerSlot.Width}x{(int)primerSlot.Height})"
					: "") +
				$"; caja del arbol x={(int)arbol.X} y={(int)arbol.Y} {(int)arbol.Width}x{(int)arbol.Height}, " +
				$"caja de objetos x={(int)objetos.X} y={(int)objetos.Y} {(int)objetos.Width}x{(int)objetos.Height}";
		}
	}
}
