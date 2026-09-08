using System;
using System.IO;
using Terraria.ModLoader;
using Terrakeep.Core.Data;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>
	/// Resuelve un id sintetico de prefijo REAL de Picaro de Calamity (<c>ObjetoBuild.PrefixId</c>,
	/// 10000-10020, tabla de <c>Assets/rogue_prefixes.json</c> - copia tal cual de
	/// <c>Terrakeep.App/Assets/calamity/rogue_prefixes.json</c> del repo hermano
	/// <c>Terrasavr-Native</c>) contra el <see cref="ModPrefix"/> real registrado por CalamityMod
	/// en ESTA partida.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que hacia falta este catalogo aparte de <see cref="Prefijos.CatalogoMejorPrefijo"/></b>:
	/// ese catalogo (<c>best_prefix.json</c>) solo conoce <c>Terraria.ID.PrefixID</c> planos
	/// (vanilla) y descarta a proposito los 21 prefijos reales de Picaro de Calamity (ver su
	/// propia cabecera) porque aplicarlos de verdad exige resolver un
	/// <c>Terraria.ModLoader.ModPrefix.Type</c> en tiempo de ejecucion, que es justo lo que hace
	/// esta clase.
	/// </para>
	/// <para>
	/// <b>Como se resuelve de verdad, confirmado contra el codigo real</b> (
	/// <c>tModLoader-Decompiled\tModLoader\Terraria\ModLoader\PrefixLoader.cs</c>/<c>ModPrefix.cs</c>
	/// y <c>tModLoader-Decompiled\CalamityMod\CalamityMod\Prefixes\*.cs</c>): un <c>ModPrefix</c> se
	/// registra con un <c>Type</c> (int) asignado en caliente por <c>PrefixLoader.ReservePrefixID()</c>
	/// (secuencial, por encima de <c>PrefixID.Count</c> vanilla) y se busca por
	/// <c>Mod.Name</c>+nombre de clase con <c>ModContent.TryFind&lt;ModPrefix&gt;</c> - el mismo
	/// patron que <c>ItemID.Search</c>/<c>ModItem.FullName</c> ya usa <c>CatalogoBuilds</c> para
	/// objetos, pero para prefijos. El nombre de clase de CalamityMod coincide EXACTO con el campo
	/// "internal" de <c>rogue_prefixes.json</c> (comprobado: <c>Prefixes/Flawless.cs</c> ->
	/// <c>class Flawless : RogueWeaponPrefix</c>, <c>Prefixes/Silent.cs</c> ->
	/// <c>class Silent : RogueAccessoryPrefix</c>, etc.), y <c>Terraria.Item.Prefix(int)</c> acepta
	/// directamente ese <c>Type</c> (no esta limitado al rango <c>byte</c> vanilla: su firma real
	/// es <c>public bool Prefix(int prefixWeWant)</c>) - nada que reimplementar, solo resolver el
	/// id y pasarselo tal cual.
	/// </para>
	/// <para>
	/// <b>Por que los bytes se leen en <c>Load()</c> y se parsean en <c>PostSetupContent()</c></b>:
	/// mismo motivo real que el resto de catalogos de este mod (<c>TmodFile.GetStream</c> lanza
	/// <c>IOException("File not open")</c> una vez cerrado el <c>.tmod</c>).
	/// </para>
	/// </remarks>
	public static class CatalogoPrefijoPicaro
	{
		/// <summary>Nombre interno real del mod Calamity - mismo valor que ya usa
		/// <c>CatalogoMejorPrefijo</c>.</summary>
		private const string NombreModCalamity = "CalamityMod";

		private const string Ruta = "Assets/rogue_prefixes.json";

		private static byte[] _bytes;
		private static RoguePrefixCatalog _catalogo;

		/// <summary>true si la tabla ya esta parseada (no implica que el ModPrefix real se haya
		/// resuelto ya: eso se hace bajo demanda en <see cref="ResolverPrefijoReal"/>, porque solo
		/// hace falta cuando de verdad se crea un objeto Picaro de una build).</summary>
		public static bool Listo => _catalogo != null;

		public static void LeerArchivo(Mod mod)
		{
			try {
				if (!mod.FileExists(Ruta)) {
					RegistroBuilds.Aviso($"{Terrakeep.LogTag} PrefijoPicaro: no se encontro {Ruta} dentro del .tmod.");
					return;
				}
				_bytes = mod.GetFileBytes(Ruta);
			}
			catch (Exception ex) {
				RegistroBuilds.Error($"{Terrakeep.LogTag} PrefijoPicaro: fallo leyendo {Ruta}: {ex.Message}");
			}
		}

		public static void Resolver()
		{
			_catalogo = null;
			if (_bytes == null) {
				return;
			}

			try {
				using (MemoryStream flujo = new MemoryStream(_bytes, false)) {
					_catalogo = RoguePrefixCatalog.LoadFromStream(flujo);
				}
				RegistroBuilds.Linea($"{Terrakeep.LogTag} PrefijoPicaro: tabla cargada desde {Ruta} " +
					$"({_catalogo.Weapon.Count} de arma + {_catalogo.Accessory.Count} de accesorio).");
			}
			catch (Exception ex) {
				RegistroBuilds.Error($"{Terrakeep.LogTag} PrefijoPicaro: no se pudo parsear {Ruta}: {ex}");
			}
		}

		public static void Descargar()
		{
			_catalogo = null;
			_bytes = null;
		}

		/// <summary>
		/// El <c>Terraria.ModLoader.ModPrefix.Type</c> real de esta partida para el id sintetico
		/// <paramref name="idSintetico"/> (10000-10020, ver <c>rogue_prefixes.json</c>), o null si
		/// la tabla no esta lista, el id no existe en ella, o CalamityMod no esta cargado (o no
		/// registro ese <c>ModPrefix</c> con el nombre esperado - version distinta, por ejemplo).
		/// Nunca lanza: es codigo que corre al crear equipo de una build, no debe poder tumbar esa
		/// operacion.
		/// </summary>
		public static int? ResolverPrefijoReal(int idSintetico)
		{
			if (_catalogo == null) {
				return null;
			}

			RoguePrefixEntryData entrada = _catalogo.ById(idSintetico);
			if (entrada == null) {
				RegistroBuilds.Aviso($"{Terrakeep.LogTag} PrefijoPicaro: id sintetico {idSintetico} " +
					"no existe en rogue_prefixes.json.");
				return null;
			}

			if (!ModContent.TryFind(NombreModCalamity, entrada.Internal, out ModPrefix prefijo)) {
				// Caso normal si Calamity no esta instalado (el catalogo de builds ni siquiera
				// habria resuelto el Item.type en ese caso, pero por si acaso se llega aqui de
				// otra forma) o si una version de Calamity renombro la clase.
				RegistroBuilds.Aviso($"{Terrakeep.LogTag} PrefijoPicaro: no se encontro el ModPrefix real " +
					$"\"{NombreModCalamity}/{entrada.Internal}\" (id sintetico {idSintetico}) en esta partida.");
				return null;
			}

			return prefijo.Type;
		}
	}
}
