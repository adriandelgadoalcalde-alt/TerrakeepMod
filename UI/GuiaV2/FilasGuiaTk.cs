using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.GuiaV2;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.GuiaV2
{
	/// <summary>
	/// Fila de un OBJETO de la guia (lo que necesitas para una parada, una pieza de la escalera de
	/// equipo...): sprite real, nombre oficial, si ya lo tienes (inventario, equipo y huchas, en
	/// vivo) y una nota opcional. Toda la fila es un boton: abre la ficha "cómo conseguirlo".
	/// </summary>
	public class ObjetoFilaTk : PilaTk
	{
		private const float LadoIcono = 30f;

		public readonly string Ref;
		public readonly int Cantidad;
		private readonly int _tipo;

		public ObjetoFilaTk(string referencia, int cantidad, string nota, string prefijo = null)
		{
			Ref = referencia;
			Cantidad = Math.Max(1, cantidad);
			_tipo = GuiaV2Sistema.TipoObjeto(referencia);
			Relleno = 4f;
			Separacion = 0f;
			Sangria = LadoIcono + 8f;

			TextoRicoTk nombre = new TextoRicoTk(() => (prefijo ?? "") + "**" + GuiaV2Sistema.NombreObjeto(Ref) + "**" +
				(Cantidad > 1 ? " ×" + Cantidad : "") + "   " + Estado(), 0.78f);
			nombre.SinEnlaces = true;
			nombre.ColorNegrita = Color.White;
			Append(nombre);

			if (!string.IsNullOrEmpty(nota)) {
				TextoRicoTk n = new TextoRicoTk(() => nota, 0.7f);
				n.SinEnlaces = true;
				n.ColorTexto = EstiloTk.TextoSuave;
				Append(n);
			}
		}

		public int Tienes => _tipo > 0 ? ProveedorEstadoGuiaV2Mod.CuantosPoseeDe(Main.LocalPlayer, _tipo) : 0;
		public bool LoTienes => Tienes >= Cantidad;

		private string Estado()
		{
			if (_tipo <= 0) {
				return Idiomas.Texto("GuiaV2.Objeto.NoExiste");
			}
			int t = Tienes;
			if (t >= Cantidad) {
				return Idiomas.Texto("GuiaV2.Objeto.LoTienes");
			}
			return Cantidad > 1 ? Idiomas.Texto("GuiaV2.Objeto.TienesDe", t, Cantidad) : Idiomas.Texto("GuiaV2.Objeto.TeFalta");
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			// El texto del estado se colorea por si lo tienes o no (el nombre va en negrita blanca).
			foreach (UIElement e in Elements) {
				TextoRicoTk t = e as TextoRicoTk;
				if (t != null && t.Escala > 0.75f) {
					t.ColorTexto = _tipo <= 0 ? EstiloTk.Neutro : (LoTienes ? EstiloTk.Correcto : EstiloTk.TextoAviso);
				}
			}
			Fondo = IsMouseHovering ? EstiloTk.BotonSobre * 0.55f : EstiloTk.FondoCaja * 0.6f;
			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
				BotonTk.PedirTooltip(Idiomas.Texto("GuiaV2.Enlace.Objeto", GuiaV2Sistema.NombreObjeto(Ref)));
			}
			if (Height.Pixels < LadoIcono + Relleno * 2f) {
				Height.Set(LadoIcono + Relleno * 2f, 0f);
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			SoundEngine.PlaySound(SoundID.MenuTick);
			if (NavegacionGuia.AbrirObjeto != null) {
				NavegacionGuia.AbrirObjeto(Ref, Cantidad);
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			CalculatedStyle d = GetDimensions();
			if (_tipo > 0) {
				IconoObjetoTk.Dibujar(spriteBatch, _tipo, new Vector2(d.X + Relleno + LadoIcono / 2f, d.Y + d.Height / 2f), LadoIcono, Color.White);
			}
		}
	}

	/// <summary>Una tarea de "Haz esto, en este orden": casilla + texto con marcado. La casilla
	/// se marca sola si la guia lo comprueba en la partida; si no, la marca el jugador (y se
	/// guarda en el personaje).</summary>
	public class TareaFilaTk : PilaTk
	{
		private const float LadoCasilla = 18f;
		private readonly ResultadoTarea _resultado;
		private readonly int _numero;

		public string TareaId => _resultado.Tarea.Id;

		public TareaFilaTk(ResultadoTarea resultado, int numero)
		{
			_resultado = resultado;
			_numero = numero;
			Relleno = 4f;
			Separacion = 1f;
			Sangria = LadoCasilla + 10f;

			TextoRicoTk texto = new TextoRicoTk(() => "**" + _numero + ".** " + _resultado.Tarea.Texto +
				(_resultado.Tarea.Opcional ? " " + Idiomas.Texto("GuiaV2.Tarea.Opcional") : ""), 0.78f);
			Append(texto);

			TextoRicoTk estado = new TextoRicoTk(EstadoTexto, 0.66f);
			estado.ColorTexto = EstiloTk.TextoSuave;
			Append(estado);
		}

		/// <summary>Estado real leido de la ultima evaluacion (no de la de cuando se monto la fila).</summary>
		private ResultadoTarea Actual {
			get {
				ResumenGuiaV2 r = GuiaV2Sistema.Resumen;
				if (r != null) {
					foreach (ResultadoParada p in r.Paradas) {
						foreach (ResultadoTarea t in p.Tareas) {
							if (t.Tarea.Id == _resultado.Tarea.Id) {
								return t;
							}
						}
					}
				}
				return _resultado;
			}
		}

		public bool Hecha => Actual.Hecha;

		private string EstadoTexto()
		{
			ResultadoTarea t = Actual;
			if (t.Condicion == null) {
				return Idiomas.Texto(t.MarcadaAMano ? "GuiaV2.Tarea.ManualHecha" : "GuiaV2.Tarea.Manual");
			}
			ResultadoCondicion c = t.Condicion;
			string linea = LineaCondicion(c);
			if (c.Estado == EstadoCondicion.Cumplida) {
				return Idiomas.Texto("GuiaV2.Tarea.Comprobada") + (linea.Length > 0 ? " · " + linea : "");
			}
			if (t.MarcadaAMano) {
				return Idiomas.Texto("GuiaV2.Tarea.MarcadaPeroNo") + (linea.Length > 0 ? " · " + linea : "");
			}
			if (c.Estado == EstadoCondicion.NoEvaluable) {
				return Idiomas.Texto("GuiaV2.Tarea.NoEvaluable");
			}
			return Idiomas.Texto("GuiaV2.Tarea.Pendiente") + (linea.Length > 0 ? " · " + linea : "");
		}

		/// <summary>La linea del motor ("tienes 2 de 5") de la primera hoja no cumplida.</summary>
		public static string LineaCondicion(ResultadoCondicion c)
		{
			if (c == null) {
				return "";
			}
			if (c.Hoja != null) {
				string clave = c.Hoja.TextoClave;
				if (string.IsNullOrEmpty(clave)) return "";
				string s = Idiomas.Texto(clave, c.Hoja.TextoArgs ?? new object[0]);
				return s == clave || s.StartsWith("Mods.", StringComparison.Ordinal) ? "" : s;
			}
			foreach (ResultadoCondicion h in c.Hijos) {
				if (h.Estado != EstadoCondicion.Cumplida) {
					string l = LineaCondicion(h);
					if (l.Length > 0) return l;
				}
			}
			return c.Hijos.Count > 0 ? LineaCondicion(c.Hijos[0]) : "";
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			bool sobreCasilla = SobreCasilla();
			if (sobreCasilla) {
				Main.LocalPlayer.mouseInterface = true;
				ResultadoTarea t = Actual;
				BotonTk.PedirTooltip(Idiomas.Texto(t.MarcadaAMano ? "GuiaV2.Tarea.Desmarcar" : "GuiaV2.Tarea.Marcar"));
			}
		}

		private bool SobreCasilla()
		{
			CalculatedStyle d = GetDimensions();
			Rectangle r = new Rectangle((int)(d.X + Relleno), (int)(d.Y + Relleno + 1f), (int)LadoCasilla + 4, (int)LadoCasilla + 4);
			return IsMouseHovering && r.Contains(Main.MouseScreen.ToPoint());
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (SobreCasilla()) {
				Alternar();
			}
		}

		/// <summary>Marca/desmarca a mano (publico para la autoprueba).</summary>
		public void Alternar()
		{
			SoundEngine.PlaySound(SoundID.MenuTick);
			GuiaV2Sistema.MarcarTarea(_resultado.Tarea.Id, !Actual.MarcadaAMano);
			GuiaV2Sistema.Reevaluar();
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			CalculatedStyle d = GetDimensions();
			ResultadoTarea t = Actual;
			Texture2D px = TextureAssets.MagicPixel.Value;
			Rectangle caja = new Rectangle((int)(d.X + Relleno + 1f), (int)(d.Y + Relleno + 2f), (int)LadoCasilla, (int)LadoCasilla);
			bool auto = t.Condicion != null && t.Condicion.Estado == EstadoCondicion.Cumplida;
			Color borde = SobreCasilla() ? Color.White : new Color(170, 185, 230);
			spriteBatch.Draw(px, caja, new Color(10, 14, 30) * 0.9f);
			if (t.Hecha) {
				Rectangle relleno = caja;
				relleno.Inflate(-3, -3);
				spriteBatch.Draw(px, relleno, auto ? EstiloTk.Correcto : new Color(120, 190, 255));
				// Marca de "hecho": dos trazos.
				Vector2 a = new Vector2(caja.X + 4, caja.Y + caja.Height / 2f);
				Vector2 b = new Vector2(caja.X + caja.Width * 0.42f, caja.Bottom - 4);
				Vector2 c = new Vector2(caja.Right - 3, caja.Y + 3);
				Linea(spriteBatch, a, b, new Color(10, 30, 15), 3f);
				Linea(spriteBatch, b, c, new Color(10, 30, 15), 3f);
			}
			spriteBatch.Draw(px, new Rectangle(caja.X, caja.Y, caja.Width, 2), borde);
			spriteBatch.Draw(px, new Rectangle(caja.X, caja.Bottom - 2, caja.Width, 2), borde);
			spriteBatch.Draw(px, new Rectangle(caja.X, caja.Y, 2, caja.Height), borde);
			spriteBatch.Draw(px, new Rectangle(caja.Right - 2, caja.Y, 2, caja.Height), borde);
		}

		public static void Linea(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float grosor)
		{
			Vector2 d = b - a;
			float largo = d.Length();
			float rot = (float)Math.Atan2(d.Y, d.X);
			sb.Draw(TextureAssets.MagicPixel.Value, a, new Rectangle(0, 0, 1, 1), color, rot, new Vector2(0f, 0.5f),
				new Vector2(largo, grosor), SpriteEffects.None, 0f);
		}
	}

	/// <summary>Una fila clicable de una lista (paradas de la ruta, articulos, resultados de busqueda):
	/// numero/estado a la izquierda, titulo con marcado y una linea pequeña debajo.</summary>
	public class FilaNavegableTk : PilaTk
	{
		private readonly Action _alPulsar;
		public Func<bool> Seleccionada;
		public Func<Color> ColorMarca;
		public Func<string> Marca;
		public string Clave;
		private const float AnchoMarca = 30f;

		public FilaNavegableTk(Func<string> titulo, Func<string> sub, Action alPulsar, float escala = 0.76f)
		{
			_alPulsar = alPulsar;
			Relleno = 5f;
			Separacion = 0f;
			Sangria = AnchoMarca + 4f;
			TextoRicoTk t = new TextoRicoTk(titulo, escala);
			t.SinEnlaces = true;
			t.ColorNegrita = Color.White;
			Append(t);
			if (sub != null) {
				TextoRicoTk s = new TextoRicoTk(sub, 0.64f);
				s.SinEnlaces = true;
				s.ColorTexto = EstiloTk.TextoSuave;
				s.ColorNegrita = EstiloTk.TextoSuave;
				Append(s);
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			bool sel = Seleccionada != null && Seleccionada();
			Fondo = sel ? EstiloTk.BotonActivo * 0.55f : (IsMouseHovering ? EstiloTk.BotonSobre * 0.5f : EstiloTk.FondoCaja * 0.55f);
			Borde = sel ? EstiloTk.BordeSobre * 0.8f : (Color?)null;
			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			Pulsar();
		}

		public void Pulsar()
		{
			SoundEngine.PlaySound(SoundID.MenuTick);
			if (_alPulsar != null) {
				_alPulsar();
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			string m = Marca != null ? Marca() : null;
			if (string.IsNullOrEmpty(m)) {
				return;
			}
			CalculatedStyle d = GetDimensions();
			float escala = 0.72f;
			Vector2 tam = FontAssets.MouseText.Value.MeasureString(m) * escala;
			if (tam.X > AnchoMarca) {
				escala *= AnchoMarca / tam.X;
				tam = FontAssets.MouseText.Value.MeasureString(m) * escala;
			}
			Vector2 pos = new Vector2(d.X + Relleno + (AnchoMarca - tam.X) / 2f, d.Y + Relleno + 1f);
			EscribirTk.Dibujar(spriteBatch, m, pos, ColorMarca != null ? ColorMarca() : EstiloTk.TextoSuave, escala);
		}
	}
}
