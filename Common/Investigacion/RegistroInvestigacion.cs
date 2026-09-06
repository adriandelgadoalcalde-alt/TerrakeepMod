using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Investigacion
{
	/// <summary>
	/// Escribe las lineas de evidencia de WS5 (Investigacion). Van siempre al log del juego
	/// (<c>tModLoader-Logs\client.log</c>, con el prefijo <c>[Terrakeep]</c>) y, ADEMAS, a un
	/// archivo propio dentro de la carpeta de guardado que se este usando.
	/// </summary>
	/// <remarks>
	/// El archivo propio lo hereda de WS4 (<c>RegistroBuilds</c>) y por el mismo motivo real,
	/// medido en este proyecto: el log del juego es <b>uno solo para todas las instancias</b> y
	/// tModLoader lo rota al arrancar (<c>client.log</c> -&gt; <c>client1.log</c>), asi que con
	/// varios agentes construyendo workstreams en paralelo y lanzando el juego a la vez, dos
	/// verificaciones que se solapan se pisan la evidencia. Como cada verificacion usa su propia
	/// carpeta de guardado (<c>Main.SavePath</c>, que es justo lo que cambia
	/// <c>-tmlsavedirectory</c>), escribir ahi la hace inmune a esas carreras. El nombre del
	/// archivo es distinto del de WS4 a proposito, aunque ya vayan a carpetas distintas.
	/// <para />
	/// Solo se escribe el archivo cuando hay una autoprueba en marcha: jugando normalmente no
	/// tiene ningun sentido dejar un archivo suelto en la carpeta de guardado del usuario.
	/// </remarks>
	public static class RegistroInvestigacion
	{
		/// <summary>Nombre del archivo de evidencia dentro de la carpeta de guardado.</summary>
		public const string NombreArchivo = "terrakeep-ws5-evidencia.log";

		private static bool _archivoIniciado;

		/// <summary>
		/// El mod, para poder escribir en su log. Lo rellena
		/// <see cref="PanelInvestigacionSystem.Load"/>: no vale <c>Terrakeep.Instance</c> porque
		/// tModLoader ejecuta <c>Mod.Autoload()</c> (que es donde corren los
		/// <c>ModSystem.Load()</c>) ANTES de <c>Mod.Load()</c>, que es donde esa propiedad se
		/// asigna - mismo hallazgo que ya dejo anotado WS4.
		/// </summary>
		public static Mod Mod;

		private static Mod ModUtil => Mod ?? Terrakeep.Instance;

		/// <summary>Una linea de evidencia, ya con el prefijo <c>[Terrakeep]</c>.</summary>
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
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PanelInvestigacionSystem.VariableAutoprueba))) {
				return;
			}

			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta,
						$"# Evidencia de WS5 (Investigacion) - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
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
