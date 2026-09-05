using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.UI.Builds;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>
	/// Punto de entrada del panel de Builds (WS4): carga el catalogo, registra el atajo de
	/// teclado propio y abre/cierra el panel.
	/// </summary>
	/// <remarks>
	/// El atajo se registra <b>desde aqui</b> y no desde la clase <c>Terrakeep</c> a proposito:
	/// asi este workstream no toca ni un archivo compartido con los demas.
	/// <c>KeybindLoader.RegisterKeybind</c> solo necesita el <c>Mod</c>, que un
	/// <c>ModSystem</c> ya tiene en <c>ModType.Mod</c> (asignado antes de llamar a
	/// <c>Load()</c>, codigo real de <c>ModType.ILoadable.Load</c>).
	/// </remarks>
	public class PanelBuildsSystem : ModSystem
	{
		/// <summary>Abre el panel de Builds solo, para verificarlo sin depender de que nadie
		/// pulse una tecla (mismo mecanismo que la autoprueba de WS0, pero con variable propia
		/// para no disparar los dos paneles a la vez).</summary>
		public const string VariableAutoprueba = "TERRAKEEP_AUTOTEST_BUILDS";

		/// <summary>SOLO ARNES DE PRUEBAS: lista de pid separados por coma que se meten en el
		/// inventario del personaje ANTES de abrir el panel, para poder verificar "ya lo tienes"
		/// y auto-equipar contra objetos concretos. La funcionalidad del mod nunca crea objetos;
		/// esto es preparar el escenario de la prueba, y solo se activa con la variable puesta,
		/// que nadie tiene definida jugando.</summary>
		public const string VariableSembrar = "TERRAKEEP_BUILDS_SEMBRAR";

		/// <summary>SOLO ARNES DE PRUEBAS: clave de clase (melee/ranged/...) sobre la que ejecutar
		/// auto-equipar automaticamente tras abrir el panel.</summary>
		public const string VariableAutoEquipar = "TERRAKEEP_BUILDS_AUTOEQUIPAR";

		/// <summary>SOLO ARNES DE PRUEBAS: clave de fuente ("vanilla" / "calamity") que
		/// seleccionar antes de auto-equipar.</summary>
		public const string VariableFuente = "TERRAKEEP_BUILDS_FUENTE";

		private static ModKeybind _atajo;
		private static PanelBuildsState _panel;
		private static uint _ultimoFotogramaAlternado;

		private static bool _autopruebaHecha;
		private static int _fotogramasEnMundo;

		/// <summary>true si el panel de Builds esta abierto ahora mismo.</summary>
		public static bool PanelAbierto =>
			Main.InGameUI != null && Main.InGameUI.CurrentState is PanelBuildsState;

		public override void Load()
		{
			RegistroBuilds.Mod = Mod;

			// Los .json hay que leerlos mientras el .tmod sigue abierto (ver CatalogoBuilds).
			CatalogoBuilds.LeerArchivos(Mod);

			if (!Main.dedServ) {
				// L no esta asignada a nada en los controles por defecto de Terraria, y K y J ya
				// las usan otros paneles de este mismo mod. Reasignable en Ajustes > Controles.
				_atajo = KeybindLoader.RegisterKeybind(Mod, "AbrirBuilds", Keys.L);
			}
		}

		public override void PostSetupContent()
		{
			// Aqui ya han registrado su contenido TODOS los mods, asi que ItemID.Search conoce
			// tambien los objetos de Calamity. Antes de este punto los pid "CalamityMod/..." no
			// se podrian resolver.
			CatalogoBuilds.Resolver(Mod);
		}

		public override void Unload()
		{
			_atajo = null;
			_panel = null;
			RegistroBuilds.Mod = null;
			CatalogoBuilds.Descargar();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// Primero y sin condiciones, por el mismo motivo que en WS0: ModKeybind.JustPressed
			// puede lanzar KeyNotFoundException durante los primeros fotogramas de una partida
			// (PlayerInput.Triggers todavia no conoce los atajos de mods) y abortaria el resto
			// del metodo.
			ActualizarAutoprueba();

			if (_atajo == null) {
				return;
			}

			try {
				if (_atajo.JustPressed) {
					AlternarPanel("atajo de teclado (tecla L)");
				}
			}
			catch (KeyNotFoundException) {
				// Se autocorrige solo en cuanto el motor procesa el PlayerInput.reinitialize.
			}
		}

		public static void AlternarPanel(string origen)
		{
			if (_ultimoFotogramaAlternado == Main.GameUpdateCount) {
				return;
			}
			_ultimoFotogramaAlternado = Main.GameUpdateCount;

			if (PanelAbierto) {
				CerrarPanel(origen);
			}
			else {
				AbrirPanel(origen);
			}
		}

		public static void AbrirPanel(string origen)
		{
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}

			if (!CatalogoBuilds.Listo) {
				RegistroBuilds.Aviso(
					$"{Terrakeep.LogTag} Builds: no hay catalogo cargado, no se abre el panel.");
				return;
			}

			// Instancia nueva cada vez, igual que el panel de WS0: OnInitialize solo corre una vez
			// por instancia y el estado depende del jugador de la partida actual.
			_panel = new PanelBuildsState();
			IngameFancyUI.OpenUIState(_panel);

			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} PANEL BUILDS ABIERTO via {origen}. " +
				$"Jugador: \"{Main.LocalPlayer.name}\". Mundo: \"{Main.worldName}\". " +
				$"Fuente=\"{_panel.FuenteActual?.Etiqueta}\" Etapa=\"{_panel.EtapaActual?.Etiqueta}\" " +
				$"Clase=\"{_panel.ClaseActual?.Etiqueta}\". " +
				$"Main.inFancyUI={Main.inFancyUI}, InGameUI.CurrentState={Main.InGameUI.CurrentState?.GetType().FullName}");

			RegistrarEstadoBuild(_panel, "al abrir");
		}

		public static void CerrarPanel(string origen)
		{
			if (!PanelAbierto) {
				return;
			}

			RegistroBuilds.Linea($"{Terrakeep.LogTag} PANEL BUILDS CERRADO via {origen}.");
			IngameFancyUI.Close();
			_panel = null;
		}

		/// <summary>Deja en el log, objeto a objeto, que tiene y que no tiene el jugador de la
		/// build seleccionada. Es la evidencia real del "ya lo tienes".</summary>
		private static void RegistrarEstadoBuild(PanelBuildsState panel, string momento)
		{
			ClaseBuild clase = panel.ClaseActual;
			if (clase == null) {
				return;
			}

			Player jugador = Main.LocalPlayer;
			int tiene = 0;
			int total = 0;
			List<string> lineas = new List<string>();

			foreach (ObjetoBuild objeto in clase.Todos()) {
				total++;
				if (!objeto.Resuelto) {
					lineas.Add($"{objeto.Nombre} [{objeto.Pid}]: NO EXISTE en esta partida");
					continue;
				}
				UbicacionObjeto donde = EquipoJugador.Buscar(jugador, objeto.Tipo);
				if (donde != null) {
					tiene++;
					lineas.Add($"{objeto.Nombre} [{objeto.Pid}] type={objeto.Tipo}: LO TIENES en {donde}");
				}
				else {
					lineas.Add($"{objeto.Nombre} [{objeto.Pid}] type={objeto.Tipo}: no lo tienes");
				}
			}

			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} Builds \"ya lo tienes\" ({momento}): {tiene} de {total} objetos de " +
				$"\"{panel.EtapaActual?.Etiqueta}\" / {clase.Etiqueta}.");
			foreach (string linea in lineas) {
				RegistroBuilds.Linea($"{Terrakeep.LogTag}   - {linea}");
			}
		}

		private static void ActualizarAutoprueba()
		{
			if (_autopruebaHecha || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableAutoprueba))) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < 180) {
				return;
			}

			_autopruebaHecha = true;
			// La marca la pone el script de verificacion: el client.log es compartido por todas
			// las instancias del juego y tModLoader lo rota (client.log -> client1.log), asi que
			// con varios workstreams probando a la vez hace falta poder identificar CUAL es el
			// log de esta ejecucion concreta.
			string marca = Environment.GetEnvironmentVariable("TERRAKEEP_BUILDS_MARCA");
			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: {VariableAutoprueba} detectada. Marca de ejecucion: {marca}");

			SembrarObjetosDePrueba();
			AbrirPanel("autoprueba (" + VariableAutoprueba + ")");
			AutoEquiparDePrueba();
		}

		/// <summary>
		/// SOLO ARNES DE PRUEBAS. Mete en el inventario los objetos que diga
		/// <see cref="VariableSembrar"/> para poder verificar "ya lo tienes" y auto-equipar sin
		/// tener que jugar una partida entera. No forma parte de la funcionalidad del mod: el
		/// auto-equipar real (<see cref="AutoEquipar"/>) nunca crea objetos.
		/// </summary>
		private static void SembrarObjetosDePrueba()
		{
			string lista = Environment.GetEnvironmentVariable(VariableSembrar);
			if (string.IsNullOrEmpty(lista)) {
				return;
			}

			Player jugador = Main.LocalPlayer;
			List<string> puestos = new List<string>();

			foreach (string bruto in lista.Split(',')) {
				string pid = bruto.Trim();
				if (pid.Length == 0) {
					continue;
				}

				int tipo = CatalogoBuilds.ResolverPid(pid);
				if (tipo <= 0) {
					puestos.Add($"{pid}=NO RESUELTO");
					continue;
				}

				int hueco = EquipoJugador.PrimerHuecoMochila(jugador);
				if (hueco < 0) {
					puestos.Add($"{pid}=SIN HUECO");
					continue;
				}

				jugador.inventory[hueco] = new Item();
				jugador.inventory[hueco].SetDefaults(tipo);
				jugador.inventory[hueco].stack = 1;
				puestos.Add($"{pid}(type={tipo})->inventario[{hueco}]");
			}

			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: objetos sembrados para la prueba: {string.Join(", ", puestos)}");
		}

		/// <summary>
		/// SOLO ARNES DE PRUEBAS: comprueba el filtro por clase pulsando de verdad las pildoras.
		/// </summary>
		private static void ProbarPildorasDeClase(string claveFinal)
		{
			EtapaBuild etapa = _panel.EtapaActual;
			if (etapa == null || etapa.Clases.Count < 2) {
				return;
			}

			string antes = _panel.ClaseActual?.Etiqueta;
			int ultima = etapa.Clases.Count - 1;
			string pulsada = _panel.PulsarPildoraClase(ultima);
			string despues = _panel.ClaseActual?.Etiqueta;

			RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: filtro por clase con un clic real en la " +
				$"pildora {ultima + 1} de {etapa.Clases.Count} (\"{pulsada}\"): clase \"{antes}\" -> \"{despues}\". " +
				$"Objetos de la clase ahora: {ContarObjetos(_panel.ClaseActual)}.");

			// Se deja el panel como lo pedia la prueba antes de auto-equipar.
			_panel.SeleccionarClase(claveFinal);
		}

		private static int ContarObjetos(ClaseBuild clase)
		{
			if (clase == null) {
				return 0;
			}
			int n = 0;
			foreach (ObjetoBuild o in clase.Todos()) {
				n++;
			}
			return n;
		}

		/// <summary>SOLO ARNES DE PRUEBAS: ejecuta auto-equipar sobre la clase indicada.</summary>
		private static void AutoEquiparDePrueba()
		{
			string clase = Environment.GetEnvironmentVariable(VariableAutoEquipar);
			if (string.IsNullOrEmpty(clase) || _panel == null) {
				return;
			}

			// Fuente opcional: sin esto el panel se queda en la primera (Vanilla), que no tiene
			// clase "rogue" - hace falta para poder ejercitar de verdad la fuente de Calamity.
			string fuente = Environment.GetEnvironmentVariable(VariableFuente);
			if (!string.IsNullOrEmpty(fuente)) {
				bool ok = _panel.SeleccionarFuente(fuente.Trim());
				RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: fuente pedida \"{fuente.Trim()}\" -> " +
					(ok ? $"seleccionada (\"{_panel.FuenteActual?.Etiqueta}\")" : "NO disponible en esta partida"));
			}

			_panel.SeleccionarClase(clase.Trim());
			RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: filtro activo -> " +
				$"Fuente=\"{_panel.FuenteActual?.Etiqueta}\" Etapa=\"{_panel.EtapaActual?.Etiqueta}\" " +
				$"Clase=\"{_panel.ClaseActual?.Etiqueta}\" (clave pedida: \"{clase.Trim()}\").");

			RegistrarEstadoBuild(_panel, "con el filtro ya aplicado");

			// Filtro por clase de punta a punta: se pulsa de verdad la ULTIMA pildora de clase
			// (disparando su OnLeftClick, el mismo camino que un clic de raton) y se comprueba
			// que el panel cambia de clase, y luego se vuelve a la que pedia la prueba.
			ProbarPildorasDeClase(clase.Trim());

			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: equipo ANTES de auto-equipar: " +
				AutoEquipar.EstadoEquipo(Main.LocalPlayer));

			_panel.EjecutarAutoEquipar();

			RegistrarEstadoBuild(_panel, "despues de auto-equipar");

			// Segunda pasada: comprueba que auto-equipar es IDEMPOTENTE. Si estuviera moviendo
			// objetos a lo tonto (o creandolos), aqui volveria a contar movimientos; lo correcto
			// es que salga movidos=0 y todo lo demas como "ya colocados".
			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: segunda pasada de auto-equipar (prueba de idempotencia).");
			_panel.EjecutarAutoEquipar();
		}
	}
}
