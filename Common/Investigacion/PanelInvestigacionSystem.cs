using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.UI.Investigacion;

namespace TerrakeepMod.Common.Investigacion
{
	/// <summary>
	/// Punto de entrada del panel de Investigacion (WS5): carga el arbol, registra el atajo de
	/// teclado propio y abre/cierra el panel.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Mecanica de apertura separada del contenido, a proposito.</b> Todo lo que hay en esta
	/// clase (el <c>ModKeybind</c>, el <c>IngameFancyUI.OpenUIState</c>, el alternar abierto/
	/// cerrado) es lo que va a sobrar cuando los seis paneles del mod se fusionen en uno solo con
	/// pestañas y una unica tecla. El CONTENIDO real - el arbol, la lista y los botones - vive en
	/// <see cref="ContenidoInvestigacion"/>, que es un <c>UIElement</c> normal y corriente que se
	/// puede colgar de cualquier sitio; <see cref="PanelInvestigacionState"/> no es mas que el
	/// marco a pantalla completa que lo envuelve mientras siga siendo un panel suelto. Fusionar
	/// esto es mover un <c>UIElement</c>, no reescribir nada.
	/// </para>
	/// <para>
	/// El atajo se registra <b>desde aqui</b> y no desde la clase <c>Terrakeep</c>, igual que hizo
	/// WS4: asi este workstream no toca ni un archivo compartido con los demas.
	/// <c>KeybindLoader.RegisterKeybind</c> solo necesita el <c>Mod</c>, que un <c>ModSystem</c> ya
	/// tiene asignado en <c>ModType.Mod</c> antes de que corra su <c>Load()</c>.
	/// </para>
	/// </remarks>
	public class PanelInvestigacionSystem : ModSystem
	{
		/// <summary>Variable de entorno que activa la autoprueba de WS5. Propia, para no disparar
		/// a la vez los paneles de los demas workstreams.</summary>
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST_WS5";

		private static ModKeybind _atajo;
		private static PanelInvestigacionState _panel;
		private static uint _ultimoFotogramaAlternado;

		private static bool _arbolConstruido;

		/// <summary>Panel abierto ahora mismo, o null. Lo usa la autoprueba.</summary>
		public static PanelInvestigacionState PanelActual => _panel;

		/// <summary>true si el panel de Investigacion esta abierto ahora mismo.</summary>
		public static bool PanelAbierto =>
			Main.InGameUI != null && Main.InGameUI.CurrentState is PanelInvestigacionState;

		public override void Load()
		{
			RegistroInvestigacion.Mod = Mod;

			// Los .json hay que leerlos mientras el .tmod sigue abierto (hallazgo de WS4).
			CatalogoInvestigacion.LeerArchivos(Mod);

			if (!Main.dedServ) {
				// I no esta asignada a nada en los controles por defecto de Terraria (comprobado
				// en el tModLoader.dll instalado, PlayerInput: las teclas de fabrica son W A S D
				// E R H J B M C y las de la barra rapida), y K, J y L ya las usan los otros
				// paneles de este mismo mod. Reasignable en Ajustes > Controles como cualquier
				// otra, gracias al SembradorDeAtajos de WS7.
				_atajo = KeybindLoader.RegisterKeybind(Mod, "AbrirInvestigacion", Keys.I);
			}
		}

		public override void PostSetupContent()
		{
			// A estas alturas todos los mods han registrado su contenido, asi que
			// ContentSamples.ItemsByType y la tabla real de sacrificios ya estan completas.
			CatalogoInvestigacion.Construir();
			_arbolConstruido = true;

			RegistroInvestigacion.Linea(
				$"{Terrakeep.LogTag} Investigacion: arbol construido. {CatalogoInvestigacion.ResumenConstruccion}");
		}

		public override void Unload()
		{
			_atajo = null;
			_panel = null;
			_arbolConstruido = false;
			RegistroInvestigacion.Mod = null;
			CatalogoInvestigacion.Descargar();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// Primero y sin condiciones, por el mismo motivo que documento WS0:
			// ModKeybind.JustPressed puede lanzar KeyNotFoundException durante los primeros
			// segundos de una partida (PlayerInput.Triggers todavia no conoce los atajos de mods)
			// y abortaria el resto del metodo antes de llegar a la autoprueba.
			AutopruebaInvestigacion.Actualizar();

			if (_atajo == null) {
				return;
			}

			try {
				if (_atajo.JustPressed) {
					AlternarPanel("atajo de teclado (tecla I)");
				}
			}
			catch (KeyNotFoundException) {
				// Se autocorrige solo en cuanto el motor procesa el PlayerInput.reinitialize.
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

			// Si el mod se cargo antes de que hubiera partida (lo normal) el arbol ya esta; esto
			// cubre el caso de que PostSetupContent no llegara a correr por lo que sea.
			if (!_arbolConstruido) {
				CatalogoInvestigacion.Construir();
				_arbolConstruido = true;
			}

			// Instancia nueva en cada apertura, misma costumbre que los demas paneles del mod: el
			// estado depende del personaje de la partida actual y OnInitialize solo corre una vez
			// por instancia.
			_panel = new PanelInvestigacionState();
			IngameFancyUI.OpenUIState(_panel);

			RegistroInvestigacion.Linea(
				$"{Terrakeep.LogTag} PANEL INVESTIGACION ABIERTO via {origen}. " +
				$"Jugador: \"{Main.LocalPlayer.name}\" (dificultad={Main.LocalPlayer.difficulty}, " +
				$"ModoViaje={EstadoInvestigacion.ModoViaje}). Mundo: \"{Main.worldName}\" " +
				$"(GameMode={Main.GameMode}). " +
				$"Progreso global: {EstadoInvestigacion.TotalCompletos}/{EstadoInvestigacion.TotalInvestigable}. " +
				$"Main.inFancyUI={Main.inFancyUI}, " +
				$"InGameUI.CurrentState={Main.InGameUI.CurrentState?.GetType().FullName}");
		}

		public static void CerrarPanel(string origen)
		{
			if (!PanelAbierto) {
				return;
			}

			RegistroInvestigacion.Linea($"{Terrakeep.LogTag} PANEL INVESTIGACION CERRADO via {origen}.");
			IngameFancyUI.Close();
			_panel = null;
		}

		/// <summary>
		/// Tecla con la que el jugador abre el menu del Modo Viaje del propio juego, leida de su
		/// perfil de controles real (no supuesta: la de fabrica es la C, pero es reasignable).
		/// Se enseña en el panel para que se vea de un vistazo donde comprobar en el juego lo que
		/// se acaba de investigar aqui.
		/// </summary>
		public static string TeclaMenuDelJuego()
		{
			try {
				PlayerInputProfile perfil = PlayerInput.CurrentProfile;
				if (perfil == null || !perfil.InputModes.ContainsKey(InputMode.Keyboard)) {
					return null;
				}

				List<string> teclas;
				if (!perfil.InputModes[InputMode.Keyboard].KeyStatus.TryGetValue("ToggleCreativeMenu", out teclas)
					|| teclas == null || teclas.Count == 0) {
					return null;
				}
				return string.Join("/", teclas);
			}
			catch (KeyNotFoundException) {
				return null;
			}
		}
	}
}
