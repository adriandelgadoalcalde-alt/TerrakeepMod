using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terrakeep.Core.Guia;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Fachada del mod hacia el cerebro UNICO de la Guia (consolidacion T1,
	/// I+D-PROXIMOS-PASOS-FAMILIA-KEEP.md, 16-sep-2026).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Que se movio y que se quedo.</b> Antes de esta ronda, este archivo tenia su propia copia
	/// (identica funcion a funcion) del despacho de requisitos y de la aritmetica de progreso
	/// (<c>Fraccion</c>/<c>Contar</c>/<c>Preparacion</c>/<c>PasoCompletado</c>) que tambien vivia en
	/// <c>Terrakeep.Core/Guia/GuideEvaluator.cs</c>, sincronizadas solo de memoria entre los dos
	/// repos. ESO es lo que se ha movido: ahora vive UNA sola vez en
	/// <c>Terrakeep.Core.Guia.GuideEvaluationEngine</c>, y este archivo solo le pasa
	/// <see cref="ProveedorEstadoGuiaMod"/> (la "fuente de estado" que lee el juego en vivo -
	/// <see cref="EstadoJugadorGuia"/>/<see cref="BanderasGuia"/>). El CONTRATO sigue siendo el
	/// mismo de siempre: un requisito cuyo tipo no se reconozca se marca no evaluable y
	/// <b>nunca</b> cuenta como cumplido.
	/// </para>
	/// <para>
	/// <b>Lo que NO se movio, a proposito.</b> <see cref="StatsDeJefe"/>, <see cref="NombreDeObjeto"/>
	/// y <see cref="NombreDeNpc"/> no son aritmetica de progreso duplicada - nunca lo fueron
	/// (Terrakeep.Core no tiene equivalente de <c>StatsDeJefe</c>: escalar la vida/daño/defensa
	/// real de un jefe exige <c>NPC.ScaleStats</c>, que solo existe con el motor de Terraria
	/// cargado). Se quedan aqui, en el mod, sin tocar.
	/// </para>
	/// </remarks>
	public static class EvaluadorGuia
	{
		private static readonly ProveedorEstadoGuiaMod _proveedor = new ProveedorEstadoGuiaMod();

		/// <summary>Comprueba un requisito contra la partida real.</summary>
		public static ResultadoRequisito Evaluar(RequisitoGuia requisito)
		{
			if (requisito == null) {
				return new ResultadoRequisito {
					Requisito = null,
					NoEvaluable = true,
					TextoClave = "Guia.Req.NoEvaluable",
					TextoArgs = new object[] { "(null)" }
				};
			}
			return GuideEvaluationEngine.Evaluar(requisito, _proveedor);
		}

		/// <summary>Los requisitos de un paso, ya evaluados, en el orden del .json.</summary>
		public static List<ResultadoRequisito> Evaluar(PasoGuia paso)
		{
			return paso == null ? new List<ResultadoRequisito>() : GuideEvaluationEngine.Evaluar(paso, _proveedor);
		}

		/// <summary>
		/// true si el paso se puede dar por hecho: todos sus requisitos <b>obligatorios</b> estan
		/// cumplidos. Los recomendados no bloquean (ver <see cref="RequisitoGuia.Recomendado"/>).
		/// </summary>
		public static bool PasoCompletado(PasoGuia paso)
		{
			return paso != null && GuideEvaluationEngine.PasoCompletado(paso, _proveedor);
		}

		/// <summary>
		/// Medidor de preparacion de un paso, de 0 a 1. Un requisito obligatorio pesa el doble que
		/// uno recomendado, y cada uno aporta su progreso PARCIAL (3 vecinos de 4 son 0,75 de ese
		/// requisito, no un cero).
		/// </summary>
		public static float Preparacion(PasoGuia paso, out int cumplidos, out int totalObligatorios)
		{
			cumplidos = 0;
			totalObligatorios = 0;
			if (paso == null) {
				return 0f;
			}
			return GuideEvaluationEngine.Preparacion(paso, _proveedor, out cumplidos, out totalObligatorios);
		}

		// -------------------------------------------------------------------------------------
		// Lo que NO es aritmetica de progreso duplicada: se queda aqui, tal cual estaba.
		// -------------------------------------------------------------------------------------

		/// <summary>Nombre de un objeto en el idioma del juego. Forwarding a
		/// <see cref="ProveedorEstadoGuiaMod.NombreDeObjeto"/> para que el resto del mod (la
		/// autoprueba incluida) no tenga que conocer la fachada de datos.</summary>
		public static string NombreDeObjeto(int tipo) => _proveedor.NombreDeObjeto(tipo);

		/// <summary>Nombre de un NPC en el idioma del juego.</summary>
		public static string NombreDeNpc(int tipo) => _proveedor.NombreDeNpc(tipo);

		/// <summary>
		/// Vida, daño y defensa REALES de un jefe en ESTA partida, escalados por el propio motor.
		/// </summary>
		/// <remarks>
		/// No es una tabla copiada de ningun sitio: se coge la muestra del NPC
		/// (<c>ContentSamples.NpcsByNetId</c>), se clona y se le llama a su propio
		/// <c>NPC.ScaleStats(jugadores, Main.GameModeInfo, null)</c> - el metodo publico real que
		/// usa el juego cuando el jefe aparece de verdad, con los multiplicadores de modo
		/// (<c>GameModeData</c>: normal x1, experto x2 vida y daño, maestro x3) y el ajuste por
		/// numero de jugadores. Por eso el numero que enseña la guia es el que el jugador se va a
		/// encontrar, y no "la vida del wiki". Esto no tiene equivalente en Terrakeep de escritorio
		/// (no hay motor cargado del que pedir <c>ScaleStats</c>), asi que nunca fue codigo
		/// duplicado.
		/// </remarks>
		public static bool StatsDeJefe(int tipoNpc, out int vida, out int dano, out int defensa)
		{
			vida = 0;
			dano = 0;
			defensa = 0;

			NPC muestra;
			if (tipoNpc <= 0 || !ContentSamples.NpcsByNetId.TryGetValue(tipoNpc, out muestra) || muestra == null) {
				return false;
			}

			NPC copia = new NPC();
			copia.SetDefaults(tipoNpc);
			copia.ScaleStats(null, Main.GameModeInfo, null);

			vida = copia.lifeMax;
			dano = copia.damage;
			defensa = copia.defense;
			return true;
		}
	}
}
