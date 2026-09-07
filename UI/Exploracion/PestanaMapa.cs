using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Map;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Exploracion;
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

		private MiniMapaTk _mapa;

		/// <summary>El mini-mapa, para que la autoprueba pueda mirarlo y accionarlo.</summary>
		public MiniMapaTk Mapa => _mapa;

		public PestanaMapa()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirMapa();
			ConstruirLateral();
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
			y += 40f;

			BotonTk enJugador = new BotonTk(Idiomas.Texto("Exploracion.Mapa.Centrar"), 0.85f);
			ColocarBoton(lateral, enJugador, 0f, y, AnchoLateral);
			enJugador.AlPulsar += () => _mapa.CentrarEnJugador();
			y += 40f;

			BotonTk todo = new BotonTk(Idiomas.Texto("Exploracion.Mapa.MundoEntero"), 0.85f);
			ColocarBoton(lateral, todo, 0f, y, AnchoLateral);
			todo.AlPulsar += () => _mapa.EncuadrarMundo();
			y += 52f;

			BotonTk verEnMapa = new BotonTk(Idiomas.Texto("Exploracion.Mapa.VerEnMapa"), 0.85f);
			ColocarBoton(lateral, verEnMapa, 0f, y, AnchoLateral);
			verEnMapa.Height.Set(40f, 0f);
			verEnMapa.Ayuda = () => Idiomas.Texto("Exploracion.Mapa.VerEnMapaAyuda");
			verEnMapa.AlPulsar += SaltarAlMapaVanilla;
			y += 50f;

			// Se parte con el ancho REAL: los tres renglones con saltos escritos a mano estaban
			// medidos para el texto en español y en ingles la tercera linea se salia del marco por la
			// derecha (visto en una captura real del juego).
			EtiquetaTk aviso = new EtiquetaTk(
				() => EtiquetaTk.PartirEnLineas(
					Idiomas.Texto("Exploracion.Mapa.AvisoExclusivo"), AnchoLateral - 4f, 0.7f),
				0.7f, AnchoLateral, 50f);
			aviso.ColorTexto = EstiloTk.TextoSuave;
			aviso.Top.Set(y, 0f);
			lateral.Append(aviso);
			// 96 y no 74: la fuente del juego a escala 0,7 gasta ~21 px por linea, y este aviso se
			// parte solo segun el ancho y el idioma. En español salen tres lineas y en ingles
			// CUATRO, y con 74 la cuarta se comia el titulo "Marcadores" de debajo (las dos veces
			// se vio en una captura real, no leyendo el codigo). Con 96 caben cuatro.
			y += 96f;

			EtiquetaTk leyenda = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mapa.Marcadores"), 0.85f, AnchoLateral, 24f);
			leyenda.Top.Set(y, 0f);
			lateral.Append(leyenda);
			y += 24f;

			EtiquetaTk detalleLeyenda = new EtiquetaTk(
				() => MarcadoresExploracion.HayAlgo
					? Idiomas.Texto("Exploracion.Mapa.ZonasDe",
						MarcadoresExploracion.Resultados.Count, MarcadoresExploracion.Titulo)
					: Idiomas.Texto("Exploracion.Mapa.SinBusqueda"),
				0.75f, AnchoLateral, 22f);
			detalleLeyenda.ColorTexto = EstiloTk.TextoSuave;
			detalleLeyenda.Top.Set(y, 0f);
			lateral.Append(detalleLeyenda);
			y += 30f;

			EtiquetaTk bajoElRaton = new EtiquetaTk(TextoBajoElRaton, 0.75f, AnchoLateral, 22f);
			bajoElRaton.ColorTexto = EstiloTk.TextoAviso;
			bajoElRaton.Top.Set(y, 0f);
			lateral.Append(bajoElRaton);

			EtiquetaTk estado = new EtiquetaTk(
				() => _mapa != null
					? Idiomas.Texto("Exploracion.Mapa.Zoom", _mapa.Escala.ToString("0.00"))
					: "",
				0.75f, AnchoLateral, 22f);
			estado.ColorTexto = EstiloTk.TextoSuave;
			estado.VAlign = 1f;
			lateral.Append(estado);
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
