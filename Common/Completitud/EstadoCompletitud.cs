using System.Collections.Generic;
using Terraria;
using Terraria.Achievements;
using Terraria.GameContent.Bestiary;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Investigacion;

namespace TerrakeepMod.Common.Completitud
{
	/// <summary>Un resumen de progreso: cuantos de cuantos, mas la lista de lo que falta (nombre
	/// ya traducido) para poder enseñarla tal cual.</summary>
	public sealed class ResumenCompletitud
	{
		public int Hecho;
		public int Total;
		public readonly List<string> Faltan = new List<string>();

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

		public static ResumenCompletitud Bestiario()
		{
			ResumenCompletitud resumen = new ResumenCompletitud();
			if (Main.BestiaryDB == null || Main.BestiaryTracker == null) {
				return resumen;
			}

			foreach (BestiaryEntry entrada in Main.BestiaryDB.Entries) {
				if (entrada.UIInfoProvider == null) {
					continue;
				}
				resumen.Total++;
				BestiaryEntryUnlockState estado = entrada.UIInfoProvider.GetEntryUICollectionInfo().UnlockState;
				if (estado != BestiaryEntryUnlockState.NotKnownAtAll_0) {
					resumen.Hecho++;
				}
			}
			// No se listan nombres aqui a proposito: un bicho SIN descubrir no tiene nombre que
			// enseñar sin spoilear el propio bestiario (asi funciona el bestiario de vanilla).
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
