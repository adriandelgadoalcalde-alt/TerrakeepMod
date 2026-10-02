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
				// Auditoria 16-sep-2026: los bulbos de Plantera exigen los TRES mecanicos
				// (WorldGen.cs:68792, 'downedMechBoss1 && downedMechBoss2 && downedMechBoss3'),
				// no "cualquiera". Misma composicion que Condition.DownedMechBossAll del propio
				// juego (Condition.cs:207); no existe como campo suelto en NPC.cs, por eso se
				// compone aqui. GuideFlags.cs (Terrakeep de escritorio) la resuelve igual.
				{ "downedMechBossAll", () => NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3 },
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
					Main.LocalPlayer != null && Main.LocalPlayer.downedDD2EventAnyDifficulty },

				// --- Guia v2 (F3, 02-oct-2026), pedidas por el contenido vanilla de F1 (seccion 12 de
				// docs/guia-v2-diseno.md del repo hermano). Campos estaticos REALES del 1.4.4.9:
				// DD2Event.DownedInvasionT1..T3 (DD2Event.cs:25-29, una por nivel del Antiguo Ejercito,
				// guardadas en el .wld) y NPC.combatBookWasUsed / combatBookVolumeTwoWasUsed /
				// peddlersSatchelWasUsed (NPC.cs:802-806, las mejoras de mundo de los libros de combate
				// y la bolsa del buhonero). Mismos nombres que GuideFlags del escritorio.
				{ "downedDD2InvasionT1", () => Terraria.GameContent.Events.DD2Event.DownedInvasionT1 },
				{ "downedDD2InvasionT2", () => Terraria.GameContent.Events.DD2Event.DownedInvasionT2 },
				{ "downedDD2InvasionT3", () => Terraria.GameContent.Events.DD2Event.DownedInvasionT3 },
				{ "combatBookWasUsed", () => NPC.combatBookWasUsed },
				{ "combatBookVolumeTwoWasUsed", () => NPC.combatBookVolumeTwoWasUsed },
				{ "peddlersSatchelWasUsed", () => NPC.peddlersSatchelWasUsed }
			};

			// Contrato con la logica pura (ContratoGuiaV2.BanderasVanilla), que es la que comprueban
			// las pruebas contra el contenido real de la guia: si las dos listas se separan, se dice.
			foreach (string nombre in GuiaV2.ContratoGuiaV2.BanderasVanilla) {
				if (!tabla.ContainsKey(nombre)) {
					RegistroGuia.Aviso(Terrakeep.LogTag + " Guia: ContratoGuiaV2.BanderasVanilla cita \"" + nombre +
						"\" y la tabla de BanderasGuia no la tiene.");
				}
			}
			if (tabla.Count != GuiaV2.ContratoGuiaV2.BanderasVanilla.Length) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " Guia: la tabla vanilla de BanderasGuia (" + tabla.Count +
					") y ContratoGuiaV2.BanderasVanilla (" + GuiaV2.ContratoGuiaV2.BanderasVanilla.Length + ") no coinciden.");
			}

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
		/// <remarks>Guia v2 (F3, 02-oct-2026): la lista vive ahora en <c>ContratoGuiaV2.BanderasCalamity</c>
		/// (logica pura, probada contra el contenido real de la guia) y suma <c>downedEoCAcidRain</c>,
		/// <c>downedAquaticScourgeAcidRain</c> y <c>downedBossRush</c> (DownedBossSystem.cs 2.2.4,
		/// lineas 830, 849 y 880). El Boss Rush entra porque la guia v2 tiene una parada final de
		/// desafio, opcional y aplazable; sigue sin ser progresion obligatoria.</remarks>
		private static readonly string[] NombresBanderasCalamity = GuiaV2.ContratoGuiaV2.BanderasCalamity;

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
