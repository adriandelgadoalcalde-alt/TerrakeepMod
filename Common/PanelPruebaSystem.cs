using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.UI;

namespace TerrakeepMod.Common
{
	/// <summary>
	/// Abre y cierra el panel de prueba de WS0, y contiene el modo de autoprueba que permite
	/// verificar la cadena entera sin depender de que alguien pulse teclas a mano.
	/// </summary>
	public class PanelPruebaSystem : ModSystem
	{
		/// <summary>Variable de entorno que activa la autoprueba. Vale "1" para abrir el panel
		/// solo, o cualquier otro valor no vacio para lo mismo. Si no esta definida, el mod se
		/// comporta con total normalidad y no hace nada por su cuenta.</summary>
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST";

		private static PanelPruebaState _panel;

		// Guarda contra el doble disparo: el atajo se consulta desde dos sitios (UpdateUI, que
		// funciona tambien con el panel abierto, y ModPlayer.ProcessTriggers, que es la via
		// recomendada por tModLoader). Sin esto, una sola pulsacion abriria y cerraria el panel
		// en el mismo fotograma.
		private static uint _ultimoFotogramaAlternado;

		private static bool _autopruebaHecha;
		private static int _fotogramasEnMundo;

		/// <summary>true si el panel de prueba esta abierto ahora mismo.</summary>
		public static bool PanelAbierto =>
			Main.InGameUI != null && Main.InGameUI.CurrentState is PanelPruebaState;

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ || Terrakeep.AbrirPanelKeybind == null) {
				return;
			}

			if (Terrakeep.AbrirPanelKeybind.JustPressed) {
				AlternarPanel("atajo de teclado (ModSystem.UpdateUI)");
			}

			ActualizarAutoprueba();
		}

		/// <summary>Abre el panel si esta cerrado y lo cierra si esta abierto.</summary>
		public static void AlternarPanel(string origen)
		{
			if (_ultimoFotogramaAlternado == Main.GameUpdateCount) {
				return;
			}
			_ultimoFotogramaAlternado = Main.GameUpdateCount;

			if (PanelAbierto) {
				CerrarPanel(origen);
			}
			else {
				AbrirPanel(origen);
			}
		}

		public static void AbrirPanel(string origen)
		{
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}

			// Se construye un UIState nuevo cada vez a proposito: OnInitialize solo se ejecuta
			// una vez por instancia, y captura el array Main.LocalPlayer.inventory. Ese array se
			// reasigna al cargar otro personaje, asi que reutilizar la instancia dejaria el slot
			// apuntando al inventario de la partida anterior.
			_panel = new PanelPruebaState();

			// Mismo mecanismo que usan el bestiario, el menu de emotes y los menus de ajustes
			// del propio juego: oculta el resto de la interfaz y toma el control.
			IngameFancyUI.OpenUIState(_panel);

			Item objeto = Main.LocalPlayer.inventory[0];
			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} PANEL ABIERTO via {origen}. " +
				$"Jugador: \"{Main.LocalPlayer.name}\" (vida {Main.LocalPlayer.statLife}/{Main.LocalPlayer.statLifeMax}). " +
				$"Mundo: \"{Main.worldName}\". " +
				$"inventory[0]: type={objeto.type} stack={objeto.stack} prefix={objeto.prefix} nombre=\"{objeto.Name}\". " +
				$"Main.inFancyUI={Main.inFancyUI}, InGameUI.CurrentState={Main.InGameUI.CurrentState?.GetType().FullName}");
		}

		public static void CerrarPanel(string origen)
		{
			if (!PanelAbierto) {
				return;
			}

			Item objeto = Main.LocalPlayer.inventory[0];
			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} PANEL CERRADO via {origen}. " +
				$"inventory[0] al cerrar: type={objeto.type} stack={objeto.stack} nombre=\"{objeto.Name}\".");

			IngameFancyUI.Close();
			_panel = null;
		}

		/// <summary>
		/// Autoprueba: si la variable de entorno esta puesta, espera a estar dentro del mundo y
		/// abre el panel por su cuenta, dejando en el log todo lo que hay que comprobar. Sirve
		/// para verificar la cadena de WS0 de forma reproducible sin simular pulsaciones.
		/// </summary>
		private static void ActualizarAutoprueba()
		{
			if (_autopruebaHecha || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableAutoprueba))) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;

			// ~3 segundos a 60 fps. Da tiempo a que termine de entrar al mundo y a que el
			// inventario del personaje este ya cargado del todo.
			if (_fotogramasEnMundo < 180) {
				return;
			}

			_autopruebaHecha = true;
			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} AUTOPRUEBA: {VariableAutoprueba} detectada, abriendo el panel automaticamente.");
			AbrirPanel("autoprueba (" + VariableAutoprueba + ")");
		}
	}
}
