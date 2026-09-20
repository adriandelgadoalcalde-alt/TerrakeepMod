using System.Collections.Generic;
using Terraria;
using Terraria.Achievements;
using Terraria.GameContent.Bestiary;
using Terraria.Localization;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Investigacion;
using BiomasBestiario = Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes;

namespace TerrakeepMod.Common.Completitud
{
	/// <summary>Un resumen de progreso: cuantos de cuantos, mas la lista de lo que falta (nombre
	/// ya traducido) para poder enseñarla tal cual.</summary>
	public sealed class ResumenCompletitud
	{
		public int Hecho;
		public int Total;
		public readonly List<string> Faltan = new List<string>();

		/// <summary>Idea 8 del catálogo de funciones ("checklist de coleccionista"): subtotales
		/// "Nombre: hecho/total" (nunca nombres de lo NO descubierto - mismo criterio que
		/// <see cref="Faltan"/> evita para Jefes/Logros, aplicado aquí a categorías en vez de a
		/// bichos concretos). Vacía para los resúmenes que no tienen un desglose con sentido.</summary>
		public readonly List<string> Desglose = new List<string>();

		public float Fraccion => Total > 0 ? (float)Hecho / Total : 1f;
	}

	/// <summary>
	/// La pieza real de "vista de qué falta para el 100%" del encargo: cuatro resúmenes de
	/// progreso, cada uno sobre datos REALES del juego que el mod ya sabe leer (o que el propio
	/// motor expone de forma pública) - nunca un número inventado.
	/// <para />
	/// <b>Jefes/eventos</b>: reutiliza el árbol de la Guía (<see cref="CatalogoGuia.Tramos"/>),
	/// que ya cataloga 21 tramos reales con su bandera citada contra el motor - no se vuelve a
	/// investigar nada, se reutiliza el mismo criterio de "tramo superado" que ya usa
	/// <c>EstadoGuia</c> (el último paso del tramo, completo).
	/// <para />
	/// <b>Bestiario</b>: <c>Main.BestiaryDB</c>/<c>Main.BestiaryTracker</c>, el sistema oficial de
	/// 1.4 - un bicho cuenta como "conocido" si su <c>UnlockState</c> ya pasó de
	/// <c>NotKnownAtAll_0</c> (visto, matado o hablado con él), el mismo criterio real con el que
	/// el propio juego decide qué enseñar en su pantalla del bestiario.
	/// <para />
	/// <b>Logros</b>: <c>Main.Achievements</c>, el <c>AchievementManager</c> oficial.
	/// <para />
	/// <b>Objetos investigados</b>: <c>EstadoInvestigacion</c> (Modo Viaje), que ya usa la propia
	/// pestaña "Investigación" del mod - aquí solo se suman los tipos investigables uno a uno
	/// (nunca sumando las carpetas del árbol de <c>CatalogoInvestigacion</c> directamente: varias
	/// carpetas raíz son vistas ALTERNATIVAS de los mismos objetos - "Categorías" y "Objetos por
	/// ID" - sumarlas habría contado el mismo objeto dos o tres veces).
	/// </summary>
	public static class EstadoCompletitud
	{
		public static ResumenCompletitud Jefes()
		{
			ResumenCompletitud resumen = new ResumenCompletitud();
			foreach (TramoGuia tramo in CatalogoGuia.Tramos) {
				if (!tramo.Implementado || tramo.Pasos.Count == 0) {
					continue;
				}
				resumen.Total++;
				bool superado = EvaluadorGuia.PasoCompletado(tramo.Pasos[tramo.Pasos.Count - 1]);
				if (superado) {
					resumen.Hecho++;
				}
				else {
					resumen.Faltan.Add(tramo.Nombre());
				}
			}
			return resumen;
		}

		/// <summary>
		/// Las etiquetas REALES de bioma que usa el propio juego para sus botones de filtro del
		/// Bestiario (investigado con el decompilado real de <c>tModLoader.dll</c>:
		/// <c>BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions</c> separa sus condiciones en
		/// CUATRO grupos - <c>Biomes</c>, <c>Events</c>, <c>Invasions</c>, <c>Times</c> - y esta es
		/// la lista completa del primero, la única categoría real de "bioma" sin mezclar hora del
		/// día, clima o invasiones). Se usan las instancias TAL CUAL: <c>Filters.ByInfoElement</c>,
		/// el filtro real que usa la propia pantalla de Bestiario de vanilla, comprueba pertenencia
		/// con <c>entry.Info.Contains(instancia)</c> - el mismo camino que <see cref="Bestiario"/>
		/// usa aquí para contar, no un camino inventado.
		/// </summary>
		private static readonly Terraria.GameContent.Bestiary.SpawnConditionBestiaryInfoElement[] EtiquetasDeBioma = {
			BiomasBestiario.Surface, BiomasBestiario.Underground, BiomasBestiario.Caverns,
			BiomasBestiario.TheUnderworld, BiomasBestiario.TheDungeon,
			BiomasBestiario.TheCorruption, BiomasBestiario.UndergroundCorruption,
			BiomasBestiario.CorruptDesert, BiomasBestiario.CorruptIce, BiomasBestiario.CorruptUndergroundDesert,
			BiomasBestiario.TheCrimson, BiomasBestiario.UndergroundCrimson,
			BiomasBestiario.CrimsonDesert, BiomasBestiario.CrimsonIce, BiomasBestiario.CrimsonUndergroundDesert,
			BiomasBestiario.TheHallow, BiomasBestiario.UndergroundHallow,
			BiomasBestiario.HallowDesert, BiomasBestiario.HallowIce, BiomasBestiario.HallowUndergroundDesert,
			BiomasBestiario.Jungle, BiomasBestiario.UndergroundJungle, BiomasBestiario.TheTemple,
			BiomasBestiario.Snow, BiomasBestiario.UndergroundSnow,
			BiomasBestiario.Desert, BiomasBestiario.UndergroundDesert, BiomasBestiario.Oasis,
			BiomasBestiario.Ocean, BiomasBestiario.Sky, BiomasBestiario.Graveyard, BiomasBestiario.SpiderNest,
			BiomasBestiario.Granite, BiomasBestiario.Marble, BiomasBestiario.Meteor,
			BiomasBestiario.SurfaceMushroom, BiomasBestiario.UndergroundMushroom,
			BiomasBestiario.NebulaPillar, BiomasBestiario.SolarPillar, BiomasBestiario.VortexPillar,
			BiomasBestiario.StardustPillar
		};

		public static ResumenCompletitud Bestiario()
		{
			ResumenCompletitud resumen = new ResumenCompletitud();
			if (Main.BestiaryDB == null || Main.BestiaryTracker == null) {
				return resumen;
			}

			// hecho[i]/total[i] van paralelos a EtiquetasDeBioma: un mismo bicho puede pertenecer a
			// VARIOS biomas a la vez (ej. una criatura de Cavernas Y de Jungla subterranea), asi
			// que no es una particion - cada bioma se cuenta por su cuenta, igual que hacen los
			// propios botones de filtro de vanilla (no son mutuamente excluyentes tampoco alli).
			int[] hechoPorBioma = new int[EtiquetasDeBioma.Length];
			int[] totalPorBioma = new int[EtiquetasDeBioma.Length];

			foreach (BestiaryEntry entrada in Main.BestiaryDB.Entries) {
				if (entrada.UIInfoProvider == null) {
					continue;
				}
				resumen.Total++;
				BestiaryEntryUnlockState estado = entrada.UIInfoProvider.GetEntryUICollectionInfo().UnlockState;
				bool descubierto = estado != BestiaryEntryUnlockState.NotKnownAtAll_0;
				if (descubierto) {
					resumen.Hecho++;
				}

				for (int i = 0; i < EtiquetasDeBioma.Length; i++) {
					if (entrada.Info.Contains(EtiquetasDeBioma[i])) {
						totalPorBioma[i]++;
						if (descubierto) {
							hechoPorBioma[i]++;
						}
					}
				}
			}

			// Ordenado por total descendente (los biomas con mas bichos reales primero) y despues
			// por nombre, para que el orden sea siempre el mismo entre partidas - nunca el orden
			// interno de EtiquetasDeBioma, que no tiene ningun significado para el jugador.
			List<(string nombre, int hecho, int total)> filas = new List<(string, int, int)>();
			for (int i = 0; i < EtiquetasDeBioma.Length; i++) {
				if (totalPorBioma[i] == 0) {
					continue;
				}
				string nombre = Language.GetTextValue(EtiquetasDeBioma[i].GetDisplayNameKey());
				filas.Add((nombre, hechoPorBioma[i], totalPorBioma[i]));
			}
			filas.Sort((a, b) => {
				int porTotal = b.total.CompareTo(a.total);
				return porTotal != 0 ? porTotal : string.CompareOrdinal(a.nombre, b.nombre);
			});
			foreach (var fila in filas) {
				resumen.Desglose.Add(Idiomas.Texto("Completitud.Bestiario.FilaBioma", fila.nombre, fila.hecho, fila.total));
			}

			// No se listan NOMBRES DE BICHOS aqui a proposito: uno sin descubrir no tiene nombre
			// que enseñar sin spoilear el propio bestiario (asi funciona el bestiario de vanilla).
			// El desglose de arriba son subtotales por bioma, nunca criaturas concretas.
			return resumen;
		}

		public static ResumenCompletitud Logros()
		{
			ResumenCompletitud resumen = new ResumenCompletitud();
			if (Main.Achievements == null) {
				return resumen;
			}

			foreach (Achievement logro in Main.Achievements.CreateAchievementsList()) {
				resumen.Total++;
				if (logro.IsCompleted) {
					resumen.Hecho++;
				}
				else {
					// Los logros marcados "Hidden" (secretos) no enseñan su nombre hasta
					// completarse, mismo criterio que la pantalla de logros de vanilla - un
					// "???" real, no un nombre inventado.
					resumen.Faltan.Add(logro.Hidden
						? Idiomas.Texto("Completitud.LogroOculto")
						: logro.FriendlyName.Value);
				}
			}
			return resumen;
		}

		public static ResumenCompletitud Investigacion()
		{
			ResumenCompletitud resumen = new ResumenCompletitud();
			for (int tipo = 1; tipo < ItemLoader.ItemCount; tipo++) {
				if (!EstadoInvestigacion.EsInvestigable(tipo)) {
					continue;
				}
				resumen.Total++;
				if (EstadoInvestigacion.Completo(tipo)) {
					resumen.Hecho++;
				}
			}
			// Tampoco se listan aqui: con miles de objetos investigables, una lista de "lo que
			// falta" no cabria ni seria legible - la propia pestaña "Investigación" del mod ya es
			// el sitio real para explorar eso carpeta a carpeta.
			return resumen;
		}
	}
}
