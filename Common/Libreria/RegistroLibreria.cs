using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Libreria
{
	/// <summary>
	/// Escribe la evidencia de WS3. Va siempre al log del juego
	/// (<c>tModLoader-Logs\client.log</c>, con el prefijo <c>[Terrakeep]</c>) y, ADEMAS, a un
	/// archivo propio dentro de la carpeta de guardado que se este usando.
	/// </summary>
	/// <remarks>
	/// El archivo propio es la solucion que ya encontro WS4 a un problema real y medido: el
	/// <c>client.log</c> del juego es <b>uno solo para todas las instancias</b> y tModLoader lo
	/// rota al arrancar (<c>client.log</c> -&gt; <c>client1.log</c>), asi que con varios agentes
	/// probando el juego a la vez, la evidencia de una prueba se la lleva por delante la de otra.
	/// Como cada verificacion usa su propia carpeta de guardado (<c>Main.SavePath</c>, que es lo
	/// que cambia <c>-tmlsavedirectory</c>), escribir ahi la hace inmune a esas carreras. WS3
	/// tiene su propio nombre de archivo para no pisar el de WS4 ni el de nadie.
	/// <para />
	/// Solo se escribe el archivo cuando hay una autoprueba en marcha: jugando normal no tiene
	/// ningun sentido dejar un archivo suelto en la carpeta de guardado del usuario.
	/// </remarks>
	public static class RegistroLibreria
	{
		/// <summary>Nombre del archivo de evidencia dentro de la carpeta de guardado.</summary>
		public const string NombreArchivo = "terrakeep-ws3-evidencia.log";

		private static bool _archivoIniciado;

		/// <summary>
		/// El mod, para poder escribir en su log. Lo rellena <c>PanelLibreriaSystem.Load()</c>: no
		/// vale <c>Terrakeep.Instance</c> porque tModLoader ejecuta los <c>ModSystem.Load()</c>
		/// ANTES de <c>Mod.Load()</c>, que es donde esa propiedad se asigna.
		/// </summary>
		public static Mod Mod;

		private static Mod ModUtil {
			get { return Mod ?? Terrakeep.Instance; }
		}

		public static void Linea(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Info(linea);
			}
			EscribirEnArchivo(linea);
		}

		public static void Aviso(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Warn(linea);
			}
			EscribirEnArchivo(linea);
		}

		public static void Error(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Error(linea);
			}
			EscribirEnArchivo(linea);
		}

		private static void EscribirEnArchivo(string linea)
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PanelLibreriaSystem.VariableAutoprueba))) {
				return;
			}

			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta,
						$"# Evidencia de WS3 (Libreria) - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
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
