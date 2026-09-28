using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.UI.Builds;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE VERIFICACION (28-sep-2026), NUNCA activo jugando normal: solo corre con la
	/// variable de entorno <see cref="Variable"/> puesta. Bug real reportado por el usuario con
	/// captura - en Builds, el anillo de progreso circular "X/Y" (el "6/13" de la captura real)
	/// quedaba DESFASADO, solapando la esquina superior derecha de la ventana y sobresaliendo por
	/// fuera del panel.
	/// <para />
	/// Arreglo real en <see cref="ContenidoBuilds"/> (ver el comentario de la constante
	/// <c>AnchoAlternadorFuente</c> en ese archivo): el anillo pasa de vivir APILADO bajo el
	/// alternador de fuente (Vanilla/Calamity) a vivir a su lado IZQUIERDO, centrado verticalmente
	/// con el. Este arnes mide la geometria REAL de los dos widgets ya colocados por el motor
	/// (<see cref="UIElement.GetDimensions"/>/<see cref="UIElement.GetInnerDimensions"/> reales,
	/// nunca una formula recalculada a mano por separado) y comprueba tres cosas:
	/// <list type="number">
	/// <item>El anillo no sobresale por NINGUN lado de los limites internos reales de la cabecera
	/// (<see cref="ContenidoBuilds.CabeceraParaPrueba"/>.GetInnerDimensions(), que ya descuenta el
	/// padding real del panel - el sintoma exacto reportado por el usuario).</item>
	/// <item>El anillo queda a la IZQUIERDA del alternador de fuente, sin solaparlo.</item>
	/// <item>Los dos quedan centrados verticalmente entre si (mismo centro Y, +/-1px).</item>
	/// </list>
	/// Archivo NUEVO: no toca ningun archivo de produccion existente aparte del propio arreglo real
	/// (<c>UI/Builds/ContenidoBuilds.cs</c>) y de la superficie <c>*ParaPrueba</c> que ese mismo
	/// arreglo ya necesitaba exponer (mismo patron ya establecido por <c>AnilloProgresoParaPrueba</c>
	/// et al.). Mismo patron real de arnes ya usado por <c>DiagnosticoInvestigador3Bugs.cs</c> (env
	/// var + maquina de pasos por fotograma, forzar por reflexion el escenario real "6/13").
	/// </summary>
	public class DiagnosticoAnilloBuildsDesbordado : ModSystem
	{
		public const string Variable = "TERRAKEEP_DIAG_ANILLO_DESBORDE";

		private const int FotogramasDeEspera = 180;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}
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
				EjecutarPaso(_paso);
			}
			catch (Exception e) {
				Log("EXCEPCION en el paso " + _paso + ": " + e);
				_terminada = true;
			}
		}

		private static void Avanzar(int siguiente, int espera = 5)
		{
			_paso = siguiente;
			_espera = espera;
		}

		private static void Log(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " ANILLO-BUILDS-DESBORDE: " + mensaje);
			}
		}

		private static object Priv(object obj, string campo)
		{
			if (obj == null) {
				return null;
			}
			System.Reflection.FieldInfo f = obj.GetType().GetField(campo,
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			return f?.GetValue(obj);
		}

		private static void SetPriv(object obj, string campo, object valor)
		{
			if (obj == null) {
				return;
			}
			System.Reflection.FieldInfo f = obj.GetType().GetField(campo,
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			f?.SetValue(obj, valor);
		}

		private static void EjecutarPaso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); Avanzar(1); break;
				case 1: ForzarEscenarioSeisDeTrece(); Avanzar(2); break;
				case 2: MedirLayout(); Avanzar(3); break;
				case 3: Terminar(); break;
			}
		}

		private static void Arrancar()
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Builds, "diagnostico anillo builds desbordado");
			Log("PASO0: panel abierto en Builds.");
		}

		/// <summary>Mismo escenario real exacto que la captura del usuario: "6/13". Mismo truco de
		/// reflexion de solo-VALOR (nunca de logica) ya usado por
		/// <see cref="DiagnosticoInvestigador3Bugs.ForzarEscenarioAnillo"/>.</summary>
		private static void ForzarEscenarioSeisDeTrece()
		{
			ContenidoBuilds contenido = PanelTerrakeepSystem.Panel?.Builds;
			SetPriv(contenido, "_objetosQueTiene", 6);
			SetPriv(contenido, "_objetosResueltos", 13);
			Log("PASO1: forzado _objetosQueTiene=6 _objetosResueltos=13 (mismo \"6/13\" real de la captura del usuario).");
		}

		private static void MedirLayout()
		{
			ContenidoBuilds contenido = PanelTerrakeepSystem.Panel?.Builds;
			if (contenido == null) {
				Log("PASO2 FALLO: ContenidoBuilds nulo (el panel no esta en Builds).");
				return;
			}

			AnilloProgresoTk anillo = contenido.AnilloProgresoParaPrueba;
			AlternadorTk alternador = contenido.AlternadorFuenteParaPrueba;
			UIPanel cabecera = contenido.CabeceraParaPrueba;
			if (anillo == null || cabecera == null) {
				Log("PASO2 FALLO: anillo=" + (anillo == null ? "null" : "ok") + " cabecera=" + (cabecera == null ? "null" : "ok") + ".");
				return;
			}

			CalculatedStyle dimAnillo = anillo.GetDimensions();
			CalculatedStyle dimCabeceraInterior = cabecera.GetInnerDimensions();
			float anilloRight = dimAnillo.X + dimAnillo.Width;
			float anilloBottom = dimAnillo.Y + dimAnillo.Height;
			float cabeceraInteriorRight = dimCabeceraInterior.X + dimCabeceraInterior.Width;
			float cabeceraInteriorBottom = dimCabeceraInterior.Y + dimCabeceraInterior.Height;

			bool dentroIzquierda = dimAnillo.X >= dimCabeceraInterior.X - 0.5f;
			bool dentroArriba = dimAnillo.Y >= dimCabeceraInterior.Y - 0.5f;
			bool dentroDerecha = anilloRight <= cabeceraInteriorRight + 0.5f;
			bool dentroAbajo = anilloBottom <= cabeceraInteriorBottom + 0.5f;
			bool sinDesborde = dentroIzquierda && dentroArriba && dentroDerecha && dentroAbajo;

			Log("PASO2 LIMITES: anillo=(" + dimAnillo.X.ToString("0.00") + "," + dimAnillo.Y.ToString("0.00") +
				") " + dimAnillo.Width.ToString("0.00") + "x" + dimAnillo.Height.ToString("0.00") +
				" -> right=" + anilloRight.ToString("0.00") + " bottom=" + anilloBottom.ToString("0.00") +
				" | cabecera.GetInnerDimensions()=(" + dimCabeceraInterior.X.ToString("0.00") + "," +
				dimCabeceraInterior.Y.ToString("0.00") + ") " + dimCabeceraInterior.Width.ToString("0.00") + "x" +
				dimCabeceraInterior.Height.ToString("0.00") + " -> right=" + cabeceraInteriorRight.ToString("0.00") +
				" bottom=" + cabeceraInteriorBottom.ToString("0.00") +
				" | izquierda=" + (dentroIzquierda ? "OK" : "MAL") + " arriba=" + (dentroArriba ? "OK" : "MAL") +
				" derecha=" + (dentroDerecha ? "OK" : "MAL") + " abajo=" + (dentroAbajo ? "OK" : "MAL") +
				" -> SIN_DESBORDE=" + sinDesborde);

			// alternador != null solo confirma que el OBJETO C# existe - Reconstruir() lo cuelga/
			// descuelga de _cabecera segun si hay mas de una fuente instalada (ver el comentario real
			// de ContenidoBuilds), asi que sin Calamity queda sin Parent y su GetDimensions() nunca
			// se recalcula (se queda en (0,0) 0x0, el valor de fabrica) - Parent != null es la
			// comprobacion real de "esta de verdad en el arbol visual ahora mismo".
			if (alternador != null && alternador.Parent != null) {
				CalculatedStyle dimAlternador = alternador.GetDimensions();
				float centroYAnillo = dimAnillo.Y + dimAnillo.Height / 2f;
				float centroYAlternador = dimAlternador.Y + dimAlternador.Height / 2f;
				bool anilloALaIzquierda = anilloRight <= dimAlternador.X + 0.5f;
				bool centrados = Math.Abs(centroYAnillo - centroYAlternador) < 1f;

				Log("PASO2 ALTERNADOR: alternador=(" + dimAlternador.X.ToString("0.00") + "," +
					dimAlternador.Y.ToString("0.00") + ") " + dimAlternador.Width.ToString("0.00") + "x" +
					dimAlternador.Height.ToString("0.00") + " centroY=" + centroYAlternador.ToString("0.00") +
					" | anillo centroY=" + centroYAnillo.ToString("0.00") +
					" | ANILLO_A_LA_IZQUIERDA_DEL_ALTERNADOR=" + anilloALaIzquierda +
					" | CENTRADOS_VERTICALMENTE=" + centrados);

				Log("PASO2 RESUMEN: " + (sinDesborde && anilloALaIzquierda && centrados
					? "OK: el anillo \"6/13\" queda dentro de los limites reales de la cabecera, a la izquierda del alternador de fuente y centrado con el."
					: "MAL: revisar los detalles de arriba."));
			}
			else {
				Log("PASO2: alternador de fuente sin Parent (una sola fuente instalada en este sandbox, sin Calamity - Reconstruir() no lo cuelga de la cabecera) - " +
					"solo se puede comprobar que el anillo no desborde, la comprobacion de \"a la izquierda del alternador/centrado con el\" queda como LIMITE REAL de este sandbox concreto. " +
					"RESUMEN: " + (sinDesborde ? "OK" : "MAL"));
			}
		}

		private static void Terminar()
		{
			PanelTerrakeepSystem.CerrarPanel("diagnostico anillo builds desbordado terminado");
			_terminada = true;
			Log("ANILLO-BUILDS-DESBORDE: terminado.");
		}
	}
}
