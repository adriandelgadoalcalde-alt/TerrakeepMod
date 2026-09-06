using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// Escribe la evidencia de la verificacion final del panel unico. Va siempre al log del juego
	/// (<c>tModLoader-Logs\client.log</c>, con el prefijo <c>[Terrakeep]</c>) y, ADEMAS, a un
	/// archivo propio dentro de la carpeta de guardado que se este usando.
	/// </summary>
	/// <remarks>
	/// Misma solucion que ya encontraron WS4, WS3, WS5 y WS6 a un problema real y medido: el
	/// <c>client.log</c> del juego es <b>uno solo para todas las instancias</b> y tModLoader lo
	/// rota al arrancar, asi que con varios agentes probando el juego a la vez la evidencia de una
	/// prueba se la lleva por delante la de otra. Como cada verificacion usa su propia carpeta de
	/// guardado (<c>Main.SavePath</c>, que es lo que cambia <c>-tmlsavedirectory</c>), escribir ahi
	/// la hace inmune a esas carreras.
	/// </remarks>
	public static class RegistroPanel
	{
		/// <summary>Nombre del archivo de evidencia dentro de la carpeta de guardado.</summary>
		public const string NombreArchivo = "terrakeep-panel-evidencia.log";

		private static bool _archivoIniciado;

		public static void Linea(string linea)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(linea);
			}
			EscribirEnArchivo(linea);
		}

		private static void EscribirEnArchivo(string linea)
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaPanelUnico.Variable))) {
				return;
			}

			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta,
						$"# Evidencia de la fusion de los seis paneles - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
				}
				File.AppendAllText(ruta, $"[{DateTime.Now:HH:mm:ss.fff}] {linea}{Environment.NewLine}");
			}
			catch (Exception) {
				// La evidencia del log del juego ya esta escrita; si el archivo no se puede
				// escribir (permisos, disco), no vale la pena tumbar nada por ello.
			}
		}
	}
}
