using System;
using System.Collections.Generic;
using Terraria;

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
	/// Solo estan las que usa hoy el arbol de progresion de vanilla. Añadir una es una linea.
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
			return new Dictionary<string, Func<bool>> {
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
				{ "downedMartians", () => NPC.downedMartians }
			};
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

		public static void Descargar()
		{
			_tabla = null;
		}
	}
}
