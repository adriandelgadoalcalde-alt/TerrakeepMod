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
	/// SOLO ARNES DE PRUEBAS: verifica con una captura real que el tooltip de una pestaña
	/// INACTIVA de la barra superior (arreglado en <see cref="BotonTk.DibujarTooltipPendiente"/>,
	/// ver bitacora.md 14-sep-2026) ya no se mezcla con el contenido de detras al no tener fondo -
	/// el hallazgo real de la revision KeepQA de esa noche (cinco capturas, ventana pequeña).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Reproduce el mismo escenario exacto de las capturas originales del hallazgo
	/// (<c>evidencia\espaciado-capturas\ajustes-800x720-minimo-en.png</c> e
	/// <c>investigacion-800x720-minimo-es.png</c>): ventana al minimo real que admite el motor
	/// (800x720, <c>Main.minScreenW</c>/<c>minScreenH</c>) y el raton de PANTALLA
	/// (<c>Main.mouseX</c>/<c>Main.mouseY</c>, no <c>Main.InGameUI.MousePosition</c>) forzado sobre
	/// una pestaña que NO es la activa - mismo mecanismo, ya verificado en el juego real, que usa
	/// <see cref="AutopruebaTooltipObjeto.ReafirmarRaton"/> para los tooltips de objeto: hay que
	/// reafirmarlo en CADA fotograma dentro de <c>PanelTerrakeepState.Draw</c>, justo antes de
	/// <c>base.Draw</c>, porque nada anterior a ese punto sobrevive (ver esa nota, con tres rondas
	/// de log real detras).
	/// </para>
	/// <para>
	/// La captura la hace <see cref="CapturaDePantalla"/> (back buffer real del motor grafico,
	/// nunca el escritorio) - por eso hace falta que <see cref="Variable"/> este en la lista de
	/// <see cref="CapturaDePantalla.Permitida"/>.
	/// </para>
	/// </remarks>
	public static class AutopruebaTooltipPestana
	{
		public const string Variable = "TERRAKEEP_AUTOTEST_TOOLTIP_PESTANA";

		private const int FotogramasDeEspera = 180;
		private const int FotogramasEntrePasos = 10;
		// Suficientes fotogramas con el raton ya puesto antes de pedir la captura: DibujarSelf
		// tiene que ejecutarse (deja pedido _tooltipPendiente) y DESPUES PanelTerrakeepState.Draw
		// tiene que dibujarlo - dos pasos reales, no instantaneos, y la captura ademas lee el
		// fotograma YA PRESENTADO (un fotograma de retraso, ver CapturaDePantalla).
		private const int FotogramasConRatonPuesto = 20;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		private static bool _forzandoRaton;
		private static int _mouseObjetivoX;
		private static int _mouseObjetivoY;
		private static BotonTk _pestanaEnPrueba;

		/// <summary>Pisa Main.mouseX/Main.mouseY con el objetivo actual, si lo hay. Mismo motivo y
		/// mismo punto de enganche que <see cref="AutopruebaTooltipObjeto.ReafirmarRaton"/> (ver esa
		/// nota): es la unica llamada que sobrevive hasta el Draw real de este mismo
		/// fotograma.</summary>
		public static void ReafirmarRaton()
		{
			if (_forzandoRaton) {
				Main.mouseX = _mouseObjetivoX;
				Main.mouseY = _mouseObjetivoY;
			}
		}

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
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP PESTAÑA: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); _espera = FotogramasEntrePasos * 3; break; // dar tiempo a SetDisplayMode
				case 1: PonerRatonSobrePestanaInactiva(); _espera = FotogramasConRatonPuesto; break;
				case 2: Capturar(); break;
				default: Terminar(); break;
			}
		}

		private static void Arrancar()
		{
			// Mismo motivo que AutopruebaTooltipObjeto.Arrancar: esta sesion nunca mueve un raton
			// fisico de verdad, asi que PlayerInput.CurrentInputMode puede haber quedado en modo
			// mando en vez de raton+teclado, y eso bloquearia IsMouseHovering sin que tenga nada
			// que ver con el arreglo que se quiere comprobar.
			Terraria.GameInput.PlayerInput.CurrentInputMode = Terraria.GameInput.InputMode.Mouse;

			// El minimo real que admite el motor - la resolucion en la que se vieron las cinco
			// capturas originales del hallazgo (ver bitacora.md).
			Main.SetDisplayMode(800, 720, false);

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "autoprueba del tooltip de pestaña");

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP PESTAÑA: " + Variable + " detectada. " +
				"Resolucion pedida 800x720 -> real " + Main.screenWidth + "x" + Main.screenHeight +
				". Panel abierto en Personaje.");
		}

		/// <summary>Busca la primera pestaña de la barra superior que NO sea la activa (con el
		/// panel recien abierto en Personaje, cualquier otra lo es) y pone el raton de PANTALLA
		/// sobre su centro - el mismo camino real que dispara <c>IsMouseHovering</c> en
		/// <see cref="BotonTk"/> y, con el arreglo, deja pedido el tooltip con fondo propio.</summary>
		private static void PonerRatonSobrePestanaInactiva()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			BotonTk pestana = panel?.MarcoHijos.OfType<BotonTk>()
				.FirstOrDefault(b => b.EsPestana && !b.Activo);

			if (pestana == null) {
				RegistroPanel.Linea(Terrakeep.LogTag +
					" AUTOPRUEBA TOOLTIP PESTAÑA: no se encontro ninguna pestaña inactiva en la barra.");
				return;
			}

			CalculatedStyle dim = pestana.GetDimensions();
			int x = (int)(dim.X + dim.Width / 2f);
			int y = (int)(dim.Y + dim.Height / 2f);
			_mouseObjetivoX = x;
			_mouseObjetivoY = y;
			_forzandoRaton = true;
			Main.mouseX = x;
			Main.mouseY = y;

			// IsMouseHovering (el que lee BotonTk.DrawSelf) NO se calcula en Draw, sino una vez por
			// fotograma dentro de UserInterface.Update -> GetMousePosition() -> hit-test contra
			// Main.InGameUI.MousePosition, ANTES de que este Update propio llegue a ejecutarse. Con
			// solo reafirmar Main.mouseX/Y en Draw (como hace AutopruebaTooltipObjeto para el
			// ContainsPoint MANUAL de SlotObjetoVanilla, un caso distinto) la entrada real polling
			// pisa el valor forzado ANTES de ese hit-test y la pestaña nunca llegaba a marcarse
			// como hovered - comprobado con una primera pasada real que compilo y corrio pero cuya
			// captura no mostraba ningun tooltip. Se fuerza aqui, en su lugar, LLAMANDO al mismo
			// metodo que dispara el motor real (BotonTk.MouseOver, publico, override de
			// UIElement.MouseOver) - mismo patron de "invocar el evento real" que ya usa
			// AutopruebaPersonaje.ComprobarDeslizadorColor con LeftMouseDown. IsMouseHovering queda
			// en true de verdad, sin depender de ganar ninguna carrera de fotograma.
			_pestanaEnPrueba = pestana;
			pestana.MouseOver(new UIMouseEvent(pestana, new Vector2(x, y)));

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP PESTAÑA - raton de PANTALLA puesto " +
				"en x=" + x + " y=" + y + " sobre la pestaña \"" + pestana.Texto + "\" (rectangulo real x=" +
				(int)dim.X + " y=" + (int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height +
				"), MouseOver disparado a mano (IsMouseHovering=" + pestana.IsMouseHovering + "), " +
				FotogramasConRatonPuesto + " fotogramas antes de pedir la captura.");
		}

		private static void Capturar()
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP PESTAÑA - IsMouseHovering=" +
				(_pestanaEnPrueba != null ? _pestanaEnPrueba.IsMouseHovering.ToString() : "(sin pestaña)") +
				" justo antes de capturar - " +
				CapturaDePantalla.Guardar("tooltip-pestana-fondo-800x720-minimo"));

			_forzandoRaton = false;
			Main.mouseX = 2;
			Main.mouseY = 2;

			if (_pestanaEnPrueba != null) {
				_pestanaEnPrueba.MouseOut(new UIMouseEvent(_pestanaEnPrueba, new Vector2(2f, 2f)));
				_pestanaEnPrueba = null;
			}
		}

		private static void Terminar()
		{
			_terminada = true;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA TOOLTIP PESTAÑA COMPLETA.");
		}
	}
}
