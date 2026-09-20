using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.UI;

namespace TerrakeepMod.UI.Guia
{
	/// <summary>
	/// <b>TM4 del catálogo de rediseño visual ("Guía con checklist de sprites"):</b> tarjeta con el
	/// sprite real del jefe a la izquierda (72px) y el título del paso a la derecha, envolviendo a
	/// su ancho real - el mismo <see cref="ParrafoTk"/> de siempre, solo que con menos ancho
	/// disponible por el icono.
	/// </summary>
	/// <remarks>
	/// El alto de la tarjeta es <c>max(72, alto real del título envuelto)</c>: con un título largo
	/// en un idioma que ocupe más (o una resolución estrecha que envuelva a más líneas), la tarjeta
	/// CRECE para que nada quede cortado - mismo criterio de "la caja crece, el texto nunca se
	/// recorta" que ya usa <see cref="ParrafoTk"/> y <see cref="FilaRequisitoTk"/>.
	/// </remarks>
	public class TarjetaObjetivoTk : UIElement
	{
		public const float LadoIcono = 72f;
		private const float Separacion = 10f;

		private readonly Func<Texture2D> _icono;
		private readonly ParrafoTk _titulo;

		public TarjetaObjetivoTk(Func<string> titulo, Func<Texture2D> icono)
		{
			_icono = icono;
			Width.Set(0f, 1f);
			Height.Set(LadoIcono, 0f);

			_titulo = new ParrafoTk(titulo, 1.0f);
			_titulo.Left.Set(LadoIcono + Separacion, 0f);
			_titulo.Width.Set(-(LadoIcono + Separacion), 1f);
			Append(_titulo);
		}

		/// <summary>Color del título - mismo patrón que <see cref="ParrafoTk.ColorTexto"/>.</summary>
		public Color ColorTitulo {
			set { _titulo.ColorTexto = value; }
		}

		/// <summary>El párrafo del título montado, para que la autoprueba pueda seguir leyendo su
		/// texto envuelto real igual que hace con cualquier otro <see cref="ParrafoTk"/> del área.</summary>
		public ParrafoTk Titulo => _titulo;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			float alto = Math.Max(LadoIcono, _titulo.GetDimensions().Height);
			if (Math.Abs(Height.Pixels - alto) >= 0.5f) {
				Height.Set(alto, 0f);
				if (Parent != null) {
					Parent.Recalculate();
				}
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			Texture2D textura = _icono != null ? _icono() : null;
			if (textura == null) {
				return;
			}

			CalculatedStyle dim = GetDimensions();
			Rectangle destino = new Rectangle((int)dim.X, (int)dim.Y, (int)LadoIcono, (int)LadoIcono);
			spriteBatch.Draw(textura, destino, Color.White);
		}
	}
}
