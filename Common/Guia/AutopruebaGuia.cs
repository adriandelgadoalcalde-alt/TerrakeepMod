using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Guia;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Verificacion en el juego REAL del primer tramo de la Guia (pre-Ojo de Cthulhu).
	/// </summary>
	/// <remarks>
	/// <para>
	/// No comprueba que el codigo "parezca correcto": <b>lleva al personaje de prueba por el
	/// tramo entero</b>, un requisito cada vez, y despues de cada cambio vuelve a preguntarle a
	/// la guia cual es el objetivo. Si el cerebro se equivoca de paso, se ve en el log en el
	/// acto. Ademas deja capturas reales del back buffer para poder mirar la estetica, que es lo
	/// unico que ninguna comprobacion de datos encuentra (leccion ya escrita en la bitacora: la
	/// fase de fusion encontro cinco fallos visuales con el log en verde).
	/// </para>
	/// <para>
	/// Es una maquina de estados con esperas explicitas y no una funcion que lo hace todo de una,
	/// por el mismo motivo que <see cref="AutopruebaPanelUnico"/>: casi nada de lo que hay que
	/// comprobar es cierto en el mismo fotograma en que se pide. La defensa, en particular, la
	/// recalcula <c>Player.ResetEffects</c>/<c>UpdateEquips</c> en el tick siguiente, asi que
	/// equipar una armadura y leer <c>statDefense</c> a continuacion daria cero.
	/// </para>
	/// <para>
	/// Todo lo que toca del mundo (las banderas de jefe, los NPC que hace aparecer) se deshace al
	/// terminar: la prueba corre en un sandbox propio con <c>-tmlsavedirectory</c>, pero dejar un
	/// mundo con <c>downedBoss1</c> puesto falsearia la siguiente ejecucion.
	/// </para>
	/// </remarks>
	public static class AutopruebaGuia
	{
		/// <summary>Variable de entorno que la activa.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_GUIA";

		private const int FotogramasEntrePasos = 14;
		private const int FotogramasDeEspera = 180;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		private static bool _downedBoss1Original;
		private static readonly List<int> _npcsCreados = new List<int>();

		/// <summary>Tipos de NPC del pueblo que ya vivian en el mundo de prueba y que la prueba
		/// retira para partir de un estado conocido. Se vuelven a crear en <see cref="Restaurar"/>.</summary>
		private static readonly List<int> _vecinosRetirados = new List<int>();

		/// <summary>Fotogramas que lleva esperando el paso actual a que se cumpla su condicion.</summary>
		private static int _fotogramasEsperando;

		/// <summary>Lo pone <see cref="EsperarA"/> cuando el paso todavia no ha conseguido lo que
		/// pedia y hay que volver a intentarlo.</summary>
		private static bool _repetir;

		/// <summary>Tope de la espera de <see cref="EsperarA"/>: ~5 s a 60 fps.</summary>
		private const int FotogramasMaximosDeEspera = 300;

		/// <summary>
		/// Espera a que se cumpla una condicion del juego en vez de dar por hecho que un numero
		/// fijo de fotogramas basta. Devuelve true cuando se cumple (o cuando se agota la espera,
		/// y entonces lo dice en el log).
		/// </summary>
		/// <remarks>
		/// Hizo falta a la primera pasada real: la defensa que da una armadura recien equipada no
		/// esta puesta al fotograma siguiente. La recalculan <c>Player.ResetEffects</c> y
		/// <c>Player.UpdateEquips</c> dentro de <c>Player.Update</c>, y en la ejecucion medida
		/// tardo bastante mas que los 14 fotogramas que separaban los pasos, asi que la
		/// comprobacion leia todavia la defensa vieja y daba un falso "NO CUADRA". Esperar por la
		/// CONDICION en vez de por un reloj es lo unico que quita ese tipo de falso negativo - y
		/// tambien el falso positivo simetrico, que es peor.
		/// </remarks>
		private static bool EsperarA(Func<bool> condicion, string que)
		{
			if (condicion()) {
				if (_fotogramasEsperando > 0) {
					RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - esperado a que " + que +
						": listo tras " + _fotogramasEsperando + " fotogramas.");
				}
				return true;
			}

			if (_fotogramasEsperando >= FotogramasMaximosDeEspera) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NO CUADRA: se agotaron " +
					_fotogramasEsperando + " fotogramas esperando a que " + que + ".");
				return true;
			}

			_repetir = true;
			return false;
		}

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
			}

			if (!_activa || _terminada) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			// ~3 s a 60 fps, igual que todas las autopruebas del mod: da tiempo a entrar del todo
			// al mundo y a que PlayerInput haya procesado el reinitialize de los atajos de mods.
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
				Paso(_paso);
				if (_repetir) {
					_fotogramasEsperando += FotogramasEntrePasos;
				}
				else {
					_paso++;
					_fotogramasEsperando = 0;
				}
			}
			catch (Exception e) {
				RegistroGuia.Error(Terrakeep.LogTag + " AUTOPRUEBA GUIA: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); break;
				case 1: AbrirLaPestanaConClicReal(); break;
				case 2: MedirLaBarraDeSietePestanas(); break;

				// --- estado de partida: un personaje recien hecho y un mundo sin vecinos --------
				case 3: DejarElMundoSinVecinos(); break;
				case 4: EsperarA(() => EstadoJugadorGuia.NpcsDelPueblo() == 0, "no quede ningun vecino"); break;
				case 5: VolcarEstadoDelJugador("al empezar"); break;
				case 6: ComprobarPaso("Refugio", "personaje recien creado y mundo sin vecinos"); break;
				case 7: Capturar("guia-1-refugio"); break;

				// --- un vecino -> toca la armadura ---------------------------------------------
				case 8: CrearVecinos(1); break;
				case 9: ComprobarPaso("Defensa", "ya hay un vecino"); break;

				// --- armadura -> tocan los cristales -------------------------------------------
				case 10: PonerArmadura(); break;
				case 11: EsperarA(() => EstadoJugadorGuia.Defensa >= 11,
					"el juego recalcule la defensa de la armadura (Player.UpdateEquips)"); break;
				case 12: VolcarEstadoDelJugador("con armadura puesta"); break;
				case 13: ComprobarPaso("CristalesDeVida", "la defensa ya pasa de 10"); break;
				case 14: Capturar("guia-2-cristales"); break;

				// --- cristales -> toca el pueblo -----------------------------------------------
				case 15: ComprobarQueLaVidaSolaNoBasta(); break;
				case 16: UsarCristalesDeVida(5); break;
				case 17: ComprobarPaso("PuebloDeCuatro", "ya hay 5 cristales de vida USADOS"); break;

				// --- cuatro vecinos -> toca el arma ---------------------------------------------
				case 18: CrearVecinos(3); break;
				case 19: ComprobarPaso("ArmaYArena", "cuatro vecinos en el pueblo"); break;
				case 20: ComprobarLecturaDelJefe(); break;
				case 21: Capturar("guia-3-arma-y-arena"); break;

				// --- arma -> toca invocarlo -----------------------------------------------------
				case 22: PonerArma(); break;
				case 23: VolcarEstadoDelJugador("con arma de verdad"); break;
				case 24: ComprobarPaso("InvocarElOjo", "arma con daño suficiente"); break;
				case 25: ComprobarLecturaDelJefe(); break;
				case 26: Capturar("guia-4-invocar-el-ojo"); break;

				// --- jefe caido -> tramo cerrado ------------------------------------------------
				case 27: MatarElOjoEnFalso(); break;
				case 28: ComprobarSinObjetivo(); break;
				case 29: Capturar("guia-5-tramo-terminado"); break;

				// --- la misma pantalla EN ESPAÑOL ------------------------------------------------
				// Es donde los rotulos son mas largos ("Investigación", "Exploración") y donde un
				// texto se sale sin que ningun dato lo diga. Se cambia por la via real del mod.
				case 30: DeshacerElOjoParaVerLaPantalla(); break;
				case 31: CambiarIdioma(IdiomaDeTerrakeep.Espanol); break;
				case 32: MedirLaBarraDeSietePestanas(); break;
				case 33: Capturar("guia-6-en-espanol"); break;
				case 34: ComprobarTextosVisibles(); break;
				case 35: CambiarIdioma(IdiomaDeTerrakeep.SeguirElJuego); break;

				case 36: Restaurar(); break;
				default: Terminar(); break;
			}
		}

		// -------------------------------------------------------------------------------------

		private static void Arrancar()
		{
			string marca = Environment.GetEnvironmentVariable("TERRAKEEP_GUIA_MARCA");
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA: " + Variable +
				" detectada. Marca de ejecucion: " + marca +
				". Jugador \"" + Main.LocalPlayer.name + "\", mundo \"" + Main.worldName +
				"\", modo " + EstadoGuia.MundoActualModo() +
				", resolucion " + Main.screenWidth + "x" + Main.screenHeight +
				", escala de interfaz " + Main.UIScale + ".");

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA/0 - catalogo: " +
				CatalogoGuia.Resumen + ". Calamity cargado=" + CatalogoGuia.HayCalamity + ".");

			int avisos = 0;
			foreach (string aviso in CatalogoGuia.Avisos) {
				avisos++;
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA/0 - aviso de datos: " + aviso);
			}
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA/0 - avisos de datos: " + avisos +
				(avisos == 0 ? " -> OK: el .json esta limpio." : " -> revisar."));

			_downedBoss1Original = NPC.downedBoss1;
			NPC.downedBoss1 = false;

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "autoprueba de la Guia");
		}

		/// <summary>Entra en la Guia por la ruta de produccion: un clic real en su pestaña.</summary>
		private static void AbrirLaPestanaConClicReal()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA: el panel no esta abierto.");
				return;
			}

			string pulsada = panel.PulsarPestana(AreaTerrakeep.Guia);
			bool ok = PanelTerrakeepSystem.AreaAbierta == AreaTerrakeep.Guia;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA/1 - CLIC REAL en la pestaña " +
				pulsada + ". Pestaña activa ahora: \"" +
				PanelTerrakeepState.NombresDeArea[(int)PanelTerrakeepSystem.AreaAbierta] + "\" " +
				(ok ? "-> OK" : "-> NO CUADRA"));
		}

		/// <summary>
		/// La septima pestaña estrecha la barra: se comprueba con NUMEROS que los siete rotulos
		/// caben enteros, que es lo que no se ve en ninguna comprobacion de datos.
		/// </summary>
		private static void MedirLaBarraDeSietePestanas()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				return;
			}

			float escala = panel.EscalaDeLasPestanas;
			System.Text.StringBuilder texto = new System.Text.StringBuilder();
			bool todoCabe = true;

			foreach (System.Collections.Generic.KeyValuePair<string, float> par in AnchosDePestana(panel, escala)) {
				texto.Append('"').Append(par.Key).Append("\"=").Append(par.Value.ToString("0.0")).Append("px ");
				if (par.Value < 0f) {
					todoCabe = false;
				}
			}

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA/2 - barra de 7 pestañas: escala de " +
				"texto comun " + escala.ToString("0.000") + " (la de reposo son 0,800). Holgura de cada " +
				"rotulo dentro de su boton: " + texto + " " +
				(todoCabe ? "-> OK: los siete caben enteros." : "-> HAY TEXTO QUE NO CABE."));
		}

		/// <summary>Holgura en pixeles de cada rotulo dentro de su boton, medida con la fuente real.</summary>
		private static List<System.Collections.Generic.KeyValuePair<string, float>> AnchosDePestana(
			PanelTerrakeepState panel, float escala)
		{
			List<System.Collections.Generic.KeyValuePair<string, float>> salida =
				new List<System.Collections.Generic.KeyValuePair<string, float>>();

			foreach (Terraria.UI.UIElement hijo in panel.MarcoHijos) {
				UI.Personaje.Widgets.BotonTk boton = hijo as UI.Personaje.Widgets.BotonTk;
				if (boton == null || !boton.EsPestana) {
					continue;
				}
				float ancho = boton.GetDimensions().Width;
				float texto = Terraria.GameContent.FontAssets.MouseText.Value
					.MeasureString(boton.Texto).X * escala * 1.06f;
				salida.Add(new System.Collections.Generic.KeyValuePair<string, float>(
					boton.Texto, ancho - texto));
			}
			return salida;
		}

		private static void VolcarEstadoDelJugador(string cuando)
		{
			string arma;
			int dano = EstadoJugadorGuia.DanoDelMejorArma(out arma);
			string gancho;
			bool llevaGancho = EstadoJugadorGuia.LlevaGancho(out gancho);

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - estado REAL del jugador (" + cuando +
				"): vida " + Main.LocalPlayer.statLifeMax2 + " (cristales consumidos " +
				EstadoJugadorGuia.CristalesVida + "), defensa " + EstadoJugadorGuia.Defensa +
				", vecinos " + EstadoJugadorGuia.NpcsDelPueblo() +
				", mejor arma \"" + (arma == "" ? "(ninguna)" : arma) + "\" con " + dano + " de daño" +
				", gancho=" + (llevaGancho ? gancho : "no") +
				", capa=" + EstadoJugadorGuia.CapaDelJugador() +
				", downedBoss1=" + NPC.downedBoss1 + ".");
		}

		/// <summary>
		/// El corazon de la prueba: comprueba que la guia esta señalando EL PASO QUE TOCA, y de
		/// paso vuelca sus requisitos uno a uno con sus numeros reales y el medidor de preparacion.
		/// </summary>
		private static void ComprobarPaso(string claveEsperada, string porque)
		{
			ContenidoGuia contenido = GuiaSystem.PanelActual;
			if (contenido == null) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA: la pestaña de la Guia no esta montada.");
				return;
			}

			// Se fuerza el remontado para no depender de que el Update del fotograma anterior ya
			// haya visto el cambio: lo que se esta comprobando es el CEREBRO, no el refresco.
			contenido.Reconstruir();

			PasoGuia paso = contenido.PasoEnPantalla;
			string clave = paso != null ? paso.Clave : "(ninguno)";
			bool ok = clave == claveEsperada;

			int cumplidos = 0;
			int total = 0;
			float preparacion = 0f;
			if (paso != null) {
				preparacion = EvaluadorGuia.Preparacion(paso, out cumplidos, out total);
			}

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - objetivo actual: \"" + clave +
				"\" (\"" + (paso != null ? paso.Titulo : "-") + "\"), esperado \"" + claveEsperada +
				"\" porque " + porque + " " + (ok ? "-> OK" : "-> NO CUADRA") +
				". Preparacion " + (int)(preparacion * 100f + 0.5f) + "% (" + cumplidos + "/" + total +
				" obligatorios). Direccion: \"" + (paso != null ? EstadoGuia.Direccion(paso) : "-") + "\".");

			if (contenido.Medidor != null) {
				float valor = contenido.Medidor.Valor;
				RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - medidor en pantalla: " +
					valor.ToString("0.000") + ", color " +
					(valor >= 0.999f ? "VERDE" : (valor >= 0.6f ? "AMBAR" : "ROJO")) +
					(Math.Abs(valor - preparacion) < 0.001f
						? " -> OK: coincide con el calculo."
						: " -> NO COINCIDE con el calculo (" + preparacion.ToString("0.000") + ")."));
			}

			List<FilaRequisitoTk> filas = contenido.FilasDeRequisito();
			for (int i = 0; i < filas.Count; i++) {
				ResultadoRequisito estado = filas[i].Estado();
				RegistroGuia.Linea(Terrakeep.LogTag + "   requisito " + (i + 1) + "/" + filas.Count + ": [" +
					(estado.NoEvaluable ? "?" : (estado.Cumplido ? "HECHO" : "FALTA")) + "] " +
					estado.Linea + " (" + estado.Actual + "/" + estado.Pedido +
					(estado.Requisito != null && estado.Requisito.Recomendado ? ", recomendado" : "") + ")");
			}
		}

		/// <summary>
		/// Comprueba la "lectura del jefe" contra los numeros REALES del motor.
		/// <para />
		/// La vida base del Ojo de Cthulhu en el <c>NPC.cs</c> decompilado de esta version es
		/// <b>2800</b>, con <b>12</b> de defensa y <b>15</b> de daño (<c>NPC.cs:3786</c>, bloque
		/// <c>type == 4</c>). Aqui no se comparan contra esa constante: se pide al propio juego
		/// que escale el NPC con <c>ScaleStats</c> y se deja el numero en el log junto al valor
		/// base, para que quien lea la evidencia pueda cuadrarlos el mismo segun el modo.
		/// </summary>
		private static void ComprobarLecturaDelJefe()
		{
			int vida, dano, defensa;
			bool ok = EvaluadorGuia.StatsDeJefe(NPCID.EyeofCthulhu, out vida, out dano, out defensa);

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - stats REALES del Ojo de Cthulhu en " +
				"esta partida (ContentSamples + NPC.ScaleStats con Main.GameModeInfo): resuelto=" + ok +
				", vida=" + vida + ", defensa=" + defensa + ", daño=" + dano +
				". Base en el NPC.cs decompilado de esta version: vida=2800, defensa=12, daño=15. " +
				"Multiplicadores reales de GameModeData: normal x1, experto x2, maestro x3 (vida y daño). " +
				"Modo de esta partida: " + EstadoGuia.MundoActualModo() + ".");

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - lectura que ve el jugador:\n" +
				EstadoGuia.LecturaDeJefe(NPCID.EyeofCthulhu));
		}

		private static void ComprobarSinObjetivo()
		{
			ContenidoGuia contenido = GuiaSystem.PanelActual;
			if (contenido == null) {
				return;
			}
			contenido.Reconstruir();

			bool ok = contenido.PasoEnPantalla == null;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - con downedBoss1=true no queda ningun " +
				"paso pendiente en el tramo implementado: paso en pantalla=" +
				(contenido.PasoEnPantalla != null ? contenido.PasoEnPantalla.Clave : "(ninguno)") + " " +
				(ok ? "-> OK: el tramo se da por cerrado." : "-> NO CUADRA."));
		}

		// -------------------------------------------------------------------------------------
		// Cambios en vivo sobre la partida
		// -------------------------------------------------------------------------------------

		/// <summary>Hace aparecer vecinos de verdad en el mundo, con la misma llamada que usa el
		/// juego (<c>NPC.NewNPC</c>), no tocando ningun contador.</summary>
		private static void CrearVecinos(int cuantos)
		{
			// Guia, Mercader, Enfermera y Demoliciones: los cuatro que un jugador puede tener de
			// verdad antes del primer jefe (comprobado en las condiciones reales de
			// NPC.SpawnAllowed_*: el Mercader pide 50 de plata, la Enfermera un cristal de vida ya
			// usado mas el Mercader, y Demoliciones una bomba mas el Mercader).
			int[] tipos = { NPCID.Guide, NPCID.Merchant, NPCID.Nurse, NPCID.Demolitionist };

			int creados = 0;
			for (int i = 0; i < tipos.Length && creados < cuantos; i++) {
				if (EstadoJugadorGuia.HayNpc(tipos[i])) {
					continue;
				}

				int indice = NPC.NewNPC(new EntitySource_WorldGen("autoprueba de la Guia"),
					(int)Main.LocalPlayer.position.X, (int)Main.LocalPlayer.position.Y, tipos[i]);
				if (indice >= 0 && indice < Main.maxNPCs) {
					Main.npc[indice].homeless = true;
					_npcsCreados.Add(indice);
					creados++;
				}
			}

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - creados " + creados +
				" vecinos de verdad con NPC.NewNPC. Vecinos vivos ahora: " +
				EstadoJugadorGuia.NpcsDelPueblo() + ".");
		}

		/// <summary>
		/// Equipa una armadura real buscada POR PROPIEDADES (no por id fijo), igual que hace
		/// <c>AutopruebaPanelUnico.PoblarAparienciaDePrueba</c>: asi la prueba no se rompe si un
		/// mod reordena los ids.
		/// </summary>
		private static void PonerArmadura()
		{
			Player jugador = Main.LocalPlayer;

			int cabeza = BuscarObjeto(o => o.headSlot >= 0 && o.defense >= 3);
			int cuerpo = BuscarObjeto(o => o.bodySlot >= 0 && o.defense >= 4);
			int piernas = BuscarObjeto(o => o.legSlot >= 0 && o.defense >= 3);

			Equipar(jugador.armor, 0, cabeza);
			Equipar(jugador.armor, 1, cuerpo);
			Equipar(jugador.armor, 2, piernas);

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - armadura puesta en vivo: \"" +
				EvaluadorGuia.NombreDeObjeto(cabeza) + "\" + \"" + EvaluadorGuia.NombreDeObjeto(cuerpo) +
				"\" + \"" + EvaluadorGuia.NombreDeObjeto(piernas) + "\". La defensa la recalcula el " +
				"propio juego en el tick siguiente (Player.ResetEffects/UpdateEquips), por eso se " +
				"mide en el paso de despues y no en este.");
		}

		/// <summary>
		/// Deja el mundo de prueba SIN vecinos para partir de un estado conocido.
		/// </summary>
		/// <remarks>
		/// Hizo falta a la primera pasada real: el mundo sintetico de las pruebas ya venia con dos
		/// NPC del pueblo dentro, asi que el primer paso ("Refugio") salia cumplido de entrada y la
		/// comprobacion daba "NO CUADRA" sin que hubiera nada roto. La guia acertaba; la prueba
		/// partia de un estado que no controlaba. Se retiran aqui y se vuelven a crear al terminar.
		/// </remarks>
		private static void DejarElMundoSinVecinos()
		{
			_vecinosRetirados.Clear();
			int retirados = 0;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc != null && npc.active && npc.townNPC) {
					_vecinosRetirados.Add(npc.type);
					npc.active = false;
					retirados++;
				}
			}

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - retirados " + retirados +
				" vecinos que ya traia el mundo de prueba, para partir de un estado conocido. " +
				"Se vuelven a crear al terminar.");
		}

		/// <summary>
		/// La comprobacion mas util de todo el arnes: demuestra <b>en el juego real</b> que subir
		/// la vida maxima a mano NO cuenta como usar cristales, que es justo lo que mira el motor.
		/// </summary>
		/// <remarks>
		/// Salio de un fallo de esta misma autoprueba: poner <c>statLifeMax = 200</c> dejaba
		/// <c>ConsumedLifeCrystals</c> en 0. Leyendo el <c>Player.cs</c> decompilado se ve por que:
		/// es un contador guardado aparte (<c>consumedLifeCrystals</c>) que solo sube al USAR un
		/// cristal. Como Terrakeep permite editar la vida maxima, este es exactamente el caso en
		/// el que una guia ingenua mentiria, asi que se deja comprobado de por vida.
		/// </remarks>
		private static void ComprobarQueLaVidaSolaNoBasta()
		{
			Player jugador = Main.LocalPlayer;
			int antes = jugador.ConsumedLifeCrystals;

			jugador.statLifeMax = 200;
			jugador.statLifeMax2 = 200;
			jugador.statLife = 200;

			int despues = jugador.ConsumedLifeCrystals;
			bool ok = despues == antes;

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - vida maxima subida a mano a 200 " +
				"(como haria el editor de Terrakeep). ConsumedLifeCrystals: " + antes + " -> " + despues +
				". " + (ok
					? "OK: no se mueve, porque es un contador guardado aparte y no (statLifeMax-100)/20. " +
					  "La guia lee el contador, que es lo que mira Main.UpdateTime_StartNight, asi que " +
					  "sigue diciendo la verdad."
					: "NO CUADRA: se esperaba que la vida a mano no moviera el contador."));
		}

		/// <summary>Sube el contador REAL de cristales usados, que es lo unico que el motor
		/// acepta como "te has tomado cinco cristales".</summary>
		private static void UsarCristalesDeVida(int cuantos)
		{
			Player jugador = Main.LocalPlayer;
			jugador.ConsumedLifeCrystals = cuantos;

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - Player.ConsumedLifeCrystals puesto a " +
				cuantos + " (el contador real que sube Player.ItemCheck al usar un Cristal de Vida, " +
				"sItem.type == 29). Lo que lee la guia ahora: " + EstadoJugadorGuia.CristalesVida + ".");
		}

		/// <summary>Mete en la mochila un arma real con daño suficiente, buscada por propiedades.</summary>
		private static void PonerArma()
		{
			int tipo = BuscarObjeto(o => o.damage >= 18 && o.damage <= 30 && !o.accessory &&
				o.ammo == AmmoID.None && o.pick == 0 && o.axe == 0 && o.hammer == 0 &&
				o.useStyle != ItemUseStyleID.None);

			if (tipo <= 0) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA: no se encontro ningun arma de prueba.");
				return;
			}

			Item objeto = new Item();
			objeto.SetDefaults(tipo);
			Main.LocalPlayer.inventory[0] = objeto;

			string nombre;
			int dano = EstadoJugadorGuia.DanoDelMejorArma(out nombre);
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - arma puesta en la mochila: \"" +
				objeto.Name + "\" (Item.damage=" + objeto.damage + "). Lo que lee la guia, pasado por " +
				"Player.GetWeaponDamage: \"" + nombre + "\" con " + dano + ".");
		}

		/// <summary>Marca el Ojo como derrotado SIN pelearlo, para comprobar el cierre del tramo.
		/// Se deshace en <see cref="Restaurar"/>.</summary>
		private static void MatarElOjoEnFalso()
		{
			NPC.downedBoss1 = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedBoss1 puesto a true a mano " +
				"(la bandera real que enciende NPC.SetEventFlagCleared al morir el Ojo, NPC.cs case 4). " +
				"Se restaura al terminar.");
		}

		private static void Restaurar()
		{
			NPC.downedBoss1 = _downedBoss1Original;

			// El personaje de prueba se deja como estaba. En la practica el script mata el proceso
			// sin guardar, pero no depender de eso es mas barato que descubrir un dia que si guardo.
			Player jugador = Main.LocalPlayer;
			jugador.ConsumedLifeCrystals = 0;
			jugador.statLifeMax = 100;
			jugador.statLifeMax2 = 100;
			jugador.statLife = 100;
			for (int i = 0; i < 3 && i < jugador.armor.Length; i++) {
				jugador.armor[i] = new Item();
			}
			jugador.inventory[0] = new Item();

			int quitados = 0;
			for (int i = 0; i < _npcsCreados.Count; i++) {
				int indice = _npcsCreados[i];
				if (indice >= 0 && indice < Main.maxNPCs && Main.npc[indice] != null && Main.npc[indice].active) {
					Main.npc[indice].active = false;
					quitados++;
				}
			}
			_npcsCreados.Clear();

			// Los vecinos que ya traia el mundo se vuelven a crear: la prueba no puede dejar el
			// mundo de pruebas peor de como lo encontro.
			int devueltos = 0;
			for (int i = 0; i < _vecinosRetirados.Count; i++) {
				int indice = NPC.NewNPC(new EntitySource_WorldGen("autoprueba de la Guia: restaurar"),
					Main.spawnTileX * 16, Main.spawnTileY * 16, _vecinosRetirados[i]);
				if (indice >= 0 && indice < Main.maxNPCs) {
					Main.npc[indice].homeless = true;
					devueltos++;
				}
			}
			_vecinosRetirados.Clear();

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - restaurado: downedBoss1=" +
				NPC.downedBoss1 + ", vecinos de prueba retirados=" + quitados +
				", vecinos originales devueltos=" + devueltos +
				", vecinos vivos ahora=" + EstadoJugadorGuia.NpcsDelPueblo() + ".");
		}

		/// <summary>Vuelve a dejar el Ojo sin matar, para que la captura en español enseñe un
		/// objetivo de verdad y no la pantalla de "no queda nada".</summary>
		private static void DeshacerElOjoParaVerLaPantalla()
		{
			NPC.downedBoss1 = false;
			ContenidoGuia contenido = GuiaSystem.PanelActual;
			if (contenido != null) {
				contenido.Reconstruir();
			}
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - downedBoss1 vuelto a false para " +
				"la pasada en español. Objetivo ahora: \"" +
				(contenido != null && contenido.PasoEnPantalla != null
					? contenido.PasoEnPantalla.Clave : "(ninguno)") + "\".");
		}

		/// <summary>Cambia el idioma por la via REAL del mod (la misma que el selector de Ajustes),
		/// no tocando <c>Language</c> a mano.</summary>
		private static void CambiarIdioma(IdiomaDeTerrakeep idioma)
		{
			Idiomas.Aplicar(idioma, "autoprueba de la Guia");
			ContenidoGuia contenido = GuiaSystem.PanelActual;
			if (contenido != null) {
				contenido.Reconstruir();
			}

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - idioma cambiado a " + idioma +
				". Cultura activa: " + Idiomas.CulturaActiva +
				". Prueba real de la recarga, clave propia de la Guia \"Guia.ObjetivoActual\" = \"" +
				Idiomas.Texto("Guia.ObjetivoActual") + "\".");
		}

		/// <summary>
		/// Vuelca TODO el texto que se esta viendo en el area, ya envuelto, y comprueba que ninguna
		/// linea se sale de su caja midiendola con la fuente real.
		/// </summary>
		private static void ComprobarTextosVisibles()
		{
			ContenidoGuia contenido = GuiaSystem.PanelActual;
			if (contenido == null) {
				return;
			}

			int parrafos = 0;
			int lineas = 0;
			float peorHolgura = float.MaxValue;
			string peor = "";

			foreach (UI.Guia.ParrafoTk parrafo in contenido.Parrafos()) {
				parrafos++;
				float ancho = parrafo.GetInnerDimensions().Width;
				string texto = parrafo.TextoEnvuelto ?? "";
				foreach (string linea in texto.Split('\n')) {
					if (linea.Length == 0) {
						continue;
					}
					lineas++;
					float mide = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(linea).X
						* parrafo.Escala;
					float holgura = ancho - mide;
					if (holgura < peorHolgura) {
						peorHolgura = holgura;
						peor = linea;
					}
				}
			}

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - texto visible en \"" +
				Idiomas.CulturaActiva + "\": " + parrafos + " parrafos, " + lineas + " lineas. " +
				"La linea mas justa se queda a " + peorHolgura.ToString("0.0") + " px del borde de su " +
				"caja: \"" + peor + "\". " +
				(peorHolgura >= 0f ? "OK: ninguna se sale." : "NO CABE: hay texto fuera de su caja."));
		}

		private static void Capturar(string nombre)
		{
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - " + CapturaDePantalla.Guardar(nombre));
		}

		private static void Terminar()
		{
			_terminada = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA COMPLETA.");
		}

		// -------------------------------------------------------------------------------------

		private static int BuscarObjeto(Func<Item, bool> condicion)
		{
			for (int tipo = 1; tipo < ItemLoader.ItemCount; tipo++) {
				Item muestra;
				if (!ContentSamples.ItemsByType.TryGetValue(tipo, out muestra) || muestra == null) {
					continue;
				}
				if (muestra.type <= 0 || string.IsNullOrEmpty(muestra.Name)) {
					continue;
				}
				if (condicion(muestra)) {
					return tipo;
				}
			}
			return 0;
		}

		private static void Equipar(Item[] equipo, int indice, int tipo)
		{
			if (tipo <= 0 || indice < 0 || indice >= equipo.Length) {
				return;
			}
			Item objeto = new Item();
			objeto.SetDefaults(tipo);
			equipo[indice] = objeto;
		}
	}
}
