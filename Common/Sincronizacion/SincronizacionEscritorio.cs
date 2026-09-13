using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using Terraria;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Sincronizacion
{
	/// <summary>
	/// Lo que de verdad tiene sentido compartir entre el mod en vivo (TerrakeepMod) y la app de
	/// escritorio hermana (Terrakeep, <c>Downloads\Terrasavr-Win\Terrasavr-Native\
	/// Terrakeep.App\</c>) - encargo del usuario del 13-sep-2026 ("investiga qué tiene sentido de
	/// verdad sincronizar... decide con criterio y documenta la decisión honestamente si algo no
	/// tiene sentido real").
	/// <para />
	/// <b>Lo que SÍ tiene un análogo real y se sincroniza aquí (dos piezas, cada una con su
	/// método):</b>
	/// <list type="bullet">
	/// <item><b>Idioma.</b> Las dos herramientas ya guardan "es"/"en" por su cuenta
	/// (<c>AjustesConfig.Idioma</c> aquí, <c>TerrakeepSettings.Language</c> en
	/// <c>%LOCALAPPDATA%\Terrakeep\settings.json</c> allí). Son la misma preferencia personal de
	/// la misma persona sobre la misma marca - no hay ninguna razón real para que digan cosas
	/// distintas. <see cref="EscribirIdiomaEscritorio"/>/<see cref="LeerIdiomaEscritorio"/>.</item>
	/// <item><b>Historial de copias de seguridad.</b> La app de escritorio ya tiene un historial
	/// real y maduro (<c>BackupHistoryService</c>, formato <c>.tkbak</c> - zip con
	/// <c>player.plr</c>+<c>meta.json</c>, carpeta <c>{personaje}-{huella}</c> bajo
	/// <c>%LOCALAPPDATA%\Terrakeep\Backups\</c>). El mod no puede "guardar" el `.plr` (eso lo hace
	/// el propio Terraria), pero SÍ puede fotografiarlo justo cuando el juego lo escribe de
	/// verdad, con el MISMO formato y la MISMA huella, para que abrir el historial desde
	/// cualquiera de las dos herramientas enseñe una única línea de tiempo, jugada en el mod o
	/// editada en el escritorio. <see cref="ComprobarGuardadoYRespaldar"/>.</item>
	/// </list>
	/// <para />
	/// <b>Lo que NO tiene un análogo real, y por qué (para no forzar nada falso):</b>
	/// <list type="bullet">
	/// <item><b>"Presets"/plantillas de apariencia o equipo.</b> La app de escritorio no tiene hoy
	/// ningún concepto de plantilla reutilizable entre personajes (no existe ese servicio en
	/// <c>Terrakeep.App/Services</c>) - sincronizar algo que ninguna de las dos partes tiene
	/// todavía sería inventarse un dato, justo lo que la marca "Keep" prohíbe.</item>
	/// <item><b>Estado de ventana/tamaño de panel.</b> <c>WindowPlacementService</c>/
	/// <c>ExplorationSidebarWidth</c> son geometría de una ventana WPF de escritorio; el panel del
	/// mod vive DENTRO de la ventana del propio juego, a resolución y escala completamente
	/// distintas (ver <c>AutopruebaEspaciado</c>) - un ancho de panel bueno en un monitor de
	/// escritorio no significa nada dentro del juego. Sincronizarlo sería un falso análogo.</item>
	/// <item><b>Carpetas extra de personajes/mundos.</b> Son para que la app de escritorio
	/// ENCUENTRE partidas fuera de <c>Documents\My Games\Terraria\</c> (instalación portable,
	/// Documentos redirigidos). El mod vive DENTRO de esa misma instalación de tModLoader que ya
	/// está cargando la partida activa - no tiene "carpetas que buscar", ya sabe exactamente cuál
	/// es la partida.</item>
	/// <item><b>Edición simultánea de la MISMA partida.</b> Las dos herramientas no pueden tocar
	/// el mismo <c>.plr</c> a la vez de verdad: mientras tModLoader tiene la partida cargada, el
	/// archivo en disco es una foto vieja hasta el próximo guardado, y la app de escritorio
	/// escribiendo encima mientras tanto se perdería en el siguiente guardado del juego. No es un
	/// hueco de esta sesión: es una limitación real de que sean dos procesos independientes sobre
	/// el mismo archivo, y por eso el historial de copias (arriba) importa más que un
	/// "sincronizar en vivo" que no puede existir de verdad.</item>
	/// </list>
	/// </summary>
	public static class SincronizacionEscritorio
	{
		/// <summary>Misma raíz que <c>SettingsService.FilePath</c>/<c>BackupHistoryService.
		/// BackupsRoot</c> de <c>Terrakeep.App</c> - literalmente la misma carpeta, no una copia.
		/// <para />
		/// <c>internal set</c> a propósito, mismo motivo real que ya documentó la app de
		/// escritorio para <c>BackupHistoryService.BackupsRoot</c> ("las pruebas... trabajan sobre
		/// rutas temporales, y cada ruta temporal distinta estrenaba una carpeta en el
		/// %LOCALAPPDATA% REAL que no retiraba nadie"): <see
		/// cref="Common.Sincronizacion.AutopruebaSincronizacion"/> apunta esto a una carpeta de
		/// prueba propia ANTES de tocar nada, así que ninguna pasada automática escribe jamás en
		/// el <c>%LOCALAPPDATA%\Terrakeep</c> real de quien esté jugando en esta máquina.</summary>
		public static string CarpetaTerrakeep { get; internal set; } = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Terrakeep");

		private static string RutaSettings => Path.Combine(CarpetaTerrakeep, "settings.json");
		private static string RutaBackups => Path.Combine(CarpetaTerrakeep, "Backups");

		// Mismo formato que BackupHistoryService.SnapshotExtension/EntryPlr/EntryTplr/EntryMeta -
		// no se referencian sus constantes porque son proyectos .csproj distintos (la app de
		// escritorio ni siquiera se compila junto al mod), pero el VALOR tiene que ser identico
		// letra a letra para que el .tkbak que escriba el mod lo lea bien la app de escritorio.
		private const string ExtensionSnapshot = ".tkbak";
		private const string EntradaPlr = "player.plr";
		private const string EntradaTplr = "player.tplr";
		private const string EntradaMeta = "meta.json";

		// ===================================================================================
		// Idioma
		// ===================================================================================

		/// <summary>"es"/"en" leidos de <c>settings.json</c> de la app de escritorio, o null si el
		/// archivo no existe todavia (nunca se ha instalado/abierto Terrakeep en esta maquina) o no
		/// se puede leer - nunca revienta el arranque del mod por esto.</summary>
		public static string LeerIdiomaEscritorio()
		{
			try {
				if (!File.Exists(RutaSettings)) {
					return null;
				}
				JObject json = JObject.Parse(File.ReadAllText(RutaSettings));
				string idioma = (string)json["Language"];
				return string.IsNullOrWhiteSpace(idioma) ? null : idioma;
			}
			catch (Exception) {
				return null;
			}
		}

		/// <summary>
		/// Escribe el idioma elegido DENTRO del mod tambien en <c>settings.json</c> de la app de
		/// escritorio, conservando el resto de campos tal cual (ancho de sidebar, carpetas extra,
		/// tope de copias...) - un merge, nunca una sustitucion del archivo entero. Si la carpeta
		/// <c>%LOCALAPPDATA%\Terrakeep</c> no existe todavia (la app de escritorio nunca se ha
		/// abierto en esta maquina) NO se crea aqui: escribir un settings.json de la nada desde el
		/// mod, sin que la app de escritorio lo haya validado nunca, es mas riesgo que beneficio
		/// para un archivo que no es "nuestro". Solo se actualiza uno que YA existe.
		/// </summary>
		public static void EscribirIdiomaEscritorio(string idiomaEsEn)
		{
			try {
				if (!File.Exists(RutaSettings)) {
					return;
				}

				JObject json = JObject.Parse(File.ReadAllText(RutaSettings));
				if ((string)json["Language"] == idiomaEsEn) {
					return;
				}

				json["Language"] = idiomaEsEn;

				// Mismo criterio de escritura atomica que usa CharacterFileService.WriteAtomic de
				// la app de escritorio (temporal + File.Move): un corte a mitad deja el .tmp
				// suelto, nunca el settings.json real a medio escribir.
				string tmp = RutaSettings + ".tmp";
				File.WriteAllText(tmp, json.ToString());
				File.Delete(RutaSettings);
				File.Move(tmp, RutaSettings);

				RegistroSincronizacion.Linea("Idioma reflejado en settings.json de Terrakeep (escritorio): " +
					idiomaEsEn + ".");
			}
			catch (Exception e) {
				RegistroSincronizacion.Linea("No se pudo escribir el idioma en settings.json de Terrakeep " +
					"(escritorio, no crítico): " + e.Message);
			}
		}

		// ===================================================================================
		// Historial de copias de seguridad compartido
		// ===================================================================================

		/// <summary>
		/// Igual que <c>BackupHistoryService.HuellaDeRuta</c> de la app de escritorio: 8 hex de
		/// SHA-256 sobre la ruta completa normalizada en minusculas. Tiene que dar EXACTAMENTE el
		/// mismo resultado para el mismo archivo o el mod escribiria en una carpeta de historial
		/// distinta a la que ya usa la app de escritorio para ese personaje.
		/// </summary>
		public static string HuellaDeRuta(string plrPath)
		{
			string normalizada;
			try {
				normalizada = Path.GetFullPath(plrPath).ToLowerInvariant();
			}
			catch (Exception) {
				normalizada = plrPath.ToLowerInvariant();
			}
			byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizada));
			StringBuilder sb = new StringBuilder(8);
			for (int i = 0; i < 4; i++) {
				sb.Append(hash[i].ToString("x2"));
			}
			return sb.ToString();
		}

		private static string SanearNombreArchivo(string nombre)
		{
			foreach (char c in Path.GetInvalidFileNameChars()) {
				nombre = nombre.Replace(c, '_');
			}
			return nombre;
		}

		/// <summary>Carpeta de historial de un personaje concreto, IDENTICA a la que calcula
		/// <c>BackupHistoryService.DirectoryFor</c> para el mismo <c>.plr</c>.</summary>
		public static string CarpetaDeHistorial(string plrPath)
		{
			string nombre = SanearNombreArchivo(Path.GetFileNameWithoutExtension(plrPath));
			return Path.Combine(RutaBackups, nombre + "-" + HuellaDeRuta(plrPath));
		}

		/// <summary>
		/// Fotografia el <c>.plr</c> (y su <c>.tplr</c> hermano si existe) TAL CUAL ESTA EN DISCO
		/// ahora mismo, en el mismo formato <c>.tkbak</c> que ya sabe leer la app de escritorio.
		/// Motivo 'A' (BeforeSave) reutilizado a proposito: es la categoria existente mas honesta
		/// para "una foto tomada justo alrededor de un guardado real a disco" - inventarse un
		/// motivo nuevo sin poder volver a compilar/probar <c>Terrakeep.App</c> en esta sesion
		/// seria arriesgar que su lector de <c>meta.json</c> no lo reconozca.
		/// </summary>
		public static bool GuardarInstantanea(string plrPath)
		{
			try {
				if (!File.Exists(plrPath)) {
					return false;
				}

				byte[] plrBytes = File.ReadAllBytes(plrPath);
				string tplrPath = Path.ChangeExtension(plrPath, ".tplr");
				byte[] tplrBytes = File.Exists(tplrPath) ? File.ReadAllBytes(tplrPath) : null;

				string carpeta = CarpetaDeHistorial(plrPath);
				Directory.CreateDirectory(carpeta);

				DateTime ahora = DateTime.Now;
				string destino = RutaLibre(carpeta, ahora);
				string tmp = destino + ".tmp";

				JObject meta = ConstruirMeta(plrPath, plrBytes, tplrBytes, ahora);

				using (FileStream fs = File.Create(tmp))
				using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create)) {
					EscribirEntrada(zip, EntradaPlr, plrBytes, CompressionLevel.NoCompression);
					if (tplrBytes != null) {
						EscribirEntrada(zip, EntradaTplr, tplrBytes, CompressionLevel.NoCompression);
					}
					EscribirEntrada(zip, EntradaMeta, Encoding.UTF8.GetBytes(meta.ToString()), CompressionLevel.Optimal);
				}
				File.Move(tmp, destino);

				Purgar(carpeta);

				RegistroSincronizacion.Linea("Instantanea compartida guardada: \"" + destino + "\" (" +
					plrBytes.LongLength + " bytes de .plr" + (tplrBytes != null ? " + .tplr" : "") + ").");
				return true;
			}
			catch (Exception e) {
				RegistroSincronizacion.Linea("No se pudo guardar la instantanea compartida (no critico): " + e);
				return false;
			}
		}

		private static void EscribirEntrada(ZipArchive zip, string nombre, byte[] datos, CompressionLevel nivel)
		{
			ZipArchiveEntry entrada = zip.CreateEntry(nombre, nivel);
			using (Stream s = entrada.Open()) {
				s.Write(datos, 0, datos.Length);
			}
		}

		private static string RutaLibre(string carpeta, DateTime ahora)
		{
			for (int i = 0; i < 1000; i++) {
				string ruta = Path.Combine(carpeta, ahora.AddMilliseconds(i).ToString("yyyyMMdd-HHmmss-fff") + "-A" + ExtensionSnapshot);
				if (!File.Exists(ruta)) {
					return ruta;
				}
			}
			return Path.Combine(carpeta, ahora.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + "-A" + ExtensionSnapshot);
		}

		/// <summary>
		/// Mismos nombres de campo EXACTOS (may/min incluidas) que <c>BackupSnapshotInfo</c> de la
		/// app de escritorio, para que su <c>System.Text.Json</c> (sin
		/// <c>PropertyNameCaseInsensitive</c>) lo lea bien. Los datos salen del JUGADOR VIVO
		/// (<c>Main.LocalPlayer</c>/<c>Main.ActivePlayerFileData</c>), nunca inventados - lo que el
		/// mod no puede saber de verdad (SaveVersion, PveDeaths: no hay un contador vivo accesible
		/// sin decompilar mas de la cuenta para un dato secundario) se deja a 0, exactamente el
		/// mismo criterio "sin resumen completo, la copia sigue siendo una copia buena" que ya
		/// documenta <c>BackupHistoryService</c>.
		/// </summary>
		private static JObject ConstruirMeta(string plrPath, byte[] plrBytes, byte[] tplrBytes, DateTime ahora)
		{
			Player jugador = Main.LocalPlayer;
			int cuentaObjetos = 0;
			if (jugador != null && jugador.inventory != null) {
				foreach (Item objeto in jugador.inventory) {
					if (objeto != null && !objeto.IsAir) {
						cuentaObjetos++;
					}
				}
			}

			long playTimeTicks = 0;
			try {
				if (Main.ActivePlayerFileData != null) {
					playTimeTicks = Main.ActivePlayerFileData.GetPlayTime().Ticks;
				}
			}
			catch (Exception) {
				// Best-effort, igual que el resto de este resumen.
			}

			JObject meta = new JObject {
				["Reason"] = "BeforeSave",
				["CreatedLocal"] = ahora.ToString("o"),
				["SourcePlrPath"] = plrPath,
				["AppVersion"] = "TerrakeepMod " + (Terrakeep.Instance != null ? Terrakeep.Instance.Version.ToString() : "?"),
				["SummaryAvailable"] = jugador != null,
				["CharacterName"] = jugador != null ? jugador.name : "",
				["SaveVersion"] = 0,
				["Difficulty"] = jugador != null ? (int)jugador.difficulty : 0,
				["HealthNow"] = jugador != null ? jugador.statLife : 0,
				["HealthMax"] = jugador != null ? jugador.statLifeMax2 : 0,
				["ManaNow"] = jugador != null ? jugador.statMana : 0,
				["ManaMax"] = jugador != null ? jugador.statManaMax2 : 0,
				["PlayTimeTicks"] = playTimeTicks,
				["PveDeaths"] = 0,
				["ItemCount"] = cuentaObjetos,
				["HasTplr"] = tplrBytes != null,
				["PlrBytes"] = plrBytes.LongLength,
				["TplrBytes"] = tplrBytes != null ? tplrBytes.LongLength : 0L
			};
			return meta;
		}

		/// <summary>
		/// Mismo criterio que <c>BackupHistoryService.Purge</c>: si hay mas de <see
		/// cref="TopeDeInstantaneas"/> instantaneas AUTOMATICAS ('A'/'X', nunca 'M' de manual) en
		/// la carpeta, se borran las mas antiguas por el SELLO REAL del nombre de archivo (nunca
		/// por la fecha de modificacion, que un <c>File.Copy</c> podria heredar del origen).
		/// </summary>
		private const int TopeDeInstantaneas = 20;

		private static void Purgar(string carpeta)
		{
			try {
				string[] archivos = Directory.GetFiles(carpeta, "*" + ExtensionSnapshot);
				if (archivos.Length <= TopeDeInstantaneas) {
					return;
				}

				Array.Sort(archivos, (a, b) => string.CompareOrdinal(
					Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b)));

				int sobran = archivos.Length - TopeDeInstantaneas;
				for (int i = 0; i < archivos.Length && sobran > 0; i++) {
					string nombre = Path.GetFileNameWithoutExtension(archivos[i]);
					if (nombre.EndsWith("-M", StringComparison.Ordinal)) {
						continue;
					}
					File.Delete(archivos[i]);
					sobran--;
				}
			}
			catch (Exception e) {
				RegistroSincronizacion.Linea("No se pudo purgar el historial compartido (no critico): " + e.Message);
			}
		}
	}
}
