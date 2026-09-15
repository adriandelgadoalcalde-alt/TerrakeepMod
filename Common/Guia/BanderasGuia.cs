using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Las banderas permanentes de la partida que la guia sabe leer, por nombre.
	/// </summary>
	/// <remarks>
	/// <b>Los nombres son los campos REALES del motor</b>, no alias inventados: se sacaron
	/// listando los identificadores <c>downed*</c> del <c>NPC.cs</c> decompilado de la version
	/// instalada, para que el <c>.json</c> pueda citarlos tal cual y cualquiera pueda ir a
	/// comprobarlos al codigo del juego. Si un dia se pide una bandera que no esta en esta
	/// tabla, el evaluador la marca como <b>no evaluable</b> y lo dice: nunca la da por cumplida.
	/// <para />
	/// Las de vanilla, mas abajo. Las de <see cref="AgregarBanderasCalamity"/>, por reflexion
	/// contra <c>CalamityMod.DownedBossSystem</c> (ver esa cabecera) - las dos tablas se
	/// fusionan aqui sin que el resto del mod tenga que saber la diferencia.
	/// </remarks>
	public static class BanderasGuia
	{
		private static Dictionary<string, Func<bool>> _tabla;

		private static Dictionary<string, Func<bool>> Tabla {
			get {
				if (_tabla == null) {
					_tabla = Construir();
				}
				return _tabla;
			}
		}

		private static Dictionary<string, Func<bool>> Construir()
		{
			Dictionary<string, Func<bool>> tabla = new Dictionary<string, Func<bool>> {
				// --- jefes de la progresion principal, en orden ---------------------------------
				{ "downedSlimeKing", () => NPC.downedSlimeKing },
				{ "downedBoss1", () => NPC.downedBoss1 },                 // Ojo de Cthulhu
				{ "downedBoss2", () => NPC.downedBoss2 },                 // Devorador de Mundos / Cerebro
				{ "downedQueenBee", () => NPC.downedQueenBee },
				{ "downedBoss3", () => NPC.downedBoss3 },                 // Esqueletron
				{ "downedDeerclops", () => NPC.downedDeerclops },
				{ "hardMode", () => Main.hardMode },                      // lo enciende el Muro de Carne
				{ "downedQueenSlime", () => NPC.downedQueenSlime },
				{ "downedMechBoss1", () => NPC.downedMechBoss1 },         // Destructor
				{ "downedMechBoss2", () => NPC.downedMechBoss2 },         // Gemelos
				{ "downedMechBoss3", () => NPC.downedMechBoss3 },         // Esqueletron Prime
				{ "downedMechBossAny", () => NPC.downedMechBossAny },
				{ "downedPlantBoss", () => NPC.downedPlantBoss },
				{ "downedGolemBoss", () => NPC.downedGolemBoss },
				{ "downedFishron", () => NPC.downedFishron },
				{ "downedEmpressOfLight", () => NPC.downedEmpressOfLight },
				{ "downedAncientCultist", () => NPC.downedAncientCultist },
				{ "downedTowers", () => NPC.downedTowers },
				{ "downedMoonlord", () => NPC.downedMoonlord },

				// --- progreso a medias, no solo "jefe muerto" ------------------------------------
				// Persiste de verdad: se guarda en el .wld (WorldFile.cs, WorldGen.shadowOrbSmashed)
				// y solo se borra al crear un mundo nuevo (WorldGen.cs, la funcion que limpia todas
				// las banderas de una partida al generarla). Sirve para decir "ya has roto una" sin
				// fingir que se sabe CUANTAS: el contador real, shadowOrbCount, es modulo 3 (vuelve a
				// 0 en cuanto invoca al jefe), asi que un "vas 2 de 3" dejaria de ser cierto justo
				// cuando mas importa.
				{ "shadowOrbSmashed", () => WorldGen.shadowOrbSmashed },

				// --- eventos y rescates que abren cosas ------------------------------------------
				{ "downedGoblins", () => NPC.downedGoblins },
				{ "savedGoblin", () => NPC.savedGoblin },
				{ "savedMech", () => NPC.savedMech },
				{ "savedWizard", () => NPC.savedWizard },
				{ "downedPirates", () => NPC.downedPirates },
				{ "downedMartians", () => NPC.downedMartians },
				// NPC.cs: "downedFrost" (no "downedFrostLegion") es el nombre real del campo que
				// enciende la Legion de Escarcha al derrotarla.
				{ "downedFrost", () => NPC.downedFrost },

				// --- Luna de Calabazas y Luna Helada: DOS y TRES banderas, un jefe de oleada cada
				// una (NPC.cs, no hay una bandera unica de "evento completo" para ninguno de los dos) --
				{ "downedHalloweenTree", () => NPC.downedHalloweenTree },     // Mourning Wood
				{ "downedHalloweenKing", () => NPC.downedHalloweenKing },     // Pumpking
				{ "downedChristmasTree", () => NPC.downedChristmasTree },     // Everscream
				{ "downedChristmasSantank", () => NPC.downedChristmasSantank }, // Santa-NK1
				{ "downedChristmasIceQueen", () => NPC.downedChristmasIceQueen }, // Reina de Hielo

				// --- Antiguo Ejercito D2: la PRIMERA bandera de la tabla que lee Player en vez de
				// NPC/WorldGen/Main (Player.cs, ~linea 2171/23413/55965). Player.cs comprobado:
				// se guarda y se lee del .plr sin condicion, como cualquier otro downed*.
				{ "downedDD2EventAnyDifficulty", () =>
					Main.LocalPlayer != null && Main.LocalPlayer.downedDD2EventAnyDifficulty }
			};

			AgregarBanderasCalamity(tabla);
			return tabla;
		}

		/// <summary>
		/// Los 37 nombres REALES de <c>CalamityMod.DownedBossSystem</c> que usa el arbol de
		/// Calamity de la Guia (de las 44 propiedades publicas que tiene la clase en la version
		/// 2.2.2 instalada, se quedan fuera <c>startedBossRushAtLeastOnce</c>/<c>downedBossRush</c>
		/// -el Boss Rush es un modo de desafio aparte, nunca progresion real- y
		/// <c>downedSecondSentinels</c> -unica propiedad de las 44 sin el envoltorio
		/// <c>NPC.SetEventFlagCleared</c> de las demas y sin ningun sitio real del ensamblado que
		/// la ponga a <c>true</c>: parece un campo heredado de una version vieja, sin uso hoy).
		/// Confirmado DECOMPILANDO <c>CalamityMod.dll</c> v2.2.2 instalado con <c>ilspycmd</c> el
		/// 15-sep-2026 (<c>ilspycmd -t CalamityMod.DownedBossSystem CalamityMod.dll</c>), no una
		/// lista de memoria - ver bitacora.md para el detalle completo de esa sesion.
		/// </summary>
		private static readonly string[] NombresBanderasCalamity = {
			"downedDesertScourge", "downedCrabulon", "downedHiveMind", "downedPerforator",
			"downedSlimeGod", "downedDreadnautilus", "downedCryogen", "downedAquaticScourge",
			"downedBrimstoneElemental", "downedCalamitasClone", "downedLeviathan",
			"downedAstrumAureus", "downedPlaguebringer", "downedRavager", "downedAstrumDeus",
			"downedGuardians", "downedDragonfolly", "downedProvidence", "downedCeaselessVoid",
			"downedStormWeaver", "downedSignus", "downedPolterghast", "downedBoomerDuke",
			"downedDoG", "downedYharon", "downedExoMechs", "downedCalamitas",
			"downedPrimordialWyrm", "downedHorribleHog", "downedCLAM", "downedCLAMHardMode",
			"downedCragmawMire", "downedGSS", "downedMauler", "downedNuclearTerror",
			"downedAres", "downedThanatos", "downedArtemisAndApollo"
		};

		/// <summary>
		/// Añade las banderas de Calamity a la tabla, leidas por REFLEXION contra el ensamblado ya
		/// cargado del mod.
		/// </summary>
		/// <remarks>
		/// <b>Por que reflexion y no una referencia normal.</b> <c>TerrakeepMod.csproj</c>/
		/// <c>build.txt</c> no traen <c>CalamityMod</c> en <c>dllReferences</c> a proposito -
		/// Calamity es un mod OPCIONAL de la partida, no una dependencia dura del mod (el jugador
		/// puede no tenerlo instalado, y el mod tiene que seguir funcionando igual de bien sin el)
		/// - asi que no hay ningun <c>CalamityMod.dll</c> contra el que compilar en forma de tipos
		/// C# reales. La unica via real para leer <c>CalamityMod.DownedBossSystem.downedProvidence</c>
		/// (una PROPIEDAD publica y estatica, confirmada por decompilacion, no un campo) sin esa
		/// referencia es preguntarle al ensamblado ya cargado por su nombre completo, exactamente
		/// el mismo patron que ya usa <c>CatalogoMejorPrefijo</c> para leer
		/// <c>ModItem.Mod.Name</c>/<c>ModItem.Name</c> de un objeto de Calamity sin referenciar el
		/// tipo real.
		/// <para />
		/// <b>Que pasa si Calamity cambia una propiedad en una actualizacion futura.</b>
		/// <c>Type.GetProperty</c> devuelve <c>null</c> para una que ya no exista con ese nombre
		/// exacto: esa bandera en concreto se queda fuera de la tabla (nunca en la tabla con un
		/// valor inventado), asi que <see cref="Existe"/> devuelve <c>false</c> para ella y el
		/// evaluador la enseña como "no evaluable" - el mismo contrato honesto que ya tiene
		/// cualquier bandera de vanilla desconocida, sin que un cambio de Calamity pueda tirar el
		/// mod ni fingir un progreso que no es real.
		/// </remarks>
		private static void AgregarBanderasCalamity(Dictionary<string, Func<bool>> tabla)
		{
			if (!CatalogoGuia.HayCalamity) {
				return;
			}

			Mod calamity = ModLoader.GetMod(CatalogoGuia.NombreModCalamity);
			Type tipo = calamity != null
				? calamity.GetType().Assembly.GetType("CalamityMod.DownedBossSystem")
				: null;
			if (tipo == null) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " Guia: Calamity esta cargado pero no se " +
					"encontro CalamityMod.DownedBossSystem por reflexion (¿version del mod con " +
					"otra estructura interna?) - ninguna bandera de Calamity sera evaluable esta " +
					"partida.");
				return;
			}

			for (int i = 0; i < NombresBanderasCalamity.Length; i++) {
				string nombre = NombresBanderasCalamity[i];
				PropertyInfo propiedad = tipo.GetProperty(nombre, BindingFlags.Public | BindingFlags.Static);
				if (propiedad == null) {
					RegistroGuia.Aviso(Terrakeep.LogTag + " Guia: CalamityMod.DownedBossSystem." +
						nombre + " no existe en esta version del mod - se queda como no evaluable.");
					continue;
				}
				PropertyInfo capturada = propiedad;
				tabla[nombre] = () => (bool)capturada.GetValue(null);
			}
		}

		/// <summary>true si la bandera existe en la tabla.</summary>
		public static bool Existe(string nombre)
		{
			return !string.IsNullOrEmpty(nombre) && Tabla.ContainsKey(nombre);
		}

		/// <summary>Valor real de la bandera ahora mismo. Solo tiene sentido si
		/// <see cref="Existe"/> devuelve true.</summary>
		public static bool Valor(string nombre)
		{
			Func<bool> leer;
			return Tabla.TryGetValue(nombre ?? "", out leer) && leer();
		}

		/// <summary>
		/// Escribe una bandera de Calamity DE VERDAD (no solo la lee) - solo para
		/// <c>AutopruebaGuia</c>, que necesita poder marcar "superados" a mano los tramos
		/// opcionales de Calamity con Orden mas bajo que ReySlime/EjercitoGoblin/etc., exactamente
		/// igual que ya hace con <c>NPC.downedSlimeKing</c> y compañia, para que el resto del
		/// recorrido (escrito antes de que existiera el arbol de Calamity) no se desvie. Usa la
		/// MISMA propiedad publica (con su setter real, confirmado por decompilacion) que ya lee
		/// <see cref="AgregarBanderasCalamity"/> - nunca el campo interno <c>_downedX</c>.
		/// Devuelve false sin lanzar nada si Calamity no esta cargado o la propiedad no existe (la
		/// autoprueba lo trata como "no hay nada que neutralizar aqui").
		/// </summary>
		public static bool IntentarEscribirBanderaCalamity(string nombre, bool valor)
		{
			if (!CatalogoGuia.HayCalamity) {
				return false;
			}

			Mod calamity = ModLoader.GetMod(CatalogoGuia.NombreModCalamity);
			Type tipo = calamity != null
				? calamity.GetType().Assembly.GetType("CalamityMod.DownedBossSystem")
				: null;
			if (tipo == null) {
				return false;
			}

			PropertyInfo propiedad = tipo.GetProperty(nombre, BindingFlags.Public | BindingFlags.Static);
			if (propiedad == null || !propiedad.CanWrite) {
				return false;
			}

			propiedad.SetValue(null, valor);
			return true;
		}

		public static void Descargar()
		{
			_tabla = null;
		}
	}
}
