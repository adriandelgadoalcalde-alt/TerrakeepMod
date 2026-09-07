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
		private const float EscalaTexto = 0.8f;
		private const float AltoMinimo = 34f;

		private readonly int _iconoTipo;
		private readonly string _nombre;
		private readonly bool _tieneHijas;
		private readonly int _objetos;

		/// <summary>Nombre YA envuelto (<see cref="EtiquetaTk.PartirEnLineas"/>) a tantas lineas como
		/// haga falta para caber en el ancho real de esta fila. Se calcula UNA vez, en el
		/// constructor, porque <paramref name="ancho"/> es fijo durante toda la vida de la fila (lo
		/// decide <c>AnchoColumnaCarpetas</c>, una constante, no algo que cambie con la ventana).</summary>
		private readonly string _nombrePartido;

		/// <summary>Alto REAL (con la fuente real) del bloque de texto ya envuelto. Con esto la fila
		/// crece lo que haga falta en vez de recortar el nombre - ver <see cref="AltoFila"/>.</summary>
		private readonly float _altoTexto;

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

			// Nunca se recorta con "...": si el nombre no cabe en una linea se ENVUELVE a varias
			// (mismo patron que ya uso PestanaBuffs con el nombre de un buff activo, commit cae61b5)
			// y la fila crece lo que haga falta - esta lista es un UIList real (ver
			// ContenidoLibreria.RellenarCarpetas), asi que una fila mas alta simplemente empuja a la
			// siguiente hacia abajo, no rompe nada.
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			float xTexto = _iconoTipo > 0 ? 6f + LadoIcono + 8f : 10f;
			float anchoTexto = ancho - xTexto - (_tieneHijas ? 22f : 10f);
			_nombrePartido = EtiquetaTk.PartirEnLineas(_nombre, anchoTexto > 10f ? anchoTexto : ancho, EscalaTexto);
			_altoTexto = fuente.MeasureString(_nombrePartido).Y * EscalaTexto;
			float altoUnaLinea = fuente.MeasureString("Ag").Y * EscalaTexto;
			float relleno = AltoMinimo - altoUnaLinea;

			SetPadding(0f);
			Width.Set(ancho, 0f);
			Height.Set(Math.Max(AltoMinimo, _altoTexto + relleno), 0f);
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
			float yTexto = dim.Y + (dim.Height - _altoTexto) / 2f;
			Utils.DrawBorderString(spriteBatch, _nombrePartido,
				new Vector2(xTexto, yTexto), Color.White, EscalaTexto);

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
	}
}
