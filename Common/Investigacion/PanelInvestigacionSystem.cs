using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Investigacion;
using TerrakeepMod.UI.Panel;

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

		private static bool _arbolConstruido;

		/// <summary>El atajo de Investigacion. Lo registra este sistema y lo LEE
		/// <c>PanelTerrakeepSystem</c>, que es quien abre el panel unico en esta pestaña.</summary>
		public static ModKeybind Atajo => _atajo;

		/// <summary>El contenido de Investigacion montado ahora mismo, o null. Lo usa la
		/// autoprueba.</summary>
		public static ContenidoInvestigacion PanelActual
		{
			get
			{
				PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
				return panel != null ? panel.Investigacion : null;
			}
		}

		/// <summary>true si el panel esta abierto Y en la pestaña de Investigacion.</summary>
		public static bool PanelAbierto => PanelTerrakeepSystem.AreaAbiertaEs(AreaTerrakeep.Investigacion);

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
			RegistroInvestigacion.Linea(
				$"{Terrakeep.LogTag} Investigacion: carpetas raiz -> {CatalogoInvestigacion.ResumenRaices()}");
		}

		public override void Unload()
		{
			_atajo = null;
			_arbolConstruido = false;
			RegistroInvestigacion.Mod = null;
			CatalogoInvestigacion.Descargar();
		}

		/// <summary>
		/// El arbol de carpetas lleva DENTRO los nombres ya resueltos (los de las categorias de los
		/// objetos de mod, el de "Otros objetos" y los de las carpetas del arbol curado), asi que
		/// hay que reconstruirlo cuando cambian las traducciones. Mismo criterio que ya usaba la
		/// Libreria en <c>PanelLibreriaSystem.OnLocalizationsLoaded</c>.
		/// </summary>
		public override void OnLocalizationsLoaded()
		{
			if (Main.dedServ || !_arbolConstruido) {
				return;
			}
			CatalogoInvestigacion.Construir();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			AutopruebaInvestigacion.Actualizar();
		}

		/// <summary>Abre el panel en la pestaña de Investigacion, o lo cierra si ya estaba ahi.</summary>
		public static void AlternarPanel(string origen)
		{
			PanelTerrakeepSystem.AlternarArea(AreaTerrakeep.Investigacion, origen);
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

			RegistroInvestigacion.Linea(
				$"{Terrakeep.LogTag} Investigacion: se pide abrir el panel via {origen}. " +
				$"Jugador: \"{Main.LocalPlayer.name}\" (dificultad={Main.LocalPlayer.difficulty}, " +
				$"ModoViaje={EstadoInvestigacion.ModoViaje}). Mundo: \"{Main.worldName}\" " +
				$"(GameMode={Main.GameMode}). " +
				$"Progreso global: {EstadoInvestigacion.TotalCompletos}/{EstadoInvestigacion.TotalInvestigable}.");

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Investigacion, origen);
		}

		public static void CerrarPanel(string origen)
		{
			PanelTerrakeepSystem.CerrarPanel(origen);
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
