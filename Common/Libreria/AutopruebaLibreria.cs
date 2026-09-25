using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.Common.Prefijos;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Libreria.Widgets;
using TerrakeepMod.UI.Personaje.Widgets;
using Terrakeep.Core.Data;

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
				// Los tres bugs que reporto el usuario con captura sobre este mismo desplegable (se
				// dibujaba FUERA del panel, sobre el mundo; el boton "Cerrar (O)" lo tapaba; el scroll
				// no respondia). Cada uno se comprueba con datos reales, no de vista:
				case 19: ComprobarPopupDentroDelPanel(); break;   // + captura 1 (arriba del todo)
				case 20: ComprobarScrollReal(); break;            // rueda de verdad, por la ruta del motor
				case 21: CapturarTrasScroll(); break;             // captura 2 (ya desplazado)
				case 22: CapturarYPulsarPrefijo(); break;
				case 23: ComprobarPapeleraDesdeLibreria(); break;
				// KeepQA - verificarBordeViewport.js (15-sep-2026): cierra el hueco de cobertura
				// documentado en PATRONES.md ("tampoco se ha cableado esta pieza en TerrakeepMod
				// todavia") para la rejilla de resultados de la Libreria (UIList real, la misma
				// categoria de widget que dejo pasar el bug real de StarvekeepMod). Dos pasos: uno
				// prepara la busqueda amplia y desplaza al final (el desplazamiento visual solo se
				// aplica en el Draw del fotograma siguiente), el otro - ya con eso dibujado - vuelca
				// la geometria real con el campo viewportAlto nuevo.
				case 24: PrepararBusquedaViewport(); break;
				case 25: VolcarViewportLibreria(); break;
				// TM2 del catalogo de rediseño visual ("Editor de objeto flotante"): re-arrastra un
				// objeto de prueba fresco al recuadro de seleccion (el de los pasos 14-15 puede haber
				// acabado en la papelera del paso 23) y comprueba que la tarjeta flotante aparece de
				// verdad, con captura real para poder inspeccionar los pixeles.
				case 26: PrepararTarjetaFlotante(); break;
				case 27: ComprobarYCapturarTarjetaFlotante(); break;
				// Idea 6 del catalogo de funciones ("cofres del mundo en vivo"): siembra un cofre
				// de PRUEBA con contenido conocido (mundo aislado, nunca uno real - ver
				// SembrarCofreDePrueba), abre el selector con un clic REAL sobre el boton "Cofre",
				// comprueba sus filas reales y elige el cofre sembrado, y por ultimo comprueba que
				// la rejilla de destino enseña de verdad el Item[] de ESE cofre.
				case 28: SembrarCofreDePrueba(); break;
				case 29: AbrirSelectorCofre(); break;
				case 30: ComprobarYCapturarSelectorCofre(); break;
				case 31: ElegirCofreDePrueba(); break;
				// Separado del paso 31 a proposito, mismo motivo real ya documentado en el paso 18/
				// 19 (CapturaDePantalla.Guardar captura el fotograma YA PRESENTADO, el ANTERIOR):
				// capturar en el mismo paso que el clic enseñaria la rejilla todavia con el destino
				// de ANTES del clic.
				case 32: ComprobarDestinoCofreYCapturar(); break;

				// --- idea 6, hueco cerrado el 20-sep-2026: "buscar por contenido, ordenar/agrupar,
				// traer a mi inventario, arrastrar desde la Libreria" - lo unico que quedaba sin
				// verificar del selector de cofres del mundo. El popup del paso 29-31 ya se cerro
				// solo (ElegirCofreDePrueba llama a Cerrar()), asi que se reabre aqui mismo.
				case 33: AbrirSelectorCofre(); break;
				case 34: BuscarPorContenidoYComprobar(); break;
				case 35: TraerAlInventarioDesdeBusqueda(); break;
				case 36: ComprobarTraidoAlInventario(); break;
				case 37: CiclarOrdenYComprobar(); break;
				case 38: PrepararArrastreDesdeLibreria(); break;
				case 39: ComprobarArrastreDesdeLibreria(); break;

				// Hueco de cobertura cerrado (25-sep-2026, investigacion del bug reportado con
				// imagen9.png: "Libreria / Editar objeto: el pequeño editor queda atrapado, bloquea
				// la Libreria hasta cambiar de pestaña"). Los pasos 26-27 solo comprobaban que la
				// tarjeta flotante se ABRIA; ninguno ejercitaba clicar FUERA de ella con la tarjeta
				// abierta ni cambiar de pestaña con algo dentro del recuadro de seleccion - los dos
				// gestos que el encargo pedia probar explicitamente. Ver el XMLdoc de cada metodo.
				case 40: PrepararTrampaClicFuera(); break;
				case 41: ComprobarTrampaClicFuera(); break;
				case 42: ComprobarTrampaReabreSola(); break;
				case 43: PrepararPerdidaAlCambiarPestana(); break;
				case 44: ComprobarPerdidaAlCambiarPestana(); break;

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

		/// <summary>
		/// TM2: repone los dos objetos de prueba (por si el paso 23, la papelera, se llevo el que
		/// estuviera en el recuadro) y arrastra el APILABLE de vuelta al recuadro de seleccion, con
		/// el mismo camino real que <see cref="ArrastrarAlRecuadroDeSeleccion"/> (paso 15).
		/// </summary>
		private static void PrepararTarjetaFlotante()
		{
			PrepararObjetosDeHerramientas();

			if (_tipoStack <= 0) {
				Registrar("Paso 26 - sin objetos de prueba, se salta.");
				return;
			}

			Player jugador = Main.LocalPlayer;
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null) {
				Registrar("Paso 26 - Contenido.Herramientas es null.");
				return;
			}

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				ItemSlot.LeftClick(jugador.inventory, ItemSlot.Context.InventoryItem, RanuraStack);
				herramientas.Seleccion.EjercitarHandle();
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}

			Registrar("Paso 26 - reposicionado para la tarjeta flotante. Recuadro de seleccion: "
				+ Describir(herramientas.Seleccion.ObjetoActual));
		}

		/// <summary>
		/// TM2: comprueba con DATOS REALES (no solo "compila") que la tarjeta flotante se ha
		/// abierto de verdad - <c>PanelHerramientasLibreriaTk.TarjetaFlotanteAbierta</c>, la misma
		/// bandera que decide si se ve o no - y deja una captura real para inspeccionar a mano el
		/// sprite a 2x, el nombre coloreado por rareza y la linea de prefijo.
		/// </summary>
		private static void ComprobarYCapturarTarjetaFlotante()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null) {
				Registrar("Paso 27 - Contenido.Herramientas es null.");
				return;
			}

			bool hayObjeto = !herramientas.Seleccion.ObjetoActual.IsAir;
			bool tarjetaAbierta = herramientas.TarjetaFlotanteAbierta;
			string textoPrefijo = herramientas.TarjetaFlotante != null
				? herramientas.TarjetaFlotante.TextoPrefijo()
				: "(tarjeta null)";

			Registrar("Paso 27 - tarjeta flotante (TM2): recuadro=" + Describir(herramientas.Seleccion.ObjetoActual)
				+ ", TarjetaFlotanteAbierta=" + tarjetaAbierta
				+ " (" + (hayObjeto == tarjetaAbierta ? "OK: coincide con si hay objeto" : "NO CUADRA") + "), "
				+ "linea de prefijo=\"" + textoPrefijo + "\". "
				+ CapturaDePantalla.Guardar("ws3-tarjeta-flotante"));
		}

		// =========================================================================================
		// Hueco de cobertura cerrado (25-sep-2026): investigacion del bug reportado con imagen9.png
		// ("Libreria / Editar objeto: el pequeño editor queda atrapado, bloquea la Libreria hasta
		// cambiar de pestaña superior"). Los pasos 26-27 de arriba solo comprobaban que la tarjeta
		// flotante se ABRIA con datos reales; ninguno ejercitaba los dos gestos que el propio
		// encargo del usuario pedia probar ("cancelar; click fuera; cambio de pestaña"). Reproducido
		// con evidencia real (GetElementAt + LeftClick, el mismo camino que un clic de verdad, y
		// conteo real de objetos en todos los contenedores del jugador), dos defectos reales
		// encontrados y documentados en bitacora.md para que aplicador-fix los corrija sin tener que
		// reinvestigar nada:
		//
		//  1. CapaSuperposicionTk (UI/Panel/CapaSuperposicionTk.cs) ocupa TODA la zona de contenido
		//     del panel (PanelTerrakeepState.cs: _capaSuperposicion.Width/Height iguales a
		//     _contenedor), no solo el area de la tarjeta. Con la tarjeta abierta
		//     (IgnoresMouseInteraction=false), CUALQUIER clic dentro de esa zona (catalogo, arbol,
		//     busqueda) resuelve a la capa antes que a lo de abajo, y su LeftClick/RightClick
		//     (lineas 165-180) lo cierra con Quitar(null) - pero
		//     PanelHerramientasLibreriaTk.ActualizarTarjetaFlotante (lineas 199-202) la reabre SOLA
		//     el fotograma siguiente porque SlotSeleccionTk.ObjetoActual sigue lleno: bucle cerrado,
		//     ningun clic dentro de la Libreria llega nunca a su destino real mientras haya algo
		//     seleccionado.
		//  2. PanelTerrakeepState.CambiarArea (lineas 609-612) destruye _contenidoActual (con el
		//     PanelHerramientasLibreriaTk y su SlotSeleccionTk) en CUALQUIER cambio de pestaña, sin
		//     devolver antes SlotSeleccionTk.ObjetoActual a ningun sitio - y ese objeto ya SALIO de
		//     su ranura de origen (SlotSeleccionTk.cs, su propio XMLdoc: "arrastrar aqui MUEVE el
		//     objeto de verdad"). El propio "cambiar de pestaña" que el usuario usa para escapar del
		//     bloqueo del punto 1 BORRA PARA SIEMPRE el objeto que estaba editando.
		// =========================================================================================

		/// <summary>Reprepara un objeto de prueba fresco en el recuadro de seleccion (por si los
		/// pasos de cofres de en medio dejaron otro estado) y deja la Libreria enseñando catalogo
		/// real, para tener una ranura de catalogo de verdad con la que probar el bloqueo.</summary>
		private static void PrepararTrampaClicFuera()
		{
			PrepararObjetosDeHerramientas();
			Contenido.IrALaRaiz();
			Contenido.FijarBusqueda("#1-60");

			if (_tipoStack <= 0) {
				Registrar("Paso 40 - sin objetos de prueba, se salta.");
				return;
			}

			Player jugador = Main.LocalPlayer;
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null) {
				Registrar("Paso 40 - Contenido.Herramientas es null.");
				return;
			}

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				ItemSlot.LeftClick(jugador.inventory, ItemSlot.Context.InventoryItem, RanuraStack);
				herramientas.Seleccion.EjercitarHandle();
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}

			Registrar("Paso 40 - preparada la trampa: recuadro de seleccion=" + Describir(herramientas.Seleccion.ObjetoActual)
				+ ", ranuras de catalogo visibles=" + Contenido.SlotsResultado.Count + ".");
		}

		/// <summary>
		/// Bug 1 (el reportado, "queda atrapado"): reproduce con el camino REAL del motor
		/// (<c>UIElement.GetElementAt</c>, la misma llamada que usa <c>UserInterface.Update</c> para
		/// repartir clics) que, con la tarjeta flotante abierta, un clic sobre una ranura REAL del
		/// catalogo no llega a esa ranura sino a <see cref="CapaSuperposicionTk"/> - y dispara ese
		/// clic de verdad (<c>UIElement.LeftClick</c>) para comprobar que ademas cierra la tarjeta,
		/// demostrando que el "clic fuera" SI se registra pero nunca alcanza lo que el jugador
		/// queria pulsar.
		/// </summary>
		private static void ComprobarTrampaClicFuera()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			CapaSuperposicionTk capa = panel != null ? panel.CapaSuperposicion : null;
			if (panel == null || herramientas == null || capa == null || herramientas.TarjetaFlotante == null
				|| !herramientas.TarjetaFlotanteAbierta) {
				Registrar("Paso 41 - condiciones no cumplidas (panel=" + (panel != null) + ", herramientas="
					+ (herramientas != null) + ", capa=" + (capa != null) + ", tarjetaAbierta="
					+ (herramientas != null && herramientas.TarjetaFlotanteAbierta) + "), se salta.");
				return;
			}

			// Punto DENTRO de la capa (misma zona que TODO el contenido de la Libreria: catalogo,
			// arbol, busqueda - ver PanelTerrakeepState.cs, _capaSuperposicion.Width/Height iguales a
			// _contenedor) pero FUERA de la tarjeta flotante - las dos geometrias se leen EN VIVO
			// (GetDimensions ya recalculado, mismo fotograma), nada hardcodeado ni supuesto. La
			// tarjeta se posiciona "pegada" al mini-panel (PosicionarTarjetaFlotante, a la izquierda o
			// derecha de el, nunca en la esquina superior-izquierda de la capa), asi que la esquina
			// superior-izquierda de la capa + un margen pequeño cae, por construccion, fuera de ella -
			// se comprueba de todas formas antes de usarlo, sin asumirlo a ciegas.
			Rectangle capaRect = capa.GetDimensions().ToRectangle();
			Rectangle tarjetaRect = herramientas.TarjetaFlotante.GetDimensions().ToRectangle();
			Vector2 puntoFueraDeLaTarjeta = new Vector2(capaRect.X + 12f, capaRect.Y + 12f);

			if (tarjetaRect.Contains(puntoFueraDeLaTarjeta.ToPoint())) {
				Registrar("Paso 41 - el punto de control (" + (int)puntoFueraDeLaTarjeta.X + ","
					+ (int)puntoFueraDeLaTarjeta.Y + ") cae DENTRO de la tarjeta (rect=" + tarjetaRect
					+ "), no sirve para probar \"fuera\"; se salta este paso.");
				return;
			}

			UIElement bajoElRaton = panel.GetElementAt(puntoFueraDeLaTarjeta);
			bool esLaCapaOAlgoDeLaTarjeta = bajoElRaton != null
				&& (ReferenceEquals(bajoElRaton, capa) || EsDescendienteDe(bajoElRaton, capa));

			Registrar("Paso 41 - clic REAL (via UIElement.GetElementAt, la misma llamada que reparte "
				+ "clics de verdad) en (" + (int)puntoFueraDeLaTarjeta.X + "," + (int)puntoFueraDeLaTarjeta.Y
				+ "): dentro del area de contenido de la capa (rect=" + capaRect + ") pero fuera de la "
				+ "tarjeta flotante (rect=" + tarjetaRect + "), CON la tarjeta abierta. El motor entrega "
				+ "el punto a: " + (bajoElRaton == null ? "null" : bajoElRaton.GetType().Name) + ". "
				+ (esLaCapaOAlgoDeLaTarjeta
					? "OK (bug REPRODUCIDO): CUALQUIER clic dentro de la zona de contenido de la Libreria "
						+ "que no caiga exactamente sobre la tarjeta lo absorbe CapaSuperposicionTk - la "
						+ "rejilla de catalogo, el arbol de carpetas y la busqueda quedan inalcanzables "
						+ "mientras haya algo en el recuadro de seleccion."
					: "revisar: ni la capa ni la tarjeta reciben el punto (" + bajoElRaton?.GetType().Name
						+ "), la hipotesis no se confirma con ESTE punto concreto."));

			if (bajoElRaton != null) {
				bool abiertaAntes = herramientas.TarjetaFlotanteAbierta;
				bajoElRaton.LeftClick(new UIMouseEvent(bajoElRaton, puntoFueraDeLaTarjeta));
				bool abiertaDespues = herramientas.TarjetaFlotanteAbierta;
				Registrar("Paso 41 - se dispara ese clic de verdad (UIElement.LeftClick) sobre lo que "
					+ "recibio el punto. TarjetaFlotanteAbierta antes=" + abiertaAntes + ", justo despues="
					+ abiertaDespues + " (" + (abiertaAntes && !abiertaDespues
						? "el clic SI cerro la tarjeta (CapaSuperposicionTk.Quitar) - pero el objeto sigue "
							+ "en el recuadro, asi que el paso siguiente comprueba si se reabre sola"
						: "no cambio") + "). Recuadro de seleccion tras el clic: "
					+ Describir(herramientas.Seleccion.ObjetoActual) + " (se espera que SIGA con algo dentro: "
					+ "el clic fuera nunca lo vacia).");
			}
		}

		/// <summary>Un paso real despues (~<see cref="FotogramasEntrePasos"/> fotogramas): comprueba
		/// que la tarjeta se ha vuelto a abrir SOLA, sin ningun clic nuevo -
		/// <c>PanelHerramientasLibreriaTk.ActualizarTarjetaFlotante</c> la reabre en cuanto ve que
		/// <c>SlotSeleccionTk.ObjetoActual</c> sigue lleno - cerrando el circulo: el clic de fuera SI
		/// cierra la tarjeta un instante (paso 41), pero nunca deja pasar el clic Y ademas la reabre
		/// ella sola el fotograma siguiente, asi que no hay forma real de interactuar con el resto de
		/// la Libreria mientras haya algo seleccionado.</summary>
		private static void ComprobarTrampaReabreSola()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null) {
				Registrar("Paso 42 - Contenido.Herramientas es null.");
				return;
			}

			bool sigueConAlgo = !herramientas.Seleccion.ObjetoActual.IsAir;
			bool reabierta = herramientas.TarjetaFlotanteAbierta;

			Registrar("Paso 42 - " + FotogramasEntrePasos + " fotogramas reales despues del clic fuera "
				+ "del paso 41, SIN pulsar nada mas: recuadro de seleccion sigue con algo dentro=" + sigueConAlgo
				+ ", TarjetaFlotanteAbierta=" + reabierta + " -> "
				+ (sigueConAlgo && reabierta
					? "OK (bug CONFIRMADO): se reabrio SOLA, sin ningun clic nuevo - el bucle "
						+ "cierra/reabre nunca deja un hueco real para clicar otra cosa de la Libreria."
					: "no se reabrio, revisar la hipotesis") + " "
				+ CapturaDePantalla.Guardar("ws3-trampa-tarjeta-flotante"));
		}

		private static int _tipoPerdida;
		private static int _stackPerdidaAntes;

		/// <summary>Deja constancia REAL de cuanto hay del objeto seleccionado en TODOS los demas
		/// sitios del juego antes de cambiar de pestaña, para poder comparar despues.</summary>
		private static void PrepararPerdidaAlCambiarPestana()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null || herramientas.Seleccion.ObjetoActual.IsAir) {
				Registrar("Paso 43 - el recuadro de seleccion esta vacio (pasos 40-42 no dejaron nada "
					+ "dentro), se salta la prueba de perdida de objeto.");
				_tipoPerdida = 0;
				return;
			}

			_tipoPerdida = herramientas.Seleccion.ObjetoActual.type;
			_stackPerdidaAntes = herramientas.Seleccion.ObjetoActual.stack;
			int totalFueraDelRecuadro = ContarTotalDelTipoEnElJuego(_tipoPerdida);

			Registrar("Paso 43 - antes de cambiar de pestaña: recuadro de seleccion=" + Describir(herramientas.Seleccion.ObjetoActual)
				+ ". Ese mismo tipo en cualquier OTRO sitio del juego (los 7 contenedores fijos del "
				+ "jugador, papelera, suelo, raton)=" + totalFueraDelRecuadro + " (se espera 0: el objeto "
				+ "salio de su origen al arrastrarlo aqui, es el UNICO sitio donde existe ahora mismo).");
		}

		/// <summary>
		/// Bug 2 (el gesto que el usuario usa para "escapar" del bloqueo del paso 41/42: cambiar a
		/// otra pestaña superior y volver): reproduce con <c>PanelTerrakeepState.CambiarArea</c> real
		/// (el mismo metodo que dispara el clic de una pestaña de verdad) y compara con conteo REAL,
		/// en todos los contenedores del jugador a la vez, si el objeto que estaba en el recuadro de
		/// seleccion sigue existiendo en algun sitio despues.
		/// </summary>
		private static void ComprobarPerdidaAlCambiarPestana()
		{
			if (_tipoPerdida <= 0) {
				Registrar("Paso 44 - sin objeto de prueba (paso 43 se salto), se salta.");
				return;
			}

			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				Registrar("Paso 44 - PanelTerrakeepSystem.Panel es null.");
				return;
			}

			panel.CambiarArea(AreaTerrakeep.Personaje, "autoprueba WS3 - trampa tarjeta flotante");
			panel.CambiarArea(AreaTerrakeep.Libreria, "autoprueba WS3 - trampa tarjeta flotante");

			int totalDespues = ContarTotalDelTipoEnElJuego(_tipoPerdida);
			PanelHerramientasLibreriaTk herramientasNuevas = Contenido.Herramientas;
			bool recuadroNuevoVacio = herramientasNuevas == null || herramientasNuevas.Seleccion.ObjetoActual.IsAir;

			Registrar("Paso 44 - tras cambiar Libreria -> Personaje -> Libreria (con " + _stackPerdidaAntes
				+ " unidades reales de type=" + _tipoPerdida + " dentro del recuadro de seleccion ANTES "
				+ "de cambiar): PanelHerramientasLibreriaTk se reconstruye de cero (nuevo Contenido.Herramientas), "
				+ "su recuadro de seleccion nuevo esta vacio=" + recuadroNuevoVacio + ". Ese mismo tipo AHORA en "
				+ "cualquier sitio del juego (los 7 contenedores fijos, papelera, suelo, raton)=" + totalDespues + " -> "
				+ (totalDespues == 0 && recuadroNuevoVacio
					? "OK (bug CONFIRMADO): las " + _stackPerdidaAntes + " unidades que habia en el recuadro "
						+ "de seleccion ANTES de cambiar de pestaña ya NO ESTAN EN NINGUN SITIO del juego - "
						+ "se han borrado de verdad, no se han movido. PanelTerrakeepState.CambiarArea destruye "
						+ "el contenido sin devolver antes SlotSeleccionTk.ObjetoActual a su origen."
					: "no se perdio nada, revisar la hipotesis") + ".");
		}

		/// <summary>Sube por la cadena <c>Parent</c> desde <paramref name="hijo"/> buscando si es
		/// <paramref name="posiblePadre"/> o cuelga de el - la forma real de comprobar si un
		/// elemento que recibio el raton pertenece a la capa de superposicion (o a lo que flota
		/// dentro de ella) sin asumir que sea EXACTAMENTE la capa misma.</summary>
		private static bool EsDescendienteDe(UIElement hijo, UIElement posiblePadre)
		{
			UIElement actual = hijo;
			while (actual != null) {
				if (ReferenceEquals(actual, posiblePadre)) {
					return true;
				}
				actual = actual.Parent;
			}
			return false;
		}

		/// <summary>Suma cuanto hay del <paramref name="tipo"/> pedido en TODOS los sitios reales
		/// donde podria estar fuera del recuadro de seleccion: los <see cref="ContenidoLibreria.Destinos"/>
		/// del jugador (los mismos 7-8 contenedores fijos que ya recorre el paso 10, via
		/// TotalDestinos/MostrarDestino/ArrayDestino), la papelera, el raton y lo tirado por el
		/// suelo. Si un objeto real desaparece de verdad (no esta en NINGUNO de estos sitios, ni en
		/// el propio recuadro) es que el motor lo ha borrado, no que se haya movido de sitio.</summary>
		private static int ContarTotalDelTipoEnElJuego(int tipo)
		{
			int total = 0;
			ContenidoLibreria contenido = Contenido;
			if (contenido != null) {
				for (int i = 0; i < contenido.TotalDestinos; i++) {
					contenido.MostrarDestino(i);
					Item[] array = contenido.ArrayDestino;
					if (array == null) {
						continue;
					}
					for (int j = 0; j < array.Length; j++) {
						if (array[j] != null && !array[j].IsAir && array[j].type == tipo) {
							total += array[j].stack;
						}
					}
				}
				contenido.MostrarDestino(0);
			}

			Player jugador = Main.LocalPlayer;
			if (jugador != null && jugador.trashItem != null && !jugador.trashItem.IsAir && jugador.trashItem.type == tipo) {
				total += jugador.trashItem.stack;
			}

			for (int i = 0; i < Main.item.Length; i++) {
				if (Main.item[i] != null && Main.item[i].active && Main.item[i].type == tipo) {
					total += Main.item[i].stack;
				}
			}

			if (Main.mouseItem != null && !Main.mouseItem.IsAir && Main.mouseItem.type == tipo) {
				total += Main.mouseItem.stack;
			}

			return total;
		}

		/// <summary>Ultimo hueco de <c>Main.chest[]</c> (tope real 8000, <c>Main.maxChests</c>):
		/// una partida real jamas llega a usarlo (los cofres se asignan por orden creciente segun
		/// se colocan/generan), asi que es seguro y estable sembrar aqui el cofre SINTETICO de
		/// prueba en cada pasada, sin arriesgarse a pisar un cofre real del mundo de pruebas.</summary>
		private const int IndiceCofrePrueba = 7999;

		/// <summary>
		/// Idea 6 (cofres del mundo en vivo). SOLO ARNES DE PRUEBAS: siembra un <see cref="Chest"/>
		/// SINTETICO (nunca un cofre real del jugador) con dos objetos reales conocidos, siguiendo
		/// el mismo patron con el que el propio juego crea uno de verdad
		/// (<c>Chest.CreateChest</c>, decompilado: <c>item[i] = new Item()</c> en las 40 ranuras
		/// antes de rellenar ninguna). Se repite cada pasada, asi que es idempotente.
		/// </summary>
		private static void SembrarCofreDePrueba()
		{
			if (Main.chest == null || IndiceCofrePrueba >= Main.chest.Length) {
				Registrar("Paso 28 - Main.chest no esta listo, se salta.");
				return;
			}

			Player jugador = Main.LocalPlayer;
			int tileX = (int)(jugador.Center.X / 16f) + 6;
			int tileY = (int)(jugador.Center.Y / 16f);

			Chest cofre = new Chest();
			cofre.x = tileX;
			cofre.y = tileY;
			cofre.name = "Cofre de prueba WS3";
			for (int i = 0; i < cofre.item.Length; i++) {
				cofre.item[i] = new Item();
			}
			cofre.item[0].SetDefaults(ItemID.IronBar);
			cofre.item[0].stack = 20;
			cofre.item[3].SetDefaults(ItemID.GoldCoin);
			cofre.item[3].stack = 5;

			Main.chest[IndiceCofrePrueba] = cofre;

			Registrar("Paso 28 - SOLO ARNES DE PRUEBAS: sembrado un cofre sintetico en Main.chest["
				+ IndiceCofrePrueba + "] (nunca un cofre real del jugador) en tile (" + tileX + ", " + tileY
				+ ") con 2 objetos reales conocidos: " + Describir(cofre.item[0]) + ", " + Describir(cofre.item[3]) + ".");
		}

		/// <summary>Idea 6: clic REAL sobre el boton "Cofre" de la barra de destinos, por la misma
		/// ruta que un clic de verdad (<c>UIElement.LeftClick</c>).</summary>
		private static void AbrirSelectorCofre()
		{
			ContenidoLibreria contenido = Contenido;
			BotonTk boton = contenido != null ? contenido.BotonDestinoCofre : null;
			if (contenido == null || boton == null || contenido.SelectorCofre == null) {
				Registrar("Paso 29 - no se encontro el boton \"Cofre\" o el selector, se salta.");
				return;
			}

			CalculatedStyle dim = boton.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			boton.LeftClick(new UIMouseEvent(boton, centro));

			Registrar("Paso 29 - clic real en el boton \"Cofre\" (x=" + (int)dim.X + " y=" + (int)dim.Y + " "
				+ (int)dim.Width + "x" + (int)dim.Height + "). Selector abierto=" + contenido.SelectorCofre.Abierto + ".");
		}

		/// <summary>Idea 6: con el selector ya abierto (paso 29), cuenta los cofres reales del
		/// mundo y deja una captura real para inspeccionar la lista a mano.</summary>
		private static void ComprobarYCapturarSelectorCofre()
		{
			ContenidoLibreria contenido = Contenido;
			SelectorCofreMundoTk selector = contenido != null ? contenido.SelectorCofre : null;
			if (selector == null || !selector.Abierto) {
				Registrar("Paso 30 - el selector no esta abierto (paso 29 fallo), se salta.");
				return;
			}

			int totalReales = SelectorCofreMundoTk.TotalCofresReales();
			Registrar("Paso 30 - selector de cofres abierto: " + totalReales + " cofres reales en Main.chest[], "
				+ selector.FilasDibujadas + " filas dibujadas (Main.netMode=" + Main.netMode + "). Geometria real: "
				+ selector.GeometriaParaPrueba() + ". "
				+ CapturaDePantalla.Guardar("ws3-selector-cofre"));
		}

		/// <summary>Texto real de la fila pulsada en el paso 31, para que el paso 32 (un fotograma
		/// real despues) pueda dejarlo en su propio mensaje sin tener que volver a buscarla.</summary>
		private static string _filaCofrePulsada;

		/// <summary>Idea 6: busca entre las filas REALES del popup (paso 30) la del cofre sembrado
		/// en el paso 28 por su nombre y la pulsa de verdad. La comprobacion de que la rejilla de
		/// destino cambio de verdad va en el paso 32, un fotograma real despues (ver su
		/// comentario).</summary>
		private static void ElegirCofreDePrueba()
		{
			ContenidoLibreria contenido = Contenido;
			SelectorCofreMundoTk selector = contenido != null ? contenido.SelectorCofre : null;
			if (contenido == null || selector == null || !selector.Abierto) {
				Registrar("Paso 31 - el selector no esta abierto (paso 29/30 fallo), se salta.");
				return;
			}

			List<BotonTk> filas = selector.FilasParaAutoprueba();
			BotonTk filaDelCofre = filas.Find(b => b.Texto != null && b.Texto.Contains("prueba WS3"));
			if (filaDelCofre == null) {
				Registrar("Paso 31 - no se encontro la fila del cofre sembrado entre " + filas.Count
					+ " filas reales, se salta. Textos: " + string.Join(" | ", filas.ConvertAll(b => b.Texto)));
				_filaCofrePulsada = null;
				return;
			}

			_filaCofrePulsada = filaDelCofre.Texto;

			CalculatedStyle dim = filaDelCofre.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			filaDelCofre.LeftClick(new UIMouseEvent(filaDelCofre, centro));

			Registrar("Paso 31 - clic real en la fila del cofre sembrado: \"" + filaDelCofre.Texto + "\".");
		}

		/// <summary>Un fotograma real despues del clic (paso 31): comprueba que la rejilla de
		/// destino pasa a enseñar el <c>Item[]</c> de ESE cofre exacto (no una copia) - los dos
		/// objetos conocidos tienen que aparecer tal cual en <c>ArrayDestino</c> - y deja una
		/// captura real ya con el cambio dibujado.</summary>
		private static void ComprobarDestinoCofreYCapturar()
		{
			ContenidoLibreria contenido = Contenido;
			if (contenido == null || _filaCofrePulsada == null) {
				Registrar("Paso 32 - no se pulso ninguna fila (paso 31 fallo), se salta.");
				return;
			}

			Item[] arrayDestino = contenido.ArrayDestino;
			bool coincideArray = arrayDestino != null && Main.chest[IndiceCofrePrueba] != null
				&& ReferenceEquals(arrayDestino, Main.chest[IndiceCofrePrueba].item);
			bool item0Ok = arrayDestino != null && arrayDestino.Length > 0
				&& arrayDestino[0].type == ItemID.IronBar && arrayDestino[0].stack == 20;
			bool item3Ok = arrayDestino != null && arrayDestino.Length > 3
				&& arrayDestino[3].type == ItemID.GoldCoin && arrayDestino[3].stack == 5;

			Registrar("Paso 32 - fila pulsada en el paso 31: \"" + _filaCofrePulsada + "\". Destino actual=\""
				+ contenido.NombreDestino + "\". ArrayDestino es el Item[] REAL del cofre sembrado="
				+ coincideArray + " (" + (coincideArray ? "OK" : "NO CUADRA") + "); ranura 0="
				+ Describir(arrayDestino != null && arrayDestino.Length > 0 ? arrayDestino[0] : null)
				+ " (" + (item0Ok ? "OK" : "NO CUADRA") + "); ranura 3="
				+ Describir(arrayDestino != null && arrayDestino.Length > 3 ? arrayDestino[3] : null)
				+ " (" + (item3Ok ? "OK" : "NO CUADRA") + "). "
				+ CapturaDePantalla.Guardar("ws3-destino-cofre"));
		}

		// =========================================================================================
		// Idea 6, hueco cerrado el 20-sep-2026: cotejo literal del coordinador contra el catalogo
		// encontro que faltaba "buscar por contenido, ordenar/agrupar, traer a mi inventario,
		// arrastrar desde la Libreria" - el selector de cofres solo tenia la lista por distancia.
		// =========================================================================================

		private static int _mochilaAntesDeTraer;

		/// <summary>Idea 6, "buscar por contenido": escribe un trozo REAL del nombre del Iron Bar
		/// (el objeto que el paso 28 de verdad puso en el cofre sintetico) en el campo de busqueda,
		/// por la via <c>BuscarParaPrueba</c> (dispara el mismo <c>AlCambiar</c> real que una tecla
		/// de verdad), y comprueba que el cofre que SI lo tiene sigue en la lista con su boton
		/// "Traer" real.</summary>
		private static void BuscarPorContenidoYComprobar()
		{
			ContenidoLibreria contenido = Contenido;
			SelectorCofreMundoTk selector = contenido != null ? contenido.SelectorCofre : null;
			if (selector == null || !selector.Abierto) {
				Registrar("Paso 34 - el selector no esta abierto (paso 33 fallo), se salta.");
				return;
			}

			Item muestraBarra;
			string nombreReal = ContentSamples.ItemsByType.TryGetValue(ItemID.IronBar, out muestraBarra) && muestraBarra != null
				? muestraBarra.Name
				: "Iron Bar";
			string trozoBusqueda = nombreReal.Length > 4 ? nombreReal.Substring(0, 4) : nombreReal;

			selector.BuscarParaPrueba(trozoBusqueda);

			List<BotonTk> filas = selector.FilasParaAutoprueba();
			BotonTk filaDelCofre = filas.Find(b => b.Texto != null && b.Texto.Contains("prueba WS3"));
			List<BotonTk> botonesTraer = selector.BotonesTraerParaAutoprueba();

			Registrar("Paso 34 - idea 6, \"buscar por contenido\": busqueda=\"" + trozoBusqueda
				+ "\" (trozo real del nombre \"" + nombreReal + "\"). Filas dibujadas=" + selector.FilasDibujadas
				+ ", fila del cofre sintetico encontrada=" + (filaDelCofre != null)
				+ ", boton \"Traer\" real visible=" + (botonesTraer.Count > 0) + " -> "
				+ (filaDelCofre != null && botonesTraer.Count > 0
					? "OK: la busqueda por contenido encontro el cofre real que SI tiene el objeto dentro."
					: "NO CUADRA."));
		}

		/// <summary>Idea 6, "traer a mi inventario": clic REAL en el boton "Traer" que dejo el paso
		/// 34, contando antes cuanto Iron Bar lleva de verdad el jugador en la mochila.</summary>
		private static void TraerAlInventarioDesdeBusqueda()
		{
			ContenidoLibreria contenido = Contenido;
			SelectorCofreMundoTk selector = contenido != null ? contenido.SelectorCofre : null;
			if (selector == null || !selector.Abierto) {
				Registrar("Paso 35 - el selector no esta abierto, se salta.");
				return;
			}
			List<BotonTk> botonesTraer = selector.BotonesTraerParaAutoprueba();
			if (botonesTraer.Count == 0) {
				Registrar("Paso 35 - no hay ningun boton \"Traer\" real (paso 34 fallo), se salta.");
				return;
			}

			_mochilaAntesDeTraer = ContarEnMochila(ItemID.IronBar);

			BotonTk boton = botonesTraer[0];
			CalculatedStyle dim = boton.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			boton.LeftClick(new UIMouseEvent(boton, centro));

			Registrar("Paso 35 - clic real en el boton \"" + boton.Texto + "\". Antes de pulsar, la mochila real "
				+ "tenia " + _mochilaAntesDeTraer + " de Iron Bar.");
		}

		/// <summary>Un fotograma real despues del clic (paso 35): el objeto tiene que haber
		/// desaparecido del cofre REAL del mundo y haber aparecido en la mochila REAL del
		/// jugador - las dos cosas a la vez, por <c>Player.GetItem</c>, la misma API publica que
		/// usa el boton "Loot All" de vanilla.</summary>
		private static void ComprobarTraidoAlInventario()
		{
			Chest cofre = Main.chest != null && IndiceCofrePrueba < Main.chest.Length ? Main.chest[IndiceCofrePrueba] : null;
			int ahora = ContarEnMochila(ItemID.IronBar);
			bool cofreVacioEnRanura0 = cofre != null && cofre.item[0] != null && cofre.item[0].IsAir;
			bool mochilaSubio = ahora > _mochilaAntesDeTraer;

			Registrar("Paso 36 - tras \"traer a mi inventario\": mochila real ahora tiene " + ahora
				+ " de Iron Bar (antes " + _mochilaAntesDeTraer + "). Ranura 0 del cofre sintetico vacia="
				+ cofreVacioEnRanura0 + ". " + CapturaDePantalla.Guardar("ws3-traer-a-inventario") + " -> "
				+ (mochilaSubio && cofreVacioEnRanura0
					? "OK: el objeto se movio de verdad del cofre del mundo al inventario real."
					: "NO CUADRA."));
		}

		private static int ContarEnMochila(int tipo)
		{
			Player jugador = Main.LocalPlayer;
			if (jugador == null || jugador.inventory == null) {
				return 0;
			}
			int total = 0;
			int tope = Math.Min(58, jugador.inventory.Length);
			for (int i = 0; i < tope; i++) {
				Item objeto = jugador.inventory[i];
				if (objeto != null && !objeto.IsAir && objeto.type == tipo) {
					total += objeto.stack;
				}
			}
			return total;
		}

		/// <summary>Idea 6, "ordenar/agrupar": limpia la busqueda (para ver todos los cofres) y
		/// cicla el boton de orden real tres veces - tiene que pasar por los tres modos reales
		/// (Distancia/Nombre/Llenos) sin excepciones y volver al inicial a la cuarta.</summary>
		private static void CiclarOrdenYComprobar()
		{
			ContenidoLibreria contenido = Contenido;
			SelectorCofreMundoTk selector = contenido != null ? contenido.SelectorCofre : null;
			if (selector == null || !selector.Abierto) {
				Registrar("Paso 37 - el selector no esta abierto, se salta.");
				return;
			}
			selector.BuscarParaPrueba("");

			string modo0 = selector.ModoOrdenParaPrueba;
			selector.CiclarOrdenParaPrueba();
			string modo1 = selector.ModoOrdenParaPrueba;
			selector.CiclarOrdenParaPrueba();
			string modo2 = selector.ModoOrdenParaPrueba;
			selector.CiclarOrdenParaPrueba();
			string modo3 = selector.ModoOrdenParaPrueba;

			bool ciclaBien = modo0 != modo1 && modo1 != modo2 && modo2 != modo3 && modo3 == modo0;
			Registrar("Paso 37 - idea 6, \"ordenar/agrupar\": ciclo real de modos " + modo0 + " -> " + modo1
				+ " -> " + modo2 + " -> " + modo3 + " -> "
				+ (ciclaBien ? "OK: los tres modos ciclan sin excepciones y vuelve al inicial." : "NO CUADRA."));
		}

		/// <summary>Idea 6, "arrastrar desde la Libreria": vuelve a elegir el cofre sintetico como
		/// destino (la busqueda ya esta limpia desde el paso 37, asi que su fila real vuelve a
		/// estar en la lista).</summary>
		private static void PrepararArrastreDesdeLibreria()
		{
			ContenidoLibreria contenido = Contenido;
			SelectorCofreMundoTk selector = contenido != null ? contenido.SelectorCofre : null;
			if (contenido == null || selector == null || !selector.Abierto) {
				Registrar("Paso 38 - el selector no esta abierto (paso 37 fallo), se salta.");
				return;
			}
			List<BotonTk> filas = selector.FilasParaAutoprueba();
			BotonTk filaDelCofre = filas.Find(b => b.Texto != null && b.Texto.Contains("prueba WS3"));
			if (filaDelCofre == null) {
				Registrar("Paso 38 - no se encontro la fila del cofre sintetico entre " + filas.Count + " filas, se salta.");
				return;
			}

			CalculatedStyle dim = filaDelCofre.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			filaDelCofre.LeftClick(new UIMouseEvent(filaDelCofre, centro));

			Registrar("Paso 38 - re-elegido el cofre sintetico como destino (\"" + filaDelCofre.Texto
				+ "\"), para comprobar \"arrastrar desde la Libreria\" contra un cofre del MUNDO.");
		}

		private const int RanuraArrastreCofreMundo = 10;
		private const int TipoArrastreCofreMundo = ItemID.LifeCrystal;

		/// <summary>Un fotograma real despues (paso 38): coloca un objeto real del catalogo en el
		/// cofre del MUNDO elegido por la MISMA ruta real de vanilla que ya usa cualquier otro
		/// destino (<c>ItemSlot.LeftClick</c>, via <c>ContenidoLibreria.ColocarEnRanura</c>) - "
		/// arrastrar desde la Libreria" nunca fue un mecanismo aparte para el cofre del mundo,
		/// reutiliza el generico de siempre; esto lo comprueba de verdad en vez de darlo por
		/// hecho.</summary>
		private static void ComprobarArrastreDesdeLibreria()
		{
			ContenidoLibreria contenido = Contenido;
			if (contenido == null) {
				Registrar("Paso 39 - sin ContenidoLibreria, se salta.");
				return;
			}

			string resultado = contenido.ColocarEnRanura(TipoArrastreCofreMundo, RanuraArrastreCofreMundo, false);

			Chest cofre = Main.chest != null && IndiceCofrePrueba < Main.chest.Length ? Main.chest[IndiceCofrePrueba] : null;
			Item enElCofre = cofre != null && cofre.item.Length > RanuraArrastreCofreMundo
				? cofre.item[RanuraArrastreCofreMundo] : null;
			bool ok = enElCofre != null && !enElCofre.IsAir && enElCofre.type == TipoArrastreCofreMundo;

			Registrar("Paso 39 - idea 6, \"arrastrar desde la Libreria\": destino actual=\"" + contenido.NombreDestino
				+ "\". Colocado en la ranura " + RanuraArrastreCofreMundo + " del cofre del mundo (devuelto: "
				+ resultado + "). Ranura real del cofre ahora=" + Describir(enElCofre) + ". "
				+ CapturaDePantalla.Guardar("ws3-arrastre-a-cofre") + " -> "
				+ (ok ? "OK: el objeto del catalogo llego de verdad al cofre real del mundo." : "NO CUADRA."));
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

		// --- Pasos 19-21: los tres bugs del desplegable de prefijos, con datos reales -------------
		private static float _scrollAntes;
		private static float _filaAntes;
		private static List<string> _visiblesAntes = new List<string>();

		/// <summary>
		/// Bugs 1 y 2 del encargo: el desplegable se dibujaba FUERA del panel (sobre el mundo del
		/// juego) y el boton "Cerrar (O)" lo tapaba. Los dos se comprueban con geometria REAL ya
		/// calculada, no de vista: que el rectangulo del popup cabe entero dentro de la capa del
		/// panel, que NO se cruza con el del boton de cerrar, y que la capa se dibuja DESPUES que ese
		/// boton (o sea, por encima) en el orden real de hijos del marco.
		/// </summary>
		private static void ComprobarPopupDentroDelPanel()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (herramientas == null || panel == null || !herramientas.EditorPrefijo.PopupAbierto) {
				Registrar("Paso 19 - el popup no esta abierto (paso 18 fallo), se salta.");
				return;
			}

			EditorPrefijoTk editor = herramientas.EditorPrefijo;
			Registrar("Paso 19 - geometria real: " + editor.DiagnosticoGeometria());
			Registrar("Paso 19 - orden REAL de dibujado dentro del marco (UIElement.DrawChildren "
				+ "dibuja en el orden de Append, asi que el ULTIMO es el que queda encima): "
				+ panel.DiagnosticoOrdenDibujado());

			// Bug 1: dentro del panel.
			Registrar("Paso 19 - el popup cabe entero dentro de la capa del panel: "
				+ editor.DentroDeLaCapa + " (" + (editor.DentroDeLaCapa
					? "OK, ya no se sale hacia el mundo del juego" : "FALLO") + ").");

			// Bug 2: ni tapado por el boton Cerrar, ni cruzandose con el.
			Rectangle popup = editor.RectanguloPopup.ToRectangle();
			Rectangle cerrar = panel.BotonCerrar != null
				? panel.BotonCerrar.GetDimensions().ToRectangle() : Rectangle.Empty;
			bool seCruzan = popup.Intersects(cerrar);
			int indicePopup = IndiceEnElMarco(panel, panel.CapaSuperposicion);
			int indiceCerrar = IndiceEnElMarco(panel, panel.BotonCerrar);
			Registrar("Paso 19 - boton \"Cerrar\" en x=" + cerrar.X + " y=" + cerrar.Y + " "
				+ cerrar.Width + "x" + cerrar.Height + "; popup en x=" + popup.X + " y=" + popup.Y
				+ " " + popup.Width + "x" + popup.Height + ". Se cruzan: " + seCruzan + " ("
				+ (seCruzan ? "se cruzan, pero" : "OK, ni se rozan; y ademas") + " la capa se dibuja "
				+ "en la posicion " + indicePopup + " del marco y el boton Cerrar en la " + indiceCerrar
				+ " -> " + (indicePopup > indiceCerrar
					? "OK, el desplegable queda POR ENCIMA del boton de cerrar"
					: "FALLO: el boton de cerrar se dibuja despues y lo taparia") + ").");

			// Bug 3, primera mitad: que el motor le entregue el raton al popup. Es la misma llamada
			// exacta que hace UserInterface.Update para repartir clics y rueda.
			Vector2 centro = editor.CentroPopup;
			UIElement bajoElRaton = panel.GetElementAt(centro);
			bool esDelPopup = editor.EsDelPopup(bajoElRaton);
			Registrar("Paso 19 - UIElement.GetElementAt(" + (int)centro.X + "," + (int)centro.Y
				+ ") (la MISMA llamada con la que UserInterface reparte clics y rueda) devuelve: "
				+ (bajoElRaton == null ? "null" : bajoElRaton.GetType().Name
					+ (bajoElRaton is BotonTk ? " \"" + ((BotonTk)bajoElRaton).Texto + "\"" : ""))
				+ " -> " + (esDelPopup
					? "OK, el raton llega de verdad al desplegable"
					: "FALLO: el motor entrega el raton a otra cosa, el desplegable esta sordo") + ".");

			Registrar("Paso 19 - filas visibles ahora (arriba del todo): "
				+ string.Join(" | ", editor.FilasVisibles()));

			// Captura 1 de las dos que pide la verificacion: el desplegable abierto, sin desplazar.
			Registrar("Paso 19 - " + CapturaDePantalla.Guardar("ws3-prefijo-1-abierto-dentro-del-panel"));
		}

		/// <summary>Posicion de un elemento entre los hijos del marco (el orden en que se dibujan).</summary>
		private static int IndiceEnElMarco(PanelTerrakeepState panel, UIElement buscado)
		{
			if (panel == null || buscado == null) {
				return -1;
			}
			int indice = 0;
			foreach (UIElement hijo in panel.MarcoHijos) {
				if (ReferenceEquals(hijo, buscado)) {
					return indice;
				}
				indice++;
			}
			return -1;
		}

		/// <summary>
		/// Bug 3 del encargo: "el scroll del desplegable no funciona". Se mueve la rueda DE VERDAD,
		/// por la ruta real del motor: se pregunta que elemento hay bajo el punto
		/// (<c>GetElementAt</c>) y se le manda un <c>UIScrollWheelEvent</c> - exactamente los dos
		/// pasos que da <c>UserInterface.Update</c> con una rueda real. Tres muescas de 120, como
		/// tres golpes de rueda seguidos.
		/// </summary>
		private static void ComprobarScrollReal()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (herramientas == null || panel == null || !herramientas.EditorPrefijo.PopupAbierto) {
				Registrar("Paso 20 - el popup no esta abierto, se salta el scroll.");
				return;
			}

			EditorPrefijoTk editor = herramientas.EditorPrefijo;
			if (!editor.NecesitaScroll) {
				Registrar("Paso 20 - la lista de prefijos de este objeto CABE entera (CanScroll=false), "
					+ "asi que no hay scroll que probar aqui. " + editor.DiagnosticoScroll());
				return;
			}

			_scrollAntes = editor.PosicionScroll;
			_filaAntes = editor.PrimeraFilaY;
			_visiblesAntes = editor.FilasVisibles();

			Vector2 centro = editor.CentroPopup;
			UIElement bajoElRaton = panel.GetElementAt(centro);
			if (bajoElRaton == null || !editor.EsDelPopup(bajoElRaton)) {
				Registrar("Paso 20 - FALLO: bajo el centro del popup el motor no encuentra nada del "
					+ "popup (" + (bajoElRaton == null ? "null" : bajoElRaton.GetType().Name)
					+ "), asi que la rueda nunca podria llegarle. No se sigue.");
				return;
			}

			for (int muesca = 0; muesca < 3; muesca++) {
				bajoElRaton.ScrollWheel(new UIScrollWheelEvent(bajoElRaton, centro, -120));
			}

			Registrar("Paso 20 - rueda REAL hacia abajo (3 muescas de 120 sobre \""
				+ bajoElRaton.GetType().Name + "\", via UIElement.ScrollWheel, igual que "
				+ "UserInterface.Update). ViewPosition " + _scrollAntes.ToString("0.0") + " -> "
				+ editor.PosicionScroll.ToString("0.0") + " ("
				+ (editor.PosicionScroll > _scrollAntes ? "OK, la barra se ha movido"
					: "FALLO, la barra sigue igual") + "). " + editor.DiagnosticoScroll()
				+ ". El contenido dibujado se comprueba en el paso siguiente: UIList mueve sus filas "
				+ "en su DrawSelf, o sea en el fotograma que viene, no en este.");
		}

		/// <summary>
		/// Segunda mitad de la prueba de scroll: con el desplazamiento ya DIBUJADO, se comprueba que
		/// las filas se han movido de sitio de verdad y que las que se ven son otras - y se guarda la
		/// segunda captura, la que junto a la del paso 19 demuestra que el contenido visible cambia.
		/// Despues se sube otra vez con la rueda, para dejar la lista donde estaba y de paso probar
		/// las dos direcciones.
		/// </summary>
		private static void CapturarTrasScroll()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (herramientas == null || panel == null || !herramientas.EditorPrefijo.PopupAbierto) {
				Registrar("Paso 21 - el popup no esta abierto, se salta.");
				return;
			}

			EditorPrefijoTk editor = herramientas.EditorPrefijo;
			List<string> visiblesAhora = editor.FilasVisibles();
			bool cambioLoVisible = !visiblesAhora.SequenceEqual(_visiblesAntes);
			bool seMovieronLasFilas = Math.Abs(editor.PrimeraFilaY - _filaAntes) > 1f;

			Registrar("Paso 21 - " + CapturaDePantalla.Guardar("ws3-prefijo-2-tras-scroll"));
			Registrar("Paso 21 - primera fila de la lista: y=" + _filaAntes.ToString("0.0")
				+ " -> y=" + editor.PrimeraFilaY.ToString("0.0") + " ("
				+ (seMovieronLasFilas ? "OK, el contenido se ha desplazado de verdad"
					: "FALLO, las filas siguen en el mismo sitio") + ").");
			Registrar("Paso 21 - filas visibles ANTES: " + string.Join(" | ", _visiblesAntes));
			Registrar("Paso 21 - filas visibles DESPUES: " + string.Join(" | ", visiblesAhora)
				+ " (" + (cambioLoVisible ? "OK, se ven otras" : "FALLO, se ven las mismas") + ").");

			// Y de vuelta arriba, con la rueda al reves: prueba la otra direccion y deja la lista
			// como estaba para el paso que pulsa un prefijo.
			Vector2 centro = editor.CentroPopup;
			UIElement bajoElRaton = panel.GetElementAt(centro);
			float antesDeSubir = editor.PosicionScroll;
			if (bajoElRaton != null && editor.EsDelPopup(bajoElRaton)) {
				for (int muesca = 0; muesca < 4; muesca++) {
					bajoElRaton.ScrollWheel(new UIScrollWheelEvent(bajoElRaton, centro, 120));
				}
			}
			Registrar("Paso 21 - rueda REAL hacia arriba (4 muescas): ViewPosition "
				+ antesDeSubir.ToString("0.0") + " -> " + editor.PosicionScroll.ToString("0.0") + " ("
				+ (editor.PosicionScroll < antesDeSubir ? "OK, sube tambien" : "FALLO, no sube") + ").");
		}

		private static void CapturarYPulsarPrefijo()
		{
			PanelHerramientasLibreriaTk herramientas = Contenido.Herramientas;
			if (herramientas == null || herramientas.Seleccion.ObjetoActual.IsAir) {
				Registrar("Paso 22 - no hay nada seleccionado en el recuadro (paso 18 fallo), se salta.");
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

			Registrar("Paso 22 - diagnostico de geometria real: " + editor.DiagnosticoGeometria());

			// Captura real con el popup YA ABIERTO y YA DIBUJADO (fotograma de sobra desde el paso
			// 18): pedido explicito del usuario ("el prefijo se abre a la derecha con opciones
			// reales visibles") - la unica forma honesta de demostrarlo es una imagen real del back
			// buffer, no solo el recuento de botones.
			Registrar("Paso 22 - " + CapturaDePantalla.Guardar("ws3-prefijo-3-antes-de-pulsar"));

			// El primero de la lista es siempre "Ninguno" (quitar prefijo); el primer prefijo real
			// legal es el segundo, si lo hay.
			if (botones.Count < 2) {
				Registrar("Paso 22 - el popup se abrio (" + editor.PopupAbierto + ") pero solo trae "
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

			Registrar("Paso 22 - editor de prefijo sobre \"" + objetivo.Name + "\". Boton pulsado: \""
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
				Registrar("Paso 23 - no hay nada seleccionado en el recuadro (paso 15 fallo), se salta.");
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

				Registrar("Paso 23 - papelera de Libreria (PanelHerramientasLibreriaTk.Papelera, "
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

		/// <summary>
		/// Paso 24 - busqueda amplia (mismo termino "es" que ya uso el arnes hermano de Terrakeep-WPF
		/// para su Biblioteca, ver <c>Terrakeep.App.Tests\AuditoriaViewportScroll.cs</c>: 2+ caracteres
		/// para no chocar con la gramatica real que ignora terminos de menos de 2, y muy comun en
		/// nombres en español) para llenar <c>ContenidoLibreria._listaResultados</c> con hasta el tope
		/// real de 100 resultados, y desplazarla al final YA - el desplazamiento visual solo se aplica
		/// en el Draw del fotograma siguiente (UIList.DrawSelf), asi que el volcado real va en el paso
		/// de despues, tras los ~12 fotogramas de espera que ya mete <see cref="Actualizar"/> entre
		/// pasos.
		/// </summary>
		private static void PrepararBusquedaViewport()
		{
			Contenido.IrALaRaiz();
			Contenido.FijarBusqueda("es");
			Contenido.DesplazarResultadosAlFinalParaQA();

			Registrar("Paso 24 - busqueda amplia \"es\" para el volcado de viewport: "
				+ Contenido.TotalCasados + " objetos casan, " + Contenido.SlotsResultado.Count
				+ " enseñados (tope real 100). Rejilla de resultados Y arbol de carpetas desplazados "
				+ "al final (UIList.ViewPosition = float.MaxValue en los dos) - se aplicara en el "
				+ "Draw del fotograma que viene.");
		}

		/// <summary>
		/// Paso 25 - con la rejilla YA dibujada desplazada al final (paso 24, ~12 fotogramas de por
		/// medio), vuelca la geometria real del panel completo con
		/// <see cref="PanelTerrakeepState.VolcarGeometriaJson"/> (ya trae <c>viewportAlto</c> para
		/// cualquier <c>UIList</c> real, extractor ampliado esta misma noche) a la MISMA carpeta que
		/// las capturas de esta autoprueba - mismo patron que
		/// <c>Common\Panel\AutopruebaIdiomas.cs.VolcarGeometriaSiToca</c> - y guarda la captura
		/// correspondiente. El volcado en si NO comprueba nada (solo escribe el JSON): la
		/// verificacion real la hace <c>verificarBordeViewport.js</c> de KeepQA sobre este archivo,
		/// fuera del proceso del juego.
		/// </summary>
		private static void VolcarViewportLibreria()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				Registrar("Paso 25 - PanelTerrakeepState.Panel es null, se omite el volcado.");
				return;
			}

			Registrar("Paso 25 - " + CapturaDePantalla.Guardar("ws3-viewport-libreria-final"));

			try {
				string carpeta = System.IO.Path.Combine(Main.SavePath, CapturaDePantalla.Carpeta);
				System.IO.Directory.CreateDirectory(carpeta);
				string rutaJson = System.IO.Path.Combine(carpeta, "geometria-viewport-libreria.json");
				System.IO.File.WriteAllText(rutaJson, panel.VolcarGeometriaJson());
				Registrar("Paso 25 - geometria (con viewportAlto real de la UIList de resultados) "
					+ "volcada en \"" + System.IO.Path.GetFileName(rutaJson) + "\" ("
					+ System.IO.Path.GetDirectoryName(rutaJson) + ").");
			}
			catch (Exception e) {
				Registrar("Paso 25 - volcado de geometria fallido: " + e.GetType().Name + ": " + e.Message);
			}
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
