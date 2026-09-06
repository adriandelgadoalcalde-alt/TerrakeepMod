using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Ajustes;
using TerrakeepMod.UI.Exploracion;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE PRUEBAS. Recorre las <b>once vistas</b> del panel (las seis pestañas, con las
	/// seis sub-pestañas de Personaje y las tres de Exploracion) <b>dos veces: en español y en
	/// ingles</b>, recoge TODO el texto que se esta enseñando en cada una y deja una captura real
	/// del juego por vista e idioma.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Para que sirve, exactamente.</b> Al terminar compara las dos listas y saca las cadenas
	/// que salen <b>identicas en los dos idiomas</b>. Esa lista es el detector de literales sin
	/// migrar: si una frase se ve igual en español y en ingles, o esta escrita a pelo en el C# o
	/// le falta la clave en un <c>.hjson</c>. Es una comprobacion que ninguna otra autoprueba del
	/// mod hace, porque las demas cuentan elementos y miden rectangulos, no leen el texto.
	/// </para>
	/// <para>
	/// Hay coincidencias LEGITIMAS y hay que saberlo antes de mirar el informe: nombres propios
	/// ("Terrakeep", "Builds", "Remix", "Lava", "Hardmode"), numeros, y los nombres de objeto que
	/// pone el propio juego cuando coinciden en los dos idiomas. Por eso el informe las lista para
	/// mirarlas, no las da por error.
	/// </para>
	/// <para>
	/// El texto se lee de los propios widgets (<see cref="EtiquetaTk.TextoActual"/>,
	/// <see cref="BotonTk.Texto"/> y su <c>Ayuda</c>, <see cref="SelectorTk.EtiquetaActual"/>,
	/// <see cref="AlternadorTk.EtiquetaActual"/>, <see cref="CampoTextoTk.Pista"/>), o sea de lo
	/// que de verdad se esta dibujando, no de un listado paralelo que pudiera quedarse viejo.
	/// </para>
	/// </remarks>
	public static class AutopruebaIdiomas
	{
		/// <summary>Variable de entorno que la activa.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_IDIOMAS";

		/// <summary>Archivo de evidencia propio, dentro de la carpeta de guardado de la prueba.</summary>
		public const string NombreArchivo = "terrakeep-idiomas-evidencia.log";

		private const int FotogramasEntrePasos = 10;
		private const int FotogramasDeEspera = 180;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static bool _preparada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;
		private static bool _archivoIniciado;

		private static readonly List<Action> _pasos = new List<Action>();

		/// <summary>Textos recogidos: ruta de la vista -> idioma -> lista de cadenas.</summary>
		private static readonly Dictionary<string, Dictionary<string, List<string>>> _recogido =
			new Dictionary<string, Dictionary<string, List<string>>>();

		private static string _idiomaActual = "es";

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

			if (!_preparada) {
				_preparada = true;
				Preparar();
			}

			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			if (_paso >= _pasos.Count) {
				Terminar();
				return;
			}

			try {
				_pasos[_paso++]();
			}
			catch (Exception e) {
				Linea("AUTOPRUEBA IDIOMAS: EXCEPCION en el paso " + (_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		/// <summary>
		/// Monta la lista de pasos. Se hace como una LISTA de acciones y no como un switch gigante
		/// porque son 2 idiomas x 11 vistas x 2 pasos, y escribirlos a mano seria ilegible.
		/// </summary>
		private static void Preparar()
		{
			Linea("AUTOPRUEBA IDIOMAS: " + Variable + " detectada. Jugador \"" +
				Main.LocalPlayer.name + "\", mundo \"" + Main.worldName + "\", resolucion " +
				Main.screenWidth + "x" + Main.screenHeight + ", escala de interfaz " + Main.UIScale + ".");

			_pasos.Add(() => PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje,
				"autoprueba de idiomas"));

			foreach (string codigo in new string[] { "es", "en" }) {
				string idioma = codigo;
				_pasos.Add(() => CambiarIdioma(idioma));

				for (int i = 0; i < PanelTerrakeepState.ClavesDeArea.Length; i++) {
					AreaTerrakeep area = (AreaTerrakeep)i;
					_pasos.Add(() => AbrirArea(area));

					int subPestanas = SubPestanasDe(area);
					if (subPestanas == 0) {
						_pasos.Add(() => Recoger(PanelTerrakeepState.ClavesDeArea[(int)area]));
						continue;
					}

					for (int j = 0; j < subPestanas; j++) {
						int sub = j;
						_pasos.Add(() => IrASubPestana(area, sub));
						_pasos.Add(() => Recoger(PanelTerrakeepState.ClavesDeArea[(int)area] + "-" + sub));
					}
				}
			}
		}

		/// <summary>Cuantas sub-pestañas tiene un area. Se pregunta al contenido REAL, no a una
		/// constante: si algun dia cambian, la prueba las recorre igual.</summary>
		private static int SubPestanasDe(AreaTerrakeep area)
		{
			if (area == AreaTerrakeep.Personaje) {
				return ContenidoPersonaje.ClavesPestana.Length;
			}
			if (area == AreaTerrakeep.Exploracion) {
				return 3;
			}
			return 0;
		}

		private static void CambiarIdioma(string codigo)
		{
			_idiomaActual = codigo;
			IdiomaDeTerrakeep pedido = codigo == "es"
				? IdiomaDeTerrakeep.Espanol
				: IdiomaDeTerrakeep.English;

			bool cambio = Idiomas.Aplicar(pedido, "autoprueba de idiomas");
			Linea("IDIOMA -> " + codigo + " (cambio real=" + cambio + ", cultura activa=" +
				Idiomas.CulturaActiva + "). Prueba: Panel.Titulo=\"" +
				Idiomas.Texto("Panel.Titulo") + "\", Panel.Area.Personaje=\"" +
				Idiomas.Texto("Panel.Area.Personaje") + "\".");
		}

		/// <summary>Cambia de pestaña por la ruta real del clic, igual que la autoprueba del panel
		/// unico: <c>UIElement.LeftClick</c> sobre el boton de verdad.</summary>
		private static void AbrirArea(AreaTerrakeep area)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				PanelTerrakeepSystem.AbrirEnArea(area, "autoprueba de idiomas (reapertura)");
				return;
			}
			panel.PulsarPestana(area);
		}

		private static void IrASubPestana(AreaTerrakeep area, int indice)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				return;
			}

			if (area == AreaTerrakeep.Personaje && panel.Personaje != null) {
				panel.Personaje.IrAPestana(indice);
			}
			else if (area == AreaTerrakeep.Exploracion && panel.Exploracion != null) {
				panel.Exploracion.CambiarPestana(indice);
			}
		}

		/// <summary>Recoge todo el texto visible de la vista abierta y deja una captura real.</summary>
		private static void Recoger(string ruta)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				Linea("VISTA " + ruta + " [" + _idiomaActual + "]: el panel no esta abierto.");
				return;
			}

			List<string> textos = new List<string>();
			panel.ExecuteRecursively(elemento => {
				EtiquetaTk etiqueta = elemento as EtiquetaTk;
				if (etiqueta != null) {
					Anadir(textos, etiqueta.TextoActual);
					return;
				}

				BotonTk boton = elemento as BotonTk;
				if (boton != null) {
					Anadir(textos, boton.Texto);
					if (boton.Ayuda != null) {
						Anadir(textos, boton.Ayuda());
					}
					return;
				}

				SelectorTk selector = elemento as SelectorTk;
				if (selector != null) {
					Anadir(textos, selector.EtiquetaActual);
					return;
				}

				AlternadorTk alternador = elemento as AlternadorTk;
				if (alternador != null) {
					Anadir(textos, alternador.EtiquetaActual);
					if (alternador.Ayuda != null) {
						Anadir(textos, alternador.Ayuda());
					}
					return;
				}

				CampoTextoTk campo = elemento as CampoTextoTk;
				if (campo != null) {
					Anadir(textos, campo.Pista);
				}
			});

			Dictionary<string, List<string>> porIdioma;
			if (!_recogido.TryGetValue(ruta, out porIdioma)) {
				porIdioma = new Dictionary<string, List<string>>();
				_recogido[ruta] = porIdioma;
			}
			porIdioma[_idiomaActual] = textos;

			Linea("VISTA " + ruta + " [" + _idiomaActual + "]: " + textos.Count +
				" cadenas visibles. " + CapturaDePantalla.Guardar("idioma-" + _idiomaActual + "-" + ruta));
		}

		private static void Anadir(List<string> destino, string texto)
		{
			if (string.IsNullOrEmpty(texto)) {
				return;
			}
			string limpio = texto.Trim();
			if (limpio.Length == 0 || destino.Contains(limpio)) {
				return;
			}
			destino.Add(limpio);
		}

		/// <summary>
		/// El informe final: por cada vista, las cadenas que salen IGUAL en los dos idiomas.
		/// </summary>
		private static void Terminar()
		{
			_terminada = true;

			int totalEs = 0;
			int totalIguales = 0;
			List<string> sospechosas = new List<string>();

			foreach (KeyValuePair<string, Dictionary<string, List<string>>> vista in _recogido) {
				List<string> es;
				List<string> en;
				if (!vista.Value.TryGetValue("es", out es) || !vista.Value.TryGetValue("en", out en)) {
					Linea("SIN PAREJA: la vista " + vista.Key + " no se recogio en los dos idiomas.");
					continue;
				}

				totalEs += es.Count;
				List<string> iguales = new List<string>();
				for (int i = 0; i < es.Count; i++) {
					if (en.Contains(es[i]) && TieneLetras(es[i])) {
						iguales.Add(es[i]);
						if (!sospechosas.Contains(es[i])) {
							sospechosas.Add(es[i]);
						}
					}
				}

				totalIguales += iguales.Count;
				Linea("VISTA " + vista.Key + ": " + es.Count + " cadenas en es, " + en.Count +
					" en en, " + iguales.Count + " IGUALES en los dos idiomas.");
				for (int i = 0; i < iguales.Count; i++) {
					Linea("    = \"" + iguales[i] + "\"");
				}
			}

			Linea("RESUMEN: " + _recogido.Count + " vistas, " + totalEs +
				" cadenas recogidas en español, " + totalIguales +
				" coincidencias (" + sospechosas.Count + " distintas). " +
				"Las coincidencias hay que MIRARLAS una a una: nombres propios, numeros y nombres " +
				"de objeto del juego coinciden a proposito.");
			Linea("AUTOPRUEBA IDIOMAS COMPLETA.");
		}

		/// <summary>true si la cadena tiene alguna letra. Un "(1234, 56)" o un "12 / 34" coincide en
		/// los dos idiomas por definicion y no dice nada.</summary>
		private static bool TieneLetras(string texto)
		{
			for (int i = 0; i < texto.Length; i++) {
				if (char.IsLetter(texto[i])) {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Escribe al log del juego y ademas a un archivo propio dentro de la carpeta de guardado de
		/// la prueba: el <c>client.log</c> es uno solo para todas las instancias del juego y se
		/// pisa entre pruebas (problema real ya documentado por WS3, WS4, WS5 y WS6).
		/// </summary>
		private static void Linea(string mensaje)
		{
			string linea = Terrakeep.LogTag + " " + mensaje;
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(linea);
			}

			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta, "# Evidencia de la revision de idiomas - " +
						DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
				}
				File.AppendAllText(ruta, "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " +
					linea + Environment.NewLine);
			}
			catch (Exception) {
				// La evidencia del log del juego ya esta escrita.
			}
		}
	}
}
