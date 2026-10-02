using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terrakeep.Core.Guia;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Panel;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Cerebro de la Guia v2 DENTRO del juego: carga el contenido compartido (incrustado en
	/// <c>lib\Terrakeep.Core.dll</c>, el mismo que lee Terrakeep de escritorio), lo evalua contra la
	/// partida en vivo cada medio segundo y calcula donde esta la siguiente parada en el mundo
	/// cargado (para la marca permanente del mapa, <see cref="CapaMarcaGuia"/>).
	/// </summary>
	/// <remarks>
	/// <b>Vanilla o Calamity</b>: la guia se elige sola segun haya Calamity cargado. Si la guia que
	/// toca todavia no esta incrustada en la DLL (vanilla la genera la fase F1 en paralelo), se dice
	/// tal cual en la UI (<see cref="MotivoSinGuia"/>), nunca se enseña la otra en su lugar: con
	/// Calamity descargado, la guia de Calamity citaria jefes y objetos que no existen en la partida.
	/// </remarks>
	public class GuiaV2Sistema : ModSystem
	{
		public static GuiaV2Referencias Refs { get; private set; }
		public static GuiaV2Doc Doc { get; private set; }
		public static GuiaV2Evaluador Evaluador { get; private set; }
		public static string IdGuia { get; private set; }
		public static string MotivoSinGuia { get; private set; } = "";

		/// <summary>Ultima evaluacion completa (null fuera de partida o sin guia).</summary>
		public static ResumenGuiaV2 Resumen { get; private set; }

		/// <summary>Sube cada vez que cambia algo visible de la evaluacion (paradas hechas, tareas
		/// hechas, siguiente parada). La UI lo usa para saber cuando reconstruirse.</summary>
		public static int Version { get; private set; }

		public static readonly ProveedorEstadoGuiaV2Mod Proveedor = new ProveedorEstadoGuiaV2Mod();

		private static ResolutorRefsGuia _resolutor;
		private static readonly Dictionary<string, int> _tiposObjeto = new Dictionary<string, int>();
		private static readonly Dictionary<string, int> _tiposNpc = new Dictionary<string, int>();
		private static int _fotogramas;
		private static string _firmaUltima = "";
		private static int _cambiosVistos = -1;

		public static bool HayGuia => Doc != null && Evaluador != null;

		public override void PostSetupContent()
		{
			Cargar();
		}

		public override void Unload()
		{
			Refs = null;
			Doc = null;
			Evaluador = null;
			Resumen = null;
			_resolutor = null;
			_tiposObjeto.Clear();
			_tiposNpc.Clear();
			ReflexionCalamity.Descargar();
			UbicacionGuia.Limpiar();
			TexturasMarca.Descargar();
		}

		public static void Cargar()
		{
			try {
				Refs = GuiaV2Cargador.CargarReferenciasIncrustadas();
				IReadOnlyList<string> disponibles = GuiaV2Cargador.GuiasDisponibles();
				string quiero = CatalogoGuia.HayCalamity ? "calamity" : "vanilla";
				if (disponibles.Contains(quiero)) {
					Doc = GuiaV2Cargador.CargarGuiaIncrustada(quiero);
					IdGuia = quiero;
					_resolutor = new ResolutorRefsGuia(Refs, ResolverObjetoMod, ResolverNpcMod);
					Evaluador = new GuiaV2Evaluador(Doc, _resolutor);
					MotivoSinGuia = "";
				}
				else {
					Doc = null;
					Evaluador = null;
					IdGuia = quiero;
					MotivoSinGuia = quiero;
				}
				RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: guias incrustadas en Terrakeep.Core = [" +
					string.Join(", ", disponibles) + "]; Calamity cargado=" + CatalogoGuia.HayCalamity +
					"; guia activa=" + (Doc != null ? IdGuia + " (" + Doc.Paradas.Count + " paradas, " +
					Doc.Paradas.Sum(p => p.Tareas.Count) + " tareas)" : "NINGUNA (falta guia_v2_" + quiero + ".json)") +
					"; referencias=" + Refs.Objetos.Count + " objetos/" + Refs.Npcs.Count + " NPC.");
			}
			catch (Exception e) {
				Doc = null;
				Evaluador = null;
				MotivoSinGuia = "error";
				RegistroGuia.Error(Terrakeep.LogTag + " Guia v2: EXCEPCION al cargar el contenido: " + e);
			}
		}

		// ---- referencias -----------------------------------------------------------------------

		private static int? ResolverObjetoMod(string referencia)
		{
			int t = TipoObjeto(referencia);
			return t > 0 ? t : (int?)null;
		}

		private static int? ResolverNpcMod(string referencia)
		{
			int t = TipoNpc(referencia);
			return t != 0 ? t : (int?)null;
		}

		private static bool PartirRef(string referencia, out string mod, out string nombre)
		{
			mod = nombre = null;
			if (string.IsNullOrEmpty(referencia)) {
				return false;
			}
			int barra = referencia.IndexOf('/');
			if (barra <= 0) {
				return false;
			}
			mod = referencia.Substring(0, barra);
			nombre = referencia.Substring(barra + 1);
			return true;
		}

		/// <summary>Tipo real del objeto en ESTA partida: vanilla por la tabla (ItemID 1.4.4.9) o
		/// por nombre interno; de mod con <c>ModContent.TryFind&lt;ModItem&gt;</c>. 0 = no existe.</summary>
		public static int TipoObjeto(string referencia)
		{
			if (string.IsNullOrEmpty(referencia)) {
				return 0;
			}
			int t;
			if (_tiposObjeto.TryGetValue(referencia, out t)) {
				return t;
			}
			t = 0;
			string mod, nombre;
			if (PartirRef(referencia, out mod, out nombre)) {
				if (mod == "Terraria") {
					RefObjeto o;
					if (Refs != null && Refs.Objetos.TryGetValue(referencia, out o) && o.Id.HasValue) {
						t = o.Id.Value;
					}
					else if (ItemID.Search.TryGetId(nombre, out int id)) {
						t = id;
					}
				}
				else if (ModLoader.HasMod(mod)) {
					ModItem mi;
					if (ModContent.TryFind(mod, nombre, out mi)) {
						t = mi.Type;
					}
				}
			}
			_tiposObjeto[referencia] = t;
			return t;
		}

		/// <summary>Tipo real del NPC (puede ser negativo: variante por netID vanilla).</summary>
		public static int TipoNpc(string referencia)
		{
			if (string.IsNullOrEmpty(referencia)) {
				return 0;
			}
			int t;
			if (_tiposNpc.TryGetValue(referencia, out t)) {
				return t;
			}
			t = 0;
			string mod, nombre;
			if (PartirRef(referencia, out mod, out nombre)) {
				if (mod == "Terraria") {
					RefNpc n;
					if (Refs != null && Refs.Npcs.TryGetValue(referencia, out n) && n.Id.HasValue) {
						t = n.Id.Value;
					}
					else if (NPCID.Search.TryGetId(nombre, out int id)) {
						t = id;
					}
				}
				else if (ModLoader.HasMod(mod)) {
					ModNPC mn;
					if (ModContent.TryFind(mod, nombre, out mn)) {
						t = mn.Type;
					}
				}
			}
			_tiposNpc[referencia] = t;
			return t;
		}

		/// <summary>Ref "Mod/Nombre" de un tipo de objeto vivo (para objetos que no estan en la
		/// tabla: ingredientes de tercer nivel, recetas en vivo).</summary>
		public static string RefDeTipo(int tipo)
		{
			if (tipo <= 0) {
				return null;
			}
			if (tipo < ItemID.Count) {
				return "Terraria/" + ItemID.Search.GetName(tipo);
			}
			ModItem mi = ItemLoader.GetItem(tipo);
			return mi != null ? mi.Mod.Name + "/" + mi.Name : null;
		}

		// ---- nombres -----------------------------------------------------------------------------

		/// <summary>Nombre del objeto: oficial en español de la tabla (Terraria es-ES / CalamityModEsp)
		/// o en ingles segun el idioma; si no esta en la tabla, el nombre vivo del juego.</summary>
		public static string NombreObjeto(string referencia)
		{
			RefObjeto o;
			if (Refs != null && referencia != null && Refs.Objetos.TryGetValue(referencia, out o)) {
				return Idiomas.EnEspanol ? o.Es : o.En;
			}
			int t = TipoObjeto(referencia);
			if (t > 0) {
				return Lang.GetItemNameValue(t);
			}
			return referencia ?? "";
		}

		public static string NombreNpc(string referencia)
		{
			RefNpc n;
			if (Refs != null && referencia != null && Refs.Npcs.TryGetValue(referencia, out n)) {
				return Idiomas.EnEspanol ? n.Es : n.En;
			}
			int t = TipoNpc(referencia);
			if (t != 0) {
				return Lang.GetNPCNameValue(t);
			}
			return referencia ?? "";
		}

		public static string NombreEstacion(string clave)
		{
			RefNombre e;
			if (Refs != null && clave != null && Refs.Estaciones.TryGetValue(clave, out e)) {
				return Idiomas.EnEspanol ? e.Es : e.En;
			}
			return clave ?? "";
		}

		public static string NombreGrupo(string clave)
		{
			RefNombre g;
			if (Refs != null && clave != null && Refs.Grupos.TryGetValue(clave, out g)) {
				return Idiomas.EnEspanol ? g.Es : g.En;
			}
			return clave ?? "";
		}

		public static Zona ZonaPorId(string id) => Doc != null && id != null ? Doc.Zonas.FirstOrDefault(z => z.Id == id) : null;
		public static Parada ParadaPorId(string id) => Doc != null && id != null ? Doc.Paradas.FirstOrDefault(p => p.Id == id) : null;
		public static Articulo ArticuloPorId(string id) => Doc != null && id != null ? Doc.Articulos.FirstOrDefault(a => a.Id == id) : null;

		public static string NombreZona(string id) { Zona z = ZonaPorId(id); return z != null ? z.Nombre : id; }

		/// <summary>Texto plano (buscador, registros, autopruebas) con objetos, NPC y zonas ya en el
		/// idioma activo (GuiaV2Texto.Plano de Core usa la tabla solo en español).</summary>
		public static string PlanoLocal(string texto)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach (SegmentoTexto s in GuiaV2Texto.Analizar(texto)) {
				switch (s.Tipo) {
					case TipoSegmento.Texto:
					case TipoSegmento.Negrita: sb.Append(s.Valor); break;
					case TipoSegmento.Objeto: sb.Append(s.TextoPropio ?? NombreObjeto(s.Valor)); break;
					case TipoSegmento.Npc: sb.Append(s.TextoPropio ?? NombreNpc(s.Valor)); break;
					case TipoSegmento.Zona: sb.Append(s.TextoPropio ?? NombreZona(s.Valor)); break;
					case TipoSegmento.Parada: { Parada p = ParadaPorId(s.Valor); sb.Append(s.TextoPropio ?? (p != null ? PlanoLocal(p.Titulo) : s.Valor)); break; }
					case TipoSegmento.Articulo: { Articulo a = ArticuloPorId(s.Valor); sb.Append(s.TextoPropio ?? (a != null ? PlanoLocal(a.Titulo) : s.Valor)); break; }
				}
			}
			return sb.ToString();
		}

		// ---- progreso y clase ------------------------------------------------------------------

		public static GuiaV2ProgresoManual Progreso {
			get {
				GuiaV2Jugador j = GuiaV2Jugador.Local;
				return j != null && IdGuia != null ? j.Progreso(IdGuia) : null;
			}
		}

		/// <summary>Clases que admite la guia activa, en orden.</summary>
		public static List<ClaseGuia> ClasesDisponibles()
		{
			List<ClaseGuia> salida = new List<ClaseGuia>();
			if (Doc == null) {
				return salida;
			}
			foreach (string c in Doc.Clases) {
				ClaseGuia? cg = GuiaV2Evaluador.ClaseDesdeClave(c);
				if (cg.HasValue) {
					salida.Add(cg.Value);
				}
			}
			return salida;
		}

		/// <summary>Clase elegida por el jugador; si no ha elegido, la que propone su mejor arma.</summary>
		public static ClaseGuia Clase {
			get {
				GuiaV2ProgresoManual p = Progreso;
				ClaseGuia? elegida = p != null ? GuiaV2Evaluador.ClaseDesdeClave(p.Clase) : null;
				return elegida ?? ClasePropuesta();
			}
		}

		public static bool ClaseElegidaAMano {
			get { GuiaV2ProgresoManual p = Progreso; return p != null && GuiaV2Evaluador.ClaseDesdeClave(p.Clase).HasValue; }
		}

		public static void ElegirClase(ClaseGuia clase)
		{
			GuiaV2ProgresoManual p = Progreso;
			if (p == null) {
				return;
			}
			p.Clase = GuiaV2Evaluador.ClaveClase(clase);
			GuiaV2Jugador.AvisarCambio();
		}

		/// <summary>Propuesta por el arma de mas daño de la mochila (DamageType real del objeto).
		/// La ultima palabra es del jugador (selector de clase).</summary>
		public static ClaseGuia ClasePropuesta()
		{
			Player p = Main.LocalPlayer;
			if (p == null || !p.active || Main.gameMenu) {
				return ClaseGuia.CuerpoACuerpo;
			}
			int mejor = 0;
			ClaseGuia clase = ClaseGuia.CuerpoACuerpo;
			for (int i = 0; i < 58 && i < p.inventory.Length; i++) {
				Item it = p.inventory[i];
				if (it == null || it.IsAir || it.damage <= 0 || it.accessory || it.ammo != AmmoID.None) {
					continue;
				}
				int d = p.GetWeaponDamage(it);
				if (d <= mejor) {
					continue;
				}
				ClaseGuia? c = ClaseDeObjeto(it);
				if (c.HasValue) {
					mejor = d;
					clase = c.Value;
				}
			}
			if (clase == ClaseGuia.Picaro && IdGuia != "calamity") {
				clase = ClaseGuia.Distancia;
			}
			return clase;
		}

		private static ClaseGuia? ClaseDeObjeto(Item it)
		{
			DamageClass dc = it.DamageType;
			if (dc == null) {
				return null;
			}
			if (dc.Name == "RogueDamageClass") return ClaseGuia.Picaro;
			if (dc.CountsAsClass(DamageClass.Summon) || dc.CountsAsClass(DamageClass.SummonMeleeSpeed)) return ClaseGuia.Invocacion;
			if (dc.CountsAsClass(DamageClass.Magic)) return ClaseGuia.Magia;
			if (dc.CountsAsClass(DamageClass.Ranged)) return ClaseGuia.Distancia;
			if (dc.CountsAsClass(DamageClass.Melee)) return ClaseGuia.CuerpoACuerpo;
			return null;
		}

		public static string NombreClase(ClaseGuia c) => Idiomas.Texto("GuiaV2.Clase." + GuiaV2Evaluador.ClaveClase(c));

		public static void MarcarTarea(string id, bool hecha)
		{
			GuiaV2ProgresoManual p = Progreso;
			if (p == null) return;
			p.MarcarTarea(id, hecha);
			GuiaV2Jugador.AvisarCambio();
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: tarea " + id + " marcada a mano = " + hecha + ".");
		}

		public static void MarcarParada(string id, bool hecha)
		{
			GuiaV2ProgresoManual p = Progreso;
			if (p == null) return;
			p.MarcarParada(id, hecha);
			GuiaV2Jugador.AvisarCambio();
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: parada " + id + " marcada a mano = " + hecha + ".");
		}

		public static void AplazarParada(string id, bool aplazada)
		{
			GuiaV2ProgresoManual p = Progreso;
			if (p == null) return;
			p.AplazarParada(id, aplazada);
			GuiaV2Jugador.AvisarCambio();
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: parada " + id + " aplazada = " + aplazada + ".");
		}

		// ---- evaluacion --------------------------------------------------------------------------

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ || !HayGuia) {
				return;
			}
			if (!EstadoJugadorGuia.HayPartida) {
				Resumen = null;
				return;
			}
			_fotogramas++;
			if (_fotogramas >= 30 || Resumen == null || _cambiosVistos != GuiaV2Jugador.Cambios) {
				Reevaluar();
			}
			UbicacionGuia.Actualizar();
		}

		/// <summary>Evalua ya la guia entera contra la partida. Publico para la autoprueba.</summary>
		public static void Reevaluar()
		{
			_fotogramas = 0;
			_cambiosVistos = GuiaV2Jugador.Cambios;
			GuiaV2ProgresoManual progreso = Progreso;
			if (!HayGuia || progreso == null) {
				Resumen = null;
				return;
			}
			try {
				ResumenGuiaV2 r = Evaluador.Evaluar(Proveedor, progreso, Clase);
				Resumen = r;
				string firma = (r.Siguiente != null ? r.Siguiente.Parada.Id : "-") + "|" + r.ParadasCompletadas + "|" +
					r.TareasHechas + "|" + string.Join(",", r.ModosActivos) + "|" + Clase;
				if (firma != _firmaUltima) {
					bool cambioSiguiente = _firmaUltima.Split('|')[0] != firma.Split('|')[0];
					_firmaUltima = firma;
					Version++;
					RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: evaluacion -> siguiente parada=" +
						(r.Siguiente != null ? r.Siguiente.Parada.Id : "(ruta terminada)") + ", paradas " +
						r.ParadasCompletadas + "/" + r.Paradas.Count + ", tareas " + r.TareasHechas + "/" + r.TareasTotales +
						", modos [" + string.Join(",", r.ModosActivos) + "], clase " + Clase + ".");
					if (cambioSiguiente) {
						UbicacionGuia.Invalidar();
					}
				}
			}
			catch (Exception e) {
				RegistroGuia.Error(Terrakeep.LogTag + " Guia v2: EXCEPCION evaluando: " + e);
			}
		}

		public override void OnWorldLoad()
		{
			Resumen = null;
			_firmaUltima = "";
			UbicacionGuia.Limpiar();
		}

		public override void OnWorldUnload()
		{
			Resumen = null;
			_firmaUltima = "";
			UbicacionGuia.Limpiar();
		}
	}
}
