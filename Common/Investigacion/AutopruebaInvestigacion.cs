using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Investigacion;

namespace TerrakeepMod.Common.Investigacion
{
	/// <summary>
	/// Arnes de verificacion de WS5. Se enciende con <c>TERRAKEEP_AUTOTEST_WS5</c>, abre el panel
	/// el solo y ejercita cada funcion sobre el estado de investigacion REAL del personaje,
	/// dejando en el log el antes y el despues leidos del propio juego.
	/// <para />
	/// Donde se puede, se pulsan los botones de verdad (<c>UIElement.LeftClick</c>, el mismo
	/// camino que recorre un clic de raton una vez resuelto sobre que elemento cae) en vez de
	/// llamar al metodo por dentro: es la tecnica que ya uso WS4 con las pildoras de clase, y sin
	/// sesion de escritorio es la forma honesta de probar un boton (ver bitacora de WS0: un clic
	/// sintetico de Windows no llega nunca al juego).
	/// <para />
	/// Dos fases, que elige la variable <c>TERRAKEEP_WS5_FASE</c>:
	/// <list type="bullet">
	/// <item><c>investigar</c> (por defecto): la bateria completa, y al final guarda el personaje
	/// con <c>Player.SavePlayer</c>;</item>
	/// <item><c>comprobar</c>: no toca nada, solo lee el estado del objeto de prueba. Sirve para
	/// demostrar que lo que escribio la fase anterior <b>sobrevivio al guardado y a la carga
	/// reales del juego</b>, o sea que acabo dentro del <c>.plr</c> de verdad.</item>
	/// </list>
	/// </summary>
	public static class AutopruebaInvestigacion
	{
		/// <summary>Variable que enciende esta autoprueba.</summary>
		public const string Variable = PanelInvestigacionSystem.VariableAutoprueba;

		/// <summary>Fase: "investigar" (por defecto) o "comprobar".</summary>
		public const string VariableFase = "TERRAKEEP_WS5_FASE";

		/// <summary>Marca de la ejecucion, para poder identificar el log de ESTA prueba entre
		/// varias instancias del juego (mismo motivo que documento WS4).</summary>
		public const string VariableMarca = "TERRAKEEP_WS5_MARCA";

		private const int FotogramasAntesDeEmpezar = 180;
		private const int FotogramasEntrePasos = 10;

		// Objeto de prueba: el bloque de tierra. Se elige a proposito uno de vanilla, muy conocido
		// y con un N alto (100 unidades) para que el "x/N" se vea de verdad; ademas existe en
		// cualquier partida, con o sin mods.
		private const int TipoDePrueba = ItemID.DirtBlock;

		private static bool _comprobada;
		private static bool _activa;
		private static bool _terminada;
		private static bool _comprobarSolo;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		private static byte _dificultadOriginal;
		private static CarpetaInvestigacion _carpetaDePrueba;
		private static int _globalAntes;

		/// <summary>Se llama en cada <c>UpdateUI</c>. No hace nada si la variable no esta puesta.</summary>
		public static void Actualizar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
				string fase = Environment.GetEnvironmentVariable(VariableFase);
				_comprobarSolo = fase != null && fase.Trim().ToLowerInvariant() == "comprobar";
				if (_activa) {
					Registrar($"AUTOPRUEBA WS5: variable {Variable} detectada. Fase: " +
						(_comprobarSolo ? "comprobar (solo lectura)" : "investigar") +
						". Marca de ejecucion: " + Environment.GetEnvironmentVariable(VariableMarca));
				}
			}

			if (!_activa || _terminada) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			// Los 180 fotogramas de margen son los mismos que usan los demas workstreams: entrando
			// con -skipselect, PlayerInput todavia esta reinicializandose los primeros segundos.
			if (++_fotogramasEnMundo < FotogramasAntesDeEmpezar) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			try {
				if (_comprobarSolo) {
					EjecutarComprobacion();
				}
				else {
					EjecutarPaso(_paso);
					_paso++;
				}
			}
			catch (Exception excepcion) {
				Registrar($"AUTOPRUEBA WS5: EXCEPCION en el paso {_paso}: {excepcion}");
				_terminada = true;
			}
		}

		// ------------------------------------------------------------------ fase "comprobar"

		private static void EjecutarComprobacion()
		{
			_terminada = true;
			Registrar("PERSISTENCIA/1 - Personaje recien cargado del disco por el propio juego. " +
				"Estado del objeto de prueba: " + EstadoInvestigacion.Describir(TipoDePrueba));

			List<int> infinitos = EstadoInvestigacion.ObjetosInfinitosSegunElJuego();
			Registrar($"PERSISTENCIA/2 - El juego considera {infinitos.Count} objetos disponibles de " +
				$"forma infinita; el de prueba esta en esa lista: {infinitos.Contains(TipoDePrueba)}. " +
				$"Progreso global: {EstadoInvestigacion.TotalCompletos}/{EstadoInvestigacion.TotalInvestigable}.");
			Registrar("AUTOPRUEBA WS5: fase de comprobacion terminada.");
		}

		// ------------------------------------------------------------------ fase "investigar"

		private static void EjecutarPaso(int paso)
		{
			ContenidoInvestigacion contenido = PanelInvestigacionSystem.PanelActual;

			switch (paso) {
				case 0:
					_dificultadOriginal = Main.LocalPlayer.difficulty;
					Registrar($"Paso 0 - Entorno: jugador \"{Main.LocalPlayer.name}\", " +
						$"dificultad={_dificultadOriginal}, mundo \"{Main.worldName}\" GameMode={Main.GameMode}. " +
						$"EstadoInvestigacion.ModoViaje={EstadoInvestigacion.ModoViaje}, " +
						$"MundoModoViaje={EstadoInvestigacion.MundoModoViaje}. " +
						$"Objetos investigables segun la tabla real del juego: " +
						$"{EstadoInvestigacion.TotalInvestigable}, ya investigados: " +
						$"{EstadoInvestigacion.TotalCompletos}.");
					break;

				case 1:
					// El panel se abre PRIMERO con el personaje tal cual esta (dificultad 0), que
					// es el caso real de alguien que no juega en Modo Viaje: lo que hay que ver es
					// que el panel lo avisa en vez de dejarle hacer clics que no sirven de nada.
					PanelInvestigacionSystem.AbrirPanel("autoprueba (" + Variable + ")");
					break;

				case 2:
					Registrar($"Paso 2 - Panel abierto: {(PanelInvestigacionSystem.PanelAbierto ? "SI" : "NO")}, " +
						$"sin Modo Viaje. AVISO que se esta pintando: \"{contenido?.AvisoVisible}\". " +
						$"Arbol: {CatalogoInvestigacion.ResumenConstruccion}");
					break;

				case 3:
					// ESCENARIO DE LA PRUEBA, no funcionalidad del mod: se pone al personaje en
					// Modo Viaje para poder ejercitar la investigacion como se ejercitaria de
					// verdad. Se deja como estaba en el paso final, antes de guardar, para que el
					// sandbox siga sirviendo para volver a lanzar la prueba.
					Main.LocalPlayer.difficulty = EstadoInvestigacion.DificultadModoViaje;
					Registrar($"Paso 3 - ESCENARIO: Player.difficulty {_dificultadOriginal} -> " +
						$"{Main.LocalPlayer.difficulty}. EstadoInvestigacion.ModoViaje=" +
						$"{EstadoInvestigacion.ModoViaje} (esperado True).");
					break;

				case 4:
					Registrar($"Paso 4 - Con Modo Viaje, el aviso desaparece solo: " +
						$"\"{contenido?.AvisoVisible}\" (esperado vacio). " +
						$"Contenido: {contenido?.Informe()}");
					break;

				case 5: {
					// Se parte de cero usando la propia accion de quitar, asi la prueba es
					// repetible y de paso queda ejercitada esa ruta.
					string antes = EstadoInvestigacion.Describir(TipoDePrueba);
					EstadoInvestigacion.Quitar("Preparar la prueba", new int[] { TipoDePrueba });
					Registrar($"Paso 5 - Punto de partida del objeto de prueba. ANTES: {antes}. " +
						$"DESPUES de quitarle la investigacion: {EstadoInvestigacion.Describir(TipoDePrueba)}.");
					break;
				}

				case 6: {
					// Se abre la carpeta que contiene el objeto de prueba para que su fila exista
					// de verdad en la lista y se pueda pulsar su boton.
					CarpetaInvestigacion carpeta = BuscarCarpetaDe(TipoDePrueba);
					if (carpeta != null && contenido != null) {
						contenido.Seleccionar(carpeta, true);
					}
					Registrar($"Paso 6 - Carpeta del objeto de prueba: " +
						$"\"{carpeta?.Nombre}\" (ruta \"{carpeta?.Ruta}\", {carpeta?.Hechos}/{carpeta?.Total}). " +
						$"Filas de carpeta visibles en el arbol: {contenido?.FilasDeCarpeta}.");
					break;
				}

				case 7: {
					int antes = EstadoInvestigacion.Hecho(TipoDePrueba);
					string textoBoton = contenido?.PulsarBotonDeObjeto(TipoDePrueba);
					Registrar($"Paso 7 - CLIC REAL en el boton \"{textoBoton}\" de la fila del objeto de " +
						$"prueba. ANTES: {antes}/{EstadoInvestigacion.Necesario(TipoDePrueba)}. " +
						$"DESPUES: {EstadoInvestigacion.Describir(TipoDePrueba)}.");
					break;
				}

				case 8: {
					// La comprobacion que de verdad importa: preguntarle AL JUEGO, con su propio
					// metodo, si considera ese objeto disponible infinitamente. Es literalmente lo
					// que alimenta el menu de duplicacion del Modo Viaje.
					List<int> infinitos = EstadoInvestigacion.ObjetosInfinitosSegunElJuego();
					Registrar($"Paso 8 - Segun el JUEGO (FillListOfItemsThatCanBeObtainedInfinitely): " +
						$"{infinitos.Count} objetos disponibles infinitamente, y el de prueba esta en la " +
						$"lista: {infinitos.Contains(TipoDePrueba)}. " +
						$"CreativeUI.GetSacrificeCount dice: {LeerConCreativeUI(TipoDePrueba)}.");
					break;
				}

				case 9: {
					string deshecho = Historial.Deshacer();
					Registrar($"Paso 9 - Ctrl+Z sobre la investigacion. Historial ha deshecho: " +
						$"\"{deshecho}\". Estado ahora: {EstadoInvestigacion.Describir(TipoDePrueba)}.");
					break;
				}

				case 10: {
					string rehecho = Historial.Rehacer();
					Registrar($"Paso 10 - Ctrl+Y. Historial ha rehecho: \"{rehecho}\". " +
						$"Estado ahora: {EstadoInvestigacion.Describir(TipoDePrueba)}.");
					break;
				}

				case 11: {
					// Carpeta pequeña de verdad para la prueba de "investigar carpeta entera": se
					// coge la hoja con menos objetos pendientes, para no investigar media partida.
					_carpetaDePrueba = CarpetaHojaPequenia(6);
					_globalAntes = EstadoInvestigacion.TotalCompletos;
					if (_carpetaDePrueba != null && contenido != null) {
						contenido.Seleccionar(_carpetaDePrueba, true);
					}
					Registrar($"Paso 11 - Carpeta elegida para la prueba de carpeta entera: " +
						$"\"{_carpetaDePrueba?.Nombre}\" ({_carpetaDePrueba?.Hechos}/{_carpetaDePrueba?.Total}). " +
						$"Progreso global antes: {_globalAntes}/{EstadoInvestigacion.TotalInvestigable}.");
					break;
				}

				case 12: {
					string textoBoton = contenido?.PulsarBotonDeCarpeta(true);
					Registrar($"Paso 12 - CLIC REAL en \"{textoBoton}\". Carpeta " +
						$"\"{_carpetaDePrueba?.Nombre}\" ahora: {_carpetaDePrueba?.Hechos}/{_carpetaDePrueba?.Total} " +
						$"(esperado que cuadren). Progreso global: {EstadoInvestigacion.TotalCompletos}/" +
						$"{EstadoInvestigacion.TotalInvestigable} (antes {_globalAntes}).");
					break;
				}

				case 13: {
					string textoBoton = contenido?.PulsarBotonDeCarpeta(false);
					Registrar($"Paso 13 - CLIC REAL en \"{textoBoton}\". Carpeta " +
						$"\"{_carpetaDePrueba?.Nombre}\" ahora: {_carpetaDePrueba?.Hechos}/{_carpetaDePrueba?.Total} " +
						$"(esperado 0/N). Progreso global: {EstadoInvestigacion.TotalCompletos}.");
					break;
				}

				case 14: {
					// El objeto de prueba lo dejo investigado el paso 10, y quitar la carpeta del
					// paso 13 no lo toca (es otra carpeta). Se comprueba explicitamente.
					Registrar($"Paso 14 - El objeto de prueba sigue como lo dejo el rehacer: " +
						$"{EstadoInvestigacion.Describir(TipoDePrueba)}.");
					break;
				}

				case 15: {
					// Objetos de MOD: solo tiene sentido si hay algun mod de contenido cargado
					// (el script lo hace con -Calamity). Demuestra que el arbol los recoge y que
					// se investigan por la misma via oficial que los de vanilla.
					int tipoDeMod = PrimerTipoDeMod();
					if (tipoDeMod <= 0) {
						Registrar("Paso 15 - No hay ningun objeto investigable de mod en esta partida; " +
							"nada que probar aqui (relanzar con -Calamity para cubrirlo).");
						break;
					}

					CarpetaInvestigacion carpeta = BuscarCarpetaDe(tipoDeMod);
					if (carpeta != null && contenido != null) {
						contenido.Seleccionar(carpeta, true);
					}
					string antes = EstadoInvestigacion.Describir(tipoDeMod);
					string boton = contenido?.PulsarBotonDeObjeto(tipoDeMod);
					if (boton == null) {
						// La fila no esta listada (carpeta muy grande, tope de filas): se ejercita
						// igual la accion, y se dice claramente que no fue por el boton.
						EstadoInvestigacion.Investigar("Investigar objeto de mod", new int[] { tipoDeMod });
					}
					Registrar($"Paso 15 - Objeto de MOD en la carpeta \"{carpeta?.Nombre}\" " +
						$"(ruta \"{carpeta?.Ruta}\"). " +
						(boton != null ? $"CLIC REAL en el boton \"{boton}\". " : "Sin fila visible, via directa. ") +
						$"ANTES: {antes}. DESPUES: {EstadoInvestigacion.Describir(tipoDeMod)}. " +
						$"Progreso global: {EstadoInvestigacion.TotalCompletos}/{EstadoInvestigacion.TotalInvestigable}.");
					break;
				}

				case 16: {
					string texto = contenido?.PulsarBotonGlobal(false);
					// Se deja caducar la confirmacion a proposito en vez de darle el segundo clic:
					// lo que hay que demostrar es que UN SOLO clic no borra nada. Confirmarla de
					// verdad destruiria justo el estado que necesita la fase de persistencia.
					_espera = 320;   // algo mas de los 240 fotogramas que dura la confirmacion
					Registrar($"Paso 16 - Primer clic REAL en \"Quitar TODA la investigacion\": el boton pasa " +
						$"a \"{texto}\" y NO se ha ejecutado nada " +
						$"(esperando confirmacion: {contenido?.EsperandoConfirmacion}). " +
						$"El objeto de prueba sigue: {EstadoInvestigacion.Describir(TipoDePrueba)}. " +
						$"Progreso global: {EstadoInvestigacion.TotalCompletos}/{EstadoInvestigacion.TotalInvestigable}.");
					break;
				}

				case 17: {
					Registrar($"Paso 17 - Sin segundo clic, la confirmacion caduca sola. " +
						$"Esperando confirmacion: {contenido?.EsperandoConfirmacion} (esperado False), " +
						$"boton otra vez en \"{contenido?.TextoBotonGlobal(false)}\". " +
						$"Estado intacto: {EstadoInvestigacion.Describir(TipoDePrueba)}. " +
						$"Progreso global: {EstadoInvestigacion.TotalCompletos}/{EstadoInvestigacion.TotalInvestigable}.");
					break;
				}

				case 18: {
					Main.LocalPlayer.difficulty = _dificultadOriginal;
					Player.SavePlayer(Main.ActivePlayerFileData, true);
					Registrar($"Paso 18 - Dificultad devuelta a {_dificultadOriginal} y personaje guardado " +
						$"con Player.SavePlayer. Lo investigado NO depende de la dificultad: se guarda en " +
						$"Player.creativeTracker, dentro del propio .plr. La siguiente ejecucion con " +
						$"{VariableFase}=comprobar lo leera del disco.");
					break;
				}

				case 19:
					Registrar("AUTOPRUEBA WS5 COMPLETA.");
					_terminada = true;
					break;
			}
		}

		/// <summary>
		/// Lee el recuento con el OTRO metodo publico que ofrece el juego
		/// (<c>CreativeUI.GetSacrificeCount</c>), como segunda opinion independiente del que usa
		/// el panel (<c>TryGetSacrificeNumbers</c>).
		/// </summary>
		private static string LeerConCreativeUI(int tipo)
		{
			bool completo;
			int cuantas = Terraria.GameContent.Creative.CreativeUI.GetSacrificeCount(tipo, out completo);
			return $"{cuantas} sacrificados, completo={completo}";
		}

		/// <summary>
		/// El primer objeto investigable que aporte un MOD (tipo por encima de
		/// <c>ItemID.Count</c>), sin contar los que ya estuvieran investigados. 0 si no hay
		/// ninguno, que es lo normal en una partida sin mods de contenido.
		/// </summary>
		private static int PrimerTipoDeMod()
		{
			int mejor = 0;
			foreach (int tipo in EstadoInvestigacion.TiposInvestigables) {
				int canonico = EstadoInvestigacion.TipoCanonico(tipo);
				if (canonico < ItemID.Count || EstadoInvestigacion.Completo(canonico)) {
					continue;
				}
				if (mejor == 0 || canonico < mejor) {
					mejor = canonico;
				}
			}
			return mejor;
		}

		private static CarpetaInvestigacion BuscarCarpetaDe(int tipo)
		{
			foreach (CarpetaInvestigacion raiz in CatalogoInvestigacion.Raices) {
				CarpetaInvestigacion hoja = BuscarHojaCon(raiz, tipo);
				if (hoja != null) {
					return hoja;
				}
			}
			return null;
		}

		private static CarpetaInvestigacion BuscarHojaCon(CarpetaInvestigacion carpeta, int tipo)
		{
			if (Array.IndexOf(carpeta.Tipos, tipo) < 0) {
				return null;
			}
			for (int i = 0; i < carpeta.Hijos.Count; i++) {
				CarpetaInvestigacion hoja = BuscarHojaCon(carpeta.Hijos[i], tipo);
				if (hoja != null) {
					return hoja;
				}
			}
			return carpeta.EsHoja ? carpeta : null;
		}

		/// <summary>
		/// Hoja pequeña pero no ridicula para probar "investigar carpeta entera": la que menos
		/// objetos tenga de entre las que tengan al menos <paramref name="minimo"/>. Con una
		/// carpeta de un solo objeto la prueba no demostraria gran cosa, y con una de miles se
		/// investigaria media partida de golpe.
		/// </summary>
		private static CarpetaInvestigacion CarpetaHojaPequenia(int minimo)
		{
			CarpetaInvestigacion mejor = null;
			foreach (CarpetaInvestigacion raiz in CatalogoInvestigacion.Raices) {
				mejor = Menor(raiz, mejor, minimo);
			}
			// Si en esta partida no hay ninguna hoja tan grande, vale cualquiera.
			return mejor ?? CarpetaHojaPequeniaSinMinimo();
		}

		private static CarpetaInvestigacion CarpetaHojaPequeniaSinMinimo()
		{
			CarpetaInvestigacion mejor = null;
			foreach (CarpetaInvestigacion raiz in CatalogoInvestigacion.Raices) {
				mejor = Menor(raiz, mejor, 1);
			}
			return mejor;
		}

		private static CarpetaInvestigacion Menor(CarpetaInvestigacion carpeta, CarpetaInvestigacion mejor, int minimo)
		{
			if (carpeta.EsHoja && carpeta.Total >= minimo && (mejor == null || carpeta.Total < mejor.Total)) {
				mejor = carpeta;
			}
			for (int i = 0; i < carpeta.Hijos.Count; i++) {
				mejor = Menor(carpeta.Hijos[i], mejor, minimo);
			}
			return mejor;
		}

		private static void Registrar(string mensaje)
		{
			RegistroInvestigacion.Linea($"{Terrakeep.LogTag} {mensaje}");
		}
	}
}
