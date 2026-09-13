using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Personaje;

namespace TerrakeepMod.Common.Completitud
{
	/// <summary>
	/// Arranque de la autoprueba de Completitud: abre el panel en Personaje y salta a la
	/// sub-pestaña "Completitud" (indice 7). Mismo patron que <see cref="Loadouts.LoadoutsSystem"/>
	/// (del que sale este, copiado a proposito para no acoplar las dos autopruebas entre si): el
	/// resto vive dentro de <c>PestanaCompletitud.Update</c> -&gt; <see cref="AutopruebaCompletitud"/>.
	/// </summary>
	public class CompletitudSystem : ModSystem
	{
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST_COMPLETITUD";

		private static bool _comprobada;
		private static bool _activa;
		private static bool _hecho;
		private static bool _saltoALaPestana;
		private static int _fotogramasEnMundo;

		public static bool Activa => _activa;

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableAutoprueba));
			}

			if (!_activa || _hecho) {
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

			if (!PanelPruebaSystem.PanelAbierto) {
				PanelPruebaSystem.AbrirPanel("autoprueba (" + VariableAutoprueba + ")");
				return;
			}

			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				return;
			}

			if (!_saltoALaPestana) {
				_saltoALaPestana = true;
				panel.IrAPestana(7);
				Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} AUTOPRUEBA COMPLETITUD: saltado a la " +
					$"pestaña 7 (\"{panel.NombrePestanaActual}\"). A partir de aqui la dirige AutopruebaCompletitud.");
				return;
			}

			_hecho = true;
		}
	}
}
