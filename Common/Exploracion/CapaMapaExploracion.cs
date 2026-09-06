using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Dibuja los marcadores de Terrakeep ENCIMA del mapa del propio juego.
	/// </summary>
	/// <remarks>
	/// Esta es la mitad "vanilla" del diseño hibrido que decidio el usuario, y existe por un
	/// hallazgo real del motor: mientras <c>Main.mapFullscreen</c> es true, el juego dibuja el
	/// mapa y <b>hace <c>return</c> antes de llegar a la interfaz</b> (codigo real del
	/// <c>tModLoader.dll</c> instalado, dentro de <c>Main.DoDraw</c>), asi que NINGUNA interfaz de
	/// mod se pinta ahi: el panel propio y el mapa a pantalla completa son mutuamente
	/// excluyentes. La forma soportada de poner algo propio encima de ese mapa es un
	/// <see cref="ModMapLayer"/>.
	/// <para />
	/// La ventaja de hacerlo asi, y no dibujando a pelo, es que
	/// <see cref="MapOverlayDrawContext"/> ya trae la transformacion mapa-&gt;pantalla resuelta
	/// (<c>(posicion - MapPosition) * MapScale + MapOffset</c>) y ademas devuelve si el raton esta
	/// encima del icono, que es lo que da el texto flotante. Se le pasan coordenadas de TILE, no
	/// de mundo.
	/// <para />
	/// La capa se dibuja tambien en el mini-mapa y en el mapa superpuesto de vanilla, no solo a
	/// pantalla completa. Se limita a <c>Main.mapFullscreen</c> a proposito: el mini-mapa de la
	/// esquina es diminuto y llenarlo de rombos solo estorbaria, y el panel de Terrakeep ya pinta
	/// los suyos por su cuenta.
	/// </remarks>
	public class CapaMapaExploracion : ModMapLayer
	{
		/// <summary>Cuantos marcadores se han llegado a dibujar en el ultimo fotograma. Lo lee la
		/// autoprueba: es la evidencia de que la capa se ejecuta de verdad dentro del mapa
		/// vanilla, no solo de que este registrada.</summary>
		public static int DibujadosUltimoFotograma;

		/// <summary>Fotogramas en los que esta capa ha llegado a dibujar algo.</summary>
		public static int FotogramasDibujados;

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			if (!Main.mapFullscreen || !MarcadoresExploracion.HayAlgo) {
				DibujadosUltimoFotograma = 0;
				return;
			}

			Texture2D icono = IconosExploracion.Rombo(Main.spriteBatch.GraphicsDevice);
			int dibujados = 0;

			foreach (ResultadoBusqueda resultado in MarcadoresExploracion.Resultados) {
				MapOverlayDrawContext.DrawResult trazo = context.Draw(
					icono,
					resultado.Tile,
					MarcadoresExploracion.Color,
					new SpriteFrame(1, 1),
					1f,       // escala normal
					1.4f,     // escala con el raton encima, igual que hacen los iconos de vanilla
					Alignment.Center);

				dibujados++;

				if (trazo.IsMouseOver) {
					text = "Terrakeep: " + resultado.Etiqueta +
						" (" + (int)resultado.Tile.X + ", " + (int)resultado.Tile.Y + ")";
				}
			}

			DibujadosUltimoFotograma = dibujados;
			if (dibujados > 0) {
				FotogramasDibujados++;
			}
		}
	}
}
