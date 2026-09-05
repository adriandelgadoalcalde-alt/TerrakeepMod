using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Fila "etiqueta  [-] valor [+]". El valor NO se guarda aqui: se pide con
	/// <c>textoValor()</c> en cada dibujado, asi que siempre enseña el estado real del jugador
	/// aunque lo haya cambiado el propio juego.
	/// </summary>
	public class SelectorTk : UIElement
	{
		private readonly string _etiqueta;
		private readonly Func<string> _textoValor;
		private readonly float _anchoEtiqueta;

		public SelectorTk(string etiqueta, Func<string> textoValor, Action<int> alPaso,
			float anchoEtiqueta = 130f, int pasoGrande = 0, float ancho = 330f)
		{
			_etiqueta = etiqueta;
			_textoValor = textoValor;
			_anchoEtiqueta = anchoEtiqueta;

			Width.Set(ancho, 0f);
			Height.Set(30f, 0f);

			float x = ancho;

			if (pasoGrande > 0) {
				x -= 32f;
				Append(CrearBoton(">>", x, () => alPaso(pasoGrande)));
			}
			x -= 32f;
			Append(CrearBoton(">", x, () => alPaso(1)));

			float xIzquierda = _anchoEtiqueta;
			Append(CrearBoton("<", xIzquierda, () => alPaso(-1)));
			if (pasoGrande > 0) {
				Append(CrearBoton("<<", xIzquierda - 32f, () => alPaso(-pasoGrande)));
			}
		}

		private static BotonTk CrearBoton(string texto, float izquierda, Action accion)
		{
			BotonTk boton = new BotonTk(texto, 0.8f);
			boton.Width.Set(28f, 0f);
			boton.Height.Set(28f, 0f);
			boton.Left.Set(izquierda, 0f);
			boton.Top.Set(1f, 0f);
			boton.AlPulsar += accion;
			return boton;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dim = GetDimensions();

			Utils.DrawBorderString(spriteBatch, _etiqueta,
				new Vector2(dim.X, dim.Y + 6f), EstiloTk.TextoSuave, 0.8f);

			string valor = _textoValor();
			Utils.DrawBorderString(spriteBatch, valor,
				new Vector2(dim.X + _anchoEtiqueta + 34f, dim.Y + 6f), Color.White, 0.8f);
		}
	}
}
