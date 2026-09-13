using Terraria;
using Terraria.ID;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Personaje;

namespace TerrakeepMod.Common.Loadouts
{
	/// <summary>
	/// Cuerpo real de la autoprueba de Conjuntos - ver <see cref="LoadoutsSystem"/> para como
	/// arranca (abre el panel y salta a esta pestaña). Corre en el juego REAL, sobre el
	/// inventario/equipo del personaje de pruebas, sin que nadie pulse nada.
	/// </summary>
	public static class AutopruebaConjuntos
	{
		private const int RanuraCabeza = 0;

		private static int _paso;
		private static int _espera;
		private static bool _terminada;

		public static void Avanzar(PestanaConjuntos pestana)
		{
			if (!LoadoutsSystem.Activa || _terminada) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}

			switch (_paso) {
				case 0: Paso0Inicial(pestana); break;
				case 1: Paso1Renombrar(pestana); break;
				case 2: Paso2GuardarPreset(pestana); break;
				case 3: Paso3CambiarYAplicar(pestana); break;
				case 4: Paso4DeshacerYRehacer(pestana); break;
				case 5: Paso5CapturaEspanol(pestana); break;
				case 6: Paso6CapturaIngles(pestana); break;
				case 7: Paso7RestaurarIdioma(pestana); break;
				case 8: Paso8Limpieza(pestana); break;
			}
		}

		private static void Paso0Inicial(PestanaConjuntos pestana)
		{
			Player jugador = PersonajeVivo.Jugador;
			Registrar("PASO0 pestaña Conjuntos abierta. Conjuntos nativos construidos=" +
				pestana.TotalNativosParaPrueba + " (se esperan 3), presets guardados=" +
				pestana.TotalPresetsParaPrueba + ", CurrentLoadoutIndex=" + jugador.CurrentLoadoutIndex + " | " +
				(pestana.TotalNativosParaPrueba == 3 ? "OK" : "FALLO: NO son 3."));

			Avanzar(1);
		}

		private static void Paso1Renombrar(PestanaConjuntos pestana)
		{
			LoadoutsPlayer datos = PersonajeVivo.Jugador.GetModPlayer<LoadoutsPlayer>();
			pestana.RenombrarParaPrueba(0, "Set de pruebas");

			bool guardado = datos.Nombres[0] == "Set de pruebas";
			Registrar("PASO1 renombrado el conjunto 0 a \"Set de pruebas\": LoadoutsPlayer.Nombres[0]=\"" +
				datos.Nombres[0] + "\" | " + (guardado ? "OK: persistido en el ModPlayer del personaje." : "FALLO."));

			Avanzar(2);
		}

		private static void Paso2GuardarPreset(PestanaConjuntos pestana)
		{
			Player jugador = PersonajeVivo.Jugador;
			int antesPresets = pestana.TotalPresetsParaPrueba;

			jugador.armor[RanuraCabeza] = new Item();
			jugador.armor[RanuraCabeza].SetDefaults(ItemID.MoltenHelmet);

			pestana.GuardarPresetNuevoParaPrueba("Preset de pruebas");

			int ahoraPresets = pestana.TotalPresetsParaPrueba;
			LoadoutsPlayer datos = jugador.GetModPlayer<LoadoutsPlayer>();
			PresetLoadout ultimo = datos.Presets[datos.Presets.Count - 1];
			bool guardoElCasco = ultimo.Armadura[RanuraCabeza].type == ItemID.MoltenHelmet;

			Registrar("PASO2 armor[0]=Casco Fundido, guardado como preset \"" + ultimo.Nombre + "\": " +
				"presets antes=" + antesPresets + " ahora=" + ahoraPresets +
				", el preset guardo el casco=" + guardoElCasco + " | " +
				(ahoraPresets == antesPresets + 1 && guardoElCasco
					? "OK: preset nuevo con el equipo activo real."
					: "FALLO."));

			Avanzar(3);
		}

		private static int _indicePresetDePrueba;

		private static void Paso3CambiarYAplicar(PestanaConjuntos pestana)
		{
			Player jugador = PersonajeVivo.Jugador;
			LoadoutsPlayer datos = jugador.GetModPlayer<LoadoutsPlayer>();
			_indicePresetDePrueba = datos.Presets.Count - 1;

			// Simula que el jugador siguio jugando y se cambio de casco despues de guardar el
			// preset - lo que "Aplicar" tiene que deshacer.
			jugador.armor[RanuraCabeza] = new Item();
			jugador.armor[RanuraCabeza].SetDefaults(ItemID.AncientGoldHelmet);

			Historial.Pila.Limpiar();
			pestana.AplicarPresetParaPrueba(_indicePresetDePrueba);

			bool volvioElCasco = jugador.armor[RanuraCabeza].type == ItemID.MoltenHelmet;
			Registrar("PASO3 armor[0] cambiado a Casco Dorado Antiguo, luego Aplicar preset \"" +
				datos.Presets[_indicePresetDePrueba].Nombre + "\": armor[0] ahora=\"" +
				jugador.armor[RanuraCabeza].Name + "\" | " +
				(volvioElCasco ? "OK: el preset se aplico sobre el conjunto activo de verdad."
					: "FALLO: NO se aplico."));

			Avanzar(4);
		}

		private static void Paso4DeshacerYRehacer(PestanaConjuntos pestana)
		{
			Player jugador = PersonajeVivo.Jugador;

			bool puedeDeshacer = Historial.Pila.PuedeDeshacer;
			string deshecho = Historial.Deshacer();
			bool volvioElDorado = jugador.armor[RanuraCabeza].type == ItemID.AncientGoldHelmet;

			Registrar("PASO4a puedeDeshacer=" + puedeDeshacer + ", tras Ctrl+Z (\"" + deshecho + "\"): armor[0]=\"" +
				jugador.armor[RanuraCabeza].Name + "\" | " +
				(puedeDeshacer && volvioElDorado
					? "OK: aplicar un preset quedo deshacible por el historial general de WS7."
					: "FALLO: aplicar un preset NO quedo deshacible."));

			string rehecho = Historial.Rehacer();
			bool volvioElFundido = jugador.armor[RanuraCabeza].type == ItemID.MoltenHelmet;
			Registrar("PASO4b tras Ctrl+Y (\"" + rehecho + "\"): armor[0]=\"" + jugador.armor[RanuraCabeza].Name +
				"\" | " + (volvioElFundido ? "OK: rehecho tambien." : "FALLO."));

			Avanzar(5);
		}

		private static IdiomaDeTerrakeep _idiomaOriginal;

		private static void Paso5CapturaEspanol(PestanaConjuntos pestana)
		{
			_idiomaOriginal = Idiomas.IdiomaConfigurado;
			Registrar("PASO5 (es) " + CapturaDePantalla.Guardar("conjuntos-01-es"));
			Idiomas.Elegir(IdiomaDeTerrakeep.English);
			Avanzar(6);
		}

		private static void Paso6CapturaIngles(PestanaConjuntos pestana)
		{
			Registrar("PASO6 (en) cultura activa=" + Idiomas.CulturaActiva + " " +
				CapturaDePantalla.Guardar("conjuntos-02-en"));
			Avanzar(7);
		}

		private static void Paso7RestaurarIdioma(PestanaConjuntos pestana)
		{
			Idiomas.Elegir(_idiomaOriginal);
			Registrar("PASO7 idioma restaurado a " + _idiomaOriginal + " (cultura activa=" + Idiomas.CulturaActiva + ").");
			Avanzar(8);
		}

		private static void Paso8Limpieza(PestanaConjuntos pestana)
		{
			Player jugador = PersonajeVivo.Jugador;
			LoadoutsPlayer datos = jugador.GetModPlayer<LoadoutsPlayer>();

			if (_indicePresetDePrueba >= 0 && _indicePresetDePrueba < datos.Presets.Count) {
				datos.Presets.RemoveAt(_indicePresetDePrueba);
			}
			datos.Nombres[0] = null;
			jugador.armor[RanuraCabeza] = new Item();
			Historial.Pila.Limpiar();

			Registrar("PASO8 limpieza: preset y nombre de prueba retirados, armor[0] vaciado, historial limpio. " +
				"Presets que quedan=" + datos.Presets.Count + ".");

			_terminada = true;
			Registrar("AUTOPRUEBA CONJUNTOS: terminada.");
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
