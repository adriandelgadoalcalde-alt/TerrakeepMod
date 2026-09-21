using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Guia
{
	/// <summary>
	/// Un parrafo que se envuelve solo al ancho REAL que tenga en pantalla y que <b>ajusta su
	/// propio alto</b> al numero de lineas que le salen.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Hace falta porque el texto de esta area es prosa de verdad (el "porque" de cada objetivo),
	/// no rotulos de dos palabras, y el proyecto ya tiene dos reglas que lo obligan: <b>ningun
	/// texto se recorta con puntos suspensivos</b>, y <b>un salto de linea escrito a mano solo
	/// vale para el idioma con el que se escribio</b> (la misma frase en ingles ocupa otra cosa).
	/// <see cref="EtiquetaTk.PartirEnLineas"/> ya sabe partir midiendo con la fuente real; lo que
	/// faltaba era un elemento que ademas creciera para que lo de abajo no se le monte encima.
	/// </para>
	/// <para>
	/// El alto se recalcula solo cuando CAMBIA (texto nuevo, otro idioma, otra resolucion) y
	/// entonces se avisa al padre con <c>Recalculate</c>. Hacerlo en cada fotograma sin la guarda
	/// dispararia un recalculo del arbol entero 60 veces por segundo para nada.
	/// </para>
	/// </remarks>
	public class ParrafoTk : UIElement
	{
		private readonly Func<string> _texto;
		private readonly float _escala;

		private string _ultimoCrudo;
		private float _ultimoAncho;
		private string _envuelto = "";

		/// <summary>Color del texto.</summary>
		public Color ColorTexto = Color.White;

		/// <summary>Alto de una linea, ya escalado.</summary>
		public float AltoLinea => FontAssets.MouseText.Value.LineSpacing * _escala;

		/// <summary>Escala con la que se dibuja. La lee la autoprueba para medir cada linea con la
		/// misma escala real y demostrar que no se sale de su caja.</summary>
		public float Escala => _escala;

		public ParrafoTk(Func<string> texto, float escala = 0.8f)
		{
			_texto = texto;
			_escala = escala;
			Width.Set(0f, 1f);
			Height.Set(0f, 0f);
		}

		/// <summary>El texto ya envuelto que se esta dibujando. Lo leen las autopruebas para
		/// comprobar que no se sale del marco y que dice lo que tiene que decir.</summary>
		public string TextoEnvuelto => _envuelto;

		/// <summary>Cuantas lineas ocupa ahora mismo.</summary>
		public int Lineas => string.IsNullOrEmpty(_envuelto) ? 0 : _envuelto.Split('\n').Length;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			Reajustar();
		}

		private void Reajustar()
		{
			string crudo = _texto != null ? (_texto() ?? "") : "";
			float ancho = GetInnerDimensions().Width;
			if (ancho <= 0f) {
				return;
			}

			if (crudo == _ultimoCrudo && Math.Abs(ancho - _ultimoAncho) < 0.5f) {
				return;
			}

			_ultimoCrudo = crudo;
			_ultimoAncho = ancho;
			_envuelto = EtiquetaTk.PartirEnLineas(crudo, ancho, _escala);

			float alto = Lineas * AltoLinea;
			if (Math.Abs(Height.Pixels - alto) >= 0.5f) {
				Height.Set(alto, 0f);
				if (Parent != null) {
					Parent.Recalculate();
				}
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			// La primera vez que se dibuja todavia no ha pasado por Update con geometria real.
			Reajustar();

			if (string.IsNullOrEmpty(_envuelto)) {
				return;
			}

			CalculatedStyle dim = GetInnerDimensions();
			EscribirTk.Dibujar(spriteBatch, _envuelto, new Vector2(dim.X, dim.Y), ColorTexto, _escala);
		}
	}
}
