using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// Pestaña "Este mundo": la ficha del mundo cargado y el cambio de dificultad EN VIVO.
	/// </summary>
	/// <remarks>
	/// El cambio de dificultad va en <b>dos pasos a proposito</b> (elegir modo -&gt; confirmar).
	/// No es un capricho de interfaz: a diferencia de la app de escritorio, que no escribe nada
	/// hasta que se lo pides, aqui el cambio queda grabado en el mundo en el siguiente guardado y
	/// no hay ventana de "descartar". El aviso de lo que eso significa esta SIEMPRE a la vista,
	/// antes de tocar nada, no en un dialogo que aparece despues.
	/// </remarks>
	public class PestanaMundo : UIElement
	{
		/// <summary>
		/// Reparto de las dos columnas, en PORCENTAJE del ancho real y no en pixeles fijos.
		/// <para />
		/// Antes eran 430 px fijos para la ficha, y lo que quedaba para la dificultad. En una
		/// ventana de 800 px eso dejaba ~318 px a la derecha, y ahi no cabian ni los botones de modo
		/// (140 px x 3 = 444) ni las tres lineas del aviso de permanencia (460 px cada una): las
		/// tres cosas se salian del marco por la derecha. Se vio en una captura real del juego.
		/// </summary>
		private const float FraccionIzquierda = 0.52f;

		/// <summary>Separacion entre las dos columnas.</summary>
		private const float SeparacionColumnas = 12f;

		/// <summary>Publicas para que la autoprueba de espaciado pueda repetir la MISMA medicion de
		/// texto real que usa <see cref="RecalcularAviso"/>, de forma independiente (a partir del
		/// texto sin partir y del ancho interior real de la caja), sin duplicar el numero a mano.</summary>
		public const float EscalaAviso = 0.72f;
		public const float EscalaAvisoSecundario = 0.68f;
		private const float PaddingCajaAviso = 8f;
		private const float SeparacionEntreAvisos = 6f;
		private const float SeparacionTrasCajaAviso = 10f;
		private const float AltoBotonConfirmar = 38f;
		private const float SeparacionTrasConfirmar = 8f;

		private int _modoElegido = -1;
		private readonly List<KeyValuePair<int, BotonTk>> _botonesModo = new List<KeyValuePair<int, BotonTk>>();
		private BotonTk _confirmar;
		private string _ultimoMensaje = "";

		private UIElement _derecha;
		private UIPanel _cajaAviso;
		private EtiquetaTk _etiquetaAviso;
		private EtiquetaTk _etiquetaEfecto;
		private EtiquetaTk _etiquetaDeshacer;
		private EtiquetaTk _mensaje;
		private string _mensajePartido = "";

		/// <summary>
		/// Columna derecha en una <see cref="UIList"/> (v0.7.0). Antes cada pieza colgaba de un Top
		/// fijo calculado a mano, y a 1280x720 el boton de confirmar y el mensaje se salian por
		/// debajo de la pestaña hasta pisar "Cerrar (P)" (visto en la captura real de la verificacion
		/// de la v0.7.0). Ahora las piezas se apilan solas por su alto real y, si no caben, aparece una
		/// barra de desplazamiento (y solo entonces): la letra nunca se encoge y nada se solapa.
		/// </summary>
		private UIList _listaDerecha;
		private UIScrollbar _scrollDerecha;
		private bool _scrollDerechaVisible;

		/// <summary>Modo que esta elegido (pendiente de confirmar), o -1 si ninguno.</summary>
		public int ModoElegido => _modoElegido;

		/// <summary>El rectangulo naranja de aviso de permanencia, para que la autoprueba de
		/// espaciado pueda medir su geometria REAL (<c>GetDimensions</c>/<c>GetInnerDimensions</c>)
		/// y comprobar que el texto de verdad cabe dentro, no solo confiar en el propio calculo que
		/// ya lo coloca.</summary>
		public UIPanel CajaAviso => _cajaAviso;

		/// <summary>La ultima de las tres etiquetas del aviso (la que marca el borde inferior real
		/// del contenido), para la misma comprobacion.</summary>
		public EtiquetaTk EtiquetaDeshacer => _etiquetaDeshacer;

		/// <summary>El boton de confirmar, para comprobar que tampoco se solapa con la caja de
		/// aviso de encima.</summary>
		public BotonTk BotonConfirmar => _confirmar;

		public PestanaMundo()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirFicha();
			ConstruirDificultad();
			RecalcularAviso();
		}

		private void ConstruirFicha()
		{
			UIPanel caja = new UIPanel();
			caja.Width.Set(-SeparacionColumnas, FraccionIzquierda);
			caja.Height.Set(0f, 1f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(12f);
			Append(caja);
			_cajaFicha = caja;

			_tituloFicha = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.Ficha"), EscalaTituloFicha, 380f, 26f);
			caja.Append(_tituloFicha);

			// "colapsable" = lo que ya repite la cabecera de Exploracion justo encima (nombre, tamaño,
			// modo, posicion y porcentaje explorado): es lo primero que se deja de enseñar si la
			// ficha no cabe ni apretando el interlineado - ver ReflowVertical.DistribuirRenglones.
			Dato(caja, "Nombre", () => MundoActual.Nombre, colapsable: true);
			Dato(caja, "Semilla", () => MundoActual.Semilla, colapsable: false);
			Dato(caja, "Tamano", () => MundoActual.TamanoLegible, colapsable: true);
			Dato(caja, "Tiles", () => MundoActual.TotalTiles.ToString("N0"), colapsable: false);
			Dato(caja, "Modo", () => Idiomas.Texto("Exploracion.Mundo.ModoValor",
				MundoActual.ModoDeJuegoLegible, MundoActual.ModoDeJuego), colapsable: true);
			Dato(caja, "Progreso", () => Idiomas.Texto(MundoActual.EsHardmode
				? "Exploracion.Hardmode"
				: "Exploracion.PreHardmode"), colapsable: false);
			Dato(caja, "MalDelMundo", () => MundoActual.MalDelMundo, colapsable: false);
			Dato(caja, "SemillasSecretas", () => MundoActual.SemillasSecretas, colapsable: false);
			Dato(caja, "Aparicion",
				() => Idiomas.Texto("Exploracion.Mundo.Tile", MundoActual.PuntoDeAparicion), colapsable: false);
			Dato(caja, "EstasEn",
				() => Idiomas.Texto("Exploracion.Mundo.Tile", MundoActual.PosicionDelJugador), colapsable: true);
			Dato(caja, "Explorado",
				() => Idiomas.Texto("Exploracion.Mundo.Porcentaje",
					MundoActual.PorcentajeExplorado().ToString("0.0")), colapsable: true);
			Dato(caja, "Autoguardado", () => Idiomas.Texto(Main.autoSave
				? "Exploracion.Mundo.Activado"
				: "Exploracion.Mundo.Desactivado"), colapsable: false);
			ConstruirInvasiones(caja);
		}

		// ---- Ficha: renglones de paso fijo con reparto legible (v0.7.0) --------------------------

		/// <summary>Donde empieza la columna de valores de la ficha.</summary>
		private const float ColumnaValor = 140f;
		private const float CabeceraFicha = 34f;
		private const float PasoFicha = 24f;
		private const float AltoRenglonFicha = 22f;
		private const float EscalaTextoFicha = 0.8f;
		private const float EscalaTituloFicha = 0.95f;

		/// <summary>Paso minimo antes de empezar a encoger la letra: 19 de 24 px. A escala 0,8 el
		/// glifo de la fuente del juego sigue cabiendo entero en un renglon de 19 px.</summary>
		private const float FraccionPasoMinimoFicha = 19f / 24f;

		private const float SeparacionBotonesInvasion = 4f;
		private const float EscalaBotonInvasion = 0.72f;

		private UIPanel _cajaFicha;
		private EtiquetaTk _tituloFicha;
		private readonly List<RenglonFicha> _renglones = new List<RenglonFicha>();
		private bool[] _colapsables;
		private bool[] _visibles;
		private readonly List<KeyValuePair<InvasionesMundo.Invasion, BotonTk>> _botonesInvasion =
			new List<KeyValuePair<InvasionesMundo.Invasion, BotonTk>>();

		/// <summary>Factores con los que se esta dibujando la ficha ahora mismo (1 = sin tocar). Los
		/// lee la autoprueba de espaciado para dejarlos en la evidencia.</summary>
		public float FactorPasoFicha { get; private set; } = 1f;
		public float FactorLetraFicha { get; private set; } = 1f;
		public int RenglonesOcultosFicha { get; private set; }

		private sealed class RenglonFicha
		{
			public EtiquetaTk Nombre;
			public EtiquetaTk Valor;
			public List<BotonTk> Botones;
			public bool Colapsable;
		}

		/// <summary>La caja de la ficha, para que la autoprueba de espaciado compruebe que ningun
		/// renglon visible se sale por debajo.</summary>
		public UIPanel CajaFicha => _cajaFicha;

		/// <summary>Los tres botones de invasion, para la autoprueba (los pulsa por su ruta real).</summary>
		public IReadOnlyList<KeyValuePair<InvasionesMundo.Invasion, BotonTk>> BotonesInvasion => _botonesInvasion;

		/// <summary>Una fila "rotulo: valor" de la ficha. Recibe la CLAVE de localizacion del
		/// rotulo, no el texto ya resuelto. Un renglon oculto por falta de sitio devuelve texto
		/// vacio (no se dibuja ni cuenta para las autopruebas de espaciado).</summary>
		private void Dato(UIElement padre, string clave, System.Func<string> valor, bool colapsable)
		{
			int indice = _renglones.Count;
			// Ancho del rotulo = hasta la columna de valores (menos 4 px), no 150: con 150 la caja del
			// rotulo invadia 10 px la del valor, que empieza en ColumnaValor (140).
			EtiquetaTk nombre = new EtiquetaTk(
				() => Visible(indice) ? Idiomas.Texto("Exploracion.Mundo.Dato." + clave) : "",
				EscalaTextoFicha, ColumnaValor - 4f, AltoRenglonFicha);
			nombre.ColorTexto = EstiloTk.TextoSuave;
			padre.Append(nombre);

			EtiquetaTk contenido = new EtiquetaTk(() => Visible(indice) ? valor() : "",
				EscalaTextoFicha, 250f, AltoRenglonFicha);
			contenido.Left.Set(ColumnaValor, 0f);
			padre.Append(contenido);

			_renglones.Add(new RenglonFicha { Nombre = nombre, Valor = contenido, Colapsable = colapsable });
		}

		private bool Visible(int indice)
		{
			return _visibles == null || indice >= _visibles.Length || _visibles[indice];
		}

		// ---- Invasiones vencidas (paridad con Terrakeep escritorio 3.3.0, commit cc1d4ddc) -------

		private void ConstruirInvasiones(UIPanel caja)
		{
			EtiquetaTk nombre = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.Dato.Invasiones"), EscalaTextoFicha, ColumnaValor - 4f, AltoRenglonFicha);
			nombre.ColorTexto = EstiloTk.TextoSuave;
			caja.Append(nombre);

			var botones = new List<BotonTk>();
			foreach (InvasionesMundo.Invasion invasion in InvasionesMundo.Todas) {
				InvasionesMundo.Invasion cerrada = invasion;
				BotonTk boton = new BotonTk(InvasionesMundo.NombreCorto(invasion), EscalaBotonInvasion);
				boton.Clave = invasion.ToString();
				boton.Height.Set(AltoRenglonFicha, 0f);
				boton.AlPulsar += () => InvasionesMundo.Alternar(cerrada, "ficha de Exploración > Este mundo");
				boton.Ayuda = () => AyudaInvasion(cerrada);
				caja.Append(boton);
				_botonesInvasion.Add(new KeyValuePair<InvasionesMundo.Invasion, BotonTk>(invasion, boton));
				botones.Add(boton);
			}

			_renglones.Add(new RenglonFicha { Nombre = nombre, Botones = botones, Colapsable = false });

			_colapsables = new bool[_renglones.Count];
			for (int i = 0; i < _renglones.Count; i++) {
				_colapsables[i] = _renglones[i].Colapsable;
			}
		}

		private static string AyudaInvasion(InvasionesMundo.Invasion invasion)
		{
			string motivo = InvasionesMundo.MotivoParaNoPoder();
			if (motivo != null) {
				return motivo;
			}
			return Idiomas.Texto(InvasionesMundo.Vencida(invasion)
				? "Exploracion.Mundo.Invasiones.AyudaVencida"
				: "Exploracion.Mundo.Invasiones.AyudaPendiente", InvasionesMundo.Nombre(invasion));
		}

		/// <summary>
		/// Cada fotograma: estado real de los tres botones de invasion (resaltado = vencida, igual
		/// que el modo actual en los botones de dificultad) y reparto de la ficha en el alto REAL de
		/// su caja con <see cref="ReflowVertical.DistribuirRenglones"/>: primero se ocultan los datos
		/// que ya repite la cabecera, luego se aprieta el interlineado y solo en ultimo caso se
		/// encoge la letra - nunca un solape.
		/// </summary>
		private void ActualizarFicha()
		{
			if (_cajaFicha == null || _colapsables == null) {
				return;
			}

			bool sePuede = InvasionesMundo.MotivoParaNoPoder() == null;
			foreach (KeyValuePair<InvasionesMundo.Invasion, BotonTk> par in _botonesInvasion) {
				par.Value.Activo = InvasionesMundo.Vencida(par.Key);
				par.Value.Habilitado = sePuede;
				par.Value.FijarTexto(InvasionesMundo.NombreCorto(par.Key));
			}

			CalculatedStyle interior = _cajaFicha.GetInnerDimensions();
			if (interior.Width <= 0f || interior.Height <= 0f) {
				return;
			}

			bool[] visibles = new bool[_colapsables.Length];
			float factorLetra;
			float factor = ReflowVertical.DistribuirRenglones(CabeceraFicha, PasoFicha, _colapsables, interior.Height,
				FraccionPasoMinimoFicha, visibles, out factorLetra);
			_visibles = visibles;
			FactorPasoFicha = factor;
			FactorLetraFicha = factorLetra;

			_tituloFicha.EscalaTexto = EscalaTituloFicha * factorLetra;
			float paso = PasoFicha * factor;
			float alto = System.Math.Min(AltoRenglonFicha, paso - 1f);
			float y = CabeceraFicha * factor;
			int ocultos = 0;
			for (int i = 0; i < _renglones.Count; i++) {
				RenglonFicha renglon = _renglones[i];
				if (!visibles[i]) {
					ocultos++;
					continue;
				}
				renglon.Nombre.Top.Set(y, 0f);
				renglon.Nombre.Height.Set(alto, 0f);
				renglon.Nombre.EscalaTexto = EscalaTextoFicha * factorLetra;
				if (renglon.Valor != null) {
					renglon.Valor.Top.Set(y, 0f);
					renglon.Valor.Height.Set(alto, 0f);
					renglon.Valor.EscalaTexto = EscalaTextoFicha * factorLetra;
				}
				if (renglon.Botones != null) {
					ColocarBotonesInvasion(renglon.Botones, interior.Width, y, alto, factorLetra);
				}
				y += paso;
			}
			RenglonesOcultosFicha = ocultos;

			_cajaFicha.Recalculate();
		}

		/// <summary>Los tres botones se reparten el hueco real de la columna de valores; si el rotulo
		/// mas largo no cabe a su escala, se baja la escala comun lo justo (nunca se recorta con
		/// "...", regla del proyecto - mismo criterio que AjustarEscalaDeLasPestanas).</summary>
		private static void ColocarBotonesInvasion(List<BotonTk> botones, float anchoInterior, float y, float alto, float factorLetra)
		{
			float anchoValor = anchoInterior - ColumnaValor;
			float anchoBoton = (anchoValor - SeparacionBotonesInvasion * 2f) / 3f;
			if (anchoBoton > 120f) {
				anchoBoton = 120f;
			}
			var fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			float escala = EscalaBotonInvasion * factorLetra;
			foreach (BotonTk boton in botones) {
				float anchoTexto = fuente.MeasureString(boton.Texto).X * escala + 12f;
				if (anchoTexto > anchoBoton && anchoTexto > 0f) {
					escala *= anchoBoton / anchoTexto;
				}
			}
			for (int i = 0; i < botones.Count; i++) {
				BotonTk boton = botones[i];
				boton.EscalaTexto = escala;
				boton.Width.Set(anchoBoton, 0f);
				boton.Height.Set(alto, 0f);
				boton.Top.Set(y, 0f);
				boton.Left.Set(ColumnaValor + i * (anchoBoton + SeparacionBotonesInvasion), 0f);
			}
		}

		private void ConstruirDificultad()
		{
			UIElement derecha = new UIElement();
			derecha.Width.Set(0f, 1f - FraccionIzquierda);
			derecha.Height.Set(0f, 1f);
			derecha.HAlign = 1f;
			Append(derecha);
			_derecha = derecha;

			_listaDerecha = new UIList();
			_listaDerecha.Width.Set(0f, 1f);
			_listaDerecha.Height.Set(0f, 1f);
			_listaDerecha.ListPadding = 0f;
			// Mismo motivo real que ContenidoGuia/PestanaVecindad: List.Sort no es estable y UIList lo
			// usa por defecto - el orden de las piezas es el de insercion y nada mas.
			_listaDerecha.ManualSortMethod = elementos => { };
			derecha.Append(_listaDerecha);

			_scrollDerecha = new UIScrollbar();
			_scrollDerecha.Width.Set(16f, 0f);
			_scrollDerecha.Height.Set(0f, 1f);
			_scrollDerecha.HAlign = 1f;
			_listaDerecha.SetScrollbar(_scrollDerecha);

			UIElement cabecera = new UIElement();
			cabecera.Width.Set(0f, 1f);
			cabecera.Height.Set(62f, 0f);
			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.Dificultad"), 0.95f, 500f, 26f);
			cabecera.Append(titulo);
			EtiquetaTk actual = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.AhoraMismo", MundoActual.ModoDeJuegoLegible),
				0.85f, 500f, 24f);
			actual.Top.Set(30f, 0f);
			cabecera.Append(actual);
			_listaDerecha.Add(cabecera);

			// DOS por fila y en porcentaje: tres botones de 140 px fijos no caben en la columna
			// derecha de una ventana de 800 (el tercero se salia del marco).
			UIElement rejilla = new UIElement();
			rejilla.Width.Set(0f, 1f);
			int i = 0;
			foreach (int modo in MundoActual.ModosDisponibles()) {
				int valor = modo;
				BotonTk boton = new BotonTk(MundoActual.NombreDeModo(modo), 0.85f);
				boton.Clave = modo.ToString();
				boton.Left.Set(0f, (i % 2) * 0.5f);
				boton.Top.Set((i / 2) * 40f, 0f);
				boton.Width.Set(-6f, 0.5f);
				boton.Height.Set(34f, 0f);
				boton.AlPulsar += () => Elegir(valor);
				rejilla.Append(boton);
				_botonesModo.Add(new KeyValuePair<int, BotonTk>(valor, boton));
				i++;
			}
			rejilla.Height.Set(((i + 1) / 2) * 40f + 8f, 0f);
			_listaDerecha.Add(rejilla);

			// El aviso de permanencia va SIEMPRE visible, antes de tocar nada. Su altura NO es fija:
			// las tres frases se parten en las lineas que quepan en el ancho real de la caja (en una
			// sola linea median ~460 px y se salian por la derecha del marco, captura real del juego),
			// y el numero de lineas cambia con el idioma y con el estado del autoguardado (la frase
			// de permanencia es mas larga con el autoguardado apagado). Con una altura fija de 120 px
			// el texto se salia igualmente por ABAJO del propio rectangulo (visto en captura real):
			// aqui se mide el texto YA partido con la fuente real y se ajusta la caja entera a la
			// altura que de verdad hace falta, recalculado cada fotograma en RecalcularAviso; la lista
			// recoloca sola lo que va debajo (el boton de confirmar y el mensaje).
			_cajaAviso = new UIPanel();
			_cajaAviso.Width.Set(0f, 1f);
			_cajaAviso.Height.Set(120f, 0f);
			_cajaAviso.BackgroundColor = new Color(92, 60, 30) * 0.92f;
			_cajaAviso.BorderColor = new Color(0, 0, 0, 0);
			_cajaAviso.SetPadding(PaddingCajaAviso);
			_listaDerecha.Add(_cajaAviso);

			_etiquetaAviso = new EtiquetaTk(
				() => TextoEnLineas("⚠  " + DificultadMundo.AvisoDePermanencia(), _cajaAviso, EscalaAviso),
				EscalaAviso, 0f, 22f);
			_etiquetaAviso.Width.Set(0f, 1f);
			_etiquetaAviso.ColorTexto = EstiloTk.TextoAviso;
			_cajaAviso.Append(_etiquetaAviso);

			_etiquetaEfecto = new EtiquetaTk(
				() => TextoEnLineas(DificultadMundo.AvisoDeEfecto(), _cajaAviso, EscalaAvisoSecundario),
				EscalaAvisoSecundario, 0f, 22f);
			_etiquetaEfecto.Width.Set(0f, 1f);
			_etiquetaEfecto.ColorTexto = new Color(235, 220, 200);
			_cajaAviso.Append(_etiquetaEfecto);

			_etiquetaDeshacer = new EtiquetaTk(
				() => TextoEnLineas(Idiomas.Texto("Exploracion.Mundo.AvisoDeshacer"), _cajaAviso, EscalaAvisoSecundario),
				EscalaAvisoSecundario, 0f, 22f);
			_etiquetaDeshacer.ColorTexto = new Color(235, 220, 200);
			_etiquetaDeshacer.Width.Set(0f, 1f);
			_cajaAviso.Append(_etiquetaDeshacer);

			_listaDerecha.Add(Hueco(SeparacionTrasCajaAviso));

			_confirmar = new BotonTk(Idiomas.Texto("Exploracion.Mundo.EligeModo"), 0.85f);
			_confirmar.Width.Set(0f, 1f);
			_confirmar.Height.Set(AltoBotonConfirmar, 0f);
			_confirmar.Habilitado = false;
			_confirmar.AlPulsar += Confirmar;
			_listaDerecha.Add(_confirmar);

			_listaDerecha.Add(Hueco(SeparacionTrasConfirmar));

			// Partido con el ancho REAL (antes era un renglon de 500 px fijos: un "No se puede: ..."
			// largo se salia por la derecha) y con alto 0 mientras no hay nada que decir.
			_mensaje = new EtiquetaTk(() => _mensajePartido, EscalaMensaje, 0f, 0f);
			_mensaje.Width.Set(0f, 1f);
			_mensaje.ColorTexto = EstiloTk.TextoAviso;
			_listaDerecha.Add(_mensaje);

			// Con la altura real de la caja de aviso todavia sin calcular (depende de
			// GetInnerDimensions, que solo existe tras el primer Recalculate del arbol), se deja el
			// primer ajuste de verdad para el primer Update: RecalcularAviso() se reintenta sola cada
			// fotograma hasta que el ancho interior deja de ser cero.
		}

		private const float EscalaMensaje = 0.75f;

		private static UIElement Hueco(float alto)
		{
			UIElement hueco = new UIElement();
			hueco.Width.Set(0f, 1f);
			hueco.Height.Set(alto, 0f);
			return hueco;
		}

		/// <summary>true si la columna de dificultad no cabe entera y enseña su barra de
		/// desplazamiento. Lo lee la autoprueba de espaciado.</summary>
		public bool ScrollDificultadVisible => _scrollDerechaVisible;

		/// <summary>La lista de la columna de dificultad, para que la autoprueba compruebe que su
		/// contenido visible no se sale de la pestaña.</summary>
		public UIList ListaDificultad => _listaDerecha;

		/// <summary>
		/// Mide con la fuente REAL el texto ya partido de los tres avisos, ajusta la altura de
		/// <see cref="_cajaAviso"/> a lo que de verdad ocupan (nunca una cifra fija), y recoloca el
		/// boton de confirmar y el mensaje de resultado justo debajo. Se llama cada fotograma porque
		/// el texto puede cambiar (idioma, autoguardado) y el ancho de la caja tambien (redimensionar
		/// la ventana).
		/// </summary>
		private void RecalcularAviso()
		{
			if (_cajaAviso == null) {
				return;
			}

			float anchoInterior = _cajaAviso.GetInnerDimensions().Width;
			if (anchoInterior <= 0f) {
				// El layout todavia no se ha calculado (primerisimo fotograma): se reintenta solo en
				// el Update siguiente, sin tocar nada mientras tanto.
				return;
			}

			string avisoPartido = EtiquetaTk.PartirEnLineas(
				"⚠  " + DificultadMundo.AvisoDePermanencia(), anchoInterior, EscalaAviso);
			string efectoPartido = EtiquetaTk.PartirEnLineas(
				DificultadMundo.AvisoDeEfecto(), anchoInterior, EscalaAvisoSecundario);
			string deshacerPartido = EtiquetaTk.PartirEnLineas(
				Idiomas.Texto("Exploracion.Mundo.AvisoDeshacer"), anchoInterior, EscalaAvisoSecundario);

			var fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			float altoAviso = fuente.MeasureString(avisoPartido).Y * EscalaAviso;
			float altoEfecto = fuente.MeasureString(efectoPartido).Y * EscalaAvisoSecundario;
			float altoDeshacer = fuente.MeasureString(deshacerPartido).Y * EscalaAvisoSecundario;

			float yEfecto = altoAviso + SeparacionEntreAvisos;
			float yDeshacer = yEfecto + altoEfecto + SeparacionEntreAvisos;
			float alturaInterior = yDeshacer + altoDeshacer;
			float alturaCaja = alturaInterior + PaddingCajaAviso * 2f;

			_etiquetaAviso.Top.Set(0f, 0f);
			_etiquetaEfecto.Top.Set(yEfecto, 0f);
			_etiquetaDeshacer.Top.Set(yDeshacer, 0f);
			_cajaAviso.Height.Set(alturaCaja, 0f);

			_mensajePartido = string.IsNullOrEmpty(_ultimoMensaje)
				? ""
				: EtiquetaTk.PartirEnLineas(_ultimoMensaje, _listaDerecha.GetInnerDimensions().Width, EscalaMensaje);
			_mensaje.Height.Set(_mensajePartido.Length == 0 ? 0f : fuente.MeasureString(_mensajePartido).Y * EscalaMensaje, 0f);

			_listaDerecha.Recalculate();

			// Barra de desplazamiento solo si de verdad no cabe. Sin oscilar: con la barra puesta la
			// lista es 20 px mas estrecha, el texto parte en mas lineas y el total solo puede crecer,
			// asi que una vez que hace falta sigue haciendo falta; y al quitarla, lo mismo al reves.
			bool hace = _listaDerecha.GetTotalHeight() > _derecha.GetInnerDimensions().Height + 0.5f;
			if (hace != _scrollDerechaVisible) {
				_scrollDerechaVisible = hace;
				if (hace) {
					_listaDerecha.Width.Set(-20f, 1f);
					_derecha.Append(_scrollDerecha);
				}
				else {
					_listaDerecha.Width.Set(0f, 1f);
					_derecha.RemoveChild(_scrollDerecha);
					_scrollDerecha.ViewPosition = 0f;
				}
			}

			_derecha.Recalculate();
		}

		/// <summary>
		/// Parte un texto en lineas que quepan en el ancho REAL del elemento, midiendolas con la
		/// fuente con la que se van a dibujar. Se hace en cada dibujado y no una vez porque el
		/// ancho depende de la resolucion y de la escala de interfaz del jugador, y porque el texto
		/// cambia con el idioma y con el estado del autoguardado.
		/// </summary>
		private static string TextoEnLineas(string texto, UIElement caja, float escala)
		{
			return EtiquetaTk.PartirEnLineas(texto, caja.GetInnerDimensions().Width, escala);
		}

		/// <summary>Elige un modo (primer paso). Publico porque lo usa la autoprueba.</summary>
		public void Elegir(int modo)
		{
			string motivo = DificultadMundo.MotivoParaNoPoder(modo);
			if (motivo != null) {
				_modoElegido = -1;
				_ultimoMensaje = Idiomas.Texto("Exploracion.Mundo.NoSePuede", motivo);
				RefrescarBotones();
				RegistroExploracion.Linea(Terrakeep.LogTag + " Dificultad: modo \"" +
					MundoActual.NombreDeModo(modo) + "\" NO disponible. " + motivo);
				return;
			}

			_modoElegido = modo;
			_ultimoMensaje = Idiomas.Texto("Exploracion.Mundo.VasAPasar",
				MundoActual.ModoDeJuegoLegible, MundoActual.NombreDeModo(modo));
			RefrescarBotones();
		}

		/// <summary>Aplica el modo elegido (segundo paso). Publico para la autoprueba.</summary>
		public void Confirmar()
		{
			if (_modoElegido < 0) {
				return;
			}

			string motivo;
			int pedido = _modoElegido;
			if (DificultadMundo.Aplicar(pedido, "panel de Exploración (dos pasos)", out motivo)) {
				_ultimoMensaje = Idiomas.Texto("Exploracion.Mundo.Hecho",
					MundoActual.NombreDeModo(pedido));
			}
			else {
				_ultimoMensaje = Idiomas.Texto("Exploracion.Mundo.NoSePudo", motivo);
			}

			_modoElegido = -1;
			RefrescarBotones();
		}

		private void RefrescarBotones()
		{
			foreach (KeyValuePair<int, BotonTk> par in _botonesModo) {
				bool esElActual = par.Key == MundoActual.ModoDeJuego;
				string motivo = DificultadMundo.MotivoParaNoPoder(par.Key);

				par.Value.Activo = esElActual || par.Key == _modoElegido;
				par.Value.Habilitado = motivo == null;
				int modoDelBoton = par.Key;
				par.Value.Ayuda = () => modoDelBoton == MundoActual.ModoDeJuego
					? Idiomas.Texto("Exploracion.Mundo.AyudaModoActual")
					: (DificultadMundo.MotivoParaNoPoder(modoDelBoton)
						?? Idiomas.Texto("Exploracion.Mundo.AyudaElegirModo"));
			}

			if (_confirmar == null) {
				return;
			}

			if (_modoElegido < 0) {
				_confirmar.FijarTexto(Idiomas.Texto("Exploracion.Mundo.EligeModo"));
				_confirmar.Habilitado = false;
			}
			else {
				_confirmar.FijarTexto(Idiomas.Texto("Exploracion.Mundo.Confirmar",
					MundoActual.NombreDeModo(_modoElegido)));
				_confirmar.Habilitado = true;
			}

			// Los rotulos de los botones de modo se fijan al construirlos: se vuelven a poner para
			// que cambien en vivo con el selector de idioma del area de Ajustes.
			foreach (KeyValuePair<int, BotonTk> par in _botonesModo) {
				par.Value.FijarTexto(MundoActual.NombreDeModo(par.Key));
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			// El modo del mundo puede cambiar sin pasar por aqui (deshacer con Ctrl+Z, otro panel),
			// asi que los botones se recalculan solos.
			RefrescarBotones();
			// Igual que los botones: el aviso de permanencia puede cambiar de numero de lineas sin
			// pasar por aqui (idioma, autoguardado), asi que su altura tambien se recalcula sola.
			RecalcularAviso();
			ActualizarFicha();
		}
	}
}
