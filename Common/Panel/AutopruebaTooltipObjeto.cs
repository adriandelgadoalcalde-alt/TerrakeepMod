using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// Verifica el arreglo del bug real reportado por el usuario: ninguna ranura de ningun panel
	/// de Terrakeep enseñaba tooltip vainilla al pasar el raton por encima, en ninguna de las
	/// cuatro zonas (Inventario, Almacenes, Equipo, Libreria).
	/// <para />
	/// La causa y el arreglo estan documentados junto a
	/// <see cref="PanelTerrakeepState.DibujarTooltipDeObjeto"/>. Esta autopreuba comprueba el
	/// EFECTO real sobre el motor: que <c>Main.HoverItem</c>/<c>Main.hoverItemName</c> se rellenan
	/// con el objeto correcto cuando el raton esta encima de una ranura con objeto, y que se
	/// vacian solos al apartar el raton (la otra mitad del arreglo: el reseteo de
	/// <c>Main.hoverItemName</c> al principio de cada <c>Draw</c>, que evita que se quede pegado el
	/// ultimo objeto sobre el que se paso).
	/// <para />
	/// <b>Raton de pantalla, no raton de la interfaz.</b> <see cref="TerrakeepMod.UI.SlotObjetoVanilla"/>
	/// no usa el sistema de eventos de <c>UIElement</c> (<c>MouseOver</c>/<c>IsMouseHovering</c>,
	/// que lee <c>Main.InGameUI.MousePosition</c> - la via que ya usa
	/// <c>AutopruebaPersonaje.PrepararHoverParaEditorCantidad</c> para otros widgets): hace su
	/// propio <c>ContainsPoint(Main.MouseScreen)</c> a mano dentro de <c>DrawSelf</c>, y
	/// <c>Main.MouseScreen</c> es <c>new Vector2(Main.mouseX, Main.mouseY)</c> (confirmado
	/// decompilando <c>Terraria.Main</c> con <c>ilspycmd</c> - es una propiedad de solo lectura
	/// sobre esos dos campos, ajena por completo a <c>Main.InGameUI.MousePosition</c>, que
	/// <c>UserInterface.GetMousePosition()</c> reescribe cada fotograma desde esos MISMOS campos).
	/// Por eso esta autopreuba mueve el raton escribiendo <c>Main.mouseX</c>/<c>Main.mouseY</c>
	/// directamente: es la unica forma de ejercitar de verdad el <c>ItemSlot.Handle</c> real que
	/// llama <see cref="TerrakeepMod.UI.SlotObjetoVanilla"/>, sin necesitar sesion de escritorio ni
	/// raton fisico (misma limitacion de siempre, ver WS0).
	/// </summary>
	public static class AutopruebaTooltipObjeto
	{
		/// <summary>Variable de entorno que la activa.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_TOOLTIP";

		private const int FotogramasEntrePasos = 6;
		private const int FotogramasDeEspera = 180;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		private static Item _objetoEsperado;
		private static string _zonaEnPrueba;

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
			}

			if (!_activa || _terminada) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < FotogramasDeEspera) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			try {
				Paso(_paso++);
			}
			catch (Exception e) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); break;

				// --- Personaje / Inventario ------------------------------------------------------
				case 1: IrAPestanaPersonaje(0, "Inventario"); break;
				case 2: PonerRatonSobre("Personaje/Inventario"); break;
				case 3: ComprobarHover(); break;
				case 4: QuitarRaton(); break;
				case 5: ComprobarSinHover("Personaje/Inventario"); break;

				// --- Personaje / Almacenes (Hucha, Player.bank) ------------------------------------
				case 6: IrAPestanaPersonaje(1, "Almacenes"); break;
				case 7: PonerRatonSobre("Personaje/Almacenes"); break;
				case 8: ComprobarHover(); break;
				case 9: QuitarRaton(); break;
				case 10: ComprobarSinHover("Personaje/Almacenes"); break;

				// --- Personaje / Equipo -------------------------------------------------------------
				case 11: IrAPestanaPersonaje(2, "Equipo"); break;
				case 12: PonerRatonSobre("Personaje/Equipo"); break;
				case 13: ComprobarHover(); break;
				case 14: QuitarRaton(); break;
				case 15: ComprobarSinHover("Personaje/Equipo"); break;

				// --- Libreria (destino "Inventario", el MISMO Player.inventory de arriba) ----------
				case 16: IrALibreria(); break;
				case 17: PonerRatonSobre("Libreria"); break;
				case 18: ComprobarHover(); break;
				case 19: QuitarRaton(); break;
				case 20: ComprobarSinHover("Libreria"); break;

				default: Terminar(); break;
			}
		}

		// -------------------------------------------------------------------------------------

		private static void Arrancar()
		{
			Player jugador = Main.LocalPlayer;

			// Esta sesion automatizada nunca mueve un raton fisico de verdad (WS0 ya documento la
			// limitacion: sin sesion de escritorio real no hay clics reales), asi que
			// PlayerInput.CurrentInputMode puede haber quedado en modo mando en vez de raton+teclado.
			// PlayerInput.IgnoreMouseInterface (real, decompilado) devuelve true cuando
			// UsingGamepad && !UILinkPointNavigator.Available, y nuestro panel no registra puntos de
			// navegacion de mando - eso bloquearia ItemSlot.Handle SIEMPRE, sin que tenga nada que
			// ver con el objeto de la ranura ni con el arreglo del tooltip. Forzarlo a Mouse aqui es
			// lo mismo que ya hace esta autopreuba con PlayerInput.Triggers.JustPressed.KeyStatus en
			// otras clases: pisar a mano el estado de entrada que un raton fisico pondria solo.
			Terraria.GameInput.PlayerInput.CurrentInputMode = Terraria.GameInput.InputMode.Mouse;

			// Objetos deterministas, sin depender de lo que trajera el personaje de prueba (mismo
			// criterio que AutopruebaPersonaje.PoblarInventario/PoblarAlmacenes/PoblarEquipo).
			jugador.inventory[1] = new Item();
			jugador.inventory[1].SetDefaults(ItemID.DirtBlock);
			jugador.inventory[1].stack = 250;

			Item[] hucha = PersonajeVivo.ObtenerAlmacen(0);
			hucha[0] = new Item();
			hucha[0].SetDefaults(ItemID.Chest);

			jugador.armor[0] = new Item();
			jugador.armor[0].SetDefaults(ItemID.CopperHelmet);

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP: " + Variable + " detectada. " +
				"Objetos de prueba puestos: inventory[1]=" + PersonajeVivo.DescribirObjeto(jugador.inventory[1]) +
				", bank.item[0]=" + PersonajeVivo.DescribirObjeto(hucha[0]) +
				", armor[0]=" + PersonajeVivo.DescribirObjeto(jugador.armor[0]) + ".");

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "autoprueba del tooltip");
		}

		private static void IrAPestanaPersonaje(int indicePestana, string nombre)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null || panel.Personaje == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP: no hay panel de Personaje " +
					"montado (pestaña \"" + nombre + "\").");
				return;
			}

			panel.Personaje.IrAPestana(indicePestana);
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP - pestaña \"" + nombre +
				"\" abierta. Actual: \"" + panel.Personaje.NombrePestanaActual + "\".");
		}

		private static void IrALibreria()
		{
			PanelTerrakeepSystem.IrAArea(AreaTerrakeep.Libreria, "autoprueba del tooltip");
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP - area \"Libreria\" abierta " +
				"(destino por defecto: \"Inventario\", el mismo Player.inventory de las pestañas de Personaje).");
		}

		/// <summary>
		/// Busca la PRIMERA ranura con un objeto real dentro del contenido actualmente montado y le
		/// pone el raton de PANTALLA (Main.mouseX/Main.mouseY, no Main.InGameUI.MousePosition -
		/// ver la nota de la clase) encima de su centro, exactamente donde tendria que estar el
		/// cursor fisico del jugador para disparar <c>ItemSlot.Handle</c> desde
		/// <see cref="TerrakeepMod.UI.SlotObjetoVanilla.DrawSelf"/>.
		/// </summary>
		private static void PonerRatonSobre(string zona)
		{
			_zonaEnPrueba = zona;
			_objetoEsperado = null;

			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			UIElement contenido = panel != null ? panel.ContenidoActual : null;
			if (contenido == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP (" + zona +
					"): no hay contenido montado.");
				return;
			}

			SlotObjetoVanilla encontrado = null;
			contenido.ExecuteRecursively(elemento => {
				if (encontrado == null && elemento is SlotObjetoVanilla slot && !slot.ObjetoActual.IsAir) {
					encontrado = slot;
				}
			});

			if (encontrado == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP (" + zona +
					"): no se encontro ninguna ranura con objeto en el contenido montado.");
				return;
			}

			_objetoEsperado = encontrado.ObjetoActual;

			CalculatedStyle dim = encontrado.GetDimensions();
			int x = (int)(dim.X + dim.Width / 2f);
			int y = (int)(dim.Y + dim.Height / 2f);
			Main.mouseX = x;
			Main.mouseY = y;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP (" + zona +
				") - raton de PANTALLA puesto en x=" + x + " y=" + y + " sobre la ranura de \"" +
				_objetoEsperado.Name + "\" (rectangulo real x=" + (int)dim.X + " y=" + (int)dim.Y + " " +
				(int)dim.Width + "x" + (int)dim.Height + "). Un fotograma mas para que " +
				"SlotObjetoVanilla.DrawSelf lo procese.");
		}

		/// <summary>Comprueba que, tras el fotograma en el que el raton estuvo encima, el motor
		/// rellenó de verdad Main.HoverItem/Main.hoverItemName con el objeto esperado.</summary>
		private static void ComprobarHover()
		{
			if (_objetoEsperado == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP (" + _zonaEnPrueba +
					"): sin objeto esperado, no se puede comprobar el hover (ver el paso anterior).");
				return;
			}

			bool hoverItemOk = Main.HoverItem != null && Main.HoverItem.type == _objetoEsperado.type;
			bool nombreOk = !string.IsNullOrEmpty(Main.hoverItemName) &&
				Main.hoverItemName.StartsWith(_objetoEsperado.Name);

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP (" + _zonaEnPrueba +
				") - tras el fotograma con el raton encima: Main.HoverItem.type=" +
				(Main.HoverItem != null ? Main.HoverItem.type.ToString() : "null") +
				" (esperado " + _objetoEsperado.type + "), Main.hoverItemName=\"" + Main.hoverItemName +
				"\" (esperado que empiece por \"" + _objetoEsperado.Name + "\"). " +
				(hoverItemOk && nombreOk
					? "OK: el tooltip vainilla tiene lo que necesita para pintarse solo."
					: "FALLO: el bug del tooltip sigue presente en esta zona.") +
				" [diagnostico: IgnoreMouseInterface=" + Terraria.GameInput.PlayerInput.IgnoreMouseInterface +
				", UsingGamepad=" + Terraria.GameInput.PlayerInput.UsingGamepad +
				", CurrentInputMode=" + Terraria.GameInput.PlayerInput.CurrentInputMode +
				", itemAnimation=" + Main.LocalPlayer.itemAnimation +
				", mouseInterface=" + Main.LocalPlayer.mouseInterface +
				", mouseX=" + Main.mouseX + " mouseY=" + Main.mouseY +
				", hasFocus=" + Main.hasFocus + "]");
		}

		/// <summary>Aparta el raton de pantalla de cualquier ranura del panel (una esquina fuera del
		/// marco), dejando el fotograma para que se procese.</summary>
		private static void QuitarRaton()
		{
			Main.mouseX = 2;
			Main.mouseY = 2;
		}

		/// <summary>Comprueba la otra mitad del arreglo: que Main.hoverItemName no se queda pegado
		/// al ultimo objeto tras apartar el raton (el reseteo al principio de
		/// <see cref="PanelTerrakeepState.Draw"/>).</summary>
		private static void ComprobarSinHover(string zona)
		{
			bool sigueEnganchado = !string.IsNullOrEmpty(Main.hoverItemName);
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP (" + zona +
				") - tras apartar el raton de toda ranura: Main.hoverItemName=\"" + Main.hoverItemName + "\". " +
				(sigueEnganchado
					? "FALLO: el tooltip se ha quedado pegado al objeto anterior."
					: "OK: se ha vaciado solo, no se queda pegado."));
		}

		private static void Terminar()
		{
			_terminada = true;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP COMPLETA.");
		}
	}
}
