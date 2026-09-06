using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Panel;

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

		private static bool _autopruebaHecha;
		private static int _fotogramasEnMundo;

		/// <summary>El atajo de la Libreria. Lo registra este sistema y lo LEE
		/// <c>PanelTerrakeepSystem</c>, que es quien abre el panel unico en esta pestaña.</summary>
		public static ModKeybind Atajo {
			get { return _atajo; }
		}

		/// <summary>true si el panel esta abierto Y en la pestaña de Libreria.</summary>
		public static bool PanelAbierto {
			get { return PanelTerrakeepSystem.AreaAbiertaEs(AreaTerrakeep.Libreria); }
		}

		/// <summary>El contenido de la Libreria montado ahora mismo, o null. Lo usa el arnes de
		/// pruebas.</summary>
		public static ContenidoLibreria PanelActual {
			get {
				PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
				return panel != null ? panel.Libreria : null;
			}
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

			// Primero y sin condiciones, por el mismo motivo que documentaron WS0 y WS4: una
			// excepcion aqui abortaria el resto del metodo antes de llegar a la autoprueba.
			ActualizarAutoprueba();
			AutopruebaLibreria.Actualizar();
		}

		/// <summary>Abre el panel en la pestaña de Libreria, o lo cierra si ya estaba ahi.</summary>
		public static void AlternarPanel(string origen)
		{
			PanelTerrakeepSystem.AlternarArea(AreaTerrakeep.Libreria, origen);
		}

		public static void AbrirPanel(string origen)
		{
			RegistroLibreria.Linea(
				$"{Terrakeep.LogTag} Libreria: se pide abrir el panel via {origen}. " +
				$"Arbol: {ArbolLibreria.Resumen}.");
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Libreria, origen);
		}

		public static void CerrarPanel(string origen)
		{
			PanelTerrakeepSystem.CerrarPanel(origen);
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
