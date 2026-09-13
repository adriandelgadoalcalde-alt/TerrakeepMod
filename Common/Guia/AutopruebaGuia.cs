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
		private static bool _downedBoss2Original;
		private static bool _downedBoss3Original;
		private static bool _shadowOrbSmashedOriginal;
		private static bool _hardModeOriginal;
		private static bool _downedMechBoss1Original;
		private static bool _downedMechBossAnyOriginal;
		private static bool _downedPlantBossOriginal;
		private static bool _downedGolemBossOriginal;
		private static bool _downedAncientCultistOriginal;
		private static bool _downedTowerSolarOriginal;
		private static bool _downedTowerVortexOriginal;
		private static bool _downedTowerNebulaOriginal;
		private static bool _downedTowerStardustOriginal;
		private static bool _downedMoonlordOriginal;
		private static bool _downedQueenBeeOriginal;
		private static bool _downedSlimeKingOriginal;
		private static bool _downedDeerclopsOriginal;
		private static int _dungeonXOriginal;
		private static Vector2 _posicionOriginal;
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
				// OJO: en este punto el jugador TODAVIA lleva la Furia de estrellas (25 de daño) de
				// case22, y con MaldadDelMundo ya implementado eso basta por si solo para el unico
				// requisito obligatorio de su primer paso ("ArmaContraLaMaldad", dano_arma>=20). El
				// compas no se queda "sin nada pendiente": salta directo al SEGUNDO paso de la
				// maldad del mundo ("VencerLaMaldad"), que es justo el comportamiento que se quiere
				// (nunca se pide dos veces lo que ya tienes). Se comprueba eso, no un "(ninguno)".
				case 27: MatarElOjoEnFalso(); break;
				case 28: ComprobarPaso("VencerLaMaldad",
					"el Ojo ya cayo (downedBoss1=true) y la Furia de estrellas que llevas desde " +
					"antes (25 de daño) ya cumple ella sola el unico requisito obligatorio del " +
					"primer paso de la maldad del mundo, asi que el compas salta directo al segundo"); break;
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

				// --- segundo tramo implementado: la maldad del mundo (Devorador / Cerebro) ------
				// El tramo 1 se cierra a mano (downedBoss1=true) para que EstadoGuia.PasoActual pase
				// al siguiente tramo OBLIGATORIO. ReySlime (Orden 5) y Deerclops (Orden 15) quedan
				// entre medias en el .json, y ReinaAbeja (Orden 25) justo despues de este tramo: los
				// tres son TramoGuia.Opcional=true, asi que PasoActual los salta siempre sin mirar su
				// Orden - si el paso que aparece aqui fuera el de cualquiera de los tres, seria la
				// señal de que ese filtro se ha roto.
				case 37: PrepararMaldadDelMundo(); break;
				case 38: ComprobarPaso("ArmaContraLaMaldad",
					"downedBoss1=true a mano y nada de la maldad del mundo hecho todavia (y NO " +
					"\"ReySlime\"/\"Deerclops\"/\"ReinaAbeja\", los tres Opcional=true y saltados " +
					"siempre por PasoActual)"); break;
				case 39: MarcarEsferaRotaDeMentira(); break;
				case 40: PonerObjetoDeInvocacion(); break;
				case 41: ComprobarPaso("ArmaContraLaMaldad",
					"esfera marcada rota + objeto de invocacion en la mochila: los dos son " +
					"recomendados, asi que suben la preparacion pero el paso sigue sin cerrarse"); break;
				case 42: Capturar("guia-7-maldad-preparativos"); break;
				case 43: PonerArmaConDano(20); break;
				case 44: VolcarEstadoDelJugador("con arma contra la maldad del mundo"); break;
				case 45: ComprobarPaso("VencerLaMaldad",
					"arma de 20+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, así que el paso siguiente pasa a ser el actual"); break;
				case 46: ComprobarLecturaDeJefeDelTramoActual("Devorador de Mundos / Cerebro de Cthulhu"); break;
				case 47: Capturar("guia-8-maldad-vencer"); break;
				case 48: MatarLaMaldadEnFalso(); break;

				// El arma de case43 (20+ de daño) sigue en la mochila y por si sola ya cumpliria el
				// unico requisito obligatorio de "ArmaParaEsqueletron" tambien: se quita aqui para
				// poder comprobar de verdad que ese paso es el que toca ANTES de tener arma, y no
				// dar por buena una casualidad de la propia prueba.
				case 49: QuitarArmaDeLaMochila(); break;
				case 50: ComprobarPaso("ArmaParaEsqueletron",
					"downedBoss2=true a mano (la maldad del mundo se da por cerrada, el siguiente " +
					"tramo implementado es Esqueletron) y sin arma todavia en la mochila"); break;

				// --- cuarto tramo implementado: Esqueletron y la Mazmorra ------------------------
				case 51: CrearAncianoDeMentira(); break;
				case 52: ComprobarPaso("ArmaParaEsqueletron",
					"el Anciano ya vive en el mundo: es un requisito recomendado (\"Que viva " +
					"contigo\"), asi que se ve cumplido en el medidor pero sigue sin cerrar el paso"); break;
				case 53: PonerArmaConDano(20); break;
				case 54: VolcarEstadoDelJugador("con arma contra Esqueletron"); break;
				case 55: ComprobarPaso("VencerAEsqueletron",
					"arma de 20+ de daño puesta: el primer paso de Esqueletron ya esta cumplido"); break;
				case 56: ComprobarLecturaDeJefeDelTramoActual("Esqueletron"); break;
				case 57: Capturar("guia-9-esqueletron"); break;
				case 58: MatarAEsqueletronEnFalso(); break;

				// --- quinto tramo implementado: el Muro de Carne (paso a Hardmode) --------------
				// El arma de case53 (20+ de daño) sigue en la mochila y por su rango (20 a 40) puede
				// bastar por casualidad para el umbral de 25 de "ArmaParaElMuro": se quita aqui,
				// igual que se hizo entre la maldad del mundo y Esqueletron, para comprobar de
				// verdad que el paso es el que toca ANTES de tener arma.
				case 59: PrepararMuroDeCarne(); break;
				case 60: QuitarArmaDeLaMochila(); break;
				case 61: ComprobarPaso("ArmaParaElMuro",
					"downedBoss3=true a mano (el siguiente tramo implementado es el Muro de Carne) " +
					"y sin arma todavia en la mochila"); break;
				case 62: Capturar("guia-11-muro-preparativos"); break;
				case 63: PonerArmaConDano(25); break;
				case 64: VolcarEstadoDelJugador("con arma contra el Muro de Carne"); break;
				case 65: ComprobarPaso("VencerAlMuro",
					"arma de 25+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, asi que el paso siguiente pasa a ser el actual"); break;
				case 66: ComprobarLecturaDeJefeDelTramoActual("Muro de Carne"); break;
				case 67: Capturar("guia-12-muro-vencer"); break;
				case 68: PasarAModoDificilDeMentira(); break;

				// --- sexto tramo implementado: los tres mecanicos --------------------------------
				// El arma de case63 (25+ de daño) sigue en la mochila y por su rango (25 a 45) puede
				// bastar por casualidad para el umbral de 40 de "ArmaParaLosMecanicos": se quita
				// aqui, igual que entre los tramos anteriores, para comprobar de verdad que el paso
				// es el que toca ANTES de tener arma.
				case 69: QuitarArmaDeLaMochila(); break;
				case 70: ComprobarPaso("ArmaParaLosMecanicos",
					"hardMode=true a mano (el siguiente tramo implementado son los tres mecanicos) " +
					"y sin arma todavia en la mochila"); break;
				case 71: Capturar("guia-14-mecanicos-preparativos"); break;
				case 72: PonerArmaConDano(40); break;
				case 73: VolcarEstadoDelJugador("con arma contra los mecanicos"); break;
				case 74: ComprobarPaso("VencerAUnMecanico",
					"arma de 40+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, asi que el paso siguiente pasa a ser el actual"); break;
				case 75: ComprobarLecturaDeJefeDelTramoActual("Destructor (jefeFinal del tramo)"); break;
				case 76: Capturar("guia-15-mecanicos-vencer"); break;
				case 77: MarcarUnMecanicoDerrotadoDeMentira(); break;

				// --- septimo tramo implementado: Plantera -----------------------------------------
				case 78: QuitarArmaDeLaMochila(); break;
				case 79: ComprobarPaso("ArmaParaPlantera",
					"downedMechBossAny=true a mano (el siguiente tramo implementado es Plantera) y " +
					"sin arma todavia en la mochila"); break;
				case 80: Capturar("guia-17-plantera-preparativos"); break;
				case 81: PonerArmaConDano(45); break;
				case 82: VolcarEstadoDelJugador("con arma contra Plantera"); break;
				case 83: ComprobarPaso("VencerAPlantera",
					"arma de 45+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, asi que el paso siguiente pasa a ser el actual"); break;
				case 84: ComprobarLecturaDeJefeDelTramoActual("Plantera"); break;
				case 85: Capturar("guia-18-plantera-vencer"); break;
				case 86: MarcarPlanteraDerrotadaDeMentira(); break;

				// --- octavo tramo implementado: el Templo y el Golem ------------------------------
				case 87: QuitarArmaDeLaMochila(); break;
				case 88: ComprobarPaso("ArmaParaElTemplo",
					"downedPlantBoss=true a mano (el siguiente tramo implementado es el Templo y el " +
					"Golem) y sin arma todavia en la mochila"); break;
				case 89: Capturar("guia-19-templo-preparativos"); break;
				case 90: PonerArmaConDano(55); break;
				case 91: VolcarEstadoDelJugador("con arma contra el Golem"); break;
				case 92: ComprobarPaso("VencerAlGolem",
					"arma de 55+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, asi que el paso siguiente pasa a ser el actual"); break;
				case 93: ComprobarLecturaDeJefeDelTramoActual("Golem"); break;
				case 94: Capturar("guia-20-templo-vencer"); break;
				case 95: MarcarGolemDerrotadoDeMentira(); break;

				// --- noveno tramo implementado: el Cultista Lunatico y las cuatro torres ---------
				case 96: QuitarArmaDeLaMochila(); break;
				case 97: ComprobarPaso("ArmaParaElCultista",
					"downedGolemBoss=true a mano (el siguiente tramo implementado es el Cultista y " +
					"las torres) y sin arma todavia en la mochila"); break;
				case 98: Capturar("guia-22-cultista-preparativos"); break;
				case 99: PonerArmaConDano(65); break;
				case 100: VolcarEstadoDelJugador("con arma contra el Cultista"); break;
				case 101: ComprobarPaso("VencerAlCultista",
					"arma de 65+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, asi que el paso siguiente pasa a ser el actual"); break;
				case 102: ComprobarLecturaDeJefeDelTramoActual("Cultista Lunatico"); break;
				case 103: Capturar("guia-23-cultista-vencer"); break;
				case 104: MarcarCultistaDerrotadoDeMentira(); break;

				case 105: QuitarArmaDeLaMochila(); break;
				case 106: ComprobarPaso("PrepararLasTorres",
					"downedAncientCultist=true a mano (tercer paso del mismo tramo: las torres) y " +
					"sin arma todavia en la mochila"); break;
				case 107: Capturar("guia-24-torres-preparativos"); break;
				case 108: PonerArmaConDano(75); break;
				case 109: VolcarEstadoDelJugador("con arma contra las torres"); break;
				case 110: ComprobarPaso("VencerALasTorres",
					"arma de 75+ de daño puesta: el unico requisito obligatorio del tercer paso ya " +
					"esta cumplido, asi que el cuarto paso pasa a ser el actual"); break;
				case 111: Capturar("guia-25-torres-vencer"); break;
				case 112: MarcarTorresDerrotadasDeMentira(); break;

				// --- decimo y ultimo tramo implementado: Moon Lord --------------------------------
				case 113: QuitarArmaDeLaMochila(); break;
				case 114: ComprobarPaso("ArmaParaMoonLord",
					"downedTowers=true a mano (el siguiente tramo implementado es Moon Lord) y sin " +
					"arma todavia en la mochila"); break;
				case 115: Capturar("guia-26-moonlord-preparativos"); break;
				case 116: PonerArmaConDano(90); break;
				case 117: VolcarEstadoDelJugador("con arma contra Moon Lord"); break;
				case 118: ComprobarPaso("VencerAMoonLord",
					"arma de 90+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, asi que el paso siguiente pasa a ser el actual"); break;
				case 119: ComprobarLecturaDeJefeDelTramoActual("Moon Lord"); break;
				case 120: Capturar("guia-27-moonlord-vencer"); break;
				case 121: MarcarMoonLordDerrotadoDeMentira(); break;
				case 122: ComprobarOpcionalSuperadoDesaparece(); break;

				case 123: ComprobarSinObjetivo(); break;
				case 124: Capturar("guia-28-todo-lo-implementado-hecho"); break;

				case 125: RestaurarTramosNuevos(); break;

				// --- los dos tramos opcionales nuevos de esta sesion: ReySlime y Deerclops -------
				// Los dos se dejaron marcados "superados" desde Arrancar() para no interferir con
				// nada de lo anterior. Aqui se ponen en false uno a uno, se comprueban con
				// EstadoGuia.PasoOpcionalActual (el mismo camino que usa ContenidoGuia.MontarDetalle
				// para la seccion "Objetivo opcional") y se devuelven a su valor original al final.
				case 126: PrepararOpcionalesTempranos(); break;
				case 127: ComprobarPasoOpcional("ReySlime", "ArmaParaElReySlime",
					"ningun opcional sin superar todavia: ReySlime es el de menor Orden (5) de todos " +
					"los tramos opcionales, por delante incluso de PreOjo (10)"); break;
				case 128: Capturar("guia-29-reyslime-preparativos"); break;
				case 129: PonerArmaConDano(12); break;
				case 130: VolcarEstadoDelJugador("con arma contra el Rey Slime"); break;
				case 131: ComprobarPasoOpcional("ReySlime", "VencerAlReySlime",
					"arma de 12+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, asi que el paso siguiente pasa a ser el objetivo opcional"); break;
				case 132: ComprobarLecturaDeJefeOpcional("Rey Slime"); break;
				case 133: Capturar("guia-30-reyslime-vencer"); break;
				case 134: MarcarReySlimeDerrotadoDeMentira(); break;
				case 135: QuitarArmaDeLaMochila(); break;
				case 136: ComprobarPasoOpcional("Deerclops", "ArmaParaDeerclops",
					"ReySlime ya superado (downedSlimeKing=true a mano): el siguiente opcional por " +
					"Orden es Deerclops (15), por delante todavia de ReinaAbeja (25)"); break;
				case 137: Capturar("guia-31-deerclops-preparativos"); break;
				case 138: PonerArmaConDano(16); break;
				case 139: VolcarEstadoDelJugador("con arma contra Deerclops"); break;
				case 140: ComprobarPasoOpcional("Deerclops", "VencerADeerclops",
					"arma de 16+ de daño puesta: el unico requisito obligatorio del primer paso ya " +
					"esta cumplido, asi que el paso siguiente pasa a ser el objetivo opcional"); break;
				case 141: ComprobarLecturaDeJefeOpcional("Deerclops"); break;
				case 142: Capturar("guia-32-deerclops-vencer"); break;
				case 143: MarcarDeerclopsDerrotadoDeMentira(); break;
				case 144: QuitarArmaDeLaMochila(); break;
				case 145: ComprobarLosDosOpcionalesTempranosDesaparecen(); break;
				case 146: RestaurarOpcionalesTempranos(); break;
				// Reutilizada (es idempotente: vuelve a escribir los mismos _xOriginal de
				// Arrancar()) para deshacer el cierre del camino obligatorio que hizo
				// PrepararOpcionalesTempranos solo para las capturas de este bloque.
				case 147: RestaurarTramosNuevos(); break;

				// --- direccion horizontal real (izquierda/derecha), la otra pieza de diseño ------
				// pendiente del encargo. Solo cubre la Mazmorra por ahora (Main.dungeonX es el unico
				// ancla horizontal real y siempre disponible del motor - ver el porque completo en
				// EstadoGuia.LadoHorizontalDelPaso). Se prueba con el paso real "ArmaParaEsqueletron"
				// (Zona=Mazmorra), moviendo al jugador de verdad a los dos lados de dungeonX.
				case 148: ComprobarDireccionHorizontalMazmorra(); break;
				case 149: RestaurarDireccionHorizontal(); break;
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
			_downedBoss2Original = NPC.downedBoss2;
			_downedBoss3Original = NPC.downedBoss3;
			_shadowOrbSmashedOriginal = Terraria.WorldGen.shadowOrbSmashed;
			_hardModeOriginal = Main.hardMode;
			_downedMechBoss1Original = NPC.downedMechBoss1;
			_downedMechBossAnyOriginal = NPC.downedMechBossAny;
			_downedPlantBossOriginal = NPC.downedPlantBoss;
			_downedGolemBossOriginal = NPC.downedGolemBoss;
			_downedAncientCultistOriginal = NPC.downedAncientCultist;
			_downedTowerSolarOriginal = NPC.downedTowerSolar;
			_downedTowerVortexOriginal = NPC.downedTowerVortex;
			_downedTowerNebulaOriginal = NPC.downedTowerNebula;
			_downedTowerStardustOriginal = NPC.downedTowerStardust;
			_downedMoonlordOriginal = NPC.downedMoonlord;
			_downedQueenBeeOriginal = NPC.downedQueenBee;
			NPC.downedBoss1 = false;

			// ReySlime y Deerclops (los dos tramos opcionales nuevos de esta sesion, Orden 5 y 15,
			// los mas bajos de todos los opcionales) se marcan SUPERADOS desde ya: asi no interfieren
			// con ningun paso 1-125 de este arnes (que ya daban por buenos "ReinaAbeja"/
			// "InicioModoDificil"/"JefesOpcionalesTardios" como los opcionales de menor Orden sin
			// superar). Su propio bloque dedicado, al final de la prueba, los pone en false, los
			// comprueba de verdad y los devuelve a su valor ORIGINAL (no a "true") al terminar.
			_downedSlimeKingOriginal = NPC.downedSlimeKing;
			_downedDeerclopsOriginal = NPC.downedDeerclops;
			NPC.downedSlimeKing = true;
			NPC.downedDeerclops = true;

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

		/// <summary>
		/// Igual que <see cref="ComprobarLecturaDelJefe"/> pero SIN un tipo de NPC fijo: pregunta al
		/// propio tramo en pantalla cual es su <c>JefeFinal</c> (ya resuelto por
		/// <see cref="CatalogoGuia"/> segun corrupcion/carmesi si toca), asi sirve para cualquier
		/// tramo nuevo sin tener que escribir una copia por jefe.
		/// </summary>
		private static void ComprobarLecturaDeJefeDelTramoActual(string etiqueta)
		{
			ContenidoGuia contenido = GuiaSystem.PanelActual;
			if (contenido == null || contenido.TramoEnPantalla == null) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA: no hay tramo en pantalla para " +
					"leer el jefe de \"" + etiqueta + "\".");
				return;
			}

			int tipoJefe = contenido.TramoEnPantalla.JefeFinal;
			int vida, dano, defensa;
			bool ok = EvaluadorGuia.StatsDeJefe(tipoJefe, out vida, out dano, out defensa);

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - stats REALES de \"" + etiqueta +
				"\" en esta partida (ContentSamples + NPC.ScaleStats con Main.GameModeInfo): tipo=" +
				tipoJefe + " (\"" + EvaluadorGuia.NombreDeNpc(tipoJefe) + "\"), resuelto=" + ok +
				", vida=" + vida + ", defensa=" + defensa + ", daño=" + dano +
				". Mundo carmesi=" + Terraria.WorldGen.crimson + ". Modo de esta partida: " +
				EstadoGuia.MundoActualModo() + ".");

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - lectura que ve el jugador:\n" +
				EstadoGuia.LecturaDeJefe(tipoJefe));
		}

		private static void ComprobarSinObjetivo()
		{
			ContenidoGuia contenido = GuiaSystem.PanelActual;
			if (contenido == null) {
				return;
			}
			contenido.Reconstruir();

			bool ok = contenido.PasoEnPantalla == null;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - con todos los tramos IMPLEMENTADOS " +
				"dados por hechos no queda ningun paso pendiente: paso en pantalla=" +
				(contenido.PasoEnPantalla != null ? contenido.PasoEnPantalla.Clave : "(ninguno)") + " " +
				(ok ? "-> OK: se da por cerrado." : "-> NO CUADRA."));

			// Hallazgo real (se vio en una captura, no en el log): con todo cerrado, "lo que viene
			// despues" volvia a listar los tramos YA SUPERADOS porque el punto de partida era 0.
			// Se comprueba aqui con nombres, no solo con datos, que ninguno de los tres tramos que
			// esta prueba acaba de cerrar aparece en la hoja de ruta.
			System.Collections.Generic.List<TramoGuia> porDelante = EstadoGuia.TramosPorDelante(null);
			System.Text.StringBuilder claves = new System.Text.StringBuilder();
			bool haySuperado = false;
			for (int i = 0; i < porDelante.Count; i++) {
				if (i > 0) {
					claves.Append(", ");
				}
				claves.Append(porDelante[i].Clave);
				if (porDelante[i].Clave == "PreOjo" || porDelante[i].Clave == "MaldadDelMundo" ||
					porDelante[i].Clave == "Esqueletron" || porDelante[i].Clave == "MuroDeCarne" ||
					porDelante[i].Clave == "Mecanicos" || porDelante[i].Clave == "Plantera" ||
					porDelante[i].Clave == "TemploYGolem" || porDelante[i].Clave == "EventosLunares" ||
					porDelante[i].Clave == "MoonLord") {
					haySuperado = true;
				}
			}
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - hoja de ruta con todo cerrado (camino " +
				"obligatorio entero, incluido Moon Lord): " + claves + " " + (!haySuperado
					? "-> OK: ninguno de los nueve tramos ya superados vuelve a aparecer."
					: "-> NO CUADRA: hay un tramo ya superado en \"lo que viene despues\"."));

			// Comprueba el arreglo REAL de esta misma sesion (visto primero en una captura, no en
			// el log: con Moon Lord cerrado la columna derecha se quedaba completamente en blanco,
			// escondiendo los tres tramos opcionales - ReinaAbeja, InicioModoDificil,
			// JefesOpcionalesTardios - justo el "sentirse perdido en contenido opcional" que no se
			// puede permitir). Los tres estan IMPLEMENTADOS (tienen requisitos evaluables de
			// verdad) pero son OPCIONALES (TramoGuia.Opcional), asi que deben seguir en la hoja de
			// ruta mientras ninguno este superado, sin que su Orden influya.
			bool tieneReinaAbeja = false, tieneInicioModoDificil = false, tieneJefesOpcionales = false;
			for (int i = 0; i < porDelante.Count; i++) {
				if (porDelante[i].Clave == "ReinaAbeja") tieneReinaAbeja = true;
				if (porDelante[i].Clave == "InicioModoDificil") tieneInicioModoDificil = true;
				if (porDelante[i].Clave == "JefesOpcionalesTardios") tieneJefesOpcionales = true;
			}
			bool tresPresentes = tieneReinaAbeja && tieneInicioModoDificil && tieneJefesOpcionales;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - los tres tramos opcionales, " +
				"ninguno superado todavia (ReinaAbeja=" + tieneReinaAbeja + ", InicioModoDificil=" +
				tieneInicioModoDificil + ", JefesOpcionalesTardios=" + tieneJefesOpcionales + ") " +
				(tresPresentes
					? "-> OK: los tres siguen en la hoja de ruta pese a que su Orden ya quedo atras."
					: "-> NO CUADRA: falta al menos uno de los tres opcionales en \"lo que viene despues\"."));
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

		// -------------------------------------------------------------------------------------
		// Segundo y cuarto tramo: la maldad del mundo y Esqueletron
		// -------------------------------------------------------------------------------------

		/// <summary>Cierra el primer tramo a mano (para que la guia pase al siguiente IMPLEMENTADO)
		/// y deja limpio el estado propio de la maldad del mundo.</summary>
		private static void PrepararMaldadDelMundo()
		{
			NPC.downedBoss1 = true;
			NPC.downedBoss2 = false;
			Terraria.WorldGen.shadowOrbSmashed = false;

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - preparado el tramo de la maldad " +
				"del mundo: downedBoss1=true (a mano), downedBoss2=false, shadowOrbSmashed=false. " +
				"Mundo carmesi (Terraria.WorldGen.crimson)=" + Terraria.WorldGen.crimson + ".");
		}

		/// <summary>Pone <c>WorldGen.shadowOrbSmashed</c> a true SIN romper ninguna esfera de
		/// verdad, para comprobar que la bandera nueva de <see cref="BanderasGuia"/> lee el campo
		/// real y no un contador propio.</summary>
		private static void MarcarEsferaRotaDeMentira()
		{
			bool antes = Terraria.WorldGen.shadowOrbSmashed;
			Terraria.WorldGen.shadowOrbSmashed = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - WorldGen.shadowOrbSmashed puesto " +
				"a true a mano (" + antes + " -> " + Terraria.WorldGen.shadowOrbSmashed + "). Lo que lee " +
				"la bandera \"shadowOrbSmashed\" ahora: " + BanderasGuia.Valor("shadowOrbSmashed") + ".");
		}

		/// <summary>Mete en la mochila la Comida de Gusano (o la Espina Sangrienta si esa no existe
		/// en esta version), objetos reales buscados por id, no por nombre.</summary>
		private static void PonerObjetoDeInvocacion()
		{
			int tipo = ItemID.WormFood;
			if (!ContentSamples.ItemsByType.ContainsKey(tipo)) {
				tipo = ItemID.BloodySpine;
			}

			Item objeto = new Item();
			objeto.SetDefaults(tipo);
			Main.LocalPlayer.inventory[1] = objeto;

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - objeto de invocacion puesto en la " +
				"mochila: \"" + objeto.Name + "\" (id " + tipo + "). Lo que cuenta la guia (" +
				"objeto_cualquiera 70/1331): " +
				(EstadoJugadorGuia.CuantosLleva(ItemID.WormFood) + EstadoJugadorGuia.CuantosLleva(ItemID.BloodySpine)) + ".");
		}

		/// <summary>Equipa un arma real, buscada POR DAÑO (no por id fijo), con el mismo daño que
		/// pide el paso de turno. Sirve para el tramo de la maldad del mundo y para Esqueletron:
		/// los dos piden 20 de daño obligatorio.</summary>
		private static void PonerArmaConDano(int minimo)
		{
			int tipo = BuscarObjeto(o => o.damage >= minimo && o.damage <= minimo + 20 && !o.accessory &&
				o.ammo == AmmoID.None && o.pick == 0 && o.axe == 0 && o.hammer == 0 &&
				o.useStyle != ItemUseStyleID.None);

			if (tipo <= 0) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA: no se encontro ningun arma de " +
					"al menos " + minimo + " de daño.");
				return;
			}

			Item objeto = new Item();
			objeto.SetDefaults(tipo);
			Main.LocalPlayer.inventory[0] = objeto;

			string nombre;
			int dano = EstadoJugadorGuia.DanoDelMejorArma(out nombre);
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - arma puesta en la mochila: \"" +
				objeto.Name + "\" (Item.damage=" + objeto.damage + "). Lo que lee la guia: \"" + nombre +
				"\" con " + dano + ".");
		}

		/// <summary>Vacia la ranura 0 de la mochila. Hace falta entre los dos tramos nuevos: el
		/// arma de 20+ de daño que pide "ArmaContraLaMaldad" tambien cumple, ella sola,
		/// "ArmaParaEsqueletron" (el mismo umbral), y sin quitarla de en medio la prueba no
		/// comprobaria de verdad que ese paso es el que toca antes de tener un arma.</summary>
		private static void QuitarArmaDeLaMochila()
		{
			Main.LocalPlayer.inventory[0] = new Item();
			string nombre;
			int dano = EstadoJugadorGuia.DanoDelMejorArma(out nombre);
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - arma quitada de la mochila antes " +
				"de Esqueletron. Mejor arma que lee la guia ahora: \"" +
				(string.IsNullOrEmpty(nombre) ? "(ninguna)" : nombre) + "\" con " + dano + ".");
		}

		/// <summary>Marca la maldad del mundo como derrotada SIN pelearla, para comprobar el cierre
		/// del segundo tramo y el paso al siguiente tramo IMPLEMENTADO.</summary>
		private static void MatarLaMaldadEnFalso()
		{
			NPC.downedBoss2 = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedBoss2 puesto a true a " +
				"mano (la bandera real que enciende NPC.SetEventFlagCleared al morir el Devorador o el " +
				"Cerebro). Se restaura al terminar.");
		}

		/// <summary>Hace aparecer al Anciano de verdad con <c>NPC.NewNPC</c>, para comprobar el
		/// requisito recomendado "npc" id 37 del tramo de Esqueletron.</summary>
		private static void CrearAncianoDeMentira()
		{
			if (EstadoJugadorGuia.HayNpc(NPCID.OldMan)) {
				RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - el Anciano ya vivia en el " +
					"mundo de prueba, no hace falta crearlo.");
				return;
			}

			int indice = NPC.NewNPC(new EntitySource_WorldGen("autoprueba de la Guia"),
				(int)Main.LocalPlayer.position.X, (int)Main.LocalPlayer.position.Y, NPCID.OldMan);
			if (indice >= 0 && indice < Main.maxNPCs) {
				Main.npc[indice].homeless = true;
				_npcsCreados.Add(indice);
			}

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - Anciano creado de verdad con " +
				"NPC.NewNPC (id " + NPCID.OldMan + "). HayNpc(37) ahora: " +
				EstadoJugadorGuia.HayNpc(NPCID.OldMan) + ".");
		}

		/// <summary>Marca a Esqueletron como derrotado SIN pelearlo, para comprobar el cierre del
		/// cuarto tramo.</summary>
		private static void MatarAEsqueletronEnFalso()
		{
			NPC.downedBoss3 = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedBoss3 puesto a true a " +
				"mano. Se restaura al terminar.");
		}

		/// <summary>Deja el mundo listo para el Muro de Carne: fuerza <c>Main.hardMode</c> a false
		/// por si el mundo sintetico de pruebas ya lo traia puesto, para partir de un estado
		/// conocido igual que <see cref="PrepararMaldadDelMundo"/>.</summary>
		private static void PrepararMuroDeCarne()
		{
			Main.hardMode = false;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - preparado el tramo del Muro de " +
				"Carne: Main.hardMode=false (a mano, por si el mundo de pruebas ya lo traia puesto).");
		}

		/// <summary>Pone <c>Main.hardMode</c> a true SIN pelear al Muro de Carne de verdad, para
		/// comprobar el cierre del quinto tramo. El motor real lo hace desde
		/// <c>WorldGen.StartHardmode()</c> (NPC.cs, case 113 de la muerte del jefe), que convierte
		/// medio mundo de golpe; aqui solo se marca la bandera que la guia lee, igual que las demas
		/// pruebas "EnFalso" de este arnes marcan solo la bandera de jefe sin simular el combate
		/// entero.</summary>
		private static void PasarAModoDificilDeMentira()
		{
			Main.hardMode = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - Main.hardMode puesto a true a " +
				"mano (la bandera real que enciende WorldGen.StartHardmode al morir el Muro de Carne, " +
				"NPC.cs case 113). Se restaura al terminar.");
		}

		/// <summary>Marca uno de los tres mecanicos (el Destructor) como derrotado SIN pelearlo,
		/// para comprobar el cierre del sexto tramo. El motor real solo exige UNO de los tres:
		/// NPC.cs case 134 pone downedMechBoss1 y downedMechBossAny juntos al morir el Destructor,
		/// y son los mismos dos campos que tocaria matar a cualquiera de los otros dos.</summary>
		private static void MarcarUnMecanicoDerrotadoDeMentira()
		{
			NPC.downedMechBoss1 = true;
			NPC.downedMechBossAny = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedMechBoss1 y " +
				"downedMechBossAny puestos a true a mano (la pareja real que enciende " +
				"NPC.SetEventFlagCleared al morir el Destructor, NPC.cs case 134; matar a los " +
				"Gemelos o a Esqueletron Prime en su lugar tocaria los mismos dos campos). Se " +
				"restaura al terminar.");
		}

		/// <summary>Marca a Plantera como derrotada SIN pelearla, para comprobar el cierre del
		/// septimo tramo.</summary>
		private static void MarcarPlanteraDerrotadaDeMentira()
		{
			NPC.downedPlantBoss = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedPlantBoss puesto a " +
				"true a mano (la bandera real que enciende NPC.SetEventFlagCleared al morir Plantera, " +
				"NPC.cs case 262 - la misma que abre la puerta de piedra del Templo). Se restaura al " +
				"terminar.");
		}

		/// <summary>Marca al Golem como derrotado SIN pelearlo, para comprobar el cierre del octavo
		/// tramo.</summary>
		private static void MarcarGolemDerrotadoDeMentira()
		{
			NPC.downedGolemBoss = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedGolemBoss puesto a " +
				"true a mano (la bandera real que enciende NPC.SetEventFlagCleared al morir el Golem, " +
				"NPC.cs case 245). Se restaura al terminar.");
		}

		/// <summary>Marca al Cultista Lunatico como derrotado SIN pelearlo. A proposito NO se llama
		/// a <c>WorldGen.TriggerLunarApocalypse()</c> (lo que hace el motor real en el mismo caso):
		/// eso encenderia el evento de verdad sobre el mundo de pruebas. Solo se toca la bandera
		/// que lee la guia, igual que el resto de los "EnFalso" de este arnes.</summary>
		private static void MarcarCultistaDerrotadoDeMentira()
		{
			NPC.downedAncientCultist = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedAncientCultist puesto " +
				"a true a mano (la bandera real que enciende NPC.SetEventFlagCleared al morir el " +
				"Cultista, NPC.cs case 439 - la misma linea llama tambien a " +
				"WorldGen.TriggerLunarApocalypse(), que aqui NO se ejecuta a proposito para no " +
				"encender el evento de verdad sobre el mundo de pruebas). Se restaura al terminar.");
		}

		/// <summary>Marca las cuatro torres celestiales como derrotadas SIN pelearlas, para
		/// comprobar que <c>downedTowers</c> (la propiedad calculada que exige las cuatro juntas)
		/// cierra el tramo.</summary>
		private static void MarcarTorresDerrotadasDeMentira()
		{
			NPC.downedTowerSolar = true;
			NPC.downedTowerVortex = true;
			NPC.downedTowerNebula = true;
			NPC.downedTowerStardust = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - las cuatro downedTower* " +
				"puestas a true a mano. NPC.downedTowers (la propiedad calculada) ahora: " +
				NPC.downedTowers + ". Se restaura al terminar.");
		}

		/// <summary>Marca a Moon Lord como derrotado SIN pelearlo, para comprobar el cierre del
		/// ultimo tramo del camino obligatorio.</summary>
		private static void MarcarMoonLordDerrotadoDeMentira()
		{
			NPC.downedMoonlord = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedMoonlord puesto a " +
				"true a mano (la bandera real que enciende NPC.SetEventFlagCleared al morir Moon " +
				"Lord, NPC.cs case 398). Se restaura al terminar.");
		}

		/// <summary>
		/// Comprueba el otro sentido del arreglo del tramo opcional: no solo tiene que APARECER
		/// mientras no este superado (ya lo comprueba <see cref="ComprobarSinObjetivo"/> con los
		/// tres sin tocar), tambien tiene que DESAPARECER en cuanto el jugador lo cierra de
		/// verdad, sin depender de su Orden. Se marca solo la Reina Abeja como derrotada (la mas
		/// facil de aislar, Orden 3) y se comprueba que las OTRAS dos opcionales siguen ahi.
		/// </summary>
		private static void ComprobarOpcionalSuperadoDesaparece()
		{
			NPC.downedQueenBee = true;

			System.Collections.Generic.List<TramoGuia> porDelante = EstadoGuia.TramosPorDelante(null);
			bool tieneReina = false, tieneInicio = false, tieneOpcionalesTardios = false;
			for (int i = 0; i < porDelante.Count; i++) {
				if (porDelante[i].Clave == "ReinaAbeja") tieneReina = true;
				if (porDelante[i].Clave == "InicioModoDificil") tieneInicio = true;
				if (porDelante[i].Clave == "JefesOpcionalesTardios") tieneOpcionalesTardios = true;
			}
			bool ok = !tieneReina && tieneInicio && tieneOpcionalesTardios;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedQueenBee puesto a true " +
				"a mano. Hoja de ruta: ReinaAbeja presente=" + tieneReina + " (debe ser false, ya " +
				"superada), InicioModoDificil presente=" + tieneInicio + ", JefesOpcionalesTardios " +
				"presente=" + tieneOpcionalesTardios + " (deben seguir true, no superados) " +
				(ok ? "-> OK." : "-> NO CUADRA."));

			// El "objetivo opcional" (la seccion nueva del panel, ver ContenidoGuia.MontarDetalle)
			// tiene que saltar de la Reina Abeja (recien superada) al tramo opcional SIGUIENTE
			// (InicioModoDificil), sin que le importe el Orden ni el tramo obligatorio activo. El
			// arma de 90+ de daño del paso de Moon Lord (case116) sigue puesta a estas alturas de
			// la prueba y por si sola ya cumple el unico requisito obligatorio del primer paso de
			// InicioModoDificil (dano_arma>=30), asi que lo correcto es que el objetivo opcional
			// sea ya su SEGUNDO paso, no el primero - la misma logica ya probada mas arriba con
			// "VencerAlCultista".
			TramoGuia tramoOpcional;
			PasoGuia pasoOpcional = EstadoGuia.PasoOpcionalActual(out tramoOpcional);
			bool okOpcional = pasoOpcional != null && pasoOpcional.Clave == "VencerALaReinaSlime" &&
				tramoOpcional != null && tramoOpcional.Clave == "InicioModoDificil";
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - objetivo opcional con la Reina " +
				"Abeja superada: \"" + (pasoOpcional != null ? tramoOpcional.Clave + "/" + pasoOpcional.Clave : "(ninguno)") +
				"\", esperado \"InicioModoDificil/VencerALaReinaSlime\" (el arma de 90+ de daño " +
				"que sigue puesta desde el paso de Moon Lord ya cumple el primer paso ella sola) " +
				(okOpcional ? "-> OK." : "-> NO CUADRA."));

			NPC.downedQueenBee = _downedQueenBeeOriginal;
		}

		/// <summary>Deshace TODO lo que han tocado los pasos 37-122: las banderas nuevas, el mundo y
		/// el inventario, ademas de los NPC creados en este segundo bloque de la prueba.</summary>
		private static void RestaurarTramosNuevos()
		{
			NPC.downedBoss1 = _downedBoss1Original;
			NPC.downedBoss2 = _downedBoss2Original;
			NPC.downedBoss3 = _downedBoss3Original;
			Terraria.WorldGen.shadowOrbSmashed = _shadowOrbSmashedOriginal;
			Main.hardMode = _hardModeOriginal;
			NPC.downedMechBoss1 = _downedMechBoss1Original;
			NPC.downedMechBossAny = _downedMechBossAnyOriginal;
			NPC.downedPlantBoss = _downedPlantBossOriginal;
			NPC.downedGolemBoss = _downedGolemBossOriginal;
			NPC.downedAncientCultist = _downedAncientCultistOriginal;
			NPC.downedTowerSolar = _downedTowerSolarOriginal;
			NPC.downedTowerVortex = _downedTowerVortexOriginal;
			NPC.downedTowerNebula = _downedTowerNebulaOriginal;
			NPC.downedTowerStardust = _downedTowerStardustOriginal;
			NPC.downedMoonlord = _downedMoonlordOriginal;
			NPC.downedQueenBee = _downedQueenBeeOriginal;

			Player jugador = Main.LocalPlayer;
			jugador.inventory[0] = new Item();
			jugador.inventory[1] = new Item();

			int quitados = 0;
			for (int i = 0; i < _npcsCreados.Count; i++) {
				int indice = _npcsCreados[i];
				if (indice >= 0 && indice < Main.maxNPCs && Main.npc[indice] != null && Main.npc[indice].active) {
					Main.npc[indice].active = false;
					quitados++;
				}
			}
			_npcsCreados.Clear();

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - restaurado tras el camino " +
				"obligatorio entero: downedBoss1=" + NPC.downedBoss1 + ", downedBoss2=" + NPC.downedBoss2 +
				", downedBoss3=" + NPC.downedBoss3 + ", shadowOrbSmashed=" +
				Terraria.WorldGen.shadowOrbSmashed + ", hardMode=" + Main.hardMode +
				", downedMechBoss1=" + NPC.downedMechBoss1 + ", downedMechBossAny=" + NPC.downedMechBossAny +
				", downedPlantBoss=" + NPC.downedPlantBoss + ", downedGolemBoss=" + NPC.downedGolemBoss +
				", downedAncientCultist=" + NPC.downedAncientCultist + ", downedTowers=" + NPC.downedTowers +
				", downedMoonlord=" + NPC.downedMoonlord + ", NPC de prueba retirados=" + quitados + ".");
		}

		// -------------------------------------------------------------------------------------
		// Los dos tramos opcionales nuevos de esta sesion: ReySlime y Deerclops
		// -------------------------------------------------------------------------------------

		/// <summary>Igual que <see cref="ComprobarPaso"/> pero para el objetivo OPCIONAL
		/// (<see cref="EstadoGuia.PasoOpcionalActual"/>), que vive en su propio hueco del panel
		/// (la seccion "Objetivo opcional" de <c>ContenidoGuia.MontarDetalle</c>) y nunca sustituye
		/// al objetivo obligatorio de <see cref="ComprobarPaso"/>.</summary>
		private static void ComprobarPasoOpcional(string tramoEsperado, string claveEsperada, string porque)
		{
			ContenidoGuia contenido = GuiaSystem.PanelActual;
			if (contenido == null) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA: la pestaña de la Guia no esta montada.");
				return;
			}
			contenido.Reconstruir();

			TramoGuia tramo;
			PasoGuia paso = EstadoGuia.PasoOpcionalActual(out tramo);
			string clave = paso != null ? paso.Clave : "(ninguno)";
			string tramoClave = tramo != null ? tramo.Clave : "(ninguno)";
			bool ok = clave == claveEsperada && tramoClave == tramoEsperado;

			int cumplidos = 0, total = 0;
			float preparacion = paso != null ? EvaluadorGuia.Preparacion(paso, out cumplidos, out total) : 0f;

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - objetivo OPCIONAL: \"" + tramoClave + "/" +
				clave + "\" (\"" + (paso != null ? paso.Titulo : "-") + "\"), esperado \"" + tramoEsperado + "/" +
				claveEsperada + "\" porque " + porque + " " + (ok ? "-> OK" : "-> NO CUADRA") +
				". Preparacion " + (int)(preparacion * 100f + 0.5f) + "% (" + cumplidos + "/" + total +
				" obligatorios).");

			if (paso != null) {
				List<ResultadoRequisito> resultados = EvaluadorGuia.Evaluar(paso);
				for (int i = 0; i < resultados.Count; i++) {
					ResultadoRequisito estado = resultados[i];
					RegistroGuia.Linea(Terrakeep.LogTag + "   requisito opcional " + (i + 1) + "/" +
						resultados.Count + ": [" +
						(estado.NoEvaluable ? "?" : (estado.Cumplido ? "HECHO" : "FALTA")) + "] " +
						estado.Linea + " (" + estado.Actual + "/" + estado.Pedido +
						(estado.Requisito != null && estado.Requisito.Recomendado ? ", recomendado" : "") + ")");
				}
			}
		}

		/// <summary>Igual que <see cref="ComprobarLecturaDeJefeDelTramoActual"/> pero leyendo el jefe
		/// del objetivo OPCIONAL en vez del obligatorio.</summary>
		private static void ComprobarLecturaDeJefeOpcional(string etiqueta)
		{
			TramoGuia tramo;
			PasoGuia paso = EstadoGuia.PasoOpcionalActual(out tramo);
			if (paso == null || tramo == null) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA: no hay objetivo opcional para leer " +
					"el jefe de \"" + etiqueta + "\".");
				return;
			}

			int tipoJefe = paso.Jefe != 0 ? paso.Jefe : tramo.JefeFinal;
			int vida, dano, defensa;
			bool ok = EvaluadorGuia.StatsDeJefe(tipoJefe, out vida, out dano, out defensa);

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - stats REALES de \"" + etiqueta +
				"\" (opcional) en esta partida (ContentSamples + NPC.ScaleStats con Main.GameModeInfo): " +
				"tipo=" + tipoJefe + " (\"" + EvaluadorGuia.NombreDeNpc(tipoJefe) + "\"), resuelto=" + ok +
				", vida=" + vida + ", defensa=" + defensa + ", daño=" + dano + ". Modo de esta partida: " +
				EstadoGuia.MundoActualModo() + ".");

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - lectura que ve el jugador:\n" +
				EstadoGuia.LecturaDeJefe(tipoJefe));
		}

		/// <summary>
		/// Pone <c>downedSlimeKing</c> y <c>downedDeerclops</c> a false SIN pelear a ninguno de
		/// los dos, para poder comprobar sus tramos desde un estado conocido. Se restauran a su
		/// valor ORIGINAL (capturado en <see cref="Arrancar"/>, no necesariamente false) en
		/// <see cref="RestaurarOpcionalesTempranos"/>.
		/// </summary>
		/// <remarks>
		/// Ademas cierra el camino OBLIGATORIO entero (los mismos flags que ya deja en true
		/// <see cref="ComprobarSinObjetivo"/> mas arriba). No es cosmetica de laboratorio: sin
		/// esto, la columna izquierda del panel se queda enseñando "Armadura: mas de 10 de
		/// defensa" (el objetivo obligatorio, que aqui no se ha tocado) durante todo este bloque,
		/// y la seccion "Objetivo opcional" de la columna derecha (donde vive de verdad
		/// ReySlime/Deerclops) queda empujada FUERA del back buffer visible por el "Este tramo,
		/// paso a paso" del objetivo obligatorio - visto la primera vez en la propia captura real
		/// de esta sesion, no en el log (que seguia en verde: <see cref="ComprobarPasoOpcional"/>
		/// lee <see cref="EstadoGuia.PasoOpcionalActual"/> directamente, sin pasar por scroll ni
		/// por lo que quede o no dentro del viewport). Con el camino obligatorio cerrado, el
		/// objetivo opcional sube a la parte visible, igual que ya demostro la captura
		/// "guia-28-todo-lo-implementado-hecho.png" de la sesion anterior. Se restaura con
		/// <see cref="RestaurarTramosNuevos"/> (reutilizada, es idempotente) al final de este
		/// bloque.
		/// </remarks>
		private static void PrepararOpcionalesTempranos()
		{
			NPC.downedSlimeKing = false;
			NPC.downedDeerclops = false;

			NPC.downedBoss1 = true;
			NPC.downedBoss2 = true;
			NPC.downedBoss3 = true;
			Main.hardMode = true;
			NPC.downedMechBoss1 = true;
			NPC.downedMechBossAny = true;
			NPC.downedPlantBoss = true;
			NPC.downedGolemBoss = true;
			NPC.downedAncientCultist = true;
			NPC.downedTowerSolar = true;
			NPC.downedTowerVortex = true;
			NPC.downedTowerNebula = true;
			NPC.downedTowerStardust = true;
			NPC.downedMoonlord = true;

			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - preparados los dos opcionales " +
				"tempranos: downedSlimeKing=false, downedDeerclops=false (a mano, estaban en true desde " +
				"Arrancar() para no interferir con el resto de la prueba). El camino OBLIGATORIO se " +
				"cierra entero de nuevo (mismos flags que ComprobarSinObjetivo) para que la seccion " +
				"\"Objetivo opcional\" suba a la parte visible del back buffer en las capturas de este " +
				"bloque, en vez de quedar tapada por el objetivo obligatorio. Se restaura todo al " +
				"terminar.");
		}

		/// <summary>Marca al Rey Slime como derrotado SIN pelearlo.</summary>
		private static void MarcarReySlimeDerrotadoDeMentira()
		{
			NPC.downedSlimeKing = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedSlimeKing puesto a true " +
				"a mano (la bandera real que enciende NPC.SetEventFlagCleared al morir el Rey Slime, " +
				"NPC.cs case 50). Se restaura al terminar.");
		}

		/// <summary>Marca a Deerclops como derrotado SIN pelearlo.</summary>
		private static void MarcarDeerclopsDerrotadoDeMentira()
		{
			NPC.downedDeerclops = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - NPC.downedDeerclops puesto a true " +
				"a mano (la bandera real que enciende NPC.SetEventFlagCleared al morir Deerclops, " +
				"NPC.cs case 668, linea ~85353). Se restaura al terminar.");
		}

		/// <summary>
		/// Comprueba el otro sentido del arreglo del tramo opcional (el mismo que ya comprobaba
		/// <see cref="ComprobarOpcionalSuperadoDesaparece"/> para la Reina Abeja), aplicado a los
		/// dos tramos opcionales de menor Orden de todos: tienen que DESAPARECER de la hoja de ruta
		/// en cuanto se superan, y el objetivo opcional tiene que saltar al siguiente pendiente por
		/// Orden (ReinaAbeja, sin tocar en todo este bloque).
		/// </summary>
		private static void ComprobarLosDosOpcionalesTempranosDesaparecen()
		{
			List<TramoGuia> porDelante = EstadoGuia.TramosPorDelante(null);
			bool tieneReySlime = false, tieneDeerclops = false, tieneReinaAbeja = false;
			for (int i = 0; i < porDelante.Count; i++) {
				if (porDelante[i].Clave == "ReySlime") tieneReySlime = true;
				if (porDelante[i].Clave == "Deerclops") tieneDeerclops = true;
				if (porDelante[i].Clave == "ReinaAbeja") tieneReinaAbeja = true;
			}
			bool ok = !tieneReySlime && !tieneDeerclops && tieneReinaAbeja;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - con ReySlime y Deerclops superados, " +
				"hoja de ruta: ReySlime presente=" + tieneReySlime + " (debe ser false, ya superado), " +
				"Deerclops presente=" + tieneDeerclops + " (debe ser false, ya superado), ReinaAbeja " +
				"presente=" + tieneReinaAbeja + " (debe seguir true, no se ha tocado) " +
				(ok ? "-> OK." : "-> NO CUADRA."));

			TramoGuia tramoOpcional;
			PasoGuia pasoOpcional = EstadoGuia.PasoOpcionalActual(out tramoOpcional);
			bool okSiguiente = pasoOpcional != null && tramoOpcional != null &&
				tramoOpcional.Clave == "ReinaAbeja" && pasoOpcional.Clave == "ArmaParaLaReina";
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - objetivo opcional tras superar " +
				"ReySlime y Deerclops (sin arma en la mochila): \"" +
				(pasoOpcional != null ? tramoOpcional.Clave + "/" + pasoOpcional.Clave : "(ninguno)") +
				"\", esperado \"ReinaAbeja/ArmaParaLaReina\" (el siguiente opcional pendiente por Orden) " +
				(okSiguiente ? "-> OK." : "-> NO CUADRA."));
		}

		/// <summary>Devuelve <c>downedSlimeKing</c> y <c>downedDeerclops</c> a su valor ORIGINAL
		/// (el que tenia el mundo de pruebas antes de que <see cref="Arrancar"/> los forzara a
		/// true), y limpia la mochila.</summary>
		private static void RestaurarOpcionalesTempranos()
		{
			NPC.downedSlimeKing = _downedSlimeKingOriginal;
			NPC.downedDeerclops = _downedDeerclopsOriginal;
			Main.LocalPlayer.inventory[0] = new Item();
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - restaurados los dos opcionales " +
				"tempranos a su valor original: downedSlimeKing=" + NPC.downedSlimeKing +
				", downedDeerclops=" + NPC.downedDeerclops + ".");
		}

		// -------------------------------------------------------------------------------------
		// Direccion horizontal real (izquierda/derecha), solo Mazmorra por ahora
		// -------------------------------------------------------------------------------------

		/// <summary>Primer paso del catalogo con esa clave, en cualquier tramo. Lo usa la prueba de
		/// direccion horizontal para coger un paso real (Zona=Mazmorra) sin depender de en que
		/// tramo este activa la guia ahora mismo.</summary>
		private static PasoGuia BuscarPaso(string clave)
		{
			List<TramoGuia> tramos = CatalogoGuia.Tramos;
			for (int i = 0; i < tramos.Count; i++) {
				for (int j = 0; j < tramos[i].Pasos.Count; j++) {
					if (tramos[i].Pasos[j].Clave == clave) {
						return tramos[i].Pasos[j];
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Mueve al jugador de verdad a los dos lados de <c>Main.dungeonX</c> (con un valor de
		/// prueba propio, para no depender de donde haya generado la Mazmorra el mundo sintetico) y
		/// comprueba que <see cref="EstadoGuia.Direccion"/> dice el lado real que toca, usando el
		/// paso real "ArmaParaEsqueletron" (Zona=Mazmorra, el mismo dato que ve el jugador en la
		/// guia real, no un paso de mentira construido para la prueba).
		/// </summary>
		private static void ComprobarDireccionHorizontalMazmorra()
		{
			_dungeonXOriginal = Main.dungeonX;
			_posicionOriginal = Main.LocalPlayer.position;

			PasoGuia paso = BuscarPaso("ArmaParaEsqueletron");
			if (paso == null) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " AUTOPRUEBA GUIA: no se encontro el paso " +
					"\"ArmaParaEsqueletron\" para probar la direccion horizontal.");
				return;
			}

			Main.dungeonX = 1000;

			// El jugador 500 tiles a la IZQUIERDA de la Mazmorra: tiene que decir "a tu derecha".
			Main.LocalPlayer.position = new Vector2((Main.dungeonX - 500) * 16f, Main.LocalPlayer.position.Y);
			string siEstaALaIzquierda = EstadoGuia.Direccion(paso);
			bool okDerecha = siEstaALaIzquierda.Contains(Idiomas.Texto("Guia.Direccion.Derecha"));

			// El jugador 500 tiles a la DERECHA: tiene que decir "a tu izquierda".
			Main.LocalPlayer.position = new Vector2((Main.dungeonX + 500) * 16f, Main.LocalPlayer.position.Y);
			string siEstaALaDerecha = EstadoGuia.Direccion(paso);
			bool okIzquierda = siEstaALaDerecha.Contains(Idiomas.Texto("Guia.Direccion.Izquierda"));

			// El jugador justo en la columna de la Mazmorra: dentro de la tolerancia, no debe decir
			// ningun lado (evita que el aviso titubee entre izquierda/derecha por un paso de nada).
			Main.LocalPlayer.position = new Vector2(Main.dungeonX * 16f, Main.LocalPlayer.position.Y);
			string siEstaEncima = EstadoGuia.Direccion(paso);
			bool okSinLado = !siEstaEncima.Contains(Idiomas.Texto("Guia.Direccion.Derecha")) &&
				!siEstaEncima.Contains(Idiomas.Texto("Guia.Direccion.Izquierda"));

			bool ok = okDerecha && okIzquierda && okSinLado;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - direccion horizontal real " +
				"(Main.dungeonX=" + Main.dungeonX + " a mano, paso \"ArmaParaEsqueletron\"): jugador 500 " +
				"tiles a la izquierda de la Mazmorra -> \"" + siEstaALaIzquierda + "\" (debe contener \"" +
				Idiomas.Texto("Guia.Direccion.Derecha") + "\"); jugador 500 tiles a la derecha -> \"" +
				siEstaALaDerecha + "\" (debe contener \"" + Idiomas.Texto("Guia.Direccion.Izquierda") +
				"\"); jugador justo en la columna de la Mazmorra -> \"" + siEstaEncima +
				"\" (no debe decir ningun lado, dentro de la tolerancia) " +
				(ok ? "-> OK." : "-> NO CUADRA."));
		}

		private static void RestaurarDireccionHorizontal()
		{
			Main.dungeonX = _dungeonXOriginal;
			Main.LocalPlayer.position = _posicionOriginal;
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA - restaurados Main.dungeonX (" +
				Main.dungeonX + ") y la posicion real del jugador tras la prueba de direccion horizontal.");
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
