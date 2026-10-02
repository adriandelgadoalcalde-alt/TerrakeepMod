using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terrakeep.Core.Guia.V2;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// <see cref="IMundoGuia"/> sobre el mundo CARGADO de verdad (<c>Main.tile</c>), para que
	/// <c>GuiaV2Ubicaciones</c> (Core, el mismo codigo que usa el escritorio sobre el .wld) situe
	/// cada zona de la guia en este mundo concreto.
	/// </summary>
	/// <remarks>
	/// Tiles de mod: <c>TileLoader.GetTile(type)</c> → "Mod/NombreInterno", cacheado por tipo (se
	/// pregunta millones de veces en un barrido). Puntos guardados: los centros de laboratorio de
	/// Calamity (<see cref="ReflexionCalamity.CentroLaboratorio"/>) y "altar" (el altar demoniaco o
	/// carmesi mas cercano al jugador, recorrido en vivo).
	/// </remarks>
	public sealed class MundoGuiaVivo : IMundoGuia
	{
		private readonly string[] _nombresMod;

		public MundoGuiaVivo()
		{
			_nombresMod = new string[TileLoader.TileCount];
		}

		public int Ancho => Main.maxTilesX;
		public int Alto => Main.maxTilesY;
		public int NivelSuperficie => (int)Main.worldSurface;
		public int NivelRoca => (int)Main.rockLayer;
		public int SpawnX => Main.spawnTileX;
		public int SpawnY => Main.spawnTileY;
		public int MazmorraX => Main.dungeonX;
		public int MazmorraY => Main.dungeonY;

		public int TileVanilla(int x, int y)
		{
			Tile t = Main.tile[x, y];
			if (!t.HasTile) {
				return -1;
			}
			ushort tipo = t.TileType;
			return tipo < TileID.Count ? tipo : -1;
		}

		public string TileMod(int x, int y)
		{
			Tile t = Main.tile[x, y];
			if (!t.HasTile) {
				return null;
			}
			ushort tipo = t.TileType;
			if (tipo < TileID.Count || tipo >= _nombresMod.Length) {
				return null;
			}
			string nombre = _nombresMod[tipo];
			if (nombre == null) {
				ModTile mt = TileLoader.GetTile(tipo);
				nombre = mt != null ? mt.Mod.Name + "/" + mt.Name : "";
				_nombresMod[tipo] = nombre;
			}
			return nombre.Length > 0 ? nombre : null;
		}

		public (int X, int Y)? Punto(string clave)
		{
			if (clave == "altar") {
				return AltarMasCercano();
			}
			return ReflexionCalamity.CentroLaboratorio(clave);
		}

		/// <summary>Altar demoniaco/carmesi (TileID 26) mas cercano al jugador, por barrido cada 2
		/// casillas (un altar mide 3x2, no se escapa).</summary>
		public static (int X, int Y)? AltarMasCercano()
		{
			Player p = Main.LocalPlayer;
			if (p == null || !p.active) {
				return null;
			}
			int px = (int)(p.Center.X / 16f), py = (int)(p.Center.Y / 16f);
			long mejor = long.MaxValue;
			(int, int)? r = null;
			for (int x = 1; x < Main.maxTilesX - 1; x += 2) {
				for (int y = (int)Main.worldSurface; y < Main.maxTilesY - 1; y += 2) {
					Tile t = Main.tile[x, y];
					if (!t.HasTile || t.TileType != TileID.DemonAltar) {
						continue;
					}
					long dx = x - px, dy = y - py;
					long d = dx * dx + dy * dy;
					if (d < mejor) {
						mejor = d;
						r = (x, y);
					}
				}
			}
			return r;
		}
	}
}
