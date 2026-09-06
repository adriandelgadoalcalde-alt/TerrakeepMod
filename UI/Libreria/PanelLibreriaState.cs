using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Libreria
{
	/// <summary>
	/// Panel de Libreria (WS3, tecla <b>O</b>): el marco, la cabecera y el boton de cerrar. Todo
	/// lo que se ve DENTRO lo pone <see cref="ContenidoLibreria"/>.
	/// </summary>
	/// <remarks>
	/// <b>Esta clase es la MECANICA DE APERTURA, y esta separada a proposito.</b> Cuando los seis
	/// paneles del mod se fusionen en uno solo con pestañas y una sola tecla, esta clase y
	/// <see cref="Common.Libreria.PanelLibreriaSystem"/> se tiran enteras, y el
	/// <c>ContenidoLibreria</c> se cuelga tal cual del contenedor de la pestaña: no hay ni una
	/// linea de la Libreria de verdad aqui dentro que haya que reescribir.
	/// <para />
	/// El marco copia las medidas y la paleta del panel de Personaje (WS1) para que los dos se
	/// vean como la misma aplicacion: mismo <c>UIPanel</c> centrado al 96%/94% con tope de
	/// 1080x700, mismo <see cref="EstiloTk.FondoPanel"/>, mismo relleno de 10, misma cabecera de
	/// ~90 px y mismo boton "Cerrar (tecla)" abajo a la derecha.
	/// </remarks>
	public class PanelLibreriaState : UIState
	{
		private const float AnchoMaximo = 1080f;
		private const float AltoMaximo = 700f;
		private const float AltoCabecera = 62f;
		private const float AltoPie = 42f;

		private UIPanel _marco;
		private ContenidoLibreria _contenido;
		private bool _medidasRegistradas;

		/// <summary>El contenido de la Libreria. Lo usa el arnes de pruebas.</summary>
		public ContenidoLibreria Contenido {
			get { return _contenido; }
		}

		/// <summary>Fotogramas en los que se ha llegado a dibujar el objeto cogido con el raton.
		/// Lo lee la autoprueba para demostrar que esa ruta de dibujado se ejecuta de verdad.</summary>
		public static int FotogramasObjetoEnRaton;

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

			ConstruirCabecera();

			UIElement hueco = new UIElement();
			hueco.Width.Set(0f, 1f);
			hueco.Top.Set(AltoCabecera, 0f);
			hueco.Height.Set(-(AltoCabecera + AltoPie), 1f);
			_marco.Append(hueco);

			_contenido = new ContenidoLibreria();
			hueco.Append(_contenido);

			BotonTk cerrar = new BotonTk("Cerrar (O)", 0.85f);
			cerrar.Width.Set(150f, 0f);
			cerrar.Height.Set(34f, 0f);
			cerrar.HAlign = 1f;
			cerrar.VAlign = 1f;
			cerrar.AlPulsar += () => PanelLibreriaSystem.CerrarPanel("boton Cerrar");
			_marco.Append(cerrar);
		}

		private void ConstruirCabecera()
		{
			UIElement cabecera = new UIElement();
			cabecera.Width.Set(0f, 1f);
			cabecera.Height.Set(AltoCabecera - 8f, 0f);
			_marco.Append(cabecera);

			UIText titulo = new UIText("Terrakeep - Librería", 1f, true);
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(2f, 0f);
			cabecera.Append(titulo);

			EtiquetaTk detalle = new EtiquetaTk(
				() => ArbolLibreria.Listo
					? $"{CatalogoVivo.Objetos.Count} objetos del juego y de sus mods, descubiertos en vivo."
					: "Construyendo el catálogo...",
				0.75f, 700f, 20f);
			detalle.ColorTexto = EstiloTk.TextoSuave;
			detalle.Left.Set(0f, 0f);
			detalle.Top.Set(30f, 0f);
			cabecera.Append(detalle);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			MantenerInventarioAbierto();

			// Si el personaje desaparece (muerte en modo extremo, salir al menu...) no hay ningun
			// contenedor real al que llevar los objetos, y el panel se cierra solo antes de petar
			// leyendo nulos.
			if (Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				PanelLibreriaSystem.CerrarPanel("el jugador ha dejado de estar disponible");
			}
		}

		/// <summary>
		/// Deja <c>Main.playerInventory</c> a true mientras el panel esta abierto.
		/// <para />
		/// Es IMPRESCINDIBLE y es un fallo REAL que ya se vio en el juego construyendo WS1, no una
		/// precaucion teorica: <c>IngameFancyUI.OpenUIState</c> pone
		/// <c>Main.playerInventory = false</c>, y <c>Player.dropItemCheck</c> (que corre en cada
		/// tick) vacia <c>Main.mouseItem</c> siempre que ese campo este a false. O sea que sin
		/// esto, coger un objeto del catalogo y soltarlo en una ranura seria literalmente
		/// imposible: el objeto se devolveria solo al inventario (o caeria al suelo con el
		/// inventario lleno) en el tick siguiente.
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
		/// Dibuja el objeto que se lleva "cogido" con el raton.
		/// <para />
		/// Otro hallazgo real de WS1 que aqui hace todavia mas falta (la Libreria existe
		/// precisamente para coger objetos): la interfaz del juego es una lista de capas que se
		/// recorre hasta que una devuelve false, y la capa <c>"Vanilla: Fancy UI"</c> (indice 12)
		/// devuelve false siempre que hay un panel abierto con <c>IngameFancyUI</c>. La capa que
		/// dibuja el objeto cogido, <c>DrawInterface_38_MouseCarriedObject</c>, va despues
		/// (indice 38) y nunca se ejecuta. Sin esto, coger un objeto del catalogo lo haria
		/// desaparecer de la vista hasta soltarlo.
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

		/// <summary>Si al cerrar queda un objeto cogido con el raton se devuelve al inventario:
		/// fuera del panel nadie lo dibuja, asi que dejarlo ahi seria perderlo de vista.</summary>
		public static void DevolverObjetoDelRaton()
		{
			if (Main.mouseItem == null || Main.mouseItem.IsAir) {
				return;
			}

			// Hay que describirlo ANTES: Player.GetItem consume el objeto que se le pasa.
			string descripcion = $"{Main.mouseItem.Name} x{Main.mouseItem.stack}";

			Item sobrante = Main.LocalPlayer.GetItem(Main.myPlayer, Main.mouseItem,
				GetItemSettings.InventoryUIToInventorySettings);
			Main.mouseItem = sobrante;

			RegistroLibreria.Linea(
				$"{Terrakeep.LogTag} Libreria: al cerrar habia un objeto cogido con el raton " +
				$"({descripcion}); devuelto al inventario. Sobrante: " +
				$"{(sobrante == null || sobrante.IsAir ? "(vacio)" : sobrante.Name + " x" + sobrante.stack)}.");
		}

		/// <summary>Deja en el log, una sola vez por apertura, las medidas reales del panel ya
		/// dibujado. Es la evidencia de que esta puesto de verdad en pantalla y no solo construido
		/// en memoria.</summary>
		private void RegistrarMedidasUnaVez()
		{
			if (_medidasRegistradas || _marco == null) {
				return;
			}

			CalculatedStyle dim = _marco.GetDimensions();
			if (dim.Width <= 0f || _contenido == null || _contenido.GetDimensions().Width <= 0f) {
				return;
			}

			_medidasRegistradas = true;
			RegistroLibreria.Linea(
				$"{Terrakeep.LogTag} Panel de Libreria dibujado. Marco en coordenadas de pantalla: " +
				$"x={(int)dim.X} y={(int)dim.Y} w={(int)dim.Width} h={(int)dim.Height}. " +
				$"Resolucion {Main.screenWidth}x{Main.screenHeight}, escala de interfaz {Main.UIScale}. " +
				$"Contenido: {_contenido.Informe()}.");
		}
	}
}
