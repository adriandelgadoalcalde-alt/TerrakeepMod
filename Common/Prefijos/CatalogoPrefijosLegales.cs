using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using TerrasavrNative.Core.Calamity;
using TerrasavrNative.Core.Data;
// ARREGLO MINIMO AJENO (ver bitacora.md): 'PrefixCategory' existe TAMBIEN en Terraria.ModLoader
// (usado via el "using" de arriba) y eso volvia ambigua toda referencia sin calificar en este
// archivo, bloqueando la compilacion del proyecto ENTERO para cualquiera. Este archivo usa el
// tipo de Core (categorias con flags propias del catalogo real de prefijos), no el de
// Terraria.ModLoader.
using PrefixCategory = TerrasavrNative.Core.Data.PrefixCategory;

namespace TerrakeepMod.Common.Prefijos
{
	/// <summary>
	/// Que prefijos son LEGALES para un objeto real (vivo, del jugador), para el nuevo picker de
	/// prefijo de la Libreria. Encargo explícito del usuario: "investiga la vía real y correcta
	/// para saber qué prefijos son legales para una clase de objeto en concreto... no reinventes
	/// las reglas de legalidad a mano si ya existen" - la lógica es la MISMA que ya resolvió la
	/// app de escritorio hermana <c>Terrasavr-Native</c> (<see cref="PrefixRulesCatalog"/>,
	/// extraída del código decompilado real de <c>PrefixLegacy.cs</c>/<c>Item.cs</c>, ver su
	/// cabecera), reutilizada aquí tal cual vía <c>TerrasavrNative.Core</c> - nada reimplementado.
	/// </summary>
	/// <remarks>
	/// <b>Dos archivos copiados TAL CUAL</b> del repo hermano (mismo patrón que
	/// <see cref="CatalogoMejorPrefijo"/> con <c>best_prefix.json</c>):
	/// <c>Assets/vanilla_prefix_rules.json</c> (de <c>TerrasavrNative.App/Assets/
	/// vanilla_prefix_rules.json</c>) y <c>Assets/vanilla_prefix_effects.json</c> (mismo origen,
	/// mismo nombre) - el segundo es opcional, solo enriquece el tooltip de cada prefijo con su
	/// efecto real ("+15% de daño"...), nunca decide legalidad.
	/// <para />
	/// <b>Los agrupamientos con nombre (Biblioteca/Positivos/Negativos, "Cuerpo a cuerpo +"...)
	/// son <see cref="PrefixGroupCatalog"/> de Core</b>, código puro sin archivo de datos propio:
	/// se reutiliza literalmente la tabla que ya usa la app de escritorio para el selector manual
	/// de prefijo, con sus mismas 8+6 categorías y sus mismos nombres ES/EN.
	/// <para />
	/// <b>Objetos VANILLA</b>: <c>Item.type</c> YA ES el <c>ItemID</c> real que usa
	/// <see cref="PrefixRulesCatalog"/> (no hace falta ningún id sintético, a diferencia de los
	/// objetos de Calamity) - <see cref="PrefixRulesCatalog.VanillaCategories"/> da la categoría
	/// exacta por objeto (683 objetos reales tabulados, no solo por "es un arma").
	/// <para />
	/// <b>Objetos de MOD (Calamity incluido)</b>: <c>Item.type</c> es un id asignado en caliente
	/// por tModLoader según el orden de carga, así que NO coincide con el id sintético que usa
	/// <c>CalamityCatalog</c> de Core (pensado para <c>catalog.json</c>, offline) - traer ese
	/// catálogo entero solo para esto duplicaría el sistema de ids sintéticos que
	/// <c>CatalogoVivo</c> ya evitó a propósito (ver su cabecera: "de paso desaparecen los ids
	/// sinteticos"). En su lugar se detecta la categoría con los campos REALES que el <c>Item</c>
	/// ya tiene resueltos en esta partida (<c>item.accessory</c>, <c>item.DamageType</c>) - mismo
	/// criterio que ya usa <c>CatalogoVivo.Categorizar</c> para las carpetas de la Librería, y
	/// funciona para CUALQUIER mod, no solo Calamity. <c>item.DamageType</c> es la clase real de
	/// <c>tModLoader</c> (<c>MeleeDamageClass</c>/<c>RangedDamageClass</c>/<c>MagicDamageClass</c>/
	/// <c>SummonDamageClass</c>/y la <c>RogueDamageClass</c> real de Calamity si el mod está
	/// cargado) - se compara por el NOMBRE de su tipo .NET, no por una referencia directa a
	/// Calamity (este mod no depende de Calamity para compilar ni para cargar).
	/// <para />
	/// <b>Alcance deliberadamente SIN los 21 <c>ModPrefix</c> reales de Calamity</b> (17 de arma
	/// Pícaro + 4 de accesorio, ids sintéticos de Core &gt;= <see cref="CalamityIds.PrefixIdBase"/>):
	/// aplicarlos de verdad exigiría resolver su <c>Terraria.ModLoader.ModPrefix.Type</c> EN
	/// TIEMPO DE EJECUCIÓN de esta partida (vía <c>PrefixLoader</c>/<c>ModContent.Find</c>), algo
	/// que <see cref="CatalogoMejorPrefijo"/> YA decidió no hacer para el prefijo automático por
	/// el mismo motivo (ver su cabecera) - mismo criterio aquí, documentado igual, para no volver
	/// a reabrir la misma decisión sin nueva información. Los grupos "Invocación +/-" tampoco se
	/// pueden ofrecer aquí: sus ids (85-97) son de <c>PrefixID</c> de TerrariaVanilla 1.4.5.8 y no
	/// existen en el <c>Terraria.ID.PrefixID</c> real de la 1.4.4.9 instalada
	/// (<c>PrefixID.Count</c>=85) - mismo hallazgo real que ya documentó
	/// <see cref="CatalogoMejorPrefijo"/>, defendido aquí con la MISMA comprobación de rango
	/// (<c>id &lt; PrefixID.Count</c>), leyendo el valor real en vivo y no una constante fija.
	/// </remarks>
	public static class CatalogoPrefijosLegales
	{
		private const string RutaReglas = "Assets/vanilla_prefix_rules.json";
		private const string RutaEfectos = "Assets/vanilla_prefix_effects.json";

		private static byte[] _bytesReglas;
		private static byte[] _bytesEfectos;
		private static PrefixRulesCatalog _reglas;
		private static PrefixEffectCatalog _efectos;
		private static Mod _mod;

		public static bool Listo {
			get { return _reglas != null; }
		}

		/// <summary>Lee los .json de dentro del .tmod. Hay que llamarlo con el archivo del mod
		/// todavía abierto (misma regla que <see cref="CatalogoMejorPrefijo.LeerArchivo"/>).</summary>
		public static void LeerArchivos(Mod mod)
		{
			_mod = mod;
			try {
				if (mod.FileExists(RutaReglas)) {
					_bytesReglas = mod.GetFileBytes(RutaReglas);
				}
				else {
					LogAviso($"{Terrakeep.LogTag} PrefijosLegales: no se encontro {RutaReglas} dentro del .tmod.");
				}

				if (mod.FileExists(RutaEfectos)) {
					_bytesEfectos = mod.GetFileBytes(RutaEfectos);
				}
				else {
					LogAviso($"{Terrakeep.LogTag} PrefijosLegales: no se encontro {RutaEfectos} dentro del .tmod.");
				}
			}
			catch (Exception ex) {
				LogError($"{Terrakeep.LogTag} PrefijosLegales: fallo leyendo los .json: {ex.Message}");
			}
		}

		public static void Resolver()
		{
			_reglas = null;
			_efectos = null;

			if (_bytesReglas != null) {
				try {
					using (MemoryStream flujo = new MemoryStream(_bytesReglas, false)) {
						_reglas = PrefixRulesCatalog.LoadFromStream(flujo);
					}
					LogLinea($"{Terrakeep.LogTag} PrefijosLegales: reglas cargadas desde {RutaReglas}.");
				}
				catch (Exception ex) {
					LogError($"{Terrakeep.LogTag} PrefijosLegales: no se pudo parsear {RutaReglas}: {ex}");
				}
			}

			if (_bytesEfectos != null) {
				try {
					using (MemoryStream flujo = new MemoryStream(_bytesEfectos, false)) {
						_efectos = PrefixEffectCatalog.LoadFromStream(flujo);
					}
					LogLinea($"{Terrakeep.LogTag} PrefijosLegales: efectos cargados desde {RutaEfectos}.");
				}
				catch (Exception ex) {
					// Los efectos son un extra de tooltip: si fallan, el picker sigue funcionando
					// sin ellos (Efecto() ya devuelve "" cuando _efectos es null).
					LogError($"{Terrakeep.LogTag} PrefijosLegales: no se pudo parsear {RutaEfectos}: {ex}");
				}
			}
		}

		public static void Descargar()
		{
			_bytesReglas = null;
			_bytesEfectos = null;
			_reglas = null;
			_efectos = null;
			_mod = null;
		}

		/// <summary>
		/// Categorías reales del objeto (para decidir qué grupos del picker aplican). Vanilla via
		/// la tabla exacta de Core; cualquier mod (Calamity incluido) via los campos REALES que ya
		/// tiene resueltos <paramref name="item"/> en esta partida - ver la nota de cabecera.
		/// </summary>
		public static PrefixCategory CategoriasDe(Item item)
		{
			if (item == null || item.IsAir || _reglas == null) {
				return PrefixCategory.None;
			}

			if (item.type < ItemID.Count) {
				return _reglas.VanillaCategories(item.type);
			}

			PrefixCategory categorias = PrefixCategory.None;

			if (item.accessory) {
				categorias |= PrefixCategory.Accessory;
			}

			if (item.damage > 0 && item.DamageType != null) {
				categorias |= PrefixCategory.AnyWeapon;

				string clase = item.DamageType.GetType().Name;
				if (clase.IndexOf("Melee", StringComparison.OrdinalIgnoreCase) >= 0) {
					categorias |= PrefixCategory.Melee;
				}
				else if (clase.IndexOf("Ranged", StringComparison.OrdinalIgnoreCase) >= 0) {
					categorias |= PrefixCategory.Ranged;
				}
				else if (clase.IndexOf("Magic", StringComparison.OrdinalIgnoreCase) >= 0) {
					categorias |= PrefixCategory.Magic;
				}
				else if (clase.IndexOf("Summon", StringComparison.OrdinalIgnoreCase) >= 0) {
					categorias |= PrefixCategory.Summon;
				}
				else if (clase.IndexOf("Rogue", StringComparison.OrdinalIgnoreCase) >= 0) {
					categorias |= PrefixCategory.Rogue;
				}
			}

			return categorias;
		}

		/// <summary>
		/// Los grupos legales para <paramref name="item"/>, listos para el picker: cada uno con su
		/// meta (Biblioteca/Positivos/Negativos), su grupo (nombre + categoría) y la lista YA
		/// FILTRADA de <c>Terraria.ID.PrefixID</c> reales y aplicables en ESTA partida (nunca un
		/// id sintético de Calamity ni uno fuera del <c>PrefixID.Count</c> real - ver la nota de
		/// cabecera). Vacío si el catálogo no está listo o el objeto no admite ningún prefijo.
		/// </summary>
		public static IEnumerable<(PrefixMeta Meta, PrefixGroup Grupo, List<int> Ids)> GruposLegales(Item item)
		{
			if (item == null || item.IsAir || _reglas == null) {
				yield break;
			}

			PrefixCategory categorias = CategoriasDe(item);
			bool esVanilla = item.type < ItemID.Count;

			foreach (PrefixMeta meta in PrefixGroupCatalog.Metas) {
				foreach (PrefixGroup grupo in PrefixGroupCatalog.GroupsFor(meta, categorias, !esVanilla, item.type, _reglas)) {
					List<int> ids = new List<int>();
					foreach (int id in PrefixGroupCatalog.PrefixIdsFor(grupo, !esVanilla, item.type, _reglas)) {
						// Nunca un id sintetico de Calamity (necesitaria resolver un ModPrefix en
						// tiempo de ejecucion, fuera de alcance aqui - ver la nota de cabecera) ni
						// uno fuera del PrefixID.Count REAL de este tModLoader instalado (los
						// grupos de Invocacion traen ids de 1.4.5.8 que no existen en la 1.4.4.9).
						if (id > 0 && id < PrefixID.Count && id < CalamityIds.PrefixIdBase) {
							ids.Add(id);
						}
					}
					if (ids.Count > 0) {
						yield return (meta, grupo, ids);
					}
				}
			}
		}

		/// <summary>Nombre del prefijo con el idioma activo AHORA MISMO, tal cual lo enseña el
		/// propio juego (<c>Lang.prefix</c>, no una tabla propia): sigue el selector de idioma en
		/// vivo de WS7 sin que este catálogo tenga que saber nada de idiomas.</summary>
		public static string NombrePrefijo(int prefixId)
		{
			if (prefixId <= 0 || prefixId >= PrefixID.Count) {
				return Idiomas.Texto("Libreria.Prefijo.Ninguno");
			}
			return Lang.prefix[prefixId].Value;
		}

		/// <summary>Efecto real del prefijo, ya formateado en una linea corta ("+15% de daño, -10%
		/// de retroceso"), o "" si no hay tabla de efectos o el prefijo no tiene ninguno tabulado
		/// (algunos, como los que solo cambian el nombre, no tocan ningun numero).</summary>
		public static string Efecto(int prefixId)
		{
			if (_efectos == null || prefixId <= 0) {
				return "";
			}

			IReadOnlyList<ItemPrefixStatEffect> partes = _efectos.Effects(prefixId);
			if (partes.Count == 0) {
				return "";
			}

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			for (int i = 0; i < partes.Count; i++) {
				if (i > 0) {
					sb.Append(", ");
				}
				sb.Append(partes[i].Amount).Append(' ').Append(NombreEstadistica(partes[i].Stat));
			}
			return sb.ToString();
		}

		private static string NombreEstadistica(ItemPrefixStat estadistica)
		{
			switch (estadistica) {
				case ItemPrefixStat.Damage: return Idiomas.Texto("Libreria.Prefijo.Stat.Damage");
				case ItemPrefixStat.CritChance: return Idiomas.Texto("Libreria.Prefijo.Stat.CritChance");
				case ItemPrefixStat.Knockback: return Idiomas.Texto("Libreria.Prefijo.Stat.Knockback");
				case ItemPrefixStat.UseTime: return Idiomas.Texto("Libreria.Prefijo.Stat.UseTime");
				case ItemPrefixStat.Size: return Idiomas.Texto("Libreria.Prefijo.Stat.Size");
				case ItemPrefixStat.ShootSpeed: return Idiomas.Texto("Libreria.Prefijo.Stat.ShootSpeed");
				case ItemPrefixStat.ManaCost: return Idiomas.Texto("Libreria.Prefijo.Stat.ManaCost");
				case ItemPrefixStat.Defense: return Idiomas.Texto("Libreria.Prefijo.Stat.Defense");
				case ItemPrefixStat.MaxMana: return Idiomas.Texto("Libreria.Prefijo.Stat.MaxMana");
				case ItemPrefixStat.MoveSpeed: return Idiomas.Texto("Libreria.Prefijo.Stat.MoveSpeed");
				case ItemPrefixStat.MeleeSpeed: return Idiomas.Texto("Libreria.Prefijo.Stat.MeleeSpeed");
				default: return "";
			}
		}

		private static Mod ModUtil {
			get { return _mod ?? Terrakeep.Instance; }
		}

		private static void LogLinea(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Info(linea);
			}
		}

		private static void LogAviso(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Warn(linea);
			}
		}

		private static void LogError(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Error(linea);
			}
		}
	}
}
