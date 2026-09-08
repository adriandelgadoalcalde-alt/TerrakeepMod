using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terrakeep.Core.Data;

namespace TerrakeepMod.Common.Libreria
{
	/// <summary>
	/// Catalogo de objetos de la Libreria, generado <b>EN VIVO</b> a partir de
	/// <see cref="ContentSamples.ItemsByType"/> - la instancia real de cada <see cref="Item"/>
	/// que el propio juego ya tiene cargada en memoria.
	/// <para />
	/// <b>Esta es la pieza que WS2 dejo a proposito sin hacer</b> (ver el comentario de cabecera
	/// de <c>LiveItemTreeBuilder.cs</c> en <c>Terrakeep.Core</c>): Core no puede depender
	/// de Terraria/tModLoader (dejaria de compilar para net10.0, que es lo que consume la app de
	/// escritorio), asi que la extraccion tiene que vivir forzosamente en el lado del mod. Lo que
	/// sale de aqui son <see cref="LiveItemInfo"/>, el DTO neutral que Core entiende.
	/// </summary>
	/// <remarks>
	/// <b>Por que en vivo y no el <c>calamity/catalog.json</c> estatico de la app de escritorio</b>
	/// (decision del plan del proyecto, no de aqui): asi la Libreria del mod cubre
	/// automaticamente <b>cualquier mod instalado</b>, no solo Calamity, y sin que nadie tenga
	/// que regenerar un catalogo cada vez que Calamity saca version. De paso desaparecen los ids
	/// sinteticos (<c>CalamityIds.ItemIdBase</c>) que la app necesitaba para no chocar con los
	/// vanilla: aqui el id es el <c>Item.type</c> real de la partida.
	/// <para />
	/// El catalogo se construye <b>perezosamente, la primera vez que se abre el panel</b>, no en
	/// <c>PostSetupContent</c>: recorrer ~8000 objetos y plegar sus nombres no tiene por que
	/// alargar la carga del juego para alguien que no llegue a abrir la Libreria. Los tooltips,
	/// ademas, se pliegan solo cuando alguien busca con <c>.texto</c> (la mayoria de busquedas no
	/// los toca nunca).
	/// </remarks>
	public static class CatalogoVivo
	{
		/// <summary>Nombre con el que se identifica al contenido del juego base.</summary>
		public const string ModVanilla = "Terraria";

		private static List<LiveItemInfo> _objetos;
		private static string[] _nombre;
		private static string[] _nombrePlegado;
		private static string[] _tooltipPlegado;
		private static string[] _modDe;

		/// <summary>true si el catalogo ya esta construido.</summary>
		public static bool Listo {
			get { return _objetos != null; }
		}

		/// <summary>Objetos descubiertos, en orden de id. Vacio si no se ha construido aun.</summary>
		public static IReadOnlyList<LiveItemInfo> Objetos {
			get { return _objetos != null ? (IReadOnlyList<LiveItemInfo>)_objetos : new List<LiveItemInfo>(); }
		}

		/// <summary>Milisegundos reales que tardo la ultima construccion. Va al log de evidencia.</summary>
		public static double MilisegundosConstruccion { get; private set; }

		/// <summary>Cuantos objetos vienen de un mod (no del juego base).</summary>
		public static int DeMods { get; private set; }

		/// <summary>
		/// Construye el catalogo si hace falta. Idempotente: llamarlo dos veces no rehace nada.
		/// </summary>
		public static void ConstruirSiHaceFalta()
		{
			if (_objetos != null) {
				return;
			}

			Stopwatch reloj = Stopwatch.StartNew();

			int tope = ItemLoader.ItemCount;
			_nombre = new string[tope];
			_nombrePlegado = new string[tope];
			_tooltipPlegado = new string[tope];
			_modDe = new string[tope];
			_objetos = new List<LiveItemInfo>(tope);
			DeMods = 0;

			for (int tipo = 1; tipo < tope; tipo++) {
				Item muestra;
				if (!ContentSamples.ItemsByType.TryGetValue(tipo, out muestra) || muestra == null || muestra.type != tipo) {
					continue;
				}

				// El nombre que se enseña es el que enseña el juego, con el idioma que tenga
				// puesto ahora mismo: ni una tabla propia de nombres ni una traduccion inventada.
				string nombre = muestra.Name;
				if (string.IsNullOrEmpty(nombre)) {
					continue;   // Objeto sin nombre real: no hay nada que enseñar de el.
				}

				// ItemLoader.GetItem devuelve null para todo lo que sea vanilla (comprobado en el
				// tModLoader.dll instalado: `if (type < ItemID.Count) return null`), asi que esto
				// distingue el juego base de cualquier mod sin listas de nombres a mano.
				ModItem deMod = ItemLoader.GetItem(tipo);
				string modPropietario = deMod != null && deMod.Mod != null ? deMod.Mod.Name : ModVanilla;

				_nombre[tipo] = nombre;
				_nombrePlegado[tipo] = GramaticaBusqueda.Plegar(nombre);
				_modDe[tipo] = modPropietario;

				if (modPropietario != ModVanilla) {
					DeMods++;
				}

				_objetos.Add(new LiveItemInfo(tipo, nombre, modPropietario, Categorizar(muestra)) {
					EquipSlot = RanuraDe(muestra),
					Rarity = muestra.rare
				});
			}

			reloj.Stop();
			MilisegundosConstruccion = reloj.Elapsed.TotalMilliseconds;
		}

		/// <summary>
		/// Tira el catalogo. Hace falta al cambiar de idioma (WS7 permite cambiarlo en vivo y los
		/// nombres de los objetos cambian con el) y al descargar el mod (los ids dejan de valer).
		/// </summary>
		public static void Invalidar()
		{
			_objetos = null;
			_nombre = null;
			_nombrePlegado = null;
			_tooltipPlegado = null;
			_modDe = null;
		}

		/// <summary>Nombre real del objeto tal cual lo llama el juego, o "" si no existe.</summary>
		public static string Nombre(int tipo)
		{
			return _nombre != null && tipo > 0 && tipo < _nombre.Length && _nombre[tipo] != null ? _nombre[tipo] : "";
		}

		/// <summary>Nombre ya plegado para comparar (sin acentos, en minusculas).</summary>
		public static string NombrePlegado(int tipo)
		{
			return _nombrePlegado != null && tipo > 0 && tipo < _nombrePlegado.Length && _nombrePlegado[tipo] != null
				? _nombrePlegado[tipo] : "";
		}

		/// <summary>
		/// true si <paramref name="tipo"/> es un objeto REAL del juego base <b>en la version de
		/// Terraria que esta corriendo ahora mismo</b>.
		/// <para />
		/// Las dos condiciones son necesarias y ninguna sobra:
		/// <list type="bullet">
		/// <item><c>tipo &lt; ItemID.Count</c> deja fuera los ids de MODS. tModLoader reparte los
		/// ids de los mods a partir de <c>ItemID.Count</c>, asi que un id vanilla inventado (o
		/// heredado de una version mas nueva del juego) cae justo encima de un objeto de Calamity y
		/// se colaria en una carpeta vanilla como si fuera suyo.</item>
		/// <item>Tener nombre real en el catalogo deja fuera los huecos: no todos los ids por debajo
		/// de <c>ItemID.Count</c> son un objeto de verdad.</item>
		/// </list>
		/// Lo usa <see cref="ArbolLibreria"/> para podar el arbol vanilla curado, que se extrajo de
		/// Terraria <b>1.4.5.8</b> (<c>ItemID.Count = 6196</c>) mientras que tModLoader va por
		/// <b>1.4.4.9</b> (<c>ItemID.Count = 5456</c>) - ver la cabecera de esa clase.
		/// </summary>
		public static bool EsVanillaReal(int tipo)
		{
			return tipo > 0 && tipo < ItemID.Count
				&& _nombre != null && tipo < _nombre.Length && _nombre[tipo] != null;
		}

		/// <summary>Mod dueño del objeto ("Terraria" si es del juego base).</summary>
		public static string ModDe(int tipo)
		{
			return _modDe != null && tipo > 0 && tipo < _modDe.Length && _modDe[tipo] != null ? _modDe[tipo] : ModVanilla;
		}

		/// <summary>
		/// Tooltip del objeto ya plegado, calculado la primera vez que se pide. Sale de
		/// <c>Lang.GetTooltip(tipo)</c>, que tModLoader rellena TAMBIEN para los objetos de mods
		/// (<c>ItemLoader.ResizeArrays</c> hace <c>Lang._itemTooltipCache[item.Type] =
		/// ItemTooltip.FromLocalization(item.Tooltip)</c>, codigo real del dll instalado).
		/// </summary>
		public static string TooltipPlegado(int tipo)
		{
			if (_tooltipPlegado == null || tipo <= 0 || tipo >= _tooltipPlegado.Length) {
				return "";
			}
			if (_tooltipPlegado[tipo] != null) {
				return _tooltipPlegado[tipo];
			}

			string bruto = "";
			try {
				ItemTooltip tooltip = Lang.GetTooltip(tipo);
				if (tooltip != null && tooltip.Lines > 0) {
					System.Text.StringBuilder sb = new System.Text.StringBuilder();
					for (int i = 0; i < tooltip.Lines; i++) {
						if (i > 0) {
							sb.Append(' ');
						}
						sb.Append(tooltip.GetLine(i));
					}
					bruto = sb.ToString();
				}
			}
			catch (Exception) {
				// Un mod con una clave de localizacion rota no puede tumbar la busqueda entera:
				// ese objeto se queda sin texto donde buscar y ya.
			}

			_tooltipPlegado[tipo] = GramaticaBusqueda.Plegar(bruto);
			return _tooltipPlegado[tipo];
		}

		/// <summary>
		/// Categoria (ruta de carpeta) de un objeto, deducida de sus campos REALES.
		/// <para />
		/// Las claves que devuelve son deliberadamente las MISMAS que usa
		/// <c>calamity/catalog.json</c> en la app de escritorio ("Weapons/Melee",
		/// "Accessories/Wings", "Placeables/Furniture"...), porque asi se puede reutilizar tal
		/// cual <c>LibraryTreeBuilder.CalamityCategoryLabel</c> de Core como traductor de
		/// etiquetas: ya trae las 121 categorias reales traducidas al español, y para cualquier
		/// clave que no conozca separa el CamelCase y la deja legible en vez de inventarse nada.
		/// <para />
		/// El orden de las comprobaciones importa: un objeto puede cumplir varias (una espada es
		/// arma y ademas material), y se queda con la primera, igual que hace el catalogo real.
		/// </summary>
		public static string Categorizar(Item it)
		{
			// 1. Armadura y vanidad de armadura: lo mas identificable de un objeto.
			if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) {
				return it.vanity ? "Armor/Vanity" : "Armor";
			}

			// 2. Accesorios. Las alas y la vanidad tienen carpeta propia, igual que en el catalogo
			// real de Calamity.
			if (it.accessory) {
				if (it.wingSlot > 0) {
					return "Accessories/Wings";
				}
				return it.vanity ? "Accessories/Vanity" : "Accessories";
			}

			// 3. Monturas y vagonetas (mountType real, no el nombre del objeto).
			if (it.mountType >= 0) {
				return MountID.Sets.Cart != null && it.mountType < MountID.Sets.Cart.Length && MountID.Sets.Cart[it.mountType]
					? "Mounts/Minecarts" : "Mounts";
			}

			// 4. Mascotas de vanidad y de luz: se reconocen por el buff que dan, que es lo que el
			// propio juego mira (Main.vanityPet / Main.lightPet, indexados por buffType).
			if (it.buffType > 0) {
				if (Main.vanityPet != null && it.buffType < Main.vanityPet.Length && Main.vanityPet[it.buffType]) {
					return "Pets";
				}
				if (Main.lightPet != null && it.buffType < Main.lightPet.Length && Main.lightPet[it.buffType]) {
					return "Pets";
				}
			}

			// 5. Tintes. hairDye vale -1 cuando no es tinte de pelo (valor por defecto real).
			if (it.hairDye >= 0) {
				return "Dyes/HairDye";
			}
			if (it.dye > 0) {
				return "Dyes";
			}

			// 6. Municion. Va antes que las armas porque una flecha tiene daño y no es un arma.
			if (it.ammo != AmmoID.None) {
				return "Ammo";
			}

			// 7. Herramientas de verdad (pico/hacha/martillo) antes que "arma": casi todas hacen
			// daño y si no se acabarian todas en "Armas - Cuerpo a cuerpo".
			if (it.pick > 0 || it.axe > 0 || it.hammer > 0) {
				return "Tools";
			}

			// 8. Pesca. Ojo: fishingPole vale 1 por defecto en un Item recien creado (campo real
			// `public int fishingPole = 1;`), asi que la caña de verdad es > 1.
			if (it.fishingPole > 1) {
				return "Fishing/FishingRods";
			}
			if (it.bait > 0) {
				return "Fishing";
			}
			if (it.questItem) {
				return "Fishing";
			}

			// 9. Armas, por su clase de daño REAL. DamageType es un DamageClass, asi que las
			// clases que añaden los mods (el Picaro de Calamity, por ejemplo) salen solas con su
			// nombre traducido por el propio mod, sin ninguna tabla nuestra.
			if (it.damage > 0) {
				return "Weapons/" + NombreClaseDano(it);
			}

			// 10. Colocables. Se separan muebles de bloques con Main.tileFrameImportant, que es
			// justo lo que distingue un tile con marco propio (mesa, cofre, estatua) de un bloque
			// liso.
			if (it.createWall >= 0) {
				return "Placeables/Walls";
			}
			if (it.createTile >= 0) {
				bool mueble = Main.tileFrameImportant != null
					&& it.createTile < Main.tileFrameImportant.Length
					&& Main.tileFrameImportant[it.createTile];
				return mueble ? "Placeables/Furniture" : "Placeables";
			}

			// 11. Consumibles con efecto (pociones, comida, buffs).
			if (it.potion || (it.consumable && it.buffType > 0)) {
				return "Potions";
			}

			// 12. Invocadores de jefes y de eventos: consumibles que hacen aparecer un NPC.
			if (it.makeNPC > 0) {
				return "Critters";
			}

			if (it.paint > 0) {
				return "Dyes";
			}

			if (it.material) {
				return "Materials";
			}

			return "Misc";
		}

		/// <summary>
		/// Nombre de carpeta para la clase de daño de un arma. Las cinco vanilla se mapean a la
		/// misma clave que usa el catalogo real de la app ("Melee", "Ranged"...); cualquier otra
		/// (las de mods) usa su propio <c>DisplayName</c>, que es como el mod quiere que se llame.
		/// </summary>
		private static string NombreClaseDano(Item it)
		{
			DamageClass clase = it.DamageType;
			if (clase == null) {
				return "Typeless";
			}
			if (clase == DamageClass.Melee || clase == DamageClass.MeleeNoSpeed) {
				return "Melee";
			}
			if (clase == DamageClass.Ranged) {
				return "Ranged";
			}
			if (clase == DamageClass.Magic) {
				return "Magic";
			}
			if (clase == DamageClass.Summon || clase == DamageClass.SummonMeleeSpeed) {
				return "Summon";
			}
			if (clase == DamageClass.Throwing) {
				return "Throwing";
			}
			if (clase == DamageClass.Default || clase == DamageClass.Generic) {
				return "Typeless";
			}

			string nombre = null;
			try {
				nombre = clase.DisplayName != null ? clase.DisplayName.Value : null;
			}
			catch (Exception) {
				// Una clase de daño de un mod sin localizacion no puede romper el catalogo.
			}
			return string.IsNullOrWhiteSpace(nombre) ? clase.Name : nombre;
		}

		/// <summary>
		/// Ranura de equipo que ocupa el objeto, si ocupa alguna. Solo informativo: viaja en el
		/// <see cref="LiveItemInfo.EquipSlot"/> y se enseña en la ficha del objeto.
		/// </summary>
		private static string RanuraDe(Item it)
		{
			if (it.headSlot >= 0) return "Cabeza";
			if (it.bodySlot >= 0) return "Cuerpo";
			if (it.legSlot >= 0) return "Piernas";
			if (it.wingSlot > 0) return "Alas";
			if (it.shieldSlot >= 0) return "Escudo";
			if (it.neckSlot >= 0) return "Cuello";
			if (it.faceSlot >= 0) return "Cara";
			if (it.backSlot >= 0) return "Espalda";
			if (it.frontSlot >= 0) return "Frente";
			if (it.shoeSlot >= 0) return "Pies";
			if (it.waistSlot >= 0) return "Cintura";
			if (it.balloonSlot >= 0) return "Globo";
			if (it.handOnSlot >= 0) return "Mano";
			if (it.beardSlot >= 0) return "Barba";
			if (it.accessory) return "Accesorio";
			return null;
		}
	}
}
