using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.Common.Prefijos;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Libreria.Widgets
{
	/// <summary>
	/// <b>TM2 del catálogo de rediseño visual ("Editor de objeto flotante"):</b> tarjeta que
	/// flota junto al mini-panel de herramientas de la Librería con el objeto seleccionado a 2x,
	/// su nombre en el color real de su rareza, y los mismos editores de cantidad/prefijo/papelera
	/// ya maduros - solo que más grandes y sueltos del rincón fijo de siempre.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por qué NO sustituye el arrastre a <see cref="SlotSeleccionTk"/> por un clic directo en
	/// un slot real.</b> Investigado antes de escribir código (disciplina de dos fases): todo slot
	/// de objeto REAL de este mod (<c>SlotObjetoVanilla</c>) gestiona el ratón con
	/// <c>ItemSlot.Handle</c>, el mismo código con el que vanilla resuelve clic
	/// izquierdo/derecho/mayús para cualquier ranura del juego - recoger, soltar, apilar, mover
	/// rápido. Reasignar el clic izquierdo de un slot real a "abrir esta tarjeta" rompería esa
	/// recogida, la interacción más usada del panel y la que el propio usuario pidió que
	/// funcionara "arrastrando" (ver la cabecera de <see cref="SlotSeleccionTk"/>: encargo
	/// explícito ya resuelto una vez). Por eso la tarjeta se engancha al mismo gesto de arrastre
	/// que ya existe y funciona, no a uno nuevo que competiría con él.
	/// </para>
	/// <para>
	/// <b>Por qué NO usa "cerrar al pulsar fuera" del catálogo original al pie de la letra.</b>
	/// <see cref="TerrakeepMod.UI.Panel.CapaSuperposicionTk"/> cierra cualquier contenido suyo con
	/// un clic fuera - pero esta tarjeta no es un menú desplegable de un solo uso (como el selector
	/// de prefijo): es el editor ACTIVO de un objeto que sigue "cogido" en
	/// <see cref="SlotSeleccionTk"/> mientras se trabaja con él. Cerrarla con un clic accidental
	/// fuera, sin soltar el objeto, dejaría al jugador sin ver lo que tiene cogido - una sorpresa
	/// real, no una mejora. Se sustituye por un criterio más predecible y honesto con lo que
	/// pasa de verdad: la tarjeta se muestra SIEMPRE que hay algo en el recuadro de selección, y
	/// se esconde sola en cuanto se arrastra de vuelta fuera (<c>PanelHerramientasLibreriaTk.
	/// ActualizarTarjetaFlotante</c>) - ni un evento de clic que rastrear, ni una tarjeta vacía
	/// flotando por error.
	/// <para />
	/// <b>Bug real encontrado DESPUÉS de escribir lo de arriba (bitacora.md, "editor atrapado"/
	/// "trampa tarjeta flotante"):</b> vivir dentro de <c>CapaSuperposicionTk</c> tenía un efecto
	/// secundario que nadie había investigado: esa capa cubre TODA la zona de contenido del panel
	/// (no solo el rectángulo de la tarjeta), así que en cuanto tenía algo dentro se comportaba como
	/// el fondo de un modal de zona completa - cualquier clic en el catálogo/árbol/búsqueda de
	/// debajo, e incluso arrastrar el objeto de vuelta fuera del propio <see cref="SlotSeleccionTk"/>
	/// (el mecanismo de cierre descrito arriba), quedaba bloqueado mientras la tarjeta estuviera
	/// abierta. Arreglado moviendo la tarjeta a <see cref="TerrakeepMod.UI.Panel.HospedajeFlotanteTk"/>,
	/// un hospedaje separado que NO es modal: ver su XMLdoc para el porqué técnico exacto.
	/// </para>
	/// <para>
	/// <b>El prefijo aquí es el editor INTERACTIVO y la tarjeta es el único editor visible
	/// (v0.7.1).</b> Hasta la v0.7.0 esta línea era solo informativa "para no duplicar el mismo
	/// control en dos sitios", pero la cantidad y la papelera SÍ estaban en la tarjeta y en el
	/// mini-panel a la vez: dos editores del mismo objeto en pantalla (visto en la captura del README
	/// de la v0.7.0). Como tarjeta y desplegable ya no se disputan ningún hueco (hospedajes distintos,
	/// y la capa del desplegable va por encima y recibe el ratón primero), el
	/// <see cref="EditorPrefijoTk"/> vive aquí y <see cref="PanelHerramientasLibreriaTk"/> esconde sus
	/// propios editores mientras la tarjeta está abierta. <see cref="TextoPrefijo"/> se conserva para
	/// la autoprueba.
	/// </para>
	/// <para>
	/// <b>Por qué no hay un tercer botón "Quitar" junto a Aplicar/Papelera.</b> El catálogo
	/// original pedía tres botones porque imaginaba un editor anclado a un slot de equipo REAL,
	/// donde "quitar" significa desequipar. Aquí el objeto editado no vive en un slot de equipo:
	/// vive en <see cref="SlotSeleccionTk"/>, que ya lo saca de su hueco de origen al arrastrarlo
	/// (ver la cabecera de esa clase - "arrastrar aquí MUEVE el objeto de verdad"). "Quitar" ya
	/// existe y funciona: es arrastrarlo de vuelta fuera con el mismo <c>ItemSlot.Handle</c>
	/// vanilla, el gesto que el propio usuario pidió explícitamente (ver esa misma cabecera). Un
	/// botón "Quitar" sería una segunda forma de hacer lo mismo que el arrastre ya hace, no una
	/// pieza nueva - por eso no está, no porque falte.
	/// </para>
	/// </remarks>
	public class TarjetaEdicionFlotanteTk : UIPanel
	{
		public const float Ancho = 240f;
		/// <summary>204 hasta la v0.6.2; +28 desde la v0.7.0 para la fila "+10 / +100 / Máx" del
		/// editor de cantidad (paridad con Terrakeep escritorio 3.3.0, ver <see cref="CantidadRapida"/>).
		/// v0.7.1: 236 - el prefijo pasa a ser el EDITOR interactivo (26 px) en vez de una linea
		/// informativa (18 px), porque la tarjeta es ya el UNICO editor visible (ver
		/// <c>PanelHerramientasLibreriaTk.AjustarEditoresDelMiniPanel</c>).</summary>
		public const float Alto = 236f;

		/// <summary>2x real de la escala de ranura que ya usa la rejilla de destino de la Librería
		/// (<c>ContenidoLibreria.EscalaSlotDestino</c> = 0.7f) - "sprite a 2x" tal cual pide TM2.</summary>
		private const float EscalaSprite = 1.4f;

		private readonly Func<Item> _proveedor;
		private readonly Item[] _muestra = new Item[1];
		private readonly EditorCantidadTk _editorCantidad;
		private readonly EditorPrefijoTk _editorPrefijo;
		private readonly SlotPapeleraTk _papelera;

		public TarjetaEdicionFlotanteTk(Func<Item> proveedor)
		{
			_proveedor = proveedor;

			Width.Set(Ancho, 0f);
			Height.Set(Alto, 0f);
			// OPACO (v0.7.1): con EstiloTk.FondoCaja (alfa 0,92) se transparentaban los numeros de
			// las ranuras y la pista de la rejilla de destino que quedan debajo, justo detras del
			// nombre del objeto (visto en la captura del README de la v0.7.0).
			BackgroundColor = new Color(EstiloTk.FondoCaja.R, EstiloTk.FondoCaja.G, EstiloTk.FondoCaja.B, (byte)255);
			// Borde visible, igual que el popup de prefijo: esta tarjeta flota POR ENCIMA de otros
			// controles y sin una línea que la separe se lee como si formara parte de lo de abajo.
			BorderColor = EstiloTk.BordeSobre * 0.55f;
			SetPadding(8f);

			float filaCantidad = EscalaSprite * 52f + 16f;
			_editorCantidad = new EditorCantidadTk(() => _proveedor != null ? _proveedor() : null, Ancho - 16f,
				filaRapida: true);
			_editorCantidad.Top.Set(filaCantidad, 0f);
			Append(_editorCantidad);

			// v0.7.1: el editor de prefijo INTERACTIVO vive aqui (antes era una linea informativa y el
			// editor real seguia en el mini-panel, con la cantidad repetida en los dos sitios a la
			// vez). Su desplegable cuelga de la CapaSuperposicionTk del panel, que va por encima del
			// hospedaje de esta tarjeta y recibe el raton primero (PanelTerrakeepState.OnInitialize).
			float filaPrefijo = filaCantidad + EditorCantidadTk.AltoConFilaRapida + 8f;
			_editorPrefijo = new EditorPrefijoTk(() => _proveedor != null ? _proveedor() : null, Ancho - 16f);
			_editorPrefijo.Top.Set(filaPrefijo, 0f);
			Append(_editorPrefijo);

			float filaPapelera = filaPrefijo + 26f + 10f;
			EtiquetaTk etiquetaPapelera = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Herramientas.Papelera"), 0.65f, 150f, 16f);
			etiquetaPapelera.ColorTexto = EstiloTk.TextoSuave;
			etiquetaPapelera.Left.Set(36f, 0f);
			etiquetaPapelera.Top.Set(filaPapelera + 6f, 0f);
			Append(etiquetaPapelera);

			_papelera = new SlotPapeleraTk(0.6f);
			_papelera.Left.Set(0f, 0f);
			_papelera.Top.Set(filaPapelera, 0f);
			Append(_papelera);
		}

		/// <summary>La papelera de esta tarjeta - la usa <see cref="PanelHerramientasLibreriaTk"/>
		/// para poder vaciarla junto con la del mini-panel fijo cuando hace falta (ver
		/// <c>ActualizarTarjetaFlotante</c>) y la autoprueba para comprobarla en el juego real.</summary>
		public SlotPapeleraTk Papelera => _papelera;

		/// <summary>El editor de cantidad de esta tarjeta (con la fila "+10 / +100 / Máx"), para la
		/// autoprueba.</summary>
		public EditorCantidadTk EditorCantidad => _editorCantidad;

		/// <summary>El editor de prefijo de esta tarjeta, para la autoprueba.</summary>
		public EditorPrefijoTk EditorPrefijo => _editorPrefijo;

		/// <summary>El texto de la línea informativa de prefijo: el que lleva puesto ahora mismo, y
		/// si no coincide con el "mejor prefijo" ya calculado (<see cref="CatalogoMejorPrefijo"/>,
		/// el mismo dato real que ya usa <c>GlobalItemMejorPrefijo</c> para el aviso del tooltip),
		/// también cuál es ese. Público para que la autoprueba pueda leerlo sin duplicar la
		/// resolución.</summary>
		public string TextoPrefijo()
		{
			Item objeto = _proveedor != null ? _proveedor() : null;
			if (objeto == null || objeto.IsAir) {
				return "";
			}

			string actual = objeto.prefix > 0 && objeto.prefix < Lang.prefix.Length
				? Lang.prefix[objeto.prefix].Value
				: Idiomas.Texto("Libreria.Prefijo.Ninguno");

			byte? mejor = CatalogoMejorPrefijo.MejorPrefijo(objeto.type);
			if (!mejor.HasValue || mejor.Value == objeto.prefix) {
				return Idiomas.Texto("Libreria.TarjetaFlotante.Prefijo", actual);
			}

			string nombreMejor = mejor.Value < Lang.prefix.Length ? Lang.prefix[mejor.Value].Value : "";
			return Idiomas.Texto("Libreria.TarjetaFlotante.PrefijoConMejor", actual, nombreMejor);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);

			Item objeto = _proveedor != null ? _proveedor() : null;
			if (objeto == null || objeto.IsAir) {
				return;
			}

			CalculatedStyle dim = GetInnerDimensions();

			// Sprite real a 2x: la MISMA técnica que cualquier ranura del mod (ItemSlot.Draw con
			// Main.inventoryScale ajustado antes de dibujar, nunca una textura suelta) - así se
			// conservan gratis el tinte, el contador de munición/pila y el brillo real de prefijo,
			// en vez de reconstruir a mano lo que el propio juego ya sabe pintar.
			float escalaPrevia = Main.inventoryScale;
			Color fondoPrevio = Main.inventoryBack;
			Main.inventoryScale = EscalaSprite;
			Main.inventoryBack = EstiloTk.FondoCaja;
			_muestra[0] = objeto;
			ItemSlot.Draw(spriteBatch, _muestra, ItemSlot.Context.ChestItem, 0, new Vector2(dim.X, dim.Y));
			Main.inventoryScale = escalaPrevia;
			Main.inventoryBack = fondoPrevio;

			// Nombre, en el color REAL de su rareza (Terraria.GameContent.UI.ItemRarity - la misma
			// tabla con la que el juego pinta el nombre en su propio tooltip, ver el XMLdoc de esta
			// clase). Reduce de escala si hace falta para no recortar - nunca "...".
			float anchoSprite = EscalaSprite * 52f;
			float xNombre = dim.X + anchoSprite + 10f;
			float anchoDisponibleNombre = dim.X + dim.Width - xNombre;

			string nombre = objeto.Name ?? "";
			float escalaNombre = 0.72f;
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			Vector2 tamano = fuente.MeasureString(nombre) * escalaNombre;
			if (anchoDisponibleNombre > 10f && tamano.X > anchoDisponibleNombre) {
				escalaNombre *= anchoDisponibleNombre / tamano.X;
			}

			Color colorNombre = ItemRarity.GetColor(objeto.rare);
			EscribirTk.Dibujar(spriteBatch, nombre, new Vector2(xNombre, dim.Y + 4f), colorNombre, escalaNombre);
		}
	}
}
