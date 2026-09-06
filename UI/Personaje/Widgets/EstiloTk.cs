using Microsoft.Xna.Framework;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Paleta y medidas comunes del panel de Personaje. Se centralizan aqui para que todas las
	/// pestañas se vean como una sola cosa y para no repetir constantes por todo el codigo.
	/// <para />
	/// Los tonos son los del propio Terraria (los azules del inventario y de los menus), no
	/// inventados: el mod tiene que parecer parte del juego, no una ventana ajena.
	/// </summary>
	public static class EstiloTk
	{
		/// <summary>Fondo del marco grande del panel.</summary>
		public static readonly Color FondoPanel = new Color(33, 43, 79) * 0.94f;

		/// <summary>Fondo de una caja secundaria dentro del panel (cabecera, cajas de pestaña).</summary>
		public static readonly Color FondoCaja = new Color(43, 56, 101) * 0.92f;

		/// <summary>Fondo normal de un boton.</summary>
		public static readonly Color BotonNormal = new Color(63, 82, 151) * 0.92f;

		/// <summary>Fondo de un boton con el raton encima.</summary>
		public static readonly Color BotonSobre = new Color(88, 112, 194) * 0.95f;

		/// <summary>Fondo de un boton activo (pestaña seleccionada, loadout actual...).</summary>
		public static readonly Color BotonActivo = new Color(129, 163, 245) * 0.95f;

		/// <summary>Fondo de un boton deshabilitado.</summary>
		public static readonly Color BotonApagado = new Color(50, 55, 74) * 0.9f;

		/// <summary>Borde que aparece al pasar el raton por un boton. Se dibuja con la textura
		/// <c>Images/UI/PanelBorder</c> del propio juego, no con un rectangulo propio.</summary>
		public static readonly Color BordeSobre = new Color(200, 220, 255);

		/// <summary>Color del texto secundario (etiquetas, unidades, avisos suaves).</summary>
		public static readonly Color TextoSuave = new Color(190, 200, 225);

		/// <summary>Color de un aviso importante.</summary>
		public static readonly Color TextoAviso = new Color(255, 210, 120);

		/// <summary>Lado de un slot de objeto a escala 1 (el tamaño real de la textura vanilla).</summary>
		public const float LadoSlot = 52f;

		// -------------------------------------------------------------------------------------
		// Acentos comunes a todas las areas del panel unico.
		//
		// Se centralizaron aqui al fusionar los seis paneles: hasta entonces el verde/gris/rojo
		// del "ya lo tienes" de Builds y los de Investigacion estaban DUPLICADOS en dos archivos
		// distintos con los mismos valores copiados a mano (EstiloInvestigacion los copio de
		// PanelBuildsState). Con dos copias, cambiar un tono en un sitio y no en el otro solo era
		// cuestion de tiempo.
		// -------------------------------------------------------------------------------------

		/// <summary>Verde de "hecho / ya lo tienes".</summary>
		public static readonly Color Correcto = new Color(140, 235, 160);

		/// <summary>Gris de "no lo tienes / sin empezar".</summary>
		public static readonly Color Neutro = new Color(170, 170, 180);

		/// <summary>Rojo de "no existe aqui / accion delicada".</summary>
		public static readonly Color Peligro = new Color(235, 130, 130);

		// -------------------------------------------------------------------------------------
		// Medidas comunes de la barra de pestañas. Las tres areas con sub-pestañas (Personaje,
		// Exploracion) y la barra principal del panel unico usan las mismas, para que las dos
		// filas de pestañas se vean como una sola familia y no como dos barras distintas.
		// -------------------------------------------------------------------------------------

		/// <summary>Alto de un boton de pestaña.</summary>
		public const float AltoPestana = 30f;

		/// <summary>Separacion horizontal entre pestañas.</summary>
		public const float SeparacionPestanas = 6f;

		/// <summary>Escala de texto de un boton de pestaña.</summary>
		public const float EscalaPestana = 0.8f;

		/// <summary>Escala de texto de un boton normal.</summary>
		public const float EscalaBoton = 0.8f;
	}
}
