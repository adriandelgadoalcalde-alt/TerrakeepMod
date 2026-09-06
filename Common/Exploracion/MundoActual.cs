using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.IO;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Lectura de los datos del mundo cargado ahora mismo (nombre, semilla, tamaño, modo de
	/// juego, progreso). Todo sale de campos reales de <see cref="Main"/> y de
	/// <see cref="Main.ActiveWorldFileData"/>: ni un archivo abierto, ni una copia intermedia.
	/// </summary>
	public static class MundoActual
	{
		/// <summary>true si hay un mundo cargado del que se pueda enseñar algo.</summary>
		public static bool HayMundo => !Main.gameMenu && Main.ActiveWorldFileData != null;

		public static string Nombre => Main.worldName ?? "";

		/// <summary>Semilla en el formato en que la enseña el propio juego (incluye las semillas
		/// secretas si las hay). Vacia en mundos generados antes de que existiera el campo.</summary>
		public static string Semilla
		{
			get {
				WorldFileData datos = Main.ActiveWorldFileData;
				if (datos == null) {
					return "";
				}
				if (!datos.HasValidSeed) {
					return "(el mundo no guarda semilla)";
				}
				return datos.GetFullSeedText();
			}
		}

		public static int Ancho => Main.maxTilesX;

		public static int Alto => Main.maxTilesY;

		/// <summary>Nombre del tamaño tal y como lo llama el juego (Pequeño/Mediano/Grande), o las
		/// medidas en bruto si es un tamaño que el juego no reconoce (mundos de mods).</summary>
		public static string TamanoLegible
		{
			get {
				WorldFileData datos = Main.ActiveWorldFileData;
				string nombre = datos != null ? datos.WorldSizeName : null;
				string medidas = Main.maxTilesX + "x" + Main.maxTilesY;
				return string.IsNullOrEmpty(nombre) ? medidas : nombre + " (" + medidas + ")";
			}
		}

		/// <summary>Total de tiles del mundo. Es el tamaño real de los arrays que recorre la
		/// busqueda, asi que se enseña para que se entienda lo que cuesta buscar.</summary>
		public static long TotalTiles => (long)Main.maxTilesX * Main.maxTilesY;

		/// <summary>Identificador numerico del modo de juego actual (ver <see cref="GameModeID"/>).</summary>
		public static int ModoDeJuego => Main.GameMode;

		public static string ModoDeJuegoLegible => NombreDeModo(Main.GameMode);

		public static string NombreDeModo(int modo)
		{
			switch (modo) {
				case GameModeID.Normal: return "Clásico";
				case GameModeID.Expert: return "Experto";
				case GameModeID.Master: return "Maestro";
				case GameModeID.Creative: return "Viaje";
				default: return "Desconocido (" + modo + ")";
			}
		}

		/// <summary>Los cuatro modos que ofrece el panel, en el mismo orden que el menu del juego.</summary>
		public static IEnumerable<int> ModosDisponibles()
		{
			yield return GameModeID.Normal;
			yield return GameModeID.Expert;
			yield return GameModeID.Master;
			yield return GameModeID.Creative;
		}

		public static bool EsHardmode => Main.hardMode;

		/// <summary>Mal del mundo: corrupcion o carmesi. Sale del propio archivo del mundo.</summary>
		public static string MalDelMundo
		{
			get {
				WorldFileData datos = Main.ActiveWorldFileData;
				if (datos == null) {
					return "(desconocido)";
				}
				return datos.HasCrimson ? "Carmesí" : "Corrupción";
			}
		}

		/// <summary>Semillas secretas activas, si las hay. Cambian bastante el comportamiento del
		/// mundo, asi que merece la pena enseñarlas junto a la dificultad.</summary>
		public static string SemillasSecretas
		{
			get {
				List<string> activas = new List<string>();
				if (Main.drunkWorld) { activas.Add("Mundo borracho"); }
				if (Main.getGoodWorld) { activas.Add("Por los que lo valen"); }
				if (Main.tenthAnniversaryWorld) { activas.Add("10º aniversario"); }
				if (Main.notTheBeesWorld) { activas.Add("¡Las abejas no!"); }
				if (Main.dontStarveWorld) { activas.Add("Don't Starve"); }
				if (Main.remixWorld) { activas.Add("Remix"); }
				if (Main.noTrapsWorld) { activas.Add("Sin trampas"); }
				if (Main.zenithWorld) { activas.Add("Cénit"); }
				return activas.Count == 0 ? "(ninguna)" : string.Join(", ", activas);
			}
		}

		/// <summary>Posicion de aparicion, en tiles.</summary>
		public static string PuntoDeAparicion => Main.spawnTileX + ", " + Main.spawnTileY;

		/// <summary>Posicion del jugador, en tiles. Se recalcula cada vez que se pregunta.</summary>
		public static string PosicionDelJugador
		{
			get {
				Player jugador = Main.LocalPlayer;
				if (jugador == null || !jugador.active) {
					return "(sin jugador)";
				}
				return (int)(jugador.Center.X / 16f) + ", " + (int)(jugador.Center.Y / 16f);
			}
		}

		/// <summary>Cuanto del mundo se ha descubierto ya, en tanto por ciento. Se calcula
		/// muestreando (no tile a tile) porque esto se pinta en cada fotograma: se mira uno de
		/// cada 16 en las dos dimensiones, o sea 1 de cada 256, que para un porcentaje sobra.</summary>
		public static float PorcentajeExplorado()
		{
			if (Main.Map == null || !Main.mapEnabled) {
				return 0f;
			}

			int vistos = 0;
			int mirados = 0;
			for (int x = 0; x < Main.maxTilesX; x += 16) {
				for (int y = 0; y < Main.maxTilesY; y += 16) {
					mirados++;
					if (Main.Map.IsRevealed(x, y)) {
						vistos++;
					}
				}
			}

			return mirados == 0 ? 0f : (float)vistos / mirados * 100f;
		}
	}
}
