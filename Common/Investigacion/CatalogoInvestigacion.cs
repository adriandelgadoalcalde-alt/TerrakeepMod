using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using Terrakeep.Core.Data;

namespace TerrakeepMod.Common.Investigacion
{
	/// <summary>
	/// Una carpeta del arbol de Investigacion: el nodo de datos que viene de
	/// <c>Terrakeep.Core</c> mas lo que hace falta para pintarlo y contarlo aqui.
	/// </summary>
	public sealed class CarpetaInvestigacion
	{
		/// <summary>Nombre visible de la carpeta.</summary>
		public readonly string Nombre;

		/// <summary>Ruta completa (clave estable en ingles), unica en todo el arbol. Es la que
		/// recuerda que carpetas estan desplegadas.</summary>
		public readonly string Ruta;

		/// <summary>Nivel de anidamiento, para la sangria del arbol.</summary>
		public readonly int Profundidad;

		public readonly CarpetaInvestigacion Padre;

		public readonly List<CarpetaInvestigacion> Hijos = new List<CarpetaInvestigacion>();

		/// <summary>Tipos INVESTIGABLES de esta carpeta y de todas las de dentro, ya canonizados
		/// y sin repetidos. Es el conjunto sobre el que se cuenta y sobre el que actuan los
		/// botones de "investigar carpeta" / "quitar carpeta".</summary>
		public readonly int[] Tipos;

		/// <summary>Solo los tipos que cuelgan directamente de esta carpeta (los que se listan a
		/// la derecha al seleccionarla). En una carpeta con hijas suele estar vacio.</summary>
		public readonly int[] TiposPropios;

		/// <summary>Si la carpeta esta desplegada en el arbol.</summary>
		public bool Desplegada;

		/// <summary>Cuantos de <see cref="Tipos"/> estan investigados del todo. Lo recalcula
		/// <see cref="CatalogoInvestigacion.RefrescarContadores"/>.</summary>
		public int Hechos;

		public int Total => Tipos.Length;

		public bool EsHoja => Hijos.Count == 0;

		public CarpetaInvestigacion(string nombre, string ruta, int profundidad,
			CarpetaInvestigacion padre, int[] tipos, int[] tiposPropios)
		{
			Nombre = nombre;
			Ruta = ruta;
			Profundidad = profundidad;
			Padre = padre;
			Tipos = tipos;
			TiposPropios = tiposPropios;
		}
	}

	/// <summary>
	/// Construye el arbol de carpetas del panel de Investigacion.
	/// <para />
	/// <b>Reutiliza el MISMO arbol de la Libreria, no una copia</b>: el algoritmo real vive en
	/// <c>Terrakeep.Core.Data.LibraryTreeBuilder</c> / <c>LiveItemTreeBuilder</c> (movido a
	/// Core por WS2 justamente para esto), y los datos son los mismos archivos
	/// <c>Assets/vanilla_library_tree.json</c> + <c>Assets/vanilla_library_labels_es.json</c> que
	/// consume la Libreria del mod (WS3) y que la app de escritorio Terrakeep. Aqui no se
	/// reimplementa ni el agrupado ni el paginado ni las etiquetas en español.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Tres fuentes, en este orden</b>, porque el arbol vanilla de Terrasavr no cubre por si
	/// solo todo lo que el juego considera investigable:
	/// <list type="number">
	/// <item>el arbol vanilla estatico (<c>LibraryTreeBuilder.BuildItemTree</c>), que es el que
	/// da las carpetas reconocibles ("Materiales / Pre-Modo Dificil / Cobre &amp; Estaño"...);</item>
	/// <item>una raiz por MOD cargado (<c>LiveItemTreeBuilder.BuildTree</c>, el mismo
	/// <c>BuildGroupedRoot</c> de Core por dentro) con los objetos investigables que aporta cada
	/// mod - descubiertos EN VIVO desde <c>ContentSamples.ItemsByType</c>, asi que Calamity o
	/// cualquier otro mod entran solos sin catalogo estatico;</item>
	/// <item>una ultima carpeta "Otros objetos" con lo investigable de vanilla que no aparezca en
	/// el arbol estatico, para que la suma de las carpetas sea EXACTAMENTE el total real del
	/// juego y no quede nada escondido.</item>
	/// </list>
	/// </para>
	/// <para>
	/// <b>Solo entra lo investigable.</b> Una carpeta cuyos objetos no se puedan investigar
	/// ninguno no se enseña: un "0/0" no significa nada y solo ensucia. Lo que decide que es
	/// investigable es la tabla real del juego (ver <see cref="EstadoInvestigacion"/>), nunca una
	/// lista propia.
	/// </para>
	/// <para>
	/// <b>Sobre WS3 (Libreria), que se estaba construyendo en paralelo</b>: la lectura de los dos
	/// <c>.json</c> se hace aqui de forma independiente y tolerante (si faltan, el arbol se
	/// construye igual con lo vivo). Es a proposito: dos agentes trabajando a la vez sobre el
	/// mismo repo no pueden depender del codigo a medias del otro. En el pase de fusion de los
	/// seis paneles, esto se puede sustituir por el catalogo compartido de la Libreria sin tocar
	/// nada de la interfaz de este panel.
	/// </para>
	/// </remarks>
	public static class CatalogoInvestigacion
	{
		private const string RutaArbol = "Assets/vanilla_library_tree.json";
		private const string RutaEtiquetas = "Assets/vanilla_library_labels_es.json";

		/// <summary>Nombre que ningun mod real puede tener. Ver donde se usa, en
		/// <see cref="ConstruirArbolDeMods"/>.</summary>
		private const string NombreImposibleDeMod = "(sin vanilla)";

		private static byte[] _bytesArbol;
		private static byte[] _bytesEtiquetas;

		private static List<CarpetaInvestigacion> _raices = new List<CarpetaInvestigacion>();

		/// <summary>Carpetas raiz del arbol.</summary>
		public static IReadOnlyList<CarpetaInvestigacion> Raices => _raices;

		/// <summary>Resumen de como se construyo el arbol, para el log de las pruebas.</summary>
		public static string ResumenConstruccion { get; private set; } = "(sin construir)";

		/// <summary>true si hay arbol utilizable.</summary>
		public static bool Listo => _raices.Count > 0;

		/// <summary>Las carpetas raiz con su recuento, para el log de las pruebas.</summary>
		public static string ResumenRaices()
		{
			List<string> partes = new List<string>();
			foreach (CarpetaInvestigacion raiz in _raices) {
				partes.Add("\"" + raiz.Nombre + "\" " + raiz.Hechos + "/" + raiz.Total);
			}
			return string.Join(" | ", partes);
		}

		/// <summary>
		/// Lee los .json de dentro del .tmod. Hay que llamarlo mientras el archivo del mod sigue
		/// abierto, o sea durante la carga: <c>TmodFile.GetStream</c> lanza
		/// <c>IOException("File not open")</c> una vez cerrado (hallazgo real de WS4).
		/// </summary>
		public static void LeerArchivos(Mod mod)
		{
			_bytesArbol = LeerSiExiste(mod, RutaArbol);
			_bytesEtiquetas = LeerSiExiste(mod, RutaEtiquetas);
		}

		private static byte[] LeerSiExiste(Mod mod, string ruta)
		{
			try {
				if (!mod.FileExists(ruta)) {
					return null;
				}
				using (Stream flujo = mod.GetFileStream(ruta))
				using (MemoryStream memoria = new MemoryStream()) {
					flujo.CopyTo(memoria);
					return memoria.ToArray();
				}
			}
			catch (Exception e) {
				RegistroInvestigacion.Aviso(
					$"{Terrakeep.LogTag} Investigacion: no se pudo leer {ruta} del .tmod ({e.GetType().Name}: {e.Message}).");
				return null;
			}
		}

		/// <summary>
		/// Construye el arbol contra el contenido de ESTA partida. Hay que llamarlo cuando todos
		/// los mods ya han registrado su contenido (<c>PostSetupContent</c>) o mas tarde: antes de
		/// eso ni <c>ContentSamples.ItemsByType</c> ni la tabla de sacrificios estan completas.
		/// </summary>
		public static void Construir()
		{
			List<CarpetaInvestigacion> raices = new List<CarpetaInvestigacion>();
			HashSet<int> yaColocados = new HashSet<int>();

			// --- 1. Arbol vanilla estatico, con el constructor real de Core -------------------
			int raicesEstaticas = 0;
			List<CategoryTreeNodeData> nodosEstaticos = ConstruirArbolEstatico();
			foreach (CategoryTreeNodeData nodo in nodosEstaticos) {
				CarpetaInvestigacion carpeta = Convertir(nodo, null, 0, yaColocados, true);
				if (carpeta != null) {
					raices.Add(carpeta);
					raicesEstaticas++;
				}
			}

			// --- 2. Una raiz por mod, descubierta en vivo -------------------------------------
			int raicesDeMod = 0;
			foreach (CategoryTreeNodeData nodo in ConstruirArbolDeMods()) {
				CarpetaInvestigacion carpeta = Convertir(nodo, null, 0, yaColocados, false);
				if (carpeta != null) {
					raices.Add(carpeta);
					raicesDeMod++;
				}
			}

			// --- 3. Lo investigable de vanilla que no estaba en el arbol estatico -------------
			List<int> sueltos = new List<int>();
			foreach (int tipo in EstadoInvestigacion.TiposInvestigables) {
				int canonico = EstadoInvestigacion.TipoCanonico(tipo);
				if (EsDeMod(canonico) || !yaColocados.Add(canonico)) {
					continue;
				}
				sueltos.Add(canonico);
			}
			sueltos.Sort();
			if (sueltos.Count > 0) {
				int[] tipos = sueltos.ToArray();
				raices.Add(new CarpetaInvestigacion(
					Idiomas.Texto("Investigacion.OtrosObjetos", tipos.Length), "Otros", 0, null, tipos, tipos));
			}

			_raices = raices;

			int totalEnArbol = yaColocados.Count;
			int totalJuego = EstadoInvestigacion.TotalInvestigable;
			ResumenConstruccion =
				$"{raices.Count} carpetas raiz ({raicesEstaticas} del arbol vanilla de Terrasavr, " +
				$"{raicesDeMod} de mods, {(sueltos.Count > 0 ? 1 : 0)} de \"Otros objetos\" con {sueltos.Count}). " +
				$"Objetos investigables en el arbol: {totalEnArbol}; segun la tabla real del juego: {totalJuego}" +
				(totalEnArbol == totalJuego ? " (cuadra)" : " (NO cuadra)");

			RefrescarContadores();
		}

		private static List<CategoryTreeNodeData> ConstruirArbolEstatico()
		{
			if (_bytesArbol == null) {
				RegistroInvestigacion.Aviso(
					$"{Terrakeep.LogTag} Investigacion: sin {RutaArbol}; el arbol se construye solo con " +
					"lo descubierto en vivo.");
				return new List<CategoryTreeNodeData>();
			}

			try {
				VanillaLibraryTreeCatalog arbol;
				using (MemoryStream memoria = new MemoryStream(_bytesArbol)) {
					arbol = VanillaLibraryTreeCatalog.LoadFromStream(memoria);
				}

				LibraryLabelCatalog etiquetas;
				// El archivo de etiquetas solo existe en español. En cualquier otro idioma se usa
				// un catalogo VACIO a proposito: los nombres del propio arbol ya vienen en ingles
				// (son la clave con la que se busca la traduccion) y Translate devuelve la clave
				// cuando no la encuentra. Misma decision que en ArbolLibreria.
				if (_bytesEtiquetas != null && Idiomas.EnEspanol) {
					using (MemoryStream memoria = new MemoryStream(_bytesEtiquetas)) {
						etiquetas = LibraryLabelCatalog.LoadFromStream(memoria);
					}
				}
				else {
					// Sin traducciones el arbol sale igual, en ingles: Translate devuelve la clave.
					using (MemoryStream vacio = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"))) {
						etiquetas = LibraryLabelCatalog.LoadFromStream(vacio);
					}
				}

				// calamity = null: dentro del juego no se injerta ningun catalogo estatico de
				// Calamity, el contenido de cualquier mod se descubre en vivo (paso 2).
				// El icono de carpeta no se usa aqui (este panel dibuja los iconos reales del
				// juego con ItemSlot), asi que el resolutor devuelve siempre null.
				return LibraryTreeBuilder.BuildItemTree(arbol, etiquetas, null, id => null);
			}
			catch (Exception e) {
				RegistroInvestigacion.Aviso(
					$"{Terrakeep.LogTag} Investigacion: no se pudo construir el arbol vanilla " +
					$"({e.GetType().Name}: {e.Message}). Se sigue con lo descubierto en vivo.");
				return new List<CategoryTreeNodeData>();
			}
		}

		private static List<CategoryTreeNodeData> ConstruirArbolDeMods()
		{
			List<LiveItemInfo> deMods = new List<LiveItemInfo>();
			HashSet<int> vistos = new HashSet<int>();

			foreach (int tipo in EstadoInvestigacion.TiposInvestigables) {
				int canonico = EstadoInvestigacion.TipoCanonico(tipo);
				if (!vistos.Add(canonico) || !EsDeMod(canonico)) {
					continue;
				}

				Item muestra;
				if (!ContentSamples.ItemsByType.TryGetValue(canonico, out muestra) || muestra == null) {
					continue;
				}

				deMods.Add(new LiveItemInfo(canonico, muestra.Name, NombreDelMod(muestra), Categoria(muestra)) {
					Rarity = muestra.rare
				});
			}

			if (deMods.Count == 0) {
				return new List<CategoryTreeNodeData>();
			}

			// LiveItemTreeBuilder pone SIEMPRE la primera la carpeta del mod que se llame como
			// vanillaModName. Aqui no hay ni un objeto de vanilla (los filtra EsDeMod), asi que se
			// le pasa un nombre que ningun mod puede tener y todas las raices quedan ordenadas por
			// orden ordinal, sin ninguna privilegiada.
			return LiveItemTreeBuilder.BuildTree(deMods, id => null, null,
				n => Idiomas.Texto("Libreria.Pagina", n), NombreImposibleDeMod);
		}

		/// <summary>true si el tipo lo aporta un mod (no es contenido de Terraria).</summary>
		private static bool EsDeMod(int tipo)
		{
			return tipo >= ItemID.Count;
		}

		/// <summary>Nombre con el que se enseña la carpeta raiz de un mod: el que el propio mod
		/// declara para el usuario (<c>Mod.DisplayName</c>, lo que se ve en la lista de mods del
		/// juego), no su nombre interno.</summary>
		private static string NombreDelMod(Item muestra)
		{
			if (muestra.ModItem == null || muestra.ModItem.Mod == null) {
				return "Terraria";
			}
			Mod duenio = muestra.ModItem.Mod;
			return string.IsNullOrEmpty(duenio.DisplayName) ? duenio.Name : duenio.DisplayName;
		}

		/// <summary>
		/// Carpeta a la que va a parar un objeto de mod. Se decide con las propiedades REALES del
		/// <c>Item</c> que ya tiene cargado el juego (ranura de armadura, accesorio, clase de
		/// daño, herramienta, si coloca un bloque...), nunca con una lista de ids: asi funciona
		/// igual con cualquier mod instalado, no solo con Calamity.
		/// </summary>
		/// <summary>
		/// Rotulo de una categoria de objetos de mod, traducido al idioma activo. Con dos partes
		/// devuelve "Padre/Hija", que es la forma con la que <c>LiveItemTreeBuilder</c> monta una
		/// subcarpeta.
		/// </summary>
		private static string Categoria(string clave, string subclave = null)
		{
			string padre = Idiomas.Texto("Investigacion.Categoria." + clave);
			return subclave == null
				? padre
				: padre + "/" + Idiomas.Texto("Investigacion.Categoria." + clave + subclave);
		}

		private static string Categoria(Item item)
		{
			if (item.headSlot > 0) {
				return Categoria("Armadura", "Cascos");
			}
			if (item.bodySlot > 0) {
				return Categoria("Armadura", "Petos");
			}
			if (item.legSlot > 0) {
				return Categoria("Armadura", "Perneras");
			}
			if (item.accessory) {
				return Categoria("Accesorios");
			}
			if (item.damage > 0) {
				if (item.CountsAsClass(DamageClass.Melee)) {
					return Categoria("Armas", "Melee");
				}
				if (item.CountsAsClass(DamageClass.Ranged)) {
					return Categoria("Armas", "Ranged");
				}
				if (item.CountsAsClass(DamageClass.Magic)) {
					return Categoria("Armas", "Magic");
				}
				if (item.CountsAsClass(DamageClass.Summon)) {
					return Categoria("Armas", "Summon");
				}
				return Categoria("Armas", "Otras");
			}
			if (item.pick > 0 || item.axe > 0 || item.hammer > 0) {
				return Categoria("Herramientas");
			}
			if (item.createTile >= 0) {
				return Categoria("Bloques");
			}
			if (item.createWall >= 0) {
				return Categoria("Paredes");
			}
			if (item.ammo > 0) {
				return Categoria("Municion");
			}
			if (item.consumable) {
				return Categoria("Consumibles");
			}
			if (item.material) {
				return Categoria("Materiales");
			}
			return Categoria("Otros");
		}

		/// <summary>
		/// Pasa un nodo de datos de Core a una carpeta de este panel, quedandose solo con lo
		/// investigable y descartando las carpetas que se quedan vacias.
		/// </summary>
		private static CarpetaInvestigacion Convertir(CategoryTreeNodeData nodo, CarpetaInvestigacion padre,
			int profundidad, HashSet<int> yaColocados, bool soloVanilla)
		{
			// Los tipos investigables de este nodo, canonizados y sin repetir.
			List<int> propios = new List<int>();
			HashSet<int> enEsteNodo = new HashSet<int>();
			foreach (int id in nodo.ItemIdsOrdered) {
				// El arbol vanilla estatico SOLO puede aportar objetos de vanilla. El catalogo de
				// Terrasavr del que sale trae ids por encima del ItemID.Count de esta version del
				// juego (5456), y esos ids, dentro de la partida, ya no son de vanilla: los ocupan
				// los objetos que registran los mods. Sin este filtro, un objeto de mod aparecia
				// DOS veces - colado en una carpeta vanilla que no le corresponde y otra vez en la
				// carpeta de su mod. Visto de verdad en la prueba con Calamity: "Andromedon Body"
				// (type=5456, de tModLoader) salia dentro de "Daño de Invocacion".
				if (soloVanilla && id >= ItemID.Count) {
					continue;
				}
				int canonico = EstadoInvestigacion.TipoCanonico(id);
				if (EstadoInvestigacion.EsInvestigable(canonico) && enEsteNodo.Add(canonico)) {
					propios.Add(canonico);
				}
			}

			if (propios.Count == 0) {
				return null;
			}

			int[] tipos = propios.ToArray();
			CarpetaInvestigacion carpeta = new CarpetaInvestigacion(
				nodo.Name, nodo.FullPath, profundidad, padre, tipos,
				// Una carpeta con hijas no lista objetos por su cuenta: los suyos son la union de
				// los de sus hijas (asi lo construye OrderedUnion en Core) y se veran al abrirlas.
				nodo.Children.Count == 0 ? tipos : new int[0]);

			foreach (CategoryTreeNodeData hijo in nodo.Children) {
				CarpetaInvestigacion carpetaHija = Convertir(hijo, carpeta, profundidad + 1, yaColocados, soloVanilla);
				if (carpetaHija != null) {
					carpeta.Hijos.Add(carpetaHija);
				}
			}

			foreach (int tipo in tipos) {
				yaColocados.Add(tipo);
			}

			return carpeta;
		}

		private static int _versionContada = -1;

		/// <summary>
		/// Recalcula el "x/N" de todas las carpetas. Se hace solo cuando el estado de
		/// investigacion ha cambiado de verdad, mirando <c>LastEditId</c> del tracker oficial
		/// (sube con cada edicion), en vez de recontar decenas de miles de ids cada fotograma.
		/// </summary>
		public static bool RefrescarContadoresSiHaceFalta()
		{
			int version = EstadoInvestigacion.VersionDeEstado;
			if (version == _versionContada) {
				return false;
			}
			RefrescarContadores();
			return true;
		}

		/// <summary>Recalcula el "x/N" de todas las carpetas, pase lo que pase.</summary>
		public static void RefrescarContadores()
		{
			_versionContada = EstadoInvestigacion.VersionDeEstado;
			foreach (CarpetaInvestigacion raiz in _raices) {
				Contar(raiz);
			}
		}

		private static void Contar(CarpetaInvestigacion carpeta)
		{
			int hechos = 0;
			for (int i = 0; i < carpeta.Tipos.Length; i++) {
				if (EstadoInvestigacion.Completo(carpeta.Tipos[i])) {
					hechos++;
				}
			}
			carpeta.Hechos = hechos;

			for (int i = 0; i < carpeta.Hijos.Count; i++) {
				Contar(carpeta.Hijos[i]);
			}
		}

		/// <summary>Vacia el arbol (al descargar el mod).</summary>
		public static void Descargar()
		{
			_raices = new List<CarpetaInvestigacion>();
			_bytesArbol = null;
			_bytesEtiquetas = null;
			_versionContada = -1;
			ResumenConstruccion = "(sin construir)";
		}
	}
}
