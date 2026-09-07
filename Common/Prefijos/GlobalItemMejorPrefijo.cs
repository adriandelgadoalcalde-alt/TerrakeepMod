using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Prefijos
{
	/// <summary>
	/// Indicador de calidad de prefijo: añade una linea al tooltip REAL del juego cuando el
	/// prefijo actual de un objeto NO es el mejor que ese objeto concreto puede llevar, segun
	/// <see cref="CatalogoMejorPrefijo"/>.
	/// </summary>
	/// <remarks>
	/// <b>Por que un <c>GlobalItem.ModifyTooltips</c> y no un widget propio</b>: es el mismo
	/// tooltip REAL que ya usan las ranuras del mod (<c>ItemSlot.MouseHover</c>, ver
	/// <c>SlotCatalogoLibreria</c>) y CUALQUIER ranura vanilla del Inventario, chest, etc. - un
	/// unico gancho cubre "Libreria y/o Inventario" a la vez, sin construir ni mantener una ficha
	/// de objeto propia que ya duplicaria lo que el juego enseña solo.
	/// <para />
	/// <b>Tambien se ve en la Libreria sobre el objeto de MUESTRA</b> (el que crea
	/// <c>SlotCatalogoLibreria</c> con <c>SetDefaults</c>, sin prefijo, <c>prefix == 0</c>): eso es
	/// intencionado, no un caso raro a filtrar. Un objeto recien sacado del catalogo TODAVIA no
	/// tiene el mejor prefijo (se lo pone <c>ContenidoLibreria.PedirObjeto</c> al cogerlo), asi
	/// que enseñar aqui el mejor prefijo posible es exactamente la pista que pide el problema 1:
	/// "que prefijo puede llegar a tener este objeto en concreto".
	/// </remarks>
	public class GlobalItemMejorPrefijo : GlobalItem
	{
		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (item == null || item.type <= 0) {
				return;
			}

			byte? mejor = CatalogoMejorPrefijo.MejorPrefijo(item.type);
			if (!mejor.HasValue || item.prefix == mejor.Value) {
				return;
			}

			// Nombre real del prefijo, tal cual lo llama el juego en el idioma activo - nada de
			// tabla propia, mismo criterio que CatalogoVivo con los nombres de objeto.
			string nombrePrefijo = Lang.prefix[mejor.Value].Value;

			TooltipLine linea = new TooltipLine(Mod, "TerrakeepMejorPrefijo",
				Idiomas.Texto("Prefijos.MejorPrefijo", nombrePrefijo));
			linea.OverrideColor = EstiloTk.TextoAviso;
			tooltips.Add(linea);
		}
	}
}
