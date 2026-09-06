using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TerrakeepMod.UI.Libreria
{
	/// <summary>
	/// Dibuja el sprite REAL de un objeto (sin el marco de ranura), centrado y escalado para
	/// caber en un cuadrado. Se usa para el icono de cada carpeta del arbol de la Libreria.
	/// </summary>
	/// <remarks>
	/// Es exactamente lo que hace el juego para pintar un objeto suelto: cargar su textura bajo
	/// demanda con <c>Main.instance.LoadItem(id)</c> (las texturas de objeto son perezosas, no
	/// estan todas cargadas de fabrica) y, si ese objeto tiene animacion registrada
	/// (<c>Main.itemAnimations</c>, ej. las monedas o los corazones), pedirle el fotograma actual
	/// en vez de dibujar la tira entera.
	/// <para />
	/// Aqui NO se usa <c>ItemSlot.Draw</c> a proposito: ese dibuja tambien el marco de la ranura,
	/// que en una lista de carpetas competiria visualmente con las ranuras de verdad del panel.
	/// </remarks>
	public static class IconoObjetoTk
	{
		/// <summary>
		/// Dibuja el icono del objeto <paramref name="tipo"/> centrado en <paramref name="centro"/>
		/// sin pasarse de <paramref name="lado"/> pixeles de ancho ni de alto.
		/// </summary>
		/// <returns>false si no habia nada que dibujar (id 0, textura no disponible).</returns>
		public static bool Dibujar(SpriteBatch spriteBatch, int tipo, Vector2 centro, float lado, Color color)
		{
			if (tipo <= 0 || tipo >= ItemLoader.ItemCount) {
				return false;
			}

			try {
				Main.instance.LoadItem(tipo);
				if (TextureAssets.Item[tipo] == null || !TextureAssets.Item[tipo].IsLoaded) {
					return false;
				}

				Texture2D textura = TextureAssets.Item[tipo].Value;
				Rectangle marco = Main.itemAnimations != null && tipo < Main.itemAnimations.Length
					&& Main.itemAnimations[tipo] != null
					? Main.itemAnimations[tipo].GetFrame(textura)
					: textura.Frame(1, 1, 0, 0);

				if (marco.Width <= 0 || marco.Height <= 0) {
					return false;
				}

				// Solo se REDUCE: un sprite de 16x16 ampliado a 26 se ve borroso y desalineado con
				// el resto de la interfaz del juego, que siempre dibuja los objetos pequeños a su
				// tamaño real.
				float escala = Math.Min(lado / marco.Width, lado / marco.Height);
				if (escala > 1f) {
					escala = 1f;
				}

				spriteBatch.Draw(textura, centro, marco, color, 0f,
					new Vector2(marco.Width / 2f, marco.Height / 2f), escala, SpriteEffects.None, 0f);
				return true;
			}
			catch (Exception) {
				// Un mod con una textura rota no puede tumbar el dibujado de la lista entera.
				return false;
			}
		}
	}
}
