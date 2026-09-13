using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Evidencia de la Guia: al log del juego (<c>client.log</c>, con el prefijo
	/// <c>[Terrakeep]</c>) y, ademas, a un archivo propio dentro de la carpeta de guardado que se
	/// este usando.
	/// </summary>
	/// <remarks>
	/// Mismo mecanismo y mismo motivo que <c>RegistroInvestigacion</c>: el <c>client.log</c> es
	/// UNO para todas las instancias y tModLoader lo rota al arrancar, asi que dos
	/// verificaciones que se solapen se pisan la evidencia. Como cada verificacion usa su propia
	/// carpeta de guardado (<c>Main.SavePath</c>, que es lo que cambia
	/// <c>-tmlsavedirectory</c>), escribir ahi la hace inmune a esa carrera.
	/// <para />
	/// El archivo solo se escribe con la autoprueba en marcha: jugando normal el mod no deja
	/// ningun archivo suelto en la carpeta del usuario.
	/// </remarks>
	public static class RegistroGuia
	{
		public const string NombreArchivo = "terrakeep-guia-evidencia.log";

		private static bool _archivoIniciado;

		/// <summary>El mod, para poder escribir en su log. Lo rellena <c>GuiaSystem.Load</c>: no
		/// vale <c>Terrakeep.Instance</c> porque tModLoader ejecuta los <c>ModSystem.Load()</c>
		/// ANTES de <c>Mod.Load()</c>, que es donde esa propiedad se asigna (mismo hallazgo que ya
		/// dejaron escrito WS4 y WS5).</summary>
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
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaGuia.Variable))) {
				return;
			}

			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta,
						"# Evidencia de la Guia en tiempo real - " +
						DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
				}
				File.AppendAllText(ruta,
					"[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + linea + Environment.NewLine);
			}
			catch (Exception) {
				// La evidencia del log del juego ya esta escrita; si el archivo no se puede
				// escribir (permisos, disco) no vale la pena tumbar nada por ello.
			}
		}
	}
}
