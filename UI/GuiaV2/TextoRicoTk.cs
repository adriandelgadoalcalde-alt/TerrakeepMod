using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.GuiaV2;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.GuiaV2
{
	/// <summary>
	/// La fuente del juego (FontAssets.MouseText) no tiene todos los glifos que usa el contenido de
	/// la guia (flechas, viñetas, rayas). Un caracter sin glifo se pinta como un hueco o un
	/// interrogante, asi que se cambia por su equivalente ASCII antes de medir y pintar. Se
	/// pregunta a la propia fuente (<c>IsCharacterSupported</c>, lo mismo que hace
	/// <c>LanguageManager</c> del juego), no a una lista fija.
	/// </summary>
	public static class GlifosTk
	{
		private static readonly System.Collections.Generic.Dictionary<char, string> Equivalentes =
			new System.Collections.Generic.Dictionary<char, string> {
				{ '→', "->" }, { '←', "<-" }, { '↑', "^" }, { '↓', "v" }, { '↗', "»" },
				{ '•', "·" }, { '–', "-" }, { '—', "-" }, { '…', "..." }, { '≤', "<=" },
				{ '≥', ">=" }, { '†', "+" }, { '✓', "v" }, { '✕', "x" }, { '×', "x" },
				{ '≈', "~" }, { '’', "'" }, { '‘', "'" }, { '“', "\"" }, { '”', "\"" },
				{ ' ', " " }, { ' ', " " }, { ' ', " " },
			};

		public static string Seguro(string texto)
		{
			if (string.IsNullOrEmpty(texto)) {
				return texto;
			}
			ReLogic.Graphics.DynamicSpriteFont fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			System.Text.StringBuilder sb = null;
			for (int i = 0; i < texto.Length; i++) {
				char c = texto[i];
				bool ok = c < 128 || fuente.IsCharacterSupported(c);
				if (ok && sb == null) {
					continue;
				}
				if (sb == null) {
					sb = new System.Text.StringBuilder(texto.Length + 8);
					sb.Append(texto, 0, i);
				}
				if (ok) {
					sb.Append(c);
					continue;
				}
				string eq;
				if (Equivalentes.TryGetValue(c, out eq) && (eq.Length != 1 || eq[0] < 128 || fuente.IsCharacterSupported(eq[0]))) {
					sb.Append(eq);
				}
				else {
					sb.Append('?');
				}
			}
			return sb != null ? sb.ToString() : texto;
		}
	}

	/// <summary>A donde lleva un enlace de la guia. Lo rellena <see cref="ContenidoGuiaV2"/>.</summary>
	public static class NavegacionGuia
	{
		public static Action<string, int> AbrirObjeto;
		public static Action<string> AbrirZona;
		public static Action<string> AbrirParada;
		public static Action<string> AbrirArticulo;

		/// <summary>Parada cuyo jefe es este NPC (para que el nombre de un jefe lleve a su parada).</summary>
		public static string ParadaDeJefe(string referencia)
		{
			if (GuiaV2Sistema.Doc == null || referencia == null) {
				return null;
			}
			foreach (Parada p in GuiaV2Sistema.Doc.Paradas) {
				if (p.Jefes.Contains(referencia)) {
					return p.Id;
				}
			}
			return null;
		}
	}

	/// <summary>
	/// Texto de la guia con su marcado en linea (<see cref="GuiaV2Texto"/>) pintado con la fuente
	/// del juego: negritas, sprite REAL de cada objeto y jefe delante de su nombre oficial, y
	/// enlaces clicables (objeto → ficha "cómo conseguirlo"; zona → ficha de zona y mapa; parada y
	/// articulo → navegan). Envuelve por palabras contra el ancho REAL y ajusta su propio alto, como
	/// <see cref="Guia.ParrafoTk"/>, asi que nunca se sale de su caja a ninguna resolucion.
	/// </summary>
	/// <remarks>
	/// El sprite y la primera palabra del nombre van "pegados": nunca queda un icono huerfano al
	/// final de una linea y su nombre al principio de la siguiente. Una palabra mas ancha que la
	/// caja entera (no pasa con el contenido real, pero se cubre) se parte por caracteres en vez de
	/// salirse.
	/// </remarks>
	public class TextoRicoTk : UIElement
	{
		public static readonly Color ColorEnlace = new Color(150, 205, 255);
		public static readonly Color ColorEnlaceSobre = new Color(255, 255, 255);

		private readonly Func<string> _texto;
		private readonly float _escala;

		public Color ColorTexto = Color.White;
		public Color ColorNegrita = new Color(255, 232, 170);
		/// <summary>Sin enlaces (titulos de botones, listas donde el clic ya es de la fila).</summary>
		public bool SinEnlaces;

		private sealed class Atomo
		{
			public string Texto;
			public int Objeto;
			public int Npc;
			public float Ancho;
			public float Espacio;
			public Color Color;
			public int Enlace = -1;
			public bool Salto;
			public bool PegadoAlSiguiente;
			public float X, Y;
		}

		private struct Enlace
		{
			public TipoSegmento Tipo;
			public string Valor;
		}

		private readonly List<Atomo> _atomos = new List<Atomo>();
		private readonly List<Enlace> _enlaces = new List<Enlace>();
		private string _ultimo;
		private float _ultimoAncho = -1f;
		private bool _ultimoEspanol;
		private float _ultimaEscalaUi = -1f;
		private int _enlaceSobre = -1;
		private int _lineas;

		public TextoRicoTk(Func<string> texto, float escala = 0.8f)
		{
			_texto = texto;
			_escala = escala;
			Width.Set(0f, 1f);
			Height.Set(0f, 0f);
		}

		public float Escala => _escala;
		public float AltoLinea => FontAssets.MouseText.Value.LineSpacing * _escala;
		public int Lineas => _lineas;
		public string TextoPlano => GuiaV2Sistema.PlanoLocal(_texto != null ? _texto() : "");

		/// <summary>Ancho del renglon mas largo ya maquetado (para la autoprueba de "nada se sale").</summary>
		public float AnchoMaximoUsado { get; private set; }

		/// <summary>Numero de enlaces (lo lee la autoprueba para pulsar uno de verdad).</summary>
		public int CuantosEnlaces => _enlaces.Count;

		/// <summary>Centro en pantalla del primer atomo del enlace indicado (autoprueba).</summary>
		public Vector2? CentroDeEnlace(int indice)
		{
			CalculatedStyle dim = GetInnerDimensions();
			foreach (Atomo a in _atomos) {
				if (a.Enlace == indice) {
					return new Vector2(dim.X + a.X + a.Ancho / 2f, dim.Y + a.Y + AltoLinea / 2f);
				}
			}
			return null;
		}

		public string ValorDeEnlace(int indice) => indice >= 0 && indice < _enlaces.Count ? _enlaces[indice].Valor : null;
		public TipoSegmento TipoDeEnlace(int indice) => indice >= 0 && indice < _enlaces.Count ? _enlaces[indice].Tipo : TipoSegmento.Texto;

		/// <summary>Ejecuta un enlace como si se hubiera pulsado (autoprueba y teclado).</summary>
		public void PulsarEnlace(int indice)
		{
			if (indice < 0 || indice >= _enlaces.Count) {
				return;
			}
			Enlace e = _enlaces[indice];
			switch (e.Tipo) {
				case TipoSegmento.Objeto: if (NavegacionGuia.AbrirObjeto != null) NavegacionGuia.AbrirObjeto(e.Valor, 1); break;
				case TipoSegmento.Zona: if (NavegacionGuia.AbrirZona != null) NavegacionGuia.AbrirZona(e.Valor); break;
				case TipoSegmento.Parada: if (NavegacionGuia.AbrirParada != null) NavegacionGuia.AbrirParada(e.Valor); break;
				case TipoSegmento.Articulo: if (NavegacionGuia.AbrirArticulo != null) NavegacionGuia.AbrirArticulo(e.Valor); break;
				case TipoSegmento.Npc: {
					string parada = NavegacionGuia.ParadaDeJefe(e.Valor);
					if (parada != null && NavegacionGuia.AbrirParada != null) NavegacionGuia.AbrirParada(parada);
					break;
				}
			}
		}

		// ---- maquetacion -------------------------------------------------------------------------

		private void Construir(string crudo)
		{
			_atomos.Clear();
			_enlaces.Clear();
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			float espacio = EscribirTk.Ancho(" ", _escala);
			float lado = AltoLinea * 0.86f;

			foreach (SegmentoTexto s in GuiaV2Texto.Analizar(crudo)) {
				switch (s.Tipo) {
					case TipoSegmento.Texto:
						Palabras(s.Valor, ColorTexto, -1, fuente, espacio);
						break;
					case TipoSegmento.Negrita:
						Palabras(s.Valor, ColorNegrita, -1, fuente, espacio);
						break;
					case TipoSegmento.Objeto: {
						int enlace = NuevoEnlace(s);
						int tipo = GuiaV2Sistema.TipoObjeto(s.Valor);
						if (tipo > 0) {
							_atomos.Add(new Atomo { Objeto = tipo, Ancho = lado, Espacio = espacio * 0.5f, Enlace = enlace, PegadoAlSiguiente = true });
						}
						Palabras(s.TextoPropio ?? GuiaV2Sistema.NombreObjeto(s.Valor), SinEnlaces ? ColorNegrita : ColorEnlace, enlace, fuente, espacio);
						break;
					}
					case TipoSegmento.Npc: {
						string parada = NavegacionGuia.ParadaDeJefe(s.Valor);
						int enlace = parada != null ? NuevoEnlace(s) : -1;
						int tipo = GuiaV2Sistema.TipoNpc(s.Valor);
						if (tipo != 0 && CapaMarcaGuia.CabezaNpc(tipo) != null) {
							_atomos.Add(new Atomo { Npc = tipo, Ancho = lado, Espacio = espacio * 0.5f, Enlace = enlace, PegadoAlSiguiente = true });
						}
						Palabras(s.TextoPropio ?? GuiaV2Sistema.NombreNpc(s.Valor),
							enlace >= 0 && !SinEnlaces ? ColorEnlace : ColorNegrita, enlace, fuente, espacio);
						break;
					}
					case TipoSegmento.Zona: {
						int enlace = NuevoEnlace(s);
						Palabras(s.TextoPropio ?? GuiaV2Sistema.NombreZona(s.Valor), SinEnlaces ? ColorNegrita : ColorEnlace, enlace, fuente, espacio);
						break;
					}
					case TipoSegmento.Parada: {
						int enlace = NuevoEnlace(s);
						Parada p = GuiaV2Sistema.ParadaPorId(s.Valor);
						Palabras(s.TextoPropio ?? (p != null ? GuiaV2Sistema.PlanoLocal(p.Titulo) : s.Valor),
							SinEnlaces ? ColorNegrita : ColorEnlace, enlace, fuente, espacio);
						break;
					}
					case TipoSegmento.Articulo: {
						int enlace = NuevoEnlace(s);
						Articulo a = GuiaV2Sistema.ArticuloPorId(s.Valor);
						Palabras(s.TextoPropio ?? (a != null ? GuiaV2Sistema.PlanoLocal(a.Titulo) : s.Valor),
							SinEnlaces ? ColorNegrita : ColorEnlace, enlace, fuente, espacio);
						break;
					}
				}
			}
			if (SinEnlaces) {
				foreach (Atomo a in _atomos) a.Enlace = -1;
				_enlaces.Clear();
			}
		}

		private int NuevoEnlace(SegmentoTexto s)
		{
			_enlaces.Add(new Enlace { Tipo = s.Tipo, Valor = s.Valor });
			return _enlaces.Count - 1;
		}

		private void Palabras(string texto, Color color, int enlace, DynamicSpriteFont fuente, float espacio)
		{
			if (string.IsNullOrEmpty(texto)) {
				return;
			}
			int i = 0;
			while (i < texto.Length) {
				char c = texto[i];
				if (c == '\n') {
					_atomos.Add(new Atomo { Salto = true });
					i++;
					continue;
				}
				if (c == ' ' || c == '\t') {
					// Espacio delante: se suma al atomo anterior.
					if (_atomos.Count > 0 && !_atomos[_atomos.Count - 1].Salto) {
						Atomo previo = _atomos[_atomos.Count - 1];
						previo.Espacio = espacio;
						previo.PegadoAlSiguiente = false;
					}
					i++;
					continue;
				}
				int fin = i;
				while (fin < texto.Length && texto[fin] != ' ' && texto[fin] != '\n' && texto[fin] != '\t') {
					fin++;
				}
				string palabra = GlifosTk.Seguro(texto.Substring(i, fin - i));
				Atomo a = new Atomo {
					Texto = palabra, Ancho = EscribirTk.Ancho(palabra, _escala), Color = color, Enlace = enlace,
				};
				// Si el atomo anterior no dejo espacio (p. ej. "{o:X}," o un icono), van pegados.
				if (_atomos.Count > 0) {
					Atomo previo = _atomos[_atomos.Count - 1];
					if (!previo.Salto && previo.Espacio <= 0f) {
						previo.PegadoAlSiguiente = true;
					}
				}
				_atomos.Add(a);
				i = fin;
			}
		}

		private void Maquetar(float ancho)
		{
			float alto = AltoLinea;
			float x = 0f, y = 0f, maximo = 0f;
			int lineas = _atomos.Count > 0 ? 1 : 0;
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;

			for (int i = 0; i < _atomos.Count; i++) {
				Atomo a = _atomos[i];
				if (a.Salto) {
					x = 0f;
					y += alto;
					lineas++;
					continue;
				}
				// Ancho del grupo pegado (icono + primera palabra, palabra + coma...).
				float grupo = a.Ancho;
				for (int j = i; j < _atomos.Count - 1 && _atomos[j].PegadoAlSiguiente && !_atomos[j + 1].Salto; j++) {
					grupo += _atomos[j].Espacio + _atomos[j + 1].Ancho;
				}
				bool empiezaGrupo = i == 0 || !_atomos[i - 1].PegadoAlSiguiente;
				if (x > 0f && empiezaGrupo && x + grupo > ancho) {
					x = 0f;
					y += alto;
					lineas++;
				}
				else if (x > 0f && !empiezaGrupo && x + a.Ancho > ancho) {
					x = 0f;
					y += alto;
					lineas++;
				}
				// Palabra suelta mas ancha que la caja: se parte por caracteres.
				if (a.Texto != null && a.Ancho > ancho && ancho > 20f) {
					string resto = a.Texto;
					while (resto.Length > 0) {
						int n = resto.Length;
						while (n > 1 && EscribirTk.Ancho(resto.Substring(0, n), _escala) > ancho - x) {
							n--;
						}
						string trozo = resto.Substring(0, n);
						resto = resto.Substring(n);
						if (resto.Length == 0) {
							a.Texto = trozo;
							a.Ancho = EscribirTk.Ancho(trozo, _escala);
							break;
						}
						_atomos.Insert(i, new Atomo { Texto = trozo, Ancho = EscribirTk.Ancho(trozo, _escala), Color = a.Color, Enlace = a.Enlace, X = x, Y = y });
						i++;
						maximo = Math.Max(maximo, x + _atomos[i - 1].Ancho);
						x = 0f;
						y += alto;
						lineas++;
					}
				}
				a.X = x;
				a.Y = y;
				x += a.Ancho;
				maximo = Math.Max(maximo, x);
				x += a.Espacio;
			}
			_lineas = lineas;
			AnchoMaximoUsado = maximo;

			float nuevo = lineas * alto;
			if (Math.Abs(Height.Pixels - nuevo) >= 0.5f) {
				Height.Set(nuevo, 0f);
				if (Parent != null) {
					Parent.Recalculate();
				}
			}
		}

		private void Reajustar()
		{
			string crudo = _texto != null ? (_texto() ?? "") : "";
			float ancho = GetInnerDimensions().Width;
			if (ancho <= 0f) {
				return;
			}
			bool espanol = Idiomas.EnEspanol;
			// La escala de interfaz entra en la clave: cambia como mide EscribirTk (letra a letra por
			// encima de escala 1 en pantalla).
			if (crudo == _ultimo && Math.Abs(ancho - _ultimoAncho) < 0.5f && espanol == _ultimoEspanol && Main.UIScale == _ultimaEscalaUi) {
				return;
			}
			_ultimaEscalaUi = Main.UIScale;
			_ultimo = crudo;
			_ultimoAncho = ancho;
			_ultimoEspanol = espanol;
			Construir(crudo);
			Maquetar(ancho);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			Reajustar();
			_enlaceSobre = -1;
			if (!IsMouseHovering || _enlaces.Count == 0) {
				return;
			}
			CalculatedStyle dim = GetInnerDimensions();
			Vector2 raton = Main.MouseScreen;
			float alto = AltoLinea;
			foreach (Atomo a in _atomos) {
				if (a.Enlace < 0) continue;
				if (raton.X >= dim.X + a.X && raton.X <= dim.X + a.X + a.Ancho + a.Espacio &&
					raton.Y >= dim.Y + a.Y && raton.Y <= dim.Y + a.Y + alto) {
					_enlaceSobre = a.Enlace;
					break;
				}
			}
			if (_enlaceSobre >= 0) {
				Main.LocalPlayer.mouseInterface = true;
				Enlace e = _enlaces[_enlaceSobre];
				switch (e.Tipo) {
					case TipoSegmento.Objeto: BotonTk.PedirTooltip(Idiomas.Texto("GuiaV2.Enlace.Objeto", GuiaV2Sistema.NombreObjeto(e.Valor))); break;
					case TipoSegmento.Zona: BotonTk.PedirTooltip(Idiomas.Texto("GuiaV2.Enlace.Zona", GuiaV2Sistema.NombreZona(e.Valor))); break;
					case TipoSegmento.Npc:
					case TipoSegmento.Parada: BotonTk.PedirTooltip(Idiomas.Texto("GuiaV2.Enlace.Parada")); break;
					case TipoSegmento.Articulo: BotonTk.PedirTooltip(Idiomas.Texto("GuiaV2.Enlace.Articulo")); break;
				}
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (_enlaceSobre >= 0) {
				Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
				PulsarEnlace(_enlaceSobre);
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			Reajustar();
			CalculatedStyle dim = GetInnerDimensions();
			float alto = AltoLinea;
			float lado = alto * 0.86f;
			foreach (Atomo a in _atomos) {
				if (a.Salto) continue;
				Vector2 pos = new Vector2(dim.X + a.X, dim.Y + a.Y);
				bool sobre = a.Enlace >= 0 && a.Enlace == _enlaceSobre;
				if (a.Objeto > 0) {
					IconoObjetoTk.Dibujar(spriteBatch, a.Objeto, pos + new Vector2(lado / 2f, alto / 2f - 2f), lado, Color.White);
					continue;
				}
				if (a.Npc != 0) {
					Texture2D cabeza = CapaMarcaGuia.CabezaNpc(a.Npc);
					if (cabeza != null) {
						float e = Math.Min(lado / cabeza.Width, lado / cabeza.Height);
						spriteBatch.Draw(cabeza, pos + new Vector2(lado / 2f, alto / 2f - 2f), null, Color.White, 0f,
							new Vector2(cabeza.Width / 2f, cabeza.Height / 2f), e, SpriteEffects.None, 0f);
					}
					continue;
				}
				if (string.IsNullOrEmpty(a.Texto)) continue;
				EscribirTk.Dibujar(spriteBatch, a.Texto, pos, sobre ? ColorEnlaceSobre : a.Color, _escala);
				if (sobre) {
					spriteBatch.Draw(TextureAssets.MagicPixel.Value,
						new Rectangle((int)pos.X, (int)(pos.Y + alto - 4f), (int)(a.Ancho + 0.5f), 1), ColorEnlaceSobre);
				}
			}
		}
	}
}
