using System.Collections.Generic;
using TerrasavrNative.Core.Data;

namespace TerrakeepMod.Common.Libreria
{
	/// <summary>Resultado de una consulta de la Libreria.</summary>
	public sealed class ResultadoBusqueda
	{
		/// <summary>Ids que se enseñan (ya recortados a <see cref="BusquedaLibreria.Maximo"/>).</summary>
		public List<int> Mostrados = new List<int>();

		/// <summary>Cuantos casaban en total, antes de recortar.</summary>
		public int TotalCasados;

		/// <summary>true si se recorto la lista.</summary>
		public bool Recortado {
			get { return TotalCasados > Mostrados.Count; }
		}
	}

	/// <summary>
	/// Consulta de la Libreria: aplica la <see cref="GramaticaBusqueda"/> sobre el ambito que
	/// toque y devuelve los ids a enseñar.
	/// </summary>
	/// <remarks>
	/// <b>El ambito replica el de la app de escritorio, a proposito</b>
	/// (<c>LibraryViewModel.Refresh</c>): con una carpeta abierta se busca DENTRO de esa carpeta,
	/// recorriendo su <c>ItemIdsOrdered</c> (que respeta el orden curado real de Terrasavr, a
	/// diferencia del <c>ItemIdSet</c>, que es un HashSet sin orden garantizado); sin carpeta
	/// abierta se busca sobre el catalogo entero, en orden de id. El tope de 100 resultados es
	/// tambien el real de la app (<c>MaxResults</c>).
	/// </remarks>
	public static class BusquedaLibreria
	{
		/// <summary>Tope de resultados que se enseñan de una vez. Mismo valor que la app de
		/// escritorio: enseñar 8000 slots de golpe no ayuda a nadie y ademas los dibuja todos.</summary>
		public const int Maximo = 100;

		/// <summary>
		/// Ejecuta la consulta.
		/// </summary>
		/// <param name="consulta">Texto del buscador; vacio = sin filtro.</param>
		/// <param name="carpeta">Carpeta abierta, o null para buscar en todo el catalogo.</param>
		public static ResultadoBusqueda Buscar(string consulta, CategoryTreeNodeData carpeta)
		{
			ResultadoBusqueda resultado = new ResultadoBusqueda();
			bool hayConsulta = !string.IsNullOrEmpty(consulta) && consulta.Trim().Length > 0;

			if (carpeta != null) {
				foreach (int id in carpeta.ItemIdsOrdered) {
					if (!hayConsulta || Casa(consulta, id)) {
						Acumular(resultado, id);
					}
				}
			}
			else if (hayConsulta) {
				foreach (LiveItemInfo info in CatalogoVivo.Objetos) {
					if (Casa(consulta, info.Id)) {
						Acumular(resultado, info.Id);
					}
				}
			}
			// Sin carpeta y sin consulta no se enseña nada: el catalogo entero son ~8000 objetos y
			// la pantalla de inicio de la Libreria son las carpetas, igual que en la app.

			return resultado;
		}

		private static bool Casa(string consulta, int id)
		{
			// El tooltip solo se pide (y se pliega) si la consulta lo va a mirar de verdad. Es lo
			// que permite que el catalogo se construya sin tocar los ~8000 tooltips del juego.
			string tooltip = ConsultaMiraTooltip(consulta) ? CatalogoVivo.TooltipPlegado(id) : null;
			return GramaticaBusqueda.Casa(consulta, id, CatalogoVivo.NombrePlegado(id), tooltip);
		}

		/// <summary>true si algun termino de la consulta empieza por "." (busqueda en tooltip).</summary>
		private static bool ConsultaMiraTooltip(string consulta)
		{
			string[] terminos = consulta.Split(',');
			for (int i = 0; i < terminos.Length; i++) {
				string t = terminos[i].Trim();
				if (t.Length >= 2 && t[0] == '.') {
					return true;
				}
			}
			return false;
		}

		private static void Acumular(ResultadoBusqueda resultado, int id)
		{
			resultado.TotalCasados++;
			if (resultado.Mostrados.Count < Maximo) {
				resultado.Mostrados.Add(id);
			}
		}
	}
}
