using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Deslizador de 0 a 1. Hereda de <see cref="UIColoredSliderSimple"/>, que es la barra de
	/// color REAL del juego (usa <c>TextureAssets.ColorBar</c>), pero que por si sola solo
	/// dibuja: no lee el raton. Esta clase le añade el arrastre.
	/// <para />
	/// La posicion del raton se toma de <c>Main.InGameUI.MousePosition</c> y no de
	/// <c>Main.MouseScreen</c> a proposito: la interfaz del mod se dibuja con
	/// <c>InterfaceScaleType.UI</c>, asi que sus coordenadas van divididas por la escala de
	/// interfaz del usuario. Con <c>Main.MouseScreen</c> el deslizador se descuadraria en cuanto
	/// alguien tuviera la escala de interfaz distinta de 100%.
	/// </summary>
	public class DeslizadorTk : UIColoredSliderSimple
	{
		private bool _arrastrando;

		/// <summary>Se dispara con el valor nuevo (0..1) mientras se arrastra.</summary>
		public event Action<float> AlCambiar;

		public DeslizadorTk()
		{
			Width.Set(150f, 0f);
			Height.Set(20f, 0f);
			EmptyColor = new Color(30, 35, 60);
			FilledColor = new Color(120, 160, 240);
		}

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			_arrastrando = true;
			AplicarPosicionRaton();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
			}

			if (!_arrastrando) {
				return;
			}

			// El arrastre sigue aunque el raton se salga del elemento, como en cualquier
			// deslizador de verdad; solo termina al soltar el boton.
			if (!Main.mouseLeft) {
				_arrastrando = false;
				return;
			}

			AplicarPosicionRaton();
		}

		private void AplicarPosicionRaton()
		{
			CalculatedStyle dim = GetDimensions();
			if (dim.Width <= 0f) {
				return;
			}

			float proporcion = (Main.InGameUI.MousePosition.X - dim.X) / dim.Width;
			if (proporcion < 0f) {
				proporcion = 0f;
			}
			if (proporcion > 1f) {
				proporcion = 1f;
			}

			if (Math.Abs(proporcion - FillPercent) < 0.0005f) {
				return;
			}

			FillPercent = proporcion;
			if (AlCambiar != null) {
				AlCambiar(proporcion);
			}
		}
	}
}
