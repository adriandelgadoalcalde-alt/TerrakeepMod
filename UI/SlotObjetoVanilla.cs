using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Undo;
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
					// ItemSlot.cs reutilizadas sin reimplementar nada. Envuelto en el historial
					// (WS7 ampliado): ver ManejarConHistorial mas abajo, por que hacia falta.
					ManejarConHistorial();
				}
			}

			ItemSlot.Draw(spriteBatch, _inventario, _contexto, _indice, rect.TopLeft());

			Main.inventoryScale = escalaPrevia;
		}

		/// <summary>
		/// Envuelve <c>ItemSlot.Handle</c> con el historial de deshacer/rehacer de WS7.
		/// <para />
		/// <b>Por que hacia falta (hallazgo real, no un gusto de diseño).</b> WS7 dejo el
		/// historial preparado y CADA workstream envolvio SUS acciones programaticas concretas
		/// (el editor de cantidad, el editor de prefijo, "colocar desde el catalogo"), pero
		/// ninguno envolvio jamas esta clase - la propia <see cref="TerrakeepMod.UI.Libreria.
		/// ContenidoLibreria.ColocarEnRanura"/> lo deja dicho en su propio comentario: "jugando,
		/// lo que se usa es exactamente el mismo ItemSlot.Handle a traves de SlotObjetoVanilla".
		/// Como <see cref="SlotObjetoVanilla"/> es la unica pieza que dibuja y gestiona TODAS las
		/// ranuras reales del mod (inventario, equipo, hucha, caja, forja, boveda, y la propia
		/// rejilla de destino de Libreria), el resultado real era que la accion MAS comun de
		/// todas - arrastrar un objeto con el raton - nunca quedaba deshacible, solo las acciones
		/// que pasaban por un boton propio. Comprobado leyendo el codigo, no solo suponiendolo.
		/// <para />
		/// No se puede usar <see cref="Historial.CambiarObjetos"/> a secas porque esa version
		/// exige el ROTULO antes de ejecutar la edicion, y aqui no se sabe si el clic real va a
		/// coger, soltar, apilar, intercambiar o marcar favorito hasta que <c>ItemSlot.Handle</c>
		/// ya ha corrido. Por eso <see cref="Historial.CambiarObjetoDeSlotConCeldas"/>, que ademas
		/// vigila <c>Main.mouseItem</c> (la "mano") y <c>Player.trashItem</c> (la papelera) - ver
		/// su XMLdoc para el porque real, encontrado leyendo <c>ItemSlot.cs</c> decompilado: un
		/// clic normal casi siempre pasa el objeto POR uno de esos dos campos sueltos, nunca solo
		/// dentro de la ranura, y sin vigilarlos tambien "deshacer" el segundo clic de un
		/// arrastre (soltar) haria desaparecer el objeto en vez de devolverlo a la ranura de
		/// origen.
		/// <para />
		/// <b>Cuando NO se envuelve, a proposito.</b> Con un cofre/banca DE VERDAD abierta
		/// (<c>Player.chest</c>), o con el menu vanilla de Reforjar/Guia/Investigar activo,
		/// <c>ItemSlot.Handle</c> puede mover el objeto a un TERCER sitio que tampoco es ninguna
		/// celda vigilada (otro cofre, el yunque, la mesa del Guia, la ranura de sacrificio del
		/// Modo Viaje) - nuestro panel es a pantalla completa y ninguno de esos deberia estar
		/// abierto a la vez, pero por si un estado se quedara pegado de antes de abrir el panel,
		/// se comprueba y, si lo estuviera, se ejecuta el <c>ItemSlot.Handle</c> de siempre SIN
		/// envolver - ni mejor ni peor que el comportamiento de antes de este arreglo, nunca un
		/// riesgo de duplicar o perder el objeto.
		/// <para />
		/// <c>internal</c> y no <c>private</c> a proposito: <c>Common/Undo/
		/// AutopruebaDeshacerArrastre.cs</c> la llama tal cual para probar este codigo de
		/// produccion exacto (no una copia) simulando el clic igual que ya hace
		/// <c>ContenidoLibreria.ColocarEnRanura</c> - <c>Main.mouseLeft</c>/<c>mouseLeftRelease</c>
		/// a mano, sin teclado ni raton fisico (ver esa autoprueba para el porque).
		/// </summary>
		internal void ManejarConHistorial()
		{
			if (HayOtroContenedorAbierto()) {
				ItemSlot.Handle(_inventario, _contexto, _indice);
				return;
			}

			Historial.CambiarObjetoDeSlotConCeldas(_inventario, _indice, CeldasVigiladas, EtiquetaCambio,
				() => ItemSlot.Handle(_inventario, _contexto, _indice));
		}

		private static bool HayOtroContenedorAbierto()
		{
			return Main.LocalPlayer.chest != -1
				|| Main.InReforgeMenu
				|| Main.InGuideCraftMenu
				|| (Main.CreativeMenu != null && Main.CreativeMenu.IsShowingResearchMenu());
		}

		private static Historial.CeldaDeObjeto[] CeldasVigiladas => new[] {
			new Historial.CeldaDeObjeto(() => Main.mouseItem, v => Main.mouseItem = v),
			new Historial.CeldaDeObjeto(() => Main.LocalPlayer.trashItem, v => Main.LocalPlayer.trashItem = v),
		};

		/// <summary>
		/// Construye un rotulo legible a partir de lo que habia en la ranura antes del clic y lo
		/// que quedo despues. Cubre los cinco casos reales que puede dejar
		/// <c>ItemSlot.Handle</c> sobre una unica ranura: aparecer, desaparecer, cambiar de
		/// objeto, cambiar de cantidad (apilar/repartir) o cambiar solo de favorito.
		/// </summary>
		private static string EtiquetaCambio(Item antes, Item despues)
		{
			bool antesVacio = antes == null || antes.IsAir;
			bool despuesVacio = despues == null || despues.IsAir;

			if (despuesVacio) {
				return Idiomas.Texto("Historial.Ranura.Quitar", antes.Name);
			}
			if (antesVacio) {
				return Idiomas.Texto("Historial.Ranura.Colocar", despues.Name);
			}
			if (antes.type != despues.type) {
				return Idiomas.Texto("Historial.Ranura.Cambiar", antes.Name, despues.Name);
			}
			if (antes.stack != despues.stack) {
				return Idiomas.Texto("Historial.Ranura.Cantidad", despues.Name, antes.stack, despues.stack);
			}
			if (antes.favorited != despues.favorited) {
				return despues.favorited
					? Idiomas.Texto("Historial.Ranura.Favorito", despues.Name)
					: Idiomas.Texto("Historial.Ranura.QuitarFavorito", despues.Name);
			}
			// Cubre lo que quede (tipicamente un cambio de prefijo por "clic derecho + Investigar"
			// o un reforjado): no se inventa un caso mas especifico que no se pueda distinguir sin
			// leer mas campos de Item de los que Historial ya compara en MismoContenidoQue.
			return Idiomas.Texto("Historial.Ranura.Generico", despues.Name);
		}
	}
}
