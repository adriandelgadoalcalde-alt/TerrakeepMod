using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Personaje;

namespace TerrakeepMod.Common.Completitud
{
	/// <summary>
	/// Cuerpo real de la autoprueba de Completitud - ver <see cref="CompletitudSystem"/> para
	/// como arranca. Corre en el juego REAL, con los datos reales del personaje/mundo de pruebas
	/// (no hace falta preparar ningun escenario: los cuatro resumenes ya leen del motor tal cual
	/// esta la partida).
	/// </summary>
	public static class AutopruebaCompletitud
	{
		private static int _paso;
		private static int _espera;
		private static bool _terminada;

		public static void Avanzar(PestanaCompletitud pestana)
		{
			if (!CompletitudSystem.Activa || _terminada) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}

			switch (_paso) {
				case 0: Paso0Numeros(pestana); break;
				case 1: Paso1CapturaEspanol(pestana); break;
				case 2: Paso2CapturaIngles(pestana); break;
				case 3: Paso3Fin(pestana); break;
			}
		}

		private static void Paso0Numeros(PestanaCompletitud pestana)
		{
			bool bienDeVerdad = pestana.TotalFilasParaPrueba == 4;
			Registrar("PASO0 filas de resumen construidas=" + pestana.TotalFilasParaPrueba + " (se esperan 4) | " +
				(bienDeVerdad ? "OK" : "FALLO: NO son 4."));

			string[] nombres = { "Jefes", "Bestiario", "Logros", "Investigacion" };
			bool todosConDatosReales = true;

			for (int i = 0; i < pestana.TotalFilasParaPrueba; i++) {
				ResumenCompletitud resumen = pestana.ResumenParaPrueba(i);
				bool coherente = resumen != null && resumen.Total > 0 && resumen.Hecho >= 0 && resumen.Hecho <= resumen.Total;
				todosConDatosReales &= coherente;
				Registrar("PASO0." + i + " " + nombres[i] + ": " +
					(resumen != null ? resumen.Hecho + "/" + resumen.Total + " (" + (int)(resumen.Fraccion * 100f) + "%), faltan.Count=" + resumen.Faltan.Count : "null") +
					" | " + (coherente ? "OK: datos reales y coherentes (Total>0, 0<=Hecho<=Total)." : "FALLO."));
			}

			// Cruce contra un numero YA conocido y verificado en otra parte del mod (bitacora,
			// 13-sep-2026): la Guia tiene exactamente 21 tramos implementados. Si esto no
			// coincidiera, "Jefes y eventos" estaria leyendo mal el mismo catalogo que ya usa la
			// Guia.
			ResumenCompletitud jefes = pestana.ResumenParaPrueba(0);
			bool coincideConLaGuia = jefes != null && jefes.Total == 21;
			Registrar("PASO0.cruce Jefes.Total=" + (jefes != null ? jefes.Total.ToString() : "?") +
				", la Guia real tiene 21 tramos implementados (bitacora, verificado el 13-sep-2026) | " +
				(coincideConLaGuia ? "OK: mismo catalogo, mismo numero." : "FALLO: no coincide."));

			Avanzar(1);
		}

		private static IdiomaDeTerrakeep _idiomaOriginal;

		private static void Paso1CapturaEspanol(PestanaCompletitud pestana)
		{
			_idiomaOriginal = Idiomas.IdiomaConfigurado;
			Registrar("PASO1 (es) " + CapturaDePantalla.Guardar("completitud-01-es"));
			Idiomas.Elegir(IdiomaDeTerrakeep.English);
			Avanzar(2);
		}

		private static void Paso2CapturaIngles(PestanaCompletitud pestana)
		{
			Registrar("PASO2 (en) cultura activa=" + Idiomas.CulturaActiva + " " +
				CapturaDePantalla.Guardar("completitud-02-en"));
			Idiomas.Elegir(_idiomaOriginal);
			Avanzar(3);
		}

		private static void Paso3Fin(PestanaCompletitud pestana)
		{
			_terminada = true;
			Registrar("AUTOPRUEBA COMPLETITUD: terminada.");
		}

		private static void Avanzar(int siguientePaso)
		{
			_paso = siguientePaso;
			_espera = 20;
		}

		private static void Registrar(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
			}
		}
	}
}
