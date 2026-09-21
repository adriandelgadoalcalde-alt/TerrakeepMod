using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Envoltorio de <c>Utils.DrawBorderString</c> que evita un bug real de la fuente vainilla
	/// (<see cref="FontAssets.MouseText"/>, bitmap font) reportado por el usuario el 21-sep-2026 con
	/// captura real: a un UIScale alto (su config.json real: <c>UIScale=1.4666667</c>, nunca probado
	/// antes en toda la sesion) combinado con la escala local de un boton/etiqueta del panel (entre
	/// 0.55 y 0.96 segun el sitio), ciertos pares de letras con "kerning"/bearing negativo EN LA
	/// FUENTE DEL JUEGO se solapan hasta fundirse en un solo glifo ilegible - caso confirmado con
	/// captura real: la pestaña "Vecindad" se veia como <b>"Veandad"</b> (la "i" se dibuja literalmente
	/// ENCIMA de la "c" en vez de al lado, ver <c>diag-vecindad-f03.png</c>, recorte a 800% en
	/// <c>bitacora.md</c>).
	/// <para/>
	/// <b>Causa raiz real, no supuesta</b> - confirmado leyendo el codigo real decompilado de
	/// <c>ReLogic.Graphics.DynamicSpriteFont.InternalDrawFast</c> (via <c>ilspycmd -t
	/// ReLogic.Graphics.DynamicSpriteFont Terraria.Libraries.ReLogic.ReLogic.dll</c>, ver
	/// <c>tModLoader-Decompiled\TerrariaVanilla\Terraria.Libraries.ReLogic.ReLogic.dll</c>): cada
	/// caracter avanza <c>kerning.X</c> (bearing IZQUIERDO, puede ser negativo - asi es como una
	/// fuente tipografica normal "acerca" pares de letras como "ci") ANTES de dibujarse, y <c>kerning.Y
	/// + kerning.Z</c> (ancho del glifo + bearing derecho) DESPUES - todo multiplicado por la MISMA
	/// escala final en pantalla. El bearing negativo es proporcional a esa escala: a
	/// escala pequeña el solape resultante es de una fraccion de pixel (invisible, por eso ninguna
	/// auditoria visual de esta sesion lo vio nunca - todas se hicieron a UIScale=1.0, con escala
	/// local siempre por debajo de 1.0 tambien), pero a partir de cierto tamaño de pixel final el
	/// solape crece lo bastante para que un trazo fino como el de la "i" quede tapado por el cuerpo de
	/// la "c" en vez de a su lado.
	/// <para/>
	/// El arreglo NO toca la fuente ni el motor (no se puede: <c>ReLogic.dll</c> es codigo de
	/// terceros, y parchear un ensamblado del propio tModLoader esta fuera de lo que un mod deberia
	/// tocar). En vez de eso: cuando la escala FINAL en pantalla (escala local del texto x
	/// <see cref="Main.UIScale"/>) supera <see cref="LimiteEscalaSegura"/>, cada caracter se dibuja
	/// SUELTO con su propio <c>Utils.DrawBorderString</c> de una sola letra, y se avanza con el ancho
	/// de ESE caracter aislado (<c>DynamicSpriteFont.MeasureString</c> de un solo caracter nunca
	/// aplica el bearing cruzado entre pares de letras) - el espaciado queda un pelin mas suelto que
	/// el kerning "de fabrica", pero NUNCA puede solaparse, sea cual sea la palabra o la escala:
	/// ataca la causa de verdad (bearing negativo acumulado) en vez de parchear solo la palabra
	/// "Vecindad".
	/// <para/>
	/// Por debajo del limite, el comportamiento es IDENTICO pixel a pixel al
	/// <c>Utils.DrawBorderString</c> de siempre (se le delega sin tocar nada) - cero riesgo de mover
	/// ni un pixel de las capturas ya verificadas en rondas anteriores de KeepQA, todas hechas a
	/// UIScale=1.0.
	/// </summary>
	public static class EscribirTk
	{
		/// <summary>
		/// Por encima de esta escala FINAL en pantalla (escala local del texto x
		/// <see cref="Main.UIScale"/>) se activa el dibujado caracter a caracter. 1.0 porque es el
		/// tope real que ya paso por toda la auditoria visual de esta sesion (todo el panel se probo
		/// exhaustivamente a <c>UIScale=1.0</c> con escalas locales que nunca llegan a 1.0 - ver
		/// <c>EstiloTk.EscalaPestana</c>/<c>EscalaBoton</c> = 0.8, <c>BotonTk.EscalaSobre</c> = 0.96)
		/// hasta el bug reportado esta noche con un UIScale real de usuario mayor que 1. Por debajo
		/// de este limite, cero riesgo de tocar nada ya verificado.
		/// </summary>
		private const float LimiteEscalaSegura = 1f;

		/// <summary>
		/// Mismo contrato que <c>Utils.DrawBorderString(spriteBatch, texto, posicion, color, escala)</c>
		/// - sustituye esa llamada tal cual en cualquier sitio del panel. Devuelve el tamaño real
		/// dibujado (ancho total, alto de linea), igual que la funcion vainilla.
		/// </summary>
		public static Vector2 Dibujar(SpriteBatch spriteBatch, string texto, Vector2 posicion, Color color, float escala = 1f)
		{
			if (string.IsNullOrEmpty(texto)) {
				return Vector2.Zero;
			}

			float escalaFinal = escala * Main.UIScale;

			// El solape reportado es horizontal, dentro de una misma linea de texto - separar por
			// caracter perderia el ajuste de salto de linea real que ya hace
			// Utils.DrawBorderString/ChatManager con los saltos "\n", asi que el texto multilinea
			// (ParrafoTk envuelto, FilaRequisitoTk) se deja siempre con el camino de siempre.
			if (escalaFinal <= LimiteEscalaSegura || texto.IndexOf('\n') >= 0) {
				return Utils.DrawBorderString(spriteBatch, texto, posicion, color, escala);
			}

			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			float x = posicion.X;
			float altoMaximo = 0f;
			for (int i = 0; i < texto.Length; i++) {
				string letra = texto[i].ToString();
				Utils.DrawBorderString(spriteBatch, letra, new Vector2(x, posicion.Y), color, escala);
				Vector2 medida = fuente.MeasureString(letra) * escala;
				x += medida.X;
				if (medida.Y > altoMaximo) {
					altoMaximo = medida.Y;
				}
			}

			return new Vector2(x - posicion.X, altoMaximo);
		}
	}
}
