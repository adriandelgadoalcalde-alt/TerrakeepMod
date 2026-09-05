using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Undo
{
	/// <summary>
	/// Ata el historial de <see cref="Historial"/> al juego: los atajos globales
	/// <b>Ctrl+Z</b> / <b>Ctrl+Y</b> y el vaciado obligatorio del historial al entrar o salir de
	/// un mundo.
	///
	/// <para />
	/// <b>Por que Ctrl se comprueba a mano y no es parte del atajo.</b> Se decompilo el
	/// <c>tModLoader.dll</c> REAL instalado (v2026.7.3.0, no la referencia vieja de
	/// <c>tModLoader-Decompiled\</c>, que es 1.4.4.9): las dos sobrecargas de
	/// <c>KeybindLoader.RegisterKeybind</c> aceptan <b>una sola</b> tecla
	/// (<c>Keys defaultBinding</c>, o su nombre como <c>string</c>), y <c>ModKeybind</c> resuelve
	/// su estado indexando <c>PlayerInput.Triggers.JustPressed.KeyStatus[FullName]</c>, que es un
	/// diccionario de tecla suelta. <b>No existe ninguna API de combinaciones con modificador en
	/// esta version.</b> La forma real de hacer un Ctrl+Z es la de aqui: un <c>ModKeybind</c>
	/// normal para la letra (reasignable por el usuario en Ajustes &gt; Controles como cualquier
	/// otro) y el modificador leido directamente de <c>Main.keyState</c>, que es el
	/// <c>KeyboardState</c> del fotograma actual.
	/// </summary>
	public class HistorialSystem : ModSystem
	{
		/// <summary>Atajo de deshacer. Por defecto Z, y exige Ctrl (ver la nota de la clase).</summary>
		public static ModKeybind DeshacerKeybind { get; private set; }

		/// <summary>Atajo de rehacer. Por defecto Y, y exige Ctrl.</summary>
		public static ModKeybind RehacerKeybind { get; private set; }

		public override void OnModLoad()
		{
			if (!Main.dedServ) {
				DeshacerKeybind = KeybindLoader.RegisterKeybind(Mod, "Deshacer", Keys.Z);
				RehacerKeybind = KeybindLoader.RegisterKeybind(Mod, "Rehacer", Keys.Y);
			}
		}

		public override void OnModUnload()
		{
			DeshacerKeybind = null;
			RehacerKeybind = null;
			Historial.Pila.Limpiar();
		}

		/// <summary>
		/// El historial NO sobrevive a un cambio de partida. Las fotos guardan una referencia al
		/// array real que editaron (<c>Player.inventory</c>, <c>Main.chest[i].item</c>...) y esos
		/// arrays se reasignan al cargar otro personaje u otro mundo: deshacer despues escribiria
		/// sobre datos que ya no son los que se editaron.
		/// </summary>
		public override void OnWorldLoad()
		{
			Historial.Pila.Limpiar();
		}

		public override void OnWorldUnload()
		{
			Historial.Pila.Limpiar();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ || Main.gameMenu || Main.drawingPlayerChat) {
				return;
			}

			if (!CtrlPulsado()) {
				return;
			}

			try {
				if (DeshacerKeybind != null && DeshacerKeybind.JustPressed) {
					DeshacerConAviso("Ctrl+Z");
				}
				else if (RehacerKeybind != null && RehacerKeybind.JustPressed) {
					RehacerConAviso("Ctrl+Y");
				}
			}
			catch (KeyNotFoundException) {
				// Mismo hueco conocido de arranque que en WS0: PlayerInput todavia no ha
				// procesado el reinitialize que registra los atajos de los mods.
			}
		}

		/// <summary>Ctrl izquierdo o derecho, leidos del teclado real de este fotograma.</summary>
		public static bool CtrlPulsado()
		{
			return Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl);
		}

		/// <summary>Deshace y avisa por el chat del juego, en el idioma configurado.</summary>
		public static void DeshacerConAviso(string origen)
		{
			string etiqueta = Historial.Deshacer();
			if (etiqueta == null) {
				Avisar(Idiomas.Texto("Historial.NadaQueDeshacer"), Color.Gray, origen, null);
			}
			else {
				Avisar(Idiomas.Texto("Historial.Deshecho", etiqueta), Color.Orange, origen, etiqueta);
			}
		}

		/// <summary>Rehace y avisa por el chat del juego, en el idioma configurado.</summary>
		public static void RehacerConAviso(string origen)
		{
			string etiqueta = Historial.Rehacer();
			if (etiqueta == null) {
				Avisar(Idiomas.Texto("Historial.NadaQueRehacer"), Color.Gray, origen, null);
			}
			else {
				Avisar(Idiomas.Texto("Historial.Rehecho", etiqueta), Color.LightGreen, origen, etiqueta);
			}
		}

		private static void Avisar(string mensaje, Color color, string origen, string etiqueta)
		{
			if (!Main.gameMenu) {
				Main.NewText(mensaje, color);
			}

			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(
					Terrakeep.LogTag + " HISTORIAL via " + origen + ": " + mensaje +
					" (entrada=" + (etiqueta ?? "ninguna") +
					", puedeDeshacer=" + Historial.Pila.PuedeDeshacer +
					", puedeRehacer=" + Historial.Pila.PuedeRehacer + ").");
			}
		}
	}
}
