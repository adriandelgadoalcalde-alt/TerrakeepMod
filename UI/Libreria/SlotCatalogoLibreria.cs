using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace TerrakeepMod.UI.Libreria
{
	/// <summary>
	/// Ranura de CATALOGO de la Libreria: enseña un objeto del juego con el dibujado nativo de
	/// Terraria y su tooltip real, y al hacer clic deja una copia <b>en el raton</b> para que el
	/// jugador la suelte donde quiera.
	/// </summary>
	/// <remarks>
	/// <b>Por que el objeto va al raton y no directamente a un hueco.</b> Poniendolo en
	/// <c>Main.mouseItem</c> el resto del recorrido lo hace vanilla tal cual: soltarlo en
	/// cualquier <see cref="SlotObjetoVanilla"/> del panel (que llama a <c>ItemSlot.Handle</c>)
	/// pasa por el mismo codigo que usa el inventario del propio juego, con sus reglas de que
	/// acepta cada ranura, de apilado y de intercambio. No hay ni una linea propia de "colocar
	/// objeto" que pueda desviarse del comportamiento real.
	/// <para />
	/// Igual que <c>SlotCatalogoBuild</c> (WS4), este slot <b>no llama nunca a
	/// <c>ItemSlot.Handle</c></b> sobre si mismo: opera sobre un ejemplar de muestra, no sobre un
	/// array vivo del jugador.
	/// <para />
	/// A diferencia del panel de Builds, aqui SI se crean objetos de la nada, y es lo correcto:
	/// la Libreria de Terrakeep es precisamente el catalogo del que se sacan objetos (lo mismo
	/// que hace la app de escritorio). Builds no los crea porque su cometido es organizar lo que
	/// ya tienes, no dartelo.
	/// </remarks>
	public class SlotCatalogoLibreria : UIElement
	{
		private static readonly Color FondoCatalogo = new Color(80, 92, 140);

		private readonly Item[] _muestra = new Item[1];
		private readonly float _escala;

		/// <summary>Tipo real del objeto que enseña esta ranura.</summary>
		public readonly int Tipo;

		/// <summary>Se dispara con el clic izquierdo: pide una unidad.</summary>
		public event Action<int, bool> AlPedir;

		public SlotCatalogoLibreria(int tipo, float escala = 0.85f)
		{
			Tipo = tipo;
			_escala = escala;

			_muestra[0] = new Item();
			if (tipo > 0) {
				_muestra[0].SetDefaults(tipo);
			}

			Width.Set(52f * escala, 0f);
			Height.Set(52f * escala, 0f);

			// Izquierdo = una unidad. Derecho = la pila maxima del objeto. Es la misma pareja de
			// gestos que ya usa el jugador con las ranuras del juego, asi que no hay nada nuevo
			// que aprender.
			OnLeftClick += (evento, elemento) => Pedir(false);
			OnRightClick += (evento, elemento) => Pedir(true);
		}

		/// <summary>Nombre real del objeto, tal cual lo llama el juego.</summary>
		public string Nombre {
			get { return _muestra[0] != null ? _muestra[0].Name : ""; }
		}

		private void Pedir(bool pilaCompleta)
		{
			if (Tipo > 0 && AlPedir != null) {
				AlPedir(Tipo, pilaCompleta);
			}
		}

		/// <summary>Dispara el clic izquierdo por la ruta REAL del motor de interfaz (la misma que
		/// recorre un clic de raton una vez resuelto sobre que elemento cae). La usa el arnes de
		/// pruebas para verificar la colocacion sin depender de mover el raton.</summary>
		public void PulsarComoUnClic()
		{
			LeftClick(new UIMouseEvent(this, GetDimensions().Center()));
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			// ItemSlot.Draw lee la escala y el color de fondo de estado GLOBAL del juego, no de
			// parametros: hay que guardarlos y restaurarlos o se descuadra y destiñe el inventario
			// normal al cerrar el panel (hallazgo ya anotado por WS0/WS4).
			float escalaPrevia = Main.inventoryScale;
			Color fondoPrevio = Main.inventoryBack;

			Main.inventoryScale = _escala;
			Main.inventoryBack = FondoCatalogo;

			Rectangle rect = GetDimensions().ToRectangle();
			ItemSlot.Draw(spriteBatch, _muestra, ItemSlot.Context.ChestItem, 0, rect.TopLeft());

			if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface) {
				// Sin esto el clic atraviesa el panel y el jugador ataca o coloca bloques detras.
				Main.LocalPlayer.mouseInterface = true;
				// Tooltip REAL del juego (nombre, daño, prefijos, texto del mod...). No hace falta
				// ningun formateador propio: esto ya lo da vanilla entero y actualizado.
				ItemSlot.MouseHover(_muestra, ItemSlot.Context.ChestItem, 0);
			}

			Main.inventoryScale = escalaPrevia;
			Main.inventoryBack = fondoPrevio;
		}
	}
}
