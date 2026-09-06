using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using TerrasavrNative.Core.Data;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>
	/// Catalogo estatico de equipo recomendado por etapa y clase (WS4). Lee los MISMOS archivos
	/// de datos que la app de escritorio Terrakeep (<c>builds.json</c> vanilla y
	/// <c>builds_calamity.json</c>), empaquetados dentro del propio <c>.tmod</c>, y los resuelve
	/// a objetos reales de la partida en curso.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Empaquetado de los .json</b>: no hace falta declararlos en ningun sitio.
	/// <c>ModCompile.PackageMod</c> (codigo real del tModLoader instalado, v2026.7.3.0) mete en
	/// el <c>.tmod</c> <b>todos</b> los archivos de la carpeta del mod salvo los que descarta
	/// <c>IgnoreResource</c>: los de <c>buildIgnore</c>, los que empiezan por ".", <c>bin\</c>,
	/// <c>obj\</c>, el codigo fuente (si no hay <c>includeSource</c>) y <c>Thumbs.db</c>. Los
	/// <c>.json</c> no encajan en ninguna de esas reglas, asi que entran solos. Las rutas se
	/// normalizan a "/" en <c>TmodFile.Sanitize</c>, de ahi "Assets/builds.json".
	/// </para>
	/// <para>
	/// <b>Por que se leen los BYTES en Load() y se parsean despues</b>: <c>TmodFile.GetStream</c>
	/// lanza <c>IOException("File not open")</c> si el <c>.tmod</c> ya se cerro, cosa que pasa
	/// cuando termina la carga. Leyendo los bytes durante <c>Load()</c> (archivo abierto seguro) y
	/// parseandolos en <c>PostSetupContent()</c> se evita depender de ese detalle. Son 94 KB en
	/// total, el coste es irrelevante.
	/// </para>
	/// <para>
	/// <b>Como se resuelve un pid a un objeto real</b>: con <c>ItemID.Search</c>, el
	/// <c>IdDictionary</c> de ReLogic. Sirve para los dos formatos de pid a la vez, que es
	/// justo lo que hace falta aqui:
	/// <list type="bullet">
	/// <item>vanilla ("MoltenHelmet"): las claves son los nombres de campo de
	/// <c>Terraria.ID.ItemID</c>, que es exactamente el formato de los pid sin "/" del JSON
	/// (comprobado: los 101 pid vanilla de los dos archivos existen como campo de ItemID);</item>
	/// <item>de mod ("CalamityMod/SmokingComet"): <c>ModItem.Register</c> hace
	/// <c>ItemID.Search.Add(FullName, Type)</c> con <c>FullName = "Mod/NombreInterno"</c>, o sea
	/// el mismo formato con "/" del JSON.</item>
	/// </list>
	/// Es mas directo que <c>ModContent.Find&lt;ModItem&gt;(mod, interno).Type</c>, no necesita
	/// distinguir los dos casos ni conocer los ids sinteticos de Calamity que si usa la app de
	/// escritorio (<c>CalamityIds.ItemIdBase</c>), y no lanza si el mod no esta instalado.
	/// </para>
	/// </remarks>
	public static class CatalogoBuilds
	{
		private const string RutaVanilla = "Assets/builds.json";
		private const string RutaCalamity = "Assets/builds_calamity.json";

		private static byte[] _bytesVanilla;
		private static byte[] _bytesCalamity;

		private static readonly List<FuenteBuilds> _fuentes = new List<FuenteBuilds>();

		/// <summary>Fuentes utilizables en esta partida (las que resolvieron algun objeto).</summary>
		public static IReadOnlyList<FuenteBuilds> Fuentes => _fuentes;

		/// <summary>true si ya se resolvio el catalogo contra el contenido de esta partida.</summary>
		public static bool Listo { get; private set; }

		/// <summary>
		/// Nombre traducido de una clase, a partir de la clave del JSON. Las cinco claves son las
		/// del archivo de datos (melee / ranged / mage / summoner / rogue, esta ultima solo en
		/// Calamity); cualquier otra que apareciera se enseña tal cual en vez de inventarse nada.
		/// </summary>
		public static string EtiquetaClase(string clave)
		{
			switch (clave) {
				case "melee": return Idiomas.Texto("Builds.Clase.Melee");
				case "ranged": return Idiomas.Texto("Builds.Clase.Ranged");
				case "mage": return Idiomas.Texto("Builds.Clase.Mage");
				case "summoner": return Idiomas.Texto("Builds.Clase.Summoner");
				case "rogue": return Idiomas.Texto("Builds.Clase.Rogue");
				default: return clave;
			}
		}

		/// <summary>
		/// Lee los dos .json de dentro del .tmod. Hay que llamarlo mientras el archivo del mod
		/// sigue abierto, o sea durante la carga (<c>Mod.Load</c> / <c>ModSystem.Load</c>).
		/// </summary>
		public static void LeerArchivos(Mod mod)
		{
			_bytesVanilla = LeerSiExiste(mod, RutaVanilla);
			_bytesCalamity = LeerSiExiste(mod, RutaCalamity);
		}

		private static byte[] LeerSiExiste(Mod mod, string ruta)
		{
			try {
				if (!mod.FileExists(ruta)) {
					RegistroBuilds.Aviso($"{Terrakeep.LogTag} Builds: no se encontro {ruta} dentro del .tmod.");
					return null;
				}
				return mod.GetFileBytes(ruta);
			}
			catch (Exception ex) {
				RegistroBuilds.Error($"{Terrakeep.LogTag} Builds: fallo leyendo {ruta}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Parsea los archivos ya leidos y resuelve cada pid a un <c>Item.type</c> real. Hay que
		/// llamarlo cuando TODOS los mods han registrado ya su contenido, o sea desde
		/// <c>ModSystem.PostSetupContent</c>: antes de eso, los pid de Calamity todavia no estan
		/// en <c>ItemID.Search</c>.
		/// </summary>
		public static void Resolver(Mod mod)
		{
			_fuentes.Clear();
			Listo = false;

			AgregarFuente(mod, "vanilla", "Vanilla", _bytesVanilla);
			AgregarFuente(mod, "calamity", "Calamity", _bytesCalamity);

			Listo = _fuentes.Count > 0;

			StringBuilder resumen = new StringBuilder();
			resumen.Append($"{Terrakeep.LogTag} Builds: catalogo resuelto. ");
			foreach (FuenteBuilds f in _fuentes) {
				resumen.Append($"[{f.Etiqueta}: {f.Etapas.Count} etapas, {f.Resueltos}/{f.Total} objetos resueltos] ");
			}
			if (_fuentes.Count == 0) {
				resumen.Append("(ninguna fuente utilizable)");
			}
			RegistroBuilds.Linea(resumen.ToString());
		}

		private static void AgregarFuente(Mod mod, string clave, string etiqueta, byte[] bytes)
		{
			if (bytes == null) {
				return;
			}

			BuildsCatalog catalogo;
			try {
				// Codigo REAL de TerrasavrNative.Core (repo hermano) reutilizado tal cual: el
				// mismo parser que usa la app de escritorio para estos dos archivos. Es
				// justamente el patron que preveia el plan (LoadFromStream encaja directo con
				// los archivos de dentro del .tmod).
				using (MemoryStream flujo = new MemoryStream(bytes, false)) {
					catalogo = BuildsCatalog.LoadFromStream(flujo);
				}
			}
			catch (Exception ex) {
				RegistroBuilds.Error($"{Terrakeep.LogTag} Builds: no se pudo parsear la fuente \"{etiqueta}\": {ex}");
				return;
			}

			FuenteBuilds fuente = new FuenteBuilds { Clave = clave, Etiqueta = etiqueta };
			List<string> sinResolver = new List<string>();

			foreach (BuildStage etapaCore in catalogo.Stages) {
				EtapaBuild etapa = new EtapaBuild { Clave = etapaCore.Key, Etiqueta = etapaCore.Label };

				foreach (KeyValuePair<string, BuildClassGear> par in etapaCore.Classes) {
					ClaseBuild clase = new ClaseBuild { Clave = par.Key };

					Convertir(par.Value.Armor, clase.Armadura, fuente, sinResolver);
					Convertir(par.Value.Weapons, clase.Armas, fuente, sinResolver);
					Convertir(par.Value.Accessories, clase.Accesorios, fuente, sinResolver);

					etapa.Clases.Add(clase);
				}

				if (etapa.Clases.Count > 0) {
					fuente.Etapas.Add(etapa);
				}
			}

			if (!fuente.Utilizable) {
				// Caso normal y esperado: builds_calamity.json con Calamity sin instalar. No es
				// un error, simplemente esa fuente no se enseña.
				RegistroBuilds.Linea($"{Terrakeep.LogTag} Builds: la fuente \"{etiqueta}\" no es utilizable en esta partida " +
					$"({fuente.Resueltos}/{fuente.Total} objetos resueltos, y {fuente.DeModResueltos}/{fuente.DeMod} " +
					$"de los que vienen de un mod); no se enseñara en el panel.");
				return;
			}

			if (sinResolver.Count > 0) {
				RegistroBuilds.Linea($"{Terrakeep.LogTag} Builds: en \"{etiqueta}\" quedaron {sinResolver.Count} pid sin resolver. " +
					$"Ejemplos: {string.Join(", ", sinResolver.GetRange(0, Math.Min(8, sinResolver.Count)))}");
			}

			_fuentes.Add(fuente);
		}

		private static void Convertir(List<BuildItemRef> origen, List<ObjetoBuild> destino, FuenteBuilds fuente, List<string> sinResolver)
		{
			if (origen == null) {
				return;
			}

			foreach (BuildItemRef refe in origen) {
				if (refe == null || string.IsNullOrEmpty(refe.Pid)) {
					continue;
				}

				ObjetoBuild objeto = new ObjetoBuild {
					Pid = refe.Pid,
					NombreEs = refe.Es,
					NombreEn = refe.En,
					PrefijoRecomendado = refe.Prefix,
					Tipo = ResolverPid(refe.Pid),
				};

				bool esDeMod = refe.Pid.IndexOf('/') >= 0;

				fuente.Total++;
				if (esDeMod) {
					fuente.DeMod++;
				}
				if (objeto.Resuelto) {
					fuente.Resueltos++;
					if (esDeMod) {
						fuente.DeModResueltos++;
					}
				}
				else {
					sinResolver.Add(refe.Pid);
				}

				destino.Add(objeto);
			}
		}

		/// <summary>
		/// pid -> <c>Item.type</c> real de esta partida. Devuelve 0 si no existe (mod no
		/// instalado, o nombre interno que ya no existe en esta version del juego).
		/// </summary>
		public static int ResolverPid(string pid)
		{
			if (string.IsNullOrEmpty(pid)) {
				return 0;
			}

			if (!ItemID.Search.TryGetId(pid, out int tipo)) {
				return 0;
			}

			// Defensa barata: un id fuera de rango reventaria en Item.SetDefaults.
			return tipo > 0 && tipo < ItemLoader.ItemCount ? tipo : 0;
		}

		public static void Descargar()
		{
			_fuentes.Clear();
			_bytesVanilla = null;
			_bytesCalamity = null;
			Listo = false;
		}
	}
}
