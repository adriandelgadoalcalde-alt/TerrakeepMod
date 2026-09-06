using Terraria;
using Terraria.ID;
using TerrakeepMod.Common.Undo;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Cambio EN VIVO del modo de juego del mundo (Clásico / Experto / Maestro / Viaje), con las
	/// salvaguardas que exigia el plan del proyecto.
	/// </summary>
	/// <remarks>
	/// Tres cosas que hay que tener MUY presentes, y que separan esto de lo que hace la app de
	/// escritorio (que edita un archivo y no escribe nada hasta que se lo piden):
	/// <list type="number">
	/// <item><b>Siempre por <c>Main.GameMode</c>, nunca escribiendo
	/// <c>Main.ActiveWorldFileData.GameMode</c> a pelo.</b> El setter de la propiedad hace las DOS
	/// cosas (codigo real del <c>tModLoader.dll</c> instalado, v2026.7.3.0):
	/// <code>
	/// set {
	///     if (ActiveWorldFileData != null &amp;&amp; RegisteredGameModes.TryGetValue(value, out var value2)) {
	///         ActiveWorldFileData.GameMode = value;
	///         _currentGameModeInfo = value2;
	///     }
	/// }
	/// </code>
	/// y <c>_currentGameModeInfo</c> es de donde salen <c>Main.expertMode</c>,
	/// <c>Main.masterMode</c> y los multiplicadores de daño/vida. Escribir solo el campo del
	/// archivo dejaria el mundo diciendo "experto" mientras el juego lo sigue tratando de clásico.
	/// Ademas, el <c>TryGetValue</c> hace que un valor invalido no cambie nada en vez de romper
	/// algo.</item>
	/// <item><b>El cambio es PERMANENTE en el siguiente guardado.</b> No hay que confirmarlo en
	/// ningun sitio: en cuanto el mundo se autoguarde (o se salga al menu) queda escrito en el
	/// <c>.wld</c>. Por eso el panel enseña el aviso ANTES, y el cambio va en dos pasos.</item>
	/// <item><b>El modo Viaje no es intercambiable a voluntad.</b> La regla real del juego
	/// (<c>UIWorldSelect.CanWorldBePlayed</c>) es
	/// <c>(jugador.difficulty == 3) == (mundo.GameMode == 3)</c>: un personaje de Viaje solo puede
	/// entrar en mundos de Viaje, y uno normal solo en mundos que no lo sean. O sea que cruzar esa
	/// linea deja al personaje que tienes cargado <b>sin poder volver a entrar</b> en este mundo.
	/// Por eso ese cambio concreto se BLOQUEA, y se explica por que.</item>
	/// </list>
	/// </remarks>
	public static class DificultadMundo
	{
		/// <summary>Motivo por el que un modo no se puede aplicar ahora mismo, o null si si se
		/// puede.</summary>
		public static string MotivoParaNoPoder(int modo)
		{
			if (!MundoActual.HayMundo) {
				return "No hay ningún mundo cargado.";
			}

			// Un cliente de multijugador no manda sobre el mundo: el modo vive en el servidor, y
			// tocarlo aqui solo desincronizaria a este cliente del resto de la partida.
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				return "Estás en una partida multijugador: el modo del mundo lo decide el servidor.";
			}

			if (modo == Main.GameMode) {
				return "El mundo ya está en este modo.";
			}

			if (!Main.RegisteredGameModes.ContainsKey(modo)) {
				return "Este modo de juego no existe en esta versión del juego.";
			}

			Player jugador = Main.LocalPlayer;
			if (jugador == null || !jugador.active) {
				return "No hay ningún personaje cargado.";
			}

			bool jugadorDeViaje = jugador.difficulty == 3;
			bool modoDeViaje = modo == GameModeID.Creative;
			if (jugadorDeViaje != modoDeViaje) {
				return jugadorDeViaje
					? "Tu personaje es de Viaje y solo puede entrar en mundos de Viaje: si quitas el modo Viaje de este mundo, este personaje se quedará fuera."
					: "Tu personaje NO es de Viaje: si pones este mundo en modo Viaje, este personaje ya no podrá volver a entrar en él.";
			}

			return null;
		}

		/// <summary>true si el modo se puede aplicar ahora mismo.</summary>
		public static bool SePuedeAplicar(int modo)
		{
			return MotivoParaNoPoder(modo) == null;
		}

		/// <summary>
		/// Aplica el modo de juego. Devuelve true si el cambio se hizo de verdad; si no, deja en
		/// <paramref name="motivo"/> por que no.
		/// <para />
		/// Queda registrado en el historial de deshacer/rehacer de WS7, asi que un cambio
		/// equivocado se puede revertir con Ctrl+Z <b>siempre que se haga antes del siguiente
		/// guardado</b>; despues de eso el <c>.wld</c> ya esta escrito.
		/// </summary>
		public static bool Aplicar(int modo, string origen, out string motivo)
		{
			motivo = MotivoParaNoPoder(modo);
			if (motivo != null) {
				RegistroExploracion.Aviso(Terrakeep.LogTag + " Dificultad: se rechaza el cambio a \"" +
					MundoActual.NombreDeModo(modo) + "\" via " + origen + ". Motivo: " + motivo);
				return false;
			}

			int antes = Main.GameMode;
			bool expertoAntes = Main.expertMode;
			bool maestroAntes = Main.masterMode;

			Historial.CambiarValor<int>(
				"Modo del mundo: " + MundoActual.NombreDeModo(antes) + " -> " + MundoActual.NombreDeModo(modo),
				antes, modo, (int valor) => { Main.GameMode = valor; });

			// El propio setter de Main.GameMode. Se llama aparte del historial (que solo guarda las
			// dos fotos) para que el cambio ocurra exactamente por el mismo camino que usaria el
			// juego, ni antes ni despues.
			Main.GameMode = modo;

			RegistroExploracion.Linea(Terrakeep.LogTag + " Dificultad CAMBIADA via " + origen + ": " +
				MundoActual.NombreDeModo(antes) + " -> " + MundoActual.NombreDeModo(modo) + ". " +
				"Estado real releido del juego: Main.GameMode=" + Main.GameMode +
				", Main.expertMode=" + Main.expertMode + " (antes " + expertoAntes + ")" +
				", Main.masterMode=" + Main.masterMode + " (antes " + maestroAntes + ")" +
				", Main.GameModeInfo.IsExpertMode=" + Main.GameModeInfo.IsExpertMode +
				", Main.GameModeInfo.IsMasterMode=" + Main.GameModeInfo.IsMasterMode +
				", Main.GameModeInfo.IsJourneyMode=" + Main.GameModeInfo.IsJourneyMode +
				", ActiveWorldFileData.GameMode=" + Main.ActiveWorldFileData.GameMode +
				" (los dos tienen que coincidir: si no, _currentGameModeInfo estaria desincronizado).");

			return true;
		}

		/// <summary>
		/// Aviso de permanencia, siempre visible en el panel antes de aplicar nada. Se adapta a si
		/// el autoguardado esta puesto o no, porque cambia mucho lo urgente que es.
		/// </summary>
		public static string AvisoDePermanencia()
		{
			return Main.autoSave
				? "El cambio es PERMANENTE: se escribe en el mundo en el siguiente autoguardado (lo tienes activado)."
				: "El cambio es PERMANENTE: se escribe en el mundo en cuanto se guarde (tienes el autoguardado desactivado, pero salir al menú guarda igual).";
		}

		/// <summary>
		/// Lo que el cambio NO hace, dicho claro. Sale del comportamiento real del juego: los
		/// multiplicadores del modo se consultan al calcular el daño y el botin, asi que los NPC
		/// que ya estan en el mundo conservan la vida con la que aparecieron.
		/// </summary>
		public static string AvisoDeEfecto()
		{
			return "Los enemigos ya generados no cambian de vida ni de daño hasta que reaparezcan; el botín sí reacciona al instante.";
		}
	}
}
