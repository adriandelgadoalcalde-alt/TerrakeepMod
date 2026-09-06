using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.UI.Libreria;

namespace TerrakeepMod.Common.Libreria
{
	/// <summary>
	/// Punto de entrada del panel de Libreria (WS3): lee los archivos de datos, registra el atajo
	/// de teclado propio y abre/cierra el panel.
	/// </summary>
	/// <remarks>
	/// El atajo se registra <b>desde aqui</b> y no desde la clase <c>Terrakeep</c>, igual que hizo
	/// WS4: asi este workstream no toca ni un archivo compartido con los demas.
	/// <c>KeybindLoader.RegisterKeybind</c> solo necesita el <c>Mod</c>, que un <c>ModSystem</c>
	/// ya tiene asignado en <c>ModType.Mod</c> antes de que corra su <c>Load()</c>.
	/// <para />
	/// <b>Esta clase entera desaparece</b> cuando los seis paneles se fusionen en uno con
	/// pestañas: lo que sobrevive es <c>ContenidoLibreria</c>. Ver
	/// <see cref="PanelLibreriaState"/>.
	/// </remarks>
	public class PanelLibreriaSystem : ModSystem
	{
		/// <summary>Variable de entorno que abre el panel solo, sin depender de que nadie pulse
		/// una tecla. Es propia de WS3 para no disparar a la vez los paneles de otros
		/// workstreams.</summary>
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST_WS3";

		private static ModKeybind _atajo;
		private static PanelLibreriaState _panel;
		private static uint _ultimoFotogramaAlternado;

		private static bool _autopruebaHecha;
		private static int _fotogramasEnMundo;

		/// <summary>true si el panel de Libreria esta abierto ahora mismo.</summary>
		public static bool PanelAbierto {
			get { return Main.InGameUI != null && Main.InGameUI.CurrentState is PanelLibreriaState; }
		}

		/// <summary>Instancia abierta ahora mismo, o null. La usa el arnes de pruebas.</summary>
		public static PanelLibreriaState PanelActual {
			get { return _panel; }
		}

		public override void Load()
		{
			RegistroLibreria.Mod = Mod;

			// Los archivos de datos hay que leerlos con el .tmod todavia abierto (ver ArbolLibreria).
			ArbolLibreria.LeerArchivos(Mod);

			if (!Main.dedServ) {
				// O no esta asignada a nada en los controles por defecto de Terraria (comprobado en
				// PlayerInput.SetupKeys del tModLoader.dll instalado: vanilla usa W/A/S/D, Espacio,
				// Escape, E, R, H, J, B, Tab, M, C, F1-F4, los numeros y los signos +/-), y K, L, J,
				// Z e Y ya las usan los demas paneles de este mismo mod. Reasignable por el usuario
				// en Ajustes > Controles como cualquier otro atajo.
				_atajo = KeybindLoader.RegisterKeybind(Mod, "AbrirLibreria", Keys.O);
			}
		}

		public override void Unload()
		{
			_atajo = null;
			_panel = null;
			RegistroLibreria.Mod = null;
			ArbolLibreria.Descargar();
			CatalogoVivo.Invalidar();
		}

		/// <summary>
		/// El idioma se puede cambiar en vivo (WS7) y con el cambian los nombres de TODOS los
		/// objetos y de las carpetas. El catalogo y el arbol se tiran para que se reconstruyan con
		/// los nombres nuevos en cuanto alguien vuelva a abrir la Libreria.
		/// </summary>
		public override void OnLocalizationsLoaded()
		{
			CatalogoVivo.Invalidar();
			ArbolLibreria.Invalidar();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// Primero y sin condiciones, por el mismo motivo que documentaron WS0 y WS4:
			// ModKeybind.JustPressed puede lanzar KeyNotFoundException durante los primeros
			// fotogramas de una partida (PlayerInput.Triggers todavia no conoce los atajos de
			// mods) y abortaria el resto del metodo antes de llegar a la autoprueba.
			ActualizarAutoprueba();
			AutopruebaLibreria.Actualizar();

			if (PanelAbierto) {
				// Se reafirma cada fotograma: si algo lo volviera a poner a false,
				// Player.dropItemCheck vaciaria el objeto que se lleva cogido con el raton.
				PanelLibreriaState.MantenerInventarioAbierto();
			}

			if (_atajo == null) {
				return;
			}

			try {
				if (_atajo.JustPressed) {
					AlternarPanel("atajo de teclado (tecla O)");
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

			// El catalogo se construye la primera vez que hace falta de verdad, no al cargar el
			// mod: quien no abra la Libreria no paga el recorrido de los ~8000 objetos.
			ArbolLibreria.ConstruirSiHaceFalta();

			// Instancia nueva cada vez, igual que los demas paneles del mod: OnInitialize solo
			// corre una vez por instancia y las ranuras de destino capturan los arrays reales del
			// jugador, que se reasignan al cargar otro personaje.
			_panel = new PanelLibreriaState();
			IngameFancyUI.OpenUIState(_panel);
			PanelLibreriaState.MantenerInventarioAbierto();

			RegistroLibreria.Linea(
				$"{Terrakeep.LogTag} PANEL LIBRERIA ABIERTO via {origen}. " +
				$"Jugador: \"{Main.LocalPlayer.name}\". Mundo: \"{Main.worldName}\". " +
				$"Arbol: {ArbolLibreria.Resumen}. " +
				$"Main.inFancyUI={Main.inFancyUI}, " +
				$"InGameUI.CurrentState={Main.InGameUI.CurrentState?.GetType().FullName}");
		}

		public static void CerrarPanel(string origen)
		{
			if (!PanelAbierto) {
				return;
			}

			// Fuera del panel nadie dibuja el objeto que se lleva "cogido" con el raton.
			PanelLibreriaState.DevolverObjetoDelRaton();

			RegistroLibreria.Linea($"{Terrakeep.LogTag} PANEL LIBRERIA CERRADO via {origen}.");
			IngameFancyUI.Close();
			_panel = null;
		}

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
			if (_fotogramasEnMundo < 180) {   // ~3 s a 60 fps: da tiempo a entrar del todo al mundo.
				return;
			}

			_autopruebaHecha = true;

			// La marca la pone el script de verificacion: el client.log es compartido por todas las
			// instancias del juego y tModLoader lo rota, asi que con varios workstreams probando a
			// la vez hace falta poder identificar CUAL es el log de esta ejecucion concreta.
			string marca = Environment.GetEnvironmentVariable("TERRAKEEP_WS3_MARCA");
			RegistroLibreria.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA WS3: {VariableAutoprueba} detectada. Marca de ejecucion: {marca}");

			AbrirPanel("autoprueba (" + VariableAutoprueba + ")");
		}
	}
}
