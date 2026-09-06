using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Ajustes;
using TerrakeepMod.UI.Panel;

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

		private bool _idiomaArrancado;
		private int _fotogramasEnMundo;
		private bool _autopruebaHecha;

		/// <summary>true si el panel esta abierto Y en la pestaña de Ajustes.</summary>
		public static bool PanelAbierto {
			get { return PanelTerrakeepSystem.AreaAbiertaEs(AreaTerrakeep.Ajustes); }
		}

		/// <summary>El contenido de Ajustes montado ahora mismo, o null.</summary>
		public static ContenidoAjustes Contenido {
			get {
				PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
				return panel != null ? panel.Ajustes : null;
			}
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
			// Los catalogos que guardan NOMBRES ya resueltos hay que tirarlos al cambiar de idioma:
			// los de Personaje son los tintes de pelo (el "Ninguno" de la posicion 0 es texto
			// nuestro, y los demas son nombres de objeto, que tambien cambian).
			Personaje.PersonajeVivo.Descargar();
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

			// Sin esto NINGUN atajo del mod tiene tecla asignada - ver la explicacion completa,
			// con el codigo real de tModLoader que lo provoca, en SembradorDeAtajos.
			SembradorDeAtajos.SembrarSiHaceFalta();

			// La autoprueba va primero e incondicional, por la misma razon que en WS0: si el
			// atajo lanza KeyNotFoundException (PlayerInput.Triggers todavia no conoce los
			// atajos del mod), la excepcion abortaria el resto del metodo y la autoprueba no
			// llegaria a dispararse nunca.
			ActualizarAutoprueba();
		}

		/// <summary>Abre el panel en la pestaña de Ajustes, o lo cierra si ya estaba ahi.</summary>
		public static void AlternarPanel(string origen)
		{
			PanelTerrakeepSystem.AlternarArea(AreaTerrakeep.Ajustes, origen);
		}

		public static void AbrirPanel(string origen)
		{
			Terrakeep.Instance.Logger.Info(
				Terrakeep.LogTag + " Ajustes: se pide abrir el panel via " + origen +
				". Idioma configurado=" + Idiomas.IdiomaConfigurado +
				", cultura activa del juego=" + Idiomas.CulturaActiva + ".");

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Ajustes, origen);
		}

		public static void CerrarPanel(string origen)
		{
			PanelTerrakeepSystem.CerrarPanel(origen);
		}

		private void ActualizarAutoprueba()
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableAutoprueba))) {
				return;
			}

			if (_autopruebaHecha) {
				// Los modos que esperan a algo de fuera (una pulsacion real de Ctrl+Z, una
				// captura de pantalla) siguen vivos despues de arrancar.
				AutopruebaWs7.Vigilar();
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
