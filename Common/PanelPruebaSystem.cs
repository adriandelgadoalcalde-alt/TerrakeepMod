using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje;

namespace TerrakeepMod.Common
{
	/// <summary>
	/// Punto de entrada del area de <b>Personaje</b> dentro del panel unico, y sitio donde vive la
	/// autoprueba de WS1.
	/// <para />
	/// Nacio en WS0 abriendo un panel de prueba de una sola ranura, en WS1 paso a abrir el panel
	/// de Personaje entero, y desde la fusion de los seis paneles ya no abre ninguna
	/// <c>UIState</c> propia: delega en <see cref="PanelTerrakeepSystem"/>, que abre el panel
	/// unico en la pestaña "Personaje".
	/// </summary>
	/// <remarks>
	/// Se ha conservado a proposito el nombre de la clase y del archivo: es el punto de entrada
	/// historico del mod, lo referencian las autopruebas y los scripts de verificacion, y
	/// renombrarlo no le aporta nada al usuario.
	/// </remarks>
	public class PanelPruebaSystem : ModSystem
	{
		/// <summary>Variable de entorno que activa la autoprueba. Vale "1" para abrir el panel
		/// solo, o cualquier otro valor no vacio para lo mismo. Si no esta definida, el mod se
		/// comporta con total normalidad y no hace nada por su cuenta.</summary>
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST";

		private static bool _autopruebaHecha;
		private static int _fotogramasEnMundo;

		/// <summary>true si el panel esta abierto Y en la pestaña de Personaje.</summary>
		public static bool PanelAbierto => PanelTerrakeepSystem.AreaAbiertaEs(AreaTerrakeep.Personaje);

		/// <summary>El contenido de Personaje montado ahora mismo, o null. Lo usa la autoprueba de
		/// WS1 para recorrer las sub-pestañas sin simular clics.</summary>
		public static ContenidoPersonaje PanelActual
		{
			get
			{
				PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
				return panel != null ? panel.Personaje : null;
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// La autoprueba va PRIMERO y sin condiciones. Motivo real, encontrado verificando WS0
			// en el juego: la lectura de un ModKeybind puede lanzar KeyNotFoundException durante
			// los primeros fotogramas de una partida y abortar el resto del metodo, con lo que la
			// autoprueba no llegaba a dispararse nunca. Ahora las teclas las lee
			// PanelTerrakeepSystem (con su propio try/catch), pero el orden se mantiene.
			ActualizarAutoprueba();
			AutopruebaPersonaje.Actualizar();
		}

		/// <summary>Los catalogos que cachea el area de Personaje (tintes de pelo, lista de
		/// desbloqueos) se tiran al descargar el mod: al recargar mods los ids cambian y una lista
		/// vieja seria mentira.</summary>
		public override void Unload()
		{
			PersonajeVivo.Descargar();
			Desbloqueos.Descargar();
		}

		/// <summary>Abre el panel en Personaje si esta cerrado, y lo cierra si ya estaba ahi.</summary>
		public static void AlternarPanel(string origen)
		{
			PanelTerrakeepSystem.AlternarArea(AreaTerrakeep.Personaje, origen);
		}

		public static void AbrirPanel(string origen)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, origen);
		}

		public static void CerrarPanel(string origen)
		{
			PanelTerrakeepSystem.CerrarPanel(origen);
		}

		/// <summary>
		/// Autoprueba: si la variable de entorno esta puesta, espera a estar dentro del mundo y
		/// abre el panel por su cuenta, dejando en el log todo lo que hay que comprobar. Sirve
		/// para verificar la cadena entera de forma reproducible sin simular pulsaciones.
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
