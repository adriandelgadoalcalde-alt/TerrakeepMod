using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Builds;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Builds;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// Verificacion final de la fusion: recorre las SEIS pestañas del panel unico con clics reales
	/// en la barra, comprueba la animacion de los botones en dos pestañas distintas, comprueba el
	/// icono del HUD (rectangulo real en pantalla + clic real) y comprueba que las seis teclas
	/// abren el panel directamente en su pestaña.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Es una maquina de estados con esperas explicitas, no una funcion que lo hace todo de una:
	/// casi nada de lo que hay que comprobar aqui es cierto en el mismo fotograma en que se pide
	/// (un elemento recien montado no tiene geometria real hasta que pasa por <c>Recalculate</c> y
	/// se dibuja, y la animacion de un boton tarda 8 fotogramas en llegar a su tope). Es la misma
	/// leccion que dejo escrita WS6.
	/// </para>
	/// <para>
	/// Todo lo que acciona pasa por el camino de produccion: las pestañas con
	/// <c>UIElement.LeftClick</c> sobre el boton real, el icono del HUD por su comprobacion de
	/// rectangulo y su misma funcion de pulsado, y los atajos rellenando
	/// <c>PlayerInput.Triggers.JustPressed</c> y llamando al <c>ComprobarAtajos</c> de produccion
	/// (mismo metodo que corre en cada fotograma), tal como hizo WS7.
	/// </para>
	/// </remarks>
	public static class AutopruebaPanelUnico
	{
		/// <summary>Variable de entorno que la activa.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_PANEL";

		private const int FotogramasEntrePasos = 12;
		private const int FotogramasDeEspera = 180;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		private static float _escalaAntes;
		private static BotonTk _botonEnPrueba;
		private static int _fotogramasIconoAlEmpezar;

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
				Paso(_paso++);
			}
			catch (Exception e) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); break;

				// --- Las seis pestañas, una a una, con un clic REAL en la barra -----------------
				case 1: AbrirPestanaConClic(AreaTerrakeep.Personaje); break;
				case 2: MedirPestana(AreaTerrakeep.Personaje); break;
				case 3: AbrirPestanaConClic(AreaTerrakeep.Libreria); break;
				case 4: MedirPestana(AreaTerrakeep.Libreria); break;
				case 5: AbrirPestanaConClic(AreaTerrakeep.Builds); break;
				case 6: MedirPestana(AreaTerrakeep.Builds); break;
				case 7: AbrirPestanaConClic(AreaTerrakeep.Investigacion); break;
				case 8: MedirPestana(AreaTerrakeep.Investigacion); break;
				case 9: AbrirPestanaConClic(AreaTerrakeep.Exploracion); break;
				case 10: MedirPestana(AreaTerrakeep.Exploracion); break;
				case 11: AbrirPestanaConClic(AreaTerrakeep.Ajustes); break;
				case 12: MedirPestana(AreaTerrakeep.Ajustes); break;

				// --- Animacion de los botones, en dos pestañas distintas ------------------------
				case 13: EmpezarHover("Ajustes"); break;
				case 14: MedirHover("Ajustes"); break;
				case 15: TerminarHover("Ajustes"); break;
				case 16: AbrirPestanaConClic(AreaTerrakeep.Personaje); break;
				case 17: EmpezarHover("Personaje"); break;
				case 18: MedirHover("Personaje"); break;
				case 19: TerminarHover("Personaje"); break;

				// --- Los atajos de teclado, uno por uno ----------------------------------------
				case 20: ProbarAtajo(AreaTerrakeep.Libreria); break;
				case 21: ProbarAtajo(AreaTerrakeep.Exploracion); break;
				case 22: ProbarAtajo(AreaTerrakeep.Investigacion); break;
				case 23: ProbarAtajo(AreaTerrakeep.Builds); break;
				case 24: ProbarAtajo(AreaTerrakeep.Ajustes); break;
				case 25: ProbarAtajo(AreaTerrakeep.Personaje); break;
				case 26: ProbarCierreConSuPropiaTecla(); break;

				// --- El icono del HUD -----------------------------------------------------------
				case 27: PrepararIcono(); break;
				case 28: MedirIcono(); break;
				case 29: ClicEnElIcono(); break;
				case 30: ComprobarPanelTrasElIcono(); break;

				// --- La vista previa del muñeco en Apariencia (con armadura / sin armadura) -----
				case 31: PoblarAparienciaDePrueba(); break;
				case 32: AbrirApariencia(); break;
				case 33: MedirYCapturarMuneco("con-armadura"); break;
				case 34: PulsarAlternadorArmadura(); break;
				case 35: MedirYCapturarMuneco("sin-armadura"); break;
				case 36: PulsarAlternadorArmadura(); break;
				case 37: MedirYCapturarMuneco("con-armadura-otra-vez"); break;

				// --- Codigo de build (idea 7 del catalogo de funciones): auto-equipar -> exportar
				// -> importar el MISMO codigo -> tiene que ser idempotente (nada que mover/crear la
				// segunda vez), mismo criterio que ya prueba "Auto-equipar" dos veces seguidas.
				case 38: AbrirPestanaConClic(AreaTerrakeep.Builds); break;
				case 39: PulsarAutoEquiparParaCodigo(); break;
				case 40: PulsarExportarCodigo(); break;
				case 41: PulsarImportarCodigo(); break;

				// --- DPS-metro (idea 2 del catalogo de funciones): chip de cabecera -----------------
				case 42: ComprobarChipDpsSinDatos(); break;
				case 43: RegistrarGolpesDePruebaEnMedidorDanio(); break;
				case 44: ComprobarChipDpsConDatos(); break;

				default: Terminar(); break;
			}
		}

		// -------------------------------------------------------------------------------------

		private static void Arrancar()
		{
			string marca = Environment.GetEnvironmentVariable("TERRAKEEP_PANEL_MARCA");
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL: " + Variable +
				" detectada. Marca de ejecucion: " + marca +
				". Jugador \"" + Main.LocalPlayer.name + "\", mundo \"" + Main.worldName +
				"\", resolucion " + Main.screenWidth + "x" + Main.screenHeight +
				", escala de interfaz " + Main.UIScale + ".");

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/0 - teclas REALES de las seis " +
				"pestañas, leidas del perfil de controles del juego: " + TeclasDeLasSeis());

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "autoprueba del panel unico");
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/0 - panel abierto=" +
				PanelTerrakeepSystem.PanelAbierto +
				", InGameUI.CurrentState=" + (Main.InGameUI.CurrentState != null
					? Main.InGameUI.CurrentState.GetType().FullName : "(null)"));
		}

		private static string TeclasDeLasSeis()
		{
			List<string> partes = new List<string>();
			for (int i = 0; i < PanelTerrakeepState.NombresDeArea.Length; i++) {
				partes.Add(PanelTerrakeepState.NombresDeArea[i] + "=[" +
					PanelTerrakeepSystem.TeclaDe((AreaTerrakeep)i) + "]");
			}
			return string.Join(", ", partes);
		}

		private static void AbrirPestanaConClic(AreaTerrakeep area)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL: el panel no esta abierto, " +
					"no se puede pulsar la pestaña \"" + PanelTerrakeepState.NombresDeArea[(int)area] + "\".");
				return;
			}

			string pulsada = panel.PulsarPestana(area);
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL - CLIC REAL en la pestaña " +
				pulsada + ". Pestaña activa ahora: \"" +
				PanelTerrakeepState.NombresDeArea[(int)PanelTerrakeepSystem.AreaAbierta] + "\" " +
				(PanelTerrakeepSystem.AreaAbierta == area ? "-> OK" : "-> NO CUADRA"));
		}

		private static void MedirPestana(AreaTerrakeep area)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				return;
			}

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL - pestaña \"" +
				PanelTerrakeepState.NombresDeArea[(int)area] + "\" ya dibujada: " +
				panel.InformeAreaActual());

			// Imagen real de la pestaña, para poder mirar la estetica y no solo contar elementos.
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL - " +
				CapturaDePantalla.Guardar("pestana-" + (int)area + "-" +
					Sanear(PanelTerrakeepState.NombresDeArea[(int)area])));
		}

		/// <summary>Nombre de archivo sin tildes ni espacios.</summary>
		private static string Sanear(string texto)
		{
			string normal = texto.Normalize(System.Text.NormalizationForm.FormD);
			System.Text.StringBuilder salida = new System.Text.StringBuilder();
			foreach (char c in normal) {
				if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) ==
					System.Globalization.UnicodeCategory.NonSpacingMark) {
					continue;
				}
				salida.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
			}
			return salida.ToString();
		}

		// -------------------------------------------------------------------------------------
		// Animacion de los botones
		// -------------------------------------------------------------------------------------

		/// <summary>
		/// Pone el raton "encima" de un boton por su ruta real: <c>UIElement.MouseOver</c> es lo
		/// que llama <c>UserInterface.Update_Inner</c> cuando el raton entra en un elemento, y es
		/// lo unico que pone <c>IsMouseHovering</c> a true (su setter es privado). A partir de ahi
		/// corre el <c>Update</c> de produccion del boton, un fotograma por vez, como en el juego.
		/// </summary>
		private static void EmpezarHover(string donde)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			_botonEnPrueba = panel != null ? panel.BuscarPrimero<BotonTk>() : null;
			if (_botonEnPrueba == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/animacion (" + donde +
					"): no se encontro ningun BotonTk.");
				return;
			}

			_escalaAntes = _botonEnPrueba.EscalaAnimada;
			CalculatedStyle dim = _botonEnPrueba.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			_botonEnPrueba.MouseOver(new UIMouseEvent(_botonEnPrueba, centro));

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/animacion (" + donde +
				") - raton ENCIMA de \"" + _botonEnPrueba.Texto + "\" (x=" + (int)dim.X + " y=" + (int)dim.Y +
				" " + (int)dim.Width + "x" + (int)dim.Height + "). IsMouseHovering=" +
				_botonEnPrueba.IsMouseHovering + ", escala antes=" + _escalaAntes.ToString("0.00") + ".");
		}

		private static void MedirHover(string donde)
		{
			if (_botonEnPrueba == null) {
				return;
			}

			float ahora = _botonEnPrueba.EscalaAnimada;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/animacion (" + donde + ") - tras " +
				FotogramasEntrePasos + " fotogramas con el raton encima: escala " +
				_escalaAntes.ToString("0.00") + " -> " + ahora.ToString("0.00") +
				", el marco se dibuja " + _botonEnPrueba.CrecimientoActual + " px mas grande. " +
				(ahora > _escalaAntes
					? "OK: la animacion ha CRECIDO (referencia real: Main.DrawSettingButton va de 0,80 a 0,96 a 0,02 por fotograma)."
					: "NO HA CRECIDO."));

			// Imagen del boton en su punto mas resaltado, para poder VER la animacion y no solo
			// leer el numero: el marco crece y aparece el borde claro.
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/animacion - " +
				CapturaDePantalla.Guardar("animacion-" + Sanear(donde)));
		}

		private static void TerminarHover(string donde)
		{
			if (_botonEnPrueba == null) {
				return;
			}

			float alSalir = _botonEnPrueba.EscalaAnimada;
			CalculatedStyle dim = _botonEnPrueba.GetDimensions();
			_botonEnPrueba.MouseOut(new UIMouseEvent(_botonEnPrueba,
				new Vector2(dim.X - 100f, dim.Y - 100f)));

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/animacion (" + donde +
				") - raton FUERA. Escala en el momento de salir=" + alSalir.ToString("0.00") +
				", IsMouseHovering=" + _botonEnPrueba.IsMouseHovering +
				". Se comprueba que vuelve sola en el paso siguiente.");
			_escalaAntes = alSalir;
		}

		// -------------------------------------------------------------------------------------
		// Atajos de teclado
		// -------------------------------------------------------------------------------------

		/// <summary>
		/// Simula la pulsacion de la tecla de un area rellenando la MISMA entrada de la que
		/// depende <c>ModKeybind.JustPressed</c> (<c>PlayerInput.Triggers.JustPressed.KeyStatus</c>
		/// indexado por el nombre completo del atajo) y llamando despues al
		/// <c>ComprobarAtajos</c> de produccion. Lo unico que no cubre es el ultimo eslabon fisico
		/// teclado -&gt; SDL, que WS7 ya midio que no se puede simular desde fuera.
		/// </summary>
		private static void ProbarAtajo(AreaTerrakeep area)
		{
			ModKeybind atajo = PanelTerrakeepSystem.AtajoDe(area);
			string clave = PanelTerrakeepSystem.ClaveDeAtajo(area);
			if (atajo == null || clave == null || !PlayerInput.Triggers.JustPressed.KeyStatus.ContainsKey(clave)) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/atajos: PlayerInput todavia no " +
					"conoce el atajo de \"" + PanelTerrakeepState.NombresDeArea[(int)area] + "\".");
				return;
			}

			AreaTerrakeep antes = PanelTerrakeepSystem.AreaAbierta;
			bool abiertoAntes = PanelTerrakeepSystem.PanelAbierto;

			PlayerInput.Triggers.JustPressed.KeyStatus[clave] = true;
			PanelTerrakeepSystem.ComprobarAtajos();
			PlayerInput.Triggers.JustPressed.KeyStatus[clave] = false;

			bool ok = PanelTerrakeepSystem.PanelAbierto && PanelTerrakeepSystem.AreaAbierta == area;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/atajos - tecla [" +
				PanelTerrakeepSystem.TeclaDe(area) + "] (" + clave + "): panel abierto antes=" +
				abiertoAntes + " en \"" + PanelTerrakeepState.NombresDeArea[(int)antes] + "\" -> ahora abierto=" +
				PanelTerrakeepSystem.PanelAbierto + " en \"" +
				PanelTerrakeepState.NombresDeArea[(int)PanelTerrakeepSystem.AreaAbierta] + "\" " +
				(ok ? "-> OK: salta a SU pestaña sin cerrar el panel." : "-> NO CUADRA."));
		}

		/// <summary>La otra mitad del comportamiento: la tecla del area que YA esta abierta cierra
		/// el panel, en vez de no hacer nada.</summary>
		private static void ProbarCierreConSuPropiaTecla()
		{
			AreaTerrakeep area = PanelTerrakeepSystem.AreaAbierta;
			ModKeybind atajo = PanelTerrakeepSystem.AtajoDe(area);
			string clave = PanelTerrakeepSystem.ClaveDeAtajo(area);
			if (atajo == null || clave == null || !PlayerInput.Triggers.JustPressed.KeyStatus.ContainsKey(clave)) {
				return;
			}

			PlayerInput.Triggers.JustPressed.KeyStatus[clave] = true;
			PanelTerrakeepSystem.ComprobarAtajos();
			PlayerInput.Triggers.JustPressed.KeyStatus[clave] = false;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/atajos - la tecla [" +
				PanelTerrakeepSystem.TeclaDe(area) + "] pulsada estando YA en \"" +
				PanelTerrakeepState.NombresDeArea[(int)area] + "\": panel abierto ahora=" +
				PanelTerrakeepSystem.PanelAbierto +
				(PanelTerrakeepSystem.PanelAbierto ? " -> NO CUADRA (deberia haberlo cerrado)." : " -> OK: lo cierra."));
		}

		// -------------------------------------------------------------------------------------
		// Icono del HUD
		// -------------------------------------------------------------------------------------

		private static void PrepararIcono()
		{
			// Con el panel abierto, la capa del icono NO se dibuja: "Vanilla: Fancy UI" (indice 14)
			// corta el recorrido de capas y el inventario es la 28. Es lo esperado y lo que se
			// aprovecha: el icono se ve exactamente cuando hace falta.
			PanelTerrakeepSystem.CerrarPanel("autoprueba: se cierra para ver el icono del HUD");
			Main.playerInventory = true;
			_fotogramasIconoAlEmpezar = IconoHudTerrakeep.FotogramasDibujado;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/icono - panel cerrado (abierto=" +
				PanelTerrakeepSystem.PanelAbierto + "), inventario abierto=" + Main.playerInventory +
				". Fotogramas en los que el icono se habia dibujado hasta ahora: " + _fotogramasIconoAlEmpezar + ".");
		}

		private static void MedirIcono()
		{
			int dibujados = IconoHudTerrakeep.FotogramasDibujado - _fotogramasIconoAlEmpezar;
			Rectangle zona = IconoHudTerrakeep.UltimoRectangulo;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/icono - el icono se ha dibujado en " +
				dibujados + " fotogramas con el panel cerrado. Rectangulo REAL en pantalla: " +
				IconoHudTerrakeep.Describir(zona) + " (resolucion " + Main.screenWidth + "x" + Main.screenHeight +
				"). Para comparar, los iconos vanilla de esa misma fila: bestiario (498,278,30,30) y " +
				"emotes (534,278,30,30). " +
				(dibujados > 0 ? "OK: la capa del HUD corre de verdad." : "NO se ha llegado a dibujar."));

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/icono - " +
				CapturaDePantalla.Guardar("icono-hud"));
		}

		private static void ClicEnElIcono()
		{
			Rectangle zona = IconoHudTerrakeep.RectanguloAhora();
			int x = zona.X + zona.Width / 2;
			int y = zona.Y + zona.Height / 2;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/icono - " +
				IconoHudTerrakeep.ProbarClicEn(x, y));
		}

		private static void ComprobarPanelTrasElIcono()
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/icono - un fotograma despues: " +
				"panel abierto=" + PanelTerrakeepSystem.PanelAbierto +
				", pestaña=\"" + PanelTerrakeepState.NombresDeArea[(int)PanelTerrakeepSystem.AreaAbierta] + "\"" +
				", veces pulsado el icono=" + IconoHudTerrakeep.VecesPulsado +
				", InGameUI.CurrentState=" + (Main.InGameUI.CurrentState != null
					? Main.InGameUI.CurrentState.GetType().Name : "(null)") +
				". Con el panel abierto el icono deja de dibujarse a proposito (la capa " +
				"\"Vanilla: Fancy UI\" corta el recorrido antes de llegar al inventario).");
		}

		private static void Terminar()
		{
			_terminada = true;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL COMPLETA.");
		}

		// -------------------------------------------------------------------------------------
		// Vista previa del muñeco en Apariencia
		// -------------------------------------------------------------------------------------

		/// <summary>
		/// El personaje sintetico de pruebas (<c>TerrakeepPrueba</c>) llega con los siete colores
		/// a <c>(0,0,0)</c> y sin nada equipado: la vista previa saldria una silueta negra igual
		/// con y sin armadura, sin demostrar nada. Se le ponen colores vivos y una pieza real de
		/// cabeza y de cuerpo (buscadas por propiedades, no por id fijo, igual que
		/// <c>AutopruebaPersonaje.PoblarEquipo</c>) para que la diferencia SE VEA en las capturas.
		/// </summary>
		private static void PoblarAparienciaDePrueba()
		{
			Player jugador = Main.LocalPlayer;

			jugador.hairColor = new Color(210, 60, 40);
			jugador.skinColor = new Color(255, 200, 150);
			jugador.eyeColor = new Color(30, 140, 230);
			jugador.shirtColor = new Color(40, 170, 80);
			jugador.underShirtColor = new Color(230, 210, 60);
			jugador.pantsColor = new Color(70, 90, 200);
			jugador.shoeColor = new Color(120, 70, 30);

			int cabeza = BuscarObjeto(o => o.headSlot >= 0 && o.defense > 0);
			int cuerpo = BuscarObjeto(o => o.bodySlot >= 0 && o.defense > 0);
			int piernas = BuscarObjeto(o => o.legSlot >= 0 && o.defense > 0);
			PonerObjeto(jugador.armor, 0, cabeza);
			PonerObjeto(jugador.armor, 1, cuerpo);
			PonerObjeto(jugador.armor, 2, piernas);

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/muñeco - apariencia de prueba " +
				"puesta en vivo: colores no nulos y armor[0..2]=" +
				PersonajeVivo.DescribirObjeto(jugador.armor[0]) + " / " +
				PersonajeVivo.DescribirObjeto(jugador.armor[1]) + " / " +
				PersonajeVivo.DescribirObjeto(jugador.armor[2]) + ".");
		}

		private static int BuscarObjeto(Func<Item, bool> condicion)
		{
			for (int tipo = 1; tipo < Terraria.ModLoader.ItemLoader.ItemCount; tipo++) {
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

		private static void PonerObjeto(Item[] equipo, int indice, int tipo)
		{
			if (tipo <= 0 || indice < 0 || indice >= equipo.Length) {
				return;
			}
			Item objeto = new Item();
			objeto.SetDefaults(tipo);
			equipo[indice] = objeto;
		}

		/// <summary>Abre el panel directamente en Personaje -> Apariencia, sin depender de en
		/// que pestaña lo hayan dejado los pasos anteriores.</summary>
		private static void AbrirApariencia()
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje,
				"autoprueba del panel unico: vista previa de Apariencia");

			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			if (personaje == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/muñeco: no se encontro " +
					"ContenidoPersonaje tras abrir el area.");
				return;
			}

			// Indice 4 = Apariencia, mismo orden que ContenidoPersonaje.ClavesPestana.
			personaje.IrAPestana(4);
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/muñeco - abierta la sub-pestaña \"" +
				personaje.NombrePestanaActual + "\".");
		}

		/// <summary>Mide el <see cref="MunecoTk"/> ya dibujado (geometria real, no recien
		/// construida) y deja una captura real del back buffer con el nombre indicado.</summary>
		private static void MedirYCapturarMuneco(string sufijo)
		{
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			MunecoTk muneco = personaje != null ? personaje.BuscarPrimero<MunecoTk>() : null;

			if (muneco == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/muñeco: no se encontro " +
					"ningun MunecoTk en la pestaña abierta.");
				return;
			}

			CalculatedStyle dim = muneco.GetDimensions();
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/muñeco (" + sufijo + ") - " +
				"MunecoTk dibujado en x=" + (int)dim.X + " y=" + (int)dim.Y + " " +
				(int)dim.Width + "x" + (int)dim.Height + ", ConArmadura=" + muneco.ConArmadura + ". " +
				CapturaDePantalla.Guardar("apariencia-muneco-" + sufijo));
		}

		/// <summary>Pulsa de verdad el <see cref="AlternadorTk"/> de "con armadura" con su ruta
		/// real (<c>UIElement.LeftClick</c>, la misma que dispara un clic de raton de verdad),
		/// igual que WS1 ya hizo con el deslizador de color.</summary>
		private static void PulsarAlternadorArmadura()
		{
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			AlternadorTk alternador = personaje != null ? personaje.BuscarPrimero<AlternadorTk>() : null;

			if (alternador == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/muñeco: no se encontro " +
					"ningun AlternadorTk en la pestaña abierta.");
				return;
			}

			bool antes = alternador.Valor;
			CalculatedStyle dim = alternador.GetDimensions();
			alternador.LeftClick(new UIMouseEvent(alternador,
				new Vector2(dim.X + dim.Width * 0.5f, dim.Y + dim.Height * 0.5f)));

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/muñeco - clic REAL en \"" +
				alternador.EtiquetaActual + "\". Valor antes=" + antes + " -> despues=" + alternador.Valor +
				" " + (antes != alternador.Valor ? "-> OK: ha cambiado." : "-> NO HA CAMBIADO."));
		}

		// -------------------------------------------------------------------------------------
		// Codigo de build (idea 7 del catalogo de funciones): auto-equipar -> exportar -> importar
		// el MISMO codigo tiene que ser idempotente, mismo criterio ya probado por WS4 con
		// "Auto-equipar" dos veces seguidas.
		// -------------------------------------------------------------------------------------

		private static ContenidoBuilds BuildsAbierto()
		{
			return PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Builds : null;
		}

		/// <summary>Rellena el equipo real con "Auto-equipar" para tener algo de verdad que
		/// exportar (el personaje de prueba arranca sin nada puesto).</summary>
		private static void PulsarAutoEquiparParaCodigo()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			ContenidoBuilds builds = BuildsAbierto();
			if (panel == null || builds == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/codigo: no se encontro " +
					"ContenidoBuilds en la pestaña abierta.");
				return;
			}

			string pulsado = panel.PulsarBoton(Idiomas.Texto("Builds.AutoEquipar"));
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/codigo - CLIC REAL en \"" +
				Idiomas.Texto("Builds.AutoEquipar") + "\" (" + (pulsado ?? "NO ENCONTRADO") +
				") para tener equipo real que exportar. Resultado: " + builds.MensajeResultado);
		}

		/// <summary>Pulsa "Exportar" de verdad y deja el codigo resultante en el log (para poder
		/// copiarlo a mano si algun dia hace falta reproducir algo con el).</summary>
		private static void PulsarExportarCodigo()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			ContenidoBuilds builds = BuildsAbierto();
			if (panel == null || builds == null) {
				return;
			}

			string pulsado = panel.PulsarBoton(Idiomas.Texto("Builds.Codigo.Exportar"));
			string codigo = builds.CodigoParaPrueba;
			bool pareceValido = !string.IsNullOrEmpty(codigo) && codigo.StartsWith("TKBUILD1:");

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/codigo - CLIC REAL en \"" +
				Idiomas.Texto("Builds.Codigo.Exportar") + "\" (" + (pulsado ?? "NO ENCONTRADO") +
				"). Codigo en el cuadro (" + (codigo?.Length ?? 0) + " caracteres): \"" + codigo + "\" " +
				(pareceValido ? "-> OK: empieza por TKBUILD1:" : "-> NO CUADRA (no parece un codigo valido)") +
				". Mensaje de resultado: " + builds.MensajeResultado);
		}

		/// <summary>
		/// Pulsa "Importar" de verdad con el MISMO codigo que se acaba de exportar (sigue en el
		/// cuadro de texto). Como el equipo activo ya es exactamente ese, el resultado tiene que
		/// decir "ya puestos" en todo y CERO creados/movidos/sin-sitio/no-reconocidos - la misma
		/// idempotencia real que ya prueba WS4 aplicando dos veces seguidas la misma build del
		/// catalogo.
		/// </summary>
		private static void PulsarImportarCodigo()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			ContenidoBuilds builds = BuildsAbierto();
			if (panel == null || builds == null) {
				return;
			}

			string pulsado = panel.PulsarBoton(Idiomas.Texto("Builds.Codigo.Importar"));
			string mensaje = builds.MensajeResultado ?? "";

			// Numeros REALES del resultado, no el texto ya traducido (que en esta partida de prueba
			// esta en ingles: "created=0, moved=0..." - parsear el texto en español habria dado un
			// falso "NO CUADRA" con el juego en otro idioma, que es justo lo que paso la primera vez
			// que se escribio esta autoprueba, ver bitacora.md).
			ResultadoImportarCodigo resultado = builds.UltimoResultadoCodigoParaPrueba;
			bool idempotente = resultado != null && resultado.Ok &&
				resultado.Creados == 0 && resultado.Movidos == 0;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/codigo - CLIC REAL en \"" +
				Idiomas.Texto("Builds.Codigo.Importar") + "\" (" + (pulsado ?? "NO ENCONTRADO") +
				") con el MISMO codigo que se acaba de exportar. Resultado: \"" + mensaje + "\" (creados=" +
				(resultado?.Creados ?? -1) + " movidos=" + (resultado?.Movidos ?? -1) + " ya_puestos=" +
				(resultado?.YaColocados ?? -1) + ") " +
				(idempotente ? "-> OK: importar lo que ya llevabas puesto no crea ni mueve nada." :
					"-> NO CUADRA (deberia ser idempotente)."));
		}

		// -------------------------------------------------------------------------------------
		// DPS-metro (idea 2 del catalogo de funciones): el chip de cabecera lee MedidorDanio en
		// vivo, visible desde cualquier pestaña, no solo Builds.
		// -------------------------------------------------------------------------------------

		/// <summary>Antes de registrar ningun golpe, el chip tiene que decir "sin datos" y no un
		/// falso "0 DPS" (ver el XMLdoc de <see cref="MedidorDanio.HayDatosRecientes"/>).</summary>
		private static void ComprobarChipDpsSinDatos()
		{
			MedidorDanio.ReiniciarParaPrueba();
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			string texto = panel != null ? panel.ChipDpsParaPrueba : null;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/dps - chip SIN golpes registrados: \"" +
				texto + "\" (esperado: \"" + Idiomas.Texto("Panel.Cabecera.DpsSinDatos") + "\") " +
				(texto == Idiomas.Texto("Panel.Cabecera.DpsSinDatos") ? "-> OK" : "-> NO CUADRA"));
		}

		/// <summary>
		/// Registra tres golpes de daño CONOCIDO (100+150+200=450) directamente en
		/// <see cref="MedidorDanio"/> - ver el XMLdoc de <see cref="MedidorDanio.RegistrarGolpeParaPrueba"/>
		/// para el porque de no fabricar un combate real. Con la ventana de 10s completa,
		/// 450/10=45.0 DPS exactos.
		/// </summary>
		private static void RegistrarGolpesDePruebaEnMedidorDanio()
		{
			MedidorDanio.RegistrarGolpeParaPrueba(100);
			MedidorDanio.RegistrarGolpeParaPrueba(150);
			MedidorDanio.RegistrarGolpeParaPrueba(200);

			float dps = MedidorDanio.DpsUltimos10s();
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/dps - 3 golpes registrados " +
				"(100+150+200=450 de daño): MedidorDanio.DpsUltimos10s()=" + dps.ToString("0.0") +
				" (esperado 45.0) " + (System.Math.Abs(dps - 45f) < 0.01f ? "-> OK" : "-> NO CUADRA") +
				". HayDatosRecientes()=" + MedidorDanio.HayDatosRecientes());
		}

		/// <summary>El chip de la cabecera tiene que reflejar esos 45 DPS UN fotograma despues (el
		/// refresco de los chips corre en cada <c>Update</c> del panel), visto desde la pestaña
		/// "Builds" en la que ha quedado abierto el panel - prueba real de que el chip es visible
		/// desde CUALQUIER pestaña, no solo desde la Guia.</summary>
		private static void ComprobarChipDpsConDatos()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			string texto = panel != null ? panel.ChipDpsParaPrueba : null;
			string esperado = Idiomas.Texto("Panel.Cabecera.Dps", 45);

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA PANEL/dps - chip CON golpes registrados " +
				"(pestaña abierta: \"" + PanelTerrakeepState.NombresDeArea[(int)PanelTerrakeepSystem.AreaAbierta] +
				"\"): \"" + texto + "\" (esperado: \"" + esperado + "\") " +
				(texto == esperado ? "-> OK: visible desde cualquier pestaña." : "-> NO CUADRA"));

			MedidorDanio.ReiniciarParaPrueba();
		}
	}
}
