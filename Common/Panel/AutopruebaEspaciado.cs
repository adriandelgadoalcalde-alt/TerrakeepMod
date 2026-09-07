using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Exploracion;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE PRUEBAS: verifica en el juego real que el recuadro naranja de aviso de
	/// dificultad (pestaña Exploracion &gt; "Este mundo") y la pestaña Buffs de Personaje no se
	/// salen de su caja, en los dos idiomas del mod y a varias resoluciones de ventana.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que hace falta esto.</b> El reporte real del usuario ("hay que reajustar las frases
	/// se salen del recuadro... esta todo super apretado en buff") venia de probar el mod a mano,
	/// con capturas. Antes de dar el arreglo por bueno hace falta lo mismo: el juego real
	/// corriendo, con las frases REALES (los dos idiomas, que tienen longitudes distintas) y a
	/// mas de una resolucion, no solo releer el codigo.
	/// </para>
	/// <para>
	/// <b>Resoluciones probadas, y por que esas tres.</b> <c>Main.SetDisplayMode</c> es el mismo
	/// metodo publico que usa el menu de resolucion de vanilla (recalcula UIScale, reconstruye los
	/// render targets y llama a <c>UserInterface.ActiveInstance.Recalculate()</c> el solo, asi que
	/// es el camino real, no un truco). 1600x900 y 1280x720 son las dos que pidio el usuario;
	/// 800x720 es el minimo real que admite el motor (<c>Main.minScreenW</c>/<c>minScreenH</c>,
	/// visto en el <c>Main.cs</c> decompilado) - por debajo de eso el propio juego lo redondea
	/// hacia arriba, asi que no hay resolucion mas pequeña que probar.
	/// </para>
	/// <para>
	/// <b>Como mide, no solo mira.</b> Para el recuadro naranja no basta con la captura: se repite
	/// aqui, de forma independiente, la MISMA medicion real de texto que hace
	/// <c>PestanaMundo.RecalcularAviso</c> (particion con <c>EtiquetaTk.PartirEnLineas</c> +
	/// <c>MeasureString</c> con la fuente real) a partir de la geometria YA DIBUJADA
	/// (<c>GetDimensions</c>/<c>GetInnerDimensions</c>, no la que se penso al construir), y se
	/// compara el borde inferior real del texto contra el borde inferior real de la caja.
	/// </para>
	/// </remarks>
	public static class AutopruebaEspaciado
	{
		public const string Variable = "TERRAKEEP_AUTOTEST_ESPACIADO";

		private const int FotogramasDeEspera = 180;
		private const int FotogramasEntrePasos = 10;
		private const int FotogramasTrasResolucion = 30;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _espera;
		private static readonly Queue<Action> _acciones = new Queue<Action>();
		private static bool _colaConstruida;

		private static readonly (int Ancho, int Alto, string Nombre)[] Resoluciones = {
			(1600, 900, "1600x900"),
			(1280, 720, "1280x720"),
			(800, 720, "800x720-minimo"),
		};

		private static readonly (IdiomaDeTerrakeep Idioma, string Nombre)[] Idiomas_ = {
			(IdiomaDeTerrakeep.Espanol, "es"),
			(IdiomaDeTerrakeep.English, "en"),
		};

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

			if (!_colaConstruida) {
				_colaConstruida = true;
				ConstruirCola();
				Registro.Linea("AUTOPRUEBA ESPACIADO: " + _acciones.Count + " pasos en cola. " +
					"Resolucion de partida " + Main.screenWidth + "x" + Main.screenHeight + ".");
			}

			if (_espera > 0) {
				_espera--;
				return;
			}

			if (_acciones.Count == 0) {
				Terminar();
				return;
			}

			_espera = FotogramasEntrePasos;
			try {
				_acciones.Dequeue()();
			}
			catch (Exception e) {
				Registro.Linea("AUTOPRUEBA ESPACIADO: EXCEPCION: " + e);
				_terminada = true;
			}
		}

		/// <summary>
		/// Monta la cola completa: para cada resolucion, para cada idioma, mide el recuadro
		/// naranja de Exploracion/Mundo y la pestaña Buffs. Los buffs de prueba y el idioma inicial
		/// se dejan puestos una sola vez al principio.
		/// </summary>
		private static void ConstruirCola()
		{
			_acciones.Enqueue(PoblarBuffsDePrueba);

			foreach (var resolucion in Resoluciones) {
				var res = resolucion;
				_acciones.Enqueue(() => CambiarResolucion(res.Ancho, res.Alto, res.Nombre));

				foreach (var idioma in Idiomas_) {
					var idi = idioma;
					_acciones.Enqueue(() => CambiarIdioma(idi.Idioma, idi.Nombre));
					_acciones.Enqueue(() => AbrirMundo(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => MedirYCapturarAviso(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirBuffs(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => CapturarBuffs(res.Nombre, idi.Nombre));
				}
			}
		}

		// -----------------------------------------------------------------------------------

		private static void PoblarBuffsDePrueba()
		{
			Player jugador = Main.LocalPlayer;
			int puestos = 0;
			for (int tipo = 1; tipo < BuffLoader.BuffCount && puestos < 6; tipo++) {
				string nombre = PersonajeVivo.NombreBuff(tipo);
				if (string.IsNullOrEmpty(nombre)) {
					continue;
				}
				jugador.AddBuff(tipo, 600 * 60);
				puestos++;
			}
			Registro.Linea("AUTOPRUEBA ESPACIADO - buffs de prueba puestos: " + puestos +
				" (CountBuffs=" + jugador.CountBuffs() + "). Uno de ellos con nombre largo cuenta " +
				"como el caso mas exigente para la fila de \"activos\".");
		}

		private static void CambiarResolucion(int ancho, int alto, string nombreRes)
		{
			Main.SetDisplayMode(ancho, alto, false);
			Registro.Linea("AUTOPRUEBA ESPACIADO - resolucion pedida " + nombreRes +
				" -> real tras SetDisplayMode: " + Main.screenWidth + "x" + Main.screenHeight +
				", UIScale=" + Main.UIScale + ".");
			_espera = FotogramasTrasResolucion;
		}

		private static void CambiarIdioma(IdiomaDeTerrakeep idioma, string nombreIdioma)
		{
			bool cambio = Idiomas.Aplicar(idioma, "autoprueba de espaciado");
			Registro.Linea("AUTOPRUEBA ESPACIADO - idioma pedido " + nombreIdioma +
				" -> cultura activa ahora: " + Idiomas.CulturaActiva + " (cambio real=" + cambio + ").");
		}

		private static void AbrirMundo(string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Exploracion,
				"autoprueba de espaciado (" + nombreRes + "/" + nombreIdioma + ")");
			ContenidoExploracion exploracion = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Exploracion : null;
			// Indice 2 = "Mundo", mismo orden que ContenidoExploracion._clavesPestana (Mapa,
			// Busqueda, Mundo).
			exploracion?.CambiarPestana(2);
		}

		private static void MedirYCapturarAviso(string nombreRes, string nombreIdioma)
		{
			PestanaMundo mundo = PanelTerrakeepSystem.Panel != null && PanelTerrakeepSystem.Panel.Exploracion != null
				? PanelTerrakeepSystem.Panel.Exploracion.Mundo : null;
			if (mundo == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/aviso (" + nombreRes + "/" + nombreIdioma +
					"): no se encontro PestanaMundo.");
				return;
			}

			UIPanel caja = mundo.CajaAviso;
			EtiquetaTk deshacer = mundo.EtiquetaDeshacer;
			BotonTk confirmar = mundo.BotonConfirmar;
			if (caja == null || deshacer == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/aviso (" + nombreRes + "/" + nombreIdioma +
					"): la caja de aviso todavia no existe.");
				return;
			}

			CalculatedStyle dimCaja = caja.GetDimensions();
			CalculatedStyle dimInterior = caja.GetInnerDimensions();
			CalculatedStyle dimDeshacer = deshacer.GetDimensions();

			// Misma medicion real que PestanaMundo.RecalcularAviso, hecha aqui de forma
			// independiente: el texto SIN partir (tal cual lo devuelve Idiomas.Texto, con el
			// idioma activo ahora mismo) se parte con el ancho interior YA DIBUJADO y se mide con
			// la fuente real.
			string textoDeshacer = Idiomas.Texto("Exploracion.Mundo.AvisoDeshacer");
			string partido = EtiquetaTk.PartirEnLineas(textoDeshacer, dimInterior.Width, PestanaMundo.EscalaAvisoSecundario);
			float altoTexto = FontAssets.MouseText.Value.MeasureString(partido).Y * PestanaMundo.EscalaAvisoSecundario;
			float bordeInferiorTexto = dimDeshacer.Y + altoTexto;
			float bordeInferiorCaja = dimCaja.Y + dimCaja.Height;

			bool cabe = bordeInferiorTexto <= bordeInferiorCaja + 0.5f;

			bool confirmarSolapaCaja = confirmar != null &&
				confirmar.GetDimensions().Y < bordeInferiorCaja;

			Registro.Linea("AUTOPRUEBA ESPACIADO/aviso (" + nombreRes + "/" + nombreIdioma + ") - " +
				"caja en x=" + (int)dimCaja.X + " y=" + (int)dimCaja.Y + " " +
				(int)dimCaja.Width + "x" + (int)dimCaja.Height + " (interior " + (int)dimInterior.Width +
				" px de ancho). Ultima linea (\"Deshacer\") top=" + (int)dimDeshacer.Y +
				", alto real del texto YA partido=" + altoTexto.ToString("0.0") +
				" -> borde inferior del texto=" + (int)bordeInferiorTexto +
				" vs borde inferior de la caja=" + (int)bordeInferiorCaja + " -> " +
				(cabe ? "OK: el texto cabe dentro del recuadro." : "FALLO: el texto SE SALE del recuadro.") +
				" Boton confirmar en y=" + (confirmar != null ? ((int)confirmar.GetDimensions().Y).ToString() : "?") +
				" -> " + (confirmarSolapaCaja ? "FALLO: se solapa con la caja de aviso." : "OK: no se solapa."));

			Registro.Linea("AUTOPRUEBA ESPACIADO/aviso - " +
				CapturaDePantalla.Guardar("aviso-" + nombreRes + "-" + nombreIdioma));
		}

		private static void AbrirBuffs(string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje,
				"autoprueba de espaciado (" + nombreRes + "/" + nombreIdioma + ")");
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			// Indice 3 = "Buffs", mismo orden que ContenidoPersonaje.ClavesPestana.
			personaje?.IrAPestana(3);
		}

		private static void CapturarBuffs(string nombreRes, string nombreIdioma)
		{
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			PestanaBuffs buffs = personaje != null ? personaje.BuscarPrimero<PestanaBuffs>() : null;

			Registro.Linea("AUTOPRUEBA ESPACIADO/buffs (" + nombreRes + "/" + nombreIdioma + ") - " +
				(buffs != null ? "pestaña Buffs dibujada." : "NO se encontro PestanaBuffs.") + " " +
				CapturaDePantalla.Guardar("buffs-" + nombreRes + "-" + nombreIdioma));
		}

		private static void Terminar()
		{
			_terminada = true;
			// Se deja la resolucion tal como estaba pedida al arrancar el juego, para no dejar la
			// ventana rara si alguien se conecta a inspeccionar el sandbox despues.
			Registro.Linea("AUTOPRUEBA ESPACIADO COMPLETA.");
		}

		/// <summary>
		/// Evidencia propia, independiente de <see cref="RegistroPanel"/> (que solo escribe a
		/// archivo cuando ESTA activa <c>AutopruebaPanelUnico</c>): mismo patron -log del juego +
		/// archivo propio en la carpeta de guardado en uso-, para no depender de otra autoprueba ni
		/// tocar su archivo compartido.
		/// </summary>
		private static class Registro
		{
			public const string NombreArchivo = "terrakeep-espaciado-evidencia.log";
			private static bool _archivoIniciado;

			public static void Linea(string linea)
			{
				if (Terrakeep.Instance != null) {
					Terrakeep.Instance.Logger.Info("[Terrakeep] " + linea);
				}
				try {
					string ruta = Path.Combine(Main.SavePath, NombreArchivo);
					if (!_archivoIniciado) {
						_archivoIniciado = true;
						File.WriteAllText(ruta,
							"# Evidencia de espaciado (recuadro naranja + pestaña Buffs) - " +
							DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
					}
					File.AppendAllText(ruta, "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + linea + Environment.NewLine);
				}
				catch (Exception) {
					// Si no se puede escribir el archivo (permisos, disco), el log del juego ya
					// tiene la evidencia: no vale la pena tumbar nada por esto.
				}
			}
		}
	}
}
