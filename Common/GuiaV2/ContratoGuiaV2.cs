using System.Text;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Logica PURA de la Guia v2 en el mod (sin Terraria ni FNA), enlazada tal cual en
	/// TerrakeepMod.LogicaPura para que TerrakeepMod.Tests la pruebe contra el contenido REAL
	/// incrustado en lib\Terrakeep.Core.dll: nombres de las banderas que el mod sabe leer, la
	/// consulta que usa "Coger en la Librería" y la forma legible de una condicion del codigo.
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

		/// <summary>Condicion del codigo (p. ej. "CalamityConditions.DownedOldDuke") en forma legible:
		/// sin espacio de nombres y con las palabras separadas. No traduce ni interpreta: es el
		/// nombre real del codigo.</summary>
		public static string Legible(string condicion)
		{
			if (string.IsNullOrEmpty(condicion)) {
				return "";
			}
			string c = condicion.Trim();
			int paren = c.IndexOf('(');
			string cabeza = paren > 0 ? c.Substring(0, paren) : c;
			int punto = cabeza.LastIndexOf('.');
			if (punto >= 0 && paren < 0) {
				c = cabeza.Substring(punto + 1);
			}
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < c.Length; i++) {
				char ch = c[i];
				if (i > 0 && char.IsUpper(ch) && char.IsLower(c[i - 1])) {
					sb.Append(' ');
				}
				sb.Append(ch);
			}
			return sb.ToString();
		}
	}
}
