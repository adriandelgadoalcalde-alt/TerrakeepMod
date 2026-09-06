using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Exploracion;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Punto de entrada del panel de Exploracion (WS6): atajo de teclado propio, apertura y
	/// cierre, salto al mapa vanilla y vuelta, y el avance por fotogramas de la busqueda.
	/// </summary>
	/// <remarks>
	/// El atajo se registra <b>desde aqui</b> y no desde la clase <c>Terrakeep</c>, igual que hizo
	/// WS4: asi este workstream no toca ni un archivo compartido con los demas.
	/// <c>KeybindLoader.RegisterKeybind</c> solo necesita el <c>Mod</c>, que un <c>ModSystem</c>
	/// ya tiene asignado en <c>ModType.Mod</c> antes de que corra su <c>Load()</c>.
	/// </remarks>
	public class PanelExploracionSystem : ModSystem
	{
		/// <summary>Abre el panel de Exploracion solo, para poder verificarlo sin depender de que
		/// nadie pulse una tecla. Variable propia, para no disparar a la vez los paneles de los
		/// demas workstreams.</summary>
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST_WS6";

		/// <summary>SOLO ARNES DE PRUEBAS: etiqueta del objetivo que buscar automaticamente.</summary>
		public const string VariableBuscar = "TERRAKEEP_WS6_BUSCAR";

		/// <summary>SOLO ARNES DE PRUEBAS: modo de juego (0..3) que intentar aplicar.</summary>
		public const string VariableDificultad = "TERRAKEEP_WS6_DIFICULTAD";

		/// <summary>SOLO ARNES DE PRUEBAS: marca de esta ejecucion, para identificar el log.</summary>
		public const string VariableMarca = "TERRAKEEP_WS6_MARCA";

		/// <summary>Milisegundos de CPU por fotograma que puede gastar la busqueda. Con 2 ms sobre
		/// un presupuesto de 16,6 ms por fotograma, la partida no se entera.</summary>
		public const double PresupuestoBusquedaMs = 2.0;

		private static ModKeybind _atajo;

		private static bool _autopruebaHecha;
		private static int _fotogramasEnMundo;

		/// <summary>El buscador de la sesion. Vive fuera del panel para que una busqueda pueda
		/// seguir avanzando aunque el panel se cierre (por ejemplo al saltar al mapa vanilla).</summary>
		public static readonly BuscadorMundo Buscador = new BuscadorMundo();

		/// <summary>true si se salto al mapa vanilla desde el panel: al cerrarse el mapa, se
		/// vuelve al panel en vez de dejar al jugador a medias.</summary>
		private static bool _volverAlPanelAlCerrarMapa;

		/// <summary>El atajo de Exploracion. Lo registra este sistema y lo LEE
		/// <c>PanelTerrakeepSystem</c>, que es quien abre el panel unico en esta pestaña.</summary>
		public static ModKeybind Atajo => _atajo;

		/// <summary>true si el panel esta abierto Y en la pestaña de Exploracion.</summary>
		public static bool PanelAbierto => PanelTerrakeepSystem.AreaAbiertaEs(AreaTerrakeep.Exploracion);

		/// <summary>El contenido de Exploracion montado ahora mismo, o null.</summary>
		public static ContenidoExploracion Panel
		{
			get
			{
				PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
				return panel != null ? panel.Exploracion : null;
			}
		}

		public override void Load()
		{
			RegistroExploracion.Mod = Mod;

			if (!Main.dedServ) {
				// P no esta asignada a nada en los controles por defecto de Terraria (comprobado en
				// PlayerInput.Reset del tModLoader.dll instalado: el perfil de teclado usa
				// WASD/Espacio/Escape/E/H/J/B/Tab/M/C/F1-F4 y los numeros, nunca la P), y las teclas
				// K, L, J, O e I ya las usan otros paneles de este mismo mod. Reasignable como
				// cualquier otra en Ajustes > Controles.
				_atajo = KeybindLoader.RegisterKeybind(Mod, "AbrirExploracion", Keys.P);
			}
		}

		public override void Unload()
		{
			_atajo = null;
			RegistroExploracion.Mod = null;
			CatalogoObjetivos.Descargar();
			MarcadoresExploracion.Limpiar();
			IconosExploracion.Descargar();
			Buscador.Cancelar();
		}

		public override void OnWorldUnload()
		{
			// Los resultados son coordenadas de ESTE mundo: en otro no significan nada.
			Buscador.Cancelar();
			MarcadoresExploracion.Limpiar();
			_volverAlPanelAlCerrarMapa = false;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// Primero y sin condiciones, por lo que ya documento WS0: ModKeybind.JustPressed puede
			// lanzar KeyNotFoundException durante los primeros fotogramas de una partida
			// (PlayerInput.Triggers todavia no conoce los atajos de mods) y abortaria el resto del
			// metodo, dejando la autoprueba sin dispararse nunca.
			ActualizarAutoprueba();
			AutopruebaExploracion.Avanzar();

			// La busqueda avanza aunque el panel este cerrado: asi "Ver en el mapa" a mitad de una
			// busqueda no la deja congelada.
			if (Buscador.EnMarcha) {
				Buscador.Avanzar(PresupuestoBusquedaMs);
				if (!Buscador.EnMarcha) {
					AlTerminarLaBusqueda();
				}
			}

			ComprobarVueltaDelMapa();
		}

		/// <summary>Abre el panel en la pestaña de Exploracion, o lo cierra si ya estaba ahi.</summary>
		public static void AlternarPanel(string origen)
		{
			PanelTerrakeepSystem.AlternarArea(AreaTerrakeep.Exploracion, origen);
		}

		public static void AbrirPanel(string origen)
		{
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}

			RegistroExploracion.Linea(
				Terrakeep.LogTag + " Exploracion: se pide abrir el panel via " + origen + ". " +
				"Mundo: \"" + MundoActual.Nombre + "\" " + MundoActual.TamanoLegible +
				", modo " + MundoActual.ModoDeJuegoLegible + " (Main.GameMode=" + Main.GameMode + ")" +
				", semilla " + MundoActual.Semilla +
				". Main.mapReady=" + Main.mapReady + ", Main.mapEnabled=" + Main.mapEnabled + ".");

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Exploracion, origen);
		}

		public static void CerrarPanel(string origen)
		{
			PanelTerrakeepSystem.CerrarPanel(origen);
		}

		/// <summary>
		/// La otra mitad del diseño hibrido: cierra el panel y abre el mapa REAL del juego a
		/// pantalla completa, centrado donde diga <paramref name="centroTile"/>, con los
		/// marcadores de Terrakeep encima (los pinta <see cref="CapaMapaExploracion"/>).
		/// </summary>
		/// <remarks>
		/// La secuencia es la misma que ejecuta el propio juego cuando pulsas el icono del mapa
		/// (codigo real de <c>Main.DrawInterface_Resources_Buffs</c> / la barra de iconos):
		/// <c>playerInventory = false</c>, sonido 10, <c>mapFullscreenScale</c>,
		/// <c>mapFullscreen = true</c>. La unica diferencia es que en vez de dejar
		/// <c>resetMapFull = true</c> (que centraria el mapa en el jugador) se escribe
		/// <c>mapFullscreenPos</c> a mano, que es lo que permite saltar directamente al resultado
		/// de una busqueda. El propio <c>DrawMap</c> se encarga de recortar esa posicion a los
		/// limites del mundo.
		/// </remarks>
		public static void VerEnElMapa(Vector2 centroTile, float escala, string origen)
		{
			CerrarPanel("salto al mapa vanilla (" + origen + ")");

			Main.playerInventory = false;
			Main.mapFullscreenScale = escala;
			Main.mapFullscreenPos = centroTile;
			Main.resetMapFull = false;
			Main.mapFullscreen = true;
			_volverAlPanelAlCerrarMapa = true;

			RegistroExploracion.Linea(
				Terrakeep.LogTag + " VER EN EL MAPA via " + origen + ": Main.mapFullscreen=" + Main.mapFullscreen +
				", mapFullscreenPos=(" + (int)Main.mapFullscreenPos.X + ", " + (int)Main.mapFullscreenPos.Y + ")" +
				", mapFullscreenScale=" + Main.mapFullscreenScale +
				", marcadores de Terrakeep pendientes de dibujar=" + MarcadoresExploracion.Resultados.Count +
				" (\"" + MarcadoresExploracion.Titulo + "\").");
		}

		/// <summary>
		/// Si el jugador cierra el mapa vanilla (Esc, M o el icono de la esquina) y habiamos
		/// saltado ahi desde el panel, se vuelve al panel. Sin esto, "Ver en el mapa" seria un
		/// billete de ida.
		/// </summary>
		private static void ComprobarVueltaDelMapa()
		{
			if (!_volverAlPanelAlCerrarMapa || Main.mapFullscreen) {
				return;
			}

			_volverAlPanelAlCerrarMapa = false;
			if (Main.gameMenu) {
				return;
			}

			RegistroExploracion.Linea(
				Terrakeep.LogTag + " El mapa vanilla se ha cerrado; se vuelve al panel de Exploracion. " +
				"Fotogramas en los que la capa de mapa de Terrakeep dibujo marcadores: " +
				CapaMapaExploracion.FotogramasDibujados +
				" (ultimo fotograma: " + CapaMapaExploracion.DibujadosUltimoFotograma + " marcadores).");
			AbrirPanel("vuelta del mapa vanilla");
		}

		/// <summary>Empieza una busqueda y deja los marcadores listos para los dos mapas.</summary>
		public static void Buscar(ObjetivoBusqueda objetivo, bool soloExplorado, string origen)
		{
			if (objetivo == null) {
				return;
			}

			MarcadoresExploracion.Limpiar();
			Buscador.Empezar(objetivo, soloExplorado);

			RegistroExploracion.Linea(Terrakeep.LogTag + " BUSQUEDA empezada via " + origen + ": " + Buscador.Resumen());

			// Cofres y NPC terminan en el acto (no recorren el mundo), asi que hay que rematarlos
			// aqui: no van a pasar nunca por el Avanzar de UpdateUI.
			if (Buscador.Terminada) {
				AlTerminarLaBusqueda();
			}
		}

		private static void AlTerminarLaBusqueda()
		{
			MarcadoresExploracion.Fijar(
				Buscador.Objetivo != null ? Buscador.Objetivo.EtiquetaLegible() : "",
				Buscador.Resultados,
				ColorDeCategoria(Buscador.Objetivo));

			RegistroExploracion.Linea(Terrakeep.LogTag + " BUSQUEDA terminada: " + Buscador.Resumen());

			int enseñar = Math.Min(5, Buscador.Resultados.Count);
			for (int i = 0; i < enseñar; i++) {
				ResultadoBusqueda resultado = Buscador.Resultados[i];
				RegistroExploracion.Linea(Terrakeep.LogTag + "   - " + resultado.Etiqueta +
					" en el tile (" + (int)resultado.Tile.X + ", " + (int)resultado.Tile.Y + "), a " +
					(int)resultado.DistanciaAlJugador + " tiles del jugador.");
			}
		}

		/// <summary>Color de los marcadores segun la categoria. Son los tonos del propio juego para
		/// esas cosas, no colores inventados.</summary>
		public static Color ColorDeCategoria(ObjetivoBusqueda objetivo)
		{
			if (objetivo == null) {
				return new Color(255, 210, 120);
			}

			switch (objetivo.Categoria) {
				case "Minerales": return new Color(255, 200, 90);
				case "Gemas": return new Color(200, 130, 255);
				case "Tesoros": return new Color(255, 120, 150);
				case "Contenedores": return new Color(255, 235, 150);
				case "Líquidos": return new Color(120, 200, 255);
				case "Paredes": return new Color(170, 190, 220);
				default: return new Color(255, 210, 120);
			}
		}

		// ---------------------------------------------------------------------------------------
		// Arnes de pruebas
		// ---------------------------------------------------------------------------------------

		private static void ActualizarAutoprueba()
		{
			if (_autopruebaHecha || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableAutoprueba))) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;
			// 180 fotogramas de margen, igual que las autopruebas de los demas workstreams: da
			// tiempo a que el mundo termine de cargar y a que el mapa se haya dibujado al menos una
			// vez (Main.mapReady no se pone a true hasta que DrawToMap ha corrido).
			if (_fotogramasEnMundo < 180) {
				return;
			}

			_autopruebaHecha = true;
			AutopruebaExploracion.Arrancar();
		}
	}
}
