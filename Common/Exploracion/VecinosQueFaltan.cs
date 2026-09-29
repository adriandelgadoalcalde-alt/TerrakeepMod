using System.Collections.Generic;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Paridad con Terrakeep escritorio 3.3.0 (commit <c>ec916e8b</c>, idea 1 del catalogo de
	/// funciones: "añadir/quitar NPCs de pueblo del mundo"): que vecinos de la lista oficial
	/// (<c>Terrakeep.Core.Data.VanillaTownNpcRoster</c>, la MISMA que usa escritorio, compartida via
	/// <c>lib/Terrakeep.Core.dll</c>) no viven todavia en el mundo.
	/// <para />
	/// Aritmetica pura, sin Terraria (enlazada en <c>TerrakeepMod.LogicaPura</c> para
	/// <c>TerrakeepMod.Tests</c>): recibe la lista y los tipos presentes como datos. El equivalente
	/// de escritorio es <c>ExplorationViewModel.RebuildMissingNpcs</c>, que hace lo mismo sobre la
	/// lista de NPC leida del <c>.wld</c>; aqui la lista sale de <c>Main.npc</c> en vivo.
	/// </summary>
	public static class VecinosQueFaltan
	{
		/// <summary>Los tipos de <paramref name="roster"/> que no estan en
		/// <paramref name="presentes"/>, en el MISMO orden de la lista oficial y sin repetidos.</summary>
		public static List<int> Calcular(IEnumerable<int> roster, IEnumerable<int> presentes)
		{
			var hay = new HashSet<int>();
			if (presentes != null) {
				foreach (int tipo in presentes) {
					hay.Add(tipo);
				}
			}

			var faltan = new List<int>();
			if (roster == null) {
				return faltan;
			}
			var yaListados = new HashSet<int>();
			foreach (int tipo in roster) {
				if (!hay.Contains(tipo) && yaListados.Add(tipo)) {
					faltan.Add(tipo);
				}
			}
			return faltan;
		}
	}
}
