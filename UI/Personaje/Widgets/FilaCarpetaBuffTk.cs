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
using TerrasavrNative.Core.Data;

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

		private readonly int _iconoTipo;
		private readonly string _nombre;
		private readonly bool _tieneHijas;
		private readonly int _buffs;

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
				Main.LocalPlayer.mouseInterface = true;
			}

			CalculatedStyle dim = GetDimensions();

			bool hayIcono = DibujarIcono(spriteBatch,
				new Vector2(dim.X + 6f + LadoIcono / 2f, dim.Y + dim.Height / 2f));

			float xTexto = dim.X + (hayIcono ? 6f + LadoIcono + 8f : 10f);
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

		/// <summary>Recorta el texto para que quepa, midiendolo con la fuente REAL con la que se va
		/// a dibujar. Idem <see cref="TerrakeepMod.UI.Libreria.FilaCarpetaTk"/>: nunca con "…", la
		/// fuente del juego no trae ese caracter.</summary>
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
