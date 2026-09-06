using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Ajustes;
using TerrakeepMod.UI.Builds;
using TerrakeepMod.UI.Exploracion;
using TerrakeepMod.UI.Investigacion;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Panel
{
	/// <summary>
	/// <b>El panel de Terrakeep.</b> Uno solo, con una barra de pestañas, en el que viven las seis
	/// areas que antes eran seis paneles sueltos con seis teclas y seis marcos:
	/// Personaje · Librería · Builds · Investigación · Exploración · Ajustes (el mismo orden que
	/// la aplicacion de escritorio hermana, por familiaridad).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Cada pestaña monta el <c>UIElement</c> de contenido que ya construyo su workstream. Todo lo
	/// que era mecanica de apertura (marco, cabecera, boton de cerrar, dibujado del objeto cogido
	/// con el raton, mantener el inventario abierto) estaba repetido seis veces con seis copias
	/// casi identicas del mismo codigo; ahora esta aqui una sola vez.
	/// </para>
	/// <para>
	/// Los dos fallos reales del motor que encontro WS1 con <c>IngameFancyUI</c> siguen resueltos,
	/// y ahora en un unico sitio: <see cref="MantenerInventarioAbierto"/> (si no,
	/// <c>Player.dropItemCheck</c> vacia cada tick el objeto que se lleva cogido) y
	/// <see cref="DibujarObjetoEnRaton"/> (la capa 38 de vanilla nunca corre con un panel de
	/// <c>IngameFancyUI</c> abierto).
	/// </para>
	/// </remarks>
	public class PanelTerrakeepState : UIState
	{
		private const float AnchoMaximo = 1080f;
		private const float AltoMaximo = 700f;
		private const float AltoPie = 44f;

		/// <summary>
		/// Alto de la fila del titulo, que ademas hace de <b>hueco para el HUD del juego</b>.
		/// <para />
		/// No es decoracion: con un panel de <c>IngameFancyUI</c> abierto, el juego <b>sigue
		/// dibujando las barras de vida y mana ENCIMA</b>, y lo hace a proposito. El codigo real de
		/// <c>IngameFancyUI.Draw</c> (tModLoader.dll instalado) llama a
		/// <c>Main.instance.GUIBarsDraw()</c> despues de que <c>InGameUI.Draw</c> haya pintado el
		/// panel del mod, asi que los corazones quedan por encima. Se vio en una captura real del
		/// juego: la barra de pestañas estaba justo debajo de los corazones y estos tapaban el
		/// texto de la pestaña "Exploración". Dejando esta fila arriba, las pestañas caen ya por
		/// debajo del HUD, y el hueco se aprovecha para el titulo (que va a la IZQUIERDA, que es
		/// donde el HUD no pinta nada).
		/// </summary>
		private const float AltoTitulo = 30f;

		private const float AltoBarraPestanas = EstiloTk.AltoPestana;

		private UIPanel _marco;
		private UIElement _contenedor;
		private readonly List<BotonTk> _botonesPestana = new List<BotonTk>();
		private BotonTk _botonCerrar;
		private UIElement _contenidoActual;
		private AreaTerrakeep _area;

		private bool _medidasRegistradas;

		/// <summary>Pestaña abierta la ultima vez. Reabrir el panel vuelve a donde estabas.</summary>
		public static AreaTerrakeep UltimaArea = AreaTerrakeep.Personaje;

		/// <summary>Fotogramas en los que se ha llegado a dibujar el objeto cogido con el raton.
		/// Lo leen las autopruebas para demostrar que esa ruta de dibujado se ejecuta de verdad.
		/// Antes habia un contador por panel; ahora hay uno, porque hay un dibujado.</summary>
		public static int FotogramasObjetoEnRaton;

		/// <summary>Area abierta ahora mismo.</summary>
		public AreaTerrakeep AreaActual => _area;

		/// <summary>El contenido montado en la pestaña abierta, sea del tipo que sea.</summary>
		public UIElement ContenidoActual => _contenidoActual;

		public ContenidoPersonaje Personaje => _contenidoActual as ContenidoPersonaje;
		public ContenidoLibreria Libreria => _contenidoActual as ContenidoLibreria;
		public ContenidoBuilds Builds => _contenidoActual as ContenidoBuilds;
		public ContenidoInvestigacion Investigacion => _contenidoActual as ContenidoInvestigacion;
		public ContenidoExploracion Exploracion => _contenidoActual as ContenidoExploracion;
		public ContenidoAjustes Ajustes => _contenidoActual as ContenidoAjustes;

		public override void OnInitialize()
		{
			_marco = new UIPanel();
			_marco.Width.Set(0f, 0.96f);
			_marco.MaxWidth.Set(AnchoMaximo, 0f);
			_marco.Height.Set(0f, 0.94f);
			_marco.MaxHeight.Set(AltoMaximo, 0f);
			_marco.HAlign = 0.5f;
			_marco.VAlign = 0.5f;
			_marco.BackgroundColor = EstiloTk.FondoPanel;
			_marco.SetPadding(10f);
			Append(_marco);

			ConstruirTitulo();
			ConstruirBarraPestanas();

			float arribaContenido = AltoTitulo + AltoBarraPestanas + 8f;
			_contenedor = new UIElement();
			_contenedor.Width.Set(0f, 1f);
			_contenedor.Top.Set(arribaContenido, 0f);
			_contenedor.Height.Set(-(arribaContenido + AltoPie), 1f);
			_marco.Append(_contenedor);

			ConstruirPie();

			CambiarArea(UltimaArea, "reapertura");
		}

		/// <summary>La fila del titulo. El texto va pegado a la izquierda a proposito: la derecha de
		/// esa franja la ocupa el HUD de vida/mana del propio juego (ver <see cref="AltoTitulo"/>).</summary>
		private void ConstruirTitulo()
		{
			EtiquetaTk titulo = new EtiquetaTk(() => "Terrakeep", 1.15f, 300f, AltoTitulo);
			titulo.Left.Set(2f, 0f);
			titulo.Top.Set(0f, 0f);
			_marco.Append(titulo);
		}

		private void ConstruirBarraPestanas()
		{
			string[] nombres = NombresDeArea;
			float fraccion = 1f / nombres.Length;

			for (int i = 0; i < nombres.Length; i++) {
				AreaTerrakeep area = (AreaTerrakeep)i;
				BotonTk boton = new BotonTk(nombres[i], EstiloTk.EscalaPestana);
				boton.EsPestana = true;
				boton.Width.Set(-EstiloTk.SeparacionPestanas, fraccion);
				boton.Height.Set(AltoBarraPestanas, 0f);
				boton.Left.Set(0f, i * fraccion);
				boton.Top.Set(AltoTitulo, 0f);
				boton.Ayuda = AyudaDeArea(area) + "\nAtajo: " + PanelTerrakeepSystem.TeclaDe(area);
				boton.AlPulsar += () => CambiarArea(area, "clic en la pestaña");
				_botonesPestana.Add(boton);
				_marco.Append(boton);
			}
		}

		private void ConstruirPie()
		{
			// Sin repetir "Terrakeep": ya esta arriba, en la fila del titulo.
			EtiquetaTk ayuda = new EtiquetaTk(() => AyudaDeArea(_area), 0.75f, 820f, 22f);
			ayuda.ColorTexto = EstiloTk.TextoSuave;
			ayuda.Left.Set(2f, 0f);
			ayuda.VAlign = 1f;
			_marco.Append(ayuda);

			_botonCerrar = new BotonTk("", EstiloTk.EscalaBoton);
			_botonCerrar.Width.Set(160f, 0f);
			_botonCerrar.Height.Set(34f, 0f);
			_botonCerrar.HAlign = 1f;
			_botonCerrar.VAlign = 1f;
			_botonCerrar.AlPulsar += () => PanelTerrakeepSystem.CerrarPanel("botón Cerrar");
			_marco.Append(_botonCerrar);
		}

		/// <summary>Nombres de las seis pestañas, en el orden de la aplicacion de escritorio.</summary>
		public static readonly string[] NombresDeArea = {
			"Personaje", "Librería", "Builds", "Investigación", "Exploración", "Ajustes"
		};

		/// <summary>La linea de ayuda que se ve en el pie con cada area abierta. Es donde han ido a
		/// parar las notas que antes repetia cada panel dentro de su propia cabecera.</summary>
		public static string AyudaDeArea(AreaTerrakeep area)
		{
			switch (area) {
				case AreaTerrakeep.Personaje:
					return "Todo lo que toques aquí se escribe al instante sobre el personaje cargado.";
				case AreaTerrakeep.Libreria:
					return "Coge objetos del catálogo con el ratón y suéltalos en cualquier contenedor real.";
				case AreaTerrakeep.Builds:
					return "Auto-equipar solo MUEVE lo que ya tienes: nunca crea objetos.";
				case AreaTerrakeep.Investigacion:
					return "Se escribe por la vía oficial del Modo Viaje, así que el menú del juego refleja lo mismo.";
				case AreaTerrakeep.Exploracion:
					return "Arrastra el mini-mapa para moverte y usa la rueda para acercar.";
				case AreaTerrakeep.Ajustes:
					return "El idioma cambia en vivo. Ctrl+Z deshace cualquier acción de Terrakeep.";
				default:
					return "";
			}
		}

		/// <summary>Cambia de pestaña. Publico: lo usan la barra, los atajos directos y las
		/// autopruebas.</summary>
		public void CambiarArea(AreaTerrakeep area, string origen)
		{
			int indice = (int)area;
			if (indice < 0 || indice >= NombresDeArea.Length) {
				return;
			}

			_area = area;
			UltimaArea = area;

			for (int i = 0; i < _botonesPestana.Count; i++) {
				_botonesPestana[i].Activo = i == indice;
			}

			_botonCerrar.FijarTexto("Cerrar (" + PanelTerrakeepSystem.TeclaDe(area) + ")");

			if (_contenidoActual != null) {
				_contenedor.RemoveChild(_contenidoActual);
				_contenidoActual = null;
			}

			// Cada area se construye de cero al entrar en ella, igual que hacian los seis paneles
			// sueltos al abrirse: es la unica forma de garantizar que ninguna referencia se quede
			// apuntando a los arrays de otra partida (el juego reasigna Player.inventory y
			// compañia al cargar otro personaje).
			_contenidoActual = CrearContenido(area);
			if (_contenidoActual != null) {
				_contenidoActual.Width.Set(0f, 1f);
				_contenidoActual.Height.Set(0f, 1f);
				_contenedor.Append(_contenidoActual);
			}

			_contenedor.Recalculate();

			PanelTerrakeepSystem.RegistrarEnArea(area,
				Terrakeep.LogTag + " Pestaña activa: \"" + NombresDeArea[indice] + "\" (via " + origen + ").");
		}

		private static UIElement CrearContenido(AreaTerrakeep area)
		{
			switch (area) {
				case AreaTerrakeep.Personaje:
					return new ContenidoPersonaje();
				case AreaTerrakeep.Libreria:
					// El catalogo se construye la primera vez que hace falta de verdad, no al cargar
					// el mod: quien no abra la Libreria no paga el recorrido de los ~8000 objetos.
					ArbolLibreria.ConstruirSiHaceFalta();
					return new ContenidoLibreria();
				case AreaTerrakeep.Builds:
					return new ContenidoBuilds();
				case AreaTerrakeep.Investigacion:
					return new ContenidoInvestigacion();
				case AreaTerrakeep.Exploracion:
					return new ContenidoExploracion();
				case AreaTerrakeep.Ajustes:
					return new ContenidoAjustes();
				default:
					return null;
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			MantenerInventarioAbierto();

			// Si el personaje desaparece (muerte con partida en modo extremo, salir al menu...) el
			// panel no tiene nada que editar y se cierra solo antes de petar leyendo nulos.
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				PanelTerrakeepSystem.CerrarPanel("el jugador ha dejado de estar disponible");
			}
		}

		/// <summary>
		/// Deja <c>Main.playerInventory</c> a true mientras el panel esta abierto.
		/// <para />
		/// Suena raro (el panel es a pantalla completa y tapa el inventario) pero es
		/// IMPRESCINDIBLE, y es un fallo real que se vio en el juego construyendo WS1, no una
		/// precaucion teorica: <c>IngameFancyUI.OpenUIState</c> pone
		/// <c>Main.playerInventory = false</c>, y <c>Player.dropItemCheck</c> (que corre cada tick)
		/// tiene esto:
		/// <code>
		/// if (Main.mouseItem.type > 0 &amp;&amp; !Main.playerInventory) {
		///     ... GetItem de vuelta al inventario, y al suelo lo que no quepa ...
		///     Main.mouseItem = new Item();
		/// }
		/// </code>
		/// O sea: con el panel abierto tal cual, cualquier objeto que cojas de una ranura se te
		/// devuelve solo al inventario (o cae al suelo si esta lleno) en el tick siguiente, y
		/// arrastrar de una ranura a otra es literalmente imposible.
		/// <para />
		/// Poner el campo a true no dibuja el inventario de vanilla: la capa
		/// <c>"Vanilla: Inventory"</c> es la 27 y la lista de capas se corta en la 12
		/// (<c>"Vanilla: Fancy UI"</c>). Ademas <c>IngameFancyUI.Close()</c> ya lo deja a true al
		/// cerrar por su cuenta, asi que no hay nada que restaurar.
		/// </summary>
		public static void MantenerInventarioAbierto()
		{
			Main.playerInventory = true;
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);

			DibujarObjetoEnRaton(spriteBatch);
			RegistrarMedidasUnaVez();
		}

		/// <summary>
		/// Dibuja el objeto que el jugador lleva "cogido" con el raton.
		/// <para />
		/// Hace falta hacerlo a mano por un detalle real del motor: la interfaz del juego es una
		/// lista de capas que se recorre hasta que una devuelve false, y la capa
		/// <c>"Vanilla: Fancy UI"</c> (indice 12) devuelve false SIEMPRE que hay un panel abierto
		/// con <c>IngameFancyUI</c>. La capa que dibuja el objeto cogido,
		/// <c>DrawInterface_38_MouseCarriedObject</c>, va despues (indice 38), asi que nunca llega
		/// a ejecutarse. Sin esto, coger un objeto de una ranura lo haria desaparecer de la vista
		/// hasta soltarlo. El codigo es el mismo que el de esa capa vanilla, incluido el cambio
		/// temporal de <c>Main.inventoryScale</c> a <c>Main.cursorScale</c>.
		/// </summary>
		private static void DibujarObjetoEnRaton(SpriteBatch spriteBatch)
		{
			if (Main.mouseItem == null || Main.mouseItem.type <= 0 || Main.mouseItem.stack <= 0) {
				return;
			}

			float escalaPrevia = Main.inventoryScale;
			Main.inventoryScale = Main.cursorScale;
			ItemSlot.Draw(spriteBatch, ref Main.mouseItem, ItemSlot.Context.MouseItem,
				new Vector2(Main.mouseX, Main.mouseY));
			Main.inventoryScale = escalaPrevia;

			FotogramasObjetoEnRaton++;
		}

		/// <summary>Si al cerrar el panel queda un objeto cogido con el raton, se devuelve al
		/// inventario. Fuera del panel ese objeto no se dibuja en ningun sitio, asi que dejarlo
		/// ahi seria perderlo de vista.</summary>
		public static void DevolverObjetoDelRaton()
		{
			if (Main.mouseItem == null || Main.mouseItem.IsAir) {
				return;
			}

			// Hay que describirlo ANTES: Player.GetItem consume el objeto que se le pasa (le deja
			// la pila a cero y devuelve lo que no ha cabido), asi que despues ya seria "(vacio)".
			string descripcion = Common.Personaje.PersonajeVivo.DescribirObjeto(Main.mouseItem);

			Item sobrante = Main.LocalPlayer.GetItem(Main.myPlayer, Main.mouseItem,
				GetItemSettings.InventoryUIToInventorySettings);
			Main.mouseItem = sobrante;

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Al cerrar habia un objeto cogido con el raton ({descripcion}); " +
				$"devuelto al inventario. Sobrante: {Common.Personaje.PersonajeVivo.DescribirObjeto(sobrante)}.");
		}

		/// <summary>Deja en el log, una sola vez por apertura, las medidas reales del panel ya
		/// dibujado. Es la evidencia de que esta puesto de verdad en pantalla y no solo construido
		/// en memoria - mismo criterio con el que se cerraron todos los workstreams.</summary>
		private void RegistrarMedidasUnaVez()
		{
			if (_medidasRegistradas || _marco == null) {
				return;
			}

			CalculatedStyle dim = _marco.GetDimensions();
			if (dim.Width <= 0f) {
				return;
			}

			_medidasRegistradas = true;
			PanelTerrakeepSystem.RegistrarEnArea(_area,
				$"{Terrakeep.LogTag} Panel de Terrakeep dibujado. Marco en coordenadas de pantalla: " +
				$"x={(int)dim.X} y={(int)dim.Y} w={(int)dim.Width} h={(int)dim.Height}. " +
				$"Resolucion {Main.screenWidth}x{Main.screenHeight}, escala de interfaz {Main.UIScale}. " +
				$"Pestaña abierta: \"{NombresDeArea[(int)_area]}\". " +
				$"Barra de pestañas: {DescribirBarra()}");
		}

		private string DescribirBarra()
		{
			System.Text.StringBuilder texto = new System.Text.StringBuilder();
			for (int i = 0; i < _botonesPestana.Count; i++) {
				CalculatedStyle d = _botonesPestana[i].GetDimensions();
				texto.Append($"[\"{_botonesPestana[i].Texto}\" x={(int)d.X} y={(int)d.Y} " +
					$"{(int)d.Width}x{(int)d.Height}{(_botonesPestana[i].Activo ? " ACTIVA" : "")}] ");
			}
			return texto.ToString();
		}

		/// <summary>Primer elemento del panel del tipo pedido, buscando por todo el arbol de la
		/// interfaz. Lo usan las autopruebas para llegar a los controles propios sin tener que
		/// exponerlos uno a uno.</summary>
		public T BuscarPrimero<T>() where T : UIElement
		{
			T encontrado = null;
			ExecuteRecursively(elemento => {
				if (encontrado == null && elemento is T) {
					encontrado = (T)elemento;
				}
			});
			return encontrado;
		}

		/// <summary>
		/// Pulsa DE VERDAD el boton cuyo texto empiece por <paramref name="texto"/>, disparando su
		/// <c>OnLeftClick</c> con un <see cref="UIMouseEvent"/> colocado en su centro real de
		/// pantalla: el mismo camino que recorre un clic de raton una vez que
		/// <c>UserInterface</c> ha resuelto sobre que elemento cae.
		/// </summary>
		public string PulsarBoton(string texto)
		{
			BotonTk encontrado = null;
			ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (encontrado == null && boton != null && boton.Texto != null && boton.Texto.StartsWith(texto)) {
					encontrado = boton;
				}
			});

			if (encontrado == null) {
				return null;
			}

			CalculatedStyle dim = encontrado.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			encontrado.LeftClick(new UIMouseEvent(encontrado, centro));

			return "\"" + encontrado.Texto + "\" (habilitado=" + encontrado.Habilitado +
				", en x=" + (int)dim.X + " y=" + (int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height +
				", clic en " + (int)centro.X + "," + (int)centro.Y + ")";
		}

		/// <summary>
		/// Pulsa de verdad la pestaña indicada de la barra principal, por la ruta real del clic.
		/// La usa la verificacion final para demostrar que las seis pestañas se cambian con el
		/// raton y no solo llamando al metodo por dentro.
		/// </summary>
		public string PulsarPestana(AreaTerrakeep area)
		{
			int indice = (int)area;
			if (indice < 0 || indice >= _botonesPestana.Count) {
				return null;
			}

			BotonTk boton = _botonesPestana[indice];
			CalculatedStyle dim = boton.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			boton.LeftClick(new UIMouseEvent(boton, centro));

			return "\"" + boton.Texto + "\" (en x=" + (int)dim.X + " y=" + (int)dim.Y + " " +
				(int)dim.Width + "x" + (int)dim.Height + ", clic en " + (int)centro.X + "," + (int)centro.Y + ")";
		}

		/// <summary>Recuento y medidas REALES de lo que hay montado en la pestaña abierta, ya
		/// recalculado. Evidencia de que el area ocupa sitio de verdad en pantalla.</summary>
		public string InformeAreaActual()
		{
			if (_contenidoActual == null) {
				return "sin contenido";
			}

			int elementos = 0;
			int ranuras = 0;
			int botones = 0;
			_contenidoActual.ExecuteRecursively(elemento => {
				elementos++;
				if (elemento is SlotObjetoVanilla) {
					ranuras++;
				}
				if (elemento is BotonTk) {
					botones++;
				}
			});

			CalculatedStyle dim = _contenidoActual.GetDimensions();
			return _contenidoActual.GetType().Name + ": " + elementos + " elementos, " + ranuras +
				" ranuras de objeto, " + botones + " botones; area x=" + (int)dim.X + " y=" + (int)dim.Y +
				" " + (int)dim.Width + "x" + (int)dim.Height;
		}
	}
}
