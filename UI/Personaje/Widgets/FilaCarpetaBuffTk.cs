using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using Terrakeep.Core.Data;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Una fila del arbol de carpetas de <see cref="TerrakeepMod.UI.Personaje.PestanaBuffs"/>:
	/// icono real del primer buff de la carpeta, nombre, y una flecha si tiene subcarpetas dentro.
	/// <para />
	/// Calco literal de <see cref="TerrakeepMod.UI.Libreria.FilaCarpetaTk"/> (misma paleta, mismo
	/// recorte de texto con la fuente real, mismo criterio de resaltado), con la unica diferencia
	/// real que hacia falta: el icono es el de un BUFF (<c>TextureAssets.Buff</c>), no el de un
	/// objeto, asi que no se puede reutilizar <c>IconoObjetoTk.Dibujar</c> tal cual.
	/// </summary>
	public class FilaCarpetaBuffTk : UIPanel
	{
		private const float LadoIcono = 26f;
		private const float EscalaTexto = 0.8f;
		private const float AltoMinimo = 34f;

		private readonly int _iconoTipo;
		private readonly string _nombre;
		private readonly bool _tieneHijas;
		private readonly int _buffs;

		/// <summary>Nombre YA envuelto (<see cref="EtiquetaTk.PartirEnLineas"/>) a tantas lineas como
		/// haga falta para caber en el ancho real de esta fila.
		/// <para />
		/// Antes se calculaba UNA sola vez en el constructor, porque el ancho de la columna de
		/// carpetas era una CONSTANTE (<c>AnchoColumnaCarpetas</c> = 132 px fijos). Desde el
		/// 8-sep-2026 esa columna se dimensiona en vivo segun el contenido real
		/// (<c>PestanaBuffs.AjustarAnchoCarpetas</c>), asi que el ancho de la fila cambia con la
		/// resolucion y con la carpeta abierta: se recalcula desde
		/// <see cref="AjustarAlAnchoReal"/> cada vez que ese ancho cambia de verdad.</summary>
		private string _nombrePartido;

		/// <summary>Alto REAL (con la fuente real) del bloque de texto ya envuelto. Con esto la fila
		/// crece lo que haga falta en vez de recortar el nombre.</summary>
		private float _altoTexto;

		/// <summary>Ancho con el que se calculo <see cref="_nombrePartido"/> la ultima vez. Sirve
		/// para no rehacer la particion (ni pedir un <c>Recalculate</c> de la lista) en cada
		/// fotograma cuando nada ha cambiado.</summary>
		private float _anchoUsado;

		/// <summary>Relleno vertical de la fila: lo que sobra del <see cref="AltoMinimo"/> por
		/// encima de UNA linea de texto. Se conserva cuando el nombre pasa a ocupar mas lineas,
		/// para que el margen de arriba/abajo sea siempre el mismo.</summary>
		private readonly float _relleno;

		/// <summary>La carpeta que representa esta fila.</summary>
		public readonly CategoryTreeNodeData Nodo;

		/// <summary>Si es true la fila se pinta resaltada (carpeta abierta ahora mismo).</summary>
		public bool Activa;

		/// <summary>Se dispara con el clic izquierdo.</summary>
		public event Action AlPulsar;

		public FilaCarpetaBuffTk(CategoryTreeNodeData nodo, float ancho)
		{
			Nodo = nodo;
			_nombre = nodo.Name ?? "";
			_tieneHijas = nodo.Children != null && nodo.Children.Count > 0;
			_buffs = nodo.ItemIdsOrdered != null ? nodo.ItemIdsOrdered.Count : 0;
			_iconoTipo = ArbolBuffs.IdDeIcono(nodo.IconPath);

			// Nunca se recorta con "...": si el nombre no cabe en una linea se ENVUELVE a varias
			// (mismo patron que ya uso PestanaBuffs con el nombre de un buff activo, commit cae61b5,
			// y el calco literal de esta fila en la Libreria, FilaCarpetaTk) y la fila crece lo que
			// haga falta - es un UIList real (ver PestanaBuffs.RellenarCarpetas), asi que una fila
			// mas alta simplemente empuja a la siguiente hacia abajo, no rompe nada.
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			float altoUnaLinea = fuente.MeasureString("Ag").Y * EscalaTexto;
			_relleno = AltoMinimo - altoUnaLinea;

			SetPadding(0f);
			// Ancho RELATIVO (100% de la lista que la contiene), no los pixeles fijos de antes: la
			// columna de carpetas ya no mide siempre lo mismo (ver PestanaBuffs.AjustarAnchoCarpetas),
			// asi que la fila tiene que seguir a su contenedor. El parametro "ancho" solo sirve de
			// valor de PARTIDA para el primerisimo fotograma, antes de que haya geometria real.
			Width.Set(0f, 1f);
			BorderColor = new Color(0, 0, 0, 0);
			Envolver(ancho);

			OnLeftClick += (evento, elemento) => {
				if (AlPulsar != null) {
					AlPulsar();
				}
			};
		}

		/// <summary>Distancia del borde izquierdo de la fila al principio del texto: deja sitio al
		/// icono si de verdad hay uno que dibujar.</summary>
		private float XTexto {
			get { return _iconoTipo > 0 ? 6f + LadoIcono + 8f : 10f; }
		}

		/// <summary>Hueco reservado a la derecha del texto: la flecha ">" de "tiene subcarpetas", o
		/// un margen normal si no la lleva.</summary>
		private float MargenDerecho {
			get { return _tieneHijas ? 22f : 10f; }
		}

		/// <summary>
		/// Ancho de fila con el que este nombre cabria ENTERO en una sola linea, medido con la
		/// fuente real. Lo usa <c>PestanaBuffs</c> para dimensionar la columna de carpetas segun su
		/// contenido de verdad en vez de con un numero fijo puesto a ojo.
		/// </summary>
		public float AnchoParaUnaLinea {
			get {
				return XTexto + FontAssets.MouseText.Value.MeasureString(_nombre).X * EscalaTexto
					+ MargenDerecho;
			}
		}

		/// <summary>Cuantas lineas ocupa el nombre ahora mismo. 1 = se lee de un vistazo, que es lo
		/// que se persigue; mas de 1 sigue siendo legible (nunca se recorta), solo mas alto.</summary>
		public int LineasDelNombre {
			get { return _nombrePartido.Split('\n').Length; }
		}

		/// <summary>El nombre YA envuelto tal cual se dibuja, para que el arnes de pruebas mida lo
		/// mismo que se ve y no una reconstruccion suya.</summary>
		public string NombrePartido {
			get { return _nombrePartido; }
		}

		/// <summary>Ancho real disponible para el texto dentro de la fila ahora mismo.</summary>
		public float AnchoTextoDisponible {
			get {
				float ancho = GetDimensions().Width;
				return (ancho > 0f ? ancho : _anchoUsado) - XTexto - MargenDerecho;
			}
		}

		/// <summary>
		/// Reenvuelve el nombre si el ancho REAL de la fila (ya dibujada) ha cambiado desde la
		/// ultima vez - por un cambio de resolucion, o porque la columna de carpetas se ha
		/// redimensionado al abrir otra carpeta. Devuelve true si de verdad ha cambiado algo, para
		/// que quien llame pida un <c>Recalculate</c> de la lista solo cuando hace falta.
		/// </summary>
		public bool AjustarAlAnchoReal()
		{
			float ancho = GetDimensions().Width;
			if (ancho <= 0f || Math.Abs(ancho - _anchoUsado) < 0.5f) {
				return false;
			}
			Envolver(ancho);
			return true;
		}

		private void Envolver(float ancho)
		{
			_anchoUsado = ancho;
			float anchoTexto = ancho - XTexto - MargenDerecho;
			_nombrePartido = EtiquetaTk.PartirEnLineas(_nombre, anchoTexto > 10f ? anchoTexto : ancho, EscalaTexto);
			_altoTexto = FontAssets.MouseText.Value.MeasureString(_nombrePartido).Y * EscalaTexto;
			Height.Set(Math.Max(AltoMinimo, _altoTexto + _relleno), 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			BackgroundColor = Activa
				? EstiloTk.BotonActivo
				: (IsMouseHovering ? EstiloTk.BotonSobre : EstiloTk.FondoCaja);

			base.DrawSelf(spriteBatch);

			if (IsMouseHovering && !PlayerInput.IgnoreMouseInterface) {
				Main.LocalPlayer.mouseInterface = true;
			}

			CalculatedStyle dim = GetDimensions();

			bool hayIcono = DibujarIcono(spriteBatch,
				new Vector2(dim.X + 6f + LadoIcono / 2f, dim.Y + dim.Height / 2f));

			float xTexto = dim.X + (hayIcono ? 6f + LadoIcono + 8f : 10f);
			float yTexto = dim.Y + (dim.Height - _altoTexto) / 2f;
			Utils.DrawBorderString(spriteBatch, _nombrePartido,
				new Vector2(xTexto, yTexto), Color.White, EscalaTexto);

			if (_tieneHijas) {
				Utils.DrawBorderString(spriteBatch, ">",
					new Vector2(dim.X + dim.Width - 16f, dim.Y + dim.Height / 2f - 10f),
					EstiloTk.TextoSuave, 0.8f);
			}

			if (IsMouseHovering) {
				Main.instance.MouseText(_tieneHijas
					? Idiomas.Texto("Personaje.Buffs.Arbol.CarpetaConSubcarpetas", _nombre, _buffs, Nodo.Children.Count)
					: Idiomas.Texto("Personaje.Buffs.Arbol.CarpetaBuffs", _nombre, _buffs));
			}
		}

		/// <summary>Dibuja el icono real del primer buff de la carpeta, centrado en <paramref
		/// name="centro"/>. Mismas comprobaciones defensivas que <see cref="IconoBuffTk"/>: un
		/// recurso puede no estar cargado todavia, o el array puede no llegar a ese indice.</summary>
		private bool DibujarIcono(SpriteBatch spriteBatch, Vector2 centro)
		{
			if (_iconoTipo <= 0 || TextureAssets.Buff == null || _iconoTipo >= TextureAssets.Buff.Length) {
				return false;
			}

			var recurso = TextureAssets.Buff[_iconoTipo];
			if (recurso == null || !recurso.IsLoaded) {
				return false;
			}

			Texture2D textura = recurso.Value;
			float escala = LadoIcono / Math.Max(textura.Width, textura.Height);
			if (escala > 1f) {
				escala = 1f;
			}

			spriteBatch.Draw(textura,
				new Vector2(centro.X - textura.Width * escala / 2f, centro.Y - textura.Height * escala / 2f),
				null, Color.White, 0f, Vector2.Zero, escala, SpriteEffects.None, 0f);
			return true;
		}

	}
}
