using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>Que clase de cosa busca un <see cref="ObjetivoBusqueda"/>.</summary>
	public enum ClaseDeObjetivo
	{
		/// <summary>Bloques colocados, por tipo de tile.</summary>
		Tile,

		/// <summary>Paredes, por tipo de pared.</summary>
		Pared,

		/// <summary>Liquidos, por tipo de liquido.</summary>
		Liquido,

		/// <summary>Cofres y comodas del mundo (<c>Main.chest</c>), sin recorrer tiles.</summary>
		Cofres,

		/// <summary>NPC vivos ahora mismo (<c>Main.npc</c>), sin recorrer tiles.</summary>
		Npcs
	}

	/// <summary>
	/// Una cosa que se puede buscar en el mundo. Los tipos se resuelven por NOMBRE contra
	/// <c>TileID.Search</c> / <c>WallID.Search</c>, no por numero fijo.
	/// </summary>
	/// <remarks>
	/// Es el mismo truco que ya usa WS4 con <c>ItemID.Search</c>, y por los mismos motivos: esos
	/// diccionarios son de ReLogic y tienen dentro TANTO los nombres de campo de vanilla
	/// (<c>"Chlorophyte"</c>) COMO los de cualquier mod cargado, porque
	/// <c>ModTile.Register</c> hace <c>TileID.Search.Add(FullName, Type)</c> (codigo real del
	/// <c>tModLoader.dll</c> instalado). Asi que un objetivo puede pedir
	/// <c>"CalamityMod/AerialiteOre"</c> exactamente igual que <c>"Gold"</c>, y si ese mod no esta
	/// instalado el objetivo simplemente se queda sin resolver en vez de reventar.
	/// </remarks>
	public class ObjetivoBusqueda
	{
		/// <summary>Clave interna de este objetivo (estable, sin tildes). Es la ultima parte de su
		/// clave de localizacion: <c>Exploracion.Objetivo.&lt;Clave&gt;</c>.</summary>
		public string Clave;

		/// <summary>Clave interna de la categoria a la que pertenece. Se usa para agrupar y para
		/// elegir el color del marcador; el rotulo sale de <see cref="CategoriaLegible"/>.</summary>
		public string Categoria;

		public ClaseDeObjetivo Clase;

		/// <summary>Rotulo propio del objetivo, traducido al idioma activo. Es una propiedad y no
		/// un campo porque el catalogo se construye una sola vez y se cachea.</summary>
		public string Etiqueta {
			get { return Idiomas.Texto("Exploracion.Objetivo." + Clave); }
		}

		/// <summary>Nombre de la categoria, traducido al idioma activo.</summary>
		public string CategoriaLegible {
			get { return Idiomas.Texto("Exploracion.Categoria." + Categoria); }
		}

		/// <summary>Nombres a resolver (varios cuando una misma cosa tiene variantes, p.ej. los
		/// dos minerales alternativos de un mismo nivel).</summary>
		public string[] Nombres;

		/// <summary>Para <see cref="ClaseDeObjetivo.Liquido"/>: el id de <see cref="LiquidID"/>.</summary>
		public int Liquido;

		/// <summary>Tipos ya resueltos en esta partida. Vacio = el objetivo no existe aqui.</summary>
		public readonly List<int> Tipos = new List<int>();

		public bool Resuelto {
			get {
				return Clase == ClaseDeObjetivo.Cofres || Clase == ClaseDeObjetivo.Npcs
					|| Clase == ClaseDeObjetivo.Liquido || Tipos.Count > 0;
			}
		}

		/// <summary>Nombre para enseñar: el del propio juego cuando lo hay (traducido al idioma
		/// activo), y si no el que trae el objetivo.</summary>
		public string EtiquetaLegible()
		{
			if (Clase == ClaseDeObjetivo.Tile && Tipos.Count > 0) {
				string delJuego = NombreDeTile(Tipos[0]);
				if (!string.IsNullOrEmpty(delJuego)) {
					return delJuego;
				}
			}
			return Etiqueta;
		}

		/// <summary>
		/// Nombre real del tile segun el juego. Para vanilla sale del mapa
		/// (<c>Lang.GetMapObjectName(MapHelper.TileToLookup(...))</c>, que es lo que enseña el
		/// tooltip del mapa y esta traducido); para los tiles de mods, del propio
		/// <c>ModTile.Name</c>.
		/// </summary>
		public static string NombreDeTile(int tipo)
		{
			ModTile deMod = TileLoader.GetTile(tipo);
			if (deMod != null) {
				return deMod.Name;
			}

			try {
				return Lang.GetMapObjectName(MapHelper.TileToLookup(tipo, 0));
			}
			catch {
				return null;
			}
		}
	}

	/// <summary>
	/// Catalogo de lo que el panel ofrece buscar. Es una lista curada a mano (no un archivo de
	/// datos): son ids del juego, no datos de Terrakeep, y ponerlos en un .json solo añadiria un
	/// archivo que mantener sin ganar nada.
	/// </summary>
	public static class CatalogoObjetivos
	{
		private static List<ObjetivoBusqueda> _todos;

		public static IReadOnlyList<ObjetivoBusqueda> Todos {
			get {
				if (_todos == null) {
					Construir();
				}
				return _todos;
			}
		}

		public static void Descargar()
		{
			_todos = null;
		}

		/// <summary>Categorias en el orden en que se enseñan.</summary>
		public static List<string> Categorias()
		{
			List<string> orden = new List<string>();
			foreach (ObjetivoBusqueda objetivo in Todos) {
				if (!orden.Contains(objetivo.Categoria)) {
					orden.Add(objetivo.Categoria);
				}
			}
			return orden;
		}

		public static List<ObjetivoBusqueda> DeCategoria(string categoria)
		{
			List<ObjetivoBusqueda> lista = new List<ObjetivoBusqueda>();
			foreach (ObjetivoBusqueda objetivo in Todos) {
				if (objetivo.Categoria == categoria && objetivo.Resuelto) {
					lista.Add(objetivo);
				}
			}
			return lista;
		}

		private static void Construir()
		{
			_todos = new List<ObjetivoBusqueda>();

			// --- Minerales -------------------------------------------------------------------
			// Los pares alternativos (cobre/estaño, hierro/plomo...) van juntos a proposito: un
			// mundo concreto solo tiene uno de los dos, y buscarlos por separado obligaria al
			// usuario a adivinar cual le toco.
			Tile("Minerales", "CobreEstano", "Copper", "Tin");
			Tile("Minerales", "HierroPlomo", "Iron", "Lead");
			Tile("Minerales", "PlataTungsteno", "Silver", "Tungsten");
			Tile("Minerales", "OroPlatino", "Gold", "Platinum");
			Tile("Minerales", "DemonitaCarmesita", "Demonite", "Crimtane");
			Tile("Minerales", "Meteorito", "Meteorite");
			Tile("Minerales", "Obsidiana", "Obsidian");
			Tile("Minerales", "PiedraInfernal", "Hellstone");
			Tile("Minerales", "CobaltoPaladio", "Cobalt", "Palladium");
			Tile("Minerales", "MithrilOricalco", "Mythril", "Orichalcum");
			Tile("Minerales", "AdamantitaTitanio", "Adamantite", "Titanium");
			Tile("Minerales", "Clorofita", "Chlorophyte");
			Tile("Minerales", "Luminita", "LunarOre");
			Tile("Minerales", "FosilesDesierto", "DesertFossil");

			// --- Gemas -----------------------------------------------------------------------
			Tile("Gemas", "Amatista", "Amethyst");
			Tile("Gemas", "Topacio", "Topaz");
			Tile("Gemas", "Zafiro", "Sapphire");
			Tile("Gemas", "Esmeralda", "Emerald");
			Tile("Gemas", "Rubi", "Ruby");
			Tile("Gemas", "Diamante", "Diamond");
			Tile("Gemas", "GemasIncrustadas", "Crystals");

			// --- Tesoros y estructuras -------------------------------------------------------
			Tile("Tesoros", "CorazonesCristal", "Heart");
			Tile("Tesoros", "FrutosVida", "LifeFruit");
			Tile("Tesoros", "OrbesSombra", "ShadowOrbs");
			Tile("Tesoros", "AltaresDemoniacos", "DemonAltar");
			Tile("Tesoros", "AltarLihzahrd", "LihzahrdAltar");
			Tile("Tesoros", "BulbosPlantera", "PlanteraBulb");
			Tile("Tesoros", "Colmenas", "Hive");
			Tile("Tesoros", "LarvasAbeja", "Larva");

			// --- Contenedores y NPC ----------------------------------------------------------
			_todos.Add(new ObjetivoBusqueda {
				Clave = "Cofres",
				Categoria = "Contenedores",
				Clase = ClaseDeObjetivo.Cofres
			});
			_todos.Add(new ObjetivoBusqueda {
				Clave = "Npcs",
				Categoria = "Contenedores",
				Clase = ClaseDeObjetivo.Npcs
			});

			// --- Liquidos --------------------------------------------------------------------
			Liquido("Liquidos", "Agua", LiquidID.Water);
			Liquido("Liquidos", "Lava", LiquidID.Lava);
			Liquido("Liquidos", "Miel", LiquidID.Honey);
			Liquido("Liquidos", "Fulgor", LiquidID.Shimmer);

			// --- Paredes ---------------------------------------------------------------------
			// Las paredes que GENERA el mundo son las "Unsafe" (las que no dejan aparecer enemigos
			// son las colocadas por el jugador), asi que son esas las que hay que buscar.
			Pared("Paredes", "Mazmorra", "BlueDungeonUnsafe", "GreenDungeonUnsafe", "PinkDungeonUnsafe",
				"BlueDungeonSlabUnsafe", "BlueDungeonTileUnsafe", "PinkDungeonSlabUnsafe",
				"PinkDungeonTileUnsafe", "GreenDungeonSlabUnsafe", "GreenDungeonTileUnsafe");
			Pared("Paredes", "TemploLihzahrd", "LihzahrdBrickUnsafe");
			Pared("Paredes", "NidoArana", "SpiderUnsafe");

			foreach (ObjetivoBusqueda objetivo in _todos) {
				Resolver(objetivo);
			}
		}

		private static void Tile(string categoria, string clave, params string[] nombres)
		{
			_todos.Add(new ObjetivoBusqueda {
				Clave = clave,
				Categoria = categoria,
				Clase = ClaseDeObjetivo.Tile,
				Nombres = nombres
			});
		}

		private static void Pared(string categoria, string clave, params string[] nombres)
		{
			_todos.Add(new ObjetivoBusqueda {
				Clave = clave,
				Categoria = categoria,
				Clase = ClaseDeObjetivo.Pared,
				Nombres = nombres
			});
		}

		private static void Liquido(string categoria, string clave, int liquido)
		{
			_todos.Add(new ObjetivoBusqueda {
				Clave = clave,
				Categoria = categoria,
				Clase = ClaseDeObjetivo.Liquido,
				Liquido = liquido
			});
		}

		private static void Resolver(ObjetivoBusqueda objetivo)
		{
			objetivo.Tipos.Clear();
			if (objetivo.Nombres == null) {
				return;
			}

			foreach (string nombre in objetivo.Nombres) {
				int tipo;
				bool encontrado = objetivo.Clase == ClaseDeObjetivo.Pared
					? WallID.Search.TryGetId(nombre, out tipo)
					: TileID.Search.TryGetId(nombre, out tipo);
				if (encontrado && !objetivo.Tipos.Contains(tipo)) {
					objetivo.Tipos.Add(tipo);
				}
			}
		}

		/// <summary>Resumen para el log: cuantos objetivos hay y cuantos se han resuelto.</summary>
		public static string Resumen()
		{
			int resueltos = 0;
			foreach (ObjetivoBusqueda objetivo in Todos) {
				if (objetivo.Resuelto) {
					resueltos++;
				}
			}
			return Todos.Count + " objetivos en " + Categorias().Count + " categorias, " +
				resueltos + " resueltos en esta partida.";
		}
	}
}
