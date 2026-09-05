using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Panel de Personaje (WS1): edicion EN VIVO de <see cref="Main.LocalPlayer"/>.
	/// <para />
	/// No hay ningun archivo de por medio ni ninguna copia intermedia: cada ranura, cada casilla
	/// y cada deslizador apunta al campo real del jugador que hay cargado en la partida, asi que
	/// lo que se toca aqui se ve al instante en el juego (y al reves: si el juego cambia algo, el
	/// panel lo enseña sin tener que reabrirlo).
	/// </summary>
	public class PanelPersonajeState : UIState
	{
		private const float AnchoMaximo = 1080f;
		private const float AltoMaximo = 700f;
		private const float AltoCabecera = 92f;
		private const float AltoBarraPestanas = 34f;
		private const float AltoPie = 42f;

		private UIPanel _marco;
		private UIElement _contenedor;
		private readonly List<BotonTk> _botonesPestana = new List<BotonTk>();
		private readonly List<string> _nombresPestana = new List<string>();
		private UIElement _pestanaActual;
		private int _indicePestana;

		private bool _medidasRegistradas;

		/// <summary>Indice de la pestaña abierta ahora mismo. Se guarda entre aperturas para que
		/// reabrir el panel vuelva a donde estabas.</summary>
		public static int UltimaPestana;

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

			_marco.Append(new CabeceraPersonaje());

			ConstruirBarraPestanas();

			_contenedor = new UIElement();
			_contenedor.Width.Set(0f, 1f);
			_contenedor.Top.Set(AltoCabecera + AltoBarraPestanas + 8f, 0f);
			_contenedor.Height.Set(-(AltoCabecera + AltoBarraPestanas + 8f + AltoPie), 1f);
			_marco.Append(_contenedor);

			BotonTk cerrar = new BotonTk("Cerrar (K)", 0.85f);
			cerrar.Width.Set(150f, 0f);
			cerrar.Height.Set(34f, 0f);
			cerrar.HAlign = 1f;
			cerrar.VAlign = 1f;
			cerrar.AlPulsar += () => PanelPruebaSystem.CerrarPanel("boton Cerrar");
			_marco.Append(cerrar);

			CambiarPestana(PersonajeVivo.Acotar(UltimaPestana, 0, _nombresPestana.Count - 1));
		}

		private void ConstruirBarraPestanas()
		{
			_nombresPestana.Add("Inventario");
			_nombresPestana.Add("Almacenes");
			_nombresPestana.Add("Equipo");
			_nombresPestana.Add("Buffs");
			_nombresPestana.Add("Apariencia");
			_nombresPestana.Add("Desbloqueos");

			float ancho = 150f;
			float separacion = 6f;

			for (int i = 0; i < _nombresPestana.Count; i++) {
				int indice = i;
				BotonTk boton = new BotonTk(_nombresPestana[i], 0.85f);
				boton.Width.Set(ancho, 0f);
				boton.Height.Set(AltoBarraPestanas, 0f);
				boton.Left.Set(i * (ancho + separacion), 0f);
				boton.Top.Set(AltoCabecera, 0f);
				boton.AlPulsar += () => CambiarPestana(indice);
				_botonesPestana.Add(boton);
				_marco.Append(boton);
			}
		}

		private void CambiarPestana(int indice)
		{
			if (indice < 0 || indice >= _nombresPestana.Count) {
				return;
			}

			_indicePestana = indice;
			UltimaPestana = indice;

			for (int i = 0; i < _botonesPestana.Count; i++) {
				_botonesPestana[i].Activo = i == indice;
			}

			if (_pestanaActual != null) {
				_contenedor.RemoveChild(_pestanaActual);
				_pestanaActual = null;
			}

			// Cada pestaña se construye de cero al entrar en ella, igual que el propio panel se
			// construye de cero en cada apertura: es la unica forma de garantizar que ninguna
			// referencia se quede apuntando a un array viejo del jugador.
			_pestanaActual = CrearPestana(indice);
			if (_pestanaActual != null) {
				_pestanaActual.Width.Set(0f, 1f);
				_pestanaActual.Height.Set(0f, 1f);
				_contenedor.Append(_pestanaActual);
			}

			_contenedor.Recalculate();

			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Pestaña activa: \"{_nombresPestana[indice]}\".");
		}

		private static UIElement CrearPestana(int indice)
		{
			switch (indice) {
				case 0: return new PestanaInventario();
				case 1: return new PestanaAlmacenes();
				case 2: return new PestanaEquipo();
				case 3: return new PestanaBuffs();
				case 4: return new PestanaApariencia();
				case 5: return new PestanaDesbloqueos();
				default: return null;
			}
		}

		/// <summary>Nombre de la pestaña abierta, para el log de las pruebas.</summary>
		public string NombrePestanaActual =>
			_indicePestana >= 0 && _indicePestana < _nombresPestana.Count
				? _nombresPestana[_indicePestana]
				: "(ninguna)";

		/// <summary>Numero de pestañas. Lo usa la autoprueba para recorrerlas todas.</summary>
		public int TotalPestanas => _nombresPestana.Count;

		/// <summary>Cambia de pestaña desde fuera (autoprueba).</summary>
		public void IrAPestana(int indice)
		{
			CambiarPestana(indice);
		}

		/// <summary>
		/// Recuento y medidas REALES de lo que hay puesto en la pestaña abierta, ya recalculado.
		/// Es la evidencia de que la pestaña no solo se ha construido en memoria sino que ocupa
		/// sitio de verdad en la pantalla del juego.
		/// </summary>
		public string InformePestanaActual()
		{
			if (_pestanaActual == null) {
				return "sin pestaña";
			}

			int elementos = 0;
			int ranuras = 0;
			CalculatedStyle primera = new CalculatedStyle();
			CalculatedStyle ultima = new CalculatedStyle();

			_pestanaActual.ExecuteRecursively(elemento => {
				elementos++;
				if (elemento is SlotObjetoVanilla) {
					CalculatedStyle dim = elemento.GetDimensions();
					if (ranuras == 0) {
						primera = dim;
					}
					ultima = dim;
					ranuras++;
				}
			});

			CalculatedStyle marcoPestana = _pestanaActual.GetDimensions();
			string detalleRanuras = ranuras == 0
				? "sin ranuras de objeto"
				: ranuras + " ranuras de objeto, la 1ª en x=" + (int)primera.X + " y=" + (int)primera.Y
					+ " " + (int)primera.Width + "x" + (int)primera.Height
					+ ", la ultima en x=" + (int)ultima.X + " y=" + (int)ultima.Y;

			return elementos + " elementos, " + detalleRanuras
				+ "; area de la pestaña x=" + (int)marcoPestana.X + " y=" + (int)marcoPestana.Y
				+ " " + (int)marcoPestana.Width + "x" + (int)marcoPestana.Height;
		}

		/// <summary>Fotogramas en los que se ha llegado a dibujar el objeto cogido con el raton.
		/// Lo lee la autoprueba para demostrar que esa ruta de dibujado se ejecuta de verdad.</summary>
		public static int FotogramasObjetoEnRaton;

		/// <summary>Primer elemento del panel del tipo pedido, buscando por todo el arbol de la
		/// interfaz. Lo usa la autoprueba para llegar a los controles propios (deslizadores,
		/// campos de texto) sin tener que exponerlos uno a uno.</summary>
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
		/// Deja <c>Main.playerInventory</c> a true mientras el panel esta abierto.
		/// <para />
		/// Suena raro (el panel es a pantalla completa y tapa el inventario) pero es
		/// IMPRESCINDIBLE, y es un fallo real que se vio en el juego, no una precaucion teorica:
		/// <c>IngameFancyUI.OpenUIState</c> pone <c>Main.playerInventory = false</c>, y
		/// <c>Player.dropItemCheck</c> (que corre cada tick) tiene esto:
		/// <code>
		/// if (Main.mouseItem.type > 0 &amp;&amp; !Main.playerInventory) {
		///     ... GetItem de vuelta al inventario, y al suelo lo que no quepa ...
		///     Main.mouseItem = new Item();
		/// }
		/// </code>
		/// O sea: con el panel abierto tal cual, cualquier objeto que cojas de una ranura se te
		/// devuelve solo al inventario (o cae al suelo si esta lleno) en el tick siguiente, y
		/// arrastrar de una ranura a otra es literalmente imposible. En la autoprueba se vio como
		/// un objeto puesto en el raton aparecia vacio 10 fotogramas despues.
		/// <para />
		/// Poner el campo a true no dibuja el inventario de vanilla: la capa
		/// <c>"Vanilla: Inventory"</c> es la 27 y la lista de capas se corta en la 12
		/// (<c>"Vanilla: Fancy UI"</c>). Los unicos efectos secundarios reales son que se llama a
		/// <c>Player.AdjTiles()</c> cada tick y que se desactiva el cambio de objeto de la barra
		/// rapida con la rueda, dos cosas deseables con un editor abierto. Ademas
		/// <c>IngameFancyUI.Close()</c> ya lo deja a true al cerrar por su cuenta, asi que no hay
		/// nada que restaurar.
		/// </summary>
		public static void MantenerInventarioAbierto()
		{
			Main.playerInventory = true;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			MantenerInventarioAbierto();

			// Si el personaje desaparece (muerte con partida en modo extremo, salir al menu...)
			// el panel no tiene nada que editar y se cierra solo antes de petar leyendo nulos.
			if (!PersonajeVivo.HayJugador) {
				PanelPruebaSystem.CerrarPanel("el jugador ha dejado de estar disponible");
			}
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
		/// <c>DrawInterface_38_MouseCarriedObject</c>, va despues (indice 38), asi que nunca
		/// llega a ejecutarse. Sin esto, coger un objeto de una ranura lo haria desaparecer de la
		/// vista hasta soltarlo: parece que se ha perdido, y es justo lo que no puede pasar en un
		/// editor de inventario. El codigo es el mismo que el de esa capa vanilla, incluido el
		/// cambio temporal de <c>Main.inventoryScale</c> a <c>Main.cursorScale</c>.
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

		/// <summary>Deja en el log, una sola vez por apertura, las medidas reales del panel ya
		/// dibujado. Es la evidencia de que esta puesto de verdad en pantalla y no solo
		/// construido en memoria.</summary>
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
			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Panel de Personaje dibujado. Marco en coordenadas de pantalla: " +
				$"x={(int)dim.X} y={(int)dim.Y} w={(int)dim.Width} h={(int)dim.Height}. " +
				$"Resolucion {Main.screenWidth}x{Main.screenHeight}, escala de interfaz {Main.UIScale}.");
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
			string descripcion = PersonajeVivo.DescribirObjeto(Main.mouseItem);

			Item sobrante = Main.LocalPlayer.GetItem(Main.myPlayer, Main.mouseItem,
				GetItemSettings.InventoryUIToInventorySettings);
			Main.mouseItem = sobrante;

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Al cerrar habia un objeto cogido con el raton ({descripcion}); " +
				$"devuelto al inventario. Sobrante: {PersonajeVivo.DescribirObjeto(sobrante)}.");
		}
	}
}
