using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Personaje.Widgets;
using TerrasavrNative.Core.Data;

namespace TerrakeepMod.UI.Libreria
{
	/// <summary>
	/// <b>Contenido</b> de la Libreria: arbol de carpetas navegable, buscador con la gramatica
	/// real de Terrasavr, rejilla de resultados con el tooltip real del juego, y una franja con
	/// los contenedores REALES del jugador donde soltar lo que se coge.
	/// </summary>
	/// <remarks>
	/// <b>Esta clase es solo el contenido, a proposito.</b> Es un <see cref="UIElement"/> normal
	/// que se estira al 100% de lo que se le de y no sabe nada de como se ha abierto: ni
	/// <c>ModKeybind</c>, ni <c>IngameFancyUI</c>, ni cabecera, ni boton de cerrar. Todo eso vive
	/// en <see cref="PanelLibreriaState"/>, que es lo que se tirara cuando los seis paneles del
	/// mod se fusionen en uno solo con pestañas: entonces bastara con crear un
	/// <c>ContenidoLibreria</c> y colgarlo del contenedor de la pestaña, exactamente igual que
	/// hace hoy <c>PanelPersonajeState</c> con sus <c>PestanaInventario</c>/<c>PestanaEquipo</c>.
	/// <para />
	/// La estetica es deliberadamente la misma que la de los paneles ya construidos: la paleta
	/// sale entera de <see cref="EstiloTk"/> (WS1), los botones son <see cref="BotonTk"/>, los
	/// textos que cambian solos son <see cref="EtiquetaTk"/>, el buscador es el
	/// <see cref="CampoTextoTk"/> que ya usa la cabecera de Personaje, y cada ranura de destino
	/// es el <see cref="SlotObjetoVanilla"/> de WS0 con su contexto vanilla correcto.
	/// </remarks>
	public class ContenidoLibreria : UIElement
	{
		private const float AnchoColumnaCarpetas = 300f;
		private const float SeparacionColumnas = 12f;
		private const float AltoFilaBusqueda = 34f;
		private const float AltoZonaDestino = 264f;
		private const float AnchoBarraScroll = 24f;
		private const float EscalaSlotCatalogo = 0.85f;
		private const float EscalaSlotDestino = 0.7f;
		private const int ColumnasDestino = 10;

		// --- Navegacion -----------------------------------------------------------------------
		private readonly List<CategoryTreeNodeData> _ruta = new List<CategoryTreeNodeData>();
		private string _busqueda = "";

		// --- Elementos ------------------------------------------------------------------------
		private CampoTextoTk _campoBusqueda;
		private BotonTk _botonLimpiar;
		private BotonTk _botonSubir;
		private BotonTk _botonRaiz;
		private EtiquetaTk _rutaTexto;
		private UIList _listaCarpetas;
		private UIScrollbar _scrollCarpetas;
		private EtiquetaTk _resumen;
		private UIList _listaResultados;
		private UIScrollbar _scrollResultados;
		private UIElement _zonaDestino;
		private UIElement _rejillaDestino;
		private readonly List<BotonTk> _botonesDestino = new List<BotonTk>();

		private readonly List<SlotCatalogoLibreria> _slotsResultado = new List<SlotCatalogoLibreria>();

		private int _destinoActual;
		private int _columnasResultado = 15;
		private int _totalCasados;
		private int _mostrados;
		private string _ultimoAviso = "";

		/// <summary>Carpeta abierta ahora mismo, o null si estamos en la raiz.</summary>
		public CategoryTreeNodeData CarpetaActual {
			get { return _ruta.Count > 0 ? _ruta[_ruta.Count - 1] : null; }
		}

		/// <summary>Ruta legible de la carpeta abierta ("Materials > Pre-Hardmode > ...").</summary>
		public string RutaActual {
			get {
				string raiz = Idiomas.Texto("Panel.Area.Libreria");
				if (_ruta.Count == 0) {
					return raiz;
				}
				System.Text.StringBuilder sb = new System.Text.StringBuilder(raiz);
				for (int i = 0; i < _ruta.Count; i++) {
					sb.Append(" > ").Append(_ruta[i].Name);
				}
				return sb.ToString();
			}
		}

		/// <summary>Texto del buscador.</summary>
		public string Busqueda {
			get { return _busqueda; }
		}

		/// <summary>Ranuras de catalogo que se estan enseñando ahora mismo.</summary>
		public IReadOnlyList<SlotCatalogoLibreria> SlotsResultado {
			get { return _slotsResultado; }
		}

		/// <summary>Cuantos objetos casaban en total (antes del tope de 100).</summary>
		public int TotalCasados {
			get { return _totalCasados; }
		}

		/// <summary>Nombre del contenedor de destino seleccionado.</summary>
		public string NombreDestino {
			get { return Destinos[_destinoActual].Nombre; }
		}

		public ContenidoLibreria()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirFilaBusqueda();
			ConstruirColumnaCarpetas();
			ConstruirZonaResultados();
			ConstruirZonaDestino();

			ArbolLibreria.ConstruirSiHaceFalta();
			RellenarCarpetas();
			RellenarResultados();
			MostrarDestino(0);
		}

		// =========================================================================================
		// Construccion de la interfaz
		// =========================================================================================

		private void ConstruirFilaBusqueda()
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(AltoFilaBusqueda, 0f);
			Append(fila);

			_campoBusqueda = new CampoTextoTk(() => Idiomas.Texto("Libreria.PistaBusqueda"), 48, 0.8f);
			_campoBusqueda.Width.Set(AnchoColumnaCarpetas, 0f);
			_campoBusqueda.Height.Set(AltoFilaBusqueda, 0f);
			_campoBusqueda.AlCambiar += texto => {
				_busqueda = texto ?? "";
				RellenarResultados();
			};
			fila.Append(_campoBusqueda);

			_botonLimpiar = new BotonTk(Idiomas.Texto("Libreria.Limpiar"), 0.78f);
			_botonLimpiar.Width.Set(90f, 0f);
			_botonLimpiar.Height.Set(AltoFilaBusqueda, 0f);
			_botonLimpiar.Left.Set(AnchoColumnaCarpetas + 8f, 0f);
			_botonLimpiar.Ayuda = () => Idiomas.Texto("Libreria.LimpiarAyuda");
			_botonLimpiar.AlPulsar += () => FijarBusqueda("");
			fila.Append(_botonLimpiar);

			_resumen = new EtiquetaTk(TextoResumen, 0.75f, 0f, 22f);
			_resumen.ColorTexto = EstiloTk.TextoSuave;
			_resumen.Width.Set(-(AnchoColumnaCarpetas + 8f + 98f), 1f);
			_resumen.Left.Set(AnchoColumnaCarpetas + 8f + 98f, 0f);
			_resumen.Top.Set(7f, 0f);
			fila.Append(_resumen);
		}

		private void ConstruirColumnaCarpetas()
		{
			UIElement columna = new UIElement();
			columna.Width.Set(AnchoColumnaCarpetas, 0f);
			columna.Top.Set(AltoFilaBusqueda + 8f, 0f);
			columna.Height.Set(-(AltoFilaBusqueda + 8f), 1f);
			Append(columna);

			_botonRaiz = new BotonTk(Idiomas.Texto("Libreria.Inicio"), 0.78f);
			_botonRaiz.Width.Set(96f, 0f);
			_botonRaiz.Height.Set(28f, 0f);
			_botonRaiz.Ayuda = () => Idiomas.Texto("Libreria.InicioAyuda");
			_botonRaiz.AlPulsar += IrALaRaiz;
			columna.Append(_botonRaiz);

			_botonSubir = new BotonTk(Idiomas.Texto("Libreria.Subir"), 0.78f);
			_botonSubir.Width.Set(96f, 0f);
			_botonSubir.Height.Set(28f, 0f);
			_botonSubir.Left.Set(102f, 0f);
			_botonSubir.Ayuda = () => Idiomas.Texto("Libreria.SubirAyuda");
			_botonSubir.AlPulsar += Subir;
			columna.Append(_botonSubir);

			_rutaTexto = new EtiquetaTk(() => RutaCorta(), 0.72f, AnchoColumnaCarpetas, 20f);
			_rutaTexto.ColorTexto = EstiloTk.TextoSuave;
			_rutaTexto.Top.Set(32f, 0f);
			columna.Append(_rutaTexto);

			_listaCarpetas = new UIList();
			_listaCarpetas.Width.Set(-AnchoBarraScroll, 1f);
			_listaCarpetas.Top.Set(56f, 0f);
			_listaCarpetas.Height.Set(-56f, 1f);
			_listaCarpetas.ListPadding = 4f;
			columna.Append(_listaCarpetas);

			_scrollCarpetas = new UIScrollbar();
			_scrollCarpetas.Width.Set(20f, 0f);
			_scrollCarpetas.Height.Set(-56f, 1f);
			_scrollCarpetas.Top.Set(56f, 0f);
			_scrollCarpetas.HAlign = 1f;
			_scrollCarpetas.SetView(100f, 1000f);
			columna.Append(_scrollCarpetas);
			_listaCarpetas.SetScrollbar(_scrollCarpetas);
		}

		private void ConstruirZonaResultados()
		{
			// Caja con el mismo fondo secundario que usan las cajas del panel de Personaje: enmarca
			// la rejilla para que no parezca que los objetos flotan sobre el fondo del panel.
			UIPanel zona = new UIPanel();
			zona.Left.Set(AnchoColumnaCarpetas + SeparacionColumnas, 0f);
			zona.Width.Set(-(AnchoColumnaCarpetas + SeparacionColumnas), 1f);
			zona.Top.Set(AltoFilaBusqueda + 8f, 0f);
			zona.Height.Set(-(AltoFilaBusqueda + 8f + AltoZonaDestino + 8f), 1f);
			zona.BackgroundColor = EstiloTk.FondoCaja;
			zona.BorderColor = new Color(0, 0, 0, 0);
			zona.SetPadding(8f);
			Append(zona);

			_listaResultados = new UIList();
			_listaResultados.Width.Set(-AnchoBarraScroll, 1f);
			_listaResultados.Height.Set(0f, 1f);
			_listaResultados.ListPadding = 2f;
			zona.Append(_listaResultados);

			_scrollResultados = new UIScrollbar();
			_scrollResultados.Width.Set(20f, 0f);
			_scrollResultados.Height.Set(0f, 1f);
			_scrollResultados.HAlign = 1f;
			_scrollResultados.SetView(100f, 1000f);
			zona.Append(_scrollResultados);
			_listaResultados.SetScrollbar(_scrollResultados);
		}

		private void ConstruirZonaDestino()
		{
			UIPanel caja = new UIPanel();
			caja.Left.Set(AnchoColumnaCarpetas + SeparacionColumnas, 0f);
			caja.Width.Set(-(AnchoColumnaCarpetas + SeparacionColumnas), 1f);
			caja.Height.Set(AltoZonaDestino, 0f);
			caja.VAlign = 1f;
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(8f);
			Append(caja);

			_zonaDestino = caja;

			// Ancho en PORCENTAJE, no en pixeles: con 118 px fijos, los siete destinos ocupaban
			// 854 px y en una ventana de 800 se salian por la derecha del panel. Se vio en una
			// captura real del juego, no leyendo el codigo.
			float fraccion = 1f / Destinos.Length;
			for (int i = 0; i < Destinos.Length; i++) {
				int indice = i;
				// Etiqueta CORTA en el boton y nombre completo en el tooltip: siete botones se
				// reparten ~420 px en una ventana de 800, o sea 60 px cada uno, y "Monedas y
				// municion" se salia por encima del de al lado (visto en una captura real).
				BotonTk boton = new BotonTk(NombreCortoDestino(i), 0.7f);
				boton.EsPestana = true;
				boton.Width.Set(-4f, fraccion);
				boton.Height.Set(28f, 0f);
				boton.Left.Set(0f, i * fraccion);
				boton.Ayuda = () => Destinos[indice].Nombre;
				boton.AlPulsar += () => MostrarDestino(indice);
				_botonesDestino.Add(boton);
				_zonaDestino.Append(boton);
			}

			EtiquetaTk ayuda = new EtiquetaTk(TextoAyudaDestino, 0.72f, 700f, 20f);
			ayuda.ColorTexto = EstiloTk.TextoAviso;
			ayuda.Top.Set(32f, 0f);
			_zonaDestino.Append(ayuda);

			_rejillaDestino = new UIElement();
			_rejillaDestino.Width.Set(0f, 1f);
			_rejillaDestino.Top.Set(54f, 0f);
			_rejillaDestino.Height.Set(-54f, 1f);
			_zonaDestino.Append(_rejillaDestino);
		}

		// =========================================================================================
		// Navegacion por el arbol
		// =========================================================================================

		/// <summary>Vuelve a las carpetas de primer nivel.</summary>
		public void IrALaRaiz()
		{
			_ruta.Clear();
			RellenarCarpetas();
			RellenarResultados();
		}

		/// <summary>Sube un nivel. No hace nada si ya estamos en la raiz.</summary>
		public void Subir()
		{
			if (_ruta.Count == 0) {
				return;
			}
			_ruta.RemoveAt(_ruta.Count - 1);
			RellenarCarpetas();
			RellenarResultados();
		}

		/// <summary>Entra en una carpeta hija de la que hay abierta (o de la raiz).</summary>
		public void AbrirCarpeta(CategoryTreeNodeData nodo)
		{
			if (nodo == null) {
				return;
			}
			_ruta.Add(nodo);
			RellenarCarpetas();
			RellenarResultados();
		}

		/// <summary>Fija el texto del buscador desde codigo (y actualiza el campo en pantalla).</summary>
		public void FijarBusqueda(string texto)
		{
			_busqueda = texto ?? "";
			if (_campoBusqueda != null) {
				_campoBusqueda.FijarTextoSilencioso(_busqueda);
			}
			RellenarResultados();
		}

		/// <summary>Carpetas que se enseñan al nivel actual.</summary>
		private IReadOnlyList<CategoryTreeNodeData> CarpetasVisibles()
		{
			CategoryTreeNodeData actual = CarpetaActual;
			if (actual == null) {
				return ArbolLibreria.Raices;
			}
			return actual.Children ?? (IReadOnlyList<CategoryTreeNodeData>)new List<CategoryTreeNodeData>();
		}

		private void RellenarCarpetas()
		{
			_listaCarpetas.Clear();

			IReadOnlyList<CategoryTreeNodeData> carpetas = CarpetasVisibles();
			for (int i = 0; i < carpetas.Count; i++) {
				CategoryTreeNodeData nodo = carpetas[i];
				FilaCarpetaTk fila = new FilaCarpetaTk(nodo, AnchoColumnaCarpetas - AnchoBarraScroll - 4f);
				fila.AlPulsar += () => AbrirCarpeta(nodo);
				_listaCarpetas.Add(fila);
			}

			if (carpetas.Count == 0) {
				EtiquetaTk vacio = new EtiquetaTk(
					() => Idiomas.Texto("Libreria.SinSubcarpetas"), 0.75f,
					AnchoColumnaCarpetas - AnchoBarraScroll - 4f, 40f);
				vacio.ColorTexto = EstiloTk.TextoSuave;
				_listaCarpetas.Add(vacio);
			}

			_botonSubir.Habilitado = _ruta.Count > 0;
			_botonRaiz.Habilitado = _ruta.Count > 0;
			_listaCarpetas.Recalculate();
		}

		private void RellenarResultados()
		{
			_listaResultados.Clear();
			_slotsResultado.Clear();

			ResultadoBusqueda resultado = BusquedaLibreria.Buscar(_busqueda, CarpetaActual);
			_totalCasados = resultado.TotalCasados;
			_mostrados = resultado.Mostrados.Count;

			UIElement filaActual = null;
			int enFila = 0;
			float paso = 52f * EscalaSlotCatalogo + 2f;

			for (int i = 0; i < resultado.Mostrados.Count; i++) {
				if (filaActual == null || enFila >= _columnasResultado) {
					filaActual = new UIElement();
					filaActual.Width.Set(0f, 1f);
					filaActual.Height.Set(paso, 0f);
					_listaResultados.Add(filaActual);
					enFila = 0;
				}

				int tipo = resultado.Mostrados[i];
				SlotCatalogoLibreria slot = new SlotCatalogoLibreria(tipo, EscalaSlotCatalogo);
				slot.Left.Set(enFila * paso, 0f);
				slot.AlPedir += PedirObjeto;
				filaActual.Append(slot);
				_slotsResultado.Add(slot);
				enFila++;
			}

			_listaResultados.Recalculate();
		}

		// =========================================================================================
		// Coger un objeto del catalogo
		// =========================================================================================

		/// <summary>
		/// Deja una copia del objeto en el raton para que el jugador la suelte donde quiera.
		/// <para />
		/// Si el raton ya llevaba algo distinto, lo devuelve antes al inventario con
		/// <c>Player.GetItem</c> (la ruta oficial, la misma que usa el juego al recoger del
		/// suelo): asi coger un segundo objeto nunca hace desaparecer el primero. Si llevaba el
		/// MISMO objeto, se acumula hasta su pila maxima, que es lo que espera cualquiera.
		/// </summary>
		public void PedirObjeto(int tipo, bool pilaCompleta)
		{
			if (tipo <= 0 || tipo >= ItemLoader.ItemCount || Main.LocalPlayer == null) {
				return;
			}

			Item nuevo = new Item();
			nuevo.SetDefaults(tipo);
			int cantidad = pilaCompleta ? Math.Max(1, nuevo.maxStack) : 1;

			if (Main.mouseItem != null && !Main.mouseItem.IsAir && Main.mouseItem.type == tipo) {
				int hueco = Main.mouseItem.maxStack - Main.mouseItem.stack;
				int suma = Math.Min(hueco, cantidad);
				Main.mouseItem.stack += suma;
				_ultimoAviso = suma > 0
					? Idiomas.Texto("Libreria.Aviso.Sumado", suma, nuevo.Name, Main.mouseItem.stack)
					: Idiomas.Texto("Libreria.Aviso.AlMaximo", nuevo.Name);
				return;
			}

			if (Main.mouseItem != null && !Main.mouseItem.IsAir) {
				Item sobrante = Main.LocalPlayer.GetItem(Main.myPlayer, Main.mouseItem,
					GetItemSettings.InventoryUIToInventorySettings);
				Main.mouseItem = sobrante;
				if (Main.mouseItem != null && !Main.mouseItem.IsAir) {
					_ultimoAviso = Idiomas.Texto("Libreria.Aviso.NoCabe");
					return;
				}
			}

			nuevo.stack = cantidad;
			Main.mouseItem = nuevo;
			_ultimoAviso = Idiomas.Texto("Libreria.Aviso.Cogido", nuevo.Name, cantidad);

			RegistroLibreria.Linea($"{Terrakeep.LogTag} Libreria: cogido del catalogo \"{nuevo.Name}\" " +
				$"(type={tipo}) x{cantidad}; ahora esta en Main.mouseItem.");
		}

		/// <summary>
		/// Coloca un objeto del catalogo en una ranura CONCRETA del contenedor de destino, por la
		/// ruta real de vanilla (<c>ItemSlot.LeftClick</c> con el objeto puesto en el raton), y lo
		/// deja deshacible con el historial de WS7.
		/// </summary>
		/// <remarks>
		/// Es el camino que recorre un clic de raton de verdad una vez resuelto sobre que ranura
		/// cae. Existe como metodo aparte para que el arnes de pruebas pueda ejercitarlo sin
		/// depender de que haya sesion de escritorio para simular el raton; jugando, lo que se usa
		/// es exactamente el mismo <c>ItemSlot.Handle</c> a traves de
		/// <see cref="SlotObjetoVanilla"/>.
		/// </remarks>
		/// <returns>Descripcion de lo que quedo en la ranura, o null si no se pudo.</returns>
		public string ColocarEnRanura(int tipo, int ranura, bool pilaCompleta)
		{
			DestinoLibreria destino = Destinos[_destinoActual];
			Item[] array = destino.Array();
			if (array == null || ranura < 0 || ranura >= array.Length) {
				return null;
			}

			PedirObjeto(tipo, pilaCompleta);
			if (Main.mouseItem == null || Main.mouseItem.IsAir) {
				return null;
			}

			int contexto = destino.Contexto(ranura);
			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;

			Historial.CambiarObjetos($"Colocar \"{Main.mouseItem.Name}\" en {destino.Nombre}[{ranura}]",
				array, new int[] { ranura }, () => {
					// ItemSlot.LeftClick exige mouseLeftRelease && mouseLeft para considerar que hay
					// una pulsacion NUEVA; se ponen y se restauran alrededor de la llamada.
					Main.mouseLeft = true;
					Main.mouseLeftRelease = true;
					try {
						ItemSlot.LeftClick(array, contexto, ranura);
					}
					finally {
						Main.mouseLeft = izquierdoPrevio;
						Main.mouseLeftRelease = sueltoPrevio;
					}
				});

			Item colocado = array[ranura];
			return colocado == null || colocado.IsAir
				? "(vacio)"
				: $"{colocado.Name} x{colocado.stack} (type={colocado.type})";
		}

		// =========================================================================================
		// Contenedores de destino
		// =========================================================================================

		/// <summary>Un contenedor real del jugador donde se pueden soltar los objetos.</summary>
		private sealed class DestinoLibreria
		{
			public readonly Func<Item[]> Array;
			public readonly int Primero;
			public readonly int Cuantos;
			private readonly Func<int, int> _contexto;

			/// <summary>Clave de localizacion de este destino. Es un nombre interno, no texto que
			/// se enseñe.</summary>
			public readonly string Clave;

			/// <summary>Nombre largo del contenedor, traducido al idioma activo. Es una propiedad y
			/// no un campo porque el array de destinos es estatico y se construye una sola vez: un
			/// nombre guardado ahi se quedaria con el idioma que hubiera al cargar el mod.</summary>
			public string Nombre => Idiomas.Texto("Libreria.Destino." + Clave + ".Largo");

			public DestinoLibreria(string clave, Func<Item[]> array, int primero, int cuantos, Func<int, int> contexto)
			{
				Clave = clave;
				Array = array;
				Primero = primero;
				Cuantos = cuantos;
				_contexto = contexto;
			}

			public int Contexto(int indice)
			{
				return _contexto(indice);
			}
		}

		// El orden es el mismo que en el panel de Personaje (WS1): inventario, luego lo que cuelga
		// de el, luego el equipo, luego los almacenes.
		/// <summary>
		/// Lo que se escribe DENTRO de cada boton de destino. El nombre largo (el de
		/// <see cref="Destinos"/>) sigue siendo el de verdad: se usa en el tooltip, en el titulo de
		/// la zona y en el log. En el mismo orden que <see cref="Destinos"/>.
		/// </summary>
		private static string NombreCortoDestino(int indice)
		{
			return Idiomas.Texto("Libreria.Destino." + Destinos[indice].Clave + ".Corto");
		}

		private static readonly DestinoLibreria[] Destinos = {
			new DestinoLibreria("Inventario", () => Main.LocalPlayer.inventory, 0, 50,
				i => ItemSlot.Context.InventoryItem),
			new DestinoLibreria("Monedas", () => Main.LocalPlayer.inventory, 50, 8,
				i => i < 54 ? ItemSlot.Context.InventoryCoin : ItemSlot.Context.InventoryAmmo),
			new DestinoLibreria("Equipo", () => Main.LocalPlayer.armor, 0, 10,
				i => i < 3 ? ItemSlot.Context.EquipArmor : ItemSlot.Context.EquipAccessory),
			new DestinoLibreria("Hucha", () => Main.LocalPlayer.bank.item, 0, 40,
				i => ItemSlot.Context.BankItem),
			new DestinoLibreria("Caja", () => Main.LocalPlayer.bank2.item, 0, 40,
				i => ItemSlot.Context.BankItem),
			new DestinoLibreria("Forja", () => Main.LocalPlayer.bank3.item, 0, 40,
				i => ItemSlot.Context.BankItem),
			new DestinoLibreria("Boveda", () => Main.LocalPlayer.bank4.item, 0, 40,
				i => ItemSlot.Context.VoidItem)
		};

		/// <summary>Cambia el contenedor de destino que se enseña abajo.</summary>
		public void MostrarDestino(int indice)
		{
			if (indice < 0 || indice >= Destinos.Length) {
				return;
			}

			_destinoActual = indice;
			for (int i = 0; i < _botonesDestino.Count; i++) {
				_botonesDestino[i].Activo = i == indice;
			}

			_rejillaDestino.RemoveAllChildren();

			DestinoLibreria destino = Destinos[indice];
			Item[] array = destino.Array();
			if (array == null) {
				return;
			}

			float paso = 52f * EscalaSlotDestino + 2f;
			for (int k = 0; k < destino.Cuantos; k++) {
				int posicion = destino.Primero + k;
				if (posicion >= array.Length) {
					break;
				}

				SlotObjetoVanilla slot = new SlotObjetoVanilla(array, posicion,
					destino.Contexto(posicion), EscalaSlotDestino);
				slot.Left.Set((k % ColumnasDestino) * paso, 0f);
				slot.Top.Set((k / ColumnasDestino) * paso, 0f);
				_rejillaDestino.Append(slot);
			}

			_rejillaDestino.Recalculate();
		}

		/// <summary>Numero de contenedores de destino disponibles.</summary>
		public int TotalDestinos {
			get { return Destinos.Length; }
		}

		/// <summary>Array vivo del contenedor de destino seleccionado.</summary>
		public Item[] ArrayDestino {
			get { return Destinos[_destinoActual].Array(); }
		}

		// =========================================================================================
		// Textos y ciclo de vida
		// =========================================================================================

		private string TextoResumen()
		{
			if (!ArbolLibreria.Listo) {
				return Idiomas.Texto("Libreria.Cargando");
			}

			string donde = CarpetaActual != null
				? Idiomas.Texto("Libreria.EnCarpeta", CarpetaActual.Name)
				: Idiomas.Texto("Libreria.EnTodo");

			if (_mostrados == 0) {
				// Texto corto a proposito: esta linea va en el hueco que queda a la derecha del
				// buscador, que en una ventana de 800 px son ~340 px.
				return _busqueda.Length > 0
					? Idiomas.Texto("Libreria.SinResultados", donde)
					: Idiomas.Texto("Libreria.ElegirCarpeta", CatalogoVivo.Objetos.Count);
			}

			return _totalCasados > _mostrados
				? Idiomas.Texto("Libreria.MostrandoParcial", _mostrados, _totalCasados, donde)
				: Idiomas.Texto("Libreria.MostrandoTodos", _mostrados, donde);
		}

		private string TextoAyudaDestino()
		{
			if (_ultimoAviso.Length > 0) {
				return _ultimoAviso;
			}
			return Idiomas.Texto("Libreria.AyudaDestino");
		}

		private string RutaCorta()
		{
			string ruta = RutaActual;
			return ruta.Length <= 46 ? ruta : "..." + ruta.Substring(ruta.Length - 43);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// El numero de columnas depende del ancho REAL que le haya tocado al panel (que cambia
			// con la resolucion y con la escala de interfaz del jugador), y ese ancho no se conoce
			// hasta que el motor ha recalculado la disposicion. Se recomprueba aqui y solo se
			// rehace la rejilla si de verdad cambia.
			int columnas = CalcularColumnas();
			if (columnas != _columnasResultado) {
				_columnasResultado = columnas;
				RellenarResultados();
			}

			// Los rotulos de los botones se fijan al construirlos: se vuelven a pedir en cada
			// fotograma para que cambien en vivo con el selector de idioma del area de Ajustes.
			_botonLimpiar.FijarTexto(Idiomas.Texto("Libreria.Limpiar"));
			_botonRaiz.FijarTexto(Idiomas.Texto("Libreria.Inicio"));
			_botonSubir.FijarTexto(Idiomas.Texto("Libreria.Subir"));
			for (int i = 0; i < _botonesDestino.Count; i++) {
				_botonesDestino[i].FijarTexto(NombreCortoDestino(i));
			}
		}

		private int CalcularColumnas()
		{
			CalculatedStyle dim = GetDimensions();
			float disponible = dim.Width - AnchoColumnaCarpetas - SeparacionColumnas - AnchoBarraScroll;
			if (disponible <= 0f) {
				return _columnasResultado;
			}
			int columnas = (int)(disponible / (52f * EscalaSlotCatalogo + 2f));
			return columnas < 1 ? 1 : columnas;
		}

		/// <summary>
		/// Estado real del contenido ya dibujado, para el log de evidencia: cuantas carpetas y
		/// cuantas ranuras hay puestas y donde caen en la pantalla del juego.
		/// </summary>
		public string Informe()
		{
			CalculatedStyle dim = GetDimensions();
			string primera = "sin ranuras";
			if (_slotsResultado.Count > 0) {
				CalculatedStyle d = _slotsResultado[0].GetDimensions();
				primera = $"1ª ranura de catálogo (\"{_slotsResultado[0].Nombre}\") en x={(int)d.X} y={(int)d.Y} " +
					$"{(int)d.Width}x{(int)d.Height}";
			}

			int ranurasDestino = 0;
			_rejillaDestino.ExecuteRecursively(e => {
				if (e is SlotObjetoVanilla) {
					ranurasDestino++;
				}
			});

			return $"área x={(int)dim.X} y={(int)dim.Y} {(int)dim.Width}x{(int)dim.Height}; " +
				$"ruta=\"{RutaActual}\"; carpetas visibles={CarpetasVisibles().Count}; " +
				$"resultados={_slotsResultado.Count} de {_totalCasados} ({_columnasResultado} columnas); " +
				$"{primera}; destino=\"{NombreDestino}\" con {ranurasDestino} ranuras vivas";
		}

		/// <summary>Primera carpeta de este nivel que tenga objetos dentro (la usa el arnes de
		/// pruebas para entrar en una carpeta real sin depender de un nombre concreto).</summary>
		public CategoryTreeNodeData PrimeraCarpetaConObjetos()
		{
			IReadOnlyList<CategoryTreeNodeData> carpetas = CarpetasVisibles();
			for (int i = 0; i < carpetas.Count; i++) {
				if (carpetas[i].ItemIdsOrdered != null && carpetas[i].ItemIdsOrdered.Count > 0) {
					return carpetas[i];
				}
			}
			return null;
		}

		/// <summary>Pulsa de verdad la fila de carpeta que hay en la posicion indicada, disparando
		/// su <c>OnLeftClick</c> real. Devuelve el nombre de la carpeta, o null si no hay.</summary>
		public string PulsarFilaCarpeta(int indice)
		{
			int i = 0;
			foreach (UIElement hijo in _listaCarpetas._items) {
				FilaCarpetaTk fila = hijo as FilaCarpetaTk;
				if (fila == null) {
					continue;
				}
				if (i++ != indice) {
					continue;
				}
				string nombre = fila.Nodo.Name;
				fila.LeftClick(new UIMouseEvent(fila, fila.GetDimensions().Center()));
				return nombre;
			}
			return null;
		}
	}
}
