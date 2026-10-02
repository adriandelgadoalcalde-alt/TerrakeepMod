using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Marca PERMANENTE de la siguiente parada de la Guia v2 encima del mapa del juego: a pantalla
	/// completa, en el minimapa de la esquina y en el mapa superpuesto (punto 3 del encargo del
	/// usuario, 02-oct-2026).
	/// </summary>
	/// <remarks>
	/// <para>
	/// A diferencia de <c>CapaMapaExploracion</c> (solo pantalla completa, con muchos rombos), esta
	/// capa se pinta en los TRES mapas porque es una sola marca y su razon de ser es estar siempre a
	/// la vista. Se dibuja a mano (no con <c>MapOverlayDrawContext.Draw</c>) por una razon real: ese
	/// metodo DESCARTA el icono si cae fuera del recorte (<c>DrawResult.Culled</c>, codigo
	/// decompilado de tModLoader 1.4.4.9), y en el minimapa la siguiente parada casi siempre esta
	/// fuera de lo que se ve. Aqui, si el objetivo cae fuera, se pinta una FLECHA en el borde que
	/// apunta hacia el, que es justo lo que pidio el usuario ("que apunte al siguiente lugar").
	/// </para>
	/// <para>
	/// La transformacion mapa→pantalla es la del propio contexto: <c>(tile - MapPosition) *
	/// MapScale + MapOffset</c>, con <c>ClippingRectangle</c> = el marco del minimapa (null a
	/// pantalla completa y en el superpuesto, que usan la pantalla entera).
	/// </para>
	/// </remarks>
	public class CapaMarcaGuia : ModMapLayer
	{
		public static readonly Color ColorMarca = new Color(255, 186, 40);
		public static readonly Color ColorConsulta = new Color(90, 220, 255);

		/// <summary>Evidencia para la autoprueba: posicion en pantalla de la ultima marca dibujada,
		/// en que mapa y si salio como flecha de borde.</summary>
		public static Vector2 UltimaPosicionPantalla;
		public static string UltimoMapa = "";
		public static bool UltimaFueFlecha;
		public static int FotogramasDibujados;
		public static int FotogramasMinimapa;

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			if (!UbicacionGuia.Visible || Main.gameMenu) {
				return;
			}
			bool minimapa = context.ClippingRectangle.HasValue;
			string mapa = minimapa ? "minimapa" : (Main.mapFullscreen ? "pantalla completa" : "superpuesto");

			if (UbicacionGuia.Consultada != null && !minimapa) {
				DibujarObjetivo(ref context, ref text, UbicacionGuia.Consultada, ColorConsulta, false, mapa);
			}
			if (UbicacionGuia.Siguiente != null) {
				DibujarObjetivo(ref context, ref text, UbicacionGuia.Siguiente, ColorMarca, true, mapa);
				FotogramasDibujados++;
				if (minimapa) {
					FotogramasMinimapa++;
				}
			}
		}

		private static void DibujarObjetivo(ref MapOverlayDrawContext context, ref string text,
			UbicacionGuia.Objetivo o, Color color, bool esSiguiente, string mapa)
		{
			GraphicsDevice gd = Main.spriteBatch.GraphicsDevice;
			Rectangle marco = context.ClippingRectangle ?? new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
			Vector2 pos = (o.Tile - context.MapPosition) * context.MapScale + context.MapOffset;
			float escala = Math.Max(0.75f, context.DrawScale);
			bool minimapa = context.ClippingRectangle.HasValue;
			float pulso = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f);

			Rectangle interior = marco;
			interior.Inflate(-12, -12);
			bool dentro = interior.Contains(pos.ToPoint());

			Rectangle zonaRaton;
			if (dentro) {
				// Anillo de zona (solo donde cabe: a pantalla completa y en el superpuesto).
				if (o.RadioTiles > 0f && !minimapa) {
					Texture2D anillo = TexturasMarca.Anillo(gd);
					float radioPx = o.RadioTiles * context.MapScale;
					if (radioPx > 14f) {
						float esc = radioPx * 2f / anillo.Width;
						Color c = color * (o.Aproximada ? 0.45f : 0.6f);
						Main.spriteBatch.Draw(anillo, pos, null, c, 0f, new Vector2(anillo.Width / 2f, anillo.Height / 2f), esc, SpriteEffects.None, 0f);
					}
				}

				Texture2D diana = TexturasMarca.Diana(gd);
				// En el minimapa a 0,9 la diana medía ~14 px; se sube para que se vea de un vistazo.
				float e = escala * pulso * (minimapa ? 1.1f : 1.15f);
				Main.spriteBatch.Draw(diana, pos, null, color, 0f, new Vector2(diana.Width / 2f, diana.Height / 2f), e, SpriteEffects.None, 0f);
				float lado = diana.Width * e;
				zonaRaton = new Rectangle((int)(pos.X - lado / 2f), (int)(pos.Y - lado / 2f), (int)lado, (int)lado);

				// Icono del jefe/vecino al lado (no en el minimapa: no cabe).
				if (!minimapa && o.Npc != 0) {
					Texture2D cabeza = CabezaNpc(o.Npc);
					if (cabeza != null) {
						float ladoCabeza = 26f * escala;
						float ec = Math.Min(ladoCabeza / cabeza.Width, ladoCabeza / cabeza.Height);
						Main.spriteBatch.Draw(cabeza, pos + new Vector2(lado / 2f + 4f, -lado / 2f - 4f), null, Color.White, 0f,
							new Vector2(0f, cabeza.Height / 2f), ec, SpriteEffects.None, 0f);
					}
				}
			}
			else {
				// Fuera de lo que se ve: flecha en el borde apuntando hacia el objetivo.
				Vector2 centro = new Vector2(marco.Center.X, marco.Center.Y);
				Vector2 dir = pos - centro;
				if (dir.LengthSquared() < 1f) {
					dir = new Vector2(1f, 0f);
				}
				float tx = dir.X != 0f ? (interior.Width / 2f) / Math.Abs(dir.X) : float.MaxValue;
				float ty = dir.Y != 0f ? (interior.Height / 2f) / Math.Abs(dir.Y) : float.MaxValue;
				float t = Math.Min(tx, ty);
				Vector2 borde = centro + dir * t;
				Texture2D flecha = TexturasMarca.Flecha(gd);
				float rot = (float)Math.Atan2(dir.Y, dir.X);
				// A 0,85 la flecha del minimapa salía de 8x6 px de color en la captura real (F3b):
				// demasiado pequeña para algo que tiene que verse siempre.
				float e = escala * pulso * (minimapa ? 1.3f : 1.2f);
				Main.spriteBatch.Draw(flecha, borde, null, color, rot, new Vector2(flecha.Width / 2f, flecha.Height / 2f), e, SpriteEffects.None, 0f);
				float lado = flecha.Width * e;
				zonaRaton = new Rectangle((int)(borde.X - lado / 2f), (int)(borde.Y - lado / 2f), (int)lado, (int)lado);
				pos = borde;
			}

			if (esSiguiente) {
				UltimaPosicionPantalla = pos;
				UltimoMapa = mapa;
				UltimaFueFlecha = !dentro;
			}

			if (zonaRaton.Contains(Main.MouseScreen.ToPoint())) {
				text = TextoTooltip(o, esSiguiente);
			}
		}

		public static string TextoTooltip(UbicacionGuia.Objetivo o, bool esSiguiente)
		{
			string cabecera = esSiguiente
				? Idiomas.Texto("GuiaV2.Mapa.TooltipSiguiente", o.Titulo)
				: Idiomas.Texto("GuiaV2.Mapa.TooltipConsulta", o.Titulo);
			string precision = o.Aproximada
				? Idiomas.Texto("GuiaV2.Mapa.Aproximada", o.Lugar)
				: (o.Origen == "jefe" || o.Origen == "npc" ? Idiomas.Texto("GuiaV2.Mapa.EnVivo", o.Lugar)
					: (o.Origen == "firma" ? Idiomas.Texto("GuiaV2.Mapa.Zona", o.Lugar) : Idiomas.Texto("GuiaV2.Mapa.Exacta", o.Lugar)));
			string dir = UbicacionGuia.DireccionDesdeJugador(o);
			return cabecera + "\n" + precision + (dir.Length > 0 ? "\n" + dir : "");
		}

		public static Texture2D CabezaNpc(int tipo)
		{
			Texture2D jefe = IconoJefe.Resolver(tipo);
			if (jefe != null) {
				return jefe;
			}
			if (tipo > 0) {
				int cabeza = NPC.TypeToDefaultHeadIndex(tipo);
				if (cabeza > 0 && cabeza < TextureAssets.NpcHead.Length) {
					return TextureAssets.NpcHead[cabeza].Value;
				}
			}
			return null;
		}
	}

	/// <summary>Texturas de la marca, generadas por codigo (mismo criterio que IconosExploracion:
	/// tres formas simples no merecen un .png, y asi se tiñen al dibujar).</summary>
	public static class TexturasMarca
	{
		private static Texture2D _diana, _anillo, _flecha;

		public static Texture2D Diana(GraphicsDevice gd)
		{
			if (_diana == null || _diana.IsDisposed) {
				_diana = Generar(gd, 21, (x, y, c) => {
					float d = (float)Math.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
					if (d > c + 0.5f) return Color.Transparent;
					if (d > c - 1.5f) return new Color(0, 0, 0, 230);
					if (d > c - 4f) return Color.White;
					if (d > c - 5.5f) return new Color(0, 0, 0, 200);
					if (d > 2.5f) return new Color(255, 255, 255, 120);
					if (d > 1.2f) return new Color(0, 0, 0, 220);
					return Color.White;
				});
			}
			return _diana;
		}

		public static Texture2D Anillo(GraphicsDevice gd)
		{
			if (_anillo == null || _anillo.IsDisposed) {
				_anillo = Generar(gd, 96, (x, y, c) => {
					float d = (float)Math.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
					if (d > c + 0.5f) return Color.Transparent;
					if (d > c - 2.5f) return Color.White;
					return new Color(255, 255, 255, 34);
				});
			}
			return _anillo;
		}

		public static Texture2D Flecha(GraphicsDevice gd)
		{
			if (_flecha == null || _flecha.IsDisposed) {
				// Triangulo que apunta a la derecha (rotacion 0 = este), con borde negro.
				_flecha = Generar(gd, 21, (x, y, c) => {
					if (x < 3 || x > 19) return Color.Transparent;
					float mitad = 8f * (19 - x) / 16f;
					float dy = Math.Abs(y - c);
					if (dy > mitad + 0.5f) return Color.Transparent;
					if (dy > mitad - 1.5f || x <= 4) return new Color(0, 0, 0, 230);
					return Color.White;
				});
			}
			return _flecha;
		}

		private static Texture2D Generar(GraphicsDevice gd, int lado, Func<int, int, int, Color> pixel)
		{
			Texture2D t = new Texture2D(gd, lado, lado);
			Color[] datos = new Color[lado * lado];
			int c = lado / 2;
			for (int y = 0; y < lado; y++) {
				for (int x = 0; x < lado; x++) {
					// SpriteBatch (BlendState.AlphaBlend) mezcla con alfa PREMULTIPLICADO: un blanco
					// con alfa 34 sin premultiplicar se pinta casi opaco. Visto en la captura real del
					// mapa (F3b): el interior del anillo de zona salia como un disco naranja macizo
					// (159,116,27) que tapaba el mapa justo donde esta el objetivo.
					Color p = pixel(x, y, c);
					datos[y * lado + x] = new Color(p.R * p.A / 255, p.G * p.A / 255, p.B * p.A / 255, p.A);
				}
			}
			t.SetData(datos);
			return t;
		}

		public static void Descargar()
		{
			if (_diana != null && !_diana.IsDisposed) _diana.Dispose();
			if (_anillo != null && !_anillo.IsDisposed) _anillo.Dispose();
			if (_flecha != null && !_flecha.IsDisposed) _flecha.Dispose();
			_diana = _anillo = _flecha = null;
		}
	}
}
