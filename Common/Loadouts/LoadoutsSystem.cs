using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Personaje;

namespace TerrakeepMod.Common.Loadouts
{
	/// <summary>
	/// Arranque de la autoprueba de Conjuntos (loadouts): abre el panel en Personaje y salta a la
	/// sub-pestaña "Conjuntos" (indice 6) por su cuenta. El resto de la autoprueba
	/// (<see cref="AutopruebaConjuntos"/>) vive dentro de <c>PestanaConjuntos.Update</c>, mismo
	/// patron que <c>PestanaApariencia</c>/<c>AutopruebaApariencia</c>: solo puede avanzar
	/// mientras esa pestaña concreta esta construida de verdad.
	/// <para />
	/// Variable propia (<see cref="VariableAutoprueba"/>) y no la de WS1
	/// (<c>PanelPruebaSystem.VariableAutoprueba</c>) a proposito: esa ya dispara
	/// <c>AutopruebaPersonaje</c>, que recorre las pestañas en SU propio orden - las dos a la vez
	/// se pelearian por que pestaña esta abierta.
	/// </summary>
	public class LoadoutsSystem : ModSystem
	{
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST_CONJUNTOS";

		private static bool _comprobada;
		private static bool _activa;
		private static bool _hecho;
		private static bool _saltoALaPestana;
		private static int _fotogramasEnMundo;

		/// <summary>true si la variable de entorno de esta autoprueba esta puesta. La lee
		/// <see cref="AutopruebaConjuntos"/> para saber si tiene que avanzar.</summary>
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
				panel.IrAPestana(6);
				Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} AUTOPRUEBA CONJUNTOS: saltado a la " +
					$"pestaña 6 (\"{panel.NombrePestanaActual}\"). A partir de aqui la dirige AutopruebaConjuntos.");
				return;
			}

			// A partir de aqui, PestanaConjuntos ya existe y AutopruebaConjuntos.Avanzar(this) la
			// dirige sola desde el Update de la propia pestaña. Este sistema no tiene que volver a
			// hacer nada mas: solo se queda para no reabrir/recolocar nada mientras la otra
			// autoprueba trabaja.
			_hecho = true;
		}
	}
}
