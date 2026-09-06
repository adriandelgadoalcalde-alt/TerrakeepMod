using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Libreria;
using TerrasavrNative.Core.Data;

namespace TerrakeepMod.Common.Libreria
{
	/// <summary>
	/// Arnes de verificacion de WS3. Se activa con <c>TERRAKEEP_AUTOTEST_WS3</c> y, con el panel
	/// ya abierto, recorre la Libreria de verdad: navega el arbol pulsando las filas reales,
	/// ejercita las cuatro reglas de la gramatica de busqueda, coge un objeto del catalogo con el
	/// clic real de su ranura y lo coloca en el inventario del jugador por la ruta de vanilla,
	/// dejando en el log el <see cref="Item"/> real de antes y el de despues.
	/// </summary>
	/// <remarks>
	/// Mismo criterio que WS1/WS4: sin sesion de escritorio no se pueden simular clics de raton
	/// reales, asi que se dispara el <c>OnLeftClick</c> del propio elemento
	/// (<c>UIElement.LeftClick</c>, el camino exacto que recorre un clic una vez resuelto sobre
	/// que elemento cae) en vez de escribir el estado a pelo. Va troceada por fotogramas porque
	/// parte de lo que se comprueba (que las ranuras ocupan sitio real en pantalla) solo existe
	/// despues de un <c>Recalculate</c> + <c>Draw</c>.
	/// </remarks>
	public static class AutopruebaLibreria
	{
		private const int FotogramasEntrePasos = 12;

		/// <summary>Objeto con el que se hacen las pruebas de busqueda y de colocacion. Vanilla y
		/// siempre presente, asi que la prueba no depende de que haya ningun mod cargado.</summary>
		private const int TipoDePrueba = ItemID.CopperShortsword;

		/// <summary>Ranura del inventario donde se coloca. Vacia en el personaje sintetico de
		/// prueba, y lejos de la barra rapida.</summary>
		private const int RanuraDestino = 20;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _paso;
		private static int _espera;
		private static bool _repetirPaso;
		private static int _descensos;
		private static string _colocadoAntes;

		public static void Actualizar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PanelLibreriaSystem.VariableAutoprueba));
			}

			if (!_activa || _terminada) {
				return;
			}

			if (!PanelLibreriaSystem.PanelAbierto || PanelLibreriaSystem.PanelActual == null) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;
			_repetirPaso = false;

			try {
				EjecutarPaso(_paso);
			}
			catch (Exception e) {
				Registrar("AUTOPRUEBA WS3: EXCEPCION en el paso " + _paso + ": " + e);
				_terminada = true;
				return;
			}

			if (!_repetirPaso) {
				_paso++;
			}
		}

		private static ContenidoLibreria Contenido {
			get { return PanelLibreriaSystem.PanelActual; }
		}

		private static void EjecutarPaso(int paso)
		{
			switch (paso) {
				case 0: DescribirArbol(); break;
				case 1: EntrarEnUnaCarpeta(); break;
				case 2: DescribirCarpetaAbierta(); break;
				case 3: BuscarPorNombre(); break;
				case 4: BuscarPorId(); break;
				case 5: BuscarEnTooltip(); break;
				case 6: BuscarConComaYAcentos(); break;
				case 7: CogerDelCatalogo(); break;
				case 8: ColocarEnElInventario(); break;
				case 9: DeshacerYRehacer(); break;
				case 10: RecorrerDestinos(); break;
				case 11: ExplorarCarpetaDeMod(); break;
				case 12: PrepararInformeFinal(); break;
				case 13: DescribirPanelDibujado(); break;
				default:
					Registrar("AUTOPRUEBA WS3 COMPLETA. Todos los pasos ejecutados sin excepciones.");
					_terminada = true;
					break;
			}
		}

		// =========================================================================================

		private static void DescribirArbol()
		{
			Registrar("Paso 0 - arbol de la Libreria: " + ArbolLibreria.Resumen + ".");

			StringBuilder sb = new StringBuilder();
			IReadOnlyList<CategoryTreeNodeData> raices = ArbolLibreria.Raices;
			for (int i = 0; i < raices.Count; i++) {
				if (sb.Length > 0) {
					sb.Append(" | ");
				}
				sb.Append('"').Append(raices[i].Name).Append("\" (")
					.Append(raices[i].ItemIdsOrdered.Count).Append(" objetos, ")
					.Append(raices[i].Children.Count).Append(" subcarpetas)");
			}
			Registrar("Paso 0 - carpetas de primer nivel: " + sb);
		}

		/// <summary>
		/// Entra en una carpeta REAL pulsando su fila (<c>UIElement.LeftClick</c>), y se repite
		/// hasta llegar a una carpeta hoja (sin subcarpetas) que tenga objetos dentro. Es la
		/// navegacion de verdad, no un salto directo al nodo.
		/// </summary>
		private static void EntrarEnUnaCarpeta()
		{
			CategoryTreeNodeData objetivo = Contenido.PrimeraCarpetaConObjetos();
			if (objetivo == null) {
				Registrar("Paso 1 - no hay ninguna carpeta con objetos en este nivel; se para de descender.");
				return;
			}

			// Se busca su posicion para pulsar la fila exacta que le corresponde.
			int indice = 0;
			bool encontrada = false;
			foreach (CategoryTreeNodeData nodo in CarpetasDelNivel()) {
				if (ReferenceEquals(nodo, objetivo)) {
					encontrada = true;
					break;
				}
				indice++;
			}

			string pulsada = encontrada ? Contenido.PulsarFilaCarpeta(indice) : null;
			_descensos++;

			Registrar("Paso 1 - clic REAL en la fila " + (indice + 1) + " del arbol (\"" + pulsada + "\"). "
				+ "Ruta ahora: \"" + Contenido.RutaActual + "\". "
				+ "Objetos en esta carpeta: " + (Contenido.CarpetaActual != null
					? Contenido.CarpetaActual.ItemIdsOrdered.Count : 0)
				+ ", subcarpetas: " + (Contenido.CarpetaActual != null
					? Contenido.CarpetaActual.Children.Count : 0) + ".");

			// Se sigue bajando mientras haya subcarpetas, con un tope por si el arbol fuera hondo.
			if (Contenido.CarpetaActual != null && Contenido.CarpetaActual.Children.Count > 0 && _descensos < 6) {
				_repetirPaso = true;
			}
		}

		private static IEnumerable<CategoryTreeNodeData> CarpetasDelNivel()
		{
			CategoryTreeNodeData actual = Contenido.CarpetaActual;
			return actual == null ? ArbolLibreria.Raices : actual.Children;
		}

		private static void DescribirCarpetaAbierta()
		{
			CategoryTreeNodeData carpeta = Contenido.CarpetaActual;
			if (carpeta == null) {
				Registrar("Paso 2 - no se llego a abrir ninguna carpeta.");
				return;
			}

			StringBuilder sb = new StringBuilder();
			IReadOnlyList<SlotCatalogoLibreria> slots = Contenido.SlotsResultado;
			for (int i = 0; i < slots.Count && i < 8; i++) {
				if (sb.Length > 0) {
					sb.Append(", ");
				}
				sb.Append('"').Append(slots[i].Nombre).Append("\" (type=").Append(slots[i].Tipo).Append(')');
			}

			Registrar("Paso 2 - carpeta abierta \"" + carpeta.Name + "\" (ruta interna \"" + carpeta.FullPath
				+ "\"): " + carpeta.ItemIdsOrdered.Count + " objetos reales, "
				+ slots.Count + " ranuras de catalogo puestas en pantalla. Primeros: " + sb + ".");
		}

		// =========================================================================================
		// Gramatica de busqueda
		// =========================================================================================

		private static void BuscarPorNombre()
		{
			// Se busca por el nombre REAL que tenga el objeto con el idioma activo, no por una
			// cadena fija: asi la prueba vale igual en español que en ingles.
			string nombre = CatalogoVivo.Nombre(TipoDePrueba);
			string consulta = nombre.Length > 6 ? nombre.Substring(0, 6) : nombre;

			Contenido.IrALaRaiz();
			Contenido.FijarBusqueda(consulta);

			Registrar("Paso 3 - busqueda por NOMBRE \"" + consulta + "\" (trozo del nombre real de "
				+ "type=" + TipoDePrueba + ", \"" + nombre + "\") sobre toda la Libreria: "
				+ Contenido.TotalCasados + " objetos casan, " + Contenido.SlotsResultado.Count
				+ " enseñados. " + PrimerosResultados() + " "
				+ (ContieneTipo(TipoDePrueba) ? "OK: el objeto buscado esta entre los resultados."
					: "FALLO: el objeto buscado NO aparece."));
		}

		private static void BuscarPorId()
		{
			Contenido.FijarBusqueda("#" + TipoDePrueba);

			Registrar("Paso 4 - busqueda por ID exacto \"#" + TipoDePrueba + "\": "
				+ Contenido.TotalCasados + " resultado(s). " + PrimerosResultados() + " "
				+ (Contenido.TotalCasados == 1 && ContieneTipo(TipoDePrueba)
					? "OK: exactamente el objeto pedido." : "REVISAR: se esperaba uno solo."));

			// Y el rango, que es la otra mitad de la regla "#".
			Contenido.FijarBusqueda("#" + TipoDePrueba + "-" + (TipoDePrueba + 4));
			Registrar("Paso 4 - busqueda por RANGO de id \"#" + TipoDePrueba + "-" + (TipoDePrueba + 4)
				+ "\": " + Contenido.TotalCasados + " resultado(s). " + PrimerosResultados());
		}

		private static void BuscarEnTooltip()
		{
			// El "." busca en el TOOLTIP real del juego, no en el nombre. En vez de dar por hecho
			// que un objeto concreto tiene tooltip (el primer intento uso la Pocion de curacion
			// menor y salio vacia), se recorre el catalogo hasta encontrar uno que SI lo tenga con
			// el idioma que este activo, y se usa una palabra suya que NO este en su nombre.
			int referencia = 0;
			string palabra = null;
			int conTooltip = 0;

			foreach (LiveItemInfo info in CatalogoVivo.Objetos) {
				string tooltip = CatalogoVivo.TooltipPlegado(info.Id);
				if (tooltip.Length == 0) {
					continue;
				}
				conTooltip++;
				if (palabra != null) {
					continue;
				}

				string candidata = PalabraSoloDelTooltip(tooltip, CatalogoVivo.NombrePlegado(info.Id));
				if (candidata != null) {
					referencia = info.Id;
					palabra = candidata;
				}
			}

			if (palabra == null) {
				Registrar("Paso 5 - NINGUNO de los " + CatalogoVivo.Objetos.Count + " objetos tiene un "
					+ "tooltip utilizable con el idioma activo (" + conTooltip + " con tooltip); "
					+ "la regla \".texto\" no se puede ejercitar aqui.");
				return;
			}

			Contenido.FijarBusqueda("." + palabra);
			int conPunto = Contenido.TotalCasados;
			bool apareceConPunto = ContieneTipo(referencia);

			Contenido.FijarBusqueda(palabra);
			int sinPunto = Contenido.TotalCasados;
			bool apareceSinPunto = ContieneTipo(referencia);

			Registrar("Paso 5 - regla \".texto\" (busca en el tooltip, no en el nombre). "
				+ conTooltip + " de " + CatalogoVivo.Objetos.Count + " objetos tienen tooltip real. "
				+ "Objeto de referencia: \"" + CatalogoVivo.Nombre(referencia) + "\" (type=" + referencia
				+ "), palabra \"" + palabra + "\", que esta en su TOOLTIP y no en su nombre. "
				+ "Con punto (\"." + palabra + "\"): " + conPunto + " objetos, ¿aparece? "
				+ apareceConPunto + ". Sin punto (\"" + palabra + "\"): " + sinPunto
				+ " objetos, ¿aparece? " + apareceSinPunto + ". "
				+ (apareceConPunto && !apareceSinPunto
					? "OK: el punto cambia de verdad donde se busca."
					: "FALLO: las dos busquedas se comportan igual."));
		}

		/// <summary>Primera palabra de 5+ letras que este en el tooltip y NO en el nombre. Es lo
		/// que permite demostrar que el "." cambia de verdad donde se busca.</summary>
		private static string PalabraSoloDelTooltip(string tooltipPlegado, string nombrePlegado)
		{
			string[] palabras = tooltipPlegado.Split(
				new char[] { ' ', '\n', '\r', '\t', ',', '.', ';', ':', '\'', '"', '(', ')', '%' },
				StringSplitOptions.RemoveEmptyEntries);

			for (int i = 0; i < palabras.Length; i++) {
				if (palabras[i].Length < 5 || !EsSoloLetras(palabras[i])) {
					continue;
				}
				if (nombrePlegado.IndexOf(palabras[i], StringComparison.Ordinal) < 0) {
					return palabras[i];
				}
			}
			return null;
		}

		private static bool EsSoloLetras(string s)
		{
			for (int i = 0; i < s.Length; i++) {
				if (!char.IsLetter(s[i])) {
					return false;
				}
			}
			return true;
		}

		private static void BuscarConComaYAcentos()
		{
			string a = CatalogoVivo.Nombre(ItemID.DirtBlock);
			string b = CatalogoVivo.Nombre(ItemID.StoneBlock);
			Contenido.FijarBusqueda(a + "," + b);
			Registrar("Paso 6 - coma = O: \"" + a + "," + b + "\" -> " + Contenido.TotalCasados
				+ " objetos. " + PrimerosResultados());

			// AND: dos palabras del mismo nombre separadas por espacio. Es lo que demuestra que NO
			// se esta buscando la frase entera como subcadena: con AND entran tambien los objetos
			// que llevan las dos palabras en otro orden o separadas.
			string[] palabras = a.Split(' ');
			if (palabras.Length >= 2) {
				string consulta = palabras[0] + " " + palabras[palabras.Length - 1];
				Contenido.FijarBusqueda(consulta);
				Registrar("Paso 6 - espacio = Y: \"" + consulta + "\" -> " + Contenido.TotalCasados
					+ " objetos (todos los que llevan las DOS palabras, esten juntas o no). "
					+ PrimerosResultados());
			}

			// Plegado de acentos: se busca un objeto cuyo nombre real lleve tilde y se consulta sin
			// ella. Si el idioma activo no tiene ninguno, se dice y ya, no se inventa nada.
			int conTilde = BuscarObjetoConTilde();
			if (conTilde <= 0) {
				Registrar("Paso 6 - no hay ningun objeto con tilde en el idioma activo; "
					+ "el plegado de acentos no se puede ejercitar aqui.");
				return;
			}

			string nombreTilde = CatalogoVivo.Nombre(conTilde);
			string sinTilde = GramaticaBusqueda.Plegar(nombreTilde);
			Contenido.FijarBusqueda(sinTilde);
			Registrar("Paso 6 - plegado de acentos: se busca \"" + sinTilde + "\" (sin tildes) y el objeto "
				+ "real se llama \"" + nombreTilde + "\" -> " + Contenido.TotalCasados + " objetos. "
				+ (ContieneTipo(conTilde) ? "OK: lo encuentra igual." : "FALLO: no lo encuentra."));
		}

		// =========================================================================================
		// Coger y colocar
		// =========================================================================================

		private static void CogerDelCatalogo()
		{
			Contenido.IrALaRaiz();
			Contenido.FijarBusqueda("#" + TipoDePrueba);

			IReadOnlyList<SlotCatalogoLibreria> slots = Contenido.SlotsResultado;
			if (slots.Count == 0) {
				Registrar("Paso 7 - no hay ninguna ranura de catalogo que pulsar.");
				return;
			}

			Main.mouseItem = new Item();
			TerrakeepMod.UI.Panel.PanelTerrakeepState.FotogramasObjetoEnRaton = 0;

			string antes = Describir(Main.mouseItem);
			slots[0].PulsarComoUnClic();

			Registrar("Paso 7 - clic REAL en la ranura de catalogo \"" + slots[0].Nombre + "\" "
				+ "(type=" + slots[0].Tipo + "). Raton ANTES: " + antes
				+ ". Raton DESPUES: " + Describir(Main.mouseItem) + ". "
				+ (Main.mouseItem != null && Main.mouseItem.type == TipoDePrueba
					? "OK: el objeto del catalogo esta cogido con el raton."
					: "FALLO: el raton no lleva el objeto."));
		}

		private static void ColocarEnElInventario()
		{
			Player jugador = Main.LocalPlayer;
			int dibujados = TerrakeepMod.UI.Panel.PanelTerrakeepState.FotogramasObjetoEnRaton;

			// El destino se elige por su boton real, igual que lo haria el jugador.
			Contenido.MostrarDestino(0);

			_colocadoAntes = Describir(jugador.inventory[RanuraDestino]);
			string resultado = Contenido.ColocarEnRanura(TipoDePrueba, RanuraDestino, false);
			Item real = jugador.inventory[RanuraDestino];

			Registrar("Paso 8 - colocado en el contenedor \"" + Contenido.NombreDestino + "\", ranura "
				+ RanuraDestino + ", por la ruta real de vanilla (ItemSlot.LeftClick). "
				+ "El objeto cogido se dibujo en " + dibujados + " fotogramas (0 significaria que la capa "
				+ "de dibujado propia no funciona). "
				+ "ANTES inventory[" + RanuraDestino + "]=" + _colocadoAntes + ". "
				+ "DESPUES inventory[" + RanuraDestino + "]=" + Describir(real) + " (devuelto: " + resultado + "). "
				+ (real != null && real.type == TipoDePrueba
					? "OK: el Item REAL del jugador ha cambiado."
					: "FALLO: la ranura del jugador no tiene el objeto."));
		}

		private static void DeshacerYRehacer()
		{
			Player jugador = Main.LocalPlayer;

			string etiqueta = Historial.Pila.EtiquetaDeshacer;
			string deshecho = Historial.Deshacer();
			string trasDeshacer = Describir(jugador.inventory[RanuraDestino]);

			string rehecho = Historial.Rehacer();
			string trasRehacer = Describir(jugador.inventory[RanuraDestino]);

			Registrar("Paso 9 - historial de WS7 sobre la colocacion de la Libreria. "
				+ "Entrada en la cima: \"" + etiqueta + "\". "
				+ "DESHACER (\"" + deshecho + "\") -> inventory[" + RanuraDestino + "]=" + trasDeshacer
				+ " (antes de colocar era " + _colocadoAntes + ") -> "
				+ (trasDeshacer == _colocadoAntes ? "OK, la ranura vuelve a como estaba" : "DISTINTO") + ". "
				+ "REHACER (\"" + rehecho + "\") -> inventory[" + RanuraDestino + "]=" + trasRehacer + " -> "
				+ (trasRehacer.Contains("type=" + TipoDePrueba) ? "OK, el objeto vuelve" : "DISTINTO") + ".");
		}

		private static void RecorrerDestinos()
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < Contenido.TotalDestinos; i++) {
				Contenido.MostrarDestino(i);
				Item[] array = Contenido.ArrayDestino;
				if (sb.Length > 0) {
					sb.Append(" | ");
				}
				sb.Append('"').Append(Contenido.NombreDestino).Append("\" -> array vivo de ")
					.Append(array != null ? array.Length : 0).Append(" ranuras");
			}
			Contenido.MostrarDestino(0);

			Registrar("Paso 10 - los " + Contenido.TotalDestinos
				+ " contenedores reales de destino, cada uno apuntando a su array vivo del jugador: " + sb + ".");
		}

		/// <summary>
		/// Entra en la carpeta madre del mod con MAS objetos (si hay alguno instalado) y deja en el
		/// log sus subcarpetas y unos cuantos objetos reales. Es la prueba de que el descubrimiento
		/// en vivo y la categorizacion por los campos del <c>Item</c> funcionan sobre contenido que
		/// no es vanilla, sin ningun catalogo estatico por medio.
		/// </summary>
		private static void ExplorarCarpetaDeMod()
		{
			Contenido.IrALaRaiz();
			Contenido.FijarBusqueda("");

			IReadOnlyList<CategoryTreeNodeData> raices = ArbolLibreria.Raices;
			int indice = -1;
			int mejor = -1;
			for (int i = 0; i < raices.Count; i++) {
				// Las raices de mod son exactamente las que llevan el nombre interno de un mod
				// cargado en su FullPath (las vanilla llevan "Materials", "Categories"...).
				Mod mod;
				if (!ModLoader.TryGetMod(raices[i].FullPath, out mod)) {
					continue;
				}
				if (raices[i].ItemIdsOrdered.Count > mejor) {
					mejor = raices[i].ItemIdsOrdered.Count;
					indice = i;
				}
			}

			if (indice < 0) {
				Registrar("Paso 11 - no hay ningun mod de contenido cargado en esta partida; "
					+ "el descubrimiento en vivo no tiene nada que enseñar aparte de vanilla.");
				return;
			}

			string pulsada = Contenido.PulsarFilaCarpeta(indice);
			CategoryTreeNodeData raiz = Contenido.CarpetaActual;
			if (raiz == null) {
				Registrar("Paso 11 - no se pudo abrir la carpeta del mod.");
				return;
			}

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < raiz.Children.Count; i++) {
				if (sb.Length > 0) {
					sb.Append(" | ");
				}
				sb.Append('"').Append(raiz.Children[i].Name).Append('"');
			}

			Registrar("Paso 11 - clic REAL en la carpeta madre del mod \"" + pulsada + "\" (ruta interna \""
				+ raiz.FullPath + "\"): " + raiz.ItemIdsOrdered.Count + " objetos descubiertos EN VIVO, "
				+ "repartidos en estas categorias deducidas de los campos reales del Item: " + sb + ".");

			// Y se baja una vez mas, para enseñar objetos reales del mod dentro de una de ellas.
			CategoryTreeNodeData hija = Contenido.PrimeraCarpetaConObjetos();
			if (hija == null) {
				return;
			}
			int posicion = 0;
			for (int i = 0; i < raiz.Children.Count; i++) {
				if (ReferenceEquals(raiz.Children[i], hija)) {
					posicion = i;
					break;
				}
			}
			string dentro = Contenido.PulsarFilaCarpeta(posicion);
			Registrar("Paso 11 - dentro de \"" + dentro + "\": "
				+ Contenido.SlotsResultado.Count + " ranuras de catalogo con objetos reales del mod. "
				+ PrimerosResultados());
		}

		/// <summary>Deja la Libreria enseñando una tanda de objetos reales, para que el informe
		/// final se tome con ranuras de verdad colocadas en pantalla y no con la vista vacia.</summary>
		private static void PrepararInformeFinal()
		{
			Contenido.IrALaRaiz();
			Contenido.FijarBusqueda("#1-60");
			Registrar("Paso 12 - preparado el informe final: busqueda \"#1-60\" -> "
				+ Contenido.SlotsResultado.Count + " ranuras de catalogo. Se deja un fotograma para "
				+ "que el motor las recalcule y las dibuje.");
		}

		private static void DescribirPanelDibujado()
		{
			Registrar("Paso 13 - estado real del panel YA DIBUJADO: " + Contenido.Informe() + ".");
		}

		// =========================================================================================
		// Utilidades
		// =========================================================================================

		private static bool ContieneTipo(int tipo)
		{
			IReadOnlyList<SlotCatalogoLibreria> slots = Contenido.SlotsResultado;
			for (int i = 0; i < slots.Count; i++) {
				if (slots[i].Tipo == tipo) {
					return true;
				}
			}
			return false;
		}

		private static string PrimerosResultados()
		{
			IReadOnlyList<SlotCatalogoLibreria> slots = Contenido.SlotsResultado;
			if (slots.Count == 0) {
				return "Sin resultados.";
			}

			StringBuilder sb = new StringBuilder("Primeros: ");
			for (int i = 0; i < slots.Count && i < 5; i++) {
				if (i > 0) {
					sb.Append(", ");
				}
				sb.Append('"').Append(slots[i].Nombre).Append("\"(").Append(slots[i].Tipo).Append(')');
			}
			sb.Append('.');
			return sb.ToString();
		}

		private static int BuscarObjetoConTilde()
		{
			foreach (LiveItemInfo info in CatalogoVivo.Objetos) {
				string nombre = info.Name;
				for (int i = 0; i < nombre.Length; i++) {
					if (nombre[i] > 127 && char.IsLetter(nombre[i])) {
						// Solo vale si al plegarlo cambia de verdad (una "ñ" no lleva tilde que quitar).
						if (GramaticaBusqueda.Plegar(nombre) != nombre.ToLowerInvariant()) {
							return info.Id;
						}
						break;
					}
				}
			}
			return 0;
		}

		private static string Describir(Item objeto)
		{
			return objeto == null || objeto.IsAir
				? "(vacio)"
				: "\"" + objeto.Name + "\" x" + objeto.stack + " (type=" + objeto.type + ")";
		}

		private static void Registrar(string linea)
		{
			RegistroLibreria.Linea(Terrakeep.LogTag + " " + linea);
		}
	}
}
