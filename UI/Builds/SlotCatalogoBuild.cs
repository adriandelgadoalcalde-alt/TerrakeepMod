using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.Common.Builds;

namespace TerrakeepMod.UI.Builds
{
	/// <summary>
	/// Slot de CATALOGO: enseña un objeto recomendado por una build con el dibujado nativo de
	/// Terraria (<see cref="ItemSlot"/>), incluido el tooltip real del juego al pasar el raton.
	/// </summary>
	/// <remarks>
	/// A diferencia de <see cref="SlotObjetoVanilla"/> (WS0), este slot <b>no llama nunca a
	/// <c>ItemSlot.Handle</c></b>: opera sobre un ejemplar de muestra propio, no sobre un array
	/// vivo del jugador, asi que dejar coger el objeto con el raton seria sacarlo de la nada. Lo
	/// unico interactivo que hace es enseñar el tooltip con <c>ItemSlot.MouseHover</c>.
	/// <para />
	/// El estado "ya lo tienes" se pinta con las propias texturas del juego: el fondo del slot
	/// (<c>Main.inventoryBack</c>, estado global que se guarda y se restaura, igual que
	/// <c>Main.inventoryScale</c>) y el color con el que <c>ItemSlot.Draw</c> tiñe el icono.
	/// </remarks>
	public class SlotCatalogoBuild : UIElement
	{
		private static readonly Color FondoLoTienes = new Color(70, 170, 90);
		private static readonly Color FondoNoLoTienes = new Color(90, 90, 110);
		private static readonly Color IconoNoLoTienes = new Color(120, 120, 120, 190);

		private readonly Item[] _muestra = new Item[1];
		private float _escala;

		/// <summary>El objeto del catalogo que representa este slot.</summary>
		public readonly ObjetoBuild Objeto;

		/// <summary>Lo recalcula el panel en cada Update contra el inventario real del jugador.</summary>
		public bool LoTiene;

		/// <summary>Donde lo tiene, si lo tiene (para el texto de al lado).</summary>
		public string DondeLoTiene;

		/// <summary>
		/// Escala de dibujado, cambiable despues de crear la ranura: cuando la ventana es baja no
		/// caben las 7 filas de accesorios y hay que encogerlas (ver
		/// <c>ContenidoBuilds.ColocarFilasDeObjetos</c>). Es lo mismo que ya hace
		/// <c>SlotObjetoVanilla</c> en la pestaña de Equipo.
		/// </summary>
		public float Escala {
			get { return _escala; }
			set {
				_escala = value;
				Width.Set(52f * value, 0f);
				Height.Set(52f * value, 0f);
			}
		}

		public SlotCatalogoBuild(ObjetoBuild objeto, float escala = 0.85f)
		{
			Objeto = objeto;
			_escala = escala;

			_muestra[0] = new Item();
			if (objeto.Resuelto) {
				_muestra[0].SetDefaults(objeto.Tipo);
			}

			Width.Set(52f * escala, 0f);
			Height.Set(52f * escala, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			// ItemSlot.Draw lee la escala y el color de fondo de estado GLOBAL del juego, no de
			// parametros. Hay que guardarlos y restaurarlos o se descuadra/destiñe el inventario
			// normal al cerrar el panel.
			float escalaPrevia = Main.inventoryScale;
			Color fondoPrevio = Main.inventoryBack;

			Main.inventoryScale = _escala;
			Main.inventoryBack = LoTiene ? FondoLoTienes : FondoNoLoTienes;

			Rectangle rect = GetDimensions().ToRectangle();
			Color colorIcono = LoTiene ? Color.White : IconoNoLoTienes;

			ItemSlot.Draw(spriteBatch, _muestra, ItemSlot.Context.ChestItem, 0, rect.TopLeft(), colorIcono);

			if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface) {
				// Sin esto, el clic atraviesa el panel y el jugador ataca o coloca bloques detras.
				Main.LocalPlayer.mouseInterface = true;
				// Tooltip real del juego (nombre, estadisticas, prefijo...). No modifica nada.
				ItemSlot.MouseHover(_muestra, ItemSlot.Context.ChestItem, 0);
			}

			Main.inventoryScale = escalaPrevia;
			Main.inventoryBack = fondoPrevio;
		}
	}
}
