using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Ajustes;
using TerrakeepMod.UI.GuiaV2;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Autoprueba EN EL JUEGO de la Guia v2 (F3, 02-oct-2026), activada con
	/// <c>TERRAKEEP_AUTOTEST_GUIAV2=1</c> (scripts\verificar-guia-v2.ps1). Deja su evidencia en
	/// <c>terrakeep-guia-evidencia.log</c> (via <see cref="RegistroGuia"/>) y capturas reales del back
	/// buffer en <c>terrakeep-capturas\guiav2-*.png</c>.
	/// </summary>
	/// <remarks>
	/// Comprueba, con el juego de verdad: carga del contenido, evaluacion en vivo, la MARCA DEL MAPA
	/// (posicion calculada contra las casillas reales del mundo, y posicion dibujada en pantalla
	/// contra la esperada, a pantalla completa y en el minimapa), el interruptor de Ajustes, el
	/// progreso manual guardado en el personaje, la ficha "cómo conseguirlo" abierta con un clic real
	/// en un objeto que falta, el salto a la Librería con el objeto ya buscado, todas las
	/// sub-pestañas y el ajuste del texto (nada se sale de su caja) a 1280x720, 1600x900, 1920x1080 y
	/// 2560x1440 con UIScale minima, normal y maxima. Cualquier comprobacion en rojo deja una linea
	/// con "NO CUADRA" / "NO CABE" / "EXCEPCION", que es lo que busca el script.
	/// </remarks>
	public class AutopruebaGuiaV2 : ModSystem
	{
		public const string Variable = "TERRAKEEP_AUTOTEST_GUIAV2";

		private static bool? _activa;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _espera;
		private static readonly Queue<(string Nombre, Action Accion)> _cola = new Queue<(string, Action)>();
		private static bool _colaHecha;
		private static int _fallos;
		private static int _comprobaciones;
		private static float _uiScaleAntes = -1f;
		private static int _resAnchoAntes, _resAltoAntes;
		private static int _mapStyleAntes = -1;
		private static GuiaV2ProgresoManual _progresoAntes;

		public static bool Activa {
			get {
				if (!_activa.HasValue) {
					_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
				}
				return _activa.Value;
			}
		}

		private static void L(string s) => RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA V2 - " + s);

		private static void Ok(bool ok, string que)
		{
			_comprobaciones++;
			if (!ok) _fallos++;
			L((ok ? "OK: " : "NO CUADRA: ") + que);
		}

		private static void Capturar(string nombre) => L(CapturaDePantalla.Guardar("guiav2-" + nombre));

		/// <summary>
		/// Pasada SIN PANTALLA en el servidor dedicado (verificar-guia-v2.ps1 -Servidor): el cliente
		/// grafico exige Steam con sesion iniciada (CoreSocialModule.Initialize -> FatalExit
		/// "Please ensure Steam is logged in and running"), el servidor no. Aqui no hay jugador ni
		/// interfaz, pero si el contenido, las referencias, las banderas, el mundo real y las fichas.
		/// </summary>
		/// <remarks>Se lanza desde el evento <c>WorldFile.OnWorldLoad</c> (el ULTIMO paso de
		/// <c>WorldFile.LoadWorld</c>) y no desde un hook por fotograma ni desde
		/// <c>ModSystem.OnWorldLoad</c>: un servidor dedicado SIN clientes no actualiza el mundo
		/// (Main.ShouldUpdateEntities), y <c>ModSystem.OnWorldLoad</c> corre ANTES de
		/// <c>WorldIO.Load</c> (WorldFile.cs:643-644 del decompilado), o sea sin los tiles de mod ni
		/// los datos de Calamity del .twld todavia - visto en la primera pasada: 0 tiles de mod en un
		/// mundo generado con Calamity.</remarks>
		public override void Load()
		{
			Terraria.IO.WorldFile.OnWorldLoad += PasadaDeServidor;
		}

		public override void Unload()
		{
			Terraria.IO.WorldFile.OnWorldLoad -= PasadaDeServidor;
		}

		private static void PasadaDeServidor()
		{
			if (!Main.dedServ || !Activa || _terminada) {
				return;
			}
			_terminada = true;
			try {
				L("pasada de SERVIDOR (sin interfaz). Mundo " + Main.worldName + " " + Main.maxTilesX + "x" + Main.maxTilesY +
					", carmesi=" + WorldGen.crimson + ", Calamity=" + CatalogoGuia.HayCalamity + ".");
				ComprobarCarga();
				HistogramaTilesDeMod();
				if (GuiaV2Sistema.HayGuia) {
					ComprobarZonasContraElMundo();
					ComprobarLaboratorios();
					ComprobarParadasEnElMundo();
					ComprobarFichasSinJugador();
				}
			}
			catch (Exception e) {
				_fallos++;
				L("EXCEPCION en la pasada de servidor: " + e);
			}
			_terminada = false;
			Terminar();
		}

		/// <summary>Que tiles de mod hay de verdad en el mundo (diagnostico: si una zona de Calamity
		/// "no existe", se ve aqui si es porque el mundo no los tiene o porque el nombre no casa).</summary>
		private static void HistogramaTilesDeMod()
		{
			MundoGuiaVivo m = new MundoGuiaVivo();
			Dictionary<string, int> cuenta = new Dictionary<string, int>();
			for (int x = 0; x < Main.maxTilesX; x += 2) {
				for (int y = 0; y < Main.maxTilesY; y += 2) {
					string n = m.TileMod(x, y);
					if (n == null) continue;
					cuenta.TryGetValue(n, out int c);
					cuenta[n] = c + 1;
				}
			}
			L("tiles de mod en el mundo (muestreo 1 de 4): " + cuenta.Count + " tipos; los mas abundantes: " +
				string.Join(", ", cuenta.OrderByDescending(k => k.Value).Take(25).Select(k => k.Key + "=" + k.Value)) + ".");
		}

		/// <summary>Centros de los laboratorios de Draedon leidos por reflexion de CalamityWorld y
		/// comprobados contra las casillas reales (planchas de laboratorio alrededor).</summary>
		private static void ComprobarLaboratorios()
		{
			if (!CatalogoGuia.HayCalamity) {
				return;
			}
			MundoGuiaVivo m = new MundoGuiaVivo();
			foreach (string clave in new[] { "SunkenSeaLabCenter", "PlanetoidLabCenter", "JungleLabCenter", "HellLabCenter", "IceLabCenter", "CavernLabCenter" }) {
				(int X, int Y)? c = ReflexionCalamity.CentroLaboratorio(clave);
				if (!c.HasValue) { L("laboratorio " + clave + ": no generado en este mundo."); continue; }
				int n = 0;
				for (int x = c.Value.X - 40; x < c.Value.X + 40; x++) {
					for (int y = c.Value.Y - 40; y < c.Value.Y + 40; y++) {
						if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) continue;
						string t = m.TileMod(x, y);
						if (t != null && t.StartsWith("CalamityMod/Laboratory", StringComparison.Ordinal)) n++;
					}
				}
				Ok(n >= 30, "laboratorio " + clave + " en (" + c.Value.X + ", " + c.Value.Y + "): " + n + " casillas de laboratorio REALES alrededor");
			}
		}

		/// <summary>Situa TODAS las paradas en el mundo real: cuantas exactas, aproximadas o sin lugar.</summary>
		private static void ComprobarParadasEnElMundo()
		{
			int exactas = 0, aproximadas = 0, nulas = 0, sinUbicacion = 0;
			foreach (Parada p in GuiaV2Sistema.Doc.Paradas) {
				if (p.Ubicaciones.Count == 0) { sinUbicacion++; continue; }
				UbicacionGuia.Objetivo o = UbicacionGuia.ResolverParada(p);
				if (o == null) { nulas++; L("parada " + p.Id + ": su lugar NO existe en este mundo (" + string.Join(", ", p.Ubicaciones.Select(u => u.Tipo + ":" + u.Id)) + ")."); continue; }
				if (o.Aproximada) aproximadas++; else exactas++;
				L("parada " + p.Id + " -> (" + (int)o.Tile.X + ", " + (int)o.Tile.Y + ") " + o.Origen + (o.Aproximada ? " APROXIMADA" : "") + " · " + o.Lugar);
				if (p.Ubicaciones[0].Tipo == "punto" && p.Ubicaciones[0].Id == "mazmorra") {
					Ok(Vector2.Distance(o.Tile, new Vector2(Main.dungeonX, Main.dungeonY)) < 2f,
						"parada " + p.Id + ": la marca cae en la entrada real de la Mazmorra (" + Main.dungeonX + ", " + Main.dungeonY + ")");
				}
				if (p.Ubicaciones[0].Tipo == "punto" && p.Ubicaciones[0].Id == "spawn") {
					Ok((int)o.Tile.X == Main.spawnTileX && (int)o.Tile.Y == Main.spawnTileY, "parada " + p.Id + ": la marca cae en el punto de aparicion real");
				}
			}
			L("paradas situadas en este mundo: " + exactas + " exactas, " + aproximadas + " aproximadas (lo dicen), " + nulas +
				" sin lugar en este mundo, " + sinUbicacion + " sin ubicacion en el contenido.");
			Ok(exactas + aproximadas > GuiaV2Sistema.Doc.Paradas.Count / 2, "la mayoria de paradas se puede marcar en el mapa de este mundo");
		}

		/// <summary>Fichas "cómo conseguirlo" de los objetos que pide la ruta, sin jugador: contenido
		/// y, en una partida sin Calamity, que NO aparezca nada que solo exista con Calamity.</summary>
		private static void ComprobarFichasSinJugador()
		{
			HashSet<string> refs = new HashSet<string>();
			foreach (Parada p in GuiaV2Sistema.Doc.Paradas) {
				if (p.Invocacion != null && !string.IsNullOrEmpty(p.Invocacion.Objeto)) refs.Add(p.Invocacion.Objeto);
				foreach (ObjetoNecesario n in p.Necesitas) refs.Add(n.Ref);
			}
			int conComo = 0, enElMundo = 0, sinTipo = 0, deCalamity = 0;
			foreach (string r in refs) {
				ObtencionGuia.Ficha f = ObtencionGuia.Construir(r, 1);
				if (f.Tipo <= 0) { sinTipo++; L("ficha " + r + ": el objeto no existe en esta partida."); continue; }
				string todo = string.Join(" | ", f.Lineas.Select(l => GuiaV2Sistema.PlanoLocal(l.Texto)));
				if (todo.Contains(Idiomas.Texto("GuiaV2.Ficha.EnElMundo").Substring(0, 20))) enElMundo++; else conComo++;
				if (!CatalogoGuia.HayCalamity && f.Lineas.Any(l => l.Texto.Contains("CalamityMod"))) {
					deCalamity++;
					L("NO CUADRA: la ficha de " + r + " enseña datos de Calamity en una partida sin Calamity: " + todo);
				}
			}
			L("fichas de los " + refs.Count + " objetos que pide la ruta: " + conComo + " con receta/botin/bolsa/tienda, " + enElMundo +
				" «se consigue en el mundo», " + sinTipo + " sin objeto en esta partida.");
			Ok(deCalamity == 0, "ninguna ficha enseña recetas o botin de Calamity sin Calamity cargado (RefObjeto.ObtencionPara)");
			Ok(sinTipo == 0, "todos los objetos que pide la ruta existen en esta partida");
			string ejemplo = refs.FirstOrDefault(x => GuiaV2Sistema.TipoObjeto(x) > 0);
			if (ejemplo != null) {
				ObtencionGuia.Ficha f = ObtencionGuia.Construir(ejemplo, 1);
				L("ejemplo de ficha (" + ejemplo + "): " + string.Join(" | ", f.Lineas.Select(l => GuiaV2Sistema.PlanoLocal(l.Texto))));
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!Activa || _terminada || Main.dedServ) {
				return;
			}
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}
			if (++_fotogramasEnMundo < 200) {
				return;
			}
			if (!_colaHecha) {
				_colaHecha = true;
				ConstruirCola();
				L("comienza: " + _cola.Count + " pasos. Resolucion " + Main.screenWidth + "x" + Main.screenHeight +
					", UIScale " + Main.UIScale.ToString("0.00") + ", Calamity=" + CatalogoGuia.HayCalamity + ".");
			}
			if (_espera > 0) {
				_espera--;
				return;
			}
			if (_cola.Count == 0) {
				Terminar();
				return;
			}
			(string Nombre, Action Accion) paso = _cola.Dequeue();
			_espera = 12;
			try {
				paso.Accion();
			}
			catch (Exception e) {
				_fallos++;
				L("EXCEPCION en el paso \"" + paso.Nombre + "\": " + e);
			}
		}

		private static void Paso(string nombre, Action a) => _cola.Enqueue((nombre, a));
		private static void Esperar(int fotogramas) => _cola.Enqueue(("esperar", () => _espera = fotogramas));

		private static ContenidoGuiaV2 Guia => PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.GuiaV2 : null;

		// =========================================================================================

		private static void ConstruirCola()
		{
			Paso("carga", ComprobarCarga);
			if (!GuiaV2Sistema.HayGuia) {
				// Sin la guia de esta partida (p. ej. vanilla antes de F1): se comprueba que lo dice
				// con honestidad, que la Brujula sigue y que no hay marca en el mapa.
				Paso("abrir", () => AbrirGuia(ContenidoGuiaV2.Vista.MiGuia));
				Esperar(20);
				Paso("sin guia", () => {
					Ok(Guia != null && Guia.VistaActual == ContenidoGuiaV2.Vista.MiGuia, "la pestaña Guía abre aunque falte la guía de esta partida");
					Ok(UbicacionGuia.Siguiente == null, "sin guía no se pinta ninguna marca en el mapa");
					AuditarTexto("sin-guia");
					Capturar("sin-guia");
				});
				Paso("brujula", () => { Guia.CambiarVista(ContenidoGuiaV2.Vista.Brujula); });
				Esperar(30);
				Paso("brujula captura", () => { Ok(Guia.Brujula != null, "la Brújula (guía v1) sigue disponible"); Capturar("sin-guia-brujula"); });
				Paso("cerrar", () => PanelTerrakeepSystem.CerrarPanel("autoprueba guia v2"));
				return;
			}

			Paso("guardar estado", GuardarEstado);
			Paso("evaluacion", ComprobarEvaluacion);
			Paso("zonas reales", ComprobarZonasContraElMundo);

			// ---- marca en el mapa: siguiente parada = una zona con firma de casillas ----
			string paradaZona = ParadaDeZona();
			Paso("llevar a la zona", () => LlevarHastaParada(paradaZona));
			Esperar(40);
			Paso("marca zona", () => ComprobarMarcaSiguiente(paradaZona));
			Paso("mapa pantalla completa", () => AbrirMapaEn(UbicacionGuia.Siguiente));
			Esperar(50);
			Paso("captura mapa", () => ComprobarMarcaDibujada(paradaZona));
			Paso("cerrar mapa", () => { Main.mapFullscreen = false; });
			Esperar(40);
			Paso("minimapa", PonerMinimapa);
			Esperar(60);
			Paso("captura minimapa", ComprobarMinimapa);

			// ---- interruptor de Ajustes ----
			Paso("ajustes", () => PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Ajustes, "autoprueba guia v2"));
			Esperar(20);
			Paso("ajustes: ocultar", () => PulsarInterruptorMarca(false));
			Esperar(40);
			Paso("ajustes: comprobar oculta", () => ComprobarMarcaOculta(true));
			Paso("ajustes otra vez", () => PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Ajustes, "autoprueba guia v2"));
			Esperar(20);
			Paso("ajustes: mostrar", () => PulsarInterruptorMarca(true));
			Esperar(40);
			Paso("ajustes: comprobar visible", () => ComprobarMarcaOculta(false));

			// ---- la marca se actualiza sola al completar paradas ----
			Paso("hasta la mazmorra", () => LlevarHastaParada("skeletron"));
			Esperar(40);
			Paso("marca mazmorra", () => ComprobarMarcaSiguiente("skeletron"));
			Paso("mapa mazmorra", () => AbrirMapaEn(UbicacionGuia.Siguiente));
			Esperar(50);
			Paso("captura mapa mazmorra", () => ComprobarMarcaDibujada("mazmorra"));
			Paso("cerrar mapa 2", () => { Main.mapFullscreen = false; });
			Esperar(40);
			if (GuiaV2Sistema.IdGuia == "calamity") {
				Paso("zona de Calamity", () => LlevarHastaParada("sunken"));
				Esperar(30);
				Paso("marca zona de Calamity", ComprobarMarcaCalamity);
			}
			Paso("volver a la zona", () => LlevarHastaParada(paradaZona));
			Esperar(30);

			// ---- la guia te guia: objeto que falta -> ficha -> Libreria ----
			Paso("abrir Mi guía", () => AbrirGuia(ContenidoGuiaV2.Vista.MiGuia));
			Esperar(40);
			Paso("captura Mi guía", () => { AuditarTexto("mi-guia"); Capturar("mi-guia"); });
			Paso("clic en objeto que falta", ClicEnObjetoQueFalta);
			Esperar(30);
			Paso("ficha", ComprobarFicha);
			Paso("clic en ingrediente", ClicEnIngredienteDeLaFicha);
			Esperar(30);
			Paso("ficha ingrediente", () => { Ok(Guia != null && Guia.Ventana != null, "la ficha del ingrediente se abre desde la ficha (se recorre la cadena)"); Capturar("ficha-ingrediente"); });
			Paso("atras", PulsarAtras);
			Esperar(20);
			Paso("coger en la libreria", PulsarCogerEnLibreria);
			Esperar(40);
			Paso("libreria", ComprobarLibreria);

			// ---- progreso manual ----
			Paso("ruta", () => AbrirGuia(ContenidoGuiaV2.Vista.Ruta));
			Esperar(40);
			Paso("captura ruta", () => { AuditarTexto("ruta"); Capturar("ruta-arriba"); });
			Paso("tarea manual", MarcarTareaManual);
			Esperar(20);
			Paso("persistencia", ComprobarPersistencia);
			Paso("bajar ruta", () => BajarScroll(0.55f));
			Esperar(20);
			Paso("captura ruta abajo", () => Capturar("ruta-tareas"));

			// ---- resto de pestañas ----
			foreach (ContenidoGuiaV2.Vista v in new[] { ContenidoGuiaV2.Vista.Equipo, ContenidoGuiaV2.Vista.Manual, ContenidoGuiaV2.Vista.Perdido, ContenidoGuiaV2.Vista.Raro }) {
				ContenidoGuiaV2.Vista vista = v;
				Paso("vista " + v, () => AbrirGuia(vista));
				Esperar(40);
				Paso("captura " + v, () => { AuditarTexto(vista.ToString()); Capturar(vista.ToString().ToLowerInvariant()); });
			}
			Paso("articulo mapa", () => { ContenidoGuiaV2.ArticuloSeleccionado = "mapa"; Guia.CambiarVista(ContenidoGuiaV2.Vista.Manual); });
			Esperar(40);
			Paso("captura articulo mapa", () => { AuditarTexto("manual-mapa"); Capturar("manual-mapa"); });
			Paso("ficha de zona", () => Guia.AbrirFichaZona("desierto"));
			Esperar(30);
			Paso("captura ficha zona", () => { Ok(Guia.Ventana != null, "la ficha de zona se abre"); AuditarTexto("ficha-zona"); Capturar("ficha-zona"); Guia.Ventana?.Cerrar(); });
			Paso("busqueda", () => { Guia.FijarBusqueda("desierto"); });
			Esperar(30);
			Paso("captura busqueda", () => {
				Ok(Guia.VistaActual == ContenidoGuiaV2.Vista.Busqueda && Guia.ResultadosBusqueda > 0, "el buscador encuentra \"desierto\" (" + Guia.ResultadosBusqueda + " resultados)");
				AuditarTexto("busqueda");
				Capturar("busqueda");
				Guia.FijarBusqueda("");
			});
			Paso("clase", () => {
				ClaseGuia antes = GuiaV2Sistema.Clase;
				GuiaV2Sistema.ElegirClase(ClaseGuia.Magia);
				GuiaV2Sistema.Reevaluar();
				Ok(GuiaV2Sistema.Clase == ClaseGuia.Magia, "el selector de clase cambia la clase de la guía (" + antes + " -> " + GuiaV2Sistema.Clase + ")");
				Guia.CambiarVista(ContenidoGuiaV2.Vista.Equipo);
			});
			Esperar(40);
			Paso("captura equipo magia", () => { AuditarTexto("equipo-magia"); Capturar("equipo-magia"); });

			// ---- resoluciones y escala de interfaz ----
			Paso("guardar resolucion", () => { _resAnchoAntes = Main.screenWidth; _resAltoAntes = Main.screenHeight; _uiScaleAntes = Main.UIScaleWanted; });
			foreach ((int an, int al) in new[] { (1280, 720), (1600, 900), (1920, 1080), (2560, 1440) }) {
				int ancho = an, alto = al;
				Paso("resolucion " + ancho, () => {
					Main.SetDisplayMode(ancho, alto, false);
					L("resolucion pedida " + ancho + "x" + alto + " -> real " + Main.screenWidth + "x" + Main.screenHeight + ".");
				});
				Esperar(40);
				foreach (string escala in new[] { "normal", "max" }) {
					string e = escala;
					Paso("escala " + e, () => {
						Main.UIScale = e == "max" ? Main.instance.UIScaleMax : 1f;
						L("UIScale " + e + " = " + Main.UIScale.ToString("0.00") + " -> pantalla logica " +
							(int)(Main.screenWidth / Main.UIScale) + "x" + (int)(Main.screenHeight / Main.UIScale) + ".");
					});
					Esperar(30);
					string sufijo = Main.screenWidth + "x" + Main.screenHeight;
					Paso("mi guia " + ancho + e, () => AbrirGuia(ContenidoGuiaV2.Vista.MiGuia));
					Esperar(40);
					Paso("medir mi guia " + ancho + e, () => {
						string s = Main.screenWidth + "x" + Main.screenHeight + "-" + e;
						AuditarTexto("mi-guia-" + s);
						AuditarCabecera("mi-guia-" + s);
						Capturar("mi-guia-" + s);
					});
					Paso("ruta " + ancho + e, () => AbrirGuia(ContenidoGuiaV2.Vista.Ruta));
					Esperar(40);
					Paso("medir ruta " + ancho + e, () => {
						string s = Main.screenWidth + "x" + Main.screenHeight + "-" + e;
						AuditarTexto("ruta-" + s);
						Capturar("ruta-" + s);
						Guia.AbrirFichaObjeto(PrimeraRefFicha(), 1);
					});
					Esperar(30);
					Paso("medir ficha " + ancho + e, () => {
						string s = Main.screenWidth + "x" + Main.screenHeight + "-" + e;
						AuditarTexto("ficha-" + s);
						Capturar("ficha-" + s);
						Guia.Ventana?.Cerrar();
					});
				}
			}
			Paso("restaurar resolucion", () => {
				Main.UIScale = _uiScaleAntes > 0f ? _uiScaleAntes : 1f;
				if (_resAnchoAntes > 0) Main.SetDisplayMode(_resAnchoAntes, _resAltoAntes, false);
			});
			Esperar(30);
			Paso("restaurar estado", RestaurarEstado);
			Paso("cerrar", () => PanelTerrakeepSystem.CerrarPanel("autoprueba guia v2"));
		}

		// =========================================================================================

		private static void ComprobarCarga()
		{
			IReadOnlyList<string> disponibles = GuiaV2Cargador.GuiasDisponibles();
			string esperada = CatalogoGuia.HayCalamity ? "calamity" : "vanilla";
			L("guias incrustadas en Terrakeep.Core: [" + string.Join(", ", disponibles) + "]; esperada para esta partida: " + esperada + ".");
			if (disponibles.Contains(esperada)) {
				Ok(GuiaV2Sistema.HayGuia && GuiaV2Sistema.IdGuia == esperada,
					"guía activa = " + GuiaV2Sistema.IdGuia + " (" + (GuiaV2Sistema.Doc != null ? GuiaV2Sistema.Doc.Paradas.Count + " paradas, " +
					GuiaV2Sistema.Doc.Paradas.Sum(p => p.Tareas.Count) + " tareas, " + GuiaV2Sistema.Doc.Articulos.Count + " articulos" : "-") + ")");
			}
			else {
				Ok(!GuiaV2Sistema.HayGuia && GuiaV2Sistema.MotivoSinGuia == esperada,
					"la guía " + esperada + " no está incrustada todavía: el mod lo dice y no enseña otra en su lugar");
			}
			if (GuiaV2Sistema.Refs != null) {
				int resueltos = 0, total = 0;
				foreach (string r in GuiaV2Sistema.Refs.Objetos.Keys) {
					if (!CatalogoGuia.HayCalamity && r.StartsWith("CalamityMod/", StringComparison.Ordinal)) continue;
					total++;
					if (GuiaV2Sistema.TipoObjeto(r) > 0) resueltos++;
				}
				Ok(resueltos >= total * 0.97f, "referencias de objeto resueltas con ModContent.TryFind/ItemID: " + resueltos + " de " + total);
				int npcOk = 0, npcTotal = 0;
				foreach (string r in GuiaV2Sistema.Refs.Npcs.Keys) {
					if (!CatalogoGuia.HayCalamity && r.StartsWith("CalamityMod/", StringComparison.Ordinal)) continue;
					npcTotal++;
					if (GuiaV2Sistema.TipoNpc(r) != 0) npcOk++;
				}
				Ok(npcOk >= npcTotal * 0.97f, "referencias de NPC resueltas: " + npcOk + " de " + npcTotal);
			}
			// Las 6 banderas nuevas de Calamity, leidas por reflexion.
			if (CatalogoGuia.HayCalamity) {
				foreach (string b in new[] { "downedLeviathan", "downedEoCAcidRain", "downedAquaticScourgeAcidRain", "downedCLAMHardMode", "downedNuclearTerror", "downedBossRush" }) {
					Ok(BanderasGuia.Existe(b), "bandera de Calamity " + b + " legible (valor " + BanderasGuia.Valor(b) + ")");
				}
				Ok(ReflexionCalamity.EstadoMundoConocido("revenge") && ReflexionCalamity.MejoraConocida("miracleFruit"),
					"estado de mundo (revenge=" + ReflexionCalamity.EstadoMundo("revenge") + ") y mejoras de CalamityPlayer (mFruit=" +
					ReflexionCalamity.Mejora(Main.LocalPlayer, "miracleFruit") + ") legibles por reflexion");
			}
		}

		private static void GuardarEstado()
		{
			GuiaV2ProgresoManual p = GuiaV2Sistema.Progreso;
			_progresoAntes = GuiaV2ProgresoJson.Deserializar(GuiaV2ProgresoJson.Serializar(p), p.Guia, out _);
			L("progreso manual de partida guardado para restaurarlo al final (" + p.ParadasHechas.Count + " paradas, " + p.Tareas.Count + " tareas, clase \"" + p.Clase + "\").");
		}

		private static void RestaurarEstado()
		{
			GuiaV2ProgresoManual p = GuiaV2Sistema.Progreso;
			if (p == null || _progresoAntes == null) return;
			p.Tareas = _progresoAntes.Tareas;
			p.ParadasHechas = _progresoAntes.ParadasHechas;
			p.ParadasAplazadas = _progresoAntes.ParadasAplazadas;
			p.Clase = _progresoAntes.Clase;
			GuiaV2Jugador.AvisarCambio();
			GuiaV2Sistema.Reevaluar();
			if (_mapStyleAntes >= 0) Main.mapStyle = _mapStyleAntes;
			L("progreso manual restaurado; siguiente parada = " + (GuiaV2Sistema.Resumen?.Siguiente?.Parada.Id ?? "-") + ".");
		}

		private static void ComprobarEvaluacion()
		{
			GuiaV2Sistema.Reevaluar();
			ResumenGuiaV2 r = GuiaV2Sistema.Resumen;
			Ok(r != null && r.Paradas.Count == GuiaV2Sistema.Doc.Paradas.Count, "evaluación en vivo: " + (r != null ? r.Paradas.Count + " paradas, " +
				r.TareasHechas + "/" + r.TareasTotales + " tareas, siguiente=" + (r.Siguiente?.Parada.Id ?? "-") + ", modos [" + string.Join(",", r.ModosActivos) + "]" : "null"));
			if (r == null) return;
			int auto = r.Paradas.Sum(p => p.Tareas.Count(t => t.Automatica));
			int noEval = r.Paradas.Sum(p => p.Tareas.Count(t => t.Condicion != null && t.Condicion.Estado == EstadoCondicion.NoEvaluable));
			int sinDatos = r.Paradas.Sum(p => p.Tareas.Count(t => t.Condicion != null && t.Condicion.Estado == EstadoCondicion.SinDatos));
			L("tareas comprobadas solas por el juego: " + auto + "; no evaluables: " + noEval + "; sin datos: " + sinDatos + ".");
			foreach (ResultadoParada p in r.Paradas) {
				foreach (ResultadoTarea t in p.Tareas) {
					if (t.Condicion != null && t.Condicion.Estado == EstadoCondicion.NoEvaluable) {
						L("  tarea no evaluable en vivo: " + t.Tarea.Id + " -> " + TareaFilaTk.LineaCondicion(t.Condicion));
					}
				}
			}
			Ok(sinDatos == 0, "con partida cargada ninguna condición queda \"sin datos\"");
			Ok(r.ModosActivos.Count > 0, "el modo real de la partida se lee (" + ContenidoGuiaV2.ModosLegibles(r.ModosActivos) + ")");
		}

		/// <summary>Situa cada zona con firma de la guia y comprueba contra las casillas REALES de
		/// Main.tile que alrededor del punto hay de verdad esa firma.</summary>
		private static void ComprobarZonasContraElMundo()
		{
			foreach (Zona z in GuiaV2Sistema.Doc.Zonas) {
				if (z.Firma.Tiles.Count == 0 && z.Firma.TilesMod.Count == 0) continue;
				UbicacionGuia.Objetivo o = UbicacionGuia.ResolverZona(z.Id);
				if (o == null) {
					L("zona " + z.Id + " (" + z.Nombre + "): no existe en este mundo" + (z.Firma.TilesMod.Count > 0 ? " (tiles de mod que este mundo no tiene, o todavía no: p. ej. la Infección Astral llega en modo difícil)" : "") + ".");
					continue;
				}
				int cuenta = ContarFirma(z, (int)o.Tile.X, (int)o.Tile.Y, 40);
				Ok(cuenta >= 30, "zona " + z.Id + " situada en (" + (int)o.Tile.X + ", " + (int)o.Tile.Y + "): " + cuenta +
					" casillas REALES de su firma en 40 casillas a la redonda");
			}
		}

		public static int ContarFirma(Zona z, int cx, int cy, int radio)
		{
			MundoGuiaVivo m = new MundoGuiaVivo();
			HashSet<int> t = new HashSet<int>(z.Firma.Tiles);
			HashSet<string> tm = new HashSet<string>(z.Firma.TilesMod);
			int n = 0;
			for (int x = Math.Max(0, cx - radio); x < Math.Min(Main.maxTilesX, cx + radio); x++) {
				for (int y = Math.Max(0, cy - radio); y < Math.Min(Main.maxTilesY, cy + radio); y++) {
					if (t.Contains(m.TileVanilla(x, y))) n++;
					else if (tm.Count > 0) { string s = m.TileMod(x, y); if (s != null && tm.Contains(s)) n++; }
				}
			}
			return n;
		}

		/// <summary>Marca a mano todas las paradas anteriores a <paramref name="id"/> (y desmarca
		/// las de despues) para que la siguiente sea esa. Se restaura al final.</summary>
		private static void LlevarHastaParada(string id)
		{
			GuiaV2ProgresoManual p = GuiaV2Sistema.Progreso;
			int objetivo = GuiaV2Sistema.Doc.Paradas.FindIndex(x => x.Id == id);
			for (int i = 0; i < GuiaV2Sistema.Doc.Paradas.Count; i++) {
				string pid = GuiaV2Sistema.Doc.Paradas[i].Id;
				p.MarcarParada(pid, i < objetivo);
				p.AplazarParada(pid, false);
			}
			GuiaV2Jugador.AvisarCambio();
			GuiaV2Sistema.Reevaluar();
			UbicacionGuia.Recalcular();
			L("paradas anteriores a \"" + id + "\" marcadas a mano: siguiente = " + (GuiaV2Sistema.Resumen?.Siguiente?.Parada.Id ?? "-") + ".");
		}

		private static void ComprobarMarcaSiguiente(string paradaEsperada)
		{
			UbicacionGuia.Objetivo o = UbicacionGuia.Siguiente;
			string sig = GuiaV2Sistema.Resumen?.Siguiente?.Parada.Id;
			Ok(sig == paradaEsperada, "siguiente parada = " + sig + " (esperada " + paradaEsperada + ")");
			Ok(o != null && o.ParadaId == paradaEsperada, "la marca del mapa apunta a la parada " + (o != null ? o.ParadaId : "(ninguna)") +
				(o != null ? " en (" + (int)o.Tile.X + ", " + (int)o.Tile.Y + "), origen " + o.Origen + ", aproximada=" + o.Aproximada +
				", lugar \"" + o.Lugar + "\"" : "") + "; jugador en (" + (int)(Main.LocalPlayer.Center.X / 16f) + ", " + (int)(Main.LocalPlayer.Center.Y / 16f) +
				"), spawn (" + Main.spawnTileX + ", " + Main.spawnTileY + "), mazmorra (" + Main.dungeonX + ", " + Main.dungeonY + ")");
			if (o == null) return;
			if (o.Origen == "firma") {
				Parada parada = GuiaV2Sistema.ParadaPorId(paradaEsperada);
				Zona z = null;
				foreach (Ubicacion u in parada.Ubicaciones) {
					Zona zz = u.Tipo == "zona" ? GuiaV2Sistema.ZonaPorId(u.Id) : null;
					if (zz != null && (zz.Firma.Tiles.Count > 0 || zz.Firma.TilesMod.Count > 0) && zz.Nombre == o.Lugar) { z = zz; break; }
				}
				if (z != null) {
					int n = ContarFirma(z, (int)o.Tile.X, (int)o.Tile.Y, 40);
					Ok(n >= 30, "la marca de " + z.Id + " cae sobre casillas REALES de su firma (TileID " + string.Join("/", z.Firma.Tiles) +
						"): " + n + " en 40 casillas a la redonda");
				}
			}
			if (paradaEsperada == "skeletron") {
				float d = Vector2.Distance(o.Tile, new Vector2(Main.dungeonX, Main.dungeonY));
				Ok(d < 2f, "la marca de Esqueletrón está en la entrada real de la Mazmorra (Main.dungeonX/Y), distancia " + d.ToString("0.0"));
			}
			L("tooltip de la marca: \"" + CapaMarcaGuia.TextoTooltip(o, true).Replace("\n", " | ") + "\"");
		}

		private static void ComprobarMarcaCalamity()
		{
			UbicacionGuia.Objetivo o = UbicacionGuia.Siguiente;
			L("parada \"sunken\" (Mar Hundido, tiles de Calamity): estado de la marca = " + UbicacionGuia.Estado +
				(o != null ? " en (" + (int)o.Tile.X + ", " + (int)o.Tile.Y + ") origen " + o.Origen : "") + ".");
			if (o != null && o.Origen == "firma") {
				int n = ContarFirma(GuiaV2Sistema.ZonaPorId("mar_hundido"), (int)o.Tile.X, (int)o.Tile.Y, 40);
				Ok(n >= 30, "la marca del Mar Hundido cae sobre tiles reales de Calamity: " + n);
			}
			else {
				Ok(o != null || UbicacionGuia.Estado == "no_existe", "sin Mar Hundido en el mundo la guía lo dice (\"no_existe\") en vez de inventar un punto");
			}
		}

		private static void AbrirMapaEn(UbicacionGuia.Objetivo o)
		{
			if (o == null) { Ok(false, "no hay marca que enseñar en el mapa"); return; }
			CapaMarcaGuia.FotogramasDibujados = 0;
			UbicacionGuia.VerEnElMapa(o, false, "autoprueba");
		}

		private static void ComprobarMarcaDibujada(string que)
		{
			UbicacionGuia.Objetivo o = UbicacionGuia.Siguiente;
			Ok(Main.mapFullscreen, "el mapa a pantalla completa está abierto");
			Ok(CapaMarcaGuia.FotogramasDibujados > 10 && CapaMarcaGuia.UltimoMapa == "pantalla completa",
				"la marca se ha dibujado en " + CapaMarcaGuia.FotogramasDibujados + " fotogramas del mapa a pantalla completa");
			// Posicion esperada: el mapa se centro en el objetivo (Main.mapFullscreenPos), asi que la
			// marca tiene que caer en el centro de la pantalla (salvo que el mapa lo recorte en un borde).
			Vector2 centro = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
			Vector2 pos = CapaMarcaGuia.UltimaPosicionPantalla;
			float d = Vector2.Distance(pos, centro);
			Ok(!CapaMarcaGuia.UltimaFueFlecha, "la marca está DENTRO del mapa visible (no como flecha de borde)");
			L("marca dibujada en pantalla en (" + (int)pos.X + ", " + (int)pos.Y + "); centro de pantalla (" + (int)centro.X + ", " + (int)centro.Y +
				"); distancia " + d.ToString("0") + " px; mapFullscreenPos=(" + (int)Main.mapFullscreenPos.X + ", " + (int)Main.mapFullscreenPos.Y +
				"); objetivo en casillas (" + (o != null ? (int)o.Tile.X + ", " + (int)o.Tile.Y : "-") + ").");
			Ok(d < Math.Max(60f, Main.screenHeight * 0.45f), "la marca cae donde se centró el mapa (el objetivo real), a " + d.ToString("0") + " px del centro");
			Capturar("mapa-" + que);
		}

		private static void PonerMinimapa()
		{
			if (_mapStyleAntes < 0) _mapStyleAntes = Main.mapStyle;
			Main.mapStyle = 1;
			Main.mapEnabled = true;
			Main.mapMinimapScale = 1.25f;
			CapaMarcaGuia.FotogramasMinimapa = 0;
			PanelTerrakeepSystem.CerrarPanel("autoprueba guia v2: minimapa");
		}

		private static void ComprobarMinimapa()
		{
			Ok(CapaMarcaGuia.FotogramasMinimapa > 10, "la marca se dibuja en el MINIMAPA (" + CapaMarcaGuia.FotogramasMinimapa + " fotogramas; " +
				(CapaMarcaGuia.UltimaFueFlecha ? "como flecha en el borde, apuntando al objetivo fuera de vista" : "dentro del minimapa") + ", en (" +
				(int)CapaMarcaGuia.UltimaPosicionPantalla.X + ", " + (int)CapaMarcaGuia.UltimaPosicionPantalla.Y + "))");
			Capturar("minimapa");
		}

		private static void PulsarInterruptorMarca(bool debeQuedar)
		{
			ContenidoAjustes a = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Ajustes : null;
			if (a == null || a.BotonMarcaMapa == null) { Ok(false, "Ajustes abierto con su interruptor de la marca"); return; }
			BotonTk b = a.BotonMarcaMapa;
			CalculatedStyle d = b.GetDimensions();
			b.LeftClick(new UIMouseEvent(b, new Vector2(d.X + d.Width / 2f, d.Y + d.Height / 2f)));
			Ok(AjustesConfig.Instance.MarcaGuiaEnMapa == debeQuedar, "clic real en el interruptor de Ajustes: MarcaGuiaEnMapa = " + AjustesConfig.Instance.MarcaGuiaEnMapa);
			if (!debeQuedar) Capturar("ajustes-marca-oculta");
			CapaMarcaGuia.FotogramasMinimapa = 0;
			PanelTerrakeepSystem.CerrarPanel("autoprueba guia v2: ver el minimapa");
		}

		private static void ComprobarMarcaOculta(bool oculta)
		{
			bool dibujo = CapaMarcaGuia.FotogramasMinimapa > 0;
			Ok(oculta ? !dibujo : dibujo, oculta ? "con la marca oculta en Ajustes no se dibuja nada (" + CapaMarcaGuia.FotogramasMinimapa + " fotogramas)"
				: "al volver a activarla se dibuja otra vez (" + CapaMarcaGuia.FotogramasMinimapa + " fotogramas)");
		}

		private static void AbrirGuia(ContenidoGuiaV2.Vista v)
		{
			if (PanelTerrakeepSystem.Panel == null || PanelTerrakeepSystem.AreaAbierta != AreaTerrakeep.Guia) {
				PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Guia, "autoprueba guia v2");
			}
			if (Guia != null) {
				Guia.Ventana?.Cerrar();
				Guia.CambiarVista(v);
			}
		}

		private static string _refPulsada;

		private static void ClicEnObjetoQueFalta()
		{
			ObjetoFilaTk fila = null;
			Guia?.ExecuteRecursively(e => { if (fila == null && e is ObjetoFilaTk f && !f.LoTienes) fila = f; });
			if (fila == null) { Ok(false, "hay un objeto que falta en \"Lo que te falta\" para pulsarlo"); return; }
			_refPulsada = fila.Ref;
			CalculatedStyle d = fila.GetDimensions();
			L("clic real en la fila del objeto que falta: " + fila.Ref + " (\"" + GuiaV2Sistema.NombreObjeto(fila.Ref) + "\", tienes " + fila.Tienes + ").");
			fila.LeftClick(new UIMouseEvent(fila, new Vector2(d.X + d.Width / 2f, d.Y + d.Height / 2f)));
		}

		private static void ComprobarFicha()
		{
			ContenidoGuiaV2 g = Guia;
			Ok(g != null && g.Ventana != null && g.FichaObjetoAbierta == _refPulsada, "el clic abre la ficha \"cómo conseguirlo\" de " + _refPulsada);
			if (g == null || g.Ventana == null) return;
			List<string> lineas = new List<string>();
			g.Ventana.ExecuteRecursively(e => { if (e is TextoRicoTk t) lineas.Add(t.TextoPlano); });
			string todo = string.Join(" | ", lineas);
			L("contenido de la ficha: " + todo);
			Ok(todo.Contains(Idiomas.Texto("GuiaV2.Ficha.ComoConseguirlo")), "la ficha explica cómo conseguirlo");
			bool hayLibreria = false;
			g.Ventana.ExecuteRecursively(e => { if (e is BotonTk b && b.Texto == Idiomas.Texto("GuiaV2.Ficha.CogerEnLibreria")) hayLibreria = true; });
			Ok(hayLibreria, "la ficha ofrece el botón \"Coger en la Librería\"");
			AuditarTexto("ficha");
			Capturar("ficha-objeto");
		}

		private static void ClicEnIngredienteDeLaFicha()
		{
			ContenidoGuiaV2 g = Guia;
			TextoRicoTk conEnlace = null;
			int indice = -1;
			g?.Ventana?.ExecuteRecursively(e => {
				if (conEnlace != null || !(e is TextoRicoTk t)) return;
				for (int i = 0; i < t.CuantosEnlaces; i++) {
					if (t.TipoDeEnlace(i) == TipoSegmento.Objeto) { conEnlace = t; indice = i; return; }
				}
			});
			if (conEnlace == null) { L("la ficha no tiene ingredientes enlazados (no sale de receta): se salta el paso."); return; }
			L("se pulsa el ingrediente " + conEnlace.ValorDeEnlace(indice) + " dentro de la ficha.");
			conEnlace.PulsarEnlace(indice);
		}

		private static void PulsarAtras()
		{
			BotonTk atras = null;
			Guia?.Ventana?.ExecuteRecursively(e => { if (e is BotonTk b && b.Texto == Idiomas.Texto("GuiaV2.Atras")) atras = b; });
			if (atras != null) {
				CalculatedStyle d = atras.GetDimensions();
				atras.LeftClick(new UIMouseEvent(atras, new Vector2(d.X + 2, d.Y + 2)));
				Ok(Guia.FichaObjetoAbierta == _refPulsada, "\"Atrás\" vuelve a la ficha del objeto que faltaba");
			}
		}

		private static void PulsarCogerEnLibreria()
		{
			BotonTk boton = null;
			Guia?.Ventana?.ExecuteRecursively(e => { if (e is BotonTk b && b.Texto == Idiomas.Texto("GuiaV2.Ficha.CogerEnLibreria")) boton = b; });
			if (boton == null) { Ok(false, "botón \"Coger en la Librería\" encontrado"); return; }
			CalculatedStyle d = boton.GetDimensions();
			boton.LeftClick(new UIMouseEvent(boton, new Vector2(d.X + d.Width / 2f, d.Y + d.Height / 2f)));
		}

		private static void ComprobarLibreria()
		{
			PanelTerrakeepState p = PanelTerrakeepSystem.Panel;
			bool enLibreria = PanelTerrakeepSystem.AreaAbierta == AreaTerrakeep.Libreria && p != null && p.Libreria != null;
			Ok(enLibreria, "\"Coger en la Librería\" lleva a la pestaña Librería");
			if (!enLibreria) return;
			int tipo = GuiaV2Sistema.TipoObjeto(_refPulsada);
			bool esta = false;
			p.Libreria.ExecuteRecursively(e => { if (e is TerrakeepMod.UI.Libreria.SlotCatalogoLibreria s && s.Tipo == tipo) esta = true; });
			Ok(esta, "la Librería enseña el objeto buscado (" + _refPulsada + ", tipo " + tipo + ") entre sus resultados, listo para cogerlo");
			Capturar("libreria-con-objeto");
		}

		private static string _tareaMarcada;

		private static void MarcarTareaManual()
		{
			TareaFilaTk fila = null;
			Guia?.ExecuteRecursively(e => { if (fila == null && e is TareaFilaTk t && !t.Hecha) fila = t; });
			if (fila == null) { Ok(false, "hay una tarea sin hacer en la Ruta"); return; }
			_tareaMarcada = fila.TareaId;
			fila.Alternar();
			Ok(GuiaV2Sistema.Progreso.TareaMarcada(_tareaMarcada) && fila.Hecha, "la casilla de la tarea " + _tareaMarcada + " se marca a mano");
		}

		private static void ComprobarPersistencia()
		{
			GuiaV2Jugador j = GuiaV2Jugador.Local;
			TagCompound tag = new TagCompound();
			j.SaveData(tag);
			string json = tag.ContainsKey("guiaV2." + GuiaV2Sistema.IdGuia) ? tag.GetString("guiaV2." + GuiaV2Sistema.IdGuia) : null;
			Ok(json != null && json.Contains(_tareaMarcada ?? "@@"), "el progreso manual se guarda en el personaje (ModPlayer.SaveData, clave guiaV2." + GuiaV2Sistema.IdGuia + ", " +
				(json != null ? json.Length : 0) + " bytes de JSON)");
			GuiaV2Jugador copia = new GuiaV2Jugador();
			copia.LoadData(tag);
			Ok(copia.Progreso(GuiaV2Sistema.IdGuia).TareaMarcada(_tareaMarcada), "y se vuelve a leer igual (LoadData)");
		}

		private static void BajarScroll(float fraccion)
		{
			ContenidoGuiaV2 g = Guia;
			if (g?.ListaPrincipal?.Barra != null) {
				g.ListaPrincipal.Barra.ViewPosition = g.ListaPrincipal.GetTotalHeight() * fraccion;
			}
		}

		/// <summary>Parada de prueba para la marca sobre una zona con firma: el desierto de Desert
		/// Scourge en Calamity, la jungla en vanilla; si no existiera, la primera cuya primera
		/// ubicacion sea una zona con firma de casillas.</summary>
		private static string ParadaDeZona()
		{
			string preferida = GuiaV2Sistema.IdGuia == "calamity" ? "desert" : "jungla";
			if (GuiaV2Sistema.ParadaPorId(preferida) != null) return preferida;
			foreach (Parada p in GuiaV2Sistema.Doc.Paradas) {
				Zona z = p.Ubicaciones.Count > 0 && p.Ubicaciones[0].Tipo == "zona" ? GuiaV2Sistema.ZonaPorId(p.Ubicaciones[0].Id) : null;
				if (z != null && z.Firma.Tiles.Count > 0) return p.Id;
			}
			return GuiaV2Sistema.Doc.Paradas[0].Id;
		}

		private static string PrimeraRefFicha()
		{
			Parada p = GuiaV2Sistema.ParadaPorId(ParadaDeZona()) ?? GuiaV2Sistema.Doc.Paradas[0];
			return p.Necesitas.Count > 0 ? p.Necesitas[0].Ref : (p.Invocacion?.Objeto ?? "Terraria/Gel");
		}

		// ---- medidas: nada se sale de su caja ----------------------------------------------------

		private static void AuditarTexto(string contexto)
		{
			ContenidoGuiaV2 g = Guia;
			if (g == null) { Ok(false, contexto + ": la pestaña Guía está montada"); return; }
			int textos = 0, enlaces = 0, salidos = 0;
			float minEscalaPx = float.MaxValue;
			string peor = "";
			CalculatedStyle area = g.GetDimensions();
			g.ExecuteRecursively(e => {
				if (!(e is TextoRicoTk t) || t.Lineas == 0) return;
				CalculatedStyle d = t.GetInnerDimensions();
				// Solo lo que esta a la vista (dentro de la lista visible).
				if (d.Y + d.Height < area.Y || d.Y > area.Y + area.Height) return;
				textos++;
				enlaces += t.CuantosEnlaces;
				float alturaPx = t.AltoLinea * Main.UIScale;
				if (alturaPx < minEscalaPx) minEscalaPx = alturaPx;
				if (t.AnchoMaximoUsado > d.Width + 1f || d.X + Math.Min(t.AnchoMaximoUsado, d.Width) > area.X + area.Width + 1f) {
					salidos++;
					peor = t.TextoPlano;
				}
			});
			// Cabecera: botones de pestaña con el rotulo entero.
			int botonesMal = 0;
			g.ExecuteRecursively(e => {
				if (!(e is BotonTk b) || string.IsNullOrEmpty(b.Texto)) return;
				float ancho = FontAssets.MouseText.Value.MeasureString(b.Texto).X * b.EscalaTexto;
				if (ancho > b.GetDimensions().Width - 4f) { botonesMal++; peor = "boton \"" + b.Texto + "\""; }
			});
			bool ok = salidos == 0 && botonesMal == 0;
			L(contexto + ": " + textos + " textos a la vista, " + enlaces + " enlaces, linea mas baja " + minEscalaPx.ToString("0.0") +
				" px en pantalla -> " + (ok ? "OK: ningun texto se sale de su caja." : "NO CABE: " + salidos + " textos y " + botonesMal + " botones se salen (\"" + peor + "\")."));
			_comprobaciones++;
			if (!ok) _fallos++;
			bool legible = minEscalaPx >= 11f || textos == 0;
			Ok(legible, contexto + ": letra legible (la linea mas pequeña mide " + minEscalaPx.ToString("0.0") + " px reales)");
		}

		private static void AuditarCabecera(string contexto)
		{
			ContenidoGuiaV2 g = Guia;
			if (g == null) return;
			EtiquetaTk resumen = null;
			g.ExecuteRecursively(e => { if (resumen == null && e is EtiquetaTk et && et.Parent == g) resumen = et; });
			if (resumen == null) return;
			float ancho = FontAssets.MouseText.Value.MeasureString(resumen.TextoActual).X * resumen.EscalaTexto;
			Ok(ancho <= resumen.GetDimensions().Width + 1f, contexto + ": la línea de progreso de la cabecera cabe (" + ancho.ToString("0") + " de " +
				resumen.GetDimensions().Width.ToString("0") + " px, escala " + resumen.EscalaTexto.ToString("0.00") + ")");
		}

		private static void Terminar()
		{
			_terminada = true;
			L("RESUMEN: " + _comprobaciones + " comprobaciones, " + _fallos + " en rojo. Barridos de zona: " + UbicacionGuia.Barridos + ".");
			RegistroGuia.Linea(Terrakeep.LogTag + " AUTOPRUEBA GUIA V2 COMPLETA");
		}
	}
}
