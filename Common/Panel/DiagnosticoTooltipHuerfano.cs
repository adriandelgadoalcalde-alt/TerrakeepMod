using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.UI;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE PRUEBAS: reproduce en vivo el bug real reportado por el usuario (captura
	/// real dentro del juego): una pastilla/etiqueta con "Exploración" se queda pintada en mitad
	/// de la pantalla con el panel YA CERRADO, como si el tooltip de la pestaña no se limpiara al
	/// cerrar.
	/// </summary>
	/// <remarks>
	/// Escenario EXACTO: abrir el panel, pasar el ratón sobre la pestaña "Exploración" (deja
	/// <c>BotonTk._tooltipPendiente</c> pedido y <c>IsMouseHovering=true</c> en ese botón
	/// concreto), y CERRAR el panel sin apartar antes el ratón de la pestaña (nunca se llama a
	/// <c>MouseOut</c>) - a diferencia de <see cref="AutopruebaTooltipPestana"/>, que sí llama a
	/// <c>MouseOut</c> antes de terminar y por eso nunca habría visto este caso.
	/// </remarks>
	public static class DiagnosticoTooltipHuerfano
	{
		public const string Variable = "TERRAKEEP_DIAG_TOOLTIP_HUERFANO";

		private const int FotogramasDeEspera = 180;
		private const int FotogramasEntrePasos = 10;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		private static BotonTk _pestanaEnPrueba;

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
				RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); _espera = FotogramasEntrePasos * 3; break;
				case 1: HoverExploracion(); _espera = 20; break; // dar tiempo real a que DrawSelf pida el tooltip y PanelTerrakeepState.Draw lo consuma
				case 2: ComprobarTooltipConPanelAbierto(); _espera = 5; break;
				case 3: CapturaConPanelAbierto(); _espera = 5; break;
				case 4: CerrarSinApartarElRaton(); _espera = 0; break;
				// Varios fotogramas SUELTOS (sin agrupar en una sola espera) para poder loguear el
				// estado real fotograma a fotograma justo despues de cerrar - si el tooltip
				// persistiera, tiene que verse en CADA uno de estos, no solo en el primero.
				case 5: case 6: case 7: case 8: case 9:
					ComprobarFotogramaTrasCierre(paso - 4);
					_espera = 3;
					break;
				// La 1a pasada de este escaneo (solo ensamblado del mod, solo campos STATIC, 5
				// fotogramas despues de cerrar) no encontro nada - dos huecos reales en el propio
				// arnes, no en el bug: (1) Main._mouseTextCache (si existe) es un campo de INSTANCIA
				// de Terraria.Main, en OTRO ensamblado, nunca mirado; (2) 5 fotogramas de retraso es
				// tiempo de sobra para que un valor que solo vive UN fotograma ya se haya limpiado
				// solo. Repetido ya MISMO fotograma que la captura final, y mirando TAMBIEN las
				// instancias de Terraria.Main (Main.instance) ademas del ensamblado del mod.
				case 10: CapturaFinal(); EscanearCamposEstaticos(); break;
				default: Terminar(); break;
			}
		}

		private static void EscanearCamposEstaticos()
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - escaneando campos de tipo string buscando \"Ajustes\" (mismo fotograma que la captura final)...");
			int encontrados = 0;

			// 1) Todo el ensamblado del mod: campos STATIC (de instancia no tendria sentido, no hay
			// una unica instancia identificable de la mayoria de nuestras clases).
			encontrados += EscanearTipo(typeof(Terrakeep), null, soloStatic: true);
			foreach (var tipo in typeof(Terrakeep).Assembly.GetTypes()) {
				encontrados += EscanearTipo(tipo, null, soloStatic: true);
			}

			// 2) Terraria.Main: static Y de instancia sobre Main.instance - aqui es donde viviria
			// algo como el cache interno de Main.MouseText/DrawPendingMouseText si el texto viene de
			// ahi (mecanismo vainilla, no del mod).
			encontrados += EscanearTipo(typeof(Main), Main.instance, soloStatic: false);

			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - escaneo completo, " + encontrados + " campo(s) encontrados.");
		}

		private static int EscanearTipo(Type tipo, object instancia, bool soloStatic)
		{
			System.Reflection.FieldInfo[] campos;
			try {
				var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
					System.Reflection.BindingFlags.DeclaredOnly |
					(soloStatic ? System.Reflection.BindingFlags.Static
						: System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
				campos = tipo.GetFields(flags);
			}
			catch { return 0; }

			int encontrados = 0;
			foreach (var campo in campos) {
				if (campo.FieldType != typeof(string)) {
					continue;
				}
				string valor;
				try { valor = campo.GetValue(campo.IsStatic ? null : instancia) as string; }
				catch { continue; }

				if (valor != null && valor.IndexOf("Ajustes", StringComparison.Ordinal) >= 0) {
					encontrados++;
					RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - CAMPO SOSPECHOSO: " +
						tipo.FullName + "." + campo.Name + " (static=" + campo.IsStatic + ") = \"" + valor + "\"");
				}
			}
			return encontrados;
		}

		private static void Arrancar()
		{
			Terraria.GameInput.PlayerInput.CurrentInputMode = Terraria.GameInput.InputMode.Mouse;
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "diagnostico tooltip huerfano");
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO: " + Variable + " detectada. Panel abierto en Personaje.");
		}

		private static void HoverExploracion()
		{
			// Sin Clave asignada por pestaña (comprobado: PanelTerrakeepState nunca la pone en los
			// botones de la barra), se identifica por su TEXTO real, igual que hace la propia
			// PanelTerrakeepState.NombreDeArea - "Exploración" tal cual la vio el usuario en su
			// captura.
			string nombreExploracion = PanelTerrakeepState.NombreDeArea(AreaTerrakeep.Exploracion);
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			BotonTk pestana = panel?.MarcoHijos.OfType<BotonTk>()
				.FirstOrDefault(b => b.EsPestana && b.Texto == nombreExploracion);

			if (pestana == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO: no se encontro la pestaña Exploracion.");
				return;
			}

			CalculatedStyle dim = pestana.GetDimensions();
			int x = (int)(dim.X + dim.Width / 2f);
			int y = (int)(dim.Y + dim.Height / 2f);
			Main.mouseX = x;
			Main.mouseY = y;

			_pestanaEnPrueba = pestana;
			pestana.MouseOver(new UIMouseEvent(pestana, new Vector2(x, y)));

			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - raton puesto en x=" + x + " y=" + y +
				" sobre la pestaña \"" + pestana.Texto + "\", MouseOver disparado a mano. IsMouseHovering=" +
				pestana.IsMouseHovering + ".");
		}

		private static void ComprobarTooltipConPanelAbierto()
		{
			// Con el panel TODAVIA abierto, forzar unos fotogramas de Draw real (el bucle del juego
			// ya los da mientras se espera) deberia haber dejado y consumido el tooltip normalmente
			// - esto es la LINEA BASE: si aqui YA estuviera roto, el bug seria mas simple de lo que
			// parece (nunca se limpia ni con el panel abierto).
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - con el panel ABIERTO, " +
				FotogramasEntrePasos + " fotogramas despues del hover: BotonTk.TooltipPendienteParaPrueba=" +
				(BotonTk.TooltipPendienteParaPrueba ?? "(null)") + " -> " +
				(BotonTk.TooltipPendienteParaPrueba == null
					? "OK (se ha consumido solo, como cualquier fotograma normal con el panel abierto)."
					: "raro: deberia haberse consumido ya con el panel abierto."));
		}

		private static void CapturaConPanelAbierto()
		{
			// Referencia REAL de como se ve el tooltip/pestaña con el panel todavia abierto - para
			// comparar visualmente contra la captura final y saber si lo que queda despues (si algo
			// queda) es el MISMO elemento o algo distinto.
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - captura con el panel ABIERTO: " +
				CapturaDePantalla.Guardar("diagnostico-tooltip-huerfano-panel-abierto"));
		}

		private static void CerrarSinApartarElRaton()
		{
			bool hoveringAntes = _pestanaEnPrueba != null && _pestanaEnPrueba.IsMouseHovering;
			// LA PIEZA CLAVE del escenario real: cerrar el panel MIENTRAS el raton sigue "encima" de
			// la pestaña, sin llamar nunca a MouseOut - exactamente lo que pasaria si el jugador
			// cierra con un atajo de teclado (o el boton Cerrar) mientras el raton esta sobre otra
			// pestaña, en vez de apartarlo primero.
			PanelTerrakeepSystem.CerrarPanel("diagnostico tooltip huerfano (raton sigue sobre la pestaña)");
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - PANEL CERRADO sin apartar el raton. " +
				"IsMouseHovering ANTES de cerrar=" + hoveringAntes + ", PanelAbierto AHORA=" + PanelTerrakeepSystem.PanelAbierto + ".");
		}

		private static void ComprobarFotogramaTrasCierre(int n)
		{
			string pendiente = BotonTk.TooltipPendienteParaPrueba;
			bool hoveringTodavia = _pestanaEnPrueba != null && _pestanaEnPrueba.IsMouseHovering;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - fotograma +" + n + " tras cerrar: " +
				"PanelAbierto=" + PanelTerrakeepSystem.PanelAbierto +
				", BotonTk.TooltipPendienteParaPrueba=" + (pendiente ?? "(null)") +
				", IsMouseHovering (instancia vieja)=" + hoveringTodavia);
		}

		private static void CapturaFinal()
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO - captura final tras cerrar: " +
				CapturaDePantalla.Guardar("diagnostico-tooltip-huerfano-tras-cerrar"));

			Main.mouseX = 2;
			Main.mouseY = 2;
			_espera = 5;
		}

		private static void Terminar()
		{
			_terminada = true;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO TOOLTIP HUERFANO COMPLETO.");
		}
	}
}
