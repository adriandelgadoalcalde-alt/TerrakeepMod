using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Ajustes;
using TerrakeepMod.UI.Builds;
using TerrakeepMod.UI.Exploracion;
using TerrakeepMod.UI.Guia;
using TerrakeepMod.UI.Hitos;
using TerrakeepMod.UI.Investigacion;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Libreria.Widgets;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Panel
{
	/// <summary>
	/// <b>El panel de Terrakeep.</b> Uno solo, con una barra de pestañas, en el que viven las seis
	/// areas que antes eran seis paneles sueltos con seis teclas y seis marcos:
	/// Personaje · Librería · Builds · Investigación · Exploración · Ajustes (el mismo orden que
	/// la aplicacion de escritorio hermana, por familiaridad).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Cada pestaña monta el <c>UIElement</c> de contenido que ya construyo su workstream. Todo lo
	/// que era mecanica de apertura (marco, cabecera, boton de cerrar, dibujado del objeto cogido
	/// con el raton, mantener el inventario abierto) estaba repetido seis veces con seis copias
	/// casi identicas del mismo codigo; ahora esta aqui una sola vez.
	/// </para>
	/// <para>
	/// Los fallos reales del motor que encontro WS1 con <c>IngameFancyUI</c> siguen resueltos,
	/// y ahora en un unico sitio: <see cref="MantenerInventarioAbierto"/> (si no,
	/// <c>Player.dropItemCheck</c> vacia cada tick el objeto que se lleva cogido),
	/// <see cref="DibujarObjetoEnRaton"/> (la capa 38 de vanilla nunca corre con un panel de
	/// <c>IngameFancyUI</c> abierto) y <see cref="DibujarTooltipDeObjeto"/> (la capa 33, la del
	/// tooltip de objeto, tampoco corre - sin esto ninguna ranura de ninguna pestaña enseñaba
	/// nombre/prefijo/stats al pasar el raton por encima, encontrado en el juego real).
	/// </para>
	/// </remarks>
	public class PanelTerrakeepState : UIState
	{
		private const float AnchoMaximo = 1080f;
		private const float AltoPie = 44f;

		/// <summary>
		/// Fraccion de <c>Main.screenHeight</c> que puede ocupar como maximo el marco. Antes era un
		/// tope FIJO de 700px (<c>AltoMaximo</c>); en un portatil de 1366x768 eso eran 700/768 = el
		/// 91% del alto de pantalla y tapaba el HUD de vida (visto en captura real, catalogo de
		/// rediseño visual TM6). 82% de la resolucion REAL, recalculado cada fotograma igual que ya
		/// hace <see cref="AjustarEscalaDeLasPestanas"/> con el ancho (la resolucion puede cambiar en
		/// vivo si el jugador redimensiona la ventana o cambia de pantalla completa a ventana).
		/// </summary>
		private const float FraccionAltoMaximoDePantalla = 0.82f;

		/// <summary>
		/// Alto de la fila del titulo, que ademas hace de <b>hueco para el HUD del juego</b>.
		/// <para />
		/// No es decoracion: con un panel de <c>IngameFancyUI</c> abierto, el juego <b>sigue
		/// dibujando las barras de vida y mana ENCIMA</b>, y lo hace a proposito. El codigo real de
		/// <c>IngameFancyUI.Draw</c> (tModLoader.dll instalado) llama a
		/// <c>Main.instance.GUIBarsDraw()</c> despues de que <c>InGameUI.Draw</c> haya pintado el
		/// panel del mod, asi que los corazones quedan por encima. Se vio en una captura real del
		/// juego: la barra de pestañas estaba justo debajo de los corazones y estos tapaban el
		/// texto de la pestaña "Exploración". Dejando esta fila arriba, las pestañas caen ya por
		/// debajo del HUD, y el hueco se aprovecha para el titulo (que va a la IZQUIERDA, que es
		/// donde el HUD no pinta nada).
		/// </summary>
		private const float AltoTitulo = 30f;

		private const float AltoBarraPestanas = EstiloTk.AltoPestana;

		private UIPanel _marco;
		private UIElement _contenedor;
		private CapaSuperposicionTk _capaSuperposicion;
		private readonly List<BotonTk> _botonesPestana = new List<BotonTk>();
		private BotonTk _botonCerrar;

		// --- TM3 (catalogo de rediseño visual): chips de la cabecera, visibles desde cualquier
		// pestaña - "hoy hay que ir a la pestaña Guía para saber qué toca; un chip lo dice desde
		// cualquier pestaña". Solo dos de los tres que proponia el catalogo original: el tercero
		// (vida/maná) se descarta a proposito, ver el XMLdoc de ConstruirCabeceraChips.
		private BotonTk _chipHora;
		private BotonTk _chipObjetivo;

		/// <summary>Idea 2 del catalogo de funciones ("DPS-metro"): el cuarto chip que ya preveia
		/// TM3 ("aqui encaja el DPS-metro del catalogo hermano como cuarto chip") - tercero real
		/// aqui, porque el propio vida/mana se descarto (ver ConstruirCabeceraChips). Dato real de
		/// <see cref="MedidorDanio"/>.</summary>
		private BotonTk _chipDps;
		private const float AnchoChipDps = 92f;

		/// <summary>Deja sitio real al texto del titulo ("Terrakeep", invariable, nunca se
		/// traduce - ver <c>Panel.Titulo</c> en los dos <c>.hjson</c>): medido con la fuente real a
		/// escala 1.15, ronda los 120px; 140 deja un respiro pequeño sin desperdiciar ancho.</summary>
		private const float LeftChips = 140f;
		private const float AnchoChipHora = 72f;

		/// <summary>
		/// El chip de objetivo enseña un rotulo CORTO fijo ("Objetivo"/"Objective"), nunca el
		/// nombre real del tramo - ver <see cref="RefrescarCabeceraChips"/> para el porque (nombres
		/// reales medidos hasta 36 caracteres, ver <c>Guia.Tramo.InicioModoDificil.Nombre</c> en el
		/// <c>.hjson</c>). 100px es de sobra para "Objetivo"/"Objective" a escala 0.72.
		/// </summary>
		private const float AnchoChipObjetivo = 100f;
		private const float SeparacionChips = 8f;

		/// <summary>
		/// Nunca mas alla de esta fraccion del ancho REAL del marco: la derecha de la fila del
		/// titulo la pinta el HUD de vida/mana del propio juego DESPUES de este Draw (ver el XMLdoc
		/// de <see cref="AltoTitulo"/> - es el mismo hallazgo real, documentado ahi, del que salio
		/// esta franja "prohibida"). No hay una coordenada exacta que pedirle al motor: el jugador
		/// puede elegir entre tres estilos de vida/mana en las opciones de Terraria (clasico/
		/// elegante/barras) con anchos distintos, asi que se deja un margen conservador en vez de
		/// una medida puntual que solo valdria para uno de los tres.
		/// </summary>
		private const float FraccionMaximaCabecera = 0.5f;
		private UIElement _contenidoActual;
		private AreaTerrakeep _area;

		private bool _medidasRegistradas;

		/// <summary>
		/// Fotogramas de fundido que le quedan al contenido recien montado (catalogo de rediseño
		/// visual, TM6). 120ms a 60 fps son 7,2 fotogramas; se redondea a 7 porque el juego corre a
		/// 60 fps fijos (<c>Main.instance.IsFixedTimeStep</c>) - no hace falta medir el tiempo real
		/// transcurrido, contar fotogramas basta y es el mismo criterio que ya usan las cuentas
		/// atras de mensaje temporal del resto del mod (p.ej. el resultado de auto-equipar en
		/// <c>ContenidoBuilds</c>).
		/// </summary>
		private int _fotogramasVelo;
		private const int FotogramasTransicionVelo = 7;

		/// <summary>Pestaña abierta la ultima vez. Reabrir el panel vuelve a donde estabas.</summary>
		public static AreaTerrakeep UltimaArea = AreaTerrakeep.Personaje;

		/// <summary>Fotogramas en los que se ha llegado a dibujar el objeto cogido con el raton.
		/// Lo leen las autopruebas para demostrar que esa ruta de dibujado se ejecuta de verdad.
		/// Antes habia un contador por panel; ahora hay uno, porque hay un dibujado.</summary>
		public static int FotogramasObjetoEnRaton;

		/// <summary>Area abierta ahora mismo.</summary>
		public AreaTerrakeep AreaActual => _area;

		/// <summary>El contenido montado en la pestaña abierta, sea del tipo que sea.</summary>
		public UIElement ContenidoActual => _contenidoActual;

		/// <summary>La capa donde flotan los desplegables del panel. Ver
		/// <see cref="CapaSuperposicionTk"/> para el porque de que exista.</summary>
		public CapaSuperposicionTk CapaSuperposicion => _capaSuperposicion;

		/// <summary>SOLO PARA AUTOPRUEBAS: el texto que se ve ahora mismo en el chip de DPS de la
		/// cabecera (idea 2 del catalogo de funciones, ver <see cref="MedidorDanio"/>).</summary>
		public string ChipDpsParaPrueba => _chipDps != null ? _chipDps.Texto : "";

		/// <summary>El boton "Cerrar (tecla)" del pie. Expuesto para que la autoprueba pueda medir su
		/// rectangulo REAL y demostrar que ya no se solapa con un desplegable abierto.</summary>
		public BotonTk BotonCerrar => _botonCerrar;

		/// <summary>Los hijos del marco EN EL ORDEN EN QUE SE DIBUJAN (que es el orden de
		/// <c>Append</c>, ver <c>UIElement.DrawChildren</c>). La autoprueba lo recorre para demostrar
		/// que la capa de superposicion va despues del boton "Cerrar".</summary>
		public IEnumerable<UIElement> MarcoHijos =>
			_marco != null ? _marco.Children : new List<UIElement>();

		/// <summary>
		/// SOLO DIAGNOSTICO: el orden REAL en que el marco dibuja a sus hijos (que es el orden de
		/// <c>Append</c>, ver <c>UIElement.DrawChildren</c>) y la geometria ya calculada de cada uno.
		/// Lo lee la autoprueba para demostrar con datos, y no de palabra, que la capa de
		/// superposicion se dibuja DESPUES del boton "Cerrar" - o sea, por encima.
		/// </summary>
		public string DiagnosticoOrdenDibujado()
		{
			if (_marco == null) {
				return "(marco null)";
			}

			System.Text.StringBuilder texto = new System.Text.StringBuilder();
			int indice = 0;
			foreach (UIElement hijo in _marco.Children) {
				CalculatedStyle d = hijo.GetDimensions();
				string etiqueta = hijo.GetType().Name;
				if (ReferenceEquals(hijo, _botonCerrar)) {
					etiqueta = "BotonTk(Cerrar)";
				}
				else if (ReferenceEquals(hijo, _capaSuperposicion)) {
					etiqueta = "CapaSuperposicionTk" +
						(_capaSuperposicion.Ocupada ? " OCUPADA" : " vacia");
				}
				if (texto.Length > 0) {
					texto.Append(" | ");
				}
				texto.Append('[').Append(indice).Append("] ").Append(etiqueta)
					.Append(" x=").Append((int)d.X).Append(" y=").Append((int)d.Y)
					.Append(' ').Append((int)d.Width).Append('x').Append((int)d.Height);
				indice++;
			}
			return texto.ToString();
		}

		public ContenidoPersonaje Personaje => _contenidoActual as ContenidoPersonaje;
		public ContenidoLibreria Libreria => _contenidoActual as ContenidoLibreria;
		public ContenidoBuilds Builds => _contenidoActual as ContenidoBuilds;
		public ContenidoInvestigacion Investigacion => _contenidoActual as ContenidoInvestigacion;
		public ContenidoExploracion Exploracion => _contenidoActual as ContenidoExploracion;
		public ContenidoAjustes Ajustes => _contenidoActual as ContenidoAjustes;
		public ContenidoGuia Guia => _contenidoActual as ContenidoGuia;
		public ContenidoAlbum Album => _contenidoActual as ContenidoAlbum;

		public override void OnInitialize()
		{
			_marco = new UIPanel();
			_marco.Width.Set(0f, 0.96f);
			_marco.MaxWidth.Set(AnchoMaximo, 0f);
			_marco.Height.Set(0f, 0.94f);
			_marco.MaxHeight.Set(Main.screenHeight * FraccionAltoMaximoDePantalla, 0f);
			_marco.HAlign = 0.5f;
			_marco.VAlign = 0.5f;
			_marco.BackgroundColor = EstiloTk.FondoPanel;
			_marco.SetPadding(10f);
			Append(_marco);

			ConstruirTitulo();
			ConstruirBarraPestanas();

			// Los chips de la cabecera se cuelgan DESPUES de la barra de pestañas a proposito
			// (bug real, encontrado con la propia autoprueba del panel, ver bitacora.md): antes
			// vivian en ConstruirTitulo, y AutopruebaPanelUnico usa panel.BuscarPrimero&lt;BotonTk&gt;()
			// -el PRIMER BotonTk del arbol, sin mas criterio- para probar la animacion de hover de
			// "algun boton cualquiera" del panel. Con los chips colgados antes que las pestañas, ese
			// BuscarPrimero encontraba el chip de hora (informativo, Habilitado=false, nunca crece)
			// en vez de la pestaña "Character", y la prueba dejaba de demostrar lo que dice demostrar.
			// El orden de Append no cambia DONDE se dibuja cada uno (las posiciones son Left/Top
			// explicitos, no dependen del orden), asi que moverlos aqui abajo no cambia nada visible.
			ConstruirCabeceraChips();

			float arribaContenido = AltoTitulo + AltoBarraPestanas + 8f;
			_contenedor = new UIElement();
			_contenedor.Width.Set(0f, 1f);
			_contenedor.Top.Set(arribaContenido, 0f);
			_contenedor.Height.Set(-(arribaContenido + AltoPie), 1f);
			_marco.Append(_contenedor);

			ConstruirPie();

			// LA ULTIMA, y a proposito: es la capa donde flotan los desplegables (hoy el selector de
			// prefijo). UIElement.DrawChildren dibuja en el orden en que se hizo Append, asi que
			// colgarla despues del pie es lo que garantiza que un desplegable abierto quede POR ENCIMA
			// del boton "Cerrar" y de todo lo demas del marco - el bug que reporto el usuario con
			// captura era exactamente el contrario (el boton de cerrar tapando las opciones). Ocupa la
			// misma franja que _contenedor (ni el titulo, ni las pestañas, ni el pie), asi que un
			// desplegable acotado a ella no puede salirse del panel ni pisar el pie.
			_capaSuperposicion = new CapaSuperposicionTk();
			_capaSuperposicion.Width.Set(0f, 1f);
			_capaSuperposicion.Top.Set(arribaContenido, 0f);
			_capaSuperposicion.Height.Set(-(arribaContenido + AltoPie), 1f);
			_marco.Append(_capaSuperposicion);

			CambiarArea(UltimaArea, "reapertura");
		}

		/// <summary>La fila del titulo. El texto va pegado a la izquierda a proposito: la derecha de
		/// esa franja la ocupa el HUD de vida/mana del propio juego (ver <see cref="AltoTitulo"/>).</summary>
		private void ConstruirTitulo()
		{
			EtiquetaTk titulo = new EtiquetaTk(() => Idiomas.Texto("Panel.Titulo"), 1.15f, 300f, AltoTitulo);
			titulo.Left.Set(2f, 0f);
			titulo.Top.Set(0f, 0f);
			_marco.Append(titulo);
		}

		/// <summary>
		/// TM3 (catalogo de rediseño visual): dos chips junto al titulo, visibles desde cualquier
		/// pestaña - hora del mundo y el proximo objetivo de la Guia (pulsable: lleva directo a la
		/// pestaña Guia, mismo espiritu que la idea 8 del catalogo de funciones - "convertir los
		/// contadores en algo navegable").
		/// <para />
		/// <b>El tercer chip del catalogo original (vida/maná) se descarta a proposito, no por
		/// pereza.</b> Investigado antes de escribir una sola linea (disciplina de dos fases del
		/// proyecto): <see cref="AltoTitulo"/> ya documenta, con una captura real citada, que la
		/// MITAD DERECHA de esta misma fila la pinta el HUD de vida/mana del propio juego DESPUES de
		/// este <c>Draw</c> (<c>GUIBarsDraw</c>, tModLoader.dll real) - es la razon por la que el
		/// titulo del panel vive a la izquierda desde el principio. Un chip que repita ese mismo dato
		/// en ese mismo hueco no aporta nada (el jugador ya lo esta viendo, dibujado encima) y
		/// ademas reproduce el bug real ya documentado ahi (la barra de pestañas tapada por los
		/// corazones antes de que existiera <c>AltoTitulo</c>) en cuanto la resolucion es lo bastante
		/// pequeña como para que el panel llegue a tocar el borde de la pantalla. Los otros dos chips
		/// SI son seguros: se anclan con <c>Left</c> en PIXELES desde la izquierda (nunca con
		/// <c>HAlign</c>/fraccion), la misma tecnica con la que el resto del panel evita el bug real
		/// de "Left con fraccion + HAlign a la vez" (ver <c>PestanaConjuntos</c>), y solo se enseñan
		/// si de verdad hay hueco (<see cref="AjustarVisibilidadChips"/>).
		/// </summary>
		private void ConstruirCabeceraChips()
		{
			_chipHora = new BotonTk("", 0.72f);
			_chipHora.EsPestana = true;
			_chipHora.Habilitado = false; // Informativo, no se pulsa: sin animacion de "pulsable".
			_chipHora.Width.Set(AnchoChipHora, 0f);
			_chipHora.Height.Set(AltoTitulo, 0f);
			_chipHora.Left.Set(LeftChips, 0f);
			_chipHora.Top.Set(0f, 0f);
			_chipHora.Ayuda = () => Idiomas.Texto("Panel.Cabecera.AyudaHora");
			_marco.Append(_chipHora);

			_chipObjetivo = new BotonTk("", 0.72f);
			_chipObjetivo.EsPestana = true;
			_chipObjetivo.Width.Set(AnchoChipObjetivo, 0f);
			_chipObjetivo.Height.Set(AltoTitulo, 0f);
			_chipObjetivo.Left.Set(LeftChips + AnchoChipHora + SeparacionChips, 0f);
			_chipObjetivo.Top.Set(0f, 0f);
			_chipObjetivo.Ayuda = () => Idiomas.Texto("Panel.Cabecera.AyudaObjetivo", _objetivoActualParaAyuda);
			_chipObjetivo.AlPulsar += () => CambiarArea(AreaTerrakeep.Guia, "clic en el chip de objetivo de la Guía");
			_marco.Append(_chipObjetivo);

			_chipDps = new BotonTk("", 0.72f);
			_chipDps.EsPestana = true;
			_chipDps.Habilitado = false; // Informativo, no se pulsa.
			_chipDps.Width.Set(AnchoChipDps, 0f);
			_chipDps.Height.Set(AltoTitulo, 0f);
			_chipDps.Left.Set(LeftChips + AnchoChipHora + SeparacionChips + AnchoChipObjetivo + SeparacionChips, 0f);
			_chipDps.Top.Set(0f, 0f);
			_chipDps.Ayuda = () => Idiomas.Texto("Panel.Cabecera.AyudaDps");
			_marco.Append(_chipDps);
		}

		/// <summary>Texto actual de los dos chips - se pide en cada fotograma desde
		/// <see cref="RefrescarTextos"/>, mismo criterio que el resto del panel (nada se queda
		/// desfasado por no haberse enganchado a un evento).</summary>
		private void RefrescarCabeceraChips()
		{
			if (_chipHora != null) {
				float horaFloat = Terraria.Utils.GetDayTimeAs24FloatStartingFromMidnight() % 24f;
				if (horaFloat < 0f) {
					horaFloat += 24f;
				}
				int hora = (int)horaFloat;
				int minuto = (int)((horaFloat - hora) * 60f);
				_chipHora.FijarTexto(hora.ToString("00") + ":" + minuto.ToString("00"));
			}

			if (_chipObjetivo != null) {
				// El rotulo del chip es SIEMPRE "Objetivo"/"Objective" - fijo, corto, nunca se
				// recorta. El nombre real del tramo (que puede ser largo, ver el XMLdoc de
				// AnchoChipObjetivo) se guarda aparte para el tooltip (Ayuda, fijado en
				// ConstruirCabeceraChips) y para el clic.
				_chipObjetivo.FijarTexto(Idiomas.Texto("Panel.Cabecera.ObjetivoCorto"));

				TramoGuia tramo;
				EstadoGuia.PasoActual(out tramo);
				if (tramo != null) {
					_objetivoActualParaAyuda = Idiomas.Texto("Panel.Cabecera.Objetivo", tramo.Nombre());
				}
				else if (EstadoJugadorGuia.HayPartida) {
					_objetivoActualParaAyuda = Idiomas.Texto("Panel.Cabecera.GuiaCompleta");
				}
				else {
					_objetivoActualParaAyuda = Idiomas.Texto("Panel.Cabecera.SinPartida");
				}
			}

			if (_chipDps != null) {
				if (MedidorDanio.HayDatosRecientes()) {
					int dps = (int)System.Math.Round(MedidorDanio.DpsUltimos10s());
					_chipDps.FijarTexto(Idiomas.Texto("Panel.Cabecera.Dps", dps));
				}
				else {
					_chipDps.FijarTexto(Idiomas.Texto("Panel.Cabecera.DpsSinDatos"));
				}
			}
		}

		/// <summary>Nombre completo (posiblemente largo) del objetivo actual de la Guia, o el
		/// estado que toque ("Guía completa"/"Sin partida"). Lo lee el tooltip de
		/// <see cref="_chipObjetivo"/> - ver <see cref="RefrescarCabeceraChips"/>.</summary>
		private string _objetivoActualParaAyuda = "";

		/// <summary>
		/// Esconde los dos chips (ancho a 0, en vez de dejarlos dibujar fuera de su franja segura)
		/// cuando el marco es tan estrecho que invadirian la mitad derecha reservada al HUD de
		/// vida/mana - ver <see cref="FraccionMaximaCabecera"/>. Mismo patron de "la caja crece o se
		/// esconde, el texto nunca se recorta ni invade sitio ajeno" que ya usa el resto del panel.
		/// </summary>
		private void AjustarVisibilidadChips()
		{
			if (_chipHora == null || _chipObjetivo == null || _chipDps == null || _marco == null) {
				return;
			}

			float anchoMarco = _marco.GetDimensions().Width;
			float necesario = LeftChips + AnchoChipHora + SeparacionChips + AnchoChipObjetivo +
				SeparacionChips + AnchoChipDps;
			bool hayHueco = anchoMarco > 0f && necesario <= anchoMarco * FraccionMaximaCabecera;

			float anchoHora = hayHueco ? AnchoChipHora : 0f;
			float anchoObjetivo = hayHueco ? AnchoChipObjetivo : 0f;
			float anchoDps = hayHueco ? AnchoChipDps : 0f;
			if (Math.Abs(_chipHora.Width.Pixels - anchoHora) > 0.5f ||
				Math.Abs(_chipObjetivo.Width.Pixels - anchoObjetivo) > 0.5f ||
				Math.Abs(_chipDps.Width.Pixels - anchoDps) > 0.5f) {
				_chipHora.Width.Set(anchoHora, 0f);
				_chipObjetivo.Width.Set(anchoObjetivo, 0f);
				_chipDps.Width.Set(anchoDps, 0f);
				_marco.Recalculate();
			}
		}

		private void ConstruirBarraPestanas()
		{
			string[] nombres = NombresDeArea;
			float fraccion = 1f / nombres.Length;

			for (int i = 0; i < nombres.Length; i++) {
				AreaTerrakeep area = (AreaTerrakeep)i;
				BotonTk boton = new BotonTk(nombres[i], EstiloTk.EscalaPestana);
				boton.EsPestana = true;
				boton.Width.Set(-EstiloTk.SeparacionPestanas, fraccion);
				boton.Height.Set(AltoBarraPestanas, 0f);
				boton.Left.Set(0f, i * fraccion);
				boton.Top.Set(AltoTitulo, 0f);
				boton.Ayuda = () => AyudaDeArea(area) + "\n" +
					Idiomas.Texto("Panel.Atajo", PanelTerrakeepSystem.TeclaDe(area));
				boton.AlPulsar += () => CambiarArea(area, "clic en la pestaña");
				_botonesPestana.Add(boton);
				_marco.Append(boton);
			}
		}

		private void ConstruirPie()
		{
			// Sin repetir "Terrakeep": ya esta arriba, en la fila del titulo.
			EtiquetaTk ayuda = new EtiquetaTk(() => AyudaDeArea(_area), 0.75f, 820f, 22f);
			ayuda.ColorTexto = EstiloTk.TextoSuave;
			ayuda.Left.Set(2f, 0f);
			ayuda.VAlign = 1f;
			_marco.Append(ayuda);

			_botonCerrar = new BotonTk("", EstiloTk.EscalaBoton);
			_botonCerrar.Width.Set(160f, 0f);
			_botonCerrar.Height.Set(34f, 0f);
			_botonCerrar.HAlign = 1f;
			_botonCerrar.VAlign = 1f;
			_botonCerrar.AlPulsar += () => PanelTerrakeepSystem.CerrarPanel("botón Cerrar");
			_marco.Append(_botonCerrar);
		}

		/// <summary>Nombre interno (invariable) de cada area. Es la ULTIMA parte de su clave de
		/// localizacion, no un texto que se enseñe: <c>Panel.Area.&lt;clave&gt;</c> y
		/// <c>Panel.Ayuda.&lt;clave&gt;</c>.</summary>
		public static readonly string[] ClavesDeArea = {
			"Personaje", "Libreria", "Builds", "Investigacion", "Exploracion", "Ajustes", "Guia", "Album"
		};

		/// <summary>
		/// Nombres de las seis pestañas, ya traducidos al idioma activo, en el orden de la
		/// aplicacion de escritorio.
		/// <para />
		/// Es una <b>propiedad</b> y no un array fijo a proposito: los nombres cambian con el
		/// idioma, y un <c>static readonly string[]</c> se quedaria con los que hubiera al cargar
		/// el mod. El coste son seis <c>Language.GetTextValue</c>, que es lo mismo que ya paga
		/// cualquier <see cref="EtiquetaTk"/> del panel en cada fotograma.
		/// </summary>
		public static string[] NombresDeArea {
			get {
				string[] nombres = new string[ClavesDeArea.Length];
				for (int i = 0; i < nombres.Length; i++) {
					nombres[i] = NombreDeArea((AreaTerrakeep)i);
				}
				return nombres;
			}
		}

		/// <summary>Nombre traducido de una pestaña.</summary>
		public static string NombreDeArea(AreaTerrakeep area)
		{
			int indice = (int)area;
			if (indice < 0 || indice >= ClavesDeArea.Length) {
				return "";
			}
			return Idiomas.Texto("Panel.Area." + ClavesDeArea[indice]);
		}

		/// <summary>La linea de ayuda que se ve en el pie con cada area abierta. Es donde han ido a
		/// parar las notas que antes repetia cada panel dentro de su propia cabecera.</summary>
		public static string AyudaDeArea(AreaTerrakeep area)
		{
			int indice = (int)area;
			if (indice < 0 || indice >= ClavesDeArea.Length) {
				return "";
			}
			return Idiomas.Texto("Panel.Ayuda." + ClavesDeArea[indice]);
		}

		/// <summary>Cambia de pestaña. Publico: lo usan la barra, los atajos directos y las
		/// autopruebas.</summary>
		public void CambiarArea(AreaTerrakeep area, string origen)
		{
			int indice = (int)area;
			if (indice < 0 || indice >= NombresDeArea.Length) {
				return;
			}

			_area = area;
			UltimaArea = area;

			for (int i = 0; i < _botonesPestana.Count; i++) {
				_botonesPestana[i].Activo = i == indice;
			}

			RefrescarTextos();

			// Un desplegable abierto pertenece al contenido que se va a tirar, pero vive en la capa
			// de superposicion (que NO se tira al cambiar de pestaña): sin esto se quedaria flotando
			// huerfano encima de la pestaña nueva.
			if (_capaSuperposicion != null) {
				_capaSuperposicion.Quitar(null);
			}

			if (_contenidoActual != null) {
				_contenedor.RemoveChild(_contenidoActual);
				_contenidoActual = null;
			}

			// Cada area se construye de cero al entrar en ella, igual que hacian los seis paneles
			// sueltos al abrirse: es la unica forma de garantizar que ninguna referencia se quede
			// apuntando a los arrays de otra partida (el juego reasigna Player.inventory y
			// compañia al cargar otro personaje).
			_contenidoActual = CrearContenido(area);
			if (_contenidoActual != null) {
				_contenidoActual.Width.Set(0f, 1f);
				_contenidoActual.Height.Set(0f, 1f);
				_contenedor.Append(_contenidoActual);
			}

			_contenedor.Recalculate();

			// TM6 (catalogo de rediseño visual): el contenido nuevo entra con un fundido de 120ms en
			// vez de un corte seco. Se dibuja como un velo por encima en Draw() (ver
			// FotogramasTransicionVelo), no como un UIElement mas: un panel interactivo por encima
			// del contenido nuevo le robaria el clic durante esos 7 fotogramas (los eventos de
			// UIElement resuelven sobre el hijo mas reciente primero), y esto es solo dibujado.
			_fotogramasVelo = FotogramasTransicionVelo;

			PanelTerrakeepSystem.RegistrarEnArea(area,
				Terrakeep.LogTag + " Pestaña activa: \"" + NombreDeArea(area) + "\" (via " + origen + ").");
		}

		/// <summary>
		/// Vuelve a pedir todos los textos propios del marco (las seis pestañas, sus tooltips y el
		/// boton de cerrar). Se llama en cada <c>Update</c>, no al recibir el evento de cambio de
		/// idioma: es el mismo criterio con el que se hicieron <see cref="EtiquetaTk"/> y el area
		/// de Ajustes, y evita depender de que el enganche llegue a hacerse (WS6 documento que un
		/// elemento colgado con el <c>UIState</c> ya activo se puede saltar
		/// <c>OnInitialize</c>/<c>OnActivate</c>).
		/// </summary>
		private void RefrescarTextos()
		{
			for (int i = 0; i < _botonesPestana.Count; i++) {
				_botonesPestana[i].FijarTexto(NombreDeArea((AreaTerrakeep)i));
			}

			AjustarEscalaDeLasPestanas();

			if (_botonCerrar != null) {
				_botonCerrar.FijarTexto(Idiomas.Texto("Panel.Cerrar", PanelTerrakeepSystem.TeclaDe(_area)));
			}

			RefrescarCabeceraChips();
			AjustarVisibilidadChips();
		}

		/// <summary>
		/// Baja la escala del texto de TODAS las pestañas lo justo para que el rotulo mas largo
		/// quepa entero en su boton, midiendolo con la fuente real del juego.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Hizo falta al pasar de seis pestañas a siete: cada una baja de 1/6 a 1/7 del ancho del
		/// marco, y "Investigación" (el rotulo mas largo en español; en ingles lo es "Exploration")
		/// ya no cabia a la escala fija de 0,8. La regla del proyecto es que <b>ningun texto se
		/// recorta</b>: lo que se adapta es el layout.
		/// </para>
		/// <para>
		/// Se mide en cada fotograma y no una sola vez a proposito, porque las tres cosas de las que
		/// depende cambian en vivo: el IDIOMA (los rotulos son otros), la RESOLUCION y la ESCALA DE
		/// INTERFAZ del jugador (el ancho del boton es una fraccion del marco). Cuesta siete
		/// <c>MeasureString</c>, lo mismo que ya paga cualquier <see cref="EtiquetaTk"/> del panel.
		/// La escala es COMUN a las siete: con una por pestaña, las de rotulo corto se verian mas
		/// grandes que las de al lado y la barra pareceria rota.
		/// </para>
		/// </remarks>
		private void AjustarEscalaDeLasPestanas()
		{
			float escala = EstiloTk.EscalaPestana;

			for (int i = 0; i < _botonesPestana.Count; i++) {
				BotonTk boton = _botonesPestana[i];
				float ancho = boton.GetDimensions().Width;
				if (ancho <= 0f || string.IsNullOrEmpty(boton.Texto)) {
					continue;
				}

				float anchoTexto =
					Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(boton.Texto).X;
				if (anchoTexto <= 0f) {
					continue;
				}

				// 8 px de respiro a cada lado; el texto ademas crece un 6% con la animacion de
				// hover (ver BotonTk.DibujarTexto), asi que se reserva ese margen tambien.
				float cabe = (ancho - 16f) / (anchoTexto * 1.06f);
				if (cabe < escala) {
					escala = cabe;
				}
			}

			// Suelo duro: por debajo de esto la fuente del juego se vuelve ilegible, y es preferible
			// que se note que algo no cabe a enseñar un renglon borroso.
			if (escala < 0.55f) {
				escala = 0.55f;
			}

			for (int i = 0; i < _botonesPestana.Count; i++) {
				_botonesPestana[i].EscalaTexto = escala;
			}

			EscalaDeLasPestanas = escala;
		}

		/// <summary>Escala de texto que ha quedado en la barra de pestañas. La lee la autoprueba
		/// para demostrar con un numero que los siete rotulos caben de verdad.</summary>
		public float EscalaDeLasPestanas { get; private set; } = EstiloTk.EscalaPestana;

		private static UIElement CrearContenido(AreaTerrakeep area)
		{
			switch (area) {
				case AreaTerrakeep.Personaje:
					return new ContenidoPersonaje();
				case AreaTerrakeep.Libreria:
					// El catalogo se construye la primera vez que hace falta de verdad, no al cargar
					// el mod: quien no abra la Libreria no paga el recorrido de los ~8000 objetos.
					ArbolLibreria.ConstruirSiHaceFalta();
					return new ContenidoLibreria();
				case AreaTerrakeep.Builds:
					return new ContenidoBuilds();
				case AreaTerrakeep.Investigacion:
					return new ContenidoInvestigacion();
				case AreaTerrakeep.Exploracion:
					return new ContenidoExploracion();
				case AreaTerrakeep.Ajustes:
					return new ContenidoAjustes();
				case AreaTerrakeep.Guia:
					return new ContenidoGuia();
				case AreaTerrakeep.Album:
					return new ContenidoAlbum();
				default:
					return null;
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			RefrescarTextos();
			RehacerAreaSiCambioElIdioma();
			MantenerInventarioAbierto();
			AjustarAltoMaximo();

			if (_fotogramasVelo > 0) {
				_fotogramasVelo--;
			}

			// Si el personaje desaparece (muerte con partida en modo extremo, salir al menu...) el
			// panel no tiene nada que editar y se cierra solo antes de petar leyendo nulos.
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				PanelTerrakeepSystem.CerrarPanel("el jugador ha dejado de estar disponible");
			}
		}

		/// <summary>
		/// TM6 (catalogo de rediseño visual): tope de alto REAL del marco al 82% de
		/// <c>Main.screenHeight</c> (antes era un fijo de 700px que en un portatil 1366x768 ocupaba
		/// el 91% de la pantalla y tapaba el HUD de vida, ver captura real documentada en la
		/// bitacora). Se recalcula cada fotograma con el mismo criterio que ya usa
		/// <see cref="AjustarEscalaDeLasPestanas"/> para el ancho: la resolucion puede cambiar en
		/// vivo (redimensionar la ventana, pasar a pantalla completa), y el coste es una simple
		/// multiplicacion.
		/// </summary>
		private void AjustarAltoMaximo()
		{
			if (_marco == null) {
				return;
			}

			float nuevo = Main.screenHeight * FraccionAltoMaximoDePantalla;
			if (System.Math.Abs(nuevo - _marco.MaxHeight.Pixels) < 0.5f) {
				return;
			}

			_marco.MaxHeight.Set(nuevo, 0f);

			// UIElement.MaxHeight.Set(...) por si solo no actualiza nada visible hasta el proximo
			// Recalculate real - mismo bug real ya documentado y arreglado en
			// ContenidoBuilds.RecalcularPildorasYFilas para Width/Left/Top. Se evita llamarlo todos
			// los fotogramas (la resolucion normalmente NO cambia entre uno y el siguiente) con la
			// comparacion de arriba.
			_marco.Recalculate();
		}

		/// <summary>
		/// Si el idioma ha cambiado desde que se monto la pestaña, se vuelve a montar entera.
		/// <para />
		/// Casi todo el texto del mod sale de una <see cref="EtiquetaTk"/> o se refresca en su
		/// <c>Update</c>, asi que cambia solo. Pero hay cosas que se construyen UNA vez y no se
		/// vuelven a tocar: las pildoras de Builds, las filas del arbol de la Libreria, la lista
		/// de objetivos de Exploracion. Rehacer la pestaña es lo mismo que hace ya cualquier clic
		/// en la barra, cuesta un fotograma, y solo pasa cuando alguien cambia de idioma de verdad.
		/// Se hace <b>aqui</b>, en el marco, y no en cada area: asi las seis quedan cubiertas.
		/// </summary>
		private void RehacerAreaSiCambioElIdioma()
		{
			string cultura = Idiomas.CulturaActiva;
			if (_culturaDelContenido == null) {
				_culturaDelContenido = cultura;
				return;
			}
			if (_culturaDelContenido == cultura) {
				return;
			}

			_culturaDelContenido = cultura;
			CambiarArea(_area, "cambio de idioma en vivo");
		}

		/// <summary>Cultura con la que se monto el contenido de la pestaña abierta.</summary>
		private string _culturaDelContenido;

		/// <summary>
		/// Deja <c>Main.playerInventory</c> a true mientras el panel esta abierto.
		/// <para />
		/// Suena raro (el panel es a pantalla completa y tapa el inventario) pero es
		/// IMPRESCINDIBLE, y es un fallo real que se vio en el juego construyendo WS1, no una
		/// precaucion teorica: <c>IngameFancyUI.OpenUIState</c> pone
		/// <c>Main.playerInventory = false</c>, y <c>Player.dropItemCheck</c> (que corre cada tick)
		/// tiene esto:
		/// <code>
		/// if (Main.mouseItem.type > 0 &amp;&amp; !Main.playerInventory) {
		///     ... GetItem de vuelta al inventario, y al suelo lo que no quepa ...
		///     Main.mouseItem = new Item();
		/// }
		/// </code>
		/// O sea: con el panel abierto tal cual, cualquier objeto que cojas de una ranura se te
		/// devuelve solo al inventario (o cae al suelo si esta lleno) en el tick siguiente, y
		/// arrastrar de una ranura a otra es literalmente imposible.
		/// <para />
		/// Poner el campo a true no dibuja el inventario de vanilla: la capa
		/// <c>"Vanilla: Inventory"</c> es la 27 y la lista de capas se corta en la 12
		/// (<c>"Vanilla: Fancy UI"</c>). Ademas <c>IngameFancyUI.Close()</c> ya lo deja a true al
		/// cerrar por su cuenta, asi que no hay nada que restaurar.
		/// </summary>
		public static void MantenerInventarioAbierto()
		{
			Main.playerInventory = true;
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			// Mismo motivo que el reseteo real de Main.hoverItemName en DrawInterface_26_InterfaceLogic3
			// (tambien detras de la capa 12 "Vanilla: Fancy UI", tambien saltado con el panel abierto):
			// sin esto, el ultimo objeto sobre el que paso el raton se quedaria pegado en el tooltip
			// para siempre en vez de desaparecer al apartar el raton de toda ranura. Va ANTES de
			// base.Draw para que los SlotObjetoVanilla de este fotograma (que llaman a
			// ItemSlot.Handle, y ese SI rellena Main.hoverItemName/Main.HoverItem con normalidad -
			// vive en ItemSlot.cs, no en esa capa) puedan volver a rellenarlo si el raton esta encima.
			Main.hoverItemName = "";

			// SOLO arnes de pruebas: sin esto, Main.mouseX/Main.mouseY que fija
			// AutopruebaTooltipObjeto no sobrevivian ni a un fotograma - confirmado con dos rondas
			// de log real, no un supuesto. Ni UpdateUI ni PostUpdateInput (los dos probados antes)
			// bastaban: algo entre esos hooks y este Draw vuelve a sondear el raton real (0,0 sin
			// sesion de escritorio con raton fisico) y lo pisa. Aqui, ENCIMA del propio Draw y antes
			// de que los SlotObjetoVanilla del fotograma lean Main.MouseScreen, es el unico punto
			// que garantiza que nada se cuela en medio. Con la autopreuba apagada (variable de
			// entorno sin poner) esta llamada no hace nada.
			AutopruebaTooltipObjeto.ReafirmarRaton();
			AutopruebaTooltipPestana.ReafirmarRaton();

			base.Draw(spriteBatch);

			DibujarVeloDeTransicion(spriteBatch);
			DibujarObjetoEnRaton(spriteBatch);
			DibujarTooltipDeObjeto();

			// El tooltip de un BotonTk (pestañas, pildoras, acciones) con fondo propio - mismo
			// motivo que los dos de arriba: se aplaza hasta aqui para quedar encima de TODO el
			// arbol, barra de pestañas incluida. Arregla el hallazgo KeepQA del 14-sep-2026 (ver
			// bitacora.md): el tooltip vainilla de la barra de pestañas, sin panel de fondo,
			// tapaba contenido real a ventana pequeña.
			BotonTk.DibujarTooltipPendiente(spriteBatch);

			RegistrarMedidasUnaVez();
		}

		/// <summary>
		/// TM6 (catalogo de rediseño visual): el velo de fundido de la pestaña recien abierta. Se
		/// dibuja AQUI, despues de <c>base.Draw</c> (el contenido nuevo ya esta pintado debajo, no
		/// hay que esperar a que aparezca), como un simple rectangulo del mismo color que
		/// <see cref="EstiloTk.FondoPanel"/> que decae de opaco a invisible en
		/// <see cref="FotogramasTransicionVelo"/> fotogramas. Con <c>SpriteBatch</c> en
		/// <c>AlphaBlend</c> (el modo real con el que dibuja toda la interfaz del juego), un alpha
		/// que baja a cero hace que el rectangulo deje de aportar nada al resultado sin importar su
		/// color - el efecto visible es exactamente un fundido del contenido nuevo, sin tener que
		/// tocar el color de cada widget heterogeneo que pueda montar cualquiera de las ocho areas
		/// (ranuras de objeto vainilla incluidas, cuyo dibujado no es propio del mod). Cubre solo
		/// <see cref="_contenedor"/> (el area de la pestaña activa) - nunca el titulo, la barra de
		/// pestañas ni el pie, que no cambian con la transicion.
		/// </summary>
		private void DibujarVeloDeTransicion(SpriteBatch spriteBatch)
		{
			if (_fotogramasVelo <= 0 || _contenedor == null) {
				return;
			}

			CalculatedStyle dim = _contenedor.GetDimensions();
			if (dim.Width <= 0f || dim.Height <= 0f) {
				return;
			}

			float fraccion = _fotogramasVelo / (float)FotogramasTransicionVelo;
			Rectangle rect = new Rectangle((int)dim.X, (int)dim.Y, (int)dim.Width, (int)dim.Height);
			spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, rect, EstiloTk.FondoPanel * fraccion);
		}

		/// <summary>
		/// Dibuja el objeto que el jugador lleva "cogido" con el raton.
		/// <para />
		/// Hace falta hacerlo a mano por un detalle real del motor: la interfaz del juego es una
		/// lista de capas que se recorre hasta que una devuelve false, y la capa
		/// <c>"Vanilla: Fancy UI"</c> (indice 12) devuelve false SIEMPRE que hay un panel abierto
		/// con <c>IngameFancyUI</c>. La capa que dibuja el objeto cogido,
		/// <c>DrawInterface_38_MouseCarriedObject</c>, va despues (indice 38), asi que nunca llega
		/// a ejecutarse. Sin esto, coger un objeto de una ranura lo haria desaparecer de la vista
		/// hasta soltarlo. El codigo es el mismo que el de esa capa vanilla, incluido el cambio
		/// temporal de <c>Main.inventoryScale</c> a <c>Main.cursorScale</c>.
		/// </summary>
		private static void DibujarObjetoEnRaton(SpriteBatch spriteBatch)
		{
			if (Main.mouseItem == null || Main.mouseItem.type <= 0 || Main.mouseItem.stack <= 0) {
				return;
			}

			float escalaPrevia = Main.inventoryScale;
			Main.inventoryScale = Main.cursorScale;
			ItemSlot.Draw(spriteBatch, ref Main.mouseItem, ItemSlot.Context.MouseItem,
				new Vector2(Main.mouseX, Main.mouseY));
			Main.inventoryScale = escalaPrevia;

			FotogramasObjetoEnRaton++;
		}

		/// <summary>
		/// Dibuja el tooltip vainilla (nombre, prefijo, rareza, stats - todo lo que ya sabe pintar
		/// el propio juego) del objeto que el raton tiene encima en cualquier ranura del panel.
		/// <para />
		/// El bug real, reportado probando el mod en el juego: ninguna ranura de ninguna pestaña
		/// (Inventario, Almacenes, Equipo, Libreria) enseñaba tooltip al pasar el raton por encima,
		/// pese a que <see cref="TerrakeepMod.UI.SlotObjetoVanilla"/> SI llama a
		/// <c>ItemSlot.Handle</c> de verdad. La causa, confirmada decompilando el
		/// <c>tModLoader.dll</c> instalado con <c>ilspycmd</c> (nunca la referencia vieja de
		/// <c>tModLoader-Decompiled\</c>, es una version distinta): <c>ItemSlot.Handle</c> SI rellena
		/// <c>Main.HoverItem</c>/<c>Main.hoverItemName</c> con toda normalidad - vive en
		/// <c>ItemSlot.cs</c>, ajeno a la lista de capas - pero <b>nadie los pinta</b>. En vainilla
		/// eso lo hace <c>DrawInterface_33_MouseText</c> (que ademas es quien resetea
		/// <c>hoverItemName</c> cada fotograma desde <c>DrawInterface_26_InterfaceLogic3</c>, capa
		/// 26), y las dos son capas MUY posteriores a la 12 ("Vanilla: Fancy UI"), que es donde el
		/// recorrido de capas se corta en seco en cuanto hay un panel de <c>IngameFancyUI</c>
		/// abierto - el mismo hueco exacto que ya obligo a reescribir <see cref="DibujarObjetoEnRaton"/>
		/// para la capa 38 del objeto cogido con el raton.
		/// <para />
		/// <b>Ojo</b>: esto NO significa reimplementar el dibujado del tooltip. El propio
		/// <c>tModLoader.dll</c> ya reparte esa capa 12
		/// (<c>DrawInterface_12_IngameFancyUI</c>, real, decompilado) asi:
		/// <code>
		/// InGameUI.Draw(spriteBatch, gameTime);              // dibuja ESTE panel (aqui)
		/// if (inFancyUI &amp;&amp; !IngameFancyUI.Draw(spriteBatch, gameTime)) {
		///     DrawPendingMouseText();                        // pinta lo que haya en la cola
		/// }
		/// </code>
		/// O sea que <c>DrawPendingMouseText()</c> (el que de verdad pone pixeles en pantalla) YA
		/// se llama solo, cada fotograma, justo despues de que este <c>Draw</c> termine. Lo unico
		/// que falta es meter algo en su cola, y eso es exactamente lo que hace
		/// <c>Main.MouseText(...)</c> (publica, no dibuja nada por si sola: solo rellena
		/// <c>_mouseTextCache</c>) - es la misma llamada, con los mismos argumentos, que hace el
		/// propio <c>DrawInterface_33_MouseText</c> real. El parametro <c>rare</c> que se le pasa es
		/// irrelevante para un tooltip de objeto: <c>MouseText_DrawItemTooltip</c> (tambien real, ya
		/// decompilada) lo pisa enseguida con <c>HoverItem.rare</c>, asi que la rareza/el prefijo/las
		/// stats que se ven son siempre los del objeto real, gratis, sin tocarlos a mano.
		/// </summary>
		private static void DibujarTooltipDeObjeto()
		{
			// Mismo saneo que hace el propio DrawInterface_33_MouseText antes de mirar hoverItemName.
			if (Main.mouseItem.stack <= 0) {
				Main.mouseItem.type = 0;
			}

			if (string.IsNullOrEmpty(Main.hoverItemName) || Main.mouseItem.type != 0) {
				return;
			}

			Main.LocalPlayer.cursorItemIconEnabled = false;

			if (Main.SettingsEnabled_OpaqueBoxBehindTooltips) {
				Main.instance.MouseText(Main.hoverItemName, Main.rare, 0, Main.mouseX + 6, Main.mouseY + 6);
			}
			else {
				Main.instance.MouseText(Main.hoverItemName, Main.rare, 0);
			}

			Main.mouseText = true;
		}

		/// <summary>Si al cerrar el panel queda un objeto cogido con el raton, se devuelve al
		/// inventario. Fuera del panel ese objeto no se dibuja en ningun sitio, asi que dejarlo
		/// ahi seria perderlo de vista.</summary>
		public static void DevolverObjetoDelRaton()
		{
			if (Main.mouseItem == null || Main.mouseItem.IsAir) {
				return;
			}

			// Hay que describirlo ANTES: Player.GetItem consume el objeto que se le pasa (le deja
			// la pila a cero y devuelve lo que no ha cabido), asi que despues ya seria "(vacio)".
			string descripcion = Common.Personaje.PersonajeVivo.DescribirObjeto(Main.mouseItem);

			Item sobrante = Main.LocalPlayer.GetItem(Main.myPlayer, Main.mouseItem,
				GetItemSettings.InventoryUIToInventorySettings);
			Main.mouseItem = sobrante;

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Al cerrar habia un objeto cogido con el raton ({descripcion}); " +
				$"devuelto al inventario. Sobrante: {Common.Personaje.PersonajeVivo.DescribirObjeto(sobrante)}.");
		}

		/// <summary>Deja en el log, una sola vez por apertura, las medidas reales del panel ya
		/// dibujado. Es la evidencia de que esta puesto de verdad en pantalla y no solo construido
		/// en memoria - mismo criterio con el que se cerraron todos los workstreams.</summary>
		private void RegistrarMedidasUnaVez()
		{
			if (_medidasRegistradas || _marco == null) {
				return;
			}

			CalculatedStyle dim = _marco.GetDimensions();
			if (dim.Width <= 0f) {
				return;
			}

			_medidasRegistradas = true;
			PanelTerrakeepSystem.RegistrarEnArea(_area,
				$"{Terrakeep.LogTag} Panel de Terrakeep dibujado. Marco en coordenadas de pantalla: " +
				$"x={(int)dim.X} y={(int)dim.Y} w={(int)dim.Width} h={(int)dim.Height}. " +
				$"Resolucion {Main.screenWidth}x{Main.screenHeight}, escala de interfaz {Main.UIScale}. " +
				$"Pestaña abierta: \"{NombresDeArea[(int)_area]}\". " +
				$"Barra de pestañas: {DescribirBarra()}");
		}

		private string DescribirBarra()
		{
			System.Text.StringBuilder texto = new System.Text.StringBuilder();
			for (int i = 0; i < _botonesPestana.Count; i++) {
				CalculatedStyle d = _botonesPestana[i].GetDimensions();
				texto.Append($"[\"{_botonesPestana[i].Texto}\" x={(int)d.X} y={(int)d.Y} " +
					$"{(int)d.Width}x{(int)d.Height}{(_botonesPestana[i].Activo ? " ACTIVA" : "")}] ");
			}
			return texto.ToString();
		}

		/// <summary>Primer elemento del panel del tipo pedido, buscando por todo el arbol de la
		/// interfaz. Lo usan las autopruebas para llegar a los controles propios sin tener que
		/// exponerlos uno a uno.</summary>
		public T BuscarPrimero<T>() where T : UIElement
		{
			T encontrado = null;
			ExecuteRecursively(elemento => {
				if (encontrado == null && elemento is T) {
					encontrado = (T)elemento;
				}
			});
			return encontrado;
		}

		/// <summary>
		/// Pulsa DE VERDAD el boton cuyo texto empiece por <paramref name="texto"/>, disparando su
		/// <c>OnLeftClick</c> con un <see cref="UIMouseEvent"/> colocado en su centro real de
		/// pantalla: el mismo camino que recorre un clic de raton una vez que
		/// <c>UserInterface</c> ha resuelto sobre que elemento cae.
		/// </summary>
		public string PulsarBoton(string texto)
		{
			BotonTk encontrado = null;
			ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (encontrado == null && boton != null && boton.Texto != null && boton.Texto.StartsWith(texto)) {
					encontrado = boton;
				}
			});

			if (encontrado == null) {
				return null;
			}

			CalculatedStyle dim = encontrado.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			encontrado.LeftClick(new UIMouseEvent(encontrado, centro));

			return "\"" + encontrado.Texto + "\" (habilitado=" + encontrado.Habilitado +
				", en x=" + (int)dim.X + " y=" + (int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height +
				", clic en " + (int)centro.X + "," + (int)centro.Y + ")";
		}

		/// <summary>
		/// Pulsa de verdad la pestaña indicada de la barra principal, por la ruta real del clic.
		/// La usa la verificacion final para demostrar que las seis pestañas se cambian con el
		/// raton y no solo llamando al metodo por dentro.
		/// </summary>
		public string PulsarPestana(AreaTerrakeep area)
		{
			int indice = (int)area;
			if (indice < 0 || indice >= _botonesPestana.Count) {
				return null;
			}

			BotonTk boton = _botonesPestana[indice];
			CalculatedStyle dim = boton.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			boton.LeftClick(new UIMouseEvent(boton, centro));

			return "\"" + boton.Texto + "\" (en x=" + (int)dim.X + " y=" + (int)dim.Y + " " +
				(int)dim.Width + "x" + (int)dim.Height + ", clic en " + (int)centro.X + "," + (int)centro.Y + ")";
		}

		/// <summary>Recuento y medidas REALES de lo que hay montado en la pestaña abierta, ya
		/// recalculado. Evidencia de que el area ocupa sitio de verdad en pantalla.</summary>
		public string InformeAreaActual()
		{
			if (_contenidoActual == null) {
				return "sin contenido";
			}

			int elementos = 0;
			int ranuras = 0;
			int botones = 0;
			_contenidoActual.ExecuteRecursively(elemento => {
				elementos++;
				if (elemento is SlotObjetoVanilla) {
					ranuras++;
				}
				if (elemento is BotonTk) {
					botones++;
				}
			});

			CalculatedStyle dim = _contenidoActual.GetDimensions();
			return _contenidoActual.GetType().Name + ": " + elementos + " elementos, " + ranuras +
				" ranuras de objeto, " + botones + " botones; area x=" + (int)dim.X + " y=" + (int)dim.Y +
				" " + (int)dim.Width + "x" + (int)dim.Height;
		}

		// =========================================================================================
		// SOLO ARNES DE PRUEBAS (KeepQA V2.0, Fase 1, 14-sep-2026): mismo "Motor 3" ya construido y
		// validado en TModLoaderMod (commit 68684d5, "QA: extractor de orden_z/capa (Motor 3
		// tModLoader/UIElement)") - ver TModLoaderMod\UI\TrainerPanelState.cs, VolcarGeometriaJson,
		// para el razonamiento completo de por que UIElement.Children (la lista que recorre
		// DrawChildren, sin ningun Sort/ZIndex - confirmado en
		// Downloads\tModLoader-Decompiled\tModLoader\Terraria\UI\UIElement.cs) ya es el orden de
		// pintado real, sin instrumentar nada. Aqui SOLO se porta el extractor al arbol real de ESTE
		// panel (marco/pestañas/pie/contenido de area/capa de superposicion), que es distinto del de
		// TModLoaderMod (columnas de toggles/multiplicadores) - la pieza que compara (KeepQA
		// verificarCapas.js/verificarGeometria.js) no se toca ni se reimplementa.
		// =========================================================================================

		/// <summary>Profundidad maxima al recorrer el contenido de la pestaña abierta. Recorte de
		/// seguridad real, no teorico: la Libreria puede tener ~8000 objetos cargados
		/// (ArbolLibreria/RejillaSlots), y un volcado sin limite de un catalogo de ese tamaño no
		/// aporta nada nuevo a verificarGeometria.js/verificarCapas.js (que ya comparan por GRUPO,
		/// no elemento a elemento) y si un coste real de E/S y de JSON.</summary>
		private const int ProfundidadMaximaGeometria = 4;

		/// <summary>Tope de hijos volcados por contenedor, mismo motivo que
		/// <see cref="ProfundidadMaximaGeometria"/>.</summary>
		private const int HijosMaximosPorContenedorGeometria = 60;

		/// <summary>
		/// Vuelca la geometria REAL en pixeles de pantalla de todo el panel - el mismo contrato
		/// <c>{id, tipo, padre_id, x, y, ancho, alto, grupo?, capa?, orden_z?}</c> que ya consumen
		/// <c>verificarGeometria.js</c>/<c>verificarAlineacion.js</c>/<c>verificarCapas.js</c> de
		/// KeepQA (ver <c>Downloads\KeepQA\PATRONES.md</c>, "Motor 3: tModLoader/UIElement", y
		/// <c>Downloads\KeepQA\src\capas\verificarCapas.js</c> para el vocabulario cerrado de
		/// <c>capa</c>). No instrumenta nada nuevo: SOLO lee <see cref="UIElement.GetDimensions"/>
		/// y <see cref="OrdenZDe"/> (indice real dentro de <c>UIElement.Children</c> del padre
		/// real) del arbol que ya existe.
		/// </summary>
		public string VolcarGeometriaJson()
		{
			var elementos = new List<Dictionary<string, object>>();
			if (_marco == null) {
				return "[]";
			}

			var idsUsados = new HashSet<string>();

			void Agregar(string id, string tipo, string padreId, UIElement elemento, string grupo, string capa)
			{
				CalculatedStyle caja = elemento.GetDimensions();
				var d = new Dictionary<string, object> {
					["id"] = id,
					["tipo"] = tipo,
					// null de verdad (nunca ""): verificarGeometria.js solo salta la comprobacion de
					// contencion cuando padre_id es exactamente null/undefined en el JSON - ver el
					// mismo comentario en TModLoaderMod\UI\TrainerPanelState.cs.
					["padre_id"] = padreId,
					["x"] = caja.X,
					["y"] = caja.Y,
					["ancho"] = caja.Width,
					["alto"] = caja.Height,
				};
				if (grupo != null) {
					d["grupo"] = grupo;
				}
				if (capa != null) {
					d["capa"] = capa;
				}
				int? ordenZ = OrdenZDe(elemento);
				if (ordenZ.HasValue) {
					d["orden_z"] = ordenZ.Value;
				}
				// KeepQA - verificarBordeViewport.js (15-sep-2026): un UIList real SIEMPRE tiene
				// OverflowHidden=true (constructor de UIList.cs del motor) y recorta su contenido a su
				// propio GetInnerDimensions() - el area real visible, medida desde el borde superior
				// izquierdo de la caja del propio UIList (que aqui no tiene padding propio en ningun
				// uso real de la familia, verificado por grep de SetPadding contra las variables UIList
				// del mod: solo se le pone padding al UIPanel que lo ENVUELVE, nunca al UIList mismo -
				// asi que GetInnerDimensions() y GetDimensions() coinciden en tamaño y el campo
				// "viewportAlto" resultante es exacto para el contrato de verificarBordeViewport.js,
				// que mide el borde del viewport como V.y + viewportAlto). Ver
				// Downloads\tModLoader-Decompiled\tModLoader\Terraria\GameContent\UI\Elements\UIList.cs.
				if (elemento is UIList listaConScroll) {
					d["viewportAlto"] = listaConScroll.GetInnerDimensions().Height;
				}
				elementos.Add(d);
			}

			Agregar("marco", "panel", null, _marco, null, "panel");

			int i = 0;
			foreach (UIElement hijo in _marco.Children) {
				i++;
				string id, tipo, grupo, capa;
				bool esContenedorDeArea = ReferenceEquals(hijo, _contenedor);

				if (ReferenceEquals(hijo, _botonCerrar)) {
					id = "boton_cerrar"; tipo = "boton"; grupo = null; capa = "controles";
				}
				else if (hijo is BotonTk botonPestana && _botonesPestana.Contains(botonPestana)) {
					int indicePestana = _botonesPestana.IndexOf(botonPestana);
					id = "pestana_" + (indicePestana >= 0 && indicePestana < ClavesDeArea.Length
						? ClavesDeArea[indicePestana].ToLowerInvariant()
						: "idx" + indicePestana);
					tipo = "pestana"; grupo = "pestanas"; capa = "navegacion";
				}
				else if (esContenedorDeArea) {
					id = "contenedor_area"; tipo = "contenedor"; grupo = null; capa = null;
				}
				else if (ReferenceEquals(hijo, _capaSuperposicion)) {
					id = "capa_superposicion"; tipo = "overlay"; grupo = null; capa = "overlay";
				}
				else if (ReferenceEquals(hijo, _chipHora)) {
					id = "chip_hora"; tipo = "chip"; grupo = "chips_cabecera"; capa = "contenido";
				}
				else if (ReferenceEquals(hijo, _chipObjetivo)) {
					id = "chip_objetivo"; tipo = "chip"; grupo = "chips_cabecera"; capa = "contenido";
				}
				else if (ReferenceEquals(hijo, _chipDps)) {
					id = "chip_dps"; tipo = "chip"; grupo = "chips_cabecera"; capa = "contenido";
				}
				else {
					// Titulo y linea de ayuda del pie: EtiquetaTk locales a ConstruirTitulo/
					// ConstruirPie, sin campo propio - se identifican por posicion generica, honesto
					// dado que no hay confusion posible (son los 2 unicos "otros" hijos directos del
					// marco sin identidad propia; los chips de TM3 SI la tienen, ver arriba).
					id = "marco_otro_" + i; tipo = "texto"; grupo = "marco_textos"; capa = "contenido";
				}

				string idFinal = id;
				int sufijoMarco = 1;
				while (!idsUsados.Add(idFinal)) {
					idFinal = id + "_" + (++sufijoMarco);
				}

				Agregar(idFinal, tipo, "marco", hijo, grupo, capa);

				if (esContenedorDeArea) {
					VolcarHijosRecursivo(hijo, idFinal, 0, Agregar, idsUsados);
				}
			}

			return JsonConvert.SerializeObject(elementos);
		}

		/// <summary>Recorre el contenido de la pestaña abierta (heterogeneo: cada una de las ocho
		/// areas monta widgets distintos - ranuras, botones, texto, barras, iconos...) de forma
		/// GENERICA, con el mismo tope de profundidad/hijos que documentan
		/// <see cref="ProfundidadMaximaGeometria"/>/<see cref="HijosMaximosPorContenedorGeometria"/>.
		/// La clasificacion de <c>capa</c> es una aproximacion honesta por tipo de widget
		/// (<see cref="ClasificarCapa"/>) - si un tipo no encaja en el vocabulario cerrado de
		/// <c>verificarCapas.js</c>, se omite el campo en vez de forzar uno que no es cierto (Marca
		/// Keep: "documentar con claridad lo que de verdad no aplica, nunca forzar un analogo
		/// falso").</summary>
		private static void VolcarHijosRecursivo(UIElement contenedor, string padreId, int profundidad,
			Action<string, string, string, UIElement, string, string> agregar, HashSet<string> idsUsados)
		{
			if (profundidad >= ProfundidadMaximaGeometria) {
				return;
			}

			int i = 0;
			foreach (UIElement hijo in contenedor.Children) {
				i++;
				if (i > HijosMaximosPorContenedorGeometria) {
					break;
				}

				string tipo = TipoDeWidget(hijo);
				string capa = ClasificarCapa(hijo);

				string idBase = padreId + "_" + tipo + "_" + i;
				string id = idBase;
				int sufijo = 1;
				while (!idsUsados.Add(id)) {
					id = idBase + "_" + (++sufijo);
				}

				agregar(id, tipo, padreId, hijo, null, capa);

				// KeepQA - verificarBordeViewport.js (15-sep-2026): un UIList real envuelve sus filas
				// en un UIInnerList privado (UIList.cs del motor: "internal UIElement _innerList",
				// unico hijo real de UIList.Children, cuyo unico trabajo es mover su propio Top segun
				// la barra de scroll - no es un nivel semantico del arbol). Si se dejara recorrer por
				// el camino generico de aqui abajo, las filas reales quedarian con padre_id apuntando
				// al wrapper interno en vez de al propio UIList, y verificarBordeViewport.js (que solo
				// empareja hijos DIRECTOS por padre_id) nunca las encontraria. Se salta ese nivel a
				// proposito y se recorren los items reales directamente.
				if (hijo is UIList listaHijo) {
					VolcarFilasUIList(listaHijo, id, profundidad + 1, agregar, idsUsados);
				}
				else {
					VolcarHijosRecursivo(hijo, id, profundidad + 1, agregar, idsUsados);
				}
			}
		}

		/// <summary>
		/// Caso especial de <see cref="VolcarHijosRecursivo"/> para un <see cref="UIList"/> real: en
		/// vez de descender por su unico hijo real del arbol (el <c>UIInnerList</c> privado del
		/// motor, transparente para este contrato), recorre directamente <c>UIList._items</c> (campo
		/// PUBLICO real del motor - lista ordenada de filas, la misma que <c>RecalculateChildren</c>
		/// usa para apilarlas) con <paramref name="padreId"/> apuntando al PROPIO UIList. Solo vuelca
		/// las filas REALMENTE visibles en este instante (interseccion con
		/// <see cref="UIElement.GetInnerDimensions"/> del propio UIList - el mismo chequeo AABB que
		/// hace <c>UIInnerList.DrawChildren</c> del motor para decidir que fila pintar de verdad),
		/// mismo criterio que el arnes WPF de Terrakeep (solo vuelca los
		/// <c>ItemContainerGenerator</c> ya REALIZADOS) - para que esto cace el bug real hace falta
		/// llamar primero a algo que desplace la lista hasta el final (<c>UIList.ViewPosition =
		/// float.MaxValue</c>) y dejar pasar al menos un fotograma de <c>Draw</c> real (el motor
		/// mueve el wrapper interno en <c>UIList.DrawSelf</c>, no al fijar <c>ViewPosition</c>).
		/// </summary>
		private static void VolcarFilasUIList(UIList lista, string padreId, int profundidad,
			Action<string, string, string, UIElement, string, string> agregar, HashSet<string> idsUsados)
		{
			if (profundidad >= ProfundidadMaximaGeometria) {
				return;
			}

			CalculatedStyle viewport = lista.GetInnerDimensions();
			float viewportArriba = viewport.Y;
			float viewportAbajo = viewport.Y + viewport.Height;

			int i = 0;
			foreach (UIElement fila in lista._items) {
				i++;
				if (i > HijosMaximosPorContenedorGeometria) {
					break;
				}

				CalculatedStyle caja = fila.GetDimensions();
				bool visible = caja.Y + caja.Height > viewportArriba && caja.Y < viewportAbajo;
				if (!visible) {
					continue;
				}

				string tipo = TipoDeWidget(fila);
				string capa = ClasificarCapa(fila);

				string idBase = padreId + "_fila_" + i;
				string id = idBase;
				int sufijo = 1;
				while (!idsUsados.Add(id)) {
					id = idBase + "_" + (++sufijo);
				}

				agregar(id, tipo, padreId, fila, "uilist_filas_" + padreId, capa);
				VolcarHijosRecursivo(fila, id, profundidad + 1, agregar, idsUsados);
			}
		}

		/// <summary>Nombre de tipo ASCII corto, en minusculas, para el campo "tipo" del JSON - el
		/// nombre real de la clase de C#, sin adornos.</summary>
		private static string TipoDeWidget(UIElement elemento)
		{
			return elemento.GetType().Name.ToLowerInvariant();
		}

		/// <summary>
		/// Aproximacion honesta de <c>capa</c> (vocabulario cerrado de
		/// <c>Downloads\KeepQA\src\capas\verificarCapas.js</c>: fondo/decoracion/panel/contenido/
		/// controles/navegacion/overlay/modal/primer_plano) a partir del TIPO de widget - nunca de su
		/// contenido, que varia por area. Devuelve null (campo omitido) para cualquier tipo que no
		/// encaje de verdad, en vez de forzar una clasificacion falsa.
		/// </summary>
		private static string ClasificarCapa(UIElement elemento)
		{
			switch (elemento) {
				// Subtipos concretos de UIPanel PRIMERO: un switch de patrones de tipo comprueba
				// los "case" en orden y se queda con el primero que encaje, asi que si "UIPanel _"
				// fuera antes, CampoTextoTk/AlternadorTk (ambos heredan de UIPanel) nunca llegarian
				// a clasificarse como "controles" - quedarian atrapados como "panel", que no es lo
				// que son de verdad.
				case CapaSuperposicionTk _:
					return "overlay";
				case BotonTk _:
				case SlotObjetoVanilla _:
				case DeslizadorTk _:
				case AlternadorTk _:
				case CampoTextoTk _:
				case EditorCantidadTk _:
				case SlotPapeleraTk _:
				case SlotSeleccionTk _:
				case SlotCatalogoBuild _:
				case SlotCatalogoLibreria _:
				case SlotMuestraInvestigacion _:
				case EditorPrefijoTk _:
					return "controles";
				case EtiquetaTk _:
				case UIText _:
				case BarraProgresoTk _:
				case MedidorPreparacionTk _:
				case IconoBuffTk _:
				case ParrafoTk _:
				case FilaRequisitoTk _:
				case MunecoTk _:
					return "contenido";
				// UIPanel generico (marcos/tarjetas sin widget propio) va DESPUES de todos los
				// subtipos concretos de arriba, nunca antes - ver el comentario del principio de
				// este switch.
				case UIPanel _:
					return "panel";
				default:
					return null;
			}
		}

		/// <summary>Indice REAL (0-based) de <paramref name="elemento"/> dentro de la lista de hijos
		/// de su padre REAL (<see cref="UIElement.Children"/>, la propiedad publica que envuelve la
		/// lista privada <c>Elements</c> que <c>DrawChildren</c> recorre para pintar, sin ordenar por
		/// ningun otro campo). Mayor indice = insertado mas tarde = pintado mas tarde = mas al frente
		/// (algoritmo del pintor), exactamente el contrato de <c>orden_z</c> que espera
		/// <c>verificarCapas.js</c>. Mismo metodo, literal, que
		/// <c>TModLoaderMod\UI\TrainerPanelState.cs.OrdenZDe</c> - portado sin cambios porque
		/// <c>UIElement.Children</c>/<c>Parent</c> son identicos en los dos mods (mismo tModLoader).
		/// Devuelve null si el elemento no tiene padre (la raiz) o ya no aparece en la lista de su
		/// padre.</summary>
		private static int? OrdenZDe(UIElement elemento)
		{
			UIElement padre = elemento.Parent;
			if (padre == null) {
				return null;
			}
			int idx = 0;
			foreach (UIElement hijo in padre.Children) {
				if (ReferenceEquals(hijo, elemento)) {
					return idx;
				}
				idx++;
			}
			return null;
		}
	}
}
