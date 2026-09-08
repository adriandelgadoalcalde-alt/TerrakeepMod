using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terrakeep.Core.Data;
using TerrakeepMod.UI.Libreria;

namespace TerrakeepMod.Common.Libreria
{
	/// <summary>
	/// Arnes de recuento del arbol de la Libreria: cuadra <b>objeto a objeto</b> lo que enseña el
	/// arbol contra la tabla real del juego cargado, igual que ya se hizo con Buffs y con
	/// Investigacion. Se dispara con la variable de entorno
	/// <see cref="Variable"/> y escribe su propio archivo de evidencia.
	/// </summary>
	/// <remarks>
	/// <b>Por que un arnes aparte y no un paso mas de <see cref="AutopruebaLibreria"/></b>: la
	/// autoprueba de WS3 recorre la interfaz entera (incluido el editor de prefijo, que otro agente
	/// esta tocando ahora mismo), y lo que aqui se quiere medir no depende de ningun clic - es el
	/// arbol de datos contra <c>ContentSamples.ItemsByType</c>. Separandolo, este recuento sale
	/// aunque cualquier otra parte de la interfaz este a medias, y no hay que tocar un archivo que
	/// otro agente tiene abierto.
	/// <para />
	/// Lo que comprueba, en este orden:
	/// <list type="number">
	/// <item><b>Cobertura</b>: cuantos objetos vanilla REALES tiene el juego cargado y cuantos de
	/// ellos aparecen en alguna carpeta. Los que falten se listan.</item>
	/// <item><b>Salud del arbol</b>: ninguna carpeta vacia, ningun id que no exista, ningun icono
	/// que no sea un objeto real de esta version del juego, ningun objeto de un MOD colado dentro
	/// del arbol vanilla curado.</item>
	/// <item><b>Clasificacion real</b>: para cada carpeta de "Categories" con un criterio
	/// comprobable, se mira el <see cref="Item"/> real de TODOS sus objetos y se cuenta cuantos NO
	/// cumplen el criterio de la carpeta (una espada en "Daño de cuerpo a cuerpo" tiene que tener
	/// <c>DamageType</c> melee de verdad, no valernos de que el arbol lo diga).</item>
	/// <item><b>La Cenit</b> (<c>ItemID.Zenith</c>), el objeto concreto que pidio el usuario: en que
	/// carpetas cae exactamente.</item>
	/// </list>
	/// </remarks>
	public static class AuditoriaCategorias
	{
		/// <summary>Variable de entorno que la enciende.</summary>
		public const string Variable = "TERRAKEEP_AUDIT_CATEGORIAS";

		/// <summary>Archivo de evidencia propio, dentro de la carpeta de guardado que se este
		/// usando (<c>-tmlsavedirectory</c>), por el mismo motivo que el de WS3: el
		/// <c>client.log</c> es unico para todas las instancias del juego.</summary>
		public const string NombreArchivo = "terrakeep-categorias-evidencia.log";

		/// <summary>Marca que busca el script de verificacion para saber que termino.</summary>
		public const string MarcaFinal = "AUDITORIA CATEGORIAS COMPLETA";

		/// <summary>Cuantos objetos de una carpeta se listan por su nombre en el informe.</summary>
		private const int MuestraPorCarpeta = 8;

		/// <summary>Fotogramas de margen entre paso y paso de la parte visual: la interfaz solo
		/// existe de verdad despues de un <c>Recalculate</c> + <c>Draw</c>, y la captura recoge el
		/// fotograma YA PRESENTADO (el anterior).</summary>
		private static int FotogramasEntrePasos = 15;

		private static bool _comprobada;
		private static bool _activa;
		private static bool _hecha;
		private static int _fotogramas;

		private static int _paso;
		private static int _espera;

		/// <summary>Llamada una vez por fotograma desde <see cref="PanelLibreriaSystem"/>.</summary>
		public static void Actualizar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
				if (_activa) {
					// Latido: deja constancia de que el mod cargo y de que la variable llego, aunque
					// luego no se llegue a entrar al mundo. Sin esto, un arranque lento y un fallo de
					// carga se ven exactamente igual desde fuera (archivo de evidencia inexistente).
					Escribir($"{Variable} detectada. Esperando a estar dentro del mundo...");
				}
			}

			if (!_activa || _hecha) {
				return;
			}

			// Hay que estar DENTRO del mundo: ContentSamples ya esta relleno desde la carga, pero
			// Main.LocalPlayer y las texturas no, y el informe menciona los dos.
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramas = 0;
				return;
			}

			_fotogramas++;
			if (_fotogramas < 120) {   // ~2 s a 60 fps.
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			try {
				EjecutarPaso(_paso);
			}
			catch (Exception e) {
				Escribir($"AUDITORIA CATEGORIAS: EXCEPCION en el paso {_paso}: " + e);
				Escribir(MarcaFinal + " (con excepcion)");
				_hecha = true;
				return;
			}
			_paso++;
		}

		/// <summary>
		/// Los pasos VISUALES. Van despues del recuento y sirven para dos cosas a la vez: dejar una
		/// captura real de las paginas que antes salian vacias o mezcladas, y comprobar que montar
		/// esas mismas paginas en la interfaz de verdad ya no revienta (antes, un id imposible
		/// tumbaba la rejilla entera con <c>IndexOutOfRangeException</c> desde <c>SetDefaults</c>).
		/// </summary>
		private static void EjecutarPaso(int paso)
		{
			switch (paso) {
				case 0:
					Ejecutar();
					break;
				case 1:
					PanelLibreriaSystem.AbrirPanel("auditoria de categorias");
					break;
				case 2:
					Navegar("Categories", "Weapons", "Melee damage");
					break;
				case 3:
					// Ya no son "Pagina 1..8": son Espadas, Lanzas, Mayales, Yoyos, Bumeranes,
					// Picos, Taladros, Motosierras, Hachas, Martillos...
					Capturar("categorias-melee-carpetas");
					break;
				case 4:
					Navegar("Categories", "Weapons", "Melee damage", "Swords");
					break;
				case 5:
					Capturar("categorias-melee-espadas");
					break;
				case 6:
					Navegar("Categories", "Weapons", "Melee damage", "Pickaxes");
					break;
				case 7:
					Capturar("categorias-melee-picos");
					break;
				case 8:
					Navegar("Categories", "Weapons", "Ranged damage");
					break;
				case 9:
					Capturar("categorias-distancia-carpetas");
					break;
				case 10:
					Navegar("Categories", "Weapons", "Ranged damage", "Bows");
					break;
				case 11:
					Capturar("categorias-distancia-arcos");
					break;
				case 12:
					Navegar("Categories", "Weapons", "Ranged damage", "Guns");
					break;
				case 13:
					Capturar("categorias-distancia-armas-de-fuego");
					break;
				case 14:
					// "Colocable" tenia 81 paginas seguidas por id (y 14 vacias al final antes de
					// la poda); ahora son carpetas por tipo real de objeto colocable.
					Navegar("Categories", "Placeable");
					break;
				case 15:
					Capturar("categorias-colocable-carpetas");
					break;
				case 16:
					Navegar("Categories", "Placeable", "Blocks");
					break;
				case 17:
					Capturar("categorias-colocable-bloques");
					break;
				case 18:
					// Un subgrupo de mas de 40 objetos se sigue paginando por dentro, pero ya sin
					// mezclar tipos: la ultima pagina de "Bloques" son bloques y solo bloques.
					AbrirUltimaSubcarpeta();
					break;
				case 19:
					Capturar("categorias-colocable-bloques-ultima-pagina");
					break;
				case 20:
					Navegar("Categories", "Equipable", "Accessories");
					break;
				case 21:
					Capturar("categorias-accesorios-carpetas");
					break;
				default:
					Escribir(MarcaFinal);
					_hecha = true;
					break;
			}
		}

		private static ContenidoLibreria PanelUI {
			get { return PanelLibreriaSystem.PanelActual; }
		}

		/// <summary>Entra por el arbol siguiendo nombres INGLESES sin recuento.</summary>
		private static void Navegar(params string[] ruta)
		{
			ContenidoLibreria panel = PanelUI;
			if (panel == null) {
				Escribir("   [!] el panel de la Libreria no esta montado; no se puede navegar.");
				return;
			}

			panel.IrALaRaiz();
			string acumulada = "";
			foreach (string segmento in ruta) {
				acumulada = acumulada.Length == 0 ? segmento : acumulada + "/" + segmento;
				CategoryTreeNodeData nodo = Buscar(acumulada);
				if (nodo == null) {
					Escribir($"   [!] no se encontro \"{acumulada}\" al navegar.");
					return;
				}
				panel.AbrirCarpeta(nodo);
			}
			Escribir("NAVEGACION: " + panel.Informe());
		}

		private static void AbrirUltimaSubcarpeta()
		{
			ContenidoLibreria panel = PanelUI;
			if (panel == null || panel.CarpetaActual == null) {
				return;
			}
			IReadOnlyList<CategoryTreeNodeData> hijas = panel.CarpetaActual.Children;
			if (hijas == null || hijas.Count == 0) {
				Escribir("NAVEGACION: \"" + panel.CarpetaActual.Name + "\" ya es una hoja.");
				return;
			}
			panel.AbrirCarpeta(hijas[hijas.Count - 1]);
			Escribir("NAVEGACION (ultima subcarpeta): " + panel.Informe());
		}

		private static void Capturar(string nombre)
		{
			Escribir($"CAPTURA \"{nombre}\": " + TerrakeepMod.Common.Panel.CapturaDePantalla.Guardar(nombre));
		}

		// =============================================================================================

		private static void Ejecutar()
		{
			ArbolLibreria.ConstruirSiHaceFalta();

			Escribir("Arbol: " + ArbolLibreria.Resumen);
			Escribir($"Version del juego: ItemID.Count={ItemID.Count} (vanilla), " +
				$"ItemLoader.ItemCount={ItemLoader.ItemCount} (con mods).");

			// ---- 1. Cobertura objeto a objeto -------------------------------------------------
			List<int> vanillaReales = new List<int>();
			for (int tipo = 1; tipo < ItemID.Count; tipo++) {
				if (CatalogoVivo.EsVanillaReal(tipo)) {
					vanillaReales.Add(tipo);
				}
			}

			HashSet<int> enElArbol = new HashSet<int>();
			HashSet<int> enElCurado = new HashSet<int>();
			int hojasVacias = 0;
			int idsImposibles = 0;
			int iconosImposibles = 0;
			int deModEnElCurado = 0;
			List<string> problemas = new List<string>();

			IReadOnlyList<CategoryTreeNodeData> raices = ArbolLibreria.Raices;
			for (int i = 0; i < raices.Count; i++) {
				bool esCurada = EsRaizVanillaCurada(raices[i]);
				Recorrer(raices[i], esCurada, enElArbol, enElCurado, problemas,
					ref hojasVacias, ref idsImposibles, ref iconosImposibles, ref deModEnElCurado);
			}

			List<int> huerfanos = new List<int>();
			foreach (int tipo in vanillaReales) {
				if (!enElArbol.Contains(tipo)) {
					huerfanos.Add(tipo);
				}
			}

			Escribir($"RECUENTO 1 (cobertura): el juego cargado tiene {vanillaReales.Count} objetos " +
				$"vanilla reales (ids 1..{ItemID.Count - 1} con muestra y nombre). " +
				$"El arbol vanilla curado cubre {enElCurado.Count}; el arbol entero (curado + carpetas " +
				$"de mod + 'sin catalogar') cubre {enElArbol.Count} vanilla. " +
				$"Huerfanos (vanilla real en ninguna carpeta): {huerfanos.Count}.");
			if (huerfanos.Count > 0) {
				Escribir("   huerfanos: " + Listar(huerfanos, 20));
			}

			Escribir($"RECUENTO 2 (salud del arbol): carpetas vacias={hojasVacias}, " +
				$"ids que no existen en el juego={idsImposibles}, " +
				$"iconos de carpeta que no son un objeto real={iconosImposibles}, " +
				$"objetos de MOD colados dentro del arbol vanilla curado={deModEnElCurado}. " +
				$"(La poda quito {ArbolLibreria.IdsDeOtraVersion} ids de otra version del juego, " +
				$"{ArbolLibreria.AparicionesDeOtraVersion} apariciones, " +
				$"{ArbolLibreria.CarpetasVaciasPodadas} carpetas y corrigio " +
				$"{ArbolLibreria.IconosCorregidos} iconos.)");
			for (int i = 0; i < problemas.Count && i < 30; i++) {
				Escribir("   PROBLEMA: " + problemas[i]);
			}

			// ---- 2. Clasificacion real, carpeta a carpeta -------------------------------------
			Escribir("RECUENTO 3 (clasificacion, criterio comprobado sobre el Item real del juego). " +
				"OJO al leerlo: el arbol curado NO clasifica por los campos del Item, sino por la tabla " +
				"'metatype' que Terrasavr trae curada a mano (script.js real: \"Melee damage\" = " +
				"metatype contiene 'd', \"Wings\" = el tooltip dice \"allows flight\"...). Lo que se cuenta " +
				"aqui es la DISCREPANCIA entre las dos, no un error: un puñado de casos frontera " +
				"(armas cuyo daño base es 0, bichos capturables que Terrasavr trata como colocables, " +
				"cajas de musica como vanidad) es lo esperado. Lo que tiene que salir a cero son los " +
				"RECUENTOS 1 y 2.");
			int carpetasComprobadas = 0;
			int objetosComprobados = 0;
			int fallos = 0;
			foreach (KeyValuePair<string, Criterio> par in Criterios()) {
				CategoryTreeNodeData nodo = Buscar(par.Key);
				if (nodo == null) {
					if (!par.Value.Opcional) {
						Escribir($"   [!] no se encontro la carpeta \"{par.Key}\" en el arbol.");
					}
					continue;
				}

				carpetasComprobadas++;
				List<int> malos = new List<int>();
				foreach (int id in nodo.ItemIdsOrdered) {
					objetosComprobados++;
					Item muestra;
					if (!ContentSamples.ItemsByType.TryGetValue(id, out muestra) || muestra == null) {
						malos.Add(id);
						continue;
					}
					if (!par.Value.Cumple(muestra)) {
						malos.Add(id);
					}
				}
				fallos += malos.Count;

				Escribir($"   \"{nodo.Name}\" ({par.Key}): {nodo.ItemIdsOrdered.Count} objetos, " +
					$"{nodo.Children.Count} subcarpetas, {malos.Count} que NO cumplen \"{par.Value.Descripcion}\"" +
					(malos.Count > 0 ? " -> " + Listar(malos, 10) : "") + ".");
				Escribir("      muestra: " + Nombres(nodo.ItemIdsOrdered, MuestraPorCarpeta));
			}
			Escribir($"   TOTAL: {carpetasComprobadas} carpetas, {objetosComprobados} comprobaciones " +
				$"objeto a objeto, {fallos} mal clasificados.");

			// ---- 3. El objeto que pidio el usuario --------------------------------------------
			ComprobarObjeto(ItemID.Zenith, "Categories/Weapons/Melee damage");
			ComprobarObjeto(ItemID.TerraBlade, "Categories/Weapons/Melee damage");
			ComprobarObjeto(ItemID.Meowmere, "Categories/Weapons/Melee damage");
			ComprobarObjeto(ItemID.LastPrism, "Categories/Weapons/Magic damage");
			ComprobarObjeto(ItemID.SDMG, "Categories/Weapons/Ranged damage");
			ComprobarObjeto(ItemID.FireGauntlet, "Categories/Equipable/Accessories");
		}

		// =============================================================================================

		/// <summary>true si esta raiz es una de las del arbol vanilla curado (no una carpeta de mod
		/// ni la de "sin catalogar"): son las unicas que NO deben tener objetos de mods dentro.</summary>
		private static bool EsRaizVanillaCurada(CategoryTreeNodeData raiz)
		{
			// FullPath de una raiz curada es su nombre ingles del .json ("Materials", "Categories"...);
			// el de una carpeta de mod es el nombre INTERNO del mod (ver ArbolLibreria.ConNombreBonito).
			Mod mod;
			return !ModLoader.TryGetMod(raiz.FullPath, out mod)
				&& raiz.Name != Ajustes.Idiomas.Texto("Libreria.SinCatalogar");
		}

		private static void Recorrer(CategoryTreeNodeData nodo, bool curada,
			HashSet<int> enElArbol, HashSet<int> enElCurado, List<string> problemas,
			ref int hojasVacias, ref int idsImposibles, ref int iconosImposibles, ref int deModEnElCurado)
		{
			bool esHoja = nodo.Children == null || nodo.Children.Count == 0;

			if (nodo.ItemIdsOrdered == null || nodo.ItemIdsOrdered.Count == 0) {
				hojasVacias++;
				problemas.Add($"carpeta sin un solo objeto: \"{nodo.FullPath}\"");
			}

			foreach (int id in nodo.ItemIdsOrdered) {
				if (id <= 0 || id >= ItemLoader.ItemCount) {
					idsImposibles++;
					if (esHoja) {
						problemas.Add($"id inexistente {id} en \"{nodo.FullPath}\"");
					}
					continue;
				}
				if (curada) {
					if (CatalogoVivo.ModDe(id) != CatalogoVivo.ModVanilla) {
						deModEnElCurado++;
						if (esHoja) {
							problemas.Add($"objeto de mod {id} (\"{CatalogoVivo.Nombre(id)}\", " +
								$"{CatalogoVivo.ModDe(id)}) dentro del arbol vanilla \"{nodo.FullPath}\"");
						}
						continue;
					}
					enElCurado.Add(id);
				}
				if (CatalogoVivo.EsVanillaReal(id)) {
					enElArbol.Add(id);
				}
			}

			int icono = ArbolLibreria.IdDeIcono(nodo.IconPath);
			if (icono != 0 && (icono <= 0 || icono >= ItemLoader.ItemCount
					|| (curada && !CatalogoVivo.EsVanillaReal(icono)))) {
				iconosImposibles++;
				problemas.Add($"icono {icono} imposible en \"{nodo.FullPath}\"");
			}

			if (!esHoja) {
				foreach (CategoryTreeNodeData hijo in nodo.Children) {
					Recorrer(hijo, curada, enElArbol, enElCurado, problemas,
						ref hojasVacias, ref idsImposibles, ref iconosImposibles, ref deModEnElCurado);
				}
			}
		}

		/// <summary>
		/// Busca una carpeta por su ruta INGLESA sin los recuentos: "Categories/Weapons/Melee damage"
		/// encuentra el nodo cuyo <c>FullPath</c> real es
		/// "Categories/Weapons/Melee damage (316)". El recuento del rotulo cambia con la poda, asi
		/// que buscar por el FullPath literal seria fragil.
		/// </summary>
		private static CategoryTreeNodeData Buscar(string ruta)
		{
			IReadOnlyList<CategoryTreeNodeData> raices = ArbolLibreria.Raices;
			for (int i = 0; i < raices.Count; i++) {
				CategoryTreeNodeData encontrado = BuscarEn(raices[i], ruta);
				if (encontrado != null) {
					return encontrado;
				}
			}
			return null;
		}

		private static CategoryTreeNodeData BuscarEn(CategoryTreeNodeData nodo, string ruta)
		{
			if (SinRecuento(nodo.FullPath) == ruta) {
				return nodo;
			}
			if (nodo.Children != null) {
				foreach (CategoryTreeNodeData hijo in nodo.Children) {
					CategoryTreeNodeData encontrado = BuscarEn(hijo, ruta);
					if (encontrado != null) {
						return encontrado;
					}
				}
			}
			return null;
		}

		/// <summary>"Categories/Weapons/Melee damage (316)" -> "Categories/Weapons/Melee damage".</summary>
		private static string SinRecuento(string ruta)
		{
			if (string.IsNullOrEmpty(ruta)) {
				return ruta;
			}
			StringBuilder sb = new StringBuilder(ruta.Length);
			foreach (string segmento in ruta.Split('/')) {
				int abre = segmento.LastIndexOf(" (", StringComparison.Ordinal);
				if (sb.Length > 0) {
					sb.Append('/');
				}
				sb.Append(abre > 0 && segmento.EndsWith(")", StringComparison.Ordinal)
					? segmento.Substring(0, abre) : segmento);
			}
			return sb.ToString();
		}

		private static void ComprobarObjeto(int tipo, string rutaEsperada)
		{
			List<string> rutas = new List<string>();
			IReadOnlyList<CategoryTreeNodeData> raices = ArbolLibreria.Raices;
			for (int i = 0; i < raices.Count; i++) {
				RutasDe(raices[i], tipo, rutas);
			}

			bool ok = false;
			foreach (string r in rutas) {
				if (SinRecuento(r).StartsWith(rutaEsperada, StringComparison.Ordinal)) {
					ok = true;
					break;
				}
			}

			Item muestra;
			ContentSamples.ItemsByType.TryGetValue(tipo, out muestra);
			string clase = muestra != null && muestra.DamageType != null ? muestra.DamageType.Name : "-";

			Escribir($"OBJETO {tipo} \"{CatalogoVivo.Nombre(tipo)}\" (damage={(muestra != null ? muestra.damage : 0)}, " +
				$"DamageType={clase}): esperado dentro de \"{rutaEsperada}\" -> {(ok ? "OK" : "NO APARECE")}. " +
				$"Hojas donde cae: {(rutas.Count == 0 ? "(ninguna)" : string.Join(" | ", rutas.ToArray()))}");
		}

		/// <summary>Rutas de las HOJAS donde cae un objeto (una carpeta madre lo tiene por herencia,
		/// enseñarla no aportaria nada).</summary>
		private static void RutasDe(CategoryTreeNodeData nodo, int tipo, List<string> rutas)
		{
			if (nodo.ItemIdSet == null || !nodo.ItemIdSet.Contains(tipo)) {
				return;
			}
			if (nodo.Children == null || nodo.Children.Count == 0) {
				rutas.Add(nodo.FullPath);
				return;
			}
			foreach (CategoryTreeNodeData hijo in nodo.Children) {
				RutasDe(hijo, tipo, rutas);
			}
		}

		// =============================================================================================

		private sealed class Criterio
		{
			public readonly string Descripcion;
			/// <summary>true si la carpeta puede no existir sin que sea un problema (los subtipos
			/// se declaran una vez y valen para varias carpetas madre: un "Hachamartillo" existe
			/// dentro de "Hachas" y dentro de "Martillos", pero no dentro de "Picos").</summary>
			public readonly bool Opcional;
			private readonly Func<Item, bool> _prueba;

			public Criterio(string descripcion, Func<Item, bool> prueba, bool opcional = false)
			{
				Descripcion = descripcion;
				Opcional = opcional;
				_prueba = prueba;
			}

			public Criterio ComoOpcional()
			{
				return new Criterio(Descripcion, _prueba, opcional: true);
			}

			public bool Cumple(Item it)
			{
				return _prueba(it);
			}
		}

		/// <summary>
		/// El criterio REAL de cada carpeta de "Categories", sacado de los campos del propio
		/// <see cref="Item"/> - no de ninguna tabla nuestra. Es lo que permite decir "estos 307
		/// objetos son de cuerpo a cuerpo de verdad" en vez de "el arbol dice que lo son".
		/// </summary>
		private static IEnumerable<KeyValuePair<string, Criterio>> Criterios()
		{
			yield return Par("Categories/Weapons/Melee damage", "damage>0 y DamageType melee",
				it => it.damage > 0 && (it.DamageType == DamageClass.Melee || it.DamageType == DamageClass.MeleeNoSpeed));
			yield return Par("Categories/Weapons/Ranged damage", "damage>0 y DamageType a distancia",
				it => it.damage > 0 && it.DamageType == DamageClass.Ranged);
			yield return Par("Categories/Weapons/Magic damage", "damage>0 y DamageType magia",
				it => it.damage > 0 && it.DamageType == DamageClass.Magic);
			yield return Par("Categories/Weapons/Summon damage", "damage>0 y DamageType invocacion",
				it => it.damage > 0 && (it.DamageType == DamageClass.Summon || it.DamageType == DamageClass.SummonMeleeSpeed));
			yield return Par("Categories/Weapons/Summoner whips", "latigo (DamageType SummonMeleeSpeed)",
				it => it.damage > 0 && it.DamageType == DamageClass.SummonMeleeSpeed);
			yield return Par("Categories/Weapons/Sentry damage", "item.sentry",
				it => it.sentry);
			yield return Par("Categories/Equipable/Armor", "ocupa cabeza, cuerpo o piernas",
				it => it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0);
			yield return Par("Categories/Equipable/Accessories", "item.accessory",
				it => it.accessory);
			yield return Par("Categories/Equipable/Vanity", "item.vanity",
				it => it.vanity);
			yield return Par("Categories/Equipable/Head slot", "headSlot>=0",
				it => it.headSlot >= 0);
			yield return Par("Categories/Equipable/Body slot", "bodySlot>=0",
				it => it.bodySlot >= 0);
			yield return Par("Categories/Equipable/Leg slot", "legSlot>=0",
				it => it.legSlot >= 0);
			// Criterio LITERAL de Terrasavr para esta carpeta (script.js real:
			// textLq.indexOf("allows flight")): son las 7 botas que dan vuelo, no las alas - las alas
			// de verdad viven en "Accessories", que es lo que son. Sale asi tambien en la app de
			// escritorio y en el Terrasavr original; no es cosa del mod.
			yield return Par("Categories/Equipable/Wings", "el tooltip dice \"allows flight\"",
				it => CatalogoVivo.TooltipPlegado(it.type).Contains("allows flight"));
			// Tambien literal: Terrasavr mira el NOMBRE, no el campo dye.
			yield return Par("Categories/Equipable/Dyes", "el nombre acaba en \"Dye\"",
				it => it.Name != null && it.Name.EndsWith("Dye", StringComparison.OrdinalIgnoreCase));
			yield return Par("Categories/Tools/Pickaxes", "pick>0",
				it => it.pick > 0);
			yield return Par("Categories/Tools/Axes", "axe>0",
				it => it.axe > 0);
			yield return Par("Categories/Tools/Hammers", "hammer>0",
				it => it.hammer > 0);
			yield return Par("Categories/Tools/Fishing poles", "fishingPole>1",
				it => it.fishingPole > 1);
			yield return Par("Categories/Placeable", "createTile>=0 o createWall>=0",
				it => it.createTile >= 0 || it.createWall >= 0);
			yield return Par("Categories/Walls", "createWall>=0",
				it => it.createWall >= 0);

			foreach (KeyValuePair<string, Criterio> par in CriteriosDeSubtipo()) {
				yield return new KeyValuePair<string, Criterio>(par.Key, par.Value.ComoOpcional());
			}
		}

		/// <summary>
		/// Criterio de las carpetas NUEVAS por subtipo (8-sep-2026): dentro de "Categorias", cada
		/// hoja ya no se parte en "Pagina N" de 40 en 40 por id, sino por el subtipo real del
		/// objeto (espadas, lanzas, arcos, armas de fuego, picos, bloques...).
		/// <para />
		/// Esto es una <b>comprobacion cruzada de verdad</b>, no una tautologia: el reparto se
		/// calcula FUERA del juego, leyendo el <c>Item.cs</c> decompilado de Terraria 1.4.5.8
		/// (scripts/extraer-subtipos-libreria-vanilla.py del repo hermano), y aqui se comprueba
		/// contra el <see cref="Item"/> REAL que carga tModLoader 1.4.4.9. Si la extraccion
		/// offline se equivocara en un objeto, saldria aqui como "no cumple".
		/// </summary>
		private static IEnumerable<KeyValuePair<string, Criterio>> CriteriosDeSubtipo()
		{
			const string melee = "Categories/Weapons/Melee damage/";
			const string dist = "Categories/Weapons/Ranged damage/";
			const string tools = "Categories/Tools/";

			// -- cuerpo a cuerpo, por el aiStyle REAL del proyectil que dispara el arma
			yield return Par(melee + "Swords", "no es herramienta y el juego lo trata como espada",
				EsEspada);
			yield return Par(melee + "Spears", "proyectil de lanza (aiStyle 19 o 141)",
				it => AiDelProyectil(it) == 19 || AiDelProyectil(it) == 141);
			yield return Par(melee + "Flails", "proyectil de mayal/cadena (aiStyle 15, 13 o 69)",
				it => AiDelProyectil(it) == 15 || AiDelProyectil(it) == 13 || AiDelProyectil(it) == 69);
			yield return Par(melee + "Yoyos", "ItemID.Sets.Yoyo o proyectil de yoyo (aiStyle 99)",
				it => ItemID.Sets.Yoyo[it.type] || AiDelProyectil(it) == 99);
			yield return Par(melee + "Boomerangs", "proyectil de bumeran (aiStyle 3)",
				it => AiDelProyectil(it) == 3);
			yield return Par(melee + "Other melee weapons", "hace daño de verdad (damage>0)",
				it => it.damage > 0);

			// -- herramientas. Los mismos siete subtipos valen dentro de "Daño cuerpo a cuerpo" y
			// dentro de cada una de las tres carpetas de "Herramientas" (que se solapan a
			// proposito: un hachamartillo tiene poder de hacha Y de martillo, asi que sale en las
			// dos). Las combinaciones que no existen se saltan solas, ver `Opcional`.
			foreach (string raiz in new string[] { melee, tools + "Pickaxes/", tools + "Axes/", tools + "Hammers/" }) {
				yield return Par(raiz + "Pickaxes", "pick>0 y no es taladro ni motosierra ni pico-hacha",
					it => it.pick > 0 && it.axe <= 0 && !ItemID.Sets.IsDrill[it.type] && !ItemID.Sets.IsChainsaw[it.type]);
				yield return Par(raiz + "Drills", "ItemID.Sets.IsDrill",
					it => ItemID.Sets.IsDrill[it.type]);
				yield return Par(raiz + "Chainsaws", "ItemID.Sets.IsChainsaw",
					it => ItemID.Sets.IsChainsaw[it.type]);
				yield return Par(raiz + "Pickaxe axes", "pick>0 y axe>0",
					it => it.pick > 0 && it.axe > 0);
				yield return Par(raiz + "Hamaxes", "axe>0 y hammer>0",
					it => it.axe > 0 && it.hammer > 0);
				yield return Par(raiz + "Axes", "axe>0",
					it => it.axe > 0);
				yield return Par(raiz + "Hammers", "hammer>0",
					it => it.hammer > 0);
			}

			// -- a distancia, por la MUNICION real que consume o que es
			yield return Par(dist + "Bows", "useAmmo = flecha", it => it.useAmmo == AmmoID.Arrow);
			yield return Par(dist + "Guns", "useAmmo = bala", it => it.useAmmo == AmmoID.Bullet);
			yield return Par(dist + "Launchers", "useAmmo = cohete", it => it.useAmmo == AmmoID.Rocket);
			yield return Par(dist + "Dart weapons", "useAmmo = dardo", it => it.useAmmo == AmmoID.Dart);
			yield return Par(dist + "Other ammo weapons", "gasta municion de otro tipo",
				it => it.useAmmo != AmmoID.None && it.useAmmo != AmmoID.Arrow && it.useAmmo != AmmoID.Bullet
					&& it.useAmmo != AmmoID.Rocket && it.useAmmo != AmmoID.Dart);
			yield return Par(dist + "Ammo-free weapons", "arma que no gasta municion",
				it => it.useAmmo == AmmoID.None && it.ammo == AmmoID.None && it.damage > 0);
			// Sin "damage > 0" a proposito: los tres globos (Tree/World/Moon Globe) son armas
			// arrojadizas reales con daño 0 en este 1.4.4.9.
			yield return Par(dist + "Thrown weapons", "arma arrojadiza (consumible, sin municion)",
				it => it.consumable && it.shoot > 0 && it.useAmmo == AmmoID.None);
			yield return Par(dist + "Arrows", "ammo = flecha", it => it.ammo == AmmoID.Arrow);
			yield return Par(dist + "Bullets", "ammo = bala", it => it.ammo == AmmoID.Bullet);
			yield return Par(dist + "Rockets", "ammo = cohete", it => it.ammo == AmmoID.Rocket);
			yield return Par(dist + "Darts", "ammo = dardo", it => it.ammo == AmmoID.Dart);
			yield return Par(dist + "Flares", "ammo = bengala", it => it.ammo == AmmoID.Flare);
			yield return Par(dist + "Other ammunition", "es municion de otro tipo",
				it => it.ammo != AmmoID.None && it.ammo != AmmoID.Arrow && it.ammo != AmmoID.Bullet
					&& it.ammo != AmmoID.Rocket && it.ammo != AmmoID.Dart && it.ammo != AmmoID.Flare);

			// -- equipo, por la ranura real que ocupa
			foreach (string raiz in new string[] { "Categories/Equipable/Armor/", "Categories/Equipable/Vanity/" }) {
				yield return Par(raiz + "Head", "headSlot>=0", it => it.headSlot >= 0);
				yield return Par(raiz + "Body", "bodySlot>=0", it => it.bodySlot >= 0);
				yield return Par(raiz + "Legs", "legSlot>=0", it => it.legSlot >= 0);
			}
			yield return Par("Categories/Equipable/Vanity/Accessory", "accesorio sin ranura de armadura",
				it => it.accessory && it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0);
			foreach (string raiz in new string[] { "Categories/Equipable/Head slot/",
					"Categories/Equipable/Body slot/", "Categories/Equipable/Leg slot/" }) {
				yield return Par(raiz + "Armor", "armadura de verdad (no vanidad ni accesorio)",
					it => !it.vanity && !it.accessory);
				yield return Par(raiz + "Vanity", "item.vanity", it => it.vanity);
			}
			const string acc = "Categories/Equipable/Accessories/";
			yield return Par(acc + "Wings", "wingSlot>0", it => it.wingSlot > 0);
			yield return Par(acc + "Boots", "shoeSlot>0", it => it.shoeSlot > 0);
			yield return Par(acc + "Balloons", "balloonSlot>0", it => it.balloonSlot > 0);
			yield return Par(acc + "Shields", "shieldSlot>0", it => it.shieldSlot > 0);
			yield return Par(acc + "Necklaces", "neckSlot>0", it => it.neckSlot > 0);
			yield return Par(acc + "Face accessories", "faceSlot>0", it => it.faceSlot > 0);
			yield return Par(acc + "Gloves", "handOnSlot>0", it => it.handOnSlot > 0);
			yield return Par(acc + "Back accessories", "backSlot>0", it => it.backSlot > 0);
			yield return Par(acc + "Belts", "waistSlot>0", it => it.waistSlot > 0);
			yield return Par("Categories/Equipable/Dyes/Hair dyes", "hairDye>=0", it => it.hairDye >= 0);
			yield return Par("Categories/Equipable/Dyes/Dyes", "item.dye != 0", it => it.dye != 0);

			// -- colocables, por el tile real que colocan
			const string col = "Categories/Placeable/";
			yield return Par(col + "Blocks", "coloca un tile SIN marco (o sea, un bloque)",
				it => it.createTile >= 0 && !Main.tileFrameImportant[it.createTile]);
			yield return Par(col + "Banners", "coloca el tile de estandartes",
				it => it.createTile == TileID.Banners);
			yield return Par(col + "Paintings", "TileID.Sets.Paintings",
				it => it.createTile >= 0 && TileID.Sets.Paintings[it.createTile]);
			yield return Par(col + "Platforms", "TileID.Sets.Platforms",
				it => it.createTile >= 0 && TileID.Sets.Platforms[it.createTile]);
			yield return Par(col + "Chests", "tile de cofre o comoda",
				it => it.createTile >= 0 && (TileID.Sets.BasicChest[it.createTile]
					|| TileID.Sets.BasicChestFake[it.createTile] || TileID.Sets.BasicDresser[it.createTile]));
			yield return Par(col + "Torches", "ItemID.Sets.Torches", it => ItemID.Sets.Torches[it.type]);
			// `ItemID.Sets.Campfires` (que es de donde sale el grupo al extraerlo contra 1.4.5.8)
			// todavia no existe en el 1.4.4.9 de este tModLoader; el equivalente real aqui es el
			// tile que colocan, TileID.Sets.Campfire.
			yield return Par(col + "Campfires", "coloca un tile de hoguera",
				it => it.createTile >= 0 && TileID.Sets.Campfire[it.createTile]);
			yield return Par(col + "Statues", "coloca el tile de estatuas",
				it => it.createTile == TileID.Statues);
			yield return Par(col + "Music boxes", "coloca el tile de cajas de musica",
				it => it.createTile == TileID.MusicBoxes);
		}

		/// <summary>Una espada real: no es pico/hacha/martillo y el juego la mete en su set de
		/// prefijos de espadas, o dispara el proyectil de espada corta (aiStyle 161).</summary>
		private static bool EsEspada(Item it)
		{
			if (it.pick > 0 || it.axe > 0 || it.hammer > 0) {
				return false;
			}
			return Terraria.GameContent.Prefixes.PrefixLegacy.ItemSets.SwordsHammersAxesPicks[it.type]
				|| AiDelProyectil(it) == 161;
		}

		/// <summary>aiStyle REAL del proyectil que dispara un objeto, o -1 si no dispara.</summary>
		private static int AiDelProyectil(Item it)
		{
			Projectile muestra;
			if (it.shoot <= 0 || !ContentSamples.ProjectilesByType.TryGetValue(it.shoot, out muestra) || muestra == null) {
				return -1;
			}
			return muestra.aiStyle;
		}

		private static KeyValuePair<string, Criterio> Par(string ruta, string descripcion, Func<Item, bool> prueba)
		{
			return new KeyValuePair<string, Criterio>(ruta, new Criterio(descripcion, prueba));
		}

		// =============================================================================================

		private static string Listar(List<int> ids, int tope)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < ids.Count && i < tope; i++) {
				if (i > 0) {
					sb.Append(", ");
				}
				sb.Append(ids[i]).Append(" (\"").Append(CatalogoVivo.Nombre(ids[i])).Append("\")");
			}
			if (ids.Count > tope) {
				sb.Append(", ... +").Append(ids.Count - tope);
			}
			return sb.ToString();
		}

		private static string Nombres(IReadOnlyList<int> ids, int tope)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < ids.Count && i < tope; i++) {
				if (i > 0) {
					sb.Append(", ");
				}
				sb.Append('"').Append(CatalogoVivo.Nombre(ids[i])).Append('"');
			}
			return sb.ToString();
		}

		private static bool _archivoIniciado;

		private static void Escribir(string linea)
		{
			if (RegistroLibreria.Mod != null) {
				RegistroLibreria.Mod.Logger.Info($"{Terrakeep.LogTag} AUDIT CATEGORIAS: {linea}");
			}
			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta,
						$"# Auditoria de las categorias de la Libreria - {DateTime.Now:yyyy-MM-dd HH:mm:ss}" +
						Environment.NewLine, Encoding.UTF8);
				}
				File.AppendAllText(ruta, $"[{DateTime.Now:HH:mm:ss.fff}] {linea}{Environment.NewLine}", Encoding.UTF8);
			}
			catch (Exception) {
				// El log del juego ya lleva la linea; no vale la pena tumbar nada por el archivo.
			}
		}
	}
}
