using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>
	/// Escribe las lineas de evidencia de WS4. Va siempre al log del juego
	/// (<c>tModLoader-Logs\client.log</c>, con el prefijo <c>[Terrakeep]</c>) y, ADEMAS, a un
	/// archivo propio dentro de la carpeta de guardado que se este usando.
	/// </summary>
	/// <remarks>
	/// El archivo propio no es un capricho. El log del juego es <b>uno solo para todas las
	/// instancias</b> y tModLoader lo rota al arrancar (<c>client.log</c> -&gt;
	/// <c>client1.log</c>). Construyendo varios workstreams en paralelo sobre este mismo repo,
	/// dos verificaciones que se solapan se pisan la evidencia: paso dos veces seguidas mientras
	/// se cerraba WS4 (el <c>client.log</c> acabo siendo el de WS1 la primera vez y el de WS7 la
	/// segunda, con el <c>-tmlsavedirectory</c> de esos workstreams en su cabecera). Como cada
	/// verificacion usa su propia carpeta de guardado (<c>Main.SavePath</c>, que es justo lo que
	/// cambia <c>-tmlsavedirectory</c>), escribir ahi hace la evidencia inmune a esas carreras.
	/// <para />
	/// Solo se escribe el archivo cuando hay una autoprueba en marcha: jugando normalmente no
	/// tiene ningun sentido dejar un archivo suelto en la carpeta de guardado del usuario.
	/// </remarks>
	public static class RegistroBuilds
	{
		/// <summary>Nombre del archivo de evidencia dentro de la carpeta de guardado.</summary>
		public const string NombreArchivo = "terrakeep-ws4-evidencia.log";

		private static bool _archivoIniciado;

		/// <summary>
		/// El mod, para poder escribir en su log. Lo rellena <c>PanelBuildsSystem.Load()</c>: no
		/// vale <c>Terrakeep.Instance</c> porque tModLoader ejecuta <c>Mod.Autoload()</c> (que es
		/// donde corren los <c>ModSystem.Load()</c>) ANTES de <c>Mod.Load()</c>, que es donde esa
		/// propiedad se asigna - codigo real de <c>ModContent</c>, lineas 488-489.
		/// </summary>
		public static Mod Mod;

		private static Mod ModUtil => Mod ?? Terrakeep.Instance;

		/// <summary>Una linea de evidencia. Se le pasa ya con el prefijo <c>[Terrakeep]</c>, igual
		/// que se le pasaria al Logger del mod.</summary>
		public static void Linea(string linea)
		{
			ModUtil?.Logger.Info(linea);
			EscribirEnArchivo(linea);
		}

		public static void Aviso(string linea)
		{
			ModUtil?.Logger.Warn(linea);
			EscribirEnArchivo(linea);
		}

		public static void Error(string linea)
		{
			ModUtil?.Logger.Error(linea);
			EscribirEnArchivo(linea);
		}

		private static void EscribirEnArchivo(string linea)
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PanelBuildsSystem.VariableAutoprueba))) {
				return;
			}

			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta, $"# Evidencia de WS4 (Builds) - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
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
