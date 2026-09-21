using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Boton de texto con el marco de panel nativo de Terraria y la <b>misma animacion de hover
	/// que los botones del propio juego</b>. Lo usan las seis areas del panel unico: pestañas,
	/// pildoras de filtro, acciones sueltas y el boton de cerrar.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>De donde sale la animacion (codigo real de <c>tModLoader.dll</c> v2026.7.3.0, no
	/// supuesto).</b> Lo primero que se comprobo al buscarla es que <c>UITextPanel&lt;T&gt;</c>
	/// -el boton "de manual" de la API de UI- <b>no anima absolutamente nada</b>: no sobrescribe
	/// <c>MouseOver</c>/<c>MouseOut</c>, no reproduce ningun sonido, y no tiene ningun campo de
	/// escala interpolada. Es mas: <b>no hay ni un solo <c>UIElement</c> de vanilla que interpole
	/// escala al pasar el raton</b>. Las pantallas del juego se limitan a cambiarle el color de
	/// fondo desde fuera.
	/// </para>
	/// <para>
	/// El boton que SI se anima en Terraria esta dibujado a mano en el HUD:
	/// <c>Main.DrawSettingButton(ref bool mouseOver, ref float scale, ...)</c>. Sus constantes
	/// reales, copiadas de ahi:
	/// </para>
	/// <list type="bullet">
	/// <item>escala en reposo <b>0.8</b>, escala con el raton encima <b>0.96</b>;</item>
	/// <item>se mueve <b>0.02 por fotograma</b> en los dos sentidos (unos 8 fotogramas a 60 fps,
	/// o sea ~0,13 s: se nota, pero no se hace lento);</item>
	/// <item>el sonido suena <b>solo al ENTRAR</b> el raton (<c>if (!mouseOver) PlaySound(12)</c>),
	/// no en cada fotograma;</item>
	/// <item>al hacer clic, <c>scale = 0.8f</c>: el boton se "hunde" de golpe y vuelve a crecer.
	/// Ese rebote es la mitad de la sensacion de pulsar un boton de Terraria.</item>
	/// </list>
	/// <para>
	/// <b>El sonido.</b> El <c>PlaySound(12)</c> que aparece en todo el codigo decompilado es el id
	/// legacy de <c>SoundID.MenuTick</c> (<c>SoundID.GetLegacyStyle</c>: <c>case 12: return
	/// MenuTick;</c>). Es el mismo que usan <c>UIImageButton.MouseOver</c>,
	/// <c>UIColoredImageButton.MouseOver</c>, <c>EmoteButton.MouseOver</c>, el icono del bestiario
	/// y el de emotes. Y tambien es el del CLIC (<c>UIIconTextButton.LeftMouseDown</c> y los dos
	/// iconos del inventario hacen <c>PlaySound(12)</c> justo antes de abrir su interfaz), asi que
	/// aqui se usa el mismo en los dos sitios. Ojo: la sobrecarga <c>PlaySound(int)</c> es
	/// <c>internal</c> y un mod no la puede llamar - hay que pasar el <c>SoundStyle</c>.
	/// </para>
	/// <para>
	/// <b>Como se dibuja mas grande sin romper el marco.</b> <c>UIPanel.DrawSelf</c> pinta un marco
	/// de nueve trozos con un metodo <c>private</c> que lee <c>GetDimensions()</c> directamente, o
	/// sea que no se le puede pedir que dibuje inflado. La via limpia es
	/// <c>Utils.DrawSplicedPanel</c> con margenes de 12 sobre las MISMAS dos texturas
	/// (<c>Images/UI/PanelBackground</c> y <c>Images/UI/PanelBorder</c>, 28x28 las dos,
	/// comprobado descomprimiendo los <c>.xnb</c> reales): 28 - 12 - 12 = 4, que es exactamente el
	/// <c>_barSize</c> de <c>UIPanel</c>, asi que el resultado es identico pixel a pixel. Las
	/// esquinas mantienen su tamaño y solo se estiran los bordes, que es lo que evita que el marco
	/// se deforme. Es la misma tecnica que usa <c>GroupOptionButton</c> en la creacion de
	/// personaje.
	/// </para>
	/// <para>
	/// La animacion avanza en <c>Update</c> y no en <c>DrawSelf</c> a proposito: <c>Update</c> corre
	/// a paso logico fijo (<c>Main.UpdateUIStates</c> -&gt; <c>UserInterface.Update</c> -&gt;
	/// <c>UIElement.Update</c> recursivo, confirmado en el codigo real), mientras que el dibujado
	/// puede ejecutarse mas o menos veces segun <c>Main.FrameSkipMode</c>. Acumulando en el
	/// dibujado, la velocidad de la animacion cambiaria con la configuracion de video del usuario.
	/// </para>
	/// </remarks>
	public class BotonTk : UIPanel
	{
		// Constantes reales de Main.DrawSettingButton.
		private const float EscalaReposo = 0.8f;
		private const float EscalaSobre = 0.96f;
		private const float PasoPorFotograma = 0.02f;

		/// <summary>Margen del marco de nueve trozos. 12 es el <c>_cornerSize</c> de
		/// <see cref="UIPanel"/> y la unica cifra con la que <c>DrawSplicedPanel</c> reproduce su
		/// marco exactamente (las texturas son de 28x28).</summary>
		private const int MargenMarco = 12;

		private static Asset<Texture2D> _texturaFondo;
		private static Asset<Texture2D> _texturaBorde;

		/// <summary>
		/// Texto pendiente de dibujar como tooltip CON FONDO PROPIO, fijado por
		/// <see cref="DrawSelf"/> de quien tenga el raton encima este fotograma. null si nadie lo
		/// ha pedido. Se consume (se pone a null) en cuanto <see cref="DibujarTooltipPendiente"/>
		/// lo dibuja, asi que si nadie lo vuelve a pedir el fotograma siguiente, desaparece solo -
		/// igual que el tooltip vainilla al apartar el raton.
		/// </summary>
		private static string _tooltipPendiente;

		private string _texto;
		private float _escalaTexto;
		private float _escalaAnimada = EscalaReposo;

		/// <summary>true mientras el boton de raton izquierdo sigue pulsado desde que se apreto
		/// SOBRE este boton (aunque el raton se haya movido fuera mientras tanto, igual que hace
		/// cualquier boton nativo), hasta que se suelta - sea donde sea. Lo usan los controles que
		/// necesitan repetir una accion al MANTENER pulsado (ver EditorCantidadTk, "+"/"-" de
		/// cantidad con aceleracion): no se puede usar <c>IsMouseHovering &amp;&amp; Main.mouseLeft</c>
		/// a pelo para eso, porque enganchar por hover permitiria empezar a "mantener" arrastrando
		/// el raton YA pulsado desde otro sitio, cosa que ningun boton real hace.</summary>
		private bool _pulsando;

		/// <summary>true mientras <see cref="ForzarManteniendoParaAutoprueba"/> tiene el control:
		/// mientras dure, <c>Update</c> ignora <c>Main.mouseLeft</c> del todo (ver esa nota).</summary>
		private bool _pulsandoForzadoPorAutoprueba;

		/// <summary>Si es true el boton se pinta resaltado (pestaña seleccionada, opcion activa).</summary>
		public bool Activo;

		/// <summary>
		/// Identificador interno opcional, para que quien mantenga un grupo de botones sepa cual
		/// es cual sin compararlos por su TEXTO VISIBLE - que ahora cambia con el idioma y por
		/// tanto ya no sirve como clave.
		/// </summary>
		public string Clave;

		/// <summary>Si es false el boton se pinta apagado, no se anima y no dispara
		/// <see cref="AlPulsar"/>.</summary>
		public bool Habilitado = true;

		/// <summary>Texto que se muestra en el tooltip del juego al pasar el raton. null = ninguno.
		/// <para />
		/// Es un <c>Func&lt;string&gt;</c> y no una cadena a proposito: se pide en cada dibujado,
		/// asi que un tooltip sacado de la localizacion cambia con el idioma sin que haya que
		/// reconstruir el boton.</summary>
		public Func<string> Ayuda;

		/// <summary>
		/// true si el boton es una pestaña o una pildora de una fila apretada. Crece menos al
		/// pasar el raton (2 px en vez de 3) para no llegar a tocar al de al lado: la separacion
		/// entre pestañas es de 6 px, o sea 3 por lado.
		/// </summary>
		public bool EsPestana;

		/// <summary>Se dispara con el clic izquierdo, solo si el boton esta habilitado.</summary>
		public event Action AlPulsar;

		// --- TM1 (catalogo de rediseño visual): icono real del propio juego junto al texto -----
		/// <summary>Icono opcional a la izquierda del texto. Se pide en cada dibujado (nunca se
		/// guarda ya resuelto) por si la textura tarda en cargar la primera vez. null = sin icono,
		/// el boton se comporta exactamente igual que antes de TM1.</summary>
		public Func<Texture2D> Icono;

		/// <summary>Caso especial de <see cref="Icono"/>: la cabeza REAL del jugador cargado, via
		/// <c>Main.MapPlayerRenderer</c> (el mismo renderer con el que vanilla dibuja los iconos de
		/// jugador del mapa/minimapa - un RenderTarget que tarda un fotograma en estar listo la
		/// primera vez, <c>IsReady</c> lo dice solo). Nunca a la vez que <see cref="Icono"/>.</summary>
		public bool IconoCabezaJugador;

		/// <summary>Lado del icono cuadrado, en pixeles a escala de reposo.</summary>
		public float LadoIcono = 20f;

		/// <summary>
		/// Por debajo de este ancho REAL del boton (con icono puesto), el texto se esconde y solo
		/// queda el icono centrado con su <see cref="Ayuda"/> - "por debajo de cierto ancho, solo el
		/// sprite con tooltip" (TM1). Sin icono este campo no hace nada: el texto nunca se esconde
		/// por si solo, sigue siendo responsabilidad de quien coloca el boton (ver
		/// <c>PanelTerrakeepState.AjustarEscalaDeLasPestanas</c>).
		/// </summary>
		public float AnchoMinimoConTexto = 74f;

		/// <summary>Cuanto ancho REAL le come el icono al texto ahora mismo (0 sin icono). Lo usa
		/// <c>PanelTerrakeepState.AjustarEscalaDeLasPestanas</c> para medir cuanto texto cabe de
		/// verdad en un boton con icono - sin esto la cuenta seria demasiado optimista y el texto
		/// podria acabar dibujandose encima del propio icono.</summary>
		public float MargenIconoParaMedida => (Icono != null || IconoCabezaJugador) ? LadoIcono + 8f : 0f;

		public BotonTk(string texto, float escalaTexto = 0.85f)
		{
			_texto = texto;
			_escalaTexto = escalaTexto;
			SetPadding(0f);
			Width.Set(120f, 0f);
			Height.Set(32f, 0f);
			BorderColor = new Color(0, 0, 0, 0);

			OnLeftClick += (evento, elemento) => {
				if (!Habilitado) {
					return;
				}

				// Mismo sonido que el clic de los botones del juego (PlaySound(12) = MenuTick), y
				// el mismo "hundido" instantaneo de Main.DrawSettingButton: scale = 0.8f.
				SoundEngine.PlaySound(SoundID.MenuTick);
				_escalaAnimada = EscalaReposo;

				if (AlPulsar != null) {
					AlPulsar();
				}
			};
		}

		public void FijarTexto(string texto)
		{
			_texto = texto ?? "";
		}

		public string Texto => _texto;

		/// <summary>
		/// Escala del texto. Es escribible para que quien tenga varios botones en una fila apretada
		/// pueda BAJARLA lo justo para que el rotulo mas largo quepa entero, midiendolo con la
		/// fuente real.
		/// <para />
		/// Hizo falta al añadir la septima pestaña al panel: con siete, cada una pasa de 1/6 a 1/7
		/// del ancho y "Investigación" ya no cabia a 0,8. La regla de este proyecto es que <b>ningun
		/// texto se recorta con puntos suspensivos</b>, asi que lo que se adapta es el layout, no el
		/// texto. Ver <c>PanelTerrakeepState.AjustarEscalaDeLasPestanas</c>.
		/// </summary>
		public float EscalaTexto {
			get { return _escalaTexto; }
			set { _escalaTexto = value; }
		}

		/// <summary>Escala de la animacion ahora mismo (0.8 en reposo, 0.96 con el raton encima).
		/// La leen las autopruebas para demostrar que la animacion corre de verdad.</summary>
		public float EscalaAnimada => _escalaAnimada;

		/// <summary>Cuantos pixeles se ha inflado el marco ahora mismo, ya redondeado.</summary>
		public int CrecimientoActual => (int)Crecimiento();

		/// <summary>true mientras el raton sigue pulsado desde que se apreto sobre este boton. Ver
		/// la nota de cabecera del campo <c>_pulsando</c>.</summary>
		public bool Manteniendo => _pulsando;

		/// <summary>
		/// SOLO PARA AUTOPRUEBAS: fuerza <see cref="Manteniendo"/> a un valor concreto sin pasar por
		/// <c>Main.mouseLeft</c>. Hace falta un camino aparte porque <c>Main.mouseLeft</c> NO es un
		/// flag que se pueda "dejar puesto" varios fotogramas reales sin hardware de por medio: el
		/// motor lo sobreescribe con el estado REAL del boton fisico del raton en cada fotograma de
		/// entrada (<c>PlayerInput</c>), asi que un <c>Main.mouseLeft = true</c> puesto una vez desde
		/// una autoprueba se pierde antes de que pase el tiempo suficiente para que la aceleracion
		/// llegue a dispararse - visto en el juego real verificando esta misma comprobacion (0
		/// repeticiones en 2,6s manteniendo "pulsado"). El juego real NUNCA llama a esto.
		/// </summary>
		public void ForzarManteniendoParaAutoprueba(bool valor)
		{
			if (valor) {
				_pulsandoForzadoPorAutoprueba = true;
				_pulsando = Habilitado;
			}
			else {
				// Se desactiva del todo, no solo se pone a false: asi el boton vuelve a responder a
				// Main.mouseLeft normal despues (otros pasos de la misma autoprueba pulsan otros
				// botones del mismo panel por su ruta real).
				_pulsandoForzadoPorAutoprueba = false;
				_pulsando = false;
			}
		}

		/// <summary>
		/// Suena al ENTRAR el raton, una sola vez, igual que hacen <c>UIImageButton.MouseOver</c> y
		/// <c>Main.DrawSettingButton</c>. Un boton apagado no suena: no se puede pulsar.
		/// </summary>
		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			if (Habilitado) {
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
		}

		/// <summary>Marca el inicio de "mantener pulsado" (ver <see cref="Manteniendo"/>). Solo
		/// cuenta si el boton estaba habilitado: uno apagado no debe engancharse a nada.</summary>
		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			if (Habilitado) {
				_pulsando = true;
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Se suelta en cuanto el boton fisico del raton sube, este el cursor donde este -
			// igual que "Manteniendo" documenta. Mientras una autoprueba tenga el control
			// (ForzarManteniendoParaAutoprueba), Main.mouseLeft se ignora del todo: ver esa nota.
			if (!_pulsandoForzadoPorAutoprueba && !Main.mouseLeft) {
				_pulsando = false;
			}

			// Exactamente la rampa de Main.DrawSettingButton: +-0.02 por fotograma entre 0.8 y 0.96.
			if (IsMouseHovering && Habilitado) {
				if (_escalaAnimada < EscalaSobre) {
					_escalaAnimada = Math.Min(EscalaSobre, _escalaAnimada + PasoPorFotograma);
				}
			}
			else if (_escalaAnimada > EscalaReposo) {
				_escalaAnimada = Math.Max(EscalaReposo, _escalaAnimada - PasoPorFotograma);
			}
		}

		/// <summary>Cuanto se infla el marco, en pixeles, con la animacion en su punto actual.</summary>
		private float Crecimiento()
		{
			float maximo = EsPestana ? 2f : 3f;
			return maximo * Avance();
		}

		/// <summary>0 = en reposo, 1 = del todo resaltado.</summary>
		private float Avance()
		{
			return (_escalaAnimada - EscalaReposo) / (EscalaSobre - EscalaReposo);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (!Habilitado) {
				BackgroundColor = EstiloTk.BotonApagado;
			}
			else if (Activo) {
				BackgroundColor = EstiloTk.BotonActivo;
			}
			else if (IsMouseHovering) {
				BackgroundColor = EstiloTk.BotonSobre;
			}
			else {
				BackgroundColor = EstiloTk.BotonNormal;
			}

			CargarTexturas();

			CalculatedStyle dim = GetDimensions();
			int crecimiento = (int)Crecimiento();
			int x = (int)dim.X - crecimiento;
			int y = (int)dim.Y - crecimiento;
			int ancho = (int)dim.Width + crecimiento * 2;
			int alto = (int)dim.Height + crecimiento * 2;

			// No se llama a base.DrawSelf: dibujaria el mismo marco SIN inflar justo debajo.
			if (_texturaFondo != null) {
				Utils.DrawSplicedPanel(spriteBatch, _texturaFondo.Value, x, y, ancho, alto,
					MargenMarco, MargenMarco, MargenMarco, MargenMarco, BackgroundColor);
			}

			// Borde: invisible en reposo y luminoso con el raton encima, apareciendo al mismo ritmo
			// que crece el marco. Es el equivalente al _hoveredBorderTexture de GroupOptionButton.
			if (_texturaBorde != null && Habilitado) {
				Color borde = EstiloTk.BordeSobre * Avance();
				if (borde.A > 0) {
					Utils.DrawSplicedPanel(spriteBatch, _texturaBorde.Value, x, y, ancho, alto,
						MargenMarco, MargenMarco, MargenMarco, MargenMarco, borde);
				}
			}

			if (IsMouseHovering) {
				// Sin esto el clic atraviesa el panel y el jugador ataca o coloca bloques detras.
				Main.LocalPlayer.mouseInterface = true;
				string ayuda = Ayuda != null ? Ayuda() : null;
				if (!string.IsNullOrEmpty(ayuda)) {
					// NO se llama a Main.instance.MouseText: confirmado en el codigo real de
					// Main.MouseTextInner (tModLoader.dll instalado, via ilspycmd) que la caja
					// opaca de fondo SOLO se dibuja dentro de MouseText_DrawItemTooltip (el
					// tooltip de un OBJETO) - un tooltip de texto suelto como este nunca lleva
					// panel, ni con "caja opaca detras de tooltips" activado en las opciones del
					// jugador. Sin fondo, y con dos lineas (descripcion + atajo, mas alto de lo
					// normal), tapaba contenido real del panel a ventana pequeña: hallazgo KeepQA
					// del 14-sep-2026 (bitacora.md), cinco capturas reales. Se guarda aqui y se
					// dibuja DESPUES de todo el arbol en DibujarTooltipPendiente (mismo patron ya
					// usado por PanelTerrakeepState.DibujarTooltipDeObjeto), con el mismo marco de
					// 9 trozos que este boton usa para si mismo - asi el limite entre "esto es un
					// tooltip" y "esto es contenido del panel" queda claro, en vez de mezclarse.
					_tooltipPendiente = ayuda;
				}
			}

			bool hayIcono = Icono != null || IconoCabezaJugador;
			bool textoVisible = !hayIcono || dim.Width >= AnchoMinimoConTexto || string.IsNullOrEmpty(_texto);

			if (hayIcono) {
				DibujarIcono(spriteBatch, dim, textoVisible);
			}
			if (textoVisible) {
				DibujarTexto(spriteBatch, dim, hayIcono ? LadoIcono + 8f : 0f);
			}
		}

		/// <summary>TM1: el icono a la izquierda del texto (o centrado en solitario si
		/// <paramref name="haySitioParaTexto"/> es false - el boton se quedo sin ancho para las dos
		/// cosas). <see cref="IconoCabezaJugador"/> usa el renderer real del motor; cualquier otro
		/// icono es una textura fija pedida a <see cref="Icono"/>.</summary>
		private void DibujarIcono(SpriteBatch spriteBatch, CalculatedStyle dim, bool haySitioParaTexto)
		{
			float lado = LadoIcono * (1f + 0.06f * Avance());
			float x = haySitioParaTexto ? dim.X + 6f : dim.X + (dim.Width - lado) / 2f;
			float y = dim.Y + (dim.Height - lado) / 2f;

			if (IconoCabezaJugador) {
				Player jugador = Main.LocalPlayer;
				if (jugador != null && jugador.active && Main.MapPlayerRenderer != null) {
					Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, jugador,
						new Vector2(x + lado / 2f, y + lado / 2f), Habilitado ? 1f : 0.5f, lado / 40f, Color.Transparent);
				}
				return;
			}

			Texture2D textura = Icono != null ? Icono() : null;
			if (textura == null) {
				return;
			}
			Rectangle destino = new Rectangle((int)x, (int)y, (int)lado, (int)lado);
			Color color = Habilitado ? Color.White : new Color(150, 150, 150);
			spriteBatch.Draw(textura, destino, color);
		}

		private void DibujarTexto(SpriteBatch spriteBatch, CalculatedStyle dim, float margenIzquierdo)
		{
			// El texto crece un 6% como mucho, al mismo ritmo que el marco. Mas que eso, con la
			// fuente del juego, se ve borroso y descentrado.
			float escala = _escalaTexto * (1f + 0.06f * Avance());
			Vector2 tamano = FontAssets.MouseText.Value.MeasureString(_texto) * escala;
			float anchoDisponible = dim.Width - margenIzquierdo;
			Vector2 posicion = new Vector2(
				dim.X + margenIzquierdo + (anchoDisponible - tamano.X) / 2f,
				dim.Y + (dim.Height - tamano.Y) / 2f);

			Color color = Habilitado ? Color.White : new Color(150, 150, 150);
			EscribirTk.Dibujar(spriteBatch, _texto, posicion, color, escala);
		}

		/// <summary>
		/// Dibuja, si alguien lo pidio este fotograma, el tooltip de un <see cref="BotonTk"/> con
		/// fondo propio (el mismo marco de 9 trozos y las mismas texturas, <c>PanelBackground</c>/
		/// <c>PanelBorder</c>, que ya usa este boton para su propio cuerpo - no una caja inventada
		/// aparte). Hay que llamarlo DESPUES de que todo el arbol de UI haya terminado de dibujarse
		/// (en <c>PanelTerrakeepState.Draw</c>, despues de <c>base.Draw</c>), exactamente el mismo
		/// motivo por el que <c>DibujarTooltipDeObjeto</c> tambien se aplaza a ese punto: la barra
		/// de pestañas se dibuja PRIMERO en el arbol (esta arriba del todo), asi que si el tooltip
		/// se dibujara dentro del propio <see cref="DrawSelf"/> del boton quedaria por DEBAJO de
		/// cualquier fila que el panel dibuje despues - el mismo problema de tapar contenido que
		/// esto viene a arreglar, solo que al reves.
		/// <para />
		/// Sigue "tapando" lo que haya debajo en la zona que ocupa - es un tooltip flotante junto
		/// al raton, ese es su trabajo en cualquier interfaz, incluida la vainilla - pero ahora con
		/// un borde y un fondo solido que marcan con claridad donde empieza y donde acaba, en vez
		/// de mezclar sus letras con lo que hay detras.
		/// </summary>
		/// <summary>SOLO ARNES DE PRUEBAS: valor real de <see cref="_tooltipPendiente"/> ahora
		/// mismo, sin consumirlo. Lo usa el diagnostico del "tooltip huerfano" (bitacora.md) para
		/// comprobar el estado interno de verdad, no solo lo que se ve en una captura.</summary>
		public static string TooltipPendienteParaPrueba => _tooltipPendiente;

		public static void DibujarTooltipPendiente(SpriteBatch spriteBatch)
		{
			string texto = _tooltipPendiente;
			_tooltipPendiente = null; // Consumido: si nadie lo vuelve a pedir, no se dibuja nada el fotograma que viene.
			if (string.IsNullOrEmpty(texto)) {
				return;
			}

			CargarTexturas();

			string[] lineas = texto.Split('\n');
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			const float relleno = 8f;
			const float espacioEntreLineas = 2f;

			Vector2[] tamanos = new Vector2[lineas.Length];
			float anchoMax = 0f;
			float altoTotal = 0f;
			for (int i = 0; i < lineas.Length; i++) {
				tamanos[i] = fuente.MeasureString(lineas[i]);
				anchoMax = Math.Max(anchoMax, tamanos[i].X);
				altoTotal += tamanos[i].Y;
				if (i > 0) {
					altoTotal += espacioEntreLineas;
				}
			}

			float anchoCaja = anchoMax + relleno * 2f;
			float altoCaja = altoTotal + relleno * 2f;

			// Mismo desplazamiento base (mouseX/Y + 14) que usa Main.MouseTextInner, mas un poco
			// para dejar sitio al propio cursor del juego.
			float x = Main.mouseX + 20f;
			float y = Main.mouseY + 20f;

			// Mismo clamp de pantalla que hace Main.MouseTextInner: nunca se sale del area jugable.
			if (x + anchoCaja > Main.screenWidth) {
				x = Main.screenWidth - anchoCaja;
			}
			if (y + altoCaja > Main.screenHeight) {
				y = Main.screenHeight - altoCaja;
			}
			if (x < 0f) {
				x = 0f;
			}
			if (y < 0f) {
				y = 0f;
			}

			if (_texturaFondo != null) {
				Utils.DrawSplicedPanel(spriteBatch, _texturaFondo.Value, (int)x, (int)y,
					(int)anchoCaja, (int)altoCaja, MargenMarco, MargenMarco, MargenMarco, MargenMarco,
					EstiloTk.FondoTooltip);
			}
			if (_texturaBorde != null) {
				Utils.DrawSplicedPanel(spriteBatch, _texturaBorde.Value, (int)x, (int)y,
					(int)anchoCaja, (int)altoCaja, MargenMarco, MargenMarco, MargenMarco, MargenMarco,
					EstiloTk.BordeSobre);
			}

			// La primera linea (la descripcion) en blanco, igual que cualquier tooltip vainilla; la
			// segunda en adelante (el atajo de teclado) en el mismo gris suave que ya usa el resto
			// del panel para texto secundario (ver EtiquetaTk.ColorTexto en ConstruirPie).
			float cursorY = y + relleno;
			for (int i = 0; i < lineas.Length; i++) {
				Color color = i == 0 ? Color.White : EstiloTk.TextoSuave;
				EscribirTk.Dibujar(spriteBatch, lineas[i], new Vector2(x + relleno, cursorY), color, 1f);
				cursorY += tamanos[i].Y + espacioEntreLineas;
			}

			// Mismo motivo que el resto de sitios que dibujan encima del raton: sin esto el clic
			// atravesaria el tooltip y llegaria al mundo (atacar, colocar bloques) mientras se lee.
			Main.LocalPlayer.mouseInterface = true;
		}

		/// <summary>
		/// Las dos texturas del marco de <see cref="UIPanel"/>, pedidas por su ruta real. Son las
		/// mismas que carga <c>UIPanel.LoadTextures</c>; se comparten entre todos los botones del
		/// mod porque son solo dos referencias.
		/// </summary>
		private static void CargarTexturas()
		{
			if (_texturaFondo == null) {
				_texturaFondo = Main.Assets.Request<Texture2D>("Images/UI/PanelBackground");
			}
			if (_texturaBorde == null) {
				_texturaBorde = Main.Assets.Request<Texture2D>("Images/UI/PanelBorder");
			}
		}

		/// <summary>Se llama al descargar el mod: las texturas del juego no deben quedarse
		/// referenciadas desde un ensamblado que se va.</summary>
		public static void Descargar()
		{
			_texturaFondo = null;
			_texturaBorde = null;
		}
	}
}
