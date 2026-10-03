using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// "Cómo conseguirlo" de un objeto (punto 3c del encargo del usuario: "lo mejor sería que la
	/// guía te guiara para tener todo"). Monta las lineas de la ficha de objeto con marcado de la
	/// guia ({o:}, {n:}, **negrita**), para que cada ingrediente, enemigo, bolsa o vendedor sea a su
	/// vez clicable y se pueda recorrer la cadena entera.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Fuente principal: <c>RefObjeto.Obtencion</c> de la tabla de referencias de Core, extraida del
	/// codigo decompilado real (recetas, botin, bolsas y tiendas, con archivo:linea). Encima, lo que
	/// solo se sabe EN VIVO: cuantas unidades de cada ingrediente tienes ya (inventario, equipo y
	/// huchas) y si la estacion esta a tu alcance ahora mismo (<c>Player.adjTile</c>, lo mismo que
	/// mira el juego para dejarte fabricar) o la llevas encima para colocarla.
	/// </para>
	/// <para>
	/// Si la tabla no tiene nada (objetos de tercer nivel, o que no salen de recetas/botin/tiendas),
	/// se miran las recetas VIVAS de la partida (<c>Main.recipe</c>, que incluye las de Calamity y
	/// cualquier otro mod cargado); si tampoco hay, se dice honestamente que se consigue en el mundo
	/// (cofres, mineria, pesca, eventos), nunca se inventa una fuente.
	/// </para>
	/// </remarks>
	public static class ObtencionGuia
	{
		public struct Linea
		{
			public string Texto;
			public Color Color;
			public float Escala;
			public bool EsTitulo;
		}

		public sealed class Ficha
		{
			public string Ref = "";
			public int Tipo;
			public List<Linea> Lineas = new List<Linea>();
			/// <summary>true si alguna receta pide un altar (para ofrecer "Ver el altar más cercano").</summary>
			public bool PideAltar;
		}

		public static Ficha Construir(string referencia, int cantidadNecesaria)
		{
			Ficha f = new Ficha { Ref = referencia ?? "", Tipo = GuiaV2Sistema.TipoObjeto(referencia) };
			int tienes = f.Tipo > 0 ? ProveedorEstadoGuiaV2Mod.CuantosPoseeDe(Main.LocalPlayer, f.Tipo) : 0;

			if (f.Tipo <= 0) {
				Anadir(f, Idiomas.Texto("GuiaV2.Ficha.NoExiste"), Colores.Peligro, 0.8f);
			}
			else if (cantidadNecesaria > 1) {
				Anadir(f, Idiomas.Texto("GuiaV2.Ficha.TienesDe", tienes, cantidadNecesaria),
					tienes >= cantidadNecesaria ? Colores.Correcto : Colores.Aviso, 0.82f);
			}
			else {
				Anadir(f, Idiomas.Texto(tienes > 0 ? "GuiaV2.Ficha.YaLoTienes" : "GuiaV2.Ficha.NoLoTienes", tienes),
					tienes > 0 ? Colores.Correcto : Colores.Aviso, 0.82f);
			}

			RefObjeto o = null;
			if (GuiaV2Sistema.Refs != null && referencia != null) {
				GuiaV2Sistema.Refs.Objetos.TryGetValue(referencia, out o);
			}

			bool algo = false;
			// La tabla es comun a las dos guias y trae recetas/botin que AÑADE Calamity: en una partida
			// sin Calamity solo se enseña lo vanilla (RefObjeto.ObtencionPara, seccion 12 del diseño).
			List<Obtencion> obtencion = o != null
				? new List<Obtencion>(o.ObtencionPara(GuiaV2Sistema.IdGuia == "calamity" ? "calamity" : "vanilla"))
				: new List<Obtencion>();
			if (obtencion.Count > 0) {
				AnadirTitulo(f, Idiomas.Texto("GuiaV2.Ficha.ComoConseguirlo"));
				foreach (Obtencion ob in obtencion) {
					AnadirObtencion(f, ob);
				}
				algo = true;
			}
			else if (f.Tipo > 0) {
				// Sin datos en la tabla: recetas vivas de la partida.
				int n = 0;
				for (int i = 0; i < Recipe.numRecipes && n < 4; i++) {
					Recipe r = Main.recipe[i];
					if (r == null || r.createItem == null || r.createItem.type != f.Tipo || r.Disabled) {
						continue;
					}
					if (n == 0) {
						AnadirTitulo(f, Idiomas.Texto("GuiaV2.Ficha.ComoConseguirlo"));
					}
					AnadirRecetaViva(f, r);
					n++;
					algo = true;
				}
			}

			if (!algo && f.Tipo > 0) {
				AnadirTitulo(f, Idiomas.Texto("GuiaV2.Ficha.ComoConseguirlo"));
				Anadir(f, Idiomas.Texto("GuiaV2.Ficha.EnElMundo"), Colores.Suave, 0.78f);
			}
			return f;
		}

		private static void AnadirObtencion(Ficha f, Obtencion ob)
		{
			switch (ob.Tipo) {
				case "receta": {
					StringBuilder sb = new StringBuilder();
					sb.Append("**").Append(Idiomas.Texto("GuiaV2.Ficha.Receta")).Append("**");
					if (ob.CantidadResultado > 1) {
						sb.Append(" (×").Append(ob.CantidadResultado).Append(')');
					}
					sb.Append(": ");
					for (int i = 0; i < ob.Ingredientes.Count; i++) {
						Ingrediente ing = ob.Ingredientes[i];
						if (i > 0) sb.Append(" + ");
						sb.Append(ing.Cantidad).Append(" × ");
						if (ing.Ref != null) {
							sb.Append("{o:").Append(ing.Ref).Append('}');
							int t = GuiaV2Sistema.TipoObjeto(ing.Ref);
							if (t > 0) {
								sb.Append(' ').Append(Idiomas.Texto("GuiaV2.Ficha.TienesCorto",
									ProveedorEstadoGuiaV2Mod.CuantosPoseeDe(Main.LocalPlayer, t)));
							}
						}
						else {
							sb.Append(GuiaV2Sistema.NombreGrupo(ing.Grupo));
							int g = CuantosDelGrupo(ing.Grupo);
							if (g >= 0) {
								sb.Append(' ').Append(Idiomas.Texto("GuiaV2.Ficha.TienesCorto", g));
							}
						}
					}
					Anadir(f, sb.ToString(), Color.White, 0.8f);

					if (ob.Estaciones.Count == 0) {
						Anadir(f, Idiomas.Texto("GuiaV2.Ficha.SinEstacion"), Colores.Suave, 0.74f);
					}
					foreach (string est in ob.Estaciones) {
						if (est.EndsWith("/DemonAltar", StringComparison.Ordinal)) {
							f.PideAltar = true;
						}
						string estado;
						Color color;
						EstadoEstacion(est, out estado, out color);
						Anadir(f, Idiomas.Texto("GuiaV2.Ficha.Estacion", GuiaV2Sistema.NombreEstacion(est), estado), color, 0.74f);
					}
					foreach (string c in ob.Condiciones) {
						string legible = Legible(c);
						if (legible.Length > 0) {
							Anadir(f, Idiomas.Texto("GuiaV2.Ficha.Condicion", legible), Colores.Suave, 0.72f);
						}
					}
					break;
				}
				case "botin":
					Anadir(f, Idiomas.Texto("GuiaV2.Ficha.Botin", "{n:" + ob.De + "}", ob.Probabilidad) + SufijoCondicion(ob.Condicion), Color.White, 0.8f);
					break;
				case "bolsa":
					Anadir(f, Idiomas.Texto("GuiaV2.Ficha.Bolsa", "{o:" + ob.De + "}", ob.Probabilidad) + SufijoCondicion(ob.Condicion), Color.White, 0.8f);
					break;
				case "tienda":
					Anadir(f, Idiomas.Texto("GuiaV2.Ficha.Tienda", "{n:" + ob.De + "}") + SufijoCondicion(ob.Condicion), Color.White, 0.8f);
					break;
				default: {
					string otra = Legible(ob.Condicion);
					if (otra.Length > 0) {
						Anadir(f, otra, Color.White, 0.78f);
					}
					break;
				}
			}
			// Parche 0.8.1: la linea "dato del código del juego: archivo.cs:NN" ya no se enseña (era un
			// dato para desarrolladores). El archivo:linea sigue en la tabla de referencias (ob.FuenteCodigo).
		}

		private static void AnadirRecetaViva(Ficha f, Recipe r)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("**").Append(Idiomas.Texto("GuiaV2.Ficha.Receta")).Append("**");
			if (r.createItem.stack > 1) {
				sb.Append(" (×").Append(r.createItem.stack).Append(')');
			}
			sb.Append(": ");
			for (int i = 0; i < r.requiredItem.Count; i++) {
				Item ing = r.requiredItem[i];
				if (ing == null || ing.IsAir) continue;
				if (i > 0) sb.Append(" + ");
				string rf = GuiaV2Sistema.RefDeTipo(ing.type);
				sb.Append(ing.stack).Append(" × {o:").Append(rf).Append("} ");
				sb.Append(Idiomas.Texto("GuiaV2.Ficha.TienesCorto", ProveedorEstadoGuiaV2Mod.CuantosPoseeDe(Main.LocalPlayer, ing.type)));
			}
			Anadir(f, sb.ToString(), Color.White, 0.8f);
			foreach (int tile in r.requiredTile) {
				if (tile < 0) continue;
				if (tile == TileID.DemonAltar) f.PideAltar = true;
				string nombre = Lang.GetMapObjectName(MapHelperSeguro(tile));
				if (string.IsNullOrEmpty(nombre)) nombre = "#" + tile;
				string estado; Color color;
				EstadoTile(tile, out estado, out color);
				Anadir(f, Idiomas.Texto("GuiaV2.Ficha.Estacion", nombre, estado), color, 0.74f);
			}
			Anadir(f, Idiomas.Texto("GuiaV2.Ficha.RecetaViva"), Colores.Tenue, 0.62f);
		}

		private static int MapHelperSeguro(int tile)
		{
			try {
				return Terraria.Map.MapHelper.TileToLookup(tile, 0);
			}
			catch (Exception) {
				return 0;
			}
		}

		// ---- estaciones --------------------------------------------------------------------------

		private static readonly Dictionary<string, int> _tilesEstacion = new Dictionary<string, int>();

		/// <summary>Tile real de una estacion de la tabla ("Terraria/Tile/Anvils" → TileID.Anvils por
		/// reflexion sobre las constantes de TileID; "CalamityMod/Tile/X" → ModTile). -1 si no.</summary>
		public static int TileDeEstacion(string clave)
		{
			int t;
			if (_tilesEstacion.TryGetValue(clave ?? "", out t)) {
				return t;
			}
			t = -1;
			if (clave != null) {
				string[] partes = clave.Split('/');
				if (partes.Length == 3 && partes[1] == "Tile") {
					if (partes[0] == "Terraria") {
						FieldInfo campo = typeof(TileID).GetField(partes[2], BindingFlags.Public | BindingFlags.Static);
						if (campo != null) {
							object v = campo.GetValue(null);
							t = Convert.ToInt32(v);
						}
					}
					else if (ModLoader.HasMod(partes[0])) {
						ModTile mt;
						if (ModContent.TryFind(partes[0], partes[2], out mt)) {
							t = mt.Type;
						}
					}
				}
			}
			_tilesEstacion[clave ?? ""] = t;
			return t;
		}

		private static void EstadoEstacion(string clave, out string estado, out Color color)
		{
			int tile = TileDeEstacion(clave);
			if (tile < 0) {
				estado = Idiomas.Texto("GuiaV2.Ficha.EstacionDesconocida");
				color = Colores.Suave;
				return;
			}
			EstadoTile(tile, out estado, out color);
		}

		private static void EstadoTile(int tile, out string estado, out Color color)
		{
			Player p = Main.LocalPlayer;
			if (p != null && p.active && tile < p.adjTile.Length && p.adjTile[tile]) {
				estado = Idiomas.Texto("GuiaV2.Ficha.EstacionCerca");
				color = Colores.Correcto;
				return;
			}
			if (tile == TileID.DemonAltar) {
				estado = Idiomas.Texto("GuiaV2.Ficha.EstacionAltar");
				color = Colores.Aviso;
				return;
			}
			if (LlevaColocable(p, tile)) {
				estado = Idiomas.Texto("GuiaV2.Ficha.EstacionLaLlevas");
				color = Colores.Correcto;
				return;
			}
			estado = Idiomas.Texto("GuiaV2.Ficha.EstacionNoLaTienes");
			color = Colores.Aviso;
		}

		/// <summary>true si el jugador lleva (inventario o huchas) algo que coloca ese tile o uno que
		/// cuenta como el (yunque de mitrilo/oricalco cuenta como yunque, etc., igual que adjTile).</summary>
		private static bool LlevaColocable(Player p, int tile)
		{
			if (p == null || !p.active) {
				return false;
			}
			List<int> validos = new List<int> { tile };
			if (tile == TileID.Anvils) validos.Add(TileID.MythrilAnvil);
			if (tile == TileID.Furnaces) { validos.Add(TileID.Hellforge); validos.Add(TileID.AdamantiteForge); }
			if (tile == TileID.Hellforge) validos.Add(TileID.AdamantiteForge);
			if (tile == TileID.WorkBenches) validos.Add(TileID.HeavyWorkBench);
			if (tile == TileID.Bottles) validos.Add(TileID.AlchemyTable);
			foreach (Item[] items in new[] { p.inventory, p.bank.item, p.bank2.item, p.bank3.item, p.bank4.item }) {
				if (items == null) continue;
				foreach (Item it in items) {
					if (it != null && !it.IsAir && it.createTile >= 0 && validos.Contains(it.createTile)) {
						return true;
					}
				}
			}
			return false;
		}

		private static int CuantosDelGrupo(string grupo)
		{
			if (string.IsNullOrEmpty(grupo)) {
				return -1;
			}
			int id;
			if (!RecipeGroup.recipeGroupIDs.TryGetValue(grupo, out id) &&
				!RecipeGroup.recipeGroupIDs.TryGetValue("Terraria:" + grupo, out id) &&
				!RecipeGroup.recipeGroupIDs.TryGetValue("CalamityMod:" + grupo, out id)) {
				return -1;
			}
			RecipeGroup g;
			if (!RecipeGroup.recipeGroups.TryGetValue(id, out g) || g == null) {
				return -1;
			}
			int total = 0;
			foreach (int t in g.ValidItems) {
				total += ProveedorEstadoGuiaV2Mod.CuantosPoseeDe(Main.LocalPlayer, t);
			}
			return total;
		}

		public static string Legible(string condicion) => GuiaV2Condiciones.Legible(condicion, Idiomas.EnEspanol);

		/// <summary>" · condición" para añadir a una línea, o nada si la condición del código no tiene
		/// una forma legible para el jugador (nunca se enseña código).</summary>
		private static string SufijoCondicion(string condicion)
		{
			string l = Legible(condicion);
			return l.Length == 0 ? "" : " · " + l;
		}

		private static void Anadir(Ficha f, string texto, Color color, float escala)
		{
			f.Lineas.Add(new Linea { Texto = texto, Color = color, Escala = escala });
		}

		private static void AnadirTitulo(Ficha f, string texto)
		{
			f.Lineas.Add(new Linea { Texto = texto, Color = Colores.Aviso, Escala = 0.86f, EsTitulo = true });
		}

		/// <summary>Colores de la ficha (los mismos tonos que EstiloTk, sin depender de la capa de UI).</summary>
		public static class Colores
		{
			public static readonly Color Correcto = new Color(140, 235, 160);
			public static readonly Color Aviso = new Color(255, 210, 120);
			public static readonly Color Peligro = new Color(235, 130, 130);
			public static readonly Color Suave = new Color(190, 200, 225);
			public static readonly Color Tenue = new Color(150, 158, 185);
		}
	}
}
