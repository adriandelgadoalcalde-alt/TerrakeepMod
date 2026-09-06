using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace TerrakeepMod.UI.Investigacion
{
	/// <summary>
	/// Barra de progreso. Se vuelve a preguntar en cada fotograma, igual que
	/// <c>EtiquetaTk</c> de WS1, porque el progreso cambia mientras el panel esta abierto (el
	/// propio panel investiga, y el jugador puede estar sacrificando objetos en el menu del
	/// juego).
	/// <para />
	/// Se dibuja con <c>TextureAssets.MagicPixel</c>, el pixel blanco de un solo texel que usa el
	/// propio Terraria para todos sus rectangulos planos (barras de vida de jefe, mapa, sliders),
	/// en vez de meter una textura propia en el mod: es el mismo material con el que esta hecho
	/// el juego y no hay nada que empaquetar.
	/// </summary>
	public class BarraProgresoTk : UIElement
	{
		private readonly Func<int> _hechos;
		private readonly Func<int> _total;

		/// <summary>Grosor del borde, en pixeles.</summary>
		public int Borde = 2;

		public BarraProgresoTk(Func<int> hechos, Func<int> total, float ancho, float alto)
		{
			_hechos = hechos;
			_total = total;
			Width.Set(ancho, 0f);
			Height.Set(alto, 0f);
		}

		/// <summary>Fraccion completada, entre 0 y 1.</summary>
		public float Fraccion
		{
			get
			{
				int total = _total();
				if (total <= 0) {
					return 0f;
				}
				return MathHelper.Clamp(_hechos() / (float)total, 0f, 1f);
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle marco = GetDimensions().ToRectangle();

			// Borde + canal vacio.
			spriteBatch.Draw(pixel, marco, EstiloInvestigacion.BordeBarra);
			Rectangle interior = new Rectangle(
				marco.X + Borde, marco.Y + Borde,
				Math.Max(0, marco.Width - Borde * 2), Math.Max(0, marco.Height - Borde * 2));
			spriteBatch.Draw(pixel, interior, EstiloInvestigacion.CanalBarra);

			int hechos = _hechos();
			int total = _total();
			float fraccion = Fraccion;
			int relleno = (int)Math.Round(interior.Width * fraccion);

			if (relleno > 0) {
				Color color = EstiloInvestigacion.ColorDeEstado(hechos, total);
				spriteBatch.Draw(pixel, new Rectangle(interior.X, interior.Y, relleno, interior.Height), color);

				// Brillo sutil en la mitad de arriba del relleno: es lo que hace que la barra no
				// parezca un rectangulo plano pegado encima del panel. Mismo truco que usan las
				// barras del propio juego.
				spriteBatch.Draw(pixel,
					new Rectangle(interior.X, interior.Y, relleno, Math.Max(1, interior.Height / 2)),
					Color.White * 0.12f);
			}
		}
	}
}
