using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.ID;
using TerrakeepMod.Common.Undo;

namespace TerrakeepMod.Common.Investigacion
{
	/// <summary>Recuento de lo que ha hecho una accion de investigar o de quitar investigacion.</summary>
	public sealed class ResultadoInvestigacion
	{
		/// <summary>Objetos que han pasado a estar investigados del todo con esta accion.</summary>
		public int Completados;

		/// <summary>Objetos que ya estaban como se pedia (ya investigados / ya a cero).</summary>
		public int SinCambio;

		/// <summary>Objetos que el juego no deja investigar nunca (no estan en la tabla real de
		/// sacrificios, o un mod los bloquea con <c>CanResearch</c>).</summary>
		public int NoInvestigables;

		/// <summary>Tipos que si cambiaron. Es lo que se usa para el rotulo del historial.</summary>
		public readonly List<int> Cambiados = new List<int>();

		public bool HuboCambios => Cambiados.Count > 0;

		public string Resumen =>
			$"cambiados={Cambiados.Count}, sin cambio={SinCambio}, no investigables={NoInvestigables}";
	}

	/// <summary>
	/// Foto del estado de investigacion de un conjunto de tipos de objeto. Es lo que hace
	/// deshacible cualquier accion de este panel, con el mismo modelo de snapshot de WS7
	/// (<see cref="Historial.CambiarValor{T}"/>): se guarda el recuento de antes y el de despues,
	/// nunca una closure del tipo "sumale 25", que sobre un estado que ya cambio corromperia los
	/// datos en silencio.
	/// </summary>
	public sealed class SnapshotInvestigacion
	{
		private readonly Dictionary<int, int> _recuentos;

		private SnapshotInvestigacion(Dictionary<int, int> recuentos)
		{
			_recuentos = recuentos;
		}

		public int Cuenta => _recuentos.Count;

		/// <summary>Toma la foto de los tipos indicados (ya canonizados por quien llama).</summary>
		public static SnapshotInvestigacion Tomar(IEnumerable<int> tipos)
		{
			Dictionary<int, int> recuentos = new Dictionary<int, int>();
			foreach (int tipo in tipos) {
				int canonico = EstadoInvestigacion.TipoCanonico(tipo);
				if (!recuentos.ContainsKey(canonico)) {
					recuentos[canonico] = EstadoInvestigacion.Hecho(canonico);
				}
			}
			return new SnapshotInvestigacion(recuentos);
		}

		/// <summary>Vuelca la foto sobre el estado real del juego, por la via oficial.</summary>
		public void Aplicar()
		{
			foreach (KeyValuePair<int, int> par in _recuentos) {
				EstadoInvestigacion.FijarRecuento(par.Key, par.Value);
			}
		}
	}

	/// <summary>
	/// Todo el trato con el estado de investigacion REAL del juego (Modo Viaje) pasa por aqui.
	/// <para />
	/// <b>Nada de escribir un diccionario a pelo</b>, que era el riesgo que ya señalaba el plan:
	/// se usa la API publica del propio Terraria/tModLoader, de forma que el menu de sacrificio
	/// del Modo Viaje y la lista de "objetos disponibles infinitamente" del juego quedan
	/// coherentes con lo que haga este panel, sin desincronizarse.
	/// <para />
	/// <b>API real usada</b>, comprobada con <c>ilspycmd</c> sobre el <c>tModLoader.dll</c>
	/// INSTALADO (v2026.7.3.0), no sobre la referencia decompilada vieja del repo hermano
	/// (v1.4.4.9), porque ya se sabe de WS0/WS1 que hay diferencias reales entre las dos:
	/// <list type="bullet">
	/// <item><c>CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId</c>
	/// (<c>Dictionary&lt;int,int&gt;</c> publico): la tabla REAL de "cuantos hacen falta" de cada
	/// objeto. La rellena <c>Initialize()</c> desde el recurso incrustado
	/// <c>Terraria.GameContent.Creative.Content.Sacrifices.tsv</c>, y los mods se añaden solos a
	/// ella via <c>Item.ResearchUnlockCount</c> (su setter escribe literalmente en este
	/// diccionario; <c>ModItem.AutoStaticDefaults</c> pone 1 por defecto a todo objeto de mod).
	/// Es, por tanto, la lista autoritativa de TODO lo investigable en esta partida, mods
	/// incluidos.</item>
	/// <item><c>CreativeUI.ResearchItem(int type)</c>: investiga un objeto del todo. Fabrica un
	/// <c>new Item(type, amountNeeded)</c> y lo pasa por <c>SacrificeItem</c>, que es la MISMA
	/// ruta que usa el jugador soltando objetos en la ranura de sacrificio del juego: respeta
	/// <c>ItemLoader.CanResearch</c>, avisa a los mods con <c>ItemLoader.OnResearched</c>, manda
	/// el paquete de red si el personaje es de servidor (<c>Main.ServerSideCharacter</c>) y
	/// refresca la lista de objetos infinitos del propio menu del juego.</item>
	/// <item><c>Main.LocalPlayerCreativeTracker.ItemSacrifices</c>
	/// (<c>ItemsSacrificedUnlocksTracker</c>, que cuelga de <c>Player.creativeTracker</c> y se
	/// guarda dentro del <c>.plr</c>): <c>TryGetSacrificeNumbers</c> para leer x/N,
	/// <c>SetSacrificeCountDirectly</c> para quitar investigacion, <c>Reset()</c> para quitarla
	/// toda, y <c>LastEditId</c> para enterarse de que algo cambio sin tener que recontar cada
	/// fotograma.</item>
	/// </list>
	/// <para />
	/// <b>Detalle real que importa</b>: hay objetos que comparten investigacion con otro
	/// (<c>ContentSamples.CreativeResearchItemPersistentIdOverride</c>). Casi toda la API oficial
	/// aplica ese "override" por dentro, pero <c>CreativeUI.GetSacrificeCount</c> NO lo hace
	/// (lee la cache directamente con el tipo que se le pasa), asi que aqui se lee siempre con
	/// <c>TryGetSacrificeNumbers</c>, que si lo aplica, y se canoniza el tipo antes de contar
	/// para no contar dos veces el mismo progreso.
	/// </summary>
	public static class EstadoInvestigacion
	{
		/// <summary>Dificultad de personaje que corresponde al Modo Viaje
		/// (<c>PlayerDifficultyID.Creative</c> = 3). Es exactamente la condicion que usa el propio
		/// juego para enseñar la interfaz creativa: <c>CreativeUI.Draw</c> hace
		/// <c>if (Main.LocalPlayer.difficulty != 3) Enabled = false;</c>.</summary>
		public const byte DificultadModoViaje = PlayerDifficultyID.Creative;

		/// <summary>true si el personaje cargado es de Modo Viaje. Fuera de el, la investigacion
		/// se puede escribir igual (el dato vive en el <c>.plr</c>) pero no sirve para nada: el
		/// juego ni siquiera dibuja el menu creativo. El panel lo avisa en grande.</summary>
		public static bool ModoViaje =>
			Main.LocalPlayer != null && Main.LocalPlayer.difficulty == DificultadModoViaje;

		/// <summary>true si el mundo cargado es de Modo Viaje (<c>Main.GameMode == 3</c>). El
		/// juego solo deja meter un personaje de Viaje en un mundo de Viaje, asi que en la
		/// practica va de la mano de <see cref="ModoViaje"/>; se enseña por separado porque es
		/// informacion util cuando algo no cuadra.</summary>
		public static bool MundoModoViaje => Main.GameMode == 3;

		/// <summary>
		/// El tracker real del jugador local. Vive en <c>Player.creativeTracker</c> y se
		/// serializa dentro del <c>.plr</c> (<c>Player.SavePlayer</c> -&gt;
		/// <c>creativeTracker.Save</c>), o sea que lo que se escriba aqui persiste de verdad.
		/// </summary>
		public static ItemsSacrificedUnlocksTracker Tracker =>
			Main.LocalPlayer == null ? null : Main.LocalPlayerCreativeTracker.ItemSacrifices;

		/// <summary>Contador de ediciones del tracker. Sube con cada cambio real
		/// (<c>MarkContentsDirty</c>), asi que sirve para recalcular los contadores del panel solo
		/// cuando hace falta en vez de en cada fotograma.</summary>
		public static int VersionDeEstado => Tracker == null ? 0 : Tracker.LastEditId;

		/// <summary>
		/// Tipo con el que el juego contabiliza de verdad la investigacion de este objeto. Hay
		/// objetos que la comparten con otro (variantes, objetos equivalentes); el diccionario
		/// oficial que lo dice es <c>ContentSamples.CreativeResearchItemPersistentIdOverride</c>.
		/// </summary>
		public static int TipoCanonico(int tipo)
		{
			int otro;
			return ContentSamples.CreativeResearchItemPersistentIdOverride.TryGetValue(tipo, out otro) ? otro : tipo;
		}

		/// <summary>Cuantas unidades hacen falta para investigar del todo este objeto, o 0 si el
		/// juego no lo considera investigable.</summary>
		public static int Necesario(int tipo)
		{
			int cuantos;
			return CreativeItemSacrificesCatalog.Instance.TryGetSacrificeCountCapToUnlockInfiniteItems(tipo, out cuantos)
				? cuantos
				: 0;
		}

		/// <summary>true si el objeto se puede investigar en esta partida.</summary>
		public static bool EsInvestigable(int tipo)
		{
			return Necesario(tipo) > 0;
		}

		/// <summary>Cuantas unidades lleva sacrificadas el jugador de este objeto.</summary>
		public static int Hecho(int tipo)
		{
			ItemsSacrificedUnlocksTracker tracker = Tracker;
			if (tracker == null) {
				return 0;
			}

			int llevamos;
			int hacenFalta;
			// TryGetSacrificeNumbers aplica el override de tipo compartido por dentro; leerlo con
			// CreativeUI.GetSacrificeCount NO lo aplicaria (mira la cache con el tipo tal cual).
			return tracker.TryGetSacrificeNumbers(tipo, out llevamos, out hacenFalta) ? llevamos : 0;
		}

		/// <summary>true si el objeto esta investigado del todo.</summary>
		public static bool Completo(int tipo)
		{
			ItemsSacrificedUnlocksTracker tracker = Tracker;
			if (tracker == null) {
				return false;
			}

			int llevamos;
			int hacenFalta;
			if (!tracker.TryGetSacrificeNumbers(tipo, out llevamos, out hacenFalta)) {
				return false;
			}
			return hacenFalta > 0 && llevamos >= hacenFalta;
		}

		/// <summary>Todos los tipos que el juego considera investigables ahora mismo, mods
		/// incluidos. Es la tabla real, no una lista propia.</summary>
		public static IEnumerable<int> TiposInvestigables =>
			CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.Keys;

		/// <summary>Cuantos objetos distintos se pueden investigar en esta partida (el
		/// denominador de la barra de progreso global).</summary>
		public static int TotalInvestigable
		{
			get
			{
				HashSet<int> canonicos = new HashSet<int>();
				foreach (int tipo in TiposInvestigables) {
					canonicos.Add(TipoCanonico(tipo));
				}
				return canonicos.Count;
			}
		}

		/// <summary>Cuantos de ellos estan ya investigados del todo.</summary>
		public static int TotalCompletos
		{
			get
			{
				HashSet<int> vistos = new HashSet<int>();
				int completos = 0;
				foreach (int tipo in TiposInvestigables) {
					int canonico = TipoCanonico(tipo);
					if (!vistos.Add(canonico)) {
						continue;
					}
					if (Completo(canonico)) {
						completos++;
					}
				}
				return completos;
			}
		}

		// ------------------------------------------------------------------ escritura

		/// <summary>
		/// Investiga del todo los tipos indicados, por la via oficial
		/// <c>CreativeUI.ResearchItem</c>, y deja la accion deshacible con Ctrl+Z.
		/// <para />
		/// Nunca baja nada: un objeto que ya estuviera investigado se cuenta como "sin cambio".
		/// </summary>
		public static ResultadoInvestigacion Investigar(string etiqueta, IEnumerable<int> tipos)
		{
			return Aplicar(etiqueta, tipos, true);
		}

		/// <summary>
		/// Quita la investigacion de los tipos indicados (los deja a cero), tambien deshacible.
		/// <para />
		/// Es la unica accion del panel que baja el progreso, y esta separada a proposito: el
		/// criterio del proyecto es que investigar solo sube, y que quitar hay que pedirlo.
		/// </summary>
		public static ResultadoInvestigacion Quitar(string etiqueta, IEnumerable<int> tipos)
		{
			return Aplicar(etiqueta, tipos, false);
		}

		private static ResultadoInvestigacion Aplicar(string etiqueta, IEnumerable<int> tipos, bool investigar)
		{
			ResultadoInvestigacion resultado = new ResultadoInvestigacion();
			if (Tracker == null) {
				return resultado;
			}

			// Se canoniza y se quitan repetidos ANTES de tocar nada: dos variantes que comparten
			// investigacion son un solo cambio real, y contarlas dos veces daria un resumen falso.
			List<int> objetivo = new List<int>();
			HashSet<int> vistos = new HashSet<int>();
			foreach (int tipo in tipos) {
				int canonico = TipoCanonico(tipo);
				if (vistos.Add(canonico)) {
					objetivo.Add(canonico);
				}
			}

			SnapshotInvestigacion antes = SnapshotInvestigacion.Tomar(objetivo);

			foreach (int tipo in objetivo) {
				int hacenFalta = Necesario(tipo);
				if (hacenFalta <= 0) {
					resultado.NoInvestigables++;
					continue;
				}

				int llevaba = Hecho(tipo);
				if (investigar) {
					if (llevaba >= hacenFalta) {
						resultado.SinCambio++;
						continue;
					}

					// La via oficial. Devuelve CannotSacrifice si un mod lo bloquea con
					// ItemLoader.CanResearch, y en ese caso no se ha tocado nada.
					CreativeUI.ItemSacrificeResult efecto = CreativeUI.ResearchItem(tipo);
					if (efecto == CreativeUI.ItemSacrificeResult.CannotSacrifice) {
						resultado.NoInvestigables++;
						continue;
					}
				}
				else {
					if (llevaba <= 0) {
						resultado.SinCambio++;
						continue;
					}
					FijarRecuento(tipo, 0);
				}

				resultado.Cambiados.Add(tipo);
				if (investigar && Completo(tipo)) {
					resultado.Completados++;
				}
			}

			if (resultado.HuboCambios) {
				SnapshotInvestigacion despues = SnapshotInvestigacion.Tomar(objetivo);
				Historial.CambiarValor(etiqueta, antes, despues, foto => foto.Aplicar());
			}

			return resultado;
		}

		/// <summary>
		/// Quita TODA la investigacion del personaje de una vez, con la llamada oficial
		/// <c>ItemsSacrificedUnlocksTracker.Reset()</c> (la misma que usa
		/// <c>CreativeUnlocksTracker.Reset</c> del propio juego). Deshacible: la foto de antes
		/// cubre todo lo que hubiera investigado.
		/// </summary>
		public static ResultadoInvestigacion QuitarTodo(string etiqueta)
		{
			ResultadoInvestigacion resultado = new ResultadoInvestigacion();
			ItemsSacrificedUnlocksTracker tracker = Tracker;
			if (tracker == null) {
				return resultado;
			}

			List<int> conProgreso = new List<int>();
			HashSet<int> vistos = new HashSet<int>();
			foreach (int tipo in TiposInvestigables) {
				int canonico = TipoCanonico(tipo);
				if (vistos.Add(canonico) && Hecho(canonico) > 0) {
					conProgreso.Add(canonico);
				}
			}

			if (conProgreso.Count == 0) {
				return resultado;
			}

			SnapshotInvestigacion antes = SnapshotInvestigacion.Tomar(conProgreso);
			tracker.Reset();

			resultado.Cambiados.AddRange(conProgreso);
			SnapshotInvestigacion despues = SnapshotInvestigacion.Tomar(conProgreso);
			Historial.CambiarValor(etiqueta, antes, despues, foto => foto.Aplicar());
			return resultado;
		}

		/// <summary>
		/// Fija el recuento de un objeto a un valor concreto por la via oficial
		/// <c>ItemsSacrificedUnlocksTracker.SetSacrificeCountDirectly</c>, que es publica y es la
		/// que usa el propio juego para cargar el estado desde el archivo del personaje.
		/// <para />
		/// Trabaja con el <b>id persistente</b> (<c>"MoltenHelmet"</c> para vanilla,
		/// <c>"CalamityMod/SulphurousHelmet"</c> para un mod), que es lo que se guarda en el
		/// <c>.plr</c>; el tipo numerico cambia entre partidas segun que mods haya cargados. La
		/// traduccion la da <c>ContentSamples.ItemPersistentIdsByNetIds</c>.
		/// </summary>
		public static void FijarRecuento(int tipo, int recuento)
		{
			ItemsSacrificedUnlocksTracker tracker = Tracker;
			if (tracker == null) {
				return;
			}

			int canonico = TipoCanonico(tipo);
			string idPersistente;
			if (!ContentSamples.ItemPersistentIdsByNetIds.TryGetValue(canonico, out idPersistente)) {
				return;
			}
			tracker.SetSacrificeCountDirectly(idPersistente, recuento);
		}

		/// <summary>Id persistente de un tipo (el que se guarda en el <c>.plr</c>), o null.</summary>
		public static string IdPersistente(int tipo)
		{
			string id;
			return ContentSamples.ItemPersistentIdsByNetIds.TryGetValue(TipoCanonico(tipo), out id) ? id : null;
		}

		/// <summary>
		/// Los objetos que el JUEGO considera ya disponibles de forma infinita, preguntandoselo a
		/// el con <c>FillListOfItemsThatCanBeObtainedInfinitely</c>. Es exactamente la lista que
		/// alimenta el menu de duplicacion del Modo Viaje, asi que sirve como comprobacion
		/// independiente de que lo que escribe este panel lo ve el juego de verdad.
		/// </summary>
		public static List<int> ObjetosInfinitosSegunElJuego()
		{
			List<int> lista = new List<int>();
			Tracker?.FillListOfItemsThatCanBeObtainedInfinitely(lista);
			return lista;
		}

		/// <summary>Nombre legible de un objeto, con el idioma del juego.</summary>
		public static string NombreObjeto(int tipo)
		{
			Item muestra;
			if (ContentSamples.ItemsByType.TryGetValue(tipo, out muestra) && muestra != null) {
				return muestra.Name;
			}
			return "#" + tipo;
		}

		/// <summary>Linea de estado de un objeto para el log de las pruebas.</summary>
		public static string Describir(int tipo)
		{
			int canonico = TipoCanonico(tipo);
			StringBuilder sb = new StringBuilder();
			sb.Append('"').Append(NombreObjeto(canonico)).Append("\" (type=").Append(canonico);
			string pid = IdPersistente(canonico);
			if (pid != null) {
				sb.Append(", pid=").Append(pid);
			}
			sb.Append(") ").Append(Hecho(canonico)).Append('/').Append(Necesario(canonico));
			sb.Append(Completo(canonico) ? " INVESTIGADO" : " sin terminar");
			return sb.ToString();
		}
	}
}
