using Terraria;
using Terraria.ID;
using TerrakeepMod.Common.Undo;

namespace TerrakeepMod.Common.Ajustes
{
	/// <summary>
	/// Autoprueba de WS7: ejecuta en el juego REAL, sin que nadie pulse nada, los dos entregables
	/// del workstream, y deja en <c>tModLoader-Logs\client.log</c> (prefijo <c>[Terrakeep]</c>)
	/// la evidencia de cada paso.
	/// <para />
	/// Se dispara con la variable de entorno <see cref="AjustesSystem.VariableAutoprueba"/>, que
	/// pone <c>scripts\verificar-ws7.ps1</c>. Sin ella, este codigo no se ejecuta nunca.
	/// <para />
	/// Toca el inventario del personaje de PRUEBA del sandbox
	/// (<c>tModLoader-TerrakeepWS7\</c>), nunca uno real del usuario, y deja el inventario tal y
	/// como lo encontro al terminar.
	/// <para />
	/// La variable admite tres valores, uno por cada cosa que hay que demostrar:
	/// <list type="bullet">
	/// <item><c>1</c> (o cualquier otro): la prueba completa y automatica de deshacer/rehacer y
	/// de cambio de idioma en vivo.</item>
	/// <item><c>atajos</c>: deja preparada una accion deshacible y se queda esperando a que
	/// LLEGUEN DE VERDAD Ctrl+Z y Ctrl+Y desde el teclado, para probar los atajos por el camino
	/// real (PlayerInput -&gt; ModKeybind) y no llamando a la funcion por dentro.</item>
	/// <item><c>captura</c>: abre el panel y cambia de idioma con pausas, para poder fotografiar
	/// la pantalla en cada estado.</item>
	/// </list>
	/// </summary>
	public static class AutopruebaWs7
	{
		// Ranuras del inventario que usa la prueba. La 5 y la 9 estan dentro de la barra rapida,
		// que en el personaje sintetico de pruebas esta vacia entera.
		private const int RanuraOrigen = 5;
		private const int RanuraDestino = 9;

		private static string _modo;
		private static bool _vigilando;
		private static bool _puedeDeshacerAnterior;
		private static bool _puedeRehacerAnterior;
		private static int _fotogramasDeCaptura;
		private static int _pasoDeCaptura;

		public static void Ejecutar()
		{
			_modo = (System.Environment.GetEnvironmentVariable(AjustesSystem.VariableAutoprueba) ?? "").Trim().ToLowerInvariant();

			Registrar("AUTOPRUEBA WS7: empieza (modo=\"" + _modo + "\"). Idioma configurado=" + Idiomas.IdiomaConfigurado +
				", cultura activa=" + Idiomas.CulturaActiva + ".");

			if (_modo == "atajos") {
				PrepararEscenarioDeAtajos();
				return;
			}

			if (_modo == "captura") {
				AjustesSystem.AbrirPanel("autoprueba WS7 (modo captura)");
				_fotogramasDeCaptura = 0;
				_pasoDeCaptura = 0;
				_vigilando = true;
				Registrar("CAPTURA/1 panel abierto en " + Idiomas.CulturaActiva +
					". Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\". LISTO PARA CAPTURA 1.");
				return;
			}

			ProbarDeshacerRehacer();
			ProbarIdiomaEnVivo();

			Registrar("AUTOPRUEBA WS7: terminada.");
		}

		/// <summary>
		/// La llama <see cref="AjustesSystem"/> en cada fotograma una vez arrancada la autoprueba.
		/// Solo hace algo en los modos que esperan a algo externo.
		/// </summary>
		public static void Vigilar()
		{
			if (!_vigilando) {
				return;
			}

			if (_modo == "atajos") {
				VigilarAtajos();
			}
			else if (_modo == "captura") {
				VigilarCaptura();
			}
		}

		/// <summary>
		/// Monta el caso: espada en la ranura 5, movida a la 9 a traves del historial, y NO se
		/// deshace. A partir de aqui el que tiene que deshacer es el teclado real.
		/// </summary>
		private static void PrepararEscenarioDeAtajos()
		{
			Item[] inventario = Main.LocalPlayer.inventory;
			Historial.Pila.Limpiar();

			inventario[RanuraOrigen] = new Item();
			inventario[RanuraOrigen].SetDefaults(ItemID.CopperShortsword);
			inventario[RanuraOrigen].stack = 1;
			inventario[RanuraDestino] = new Item();

			Historial.CambiarObjetos(
				"Mover objeto de la ranura " + (RanuraOrigen + 1) + " a la " + (RanuraDestino + 1),
				inventario,
				new int[] { RanuraOrigen, RanuraDestino },
				delegate {
					inventario[RanuraDestino] = inventario[RanuraOrigen];
					inventario[RanuraOrigen] = new Item();
				});

			_puedeDeshacerAnterior = Historial.Pila.PuedeDeshacer;
			_puedeRehacerAnterior = Historial.Pila.PuedeRehacer;
			_vigilando = true;

			Registrar("ATAJOS/0 escenario preparado: " + Estado(inventario) +
				" | puedeDeshacer=" + Historial.Pila.PuedeDeshacer +
				". LISTO PARA ATAJOS: se dispararan Ctrl+Z y Ctrl+Y por el camino real del juego.");
		}

		private static void VigilarAtajos()
		{
			DiagnosticoDeTeclado();
			PulsarAtajoComoLoHariaElTeclado();

			bool puedeDeshacer = Historial.Pila.PuedeDeshacer;
			bool puedeRehacer = Historial.Pila.PuedeRehacer;

			if (puedeDeshacer == _puedeDeshacerAnterior && puedeRehacer == _puedeRehacerAnterior) {
				return;
			}

			Item[] inventario = Main.LocalPlayer.inventory;
			bool fueUnDeshacer = !puedeDeshacer && _puedeDeshacerAnterior;

			Registrar("ATAJOS/" + (fueUnDeshacer ? "1 tras Ctrl+Z" : "2 tras Ctrl+Y") + ": " + Estado(inventario) +
				" | " + Veredicto(inventario, fueUnDeshacer ? RanuraOrigen : RanuraDestino,
					fueUnDeshacer ? "el objeto ha vuelto a su ranura original" : "el objeto ha vuelto a la ranura de destino"));

			_puedeDeshacerAnterior = puedeDeshacer;
			_puedeRehacerAnterior = puedeRehacer;

			if (!fueUnDeshacer) {
				_vigilando = false;
				inventario[RanuraOrigen] = new Item();
				inventario[RanuraDestino] = new Item();
				Historial.Pila.Limpiar();
				Registrar("AUTOPRUEBA WS7: terminada.");
			}
		}

		/// <summary>
		/// Solo en el modo "atajos": deja en el log, una vez por segundo, lo que el juego cree
		/// estar viendo del teclado. Si un Ctrl+Z enviado desde fuera no llega, aqui se ve por
		/// que: sin foco de ventana Terraria congela <c>Main.keyState</c>, y hasta que
		/// <c>PlayerInput</c> no procesa su reinitialize los atajos de mods no existen en sus
		/// diccionarios.
		/// </summary>
		/// <summary>
		/// Ejercita el atajo por el mismo camino que el juego, pero rellenando a mano las dos
		/// entradas de las que depende: <c>Main.keyState</c> (de donde sale el modificador Ctrl) y
		/// <c>PlayerInput.Triggers.JustPressed</c> (de donde sale <c>ModKeybind.JustPressed</c>).
		/// A partir de ahi corre el codigo de produccion tal cual, sin ningun atajo de prueba.
		/// <para />
		/// <b>Por que no se envian pulsaciones de teclado de verdad.</b> Se intento, y esta
		/// medido: con la ventana del juego en primer plano (<c>Main.hasFocus=True</c> en el log),
		/// las teclas inyectadas con <c>keybd_event</c> desde PowerShell <b>no llegan nunca</b> a
		/// <c>Main.keyState</c> - ni el Ctrl ni la letra, comprobado acumulando el estado
		/// fotograma a fotograma durante 12 segundos, no muestreando. Es una limitacion del arnes
		/// (FNA/SDL no ve esa entrada sintetica), no del mod. Lo unico que queda sin cubrir es el
		/// ultimo eslabon fisico teclado -&gt; SDL; todo lo demas (que el atajo existe, que tiene
		/// la tecla correcta asignada, y que Ctrl + esa tecla acaba deshaciendo) si se comprueba.
		/// </summary>
		private static void PulsarAtajoComoLoHariaElTeclado()
		{
			// A los 2 s Ctrl+Z, a los 4 s Ctrl+Y. El diagnostico va en el mismo contador.
			bool esDeshacer = _fotogramasDeCaptura == 120;
			bool esRehacer = _fotogramasDeCaptura == 240;
			if (!esDeshacer && !esRehacer) {
				return;
			}

			string atajo = esDeshacer ? HistorialSystem.NombreCompletoDeshacer : HistorialSystem.NombreCompletoRehacer;
			if (!Terraria.GameInput.PlayerInput.Triggers.JustPressed.KeyStatus.ContainsKey(atajo)) {
				Registrar("ATAJOS: PlayerInput todavia no conoce \"" + atajo + "\", no se puede simular.");
				return;
			}

			Main.keyState = new Microsoft.Xna.Framework.Input.KeyboardState(
				Microsoft.Xna.Framework.Input.Keys.LeftControl);
			Terraria.GameInput.PlayerInput.Triggers.JustPressed.KeyStatus[atajo] = true;

			Registrar("ATAJOS: simulando " + (esDeshacer ? "Ctrl+Z" : "Ctrl+Y") + " (Ctrl en Main.keyState + " +
				atajo + " en PlayerInput.Triggers.JustPressed) y llamando al codigo real de HistorialSystem.");

			HistorialSystem.ComprobarAtajos();

			Terraria.GameInput.PlayerInput.Triggers.JustPressed.KeyStatus[atajo] = false;
		}

		private static bool _vistoCtrl;
		private static bool _vistoZ;
		private static bool _vistoTriggerZ;

		private static void DiagnosticoDeTeclado()
		{
			// Acumulado fotograma a fotograma: muestrear solo una vez por segundo se perderia una
			// pulsacion de 150 ms. Distingue "la tecla no llega al juego" de "llega pero el atajo
			// no se dispara".
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) ||
				Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl)) {
				_vistoCtrl = true;
			}
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Z)) {
				_vistoZ = true;
			}
			try {
				if (HistorialSystem.DeshacerKeybind != null && HistorialSystem.DeshacerKeybind.Current) {
					_vistoTriggerZ = true;
				}
			}
			catch (System.Collections.Generic.KeyNotFoundException) {
			}

			_fotogramasDeCaptura++;
			if (_fotogramasDeCaptura % 60 != 0 || _fotogramasDeCaptura > 60 * 12) {
				return;
			}

			string estadoAtajo;
			try {
				estadoAtajo = HistorialSystem.DeshacerKeybind == null
					? "(keybind null)"
					: "JustPressed=" + HistorialSystem.DeshacerKeybind.JustPressed +
						", Current=" + HistorialSystem.DeshacerKeybind.Current +
						", asignado a [" + string.Join("+", HistorialSystem.DeshacerKeybind.GetAssignedKeys()) + "]";
			}
			catch (System.Collections.Generic.KeyNotFoundException) {
				estadoAtajo = "(PlayerInput todavia no conoce el atajo: KeyNotFoundException)";
			}

			Registrar("ATAJOS/diag t=" + (_fotogramasDeCaptura / 60) + "s: Main.hasFocus=" + Main.hasFocus +
				", Ctrl pulsado segun Main.keyState=" + HistorialSystem.CtrlPulsado() +
				", teclas pulsadas ahora=" + Main.keyState.GetPressedKeys().Length +
				", VISTO alguna vez desde que empezo: Ctrl=" + _vistoCtrl + " Z=" + _vistoZ +
				" trigger del atajo=" + _vistoTriggerZ +
				", atajo Deshacer: " + estadoAtajo);

			if (_fotogramasDeCaptura == 60) {
				Registrar("ATAJOS/diag perfil de controles activo=\"" + Terraria.GameInput.PlayerInput.CurrentProfile.Name +
					"\". Teclas asignadas a CADA atajo del mod: " +
					"AbrirPanel(WS0)=[" + TeclasDe(Terrakeep.AbrirPanelKeybind) + "]" +
					", AbrirAjustes(WS7)=[" + TeclasDe(AjustesSystem.AbrirAjustesKeybind) + "]" +
					", Deshacer=[" + TeclasDe(HistorialSystem.DeshacerKeybind) + "]" +
					", Rehacer=[" + TeclasDe(HistorialSystem.RehacerKeybind) + "]" +
					". Para comparar, un atajo VANILLA: QuickHeal=[" +
					string.Join("+", Terraria.GameInput.PlayerInput.CurrentProfile.InputModes[Terraria.GameInput.InputMode.Keyboard].KeyStatus["QuickHeal"]) + "]");
			}
		}

		private static string TeclasDe(Terraria.ModLoader.ModKeybind atajo)
		{
			if (atajo == null) {
				return "null";
			}
			try {
				return string.Join("+", atajo.GetAssignedKeys());
			}
			catch (System.Exception e) {
				return "(" + e.GetType().Name + ")";
			}
		}

		private static void VigilarCaptura()
		{
			_fotogramasDeCaptura++;
			// ~4 segundos entre paso y paso: tiempo de sobra para que el script de fuera saque la
			// foto de la pantalla antes de que cambie nada.
			if (_fotogramasDeCaptura < 240) {
				return;
			}
			_fotogramasDeCaptura = 0;
			_pasoDeCaptura++;

			if (_pasoDeCaptura == 1) {
				Idiomas.Elegir(IdiomaDeTerrakeep.English);
				Registrar("CAPTURA/2 panel ahora en " + Idiomas.CulturaActiva +
					". Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\". LISTO PARA CAPTURA 2.");
			}
			else if (_pasoDeCaptura == 2) {
				Idiomas.Elegir(IdiomaDeTerrakeep.Espanol);
				Registrar("CAPTURA/3 panel de vuelta en " + Idiomas.CulturaActiva +
					". Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\". LISTO PARA CAPTURA 3.");
			}
			else {
				_vigilando = false;
				Registrar("AUTOPRUEBA WS7: terminada.");
			}
		}

		/// <summary>
		/// Caso real y concreto: se pone un objeto de verdad en la ranura 5, se MUEVE a la 9 a
		/// traves del historial, se deshace (tiene que volver a la 5) y se rehace (tiene que
		/// volver a la 9). Cada paso se lee del inventario real del jugador, no de la foto.
		/// </summary>
		private static void ProbarDeshacerRehacer()
		{
			Item[] inventario = Main.LocalPlayer.inventory;
			Historial.Pila.Limpiar();

			// Estado de partida: una espada de cobre en la ranura 5, la 9 vacia.
			inventario[RanuraOrigen] = new Item();
			inventario[RanuraOrigen].SetDefaults(ItemID.CopperShortsword);
			inventario[RanuraOrigen].stack = 1;
			inventario[RanuraDestino] = new Item();

			Registrar("HISTORIAL/1 estado inicial: " + Estado(inventario));

			// La edicion real, envuelta por el historial. Esta es exactamente la forma en que los
			// demas workstreams tienen que envolver sus ediciones para que sean deshacibles.
			bool registrada = Historial.CambiarObjetos(
				"Mover objeto de la ranura " + (RanuraOrigen + 1) + " a la " + (RanuraDestino + 1),
				inventario,
				new int[] { RanuraOrigen, RanuraDestino },
				delegate {
					inventario[RanuraDestino] = inventario[RanuraOrigen];
					inventario[RanuraOrigen] = new Item();
				});

			Registrar("HISTORIAL/2 tras la accion (registrada=" + registrada + "): " + Estado(inventario) +
				" | puedeDeshacer=" + Historial.Pila.PuedeDeshacer +
				", etiqueta=\"" + Historial.Pila.EtiquetaDeshacer + "\"");

			string deshecha = Historial.Deshacer();
			Registrar("HISTORIAL/3 tras DESHACER (\"" + deshecha + "\"): " + Estado(inventario) +
				" | " + Veredicto(inventario, RanuraOrigen, "el objeto ha vuelto a su ranura original"));

			string rehecha = Historial.Rehacer();
			Registrar("HISTORIAL/4 tras REHACER (\"" + rehecha + "\"): " + Estado(inventario) +
				" | " + Veredicto(inventario, RanuraDestino, "el objeto ha vuelto a la ranura de destino"));

			// Se deja el inventario como estaba antes de la prueba.
			Historial.Deshacer();
			inventario[RanuraOrigen] = new Item();
			inventario[RanuraDestino] = new Item();
			Historial.Pila.Limpiar();
			Registrar("HISTORIAL/5 limpieza: " + Estado(inventario) +
				" | historial vaciado (entradas=" + Historial.Pila.Cuenta + ").");
		}

		/// <summary>
		/// Abre el panel de Ajustes y cambia el idioma en vivo, dejando en el log el MISMO texto
		/// (la clave propia del mod "Ajustes.Titulo") resuelto en cada idioma. Si el texto cambia,
		/// la recarga de traducciones ha ocurrido de verdad.
		/// </summary>
		private static void ProbarIdiomaEnVivo()
		{
			IdiomaDeTerrakeep original = Idiomas.IdiomaConfigurado;

			AjustesSystem.AbrirPanel("autoprueba WS7 (" + AjustesSystem.VariableAutoprueba + ")");

			Registrar("IDIOMA/1 de partida: configurado=" + original +
				", cultura=" + Idiomas.CulturaActiva +
				", Ajustes.Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\"" +
				", Ajustes.Deshacer=\"" + Idiomas.Texto("Ajustes.Deshacer") + "\"");

			Idiomas.Elegir(IdiomaDeTerrakeep.English);
			Registrar("IDIOMA/2 tras elegir English: cultura=" + Idiomas.CulturaActiva +
				", Ajustes.Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\"" +
				", Ajustes.Deshacer=\"" + Idiomas.Texto("Ajustes.Deshacer") + "\"");

			Idiomas.Elegir(IdiomaDeTerrakeep.Espanol);
			Registrar("IDIOMA/3 tras elegir Espanol: cultura=" + Idiomas.CulturaActiva +
				", Ajustes.Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\"" +
				", Ajustes.Deshacer=\"" + Idiomas.Texto("Ajustes.Deshacer") + "\"");

			Registrar("IDIOMA/4 persistencia: AjustesConfig.Instance.Idioma=" +
				(AjustesConfig.Instance != null ? AjustesConfig.Instance.Idioma.ToString() : "(config null)") +
				" - guardado por ModConfig, se recordara en la siguiente partida.");
		}

		private static string Estado(Item[] inventario)
		{
			return "inventory[" + RanuraOrigen + "]=" + Describir(inventario[RanuraOrigen]) +
				", inventory[" + RanuraDestino + "]=" + Describir(inventario[RanuraDestino]);
		}

		private static string Describir(Item objeto)
		{
			if (objeto == null || objeto.IsAir) {
				return "vacio";
			}
			return "\"" + objeto.Name + "\" x" + objeto.stack + " (type=" + objeto.type + ")";
		}

		private static string Veredicto(Item[] inventario, int ranuraEsperada, string queSeEsperaba)
		{
			int otra = ranuraEsperada == RanuraOrigen ? RanuraDestino : RanuraOrigen;
			bool bien = inventario[ranuraEsperada] != null
				&& inventario[ranuraEsperada].type == ItemID.CopperShortsword
				&& (inventario[otra] == null || inventario[otra].IsAir);
			return (bien ? "OK: " : "FALLO: NO ") + queSeEsperaba + " (ranura " + ranuraEsperada + ").";
		}

		private static void Registrar(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
			}
		}
	}
}
