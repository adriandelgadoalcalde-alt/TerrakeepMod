using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// El marcador de la BRUJULA de la Guia: donde esta, DE VERDAD, lo que ya has explorado del
	/// objetivo actual del tramo activo. Vive aparte de <see cref="MarcadoresExploracion"/> (la
	/// busqueda manual de la pestaña Exploracion) a proposito, para que abrir la Guia nunca borre
	/// una busqueda que el jugador tenga en marcha, y viceversa - <see cref="CapaMapaExploracion"/>
	/// dibuja los dos conjuntos a la vez, con colores distintos.
	/// </summary>
	/// <remarks>
	/// <b>Brujula, no GPS, tambien aqui.</b> Quien rellena esto (<c>Common.Guia.BrujulaGuia</c>)
	/// busca SOLO dentro de lo que el propio jugador ya ha revelado en su mapa
	/// (<c>BuscadorMundo</c> con <c>soloExplorado=true</c>). Si el objetivo esta en una zona que
	/// todavia no has visto, aqui no hay nada que enseñar - a proposito: un marcador en un sitio sin
	/// explorar seria justo el "GPS que te lleva de la mano" que el diseño prohibe. Lo que SI hace
	/// es recordarte donde esta lo que ya encontraste, como cualquier mapa de Cyberpunk/Witcher
	/// 3/Souls marca un sitio ya visitado.
	/// </remarks>
	public static class MarcadoresGuia
	{
		/// <summary>El unico marcador (el hallazgo mas cercano al jugador de lo que ya exploro), o
		/// vacio si el objetivo actual no tiene brujula (la mayoria de zonas: ver
		/// <c>BrujulaGuia.ObjetivoParaZona</c>) o si todavia no se ha explorado nada de esa zona.</summary>
		public static List<ResultadoBusqueda> Resultados = new List<ResultadoBusqueda>();

		/// <summary>Color de la brujula: dorado, deliberadamente distinto de los colores por
		/// categoria de <c>PanelExploracionSystem.ColorDeCategoria</c> (ambar/moradas/rosas/azules)
		/// para que un jugador que tenga las dos cosas en pantalla a la vez sepa cual es cual.</summary>
		public static readonly Color Color = new Color(255, 230, 60);

		public static string Titulo = "";

		public static void Fijar(string titulo, List<ResultadoBusqueda> resultados)
		{
			Titulo = titulo ?? "";
			Resultados = resultados ?? new List<ResultadoBusqueda>();
		}

		public static void Limpiar()
		{
			Titulo = "";
			Resultados = new List<ResultadoBusqueda>();
		}

		public static bool HayAlgo => Resultados != null && Resultados.Count > 0;
	}
}
