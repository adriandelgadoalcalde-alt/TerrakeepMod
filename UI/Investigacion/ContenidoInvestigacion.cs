using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
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
		private UIScrollbar _scrollObjetos;
		private EtiquetaTk _tituloObjetos;
		private EtiquetaTk _aviso;
		private EtiquetaTk _mensaje;

		private BotonTk _botonCarpeta;
		private BotonTk _botonQuitarCarpeta;
		private BotonTk _botonTodo;
		private BotonTk _botonQuitarTodo;
		private AlternadorTk _soloPendientes;

		private readonly List<FilaCarpetaInvestigacion> _filasCarpeta = new List<FilaCarpetaInvestigacion>();
		private readonly List<FilaObjetoInvestigacion> _filasObjeto = new List<FilaObjetoInvestigacion>();

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
				() => Idiomas.Texto("Investigacion.ProgresoGlobal"), 0.85f, 160f, 22f);
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

			_botonCarpeta = new BotonTk(Idiomas.Texto("Investigacion.InvestigarCarpeta"), 0.75f);
			_botonCarpeta.Width.Set(-6f, 0.28f);
			_botonCarpeta.Height.Set(28f, 0f);
			_botonCarpeta.Top.Set(0f, 0f);
			_botonCarpeta.Left.Set(0f, 0.44f);
			_botonCarpeta.Ayuda = () => Idiomas.Texto("Investigacion.InvestigarCarpetaAyuda");
			_botonCarpeta.AlPulsar += InvestigarCarpeta;
			_cajaObjetos.Append(_botonCarpeta);

			_botonQuitarCarpeta = new BotonTk(Idiomas.Texto("Investigacion.QuitarCarpeta"), 0.75f);
			_botonQuitarCarpeta.Width.Set(-6f, 0.28f);
			_botonQuitarCarpeta.Height.Set(28f, 0f);
			_botonQuitarCarpeta.Top.Set(0f, 0f);
			_botonQuitarCarpeta.Left.Set(0f, 0.72f);
			_botonQuitarCarpeta.Ayuda = () => Idiomas.Texto("Investigacion.QuitarCarpetaAyuda");
			_botonQuitarCarpeta.AlPulsar += QuitarCarpeta;
			_cajaObjetos.Append(_botonQuitarCarpeta);

			_listaObjetos = new UIList();
			_listaObjetos.Width.Set(-22f, 1f);
			_listaObjetos.Top.Set(34f, 0f);
			_listaObjetos.Height.Set(-34f, 1f);
			_listaObjetos.ListPadding = 2f;
			_cajaObjetos.Append(_listaObjetos);

			_scrollObjetos = new UIScrollbar();
			_scrollObjetos.HAlign = 1f;
			_scrollObjetos.Top.Set(34f, 0f);
			_scrollObjetos.Height.Set(-34f, 1f);
			_scrollObjetos.SetView(100f, 1000f);
			_cajaObjetos.Append(_scrollObjetos);
			_listaObjetos.SetScrollbar(_scrollObjetos);
		}

		private void ConstruirPie()
		{
			_soloPendientes = new AlternadorTk(() => Idiomas.Texto("Investigacion.SoloFalta"),
				() => _soloFaltantes, valor => { _soloFaltantes = valor; ReconstruirObjetos(); });
			_soloPendientes.Width.Set(220f, 0f);
			_soloPendientes.Height.Set(28f, 0f);
			_soloPendientes.VAlign = 1f;
			_soloPendientes.Left.Set(0f, 0f);
			_soloPendientes.Ayuda = () => Idiomas.Texto("Investigacion.SoloFaltaAyuda");
			Append(_soloPendientes);

			_botonTodo = new BotonTk(Idiomas.Texto("Investigacion.InvestigarTodo"), 0.8f);
			_botonTodo.Width.Set(200f, 0f);
			_botonTodo.Height.Set(32f, 0f);
			_botonTodo.VAlign = 1f;
			_botonTodo.Left.Set(240f, 0f);
			_botonTodo.Ayuda = () => Idiomas.Texto("Investigacion.InvestigarTodoAyuda");
			_botonTodo.AlPulsar += () => Confirmar(_botonTodo, InvestigarTodo);
			Append(_botonTodo);

			_botonQuitarTodo = new BotonTk(Idiomas.Texto("Investigacion.QuitarTodo"), 0.8f);
			_botonQuitarTodo.Width.Set(280f, 0f);
			_botonQuitarTodo.Height.Set(32f, 0f);
			_botonQuitarTodo.VAlign = 1f;
			_botonQuitarTodo.Left.Set(452f, 0f);
			_botonQuitarTodo.Ayuda = () => Idiomas.Texto("Investigacion.QuitarTodoAyuda");
			_botonQuitarTodo.AlPulsar += () => Confirmar(_botonQuitarTodo, QuitarTodo);
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
			return Idiomas.Texto("Investigacion.AvisoNoViaje",
				Main.LocalPlayer != null ? Main.LocalPlayer.difficulty.ToString() : "?");
		}

		private static string TextoProgreso()
		{
			int hechos = EstadoInvestigacion.TotalCompletos;
			int total = EstadoInvestigacion.TotalInvestigable;
			int porcentaje = total > 0 ? (int)Math.Round(hechos * 100.0 / total) : 0;
			return Idiomas.Texto("Investigacion.Progreso", hechos, total, porcentaje);
		}

		/// <summary>Ancho fijo (no depende de la resolucion) de <see cref="_tituloObjetos"/>: el
		/// hueco que queda a la izquierda de los dos botones de la derecha. Se guarda aparte porque
		/// <see cref="AjustarAlturaTitulo"/> necesita el mismo numero para medir el alto real.</summary>
		private const float AnchoTitulo = 200f;
		private const float EscalaTitulo = 0.8f;

		/// <summary>Alto real de <see cref="_tituloObjetos"/> ahora mismo. Ver
		/// <see cref="AjustarAlturaTitulo"/>.</summary>
		private float _altoTitulo = 22f;

		/// <summary>
		/// Nombre de la carpeta ENVUELTO al ancho fijo de la etiqueta (nunca recortado con "..."
		/// como antes - el codigo viejo cortaba a 22 CARACTERES exactos, sin medir con la fuente
		/// real: una carpeta con un nombre normal en ingles ya se veia truncada). Ver
		/// <see cref="AjustarAlturaTitulo"/> para el alto real que le hace falta.
		/// </summary>
		private string TextoTituloCarpeta()
		{
			if (_seleccionada == null) {
				return Idiomas.Texto("Investigacion.EligeCarpeta");
			}
			string texto = _seleccionada.Nombre + "  " + _seleccionada.Hechos + "/" + _seleccionada.Total;
			return EtiquetaTk.PartirEnLineas(texto, AnchoTitulo, EscalaTitulo);
		}

		/// <summary>
		/// Mide el alto REAL de <see cref="TextoTituloCarpeta"/> ya envuelto y empuja
		/// <see cref="_listaObjetos"/>/<see cref="_scrollObjetos"/> hacia abajo lo que haga falta -
		/// mismo patron que <c>ContenidoBuilds.RecalcularCabecera</c>. Se llama cada fotograma (ver
		/// <see cref="Update"/>) porque el nombre de la carpeta seleccionada cambia con cada clic.
		/// </summary>
		private void AjustarAlturaTitulo()
		{
			string partido = TextoTituloCarpeta();
			float altoTexto = Terraria.GameContent.FontAssets.MouseText.Value
				.MeasureString(partido).Y * EscalaTitulo;
			float altoNuevo = altoTexto > 22f ? altoTexto : 22f;

			if (Math.Abs(altoNuevo - _altoTitulo) < 0.5f) {
				return;
			}
			_altoTitulo = altoNuevo;

			_tituloObjetos.Height.Set(_altoTitulo, 0f);

			const float topBase = 34f;
			const float topTitulo = 2f;
			const float separacion = 4f;
			float topLista = topTitulo + _altoTitulo + separacion;
			if (topLista < topBase) {
				topLista = topBase;
			}

			_listaObjetos.Top.Set(topLista, 0f);
			_listaObjetos.Height.Set(-topLista, 1f);
			_scrollObjetos.Top.Set(topLista, 0f);
			_scrollObjetos.Height.Set(-topLista, 1f);

			// Sin esto, GetDimensions() de _listaObjetos/_scrollObjetos seguiria devolviendo el
			// hueco viejo hasta que algo mas disparara un Recalculate por su cuenta - mismo bug
			// real que se encontro en ContenidoBuilds.RecalcularPildorasYFilas (ver su XMLdoc):
			// Top.Set()/Height.Set() no mueve nada visible por si solo.
			Recalculate();
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

		/// <summary>
		/// Mide cada fila del arbol de carpetas (<see cref="FilaCarpetaInvestigacion.ActualizarLayout"/>)
		/// y ajusta su alto real - mismo patron que <c>PestanaBuffs.AjustarAltoFilasActivas</c>: la
		/// fila crece cuando el nombre necesita envolverse, nunca se recorta. Solo se llama a
		/// <c>Recalculate()</c> en la lista si de verdad cambio algo, para no rehacer el layout de
		/// las 200+ filas que puede tener el arbol completo en cada fotograma sin necesidad.
		/// </summary>
		private void AjustarAltoFilasCarpeta()
		{
			bool cambio = false;
			for (int i = 0; i < _filasCarpeta.Count; i++) {
				FilaCarpetaInvestigacion fila = _filasCarpeta[i];
				float altoNecesario = fila.ActualizarLayout();
				if (Math.Abs(fila.Height.Pixels - altoNecesario) > 0.5f) {
					fila.Height.Set(altoNecesario, 0f);
					cambio = true;
				}
			}

			if (cambio) {
				_listaCarpetas.Recalculate();
			}
		}

		/// <summary>
		/// Mide cada fila de objeto (<see cref="FilaObjetoInvestigacion.ActualizarLayout"/>) y
		/// ajusta su alto real - mismo patron que <see cref="AjustarAltoFilasCarpeta"/> y que
		/// <c>PestanaBuffs.AjustarAltoFilasResultado</c>: el nombre se envuelve, la fila crece,
		/// nunca se recorta.
		/// </summary>
		private void AjustarAltoFilasObjeto()
		{
			bool cambio = false;
			for (int i = 0; i < _filasObjeto.Count; i++) {
				FilaObjetoInvestigacion fila = _filasObjeto[i];
				float altoNecesario = fila.ActualizarLayout();
				if (Math.Abs(fila.Height.Pixels - altoNecesario) > 0.5f) {
					fila.Height.Set(altoNecesario, 0f);
					cambio = true;
				}
			}

			if (cambio) {
				_listaObjetos.Recalculate();
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
			_filasObjeto.Clear();
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
				_filasObjeto.Add(fila);
				puestos++;
			}

			int sinListar = Math.Max(0, tipos.Length - escondidos - puestos);
			if (sinListar > 0) {
				EtiquetaTk pie = new EtiquetaTk(
					() => Idiomas.Texto("Investigacion.MasObjetosDentro", sinListar), 0.72f, 500f, 22f);
				pie.ColorTexto = EstiloTk.TextoSuave;
				_listaObjetos.Add(pie);
			}
			else if (puestos == 0) {
				EtiquetaTk vacio = new EtiquetaTk(
					() => _soloFaltantes
						? Idiomas.Texto("Investigacion.NadaPendiente")
						: Idiomas.Texto("Investigacion.SinInvestigables"),
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
				? EstadoInvestigacion.Investigar(Idiomas.Texto("Investigacion.AccionInvestigar", nombre), uno)
				: EstadoInvestigacion.Quitar(Idiomas.Texto("Investigacion.AccionQuitar", nombre), uno);

			Anunciar(Idiomas.Texto(
				investigar ? "Investigacion.MensajeInvestigado" : "Investigacion.MensajeSinInvestigar",
				nombre, EstadoInvestigacion.Describir(tipo)), resultado);
		}

		// ------------------------------------------------------------------ acciones

		/// <summary>Investiga del todo la carpeta abierta (y todo lo que cuelgue de ella).</summary>
		public void InvestigarCarpeta()
		{
			if (_seleccionada == null) {
				return;
			}
			ResultadoInvestigacion resultado = EstadoInvestigacion.Investigar(
				Idiomas.Texto("Investigacion.AccionInvestigarCarpeta", _seleccionada.Nombre),
				_seleccionada.Tipos);
			Anunciar(Idiomas.Texto("Investigacion.MensajeCarpeta",
				_seleccionada.Nombre, resultado.Resumen), resultado);
		}

		/// <summary>Quita la investigacion de toda la carpeta abierta.</summary>
		public void QuitarCarpeta()
		{
			if (_seleccionada == null) {
				return;
			}
			ResultadoInvestigacion resultado = EstadoInvestigacion.Quitar(
				Idiomas.Texto("Investigacion.AccionQuitarCarpeta", _seleccionada.Nombre),
				_seleccionada.Tipos);
			Anunciar(Idiomas.Texto("Investigacion.MensajeCarpetaQuitada",
				_seleccionada.Nombre, resultado.Resumen), resultado);
		}

		/// <summary>Investiga TODO lo investigable de la partida.</summary>
		public void InvestigarTodo()
		{
			List<int> todos = new List<int>(EstadoInvestigacion.TiposInvestigables);
			ResultadoInvestigacion resultado = EstadoInvestigacion.Investigar(
				Idiomas.Texto("Investigacion.AccionInvestigarTodo"), todos);
			Anunciar(Idiomas.Texto("Investigacion.MensajeTodo", resultado.Resumen), resultado);
		}

		/// <summary>Deja el personaje sin nada investigado.</summary>
		public void QuitarTodo()
		{
			ResultadoInvestigacion resultado = EstadoInvestigacion.QuitarTodo(
				Idiomas.Texto("Investigacion.AccionQuitarTodo"));
			Anunciar(Idiomas.Texto("Investigacion.MensajeTodoQuitado", resultado.Resumen), resultado);
		}

		/// <summary>
		/// Confirmacion en dos pasos de las acciones globales: el primer clic cambia el texto del
		/// boton y espera; el segundo, dentro de unos segundos, ejecuta. Un solo clic no puede
		/// rehacer miles de objetos de golpe, por muy deshacible que sea despues.
		/// </summary>
		private void Confirmar(BotonTk boton, Action accion)
		{
			if (_pendienteDeConfirmar == boton) {
				_pendienteDeConfirmar = null;
				boton.FijarTexto(TextoNormalDe(boton));
				accion();
				return;
			}

			CancelarConfirmacion();
			_pendienteDeConfirmar = boton;
			_fotogramasConfirmacion = FotogramasDeConfirmacion;
			boton.FijarTexto(Idiomas.Texto("Investigacion.Confirmar"));
			boton.Activo = true;
		}

		/// <summary>Rotulo normal (sin confirmacion pendiente) de los dos botones globales.</summary>
		private string TextoNormalDe(BotonTk boton)
		{
			return Idiomas.Texto(boton == _botonTodo
				? "Investigacion.InvestigarTodo"
				: "Investigacion.QuitarTodo");
		}

		private void CancelarConfirmacion()
		{
			if (_pendienteDeConfirmar == null) {
				return;
			}
			_pendienteDeConfirmar.Activo = false;
			_pendienteDeConfirmar.FijarTexto(TextoNormalDe(_pendienteDeConfirmar));
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
				Main.NewText(Idiomas.Texto("Investigacion.Chat", texto), EstiloInvestigacion.Hecho);
			}

			CatalogoInvestigacion.RefrescarContadores();
			ReconstruirObjetos();
		}

		// ------------------------------------------------------------------ ciclo de vida

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// El nombre de cada carpeta se envuelve segun el ancho REAL disponible (nunca se
			// recorta con "..."), que cambia con la resolucion de la ventana: se recalcula cada
			// fotograma, mismo patron que PestanaMundo.RecalcularAviso.
			AjustarAltoFilasCarpeta();
			AjustarAltoFilasObjeto();
			AjustarAlturaTitulo();

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
