using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.Common.Investigacion;

namespace TerrakeepMod.UI.Investigacion
{
	/// <summary>
	/// Slot de MUESTRA de un objeto del catalogo, con el dibujado y el tooltip nativos de
	/// Terraria (<see cref="ItemSlot"/>), teñido segun lo investigado que este.
	/// </summary>
	/// <remarks>
	/// Igual que <c>SlotCatalogoBuild</c> de WS4, <b>no llama nunca a <c>ItemSlot.Handle</c></b>:
	/// esto no es una ranura del inventario del jugador sino un ejemplar de muestra, y dejar
	/// cogerlo con el raton seria sacar un objeto de la nada. Lo unico interactivo es el tooltip
	/// real del juego al pasar por encima.
	/// <para />
	/// El estado se pinta con las propias texturas del juego, cambiando temporalmente el estado
	/// global del que ItemSlot los lee (<c>Main.inventoryScale</c> y <c>Main.inventoryBack</c>) y
	/// devolviendolos a su valor, o se descuadraria y destiñiria el inventario normal al cerrar.
	/// </remarks>
	public class SlotMuestraInvestigacion : UIElement
	{
		private readonly Item[] _muestra = new Item[1];
		private readonly float _escala;

		/// <summary>Tipo de objeto que representa este slot.</summary>
		public readonly int Tipo;

		public SlotMuestraInvestigacion(int tipo, float escala = 0.72f)
		{
			Tipo = tipo;
			_escala = escala;

			_muestra[0] = new Item();
			_muestra[0].SetDefaults(tipo);

			Width.Set(52f * escala, 0f);
			Height.Set(52f * escala, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			int hechas = EstadoInvestigacion.Hecho(Tipo);
			int necesarias = EstadoInvestigacion.Necesario(Tipo);
			bool completo = necesarias > 0 && hechas >= necesarias;

			float escalaPrevia = Main.inventoryScale;
			Color fondoPrevio = Main.inventoryBack;

			Main.inventoryScale = _escala;
			Main.inventoryBack = completo
				? EstiloInvestigacion.FondoSlotHecho
				: (hechas > 0 ? EstiloInvestigacion.FondoSlotAMedias : EstiloInvestigacion.FondoSlotSinEmpezar);

			Rectangle rect = GetDimensions().ToRectangle();
			Color colorIcono = completo ? Color.White : EstiloInvestigacion.IconoSinEmpezar;

			ItemSlot.Draw(spriteBatch, _muestra, ItemSlot.Context.ChestItem, 0, rect.TopLeft(), colorIcono);

			if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface) {
				// Sin esto el clic atraviesa el panel y el jugador ataca o coloca bloques detras.
				Main.LocalPlayer.mouseInterface = true;
				ItemSlot.MouseHover(_muestra, ItemSlot.Context.ChestItem, 0);
			}

			Main.inventoryScale = escalaPrevia;
			Main.inventoryBack = fondoPrevio;
		}
	}
}
