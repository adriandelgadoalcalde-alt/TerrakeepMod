using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// TM5 del catálogo de rediseño visual ("Builds: filtros en dos filas, no en cuatro"): un
	/// anillo de progreso circular, para el resumen "Tienes X de Y" que antes era solo texto.
	/// </summary>
	/// <remarks>
	/// <b>Cómo se dibuja un anillo real sin sombreadores.</b> <c>SpriteBatch</c> no tiene ningún
	/// primitivo circular - se dibuja como el motor del propio juego dibuja cualquier otra cosa sin
	/// forma rectangular (líneas, barras curvas del minimapa): un cuadradito de
	/// <c>TextureAssets.MagicPixel</c> repetido a lo largo de la circunferencia, tantas veces como
	/// <see cref="Segmentos"/>, coloreado de <see cref="ColorLleno"/> o <see cref="ColorVacio"/>
	/// según si ese segmento cae dentro de la fracción completada - el mismo principio real (no un
	/// truco nuevo) que ya usa <see cref="FilaRequisitoTk.DibujarMarca"/> para su cuadradito de
	/// estado, aplicado en círculo en vez de en un rectángulo.
	/// </remarks>
	public class AnilloProgresoTk : UIElement
	{
		private const int Segmentos = 28;
		private const float GrosorSegmento = 4f;

		private readonly Func<float> _fraccion;
		private readonly Func<string> _textoCentro;
		private readonly float _radio;

		public Color ColorLleno = new Color(140, 235, 160);
		public Color ColorVacio = new Color(70, 78, 110);

		public AnilloProgresoTk(Func<float> fraccion, Func<string> textoCentro, float radio = 26f)
		{
			_fraccion = fraccion;
			_textoCentro = textoCentro;
			_radio = radio;
			Width.Set(_radio * 2f, 0f);
			Height.Set(_radio * 2f, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dim = GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);

			float fraccion = _fraccion != null ? MathHelper.Clamp(_fraccion(), 0f, 1f) : 0f;
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			for (int i = 0; i < Segmentos; i++) {
				float t = (float)i / Segmentos;
				// Empieza arriba (-90°) y avanza en sentido horario, igual que cualquier medidor de
				// progreso circular real (relojes, barras de carga) - nunca desde la derecha (0°),
				// que se leeria raro.
				float angulo = -MathHelper.PiOver2 + t * MathHelper.TwoPi;
				Vector2 punto = centro + new Vector2((float)Math.Cos(angulo), (float)Math.Sin(angulo)) * _radio;

				Color color = t < fraccion ? ColorLleno : ColorVacio;
				Rectangle destino = new Rectangle(
					(int)(punto.X - GrosorSegmento / 2f), (int)(punto.Y - GrosorSegmento / 2f),
					(int)GrosorSegmento, (int)GrosorSegmento);
				spriteBatch.Draw(pixel, destino, color);
			}

			string texto = _textoCentro != null ? (_textoCentro() ?? "") : "";
			if (texto.Length == 0) {
				return;
			}

			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			float escala = 0.68f;
			Vector2 tamano = fuente.MeasureString(texto) * escala;
			float anchoDisponible = _radio * 2f - GrosorSegmento * 3f;
			if (tamano.X > anchoDisponible && anchoDisponible > 4f) {
				escala *= anchoDisponible / tamano.X;
				tamano = fuente.MeasureString(texto) * escala;
			}
			Vector2 posicionTexto = centro - tamano / 2f;
			EscribirTk.Dibujar(spriteBatch, texto, posicionTexto, Color.White, escala);
		}
	}
}
