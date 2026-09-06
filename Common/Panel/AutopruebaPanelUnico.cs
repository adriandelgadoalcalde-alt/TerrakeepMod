using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.UI.Panel;
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
	}
}
