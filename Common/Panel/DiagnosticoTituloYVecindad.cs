using System;
using Terraria;
using Terraria.UI;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE PRUEBAS: reproduce en vivo dos bugs reales reportados por el usuario con
	/// capturas reales a SU configuracion exacta (config.json real del usuario, comprobado antes de
	/// escribir este arnes: <c>UIScale=1.4666667</c>, <c>2560x1377</c> ventana, no pantalla
	/// completa - nunca probado antes con un UIScale distinto de 1.0 en toda la sesion).
	/// <para />
	/// 1) El titulo "Terrakeep" se ve recortado como solo "keep" en la esquina superior izquierda,
	/// sin panel alrededor.
	/// <para />
	/// 2) Parpadeos reales en la pestaña Vecindad.
	/// </summary>
	public static class DiagnosticoTituloYVecindad
	{
		public const string Variable = "TERRAKEEP_DIAG_TITULO_VECINDAD";

		private const int FotogramasDeEspera = 180;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		/// <summary>
		/// Fase 3 (bug del titulo "Terrakeep" recortado a "keep", escenario (b) NO probado todavia
		/// por ningun diagnostico de la sesion: redimensionar la ventana EN VIVO con el panel ya
		/// abierto). El propio proceso del juego no puede simular un resize real de la ventana de
		/// Windows desde dentro - lo hace un proceso EXTERNO (pywinauto/SetWindowPos) en cuanto ve el
		/// marcador <see cref="MarcadorListoParaRedimensionar"/> en el log. Esta fase se limita a
		/// VIGILAR: registra <c>PanelTerrakeepState.MarcoDimensionesParaPrueba</c> fotograma a
		/// fotograma mientras dure, para pillar el instante exacto (si existe) en que el marco -o el
		/// titulo dentro de el- queda con una X negativa u otro valor que recortaria el lado
		/// IZQUIERDO del rotulo por fuera de la pantalla, que es justo lo que explicaria "Terrakeep"
		/// -> "keep" (se ve el final de la palabra, no el principio).
		/// </summary>
		public const string MarcadorListoParaRedimensionar = "LISTO PARA REDIMENSIONAR";

		private const int FotogramasRedimension = 90;

		private static float _anchoInicialRedim;
		private static bool _cambioDetectado;
		private static int _fotogramasTrasCambio;

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

			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < FotogramasDeEspera) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}

			try {
				Paso(_paso++);
			}
			catch (Exception e) {
				RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); _espera = 30; break;
				case 1: LogEstadoReal(); _espera = 5; break;
				case 2: CapturarTitulo("diag-titulo-01"); _espera = 5; break;
				case 3: CapturarTitulo("diag-titulo-02"); _espera = 5; break;
				case 4: IrAVecindad(); _espera = 5; break;
				// 10 capturas SEGUIDAS, una por fotograma (sin espera entre ellas), para poder
				// comparar pixel a pixel si algo cambia de un fotograma al siguiente sin que el
				// jugador haya tocado nada - eso es justo lo que describe un "parpadeo real".
				case 5: case 6: case 7: case 8: case 9:
				case 10: case 11: case 12: case 13: case 14:
					CapturarVecindad("diag-vecindad-f" + (paso - 5).ToString("00"));
					_espera = 0;
					break;
				case 15: VolverAPersonajeYAvisar(); _espera = 10; break;
				// El primer intento (esperar a que un proceso EXTERNO redimensione la ventana con
				// pywinauto/SetWindowPos) NO disparo ningun cambio real de Main.screenWidth en 240
				// fotogramas (240 muestras seguidas, "screenW=1745" en todas - confirmado en el log
				// real de una pasada previa): un SetWindowPos programatico mueve la ventana de
				// verdad, pero tModLoader no lo trata como un resize real del jugador. La via
				// verificada leyendo Terraria.Main.SetDisplayMode (codigo real decompilado, publico y
				// estatico, es EXACTAMENTE lo que llama el menu de Ajustes > Video del propio juego)
				// SI cambia screenWidth/Height de verdad, asi que el proceso lo dispara EL MISMO
				// desde dentro en vez de depender de un proceso externo.
				// Un solo salto limpio (probado antes) recalculo bien a la primera, sin ningun
				// fotograma raro. Un ARRASTRE real del borde de la ventana dispara MUCHOS eventos de
				// resize seguidos, uno detras de otro sin apenas tiempo entre ellos, no uno solo -
				// asi que el ultimo intento real es simular justo eso: varios SetDisplayMode
				// distintos en fotogramas CONSECUTIVOS, sin espera, a ver si el solapamiento de
				// varios recalculos seguidos deja alguno a medias.
				case 16: CambiarResolucionEnVivo(1300, 760); _espera = 0; break;
				case 17: CambiarResolucionEnVivo(950, 560); _espera = 0; break;
				case 18: CambiarResolucionEnVivo(1600, 900); _espera = 0; break;
				case 19: CambiarResolucionEnVivo(872, 480); _espera = 0; break;
				default:
					int fotogramaRedim = paso - 20;
					if (fotogramaRedim >= 0 && fotogramaRedim < FotogramasRedimension) {
						VigilarRedimension(fotogramaRedim);
						_espera = 0;
					}
					else {
						Terminar();
					}
					break;
			}
		}

		private static int _anchoOriginalReal;
		private static int _altoOriginalReal;

		private static void VolverAPersonajeYAvisar()
		{
			PanelTerrakeepSystem.Panel?.CambiarArea(AreaTerrakeep.Personaje, "diagnostico titulo/vecindad - antes de redimension");
			_anchoInicialRedim = Main.screenWidth;
			_anchoOriginalReal = Main.screenWidth;
			_altoOriginalReal = Main.screenHeight;
			_cambioDetectado = false;
			_fotogramasTrasCambio = 0;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - " + MarcadorListoParaRedimensionar +
				" (screenWidth inicial=" + Main.screenWidth + ", screenHeight inicial=" + Main.screenHeight + ").");
		}

		/// <summary>
		/// Dispara un cambio de resolucion REAL, desde dentro del propio proceso, con el panel YA
		/// ABIERTO en Personaje (donde esta el titulo) - el mismo camino de codigo que el menu de
		/// Ajustes > Video (<c>Terraria.Main.SetDisplayMode</c>, publico y estatico, confirmado leyendo
		/// el <c>Main.cs</c> real decompilado). El tamaño elegido es notablemente distinto del actual
		/// (mitad de ancho/alto, con un suelo de 800x480) para forzar un recalculo de verdad, no un
		/// cambio tan pequeño que pase desapercibido.
		/// </summary>
		private static void CambiarResolucionEnVivo(int nuevoAncho, int nuevoAlto)
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - llamando a Main.SetDisplayMode(" +
				nuevoAncho + ", " + nuevoAlto + ", false) con el panel abierto...");
			Main.SetDisplayMode(nuevoAncho, nuevoAlto, false);
			CalculatedStyle dimAhora = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.MarcoDimensionesParaPrueba : default;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - SetDisplayMode devuelto. " +
				"screenWidth AHORA=" + Main.screenWidth + ", screenHeight AHORA=" + Main.screenHeight +
				", marco.X AHORA=" + dimAhora.X + (dimAhora.X < 0f ? " -> OJO: X NEGATIVO" : "") + " - " +
				CapturaDePantalla.Guardar("diag-resize-salto-" + nuevoAncho + "x" + nuevoAlto));
		}

		private static void VigilarRedimension(int n)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			CalculatedStyle dim = panel != null ? panel.MarcoDimensionesParaPrueba : default;
			bool anchoDistinto = Math.Abs(Main.screenWidth - _anchoInicialRedim) > 0.5f;

			if (!_cambioDetectado && anchoDistinto) {
				_cambioDetectado = true;
				_fotogramasTrasCambio = 0;
				RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - REDIMENSION DETECTADA en fotograma +" +
					n + ": screenWidth " + _anchoInicialRedim + " -> " + Main.screenWidth + ", screenHeight=" + Main.screenHeight);
			}

			bool marcoNegativo = dim.X < 0f;
			bool merecCaptura = marcoNegativo || n % 20 == 0 ||
				(_cambioDetectado && _fotogramasTrasCambio <= 20 && _fotogramasTrasCambio % 2 == 0);

			if (_cambioDetectado) {
				_fotogramasTrasCambio++;
			}

			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - redim +" + n +
				": screenW=" + Main.screenWidth + " screenH=" + Main.screenHeight +
				" marco.X=" + dim.X + " marco.Y=" + dim.Y + " marco.W=" + dim.Width + " marco.H=" + dim.Height +
				(marcoNegativo ? " -> OJO: X NEGATIVO, el titulo se recortaria por la izquierda" : ""));

			if (merecCaptura) {
				RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - " +
					CapturaDePantalla.Guardar("diag-redim-" + n.ToString("000")));
			}
		}

		private static void Arrancar()
		{
			Terraria.GameInput.PlayerInput.CurrentInputMode = Terraria.GameInput.InputMode.Mouse;
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "diagnostico titulo/vecindad");
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD: " + Variable + " detectada. Panel abierto en Personaje.");
		}

		private static void LogEstadoReal()
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - estado real: " +
				"Main.screenWidth=" + Main.screenWidth + ", Main.screenHeight=" + Main.screenHeight +
				", Main.UIScale=" + Main.UIScale + ", Main.UIScaleWanted=" + Main.UIScaleWanted +
				", Fullscreen=" + Main.graphics.IsFullScreen);

			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel != null) {
				var dim = panel.MarcoDimensionesParaPrueba;
				RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - marco real: x=" +
					dim.X + " y=" + dim.Y + " " + dim.Width + "x" + dim.Height +
					(dim.X < 0 ? " -> OJO: X NEGATIVO, el marco empieza fuera de la pantalla por la izquierda" : " -> X ok (>=0)"));
			}
		}

		private static void CapturarTitulo(string nombre)
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - " + CapturaDePantalla.Guardar(nombre));
		}

		private static void IrAVecindad()
		{
			PanelTerrakeepSystem.Panel?.CambiarArea(AreaTerrakeep.Exploracion, "diagnostico titulo/vecindad");
			PanelTerrakeepSystem.Panel?.Exploracion?.CambiarPestana(3); // 3 = Vecindad
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - cambiado a Exploracion/Vecindad.");
		}

		private static void CapturarVecindad(string nombre)
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD - " + CapturaDePantalla.Guardar(nombre));
		}

		private static void Terminar()
		{
			_terminada = true;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TITULO/VECINDAD COMPLETO.");
		}
	}
}
