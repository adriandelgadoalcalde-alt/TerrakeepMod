using System;
using Terraria;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.UI.Builds;
using TerrakeepMod.UI.Exploracion;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE PRUEBAS: dos bugs reales reportados por el usuario el 21-sep-2026 (captura real
	/// en vivo, ver bitacora.md):
	/// 1) El desplegable de etapa de Builds recortaba etiquetas largas en el popup (ancho fijo).
	/// 2) "Rebobinar" perdia la foto al cerrar el panel (los datos vivian en el propio widget).
	/// </summary>
	public static class DiagnosticoBuildsYRebobinar
	{
		public const string Variable = "TERRAKEEP_DIAG_BUILDS_REBOBINAR";

		private const int FotogramasDeEspera = 180;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		private static int _tileXPrueba, _tileYPrueba;
		private static ushort _tileOriginal;

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
				RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: AbrirBuilds(); _espera = 20; break;
				case 1: AbrirDesplegableEtapa(); _espera = 10; break;
				case 2: CapturarDesplegable(); _espera = 5; break;
				case 3: CerrarDesplegable(); _espera = 5; break;
				case 4: IrARebobinarYMarcar(); _espera = 10; break;
				case 5: CapturarTrasMarcar(); _espera = 5; break;
				case 6: CerrarPanelSinTocarNada(); _espera = 10; break;
				case 7: CambiarTileConPanelCerrado(); _espera = 70; break; // > 60 fotogramas para que RebobinarSystem recalcule solo
				case 8: ComprobarDiferenciaTrasReabrir(); _espera = 10; break;
				case 9: PulsarRebobinarYComprobar(); _espera = 5; break;
				case 10: CapturaFinal(); break;
				default: Terminar(); break;
			}
		}

		private static void AbrirBuilds()
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Builds, "diagnostico builds/rebobinar");
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: " + Variable + " detectada. Panel abierto en Builds.");
		}

		private static void AbrirDesplegableEtapa()
		{
			ContenidoBuilds contenido = PanelTerrakeepSystem.Panel?.ContenidoActual as ContenidoBuilds;
			DesplegableTk desplegable = contenido?.SelectorEtapaParaPrueba;
			if (desplegable == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: no se encontro el desplegable de etapa.");
				return;
			}

			ClicReal(desplegable.BotonToggle);
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: desplegable de etapa, Abierto=" + desplegable.Abierto);
		}

		private static void CapturarDesplegable()
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR - " +
				CapturaDePantalla.Guardar("diag-desplegable-etapa"));
		}

		private static void CerrarDesplegable()
		{
			ContenidoBuilds contenido = PanelTerrakeepSystem.Panel?.ContenidoActual as ContenidoBuilds;
			DesplegableTk desplegable = contenido?.SelectorEtapaParaPrueba;
			if (desplegable != null && desplegable.Abierto) {
				ClicReal(desplegable.BotonToggle);
			}
		}

		/// <summary>Mismo camino real que <c>PanelTerrakeepState.PulsarBoton</c>: un
		/// <c>UIMouseEvent</c> real en el centro del boton, nunca llamar al evento directamente
		/// (<c>AlPulsar</c> es privado a proposito, invocarlo desde fuera no seria el mismo camino
		/// que ejercita un clic real del jugador).</summary>
		private static void ClicReal(BotonTk boton)
		{
			if (boton == null) {
				return;
			}
			Terraria.UI.CalculatedStyle dim = boton.GetDimensions();
			Microsoft.Xna.Framework.Vector2 centro = new Microsoft.Xna.Framework.Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			boton.LeftClick(new Terraria.UI.UIMouseEvent(boton, centro));
		}

		private static void IrARebobinarYMarcar()
		{
			PanelTerrakeepSystem.Panel?.CambiarArea(AreaTerrakeep.Exploracion, "diagnostico builds/rebobinar");
			PanelTerrakeepSystem.Panel?.Exploracion?.CambiarPestana(4); // 4 = Rebobinar
			PestanaRebobinar pestana = PanelTerrakeepSystem.Panel?.Exploracion?.Rebobinar;
			pestana?.Marcar();

			// Tile de prueba SINTETICO, cerca del jugador, dentro del area fotografiada - mismo
			// patron que AutopruebaExploracion (pasos 25-31) para no depender de que haya un tile
			// real conocido cerca.
			Player jugador = Main.LocalPlayer;
			_tileXPrueba = (int)(jugador.Center.X / 16f) + 5;
			_tileYPrueba = (int)(jugador.Center.Y / 16f);
			_tileOriginal = Main.tile[_tileXPrueba, _tileYPrueba].TileType;

			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: Rebobinar.Marcar() llamado. HayFoto=" +
				pestana?.HayFoto + ", tile de prueba en (" + _tileXPrueba + "," + _tileYPrueba + ") tipo original=" + _tileOriginal);
		}

		private static void CapturarTrasMarcar()
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR - " +
				CapturaDePantalla.Guardar("diag-rebobinar-marcado"));
		}

		private static void CerrarPanelSinTocarNada()
		{
			PanelTerrakeepSystem.CerrarPanel("diagnostico builds/rebobinar - antes de cambiar el tile");
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: panel CERRADO. " +
				"EstadoRebobinar.HayFoto=" + EstadoRebobinar.HayFoto + " (tiene que seguir siendo true - " +
				"esto es justo lo que antes se perdia).");
		}

		private static void CambiarTileConPanelCerrado()
		{
			// Cambia el tile de prueba CON EL PANEL CERRADO - exactamente el escenario real
			// reportado ("el usuario juega con el panel cerrado la mayor parte del tiempo").
			Terraria.Tile tile = Main.tile[_tileXPrueba, _tileYPrueba];
			ushort nuevoTipo = (ushort)(_tileOriginal == Terraria.ID.TileID.Stone ? Terraria.ID.TileID.Dirt : Terraria.ID.TileID.Stone);
			tile.TileType = nuevoTipo;
			tile.HasTile = true;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: tile de prueba cambiado CON EL PANEL CERRADO, de " +
				_tileOriginal + " a " + nuevoTipo + ". Esperando >60 fotogramas para que RebobinarSystem.PostUpdateEverything recalcule solo...");
		}

		private static void ComprobarDiferenciaTrasReabrir()
		{
			// PanelTerrakeepSystem.AbrirEnArea abre en Personaje por defecto si no se especifica
			// otra cosa desde cero, pero aqui interesa comprobar EstadoRebobinar directamente (sin
			// depender de que pestaña este activa) porque el calculo ahora es independiente de la UI.
			int diferentes = EstadoRebobinar.DiferentesAhora;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: CON EL PANEL TODAVIA CERRADO, " +
				"EstadoRebobinar.DiferentesAhora=" + diferentes + " -> " +
				(diferentes >= 1 ? "OK: detecto el cambio SIN que el panel estuviera abierto" :
					"MAL: no detecto el cambio (diferentes=" + diferentes + ", deberia ser >=1)"));

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Exploracion, "diagnostico builds/rebobinar - reabrir");
			PanelTerrakeepSystem.Panel?.Exploracion?.CambiarPestana(4);
		}

		private static void PulsarRebobinarYComprobar()
		{
			PestanaRebobinar pestana = PanelTerrakeepSystem.Panel?.Exploracion?.Rebobinar;
			pestana?.Rebobinar();

			ushort tipoAhora = Main.tile[_tileXPrueba, _tileYPrueba].TileType;
			bool restaurado = tipoAhora == _tileOriginal;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR: Rebobinar() pulsado. " +
				"Tipo de tile ahora=" + tipoAhora + ", original=" + _tileOriginal + " -> " +
				(restaurado ? "OK: restaurado exactamente" : "MAL: no coincide"));
		}

		private static void CapturaFinal()
		{
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR - " +
				CapturaDePantalla.Guardar("diag-rebobinar-final"));
			_espera = 5;
		}

		private static void Terminar()
		{
			_terminada = true;
			RegistroPanel.Linea(Terrakeep.LogTag + " DIAGNOSTICO BUILDS/REBOBINAR COMPLETO.");
		}
	}
}
