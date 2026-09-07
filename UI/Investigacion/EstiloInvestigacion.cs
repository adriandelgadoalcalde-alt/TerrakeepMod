using Microsoft.Xna.Framework;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Investigacion
{
	/// <summary>
	/// Los pocos colores propios del panel de Investigacion, encima de la paleta comun del mod
	/// (<see cref="EstiloTk"/>, de WS1, que NO se toca desde aqui).
	/// <para />
	/// Existe para que los tres estados de investigacion - hecho, a medias, sin empezar - se vean
	/// igual en los tres sitios donde aparecen (la fila de carpeta, la fila de objeto y el fondo
	/// del slot), y para que el verde y el gris sean los MISMOS que ya usa el panel de Builds en
	/// su "ya lo tienes": el mod tiene que parecer una sola aplicacion, no seis.
	/// </summary>
	public static class EstiloInvestigacion
	{
		// Los tres estados salen ya de la paleta comun (EstiloTk): antes estaban copiados a mano
		// con los mismos valores que usaba Builds, o sea dos fuentes de verdad para el mismo tono.
		// Se dejan como alias con nombre propio porque aqui se leen mejor asi.

		/// <summary>Investigado del todo. Mismo verde que el "ya lo tienes" de Builds.</summary>
		public static readonly Color Hecho = EstiloTk.Correcto;

		/// <summary>Empezado pero sin terminar. Mismo ambar que los avisos del mod.</summary>
		public static readonly Color AMedias = EstiloTk.TextoAviso;

		/// <summary>Sin empezar. Mismo gris que el "no lo tienes" de Builds.</summary>
		public static readonly Color SinEmpezar = EstiloTk.Neutro;

		/// <summary>Rojo de aviso serio (no estas en Modo Viaje, accion destructiva).</summary>
		public static readonly Color Peligro = EstiloTk.Peligro;

		/// <summary>Fondo del canal vacio de una barra de progreso.</summary>
		public static readonly Color CanalBarra = new Color(24, 30, 56);

		/// <summary>Borde de una barra de progreso.</summary>
		public static readonly Color BordeBarra = new Color(20, 25, 45);

		/// <summary>Fondo del slot de un objeto investigado del todo.</summary>
		public static readonly Color FondoSlotHecho = new Color(70, 170, 90);

		/// <summary>Fondo del slot de un objeto empezado.</summary>
		public static readonly Color FondoSlotAMedias = new Color(150, 120, 60);

		/// <summary>Fondo del slot de un objeto sin empezar.</summary>
		public static readonly Color FondoSlotSinEmpezar = new Color(90, 90, 110);

		/// <summary>Icono atenuado de un objeto sin investigar.</summary>
		public static readonly Color IconoSinEmpezar = new Color(140, 140, 140, 200);

		/// <summary>Fondo de la fila seleccionada del arbol.</summary>
		public static readonly Color FilaSeleccionada = new Color(88, 112, 194) * 0.85f;

		/// <summary>Fondo de una fila del arbol con el raton encima.</summary>
		public static readonly Color FilaSobre = new Color(63, 82, 151) * 0.7f;

		/// <summary>Color con el que se pinta el estado de un conjunto de objetos.</summary>
		public static Color ColorDeEstado(int hechos, int total)
		{
			if (total <= 0) {
				return SinEmpezar;
			}
			if (hechos >= total) {
				return Hecho;
			}
			return hechos > 0 ? AMedias : SinEmpezar;
		}

	}
}
