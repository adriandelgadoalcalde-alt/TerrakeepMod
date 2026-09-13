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
		/// <para />
		/// <c>public</c> y no <c>private</c>: <c>SincronizacionSystem</c> reutiliza EXACTAMENTE
		/// esta misma lista para no escribir jamas en el <c>%LOCALAPPDATA%\Terrakeep\settings.json</c>
		/// REAL mientras cualquier autoprueba del mod esta en marcha - hallazgo real de esta
		/// sesion: <c>AutopruebaConjuntos</c> cicla el idioma es-&gt;en-&gt;es para capturar las
		/// dos versiones (mismo patron que ya usaba WS7), y sin este guardado el evento
		/// <c>Idiomas.Cambiado</c> reflejaba ESE cambio de prueba en el settings.json real de esta
		/// maquina - visto en el log real de una pasada de <c>verificar-conjuntos.ps1</c>. La
		/// prueba dejaba el archivo en un estado correcto al final (restauraba el idioma), pero
		/// era pura suerte: cualquier corte a mitad de la prueba lo habria dejado mal.
		/// </summary>
		public static bool Permitida {
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
						Libreria.AuditoriaCategorias.Variable))
					// Guia en tiempo real: sin capturas no habria forma de comprobar la estetica
					// del area nueva (dos columnas de prosa envuelta) ni de ver la barra con SIETE
					// pestañas, que es justo donde un rotulo se sale sin que ningun dato lo diga.
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
						Guia.AutopruebaGuia.Variable))
					// Conjuntos (gestion ampliada de loadouts): la pestaña nueva necesita su
					// propia captura para comprobar de verdad que los botones de renombrar y la
					// lista de presets no se solapan con nada.
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
						Loadouts.LoadoutsSystem.VariableAutoprueba))
					// Completitud (vista de que falta para el 100%): misma necesidad real que
					// Conjuntos - comprobar visualmente las cuatro barras/listas de resumen.
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
						Completitud.CompletitudSystem.VariableAutoprueba))
					// Álbum de hitos: verifica el PANEL en sí (que abre por clic real y que el
					// encabezado y la lista no se solapan). La captura automática de cada hito real
					// no pasa por aquí ni por esta lista - ver CapturaDePantalla.GuardarHito.
					|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
						Hitos.AutopruebaHitos.Variable));
			}
		}

		/// <summary>Guarda el fotograma ya presentado en <c>&lt;guardado&gt;/terrakeep-capturas/
		/// &lt;nombre&gt;.png</c>. Devuelve una linea describiendo lo que ha pasado, para el log.</summary>
		public static string Guardar(string nombre)
		{
			if (!Permitida) {
				return "captura no pedida (sin variable de autoprueba)";
			}

			string ruta = Path.Combine(Main.SavePath, Carpeta, nombre + ".png");
			string resultado = GuardarEnArchivo(ruta);
			return resultado ?? ("captura real del back buffer guardada en \"" + ruta + "\"");
		}

		/// <summary>
		/// Captura del hito real: MISMA tecnica que <see cref="Guardar"/> (back buffer del propio
		/// motor grafico, nunca el escritorio), pero <b>sin pasar por <see cref="Permitida"/></b>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <see cref="Guardar"/> es, a proposito, SOLO ARNES DE PRUEBAS: jugando normal no debe
		/// escribir nada, y esa garantia (documentada arriba, y que reutiliza
		/// <c>SincronizacionSystem</c> para no ensuciar el <c>settings.json</c> real durante una
		/// autoprueba) no se toca aqui.
		/// </para>
		/// <para>
		/// Las capturas de hito son la excepcion real y deliberada: cuando <c>HitosSystem</c> detecta
		/// que un tramo de la Guia se ha cerrado DE VERDAD, jugando normal, tiene que quedar una
		/// imagen aunque no haya ninguna autoprueba en marcha - ese es el objetivo entero de la
		/// funcion. Por eso esto es un metodo aparte y no un cambio en <see cref="Permitida"/>: la
		/// propia autoprueba de la Guia (<c>TERRAKEEP_AUTOTEST_GUIA</c>) fuerza banderas de jefe
		/// reales una a una, asi que sirve tambien para demostrar que esto dispara solo, sin tocar
		/// nada del arnes existente.
		/// </para>
		/// </remarks>
		/// <returns>La ruta completa del .png si salio bien, o null si fallo (con el detalle ya en
		/// el valor de <paramref name="detalle"/>).</returns>
		public static string GuardarHito(string carpetaAbsoluta, string nombreArchivo, out string detalle)
		{
			string ruta = Path.Combine(carpetaAbsoluta, nombreArchivo + ".png");
			string error = GuardarEnArchivo(ruta);
			if (error != null) {
				detalle = error;
				return null;
			}
			detalle = "captura real del back buffer guardada en \"" + ruta + "\"";
			return ruta;
		}

		/// <summary>
		/// El nucleo real, comun a <see cref="Guardar"/> (arnes de pruebas) y
		/// <see cref="GuardarHito"/> (hitos reales de partida): pide el fotograma YA PRESENTADO al
		/// propio <c>GraphicsDevice</c> (<c>GetBackBufferData</c>) y lo vuelca a PNG
		/// (<c>Texture2D.SaveAsPng</c>) - ver la cabecera de esta clase para el porque de esta via y
		/// no <c>CopyFromScreen</c>/<c>PrintWindow</c>, que no sirven en un juego acelerado por GPU.
		/// </summary>
		/// <returns>null si salio bien, o una linea describiendo el fallo.</returns>
		private static string GuardarEnArchivo(string ruta)
		{
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

				Directory.CreateDirectory(Path.GetDirectoryName(ruta));

				using (Texture2D textura = new Texture2D(dispositivo, ancho, alto)) {
					textura.SetData(pixeles);
					using (FileStream archivo = File.Create(ruta)) {
						textura.SaveAsPng(archivo, ancho, alto);
					}
				}

				return null;
			}
			catch (Exception e) {
				return "captura fallida: " + e.GetType().Name + ": " + e.Message;
			}
		}
	}
}
