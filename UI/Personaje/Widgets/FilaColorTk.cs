using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Fila para editar un color del personaje: etiqueta, muestra del color y tres deslizadores
	/// (rojo, verde y azul).
	/// <para />
	/// No guarda el color: lo lee y lo escribe en el campo real del jugador en cada fotograma,
	/// asi que si el color cambia por otra via (otro panel, el propio juego) la fila lo enseña
	/// sin mas. Los cambios se ven al instante sobre el personaje porque el juego ya lo dibuja
	/// leyendo esos mismos campos.
	/// </summary>
	public class FilaColorTk : UIElement
	{
		private readonly Func<string> _etiqueta;
		private readonly Func<Color> _leer;
		private readonly Action<Color> _escribir;

		private readonly DeslizadorTk _rojo;
		private readonly DeslizadorTk _verde;
		private readonly DeslizadorTk _azul;

		/// <summary>El rotulo se pide con un <c>Func&lt;string&gt;</c>: guardado ya resuelto se
		/// quedaria congelado en el idioma que hubiera al construir la fila.</summary>
		public FilaColorTk(Func<string> etiqueta, Func<Color> leer, Action<Color> escribir)
		{
			_etiqueta = etiqueta;
			_leer = leer;
			_escribir = escribir;

			Width.Set(640f, 0f);
			Height.Set(26f, 0f);

			_rojo = CrearDeslizador(180f, new Color(220, 90, 90), valor => {
				Color color = _leer();
				_escribir(new Color(ACanal(valor), color.G, color.B));
			});
			_verde = CrearDeslizador(300f, new Color(90, 220, 110), valor => {
				Color color = _leer();
				_escribir(new Color(color.R, ACanal(valor), color.B));
			});
			_azul = CrearDeslizador(420f, new Color(100, 130, 240), valor => {
				Color color = _leer();
				_escribir(new Color(color.R, color.G, ACanal(valor)));
			});
		}

		/// <summary>Rotulo que se esta enseñando ahora mismo, ya traducido. Lo lee la autoprueba de
		/// idiomas para recoger todo el texto visible de la pestaña.</summary>
		public string EtiquetaActual => _etiqueta != null ? (_etiqueta() ?? "") : "";

		private DeslizadorTk CrearDeslizador(float izquierda, Color relleno, Action<float> alCambiar)
		{
			DeslizadorTk deslizador = new DeslizadorTk();
			deslizador.Width.Set(110f, 0f);
			deslizador.Height.Set(20f, 0f);
			deslizador.Left.Set(izquierda, 0f);
			deslizador.Top.Set(3f, 0f);
			deslizador.FilledColor = relleno;
			deslizador.AlCambiar += alCambiar;
			Append(deslizador);
			return deslizador;
		}

		private static int ACanal(float proporcion)
		{
			int valor = (int)(proporcion * 255f + 0.5f);
			if (valor < 0) {
				return 0;
			}
			return valor > 255 ? 255 : valor;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Se sincroniza DESPUES de base.Update, asi que un arrastre en curso ya ha escrito su
			// valor en el jugador y lo que se lee aqui es el valor definitivo de este fotograma.
			Color color = _leer();
			_rojo.FillPercent = color.R / 255f;
			_verde.FillPercent = color.G / 255f;
			_azul.FillPercent = color.B / 255f;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dim = GetDimensions();
			Color color = _leer();

			Utils.DrawBorderString(spriteBatch, EtiquetaActual,
				new Vector2(dim.X, dim.Y + 4f), EstiloTk.TextoSuave, 0.78f);

			// Muestra del color, con un borde negro para que se vea aunque el color sea claro.
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle borde = new Rectangle((int)(dim.X + 124f), (int)(dim.Y + 3f), 44, 20);
			spriteBatch.Draw(pixel, borde, Color.Black);
			spriteBatch.Draw(pixel,
				new Rectangle(borde.X + 2, borde.Y + 2, borde.Width - 4, borde.Height - 4), color);

			Utils.DrawBorderString(spriteBatch,
				color.R + "," + color.G + "," + color.B,
				new Vector2(dim.X + 536f, dim.Y + 4f), Color.White, 0.7f);
		}
	}
}
