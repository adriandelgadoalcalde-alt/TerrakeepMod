using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Guia
{
	/// <summary>
	/// El <b>medidor de preparacion</b>: una barra que dice de un vistazo como de listo estas
	/// para el objetivo actual.
	/// </summary>
	/// <remarks>
	/// <para>
	/// El color cambia con el propio valor y eso es la mitad del mensaje: rojo = esto te va a
	/// matar, ambar = te falta algo, verde = adelante. Es la idea que se pedia de los Souls -
	/// que el juego te diga POR QUE algo no te sale todavia - trasladada a un numero que se
	/// calcula de tu personaje real, no de una tabla de niveles.
	/// </para>
	/// <para>
	/// Se dibuja con <c>TextureAssets.MagicPixel</c>, el mismo material con el que el juego pinta
	/// sus propias barras, para no empaquetar ninguna textura.
	/// </para>
	/// </remarks>
	public class MedidorPreparacionTk : UIElement
	{
		private static readonly Color Borde = new Color(20, 26, 48);
		private static readonly Color Canal = new Color(28, 36, 66);

		private readonly Func<float> _fraccion;

		public int GrosorBorde = 2;

		public MedidorPreparacionTk(Func<float> fraccion, float alto = 18f)
		{
			_fraccion = fraccion;
			Width.Set(0f, 1f);
			Height.Set(alto, 0f);
		}

		/// <summary>Valor de ahora mismo, entre 0 y 1. Lo lee la autoprueba.</summary>
		public float Valor => MathHelper.Clamp(_fraccion != null ? _fraccion() : 0f, 0f, 1f);

		/// <summary>Color que le toca a un valor de preparacion. Publico para que la autoprueba
		/// pueda comprobar en el log que el color y el numero cuadran.</summary>
		public static Color ColorDe(float fraccion)
		{
			if (fraccion >= 0.999f) {
				return EstiloTk.Correcto;
			}
			if (fraccion >= 0.6f) {
				return EstiloTk.TextoAviso;
			}
			return EstiloTk.Peligro;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle marco = GetDimensions().ToRectangle();

			spriteBatch.Draw(pixel, marco, Borde);

			Rectangle interior = new Rectangle(
				marco.X + GrosorBorde, marco.Y + GrosorBorde,
				Math.Max(0, marco.Width - GrosorBorde * 2),
				Math.Max(0, marco.Height - GrosorBorde * 2));
			spriteBatch.Draw(pixel, interior, Canal);

			float fraccion = Valor;
			int relleno = (int)Math.Round(interior.Width * fraccion);
			if (relleno <= 0) {
				return;
			}

			Color color = ColorDe(fraccion);
			spriteBatch.Draw(pixel, new Rectangle(interior.X, interior.Y, relleno, interior.Height), color);
			spriteBatch.Draw(pixel,
				new Rectangle(interior.X, interior.Y, relleno, Math.Max(1, interior.Height / 2)),
				Color.White * 0.12f);
		}
	}
}
