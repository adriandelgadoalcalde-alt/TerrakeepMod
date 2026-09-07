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
using TerrakeepMod.Common.Personaje;
using Terrakeep.Core.Data;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Una fila del arbol de carpetas de <see cref="TerrakeepMod.UI.Personaje.PestanaBuffs"/>:
	/// icono real del primer buff de la carpeta, nombre, y una flecha si tiene subcarpetas dentro.
	/// <para />
	/// Calco literal de <see cref="TerrakeepMod.UI.Libreria.FilaCarpetaTk"/> (misma paleta, mismo
	/// recorte de texto con la fuente real, mismo criterio de resaltado), con la unica diferencia
	/// real que hacia falta: el icono es el de un BUFF (<c>TextureAssets.Buff</c>), no el de un
	/// objeto, asi que no se puede reutilizar <c>IconoObjetoTk.Dibujar</c> tal cual.
	/// </summary>
	public class FilaCarpetaBuffTk : UIPanel
	{
		private const float LadoIcono = 26f;
		private const float EscalaTexto = 0.8f;
		private const float AltoMinimo = 34f;

		private readonly int _iconoTipo;
		private readonly string _nombre;
		private readonly bool _tieneHijas;
		private readonly int _buffs;

		/// <summary>Nombre YA envuelto (<see cref="EtiquetaTk.PartirEnLineas"/>) a tantas lineas como
		/// haga falta para caber en el ancho real de esta fila. Se calcula UNA vez, en el
		/// constructor, porque <paramref name="ancho"/> es fijo durante toda la vida de la fila (lo
		/// decide <c>AnchoColumnaCarpetas</c>, una constante, no algo que cambie con la ventana).</summary>
		private readonly string _nombrePartido;

		/// <summary>Alto REAL (con la fuente real) del bloque de texto ya envuelto. Con esto la fila
		/// crece lo que haga falta en vez de recortar el nombre.</summary>
		private readonly float _altoTexto;

		/// <summary>La carpeta que representa esta fila.</summary>
		public readonly CategoryTreeNodeData Nodo;

		/// <summary>Si es true la fila se pinta resaltada (carpeta abierta ahora mismo).</summary>
		public bool Activa;

		/// <summary>Se dispara con el clic izquierdo.</summary>
		public event Action AlPulsar;

		public FilaCarpetaBuffTk(CategoryTreeNodeData nodo, float ancho)
		{
			Nodo = nodo;
			_nombre = nodo.Name ?? "";
			_tieneHijas = nodo.Children != null && nodo.Children.Count > 0;
			_buffs = nodo.ItemIdsOrdered != null ? nodo.ItemIdsOrdered.Count : 0;
			_iconoTipo = ArbolBuffs.IdDeIcono(nodo.IconPath);

			// Nunca se recorta con "...": si el nombre no cabe en una linea se ENVUELVE a varias
			// (mismo patron que ya uso PestanaBuffs con el nombre de un buff activo, commit cae61b5,
			// y el calco literal de esta fila en la Libreria, FilaCarpetaTk) y la fila crece lo que
			// haga falta - es un UIList real (ver PestanaBuffs.RellenarCarpetas), asi que una fila
			// mas alta simplemente empuja a la siguiente hacia abajo, no rompe nada.
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
				Main.LocalPlayer.mouseInterface = true;
			}

			CalculatedStyle dim = GetDimensions();

			bool hayIcono = DibujarIcono(spriteBatch,
				new Vector2(dim.X + 6f + LadoIcono / 2f, dim.Y + dim.Height / 2f));

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
					? Idiomas.Texto("Personaje.Buffs.Arbol.CarpetaConSubcarpetas", _nombre, _buffs, Nodo.Children.Count)
					: Idiomas.Texto("Personaje.Buffs.Arbol.CarpetaBuffs", _nombre, _buffs));
			}
		}

		/// <summary>Dibuja el icono real del primer buff de la carpeta, centrado en <paramref
		/// name="centro"/>. Mismas comprobaciones defensivas que <see cref="IconoBuffTk"/>: un
		/// recurso puede no estar cargado todavia, o el array puede no llegar a ese indice.</summary>
		private bool DibujarIcono(SpriteBatch spriteBatch, Vector2 centro)
		{
			if (_iconoTipo <= 0 || TextureAssets.Buff == null || _iconoTipo >= TextureAssets.Buff.Length) {
				return false;
			}

			var recurso = TextureAssets.Buff[_iconoTipo];
			if (recurso == null || !recurso.IsLoaded) {
				return false;
			}

			Texture2D textura = recurso.Value;
			float escala = LadoIcono / Math.Max(textura.Width, textura.Height);
			if (escala > 1f) {
				escala = 1f;
			}

			spriteBatch.Draw(textura,
				new Vector2(centro.X - textura.Width * escala / 2f, centro.Y - textura.Height * escala / 2f),
				null, Color.White, 0f, Vector2.Zero, escala, SpriteEffects.None, 0f);
			return true;
		}

	}
}
