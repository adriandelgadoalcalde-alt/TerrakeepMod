using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Map;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// Pestaña "Mapa": el mini-mapa navegable y sus controles, mas el boton que salta al mapa
	/// vanilla a pantalla completa.
	/// </summary>
	/// <remarks>
	/// Los dos mapas conviven porque no queda mas remedio, y es la decision de diseño que ya venia
	/// tomada: mientras <c>Main.mapFullscreen</c> es true el juego <b>no dibuja ninguna interfaz
	/// de mods</b>, asi que un mapa dentro del panel y el mapa grande del juego son mutuamente
	/// excluyentes. Aqui se tiene lo mejor de cada uno: navegar sin salir de Terrakeep, y saltar
	/// al mapa de verdad (con sus iconos, sus pilones y su teletransporte) cuando hace falta sitio.
	/// </remarks>
	public class PestanaMapa : UIElement
	{
		private const float AnchoLateral = 240f;

		/// <summary>Margen de seguridad bajo el ultimo renglon del flujo, medido con
		/// <c>GetDimensions()</c> real (16-sep-2026: esta pestaña solo mide ~308-310px de alto, NO
		/// los 444 del area de contenido - eso confundio un primer intento de este mismo arreglo;
		/// diagnostico real en bitacora.md).</summary>
		private const float MargenInferior = 6f;

		private MiniMapaTk _mapa;
		private EtiquetaTk _estado;

		/// <summary>
		/// El bloque de texto que cuelga bajo los botones (aviso del mapa exclusivo, marcadores,
		/// detalle, tile bajo el raton y "Zoom"): su ESCALA BASE y su alto de linea a escala 1
		/// (para poder recalcular el Top de cada uno sumando los anteriores), mas si se
		/// <see cref="ColapsaSiVacio"/> cuando no tiene nada que enseñar ahora mismo. <see
		/// cref="Update"/> reancla y reescala TODO el bloque cada fotograma - ver su XMLdoc.
		/// </summary>
		private readonly struct BloqueTexto
		{
			public readonly EtiquetaTk Etiqueta;
			public readonly float EscalaBase;
			public readonly float AltoLinea;

			/// <summary>true si esta linea no debe reservar hueco cuando <see
			/// cref="EtiquetaTk.TextoActual"/> esta vacio (ver el "porque" completo en el XMLdoc de
			/// <see cref="Update"/>). Solo "bajo el raton" lo usa: el resto del bloque SIEMPRE
			/// tiene algo que enseñar (avisos fijos, "Marcadores", el resumen de la busqueda o
			/// "Zoom: X"), asi que reservarles hueco siempre es lo correcto.</summary>
			public readonly bool ColapsaSiVacio;

			public BloqueTexto(EtiquetaTk etiqueta, float escalaBase, float altoLinea, bool colapsaSiVacio)
			{
				Etiqueta = etiqueta;
				EscalaBase = escalaBase;
				AltoLinea = altoLinea;
				ColapsaSiVacio = colapsaSiVacio;
			}
		}

		private readonly List<BloqueTexto> _bloqueTexto = new List<BloqueTexto>();

		private float _inicioBloqueTexto;

		/// <summary>El mini-mapa, para que la autoprueba pueda mirarlo y accionarlo.</summary>
		public MiniMapaTk Mapa => _mapa;

		public PestanaMapa()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirMapa();
			ConstruirLateral();
		}

		/// <summary>
		/// BUG REAL visto en capturas reales del juego (16-sep-2026, ronda de juego-libre/dossier
		/// de KeepQA): el renglon de estado ("Zoom: X px per tile") colgaba de <c>y</c> igual que
		/// el resto del flujo de arriba (Marcadores/detalle/bajo el raton) para no pisarlos NUNCA -
		/// pero un flujo tan largo en ingles (el aviso de arriba parte en mas lineas que en español)
		/// podia acabar entrando en la franja del pie del panel. La geometria real de esta pestaña
		/// (<see cref="UIElement.GetDimensions"/>) solo se conoce cuando el layout ya ha corrido,
		/// nunca en el constructor (medido en vivo: esta pestaña solo tiene ~308-310px reales de
		/// alto, no los 444 del area de contenido entera que reporta el panel para OTRAS pestañas).
		/// <para />
		/// <b>Ampliado el 29-sep-2026</b> (auditoria de UIScale/resolucion, bitacora.md, requirement
		/// 0446b3c9): el recorte original SOLO tocaba <c>_estado</c> (el ultimo elemento), asi que
		/// cuando el hueco real es MUCHO mas pequeño de lo habitual (1366x768@150%: el marco pierde
		/// a la vez su tope de ancho Y su tope de alto, ver la cabecera de
		/// <see cref="ReflowVertical"/>) el recorte empujaba "Zoom" hacia ARRIBA hasta solaparse con
		/// el propio parrafo del aviso, en vez de con el pie. Ahora se comprime el bloque de texto
		/// ENTERO (posicion Y escala de cada elemento, con el MISMO factor -
		/// <see cref="ReflowVertical.FactorDeCompresion"/>): eso garantiza matematicamente que
		/// ningun elemento se solape con el siguiente, sea cual sea el hueco real disponible.
		/// <para />
		/// <b>Ampliado otra vez el 29-sep-2026</b> (cierre final, mismo requirement): medido con
		/// pixeles reales (bitacora.md), el caso mas comprimido (1366x768@150%) dejaba "Zoom" en
		/// 11px, por debajo de los 14-16px que usa de referencia el resto del mod - el propio
		/// <c>escalaMinima</c> de <see cref="ReflowVertical"/> (0.55, un suelo DURO para que el
		/// texto nunca desaparezca del todo) no se podia subir sin más sin que "Zoom" volviera a
		/// solaparse con el pie en ese mismo caso extremo, asi que subirlo a ciegas no era la
		/// solucion (queda documentado en la entrada de bitacora anterior). La solucion real que
		/// SI reduce cuanto hace falta comprimir, sin tocar el suelo duro: "bajo el raton" (el
		/// tile que hay bajo el cursor) esta vacio la inmensa mayoria de los fotogramas -el raton
		/// no siempre esta sobre el mapa- y aun asi reservaba sus 20px SIEMPRE, incluso vacio. Con
		/// <see cref="BloqueTexto.ColapsaSiVacio"/> ese hueco solo se reserva cuando de verdad hay
		/// algo que enseñar: el bloque entero necesita menos alto la mayoria del tiempo, asi que
		/// <see cref="ReflowVertical.FactorDeCompresion"/> comprime MENOS (a menudo nada) y "Zoom"
		/// se enseña a su tamaño base con mucha mas frecuencia, sin arriesgar el solape que ya se
		/// verifico cerrado en la pasada anterior (el suelo <c>escalaMinima</c> sigue intacto para
		/// el caso raro en que el raton SI este sobre el mapa a la vez que el hueco es minimo).
		/// </summary>
		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_bloqueTexto.Count == 0) {
				return;
			}

			// Alto NATURAL del bloque con el estado ACTUAL de cada renglon (ver el "ampliado otra
			// vez" de arriba): un renglon colapsable que ahora mismo no tiene texto no cuenta.
			float altoNatural = 0f;
			for (int i = 0; i < _bloqueTexto.Count; i++) {
				BloqueTexto item = _bloqueTexto[i];
				if (!item.ColapsaSiVacio || !string.IsNullOrEmpty(item.Etiqueta.TextoActual)) {
					altoNatural += item.AltoLinea;
				}
			}

			float altoDisponible = GetDimensions().Height - _inicioBloqueTexto - MargenInferior;
			float factor = ReflowVertical.FactorDeCompresion(altoNatural, altoDisponible);

			float yRel = 0f;
			for (int i = 0; i < _bloqueTexto.Count; i++) {
				BloqueTexto item = _bloqueTexto[i];
				item.Etiqueta.EscalaTexto = item.EscalaBase * factor;
				item.Etiqueta.Top.Set(_inicioBloqueTexto + yRel * factor, 0f);
				// Sin este Recalculate el Top/escala nuevos se guardan pero GetDimensions() (lo que
				// Draw usa de verdad) se queda con el valor calculado la vez anterior: se vio en vivo
				// que cambiar Top.Set aqui no movia nada en pantalla hasta añadir esta llamada.
				item.Etiqueta.Recalculate();
				if (!item.ColapsaSiVacio || !string.IsNullOrEmpty(item.Etiqueta.TextoActual)) {
					yRel += item.AltoLinea;
				}
			}
		}

		private void ConstruirMapa()
		{
			UIPanel marco = new UIPanel();
			marco.Width.Set(-(AnchoLateral + 10f), 1f);
			marco.Height.Set(0f, 1f);
			marco.BackgroundColor = new Color(20, 26, 48) * 0.95f;
			marco.SetPadding(4f);
			Append(marco);

			_mapa = new MiniMapaTk();
			_mapa.Width.Set(0f, 1f);
			_mapa.Height.Set(0f, 1f);
			marco.Append(_mapa);
		}

		private void ConstruirLateral()
		{
			UIElement lateral = new UIElement();
			lateral.Width.Set(AnchoLateral, 0f);
			lateral.Height.Set(0f, 1f);
			lateral.HAlign = 1f;
			Append(lateral);

			float y = 0f;

			BotonTk masCerca = new BotonTk(Idiomas.Texto("Exploracion.Mapa.Acercar"), 0.85f);
			ColocarBoton(lateral, masCerca, 0f, y, AnchoLateral / 2f - 3f);
			masCerca.Ayuda = () => Idiomas.Texto("Exploracion.Mapa.AcercarAyuda");
			masCerca.AlPulsar += () => _mapa.Acercar(1.5f);

			BotonTk masLejos = new BotonTk(Idiomas.Texto("Exploracion.Mapa.Alejar"), 0.85f);
			ColocarBoton(lateral, masLejos, AnchoLateral / 2f + 3f, y, AnchoLateral / 2f - 3f);
			masLejos.AlPulsar += () => _mapa.Acercar(1f / 1.5f);
			// BUG REAL (16-sep-2026, ver Update() mas abajo): esta pestaña, medida en vivo con
			// GetDimensions(), solo tiene 308-310px REALES de alto (no los 444 del area de
			// contenido entera - Exploracion tambien tiene el minimapa a su izquierda, pero el alto
			// SIGUE siendo el mismo para toda la pestaña). Con los huecos "sobrados" que traia esta
			// columna (40/40/48/46 entre botones, 30 tras el detalle de Marcadores) el flujo entero
			// se iba a 331px de alto - ya no cabia ni el propio "bajo el raton" antes de acabarse el
			// hueco. Los huecos de aqui abajo se ajustan al alto REAL del boton (34, ColocarBoton) +
			// un margen pequeño, no a numeros redondos elegidos a ojo.
			y += 36f;

			BotonTk enJugador = new BotonTk(Idiomas.Texto("Exploracion.Mapa.Centrar"), 0.85f);
			ColocarBoton(lateral, enJugador, 0f, y, AnchoLateral);
			enJugador.AlPulsar += () => _mapa.CentrarEnJugador();
			y += 36f;

			BotonTk todo = new BotonTk(Idiomas.Texto("Exploracion.Mapa.MundoEntero"), 0.85f);
			ColocarBoton(lateral, todo, 0f, y, AnchoLateral);
			todo.AlPulsar += () => _mapa.EncuadrarMundo();
			y += 40f;

			BotonTk verEnMapa = new BotonTk(Idiomas.Texto("Exploracion.Mapa.VerEnMapa"), 0.85f);
			ColocarBoton(lateral, verEnMapa, 0f, y, AnchoLateral);
			verEnMapa.Height.Set(40f, 0f);
			verEnMapa.Ayuda = () => Idiomas.Texto("Exploracion.Mapa.VerEnMapaAyuda");
			verEnMapa.AlPulsar += SaltarAlMapaVanilla;
			y += 42f;

			// A partir de aqui, "y" es el INICIO del bloque de texto comprimible (aviso, marcadores,
			// detalle, bajo el raton y "Zoom") - Update() reancla y reescala TODO este bloque junto
			// contra el alto real disponible (ver su XMLdoc), asi que las posiciones que se calculan
			// aqui abajo son solo las NATURALES (relativas al inicio del bloque, escala 1).
			_inicioBloqueTexto = y;
			float yRel = 0f;

			// Se parte con el ancho REAL: los tres renglones con saltos escritos a mano estaban
			// medidos para el texto en español y en ingles la tercera linea se salia del marco por la
			// derecha (visto en una captura real del juego).
			string textoAvisoPartido = EtiquetaTk.PartirEnLineas(
				Idiomas.Texto("Exploracion.Mapa.AvisoExclusivo"), AnchoLateral - 4f, 0.7f);
			EtiquetaTk aviso = new EtiquetaTk(() => textoAvisoPartido, 0.7f, AnchoLateral, 50f);
			aviso.ColorTexto = EstiloTk.TextoSuave;
			aviso.Top.Set(_inicioBloqueTexto + yRel, 0f);
			lateral.Append(aviso);
			// BUG REAL visto en captura del juego (16-sep-2026, ronda de juego-libre/dossier de
			// KeepQA, en-US a 1600x900): un "96" fijo (a mano, "~21 px por linea, hasta 4 lineas
			// caben") se quedaba corto de verdad para el numero de lineas + interlineado REALES que
			// devuelve DynamicSpriteFont.MeasureString con esta fuente, y el resto del flujo de
			// abajo (Marcadores/detalle/bajo el raton/zoom) arrancaba mas alto de lo que el hueco
			// disponible permitia. En vez de otra cifra a mano, se mide la altura REAL con la MISMA
			// fuente/escala que draw usa (ver PartirEnLineas de mas arriba: mismo principio, no
			// adivinar el ancho de un texto - aqui, no adivinar su alto), contando cuantos '\n' trajo
			// el texto ya partido. Se recalcula una sola vez aqui (el idioma no cambia sin
			// reconstruir el panel, igual que el resto de este metodo se calcula una vez).
			int avisoLineas = textoAvisoPartido.Split('\n').Length;
			float avisoAltoLinea = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString("Ay").Y * 0.7f;
			float avisoAlto = avisoLineas * avisoAltoLinea;
			float avisoAltoTotal = avisoAlto + 4f;
			_bloqueTexto.Add(new BloqueTexto(aviso, 0.7f, avisoAltoTotal, colapsaSiVacio: false));
			yRel += avisoAltoTotal;

			EtiquetaTk leyenda = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mapa.Marcadores"), 0.85f, AnchoLateral, 22f);
			leyenda.Top.Set(_inicioBloqueTexto + yRel, 0f);
			lateral.Append(leyenda);
			_bloqueTexto.Add(new BloqueTexto(leyenda, 0.85f, 22f, colapsaSiVacio: false));
			yRel += 22f;

			EtiquetaTk detalleLeyenda = new EtiquetaTk(
				() => MarcadoresExploracion.HayAlgo
					? Idiomas.Texto("Exploracion.Mapa.ZonasDe",
						MarcadoresExploracion.Resultados.Count, MarcadoresExploracion.Titulo)
					: Idiomas.Texto("Exploracion.Mapa.SinBusqueda"),
				0.75f, AnchoLateral, 22f);
			detalleLeyenda.ColorTexto = EstiloTk.TextoSuave;
			detalleLeyenda.Top.Set(_inicioBloqueTexto + yRel, 0f);
			lateral.Append(detalleLeyenda);
			_bloqueTexto.Add(new BloqueTexto(detalleLeyenda, 0.75f, 22f, colapsaSiVacio: false));
			yRel += 22f;

			// "bajo el raton" es el UNICO renglon del bloque que puede quedarse vacio buena parte
			// de los fotogramas (el raton no siempre esta sobre el mapa, ver TextoBajoElRaton):
			// ColapsaSiVacio deja de reservarle sus 20px cuando no tiene nada que enseñar, para
			// que el resto del bloque (sobre todo "Zoom") necesite menos compresion - ver el
			// "ampliado otra vez" del XMLdoc de Update().
			EtiquetaTk bajoElRaton = new EtiquetaTk(TextoBajoElRaton, 0.75f, AnchoLateral, 20f);
			bajoElRaton.ColorTexto = EstiloTk.TextoAviso;
			bajoElRaton.Top.Set(_inicioBloqueTexto + yRel, 0f);
			lateral.Append(bajoElRaton);
			_bloqueTexto.Add(new BloqueTexto(bajoElRaton, 0.75f, 20f, colapsaSiVacio: true));
			yRel += 20f;

			// Posicion NATURAL en el flujo (nunca pisa lo de arriba); Update() comprime TODO el
			// bloque contra el pie real del panel si hiciera falta - ver el comentario de Update()
			// mas arriba.
			EtiquetaTk estado = new EtiquetaTk(
				() => _mapa != null
					? Idiomas.Texto("Exploracion.Mapa.Zoom", _mapa.Escala.ToString("0.00"))
					: "",
				0.75f, AnchoLateral, 22f);
			estado.ColorTexto = EstiloTk.TextoSuave;
			estado.Top.Set(_inicioBloqueTexto + yRel, 0f);
			lateral.Append(estado);
			_estado = estado;
			_bloqueTexto.Add(new BloqueTexto(estado, 0.75f, estado.Height.Pixels, colapsaSiVacio: false));
		}

		private string TextoBajoElRaton()
		{
			if (_mapa == null) {
				return "";
			}
			Vector2? tile = _mapa.TileBajoElRaton();
			if (!tile.HasValue) {
				return "";
			}
			int x = (int)tile.Value.X;
			int y = (int)tile.Value.Y;
			if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) {
				return Idiomas.Texto("Exploracion.Mapa.FueraDelMundo");
			}
			bool visto = Main.Map != null && Main.Map.IsRevealed(x, y);
			return Idiomas.Texto(visto
				? "Exploracion.Mapa.TileExplorado"
				: "Exploracion.Mapa.TileSinExplorar", x, y, NombreBajoElCursor(x, y));
		}

		/// <summary>
		/// Que hay REALMENTE en ese tile, con el mismo nombre (y el mismo idioma) que enseñaria el
		/// propio juego.
		/// </summary>
		/// <remarks>
		/// Se probaron las dos vias que pedia la tarea. Portar <c>tiles.json</c>/<c>walls.json</c>
		/// de TEdit (la que ya usa el proyecto hermano de escritorio) exigiria arrastrar ese
		/// catalogo entero a este mod y mantenerlo aparte de lo que el propio tModLoader ya sabe de
		/// sus tiles. La otra via GANA porque estamos DENTRO del juego en marcha, no leyendo un
		/// <c>.wld</c> desde fuera: <c>MapHelper.CreateMapTile(x, y, 255)</c> es la MISMA funcion
		/// que usa el motor para decidir que enseña el mapa de vanilla en cada casilla (prioridad
		/// tile &gt; liquido &gt; pared &gt; fondo segun profundidad, con todas sus excepciones:
		/// bloques pintados invisibles, variantes de mineral por bioma, etc.), y
		/// <c>Lang.GetMapObjectName</c> es la MISMA funcion que usa el detector de menas
		/// ("GameUI.OreDetected") para nombrar lo que encuentra. Cubre tiles/paredes/liquidos de
		/// CUALQUIER mod sin catalogo propio (un <c>ModTile</c> se registra solo en
		/// <c>MapHelper.tileLookup</c>), ya sale en el idioma activo, y no hay que mantener nada.
		/// <para />
		/// Publico porque lo usa tambien la autoprueba, para comprobar sobre una coordenada
		/// CONOCIDA (una que la propia busqueda acaba de decir que tiene cobre) que el nombre que
		/// sale es el correcto, sin depender de mover el raton real.
		/// </remarks>
		public static string NombreBajoElCursor(int x, int y)
		{
			// Un NPC vivo encima tapa lo que haya debajo: es lo mas especifico que puede haber ahi,
			// igual que en la propia busqueda de "NPC vivos ahora mismo".
			for (int i = 0; i < Main.npc.Length; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active || npc.type <= 0) {
					continue;
				}
				int izquierda = (int)(npc.position.X / 16f);
				int arriba = (int)(npc.position.Y / 16f);
				int ancho = (int)System.Math.Ceiling(npc.width / 16f);
				int alto = (int)System.Math.Ceiling(npc.height / 16f);
				if (x >= izquierda && x < izquierda + ancho && y >= arriba && y < arriba + alto) {
					return npc.GivenOrTypeName;
				}
			}

			MapTile casilla = MapHelper.CreateMapTile(x, y, 255);
			string nombre = casilla.Type > 0 ? Lang.GetMapObjectName(casilla.Type) : null;
			return string.IsNullOrEmpty(nombre) ? Idiomas.Texto("Exploracion.Mapa.Vacio") : nombre;
		}

		private void SaltarAlMapaVanilla()
		{
			// Se abre el mapa grande justo por donde el jugador estaba mirando en el mini-mapa. La
			// escala del mapa vanilla y la del mini-mapa son la misma unidad (pixeles por tile),
			// asi que se puede pasar tal cual; DrawMap la recorta a su rango valido.
			PanelExploracionSystem.VerEnElMapa(_mapa.CentroTile, _mapa.Escala, "botón \"Ver en el mapa del juego\"");
		}

		/// <summary>Dispara el salto al mapa vanilla desde fuera (autoprueba).</summary>
		public void PulsarVerEnElMapa()
		{
			SaltarAlMapaVanilla();
		}

		private static void ColocarBoton(UIElement padre, BotonTk boton, float x, float y, float ancho)
		{
			boton.Left.Set(x, 0f);
			boton.Top.Set(y, 0f);
			boton.Width.Set(ancho, 0f);
			boton.Height.Set(34f, 0f);
			padre.Append(boton);
		}
	}
}
