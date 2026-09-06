using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
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

			BotonTk masCerca = new BotonTk("Acercar  +", 0.85f);
			ColocarBoton(lateral, masCerca, 0f, y, AnchoLateral / 2f - 3f);
			masCerca.Ayuda = "También puedes usar la rueda del ratón sobre el mapa.";
			masCerca.AlPulsar += () => _mapa.Acercar(1.5f);

			BotonTk masLejos = new BotonTk("Alejar  −", 0.85f);
			ColocarBoton(lateral, masLejos, AnchoLateral / 2f + 3f, y, AnchoLateral / 2f - 3f);
			masLejos.AlPulsar += () => _mapa.Acercar(1f / 1.5f);
			y += 40f;

			BotonTk enJugador = new BotonTk("Centrar en mí", 0.85f);
			ColocarBoton(lateral, enJugador, 0f, y, AnchoLateral);
			enJugador.AlPulsar += () => _mapa.CentrarEnJugador();
			y += 40f;

			BotonTk todo = new BotonTk("Ver el mundo entero", 0.85f);
			ColocarBoton(lateral, todo, 0f, y, AnchoLateral);
			todo.AlPulsar += () => _mapa.EncuadrarMundo();
			y += 52f;

			BotonTk verEnMapa = new BotonTk("Ver en el mapa del juego", 0.85f);
			ColocarBoton(lateral, verEnMapa, 0f, y, AnchoLateral);
			verEnMapa.Height.Set(40f, 0f);
			verEnMapa.Ayuda = "Cierra Terrakeep y abre el mapa grande del juego por donde estás mirando, " +
				"con los marcadores de la búsqueda encima. Al cerrarlo se vuelve aquí.";
			verEnMapa.AlPulsar += SaltarAlMapaVanilla;
			y += 50f;

			EtiquetaTk aviso = new EtiquetaTk(
				() => "El mapa grande del juego no puede\nconvivir con este panel: al abrirlo,\nTerrakeep se cierra y vuelve solo.",
				0.7f, AnchoLateral, 50f);
			aviso.ColorTexto = EstiloTk.TextoSuave;
			aviso.Top.Set(y, 0f);
			lateral.Append(aviso);
			// 74 y no 60: son TRES lineas y la fuente del juego a escala 0,7 gasta ~21 px por
			// linea, o sea 63. Con 60 la tercera linea se comia el titulo "Marcadores" de debajo -
			// se vio en una captura real, no leyendo el codigo.
			y += 74f;

			EtiquetaTk leyenda = new EtiquetaTk(() => "Marcadores", 0.85f, AnchoLateral, 24f);
			leyenda.Top.Set(y, 0f);
			lateral.Append(leyenda);
			y += 24f;

			EtiquetaTk detalleLeyenda = new EtiquetaTk(
				() => MarcadoresExploracion.HayAlgo
					? MarcadoresExploracion.Resultados.Count + " zonas de \"" + MarcadoresExploracion.Titulo + "\""
					: "Sin búsqueda: usa la pestaña Búsqueda",
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
				() => _mapa != null ? "Zoom: " + _mapa.Escala.ToString("0.00") + " px por tile" : "",
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
				return "Fuera del mundo";
			}
			bool visto = Main.Map != null && Main.Map.IsRevealed(x, y);
			return "Tile " + x + ", " + y + (visto ? " (explorado)" : " (sin explorar)");
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
