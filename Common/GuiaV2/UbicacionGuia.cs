using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Panel;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// DONDE esta la siguiente parada de la guia en el mundo CARGADO: lo que pinta la marca
	/// permanente del mapa (<see cref="CapaMarcaGuia"/>) y lo que enseña "Mi guía"/"Ruta".
	/// </summary>
	/// <remarks>
	/// <para>
	/// Punto 3 del encargo del usuario (02-oct-2026), literal: "que te marcara en el mapa qué hacer
	/// cada vez con una marca permanente en el mapa que apunte al siguiente lugar". Esto sustituye
	/// para la Guia v2 al criterio "brujula, nunca GPS" de la v1 (que sigue igual en la pestaña
	/// Brújula): lo ha pedido el propio usuario.
	/// </para>
	/// <para>
	/// Orden de preferencia, de mas a menos preciso:
	/// <list type="number">
	/// <item>Un jefe de la parada VIVO ahora mismo (estas en el combate): su posicion real.</item>
	/// <item>Las <c>ubicaciones</c> de la parada en su orden: un NPC del pueblo vivo, un punto
	/// guardado del mundo (spawn, mazmorra, centro de laboratorio de Calamity, altar mas cercano) o
	/// una zona situada por su firma de tiles con <c>GuiaV2Ubicaciones</c> de Core, el MISMO codigo
	/// que usa el escritorio sobre el .wld.</item>
	/// </list>
	/// Si el resultado es una estimacion (zona sin firma = solo la capa; oceanos), se marca como
	/// APROXIMADA y asi se dice en el tooltip y en la UI. Si la zona no existe en este mundo (p. ej.
	/// un mundo generado sin Calamity), no se pinta nada y se dice.
	/// </para>
	/// <para>
	/// Las zonas se barren UNA vez por mundo y ubicacion (cache): el barrido entero de un mundo
	/// grande cuesta decenas de milisegundos y no se repite al moverse el jugador.
	/// </para>
	/// </remarks>
	public static class UbicacionGuia
	{
		public sealed class Objetivo
		{
			public Vector2 Tile;
			public bool Aproximada;
			/// <summary>"jefe", "npc", "punto:...", "firma", "capa:..."</summary>
			public string Origen = "";
			public string Titulo = "";
			public string Lugar = "";
			public string ParadaId = "";
			/// <summary>Tipo de NPC para el icono (jefe o vecino), 0 si no hay.</summary>
			public int Npc;
			/// <summary>Radio aproximado de la zona en casillas (para el anillo del mapa).</summary>
			public float RadioTiles;
		}

		/// <summary>Marca de la siguiente parada, o null.</summary>
		public static Objetivo Siguiente { get; private set; }

		/// <summary>Marca temporal de una zona consultada desde la guia ("Ver en el mapa").</summary>
		public static Objetivo Consultada { get; private set; }

		/// <summary>Estado legible para la UI: "exacta", "aproximada", "no_existe", "sin_ubicacion",
		/// "ruta_terminada", "oculta".</summary>
		public static string Estado { get; private set; } = "sin_ubicacion";

		private static readonly Dictionary<string, UbicacionResuelta> _cache = new Dictionary<string, UbicacionResuelta>();
		private static readonly HashSet<string> _cacheNula = new HashSet<string>();
		private static MundoGuiaVivo _mundo;
		private static int _fotogramas;
		private static bool _invalidado = true;
		private static bool _volverAlPanel;

		/// <summary>Cuantos barridos de zona se han hecho (evidencia para la autoprueba).</summary>
		public static int Barridos;

		public static bool Visible => AjustesConfig.Instance == null || AjustesConfig.Instance.MarcaGuiaEnMapa;

		public static void Invalidar()
		{
			_invalidado = true;
		}

		public static void Limpiar()
		{
			_cache.Clear();
			_cacheNula.Clear();
			_mundo = null;
			Siguiente = null;
			Consultada = null;
			Estado = "sin_ubicacion";
			_invalidado = true;
		}

		public static void Actualizar()
		{
			ComprobarVueltaDelMapa();
			if (!GuiaV2Sistema.HayGuia || GuiaV2Sistema.Resumen == null || !EstadoJugadorGuia.HayPartida) {
				return;
			}
			_fotogramas++;
			if (!_invalidado && _fotogramas < 20) {
				return;
			}
			_fotogramas = 0;
			_invalidado = false;
			Recalcular();
		}

		/// <summary>Recalcula la marca ya. Publico para la autoprueba.</summary>
		public static void Recalcular()
		{
			ResultadoParada sig = GuiaV2Sistema.Resumen != null ? GuiaV2Sistema.Resumen.Siguiente : null;
			if (sig == null) {
				Siguiente = null;
				Estado = GuiaV2Sistema.Resumen != null ? "ruta_terminada" : "sin_ubicacion";
				return;
			}
			Objetivo o = ResolverParada(sig.Parada);
			Siguiente = o;
			Estado = o == null
				? (sig.Parada.Ubicaciones.Count == 0 ? "sin_ubicacion" : "no_existe")
				: (o.Aproximada ? "aproximada" : "exacta");
		}

		/// <summary>Situa cualquier parada en el mundo cargado (null si no se puede).</summary>
		public static Objetivo ResolverParada(Parada parada)
		{
			if (parada == null) {
				return null;
			}
			string titulo = GuiaV2Sistema.PlanoLocal(parada.Titulo);

			// 1) Un jefe de la parada vivo ahora mismo.
			foreach (string j in parada.Jefes) {
				int tipo = GuiaV2Sistema.TipoNpc(j);
				NPC npc = NpcVivo(tipo, false);
				if (npc != null) {
					return new Objetivo {
						Tile = npc.Center / 16f, Aproximada = false, Origen = "jefe", Titulo = titulo, ParadaId = parada.Id,
						Lugar = GuiaV2Sistema.NombreNpc(j), Npc = npc.type, RadioTiles = 0f,
					};
				}
			}

			// 2) Las ubicaciones, en su orden.
			bool? carmesi = WorldGen.crimson;
			foreach (Ubicacion u in parada.Ubicaciones) {
				if (u.SiMundo == "carmesi" && carmesi == false) continue;
				if (u.SiMundo == "corrupcion" && carmesi == true) continue;

				if (u.Tipo == "npc" || u.Tipo == "jefe") {
					int tipo = GuiaV2Sistema.TipoNpc(u.Id);
					NPC npc = NpcVivo(tipo, u.Tipo == "npc");
					if (npc != null) {
						return new Objetivo {
							Tile = npc.Center / 16f, Aproximada = false, Origen = u.Tipo, Titulo = titulo, ParadaId = parada.Id,
							Lugar = GuiaV2Sistema.NombreNpc(u.Id), Npc = npc.type,
						};
					}
					continue;
				}

				UbicacionResuelta r = ResolverUbicacion(u);
				if (r == null) {
					continue;
				}
				string lugar = u.Tipo == "zona" ? GuiaV2Sistema.NombreZona(u.Id) : Idiomas.Texto("GuiaV2.Punto." + u.Id);
				int npcIcono = 0;
				if (parada.Jefes.Count > 0) {
					int t = GuiaV2Sistema.TipoNpc(parada.Jefes[0]);
					npcIcono = t > 0 ? t : 0;
				}
				return new Objetivo {
					Tile = new Vector2(r.X, r.Y), Aproximada = r.Aproximada, Origen = r.Origen, Titulo = titulo,
					ParadaId = parada.Id, Lugar = lugar, Npc = npcIcono,
					RadioTiles = r.Aproximada ? 90f : (r.Origen == "firma" ? 32f : 0f),
				};
			}
			return null;
		}

		/// <summary>Situa una zona de la guia (para "Ver en el mapa" de la ficha de zona).</summary>
		public static Objetivo ResolverZona(string zonaId)
		{
			Zona z = GuiaV2Sistema.ZonaPorId(zonaId);
			if (z == null || !(EstadoJugadorGuia.HayPartida || (Main.dedServ && Main.maxTilesX > 0))) {
				return null;
			}
			UbicacionResuelta r = ResolverUbicacion(new Ubicacion { Tipo = "zona", Id = zonaId });
			if (r == null) {
				return null;
			}
			return new Objetivo {
				Tile = new Vector2(r.X, r.Y), Aproximada = r.Aproximada, Origen = r.Origen, Titulo = z.Nombre, Lugar = z.Nombre,
				RadioTiles = r.Aproximada ? 90f : (r.Origen == "firma" ? 32f : 0f),
			};
		}

		private static UbicacionResuelta ResolverUbicacion(Ubicacion u)
		{
			string clave = u.Tipo + ":" + u.Id;
			UbicacionResuelta r;
			if (_cache.TryGetValue(clave, out r)) {
				return r;
			}
			if (_cacheNula.Contains(clave)) {
				return null;
			}
			if (_mundo == null) {
				_mundo = new MundoGuiaVivo();
			}
			Parada temporal = new Parada { Id = "_", Ubicaciones = new List<Ubicacion> { new Ubicacion { Tipo = u.Tipo, Id = u.Id } } };
			System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
			try {
				r = GuiaV2Ubicaciones.ResolverParada(temporal, GuiaV2Sistema.Doc, _mundo, WorldGen.crimson);
			}
			catch (Exception e) {
				RegistroGuia.Error(Terrakeep.LogTag + " Guia v2: EXCEPCION situando " + clave + ": " + e);
				r = null;
			}
			Barridos++;
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: ubicacion " + clave + " -> " +
				(r != null ? "(" + r.X + ", " + r.Y + ") origen=" + r.Origen + " aproximada=" + r.Aproximada + " casillas=" + r.Casillas : "NO EXISTE en este mundo") +
				" [" + reloj.ElapsedMilliseconds + " ms]");
			// "altar" depende de donde este el jugador: no se cachea.
			if (u.Id != "altar") {
				if (r != null) _cache[clave] = r; else _cacheNula.Add(clave);
			}
			return r;
		}

		private static NPC NpcVivo(int tipo, bool soloPueblo)
		{
			if (tipo == 0) {
				return null;
			}
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC n = Main.npc[i];
				if (n != null && n.active && (n.type == tipo || n.netID == tipo) && (!soloPueblo || n.townNPC)) {
					return n;
				}
			}
			return null;
		}

		/// <summary>Direccion legible desde el jugador hasta el objetivo ("a 230 casillas al este y
		/// 40 hacia abajo").</summary>
		public static string DireccionDesdeJugador(Objetivo o)
		{
			Player p = Main.LocalPlayer;
			if (o == null || p == null || !p.active) {
				return "";
			}
			Vector2 jug = p.Center / 16f;
			int dx = (int)(o.Tile.X - jug.X), dy = (int)(o.Tile.Y - jug.Y);
			if (Math.Abs(dx) < 25 && Math.Abs(dy) < 18) {
				return Idiomas.Texto("GuiaV2.Mapa.EstasAhi");
			}
			string h = Math.Abs(dx) < 25 ? "" : Idiomas.Texto(dx > 0 ? "GuiaV2.Mapa.Este" : "GuiaV2.Mapa.Oeste", Math.Abs(dx));
			string v = Math.Abs(dy) < 18 ? "" : Idiomas.Texto(dy > 0 ? "GuiaV2.Mapa.Abajo" : "GuiaV2.Mapa.Arriba", Math.Abs(dy));
			if (h.Length > 0 && v.Length > 0) {
				return Idiomas.Texto("GuiaV2.Mapa.DireccionDoble", h, v);
			}
			return Idiomas.Texto("GuiaV2.Mapa.DireccionSimple", h.Length > 0 ? h : v);
		}

		// ---- abrir el mapa del juego -------------------------------------------------------------

		public static void VerEnElMapa(Objetivo o, bool comoConsulta, string origen)
		{
			if (o == null || Main.gameMenu) {
				return;
			}
			if (comoConsulta) {
				Consultada = o;
			}
			PanelTerrakeepSystem.CerrarPanel("Guía v2: ver en el mapa (" + origen + ")");
			Main.playerInventory = false;
			Main.mapFullscreenScale = Math.Max(Main.mapFullscreenScale, 1f);
			Main.mapFullscreenPos = o.Tile;
			Main.resetMapFull = false;
			Main.mapFullscreen = true;
			SoundEngine.PlaySound(SoundID.MenuOpen);
			_volverAlPanel = true;
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: VER EN EL MAPA via " + origen + " -> (" + (int)o.Tile.X + ", " +
				(int)o.Tile.Y + "), aproximada=" + o.Aproximada + ", consulta=" + comoConsulta + ".");
		}

		private static void ComprobarVueltaDelMapa()
		{
			if (!_volverAlPanel || Main.mapFullscreen) {
				return;
			}
			_volverAlPanel = false;
			Consultada = null;
			if (!Main.gameMenu) {
				PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Guia, "vuelta del mapa (Guía v2)");
			}
		}
	}
}
