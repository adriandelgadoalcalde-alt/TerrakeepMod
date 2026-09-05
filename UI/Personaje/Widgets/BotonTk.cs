using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Boton de texto con el fondo de panel nativo de Terraria. Se usa para las pestañas, los
	/// selectores de loadout/almacen y cualquier accion suelta del panel.
	/// <para />
	/// Hereda de <see cref="UIPanel"/> a proposito: asi el marco de nueve trozos lo dibuja el
	/// propio juego con sus texturas, y no hay que reimplementar nada.
	/// </summary>
	public class BotonTk : UIPanel
	{
		private string _texto;
		private readonly float _escalaTexto;

		/// <summary>Si es true el boton se pinta resaltado (pestaña seleccionada, opcion activa).</summary>
		public bool Activo;

		/// <summary>Si es false el boton se pinta apagado y no dispara <see cref="AlPulsar"/>.</summary>
		public bool Habilitado = true;

		/// <summary>Texto que se muestra en el tooltip del juego al pasar el raton. null = ninguno.</summary>
		public string Ayuda;

		/// <summary>Se dispara con el clic izquierdo, solo si el boton esta habilitado.</summary>
		public event Action AlPulsar;

		public BotonTk(string texto, float escalaTexto = 0.85f)
		{
			_texto = texto;
			_escalaTexto = escalaTexto;
			SetPadding(0f);
			Width.Set(120f, 0f);
			Height.Set(32f, 0f);
			BorderColor = new Color(0, 0, 0, 0);

			OnLeftClick += (evento, elemento) => {
				if (Habilitado && AlPulsar != null) {
					AlPulsar();
				}
			};
		}

		public void FijarTexto(string texto)
		{
			_texto = texto ?? "";
		}

		public string Texto => _texto;

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (!Habilitado) {
				BackgroundColor = EstiloTk.BotonApagado;
			}
			else if (Activo) {
				BackgroundColor = EstiloTk.BotonActivo;
			}
			else if (IsMouseHovering) {
				BackgroundColor = EstiloTk.BotonSobre;
			}
			else {
				BackgroundColor = EstiloTk.BotonNormal;
			}

			base.DrawSelf(spriteBatch);

			if (IsMouseHovering) {
				// Sin esto el clic atraviesa el panel y el jugador ataca o coloca bloques detras.
				Main.LocalPlayer.mouseInterface = true;
				if (!string.IsNullOrEmpty(Ayuda)) {
					Main.instance.MouseText(Ayuda);
				}
			}

			CalculatedStyle dim = GetDimensions();
			Vector2 tamano = FontAssets.MouseText.Value.MeasureString(_texto) * _escalaTexto;
			Vector2 posicion = new Vector2(
				dim.X + (dim.Width - tamano.X) / 2f,
				dim.Y + (dim.Height - tamano.Y) / 2f);

			Color color = Habilitado ? Color.White : new Color(150, 150, 150);
			Utils.DrawBorderString(spriteBatch, _texto, posicion, color, _escalaTexto);
		}
	}
}
