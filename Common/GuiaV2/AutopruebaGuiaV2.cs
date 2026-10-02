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
			Esperar(180);
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
			Esperar(10);
			// La captura va en un paso APARTE: el back buffer es el fotograma anterior al clic y
			// salia el rotulo "activada" justo despues de desactivarla (captura real de F3b).
			Paso("ajustes: captura oculta", () => CapturarAjustesYCerrar("ajustes-marca-oculta"));
			Esperar(40);
			Paso("ajustes: comprobar oculta", () => ComprobarMarcaOculta(true));
			Paso("ajustes otra vez", () => PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Ajustes, "autoprueba guia v2"));
			Esperar(20);
			Paso("ajustes: mostrar", () => PulsarInterruptorMarca(true));
			Esperar(10);
			Paso("ajustes: captura visible", () => CapturarAjustesYCerrar("ajustes-marca-visible"));
			Esperar(40);
			Paso("ajustes: comprobar visible", () => ComprobarMarcaOculta(false));

			// ---- indicador de direccion en la pantalla de juego (punto 4 del encargo) ----
			Paso("hud: preparar", PrepararHud);
			foreach ((string Nombre, float Dx, float Dy) caso in new[] {
				("al-este", 110f, 0f), ("al-oeste", -110f, 0f), ("encima", 0f, -60f), ("debajo", 0f, 60f), ("al-noreste", 90f, -60f) }) {
				(string Nombre, float Dx, float Dy) c = caso;
				Paso("hud: jugador " + c.Nombre, () => PonerJugadorRespectoAlObjetivo(c.Dx, c.Dy));
				Esperar(25);
				Paso("hud: flecha " + c.Nombre, () => ComprobarFlecha(c.Nombre, c.Dx, c.Dy));
			}
			Paso("hud: en el objetivo", () => PonerJugadorRespectoAlObjetivo(0f, 0f));
			Esperar(25);
			Paso("hud: llegado", ComprobarLlegado);
			Paso("hud: ajustes", () => PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Ajustes, "autoprueba guia v2"));
			Esperar(20);
			Paso("hud: ocultar", () => PulsarInterruptorIndicador(false));
			Esperar(20);
			Paso("hud: comprobar oculto", () => ComprobarHudVisible(false, "con el indicador desactivado en Ajustes"));
			Paso("hud: ajustes 2", () => PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Ajustes, "autoprueba guia v2"));
			Esperar(20);
			Paso("hud: mostrar", () => PulsarInterruptorIndicador(true));
			Esperar(20);
			Paso("hud: comprobar visible", () => ComprobarHudVisible(true, "al volver a activarlo"));
			Paso("hud: lejos otra vez", () => PonerJugadorRespectoAlObjetivo(110f, -20f));
			Esperar(25);
			Paso("hud: junto al personaje", () => { FijarPosicionIndicador(PosicionIndicadorGuia.JuntoAlPersonaje); });
			Esperar(15);
			Paso("hud: comprobar junto al personaje", ComprobarJuntoAlPersonaje);
			Paso("hud: tamaño 150", () => { FijarPosicionIndicador(PosicionIndicadorGuia.Automatica); AjustesConfig.Instance.TamanoIndicador = 150; });
			Esperar(15);
			Paso("hud: comprobar tamaño", ComprobarTamano150);
			Paso("hud: clic", ClicEnIndicador);
			Esperar(30);
			Paso("hud: comprobar clic", ComprobarClicIndicador);
			Esperar(20);

			// ---- la marca se actualiza sola al completar paradas ----
			Paso("hasta la mazmorra", () => LlevarHastaParada("skeletron"));
			Esperar(40);
			Paso("marca mazmorra", () => ComprobarMarcaSiguiente("skeletron"));
			Paso("hud: nuevo destino", ComprobarNuevoDestino);
			Paso("mapa mazmorra", () => AbrirMapaEn(UbicacionGuia.Siguiente));
			Esperar(180);
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
			Paso("captura Mi guía", () => { AuditarTexto("mi-guia"); AuditarEncuadre("mi-guia"); Capturar("mi-guia"); });
			Paso("clic en objeto que falta", ClicEnObjetoQueFalta);
			Esperar(30);
			Paso("ficha", ComprobarFicha);
			Paso("clic en ingrediente", ClicEnIngredienteDeLaFicha);
			Esperar(30);
			Paso("ficha ingrediente", () => {
				// Solo cuenta si de verdad se pulso un ingrediente: antes daba OK aunque el paso se
				// hubiera saltado (ficha sin receta) porque la ventana abierta era la del objeto.
				if (_ingredientePulsado == null) { L("NO APLICA: la ficha del objeto que falta no tiene ingredientes que recorrer."); return; }
				Ok(Guia != null && Guia.Ventana != null && Guia.FichaObjetoAbierta == _ingredientePulsado,
					"la ficha del ingrediente " + _ingredientePulsado + " se abre desde la ficha (se recorre la cadena)");
				Capturar("ficha-ingrediente");
			});
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
					// UIScale a 1 ANTES de cambiar de resolucion (misma leccion que AutopruebaEspaciado: con la
					// escala al maximo el motor devuelve la resolucion en unidades de interfaz). El panel se
					// deja ABIERTO a proposito: tiene que recolocarse solo (PanelTerrakeepState.AjustarAltoMaximo).
					Main.UIScale = 1f;
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
					Paso("mi guia " + ancho + e, () => AbrirGuia(ContenidoGuiaV2.Vista.MiGuia));
					Esperar(40);
					Paso("medir mi guia " + ancho + e, () => {
						string s = ancho + "x" + alto + "-" + e; // la resolucion pedida: dentro de UpdateUI Main.screenWidth es la pantalla LOGICA (con UIScale maximo todas salian "1066x600-max" y se pisaban)
						AuditarTexto("mi-guia-" + s);
						AuditarCabecera("mi-guia-" + s);
						AuditarEncuadre("mi-guia-" + s);
						Capturar("mi-guia-" + s);
					});
					Paso("ruta " + ancho + e, () => AbrirGuia(ContenidoGuiaV2.Vista.Ruta));
					Esperar(40);
					Paso("medir ruta " + ancho + e, () => {
						string s = ancho + "x" + alto + "-" + e;
						AuditarTexto("ruta-" + s);
						Capturar("ruta-" + s);
						Guia.AbrirFichaObjeto(PrimeraRefFicha(), 1);
					});
					Esperar(30);
					Paso("medir ficha " + ancho + e, () => {
						string s = ancho + "x" + alto + "-" + e;
						AuditarTexto("ficha-" + s);
						Capturar("ficha-" + s);
						Guia.Ventana?.Cerrar();
					});
					Paso("libreria " + ancho + e, () => PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Libreria, "autoprueba guia v2"));
					Esperar(30);
					Paso("medir libreria " + ancho + e, () => { string s = ancho + "x" + alto + "-" + e; AuditarLibreria("libreria-" + s); Capturar("libreria-" + s); });
					Paso("hud sin inventario " + ancho + e, () => { PanelTerrakeepSystem.CerrarPanel("autoprueba guia v2: indicador"); Main.playerInventory = false; IndicadorGuiaHud.FotogramasDibujado = 0; });
					Esperar(20);
					Paso("medir hud " + ancho + e, () => { string s = ancho + "x" + alto + "-" + e; AuditarHud("hud-" + s, false); Capturar("hud-" + s); });
					Paso("hud con inventario " + ancho + e, () => { Main.playerInventory = true; IndicadorGuiaHud.FotogramasDibujado = 0; });
					Esperar(20);
					Paso("medir hud inventario " + ancho + e, () => { string s = ancho + "x" + alto + "-" + e; AuditarHud("hud-inventario-" + s, true); Capturar("hud-inventario-" + s); Main.playerInventory = false; });
				}
			}
			Paso("restaurar resolucion", () => {
				Main.UIScale = _uiScaleAntes > 0f ? _uiScaleAntes : 1f;
				if (_resAnchoAntes > 0) Main.SetDisplayMode(_resAnchoAntes, _resAltoAntes, false);
			});
			Esperar(30);
			Paso("restaurar estado", RestaurarEstado);
			Paso("restaurar jugador", RestaurarHud);
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
			// Solo en la autoprueba (personaje del sandbox): se revela el mapa alrededor del objetivo
			// para que la captura ENSEÑE lo que hay debajo de la marca (jungla, Mazmorra...). Con el
			// mapa sin explorar la captura salia negra y no probaba nada a la vista.
			int rx = 110, ry = 70, reveladas = 0;
			for (int x = Math.Max(0, (int)o.Tile.X - rx); x < Math.Min(Main.maxTilesX, (int)o.Tile.X + rx); x++) {
				for (int y = Math.Max(0, (int)o.Tile.Y - ry); y < Math.Min(Main.maxTilesY, (int)o.Tile.Y + ry); y++) {
					Main.Map.Update(x, y, 255);
					reveladas++;
				}
			}
			// refreshMap solo se atiende junto con updateMap (Main.cs:79015-79025 del decompilado):
			// vacia el registro de secciones pintadas y el mapa se repinta por secciones, 5 ms por
			// fotograma, empezando por las mas cercanas al jugador. De ahi la espera larga despues.
			Main.refreshMap = true;
			Main.updateMap = true;
			L("mapa revelado alrededor del objetivo (" + reveladas + " casillas, solo en el personaje de prueba) para que la captura enseñe el terreno real bajo la marca.");
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
			// Con el inventario abierto las ranuras de monedas/municion tapan el minimapa en las
			// resoluciones pequeñas y la flecha de la marca quedaba medio oculta en la captura.
			Main.playerInventory = false;
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
		}

		private static void CapturarAjustesYCerrar(string nombre)
		{
			ContenidoAjustes a = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Ajustes : null;
			if (a != null && a.BotonMarcaMapa != null) {
				L("rotulo del interruptor a la vista: \"" + a.BotonMarcaMapa.Texto + "\" (activo=" + a.BotonMarcaMapa.Activo + ").");
			}
			Capturar(nombre);
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
			// Se prefiere un objeto que falta y SALE DE UNA RECETA, para recorrer tambien la cadena
			// ficha -> ingrediente; si no hay ninguno, el primero que falte.
			ObjetoFilaTk fila = null, conReceta = null;
			Guia?.ExecuteRecursively(e => {
				if (!(e is ObjetoFilaTk f) || f.LoTienes) return;
				if (fila == null) fila = f;
				if (conReceta == null && TieneReceta(GuiaV2Sistema.TipoObjeto(f.Ref))) conReceta = f;
			});
			if (conReceta != null) fila = conReceta;
			if (fila == null) { Ok(false, "hay un objeto que falta en \"Lo que te falta\" para pulsarlo"); return; }
			_refPulsada = fila.Ref;
			CalculatedStyle d = fila.GetDimensions();
			L("clic real en la fila del objeto que falta: " + fila.Ref + " (\"" + GuiaV2Sistema.NombreObjeto(fila.Ref) + "\", tienes " + fila.Tienes + ").");
			fila.LeftClick(new UIMouseEvent(fila, new Vector2(d.X + d.Width / 2f, d.Y + d.Height / 2f)));
		}

		private static bool TieneReceta(int tipo)
		{
			if (tipo <= 0) return false;
			for (int i = 0; i < Recipe.numRecipes; i++) {
				if (Main.recipe[i] != null && Main.recipe[i].createItem != null && Main.recipe[i].createItem.type == tipo) return true;
			}
			return false;
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

		private static string _ingredientePulsado;

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
			_ingredientePulsado = null;
			if (conEnlace == null) { L("la ficha no tiene ingredientes enlazados (no sale de receta): se salta el paso."); return; }
			_ingredientePulsado = conEnlace.ValorDeEnlace(indice);
			L("se pulsa el ingrediente " + _ingredientePulsado + " dentro de la ficha.");
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
			AuditarLibreria("libreria-con-objeto");
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

		/// <summary>El marco del panel entero cabe en la pantalla y esta centrado. AuditarTexto mide
		/// cada texto contra la caja del panel, asi que un panel descolocado (centrado en la
		/// pantalla ANTERIOR tras cambiar resolucion/UIScale con el abierto) le pasaba por alto:
		/// visto en las capturas reales de F3b.</summary>
		private static void AuditarEncuadre(string contexto)
		{
			UIElement marco = Guia;
			while (marco != null && marco.Parent != null && !(marco.Parent is UIState)) marco = marco.Parent;
			if (marco == null || !(marco.Parent is UIState)) { Ok(false, contexto + ": se encuentra el marco del panel"); return; }
			CalculatedStyle d = marco.GetDimensions();
			// Aqui (UpdateUI) Main.screenWidth/Height son la pantalla LOGICA (divididas por UIScale).
			float an = Main.screenWidth, al = Main.screenHeight;
			bool cabe = d.X >= -1f && d.Y >= -1f && d.X + d.Width <= an + 1f && d.Y + d.Height <= al + 1f;
			float descentrado = Math.Abs((d.X + d.Width / 2f) - an / 2f);
			Ok(cabe && descentrado <= 2f, contexto + ": el panel cabe en la pantalla y está centrado (marco " + (int)d.X + "," + (int)d.Y + " " +
				(int)d.Width + "x" + (int)d.Height + " en pantalla lógica " + (int)an + "x" + (int)al + ", descentrado " + descentrado.ToString("0") + " px)");
			PanelTerrakeepState p = PanelTerrakeepSystem.Panel;
			if (p != null && p.AyudaPie != null && p.BotonCerrar != null) {
				float finTexto = p.AyudaPie.GetDimensions().X + FontAssets.MouseText.Value.MeasureString(p.AyudaPie.TextoActual).X * p.AyudaPie.EscalaTexto;
				float inicioBoton = p.BotonCerrar.GetDimensions().X;
				Ok(finTexto <= inicioBoton, contexto + ": la ayuda del pie termina antes del botón Cerrar (" + finTexto.ToString("0") + " <= " +
					inicioBoton.ToString("0") + ", escala " + p.AyudaPie.EscalaTexto.ToString("0.00") + ")");
			}
		}

		/// <summary>La Librería es adonde lleva "Coger en la Librería": sus ranuras de destino tienen
		/// que caer DENTRO del marco del panel (captura real de F3b a 800x720: la Mochila se salía
		/// por debajo).</summary>
		private static void AuditarLibreria(string contexto)
		{
			PanelTerrakeepState p = PanelTerrakeepSystem.Panel;
			if (p == null || p.Libreria == null) { Ok(false, contexto + ": la Librería está abierta"); return; }
			UIElement marco = p.Libreria;
			while (marco.Parent != null && !(marco.Parent is UIState)) marco = marco.Parent;
			CalculatedStyle m = marco.GetInnerDimensions();
			int ranuras = 0, fuera = 0;
			float peor = 0f;
			p.Libreria.ExecuteRecursively(el => {
				if (el.GetType().Name != "SlotObjetoVanilla") return;
				ranuras++;
				CalculatedStyle d = el.GetDimensions();
				float exceso = Math.Max(d.Y + d.Height - (m.Y + m.Height), d.X + d.Width - (m.X + m.Width));
				if (exceso > 1f) { fuera++; peor = Math.Max(peor, exceso); }
			});
			Ok(fuera == 0, contexto + ": las " + ranuras + " ranuras del contenedor elegido quedan dentro del panel (" + fuera + " fuera, la peor " + peor.ToString("0") + " px)");
		}

		// ---- indicador de direccion -------------------------------------------------------------

		private static Vector2 _posicionAntes;
		private static bool _indicadorAntes = true;
		private static PosicionIndicadorGuia _posicionIndicadorAntes;
		private static int _tamanoAntes = 100;

		private static void PrepararHud()
		{
			Player j = Main.LocalPlayer;
			_posicionAntes = j.position;
			AjustesConfig c = AjustesConfig.Instance;
			_indicadorAntes = c.IndicadorGuia;
			_posicionIndicadorAntes = c.PosicionIndicador;
			_tamanoAntes = c.TamanoIndicador;
			c.IndicadorGuia = true;
			c.PosicionIndicador = PosicionIndicadorGuia.Automatica;
			c.TamanoIndicador = 100;
			Main.playerInventory = false;
			// Caida de pluma: sin daño por caida y sin que el jugador se aleje mucho del punto en el
			// que se le pone (personaje del sandbox).
			j.AddBuff(BuffID.Featherfall, 60 * 60 * 5);
			L("indicador: destino = " + (UbicacionGuia.Siguiente != null ? UbicacionGuia.Siguiente.ParadaId + " en (" + (int)UbicacionGuia.Siguiente.Tile.X + ", " +
				(int)UbicacionGuia.Siguiente.Tile.Y + ")" : "-") + "; jugador en (" + (int)(j.Center.X / 16f) + ", " + (int)(j.Center.Y / 16f) + ").");
		}

		private static void RestaurarHud()
		{
			Player j = Main.LocalPlayer;
			j.position = _posicionAntes;
			j.velocity = Vector2.Zero;
			j.fallStart = (int)(j.position.Y / 16f);
			AjustesConfig c = AjustesConfig.Instance;
			c.IndicadorGuia = _indicadorAntes;
			c.PosicionIndicador = _posicionIndicadorAntes;
			c.TamanoIndicador = _tamanoAntes;
			c.SaveChanges();
		}

		/// <summary>Pone al jugador a (dx, dy) casillas del objetivo (dx &gt; 0 = al este, dy &gt; 0 =
		/// debajo).</summary>
		private static void PonerJugadorRespectoAlObjetivo(float dx, float dy)
		{
			UbicacionGuia.Objetivo o = UbicacionGuia.Siguiente;
			if (o == null) { Ok(false, "hay destino para el indicador"); return; }
			Player j = Main.LocalPlayer;
			Vector2 centro = IndicadorGuiaHud.CentroObjetivo(o) + new Vector2(dx, dy) * 16f;
			centro.X = MathHelper.Clamp(centro.X, 800f, Main.maxTilesX * 16f - 800f);
			centro.Y = MathHelper.Clamp(centro.Y, 800f, Main.maxTilesY * 16f - 800f);
			j.position = centro - new Vector2(j.width / 2f, j.height / 2f);
			j.velocity = Vector2.Zero;
			j.fallStart = (int)(j.position.Y / 16f);
			Main.playerInventory = false;
			PanelTerrakeepSystem.CerrarPanel("autoprueba guia v2: indicador");
			IndicadorGuiaHud.FotogramasDibujado = 0;
		}

		private static void ComprobarFlecha(string nombre, float dx, float dy)
		{
			UbicacionGuia.Objetivo o = UbicacionGuia.Siguiente;
			Player j = Main.LocalPlayer;
			Ok(IndicadorGuiaHud.FotogramasDibujado > 10 && IndicadorGuiaHud.Estado == "en_ruta",
				"indicador dibujado en la pantalla de juego (" + IndicadorGuiaHud.FotogramasDibujado + " fotogramas, estado " + IndicadorGuiaHud.Estado + ", " +
				IndicadorGuiaHud.UltimaColocacion + ")");
			if (o == null) return;
			// Direccion NOMINAL: el jugador esta a (dx, dy) del objetivo, asi que la flecha tiene que
			// apuntar a (-dx, -dy). Y la REAL, desde donde haya quedado el jugador tras caer un poco.
			float nominal = (float)Math.Atan2(-dy, -dx);
			Vector2 real = IndicadorGuiaHud.CentroObjetivo(o) - j.Center;
			float geometrico = (float)Math.Atan2(real.Y, real.X);
			float flecha = IndicadorGuiaHud.UltimoAngulo;
			float difNominal = Math.Abs(MathHelper.ToDegrees(MathHelper.WrapAngle(flecha - nominal)));
			float difReal = Math.Abs(MathHelper.ToDegrees(MathHelper.WrapAngle(flecha - geometrico)));
			Ok(difNominal < 25f && difReal < 3f, "jugador " + nombre + " del objetivo: la flecha apunta a " + MathHelper.ToDegrees(flecha).ToString("0") +
				"° (esperado " + MathHelper.ToDegrees(nominal).ToString("0") + "° por la colocacion, " + MathHelper.ToDegrees(geometrico).ToString("0") +
				"° desde donde ha quedado; 0° = este, 90° = abajo); distancia mostrada \"" + IndicadorGuiaHud.TextoDistancia(IndicadorGuiaHud.UltimaDistancia) +
				"\" (" + IndicadorGuiaHud.UltimaDistancia.ToString("0") + " casillas reales); destino \"" + IndicadorGuiaHud.UltimoNombre + "\"");
			List<string> pisa = PisaInterfaz(IndicadorGuiaHud.UltimoRectangulo, Main.playerInventory);
			Ok(pisa.Count == 0, "jugador " + nombre + ": el indicador (" + IndicadorGuiaHud.UltimoRectangulo + ", " + IndicadorGuiaHud.UltimaColocacion + ") " +
				(pisa.Count == 0 ? "no pisa la interfaz del juego (inventario, vida, minimapa, aire, sigilo)" : "PISA: " + string.Join(", ", pisa)));
			Capturar("hud-jugador-" + nombre);
		}

		private static void ComprobarLlegado()
		{
			Ok(IndicadorGuiaHud.Estado == "llegado", "en el objetivo el indicador dice que has llegado (estado " + IndicadorGuiaHud.Estado + ", " +
				IndicadorGuiaHud.UltimaDistancia.ToString("0") + " casillas)");
			string esperado = Idiomas.Texto("GuiaV2.Indicador.HasLlegado", IndicadorGuiaHud.UltimoNombre);
			Ok(IndicadorGuiaHud.UltimoAviso == esperado, "aviso breve al llegar: \"" + IndicadorGuiaHud.UltimoAviso + "\"");
			Capturar("hud-llegado");
		}

		private static void PulsarInterruptorIndicador(bool debeQuedar)
		{
			ContenidoAjustes a = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Ajustes : null;
			if (a == null || a.BotonIndicador == null) { Ok(false, "Ajustes abierto con su interruptor del indicador"); return; }
			BotonTk b = a.BotonIndicador;
			CalculatedStyle d = b.GetDimensions();
			b.LeftClick(new UIMouseEvent(b, new Vector2(d.X + d.Width / 2f, d.Y + d.Height / 2f)));
			Ok(AjustesConfig.Instance.IndicadorGuia == debeQuedar, "clic real en el interruptor del indicador en Ajustes: IndicadorGuia = " + AjustesConfig.Instance.IndicadorGuia);
			PanelTerrakeepSystem.CerrarPanel("autoprueba guia v2: indicador");
			IndicadorGuiaHud.FotogramasDibujado = 0;
		}

		private static void ComprobarHudVisible(bool visible, string cuando)
		{
			bool dibujado = IndicadorGuiaHud.FotogramasDibujado > 0;
			Ok(visible == dibujado, cuando + " el indicador " + (dibujado ? "se dibuja" : "no se dibuja") + " (" + IndicadorGuiaHud.FotogramasDibujado + " fotogramas)");
			Capturar(visible ? "hud-reactivado" : "hud-desactivado");
		}

		private static void FijarPosicionIndicador(PosicionIndicadorGuia p)
		{
			AjustesConfig.Instance.PosicionIndicador = p;
			IndicadorGuiaHud.FotogramasDibujado = 0;
		}

		private static void ComprobarJuntoAlPersonaje()
		{
			Rectangle r = IndicadorGuiaHud.UltimoRectangulo;
			Player jg = Main.LocalPlayer;
			Vector2 izq = Vector2.Transform(jg.TopLeft - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;
			Vector2 centro = Vector2.Transform(jg.Center - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;
			bool junto = r.Right <= izq.X - 10f && r.Right >= izq.X - 50f && Math.Abs(r.Center.Y - centro.Y) < 8f;
			Ok(junto, "con la posicion \"junto al personaje\" el indicador va a su izquierda, a media altura (caja " + r + ", borde izquierdo del personaje en " +
				(int)izq.X + ", centro en " + (int)centro.Y + ")");
			List<string> pisa = PisaInterfaz(r, Main.playerInventory);
			Ok(pisa.Count == 0, "junto al personaje: " + (pisa.Count == 0 ? "no pisa la interfaz del juego (inventario, vida, minimapa, aire, sigilo)" : "PISA: " + string.Join(", ", pisa)));
			Capturar("hud-junto-al-personaje");
		}

		private static void ComprobarTamano150()
		{
			Rectangle r = IndicadorGuiaHud.UltimoRectangulo;
			float esperado = (IndicadorGuiaHud.Esfera + IndicadorGuiaHud.HuecoTexto + IndicadorGuiaHud.AnchoTexto + IndicadorGuiaHud.Relleno * 2f) * 1.5f;
			Ok(Math.Abs(r.Width - esperado) <= 1f, "a tamaño 150 % el indicador mide " + r.Width + " px (esperado " + esperado.ToString("0") + "), colocado " + IndicadorGuiaHud.UltimaColocacion);
			Capturar("hud-tamano-150");
			AjustesConfig.Instance.TamanoIndicador = 100;
		}

		private static void ClicEnIndicador()
		{
			L("se pulsa el indicador (mismo camino que el clic: IndicadorGuiaHud.Pulsar).");
			IndicadorGuiaHud.Pulsar("autoprueba");
		}

		private static void ComprobarClicIndicador()
		{
			string id = UbicacionGuia.Siguiente != null ? UbicacionGuia.Siguiente.ParadaId : null;
			bool ok = PanelTerrakeepSystem.AreaAbierta == AreaTerrakeep.Guia && Guia != null && Guia.VistaActual == ContenidoGuiaV2.Vista.Ruta &&
				ContenidoGuiaV2.ParadaSeleccionada == id;
			Ok(ok, "al pulsar el indicador se abre la parada " + id + " en Guía > Ruta (área " + PanelTerrakeepSystem.AreaAbierta + ", vista " +
				(Guia != null ? Guia.VistaActual.ToString() : "-") + ", parada " + ContenidoGuiaV2.ParadaSeleccionada + ")");
			Capturar("hud-clic-abre-parada");
			PanelTerrakeepSystem.CerrarPanel("autoprueba guia v2: indicador");
		}

		private static void ComprobarNuevoDestino()
		{
			string esperado = Idiomas.Texto("GuiaV2.Indicador.NuevoDestino", IndicadorGuiaHud.NombreCorto(UbicacionGuia.Siguiente));
			Ok(IndicadorGuiaHud.UltimoAviso == esperado, "al cambiar la siguiente parada el indicador avisa: \"" + IndicadorGuiaHud.UltimoAviso + "\" (esperado \"" + esperado + "\")");
			AuditarHud("hud-nuevo-destino", Main.playerInventory);
			Capturar("hud-nuevo-destino");
		}

		/// <summary>El indicador se ve, cabe en la pantalla y no pisa la interfaz de vanilla: barra
		/// rapida o inventario abierto (con monedas y municion), vida y mana, y minimapa. Medidas del
		/// codigo decompilado (ver la cabecera de IndicadorGuiaHud).</summary>
		/// <summary>Zonas de interfaz que se pintan junto al PERSONAJE: el medidor de aire (formula de
		/// Main.DrawInterface_Resources_Breath: Top - 100, o a la altura de los pies con el inventario
		/// abierto en pantallas de menos de 1000 de alto; burbujas en val + (26·i - 125, 32), dos filas
		/// de 10) y, con Calamity, su barra de sigilo (50,1 %, 55,8 % por defecto).</summary>
		private static List<string> PisaInterfaz(Rectangle r, bool inventario)
		{
			List<string> pisa = new List<string>();
			int an = Main.screenWidth, al = Main.screenHeight;
			Rectangle izquierdaArriba = inventario ? new Rectangle(0, 0, 572, 300) : new Rectangle(0, 0, 500, 74);
			if (r.Intersects(izquierdaArriba)) pisa.Add(inventario ? "inventario" : "barra rapida");
			if (r.Intersects(new Rectangle(an - 330, 0, 330, 86))) pisa.Add("vida/mana");
			if (Main.mapEnabled && Main.mapStyle == 1 && r.Intersects(new Rectangle(an - 52 - 240 - 12, 90 - 12, 240 + 24, 240 + 24))) pisa.Add("minimapa");
			// Fila de iconos que el juego pinta a la izquierda de la vida con el inventario abierto
			// (medida en las capturas reales; ver IndicadorGuiaHud.ArribaConInventario).
			if (inventario && r.Intersects(new Rectangle(an - 456, 36, 170, 38))) pisa.Add("iconos junto a la vida");
			Player p = Main.LocalPlayer;
			Vector2 v = p.Top + new Vector2(0f, p.gfxOffY);
			if (inventario && al < 1000) v.Y += p.height - 20;
			v = Vector2.Transform(v - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
			if (!inventario || al >= 1000) v.Y -= 100f;
			v /= Main.UIScale;
			Rectangle aire = new Rectangle((int)v.X - 125, (int)v.Y + 32, 260, 52);
			if (r.Intersects(aire)) pisa.Add("medidor de aire " + aire);
			if (CatalogoGuia.HayCalamity) {
				Rectangle sigilo = new Rectangle((int)(an * 0.501f) - 60, (int)(al * 0.5577f) - 14, 120, 28);
				if (r.Intersects(sigilo)) pisa.Add("barra de sigilo de Calamity " + sigilo);
			}
			return pisa;
		}

		private static void AuditarHud(string contexto, bool inventario)
		{
			Rectangle r = IndicadorGuiaHud.UltimoRectangulo;
			int an = Main.screenWidth, al = Main.screenHeight;
			bool visto = IndicadorGuiaHud.FotogramasDibujado > 5;
			bool dentro = r.X >= 0 && r.Y >= 0 && r.Right <= an && r.Bottom <= al;
			List<string> pisa = PisaInterfaz(r, inventario);
			Ok(visto && dentro && pisa.Count == 0, contexto + ": indicador en " + r + " (" + IndicadorGuiaHud.UltimaColocacion + ") en pantalla logica " + an + "x" + al +
				(inventario ? " con inventario abierto" : "") + "; " + (pisa.Count == 0 ? "no pisa la interfaz del juego" : "PISA: " + string.Join(", ", pisa)) +
				"; destino \"" + IndicadorGuiaHud.UltimoNombre + "\", " + (visto ? "dibujado" : "SIN DIBUJAR"));
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
