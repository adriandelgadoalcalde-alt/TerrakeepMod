using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Panel;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Verificacion EN EL JUEGO REAL de la idea 9 (guia de grupo multijugador), con DOS PROCESOS
	/// DISTINTOS de tModLoader conectados a un servidor dedicado real - no una simulacion de
	/// paquetes ni un solo cliente hablando consigo mismo: el protocolo de red REAL de Terraria
	/// entre dos clientes de verdad, orquestado por <c>scripts\verificar-grupo.ps1</c> (servidor
	/// headless + este cliente OBSERVADOR + un segundo cliente COMPAÑERO).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Dos roles, un solo archivo, dos variables de entorno distintas (una por proceso):
	/// </para>
	/// <list type="bullet">
	/// <item><b>Companero</b> (<see cref="VariableCompanero"/>): en cuanto entra al mundo, se pone
	/// encima una cantidad EXACTA y conocida de un objeto real (<see cref="CantidadMaderaConocida"/>
	/// de Madera) y una pieza de armadura real (Casco de Cobre). No hace falta llamar a
	/// <c>NetMessage</c> a mano: <c>Main.TrySyncingMyPlayer()</c> ya vigila esas mismas ranuras
	/// cada actualizacion de red y las reenvia sola en cuanto detecta el cambio (ver la cabecera
	/// de <see cref="GuiaGrupo"/>). Despues se queda ahi, conectado, sin hacer nada mas.</item>
	/// <item><b>Observador</b> (<see cref="VariableObservador"/>): espera a que
	/// <see cref="GuiaGrupo.IndicesConectados"/> vea a un companero real, y entonces LEE su estado
	/// por la via de produccion (<see cref="EstadoJugadorGuia.CuantosLleva"/>,
	/// <see cref="EstadoJugadorGuia.DefensaDe"/>, <see cref="EvaluadorGuia.Evaluar(RequisitoGuia,
	/// Player)"/>) y comprueba que coincide EXACTAMENTE con lo que el companero puso - si la red
	/// no sincronizara de verdad la mochila/armadura de otro jugador (que era el LIMITE REAL mal
	/// investigado antes de esta correccion), esto saldria en 0/false siempre, no por casualidad.
	/// </item>
	/// </list>
	/// </remarks>
	public static class AutopruebaGrupo
	{
		public const string VariableObservador = "TERRAKEEP_AUTOTEST_GRUPO_OBSERVADOR";
		public const string VariableCompanero = "TERRAKEEP_AUTOTEST_GRUPO_COMPANERO";

		/// <summary>Cuanta Madera (ItemID.Wood) pone el companero en su mochila - un numero exacto
		/// y arbitrario, elegido solo para que una coincidencia sea imposible por casualidad.</summary>
		public const int CantidadMaderaConocida = 17;

		private const int FotogramasDeEspera = 180;
		private const int FotogramasEntrePasos = 10;
		private const int FotogramasMaximosDeEspera = 1800; // 30 s a 60 fps: red local, de sobra.

		private static bool _comprobada;
		private static bool _esObservador;
		private static bool _esCompanero;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;
		private static bool _repetir;
		private static int _fotogramasEsperando;
		private static int _indiceCompanero = -1;
		private static bool _archivoIniciado;

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_esObservador = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableObservador));
				_esCompanero = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableCompanero));
			}
			if ((!_esObservador && !_esCompanero) || _terminada) {
				return;
			}
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < FotogramasDeEspera) {
				return;
			}
			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			try {
				_repetir = false;
				if (_esCompanero) {
					PasoCompanero(_paso);
				}
				else {
					PasoObservador(_paso);
				}
				if (_repetir) {
					_fotogramasEsperando += FotogramasEntrePasos;
				}
				else {
					_paso++;
					_fotogramasEsperando = 0;
				}
			}
			catch (Exception e) {
				Log("AUTOPRUEBA GRUPO: EXCEPCION en el paso " + _paso + " (" +
					(_esCompanero ? "companero" : "observador") + "): " + e);
				_terminada = true;
			}
		}

		// -------------------------------------------------------------------------------------
		// Rol COMPAÑERO: se pone encima un estado real, conocido y exacto, y se queda conectado.
		// -------------------------------------------------------------------------------------

		private static void PasoCompanero(int paso)
		{
			switch (paso) {
				case 0:
					Log("AUTOPRUEBA GRUPO COMPANERO: conectado. Jugador \"" + Main.LocalPlayer.name +
						"\", netMode=" + Main.netMode + ".");
					break;

				case 1: {
					// Bug real encontrado con este mismo arnes: el servidor real RECHAZA una segunda
					// conexion con el mismo nombre de jugador activo (MessageBuffer.cs, case 4:
					// "if (player.name == Main.player[i].name && Netplay.Clients[i].IsActive)
					// TrySendData(2, ..., Lang.mp[5]...)" -> "\"X\" is already on this server." - el
					// observador y el companero parten del MISMO personaje sintetico de WS0, asi que
					// se rechazaban en silencio (el cliente cierra el socket sin dejar ningun error
					// visible en su propio log, solo se ve en el log del SERVIDOR). Se corrige aqui,
					// con API real: se renombra el jugador en vivo (Player.name, un campo publico) y
					// se reenvia el paquete real que lo sincroniza (NetMessage.SendData(4, ...) =
					// PlayerInfo, el mismo mensaje que ya lleva el nombre real - ver su cabecera en
					// NetMessage.cs) para que el registro EN VIVO del servidor dejar de tener a
					// "TerrakeepPrueba" como ocupado antes de que el observador intente entrar con
					// ese mismo nombre.
					// Player.nameLen = 20 (real, Player.cs) - un nombre mas largo hace que el
					// servidor expulse la conexion entera nada mas recibir el renombrado
					// ("was booted: Name is too long", visto de verdad en server.log la primera vez
					// que este nombre media 24 caracteres). "TkGrupoCompanero" tiene 16.
					Main.LocalPlayer.name = "TkGrupoCompanero";
					NetMessage.SendData(4, -1, -1, null, Main.myPlayer);
					Log("AUTOPRUEBA GRUPO COMPANERO: renombrado en vivo a \"" + Main.LocalPlayer.name +
						"\" y reenviado el PlayerInfo real - libera el nombre \"TerrakeepPrueba\" para " +
						"que el observador pueda conectar con el.");
					break;
				}

				case 2: {
					Item madera = new Item();
					madera.SetDefaults(ItemID.Wood);
					madera.stack = CantidadMaderaConocida;
					Main.LocalPlayer.inventory[0] = madera;

					Item casco = new Item();
					casco.SetDefaults(ItemID.CopperHelmet);
					Main.LocalPlayer.armor[0] = casco;

					// Idea 9, segunda pasada ("repartir builds por clase sin solaparse"): una
					// espada REAL (DamageClass.Melee, la misma clase que el observador se pondra
					// tambien) para poder comprobar de verdad el caso de SOLAPE, no solo el caso
					// sin companero.
					Item espada = new Item();
					espada.SetDefaults(ItemID.CopperShortsword);
					Main.LocalPlayer.inventory[1] = espada;

					Log("AUTOPRUEBA GRUPO COMPANERO: dado estado real conocido - " +
						CantidadMaderaConocida + " de Madera en la ranura 0, Casco de Cobre puesto y " +
						"Espada Corta de Cobre (clase real: cuerpo a cuerpo) en la ranura 1. " +
						"Esperando a que el observador termine (nunca avanza mas alla de este paso por " +
						"su cuenta).");
					break;
				}

				default:
					// Se queda aqui para siempre: el script externo mata este proceso cuando el
					// observador termina. No es un fallo ni una espera con limite - simplemente no
					// hay nada mas que este rol tenga que hacer.
					break;
			}
		}

		// -------------------------------------------------------------------------------------
		// Rol OBSERVADOR: comprueba, con datos reales llegados por red, lo que la idea 9 necesita.
		// -------------------------------------------------------------------------------------

		private static void PasoObservador(int paso)
		{
			switch (paso) {
				case 0:
					Log("AUTOPRUEBA GRUPO OBSERVADOR: conectado. Jugador \"" + Main.LocalPlayer.name +
						"\", netMode=" + Main.netMode + ". Esperando a que se una un companero real.");
					break;

				case 1:
					if (!EsperarA(() => GuiaGrupo.IndicesConectados().Count >= 1,
						"se una un companero real (GuiaGrupo.IndicesConectados)")) {
						return;
					}
					_indiceCompanero = GuiaGrupo.IndicesConectados()[0];
					Log("AUTOPRUEBA GRUPO OBSERVADOR: companero visto en el indice " + _indiceCompanero +
						" (\"" + Main.player[_indiceCompanero].name + "\").");
					break;

				case 2:
					// La sincronizacion de red no es instantanea (ni siquiera en localhost): se
					// espera a que el dato REAL llegue, nunca un numero de fotogramas adivinado.
					EsperarA(() => EstadoJugadorGuia.CuantosLleva(ItemID.Wood, Main.player[_indiceCompanero])
						== CantidadMaderaConocida,
						"la Madera del companero sincronice por red (Main.TrySyncingMyPlayer -> " +
						"PlayerItemSlotID.Inventory0, canNetRelay=true)");
					break;

				case 3: {
					int llevado = EstadoJugadorGuia.CuantosLleva(ItemID.Wood, Main.player[_indiceCompanero]);
					bool ok = llevado == CantidadMaderaConocida;
					Log("AUTOPRUEBA GRUPO OBSERVADOR - mochila del companero via red: " + llevado +
						" de Madera (se esperaban " + CantidadMaderaConocida + ") -> " +
						(ok ? "OK: la mochila principal de OTRO jugador SI llega sincronizada de verdad." : "NO CUADRA."));
					break;
				}

				case 4: {
					// La misma lectura, pero por la via de PRODUCCION que usa de verdad el panel de
					// la Guia (EvaluadorGuia.Evaluar con un jugador concreto), no el helper crudo.
					RequisitoGuia requisito = new RequisitoGuia();
					requisito.Tipo = TipoRequisito.Objeto;
					requisito.Id = ItemID.Wood;
					requisito.Cantidad = CantidadMaderaConocida;
					ResultadoRequisito resultado = EvaluadorGuia.Evaluar(requisito, Main.player[_indiceCompanero]);
					bool ok = resultado.Cumplido && !resultado.NoEvaluable && resultado.Actual == CantidadMaderaConocida;
					Log("AUTOPRUEBA GRUPO OBSERVADOR - EvaluadorGuia.Evaluar(Objeto Madera, companero): " +
						"Actual=" + resultado.Actual + ", Pedido=" + resultado.Pedido +
						", Cumplido=" + resultado.Cumplido + ", NoEvaluable=" + resultado.NoEvaluable + " -> " +
						(ok ? "OK: el motor de requisitos de produccion evalua a un companero real." : "NO CUADRA."));
					break;
				}

				case 5:
					EsperarA(() => EstadoJugadorGuia.DefensaDe(Main.player[_indiceCompanero]) >= 1,
						"la defensa del companero (Casco de Cobre) sincronice y este cliente la recalcule " +
						"solo (Player.Update corre para TODOS los jugadores activos, no solo el local - " +
						"Main.cs:17688-17696)");
					break;

				case 6: {
					int defensa = EstadoJugadorGuia.DefensaDe(Main.player[_indiceCompanero]);
					bool ok = defensa >= 1;
					Log("AUTOPRUEBA GRUPO OBSERVADOR - defensa del companero via red: " + defensa +
						" (se esperaba >= 1 por el Casco de Cobre) -> " +
						(ok ? "OK: la armadura de OTRO jugador tambien llega y se recalcula de verdad." : "NO CUADRA."));
					break;
				}

				case 7: {
					// Idea 9, segunda pasada: el propio observador se pone TAMBIEN una espada real
					// de cuerpo a cuerpo - la MISMA clase que el companero (case 2) - para poder
					// comprobar de verdad el caso de SOLAPE real entre dos jugadores reales, no
					// solo simularlo con datos locales.
					Item espada = new Item();
					espada.SetDefaults(ItemID.CopperShortsword);
					Main.LocalPlayer.inventory[1] = espada;
					Log("AUTOPRUEBA GRUPO OBSERVADOR: puesta TAMBIEN una Espada Corta de Cobre " +
						"(cuerpo a cuerpo, DamageType real=" + espada.DamageType.GetType().Name +
						") - la misma clase que el companero, a proposito, para forzar un solape " +
						"real que comprobar.");
					break;
				}

				case 8:
					EsperarA(() => GuiaGrupo.ClaveClaseDetectada(Main.player[_indiceCompanero], out _) == "melee",
						"la espada del companero sincronice por red y este cliente detecte su clase " +
						"(GuiaGrupo.ClaveClaseDetectada sobre el jugador remoto)");
					break;

				case 9: {
					System.Collections.Generic.List<GuiaGrupo.AsignacionClase> reparto = GuiaGrupo.RepartoClases();
					bool ok = reparto.Count == 2
						&& reparto[0].ClaveDetectada == "melee" && !reparto[0].Solapa && reparto[0].ClaveSugerida == "melee"
						&& reparto[1].ClaveDetectada == "melee" && reparto[1].Solapa && reparto[1].ClaveSugerida == "ranged";
					Log("AUTOPRUEBA GRUPO OBSERVADOR - GuiaGrupo.RepartoClases() con las DOS espadas reales " +
						"puestas: " + reparto.Count + " miembros. Local: detectada=" +
						(reparto.Count > 0 ? reparto[0].ClaveDetectada : "?") + ", solapa=" +
						(reparto.Count > 0 ? reparto[0].Solapa.ToString() : "?") + ", sugerida=" +
						(reparto.Count > 0 ? reparto[0].ClaveSugerida : "?") + ". Companero: detectada=" +
						(reparto.Count > 1 ? reparto[1].ClaveDetectada : "?") + ", solapa=" +
						(reparto.Count > 1 ? reparto[1].Solapa.ToString() : "?") + ", sugerida=" +
						(reparto.Count > 1 ? reparto[1].ClaveSugerida : "?") + " -> " +
						(ok ? "OK: el reparto detecta el solape real y sugiere una clase distinta sin repetir." : "NO CUADRA."));
					break;
				}

				case 10:
					// Abre el panel de la Guia con la MISMA API publica que usa el atajo real (G) -
					// no hace falta simular un clic de raton para esto: lo que se quiere comprobar es
					// la seccion "Grupo" de dentro, no el mecanismo de apertura (ya cubierto por
					// AutopruebaGuia).
					GuiaSystem.AbrirPanel("autoprueba-grupo");
					break;

				case 11:
					if (!EsperarA(() => GuiaSystem.PanelAbierto && GuiaSystem.PanelActual != null,
						"el panel de la Guia este abierto de verdad")) {
						return;
					}
					break;

				case 12:
					Capturar("grupo-observador-companero-visible");
					Log("AUTOPRUEBA GRUPO OBSERVADOR - " + _ultimaCaptura + " (companero \"" +
						Main.player[_indiceCompanero].name +
						"\" conectado de verdad, en un proceso de tModLoader distinto) -> " +
						(_ultimaCaptura.StartsWith("captura real") ? "OK." : "NO CUADRA: no se guardo la captura."));
					break;

				default:
					Log("AUTOPRUEBA GRUPO COMPLETA.");
					_terminada = true;
					break;
			}
		}

		// -------------------------------------------------------------------------------------

		private static bool EsperarA(Func<bool> condicion, string que)
		{
			if (condicion()) {
				return true;
			}
			if (_fotogramasEsperando >= FotogramasMaximosDeEspera) {
				Log("AUTOPRUEBA GRUPO - NO CUADRA: se agotaron " + _fotogramasEsperando +
					" fotogramas esperando a que " + que + ".");
				return true; // deja avanzar igualmente: el "NO CUADRA" ya quedo en el log
			}
			_repetir = true;
			return false;
		}

		private static void Capturar(string nombre)
		{
			// Un fotograma real de por medio antes de capturar (mismo motivo que ya documento
			// AutopruebaGuia en el TM4: CapturaDePantalla.Guardar coge el fotograma YA PRESENTADO).
			if (!_repetir) {
				_ultimaCaptura = CapturaDePantalla.Guardar(nombre);
			}
		}

		/// <summary>Lo que devolvio la ULTIMA llamada a <see cref="Capturar"/>, para que el paso
		/// siguiente lo registre de verdad en vez de asumir que salio bien (bug real encontrado
		/// revisando este mismo log: decia "captura real guardada" con la variable de entorno
		/// todavia sin dar de alta en <see cref="CapturaDePantalla.Permitida"/>, que devolvia
		/// "captura no pedida" en silencio).</summary>
		private static string _ultimaCaptura = "";

		/// <summary>Evidencia propia de este arnes, en un archivo DISTINTO al de
		/// <see cref="RegistroGuia"/> (esa clase solo escribe a archivo con
		/// <c>TERRAKEEP_AUTOTEST_GUIA</c> puesta, que aqui no lo esta) y, sobre todo, DISTINTO por
		/// PROCESO: cada uno (observador/companero) tiene su propio <c>-tmlsavedirectory</c>, asi
		/// que <c>Main.SavePath</c> ya los separa solo - nunca los dos escribiendo al mismo
		/// archivo a la vez.</summary>
		private static void Log(string linea)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(linea);
			}
			try {
				string ruta = Path.Combine(Main.SavePath, "terrakeep-grupo-evidencia.log");
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta, "# Evidencia de la guia de grupo (idea 9) - " +
						DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
				}
				File.AppendAllText(ruta, "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + linea + Environment.NewLine);
			}
			catch (Exception) {
				// La evidencia del log del mod ya esta escrita; sin permisos/disco no tumba nada.
			}
		}
	}
}
