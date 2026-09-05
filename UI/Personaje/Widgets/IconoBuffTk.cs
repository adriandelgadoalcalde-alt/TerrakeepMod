using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Dibuja el icono real de un buff (<c>TextureAssets.Buff[tipo]</c>, la misma textura que usa
	/// la barra de buffs del juego).
	/// <para />
	/// Se comprueba siempre que el indice cabe y que el recurso esta cargado antes de dibujarlo:
	/// el array lo redimensiona tModLoader cuando hay mods con buffs propios, y un recurso puede
	/// no haberse pedido todavia. Si no hay icono, el elemento simplemente no dibuja nada, que es
	/// preferible a reventar el panel entero por un buff raro.
	/// </summary>
	public class IconoBuffTk : UIElement
	{
		private readonly System.Func<int> _tipo;

		public IconoBuffTk(System.Func<int> tipo, float lado = 32f)
		{
			_tipo = tipo;
			Width.Set(lado, 0f);
			Height.Set(lado, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			int tipo = _tipo();
			if (tipo <= 0 || TextureAssets.Buff == null || tipo >= TextureAssets.Buff.Length) {
				return;
			}

			Asset<Texture2D> recurso = TextureAssets.Buff[tipo];
			if (recurso == null || !recurso.IsLoaded) {
				return;
			}

			Texture2D textura = recurso.Value;
			CalculatedStyle dim = GetDimensions();
			float escala = dim.Width / textura.Width;
			if (escala > 1f) {
				escala = 1f;
			}

			spriteBatch.Draw(textura,
				new Vector2(dim.X + (dim.Width - textura.Width * escala) / 2f,
					dim.Y + (dim.Height - textura.Height * escala) / 2f),
				null, Color.White, 0f, Vector2.Zero, escala, SpriteEffects.None, 0f);
		}
	}
}
