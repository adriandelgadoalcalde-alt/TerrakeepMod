using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// El sprite REAL (tile, pared, liquido, cofre o NPC) que representa un resultado de
	/// busqueda, para pintarlo en la lista de resultados (<c>PestanaBusqueda</c>) y en los
	/// marcadores del mini-mapa (<c>MiniMapaTk</c>).
	/// </summary>
	/// <remarks>
	/// Nunca se dibuja un icono inventado: siempre es la MISMA textura que ya usa el juego para
	/// esa cosa, pedida por la via publica de <c>Main.instance.Load*</c> +
	/// <c>TextureAssets.*[tipo].Value</c> (codigo real de <c>tModLoader.dll</c> instalado:
	/// <c>Main.LoadTiles</c>/<c>LoadWall</c>/<c>LoadNPC</c>/<c>LoadItem</c> solo hacen
	/// <c>Assets.Request</c> si el estado todavia esta a <c>NotLoaded</c>, asi que llamarlos cada
	/// vez que se dibuja es barato).
	/// <para />
	/// Los tiles usan una rejilla de 16x16 con 2 px de separacion (18 px de paso) y las paredes de
	/// 32x32 con 34 px de paso: es el formato de sprite-sheet fijo de Terraria (confirmado leyendo
	/// <c>WallDrawing.cs</c> real, que construye <c>new Rectangle(0, 0, 32, 32)</c> para una
	/// pared), asi que el primer frame (variante base) siempre cae en <c>(0, 0, 16, 16)</c> /
	/// <c>(0, 0, 32, 32)</c> sin tener que conocer cuantas variantes tiene cada tile.
	/// <para />
	/// Para los cofres se usa el icono del OBJETO "Cofre" (<see cref="ItemID.Chest"/>) en vez del
	/// sprite exacto de su estilo real: recorrer el frame real de <c>TileID.Containers</c> exige
	/// distinguir cofres/comodas/estilo bloqueado con matematica de frame distinta cada uno (ver
	/// <c>Chest.cs</c> decompilado), y una comoda o un cofre de sombra dibujados con el frame
	/// equivocado serian PEOR que un icono generico pero siempre correcto. Sigue siendo un sprite
	/// real del juego, no uno inventado.
	/// </remarks>
	public static class IconoResultado
	{
		private const int LadoTile = 16;
		private const int LadoPared = 32;

		/// <summary>Textura y recorte de origen reales para un resultado. false si esta categoria
		/// no tiene (todavia) un icono resuelto.</summary>
		public static bool Obtener(ObjetivoBusqueda objetivo, ResultadoBusqueda resultado,
			out Texture2D textura, out Rectangle origen)
		{
			textura = null;
			origen = Rectangle.Empty;

			if (objetivo == null) {
				return false;
			}

			switch (objetivo.Clase) {
				case ClaseDeObjetivo.Tile:
					return DeTile(objetivo, out textura, out origen);
				case ClaseDeObjetivo.Pared:
					return DePared(objetivo, out textura, out origen);
				case ClaseDeObjetivo.Liquido:
					return DeLiquido(objetivo.Liquido, out textura, out origen);
				case ClaseDeObjetivo.Cofres:
					return DeCofre(out textura, out origen);
				case ClaseDeObjetivo.Npcs:
					return DeNpc(resultado, out textura, out origen);
				default:
					return false;
			}
		}

		/// <summary>
		/// Dibuja el icono real de <paramref name="resultado"/>, escalado (conservando proporcion,
		/// sin deformar) para que quepa entero dentro de <paramref name="destino"/> y centrado en
		/// el.
		/// </summary>
		public static void Dibujar(SpriteBatch spriteBatch, ObjetivoBusqueda objetivo,
			ResultadoBusqueda resultado, Rectangle destino, Color tinte)
		{
			if (!Obtener(objetivo, resultado, out Texture2D textura, out Rectangle origen)
				|| textura == null || textura.IsDisposed || origen.Width <= 0 || origen.Height <= 0) {
				return;
			}

			float escala = System.Math.Min(
				destino.Width / (float)origen.Width,
				destino.Height / (float)origen.Height);

			Vector2 tamano = new Vector2(origen.Width * escala, origen.Height * escala);
			Vector2 posicion = new Vector2(
				destino.X + (destino.Width - tamano.X) / 2f,
				destino.Y + (destino.Height - tamano.Y) / 2f);

			spriteBatch.Draw(textura, posicion, origen, tinte, 0f, Vector2.Zero, escala,
				SpriteEffects.None, 0f);
		}

		private static bool DeTile(ObjetivoBusqueda objetivo, out Texture2D textura, out Rectangle origen)
		{
			textura = null;
			origen = Rectangle.Empty;
			if (objetivo.Tipos.Count == 0) {
				return false;
			}

			int tipo = objetivo.Tipos[0];
			if (tipo < 0 || tipo >= TextureAssets.Tile.Length) {
				return false;
			}

			Main.instance.LoadTiles(tipo);
			textura = TextureAssets.Tile[tipo].Value;
			origen = new Rectangle(0, 0, LadoTile, LadoTile);
			return true;
		}

		private static bool DePared(ObjetivoBusqueda objetivo, out Texture2D textura, out Rectangle origen)
		{
			textura = null;
			origen = Rectangle.Empty;
			if (objetivo.Tipos.Count == 0) {
				return false;
			}

			int tipo = objetivo.Tipos[0];
			if (tipo < 0 || tipo >= TextureAssets.Wall.Length) {
				return false;
			}

			Main.instance.LoadWall(tipo);
			textura = TextureAssets.Wall[tipo].Value;
			origen = new Rectangle(0, 0, LadoPared, LadoPared);
			return true;
		}

		private static bool DeLiquido(int tipo, out Texture2D textura, out Rectangle origen)
		{
			textura = null;
			origen = Rectangle.Empty;
			if (tipo < 0 || tipo >= TextureAssets.Liquid.Length) {
				return false;
			}

			// Las texturas de liquido estan siempre cargadas (son 4: agua, lava, miel, fulgor), no
			// hay un Main.instance.LoadLiquid que pedirles antes, a diferencia de tiles/paredes/NPC.
			textura = TextureAssets.Liquid[tipo].Value;
			origen = new Rectangle(0, 0, LadoTile, LadoTile);
			return true;
		}

		private static bool DeCofre(out Texture2D textura, out Rectangle origen)
		{
			Main.instance.LoadItem(ItemID.Chest);
			textura = TextureAssets.Item[ItemID.Chest].Value;
			origen = textura != null ? textura.Bounds : Rectangle.Empty;
			return textura != null;
		}

		private static bool DeNpc(ResultadoBusqueda resultado, out Texture2D textura, out Rectangle origen)
		{
			textura = null;
			origen = Rectangle.Empty;
			if (resultado == null || resultado.TipoNpc < 0 || resultado.TipoNpc >= TextureAssets.Npc.Length) {
				return false;
			}

			Main.instance.LoadNPC(resultado.TipoNpc);
			textura = TextureAssets.Npc[resultado.TipoNpc].Value;
			if (textura == null) {
				return false;
			}

			// El frame se capturo en el instante de la busqueda (NPC.frame, que el motor mantiene
			// al dia mientras el NPC esta vivo). Si por lo que sea llego vacio (NPC recien
			// aparecido, sin haber pasado todavia por su primer FindFrame), se cae a la textura
			// entera antes que a no enseñar nada.
			origen = resultado.FrameNpc.Width > 0 && resultado.FrameNpc.Height > 0
				? resultado.FrameNpc
				: textura.Bounds;
			return true;
		}
	}
}
