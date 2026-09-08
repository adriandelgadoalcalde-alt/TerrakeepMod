using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using Terrakeep.Core.Data;

namespace TerrakeepMod.Common.Libreria
{
	/// <summary>
	/// Arbol de carpetas de la Libreria (WS3). Es un <b>hibrido</b>, y las dos mitades son
	/// deliberadas:
	/// <list type="number">
	/// <item><b>El arbol vanilla CURADO</b>, el mismo de Terrasavr y de la app de escritorio
	/// ("Materials / Pre-Hardmode / Copper &amp; Tin", "Categories / Weapons / Melee damage"...),
	/// con su orden real hecho a mano - eso no se puede deducir de los campos de un
	/// <c>Item</c>, hay que traerlo. Sale de <c>Assets/vanilla_library_tree.json</c> +
	/// <c>Assets/vanilla_library_labels_es.json</c>, empaquetados en el <c>.tmod</c>, y lo monta
	/// <c>LibraryTreeBuilder.BuildItemTree</c> de <c>Terrakeep.Core</c> (portado por WS2),
	/// sin reimplementar nada.</item>
	/// <item><b>Una carpeta madre por MOD instalado</b> ("CalamityMod (N)"), construida EN VIVO
	/// con <c>LiveItemTreeBuilder.BuildTree</c> sobre lo que descubre
	/// <see cref="CatalogoVivo"/>. Aqui NO se usa el <c>calamity/catalog.json</c> estatico de la
	/// app de escritorio: asi la Libreria del mod cubre cualquier mod instalado, no solo
	/// Calamity (decision del plan del proyecto).</item>
	/// </list>
	/// A las dos se les suma, <b>solo si hace falta</b>, una carpeta con los objetos vanilla que
	/// el arbol curado no menciona (contenido que Terrasavr no llego a catalogar): sin ella
	/// quedarian inalcanzables por navegacion.
	/// </summary>
	/// <remarks>
	/// <b>El arbol curado viene de una version de Terraria MAS NUEVA que la del juego, y hay que
	/// podarlo</b> (bug real reportado por el usuario el 8-sep-2026: "Categorías" con muchas
	/// paginas vacias, objetos que no salen y objetos mezclados que no pintan nada en su carpeta).
	/// <c>vanilla_library_tree.json</c> se extrajo del Terrasavr real, que va por Terraria
	/// <b>1.4.5.8</b> (<c>ItemID.Count = 6196</c>, ids reales hasta 6145); tModLoader va por
	/// <b>1.4.4.9</b> (<c>ItemID.Count = 5456</c>). Las tablas de ids de las dos versiones son
	/// IDENTICAS por debajo de 5456 (comprobado constante a constante contra los dos
	/// <c>ItemID.cs</c> decompilados: 5504 constantes comparadas, 0 diferencias - Terraria solo
	/// añade ids al final), asi que la unica diferencia real son los <b>690 ids que 1.4.5 añadio y
	/// aqui no existen</b>. Sin podarlos pasaba justo lo que se veia:
	/// <list type="number">
	/// <item><b>Paginas vacias</b>: 36 hojas enteras del arbol solo contenian ids de 1.4.5 (13
	/// paginas seguidas de "Colocable", "Paredes/Page 8", y 17 rangos de "Objetos por id").</item>
	/// <item><b>Objetos que no salen</b>: <c>Item.SetDefaults</c> con un id por encima de
	/// <c>ItemLoader.ItemCount</c> revienta con <c>IndexOutOfRangeException</c> en la primera linea
	/// que toca un array indexado por tipo (<c>material = ItemID.Sets.IsAMaterial[type]</c>, codigo
	/// real del <c>Item.cs</c> decompilado), asi que la rejilla se quedaba a medio montar.</item>
	/// <item><b>Todo mezclado</b>: con un mod cargado esos mismos ids SI existen - son de
	/// CalamityMod - y aparecian dentro de carpetas vanilla (1390 apariciones), incluido el icono
	/// de 40 carpetas, que es lo que el usuario describia como "los sprites indican donde deberia
	/// ir cada objeto y el contenido no se corresponde".</item>
	/// </list>
	/// La poda se hace <b>aqui, en el lado del mod</b>, y no en <c>Terrakeep.Core</c>: Core lo
	/// comparte la app de escritorio, que si corre contra 1.4.5.8 y ahi no sobra ningun id.
	/// </remarks>
	/// <remarks>
	/// <b>El icono de una carpeta viaja como texto, y es a proposito.</b> El
	/// <c>Func&lt;int,string&gt; iconResolver</c> de Core existe porque en la app de escritorio el
	/// icono es una RUTA de imagen ("pack://siteoforigin:,,,/..."). Dentro del juego no hay
	/// ninguna ruta que resolver: la textura ya esta cargada y se dibuja por id. Asi que aqui el
	/// "icono" es el id en decimal, y la interfaz lo vuelve a leer con
	/// <see cref="IdDeIcono"/>. La alternativa era cambiar la firma de Core (que consume tambien
	/// la app de escritorio) por una comodidad del mod, y no compensaba.
	/// </remarks>
	public static class ArbolLibreria
	{
		private const string RutaArbol = "Assets/vanilla_library_tree.json";
		private const string RutaEtiquetas = "Assets/vanilla_library_labels_es.json";

		private static byte[] _bytesArbol;
		private static byte[] _bytesEtiquetas;

		private static VanillaLibraryTreeCatalog _arbolVanilla;
		private static LibraryLabelCatalog _etiquetas;

		/// <summary>Idioma con el que se parsearon las etiquetas la ultima vez, para volver a
		/// parsearlas si el jugador cambia de idioma en vivo.</summary>
		private static bool _etiquetasEnEspanol;

		private static List<CategoryTreeNodeData> _raices;

		/// <summary>Carpetas de primer nivel. Vacio si todavia no se ha construido.</summary>
		public static IReadOnlyList<CategoryTreeNodeData> Raices {
			get { return _raices != null ? (IReadOnlyList<CategoryTreeNodeData>)_raices : new List<CategoryTreeNodeData>(); }
		}

		/// <summary>true si el arbol ya esta montado.</summary>
		public static bool Listo {
			get { return _raices != null; }
		}

		/// <summary>Cuantos objetos vanilla no aparecian en el arbol curado (0 en el caso normal).</summary>
		public static int VanillaSinCatalogar { get; private set; }

		/// <summary>Ids distintos del arbol curado que NO existen en esta version del juego (los que
		/// Terraria 1.4.5 añadio y 1.4.4.9 no tiene). Ver la poda.</summary>
		public static int IdsDeOtraVersion { get; private set; }

		/// <summary>Apariciones de esos ids repartidas por las carpetas (un id puede estar en
		/// varias), o sea cuantos huecos rotos habia de verdad en la interfaz.</summary>
		public static int AparicionesDeOtraVersion { get; private set; }

		/// <summary>Carpetas del arbol curado que la poda quito enteras por quedarse sin un solo
		/// objeto real.</summary>
		public static int CarpetasVaciasPodadas { get; private set; }

		/// <summary>Carpetas cuyo icono era un objeto de otra version (y con un mod cargado enseñaba
		/// el sprite de ese mod) y se sustituyo por el de su primer objeto real.</summary>
		public static int IconosCorregidos { get; private set; }

		/// <summary>Resumen de la ultima construccion, para el log de evidencia.</summary>
		public static string Resumen { get; private set; }

		/// <summary>
		/// Lee los bytes de los dos archivos de datos. Hay que hacerlo mientras el <c>.tmod</c>
		/// sigue abierto (<c>ModSystem.Load()</c>): despues, <c>TmodFile.GetStream</c> lanza
		/// <c>IOException("File not open")</c>. Mismo motivo y mismo patron que WS4.
		/// </summary>
		public static void LeerArchivos(Mod mod)
		{
			_bytesArbol = LeerRecurso(mod, RutaArbol);
			_bytesEtiquetas = LeerRecurso(mod, RutaEtiquetas);
		}

		private static byte[] LeerRecurso(Mod mod, string ruta)
		{
			try {
				using (Stream s = mod.GetFileStream(ruta))
				using (MemoryStream ms = new MemoryStream()) {
					s.CopyTo(ms);
					return ms.ToArray();
				}
			}
			catch (Exception e) {
				RegistroLibreria.Aviso($"{Terrakeep.LogTag} Libreria: no se pudo leer {ruta} del .tmod: {e.Message}");
				return null;
			}
		}

		/// <summary>Tira todo lo cacheado (descarga del mod, cambio de idioma).</summary>
		public static void Descargar()
		{
			_raices = null;
			_arbolVanilla = null;
			_etiquetas = null;
			Resumen = null;
		}

		/// <summary>Tira solo el arbol montado, conservando los archivos ya leidos. Lo usa el
		/// cambio de idioma en vivo: los nombres cambian, los datos de disco no.</summary>
		public static void Invalidar()
		{
			_raices = null;
			Resumen = null;
		}

		/// <summary>
		/// Monta el arbol si no esta montado. Necesita el catalogo vivo construido, asi que lo
		/// construye el mismo si hace falta.
		/// </summary>
		public static void ConstruirSiHaceFalta()
		{
			if (_raices != null) {
				return;
			}

			CatalogoVivo.ConstruirSiHaceFalta();

			List<CategoryTreeNodeData> raices = new List<CategoryTreeNodeData>();
			int raicesVanillaCuradas = 0;

			IdsDeOtraVersion = 0;
			AparicionesDeOtraVersion = 0;
			CarpetasVaciasPodadas = 0;
			IconosCorregidos = 0;

			// ---- 1. El arbol vanilla curado, tal cual lo monta Core, YA PODADO -----------------
			if (ParsearArchivos()) {
				// calamity = null a proposito: la carpeta madre "Calamity (mod)" de la app de
				// escritorio la sustituye aqui el descubrimiento en vivo, que ademas cubre
				// cualquier otro mod. Core ya contempla explicitamente este caso.
				List<CategoryTreeNodeData> vanilla = LibraryTreeBuilder.BuildItemTree(
					_arbolVanilla, _etiquetas, null, IconoDe);

				// La poda (ver el <remarks> de la clase): el arbol curado es de Terraria 1.4.5.8 y
				// el juego que lo enseña es 1.4.4.9.
				HashSet<int> deOtraVersion = new HashSet<int>();
				for (int i = 0; i < vanilla.Count; i++) {
					CategoryTreeNodeData podada = Podar(vanilla[i], deOtraVersion);
					if (podada != null) {
						raices.Add(podada);
					}
				}
				IdsDeOtraVersion = deOtraVersion.Count;
				raicesVanillaCuradas = raices.Count;
			}

			// ---- 2. Una carpeta madre por mod instalado, descubierta en vivo -------------------
			HashSet<int> cubiertos = new HashSet<int>();
			for (int i = 0; i < raices.Count; i++) {
				foreach (int id in raices[i].ItemIdsOrdered) {
					cubiertos.Add(id);
				}
			}

			List<LiveItemInfo> deMods = new List<LiveItemInfo>();
			List<LiveItemInfo> vanillaSueltos = new List<LiveItemInfo>();
			foreach (LiveItemInfo info in CatalogoVivo.Objetos) {
				if (info.ModName != CatalogoVivo.ModVanilla) {
					deMods.Add(info);
				}
				else if (!cubiertos.Contains(info.Id)) {
					vanillaSueltos.Add(info);
				}
			}

			int raicesDeMod = 0;
			if (deMods.Count > 0) {
				List<CategoryTreeNodeData> porMod = LiveItemTreeBuilder.BuildTree(
					deMods, IconoDe, EtiquetaCategoria, NombrePagina);
				for (int i = 0; i < porMod.Count; i++) {
					raices.Add(ConNombreBonito(porMod[i]));
				}
				raicesDeMod = porMod.Count;
			}

			// ---- 3. Lo vanilla que el arbol curado no menciona ---------------------------------
			// El arbol de Terrasavr se extrajo de una version concreta del juego; si el juego trae
			// algun objeto que no esta ahi, sin esta carpeta seria inalcanzable navegando (la
			// busqueda si lo encontraria, pero eso no es motivo para esconderlo).
			VanillaSinCatalogar = vanillaSueltos.Count;
			if (vanillaSueltos.Count > 0) {
				List<LiveItemInfo> renombrados = new List<LiveItemInfo>(vanillaSueltos.Count);
				foreach (LiveItemInfo info in vanillaSueltos) {
					renombrados.Add(new LiveItemInfo(info.Id, info.Name, SinCatalogar, info.Category) {
						EquipSlot = info.EquipSlot,
						Rarity = info.Rarity
					});
				}
				List<CategoryTreeNodeData> sueltos = LiveItemTreeBuilder.BuildTree(
					renombrados, IconoDe, EtiquetaCategoria, NombrePagina,
					SinCatalogar);
				raices.AddRange(sueltos);
			}

			_raices = raices;

			Resumen = $"{raices.Count} carpetas raiz ({raicesVanillaCuradas} del arbol vanilla curado, " +
				$"{raicesDeMod} de mods instalados, " +
				$"{(vanillaSueltos.Count > 0 ? "1 de vanilla sin catalogar" : "0 de vanilla sin catalogar")}); " +
				$"{CatalogoVivo.Objetos.Count} objetos vivos en total ({CatalogoVivo.DeMods} de mods), " +
				$"catalogo construido en {CatalogoVivo.MilisegundosConstruccion:F0} ms; " +
				$"vanilla fuera del arbol curado: {vanillaSueltos.Count}; " +
				$"poda por version del juego: {IdsDeOtraVersion} ids inexistentes " +
				$"({AparicionesDeOtraVersion} apariciones), {CarpetasVaciasPodadas} carpetas vacias " +
				$"quitadas, {IconosCorregidos} iconos corregidos";
		}

		/// <summary>
		/// Recorta un nodo del arbol curado a lo que existe DE VERDAD en esta version del juego.
		/// Devuelve <c>null</c> si la carpeta se queda sin un solo objeto real (entonces desaparece
		/// del arbol en vez de quedarse como una pagina vacia).
		/// <para />
		/// De paso arregla las dos cosas que se ven en pantalla y que dependen de esos ids:
		/// <list type="bullet">
		/// <item>El <b>recuento del rotulo</b> ("Daño de Cuerpo a Cuerpo (316)"). Se puede reescribir
		/// sin miedo porque <c>LibraryLabelCatalog.Translate</c> traduce por PLANTILLA
		/// (<c>"Melee damage ($1)"</c>) y sustituye el numero despues, asi que cambiarlo no deja
		/// ningun rotulo sin traducir. Y el numero solo puede MENGUAR, nunca crecer: el texto no
		/// puede desbordar su caja por esto.</item>
		/// <item>El <b>icono</b> de la carpeta, si era un objeto que aqui no existe (con un mod
		/// cargado enseñaba el sprite del objeto de mod que ocupa ese id). Pasa a ser el de su
		/// primer objeto real, que es el mismo criterio que usa Core para las carpetas que construye
		/// el.</item>
		/// </list>
		/// <b>No se renumeran las paginas</b> a proposito: las hojas del arbol curado van ordenadas
		/// por id, y los ids que sobran son siempre los mas altos, asi que lo que se cae es siempre
		/// la COLA ("Page 8" de una carpeta con 8 paginas, no una de en medio). Verificado hoja a
		/// hoja sobre el .json real antes de decidirlo. Renumerar ademas rompería la correspondencia
		/// con la app de escritorio, que enseña ese mismo arbol entero.
		/// </summary>
		private static CategoryTreeNodeData Podar(CategoryTreeNodeData nodo, HashSet<int> deOtraVersion)
		{
			if (nodo == null) {
				return null;
			}

			List<int> ids = new List<int>();
			HashSet<int> conjunto = new HashSet<int>();
			List<CategoryTreeNodeData> hijos = new List<CategoryTreeNodeData>();

			if (nodo.Children != null && nodo.Children.Count > 0) {
				foreach (CategoryTreeNodeData hijo in nodo.Children) {
					CategoryTreeNodeData podado = Podar(hijo, deOtraVersion);
					if (podado != null) {
						hijos.Add(podado);
					}
				}
				// Misma union ordenada que hace Core: el orden real de los hijos, sin repetir un id
				// que caiga en mas de uno.
				foreach (CategoryTreeNodeData hijo in hijos) {
					foreach (int id in hijo.ItemIdsOrdered) {
						if (conjunto.Add(id)) {
							ids.Add(id);
						}
					}
				}
				if (hijos.Count == 0) {
					CarpetasVaciasPodadas++;
					return null;
				}
			}
			else {
				if (nodo.ItemIdsOrdered != null) {
					foreach (int id in nodo.ItemIdsOrdered) {
						if (!CatalogoVivo.EsVanillaReal(id)) {
							deOtraVersion.Add(id);
							AparicionesDeOtraVersion++;
							continue;
						}
						if (conjunto.Add(id)) {
							ids.Add(id);
						}
					}
				}
				if (ids.Count == 0) {
					CarpetasVaciasPodadas++;
					return null;
				}
			}

			string icono = nodo.IconPath;
			if (!CatalogoVivo.EsVanillaReal(IdDeIcono(icono))) {
				icono = IconoDe(ids[0]);
				IconosCorregidos++;
			}

			return nodo with {
				Name = ConRecuento(nodo.Name, ids.Count),
				NameEn = ConRecuento(nodo.NameEn, ids.Count),
				IconPath = icono,
				ItemIdsOrdered = ids,
				ItemIdSet = conjunto,
				Children = hijos
			};
		}

		/// <summary>
		/// Reescribe el "(N)" final de un rotulo con el recuento de verdad. Deja intacto cualquier
		/// nombre que no acabe asi ("Materials", "5201-5240", "(&lt; 0)").
		/// </summary>
		private static string ConRecuento(string nombre, int objetos)
		{
			if (string.IsNullOrEmpty(nombre)) {
				return nombre;
			}

			int cierre = nombre.Length - 1;
			if (nombre[cierre] != ')') {
				return nombre;
			}
			int i = cierre - 1;
			while (i >= 0 && nombre[i] >= '0' && nombre[i] <= '9') {
				i--;
			}
			if (i < 0 || i == cierre - 1 || nombre[i] != '(') {
				return nombre;   // No hay ningun numero entre parentesis al final.
			}

			return nombre.Substring(0, i + 1)
				+ objetos.ToString(CultureInfo.InvariantCulture)
				+ ")";
		}

		/// <summary>
		/// Parsea los dos <c>.json</c> del arbol curado, eligiendo las etiquetas segun el idioma.
		/// <para />
		/// <b>El archivo de etiquetas solo existe en español</b>
		/// (<c>vanilla_library_labels_es.json</c>, el mismo de la app de escritorio). Pero el arbol
		/// en si (<c>vanilla_library_tree.json</c>) trae los nombres <b>en ingles</b> - "Materials",
		/// "Pre-Hardmode", "Copper &amp; Tin" - porque son la CLAVE con la que se busca la
		/// traduccion. Y <c>LibraryLabelCatalog.Lookup</c> devuelve la clave tal cual cuando no la
		/// encuentra. O sea que, para tener el arbol en ingles, no hay que traducir nada: basta con
		/// darle un catalogo de etiquetas VACIO. Es lo que se hace aqui con <c>"{}"</c>.
		/// </summary>
		private static bool ParsearArchivos()
		{
			bool espanol = Idiomas.EnEspanol;
			if (_arbolVanilla != null && _etiquetas != null && _etiquetasEnEspanol == espanol) {
				return true;
			}
			if (_bytesArbol == null || _bytesEtiquetas == null) {
				return false;
			}

			try {
				using (MemoryStream a = new MemoryStream(_bytesArbol)) {
					_arbolVanilla = VanillaLibraryTreeCatalog.LoadFromStream(a);
				}
				byte[] bytesEtiquetas = espanol
					? _bytesEtiquetas
					: System.Text.Encoding.UTF8.GetBytes("{}");
				using (MemoryStream e = new MemoryStream(bytesEtiquetas)) {
					_etiquetas = LibraryLabelCatalog.LoadFromStream(e);
				}
				_etiquetasEnEspanol = espanol;
				return true;
			}
			catch (Exception e) {
				RegistroLibreria.Error(
					$"{Terrakeep.LogTag} Libreria: no se pudo parsear el arbol vanilla curado: {e.Message}. " +
					"Se sigue solo con el descubrimiento en vivo.");
				_arbolVanilla = null;
				_etiquetas = null;
				return false;
			}
		}

		/// <summary>
		/// Cambia el rotulo de una carpeta madre de mod por el nombre BONITO del mod
		/// ("Calamity (mod)" en vez de "CalamityMod (2851)"), que es como lo llama la app de
		/// escritorio y como lo llama el propio juego en su lista de mods.
		/// <para />
		/// Solo se toca el rotulo: <c>FullPath</c> se queda con el nombre INTERNO del mod, que es
		/// la clave estable (dos mods pueden compartir nombre bonito, nunca el interno). Se hace
		/// aqui y no dentro de Core porque Core no puede preguntarle a tModLoader como se llama un
		/// mod. <c>CategoryTreeNodeData</c> es un <c>record</c>, asi que <c>with</c> devuelve una
		/// copia con un solo campo cambiado, sin duplicar las listas de ids que ya se calcularon.
		/// </summary>
		private static CategoryTreeNodeData ConNombreBonito(CategoryTreeNodeData raiz)
		{
			Mod mod;
			if (!ModLoader.TryGetMod(raiz.FullPath, out mod) || mod == null) {
				return raiz;
			}

			string bonito = string.IsNullOrWhiteSpace(mod.DisplayName) ? raiz.FullPath : mod.DisplayName;
			return raiz with { Name = Idiomas.Texto("Libreria.CarpetaDeMod", bonito) };
		}

		/// <summary>Etiqueta de una pagina de una carpeta hoja con mas de 40 objetos.</summary>
		private static string NombrePagina(int numero)
		{
			return Idiomas.Texto("Libreria.Pagina", numero);
		}

		/// <summary>Rotulo de la carpeta con lo vanilla que el arbol curado no menciona.</summary>
		private static string SinCatalogar {
			get { return Idiomas.Texto("Libreria.SinCatalogar"); }
		}

		/// <summary>
		/// Traduce la clave de categoria que produce <see cref="CatalogoVivo"/> ("Weapons/Melee",
		/// "Accessories/Wings"...) al rotulo que se ve en el arbol.
		/// <para />
		/// En español se usa <c>LibraryTreeBuilder.CalamityCategoryLabel</c> de
		/// <c>Terrakeep.Core</c>, que trae las 121 categorias traducidas a mano. Esa tabla
		/// <b>solo existe en español</b> y vive en el repositorio hermano, asi que en cualquier otro
		/// idioma se usa la clave en ingles separando su CamelCase, que es exactamente lo que hace
		/// esa misma funcion de Core con una categoria que no conoce (y lo que hacen los propios
		/// nombres internos del juego). Resultado: "Weapons/Melee" -&gt; "Weapons / Melee".
		/// </summary>
		private static string EtiquetaCategoria(string clave)
		{
			if (Idiomas.EnEspanol) {
				return LibraryTreeBuilder.CalamityCategoryLabel(clave);
			}
			return SepararCamelCase(clave);
		}

		/// <summary>"Weapons/Melee" -&gt; "Weapons / Melee"; "HairDye" -&gt; "Hair Dye".</summary>
		private static string SepararCamelCase(string clave)
		{
			if (string.IsNullOrEmpty(clave)) {
				return clave;
			}

			System.Text.StringBuilder salida = new System.Text.StringBuilder(clave.Length + 8);
			for (int i = 0; i < clave.Length; i++) {
				char c = clave[i];
				if (c == '/') {
					salida.Append(" / ");
					continue;
				}
				if (i > 0 && char.IsUpper(c) && !char.IsUpper(clave[i - 1]) && clave[i - 1] != '/') {
					salida.Append(' ');
				}
				salida.Append(c);
			}
			return salida.ToString();
		}

		/// <summary>Lo que Core guarda como "ruta del icono": aqui, el id del objeto en decimal.</summary>
		private static string IconoDe(int id)
		{
			return id > 0 ? id.ToString(CultureInfo.InvariantCulture) : null;
		}

		/// <summary>Vuelve a sacar el id de objeto de un <c>IconPath</c>. 0 = sin icono.</summary>
		public static int IdDeIcono(string iconPath)
		{
			int id;
			return !string.IsNullOrEmpty(iconPath) && int.TryParse(iconPath, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
				? id : 0;
		}
	}
}
