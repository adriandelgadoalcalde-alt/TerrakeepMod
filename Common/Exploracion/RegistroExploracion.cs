using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Escribe las lineas de evidencia de WS6 (Exploracion). Va siempre al log del juego
	/// (<c>tModLoader-Logs\client.log</c>, con el prefijo <c>[Terrakeep]</c>) y, ADEMAS, a un
	/// archivo propio dentro de la carpeta de guardado que se este usando.
	/// </summary>
	/// <remarks>
	/// Mismo motivo que documento WS4 en <c>RegistroBuilds</c>, y aqui es todavia mas necesario:
	/// el <c>client.log</c> es <b>uno solo para todas las instancias</b> del juego y tModLoader lo
	/// rota al arrancar (<c>client.log</c> -&gt; <c>client1.log</c>). Mientras se construia este
	/// workstream habia otros dos agentes (WS3 y WS5) lanzando el juego a la vez, asi que leer la
	/// evidencia del log compartido es una carrera perdida de antemano. El archivo vive en
	/// <c>Main.SavePath</c>, que es justo lo que cambia <c>-tmlsavedirectory</c>, o sea que es
	/// privado de cada ejecucion.
	/// <para />
	/// Solo se escribe cuando hay una autoprueba en marcha: jugando normalmente no tiene ningun
	/// sentido dejar un archivo suelto en la carpeta de guardado del usuario.
	/// </remarks>
	public static class RegistroExploracion
	{
		/// <summary>Nombre del archivo de evidencia dentro de la carpeta de guardado.</summary>
		public const string NombreArchivo = "terrakeep-ws6-evidencia.log";

		private static bool _archivoIniciado;

		/// <summary>
		/// El mod, para poder escribir en su log. Lo rellena
		/// <see cref="PanelExploracionSystem.Load"/>: no vale <c>Terrakeep.Instance</c> porque
		/// tModLoader ejecuta <c>Mod.Autoload()</c> (donde corren los <c>ModSystem.Load()</c>)
		/// ANTES de <c>Mod.Load()</c>, que es donde esa propiedad se asigna.
		/// </summary>
		public static Mod Mod;

		private static Mod ModUtil => Mod ?? Terrakeep.Instance;

		/// <summary>Una linea de evidencia, ya con el prefijo <c>[Terrakeep]</c>.</summary>
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
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PanelExploracionSystem.VariableAutoprueba))) {
				return;
			}

			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta,
						"# Evidencia de WS6 (Exploracion) - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
				}
				File.AppendAllText(ruta, "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + linea + Environment.NewLine);
			}
			catch (Exception) {
				// La evidencia del log del juego ya esta escrita; si el archivo no se puede
				// escribir (permisos, disco), no vale la pena tumbar nada por ello.
			}
		}
	}
}
