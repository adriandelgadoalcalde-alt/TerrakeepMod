using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.UI.Personaje.Widgets;
using TerrasavrNative.Core.Data;

namespace TerrakeepMod.UI.Libreria
{
	/// <summary>
	/// Una fila del arbol de carpetas: icono real del primer objeto de la carpeta, nombre, y una
	/// flecha si tiene subcarpetas dentro.
	/// </summary>
	/// <remarks>
	/// Hereda de <see cref="UIPanel"/> igual que <see cref="BotonTk"/> (WS1) para que el marco de
	/// nueve trozos lo dibuje el propio juego con sus texturas, y usa la MISMA paleta
	/// (<see cref="EstiloTk"/>) que los botones y pestañas del panel de Personaje: la Libreria
	/// tiene que verse como otra parte de la misma aplicacion, no como algo pegado al lado.
	/// </remarks>
	public class FilaCarpetaTk : UIPanel
	{
		private const float LadoIcono = 26f;

		private readonly int _iconoTipo;
		private readonly string _nombre;
		private readonly bool _tieneHijas;
		private readonly int _objetos;

		/// <summary>La carpeta que representa esta fila.</summary>
		public readonly CategoryTreeNodeData Nodo;

		/// <summary>Si es true la fila se pinta resaltada (carpeta abierta ahora mismo).</summary>
		public bool Activa;

		/// <summary>Se dispara con el clic izquierdo.</summary>
		public event Action AlPulsar;

		public FilaCarpetaTk(CategoryTreeNodeData nodo, float ancho)
		{
			Nodo = nodo;
			_nombre = nodo.Name ?? "";
			_tieneHijas = nodo.Children != null && nodo.Children.Count > 0;
			_objetos = nodo.ItemIdsOrdered != null ? nodo.ItemIdsOrdered.Count : 0;
			_iconoTipo = ArbolLibreria.IdDeIcono(nodo.IconPath);

			SetPadding(0f);
			Width.Set(ancho, 0f);
			Height.Set(34f, 0f);
			BorderColor = new Color(0, 0, 0, 0);

			OnLeftClick += (evento, elemento) => {
				if (AlPulsar != null) {
					AlPulsar();
				}
			};
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			BackgroundColor = Activa
				? EstiloTk.BotonActivo
				: (IsMouseHovering ? EstiloTk.BotonSobre : EstiloTk.FondoCaja);

			base.DrawSelf(spriteBatch);

			if (IsMouseHovering && !PlayerInput.IgnoreMouseInterface) {
				// Sin esto el clic atraviesa el panel y el jugador ataca o coloca bloques detras.
				Main.LocalPlayer.mouseInterface = true;
			}

			CalculatedStyle dim = GetDimensions();

			bool hayIcono = IconoObjetoTk.Dibujar(spriteBatch, _iconoTipo,
				new Vector2(dim.X + 6f + LadoIcono / 2f, dim.Y + dim.Height / 2f), LadoIcono, Color.White);

			float xTexto = dim.X + (hayIcono ? 6f + LadoIcono + 8f : 10f);
			// Se reserva sitio a la derecha para la flecha de "tiene subcarpetas".
			float anchoTexto = dim.Width - (xTexto - dim.X) - (_tieneHijas ? 22f : 10f);

			string texto = Recortar(_nombre, anchoTexto, 0.8f);
			Utils.DrawBorderString(spriteBatch, texto,
				new Vector2(xTexto, dim.Y + dim.Height / 2f - 10f), Color.White, 0.8f);

			if (_tieneHijas) {
				Utils.DrawBorderString(spriteBatch, ">",
					new Vector2(dim.X + dim.Width - 16f, dim.Y + dim.Height / 2f - 10f),
					EstiloTk.TextoSuave, 0.8f);
			}

			if (IsMouseHovering) {
				Main.instance.MouseText(_tieneHijas
					? Idiomas.Texto("Libreria.CarpetaConSubcarpetas", _nombre, _objetos, Nodo.Children.Count)
					: Idiomas.Texto("Libreria.CarpetaObjetos", _nombre, _objetos));
			}
		}

		/// <summary>
		/// Recorta el texto para que quepa, midiendolo con la fuente REAL con la que se va a
		/// dibujar. Se corta con tres puntos normales y nunca con el caracter "…": la fuente del
		/// juego solo trae el juego de caracteres con el que se genero, y uno que no este hace
		/// reventar a <c>DynamicSpriteFont</c> al medir (hallazgo ya anotado por WS4).
		/// </summary>
		private static string Recortar(string texto, float anchoMaximo, float escala)
		{
			if (string.IsNullOrEmpty(texto) || anchoMaximo <= 0f) {
				return texto ?? "";
			}

			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			if (fuente.MeasureString(texto).X * escala <= anchoMaximo) {
				return texto;
			}

			int n = texto.Length;
			while (n > 1 && fuente.MeasureString(texto.Substring(0, n) + "...").X * escala > anchoMaximo) {
				n--;
			}
			return texto.Substring(0, n) + "...";
		}
	}
}
