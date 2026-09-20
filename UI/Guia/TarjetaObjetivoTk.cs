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
		/// <summary>Lado por defecto (la tarjeta grande del objetivo actual). La tira de "lo que
		/// viene" pide tarjetas mas pequeñas (ver <see cref="TarjetaObjetivoTk(Func{string}, Func{Texture2D}, float, float)"/>),
		/// asi que dejo de ser una constante fija.</summary>
		public const float LadoIcono = 72f;
		private const float Separacion = 10f;

		private readonly Func<Texture2D> _icono;
		private readonly ParrafoTk _titulo;
		private readonly float _ladoIcono;

		public TarjetaObjetivoTk(Func<string> titulo, Func<Texture2D> icono)
			: this(titulo, icono, LadoIcono, 1.0f)
		{
		}

		/// <summary>Variante con tamaño e escala propios - la usa la tira de "lo que viene" (TM4)
		/// para sus tres tarjetas pequeñas, sin duplicar la clase entera solo por el tamaño.</summary>
		public TarjetaObjetivoTk(Func<string> titulo, Func<Texture2D> icono, float ladoIcono, float escalaTitulo)
		{
			_icono = icono;
			_ladoIcono = ladoIcono;
			Width.Set(0f, 1f);
			Height.Set(_ladoIcono, 0f);

			_titulo = new ParrafoTk(titulo, escalaTitulo);
			_titulo.Left.Set(_ladoIcono + Separacion, 0f);
			_titulo.Width.Set(-(_ladoIcono + Separacion), 1f);
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

			float alto = Math.Max(_ladoIcono, _titulo.GetDimensions().Height);
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
			Rectangle destino = new Rectangle((int)dim.X, (int)dim.Y, (int)_ladoIcono, (int)_ladoIcono);
			spriteBatch.Draw(textura, destino, Color.White);
		}
	}
}
