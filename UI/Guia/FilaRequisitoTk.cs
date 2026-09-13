using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Guia
{
	/// <summary>
	/// Una linea de "lo que te falta": una marca de estado, el texto del requisito (ya con sus
	/// numeros reales) y, si es solo recomendado, una coletilla que lo dice.
	/// </summary>
	/// <remarks>
	/// El requisito se vuelve a EVALUAR en cada dibujado, no se guarda evaluado: mientras el
	/// panel esta abierto el jugador puede recoger monedas, equiparse otra pieza o ver llegar a
	/// un vecino, y la fila tiene que reflejarlo en el acto. Mismo criterio que
	/// <see cref="EtiquetaTk"/>.
	/// <para />
	/// La distincion entre obligatorio y recomendado se ve a simple vista y esta ahi a proposito:
	/// la guia no puede convertir "te vendria bien una arena" en "el juego no te deja", porque
	/// entonces deja de ser una brujula y pasa a ser una lista de tareas.
	/// </remarks>
	public class FilaRequisitoTk : UIElement
	{
		private const float AltoFila = 24f;
		private const float Sangria = 18f;

		private readonly RequisitoGuia _requisito;
		private readonly float _escala;

		private string _envuelto = "";
		private string _ultimoCrudo;
		private float _ultimoAncho;

		public FilaRequisitoTk(RequisitoGuia requisito, float escala = 0.78f)
		{
			_requisito = requisito;
			_escala = escala;
			Width.Set(0f, 1f);
			Height.Set(AltoFila, 0f);
		}

		/// <summary>Estado real del requisito ahora mismo. Lo lee la autoprueba.</summary>
		public ResultadoRequisito Estado()
		{
			return EvaluadorGuia.Evaluar(_requisito);
		}

		/// <summary>El texto que se esta viendo, ya envuelto. Lo lee la autoprueba para demostrar
		/// que no se sale del marco.</summary>
		public string TextoVisible => _envuelto;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			Reajustar();
		}

		private string Crudo(ResultadoRequisito estado)
		{
			string texto = estado.Linea ?? "";
			if (_requisito != null && _requisito.Recomendado) {
				texto += " " + Idiomas.Texto("Guia.Req.Recomendado");
			}
			return texto;
		}

		private void Reajustar()
		{
			float ancho = GetInnerDimensions().Width - Sangria;
			if (ancho <= 0f) {
				return;
			}

			string crudo = Crudo(Estado());
			if (crudo == _ultimoCrudo && Math.Abs(ancho - _ultimoAncho) < 0.5f) {
				return;
			}

			_ultimoCrudo = crudo;
			_ultimoAncho = ancho;
			_envuelto = EtiquetaTk.PartirEnLineas(crudo, ancho, _escala);

			int lineas = _envuelto.Length == 0 ? 1 : _envuelto.Split('\n').Length;
			float alto = Math.Max(AltoFila, lineas * FontAssets.MouseText.Value.LineSpacing * _escala + 4f);
			if (Math.Abs(Height.Pixels - alto) >= 0.5f) {
				Height.Set(alto, 0f);
				if (Parent != null) {
					Parent.Recalculate();
				}
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			Reajustar();

			ResultadoRequisito estado = Estado();
			CalculatedStyle dim = GetInnerDimensions();

			Color color;
			bool relleno;
			if (estado.NoEvaluable) {
				color = EstiloTk.TextoAviso;
				relleno = false;
			}
			else if (estado.Cumplido) {
				color = EstiloTk.Correcto;
				relleno = true;
			}
			else if (_requisito != null && _requisito.Recomendado) {
				color = EstiloTk.TextoSuave;
				relleno = false;
			}
			else {
				color = EstiloTk.Neutro;
				relleno = false;
			}

			// La marca es un cuadrito dibujado con TextureAssets.MagicPixel (el pixel blanco de un
			// texel con el que el propio Terraria pinta todos sus rectangulos planos) y NO un
			// caracter tipo "✓": la fuente del juego es un DynamicSpriteFont con un juego de
			// caracteres limitado, y meterle un glifo que no tenga es pedir un fallo que solo se
			// veria en pantalla. Ademas se lee mejor de un vistazo.
			DibujarMarca(spriteBatch, new Vector2(dim.X, dim.Y + 4f), color, relleno);
			Utils.DrawBorderString(spriteBatch, _envuelto, new Vector2(dim.X + Sangria, dim.Y), color, _escala);
		}

		/// <summary>Cuadrito de 10x10: relleno si esta cumplido, solo el contorno si no.</summary>
		private static void DibujarMarca(SpriteBatch spriteBatch, Vector2 posicion, Color color, bool relleno)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle caja = new Rectangle((int)posicion.X, (int)posicion.Y, 10, 10);

			if (relleno) {
				spriteBatch.Draw(pixel, caja, color);
				return;
			}

			spriteBatch.Draw(pixel, new Rectangle(caja.X, caja.Y, caja.Width, 2), color);
			spriteBatch.Draw(pixel, new Rectangle(caja.X, caja.Bottom - 2, caja.Width, 2), color);
			spriteBatch.Draw(pixel, new Rectangle(caja.X, caja.Y, 2, caja.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(caja.Right - 2, caja.Y, 2, caja.Height), color);
		}
	}
}
