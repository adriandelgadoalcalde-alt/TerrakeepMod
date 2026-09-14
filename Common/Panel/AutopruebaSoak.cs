using System;
using System.Globalization;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// KeepQA V2.0, Fase 6, Bloque B (14-sep-2026): sesion CORTA pero real de estres/soak dentro de
	/// una partida real conectada a un servidor dedicado - abre y cierra el panel unico
	/// repetidamente durante varios minutos, reutilizando EXACTAMENTE los mismos metodos de
	/// produccion que ya usa <see cref="AutopruebaPanelUnico"/>
	/// (<see cref="PanelTerrakeepSystem.AbrirEnArea"/>/<see cref="PanelTerrakeepSystem.CerrarPanel"/>),
	/// nunca su propia logica de apertura/cierre.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A diferencia de <see cref="AutopruebaPanelUnico"/> (una pasada FIJA de 37 pasos que termina
	/// sola), esta se mantiene en bucle por TIEMPO REAL (<see cref="DateTime.UtcNow"/>, no
	/// fotogramas) hasta agotar <c>TERRAKEEP_SOAK_MINUTOS</c> - una sesion de red puede tener
	/// fotogramas irregulares (lag del servidor, carga de chunks), y una duracion medida en
	/// fotogramas se alargaria o acortaria sola con eso. Solo el ESPACIADO fino dentro de un mismo
	/// ciclo (medio segundo con el panel abierto de verdad antes de cerrarlo, para que
	/// <c>UpdateUI</c> real tenga tiempo de dibujarlo, no solo instanciarlo) usa fotogramas, igual
	/// que el resto de autopruebas del mod.
	/// </para>
	/// <para>
	/// Activada por la variable de entorno <see cref="Variable"/> (mismo patron que
	/// <see cref="AutopruebaPanelUnico.Variable"/>); duracion (<c>TERRAKEEP_SOAK_MINUTOS</c>,
	/// defecto 6) e intervalo entre ciclos (<c>TERRAKEEP_SOAK_INTERVALO_S</c>, defecto 15)
	/// configurables por variable de entorno tambien, para que el mismo binario sirva tanto para
	/// una prueba corta de verificacion (1 min) como para la sesion real de la Fase 6.
	/// </para>
	/// </remarks>
	public static class AutopruebaSoak
	{
		/// <summary>Variable de entorno que la activa.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_SOAK";

		private const int FotogramasDeEsperaInicial = 180;
		private const int FotogramasPanelAbierto = 30;

		private static bool _comprobada;
		private static bool _activa;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static DateTime _finSesion;
		private static DateTime _proximoCiclo;
		private static int _ciclo;
		private static bool _panelAbiertoEsteCiclo;
		private static int _fotogramasEsperandoCierre;

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
				if (_activa) {
					double minutos = LeerDouble("TERRAKEEP_SOAK_MINUTOS", 6.0);
					_finSesion = DateTime.UtcNow.AddMinutes(minutos);
					_proximoCiclo = DateTime.UtcNow;
					RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA SOAK: " + Variable +
						" detectada. Duracion " + minutos.ToString("0.0", CultureInfo.InvariantCulture) +
						" min, intervalo " + LeerDouble("TERRAKEEP_SOAK_INTERVALO_S", 15.0).ToString("0.0", CultureInfo.InvariantCulture) + "s.");
				}
			}

			if (!_activa || _terminada) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			// Mismo margen que el resto de autopruebas del mod: da tiempo a entrar del todo al
			// mundo antes del primer ciclo.
			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < FotogramasDeEsperaInicial) {
				return;
			}

			if (DateTime.UtcNow >= _finSesion) {
				// Si se apaga con el panel todavia abierto (soak cortado justo en mitad de un
				// ciclo), se cierra limpio antes de terminar - nunca se deja el panel abierto
				// colgado al final de la sesion.
				if (_panelAbiertoEsteCiclo) {
					PanelTerrakeepSystem.CerrarPanel("autoprueba soak: fin de la sesion, cierre limpio");
					_panelAbiertoEsteCiclo = false;
				}
				_terminada = true;
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA SOAK COMPLETA: " + _ciclo +
					" ciclos abrir/cerrar completados.");
				return;
			}

			if (!_panelAbiertoEsteCiclo) {
				if (DateTime.UtcNow < _proximoCiclo) {
					return;
				}
				_ciclo++;
				PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "autoprueba soak, ciclo " + _ciclo);
				_panelAbiertoEsteCiclo = true;
				_fotogramasEsperandoCierre = 0;
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA SOAK ciclo " + _ciclo +
					": panel abierto=" + PanelTerrakeepSystem.PanelAbierto);
			}
			else {
				_fotogramasEsperandoCierre++;
				// Medio segundo con el panel REALMENTE dibujado (no solo instanciado) antes de
				// cerrarlo - da tiempo a que UpdateUI real del panel corra al menos un puñado de
				// fotogramas, igual que espera AutopruebaPanelUnico entre pasos.
				if (_fotogramasEsperandoCierre < FotogramasPanelAbierto) {
					return;
				}
				PanelTerrakeepSystem.CerrarPanel("autoprueba soak, ciclo " + _ciclo);
				_panelAbiertoEsteCiclo = false;
				double intervaloS = LeerDouble("TERRAKEEP_SOAK_INTERVALO_S", 15.0);
				_proximoCiclo = DateTime.UtcNow.AddSeconds(intervaloS);
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA SOAK ciclo " + _ciclo +
					": panel cerrado=" + !PanelTerrakeepSystem.PanelAbierto);
			}
		}

		private static double LeerDouble(string nombreVariable, double porDefecto)
		{
			string texto = Environment.GetEnvironmentVariable(nombreVariable);
			double valor;
			return !string.IsNullOrEmpty(texto) &&
				double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor)
				? valor : porDefecto;
		}
	}
}
