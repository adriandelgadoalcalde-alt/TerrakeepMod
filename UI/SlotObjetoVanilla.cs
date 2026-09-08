using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.UI
{
	/// <summary>
	/// Elemento de UI que dibuja y gestiona un slot de objeto REAL de Terraria
	/// (<see cref="ItemSlot"/>) sobre una posicion concreta de un array de <see cref="Item"/>
	/// vivo del juego - por ejemplo <c>Main.LocalPlayer.inventory</c>.
	/// <para />
	/// No copia el objeto: opera directamente sobre el array que se le pasa, asi que arrastrar,
	/// coger con el raton o hacer clic derecho encima modifica el inventario de verdad, en vivo.
	/// Es la pieza que WS1 y siguientes van a reutilizar tal cual para todos los slots.
	/// </summary>
	public class SlotObjetoVanilla : UIElement
	{
		private readonly Item[] _inventario;
		private readonly int _indice;
		private readonly int _contexto;
		private float _escala;

		/// <summary>Acceso directo al objeto real que ocupa el slot ahora mismo.</summary>
		public Item ObjetoActual => _inventario[_indice];

		/// <summary>
		/// Escala de dibujado. Se puede cambiar despues de crear el slot: hay pestañas que tienen
		/// que encoger sus ranuras cuando la ventana del juego es baja y no caben todas las filas
		/// (ver <c>PestanaEquipo</c>), y el alto real disponible no se conoce hasta que el motor ha
		/// recalculado el arbol de la interfaz.
		/// </summary>
		public float Escala {
			get { return _escala; }
			set {
				_escala = value;
				Width.Set(52f * value, 0f);
				Height.Set(52f * value, 0f);
			}
		}

		/// <param name="inventario">Array vivo del juego (no una copia).</param>
		/// <param name="indice">Posicion dentro de ese array.</param>
		/// <param name="contexto">Uno de <see cref="ItemSlot.Context"/>; decide el fondo del
		/// slot y que objetos acepta.</param>
		/// <param name="escala">Escala de dibujado. 1f = el tamaño del inventario vanilla.</param>
		public SlotObjetoVanilla(Item[] inventario, int indice, int contexto = ItemSlot.Context.InventoryItem, float escala = 1f)
		{
			_inventario = inventario;
			_indice = indice;
			_contexto = contexto;
			_escala = escala;

			// 52x52 es el tamaño real de la textura de fondo de un slot de inventario
			// (Main.inventoryBack*, TextureAssets.InventoryBack) a escala 1.
			Width.Set(52f * escala, 0f);
			Height.Set(52f * escala, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			// ItemSlot.Draw/Handle no reciben la escala por parametro: la leen de
			// Main.inventoryScale, que es estado global del juego. Hay que guardarla y
			// restaurarla o se descuadra el inventario normal al cerrar el panel.
			float escalaPrevia = Main.inventoryScale;
			Main.inventoryScale = _escala;

			Rectangle rect = GetDimensions().ToRectangle();

			if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface) {
				// Sin esto el clic atraviesa la interfaz y el jugador ataca/coloca bloques
				// detras del panel.
				Main.LocalPlayer.mouseInterface = true;

				// Pero SI hay un desplegable abierto encima (el selector de prefijo), la ranura se
				// calla: este control gestiona el raton a mano, fuera del sistema de eventos de
				// UIElement, asi que no se entera por si solo de que algo lo tapa - sin esta
				// consulta, un clic en una fila del desplegable ademas cogeria o soltaria el objeto
				// de la ranura que quedara justo debajo. Ver CapaSuperposicionTk.TapaAlRaton.
				if (!CapaSuperposicionTk.TapaAlRaton(Main.MouseScreen)) {
					// Aqui vive TODO el comportamiento vanilla: coger, soltar, apilar, clic
					// derecho, equipar rapido con mayusculas, tooltip... 4313 lineas de
					// ItemSlot.cs reutilizadas sin reimplementar nada.
					ItemSlot.Handle(_inventario, _contexto, _indice);
				}
			}

			ItemSlot.Draw(spriteBatch, _inventario, _contexto, _indice, rect.TopLeft());

			Main.inventoryScale = escalaPrevia;
		}
	}
}
