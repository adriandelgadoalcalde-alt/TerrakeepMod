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
		/// Alto de fila de "buff activo"/"resultado de añadir", en pixeles. Antes eran 36 con el
		/// icono, el nombre y el boton practicamente pegados unos a otros (0-4 px de hueco real
		/// entre cajas, sin margen visible) - "esta todo super apretado en buff" (reporte real del
		/// usuario probando el mod, con captura). Subido a 40 para que quepa un margen vertical de
		/// verdad alrededor del icono/texto/boton.
		/// </summary>
		private const float AltoFilaBuff = 40f;

		/// <summary>Alto de una linea de texto a escala 0.8 (nombre/tiempo), ya usado como el alto
		/// de caja de una etiqueta de una sola linea antes de este arreglo.</summary>
		private const float AltoLineaTexto = 20f;

		/// <summary>Alto de la segunda linea de una fila (tiempo + boton "Quitar"/"Aplicar"): el
		/// boton manda, 26px de alto real.</summary>
		private const float AltoLineaSegunda = 26f;

		/// <summary>Margen por encima del nombre (linea 1) y por debajo de la segunda linea, en
		/// cada fila de "activos"/"resultados".</summary>
		private const float MargenSuperiorFila = 4f;
		private const float MargenInferiorFila = 4f;

		/// <summary>Hueco vertical real entre el nombre (linea 1, tantas lineas como haga falta) y
		/// la segunda linea (tiempo/boton) de una fila.</summary>
		private const float SeparacionEntreLineas = 4f;

		/// <summary>Hueco horizontal real entre el nombre y el tiempo, y entre el tiempo y el boton
		/// de "Quitar"/"Aplicar", en la fila de un buff. Antes 0 y 4 px respectivamente.</summary>
		private const float SeparacionEnFila = 10f;

		/// <summary>Margen entre el borde derecho de la fila y el boton "Quitar"/"Aplicar". Antes
		/// 2 px (practicamente pegado al borde).</summary>
		private const float MargenDerechoFila = 4f;

		/// <summary>Hueco entre la caja de la lista de "buffs activos" y el boton "Quitar todos" de
		/// debajo. Antes 4 px.</summary>
		private const float SeparacionListaActivosBoton = 16f;

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

		// --- Reparto de columnas en vivo (izquierda "Activos" / derecha "Añadir") --------------
		private UIElement _izquierda;
		private UIElement _derecha;
		private EtiquetaTk _tituloActivos;
		private UIPanel _cajaActivos;

		/// <summary>Ancho minimo de seguridad de la columna de "Activos": lo bastante para que la
		/// segunda linea de una fila (tiempo + boton "Quitar", ver <see cref="CrearFilaBuff"/>)
		/// quepa siempre con margen, aunque <see cref="AnchoMaximoActivos"/> saliera raro. El
		/// nombre YA NO necesita reserva aqui: vive en su propia linea y se envuelve solo si hace
		/// falta (nunca se recorta), asi que una columna estrecha solo hace la fila mas ALTA, no
		/// rompe nada.</summary>
		private const float AnchoMinimoActivos = 260f;

		private static float _anchoTiempoReal = -1f;
		private static float _anchoMaximoActivos = -1f;

		/// <summary>
		/// SOLO PARA AUTOPRUEBAS: una entrada por fila ya construida de "buffs activos", para que
		/// el arnes de espaciado (<c>AutopruebaEspaciado</c>) pueda medir las cajas reales sin
		/// tener que recorrer el arbol de UI a mano. Se rehace entera en cada
		/// <see cref="ReconstruirActivos"/>.
		/// </summary>
		public readonly List<(UIElement Fila, EtiquetaTk Nombre, EtiquetaTk Tiempo, BotonTk Quitar)>
			FilasActivasParaPrueba = new List<(UIElement, EtiquetaTk, EtiquetaTk, BotonTk)>();

		/// <summary>SOLO PARA AUTOPRUEBAS: una entrada por fila ya construida de "Añadir un buff"
		/// (resultados de la busqueda/carpeta), mismo proposito que
		/// <see cref="FilasActivasParaPrueba"/>. Se rehace entera en cada
		/// <see cref="ReconstruirResultados"/>.</summary>
		public readonly List<(UIElement Fila, EtiquetaTk Nombre, BotonTk Aplicar)>
			FilasResultadoParaPrueba = new List<(UIElement, EtiquetaTk, BotonTk)>();

		/// <summary>SOLO PARA AUTOPRUEBAS: dispara una busqueda como si el usuario hubiera escrito
		/// en el campo, para poder medir filas de resultados reales sin simular teclas.</summary>
		/// <summary>
		/// SOLO PARA AUTOPRUEBAS: dispara la busqueda y de paso fuerza YA MISMO (sin esperar al
		/// siguiente <c>Update</c>) el ajuste de alto de las filas nuevas - si no, medirlas en el
		/// mismo fotograma en que se crean encontraria el boton "Aplicar" todavia en su posicion
		/// de partida (Top=0, superpuesto con el nombre), porque <see cref="AjustarAltoFilasResultado"/>
		/// solo corre desde <c>Update</c>. En el juego real esto no se nota (es cuestion de UN
		/// fotograma, 1/60 s), pero una autoprueba que mide en el instante exacto de construir la
		/// fila si lo necesita.
		/// </summary>
		public void BuscarParaPrueba(string filtro)
		{
			ReconstruirResultados(filtro);
			AjustarAltoFilasResultado();
		}

		/// <summary>SOLO PARA AUTOPRUEBAS: la columna izquierda ("Activos"), para medir su ancho
		/// real ya recalculado por <see cref="RecalcularColumnas"/>.</summary>
		public UIElement ColumnaActivos => _izquierda;

		/// <summary>SOLO PARA AUTOPRUEBAS: la columna derecha ("Añadir"), idem.</summary>
		public UIElement ColumnaAnadir => _derecha;

		/// <summary>SOLO PARA AUTOPRUEBAS: el titulo de "Activos", para medir si de verdad ocupo
		/// mas de una linea.</summary>
		public EtiquetaTk TituloActivos => _tituloActivos;

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
			// El layout todavia no tiene dimensiones reales en el primerisimo fotograma (se
			// reintenta solo desde Update): sin esto la primera pasada usaria el reparto fijo de
			// FraccionColumna hasta el siguiente fotograma, un parpadeo real aunque breve.
			RecalcularColumnas();

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
			_izquierda = izquierda;

			// Ancho responsivo (100% de la columna) y no 400px fijos: con el reparto en vivo de
			// RecalcularColumnas la columna "Activos" puede acabar mas estrecha que eso en
			// ventanas anchas. NUNCA se recorta: si el contador algun dia necesitara mas de una
			// linea (texto largo o columna muy estrecha), se ENVUELVE de verdad
			// (EtiquetaTk.PartirEnLineas, la fuente real) y AjustarAlturaTitulo baja la caja de
			// abajo lo que haga falta - pedido explicito del usuario: el contenido se lee entero
			// SIEMPRE, es el layout el que se adapta, nunca el texto el que se sacrifica.
			_tituloActivos = new EtiquetaTk(() => {
				string texto = Idiomas.Texto("Personaje.Buffs.Activos",
					PersonajeVivo.Jugador.CountBuffs(), PersonajeVivo.RanurasBuff);
				float ancho = _tituloActivos.GetDimensions().Width;
				return ancho > 0f ? EtiquetaTk.PartirEnLineas(texto, ancho, 0.85f) : texto;
			}, 0.85f, 0f, 22f);
			_tituloActivos.Width.Set(0f, 1f);
			_tituloActivos.ColorTexto = EstiloTk.TextoSuave;
			_tituloActivos.Left.Set(0f, 0f);
			_tituloActivos.Top.Set(0f, 0f);
			izquierda.Append(_tituloActivos);

			_cajaActivos = new UIPanel();
			_cajaActivos.Width.Set(0f, 1f);
			// Top/Height de partida para el primerisimo fotograma (una sola linea de titulo);
			// AjustarAlturaTitulo los corrige cada fotograma con la altura REAL ya dibujada del
			// titulo, igual que PestanaMundo.RecalcularAviso hace con su recuadro naranja.
			_cajaActivos.Top.Set(26f, 0f);
			_cajaActivos.Height.Set(-(26f + 28f + SeparacionListaActivosBoton), 1f);
			_cajaActivos.BackgroundColor = EstiloTk.FondoCaja;
			izquierda.Append(_cajaActivos);

			_listaActivos = new UIList();
			_listaActivos.Width.Set(-24f, 1f);
			_listaActivos.Height.Set(0f, 1f);
			_listaActivos.ListPadding = 6f;
			_cajaActivos.Append(_listaActivos);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Height.Set(0f, 1f);
			barra.SetView(100f, 1000f);
			_cajaActivos.Append(barra);
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
			FilasActivasParaPrueba.Clear();

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
			// DOS lineas, no una: el nombre nunca se recorta - pedido explicito del usuario tras
			// probar el arreglo anterior ("un nombre mostrado como 'Mana Regenerat...' no cumple lo
			// que pide el usuario, aunque tecnicamente quepa"). El nombre vive SOLO en la linea 1,
			// con TODO el ancho de la fila para el (nadie mas compite por ese espacio), y se
			// ENVUELVE con EtiquetaTk.PartirEnLineas (la fuente real) a tantas lineas como haga
			// falta - nunca "...". La linea 2 (tiempo + boton "Quitar") va DEBAJO, en la posicion Y
			// que calcula AjustarAltoFilasActivas cada fotograma segun cuantas lineas ocupo el
			// nombre de verdad, junto con el alto real de toda la fila. anchoTiempo viene MEDIDO
			// con la fuente real (ver AnchoTiempoReal): la hora nunca se recorta tampoco, pero por
			// una razon distinta - un buffTime es int, y "9999 h 59 min" ya cubre con margen su
			// valor maximo posible (int.MaxValue / 216000 ticks/hora ~= 9942 h), asi que el ancho
			// reservado es sencillamente SIEMPRE suficiente, no hace falta envolver ni recortar.
			float anchoTiempo = AnchoTiempoReal();
			const float anchoBotonQuitar = 70f;
			float leftQuitar = -(anchoBotonQuitar + MargenDerechoFila);
			float leftTiempo = leftQuitar - SeparacionEnFila - anchoTiempo;

			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			// Alto de partida (una sola linea): AjustarAltoFilasActivas lo corrige en cuanto hay
			// dimensiones reales, normalmente el mismo fotograma en que se crea la fila.
			fila.Height.Set(AltoFilaBuff, 0f);

			IconoBuffTk icono = new IconoBuffTk(() => tipo, 32f);
			icono.Left.Set(2f, 0f);
			icono.Top.Set(4f, 0f);
			fila.Append(icono);

			EtiquetaTk nombre = null;
			nombre = new EtiquetaTk(() => {
				string texto = Idiomas.Texto("Personaje.Buffs.NombreConId",
					PersonajeVivo.NombreBuff(tipo), tipo);
				float ancho = nombre.GetDimensions().Width;
				return ancho > 0f ? EtiquetaTk.PartirEnLineas(texto, ancho, 0.8f) : texto;
			}, 0.8f, 0f, AltoLineaTexto);
			nombre.Width.Set(-(40f + MargenDerechoFila), 1f);
			nombre.Left.Set(40f, 0f);
			nombre.Top.Set(MargenSuperiorFila, 0f);
			fila.Append(nombre);

			EtiquetaTk tiempo = new EtiquetaTk(() => TextoTiempo(tipo), 0.8f, anchoTiempo, AltoLineaTexto);
			tiempo.Left.Set(leftTiempo, 1f);
			fila.Append(tiempo);

			BotonTk quitar = new BotonTk(Idiomas.Texto("Personaje.Buffs.Quitar"), 0.75f);
			_botonesQuitar.Add(quitar);
			quitar.Width.Set(anchoBotonQuitar, 0f);
			quitar.Height.Set(AltoLineaSegunda, 0f);
			quitar.Left.Set(leftQuitar, 1f);
			quitar.AlPulsar += () => QuitarBuff(tipo);
			fila.Append(quitar);

			FilasActivasParaPrueba.Add((fila, nombre, tiempo, quitar));

			return fila;
		}

		/// <summary>
		/// Recoloca la linea 2 (tiempo + boton "Quitar") justo debajo de la linea 1 (nombre, ya
		/// envuelta) y ajusta el alto de cada fila a lo que de verdad ocupa - cada fotograma,
		/// porque el ancho disponible para el nombre cambia con la resolucion/columna
		/// (<see cref="RecalcularColumnas"/>) y por tanto tambien puede cambiar cuantas lineas
		/// necesita. Mismo patron que <c>PestanaMundo.RecalcularAviso</c>: medir la geometria YA
		/// dibujada con la fuente real, nunca suponer una sola linea.
		/// </summary>
		private void AjustarAltoFilasActivas()
		{
			if (FilasActivasParaPrueba.Count == 0) {
				return;
			}

			var fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			bool algunCambio = false;

			for (int i = 0; i < FilasActivasParaPrueba.Count; i++) {
				var f = FilasActivasParaPrueba[i];
				float anchoNombre = f.Nombre.GetDimensions().Width;
				if (anchoNombre <= 0f) {
					// El layout todavia no se ha calculado este fotograma (fila recien creada):
					// se reintenta solo, sin tocar nada mientras tanto.
					continue;
				}

				float altoNombre = Math.Max(AltoLineaTexto,
					fuente.MeasureString(f.Nombre.TextoActual).Y * 0.8f);
				float topLinea2 = MargenSuperiorFila + altoNombre + SeparacionEntreLineas;
				float altoFila = topLinea2 + AltoLineaSegunda + MargenInferiorFila;

				f.Nombre.Height.Set(altoNombre, 0f);
				// El texto de tiempo se centra un poco mas abajo que el propio boton (10 vs 7 en
				// el diseño original de una sola linea): se conserva la misma diferencia de 3px.
				f.Tiempo.Top.Set(topLinea2 + 3f, 0f);
				f.Quitar.Top.Set(topLinea2, 0f);

				if (Math.Abs(f.Fila.Height.Pixels - altoFila) > 0.5f) {
					f.Fila.Height.Set(altoFila, 0f);
					algunCambio = true;
				}
			}

			if (algunCambio) {
				_listaActivos.Recalculate();
			}
		}

		/// <summary>
		/// Igual que <see cref="AjustarAltoFilasActivas"/> pero para la lista de "Añadir un buff"
		/// (resultados de busqueda/carpeta): nombre envuelto en la linea 1, boton "Aplicar" solo en
		/// la linea 2 (sin columna de tiempo).
		/// </summary>
		private void AjustarAltoFilasResultado()
		{
			if (FilasResultadoParaPrueba.Count == 0) {
				return;
			}

			var fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			bool algunCambio = false;

			for (int i = 0; i < FilasResultadoParaPrueba.Count; i++) {
				var f = FilasResultadoParaPrueba[i];
				float anchoNombre = f.Nombre.GetDimensions().Width;
				if (anchoNombre <= 0f) {
					continue;
				}

				float altoNombre = Math.Max(AltoLineaTexto,
					fuente.MeasureString(f.Nombre.TextoActual).Y * 0.8f);
				float topLinea2 = MargenSuperiorFila + altoNombre + SeparacionEntreLineas;
				float altoFila = topLinea2 + AltoLineaSegunda + MargenInferiorFila;

				f.Nombre.Height.Set(altoNombre, 0f);
				f.Aplicar.Top.Set(topLinea2, 0f);

				if (Math.Abs(f.Fila.Height.Pixels - altoFila) > 0.5f) {
					f.Fila.Height.Set(altoFila, 0f);
					algunCambio = true;
				}
			}

			if (algunCambio) {
				_listaResultados.Recalculate();
			}
		}

		/// <summary>
		/// Baja <see cref="_cajaActivos"/> lo que haga falta para que quepa el titulo de "Activos"
		/// YA envuelto (<see cref="EtiquetaTk.PartirEnLineas"/>) si alguna vez necesitara mas de una
		/// linea - mismo patron que <c>PestanaMundo.RecalcularAviso</c>, medido con la fuente real
		/// cada fotograma, nunca una constante fija.
		/// </summary>
		private void AjustarAlturaTitulo()
		{
			if (_tituloActivos == null || _cajaActivos == null) {
				return;
			}

			float anchoTitulo = _tituloActivos.GetDimensions().Width;
			if (anchoTitulo <= 0f) {
				return;
			}

			var fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			float altoTitulo = Math.Max(22f, fuente.MeasureString(_tituloActivos.TextoActual).Y * 0.85f);
			float topCaja = altoTitulo + 4f;

			_cajaActivos.Top.Set(topCaja, 0f);
			_cajaActivos.Height.Set(-(topCaja + 28f + SeparacionListaActivosBoton), 1f);
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
			_derecha = derecha;

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
			_botonInicio.Width.Set(60f, 0f);
			_botonInicio.Height.Set(24f, 0f);
			_botonInicio.Ayuda = () => Idiomas.Texto("Personaje.Buffs.Arbol.InicioAyuda");
			_botonInicio.AlPulsar += IrALaRaiz;
			columnaCarpetas.Append(_botonInicio);

			// Left=68 en vez de 66: con el boton de arriba en 60 px de ancho, deja un hueco real de
			// 8 px entre los dos botones en vez de los 4 px de antes.
			_botonSubirCarpeta = new BotonTk(Idiomas.Texto("Personaje.Buffs.Arbol.Subir"), 0.72f);
			_botonSubirCarpeta.Width.Set(64f, 0f);
			_botonSubirCarpeta.Height.Set(24f, 0f);
			_botonSubirCarpeta.Left.Set(68f, 0f);
			_botonSubirCarpeta.Ayuda = () => Idiomas.Texto("Personaje.Buffs.Arbol.SubirAyuda");
			_botonSubirCarpeta.AlPulsar += SubirCarpeta;
			columnaCarpetas.Append(_botonSubirCarpeta);

			_rutaTexto = new EtiquetaTk(RutaCorta, 0.68f, AnchoColumnaCarpetas, 18f);
			_rutaTexto.ColorTexto = EstiloTk.TextoSuave;
			_rutaTexto.Top.Set(32f, 0f);
			columnaCarpetas.Append(_rutaTexto);

			UIPanel cajaCarpetas = new UIPanel();
			cajaCarpetas.Width.Set(0f, 1f);
			cajaCarpetas.Top.Set(56f, 0f);
			cajaCarpetas.Height.Set(-56f, 1f);
			cajaCarpetas.BackgroundColor = EstiloTk.FondoCaja;
			columnaCarpetas.Append(cajaCarpetas);

			_listaCarpetas = new UIList();
			_listaCarpetas.Width.Set(-AnchoBarraScrollCarpetas, 1f);
			_listaCarpetas.Height.Set(0f, 1f);
			_listaCarpetas.ListPadding = 5f;
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
			// ventana, literalmente invisible). El campo de busqueda ocupa hasta y=28: se deja un
			// hueco real de 10 px antes de esta fila (antes 6 px) para que respire.
			EtiquetaTk etiquetaDuracion = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Segundos"), 0.8f, 90f, 20f);
			etiquetaDuracion.ColorTexto = EstiloTk.TextoSuave;
			etiquetaDuracion.Left.Set(0f, 0f);
			etiquetaDuracion.Top.Set(44f, 0f);
			columnaResultados.Append(etiquetaDuracion);

			_campoDuracion = new CampoTextoTk(() => SegundosPorDefecto.ToString(), 6);
			_campoDuracion.SoloNumeros = true;
			_campoDuracion.FijarTextoSilencioso(SegundosPorDefecto.ToString());
			_campoDuracion.Width.Set(80f, 0f);
			_campoDuracion.Height.Set(28f, 0f);
			_campoDuracion.Left.Set(80f, 0f);
			_campoDuracion.Top.Set(38f, 0f);
			columnaResultados.Append(_campoDuracion);

			// El campo de duracion ocupa hasta y=66: 10 px de hueco antes del resumen (antes 4 px).
			_resumenAnadir = new EtiquetaTk(TextoResumenAnadir, 0.68f, 0f, 30f);
			_resumenAnadir.Width.Set(0f, 1f);
			_resumenAnadir.ColorTexto = EstiloTk.TextoSuave;
			_resumenAnadir.Left.Set(0f, 0f);
			_resumenAnadir.Top.Set(76f, 0f);
			columnaResultados.Append(_resumenAnadir);

			// El resumen ocupa hasta y=106: 6 px de hueco antes de la caja de resultados (antes 0,
			// el resumen y la caja quedaban pegados borde con borde).
			UIPanel caja = new UIPanel();
			caja.Width.Set(0f, 1f);
			caja.Height.Set(-170f, 1f);
			caja.Left.Set(0f, 0f);
			caja.Top.Set(112f, 0f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			columnaResultados.Append(caja);

			_listaResultados = new UIList();
			_listaResultados.Width.Set(-24f, 1f);
			_listaResultados.Height.Set(0f, 1f);
			_listaResultados.ListPadding = 6f;
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

		// ---------------------------------------------------------------- reparto de columnas

		/// <summary>
		/// Reajusta el ancho de <see cref="_izquierda"/> ("Activos") y <see cref="_derecha"/>
		/// ("Añadir") cada fotograma, a partir del ancho REAL ya dibujado de la pestaña entera.
		/// <para />
		/// Antes era un simple <c>FraccionColumna</c> fijo al 50%: en una ventana ancha (1600x900)
		/// eso le daba a "Activos" mucho mas hueco del que la fila de un buff necesita de verdad
		/// (icono + nombre + tiempo + boton, ver <see cref="AnchoMaximoActivos"/>), dejando ese
		/// sobrante vacio de verdad en vez de aprovecharlo - "en la izquierda se puede acotar mas
		/// el espacio... para dar mas ancho a donde hace falta" (reporte real del usuario). Ahora
		/// se sigue partiendo del mismo 50%, pero con un TOPE real: por encima de ese tope el
		/// sobrante se le da entero a "Añadir" (busqueda + arbol de carpetas + resultados), que si
		/// lo puede aprovechar. En ventanas estrechas (800x720, la resolucion minima real del
		/// motor) el 50% ya se queda por debajo del tope, asi que ahi no cambia nada - el arreglo
		/// solo actua cuando de verdad sobra espacio, nunca reduciendo por debajo de lo que ya
		/// habia.
		/// </para>
		/// </summary>
		private void RecalcularColumnas()
		{
			if (_izquierda == null || _derecha == null) {
				return;
			}

			float anchoTotal = GetDimensions().Width;
			if (anchoTotal <= 0f) {
				// El layout todavia no se ha calculado (primerisimo fotograma): se reintenta solo
				// en el Update siguiente, sin tocar nada mientras tanto.
				return;
			}

			float deseado = anchoTotal * FraccionColumna - SeparacionColumnas;
			float anchoIzquierda = Math.Min(deseado, AnchoMaximoActivos());
			anchoIzquierda = Math.Max(anchoIzquierda, AnchoMinimoActivos);

			_izquierda.Width.Set(anchoIzquierda, 0f);
			// derecha sigue con HAlign=1f (pegada al borde derecho): solo hace falta decirle
			// cuanto ancho le queda, no su posicion.
			_derecha.Width.Set(-(anchoIzquierda + SeparacionColumnas), 1f);
			_izquierda.Recalculate();
			_derecha.Recalculate();
		}

		/// <summary>
		/// Ancho REAL (medido con la fuente del juego, nunca a ojo) por encima del cual la columna
		/// "Activos" deja de crecer y el sobrante se le da a "Añadir" - el tope que usa
		/// <see cref="RecalcularColumnas"/>.
		/// <para />
		/// El nombre de un buff YA NO necesita reserva aqui: vive en su propia linea y se envuelve
		/// solo si hace falta (ver <see cref="CrearFilaBuff"/>/<see cref="AjustarAltoFilasActivas"/>),
		/// nunca se recorta - una columna mas estrecha que el tope solo hace la fila mas ALTA, no
		/// rompe nada. El tope solo necesita cubrir COMODAMENTE dos cosas de ancho fijo: la segunda
		/// linea de una fila (tiempo + boton "Quitar", <see cref="AnchoTiempoReal"/>) y un colchon
		/// razonable para que un nombre corto o medio no se envuelva sin necesidad en el caso comun
		/// (p.ej. "Regeneración de maná  (id 6)", ~190px medido a escala 0.8).
		/// </para>
		/// </summary>
		private static float AnchoMaximoActivos()
		{
			if (_anchoMaximoActivos < 0f) {
				const float anchoIcono = 40f;
				const float anchoBotonQuitar = 70f;
				const float anchoBarraScroll = 24f;
				const float colchonNombreUnaLinea = 200f;

				float anchoLinea2 = anchoIcono + AnchoTiempoReal() + SeparacionEnFila +
					anchoBotonQuitar + MargenDerechoFila;
				float anchoLinea1Comoda = anchoIcono + colchonNombreUnaLinea;

				_anchoMaximoActivos = Math.Max(anchoLinea2, anchoLinea1Comoda) + anchoBarraScroll;
			}
			return _anchoMaximoActivos;
		}

		/// <summary>
		/// Ancho REAL (medido con la fuente del juego) que necesita la columna de tiempo para
		/// enseñar CUALQUIER duracion posible sin recortar NUNCA, ni en el caso mas extremo: "9999
		/// h 59 min" no es un numero optimista, es una COTA MATEMATICA real. <c>Player.buffTime</c>
		/// es <c>int</c> (32 bits con signo), asi que el valor maximo posible es
		/// <c>int.MaxValue</c> = 2147483647 ticks -> 2147483647 / 60 / 3600 ≈ 9942 horas - por
		/// debajo de las 9999 que mide esta caja, con margen. Cubre de sobra tanto lo que se puede
		/// pedir desde el propio panel (el campo "Segundos" esta acotado a 99999 s = 27 h, ver
		/// <see cref="AplicarBuff"/>) como lo que puede llegar de fuera (un personaje con un
		/// <c>buffTime</c> editado a mano, como el "9255 h 40 min" real que reporto el usuario).
		/// <para />
		/// Antes eran 70px fijos a ojo, y la etiqueta de tiempo nunca se recortaba (a diferencia
		/// del nombre): un tiempo largo se dibujaba entero por FUERA de esa caja de 70px, asomando
		/// bajo el boton "Quitar" de al lado (que se pinta DESPUES en el mismo fotograma, encima)
		/// y quedaba parcialmente tapado por el - el bug real de solape que reporto el usuario.
		/// </para>
		/// </summary>
		private static float AnchoTiempoReal()
		{
			if (_anchoTiempoReal < 0f) {
				_anchoTiempoReal = Terraria.GameContent.FontAssets.MouseText.Value
					.MeasureString("9999 h 59 min").X * 0.8f + 4f;
			}
			return _anchoTiempoReal;
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
			FilasResultadoParaPrueba.Clear();

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
			// Mismas dos lineas que CrearFilaBuff y por la misma razon: el nombre nunca se recorta,
			// se envuelve entero en su propia linea (sin la columna de tiempo, solo compite con el
			// boton "Aplicar" - que pasa igualmente a su propia linea 2, ya sin nada con lo que
			// solaparse).
			const float anchoBotonAplicar = 80f;
			float leftAplicar = -(anchoBotonAplicar + MargenDerechoFila);

			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(AltoFilaBuff, 0f);

			IconoBuffTk icono = new IconoBuffTk(() => tipo, 32f);
			icono.Left.Set(2f, 0f);
			icono.Top.Set(4f, 0f);
			fila.Append(icono);

			EtiquetaTk etiqueta = null;
			etiqueta = new EtiquetaTk(() => {
				string texto = Idiomas.Texto("Personaje.Buffs.NombreConId", nombre, tipo);
				float ancho = etiqueta.GetDimensions().Width;
				return ancho > 0f ? EtiquetaTk.PartirEnLineas(texto, ancho, 0.8f) : texto;
			}, 0.8f, 0f, AltoLineaTexto);
			etiqueta.Width.Set(-(40f + MargenDerechoFila), 1f);
			etiqueta.Left.Set(40f, 0f);
			etiqueta.Top.Set(MargenSuperiorFila, 0f);
			fila.Append(etiqueta);

			BotonTk anadir = new BotonTk(Idiomas.Texto("Personaje.Buffs.Aplicar"), 0.75f);
			_botonesAplicar.Add(anadir);
			anadir.Width.Set(anchoBotonAplicar, 0f);
			anadir.Height.Set(AltoLineaSegunda, 0f);
			anadir.Left.Set(leftAplicar, 1f);
			anadir.AlPulsar += () => AplicarBuff(tipo);
			fila.Append(anadir);

			FilasResultadoParaPrueba.Add((fila, etiqueta, anadir));

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

			// El reparto de columnas depende del ancho REAL ya dibujado (cambia con la
			// resolucion/UIScale): se recalcula cada fotograma, igual que PestanaMundo.RecalcularAviso.
			RecalcularColumnas();
			AjustarAlturaTitulo();

			// Los buffs caducan solos: si el conjunto de tipos activos ha cambiado desde el
			// ultimo fotograma, la lista de la izquierda se rehace sola.
			if (FirmaActivos() != _firmaActivos) {
				ReconstruirActivos();
			}

			// El nombre de cada fila (linea 1) se envuelve segun el ancho REAL disponible, que
			// cambia con la resolucion/columna: la altura de la fila y la posicion de la linea 2
			// (tiempo/boton) se recalculan cada fotograma a partir de eso, nunca solo al construir.
			AjustarAltoFilasActivas();
			AjustarAltoFilasResultado();

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
