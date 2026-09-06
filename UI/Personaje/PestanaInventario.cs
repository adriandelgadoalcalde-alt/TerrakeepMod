using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Pestaña "Inventario": las 50 ranuras de la mochila, las 4 de monedas y las 4 de municion,
	/// todas sobre el array vivo <c>Main.LocalPlayer.inventory</c>.
	/// <para />
	/// El reparto de ese array esta comprobado en el juego instalado
	/// (<c>inventory = new Item[59]</c>): 0-49 mochila, 50-53 monedas, 54-57 municion y 58 una
	/// ranura comodin que el juego usa internamente y que aqui no se enseña a proposito.
	/// <para />
	/// Cada ranura usa el <c>ItemSlot</c> nativo con su contexto correcto
	/// (<c>InventoryItem</c> / <c>InventoryCoin</c> / <c>InventoryAmmo</c>), asi que el fondo, lo
	/// que acepta cada hueco, el clic derecho, el apilado y los tooltips son los de vanilla sin
	/// reimplementar nada.
	/// </summary>
	public class PestanaInventario : UIElement
	{
		private const float Escala = 0.9f;

		public PestanaInventario()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			Item[] inventario = PersonajeVivo.Jugador.inventory;
			float paso = RejillaSlots.Paso(Escala);

			Titulo("Personaje.Inventario.Mochila", 0f, 0f);
			RejillaSlots.Rejilla(this, inventario, 0, PersonajeVivo.SlotsPrincipales,
				ItemSlot.Context.InventoryItem, 10, Escala, 0f, 24f);

			float derecha = 10f * paso + 30f;

			Titulo("Personaje.Inventario.Monedas", derecha, 0f);
			RejillaSlots.Rejilla(this, inventario, PersonajeVivo.PrimerSlotMonedas, 4,
				ItemSlot.Context.InventoryCoin, 4, Escala, derecha, 24f);

			Titulo("Personaje.Inventario.Municion", derecha, 24f + paso + 12f);
			RejillaSlots.Rejilla(this, inventario, PersonajeVivo.PrimerSlotMunicion, 4,
				ItemSlot.Context.InventoryAmmo, 4, Escala, derecha, 24f + paso + 36f);

			EtiquetaTk ayuda = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Inventario.Nota"),
				0.75f, 900f, 20f);
			ayuda.ColorTexto = EstiloTk.TextoSuave;
			ayuda.Left.Set(0f, 0f);
			ayuda.Top.Set(24f + 5f * paso + 10f, 0f);
			Append(ayuda);

			EtiquetaTk ocupacion = new EtiquetaTk(TextoOcupacion, 0.8f, 400f, 20f);
			ocupacion.Left.Set(derecha, 0f);
			ocupacion.Top.Set(24f + 2f * paso + 46f, 0f);
			Append(ocupacion);
		}

		/// <summary>Rotulo de una zona de la pestaña. Recibe la CLAVE de localizacion, no el texto:
		/// asi se resuelve en cada dibujado y cambia con el idioma sin reabrir el panel.</summary>
		private void Titulo(string clave, float izquierda, float arriba)
		{
			EtiquetaTk etiqueta = new EtiquetaTk(() => Idiomas.Texto(clave), 0.85f, 500f, 22f);
			etiqueta.ColorTexto = EstiloTk.TextoSuave;
			etiqueta.Left.Set(izquierda, 0f);
			etiqueta.Top.Set(arriba, 0f);
			Append(etiqueta);
		}

		private static string TextoOcupacion()
		{
			Item[] inventario = PersonajeVivo.Jugador.inventory;
			int ocupadas = 0;
			for (int i = 0; i < PersonajeVivo.SlotsPrincipales; i++) {
				if (!inventario[i].IsAir) {
					ocupadas++;
				}
			}
			return Idiomas.Texto("Personaje.Inventario.Ocupacion", ocupadas, PersonajeVivo.SlotsPrincipales);
		}
	}
}
