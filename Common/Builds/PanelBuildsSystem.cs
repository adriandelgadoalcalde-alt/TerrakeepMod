using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Builds;
using TerrakeepMod.UI.Panel;

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

		/// <summary>SOLO ARNES DE PRUEBAS: conjunto de equipo (0/1/2) al que aplicar el
		/// auto-equipar, pulsando de verdad su pildora en el selector de destino. Si no se define,
		/// se queda con el que ya tuviera seleccionado el panel (el conjunto ACTIVO, por
		/// defecto).</summary>
		public const string VariableLoadoutObjetivo = "TERRAKEEP_BUILDS_LOADOUT_OBJETIVO";

		/// <summary>SOLO ARNES DE PRUEBAS: si se define (0/1/2), tras las dos pasadas de
		/// auto-equipar se llama a <c>Player.TrySwitchingLoadout</c> con este indice para simular
		/// que el jugador cambia de verdad de conjunto ACTIVO, y se deja en el log el equipo que
		/// queda puesto. Es la comprobacion de "si aplico a un conjunto que no era el activo, al
		/// activarlo si lleva puesto lo aplicado".</summary>
		public const string VariableCambiarLoadoutActivoA = "TERRAKEEP_BUILDS_CAMBIAR_LOADOUT_A";

		private static ModKeybind _atajo;

		private static bool _autopruebaHecha;
		private static int _fotogramasEnMundo;

		/// <summary>
		/// SOLO ARNES DE PRUEBAS: -1 = no hay ninguna secuencia de capturas en marcha. >= 0 = cuantos
		/// fotogramas han pasado desde el ULTIMO clic real en "Auto-equipar" que se quiso comprobar.
		/// <para />
		/// Existe para reproducir de verdad el bug que se investigo esta sesion ("build se sigue sin
		/// aplicar"): la mitad real de la causa era que el mensaje de resultado se pisaba solo con la
		/// leyenda de colores antes de un cuarto de segundo (ver el comentario de
		/// <c>ContenidoBuilds._mensajeResultado</c>). Una sola captura justo tras el clic no lo habria
		/// demostrado - hacia falta comprobar que el mensaje SIGUE ahi varios fotogramas despues, que
		/// es justo lo que hace esta cuenta atras escalonada.
		/// </para>
		/// </summary>
		private static int _fotogramasTrasClicBoton = -1;

		/// <summary>Instantes (en fotogramas desde el clic) en los que se toma una captura + se deja
		/// en el log el mensaje/color/fotogramas-restantes reales de <c>ContenidoBuilds</c>. 0 = el
		/// mismo fotograma del clic; el resto reparte la ventana de casi 6 s
		/// (<c>ContenidoBuilds.FotogramasMensajeResultado</c>) para poder ver que el mensaje aguanta
		/// mucho mas que el cuarto de segundo que duraba antes del arreglo, y que luego SI vuelve a
		/// la leyenda (no se queda pegado para siempre).</summary>
		private static readonly int[] OffsetsCaptura = { 0, 20, 90, 200 };

		private static int _siguienteOffsetCaptura;
		private static string _prefijoCapturaActual = "builds";

		/// <summary>El atajo de Builds. Lo registra este sistema y lo LEE
		/// <c>PanelTerrakeepSystem</c>, que es quien abre el panel unico en esta pestaña.</summary>
		public static ModKeybind Atajo => _atajo;

		/// <summary>true si el panel esta abierto Y en la pestaña de Builds.</summary>
		public static bool PanelAbierto => PanelTerrakeepSystem.AreaAbiertaEs(AreaTerrakeep.Builds);

		/// <summary>El contenido de Builds montado ahora mismo, o null.</summary>
		public static ContenidoBuilds Contenido
		{
			get
			{
				PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
				return panel != null ? panel.Builds : null;
			}
		}

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
			RegistroBuilds.Mod = null;
			CatalogoBuilds.Descargar();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			ActualizarAutoprueba();
			ActualizarCapturasProgramadas();
		}

		/// <summary>
		/// SOLO ARNES DE PRUEBAS: arranca una secuencia de capturas escalonadas tras un clic real en
		/// "Auto-equipar" (ver <see cref="OffsetsCaptura"/> y <see cref="_fotogramasTrasClicBoton"/>).
		/// </summary>
		private static void IniciarSecuenciaDeCapturas(string prefijo)
		{
			_prefijoCapturaActual = prefijo;
			_fotogramasTrasClicBoton = 0;
			_siguienteOffsetCaptura = 0;
		}

		/// <summary>SOLO ARNES DE PRUEBAS: avanza la secuencia de capturas iniciada por
		/// <see cref="IniciarSecuenciaDeCapturas"/>, tomando una en cada offset de
		/// <see cref="OffsetsCaptura"/> y dejando en el log el estado REAL de
		/// <c>ContenidoBuilds.MensajeResultado</c>/<c>FotogramasMensajeResultado</c> en ese instante -
		/// la prueba de que el mensaje sigue vivo mucho mas alla del cuarto de segundo que duraba
		/// antes del arreglo.</summary>
		private static void ActualizarCapturasProgramadas()
		{
			if (_fotogramasTrasClicBoton < 0) {
				return;
			}

			if (_siguienteOffsetCaptura < OffsetsCaptura.Length &&
				_fotogramasTrasClicBoton == OffsetsCaptura[_siguienteOffsetCaptura]) {

				ContenidoBuilds panel = Contenido;
				string estado = panel != null
					? $"mensaje=\"{panel.MensajeResultado}\" fotogramasRestantes={panel.FotogramasMensajeResultado}"
					: "(sin panel montado)";
				string nombre = $"{_prefijoCapturaActual}-{_fotogramasTrasClicBoton:D3}f";
				RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: captura +{_fotogramasTrasClicBoton} " +
					$"fotogramas tras el clic real en Auto-equipar. {estado}. {GuardarCapturaBuilds(nombre)}");
				_siguienteOffsetCaptura++;
			}

			if (_siguienteOffsetCaptura >= OffsetsCaptura.Length) {
				_fotogramasTrasClicBoton = -1;
				return;
			}

			_fotogramasTrasClicBoton++;
		}

		/// <summary>
		/// SOLO ARNES DE PRUEBAS: version propia y autonoma de la tecnica real de
		/// <c>Common/Panel/CapturaDePantalla.cs</c> (<c>GraphicsDevice.GetBackBufferData</c> +
		/// <c>Texture2D.SaveAsPng</c>, la unica que de verdad funciona con FNA - ver el XMLdoc de esa
		/// clase para el porque de <c>CopyFromScreen</c>/<c>PrintWindow</c> no sirven). Se duplica
		/// aqui, en vez de añadir la variable de esta autoprueba a la lista <c>Permitida</c> de esa
		/// clase compartida, para no tocar ni una linea fuera de <c>Common/Builds</c>/<c>UI/Builds</c>
		/// mientras otros agentes trabajan en paralelo en el resto del mod.
		/// </summary>
		private static string GuardarCapturaBuilds(string nombre)
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

				string carpeta = Path.Combine(Main.SavePath, "terrakeep-capturas");
				Directory.CreateDirectory(carpeta);
				string ruta = Path.Combine(carpeta, nombre + ".png");

				using (Texture2D textura = new Texture2D(dispositivo, ancho, alto)) {
					textura.SetData(pixeles);
					using (FileStream archivo = File.Create(ruta)) {
						textura.SaveAsPng(archivo, ancho, alto);
					}
				}

				return "captura guardada en \"" + ruta + "\" (" + ancho + "x" + alto + ")";
			}
			catch (Exception e) {
				return "captura fallida: " + e.GetType().Name + ": " + e.Message;
			}
		}

		/// <summary>Abre el panel en la pestaña de Builds, o lo cierra si ya estaba ahi.</summary>
		public static void AlternarPanel(string origen)
		{
			PanelTerrakeepSystem.AlternarArea(AreaTerrakeep.Builds, origen);
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

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Builds, origen);

			ContenidoBuilds contenido = Contenido;
			if (contenido != null) {
				RegistroBuilds.Linea(
					$"{Terrakeep.LogTag} Builds: Fuente=\"{contenido.FuenteActual?.Etiqueta}\" " +
					$"Etapa=\"{contenido.EtapaActual?.Etiqueta}\" Clase=\"{contenido.ClaseActual?.Etiqueta}\".");
				RegistrarEstadoBuild(contenido, "al abrir");
			}
		}

		public static void CerrarPanel(string origen)
		{
			PanelTerrakeepSystem.CerrarPanel(origen);
		}

		/// <summary>Deja en el log, objeto a objeto, que tiene y que no tiene el jugador de la
		/// build seleccionada. Es la evidencia real del "ya lo tienes".</summary>
		private static void RegistrarEstadoBuild(ContenidoBuilds panel, string momento)
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
			ContenidoBuilds _panel = Contenido;
			if (_panel == null) {
				return;
			}
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
			ContenidoBuilds _panel = Contenido;
			if (string.IsNullOrEmpty(clase) || _panel == null) {
				return;
			}

			Player jugador = Main.LocalPlayer;

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

			// Conjunto de DESTINO (WS4 ampliado): a cual de los 3 conjuntos de equipo va a parar
			// el auto-equipar. Se pulsa de verdad la pildora, el mismo camino que un clic real.
			string loadoutObjetivo = Environment.GetEnvironmentVariable(VariableLoadoutObjetivo);
			if (!string.IsNullOrEmpty(loadoutObjetivo) && int.TryParse(loadoutObjetivo.Trim(), out int indiceObjetivo)) {
				bool pulsado = _panel.PulsarPildoraLoadoutObjetivo(indiceObjetivo);
				RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: conjunto de destino pedido={indiceObjetivo + 1} " +
					$"(pildora pulsada de verdad={pulsado}) -> seleccionado ahora={_panel.LoadoutObjetivo + 1}. " +
					$"Conjunto ACTIVO real ahora mismo={jugador.CurrentLoadoutIndex + 1}.");
			}

			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: equipo ANTES de auto-equipar (los 3 conjuntos): " +
				EstadoTresConjuntos(jugador));
			RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: {GuardarCapturaBuilds("builds-antes")}");

			// Clic REAL sobre el boton "Auto-equipar" (UIElement.LeftClick en su centro real de
			// pantalla, via ContenidoBuilds.PulsarBotonAutoEquipar), no una llamada directa a
			// EjecutarAutoEquipar por dentro. El bug real que se investigo esta sesion
			// ("build se sigue sin aplicar", reportado por el usuario probando el mod de verdad) NO
			// estaba en que el clic no llegara al boton - estaba en que el UNICO aviso visible de que
			// el clic habia hecho algo (el mensaje de resultado) se pisaba solo con la leyenda de
			// colores antes de un cuarto de segundo. Reproducir el clic por el camino exacto del
			// raton es lo que hace falta para que esta prueba hubiera detectado ese bug de haber
			// estado ahi antes del arreglo.
			_panel.PulsarBotonAutoEquipar();
			RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: clic real en \"Auto-equipar\" -> " +
				$"mensaje inmediato=\"{_panel.MensajeResultado}\" fotogramasRestantes={_panel.FotogramasMensajeResultado}.");
			RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: {GuardarCapturaBuilds("builds-justo-tras-clic")}");

			RegistrarEstadoBuild(_panel, "despues de auto-equipar");
			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: equipo DESPUES de auto-equipar (los 3 conjuntos): " +
				EstadoTresConjuntos(jugador));

			// Segunda pasada: comprueba que auto-equipar es IDEMPOTENTE. Si estuviera moviendo
			// objetos a lo tonto (o creandolos), aqui volveria a contar movimientos; lo correcto
			// es que salga movidos=0 y todo lo demas como "ya colocados". Tambien por clic real, por
			// la misma razon de arriba: un segundo clic del usuario tiene que comportarse igual de
			// bien que el primero.
			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: segunda pasada de auto-equipar (prueba de idempotencia).");
			_panel.PulsarBotonAutoEquipar();
			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: equipo tras la segunda pasada (los 3 conjuntos): " +
				EstadoTresConjuntos(jugador));

			// Simular que el jugador cambia de verdad de conjunto ACTIVO (Player.TrySwitchingLoadout,
			// la misma API oficial que ya usa la pestaña Equipo de WS1) para comprobar que lo
			// aplicado a un conjunto que ANTES no era el activo, ahora que SI lo es, se ve puesto.
			string cambiarA = Environment.GetEnvironmentVariable(VariableCambiarLoadoutActivoA);
			if (!string.IsNullOrEmpty(cambiarA) && int.TryParse(cambiarA.Trim(), out int indiceCambiar)) {
				int antes = jugador.CurrentLoadoutIndex;
				jugador.TrySwitchingLoadout(indiceCambiar);
				RegistroBuilds.Linea($"{Terrakeep.LogTag} AUTOPRUEBA BUILDS: cambio de conjunto ACTIVO real pedido -> " +
					$"{indiceCambiar + 1}. CurrentLoadoutIndex antes={antes + 1} ahora={jugador.CurrentLoadoutIndex + 1}. " +
					$"Equipo puesto ahora (Player.armor, el que dibuja y usa el juego): " +
					$"{AutoEquipar.EstadoEquipo(jugador, jugador.armor)}");
			}

			// Secuencia de capturas escalonadas (ver OffsetsCaptura): demuestra, con capturas reales
			// del back buffer y no solo con logs, que el mensaje de resultado del ultimo clic SIGUE
			// en pantalla mucho mas alla del cuarto de segundo que duraba antes del arreglo, y que
			// pasados los 6 s vuelve solo a la leyenda de colores (no se queda pegado para siempre).
			IniciarSecuenciaDeCapturas("builds-persistencia");
		}

		/// <summary>Foto de los 3 conjuntos de equipo a la vez, marcando cual es el ACTIVO ahora
		/// mismo. Evidencia real de que auto-equipar escribio donde tenia que escribir y no
		/// tambien (ni solo) en los otros dos.</summary>
		private static string EstadoTresConjuntos(Player jugador)
		{
			System.Text.StringBuilder texto = new System.Text.StringBuilder();
			for (int i = 0; i < jugador.Loadouts.Length; i++) {
				bool activo = EquipoJugador.EsLoadoutActivo(jugador, i);
				texto.Append($"[Conjunto {i + 1}{(activo ? " *ACTIVO*" : "")}: " +
					$"{AutoEquipar.EstadoEquipo(jugador, EquipoJugador.ArmorDe(jugador, i))}] ");
			}
			return texto.ToString().TrimEnd();
		}
	}
}
