using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Completitud
{
	/// <summary>
	/// Idea 8 del catálogo de funciones ("checklist de coleccionista"), pieza que faltaba: "dónde
	/// conseguirlo" - una línea real en el TOOLTIP del objeto (mismo mecanismo real que ya usa
	/// <see cref="TerrakeepMod.Common.Prefijos.GlobalItemMejorPrefijo"/>,
	/// <c>GlobalItem.ModifyTooltips</c> - un único gancho que cubre la Librería Y cualquier ranura
	/// vanilla del juego a la vez, sin construir una ficha de objeto propia) diciendo de dónde sale
	/// ese objeto en ESTA partida: la PRIMERA receta real que lo fabrica, o si no tiene ninguna, la
	/// PRIMERA tienda real que lo vende.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por qué está cacheado y no se recalcula cada fotograma.</b> Un tooltip se pide MUCHAS
	/// veces por segundo mientras el ratón está encima de un objeto - recorrer las ~3000 recetas
	/// reales (<c>Recipe.numRecipes</c>) y las tiendas reales
	/// (<c>Terraria.ModLoader.NPCShopDatabase.AllShops</c>) en cada llamada sería un coste real de
	/// rendimiento en TODO el juego, no solo en la Librería. Se construyen dos diccionarios
	/// <c>tipo → texto</c> UNA sola vez (la primera vez que hace falta), igual que
	/// <c>CatalogoMejorPrefijo</c>/<c>CatalogoBuilds.Listo</c> ya cachean sus propias tablas.
	/// </para>
	/// <para>
	/// <b>Límite real investigado, no una decisión de alcance sin mirar</b>: los objetos que SOLO
	/// salen de matar a un enemigo (sin receta ni tienda) se quedan fuera. El decompilado real
	/// (<c>Terraria.GameContent.ItemDropRules.ItemDropDatabase.GetRulesForItemID(tipo)</c>) SÍ
	/// existe y devuelve las reglas reales que producen ese objeto - pero esas reglas no llevan
	/// pegado "qué NPC las registró": el registro real es NPC → reglas
	/// (<c>RegisterToNPC</c>/<c>RegisterToMultipleNPCs</c>), nunca al revés, y no hay ningún método
	/// público que resuelva objeto → NPC directamente. Hacerlo de verdad exigiría recorrer TODOS
	/// los NPC reales, pedir sus reglas (<c>Main.ItemDropsDB.GetRulesForNPCID</c>) y bajar el árbol
	/// entero de cada <c>IItemDropRule</c> (nodos condicionales, de opciones, de porcentaje - un
	/// sistema real con su propia forma de árbol) comprobando si ESE tipo de objeto puede salir de
	/// él - un sistema aparte entero, no una línea de más. Se documenta como límite real para otra
	/// sesión (investigado a fondo, con la cita exacta de por qué), no se finge una fuente que no
	/// se ha investigado.
	/// </para>
	/// </remarks>
	public class GlobalItemDondeConseguir : GlobalItem
	{
		private static Dictionary<int, string> _cacheReceta;
		private static Dictionary<int, string> _cacheTienda;

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (item == null || item.type <= 0) {
				return;
			}

			AsegurarCache();

			string texto;
			if (!_cacheReceta.TryGetValue(item.type, out texto)) {
				_cacheTienda.TryGetValue(item.type, out texto);
			}
			if (texto == null) {
				return;
			}

			TooltipLine linea = new TooltipLine(Mod, "TerrakeepDondeConseguir", texto);
			linea.OverrideColor = EstiloTk.TextoSuave;
			tooltips.Add(linea);
		}

		private static void AsegurarCache()
		{
			if (_cacheReceta != null) {
				return;
			}

			_cacheReceta = new Dictionary<int, string>();
			for (int i = 0; i < Recipe.numRecipes; i++) {
				Recipe receta = Main.recipe[i];
				if (receta == null || receta.createItem == null || receta.createItem.type <= 0) {
					continue;
				}
				int tipo = receta.createItem.type;
				// La PRIMERA receta real que se encuentra para cada objeto - hay objetos con varias
				// recetas alternativas, pero para "dónde conseguirlo" basta con una vía real.
				if (_cacheReceta.ContainsKey(tipo)) {
					continue;
				}

				List<string> ingredientes = new List<string>();
				for (int k = 0; k < receta.requiredItem.Count && ingredientes.Count < 3; k++) {
					Item ingrediente = receta.requiredItem[k];
					if (ingrediente == null || ingrediente.IsAir) {
						continue;
					}
					ingredientes.Add(ingrediente.Name);
				}
				if (ingredientes.Count == 0) {
					continue;
				}

				string masIngredientes = receta.requiredItem.Count > ingredientes.Count
					? Idiomas.Texto("Completitud.DondeConseguir.RecetaMas", receta.requiredItem.Count - ingredientes.Count)
					: "";
				_cacheReceta[tipo] = Idiomas.Texto("Completitud.DondeConseguir.Receta",
					string.Join(", ", ingredientes) + masIngredientes);
			}

			_cacheTienda = new Dictionary<int, string>();
			foreach (Terraria.ModLoader.AbstractNPCShop tienda in Terraria.ModLoader.NPCShopDatabase.AllShops) {
				foreach (Terraria.ModLoader.AbstractNPCShop.Entry entrada in tienda.ActiveEntries) {
					if (entrada.Item == null || entrada.Item.type <= 0) {
						continue;
					}
					int tipo = entrada.Item.type;
					if (_cacheTienda.ContainsKey(tipo)) {
						continue;
					}

					NPC muestra;
					string nombreNpc = ContentSamples.NpcsByNetId.TryGetValue(tienda.NpcType, out muestra) && muestra != null
						? muestra.FullName
						: "#" + tienda.NpcType;
					_cacheTienda[tipo] = Idiomas.Texto("Completitud.DondeConseguir.Tienda", nombreNpc);
				}
			}
		}

		/// <summary>SOLO ARNES DE PRUEBAS: fuerza a reconstruir la cache (por si una autoprueba
		/// necesita comprobarla desde cero en una partida sintética recién creada).</summary>
		public static void InvalidarCacheParaPrueba()
		{
			_cacheReceta = null;
			_cacheTienda = null;
		}

		/// <summary>SOLO ARNES DE PRUEBAS: el texto real que le tocaría a este tipo de objeto, o
		/// null si no hay receta ni tienda reales conocidas.</summary>
		public static string TextoParaPrueba(int tipo)
		{
			AsegurarCache();
			string texto;
			if (_cacheReceta.TryGetValue(tipo, out texto)) {
				return texto;
			}
			_cacheTienda.TryGetValue(tipo, out texto);
			return texto;
		}
	}
}
