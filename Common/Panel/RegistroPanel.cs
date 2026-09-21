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
			// KeepQA V2.0, Fase 6, Bloque B (14-sep-2026): AutopruebaSoak tambien necesita esta
			// evidencia en archivo (Node la sondea desde fuera para saber cuando ha empezado/
			// terminado la sesion de soak, igual que ya hacia AutopruebaPanelUnico via
			// verificar-panel-unico.ps1) - se añade su variable a la comprobacion sin tocar el
			// comportamiento de AutopruebaPanelUnico.
			bool panelActivo = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaPanelUnico.Variable));
			bool soakActivo = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaSoak.Variable));
			// Mismo hueco real ya visto en CapturaDePantalla.Permitida (bitacora.md): cada
			// diagnostico/autoprueba nueva tiene que añadirse aqui A MANO o su evidencia se pierde
			// en silencio (solo llega al client.log compartido de todas las instancias, no al
			// archivo propio aislado). Encontrado esta vez con los dos diagnosticos del 21-sep-2026.
			bool tooltipHuerfanoActivo = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DiagnosticoTooltipHuerfano.Variable));
			bool tituloVecindadActivo = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DiagnosticoTituloYVecindad.Variable));
			if (!panelActivo && !soakActivo && !tooltipHuerfanoActivo && !tituloVecindadActivo) {
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
