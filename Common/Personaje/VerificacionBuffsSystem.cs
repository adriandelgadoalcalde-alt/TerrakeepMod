using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.Common.Personaje
{
	/// <summary>
	/// Arnes de verificacion de cobertura del arbol de "Añadir un buff" (ver <see cref="ArbolBuffs"/>).
	/// <para />
	/// Vive en su PROPIO <see cref="ModSystem"/> y su PROPIA variable de entorno
	/// (<see cref="Variable"/>), a proposito: en el momento de escribir esto habia otros agentes
	/// trabajando en paralelo sobre <c>PanelPruebaSystem.cs</c>/<c>AutopruebaPersonaje.cs</c> (los
	/// puntos de entrada compartidos del mod) y sobre sandboxes de juego reales ya abiertos -
	/// tocarlos aqui habria arriesgado un commit a medias ajeno o una carrera de <c>client.log</c>
	/// compartido, el mismo problema que ya documentaron WS3 y WS4 en la bitacora. Este arnes no
	/// toca ningun archivo compartido: tModLoader llama solo a <c>PostSetupContent</c>/<c>UpdateUI</c>
	/// de CUALQUIER <see cref="ModSystem"/> cargado, asi que no hace falta que nadie mas lo invoque.
	/// </summary>
	/// <remarks>
	/// <b><c>PostSetupContent</c></b> existe TANTO en el cliente grafico como en el servidor
	/// dedicado (<c>BuffLoader</c>/<c>ContentSamples</c> ya estan poblados del todo ahi, para
	/// vanilla y para cualquier mod cargado) - mismo criterio que uso WS0 para su primera
	/// verificacion sin pantalla disponible: comprueba la COBERTURA DE DATOS sin necesitar
	/// <c>Main.LocalPlayer</c> ni ninguna interfaz, y por eso es lo unico que corre en el servidor.
	/// <para />
	/// <b><c>UpdateUI</c></b> solo hace algo con un jugador real cargado (cliente grafico): abre el
	/// panel unico en el area de Personaje y salta a su sub-pestaña de Buffs
	/// (<c>ContenidoPersonaje.IrAPestana</c>), para demostrar tambien que <c>PestanaBuffs</c> y su
	/// arbol de carpetas (<see cref="TerrakeepMod.UI.Personaje.Widgets.FilaCarpetaBuffTk"/>) se
	/// CONSTRUYEN Y DIBUJAN de verdad sin excepciones, no solo que los datos cuadran.
	/// </remarks>
	public class VerificacionBuffsSystem : ModSystem
	{
		/// <summary>Variable de entorno que enciende esta comprobacion.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_BUFFS";

		private bool _activa;
		private bool _comprobada;
		private bool _hecha;
		private int _fotogramasEnMundo;

		public override void PostSetupContent()
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable))) {
				return;
			}

			ArbolBuffs.ConstruirSiHaceFalta();
			Mod.Logger.Info(
				$"{Terrakeep.LogTag} VERIFICACION-BUFFS: {ArbolBuffs.Resumen}");
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
			}
			if (!_activa || _hecha || Main.dedServ) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < 180) {   // ~3 s a 60 fps: tiempo de sobra para entrar del todo.
				return;
			}

			_hecha = true;

			try {
				PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "autoprueba (" + Variable + ")");
				PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
				if (panel == null || panel.Personaje == null) {
					Mod.Logger.Warn($"{Terrakeep.LogTag} VERIFICACION-BUFFS-UI: el panel de Personaje no llego a abrirse.");
					return;
				}

				panel.Personaje.IrAPestana(3);   // 3 = Buffs, ver ContenidoPersonaje.ClavesPestana.
				panel.Recalculate();

				Mod.Logger.Info(
					$"{Terrakeep.LogTag} VERIFICACION-BUFFS-UI: pestaña activa=\"{panel.Personaje.NombrePestanaActual}\". " +
					$"{panel.Personaje.InformePestanaActual()}. Arbol: {ArbolBuffs.Resumen}");
			}
			catch (Exception ex) {
				Mod.Logger.Error($"{Terrakeep.LogTag} VERIFICACION-BUFFS-UI: EXCEPCION al abrir/dibujar la pestaña: {ex}");
			}
		}
	}
}
