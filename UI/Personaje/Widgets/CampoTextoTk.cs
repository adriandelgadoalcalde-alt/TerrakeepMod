using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Campo de texto editable con teclado real del juego.
	/// <para />
	/// tModLoader SI trae un campo de texto hecho (<c>Terraria.ModLoader.UI.UIFocusInputTextField</c>)
	/// pero esta declarado <c>internal</c> en el ensamblado del juego, asi que un mod no puede
	/// usarlo (comprobado decompilando el tModLoader.dll REALMENTE instalado, v2026.7.3.0). Lo que
	/// si es publico es toda la maquinaria que hay debajo, y es exactamente la que usa ese campo
	/// interno: <see cref="Main.GetInputText"/>, <see cref="Main.clrInput"/>,
	/// <c>Main.instance.HandleIME()</c> y <see cref="PlayerInput.WritingText"/>. Esta clase es esa
	/// misma maquinaria, con el marco de panel nativo encima.
	/// <para />
	/// <see cref="PlayerInput.WritingText"/> hay que ponerlo a true en CADA dibujado: el motor lo
	/// devuelve a false al final de cada <c>PlayerInput.UpdateInput()</c>. Mientras esta a true,
	/// <c>KeyboardInput()</c> vacia la lista de teclas pulsadas, y por eso escribir una "k" en un
	/// campo NO cierra el panel aunque la K sea el atajo del mod.
	/// </summary>
	public class CampoTextoTk : UIPanel
	{
		private readonly Func<string> _pista;
		private readonly int _longitudMaxima;
		private readonly float _escalaTexto;

		private string _texto = "";
		private bool _enfocado;
		private int _contadorParpadeo;
		private bool _cursorVisible;

		/// <summary>Se dispara cada vez que el texto cambia por teclado.</summary>
		public event Action<string> AlCambiar;

		/// <summary>Se dispara al perder el foco (clic fuera, Escape o Intro).</summary>
		public event Action<string> AlConfirmar;

		/// <summary>Si es true solo se aceptan digitos (y un signo menos inicial).</summary>
		public bool SoloNumeros;

		/// <summary>Cuenta los fotogramas en los que ALGUN campo ha capturado el teclado. Lo lee
		/// la autoprueba para demostrar que la ruta de entrada de texto se ejecuta de verdad (y
		/// que por tanto escribir no dispara los atajos del juego).</summary>
		public static int FotogramasCapturandoTeclado;

		/// <summary>La pista (el texto gris que se ve con el campo vacio) se pide con un
		/// <c>Func&lt;string&gt;</c> y no se guarda ya resuelta: si se guardara, se quedaria
		/// congelada en el idioma que hubiera al construir el campo.</summary>
		public CampoTextoTk(Func<string> pista, int longitudMaxima = 32, float escalaTexto = 0.85f)
		{
			_pista = pista;
			_longitudMaxima = longitudMaxima;
			_escalaTexto = escalaTexto;

			SetPadding(0f);
			Width.Set(180f, 0f);
			Height.Set(30f, 0f);
			BackgroundColor = new Color(28, 36, 68) * 0.95f;

			OnLeftClick += (evento, elemento) => {
				Main.clrInput();
				_enfocado = true;
			};
		}

		public string Texto => _texto;

		/// <summary>Pista que se esta enseñando ahora mismo, ya traducida.</summary>
		public string Pista => _pista != null ? (_pista() ?? "") : "";

		public bool Enfocado => _enfocado;

		/// <summary>Cambia el texto desde codigo, SIN disparar <see cref="AlCambiar"/>.</summary>
		public void FijarTextoSilencioso(string texto)
		{
			_texto = texto ?? "";
		}

		/// <summary>Suelta el foco (y dispara <see cref="AlConfirmar"/> si lo tenia).</summary>
		public void Desenfocar()
		{
			if (!_enfocado) {
				return;
			}
			_enfocado = false;
			if (AlConfirmar != null) {
				AlConfirmar(_texto);
			}
		}

		/// <summary>Valor numerico del campo, o <paramref name="porDefecto"/> si no se puede leer.</summary>
		public int ComoEntero(int porDefecto)
		{
			int valor;
			return int.TryParse(_texto, out valor) ? valor : porDefecto;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Un clic fuera del campo lo desenfoca, igual que hace el campo interno de tModLoader.
			if (_enfocado && Main.mouseLeft && !IsMouseHovering) {
				Desenfocar();
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			BorderColor = _enfocado ? new Color(160, 190, 255) : new Color(0, 0, 0, 0);
			base.DrawSelf(spriteBatch);

			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
			}

			if (_enfocado) {
				LeerTeclado();
			}

			CalculatedStyle dim = GetDimensions();
			bool vacio = _texto.Length == 0;
			string mostrado = vacio && !_enfocado ? Pista : _texto;
			Color color = vacio && !_enfocado ? new Color(120, 128, 150) : Color.White;

			if (_enfocado && _cursorVisible) {
				mostrado += "|";
			}

			Utils.DrawBorderString(spriteBatch, mostrado,
				new Vector2(dim.X + 8f, dim.Y + (dim.Height - 20f * _escalaTexto) / 2f),
				color, _escalaTexto);
		}

		private void LeerTeclado()
		{
			// Mientras haya un campo enfocado, el motor no debe interpretar las teclas como
			// atajos de juego (ni como el atajo del propio mod).
			PlayerInput.WritingText = true;
			FotogramasCapturandoTeclado++;
			Main.instance.HandleIME();

			string nuevo = Main.GetInputText(_texto);

			if (Main.inputTextEscape || Main.inputTextEnter) {
				Main.inputTextEscape = false;
				Main.inputTextEnter = false;
				Desenfocar();
				return;
			}

			if (SoloNumeros) {
				nuevo = FiltrarNumerico(nuevo);
			}
			if (nuevo.Length > _longitudMaxima) {
				nuevo = nuevo.Substring(0, _longitudMaxima);
			}

			if (!nuevo.Equals(_texto, StringComparison.Ordinal)) {
				_texto = nuevo;
				if (AlCambiar != null) {
					AlCambiar(_texto);
				}
			}

			if (++_contadorParpadeo >= 20) {
				_contadorParpadeo = 0;
				_cursorVisible = !_cursorVisible;
			}
		}

		private static string FiltrarNumerico(string entrada)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder(entrada.Length);
			for (int i = 0; i < entrada.Length; i++) {
				char c = entrada[i];
				if (char.IsDigit(c) || (c == '-' && sb.Length == 0)) {
					sb.Append(c);
				}
			}
			return sb.ToString();
		}
	}
}
