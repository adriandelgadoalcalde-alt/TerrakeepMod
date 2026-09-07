using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.Common.Prefijos;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Libreria.Widgets;
using TerrakeepMod.UI.Personaje.Widgets;
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

		// --- Paso 17: mantener pulsado "+"/"-" con aceleracion real (mide tiempo real de verdad) --
		private static bool _holdIniciado;
		private static DateTime _holdInicio;
		private static int _holdStackInicial;
		private static int _holdRepeticionesEnVentana1;
		private static bool _holdVentana1Cerrada;

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
				// Encargo directo del usuario tras probar el mod (ver bitacora.md): la papelera
				// tambien en Libreria, y un mini-panel nuevo (arrastrar para seleccionar + editar
				// cantidad + editar prefijo) en el hueco de abajo a la derecha, junto a la rejilla
				// de destino.
				case 14: PrepararObjetosDeHerramientas(); break;
				case 15: ArrastrarAlRecuadroDeSeleccion(); break;
				case 16: ComprobarEditorCantidadCompacto(); break;
				// Encargo directo del usuario tras probar el mod: "si mantienes apretado el boton de
				// mas o menos no aceleran... deberia ir mas rapido". Va ANTES del intercambio de
				// prefijo (paso 18): necesita que el recuadro siga con el objeto APILABLE del paso
				// 15, no con el de prefijo (maxStack=1, sin margen para mantener pulsado "+" 2s).
				case 17: ComprobarAceleracionMantenerPulsado(); break;
				case 18: ComprobarEditorPrefijo(); break;
				// Separado del paso 18 a proposito: CapturaDePantalla.Guardar captura el fotograma
				// YA PRESENTADO (el anterior), asi que capturar en el MISMO paso que abre el popup
				// enseñaba el popup todavia CERRADO - bug real visto en una captura ("Prefix: None"
				// sin desplegar nada). Con un paso de por medio (~12 fotogramas reales) el popup ya
				// esta dibujado de verdad para cuando se captura.
				case 19: CapturarYPulsarPrefijo(); break;
				case 20: ComprobarPapeleraDesdeLibreria(); break;
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
		// Mini-panel de herramientas (papelera + arrastrar para seleccionar + cantidad + prefijo)
		// =========================================================================================

		private const int RanuraStack = 25;
		private const int RanuraPrefijo = 26;

		/// <summary>Objeto APILABLE (maxStack &gt; 1) para el editor de cantidad. Se busca aparte
		/// del de prefijo a proposito: <c>Item.CanHavePrefixes</c> (codigo real,
		/// <c>Terraria\Item.cs:1300</c>) exige <c>maxStack == 1 || AllowReforgeForStackableItem</c>,
		/// y NINGUN objeto vanilla real pone ese campo a true (comprobado en el decompilado
		/// entero: 0 resultados - es un hueco pensado para mods, Calamity entre ellos, para sus
		/// armas Picaro que SI apilan y SI llevan prefijo). O sea que en vanilla puro "apila" y
		/// "admite prefijo" son mutuamente excluyentes de verdad, nunca el mismo objeto - probarlo
		/// con un solo objeto (como se intento la primera vez, con Shuriken: apila, maxStack=9999,
		/// pero SIN ninguna categoria de prefijo real) habria dejado sin probar de verdad uno de
		/// los dos caminos.</summary>
		private static int _tipoStack;

		/// <summary>Objeto NO apilable (maxStack == 1) pero que SI admite prefijo, para el editor
		/// de prefijo. Se arrastra encima del recuadro DESPUES del de arriba, lo que ademas
		/// ejercita el intercambio real de <c>ItemSlot.Handle</c> cuando el recuadro ya tiene algo
		/// dentro (recuadro ocupado + raton con otro objeto -&gt; se intercambian, ni se pierde ni
		/// se duplica nada).</summary>
		private static int _tipoPrefijo;

		private static void PrepararObjetosDeHerramientas()
		{
			Player jugador = Main.LocalPlayer;

			_tipoStack = BuscarObjeto(o => o.maxStack > 1 && o.damage <= 0 && o.createTile < 0
				&& o.consumable);
			_tipoPrefijo = BuscarObjeto(o => o.maxStack == 1
				&& CatalogoPrefijosLegales.GruposLegales(o).Any());

			if (_tipoStack <= 0 || _tipoPrefijo <= 0) {
				Registrar("Paso 14 - FALLO buscando objetos de prueba: apilable(type=" + _tipoStack
					+ "), con prefijo(type=" + _tipoPrefijo + "). Se saltan las comprobaciones del mini-panel.");
				return;
			}

			Item objetoStack = new Item();
			objetoStack.SetDefaults(_tipoStack);
			objetoStack.stack = Math.Min(5, objetoStack.maxStack);
			jugador.inventory[RanuraStack] = objetoStack;

			Item objetoPrefijo = new Item();
			objetoPrefijo.SetDefaults(_tipoPrefijo);
			jugador.inventory[RanuraPrefijo] = objetoPrefijo;

			Registrar("Paso 14 - dos objetos de prueba reales puestos en el inventario. "
				+ "Apilable en inventory[" + RanuraStack + "]: " + Describir(jugador.inventory[RanuraStack])
				+ " (maxStack=" + objetoStack.maxStack + ", CanHavePrefixes="
				+ objetoStack.CanHavePrefixes() + ", se espera False: apila). "
				+ "Con prefijo en inventory[" + RanuraPrefijo + "]: " + Describir(jugador.inventory[RanuraPrefijo])
				+ " (maxStack=" + objetoPrefijo.maxStack + ", CanHavePrefixes=" + objetoPrefijo.CanHavePrefixes()
				+ ", se espera True; categorias legales: " + string.Join(", ",
					CatalogoPrefijosLegales.GruposLegales(objetoPrefijo)
						.Select(g => (Idiomas.EnEspanol ? g.Grupo.NameEs : g.Grupo.NameEn) + "(" + g.Ids.Count + ")"))
				+ ").");
		}

		/// <summary>
		/// Simula el arrastre real pedido explicitamente por el usuario ("deberiamos poder
		/// clicar/arrastrar y que quede seleccionado"): coge el objeto APILABLE del hueco de
		/// origen con <c>ItemSlot.LeftClick</c> (la misma ruta que un arrastre real) y lo suelta
		/// sobre <see cref="SlotSeleccionTk"/> con <c>SlotSeleccionTk.EjercitarHandle</c> (el
		/// MISMO <c>ItemSlot.Handle</c> que dispara <c>DrawSelf</c> cuando el raton esta encima).
		/// </summary>
		private static void ArrastrarAlRecuadroDeSeleccion()
		{
			if (_tipoStack <= 0) {
				Registrar("Paso 15 - sin objetos de prueba (paso 14 fallo), se salta.");
				return;
			}

			Player jugador = Main.LocalPlayer;
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null) {
				Registrar("Paso 15 - Contenido.Herramientas es null.");
				return;
			}

			string antesRanura = Describir(jugador.inventory[RanuraStack]);

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				ItemSlot.LeftClick(jugador.inventory, ItemSlot.Context.InventoryItem, RanuraStack);
				string trasCoger = Describir(jugador.inventory[RanuraStack]);
				string ratonTrasCoger = Describir(Main.mouseItem);

				herramientas.Seleccion.EjercitarHandle();

				bool huecoVacio = jugador.inventory[RanuraStack].IsAir;
				bool manoVacia = Main.mouseItem.IsAir;
				bool seleccionado = !herramientas.Seleccion.ObjetoActual.IsAir
					&& herramientas.Seleccion.ObjetoActual.type == _tipoStack;

				Registrar("Paso 15 - arrastre real al recuadro de seleccion. ANTES inventory["
					+ RanuraStack + "]=" + antesRanura + ". Tras cogerlo (ItemSlot.LeftClick): "
					+ "inventory[" + RanuraStack + "]=" + trasCoger + ", raton=" + ratonTrasCoger
					+ ". Tras soltarlo en el recuadro (SlotSeleccionTk.EjercitarHandle): hueco="
					+ (huecoVacio ? "OK, vacio" : "FALLO") + ", raton=" + (manoVacia ? "OK, vacio" : "FALLO")
					+ ", recuadro=" + Describir(herramientas.Seleccion.ObjetoActual)
					+ " (" + (seleccionado ? "OK: es el objeto real, misma referencia que se arrastro"
						: "FALLO: no es el objeto esperado") + ").");
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}
		}

		/// <summary>Ejercita el editor de cantidad COMPACTO (modo explicito, enganchado al
		/// recuadro de seleccion) pulsando sus botones reales, igual que ya hace WS1 con el de
		/// Personaje - misma ruta (<c>BotonTk.LeftClick</c>), distinto binding.</summary>
		private static void ComprobarEditorCantidadCompacto()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null || herramientas.Seleccion.ObjetoActual.IsAir) {
				Registrar("Paso 16 - no hay nada seleccionado en el recuadro (paso 15 fallo), se salta.");
				return;
			}

			EditorCantidadTk editor = herramientas.EditorCantidad;
			Item objetivo = editor.ObjetivoActual;
			bool mismaReferencia = ReferenceEquals(objetivo, herramientas.Seleccion.ObjetoActual);

			int stackInicial = objetivo.stack;
			int maxStack = objetivo.maxStack;

			Clic(editor.BotonMas);
			int trasMas = objetivo.stack;

			Clic(editor.BotonMenos);
			int trasMenos = objetivo.stack;

			editor.Campo.FijarTextoSilencioso("3");
			Clic(editor.BotonAplicar);
			int trasAplicarTres = objetivo.stack;

			editor.Campo.FijarTextoSilencioso((maxStack + 500).ToString());
			Clic(editor.BotonAplicar);
			int trasPedirDeMas = objetivo.stack;

			bool okMas = trasMas == stackInicial + 1;
			bool okMenos = trasMenos == trasMas - 1;
			bool okTres = trasAplicarTres == 3;
			bool okAcotado = trasPedirDeMas == maxStack;

			Registrar("Paso 16 - editor de cantidad COMPACTO (explicito, sin hover) sobre \""
				+ objetivo.Name + "\" (maxStack=" + maxStack + "). ObjetivoActual es "
				+ (mismaReferencia ? "OK, la MISMA referencia que SlotSeleccionTk.ObjetoActual"
					: "FALLO, referencia distinta") + ". stack inicial=" + stackInicial + ". "
				+ "Tras \"+\": " + trasMas + " (" + (okMas ? "OK" : "FALLO") + "). "
				+ "Tras \"-\": " + trasMenos + " (" + (okMenos ? "OK" : "FALLO") + "). "
				+ "Tras escribir \"3\" y Aplicar: " + trasAplicarTres + " (" + (okTres ? "OK" : "FALLO") + "). "
				+ "Tras pedir " + (maxStack + 500) + " (por encima del maximo) y Aplicar: " + trasPedirDeMas
				+ " (" + (okAcotado ? "OK, acotado al maximo real" : "FALLO") + "). "
				+ "El objeto REAL del recuadro tiene ahora stack=" + herramientas.Seleccion.ObjetoActual.stack
				+ " (" + (herramientas.Seleccion.ObjetoActual.stack == trasPedirDeMas
					? "OK, es el mismo objeto" : "FALLO") + ").");

			// Se deja en 5 para que el resto de pasos (prefijo, papelera) trabajen con un numero
			// comodo de leer en el log.
			objetivo.stack = Math.Min(5, maxStack);
		}

		/// <summary>
		/// Encargo directo del usuario tras probar el mod: "si mantienes apretado el boton de mas o
		/// menos no aceleran... deberia ir mas rapido para mayor comodidad". Mantiene pulsado el
		/// "+" del editor de cantidad DE VERDAD durante ~2,4 segundos REALES (<c>DateTime.UtcNow</c>,
		/// no fotogramas contados a mano) dejando que el motor siga corriendo fotogramas de verdad
		/// entre paso y paso (va troceada, igual que <c>ComprobarCaducidadBuffs</c> de WS1: no se
		/// puede "avanzar el reloj" a la fuerza), y compara las repeticiones disparadas en la
		/// PRIMERA mitad de la ventana con las de la SEGUNDA mitad (mismo intervalo real de tiempo
		/// cada una) - si de verdad acelera, la segunda mitad tiene que traer mas repeticiones que
		/// la primera.
		/// <para />
		/// <c>BotonTk.LeftMouseDown</c> se llama UNA vez para marcar el inicio de "mantener pulsado"
		/// (ver <see cref="TerrakeepMod.UI.Personaje.Widgets.BotonTk.Manteniendo"/>) y
		/// <c>Main.mouseLeft</c> se deja en <c>true</c> sin soltar durante toda la ventana - el mismo
		/// patron que ya usa el resto de esta autoprueba para "clics reales", solo que sostenido en
		/// vez de puntual. <c>Main.LocalPlayer.mouseInterface</c> se fuerza a <c>true</c> en cada
		/// paso mientras dura, para que el "click" sostenido no se cuele hacia el mundo (usar un
		/// objeto, atacar) si el cursor real no está encima de nada en ese instante.
		/// </summary>
		private static void ComprobarAceleracionMantenerPulsado()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null || herramientas.Seleccion.ObjetoActual.IsAir) {
				Registrar("Paso 17 - no hay nada seleccionado en el recuadro (paso 15 fallo), se salta.");
				return;
			}

			EditorCantidadTk editor = herramientas.EditorCantidad;
			Item objetivo = editor.ObjetivoActual;

			const double DuracionTotalS = 2.4;
			const double MitadS = DuracionTotalS / 2.0;

			BotonTk boton = editor.BotonMas;

			if (!_holdIniciado) {
				if (objetivo == null || objetivo.IsAir || objetivo.maxStack < 500) {
					Registrar("Paso 17 - el objeto seleccionado (maxStack=" + (objetivo?.maxStack ?? 0)
						+ ") no tiene pila de sobra para mantener pulsado " + DuracionTotalS
						+ "s sin toparse con el maximo real; se salta.");
					return;
				}

				objetivo.stack = 1;
				editor.ReiniciarContadoresDeRepeticion();
				boton.ForzarManteniendoParaAutoprueba(true);

				_holdStackInicial = objetivo.stack;
				_holdRepeticionesEnVentana1 = 0;
				_holdVentana1Cerrada = false;
				_holdInicio = DateTime.UtcNow;
				_holdIniciado = true;

				Registrar("Paso 17 - empieza a MANTENER pulsado \"+\" de verdad (BotonTk"
					+ ".ForzarManteniendoParaAutoprueba, sin soltar - Main.mouseLeft NO sirve aqui: el "
					+ "motor lo sobreescribe con el raton fisico real en cada fotograma de entrada, asi "
					+ "que no se puede \"dejar puesto\" varios fotogramas reales sin hardware de por "
					+ "medio, visto en el juego real) sobre \"" + objetivo.Name + "\" (maxStack="
					+ objetivo.maxStack + "). Midiendo " + DuracionTotalS + "s reales.");
				_repetirPaso = true;
				return;
			}

			// Se re-afirma en CADA reentrada (cada ~12 fotogramas reales, ver FotogramasEntrePasos):
			// el motor no tiene forma de "recordar" el forzado el solo entre llamadas de esta
			// autoprueba, asi que hay que mantenerlo puesto a mano mientras dure la ventana.
			boton.ForzarManteniendoParaAutoprueba(true);

			double transcurrido = (DateTime.UtcNow - _holdInicio).TotalSeconds;

			if (!_holdVentana1Cerrada && transcurrido >= MitadS) {
				_holdVentana1Cerrada = true;
				_holdRepeticionesEnVentana1 = editor.RepeticionesMas;
				Registrar("Paso 17 - mitad de la ventana (" + transcurrido.ToString("0.00")
					+ "s reales): " + _holdRepeticionesEnVentana1 + " repeticiones disparadas hasta ahora.");
			}

			if (transcurrido < DuracionTotalS) {
				_repetirPaso = true;
				return;
			}

			boton.ForzarManteniendoParaAutoprueba(false);
			int repeticionesTotales = editor.RepeticionesMas;
			int repeticionesSegundaMitad = repeticionesTotales - _holdRepeticionesEnVentana1;
			int stackFinal = objetivo != null && !objetivo.IsAir ? objetivo.stack : _holdStackInicial;

			bool repiteDeVerdad = repeticionesTotales >= 6;
			bool acelera = repeticionesSegundaMitad > _holdRepeticionesEnVentana1;

			Registrar("Paso 17 - soltado tras " + transcurrido.ToString("0.00") + "s reales. stack "
				+ _holdStackInicial + " -> " + stackFinal + " (+" + (stackFinal - _holdStackInicial) + "). "
				+ "Repeticiones por mantener pulsado (sin contar el clic normal inicial): primera mitad ("
				+ MitadS.ToString("0.0") + "s)=" + _holdRepeticionesEnVentana1 + ", segunda mitad="
				+ repeticionesSegundaMitad + ", total=" + repeticionesTotales + ". "
				+ (repiteDeVerdad ? "OK: repite de verdad manteniendo pulsado (no solo 1 por clic). "
					: "FALLO: casi ninguna repeticion. ")
				+ (acelera ? "OK: la segunda mitad trae MAS repeticiones que la primera, acelera de verdad."
					: "FALLO: no acelera (segunda mitad <= primera).") + " "
				+ CapturaDePantalla.Guardar("ws3-mantener-pulsado-acelera"));

			_holdIniciado = false;
		}

		/// <summary>
		/// Primero intercambia el objeto del recuadro: el de prefijo (inventory[26]) se coge con
		/// <c>ItemSlot.LeftClick</c> y se arrastra sobre el recuadro YA OCUPADO (con el apilable
		/// del paso 15 dentro) - <c>ItemSlot.Handle</c> hace el intercambio real de vanilla (el
		/// que estaba en el recuadro pasa al raton, el nuevo se queda), que se limpia soltandolo en
		/// la papelera para no dejar nada perdido en el raton. Con el objeto correcto ya
		/// seleccionado, abre el popup real y pulsa un boton de prefijo real DOS VECES SEGUIDAS con
		/// el MISMO id - es la comprobacion explicita de que <c>ResetPrefix()+Prefix()</c> no
		/// compone multiplicadores (ver la cabecera de <see cref="EditorPrefijoTk"/>): si
		/// compusiera, el daño despues del segundo clic seria distinto del de despues del primero.
		/// </summary>
		private static void ComprobarEditorPrefijo()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null || _tipoPrefijo <= 0) {
				Registrar("Paso 18 - sin objeto de prueba con prefijo (paso 14 fallo), se salta.");
				return;
			}

			Player jugador = Main.LocalPlayer;
			string antesRecuadro = Describir(herramientas.Seleccion.ObjetoActual);

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				ItemSlot.LeftClick(jugador.inventory, ItemSlot.Context.InventoryItem, RanuraPrefijo);
				herramientas.Seleccion.EjercitarHandle();   // intercambio: el apilable pasa al raton

				bool esElDePrefijo = !herramientas.Seleccion.ObjetoActual.IsAir
					&& herramientas.Seleccion.ObjetoActual.type == _tipoPrefijo;
				bool volvioElApilable = !Main.mouseItem.IsAir && Main.mouseItem.type == _tipoStack;

				Registrar("Paso 18 - intercambio real en el recuadro. ANTES=" + antesRecuadro
					+ ". Se arrastra el objeto CON PREFIJO encima (recuadro ya ocupado): recuadro AHORA="
					+ Describir(herramientas.Seleccion.ObjetoActual) + " (" + (esElDePrefijo ? "OK" : "FALLO")
					+ "), raton=" + Describir(Main.mouseItem) + " (" + (volvioElApilable
						? "OK: el apilable volvio al raton, ItemSlot.Handle intercambia de verdad"
						: "FALLO") + ").");

				// El apilable que volvio al raton se descarta en la papelera para dejar el raton
				// limpio antes de seguir - no es basura tirada al suelo, es la ruta real del juego.
				ItemSlot.Handle(ref jugador.trashItem, ItemSlot.Context.TrashItem);
				jugador.trashItem = new Item();
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}

			if (herramientas.Seleccion.ObjetoActual.IsAir || herramientas.Seleccion.ObjetoActual.type != _tipoPrefijo) {
				Registrar("Paso 18 - el recuadro no tiene el objeto esperado tras el intercambio, se aborta.");
				return;
			}

			// El popup se ABRE aqui, pero la captura (paso 19) espera a la SIGUIENTE reentrada de la
			// autoprueba (~12 fotogramas reales despues, ver FotogramasEntrePasos): CapturaDePantalla
			// captura el fotograma YA PRESENTADO (ver su cabecera), asi que capturar en el MISMO
			// fotograma en que se llama a AbrirParaAutoprueba() enseña el fotograma ANTERIOR, con el
			// popup todavia cerrado - bug real de esta autoprueba, visto en una captura (salia el
			// boton "Prefix: None" sin desplegar nada).
			herramientas.EditorPrefijo.AbrirParaAutoprueba();
			Registrar("Paso 18 - popup de prefijo abierto sobre \"" + herramientas.Seleccion.ObjetoActual.Name
				+ "\" (PopupAbierto=" + herramientas.EditorPrefijo.PopupAbierto + "). La captura y los "
				+ "clics se hacen en el paso siguiente, para darle al menos un fotograma real de sobra.");
		}

		private static void CapturarYPulsarPrefijo()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null || herramientas.Seleccion.ObjetoActual.IsAir) {
				Registrar("Paso 19 - no hay nada seleccionado en el recuadro (paso 18 fallo), se salta.");
				return;
			}

			EditorPrefijoTk editor = herramientas.EditorPrefijo;
			Item objetivo = herramientas.Seleccion.ObjetoActual;
			int prefijoAntes = objetivo.prefix;
			int dañoAntes = objetivo.damage;

			if (!editor.PopupAbierto) {
				// Por si el popup se hubiera cerrado solo entre paso y paso (no deberia: nada mas
				// deberia tocarlo aqui), se reabre antes de la captura.
				editor.AbrirParaAutoprueba();
			}
			List<BotonTk> botones = editor.BotonesPopupParaAutoprueba();

			Registrar("Paso 19 - diagnostico de geometria real: " + editor.DiagnosticoGeometria());

			// Captura real con el popup YA ABIERTO y YA DIBUJADO (fotograma de sobra desde el paso
			// 18): pedido explicito del usuario ("el prefijo se abre a la derecha con opciones
			// reales visibles") - la unica forma honesta de demostrarlo es una imagen real del back
			// buffer, no solo el recuento de botones.
			Registrar("Paso 19 - " + CapturaDePantalla.Guardar("ws3-prefijo-abierto-derecha"));

			// El primero de la lista es siempre "Ninguno" (quitar prefijo); el primer prefijo real
			// legal es el segundo, si lo hay.
			if (botones.Count < 2) {
				Registrar("Paso 19 - el popup se abrio (" + editor.PopupAbierto + ") pero solo trae "
					+ botones.Count + " boton(es) (se esperaban al menos 2: \"Ninguno\" + un prefijo "
					+ "real). Objeto=\"" + objetivo.Name + "\". Se salta la comprobacion.");
				return;
			}

			BotonTk botonPrefijo = botones[1];
			string nombrePedido = botonPrefijo.Texto;

			Clic(botonPrefijo);
			int prefijoTrasPrimerClic = objetivo.prefix;
			int dañoTrasPrimerClic = objetivo.damage;

			// Segunda vuelta: se reabre el popup (el primer clic lo cierra solo) y se pulsa la
			// fila del MISMO prefijo otra vez.
			editor.AbrirParaAutoprueba();
			List<BotonTk> botonesSegundaVez = editor.BotonesPopupParaAutoprueba();
			BotonTk mismoBoton = null;
			foreach (BotonTk b in botonesSegundaVez) {
				if (b.Texto == nombrePedido) {
					mismoBoton = b;
					break;
				}
			}
			if (mismoBoton != null) {
				Clic(mismoBoton);
			}
			int prefijoTrasSegundoClic = objetivo.prefix;
			int dañoTrasSegundoClic = objetivo.damage;

			bool cambio = prefijoTrasPrimerClic != prefijoAntes;
			bool sinAcumular = dañoTrasSegundoClic == dañoTrasPrimerClic && prefijoTrasSegundoClic == prefijoTrasPrimerClic;

			Registrar("Paso 19 - editor de prefijo sobre \"" + objetivo.Name + "\". Boton pulsado: \""
				+ nombrePedido + "\". prefix ANTES=" + prefijoAntes + " (daño=" + dañoAntes + "). "
				+ "Tras el 1er clic: prefix=" + prefijoTrasPrimerClic + " (daño=" + dañoTrasPrimerClic + ") "
				+ "(" + (cambio ? "OK, cambio de verdad" : "FALLO, no cambio") + "). "
				+ "Tras pulsar el MISMO prefijo otra vez: prefix=" + prefijoTrasSegundoClic
				+ " (daño=" + dañoTrasSegundoClic + ") "
				+ "(" + (sinAcumular ? "OK: identico al primer clic, NO compone multiplicadores"
					: "FALLO: distinto del primer clic, esta acumulando") + "). "
				+ "Historial tras el cambio: " + Historial.Pila.EtiquetaDeshacer + ".");
		}

		/// <summary>
		/// Cierra el ciclo completo del mini-panel: el objeto que se selecciono, se le edito la
		/// cantidad y se le cambio el prefijo se arrastra ahora a la papelera REAL de Libreria
		/// (pedido explicito del usuario: "la papelera tambien deberia salir en Libreria"),
		/// exactamente por la misma ruta que ya verifico WS1 en Personaje
		/// (<c>ItemSlot.Handle</c> con <c>Context.TrashItem</c> sobre <c>Player.trashItem</c>).
		/// </summary>
		private static void ComprobarPapeleraDesdeLibreria()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null || herramientas.Seleccion.ObjetoActual.IsAir) {
				Registrar("Paso 20 - no hay nada seleccionado en el recuadro (paso 15 fallo), se salta.");
				return;
			}

			Player jugador = Main.LocalPlayer;
			string antesRecuadro = Describir(herramientas.Seleccion.ObjetoActual);
			int objetosActivosAntes = ContarObjetosEnElMundo();

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				// Coger del recuadro de seleccion: el MISMO ItemSlot.Handle que dispara DrawSelf.
				herramientas.Seleccion.EjercitarHandle();
				string ratonTrasCoger = Describir(Main.mouseItem);

				// Soltar en la papelera de Libreria (SlotPapeleraTk, la misma clase que Personaje).
				ItemSlot.Handle(ref jugador.trashItem, ItemSlot.Context.TrashItem);

				int objetosActivosDespues = ContarObjetosEnElMundo();
				bool recuadroVacio = herramientas.Seleccion.ObjetoActual.IsAir;
				bool enPapelera = !jugador.trashItem.IsAir;
				bool manoVacia = Main.mouseItem.IsAir;
				bool nadaEnElSuelo = objetosActivosDespues == objetosActivosAntes;

				Registrar("Paso 20 - papelera de Libreria (PanelHerramientasLibreriaTk.Papelera, "
					+ "misma clase SlotPapeleraTk que Personaje). ANTES recuadro=" + antesRecuadro
					+ ". Tras cogerlo del recuadro: raton=" + ratonTrasCoger + ". Tras soltarlo en la "
					+ "papelera: recuadro=" + Describir(herramientas.Seleccion.ObjetoActual)
					+ " (" + (recuadroVacio ? "OK, vacio" : "FALLO") + "), papelera="
					+ Describir(jugador.trashItem) + " (" + (enPapelera ? "OK" : "FALLO") + "), raton="
					+ Describir(Main.mouseItem) + " (" + (manoVacia ? "OK" : "FALLO") + "). "
					+ "Objetos activos en el mundo: antes=" + objetosActivosAntes + ", despues="
					+ objetosActivosDespues + " (" + (nadaEnElSuelo
						? "OK, no ha aparecido nada tirado en el suelo" : "FALLO") + ").");
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}

			// Se deja limpio para no dejar basura visible el resto de la sesion.
			jugador.trashItem = new Item();
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

		/// <summary>Primer objeto del juego que cumple la condicion, usando las muestras que el
		/// propio juego mantiene - mismo patron que <c>AutopruebaPersonaje.BuscarObjeto</c>, para
		/// que esta prueba no dependa de ids concretos ni de que haya ningun mod cargado.</summary>
		private static int BuscarObjeto(Func<Item, bool> condicion)
		{
			for (int tipo = 1; tipo < ItemLoader.ItemCount; tipo++) {
				Item muestra;
				if (!ContentSamples.ItemsByType.TryGetValue(tipo, out muestra) || muestra == null) {
					continue;
				}
				if (muestra.type <= 0 || string.IsNullOrEmpty(muestra.Name)) {
					continue;
				}
				if (condicion(muestra)) {
					return tipo;
				}
			}
			return 0;
		}

		private static void Clic(BotonTk boton)
		{
			if (boton == null) {
				return;
			}
			Rectangle rect = boton.GetDimensions().ToRectangle();
			Vector2 centro = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
			boton.LeftClick(new UIMouseEvent(boton, centro));
		}

		/// <summary>Cuenta los objetos activos tirados en el mundo (<c>Main.item</c>). Se usa antes
		/// y despues de la papelera para demostrar que nada acaba en el suelo - mismo patron que
		/// <c>AutopruebaPersonaje.ContarObjetosEnElMundo</c>.</summary>
		private static int ContarObjetosEnElMundo()
		{
			int cuenta = 0;
			for (int i = 0; i < Main.item.Length; i++) {
				if (Main.item[i] != null && Main.item[i].active) {
					cuenta++;
				}
			}
			return cuenta;
		}

		private static void Registrar(string linea)
		{
			RegistroLibreria.Linea(Terrakeep.LogTag + " " + linea);
		}
	}
}
