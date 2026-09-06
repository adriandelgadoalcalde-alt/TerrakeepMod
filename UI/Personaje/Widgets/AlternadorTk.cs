using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Casilla de verificacion. No guarda estado propio: lee y escribe DIRECTAMENTE el campo real
	/// del jugador a traves de las dos funciones que se le pasan, asi que siempre muestra la
	/// verdad aunque el juego cambie el valor por su cuenta entre fotogramas.
	/// </summary>
	public class AlternadorTk : UIPanel
	{
		private readonly Func<string> _etiqueta;
		private readonly Func<bool> _leer;
		private readonly Action<bool> _escribir;

		/// <summary>Texto del tooltip al pasar el raton. Se pide en cada dibujado (y no se guarda
		/// ya resuelto) para que cambie con el idioma sin reconstruir la pestaña.</summary>
		public Func<string> Ayuda;

		/// <summary>Se dispara despues de cambiar el valor, con el valor nuevo.</summary>
		public event Action<bool> AlCambiar;

		/// <summary>El rotulo se pide con un <c>Func&lt;string&gt;</c>: guardado ya resuelto se
		/// quedaria congelado en el idioma que hubiera al construir la casilla.</summary>
		public AlternadorTk(Func<string> etiqueta, Func<bool> leer, Action<bool> escribir)
		{
			_etiqueta = etiqueta;
			_leer = leer;
			_escribir = escribir;

			SetPadding(0f);
			Width.Set(0f, 1f);
			Height.Set(30f, 0f);
			BorderColor = new Color(0, 0, 0, 0);

			OnLeftClick += (evento, elemento) => {
				bool nuevo = !_leer();
				_escribir(nuevo);
				if (AlCambiar != null) {
					AlCambiar(nuevo);
				}
			};
		}

		/// <summary>Valor real ahora mismo, leido del jugador.</summary>
		public bool Valor => _leer();

		/// <summary>Rotulo que se esta enseñando ahora mismo, ya traducido.</summary>
		public string EtiquetaActual => _etiqueta != null ? (_etiqueta() ?? "") : "";

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			bool marcado = _leer();
			BackgroundColor = IsMouseHovering ? EstiloTk.BotonSobre : EstiloTk.BotonNormal;
			base.DrawSelf(spriteBatch);

			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
				string ayuda = Ayuda != null ? Ayuda() : null;
				if (!string.IsNullOrEmpty(ayuda)) {
					Main.instance.MouseText(ayuda);
				}
			}

			CalculatedStyle dim = GetDimensions();
			string marca = marcado ? "[X]" : "[  ]";
			Color colorMarca = marcado ? new Color(120, 255, 140) : new Color(170, 170, 170);

			Utils.DrawBorderString(spriteBatch, marca,
				new Vector2(dim.X + 8f, dim.Y + 6f), colorMarca, 0.8f);
			Utils.DrawBorderString(spriteBatch, EtiquetaActual,
				new Vector2(dim.X + 44f, dim.Y + 6f), Color.White, 0.8f);
		}
	}
}
