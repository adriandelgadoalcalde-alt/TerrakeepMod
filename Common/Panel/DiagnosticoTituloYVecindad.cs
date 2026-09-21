using System;
using Terraria;
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
				default: Terminar(); break;
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
