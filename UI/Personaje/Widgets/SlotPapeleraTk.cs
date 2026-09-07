using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// La papelera REAL de Terraria, la misma que dibuja <c>Main.DrawTrashItemSlot</c> en el HUD
	/// de vanilla junto al bestiario y los emotes (comprobado en el <c>tModLoader.dll</c>
	/// instalado, v2026.7.3.0): mismo campo (<see cref="Player.trashItem"/>) y mismo contexto de
	/// <see cref="ItemSlot"/> (<c>Context.TrashItem</c> = 6). No es una papelera propia
	/// reinventada: es literalmente la papelera del juego, con su icono de cubo de basura
	/// (<c>TextureAssets.Trash</c>, lo dibuja <c>ItemSlot.Draw</c> solo cuando el slot esta vacio)
	/// colocada dentro del panel del mod, porque con <c>IngameFancyUI</c> abierto la fila de
	/// iconos del HUD (bestiario/emotes/papelera) no llega a dibujarse - su capa
	/// (<c>"Vanilla: Fancy UI"</c>, la 14) corta el recorrido antes de llegar a la del HUD.
	/// <para />
	/// Arrastrar un objeto encima hace EXACTAMENTE lo mismo que en vanilla:
	/// <see cref="ItemSlot.Handle(ref Item, int)"/> lo intercambia con la mano y lo deja en
	/// <c>trashItem</c>, vaciando el hueco de origen (que ya se vacio al cogerlo con el raton,
	/// como cualquier otra ranura del panel). <c>Player.trashItem</c> NO se guarda en el
	/// <c>.plr</c> (comprobado: no aparece en ningun <c>Load</c>/<c>Save</c> de
	/// <c>PlayerFileData</c>), asi que el objeto queda fuera de la partida guardada para siempre;
	/// dentro de la misma sesion se queda visible en el icono - tal cual pasa en el juego real -
	/// hasta que se tira otra cosa encima.
	/// <para />
	/// Se ha hecho como clase propia y no metida dentro de <see cref="SlotObjetoVanilla"/> a
	/// proposito: esa clase la esta tocando en paralelo otro agente (arreglo del tooltip), y
	/// <c>Player.trashItem</c> es un campo suelto, no una posicion de un array, asi que hacen
	/// falta las sobrecargas <c>ref Item</c> de <see cref="ItemSlot"/> en vez de las de
	/// <c>Item[]</c> que usa esa clase.
	/// </summary>
	public class SlotPapeleraTk : UIElement
	{
		private float _escala;

		/// <summary>Escala de dibujado, igual que <see cref="SlotObjetoVanilla.Escala"/>.</summary>
		public float Escala {
			get { return _escala; }
			set {
				_escala = value;
				Width.Set(52f * value, 0f);
				Height.Set(52f * value, 0f);
			}
		}

		public SlotPapeleraTk(float escala = 1f)
		{
			Escala = escala;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			Player jugador = Main.LocalPlayer;

			float escalaPrevia = Main.inventoryScale;
			Main.inventoryScale = _escala;

			Rectangle rect = GetDimensions().ToRectangle();

			if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface) {
				// Igual que cualquier otra ranura del panel: sin esto el clic atraviesa la
				// interfaz y el jugador ataca o coloca bloques detras.
				jugador.mouseInterface = true;
				ItemSlot.Handle(ref jugador.trashItem, ItemSlot.Context.TrashItem);
				ItemSlot.MouseHover(ref jugador.trashItem, ItemSlot.Context.TrashItem);
			}

			ItemSlot.Draw(spriteBatch, ref jugador.trashItem, ItemSlot.Context.TrashItem, rect.TopLeft());

			Main.inventoryScale = escalaPrevia;
		}
	}
}
