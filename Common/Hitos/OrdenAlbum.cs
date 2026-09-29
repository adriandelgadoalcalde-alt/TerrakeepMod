using System;
using System.Collections.Generic;
using System.Globalization;

namespace TerrakeepMod.Common.Hitos
{
	/// <summary>
	/// Lógica PURA del orden del álbum de hitos (sin ninguna dependencia de Terraria, para poder
	/// probarla con <c>dotnet test</c> desde <c>TerrakeepMod.Tests</c>, enlazada vía
	/// <c>TerrakeepMod.LogicaPura</c> igual que <c>GramaticaBusqueda.cs</c>/<c>ReflowVertical.cs</c>).
	/// </summary>
	/// <remarks>
	/// Bug real (29-sep-2026, visto en la captura del README): <c>AlbumHitos.Listar</c> se limitaba a
	/// dar la vuelta al orden de <c>album.json</c> suponiendo que el orden de inserción ES el
	/// cronológico. No lo es en cuanto el índice llega de otra forma que no sea "un hito cada vez, en
	/// orden" (álbum copiado/fusionado entre perfiles, reloj del sistema cambiado, índice editado a
	/// mano): la pestaña enseñaba fechas del 29/09, 15/09, 20/09, 16/09... mezcladas. Ahora se ordena
	/// de verdad por fecha, más reciente primero; a igual fecha manda el orden de inserción (la
	/// añadida después, arriba), que es lo que ya hacía el <c>Reverse</c> de antes.
	/// </remarks>
	public static class OrdenAlbum
	{
		/// <summary>Formato exacto con el que <c>AlbumHitos</c> escribe la fecha en <c>album.json</c>.</summary>
		public const string FormatoFecha = "yyyy-MM-dd HH:mm:ss";

		/// <summary>
		/// Lee la fecha de una entrada del índice. Primero el formato exacto que escribe el mod
		/// (cultura invariante: no depende del idioma del sistema), después cualquier fecha ISO
		/// razonable. Si no hay forma, devuelve false y <see cref="DateTime.MinValue"/>: la entrada
		/// se va al FINAL de la lista, nunca arriba del todo con la hora actual como antes.
		/// </summary>
		public static bool IntentarLeerFecha(string texto, out DateTime fecha)
		{
			if (!string.IsNullOrWhiteSpace(texto)) {
				if (DateTime.TryParseExact(texto, FormatoFecha, CultureInfo.InvariantCulture,
						DateTimeStyles.None, out fecha)) {
					return true;
				}
				if (DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)) {
					return true;
				}
			}
			fecha = DateTime.MinValue;
			return false;
		}

		/// <summary>
		/// Devuelve una lista NUEVA con las entradas de la más reciente a la más antigua. Estable
		/// respecto al orden de inserción invertido: a igual fecha, la que se añadió después va antes.
		/// </summary>
		public static List<T> MasRecientePrimero<T>(IReadOnlyList<T> entradas, Func<T, DateTime> fecha)
		{
			if (entradas == null) {
				throw new ArgumentNullException(nameof(entradas));
			}
			if (fecha == null) {
				throw new ArgumentNullException(nameof(fecha));
			}

			List<(T Entrada, DateTime Fecha, int Indice)> conIndice =
				new List<(T Entrada, DateTime Fecha, int Indice)>(entradas.Count);
			for (int i = 0; i < entradas.Count; i++) {
				conIndice.Add((entradas[i], fecha(entradas[i]), i));
			}

			conIndice.Sort((a, b) => {
				int porFecha = b.Fecha.CompareTo(a.Fecha);
				return porFecha != 0 ? porFecha : b.Indice.CompareTo(a.Indice);
			});

			List<T> salida = new List<T>(conIndice.Count);
			foreach ((T Entrada, DateTime Fecha, int Indice) elemento in conIndice) {
				salida.Add(elemento.Entrada);
			}
			return salida;
		}
	}
}
