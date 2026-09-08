using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Libreria.Widgets
{
	/// <summary>
	/// El "recuadro" de seleccion del nuevo mini-panel de edicion de la Libreria: un
	/// <see cref="ItemSlot"/> real, pero sin ningun array de por medio - guarda su propio
	/// <see cref="Item"/> en un campo propio, exactamente como <c>Player.trashItem</c> hace con la
	/// papelera (<c>UI.Personaje.Widgets.SlotPapeleraTk</c>, mismo patron, ver su cabecera).
	/// </summary>
	/// <remarks>
	/// Encargo explicito del usuario tras probar el editor de cantidad de Personaje ("se
	/// selecciona pasando por encima el raton pero de esa forma te puedes equivocar mucho...
	/// deberiamos poder clicar/arrastrar y que quede seleccionado"): en vez de "enganchar" el
	/// editor al slot bajo el raton (facil de equivocarse: pasar de largo hacia el editor puede
	/// enganchar el objeto equivocado), aqui la seleccion es EXPLICITA - hay que arrastrar el
	/// objeto de verdad hasta este recuadro para que sea "lo seleccionado".
	/// <para />
	/// <b>Arrastrar aqui MUEVE el objeto de verdad</b>, no lo copia ni lo "apunta": es el mismo
	/// <c>ItemSlot.Handle</c> que usa cualquier ranura del juego, asi que el objeto sale de su
	/// hueco de origen (mochila, almacen...) y pasa a vivir en <see cref="ObjetoActual"/> mientras
	/// se edita - igual que ya hace la papelera. Cuando se termina de editar, se arrastra de vuelta
	/// a donde corresponda. Es la MISMA referencia todo el tiempo (nunca una copia): editar su
	/// <c>.stack</c>/<c>.prefix</c> aqui cambia el objeto real que luego se va a soltar.
	/// </remarks>
	public class SlotSeleccionTk : UIElement
	{
		private Item _seleccion = new Item();
		private float _escala;

		/// <summary>El objeto REAL seleccionado ahora mismo (puede estar vacio). Es una referencia
		/// directa al campo interno, nunca una copia.</summary>
		public Item ObjetoActual => _seleccion;

		/// <summary>Escala de dibujado, igual que <c>SlotObjetoVanilla.Escala</c>.</summary>
		public float Escala {
			get { return _escala; }
			set {
				_escala = value;
				Width.Set(52f * value, 0f);
				Height.Set(52f * value, 0f);
			}
		}

		public SlotSeleccionTk(float escala = 1f)
		{
			Escala = escala;
		}

		/// <summary>
		/// Ejercita <c>ItemSlot.Handle</c> a mano sobre el campo interno, exactamente el mismo
		/// codigo que dispara <see cref="DrawSelf"/> cuando el raton esta encima - la autoprueba lo
		/// usa para simular un arrastre real (coger con <c>ItemSlot.LeftClick</c> en el origen,
		/// soltar aqui con esto) sin depender de que haya sesion de escritorio para mover el raton
		/// de verdad. Mismo patron ya usado en <c>SlotPapeleraTk</c>/las autopruebas de WS1.
		/// </summary>
		public void EjercitarHandle()
		{
			ItemSlot.Handle(ref _seleccion, ItemSlot.Context.InventoryItem);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			float escalaPrevia = Main.inventoryScale;
			Main.inventoryScale = _escala;

			Rectangle rect = GetDimensions().ToRectangle();

			if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface) {
				Main.LocalPlayer.mouseInterface = true;
				// Callado mientras haya un desplegable abierto encima (el selector de prefijo, que se
				// abre justo al lado de este recuadro): esta ranura gestiona el raton a mano, fuera
				// del sistema de eventos de UIElement, asi que no se entera sola de que la tapan.
				// Ver CapaSuperposicionTk.TapaAlRaton.
				if (!CapaSuperposicionTk.TapaAlRaton(Main.MouseScreen)) {
					// Context.InventoryItem: el contexto generico de vanilla, sin restriccion de tipo
					// de objeto ni comportamiento especial (a diferencia de TrashItem/EquipArmor...) -
					// admite arrastrar y sacar CUALQUIER objeto, que es justo lo que pide el encargo
					// ("arrastra el arma o el objeto", sin distinguir tipo).
					ItemSlot.Handle(ref _seleccion, ItemSlot.Context.InventoryItem);
					ItemSlot.MouseHover(ref _seleccion, ItemSlot.Context.InventoryItem);
				}
			}

			ItemSlot.Draw(spriteBatch, ref _seleccion, ItemSlot.Context.InventoryItem, rect.TopLeft());

			if (_seleccion.IsAir && !ContainsPoint(Main.MouseScreen)) {
				// Pista visual solo cuando esta vacio y el raton no lo tapa: un recuadro vacio de
				// ItemSlot no dice nada por si solo de que sirve.
				Utils.DrawBorderString(spriteBatch, Idiomas.Texto("Libreria.EditorPrefijo.RecuadroPista"),
					new Vector2(rect.X, rect.Bottom + 2f), EstiloTk.TextoSuave, 0.6f);
			}

			Main.inventoryScale = escalaPrevia;
		}
	}
}
