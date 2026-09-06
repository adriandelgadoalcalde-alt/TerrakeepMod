using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.Common.Exploracion;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// Mini-mapa navegable DENTRO del panel de Terrakeep, dibujado con las texturas REALES que ya
	/// genera el juego. Estetica 100% vanilla y solo mundo ya descubierto, porque es literalmente
	/// el mismo mapa que enseña el juego.
	/// </summary>
	/// <remarks>
	/// <b>De donde sale la imagen.</b> <c>Main.instance.mapTarget</c> es una rejilla publica de
	/// <c>RenderTarget2D</c> (5x2 como maximo) en la que el juego va pintando el mapa a medida que
	/// lo descubres. Cada casilla cubre <c>Main.textureMaxWidth</c> x
	/// <c>Main.textureMaxHeight</c> tiles (2000x1800 en esta version), y el pixel (px, py) de la
	/// casilla [k, l] es exactamente el tile <c>(k*2000 + px, l*1800 + py)</c>: se dedujo del
	/// bucle de dibujado real de <c>Main.DrawMap</c>, comprobando que la posicion en pantalla que
	/// calcula el juego para cada trozo y para cada rectangulo de origen encaja con esa cuenta.
	/// El juego mantiene esos targets al dia por su cuenta en cada fotograma (<c>DrawToMap</c> se
	/// llama desde el ciclo de dibujado, no solo cuando el mapa esta a la vista), asi que aqui no
	/// hay nada que refrescar.
	/// <para />
	/// <b>La matematica de la vista</b> es la misma que usa el mapa a pantalla completa, reducida
	/// a lo esencial. En <c>DrawMap</c>, la posicion en pantalla de un tile sale de
	/// <c>num + (tile - 10) * escala</c> con
	/// <c>num = -mapFullscreenPos * escala + anchoPantalla/2 + 10 * escala</c>, que simplificando
	/// es <c>centro + (tile - posicionDelMapa) * escala</c>. Eso es lo que hace
	/// <see cref="TileAPantalla"/>, cambiando el centro de la pantalla por el centro de este
	/// elemento.
	/// <para />
	/// <b>Por que un contenedor y un lienzo dentro.</b> El recorte y el filtrado de textura los da
	/// el propio motor: un <c>UIElement</c> con <c>OverflowHidden</c> recorta a sus hijos con el
	/// rectangulo de tijera, y un hijo con <c>OverrideSamplerState</c> hace que el motor reabra el
	/// lote de dibujado con ese sampler. Con <c>PointClamp</c> el mapa sale nitido (con el
	/// <c>AnisotropicClamp</c> por defecto de la interfaz saldria emborronado). Los dos
	/// mecanismos son publicos y estan en <c>UIElement.Draw</c>; asi no hace falta tocar el
	/// <c>SpriteBatch</c> ni el <c>GraphicsDevice</c> a mano.
	/// </remarks>
	public class MiniMapaTk : UIElement
	{
		/// <summary>Pixeles de pantalla por tile.</summary>
		public float Escala { get; private set; } = 1f;

		/// <summary>Tile que queda en el centro de la vista.</summary>
		public Vector2 CentroTile { get; private set; }

		/// <summary>Escala maxima. Mas alla de esto el mapa del juego no tiene mas detalle que
		/// enseñar: cada tile es un pixel en la textura.</summary>
		public const float EscalaMaxima = 8f;

		private bool _encuadrado;
		private bool _arrastrando;
		private Vector2 _ratonAlEmpezarArrastre;
		private Vector2 _centroAlEmpezarArrastre;

		private LienzoMapaTk _lienzo;

		public MiniMapaTk()
		{
			OverflowHidden = true;

			// El lienzo se crea AQUI y no en OnInitialize: ese metodo solo lo llama el motor cuando
			// el elemento pasa por Activate/Initialize, y un elemento añadido al arbol despues de
			// que el UIState ya este activo se lo puede saltar. Se vio en la primera verificacion
			// real: el mini-mapa se dibujaba, pero los contadores del lienzo salian a -1 porque la
			// referencia estaba a null.
			_lienzo = new LienzoMapaTk(this);
			_lienzo.Width.Set(0f, 1f);
			_lienzo.Height.Set(0f, 1f);
			Append(_lienzo);

			// Un centro razonable desde el primer fotograma. Sin esto, el mini-mapa "mira" al tile
			// (0, 0) hasta su primer Update, y cualquier cosa que lea la vista antes de eso (por
			// ejemplo pulsar "Ver en el mapa del juego" nada mas abrir la pestaña) saltaria a la
			// esquina del mundo.
			CentroTile = Main.LocalPlayer != null && Main.LocalPlayer.active
				? Main.LocalPlayer.Center / 16f
				: new Vector2(Main.maxTilesX / 2f, Main.maxTilesY / 2f);
			Escala = 2.5f;
		}

		/// <summary>Escala minima: la que hace que quepa el mundo entero.</summary>
		public float EscalaMinima()
		{
			CalculatedStyle dim = GetDimensions();
			if (dim.Width <= 0f || Main.maxTilesX == 0) {
				return 0.05f;
			}
			float porAncho = dim.Width / Main.maxTilesX;
			float porAlto = dim.Height / Main.maxTilesY;
			return porAncho < porAlto ? porAncho : porAlto;
		}

		/// <summary>Coloca la vista para que se vea el mundo entero.</summary>
		public void EncuadrarMundo()
		{
			Escala = EscalaMinima();
			CentroTile = new Vector2(Main.maxTilesX / 2f, Main.maxTilesY / 2f);
			_encuadrado = true;
		}

		/// <summary>Centra la vista en un tile, opcionalmente cambiando el zoom.</summary>
		public void CentrarEn(Vector2 tile, float escala = -1f)
		{
			if (escala > 0f) {
				Escala = Acotar(escala, EscalaMinima(), EscalaMaxima);
			}
			CentroTile = tile;
			_encuadrado = true;
			Acotar();
		}

		/// <summary>Centra la vista en el jugador.</summary>
		public void CentrarEnJugador(float escala = 2.5f)
		{
			if (Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}
			CentrarEn(Main.LocalPlayer.Center / 16f, escala);
		}

		/// <summary>Multiplica el zoom manteniendo el centro. Lo usan los botones + y -.</summary>
		public void Acercar(float factor)
		{
			Escala = Acotar(Escala * factor, EscalaMinima(), EscalaMaxima);
			Acotar();
		}

		/// <summary>Posicion en pantalla (coordenadas de interfaz) de un tile del mundo.</summary>
		public Vector2 TileAPantalla(Vector2 tile)
		{
			CalculatedStyle dim = GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			return centro + (tile - CentroTile) * Escala;
		}

		/// <summary>La inversa: que tile hay bajo un punto de la pantalla.</summary>
		public Vector2 PantallaATile(Vector2 pantalla)
		{
			CalculatedStyle dim = GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			return CentroTile + (pantalla - centro) / Escala;
		}

		/// <summary>Tile bajo el raton, o null si el raton no esta encima del mini-mapa.</summary>
		public Vector2? TileBajoElRaton()
		{
			if (!IsMouseHovering) {
				return null;
			}
			return PantallaATile(RatonEnInterfaz());
		}

		/// <summary>
		/// Posicion del raton en coordenadas de INTERFAZ. Se toma de
		/// <c>Main.InGameUI.MousePosition</c> y no de <c>Main.MouseScreen</c> por el mismo motivo
		/// que documento WS1 en su deslizador: esta interfaz se dibuja con
		/// <c>InterfaceScaleType.UI</c>, asi que con las coordenadas sin escalar todo se
		/// descuadraria en cuanto alguien tuviera la escala de interfaz distinta del 100%.
		/// </summary>
		private static Vector2 RatonEnInterfaz()
		{
			return Main.InGameUI != null ? Main.InGameUI.MousePosition : Main.MouseScreen;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			if (!_encuadrado) {
				// El primer encuadre no se puede hacer en OnInitialize: alli el elemento todavia no
				// tiene medidas calculadas y la escala minima saldria absurda.
				CalculatedStyle dim = GetDimensions();
				if (dim.Width > 0f) {
					CentrarEnJugador();
					if (Main.LocalPlayer == null || !Main.LocalPlayer.active) {
						EncuadrarMundo();
					}
				}
			}

			if (IsMouseHovering) {
				// Sin esto el clic atraviesa el panel y el jugador ataca o coloca bloques detras.
				Main.LocalPlayer.mouseInterface = true;
				AplicarRueda();
			}

			AplicarArrastre();
		}

		private void AplicarRueda()
		{
			int rueda = PlayerInput.ScrollWheelDeltaForUI;
			if (rueda == 0) {
				return;
			}

			// Zoom alrededor del raton: el tile que hay bajo el cursor se queda donde esta, que es
			// como se comporta cualquier mapa decente (y como NO se comporta el de vanilla, que
			// siempre hace zoom sobre el centro).
			Vector2 raton = RatonEnInterfaz();
			Vector2 tileBajoElRaton = PantallaATile(raton);

			float factor = rueda > 0 ? 1.25f : 0.8f;
			Escala = Acotar(Escala * factor, EscalaMinima(), EscalaMaxima);

			Vector2 tileDespues = PantallaATile(raton);
			CentroTile += tileBajoElRaton - tileDespues;
			Acotar();
		}

		private void AplicarArrastre()
		{
			if (_arrastrando) {
				if (!Main.mouseLeft) {
					_arrastrando = false;
					return;
				}
				Vector2 movido = RatonEnInterfaz() - _ratonAlEmpezarArrastre;
				CentroTile = _centroAlEmpezarArrastre - movido / Escala;
				Acotar();
				return;
			}

			if (IsMouseHovering && Main.mouseLeft && Main.mouseLeftRelease) {
				_arrastrando = true;
				_ratonAlEmpezarArrastre = RatonEnInterfaz();
				_centroAlEmpezarArrastre = CentroTile;
			}
		}

		/// <summary>No deja que la vista se vaya fuera del mundo.</summary>
		private void Acotar()
		{
			CentroTile = new Vector2(
				Acotar(CentroTile.X, 0f, Main.maxTilesX),
				Acotar(CentroTile.Y, 0f, Main.maxTilesY));
		}

		private static float Acotar(float valor, float minimo, float maximo)
		{
			if (valor < minimo) {
				return minimo;
			}
			return valor > maximo ? maximo : valor;
		}

		/// <summary>Estado real de la vista, para el log de la autoprueba.</summary>
		public string Informe()
		{
			CalculatedStyle dim = GetDimensions();
			return "mini-mapa en x=" + (int)dim.X + " y=" + (int)dim.Y +
				" " + (int)dim.Width + "x" + (int)dim.Height +
				", centro en el tile (" + (int)CentroTile.X + ", " + (int)CentroTile.Y + ")" +
				", escala " + Escala.ToString("0.000") + " px/tile" +
				" (minima " + EscalaMinima().ToString("0.000") + ")" +
				", trozos de mapa dibujados el ultimo fotograma: " + (_lienzo != null ? _lienzo.TrozosDibujados : -1) +
				", marcadores dibujados: " + (_lienzo != null ? _lienzo.MarcadoresDibujados : -1);
		}

		/// <summary>Trozos de <c>mapTarget</c> dibujados en el ultimo fotograma.</summary>
		public int TrozosDibujados => _lienzo != null ? _lienzo.TrozosDibujados : 0;
	}

	/// <summary>
	/// El dibujado del mini-mapa. Va en un elemento aparte porque asi el contenedor le puede
	/// aplicar el recorte (<c>OverflowHidden</c>) y este puede pedir su propio filtro de textura
	/// (<c>OverrideSamplerState</c>), que son cosas que <c>UIElement</c> aplica a los hijos y a
	/// <c>DrawSelf</c> respectivamente.
	/// </summary>
	public class LienzoMapaTk : UIElement
	{
		private readonly MiniMapaTk _vista;

		public int TrozosDibujados;
		public int MarcadoresDibujados;

		public LienzoMapaTk(MiniMapaTk vista)
		{
			_vista = vista;
			// Con el AnisotropicClamp por defecto de la interfaz, el mapa sale emborronado.
			OverrideSamplerState = SamplerState.PointClamp;
			IgnoresMouseInteraction = true;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dim = GetDimensions();
			Rectangle marco = new Rectangle((int)dim.X, (int)dim.Y, (int)dim.Width, (int)dim.Height);

			// Fondo: lo que no se ha descubierto todavia no es transparente, es negro, igual que en
			// el mapa del juego.
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, marco, new Color(12, 14, 24));

			DibujarMapa(spriteBatch, marco);
			DibujarMarcadores(spriteBatch, marco);
		}

		private void DibujarMapa(SpriteBatch spriteBatch, Rectangle marco)
		{
			TrozosDibujados = 0;

			if (!Main.mapEnabled || !Main.mapReady || Main.instance == null || Main.instance.mapTarget == null) {
				return;
			}

			float escala = _vista.Escala;
			int filas = Main.instance.mapTarget.GetLength(0);
			int columnas = Main.instance.mapTarget.GetLength(1);

			for (int k = 0; k < filas; k++) {
				for (int l = 0; l < columnas; l++) {
					RenderTarget2D trozo = Main.instance.mapTarget[k, l];
					if (trozo == null || trozo.IsDisposed || trozo.IsContentLost) {
						continue;
					}

					Vector2 origenTile = new Vector2(k * Main.textureMaxWidth, l * Main.textureMaxHeight);
					Vector2 posicion = _vista.TileAPantalla(origenTile);
					Rectangle destino = new Rectangle(
						(int)posicion.X, (int)posicion.Y,
						(int)(trozo.Width * escala) + 1, (int)(trozo.Height * escala) + 1);

					if (!destino.Intersects(marco)) {
						continue;
					}

					spriteBatch.Draw(trozo, posicion, null, Color.White, 0f, Vector2.Zero, escala,
						SpriteEffects.None, 0f);
					TrozosDibujados++;
				}
			}
		}

		private void DibujarMarcadores(SpriteBatch spriteBatch, Rectangle marco)
		{
			MarcadoresDibujados = 0;
			Texture2D rombo = IconosExploracion.Rombo(spriteBatch.GraphicsDevice);
			Texture2D anillo = IconosExploracion.Anillo(spriteBatch.GraphicsDevice);
			Vector2 medio = new Vector2(rombo.Width / 2f, rombo.Height / 2f);

			// Resultados de la ultima busqueda. Se dibujan a tamaño fijo (no escalados con el
			// zoom): son señales, no cosas del mundo.
			foreach (ResultadoBusqueda resultado in MarcadoresExploracion.Resultados) {
				Vector2 posicion = _vista.TileAPantalla(resultado.Tile);
				if (!marco.Contains((int)posicion.X, (int)posicion.Y)) {
					continue;
				}
				spriteBatch.Draw(rombo, posicion, null, MarcadoresExploracion.Color, 0f, medio, 0.75f,
					SpriteEffects.None, 0f);
				MarcadoresDibujados++;
			}

			// Punto de aparicion del mundo.
			Vector2 spawn = _vista.TileAPantalla(new Vector2(Main.spawnTileX, Main.spawnTileY));
			if (marco.Contains((int)spawn.X, (int)spawn.Y)) {
				spriteBatch.Draw(anillo, spawn, null, new Color(150, 220, 255), 0f, medio, 0.8f,
					SpriteEffects.None, 0f);
			}

			// El jugador, encima de todo lo demas.
			if (Main.LocalPlayer != null && Main.LocalPlayer.active) {
				Vector2 jugador = _vista.TileAPantalla(Main.LocalPlayer.Center / 16f);
				if (marco.Contains((int)jugador.X, (int)jugador.Y)) {
					spriteBatch.Draw(anillo, jugador, null, Color.White, 0f, medio, 1f,
						SpriteEffects.None, 0f);
				}
			}
		}
	}
}
