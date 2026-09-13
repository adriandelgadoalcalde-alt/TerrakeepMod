using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Sincronizacion
{
	/// <summary>
	/// Engancha <see cref="SincronizacionEscritorio"/> al ciclo de vida real del juego. Ver el
	/// XMLdoc de esa clase para QUÉ se sincroniza y por qué (y, sobre todo, qué NO se sincroniza y
	/// por qué no).
	/// </summary>
	public class SincronizacionSystem : ModSystem
	{
		// Comprobar el mtime del .plr en cada fotograma seria I/O de disco 60 veces por segundo
		// para nada: un guardado no pasa mas de una vez cada varios segundos como muy rapido.
		// Medio segundo es mas que de sobra para no perderse ninguno mientras el juego sigue
		// corriendo (la ventana real entre "Guardar y salir" y que el proceso termine de
		// desmontar el mundo dura bastante mas que eso).
		private const int FotogramasEntreComprobaciones = 30;

		private int _fotogramasHastaProximaComprobacion;
		private string _rutaPlrVigilada;
		private DateTime _ultimaEscrituraConocida;
		private bool _idiomaDeArranqueResuelto;

		/// <summary>Variable que dispara <see cref="AutopruebaSincronizacion"/>. Se comprueba AQUI,
		/// antes que nada, para que la redireccion a una carpeta de prueba ocurra antes de que
		/// cualquier otro metodo de este sistema pueda tocar el %LOCALAPPDATA%\Terrakeep real.</summary>
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST_SINCRO";

		public override void OnModLoad()
		{
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableAutoprueba))) {
				SincronizacionEscritorio.CarpetaTerrakeep = Path.Combine(
					Path.GetTempPath(), "TerrakeepSincroPrueba-" + Environment.ProcessId);
				RegistroSincronizacion.Linea("AUTOPRUEBA: redirigida la carpeta compartida a \"" +
					SincronizacionEscritorio.CarpetaTerrakeep + "\" - el %LOCALAPPDATA%\\Terrakeep real no se toca.");
			}

			if (!Main.dedServ) {
				Idiomas.Cambiado += EscribirIdiomaTrasCambio;
			}
		}

		public override void OnModUnload()
		{
			Idiomas.Cambiado -= EscribirIdiomaTrasCambio;
		}

		/// <summary>
		/// Cada vez que el idioma del juego acaba de cambiar de VERDAD (elegido en el panel de
		/// Ajustes del mod, o incluso desde el propio menu de idioma de vanilla mientras se juega -
		/// <see cref="Idiomas.Cambiado"/> no distingue el origen, y no hace falta distinguirlo: es
		/// el idioma real que esta persona ha usado jugando, la ultima vez), se refleja en
		/// settings.json de la app de escritorio si existe.
		/// </summary>
		private void EscribirIdiomaTrasCambio()
		{
			if (Idiomas.EnEspanol) {
				SincronizacionEscritorio.EscribirIdiomaEscritorio("es");
			}
			else if (Idiomas.CulturaActiva != null && Idiomas.CulturaActiva.StartsWith("en")) {
				SincronizacionEscritorio.EscribirIdiomaEscritorio("en");
			}
			// Cualquier otro idioma de vanilla (pt-BR, de-DE...) no tiene equivalente en el
			// selector es/en de la app de escritorio - no se fuerza ningun analogo falso, se deja
			// tal cual estuviera.
		}

		public override void OnWorldLoad()
		{
			_fotogramasHastaProximaComprobacion = FotogramasEntreComprobaciones;
			_idiomaDeArranqueResuelto = false;

			try {
				_rutaPlrVigilada = Main.playerPathName;
				_ultimaEscrituraConocida = File.Exists(_rutaPlrVigilada)
					? File.GetLastWriteTimeUtc(_rutaPlrVigilada)
					: DateTime.MinValue;
			}
			catch (Exception) {
				_rutaPlrVigilada = null;
			}
		}

		public override void OnWorldUnload()
		{
			// Ultima oportunidad antes de que el mundo se desmonte del todo: si "Guardar y salir"
			// acaba de escribir el .plr y el sondeo periodico todavia no le habia tocado el turno,
			// aqui se pilla igual.
			ComprobarGuardado();
			_rutaPlrVigilada = null;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// La autoprueba va primero y sin condiciones (mismo criterio que WS0/WS7): si algo de
			// abajo lanzara una excepcion, la autoprueba tiene que haber corrido igual.
			AutopruebaSincronizacion.Avanzar();

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}

			if (!_idiomaDeArranqueResuelto) {
				_idiomaDeArranqueResuelto = true;
				ResolverIdiomaDeArranque();
			}

			if (--_fotogramasHastaProximaComprobacion > 0) {
				return;
			}
			_fotogramasHastaProximaComprobacion = FotogramasEntreComprobaciones;

			ComprobarGuardado();
		}

		/// <summary>
		/// Adopcion de arranque, SOLO si el jugador nunca ha elegido un idioma a mano DENTRO del
		/// mod (<see cref="IdiomaDeTerrakeep.SeguirElJuego"/>, el valor de fabrica) - una eleccion
		/// activa jamas se pisa. Si la app de escritorio tiene una preferencia real guardada, esta
		/// sesion del mod arranca con ESA en vez de con el idioma crudo del juego: son la misma
		/// marca, y es mas util que "SeguirElJuego" siga la preferencia de Terrakeep si existe
		/// antes que caer al idioma del juego a secas. No se persiste en el ModConfig del mod (se
		/// re-evalua cada partida): si la app de escritorio cambia de idioma manana, la proxima
		/// sesion del mod lo recoge sola, sin que nadie tenga que volver a elegir nada aqui.
		/// </summary>
		private static void ResolverIdiomaDeArranque()
		{
			if (AjustesConfig.Instance == null || AjustesConfig.Instance.Idioma != IdiomaDeTerrakeep.SeguirElJuego) {
				return;
			}

			string idioma = SincronizacionEscritorio.LeerIdiomaEscritorio();
			IdiomaDeTerrakeep? equivalente = idioma == "es" ? IdiomaDeTerrakeep.Espanol
				: idioma == "en" ? IdiomaDeTerrakeep.English
				: (IdiomaDeTerrakeep?)null;

			if (equivalente == null) {
				return;
			}

			bool cambio = Idiomas.Aplicar(equivalente.Value, "preferencia de Terrakeep (escritorio) al arrancar");
			RegistroSincronizacion.Linea("Idioma de arranque: settings.json de escritorio dice \"" + idioma +
				"\", AjustesConfig sigue en SeguirElJuego -> " + (cambio ? "adoptado" : "ya coincidia") + ".");
		}

		/// <summary><c>internal</c> solo para que <see cref="AutopruebaSincronizacion"/> pueda
		/// disparar la MISMA logica de adopcion de arranque en un momento controlado (el disparo
		/// automatico de <see cref="UpdateUI"/> solo ocurre una vez por mundo cargado, y para
		/// entonces el escenario de prueba ya quiere haber preparado el settings.json y el
		/// AjustesConfig de antemano).</summary>
		internal static void ResolverIdiomaDeArranqueParaPrueba()
		{
			ResolverIdiomaDeArranque();
		}

		private void ComprobarGuardado()
		{
			if (string.IsNullOrEmpty(_rutaPlrVigilada)) {
				return;
			}

			DateTime escritura;
			try {
				if (!File.Exists(_rutaPlrVigilada)) {
					return;
				}
				escritura = File.GetLastWriteTimeUtc(_rutaPlrVigilada);
			}
			catch (Exception) {
				return;
			}

			if (escritura <= _ultimaEscrituraConocida) {
				return;
			}

			_ultimaEscrituraConocida = escritura;
			SincronizacionEscritorio.GuardarInstantanea(_rutaPlrVigilada);
		}
	}
}
