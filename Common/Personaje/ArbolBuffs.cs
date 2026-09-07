using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Libreria;
using TerrasavrNative.Core.Data;

namespace TerrakeepMod.Common.Personaje
{
	/// <summary>
	/// Arbol de carpetas de "Añadir un buff" (pestaña Buffs). Cierra un bug real reportado por el
	/// usuario ("no salen todos los buffs que se pueden aplicar... igual que en Terrakeep"), que
	/// resulta ser el MISMO bug que ya se dio y se corrigio en la app de escritorio antes de la
	/// "Fase 2" del rework de Buffs (ver <c>bitacora.md</c> del repo hermano, seccion "Libreria de
	/// buffs con arbol real de Terrasavr"): un picker de "buscar+aplicar" plano, sin ninguna
	/// categoria, con un tope de resultados bajo (aqui, 12) que solo enseñaba los primeros buffs
	/// por id salvo que se supiera EXACTAMENTE que buscar. La correccion real de la app de
	/// escritorio no fue subir el tope: fue sustituir el picker plano por una Libreria de buffs de
	/// verdad (arbol de carpetas + busqueda), y eso es lo que se porta aqui.
	/// <para />
	/// Es un <b>hibrido</b>, exactamente igual que <see cref="ArbolLibreria"/> para objetos:
	/// <list type="number">
	/// <item>El arbol CURADO real de Terrasavr (<c>BuffTreeBuilder.BuildBuffTree</c> de
	/// <c>TerrasavrNative.Core</c>, sin reimplementar nada): 6 categorias curadas (Utilidad,
	/// Offensivo, Defensivo, Special, Mascota, Negativo) mas un "Indice" paginado que cubre TODOS
	/// los buffs VANILLA conocidos (1..<c>BuffID.Count</c>-1), asi que ningun buff vanilla puede
	/// quedar fuera del arbol aunque no encaje en ninguna categoria curada.</item>
	/// <item>Una carpeta madre por MOD instalado, construida EN VIVO (igual que
	/// <see cref="CatalogoVivo"/> hace con los objetos) con <c>LiveItemTreeBuilder.BuildTree</c>
	/// sobre los buffs con <c>tipo &gt;= BuffID.Count</c> (el mismo corte real que usa
	/// <c>BuffLoader.GetBuff</c> para decidir si un buff es de mod), asi que cubre CUALQUIER mod
	/// cargado, no solo Calamity.</item>
	/// </list>
	/// Entre las dos partes cubren <c>BuffLoader.BuffCount - 1</c> buffs exactos: no hay ningun
	/// "resto sin catalogar" posible, a diferencia de la Libreria de objetos (ahi si puede sobrar
	/// vanilla fuera del arbol curado, porque ese arbol lo extrajo Terrasavr de un juego con menos
	/// contenido). Aqui el "Indice" ya esta pensado para cubrir el 100% de lo vanilla por diseño
	/// real de Terrasavr, y lo que no sea vanilla cae siempre en su carpeta de mod.
	/// </summary>
	/// <remarks>
	/// <b>No hace falta ningun archivo de datos en <c>Assets/</c>.</b> A diferencia del arbol de
	/// objetos (que trae <c>vanilla_library_tree.json</c> extraido de Terrasavr con nombres y
	/// orden reales), <c>BuffTreeBuilder.BuildBuffTree</c> solo necesita un
	/// <c>VanillaBuffCatalog</c> para saber QUE ids vanilla existen (para el "Indice": las 6
	/// carpetas curadas llevan sus listas de ids literales YA dentro de Core). Ese catalogo se
	/// monta aqui EN MEMORIA a partir de <c>Terraria.ID.BuffID.Count</c> (nunca de un <c>.json</c>
	/// estatico que se quedaria corto en cuanto el juego añadiera un buff nuevo): los nombres no
	/// hacen ninguna falta para la estructura del arbol (cada fila los vuelve a pedir en vivo con
	/// <see cref="PersonajeVivo.NombreBuff"/> al dibujarse, que es lo que de verdad respeta el
	/// idioma activo), asi que <c>VanillaBuffCatalog.LoadFromStream</c> se alimenta con
	/// descripciones y nombres en español vacios a proposito: no los usa nadie aqui.
	/// </remarks>
	public static class ArbolBuffs
	{
		private static List<CategoryTreeNodeData> _raices;
		private static bool _construidoEnEspanol;

		/// <summary>Carpetas de primer nivel. Vacio si todavia no se ha construido.</summary>
		public static IReadOnlyList<CategoryTreeNodeData> Raices {
			get { return _raices != null ? (IReadOnlyList<CategoryTreeNodeData>)_raices : new List<CategoryTreeNodeData>(); }
		}

		/// <summary>true si el arbol ya esta montado.</summary>
		public static bool Listo {
			get { return _raices != null; }
		}

		/// <summary>Cuantos buffs aplicables tiene de verdad el juego cargado ahora mismo
		/// (<c>BuffLoader.BuffCount - 1</c>, incluye vanilla + cualquier mod).</summary>
		public static int TotalBuffsAplicables { get; private set; }

		/// <summary>Cuantos de esos buffs aparecen de verdad en alguna carpeta del arbol (union de
		/// todas las raices). Tiene que coincidir EXACTAMENTE con <see cref="TotalBuffsAplicables"/>
		/// - es la comprobacion real de cobertura, mismo criterio que ya uso Investigacion.</summary>
		public static int CubiertosPorElArbol { get; private set; }

		/// <summary>Resumen de la ultima construccion, para el log de evidencia.</summary>
		public static string Resumen { get; private set; }

		/// <summary>Tira el arbol montado. El idioma se comprueba solo tambien dentro de
		/// <see cref="ConstruirSiHaceFalta"/>, pero esto fuerza una reconstruccion inmediata (lo
		/// usa el arnes de pruebas).</summary>
		public static void Invalidar()
		{
			_raices = null;
			Resumen = null;
		}

		/// <summary>
		/// Monta el arbol si no esta montado, o si el idioma activo ha cambiado desde la ultima
		/// vez (las 6 carpetas curadas y "Indice" llevan su nombre en español Y en ingles ya
		/// calculados por Core; aqui se elige uno de los dos UNA vez, no en cada fotograma, igual
		/// que <see cref="ArbolLibreria"/> hace con sus etiquetas).
		/// </summary>
		public static void ConstruirSiHaceFalta()
		{
			bool espanol = Idiomas.EnEspanol;
			if (_raices != null && _construidoEnEspanol == espanol) {
				return;
			}

			int topeReal = BuffLoader.BuffCount;     // exclusivo; vanilla + TODOS los mods cargados
			int topeVanilla = BuffID.Count;          // exclusivo; solo vanilla, el mismo corte real
			                                          // que usa BuffLoader.GetBuff para decidir si
			                                          // un tipo es "de mod".

			List<CategoryTreeNodeData> raices = new List<CategoryTreeNodeData>();
			int raicesVanillaCuradas = 0;

			// ---- 1. El arbol curado + "Indice" real de Terrasavr, tal cual lo monta Core --------
			VanillaBuffCatalog vanilla = MontarCatalogoVanillaEnMemoria(topeVanilla, topeReal);
			if (vanilla != null) {
				List<CategoryTreeNodeData> arbolVanilla = BuffTreeBuilder.BuildBuffTree(
					vanilla, null, null, IconoDe, IconoDe);
				if (!espanol) {
					arbolVanilla = AplicarIdioma(arbolVanilla);
				}
				raices.AddRange(arbolVanilla);
				raicesVanillaCuradas = arbolVanilla.Count;
			}

			// ---- 2. Una carpeta madre por MOD instalado, descubierta en vivo --------------------
			// Todo lo que BuffLoader.GetBuff resuelve (tipo >= BuffID.Count) es, por definicion,
			// de un mod: no hace falta comprobar cobertura contra el arbol vanilla como si hiciera
			// ArbolLibreria (el "Indice" de arriba YA cubre el 100% de 1..topeVanilla-1 por diseño
			// real de Terrasavr, asi que aqui nunca puede sobrar nada vanilla).
			List<LiveItemInfo> deMods = new List<LiveItemInfo>();
			for (int tipo = topeVanilla; tipo < topeReal; tipo++) {
				string nombre = PersonajeVivo.NombreBuff(tipo);
				if (string.IsNullOrEmpty(nombre)) {
					continue;
				}
				ModBuff propio = BuffLoader.GetBuff(tipo);
				string mod = propio != null && propio.Mod != null ? propio.Mod.Name : CatalogoVivo.ModVanilla;
				deMods.Add(new LiveItemInfo(tipo, nombre, mod, "Otros"));
			}

			int raicesDeMod = 0;
			if (deMods.Count > 0) {
				List<CategoryTreeNodeData> porMod = LiveItemTreeBuilder.BuildTree(
					deMods, IconoDe, EtiquetaCategoria, NombrePagina, CatalogoVivo.ModVanilla);
				for (int i = 0; i < porMod.Count; i++) {
					raices.Add(ConNombreBonito(porMod[i]));
				}
				raicesDeMod = porMod.Count;
			}

			_raices = raices;
			_construidoEnEspanol = espanol;

			HashSet<int> cubiertos = new HashSet<int>();
			for (int i = 0; i < raices.Count; i++) {
				foreach (int id in raices[i].ItemIdsOrdered) {
					cubiertos.Add(id);
				}
			}

			TotalBuffsAplicables = topeReal - 1;
			CubiertosPorElArbol = cubiertos.Count;

			Resumen = $"{raices.Count} carpetas raiz ({raicesVanillaCuradas} del arbol curado+Indice de " +
				$"Terrasavr, {raicesDeMod} de mods instalados). Buffs aplicables segun el juego " +
				$"(BuffLoader.BuffCount-1): {TotalBuffsAplicables}; cubiertos por el arbol: {CubiertosPorElArbol}" +
				(CubiertosPorElArbol == TotalBuffsAplicables ? " (cuadra)." : " (NO cuadra).");
		}

		/// <summary>
		/// Monta un <see cref="VanillaBuffCatalog"/> EN MEMORIA (nunca desde disco): solo hace
		/// falta que <c>AllEntries()</c> devuelva un id por cada buff vanilla real, para que
		/// <c>BuffTreeBuilder.BuildIndex</c> pueda paginarlos todos. Nombres/descripciones en
		/// español van vacios a proposito (ver el <c>remarks</c> de la clase).
		/// </summary>
		private static VanillaBuffCatalog MontarCatalogoVanillaEnMemoria(int topeVanilla, int topeReal)
		{
			try {
				Dictionary<string, string> nombres = new Dictionary<string, string>();
				int limite = Math.Min(topeVanilla, topeReal);
				for (int tipo = 1; tipo < limite; tipo++) {
					nombres[tipo.ToString(CultureInfo.InvariantCulture)] = PersonajeVivo.NombreBuff(tipo);
				}

				using (MemoryStream n = JsonDeString(nombres))
				using (MemoryStream d = JsonDeString(new Dictionary<string, string>()))
				using (MemoryStream e = JsonDeString(new Dictionary<string, string>())) {
					return VanillaBuffCatalog.LoadFromStream(n, d, e);
				}
			}
			catch (Exception ex) {
				Terrakeep.Instance.Logger.Error(
					$"{Terrakeep.LogTag} Buffs: no se pudo montar el catalogo vanilla en memoria: {ex.Message}. " +
					"El arbol se queda solo con lo descubierto en vivo (carpetas de mod).");
				return null;
			}
		}

		private static MemoryStream JsonDeString(Dictionary<string, string> diccionario)
		{
			byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(diccionario);
			return new MemoryStream(bytes, writable: false);
		}

		/// <summary>Sustituye recursivamente <c>Name</c> por <c>NameEn</c> (si lo hay) en un arbol
		/// entero. Se llama UNA vez al construir, nunca en cada fotograma - mismo criterio que
		/// <see cref="ArbolLibreria"/>.</summary>
		private static List<CategoryTreeNodeData> AplicarIdioma(List<CategoryTreeNodeData> nodos)
		{
			List<CategoryTreeNodeData> salida = new List<CategoryTreeNodeData>(nodos.Count);
			for (int i = 0; i < nodos.Count; i++) {
				salida.Add(AplicarIdioma(nodos[i]));
			}
			return salida;
		}

		private static CategoryTreeNodeData AplicarIdioma(CategoryTreeNodeData nodo)
		{
			IReadOnlyList<CategoryTreeNodeData> hijos = nodo.Children != null && nodo.Children.Count > 0
				? AplicarIdioma(new List<CategoryTreeNodeData>(nodo.Children))
				: nodo.Children;
			string nombre = string.IsNullOrEmpty(nodo.NameEn) ? nodo.Name : nodo.NameEn;
			return nodo with { Name = nombre, Children = hijos };
		}

		/// <summary>Cambia el rotulo de una carpeta madre de mod por el nombre BONITO del mod
		/// ("CalamityMod" -&gt; "Calamity (mod)"), igual que <see cref="ArbolLibreria"/> hace para
		/// objetos.</summary>
		private static CategoryTreeNodeData ConNombreBonito(CategoryTreeNodeData raiz)
		{
			Mod mod;
			if (!ModLoader.TryGetMod(raiz.FullPath, out mod) || mod == null) {
				return raiz;
			}

			string bonito = string.IsNullOrWhiteSpace(mod.DisplayName) ? raiz.FullPath : mod.DisplayName;
			return raiz with { Name = Idiomas.Texto("Personaje.Buffs.Arbol.CarpetaDeMod", bonito) };
		}

		/// <summary>Unica categoria real que se usa para los buffs de mod: no hay ningun dato de
		/// categoria real que leer (a diferencia de <c>calamity/buffs.json</c> en la app de
		/// escritorio, que aqui no se porta), asi que se deja como "Otros" en vez de inventar una
		/// categorizacion que no se pueda demostrar.</summary>
		private static string EtiquetaCategoria(string clave)
		{
			return Idiomas.Texto("Personaje.Buffs.Arbol.Otros");
		}

		private static string NombrePagina(int numero)
		{
			return Idiomas.Texto("Personaje.Buffs.Arbol.Pagina", numero);
		}

		/// <summary>Lo que Core guarda como "ruta del icono": aqui, el id de buff en decimal (no
		/// hay ninguna ruta que resolver dentro del juego, la textura del buff ya esta cargada por
		/// id - mismo criterio que <see cref="ArbolLibreria.IconoDe"/>).</summary>
		private static string IconoDe(int id)
		{
			return id > 0 ? id.ToString(CultureInfo.InvariantCulture) : null;
		}

		/// <summary>Vuelve a sacar el id de buff de un <c>IconPath</c>. 0 = sin icono.</summary>
		public static int IdDeIcono(string iconPath)
		{
			int id;
			return !string.IsNullOrEmpty(iconPath) && int.TryParse(iconPath, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
				? id : 0;
		}
	}
}
