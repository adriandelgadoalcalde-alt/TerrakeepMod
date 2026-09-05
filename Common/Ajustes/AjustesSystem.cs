using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.UI.Ajustes;

namespace TerrakeepMod.Common.Ajustes
{
	/// <summary>
	/// Abre y cierra el panel de Ajustes de Terrakeep (tecla <b>J</b>) y se encarga de que el
	/// idioma guardado en <see cref="AjustesConfig"/> se aplique en cuanto el juego esta en
	/// marcha.
	/// <para />
	/// La tecla es J porque K y L ya estan cogidas por otros paneles del mod (WS0/WS1 y WS4), y
	/// porque J no esta asignada a nada en los controles de Terraria de serie. Como todo
	/// <c>ModKeybind</c>, el usuario puede reasignarla en Ajustes &gt; Controles del propio juego.
	/// </summary>
	public class AjustesSystem : ModSystem
	{
		/// <summary>Variable de entorno que activa la autoprueba de WS7. Sirve para verificar en
		/// el juego real, sin que nadie pulse nada, el deshacer/rehacer y el cambio de idioma en
		/// vivo. Deliberadamente distinta de la de WS0 (<c>TERRAKEEP_AUTOTEST</c>) para que las
		/// dos autopruebas no se peleen por la interfaz.</summary>
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST_WS7";

		/// <summary>Atajo que abre y cierra el panel de Ajustes.</summary>
		public static ModKeybind AbrirAjustesKeybind { get; private set; }

		private static PanelAjustesState _panel;
		private static uint _ultimoFotogramaAlternado;

		private bool _idiomaArrancado;
		private int _fotogramasEnMundo;
		private bool _autopruebaHecha;

		/// <summary>true si el panel de Ajustes esta abierto ahora mismo.</summary>
		public static bool PanelAbierto {
			get { return Main.InGameUI != null && Main.InGameUI.CurrentState is PanelAjustesState; }
		}

		public override void OnModLoad()
		{
			if (!Main.dedServ) {
				AbrirAjustesKeybind = KeybindLoader.RegisterKeybind(Mod, "AbrirAjustes", Keys.J);
			}
		}

		public override void OnModUnload()
		{
			AbrirAjustesKeybind = null;
			_panel = null;
		}

		/// <summary>
		/// Hook oficial de tModLoader que se dispara cada vez que se recargan las traducciones,
		/// venga el cambio de este mod o del menu de idioma del propio juego
		/// (<c>LanguageManager.ReloadLanguage</c> lo llama al final, via
		/// <c>SystemLoader.OnLocalizationsLoaded</c>). Es el sitio correcto para que un panel ya
		/// abierto vuelva a pedir sus textos.
		/// </summary>
		public override void OnLocalizationsLoaded()
		{
			Idiomas.Avisar();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// El idioma guardado no se puede aplicar durante la carga de mods (implica recargar
			// todas las traducciones del juego); se aplica en el primer fotograma real de interfaz.
			if (!_idiomaArrancado) {
				_idiomaArrancado = true;
				Idiomas.ElJuegoYaEstaEnMarcha();
			}

			// La autoprueba va primero e incondicional, por la misma razon que en WS0: si el
			// atajo lanza KeyNotFoundException (PlayerInput.Triggers todavia no conoce los
			// atajos del mod), la excepcion abortaria el resto del metodo y la autoprueba no
			// llegaria a dispararse nunca.
			ActualizarAutoprueba();

			if (AbrirAjustesKeybind == null) {
				return;
			}

			try {
				if (AbrirAjustesKeybind.JustPressed && !Main.drawingPlayerChat) {
					AlternarPanel("atajo de teclado (tecla J)");
				}
			}
			catch (KeyNotFoundException) {
				// Hueco conocido entre que el mod carga y que PlayerInput procesa su
				// reinitialize pendiente. Se autocorrige solo al fotograma siguiente.
			}
		}

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

			_panel = new PanelAjustesState();
			IngameFancyUI.OpenUIState(_panel);

			Terrakeep.Instance.Logger.Info(
				Terrakeep.LogTag + " PANEL DE AJUSTES ABIERTO via " + origen +
				". Idioma configurado=" + Idiomas.IdiomaConfigurado +
				", cultura activa del juego=" + Idiomas.CulturaActiva +
				", InGameUI.CurrentState=" + (Main.InGameUI.CurrentState != null ? Main.InGameUI.CurrentState.GetType().FullName : "null"));
		}

		public static void CerrarPanel(string origen)
		{
			if (!PanelAbierto) {
				return;
			}

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " PANEL DE AJUSTES CERRADO via " + origen + ".");
			IngameFancyUI.Close();
			_panel = null;
		}

		private void ActualizarAutoprueba()
		{
			if (_autopruebaHecha || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableAutoprueba))) {
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

			_autopruebaHecha = true;
			AutopruebaWs7.Ejecutar();
		}
	}
}
