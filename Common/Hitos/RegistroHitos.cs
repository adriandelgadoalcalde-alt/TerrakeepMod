using System;
using System.IO;
using Terraria;

namespace TerrakeepMod.Common.Hitos
{
	/// <summary>
	/// Evidencia de las capturas automáticas de hito: log del juego (<c>client.log</c>, prefijo
	/// <c>[Terrakeep]</c>) y, además, un archivo propio dentro de la carpeta de guardado activa.
	/// </summary>
	/// <remarks>
	/// Mismo mecanismo que <c>TerrakeepMod.Common.Panel.RegistroPanel</c> (evitar que el
	/// <c>client.log</c> compartido se pierda entre varias instancias probando a la vez), con una
	/// diferencia a propósito: <c>RegistroPanel</c> solo escribe su archivo durante una autoprueba
	/// (variable de entorno puesta); este SIEMPRE escribe el suyo, porque a diferencia de aquel esto
	/// no es un arnés de pruebas - es la evidencia real de que el álbum del jugador se está
	/// llenando solo, jugando normal, y tiene que quedar constancia aunque no haya ninguna
	/// autoprueba de por medio.
	/// </remarks>
	public static class RegistroHitos
	{
		/// <summary>Nombre del archivo de evidencia dentro de la carpeta de guardado.</summary>
		public const string NombreArchivo = "terrakeep-hitos-evidencia.log";

		private static bool _archivoIniciado;

		public static void Linea(string linea)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(linea);
			}
			EscribirEnArchivo(linea);
		}

		public static void Aviso(string linea)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Warn(linea);
			}
			EscribirEnArchivo("AVISO: " + linea);
		}

		private static void EscribirEnArchivo(string linea)
		{
			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					Directory.CreateDirectory(Main.SavePath);
					File.WriteAllText(ruta,
						$"# Evidencia de capturas automáticas de hito - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
				}
				File.AppendAllText(ruta, $"[{DateTime.Now:HH:mm:ss.fff}] {linea}{Environment.NewLine}");
			}
			catch (Exception) {
				// Igual que RegistroPanel: si el archivo no se puede escribir (permisos, disco), no
				// vale la pena tumbar nada por ello - el log del juego ya tiene la línea.
			}
		}
	}
}
