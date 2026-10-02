using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.GuiaV2
{
	/// <summary>
	/// <c>UIList</c> que se RE-ORDENA sola cuando cambia el alto de alguno de sus elementos.
	/// </summary>
	/// <remarks>
	/// Hace falta por el codigo real de <c>UIList</c> (tModLoader 1.4.4.9): solo coloca sus
	/// elementos uno debajo de otro en <c>RecalculateChildren</c>, y un elemento que cambia de alto
	/// (un texto que se vuelve a envolver al cambiar de resolucion o de idioma) llama a
	/// <c>Parent.Recalculate()</c>, cuyo Parent es la lista INTERIOR privada, que no recoloca nada.
	/// Sin esto, un parrafo que crece se pintaria encima del siguiente. Aqui se compara la suma de
	/// altos cada fotograma (barato) y se recalcula la lista entera solo si cambio.
	/// </remarks>
	public class ListaTk : UIList
	{
		private float _firma = -1f;

		public ListaTk()
		{
			ListPadding = 4f;
			ManualSortMethod = elementos => { };
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			float firma = 0f;
			foreach (UIElement e in _items) {
				firma += e.Height.Pixels * 1.0001f + e.Width.Pixels * 0.0001f;
			}
			firma += _items.Count * 7f;
			if (Math.Abs(firma - _firma) > 0.01f) {
				_firma = firma;
				Recalculate();
			}
		}

		/// <summary>Crea una lista con su barra de desplazamiento dentro de <paramref name="padre"/>.</summary>
		public static ListaTk ConScroll(UIElement padre, float margenDerecho = 22f)
		{
			ListaTk lista = new ListaTk();
			lista.Width.Set(-margenDerecho, 1f);
			lista.Height.Set(0f, 1f);
			padre.Append(lista);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Height.Set(-8f, 1f);
			barra.Top.Set(4f, 0f);
			barra.SetView(100f, 1000f);
			padre.Append(barra);
			lista.SetScrollbar(barra);
			lista.Barra = barra;
			return lista;
		}

		public UIScrollbar Barra { get; private set; }

		public void Hueco(float alto)
		{
			UIElement h = new UIElement();
			h.Width.Set(0f, 1f);
			h.Height.Set(alto, 0f);
			Add(h);
		}

		public void IrArriba()
		{
			if (Barra != null) {
				Barra.ViewPosition = 0f;
			}
		}
	}

	/// <summary>
	/// Caja que apila sus hijos en vertical y ajusta SU alto a lo que ocupan (con fondo y borde
	/// opcionales, como un UIPanel). Es el bloque de construccion de las tarjetas de la guia: una
	/// parada, un aviso, una etapa de la escalera de equipo...
	/// </summary>
	public class PilaTk : UIElement
	{
		private static Asset<Texture2D> _fondo;
		private static Asset<Texture2D> _borde;

		public Color? Fondo;
		public Color? Borde;
		public float Relleno = 8f;
		public float Separacion = 3f;
		/// <summary>Barra de color a la izquierda (avisos y "Listo para seguir cuando…").</summary>
		public Color? BarraIzquierda;
		/// <summary>Hueco a la izquierda de todos los hijos (para un icono o una casilla que la
		/// subclase dibuja ella misma).</summary>
		public float Sangria;

		public PilaTk()
		{
			Width.Set(0f, 1f);
			Height.Set(10f, 0f);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			Reordenar();
		}

		public void Reordenar()
		{
			float extraIzq = (BarraIzquierda.HasValue ? 6f : 0f) + Sangria;
			float y = Relleno;
			bool cambio = false;
			for (int i = 0; i < Elements.Count; i++) {
				UIElement e = Elements[i];
				float anchoHijo = -(Relleno * 2f + extraIzq);
				bool anchoMal = e.Width.Percent > 0f && Math.Abs(e.Width.Pixels - anchoHijo) > 0.1f;
				if (Math.Abs(e.Top.Pixels - y) > 0.1f || Math.Abs(e.Left.Pixels - (Relleno + extraIzq)) > 0.1f || anchoMal) {
					e.Top.Set(y, 0f);
					e.Left.Set(Relleno + extraIzq, 0f);
					if (e.Width.Percent > 0f) {
						e.Width.Set(anchoHijo, e.Width.Percent);
					}
					cambio = true;
				}
				y += e.Height.Pixels + (i < Elements.Count - 1 ? Separacion : 0f);
			}
			float alto = y + Relleno;
			if (Math.Abs(Height.Pixels - alto) > 0.1f) {
				Height.Set(alto, 0f);
				cambio = true;
			}
			if (cambio) {
				Recalculate();
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (!Fondo.HasValue && !Borde.HasValue && !BarraIzquierda.HasValue) {
				return;
			}
			if (_fondo == null) {
				_fondo = Main.Assets.Request<Texture2D>("Images/UI/PanelBackground");
				_borde = Main.Assets.Request<Texture2D>("Images/UI/PanelBorder");
			}
			CalculatedStyle d = GetDimensions();
			if (Fondo.HasValue) {
				Utils.DrawSplicedPanel(spriteBatch, _fondo.Value, (int)d.X, (int)d.Y, (int)d.Width, (int)d.Height, 10, 10, 10, 10, Fondo.Value);
			}
			if (Borde.HasValue) {
				Utils.DrawSplicedPanel(spriteBatch, _borde.Value, (int)d.X, (int)d.Y, (int)d.Width, (int)d.Height, 10, 10, 10, 10, Borde.Value);
			}
			if (BarraIzquierda.HasValue) {
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)d.X + 3, (int)d.Y + 4, 3, (int)d.Height - 8), BarraIzquierda.Value);
			}
		}
	}

	/// <summary>Elemento de alto fijo con un dibujo propio (barras de progreso, separadores).</summary>
	public class DibujoTk : UIElement
	{
		private readonly Action<SpriteBatch, CalculatedStyle> _dibujar;

		public DibujoTk(float alto, Action<SpriteBatch, CalculatedStyle> dibujar)
		{
			_dibujar = dibujar;
			Width.Set(0f, 1f);
			Height.Set(alto, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (_dibujar != null) {
				_dibujar(spriteBatch, GetDimensions());
			}
		}

		/// <summary>Barra de progreso al estilo del panel.</summary>
		public static DibujoTk Barra(Func<float> fraccion, Color color)
		{
			return new DibujoTk(10f, (sb, d) => {
				Texture2D px = TextureAssets.MagicPixel.Value;
				sb.Draw(px, new Rectangle((int)d.X, (int)d.Y + 2, (int)d.Width, 7), new Color(20, 26, 50) * 0.9f);
				float f = MathHelper.Clamp(fraccion != null ? fraccion() : 0f, 0f, 1f);
				sb.Draw(px, new Rectangle((int)d.X + 1, (int)d.Y + 3, (int)((d.Width - 2) * f), 5), color);
			});
		}
	}

	/// <summary>Fila de botones que reparte el ancho y baja la escala del texto lo justo para que
	/// cada rotulo quepa entero (regla del proyecto: ningun texto se recorta).</summary>
	public class FilaBotonesTk : UIElement
	{
		private readonly List<BotonTk> _botones = new List<BotonTk>();
		private readonly float _escalaBase;
		public float Separacion = 6f;
		/// <summary>Ancho maximo de cada boton (0 = repartir todo el ancho).</summary>
		public float AnchoMaximo;

		public FilaBotonesTk(float alto, float escalaBase = 0.78f)
		{
			_escalaBase = escalaBase;
			Width.Set(0f, 1f);
			Height.Set(alto, 0f);
		}

		public IReadOnlyList<BotonTk> Botones => _botones;

		public BotonTk Anadir(string texto, Action alPulsar)
		{
			BotonTk b = new BotonTk(texto, _escalaBase);
			b.Height.Set(Height.Pixels, 0f);
			if (alPulsar != null) {
				b.AlPulsar += alPulsar;
			}
			_botones.Add(b);
			Append(b);
			return b;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			Repartir();
		}

		public void Repartir()
		{
			if (_botones.Count == 0) {
				return;
			}
			float ancho = GetInnerDimensions().Width;
			if (ancho <= 0f) {
				return;
			}
			float cada = (ancho - Separacion * (_botones.Count - 1)) / _botones.Count;
			if (AnchoMaximo > 0f) {
				cada = Math.Min(cada, AnchoMaximo);
			}
			bool cambio = false;
			for (int i = 0; i < _botones.Count; i++) {
				BotonTk b = _botones[i];
				float left = i * (cada + Separacion);
				if (Math.Abs(b.Left.Pixels - left) > 0.1f || Math.Abs(b.Width.Pixels - cada) > 0.1f) {
					b.Left.Set(left, 0f);
					b.Width.Set(cada, 0f);
					cambio = true;
				}
				float medida = FontAssets.MouseText.Value.MeasureString(b.Texto ?? "").X;
				float hueco = cada - 14f - b.MargenIconoParaMedida;
				float escala = medida > 0f ? Math.Min(_escalaBase, hueco / medida) : _escalaBase;
				b.EscalaTexto = Math.Max(0.5f, escala);
			}
			if (cambio) {
				Recalculate();
			}
		}
	}
}
