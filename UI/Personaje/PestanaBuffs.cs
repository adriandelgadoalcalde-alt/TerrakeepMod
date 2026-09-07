using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;
using TerrasavrNative.Core.Data;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Pestaña "Buffs": los buffs activos del jugador y la forma de añadir o quitar cualquiera.
	/// <para />
	/// Es la unica pestaña con un problema de refresco de verdad, y estaba previsto en el plan:
	/// <b>los buffs caducan solos</b>. El juego hace <c>buffTime[i]--</c> cada tick y, cuando
	/// llega a cero, <c>DelBuff</c> DESPLAZA el resto del array hacia arriba. Es decir, no basta
	/// con leer el estado al abrir: los tiempos cambian 60 veces por segundo y los indices se
	/// mueven bajo los pies.
	/// <para />
	/// Se resuelve con dos medidas:
	/// <list type="bullet">
	/// <item>cada fila se identifica por el TIPO de buff, no por el indice; el tiempo se busca en
	/// cada dibujado con <c>Player.FindBuffIndex(tipo)</c>;</item>
	/// <item>en cada <c>Update</c> se compara una firma del array <c>buffType</c> y, si ha
	/// cambiado (ha caducado uno, o el juego ha metido otro), la lista se reconstruye sola.</item>
	/// </list>
	/// Para añadir buffs se usa la API oficial <c>Player.AddBuff</c> y no una escritura a pelo de
	/// los arrays: es la que respeta las inmunidades, los limites y los enganches de los mods.
	/// </summary>
	public class PestanaBuffs : UIElement
	{
		private const int SegundosPorDefecto = 600;

		/// <summary>
		/// Tope de resultados que se enseñan de una vez. Antes eran 12 y SIN ningun arbol de
		/// carpetas detras: con el buscador vacio, eso enseñaba siempre los primeros 12 buffs por
		/// id (los mas bajos de "Utilidad") y escondia el resto salvo que se supiera EXACTAMENTE
		/// que buscar - el bug real reportado ("no salen todos los buffs... igual que en
		/// Terrakeep"). Es el MISMO bug que ya se dio en la app de escritorio antes de su "Fase 2"
		/// del rework de Buffs (ver <c>ArbolBuffs</c>), y la correccion real tampoco fue subir este
		/// numero: fue añadir el arbol de <see cref="ArbolBuffs"/>. Se sube igualmente a 100 (mismo
		/// tope real que usa la Libreria de objetos, <c>BusquedaLibreria.Maximo</c>) para que una
		/// carpeta grande buscada a mano no se recorte de forma silenciosa.
		/// </summary>
		private const int MaximoResultados = 100;

		private const float AnchoColumnaCarpetas = 132f;
		private const float AnchoBarraScrollCarpetas = 20f;
		private const float SeparacionSubcolumnas = 8f;

		/// <summary>
		/// Reparto de las dos columnas, en PORCENTAJE del ancho real.
		/// <para />
		/// Antes eran pixeles fijos (cajas de 470 px y la columna derecha empezando en 490), o sea
		/// 960 px de ancho total. En la ventana de 800 con la que se prueba el mod eso dejaba la
		/// caja de resultados y la nota saliendose del marco por la derecha, y el campo "Segundos"
		/// directamente FUERA de la pantalla: no se veia. Visto en una captura real del juego.
		/// </summary>
		private const float FraccionColumna = 0.5f;

		private const float SeparacionColumnas = 10f;

		private UIList _listaActivos;
		private UIList _listaResultados;
		private CampoTextoTk _campoBusqueda;
		private CampoTextoTk _campoDuracion;
		private BotonTk _botonQuitarTodos;
		private readonly List<BotonTk> _botonesQuitar = new List<BotonTk>();
		private readonly List<BotonTk> _botonesAplicar = new List<BotonTk>();
		private int _firmaActivos = -1;

		// --- Arbol de carpetas de "Añadir" (ArbolBuffs) ----------------------------------------
		private readonly List<CategoryTreeNodeData> _ruta = new List<CategoryTreeNodeData>();
		private UIList _listaCarpetas;
		private BotonTk _botonInicio;
		private BotonTk _botonSubirCarpeta;
		private EtiquetaTk _rutaTexto;
		private EtiquetaTk _resumenAnadir;
		private IReadOnlyList<CategoryTreeNodeData> _ultimasRaices;
		private string _busquedaActual = "";
		private int _totalCasados;
		private int _mostrados;

		public PestanaBuffs()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ArbolBuffs.ConstruirSiHaceFalta();
			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Buffs: arbol de \"Añadir\" listo. {ArbolBuffs.Resumen}");

			ConstruirActivos();
			ConstruirAnadir();

			ReconstruirActivos();
			_ultimasRaices = ArbolBuffs.Raices;
			RellenarCarpetas();
			ReconstruirResultados("");
		}

		// ---------------------------------------------------------------- buffs activos

		private void ConstruirActivos()
		{
			UIElement izquierda = new UIElement();
			izquierda.Width.Set(-SeparacionColumnas, FraccionColumna);
			izquierda.Height.Set(0f, 1f);
			Append(izquierda);

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Activos",
					PersonajeVivo.Jugador.CountBuffs(), PersonajeVivo.RanurasBuff),
				0.85f, 400f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(0f, 0f);
			izquierda.Append(titulo);

			UIPanel caja = new UIPanel();
			caja.Width.Set(0f, 1f);
			caja.Height.Set(-58f, 1f);
			caja.Top.Set(26f, 0f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			izquierda.Append(caja);

			_listaActivos = new UIList();
			_listaActivos.Width.Set(-24f, 1f);
			_listaActivos.Height.Set(0f, 1f);
			_listaActivos.ListPadding = 4f;
			caja.Append(_listaActivos);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Height.Set(0f, 1f);
			barra.SetView(100f, 1000f);
			caja.Append(barra);
			_listaActivos.SetScrollbar(barra);

			_botonQuitarTodos = new BotonTk(Idiomas.Texto("Personaje.Buffs.QuitarTodos"), 0.8f);
			BotonTk quitarTodos = _botonQuitarTodos;
			quitarTodos.Width.Set(150f, 0f);
			quitarTodos.Height.Set(28f, 0f);
			quitarTodos.Left.Set(0f, 0f);
			quitarTodos.VAlign = 1f;
			quitarTodos.AlPulsar += QuitarTodos;
			izquierda.Append(quitarTodos);
		}

		private void ReconstruirActivos()
		{
			Player jugador = PersonajeVivo.Jugador;
			_listaActivos.Clear();
			_botonesQuitar.Clear();

			for (int i = 0; i < jugador.buffType.Length; i++) {
				int tipo = jugador.buffType[i];
				if (tipo <= 0) {
					continue;
				}
				_listaActivos.Add(CrearFilaBuff(tipo));
			}

			_firmaActivos = FirmaActivos();
		}

		private UIElement CrearFilaBuff(int tipo)
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(36f, 0f);

			IconoBuffTk icono = new IconoBuffTk(() => tipo, 32f);
			icono.Left.Set(2f, 0f);
			icono.Top.Set(2f, 0f);
			fila.Append(icono);

			EtiquetaTk nombre = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.NombreConId",
					PersonajeVivo.NombreBuff(tipo), tipo), 0.8f, 0f, 20f);
			nombre.Width.Set(-186f, 1f);
			nombre.Left.Set(40f, 0f);
			nombre.Top.Set(8f, 0f);
			fila.Append(nombre);

			EtiquetaTk tiempo = new EtiquetaTk(() => TextoTiempo(tipo), 0.8f, 70f, 20f);
			tiempo.Left.Set(-146f, 1f);
			tiempo.Top.Set(8f, 0f);
			fila.Append(tiempo);

			BotonTk quitar = new BotonTk(Idiomas.Texto("Personaje.Buffs.Quitar"), 0.75f);
			_botonesQuitar.Add(quitar);
			quitar.Width.Set(70f, 0f);
			quitar.Height.Set(26f, 0f);
			quitar.Left.Set(-72f, 1f);
			quitar.Top.Set(4f, 0f);
			quitar.AlPulsar += () => QuitarBuff(tipo);
			fila.Append(quitar);

			return fila;
		}

		/// <summary>Tiempo restante, buscado por tipo en cada dibujado. Si el buff ya no esta,
		/// devuelve "-" y el <c>Update</c> reconstruira la lista en ese mismo fotograma.</summary>
		private static string TextoTiempo(int tipo)
		{
			Player jugador = PersonajeVivo.Jugador;
			int indice = jugador.FindBuffIndex(tipo);
			if (indice < 0) {
				return "-";
			}
			return PersonajeVivo.FormatearTiempoBuff(jugador.buffTime[indice]);
		}

		private void QuitarBuff(int tipo)
		{
			Player jugador = PersonajeVivo.Jugador;
			int indice = jugador.FindBuffIndex(tipo);
			jugador.ClearBuff(tipo);

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Buff quitado en vivo: \"{PersonajeVivo.NombreBuff(tipo)}\" (id {tipo}), " +
				$"estaba en buffType[{indice}]. FindBuffIndex despues = {jugador.FindBuffIndex(tipo)}.");

			ReconstruirActivos();
		}

		private void QuitarTodos()
		{
			Player jugador = PersonajeVivo.Jugador;
			int quitados = 0;

			for (int i = 0; i < jugador.buffType.Length; i++) {
				if (jugador.buffType[i] > 0) {
					jugador.DelBuff(i);
					quitados++;
					// DelBuff desplaza el resto del array hacia arriba, asi que hay que volver a
					// mirar la MISMA posicion en la siguiente vuelta.
					i--;
				}
			}

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Buffs quitados en vivo: {quitados}. " +
				$"CountBuffs despues = {jugador.CountBuffs()}.");

			ReconstruirActivos();
		}

		// ---------------------------------------------------------------- añadir buffs

		private void ConstruirAnadir()
		{
			UIElement derecha = new UIElement();
			derecha.Width.Set(0f, 1f - FraccionColumna);
			derecha.Height.Set(0f, 1f);
			derecha.HAlign = 1f;
			Append(derecha);

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Anadir"), 0.85f, 300f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(0f, 0f);
			derecha.Append(titulo);

			// --- Columna de carpetas (ArbolBuffs), a la izquierda de "derecha" -----------------
			UIElement columnaCarpetas = new UIElement();
			columnaCarpetas.Width.Set(AnchoColumnaCarpetas, 0f);
			columnaCarpetas.Top.Set(26f, 0f);
			columnaCarpetas.Height.Set(-26f, 1f);
			derecha.Append(columnaCarpetas);

			_botonInicio = new BotonTk(Idiomas.Texto("Personaje.Buffs.Arbol.Inicio"), 0.72f);
			_botonInicio.Width.Set(62f, 0f);
			_botonInicio.Height.Set(24f, 0f);
			_botonInicio.Ayuda = () => Idiomas.Texto("Personaje.Buffs.Arbol.InicioAyuda");
			_botonInicio.AlPulsar += IrALaRaiz;
			columnaCarpetas.Append(_botonInicio);

			_botonSubirCarpeta = new BotonTk(Idiomas.Texto("Personaje.Buffs.Arbol.Subir"), 0.72f);
			_botonSubirCarpeta.Width.Set(66f, 0f);
			_botonSubirCarpeta.Height.Set(24f, 0f);
			_botonSubirCarpeta.Left.Set(66f, 0f);
			_botonSubirCarpeta.Ayuda = () => Idiomas.Texto("Personaje.Buffs.Arbol.SubirAyuda");
			_botonSubirCarpeta.AlPulsar += SubirCarpeta;
			columnaCarpetas.Append(_botonSubirCarpeta);

			_rutaTexto = new EtiquetaTk(RutaCorta, 0.68f, AnchoColumnaCarpetas, 18f);
			_rutaTexto.ColorTexto = EstiloTk.TextoSuave;
			_rutaTexto.Top.Set(28f, 0f);
			columnaCarpetas.Append(_rutaTexto);

			UIPanel cajaCarpetas = new UIPanel();
			cajaCarpetas.Width.Set(0f, 1f);
			cajaCarpetas.Top.Set(50f, 0f);
			cajaCarpetas.Height.Set(-50f, 1f);
			cajaCarpetas.BackgroundColor = EstiloTk.FondoCaja;
			columnaCarpetas.Append(cajaCarpetas);

			_listaCarpetas = new UIList();
			_listaCarpetas.Width.Set(-AnchoBarraScrollCarpetas, 1f);
			_listaCarpetas.Height.Set(0f, 1f);
			_listaCarpetas.ListPadding = 3f;
			cajaCarpetas.Append(_listaCarpetas);

			UIScrollbar barraCarpetas = new UIScrollbar();
			barraCarpetas.HAlign = 1f;
			barraCarpetas.Height.Set(0f, 1f);
			barraCarpetas.SetView(100f, 1000f);
			cajaCarpetas.Append(barraCarpetas);
			_listaCarpetas.SetScrollbar(barraCarpetas);

			// --- Columna de busqueda + resultados, a la derecha de la de carpetas --------------
			UIElement columnaResultados = new UIElement();
			columnaResultados.Left.Set(AnchoColumnaCarpetas + SeparacionSubcolumnas, 0f);
			columnaResultados.Width.Set(-(AnchoColumnaCarpetas + SeparacionSubcolumnas), 1f);
			columnaResultados.Top.Set(26f, 0f);
			columnaResultados.Height.Set(-26f, 1f);
			derecha.Append(columnaResultados);

			EtiquetaTk etiquetaBusqueda = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Buscar"), 0.8f, 70f, 20f);
			etiquetaBusqueda.ColorTexto = EstiloTk.TextoSuave;
			etiquetaBusqueda.Left.Set(0f, 0f);
			etiquetaBusqueda.Top.Set(6f, 0f);
			columnaResultados.Append(etiquetaBusqueda);

			_campoBusqueda = new CampoTextoTk(() => Idiomas.Texto("Personaje.Buffs.PistaBusqueda"), 30);
			_campoBusqueda.Width.Set(-72f, 1f);
			_campoBusqueda.Height.Set(28f, 0f);
			_campoBusqueda.Left.Set(66f, 0f);
			_campoBusqueda.Top.Set(0f, 0f);
			_campoBusqueda.AlCambiar += ReconstruirResultados;
			columnaResultados.Append(_campoBusqueda);

			// La duracion va en su PROPIA fila, debajo del buscador: en la misma fila necesitaba
			// 470 px de ancho y con media pantalla no cabia (el campo se quedaba fuera de la
			// ventana, literalmente invisible).
			EtiquetaTk etiquetaDuracion = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Segundos"), 0.8f, 90f, 20f);
			etiquetaDuracion.ColorTexto = EstiloTk.TextoSuave;
			etiquetaDuracion.Left.Set(0f, 0f);
			etiquetaDuracion.Top.Set(40f, 0f);
			columnaResultados.Append(etiquetaDuracion);

			_campoDuracion = new CampoTextoTk(() => SegundosPorDefecto.ToString(), 6);
			_campoDuracion.SoloNumeros = true;
			_campoDuracion.FijarTextoSilencioso(SegundosPorDefecto.ToString());
			_campoDuracion.Width.Set(80f, 0f);
			_campoDuracion.Height.Set(28f, 0f);
			_campoDuracion.Left.Set(80f, 0f);
			_campoDuracion.Top.Set(34f, 0f);
			columnaResultados.Append(_campoDuracion);

			_resumenAnadir = new EtiquetaTk(TextoResumenAnadir, 0.68f, 0f, 30f);
			_resumenAnadir.Width.Set(0f, 1f);
			_resumenAnadir.ColorTexto = EstiloTk.TextoSuave;
			_resumenAnadir.Left.Set(0f, 0f);
			_resumenAnadir.Top.Set(66f, 0f);
			columnaResultados.Append(_resumenAnadir);

			UIPanel caja = new UIPanel();
			caja.Width.Set(0f, 1f);
			caja.Height.Set(-152f, 1f);
			caja.Left.Set(0f, 0f);
			caja.Top.Set(94f, 0f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			columnaResultados.Append(caja);

			_listaResultados = new UIList();
			_listaResultados.Width.Set(-24f, 1f);
			_listaResultados.Height.Set(0f, 1f);
			_listaResultados.ListPadding = 4f;
			caja.Append(_listaResultados);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Height.Set(0f, 1f);
			barra.SetView(100f, 1000f);
			caja.Append(barra);
			_listaResultados.SetScrollbar(barra);

			EtiquetaTk nota = new EtiquetaTk(
				() => EtiquetaTk.PartirEnLineas(Idiomas.Texto("Personaje.Buffs.Nota"),
					columnaResultados.GetInnerDimensions().Width, 0.72f),
				0.72f, 0f, 40f);
			nota.Width.Set(0f, 1f);
			nota.ColorTexto = EstiloTk.TextoSuave;
			nota.Left.Set(0f, 0f);
			nota.Top.Set(-44f, 1f);
			columnaResultados.Append(nota);
		}

		// ---------------------------------------------------------------- arbol de carpetas

		/// <summary>Carpeta abierta ahora mismo en el arbol de "Añadir", o null si estamos en la
		/// raiz (las 8 carpetas de primer nivel de <see cref="ArbolBuffs"/>).</summary>
		private CategoryTreeNodeData CarpetaActual {
			get { return _ruta.Count > 0 ? _ruta[_ruta.Count - 1] : null; }
		}

		private IReadOnlyList<CategoryTreeNodeData> CarpetasVisibles()
		{
			CategoryTreeNodeData actual = CarpetaActual;
			if (actual == null) {
				return ArbolBuffs.Raices;
			}
			return actual.Children ?? (IReadOnlyList<CategoryTreeNodeData>)new List<CategoryTreeNodeData>();
		}

		private void RellenarCarpetas()
		{
			_listaCarpetas.Clear();

			IReadOnlyList<CategoryTreeNodeData> carpetas = CarpetasVisibles();
			for (int i = 0; i < carpetas.Count; i++) {
				CategoryTreeNodeData nodo = carpetas[i];
				FilaCarpetaBuffTk fila = new FilaCarpetaBuffTk(nodo, AnchoColumnaCarpetas - AnchoBarraScrollCarpetas - 4f);
				fila.AlPulsar += () => AbrirCarpeta(nodo);
				_listaCarpetas.Add(fila);
			}

			if (carpetas.Count == 0) {
				EtiquetaTk vacio = new EtiquetaTk(
					() => Idiomas.Texto("Personaje.Buffs.Arbol.SinSubcarpetas"), 0.7f,
					AnchoColumnaCarpetas - AnchoBarraScrollCarpetas - 4f, 40f);
				vacio.ColorTexto = EstiloTk.TextoSuave;
				_listaCarpetas.Add(vacio);
			}

			_botonSubirCarpeta.Habilitado = _ruta.Count > 0;
			_botonInicio.Habilitado = _ruta.Count > 0;
			_listaCarpetas.Recalculate();
		}

		private void AbrirCarpeta(CategoryTreeNodeData nodo)
		{
			if (nodo == null) {
				return;
			}
			_ruta.Add(nodo);
			RellenarCarpetas();
			ReconstruirResultados(_busquedaActual);
		}

		private void SubirCarpeta()
		{
			if (_ruta.Count == 0) {
				return;
			}
			_ruta.RemoveAt(_ruta.Count - 1);
			RellenarCarpetas();
			ReconstruirResultados(_busquedaActual);
		}

		private void IrALaRaiz()
		{
			_ruta.Clear();
			RellenarCarpetas();
			ReconstruirResultados(_busquedaActual);
		}

		private string RutaCorta()
		{
			StringBuilder sb = new StringBuilder(Idiomas.Texto("Personaje.Buffs.Arbol.Raiz"));
			for (int i = 0; i < _ruta.Count; i++) {
				sb.Append(" > ").Append(_ruta[i].Name);
			}
			string ruta = sb.ToString();
			return ruta.Length <= 40 ? ruta : "..." + ruta.Substring(ruta.Length - 37);
		}

		// ---------------------------------------------------------------- resultados

		/// <summary>
		/// Reconstruye la rejilla de resultados de "Añadir". El AMBITO replica el de la Libreria
		/// de objetos (<c>BusquedaLibreria.Buscar</c>): con una carpeta abierta se busca DENTRO de
		/// ella; sin carpeta abierta, solo si hay texto en el buscador se recorren TODOS los buffs
		/// aplicables del juego cargado (<c>BuffLoader.BuffCount</c>, vanilla + cualquier mod). Sin
		/// carpeta y sin busqueda no se enseña nada: es la pantalla de entrada, las carpetas estan
		/// a la izquierda.
		/// </summary>
		private void ReconstruirResultados(string filtro)
		{
			_busquedaActual = filtro ?? "";
			_listaResultados.Clear();
			_botonesAplicar.Clear();

			string busqueda = _busquedaActual.Trim();
			int idPedido;
			bool esNumero = int.TryParse(busqueda, out idPedido);
			bool hayBusqueda = busqueda.Length > 0;
			CategoryTreeNodeData carpeta = CarpetaActual;

			_totalCasados = 0;
			_mostrados = 0;

			if (carpeta == null && !hayBusqueda) {
				EtiquetaTk vacio = new EtiquetaTk(
					() => Idiomas.Texto("Personaje.Buffs.Arbol.ElegirCarpeta", ArbolBuffs.TotalBuffsAplicables),
					0.75f, 0f, 40f);
				vacio.Width.Set(0f, 1f);
				vacio.ColorTexto = EstiloTk.TextoSuave;
				_listaResultados.Add(vacio);
				return;
			}

			if (carpeta != null) {
				IReadOnlyList<int> ids = carpeta.ItemIdsOrdered;
				for (int i = 0; i < ids.Count; i++) {
					AcumularSiCoincide(ids[i], busqueda, esNumero, idPedido);
				}
			}
			else {
				for (int tipo = 1; tipo < BuffLoader.BuffCount; tipo++) {
					AcumularSiCoincide(tipo, busqueda, esNumero, idPedido);
				}
			}

			if (_mostrados == 0) {
				EtiquetaTk vacio = new EtiquetaTk(
					() => Idiomas.Texto("Personaje.Buffs.SinResultados"), 0.8f, 300f, 24f);
				vacio.ColorTexto = EstiloTk.TextoSuave;
				_listaResultados.Add(vacio);
			}
		}

		private void AcumularSiCoincide(int tipo, string busqueda, bool esNumero, int idPedido)
		{
			string nombre = PersonajeVivo.NombreBuff(tipo);
			if (string.IsNullOrEmpty(nombre)) {
				return;
			}

			bool coincide = busqueda.Length == 0
				|| (esNumero && tipo == idPedido)
				|| nombre.IndexOf(busqueda, StringComparison.OrdinalIgnoreCase) >= 0;
			if (!coincide) {
				return;
			}

			_totalCasados++;
			if (_mostrados < MaximoResultados) {
				_listaResultados.Add(CrearFilaResultado(tipo, nombre));
				_mostrados++;
			}
		}

		/// <summary>Texto sobre la rejilla de resultados: donde se busca y cuantos hay, para que un
		/// recorte por <see cref="MaximoResultados"/> sea VISIBLE en vez de silencioso (el bug real
		/// que se corrige aqui era precisamente eso: un recorte que no se notaba).</summary>
		private string TextoResumenAnadir()
		{
			if (!ArbolBuffs.Listo) {
				return Idiomas.Texto("Personaje.Buffs.Arbol.Cargando");
			}

			CategoryTreeNodeData carpeta = CarpetaActual;
			string donde = carpeta != null
				? Idiomas.Texto("Personaje.Buffs.Arbol.EnCarpeta", carpeta.Name)
				: Idiomas.Texto("Personaje.Buffs.Arbol.EnTodo");

			if (_mostrados == 0) {
				return carpeta == null && _busquedaActual.Trim().Length == 0
					? Idiomas.Texto("Personaje.Buffs.Arbol.ElegirCarpeta", ArbolBuffs.TotalBuffsAplicables)
					: Idiomas.Texto("Personaje.Buffs.SinResultados");
			}

			return _totalCasados > _mostrados
				? Idiomas.Texto("Personaje.Buffs.Arbol.MostrandoParcial", _mostrados, _totalCasados, donde)
				: Idiomas.Texto("Personaje.Buffs.Arbol.MostrandoTodos", _mostrados, donde);
		}

		private UIElement CrearFilaResultado(int tipo, string nombre)
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(36f, 0f);

			IconoBuffTk icono = new IconoBuffTk(() => tipo, 32f);
			icono.Left.Set(2f, 0f);
			icono.Top.Set(2f, 0f);
			fila.Append(icono);

			EtiquetaTk etiqueta = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.NombreConId", nombre, tipo), 0.8f, 0f, 20f);
			etiqueta.Width.Set(-128f, 1f);
			etiqueta.Left.Set(40f, 0f);
			etiqueta.Top.Set(8f, 0f);
			fila.Append(etiqueta);

			BotonTk anadir = new BotonTk(Idiomas.Texto("Personaje.Buffs.Aplicar"), 0.75f);
			_botonesAplicar.Add(anadir);
			anadir.Width.Set(80f, 0f);
			anadir.Height.Set(26f, 0f);
			anadir.Left.Set(-82f, 1f);
			anadir.Top.Set(4f, 0f);
			anadir.AlPulsar += () => AplicarBuff(tipo);
			fila.Append(anadir);

			return fila;
		}

		private void AplicarBuff(int tipo)
		{
			Player jugador = PersonajeVivo.Jugador;
			int segundos = PersonajeVivo.Acotar(_campoDuracion.ComoEntero(SegundosPorDefecto), 1, 99999);
			int ticks = segundos * 60;

			jugador.AddBuff(tipo, ticks);

			int indice = jugador.FindBuffIndex(tipo);
			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Buff aplicado en vivo con Player.AddBuff: " +
				$"\"{PersonajeVivo.NombreBuff(tipo)}\" (id {tipo}) durante {segundos} s ({ticks} ticks). " +
				$"Resultado real: buffType[{indice}]=" +
				(indice >= 0 ? jugador.buffType[indice].ToString() : "(no puesto)") +
				", buffTime[" + indice + "]=" +
				(indice >= 0 ? jugador.buffTime[indice].ToString() : "-") + ".");

			ReconstruirActivos();
		}

		// ---------------------------------------------------------------- refresco en vivo

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Los buffs caducan solos: si el conjunto de tipos activos ha cambiado desde el
			// ultimo fotograma, la lista de la izquierda se rehace sola.
			if (FirmaActivos() != _firmaActivos) {
				ReconstruirActivos();
			}

			// Los rotulos de los botones se fijan al construirlos, asi que hay que volver a
			// ponerlos para que cambien en vivo con el selector de idioma del area de Ajustes.
			if (_botonQuitarTodos != null) {
				_botonQuitarTodos.FijarTexto(Idiomas.Texto("Personaje.Buffs.QuitarTodos"));
			}
			for (int i = 0; i < _botonesQuitar.Count; i++) {
				_botonesQuitar[i].FijarTexto(Idiomas.Texto("Personaje.Buffs.Quitar"));
			}
			for (int i = 0; i < _botonesAplicar.Count; i++) {
				_botonesAplicar[i].FijarTexto(Idiomas.Texto("Personaje.Buffs.Aplicar"));
			}
			if (_botonInicio != null) {
				_botonInicio.FijarTexto(Idiomas.Texto("Personaje.Buffs.Arbol.Inicio"));
				_botonSubirCarpeta.FijarTexto(Idiomas.Texto("Personaje.Buffs.Arbol.Subir"));
			}

			// El arbol de "Añadir" (ArbolBuffs) se reconstruye solo si el idioma activo cambio
			// desde la ultima vez (ConstruirSiHaceFalta se lo salta si no hizo falta): se detecta
			// comparando la REFERENCIA de la lista de raices, no releyendo el idioma aqui.
			ArbolBuffs.ConstruirSiHaceFalta();
			if (!ReferenceEquals(ArbolBuffs.Raices, _ultimasRaices)) {
				_ultimasRaices = ArbolBuffs.Raices;
				_ruta.Clear();
				RellenarCarpetas();
				ReconstruirResultados(_busquedaActual);
			}
		}

		private static int FirmaActivos()
		{
			int[] tipos = PersonajeVivo.Jugador.buffType;
			int firma = 17;
			for (int i = 0; i < tipos.Length; i++) {
				firma = firma * 31 + tipos[i];
			}
			return firma;
		}
	}
}
