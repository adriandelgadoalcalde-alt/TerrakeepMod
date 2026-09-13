using System;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using TerrakeepMod.UI;

namespace TerrakeepMod.Common.Undo
{
	/// <summary>
	/// Autoprueba del arreglo real de esta sesion: antes, <see cref="SlotObjetoVanilla"/>
	/// llamaba a <c>ItemSlot.Handle</c> SIN pasar por <see cref="Historial"/>, asi que la accion
	/// mas comun de todo el mod - arrastrar un objeto con el raton por cualquier ranura
	/// (inventario, equipo, hucha, caja, forja, boveda, destino de Libreria) - nunca quedaba
	/// deshacible. Ver el comentario real de <c>SlotObjetoVanilla.ManejarConHistorial</c>.
	/// <para />
	/// Corre en el juego REAL, sobre el inventario real del personaje de prueba, sin que nadie
	/// pulse nada, y deja la evidencia en <c>tModLoader-Logs\client.log</c> (prefijo
	/// <c>[Terrakeep]</c>). Se dispara con <see cref="Variable"/>, que pone
	/// <c>scripts\verificar-deshacer-arrastre.ps1</c>.
	/// <para />
	/// <b>Como se simula el clic sin teclado ni raton fisico.</b> Mismo camino ya validado por
	/// WS7 y por <c>ContenidoLibreria.ColocarEnRanura</c>: se rellenan a mano las entradas REALES
	/// de las que lee <c>ItemSlot.Handle</c> (<c>Main.mouseLeft</c>/<c>mouseLeftRelease</c> para
	/// un clic izquierdo nuevo, <c>Main.keyState</c> con Ctrl pulsado para el Ctrl+clic de
	/// papelera rapida) y se llama al codigo de PRODUCCION exacto
	/// (<see cref="SlotObjetoVanilla.ManejarConHistorial"/>, <c>internal</c> solo para esto), no
	/// una copia ni una simulacion aparte.
	/// <para />
	/// Dos escenarios, los dos con el mismo criterio real: deshacer TODO tiene que devolver el
	/// objeto exactamente a donde estaba - ni desaparecido (el bug que tenia el codigo sin
	/// envolver: deshacer solo el "soltar" sin devolver TAMBIEN el objeto a la mano) ni
	/// duplicado (el riesgo que habria si <c>Main.mouseItem</c>/<c>Player.trashItem</c> no
	/// estuvieran vigilados tambien, ver <see cref="Historial.CambiarObjetoDeSlotConCeldas"/>).
	/// </summary>
	public static class AutopruebaDeshacerArrastre
	{
		public const string Variable = "TERRAKEEP_AUTOTEST_ARRASTRE";

		// Ranuras del inventario de la barra rapida del personaje sintetico de pruebas: vacias
		// las dos, como en AutopruebaWs7.
		private const int RanuraOrigen = 5;
		private const int RanuraDestino = 9;

		private static bool _comprobada;
		private static bool _activa;
		private static bool _hecha;
		private static int _fotogramasEnMundo;

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
			}

			if (!_activa || _hecha) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < 180) {
				return;
			}

			_hecha = true;
			try {
				ProbarArrastrarYDeshacer();
				ProbarPapeleraRapidaYDeshacer();
			}
			catch (Exception e) {
				Registrar("EXCEPCION: " + e);
			}

			Registrar("AUTOPRUEBA ARRASTRE: terminada.");
		}

		/// <summary>
		/// Caso 1, el mas comun de todos: coger un objeto de una ranura (clic 1) y soltarlo en
		/// otra vacia (clic 2), ambos por <c>SlotObjetoVanilla.ManejarConHistorial</c> de verdad -
		/// exactamente lo que hace un arrastre real con el raton, en dos clics porque
		/// <c>ItemSlot.Handle</c> no distingue "arrastrar" de "dos clics seguidos": los dos pasan
		/// por el mismo camino.
		/// </summary>
		private static void ProbarArrastrarYDeshacer()
		{
			Item[] inventario = Main.LocalPlayer.inventory;
			Historial.Pila.Limpiar();

			inventario[RanuraOrigen] = new Item();
			inventario[RanuraOrigen].SetDefaults(ItemID.CopperShortsword);
			inventario[RanuraOrigen].stack = 1;
			inventario[RanuraDestino] = new Item();
			Main.mouseItem = new Item();
			Main.LocalPlayer.trashItem = new Item();

			Registrar("ARRASTRE/1 estado inicial: " + Estado(inventario) + ", mano=" + Describir(Main.mouseItem));

			SlotObjetoVanilla slotOrigen = new SlotObjetoVanilla(inventario, RanuraOrigen, ItemSlot.Context.InventoryItem);
			SlotObjetoVanilla slotDestino = new SlotObjetoVanilla(inventario, RanuraDestino, ItemSlot.Context.InventoryItem);

			// Clic 1: coger de la ranura origen. La misma pulsacion "nueva" que exige
			// ItemSlot.LeftClick, ver el comentario real de ContenidoLibreria.ColocarEnRanura.
			SimularClicIzquierdoNuevo(slotOrigen);
			Registrar("ARRASTRE/2 tras clic 1 (coger): " + Estado(inventario) + ", mano=" + Describir(Main.mouseItem) +
				" | entradas=" + Historial.Pila.Cuenta + ", etiqueta=\"" + Historial.Pila.EtiquetaDeshacer + "\"");

			// Clic 2: soltar en la ranura destino.
			SimularClicIzquierdoNuevo(slotDestino);
			Registrar("ARRASTRE/3 tras clic 2 (soltar): " + Estado(inventario) + ", mano=" + Describir(Main.mouseItem) +
				" | entradas=" + Historial.Pila.Cuenta + ", etiqueta=\"" + Historial.Pila.EtiquetaDeshacer + "\"");

			bool colocadoBien = inventario[RanuraDestino].type == ItemID.CopperShortsword
				&& inventario[RanuraOrigen].IsAir && Main.mouseItem.IsAir;
			Registrar("ARRASTRE/3b " + (colocadoBien ? "OK" : "FALLO: NO") + " el objeto quedo en la ranura destino, " +
				"la origen vacia y la mano vacia.");

			// Deshacer el clic 2 (soltar): tiene que volver TODO a como estaba justo despues del
			// clic 1 - la ranura destino vacia OTRA VEZ, Y el objeto de vuelta EN LA MANO (esto es
			// justo lo que no pasaba antes del arreglo: sin vigilar Main.mouseItem, el objeto
			// desaparecia aqui, ni en la ranura ni en la mano).
			string deshecho1 = Historial.Deshacer();
			bool manoTrasDeshacer1 = !Main.mouseItem.IsAir && Main.mouseItem.type == ItemID.CopperShortsword;
			Registrar("ARRASTRE/4 tras DESHACER 1 (\"" + deshecho1 + "\"): " + Estado(inventario) +
				", mano=" + Describir(Main.mouseItem) + " | " +
				(inventario[RanuraDestino].IsAir && manoTrasDeshacer1
					? "OK: la ranura destino volvio a quedar vacia y el objeto volvio a la MANO (no desaparecio)."
					: "FALLO: NO volvio a la mano - esto es exactamente el bug que arregla esta sesion."));

			// Deshacer el clic 1 (coger): tiene que devolver el objeto a la ranura origen y vaciar
			// la mano.
			string deshecho2 = Historial.Deshacer();
			bool bienDelTodo = inventario[RanuraOrigen].type == ItemID.CopperShortsword
				&& inventario[RanuraDestino].IsAir && Main.mouseItem.IsAir;
			Registrar("ARRASTRE/5 tras DESHACER 2 (\"" + deshecho2 + "\"): " + Estado(inventario) +
				", mano=" + Describir(Main.mouseItem) + " | " +
				(bienDelTodo
					? "OK: el objeto volvio EXACTAMENTE a la ranura origen, sin duplicarse ni perderse."
					: "FALLO: NO quedo exactamente como al principio."));

			// Rehacer los dos, para comprobar que la foto de "despues" tambien quedo bien formada.
			Historial.Rehacer();
			Historial.Rehacer();
			bool rehechoBien = inventario[RanuraDestino].type == ItemID.CopperShortsword
				&& inventario[RanuraOrigen].IsAir && Main.mouseItem.IsAir;
			Registrar("ARRASTRE/6 tras REHACER x2: " + Estado(inventario) + ", mano=" + Describir(Main.mouseItem) +
				" | " + (rehechoBien ? "OK: vuelve a quedar colocado en destino." : "FALLO."));

			inventario[RanuraOrigen] = new Item();
			inventario[RanuraDestino] = new Item();
			Main.mouseItem = new Item();
			Historial.Pila.Limpiar();
		}

		/// <summary>
		/// Caso 2: Ctrl+clic de papelera rapida (<c>ItemSlot.LeftClick_SellOrTrash</c> ->
		/// <c>SellOrTrash</c>, que por defecto de vanilla usa Ctrl y no Mayus -
		/// <c>Options.DisableLeftShiftTrashCan=true</c> de fabrica, comprobado en el
		/// <c>ItemSlot.cs</c> decompilado). El objeto se va a <c>Player.trashItem</c>, la OTRA
		/// celda vigilada ademas de la mano.
		/// </summary>
		private static void ProbarPapeleraRapidaYDeshacer()
		{
			Item[] inventario = Main.LocalPlayer.inventory;
			Historial.Pila.Limpiar();

			inventario[RanuraOrigen] = new Item();
			inventario[RanuraOrigen].SetDefaults(ItemID.CopperShortsword);
			inventario[RanuraOrigen].stack = 1;
			Main.mouseItem = new Item();
			Main.LocalPlayer.trashItem = new Item();

			Registrar("PAPELERA/1 estado inicial: " + Estado(inventario) + ", papelera=" + Describir(Main.LocalPlayer.trashItem));

			SlotObjetoVanilla slotOrigen = new SlotObjetoVanilla(inventario, RanuraOrigen, ItemSlot.Context.InventoryItem);

			SimularCtrlClicIzquierdoNuevo(slotOrigen);

			bool tiradoBien = inventario[RanuraOrigen].IsAir
				&& !Main.LocalPlayer.trashItem.IsAir && Main.LocalPlayer.trashItem.type == ItemID.CopperShortsword;
			Registrar("PAPELERA/2 tras Ctrl+clic: " + Estado(inventario) + ", papelera=" + Describir(Main.LocalPlayer.trashItem) +
				" | " + (tiradoBien ? "OK: se fue a la papelera." : "FALLO: no se tiro."));

			string deshecho = Historial.Deshacer();
			bool bienDelTodo = inventario[RanuraOrigen].type == ItemID.CopperShortsword
				&& Main.LocalPlayer.trashItem.IsAir;
			Registrar("PAPELERA/3 tras DESHACER (\"" + deshecho + "\"): " + Estado(inventario) +
				", papelera=" + Describir(Main.LocalPlayer.trashItem) + " | " +
				(bienDelTodo
					? "OK: volvio a la ranura y la papelera quedo vacia (sin duplicarse)."
					: "FALLO: NO se deshizo bien - la papelera vigilada no esta funcionando."));

			inventario[RanuraOrigen] = new Item();
			Main.LocalPlayer.trashItem = new Item();
			Historial.Pila.Limpiar();
		}

		/// <summary>
		/// Rellena las dos entradas reales de las que depende un clic izquierdo NUEVO
		/// (<c>Main.mouseLeftRelease &amp;&amp; Main.mouseLeft</c>, ver <c>ItemSlot.LeftClick</c>
		/// decompilado linea 695) y llama al codigo real del slot. Se restauran despues para no
		/// dejar el "raton" pulsado de mentira para el resto del fotograma.
		/// </summary>
		private static void SimularClicIzquierdoNuevo(SlotObjetoVanilla slot)
		{
			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			try {
				Main.mouseLeft = true;
				Main.mouseLeftRelease = true;
				slot.ManejarConHistorial();
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}
		}

		/// <summary>Igual que <see cref="SimularClicIzquierdoNuevo"/> pero con Ctrl pulsado
		/// tambien en <c>Main.keyState</c> (de donde lee <c>ItemSlot.ControlInUse</c>), para
		/// disparar la papelera rapida por defecto de vanilla.</summary>
		private static void SimularCtrlClicIzquierdoNuevo(SlotObjetoVanilla slot)
		{
			Microsoft.Xna.Framework.Input.KeyboardState estadoPrevio = Main.keyState;
			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			try {
				Main.keyState = new Microsoft.Xna.Framework.Input.KeyboardState(
					Microsoft.Xna.Framework.Input.Keys.LeftControl);
				Main.mouseLeft = true;
				Main.mouseLeftRelease = true;
				slot.ManejarConHistorial();
			}
			finally {
				Main.keyState = estadoPrevio;
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}
		}

		private static string Estado(Item[] inventario)
		{
			return "inventory[" + RanuraOrigen + "]=" + Describir(inventario[RanuraOrigen]) +
				", inventory[" + RanuraDestino + "]=" + Describir(inventario[RanuraDestino]);
		}

		private static string Describir(Item objeto)
		{
			if (objeto == null || objeto.IsAir) {
				return "vacio";
			}
			return "\"" + objeto.Name + "\" x" + objeto.stack + " (type=" + objeto.type + ")";
		}

		private static void Registrar(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
			}
		}
	}
}
