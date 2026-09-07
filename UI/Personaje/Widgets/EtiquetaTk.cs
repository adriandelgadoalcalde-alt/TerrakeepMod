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

		/// <summary>
		/// Recorta un texto de UNA sola linea a lo que quepa en <paramref name="anchoMaximo"/>
		/// pixeles, midiendolo con la fuente REAL con la que se va a dibujar, y añade "..." si hizo
		/// falta cortar (nunca con "…": la fuente del juego no trae ese caracter, visto ya en
		/// <c>FilaCarpetaBuffTk</c>/<c>FilaCarpetaTk</c>, de donde sale este mismo algoritmo).
		/// <para />
		/// <b>Ultimo recurso, no la solucion por defecto.</b> Pedido explicito del usuario: un
		/// nombre mostrado como "Mana Regenerat..." no es aceptable aunque tecnicamente quepa en su
		/// caja - el contenido tiene que leerse ENTERO, y es el layout el que se adapta (mas ancho,
		/// mas alto, salto de linea - ver <see cref="PartirEnLineas"/>), no el texto el que se
		/// sacrifica. Usar esto solo cuando de verdad no hay una forma razonable de envolver o
		/// agrandar la caja (una sola linea de altura fija e infranqueable, como una fila del arbol
		/// de carpetas), y dejar dicho por que en el sitio que lo use.
		/// </para>
		/// </summary>
		public static string Recortar(string texto, float anchoMaximo, float escala)
		{
			if (string.IsNullOrEmpty(texto) || anchoMaximo <= 0f) {
				return texto ?? "";
			}

			DynamicSpriteFont fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			if (fuente.MeasureString(texto).X * escala <= anchoMaximo) {
				return texto;
			}

			int n = texto.Length;
			while (n > 1 && fuente.MeasureString(texto.Substring(0, n) + "...").X * escala > anchoMaximo) {
				n--;
			}
			return texto.Substring(0, n) + "...";
		}

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
