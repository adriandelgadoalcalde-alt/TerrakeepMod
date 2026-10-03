using System.Text;
using System.Text.RegularExpressions;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Logica PURA de la Guia v2 en el mod (sin Terraria ni FNA), enlazada tal cual en
	/// TerrakeepMod.LogicaPura para que TerrakeepMod.Tests la pruebe contra el contenido REAL
	/// incrustado en lib\Terrakeep.Core.dll: nombres de las banderas que el mod sabe leer, la
	/// consulta que usa "Coger en la Librería".
	/// </summary>
	public static class ContratoGuiaV2
	{
		/// <summary>F2b (02-oct-2026, una sola guia): los rombos dorados de la brujula v1 (zona del
		/// paso v1 en el mapa a pantalla completa) solo se dibujan si la Guia v2 NO esta marcando su
		/// siguiente parada; con las dos a la vez el mapa podia señalar dos objetivos distintos.</summary>
		public static bool BrujulaV1EnElMapa(bool hayGuiaV2, bool marcaV2Visible) => !(hayGuiaV2 && marcaV2Visible);

		/// <summary>Banderas vanilla de <c>BanderasGuia</c> (mismos nombres que su tabla; el propio
		/// BanderasGuia avisa en el log si las dos listas se separan).</summary>
		public static readonly string[] BanderasVanilla = {
			"downedSlimeKing", "downedBoss1", "downedBoss2", "downedQueenBee", "downedBoss3", "downedDeerclops",
			"hardMode", "downedQueenSlime", "downedMechBoss1", "downedMechBoss2", "downedMechBoss3", "downedMechBossAny",
			"downedMechBossAll", "downedPlantBoss", "downedGolemBoss", "downedFishron", "downedEmpressOfLight",
			"downedAncientCultist", "downedTowers", "downedMoonlord", "shadowOrbSmashed", "downedGoblins", "savedGoblin",
			"savedMech", "savedWizard", "downedPirates", "downedMartians", "downedFrost", "downedHalloweenTree",
			"downedHalloweenKing", "downedChristmasTree", "downedChristmasSantank", "downedChristmasIceQueen",
			"downedDD2EventAnyDifficulty",
			// Guia v2 vanilla (F1/F3, 02-oct-2026).
			"downedDD2InvasionT1", "downedDD2InvasionT2", "downedDD2InvasionT3",
			"combatBookWasUsed", "combatBookVolumeTwoWasUsed", "peddlersSatchelWasUsed",
		};

		/// <summary>Propiedades REALES de <c>CalamityMod.DownedBossSystem</c> que el mod lee por
		/// reflexion (ver la cabecera de BanderasGuia). Las tres ultimas las añadio la Guia v2.</summary>
		public static readonly string[] BanderasCalamity = {
			"downedDesertScourge", "downedCrabulon", "downedHiveMind", "downedPerforator",
			"downedSlimeGod", "downedDreadnautilus", "downedCryogen", "downedAquaticScourge",
			"downedBrimstoneElemental", "downedCalamitasClone", "downedLeviathan",
			"downedAstrumAureus", "downedPlaguebringer", "downedRavager", "downedAstrumDeus",
			"downedGuardians", "downedDragonfolly", "downedProvidence", "downedCeaselessVoid",
			"downedStormWeaver", "downedSignus", "downedPolterghast", "downedBoomerDuke",
			"downedDoG", "downedYharon", "downedExoMechs", "downedCalamitas",
			"downedPrimordialWyrm", "downedHorribleHog", "downedCLAM", "downedCLAMHardMode",
			"downedCragmawMire", "downedGSS", "downedMauler", "downedNuclearTerror",
			"downedAres", "downedThanatos", "downedArtemisAndApollo",
			"downedEoCAcidRain", "downedAquaticScourgeAcidRain", "downedBossRush",
		};

		/// <summary>Consulta que escribe "Coger en la Librería" en el buscador de la Librería: el
		/// nombre del objeto (lo que el jugador reconoce) o, si la gramatica de la Librería no lo
		/// admitiria entero (la coma es "O"; menos de 2 letras se ignora), el id exacto "#id".</summary>
		public static string ConsultaLibreria(string nombre, int tipo)
		{
			if (string.IsNullOrEmpty(nombre) || nombre.IndexOf(',') >= 0 || nombre.Trim().Length < 2 ||
				nombre.TrimStart().StartsWith("#") || nombre.TrimStart().StartsWith(".")) {
				return "#" + tipo;
			}
			return nombre.Trim();
		}

		/// <summary>Parche 0.8.1: lo que se escribe bajo el sprite de un objeto (escalera de equipo,
		/// rejilla): SOLO el ID numerico ("ID 5098") o nada. Nunca el nombre interno "CalamityMod/Clase".</summary>
		public static string IdVisible(int tipo) => tipo > 0 ? "ID " + tipo : "";

		private static readonly (string Nombre, Regex Patron)[] RestosProhibidos = {
			("marca sin resolver", new Regex(@"[{}]")),
			("nombre interno Mod/Clase", new Regex(@"\b(CalamityMod|Terraria|ModLoader)/\w")),
			("ruta de codigo", new Regex(@"\.cs\b")),
			("identificador de codigo", new Regex(@"\b(Condition|Conditions|CalamityConditions|DropHelper|DownedBossSystem|Main|NPC)\.[A-Za-z]")),
			("clave de localizacion", new Regex(@"\bGuia(V2)?\.[A-Za-z]|\bMods\.TerrakeepMod")),
			("expresion de codigo", new Regex(@"=>|\(\)|\bout var\b")),
			("caracter roto", new Regex("\uFFFD")),
			("Cualquiera + Nombre", new Regex(@"\bCualquiera [A-ZÁÉÍÓÚ]")),
		};

		/// <summary>Primer resto tecnico que NO debe ver el jugador en un texto ("nombre interno Mod/Clase:
		/// CalamityMod/B"), o null si esta limpio. Lo usan la autoprueba en el juego y los tests.</summary>
		public static string RestoTecnico(string texto)
		{
			if (string.IsNullOrEmpty(texto)) {
				return null;
			}
			foreach ((string nombre, Regex patron) in RestosProhibidos) {
				Match m = patron.Match(texto);
				if (m.Success) {
					return nombre + ": " + m.Value;
				}
			}
			return null;
		}

		// Parche 0.8.1: la forma legible de una condicion del codigo salio de aqui y vive en
		// Terrakeep.Core.Guia.V2.GuiaV2Condiciones (compartida con Terrakeep escritorio). Antes
		// devolvia el nombre del codigo con las palabras separadas ("Downed Old Duke"), que es
		// justo lo que el jugador no debe ver.
	}
}
