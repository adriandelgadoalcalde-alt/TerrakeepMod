using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using Terraria;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Sincronizacion
{
	/// <summary>
	/// Autoprueba de la sincronización con la app de escritorio: ejecuta en el juego REAL, sin que
	/// nadie pulse nada, los tres comportamientos reales que promete <see
	/// cref="SincronizacionEscritorio"/>, y deja la evidencia en <c>tModLoader-Logs\client.log</c>
	/// (prefijo <c>[Terrakeep]</c>). Se dispara con <see cref="SincronizacionSystem.
	/// VariableAutoprueba"/>, que pone <c>scripts\verificar-sincronizacion.ps1</c>.
	/// <para />
	/// <b>Nunca toca el %LOCALAPPDATA%\Terrakeep real</b>: <c>SincronizacionSystem.OnModLoad</c> ya
	/// redirige <see cref="SincronizacionEscritorio.CarpetaTerrakeep"/> a una carpeta de TEMP
	/// propia en cuanto ve la misma variable de entorno, antes de que esta clase toque nada.
	/// </summary>
	public static class AutopruebaSincronizacion
	{
		private static bool _comprobada;
		private static bool _activa;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _pasoDeEspera;
		private static int _instantaneasAntesDelSondeo;
		private static string _rutaPlrDePrueba;

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SincronizacionSystem.VariableAutoprueba));
			}

			if (!_activa || _terminada) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;

			if (_pasoDeEspera == 0) {
				if (_fotogramasEnMundo < 180) {
					return;
				}
				_pasoDeEspera = 1;
				ProbarIdioma();
				ProbarInstantaneaDirecta();
				PrepararSondeoAutomatico();
				return;
			}

			if (_pasoDeEspera == 1) {
				// 60 fotogramas (1s) de sobra para que SincronizacionSystem.UpdateUI (cada 30, es
				// decir cada 0,5s) haga al menos una pasada de sondeo real tras tocar el .plr.
				if (_fotogramasEnMundo < 60) {
					return;
				}
				ComprobarSondeoAutomatico();
				_terminada = true;
				Registrar("AUTOPRUEBA SINCRONIZACION: terminada.");
			}
		}

		private static void ProbarIdioma()
		{
			// Escenario 1: la app de escritorio ya tiene una preferencia ("en") y el mod TODAVIA
			// no ha elegido ninguna (SeguirElJuego) - tiene que adoptarla al arrancar.
			IdiomaDeTerrakeep antes = AjustesConfig.Instance.Idioma;
			AjustesConfig.Instance.Idioma = IdiomaDeTerrakeep.SeguirElJuego;

			Directory.CreateDirectory(SincronizacionEscritorio.CarpetaTerrakeep);
			string rutaSettings = Path.Combine(SincronizacionEscritorio.CarpetaTerrakeep, "settings.json");
			File.WriteAllText(rutaSettings, "{\"Language\":\"en\",\"BackupHistoryCap\":20}");

			SincronizacionSystem.ResolverIdiomaDeArranqueParaPrueba();

			bool adoptoIngles = Idiomas.CulturaActiva == "en-US";
			Registrar("IDIOMA/1 settings.json de escritorio decia \"en\", AjustesConfig estaba en SeguirElJuego -> " +
				"cultura activa ahora es \"" + Idiomas.CulturaActiva + "\" | " +
				(adoptoIngles ? "OK: adoptada al arrancar." : "FALLO: NO se adopto."));

			// Escenario 2: elegir un idioma DENTRO del mod (accion explicita real, mismo camino
			// que el selector del panel de Ajustes) tiene que reflejarse en settings.json.
			Idiomas.Elegir(IdiomaDeTerrakeep.Espanol);

			string contenido = File.ReadAllText(rutaSettings);
			JObject json = JObject.Parse(contenido);
			string idiomaEscrito = (string)json["Language"];
			bool otrosCamposIntactos = json["BackupHistoryCap"] != null && (int)json["BackupHistoryCap"] == 20;

			Registrar("IDIOMA/2 tras Idiomas.Elegir(Espanol): settings.json de escritorio quedo con Language=\"" +
				idiomaEscrito + "\", BackupHistoryCap intacto=" + otrosCamposIntactos + " | " +
				(idiomaEscrito == "es" && otrosCamposIntactos
					? "OK: reflejado sin tocar el resto del archivo."
					: "FALLO: NO se reflejo bien."));

			// Deja el mod como estaba antes de la prueba (nunca se persiste el ModConfig aqui:
			// Elegir ya lo hizo con SaveChanges, asi que se restaura de la misma forma).
			Idiomas.Elegir(antes);
		}

		private static void ProbarInstantaneaDirecta()
		{
			_rutaPlrDePrueba = Main.playerPathName;
			if (string.IsNullOrEmpty(_rutaPlrDePrueba) || !File.Exists(_rutaPlrDePrueba)) {
				Registrar("INSTANTANEA/1 FALLO: no hay .plr real del personaje de pruebas en \"" + _rutaPlrDePrueba + "\".");
				return;
			}

			string carpetaHistorial = SincronizacionEscritorio.CarpetaDeHistorial(_rutaPlrDePrueba);
			// Limpio antes de empezar, por si una pasada anterior dejo algo (la carpeta de TEMP no
			// se borra sola entre ejecuciones del arnes).
			if (Directory.Exists(carpetaHistorial)) {
				Directory.Delete(carpetaHistorial, recursive: true);
			}

			bool guardada = SincronizacionEscritorio.GuardarInstantanea(_rutaPlrDePrueba);
			string[] archivos = Directory.Exists(carpetaHistorial) ? Directory.GetFiles(carpetaHistorial, "*.tkbak") : Array.Empty<string>();

			Registrar("INSTANTANEA/1 GuardarInstantanea devolvio " + guardada + ", carpeta \"" + carpetaHistorial +
				"\" tiene " + archivos.Length + " archivo(s) .tkbak | " +
				(guardada && archivos.Length == 1 ? "OK: se creo exactamente una." : "FALLO."));

			if (archivos.Length == 0) {
				return;
			}

			// El formato tiene que ser el REAL que sabe leer la app de escritorio: zip con
			// player.plr y meta.json, meta.json con los campos esperados.
			using (ZipArchive zip = ZipFile.OpenRead(archivos[0])) {
				ZipArchiveEntry entradaPlr = zip.GetEntry("player.plr");
				ZipArchiveEntry entradaMeta = zip.GetEntry("meta.json");
				bool tieneLasDosEntradas = entradaPlr != null && entradaMeta != null;

				string metaTexto = "";
				bool metaValida = false;
				string nombrePersonaje = "";
				if (entradaMeta != null) {
					using (StreamReader lector = new StreamReader(entradaMeta.Open())) {
						metaTexto = lector.ReadToEnd();
					}
					try {
						JObject meta = JObject.Parse(metaTexto);
						nombrePersonaje = (string)meta["CharacterName"];
						metaValida = (string)meta["Reason"] == "BeforeSave" && meta["PlrBytes"] != null;
					}
					catch (Exception) {
						metaValida = false;
					}
				}

				Registrar("INSTANTANEA/2 el .tkbak tiene player.plr+meta.json=" + tieneLasDosEntradas +
					", meta.json parsea bien=" + metaValida + ", CharacterName=\"" + nombrePersonaje + "\" | " +
					(tieneLasDosEntradas && metaValida
						? "OK: formato compatible con BackupHistoryService de la app de escritorio."
						: "FALLO: formato incorrecto - meta=" + metaTexto));
			}
		}

		private static void PrepararSondeoAutomatico()
		{
			if (string.IsNullOrEmpty(_rutaPlrDePrueba) || !File.Exists(_rutaPlrDePrueba)) {
				_instantaneasAntesDelSondeo = -1;
				return;
			}

			string carpetaHistorial = SincronizacionEscritorio.CarpetaDeHistorial(_rutaPlrDePrueba);
			_instantaneasAntesDelSondeo = Directory.Exists(carpetaHistorial)
				? Directory.GetFiles(carpetaHistorial, "*.tkbak").Length
				: 0;

			// Toca el .plr real (sin cambiar su contenido) para que el proximo sondeo periodico de
			// SincronizacionSystem.UpdateUI lo vea como "recien escrito" - exactamente la misma
			// señal (LastWriteTimeUtc mas nuevo) que dejaria un guardado real del juego.
			File.SetLastWriteTimeUtc(_rutaPlrDePrueba, DateTime.UtcNow);

			Registrar("SONDEO/0 preparado: " + _instantaneasAntesDelSondeo + " instantanea(s) antes de tocar el .plr. " +
				"Esperando a que SincronizacionSystem.UpdateUI lo detecte solo, por el camino real de produccion.");
		}

		private static void ComprobarSondeoAutomatico()
		{
			if (_instantaneasAntesDelSondeo < 0) {
				Registrar("SONDEO/1 FALLO: no se pudo preparar el escenario (no habia .plr real).");
				return;
			}

			string carpetaHistorial = SincronizacionEscritorio.CarpetaDeHistorial(_rutaPlrDePrueba);
			int ahora = Directory.Exists(carpetaHistorial) ? Directory.GetFiles(carpetaHistorial, "*.tkbak").Length : 0;

			Registrar("SONDEO/1 instantaneas antes=" + _instantaneasAntesDelSondeo + ", ahora=" + ahora + " | " +
				(ahora > _instantaneasAntesDelSondeo
					? "OK: SincronizacionSystem.UpdateUI detecto el guardado solo y genero una instantanea nueva, sin que nadie llamara a nada a mano."
					: "FALLO: el sondeo periodico NO genero ninguna instantanea nueva."));
		}

		private static void Registrar(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
			}
		}
	}
}
