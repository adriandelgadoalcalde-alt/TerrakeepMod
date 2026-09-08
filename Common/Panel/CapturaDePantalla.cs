using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE PRUEBAS: guarda una imagen real de lo que hay en pantalla, desde dentro del
	/// propio juego.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que hace falta esto.</b> Hasta ahora, en este proyecto, la evidencia visual era
	/// imposible y esta anotado en la bitacora con las dos vias que se probaron y fallaron:
	/// <c>Graphics.CopyFromScreen</c> (fotografia el escritorio entero, o sea lo que el usuario
	/// tuviera abierto - inservible, y ademas contenido privado) y <c>PrintWindow</c> sobre la
	/// ventana del juego (devuelve <c>true</c> pero la imagen sale <b>negra</b>, que es lo
	/// esperable en una aplicacion acelerada por GPU: FNA dibuja por Direct3D, no por GDI).
	/// </para>
	/// <para>
	/// La via que si funciona es pedirle la imagen al propio motor grafico:
	/// <c>GraphicsDevice.GetBackBufferData&lt;Color&gt;</c> y <c>Texture2D.SaveAsPng</c>, los dos
	/// publicos en la FNA que trae tModLoader (<c>Libraries\FNA\1.0.0\FNA.dll</c>, comprobado con
	/// <c>ilspycmd</c>). Captura exactamente lo que se esta viendo, interfaz de mods incluida, sin
	/// tocar el escritorio del usuario y sin depender de que la ventana tenga el foco.
	/// </para>
	/// <para>
	/// <b>Se llama desde <c>UpdateUI</c>, o sea antes del dibujado del fotograma actual</b>: lo que
	/// se captura es el fotograma ANTERIOR, ya presentado. Para lo que se usa aqui (mirar una
	/// pestaña que lleva varios fotogramas puesta) da igual, pero conviene saberlo.
	/// </para>
	/// <para>
	/// No hace nada si no esta puesta la variable de entorno de la autoprueba: jugando normal el
	/// mod no escribe ninguna imagen en ningun sitio.
	/// </para>
	/// </remarks>
	public static class CapturaDePantalla
	{
		/// <summary>Carpeta, dentro de la carpeta de guardado de la prueba, donde van las imagenes.</summary>
		public const string Carpeta = "terrakeep-capturas";

		/// <summary>
		/// true solo si hay alguna autoprueba de las que piden capturas en marcha. Jugando normal el
		/// mod no escribe ninguna imagen en ningun sitio.
		/// </summary>
		private static bool Permitida {
			get {
				return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaPanelUnico.Variable))
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaIdiomas.Variable))
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Menus.AutopruebaMenus.Variable))
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
						Exploracion.PanelExploracionSystem.VariableAutoprueba))
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaEspaciado.Variable))
					// Añadidas para el arreglo de los 4 bugs de Libreria + rediseño de Personaje (ver
					// bitacora.md): WS1 (Personaje.AutopruebaPersonaje) y WS3
					// (Libreria.PanelLibreriaSystem.VariableAutoprueba, la que enciende
					// AutopruebaLibreria) nunca habian pedido capturas hasta ahora - esta tarea SI las
					// necesita para verificar visualmente el mini-panel reutilizado y el popup de
					// prefijo abriendo a la derecha.
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
						Personaje.AutopruebaPersonaje.Variable))
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
						Libreria.PanelLibreriaSystem.VariableAutoprueba))
					// Recuento de las categorias de la Libreria (arreglo del 8-sep-2026): captura las
					// paginas que antes salian vacias o mezcladas, para poder verlas de verdad.
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
						Libreria.AuditoriaCategorias.Variable));
			}
		}

		/// <summary>Guarda el fotograma ya presentado en <c>&lt;guardado&gt;/terrakeep-capturas/
		/// &lt;nombre&gt;.png</c>. Devuelve una linea describiendo lo que ha pasado, para el log.</summary>
		public static string Guardar(string nombre)
		{
			if (!Permitida) {
				return "captura no pedida (sin variable de autoprueba)";
			}

			try {
				GraphicsDevice dispositivo = Main.instance.GraphicsDevice;
				PresentationParameters parametros = dispositivo.PresentationParameters;
				int ancho = parametros.BackBufferWidth;
				int alto = parametros.BackBufferHeight;
				if (ancho <= 0 || alto <= 0) {
					return "captura imposible: el back buffer mide " + ancho + "x" + alto;
				}

				Color[] pixeles = new Color[ancho * alto];
				dispositivo.GetBackBufferData(pixeles);

				string carpeta = Path.Combine(Main.SavePath, Carpeta);
				Directory.CreateDirectory(carpeta);
				string ruta = Path.Combine(carpeta, nombre + ".png");

				using (Texture2D textura = new Texture2D(dispositivo, ancho, alto)) {
					textura.SetData(pixeles);
					using (FileStream archivo = File.Create(ruta)) {
						textura.SaveAsPng(archivo, ancho, alto);
					}
				}

				return "captura real del back buffer guardada en \"" + ruta + "\" (" + ancho + "x" + alto + ")";
			}
			catch (Exception e) {
				return "captura fallida: " + e.GetType().Name + ": " + e.Message;
			}
		}
	}
}
