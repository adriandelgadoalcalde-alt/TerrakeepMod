using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.GuiaV2;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.GuiaV2
{
	/// <summary>
	/// Ventana modal de la guia, encima de todo el area (ficha de objeto "cómo conseguirlo", ficha
	/// de zona): oscurece lo de detras, bloquea sus clics y se cierra con "Cerrar" o pulsando fuera.
	/// </summary>
	public class VentanaGuiaTk : UIElement
	{
		private const float AltoCabecera = 52f;
		private const float AltoBotones = 34f;

		private readonly UIPanel _caja;
		public readonly ListaTk Lista;
		public readonly FilaBotonesTk Botones;
		private readonly TextoRicoTk _titulo;
		private readonly TextoRicoTk _subtitulo;
		private readonly UIElement _cuerpo;

		public Func<int> IconoObjeto;
		public Func<int> IconoNpc;
		public event Action AlCerrar;

		public VentanaGuiaTk(Func<string> titulo, Func<string> subtitulo)
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			_caja = new UIPanel();
			_caja.Width.Set(0f, 0.94f);
			_caja.MaxWidth.Set(760f, 0f);
			_caja.Height.Set(0f, 0.94f);
			_caja.HAlign = 0.5f;
			_caja.VAlign = 0.5f;
			_caja.BackgroundColor = new Color(30, 40, 78) * 0.98f;
			_caja.BorderColor = EstiloTk.BordeSobre * 0.8f;
			_caja.SetPadding(12f);
			Append(_caja);

			_titulo = new TextoRicoTk(titulo, 1.0f);
			_titulo.SinEnlaces = true;
			_titulo.ColorNegrita = Color.White;
			_titulo.Left.Set(50f, 0f);
			_titulo.Width.Set(-50f, 1f);
			_caja.Append(_titulo);

			_subtitulo = new TextoRicoTk(subtitulo ?? (() => ""), 0.7f);
			_subtitulo.SinEnlaces = true;
			_subtitulo.ColorTexto = EstiloTk.TextoSuave;
			_subtitulo.Left.Set(50f, 0f);
			_subtitulo.Width.Set(-50f, 1f);
			_caja.Append(_subtitulo);

			_cuerpo = new UIElement();
			_cuerpo.Width.Set(0f, 1f);
			_cuerpo.Top.Set(AltoCabecera, 0f);
			_cuerpo.Height.Set(-(AltoCabecera + AltoBotones + 8f), 1f);
			_caja.Append(_cuerpo);
			Lista = ListaTk.ConScroll(_cuerpo);

			Botones = new FilaBotonesTk(AltoBotones, 0.8f);
			Botones.VAlign = 1f;
			_caja.Append(Botones);
		}

		public BotonTk BotonCerrar { get; private set; }

		/// <summary>Añade el boton "Cerrar" al final de la fila (se llama despues de los demas).</summary>
		public void AnadirCerrar()
		{
			BotonCerrar = Botones.Anadir(Idiomas.Texto("GuiaV2.Cerrar"), Cerrar);
		}

		public void Cerrar()
		{
			if (Parent != null) {
				Parent.RemoveChild(this);
			}
			if (AlCerrar != null) {
				AlCerrar();
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			// Titulo y subtitulo colocados segun su alto real (el titulo puede ocupar dos lineas).
			float altoTitulo = _titulo.Height.Pixels;
			float altoCabecera = Math.Max(AltoCabecera, altoTitulo + _subtitulo.Height.Pixels + 8f);
			if (Math.Abs(_subtitulo.Top.Pixels - altoTitulo) > 0.1f || Math.Abs(_cuerpo.Top.Pixels - altoCabecera) > 0.1f) {
				_subtitulo.Top.Set(altoTitulo, 0f);
				_cuerpo.Top.Set(altoCabecera, 0f);
				_cuerpo.Height.Set(-(altoCabecera + AltoBotones + 8f), 1f);
				_caja.Recalculate();
			}
			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			// Clic en lo oscuro de fuera de la caja: cerrar.
			if (evt.Target == this) {
				Cerrar();
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle d = GetDimensions();
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, d.ToRectangle(), new Color(5, 8, 20) * 0.72f);
		}

		protected override void DrawChildren(SpriteBatch spriteBatch)
		{
			base.DrawChildren(spriteBatch);
			CalculatedStyle c = _caja.GetInnerDimensions();
			Vector2 centro = new Vector2(c.X + 20f, c.Y + 20f);
			int obj = IconoObjeto != null ? IconoObjeto() : 0;
			if (obj > 0) {
				IconoObjetoTk.Dibujar(spriteBatch, obj, centro, 40f, Color.White);
				return;
			}
			int npc = IconoNpc != null ? IconoNpc() : 0;
			if (npc != 0) {
				Texture2D cabeza = CapaMarcaGuia.CabezaNpc(npc);
				if (cabeza != null) {
					float e = Math.Min(40f / cabeza.Width, 40f / cabeza.Height);
					spriteBatch.Draw(cabeza, centro, null, Color.White, 0f, new Vector2(cabeza.Width / 2f, cabeza.Height / 2f), e, SpriteEffects.None, 0f);
				}
			}
		}
	}
}
