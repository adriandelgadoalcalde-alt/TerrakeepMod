using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
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

		/// <summary>
		/// Parte un texto en las lineas que quepan en <paramref name="ancho"/> pixeles, midiendolas
		/// con la fuente REAL con la que se van a dibujar.
		/// <para />
		/// Existe porque un salto de linea escrito a mano dentro de un texto solo vale para el
		/// idioma con el que se escribio: los mismos tres renglones del aviso del mini-mapa se salian del marco
		/// en ingles (visto en una captura real). Se llama en cada dibujado y no una vez porque el
		/// ancho depende de la resolucion y de la escala de interfaz del jugador.
		/// </summary>
		public static string PartirEnLineas(string texto, float ancho, float escala)
		{
			if (string.IsNullOrEmpty(texto) || ancho <= 0f) {
				return texto;
			}

			DynamicSpriteFont fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			if (fuente.MeasureString(texto).X * escala <= ancho && texto.IndexOf('\n') < 0) {
				return texto;
			}

			// Los saltos que ya trajera el texto se respetan: se parte cada renglon por separado.
			string[] renglones = texto.Split('\n');
			StringBuilder salida = new StringBuilder();

			for (int r = 0; r < renglones.Length; r++) {
				string[] palabras = renglones[r].Split(' ');
				StringBuilder linea = new StringBuilder();

				for (int i = 0; i < palabras.Length; i++) {
					string candidata = linea.Length == 0 ? palabras[i] : linea + " " + palabras[i];
					if (linea.Length > 0 && fuente.MeasureString(candidata).X * escala > ancho) {
						Anadir(salida, linea.ToString());
						linea.Clear();
						linea.Append(palabras[i]);
						continue;
					}
					linea.Clear();
					linea.Append(candidata);
				}

				if (linea.Length > 0) {
					Anadir(salida, linea.ToString());
				}
			}

			return salida.ToString();
		}

		private static void Anadir(StringBuilder salida, string linea)
		{
			if (salida.Length > 0) {
				salida.Append('\n');
			}
			salida.Append(linea);
		}

		// EtiquetaTk.Recortar (recorte de una linea con "...", "ultimo recurso") vivio aqui hasta
		// que la pasada de "todo el texto se lee entero, nunca con puntos suspensivos" (ver
		// bitacora.md) le quito su ULTIMO llamador real (FilaCarpetaBuffTk/FilaCarpetaTk, que
		// ahora envuelven el nombre a varias lineas con PartirEnLineas en vez de recortarlo).
		// Se borro en vez de dejarla como "utilidad generica sin usar": el propio criterio de esta
		// tarea es que el recorte por caracteres/pixeles nunca es la solucion por defecto, y una
		// funcion asi disponible sin ningun llamador es una invitacion a que la siguiente persona
		// "resuelva" un desbordamiento recortando en vez de adaptar el layout - el mismo patron que
		// esta tarea vino a corregir en primer lugar.

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
