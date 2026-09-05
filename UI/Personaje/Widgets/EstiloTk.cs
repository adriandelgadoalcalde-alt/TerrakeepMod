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

		/// <summary>Color del texto secundario (etiquetas, unidades, avisos suaves).</summary>
		public static readonly Color TextoSuave = new Color(190, 200, 225);

		/// <summary>Color de un aviso importante.</summary>
		public static readonly Color TextoAviso = new Color(255, 210, 120);

		/// <summary>Lado de un slot de objeto a escala 1 (el tamaño real de la textura vanilla).</summary>
		public const float LadoSlot = 52f;
	}
}
