using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Terraria;
using TerrakeepMod.Common.Builds;

namespace TerrakeepMod.Common.Hitos
{
	/// <summary>Una entrada del álbum: un hito real que el jugador cerró de verdad, con su captura
	/// y el momento exacto en que se disparó.</summary>
	public class EntradaAlbum
	{
		/// <summary>Clave interna del tramo de la Guía que se cerró (ver <c>TramoGuia.Clave</c>).</summary>
		public string Clave = "";

		/// <summary>Nombre del tramo YA TRADUCIDO, tal cual estaba el idioma en el momento de la
		/// captura. Se guarda resuelto (y no la clave) a propósito: es una entrada de diario, no un
		/// dato que tenga que seguir vivo si el jugador cambia de idioma después.</summary>
		public string Nombre = "";

		/// <summary>Nombre del archivo .png, sin ruta (vive en <see cref="AlbumHitos.CarpetaAbsoluta"/>).</summary>
		public string Archivo = "";

		public DateTime Fecha = DateTime.Now;
		public string Personaje = "";
		public string Mundo = "";

		// --- Idea 3 del catalogo de funciones ("Diario de partida automatico"): el hito guarda
		// tambien dia del mundo, equipo llevado, tiempo de sesion y jefe. Los cuatro son
		// OPCIONALES a proposito (valores por defecto = "sin dato"): un album grabado con una
		// version anterior del mod (antes de que existieran estos campos) sigue leyendose sin
		// romper nada, solo enseña estos cuatro vacios/en cero en sus entradas viejas - ver
		// Listar().
		/// <summary>Amaneceres del mundo transcurridos hasta el hito (ver <see cref="ContadorDiasSystem"/>),
		/// o -1 si no se pudo leer.</summary>
		public int DiaDelMundo = -1;

		/// <summary>Resumen de una linea del equipo activo en el momento del hito (mismo formato
		/// real que ya usa <c>AutoEquipar.EstadoEquipo</c> para el log de auto-equipar).</summary>
		public string Equipo = "";

		/// <summary>Segundos reales de ESTA sesion (desde que se cargo la partida) hasta el hito -
		/// nunca el total acumulado de la partida entera, que el mod no puede saber sin guardarlo
		/// el juego por su cuenta.</summary>
		public int TiempoSesionSegundos = -1;

		/// <summary>Nombre del jefe/evento que cierra el tramo, ya traducido, o "" si el tramo no
		/// tiene uno (p.ej. un tramo de progresion sin jefe final).</summary>
		public string Jefe = "";
	}

	/// <summary>
	/// El álbum de hitos: captura automática + índice, todo dentro de
	/// <c>&lt;guardado&gt;/terrakeep-hitos/</c> (mismo criterio que <c>CapturaDePantalla.Carpeta</c>
	/// y <c>RegistroPanel</c>: la carpeta de guardado ACTIVA, para que cada perfil/instalación tenga
	/// su propio álbum sin pisarse con otro).
	/// </summary>
	/// <remarks>
	/// <para>
	/// El índice (<c>album.json</c>) es lo que lee el panel para no tener que enumerar el disco cada
	/// vez que se abre la pestaña "Álbum": una lista de objetos con clave, nombre ya traducido,
	/// archivo, fecha, personaje y mundo. Se reescribe entero cada vez que se añade un hito (nunca
	/// va a tener más que unas pocas decenas de entradas por partida), con el mismo patrón de
	/// escritura atómica que ya usa <c>SincronizacionEscritorio.EscribirIdiomaEscritorio</c>
	/// (temporal + <c>File.Move</c>): un corte a mitad de escritura deja el <c>.tmp</c> suelto,
	/// nunca el índice real a medio escribir.
	/// </para>
	/// <para>
	/// Si el índice no se puede leer (corrupto por un corte real en una sesión anterior, por
	/// ejemplo), <see cref="Listar"/> devuelve una lista vacía en vez de reventar el panel - las
	/// capturas .png en disco no se pierden, solo dejarían de listarse hasta que se registre un
	/// hito nuevo (que reescribe el índice desde cero).
	/// </para>
	/// </remarks>
	public static class AlbumHitos
	{
		/// <summary>Carpeta, dentro de la carpeta de guardado activa, donde viven las capturas de
		/// hito y su índice. Distinta de <c>CapturaDePantalla.Carpeta</c> ("terrakeep-capturas") a
		/// propósito: aquella es SOLO arnés de pruebas y se puede borrar sin perder nada real; esta
		/// es el álbum de verdad del jugador.</summary>
		public const string Carpeta = "terrakeep-hitos";

		private const string ArchivoIndice = "album.json";

		public static string CarpetaAbsoluta => Path.Combine(Main.SavePath, Carpeta);
		private static string RutaIndice => Path.Combine(CarpetaAbsoluta, ArchivoIndice);

		/// <summary>
		/// Dispara la captura real (misma técnica que <c>CapturaDePantalla</c>: back buffer del
		/// propio motor gráfico) y anota el hito en el índice. Nunca lanza: cualquier fallo (disco
		/// lleno, permisos...) se cuenta en la línea devuelta, para el log, y no interrumpe la
		/// partida.
		/// <para />
		/// Idea 3 del catálogo de funciones: <paramref name="tiempoSesionSegundos"/> y
		/// <paramref name="jefe"/> los conoce quien detecta el hito (<c>HitosSystem</c>, que ya
		/// lleva su propio reloj de sesión y el <c>TramoGuia</c> con su <c>JefeFinal</c>) - el resto
		/// (día del mundo, equipo llevado) se puede leer aquí mismo en el momento del disparo.
		/// </summary>
		public static string Registrar(string claveTramo, string nombreLegible, int tiempoSesionSegundos, string jefe)
		{
			try {
				DateTime ahora = DateTime.Now;
				string nombreArchivo = ahora.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + SanearNombre(claveTramo);

				string detalle;
				string ruta = Common.Panel.CapturaDePantalla.GuardarHito(CarpetaAbsoluta, nombreArchivo, out detalle);
				if (ruta == null) {
					return "HITO \"" + claveTramo + "\" (" + nombreLegible + "): " + detalle;
				}

				Player jugador = Main.LocalPlayer;
				EntradaAlbum entrada = new EntradaAlbum {
					Clave = claveTramo,
					Nombre = nombreLegible,
					Archivo = Path.GetFileName(ruta),
					Fecha = ahora,
					Personaje = jugador != null ? jugador.name : "",
					Mundo = Main.worldName ?? "",
					DiaDelMundo = ContadorDiasSystem.DiasTranscurridos,
					Equipo = jugador != null ? AutoEquipar.EstadoEquipo(jugador, jugador.armor) : "",
					TiempoSesionSegundos = tiempoSesionSegundos,
					Jefe = jefe ?? ""
				};
				AnadirAlIndice(entrada);

				return "HITO \"" + claveTramo + "\" (" + nombreLegible + "): " + detalle;
			}
			catch (Exception e) {
				return "HITO \"" + claveTramo + "\" (" + nombreLegible + "): fallo registrando el " +
					"álbum: " + e.GetType().Name + ": " + e.Message;
			}
		}

		private static void AnadirAlIndice(EntradaAlbum entrada)
		{
			JArray lista;
			if (File.Exists(RutaIndice)) {
				try {
					lista = JArray.Parse(File.ReadAllText(RutaIndice));
				}
				catch (Exception) {
					// Índice corrupto: se empieza de cero en vez de perder la posibilidad de seguir
					// apuntando hitos nuevos. Las capturas .png anteriores no se tocan ni se borran.
					lista = new JArray();
				}
			}
			else {
				lista = new JArray();
			}

			lista.Add(new JObject {
				["clave"] = entrada.Clave,
				["nombre"] = entrada.Nombre,
				["archivo"] = entrada.Archivo,
				["fecha"] = entrada.Fecha.ToString("yyyy-MM-dd HH:mm:ss"),
				["personaje"] = entrada.Personaje,
				["mundo"] = entrada.Mundo,
				["diaDelMundo"] = entrada.DiaDelMundo,
				["equipo"] = entrada.Equipo,
				["tiempoSesionSegundos"] = entrada.TiempoSesionSegundos,
				["jefe"] = entrada.Jefe
			});

			Directory.CreateDirectory(CarpetaAbsoluta);
			string tmp = RutaIndice + ".tmp";
			File.WriteAllText(tmp, lista.ToString(Newtonsoft.Json.Formatting.Indented));
			if (File.Exists(RutaIndice)) {
				File.Delete(RutaIndice);
			}
			File.Move(tmp, RutaIndice);
		}

		/// <summary>Todas las entradas del álbum, MÁS RECIENTE PRIMERO. Lee del disco cada vez que
		/// se llama (solo la usa el panel al abrir o al pulsar "Actualizar", nunca en cada
		/// fotograma).</summary>
		public static List<EntradaAlbum> Listar()
		{
			List<EntradaAlbum> salida = new List<EntradaAlbum>();
			try {
				if (!File.Exists(RutaIndice)) {
					return salida;
				}

				JArray lista = JArray.Parse(File.ReadAllText(RutaIndice));
				foreach (JToken token in lista) {
					JObject o = token as JObject;
					if (o == null) {
						continue;
					}

					EntradaAlbum entrada = new EntradaAlbum {
						Clave = (string)o["clave"] ?? "",
						Nombre = (string)o["nombre"] ?? "",
						Archivo = (string)o["archivo"] ?? "",
						Personaje = (string)o["personaje"] ?? "",
						Mundo = (string)o["mundo"] ?? "",
						// Idea 3 del catalogo de funciones: los cuatro campos nuevos. (int?)/(string)
						// sobre un token ausente (album.json de una version anterior del mod, antes
						// de que existieran) da null, no una excepcion - de ahi el ?? de respaldo.
						DiaDelMundo = (int?)o["diaDelMundo"] ?? -1,
						Equipo = (string)o["equipo"] ?? "",
						TiempoSesionSegundos = (int?)o["tiempoSesionSegundos"] ?? -1,
						Jefe = (string)o["jefe"] ?? ""
					};

					DateTime fecha;
					if (DateTime.TryParse((string)o["fecha"], out fecha)) {
						entrada.Fecha = fecha;
					}
					salida.Add(entrada);
				}
			}
			catch (Exception) {
				// Álbum ilegible: se enseña vacío en vez de reventar el panel (ver la cabecera).
			}

			salida.Reverse();
			return salida;
		}

		/// <summary>Nombre de archivo seguro: letras/dígitos tal cual, cualquier otra cosa (tildes,
		/// espacios) se convierte en guion. La clave de un tramo ya es ASCII sin espacios en la
		/// práctica, pero esto lo deja garantizado sin tener que confiar en esa convención.</summary>
		private static string SanearNombre(string texto)
		{
			System.Text.StringBuilder limpio = new System.Text.StringBuilder();
			foreach (char c in texto ?? "") {
				limpio.Append(char.IsLetterOrDigit(c) ? c : '-');
			}
			return limpio.Length > 0 ? limpio.ToString() : "hito";
		}
	}
}
