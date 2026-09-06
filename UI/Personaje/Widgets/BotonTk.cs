using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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

		private string _texto;
		private readonly float _escalaTexto;
		private float _escalaAnimada = EscalaReposo;

		/// <summary>Si es true el boton se pinta resaltado (pestaña seleccionada, opcion activa).</summary>
		public bool Activo;

		/// <summary>Si es false el boton se pinta apagado, no se anima y no dispara
		/// <see cref="AlPulsar"/>.</summary>
		public bool Habilitado = true;

		/// <summary>Texto que se muestra en el tooltip del juego al pasar el raton. null = ninguno.</summary>
		public string Ayuda;

		/// <summary>
		/// true si el boton es una pestaña o una pildora de una fila apretada. Crece menos al
		/// pasar el raton (2 px en vez de 3) para no llegar a tocar al de al lado: la separacion
		/// entre pestañas es de 6 px, o sea 3 por lado.
		/// </summary>
		public bool EsPestana;

		/// <summary>Se dispara con el clic izquierdo, solo si el boton esta habilitado.</summary>
		public event Action AlPulsar;

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

		/// <summary>Escala de la animacion ahora mismo (0.8 en reposo, 0.96 con el raton encima).
		/// La leen las autopruebas para demostrar que la animacion corre de verdad.</summary>
		public float EscalaAnimada => _escalaAnimada;

		/// <summary>Cuantos pixeles se ha inflado el marco ahora mismo, ya redondeado.</summary>
		public int CrecimientoActual => (int)Crecimiento();

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

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

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
				if (!string.IsNullOrEmpty(Ayuda)) {
					Main.instance.MouseText(Ayuda);
				}
			}

			DibujarTexto(spriteBatch, dim);
		}

		private void DibujarTexto(SpriteBatch spriteBatch, CalculatedStyle dim)
		{
			// El texto crece un 6% como mucho, al mismo ritmo que el marco. Mas que eso, con la
			// fuente del juego, se ve borroso y descentrado.
			float escala = _escalaTexto * (1f + 0.06f * Avance());
			Vector2 tamano = FontAssets.MouseText.Value.MeasureString(_texto) * escala;
			Vector2 posicion = new Vector2(
				dim.X + (dim.Width - tamano.X) / 2f,
				dim.Y + (dim.Height - tamano.Y) / 2f);

			Color color = Habilitado ? Color.White : new Color(150, 150, 150);
			Utils.DrawBorderString(spriteBatch, _texto, posicion, color, escala);
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
