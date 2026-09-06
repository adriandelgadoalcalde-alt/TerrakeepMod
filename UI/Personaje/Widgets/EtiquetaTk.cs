using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Texto que se vuelve a preguntar en cada fotograma.
	/// <para />
	/// Existe porque casi todo lo que enseña este panel puede cambiar sin que el panel se entere:
	/// los buffs caducan solos (<c>buffTime[i]--</c> cada tick), la vida sube y baja, el dinero
	/// cambia al recoger monedas... Con un <c>UIText</c> normal habria que acordarse de llamar a
	/// <c>SetText</c> desde algun sitio; con esto, es imposible que se quede desfasado.
	/// </summary>
	public class EtiquetaTk : UIElement
	{
		private readonly Func<string> _texto;
		private readonly float _escala;

		/// <summary>Color del texto. Se puede cambiar en cualquier momento.</summary>
		public Color ColorTexto = Color.White;

		/// <summary>Si es true el texto se centra horizontalmente dentro del elemento.</summary>
		public bool Centrado;

		public EtiquetaTk(Func<string> texto, float escala = 0.85f, float ancho = 300f, float alto = 24f)
		{
			_texto = texto;
			_escala = escala;
			Width.Set(ancho, 0f);
			Height.Set(alto, 0f);
		}

		/// <summary>Texto que se esta enseñando ahora mismo, ya resuelto. Lo lee la autoprueba de
		/// idiomas para recoger TODO el texto visible de una pestaña sin tener que exponer cada
		/// etiqueta una a una.</summary>
		public string TextoActual => _texto != null ? (_texto() ?? "") : "";

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			string cadena = _texto();
			if (string.IsNullOrEmpty(cadena)) {
				return;
			}

			CalculatedStyle dim = GetDimensions();
			float x = dim.X;

			if (Centrado) {
				Vector2 tamano = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(cadena) * _escala;
				x = dim.X + (dim.Width - tamano.X) / 2f;
			}

			Utils.DrawBorderString(spriteBatch, cadena, new Vector2(x, dim.Y), ColorTexto, _escala);
		}
	}
}
