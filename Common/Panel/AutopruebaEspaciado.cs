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
using TerrakeepMod.Common.Investigacion;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Ajustes;
using TerrakeepMod.UI.Builds;
using TerrakeepMod.UI.Exploracion;
using TerrakeepMod.UI.Investigacion;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;
using Terrakeep.Core.Data;

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
			// --- Ampliacion 13-sep-2026: auditoria de espaciado para TODO el mod, no solo el
			// recuadro naranja y Buffs. Ver bitacora.md. Las dos siguientes preparan contenido
			// realista UNA sola vez (no depende de resolucion/idioma): objetos reales en
			// Inventario/Almacenes/Equipo (reutilizando AutopruebaPersonaje, sin duplicar la
			// logica) y dos entradas reales en el historial de deshacer para medir los botones
			// Deshacer/Rehacer con su texto largo real en vez del estado vacio por defecto.
			_acciones.Enqueue(PoblarPersonajeDePrueba);
			_acciones.Enqueue(PrepararAjustesDePrueba);

			foreach (var resolucion in Resoluciones) {
				var res = resolucion;
				_acciones.Enqueue(() => CambiarResolucion(res.Ancho, res.Alto, res.Nombre));

				foreach (var idioma in Idiomas_) {
					var idi = idioma;
					_acciones.Enqueue(() => CambiarIdioma(idi.Idioma, idi.Nombre));
					_acciones.Enqueue(() => AbrirMundo(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => MedirYCapturarAviso(res.Nombre, idi.Nombre));
					// v0.7.0 (paridad con escritorio 3.3.0): la ficha gana la fila de invasiones
					// vencidas - se audita el arbol ENTERO de "Este mundo", no solo el recuadro naranja.
					_acciones.Enqueue(() => AuditarYCapturarExploracion("mundo", res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => ComprobarMundo(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirBuffs(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => MedirYCapturarBuffs(res.Nombre, idi.Nombre));

					// --- Verificacion de "todo el texto se lee entero, nunca con ...", 7-sep-2026 ---
					_acciones.Enqueue(() => AbrirCarpetaLargaBuffs(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => MedirYCapturarCarpetasBuffs(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirBuilds(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => MedirYCapturarBuilds(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirLibreria(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => MedirYCapturarLibreria(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirInvestigacion(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => MedirYCapturarInvestigacion(res.Nombre, idi.Nombre));

					// --- Ampliacion 13-sep-2026: el resto de Personaje, Ajustes y Exploracion ---
					_acciones.Enqueue(() => AbrirPersonajePestana(2, res.Nombre, idi.Nombre)); // Equipo
					_acciones.Enqueue(() => AuditarYCapturarPersonaje("equipo", res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirPersonajePestana(0, res.Nombre, idi.Nombre)); // Inventario
					_acciones.Enqueue(() => AuditarYCapturarPersonaje("inventario", res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirPersonajePestana(1, res.Nombre, idi.Nombre)); // Almacenes
					_acciones.Enqueue(() => AuditarYCapturarPersonaje("almacenes", res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirPersonajePestana(5, res.Nombre, idi.Nombre)); // Desbloqueos
					_acciones.Enqueue(() => AuditarYCapturarPersonaje("desbloqueos", res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirPersonajePestana(4, res.Nombre, idi.Nombre)); // Apariencia
					_acciones.Enqueue(() => AuditarYCapturarPersonaje("apariencia", res.Nombre, idi.Nombre));

					_acciones.Enqueue(() => AbrirAjustes(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AuditarYCapturarAjustes(res.Nombre, idi.Nombre));

					_acciones.Enqueue(() => AbrirExploracionPestana(0, res.Nombre, idi.Nombre)); // Mapa
					_acciones.Enqueue(() => AuditarYCapturarExploracion("mapa", res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => ComprobarLegibilidadZoom(res.Nombre, idi.Nombre));
					// v0.7.0: Vecindad gana "Traer vecino" y un boton "Echar" por fila.
					_acciones.Enqueue(() => AbrirExploracionPestana(3, res.Nombre, idi.Nombre)); // Vecindad
					_acciones.Enqueue(() => EsperarFotogramas(45)); // primer refresco real de la lista
					_acciones.Enqueue(() => AuditarYCapturarExploracion("vecindad", res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => AbrirExploracionPestana(1, res.Nombre, idi.Nombre)); // Busqueda
					_acciones.Enqueue(() => PrepararBusqueda(res.Nombre, idi.Nombre));
					_acciones.Enqueue(() => EsperarFotogramas(180)); // dar tiempo a que el buscador progrese/termine
					_acciones.Enqueue(() => AuditarYCapturarExploracion("busqueda", res.Nombre, idi.Nombre));
				}
			}
		}

		// -----------------------------------------------------------------------------------

		/// <summary>
		/// Ticks de "9255 h 40 min", el caso EXACTO que reporto el usuario con captura ("el boton
		/// quitar se come parte de la caja de tiempo del buff"). Antes esta autoprueba solo ponia 6
		/// buffs con la duracion por defecto (600 s = 10 min): nunca llegaba a ejercitar ni el
		/// recorte de nombre largo ni el de tiempo largo de verdad, asi que nunca habria detectado
		/// el bug real.
		/// </summary>
		private const int TicksTiempoLargo = 9255 * 3600 * 60 + 40 * 60 * 60;

		private static void PoblarBuffsDePrueba()
		{
			Player jugador = Main.LocalPlayer;
			int puestos = 0;

			// El nombre mas largo de verdad entre TODOS los buffs cargados (vanilla + cualquier
			// mod), medido con la fuente real - no adivinado a mano - para forzar el caso mas
			// exigente de recorte de nombre, ademas de MUCHOS buffs a la vez (fuerza scroll real).
			var fuente = FontAssets.MouseText.Value;
			string nombreMasLargo = null;
			int tipoNombreMasLargo = -1;
			float anchoMasLargo = -1f;

			for (int tipo = 1; tipo < BuffLoader.BuffCount && puestos < 30; tipo++) {
				string nombre = PersonajeVivo.NombreBuff(tipo);
				if (string.IsNullOrEmpty(nombre)) {
					continue;
				}
				jugador.AddBuff(tipo, 600 * 60);
				puestos++;

				float ancho = fuente.MeasureString(nombre).X;
				if (ancho > anchoMasLargo) {
					anchoMasLargo = ancho;
					nombreMasLargo = nombre;
					tipoNombreMasLargo = tipo;
				}
			}

			// Al buff de nombre mas largo se le fuerza ademas el tiempo largo real del reporte:
			// escribir buffTime directamente (no AddBuff, que no garantiza una duracion tan larga)
			// es seguro aqui porque es SOLO este arnes de pruebas, detras de su variable de entorno.
			if (tipoNombreMasLargo > 0) {
				int indice = jugador.FindBuffIndex(tipoNombreMasLargo);
				if (indice >= 0) {
					jugador.buffTime[indice] = TicksTiempoLargo;
				}
			}

			Registro.Linea("AUTOPRUEBA ESPACIADO - buffs de prueba puestos: " + puestos +
				" (CountBuffs=" + jugador.CountBuffs() + "). Nombre mas largo real=\"" +
				nombreMasLargo + "\" (id " + tipoNombreMasLargo + ", " + anchoMasLargo.ToString("0") +
				"px sin escalar), forzado ademas a " + TicksTiempoLargo + " ticks (9255 h 40 min, " +
				"el caso real del reporte del usuario) - el caso mas exigente para la fila.");
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

		/// <summary>
		/// Mide de verdad, con los datos REALES ya dibujados (no supuestos): (1) el nombre de cada
		/// fila se lee ENTERO - nunca "..." - envuelto a tantas lineas como haga falta y sin que
		/// ninguna linea mida mas que su caja; (2) la segunda linea (tiempo + boton "Quitar"/
		/// "Aplicar") nunca invade la zona del nombre, ni tiempo se solapa con el boton; (3) la fila
		/// entera es lo bastante alta para las dos lineas; (4) la columna "Activos" deja de verdad
		/// mas hueco a "Añadir" cuando sobra sitio (<c>PestanaBuffs.RecalcularColumnas</c>); y (5) el
		/// titulo "Buffs activos: X de Y" tampoco se recorta.
		/// </summary>
		private static void MedirYCapturarBuffs(string nombreRes, string nombreIdioma)
		{
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			PestanaBuffs buffs = personaje != null ? personaje.BuscarPrimero<PestanaBuffs>() : null;

			if (buffs == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/buffs (" + nombreRes + "/" + nombreIdioma +
					"): NO se encontro PestanaBuffs.");
				return;
			}

			// Ademas de los buffs activos (puestos por PoblarBuffsDePrueba), se fuerza tambien una
			// busqueda amplia en "Añadir un buff" para que la lista de RESULTADOS tenga filas reales
			// que medir (por defecto, sin carpeta ni busqueda, esa lista solo enseña el aviso de
			// "elige una carpeta" - vacia de filas de verdad).
			buffs.BuscarParaPrueba("a");

			var fuente = FontAssets.MouseText.Value;
			int fallosActivos = MedirFilasActivas(buffs.FilasActivasParaPrueba, nombreRes, nombreIdioma, fuente);
			int fallosResultados = MedirFilasResultado(buffs.FilasResultadoParaPrueba, nombreRes, nombreIdioma, fuente);

			// El titulo "Buffs activos: X de Y ranuras" tampoco puede recortarse ni desbordar su
			// propia caja: se mide igual que las filas, con el texto YA envuelto que devuelve
			// TextoActual.
			int fallosTitulo = 0;
			if (buffs.TituloActivos != null) {
				CalculatedStyle dimTitulo = buffs.TituloActivos.GetDimensions();
				string textoTitulo = buffs.TituloActivos.TextoActual;
				float anchoTitulo = fuente.MeasureString(textoTitulo).X * 0.85f;
				if (dimTitulo.Width > 0f && anchoTitulo > dimTitulo.Width + 0.5f) {
					fallosTitulo++;
					Registro.Linea("AUTOPRUEBA ESPACIADO/buffs (" + nombreRes + "/" + nombreIdioma +
						") - FALLO: titulo \"" + textoTitulo + "\" mide " + anchoTitulo.ToString("0.0") +
						"px en caja de " + dimTitulo.Width.ToString("0.0") + "px.");
				}
			}

			// Evidencia real de que la columna izquierda se acota cuando sobra sitio (punto 2 de
			// la verificacion): ancho real ya recalculado vs el 50% "de toda la vida" sin tope.
			float anchoIzquierda = buffs.ColumnaActivos != null ? buffs.ColumnaActivos.GetDimensions().Width : -1f;
			float anchoAnadir = buffs.ColumnaAnadir != null ? buffs.ColumnaAnadir.GetDimensions().Width : -1f;
			float anchoTotalTab = buffs.GetDimensions().Width;
			float mitadSinTope = anchoTotalTab * 0.5f - 10f;

			int totalFallos = fallosActivos + fallosResultados + fallosTitulo;
			Registro.Linea("AUTOPRUEBA ESPACIADO/buffs (" + nombreRes + "/" + nombreIdioma + ") - " +
				buffs.FilasActivasParaPrueba.Count + " filas activas + " +
				buffs.FilasResultadoParaPrueba.Count + " filas de resultados medidas -> " +
				(totalFallos == 0 ? "OK, nada recortado ni solapado" : (totalFallos + " FALLO(S)")) + ". " +
				"Columna Activos=" + anchoIzquierda.ToString("0") + "px (50% sin tope seria " +
				mitadSinTope.ToString("0") + "px), Añadir=" + anchoAnadir.ToString("0") + "px, " +
				"pestaña=" + anchoTotalTab.ToString("0") + "px.");

			// La captura incluye ya la busqueda "a" aplicada (BuscarParaPrueba, mas arriba), asi que
			// la columna de resultados de la derecha tambien queda documentada en imagen, no solo
			// medida por codigo.
			Registro.Linea("AUTOPRUEBA ESPACIADO/buffs (" + nombreRes + "/" + nombreIdioma + ") - " +
				CapturaDePantalla.Guardar("buffs-" + nombreRes + "-" + nombreIdioma));
		}

		/// <summary>
		/// Mide las filas de "Buffs activos" (nombre envuelto en la linea 1, tiempo + boton
		/// "Quitar" en la linea 2): ninguna linea del nombre debe medir mas que su caja, el nombre
		/// no debe invadir verticalmente la linea 2, tiempo no debe solaparse con el boton, y la
		/// fila debe ser lo bastante alta para las dos lineas. Devuelve cuantas filas fallaron
		/// (cada fallo se registra con su propia linea de log).
		/// </summary>
		private static int MedirFilasActivas(
			System.Collections.Generic.List<(UIElement Fila, EtiquetaTk Nombre, EtiquetaTk Tiempo, BotonTk Quitar)> filas,
			string nombreRes, string nombreIdioma, ReLogic.Graphics.DynamicSpriteFont fuente)
		{
			int fallos = 0;

			for (int i = 0; i < filas.Count; i++) {
				var f = filas[i];
				CalculatedStyle dimFila = f.Fila.GetDimensions();
				CalculatedStyle dimNombre = f.Nombre.GetDimensions();
				CalculatedStyle dimTiempo = f.Tiempo.GetDimensions();
				CalculatedStyle dimQuitar = f.Quitar.GetDimensions();

				string textoNombre = f.Nombre.TextoActual;
				Vector2 tamanoNombre = fuente.MeasureString(textoNombre) * 0.8f;

				bool desbordeAncho = tamanoNombre.X > dimNombre.Width + 0.5f;
				bool nombreInvadeLinea2 = (dimNombre.Y + tamanoNombre.Y) > dimQuitar.Y + 0.5f;
				bool filaDemasiadoBaja = (dimQuitar.Y + dimQuitar.Height) > (dimFila.Y + dimFila.Height + 0.5f);
				bool tiempoSolapaBoton = (dimTiempo.X + dimTiempo.Width) > dimQuitar.X + 0.5f;

				if (desbordeAncho || nombreInvadeLinea2 || filaDemasiadoBaja || tiempoSolapaBoton) {
					fallos++;
					Registro.Linea("AUTOPRUEBA ESPACIADO/buffs (" + nombreRes + "/" + nombreIdioma +
						") - ACTIVOS FILA " + i + ": FALLO. nombre=\"" + textoNombre.Replace("\n", " | ") +
						"\" mide " + tamanoNombre.X.ToString("0.0") + "x" + tamanoNombre.Y.ToString("0.0") +
						"px en caja " + dimNombre.Width.ToString("0.0") + "x" + dimNombre.Height.ToString("0.0") +
						"px (desbordeAncho=" + desbordeAncho + ", invadeLinea2=" + nombreInvadeLinea2 +
						", filaBaja=" + filaDemasiadoBaja + ", tiempoSolapaBoton=" + tiempoSolapaBoton + ").");
				}
			}

			return fallos;
		}

		/// <summary>Igual que <see cref="MedirFilasActivas"/> pero para "Añadir un buff"
		/// (nombre envuelto en la linea 1, solo el boton "Aplicar" en la linea 2, sin tiempo).</summary>
		private static int MedirFilasResultado(
			System.Collections.Generic.List<(UIElement Fila, EtiquetaTk Nombre, BotonTk Aplicar)> filas,
			string nombreRes, string nombreIdioma, ReLogic.Graphics.DynamicSpriteFont fuente)
		{
			int fallos = 0;

			for (int i = 0; i < filas.Count; i++) {
				var f = filas[i];
				CalculatedStyle dimFila = f.Fila.GetDimensions();
				CalculatedStyle dimNombre = f.Nombre.GetDimensions();
				CalculatedStyle dimAplicar = f.Aplicar.GetDimensions();

				string textoNombre = f.Nombre.TextoActual;
				Vector2 tamanoNombre = fuente.MeasureString(textoNombre) * 0.8f;

				bool desbordeAncho = tamanoNombre.X > dimNombre.Width + 0.5f;
				bool nombreInvadeLinea2 = (dimNombre.Y + tamanoNombre.Y) > dimAplicar.Y + 0.5f;
				bool filaDemasiadoBaja = (dimAplicar.Y + dimAplicar.Height) > (dimFila.Y + dimFila.Height + 0.5f);

				if (desbordeAncho || nombreInvadeLinea2 || filaDemasiadoBaja) {
					fallos++;
					Registro.Linea("AUTOPRUEBA ESPACIADO/buffs (" + nombreRes + "/" + nombreIdioma +
						") - AÑADIR FILA " + i + ": FALLO. nombre=\"" + textoNombre.Replace("\n", " | ") +
						"\" mide " + tamanoNombre.X.ToString("0.0") + "x" + tamanoNombre.Y.ToString("0.0") +
						"px en caja " + dimNombre.Width.ToString("0.0") + "x" + dimNombre.Height.ToString("0.0") +
						"px (desbordeAncho=" + desbordeAncho + ", invadeLinea2=" + nombreInvadeLinea2 +
						", filaBaja=" + filaDemasiadoBaja + ").");
				}
			}

			return fallos;
		}

		// ============================================================================================
		// 7-sep-2026 - "todo el texto se lee entero, nunca con ...": pildoras de Builds, carpetas
		// de Libreria/Buffs y su ruta, y el arbol + lista de Investigacion. Ver bitacora.md.
		// ============================================================================================

		/// <summary>
		/// Camino descendiendo por el arbol de carpetas (Libreria/Buffs, mismo tipo de nodo
		/// <c>CategoryTreeNodeData</c>), eligiendo en CADA nivel el hijo de nombre MAS LARGO -
		/// fuerza tanto una ruta larga (para el envoltorio del breadcrumb) como nombres de fila
		/// largos de verdad en el ultimo nivel (para el envoltorio de la fila), sin tener que
		/// adivinar a mano ningun nombre real del catalogo.
		/// </summary>
		private static List<CategoryTreeNodeData> CaminoLargo(IReadOnlyList<CategoryTreeNodeData> raices, int nivelesMax)
		{
			List<CategoryTreeNodeData> camino = new List<CategoryTreeNodeData>();
			IReadOnlyList<CategoryTreeNodeData> nivelActual = raices;

			for (int nivel = 0; nivel < nivelesMax && nivelActual != null && nivelActual.Count > 0; nivel++) {
				CategoryTreeNodeData mejor = null;
				int mejorLargo = -1;
				foreach (CategoryTreeNodeData n in nivelActual) {
					int largo = (n.Name ?? "").Length;
					if (largo > mejorLargo) {
						mejorLargo = largo;
						mejor = n;
					}
				}
				if (mejor == null) {
					break;
				}
				camino.Add(mejor);
				nivelActual = mejor.Children;
			}

			return camino;
		}

		/// <summary>Igual que <see cref="CaminoLargo"/> pero para el arbol de Investigacion
		/// (<c>CarpetaInvestigacion</c>, un tipo de nodo distinto), y de paso deja DESPLEGADAS
		/// todas las carpetas del camino salvo la ultima - si no, la fila de la carpeta objetivo ni
		/// siquiera se pintaria en el arbol (empieza colapsado).</summary>
		private static List<CarpetaInvestigacion> CaminoLargoInvestigacion(IReadOnlyList<CarpetaInvestigacion> raices, int nivelesMax)
		{
			List<CarpetaInvestigacion> camino = new List<CarpetaInvestigacion>();
			IReadOnlyList<CarpetaInvestigacion> nivelActual = raices;

			for (int nivel = 0; nivel < nivelesMax && nivelActual != null && nivelActual.Count > 0; nivel++) {
				CarpetaInvestigacion mejor = null;
				int mejorLargo = -1;
				foreach (CarpetaInvestigacion n in nivelActual) {
					int largo = (n.Nombre ?? "").Length;
					if (largo > mejorLargo) {
						mejorLargo = largo;
						mejor = n;
					}
				}
				if (mejor == null) {
					break;
				}
				if (camino.Count > 0) {
					camino[camino.Count - 1].Desplegada = true;
				}
				camino.Add(mejor);
				nivelActual = mejor.Hijos;
			}

			return camino;
		}

		private static void AbrirCarpetaLargaBuffs(string nombreRes, string nombreIdioma)
		{
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			PestanaBuffs buffs = personaje != null ? personaje.BuscarPrimero<PestanaBuffs>() : null;
			if (buffs == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/carpetas-buffs (" + nombreRes + "/" + nombreIdioma +
					"): no se encontro PestanaBuffs.");
				return;
			}

			ArbolBuffs.ConstruirSiHaceFalta();
			List<CategoryTreeNodeData> camino = CaminoLargo(ArbolBuffs.Raices, 3);
			foreach (CategoryTreeNodeData nodo in camino) {
				buffs.AbrirCarpetaParaPrueba(nodo);
			}

			Registro.Linea("AUTOPRUEBA ESPACIADO/carpetas-buffs (" + nombreRes + "/" + nombreIdioma +
				") - camino abierto (" + camino.Count + " niveles): " +
				string.Join(" > ", camino.ConvertAll(n => "\"" + n.Name + "\"")));
		}

		private static void MedirYCapturarCarpetasBuffs(string nombreRes, string nombreIdioma)
		{
			Registro.Linea("AUTOPRUEBA ESPACIADO/carpetas-buffs (" + nombreRes + "/" + nombreIdioma + ") - " +
				CapturaDePantalla.Guardar("buffs-carpetas-" + nombreRes + "-" + nombreIdioma));
		}

		private static void AbrirBuilds(string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Builds,
				"autoprueba de espaciado (" + nombreRes + "/" + nombreIdioma + ")");
			ContenidoBuilds builds = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Builds : null;
			if (builds == null) {
				return;
			}
			// Indice 1 = etapa "temprana" (earlyhardmode): la de la etiqueta real mas larga del
			// catalogo vanilla ("Hardmode temprano (antes de los jefes mecanicos)" / "Early
			// Hardmode (before the mechanical bosses)"), el caso mas exigente para las pildoras.
			builds.SeleccionarEtapa(1);
		}

		/// <summary>
		/// Mide de verdad, con la geometria YA dibujada, que NINGUNA pildora de las 3 filas
		/// (etapa/clase/conjunto de destino - la de fuente solo existe con Calamity instalado)
		/// mide su texto mas ancho que la caja real que el <see cref="GrupoPildoras.Reflow"/> le
		/// dio.
		/// </summary>
		private static void MedirYCapturarBuilds(string nombreRes, string nombreIdioma)
		{
			ContenidoBuilds builds = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Builds : null;
			if (builds == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/builds (" + nombreRes + "/" + nombreIdioma +
					"): no se encontro ContenidoBuilds.");
				return;
			}

			var fuente = FontAssets.MouseText.Value;
			int fallos = 0;
			int pildoras = 0;

			// TM5 del catalogo de rediseño visual: fuente y etapa ya no son filas de pildoras
			// (fuente es un alternador de dos estados, etapa es un DesplegableTk) - solo quedan
			// clase y conjunto de destino con el mecanismo real de GrupoPildoras.Reflow que esta
			// funcion audita.
			foreach (UIElement fila in new[] {
				builds.FilaClasesParaPrueba, builds.FilaLoadoutParaPrueba
			}) {
				if (fila == null) {
					continue;
				}
				foreach (UIElement hijo in fila.Children) {
					BotonTk pildora = hijo as BotonTk;
					if (pildora == null) {
						continue;
					}
					pildoras++;
					CalculatedStyle dim = pildora.GetDimensions();
					Vector2 medida = fuente.MeasureString(pildora.Texto) * 0.75f;
					if (dim.Width > 0f && medida.X > dim.Width + 0.5f) {
						fallos++;
						Registro.Linea("AUTOPRUEBA ESPACIADO/builds (" + nombreRes + "/" + nombreIdioma +
							") - FALLO: pildora \"" + pildora.Texto.Replace("\n", " | ") + "\" mide " +
							medida.X.ToString("0.0") + "px en caja de " + dim.Width.ToString("0.0") + "px.");
					}
				}
			}

			Registro.Linea("AUTOPRUEBA ESPACIADO/builds (" + nombreRes + "/" + nombreIdioma + ") - " +
				pildoras + " pildoras medidas -> " + (fallos == 0 ? "OK, ninguna recorta su texto" : (fallos + " FALLO(S)")) + ".");
			Registro.Linea("AUTOPRUEBA ESPACIADO/builds (" + nombreRes + "/" + nombreIdioma + ") - " +
				CapturaDePantalla.Guardar("builds-" + nombreRes + "-" + nombreIdioma));
		}

		private static void AbrirLibreria(string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Libreria,
				"autoprueba de espaciado (" + nombreRes + "/" + nombreIdioma + ")");
			ContenidoLibreria libreria = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Libreria : null;
			if (libreria == null) {
				return;
			}

			ArbolLibreria.ConstruirSiHaceFalta();
			List<CategoryTreeNodeData> camino = CaminoLargo(ArbolLibreria.Raices, 3);
			foreach (CategoryTreeNodeData nodo in camino) {
				libreria.AbrirCarpeta(nodo);
			}

			Registro.Linea("AUTOPRUEBA ESPACIADO/libreria (" + nombreRes + "/" + nombreIdioma +
				") - camino abierto (" + camino.Count + " niveles): " +
				string.Join(" > ", camino.ConvertAll(n => "\"" + n.Name + "\"")) +
				" -> RutaActual=\"" + libreria.RutaActual + "\" (" + libreria.RutaActual.Length + " caracteres).");
		}

		private static void MedirYCapturarLibreria(string nombreRes, string nombreIdioma)
		{
			Registro.Linea("AUTOPRUEBA ESPACIADO/libreria (" + nombreRes + "/" + nombreIdioma + ") - " +
				CapturaDePantalla.Guardar("libreria-" + nombreRes + "-" + nombreIdioma));
		}

		private static void AbrirInvestigacion(string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Investigacion,
				"autoprueba de espaciado (" + nombreRes + "/" + nombreIdioma + ")");
			ContenidoInvestigacion investigacion = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Investigacion : null;
			if (investigacion == null) {
				return;
			}

			CatalogoInvestigacion.RefrescarContadores();
			List<CarpetaInvestigacion> camino = CaminoLargoInvestigacion(CatalogoInvestigacion.Raices, 4);
			if (camino.Count > 0) {
				investigacion.Seleccionar(camino[camino.Count - 1], true);
			}

			Registro.Linea("AUTOPRUEBA ESPACIADO/investigacion (" + nombreRes + "/" + nombreIdioma +
				") - camino abierto (" + camino.Count + " niveles): " +
				string.Join(" > ", camino.ConvertAll(n => "\"" + n.Nombre + "\"")));
		}

		private static void MedirYCapturarInvestigacion(string nombreRes, string nombreIdioma)
		{
			Registro.Linea("AUTOPRUEBA ESPACIADO/investigacion (" + nombreRes + "/" + nombreIdioma + ") - " +
				CapturaDePantalla.Guardar("investigacion-" + nombreRes + "-" + nombreIdioma));
		}

		// ============================================================================================
		// 13-sep-2026 - Auditoria de espaciado para TODO el mod (editor de personaje, catalogo de
		// objetos, Ajustes, Exploracion): "ningun texto mas grande que su caja, nada se solapa".
		// Ver bitacora.md.
		// ============================================================================================

		/// <summary>Deja Inventario/Almacenes/Equipo con objetos REALES (mismo camino que WS1,
		/// reutilizado sin duplicar la logica: <see cref="AutopruebaPersonaje.PoblarInventario"/> y
		/// compañia se cambiaron de <c>private</c> a <c>internal</c> justo para esto). Se hace UNA
		/// sola vez: el contenido no depende de la resolucion ni del idioma.</summary>
		private static void PoblarPersonajeDePrueba()
		{
			AutopruebaPersonaje.PoblarInventario();
			AutopruebaPersonaje.PoblarAlmacenes();
			AutopruebaPersonaje.PoblarEquipo();
			Registro.Linea("AUTOPRUEBA ESPACIADO - Inventario/Almacenes/Equipo poblados con objetos reales.");
		}

		/// <summary>Dos entradas reales en el historial de deshacer, para que los botones
		/// Deshacer/Rehacer del area de Ajustes enseñen su rotulo LARGO real
		/// ("Deshacer: <![CDATA[<etiqueta>]]>") en vez del texto corto del estado vacio - el caso mas
		/// exigente para esa caja. Se deja ademas una entrada rehacible (un <c>Deshacer()</c> de mas)
		/// para medir tambien el boton Rehacer con texto real.</summary>
		private static void PrepararAjustesDePrueba()
		{
			Historial.CambiarValor("vida máxima (autoprueba de espaciado)", 100, 500, v => { });
			Historial.CambiarValor("maná máximo (autoprueba de espaciado)", 20, 200, v => { });
			Historial.Deshacer();
			Registro.Linea("AUTOPRUEBA ESPACIADO - historial de deshacer preparado: " +
				Historial.Pila.Cuenta + " entrada(s), PuedeDeshacer=" + Historial.Pila.PuedeDeshacer +
				", PuedeRehacer=" + Historial.Pila.PuedeRehacer + ".");
		}

		private static void AbrirPersonajePestana(int indice, string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje,
				"autoprueba de espaciado (" + nombreRes + "/" + nombreIdioma + ")");
			PanelTerrakeepSystem.Panel?.Personaje?.IrAPestana(indice);
		}

		private static void AuditarYCapturarPersonaje(string nombreCorto, string nombreRes, string nombreIdioma)
		{
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Personaje : null;
			if (personaje == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/" + nombreCorto + " (" + nombreRes + "/" + nombreIdioma +
					"): no se encontro ContenidoPersonaje.");
				return;
			}
			AuditarArbol(personaje, nombreCorto + " (" + nombreRes + "/" + nombreIdioma + ")");
			Registro.Linea("AUTOPRUEBA ESPACIADO/" + nombreCorto + " (" + nombreRes + "/" + nombreIdioma + ") - " +
				CapturaDePantalla.Guardar(nombreCorto + "-" + nombreRes + "-" + nombreIdioma));
		}

		private static void AbrirAjustes(string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Ajustes,
				"autoprueba de espaciado (" + nombreRes + "/" + nombreIdioma + ")");
		}

		private static void AuditarYCapturarAjustes(string nombreRes, string nombreIdioma)
		{
			ContenidoAjustes ajustes = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Ajustes : null;
			if (ajustes == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/ajustes (" + nombreRes + "/" + nombreIdioma +
					"): no se encontro ContenidoAjustes.");
				return;
			}
			AuditarArbol(ajustes, "ajustes (" + nombreRes + "/" + nombreIdioma + ")");
			Registro.Linea("AUTOPRUEBA ESPACIADO/ajustes (" + nombreRes + "/" + nombreIdioma + ") - " +
				CapturaDePantalla.Guardar("ajustes-" + nombreRes + "-" + nombreIdioma));
		}

		private static void AbrirExploracionPestana(int indice, string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Exploracion,
				"autoprueba de espaciado (" + nombreRes + "/" + nombreIdioma + ")");
			PanelTerrakeepSystem.Panel?.Exploracion?.CambiarPestana(indice);
		}

		private static void AuditarYCapturarExploracion(string nombreCorto, string nombreRes, string nombreIdioma)
		{
			ContenidoExploracion exploracion = PanelTerrakeepSystem.Panel != null ? PanelTerrakeepSystem.Panel.Exploracion : null;
			if (exploracion == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/" + nombreCorto + " (" + nombreRes + "/" + nombreIdioma +
					"): no se encontro ContenidoExploracion.");
				return;
			}
			AuditarArbol(exploracion, nombreCorto + " (" + nombreRes + "/" + nombreIdioma + ")");
			Registro.Linea("AUTOPRUEBA ESPACIADO/" + nombreCorto + " (" + nombreRes + "/" + nombreIdioma + ") - " +
				CapturaDePantalla.Guardar(nombreCorto + "-" + nombreRes + "-" + nombreIdioma));
		}

		/// <summary>
		/// v0.7.0: "Este mundo" tiene que caber ENTERO en su pestaña. La ficha (con la fila nueva de
		/// invasiones) se reparte con ReflowVertical.DistribuirRenglones y ningun renglon visible puede
		/// salirse por debajo de su caja; la columna de dificultad es una lista que no puede salirse de
		/// la pestaña (si no cabe, tiene que enseñar su barra de desplazamiento). Antes de la v0.7.0 el
		/// boton de confirmar se salia por debajo a 1280x720 y pisaba "Cerrar (P)".
		/// </summary>
		private static void ComprobarMundo(string nombreRes, string nombreIdioma)
		{
			PestanaMundo mundo = PanelTerrakeepSystem.Panel != null && PanelTerrakeepSystem.Panel.Exploracion != null
				? PanelTerrakeepSystem.Panel.Exploracion.Mundo : null;
			if (mundo == null || mundo.CajaFicha == null || mundo.ListaDificultad == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/mundo-cabe (" + nombreRes + "/" + nombreIdioma + "): no se encontro PestanaMundo.");
				return;
			}

			CalculatedStyle interior = mundo.CajaFicha.GetInnerDimensions();
			float fondoFicha = 0f;
			foreach (UIElement hijo in mundo.CajaFicha.Children) {
				EtiquetaTk etiqueta = hijo as EtiquetaTk;
				BotonTk boton = hijo as BotonTk;
				bool conTexto = (etiqueta != null && !string.IsNullOrEmpty(etiqueta.TextoActual)) ||
					(boton != null && !string.IsNullOrEmpty(boton.Texto));
				if (conTexto) {
					CalculatedStyle d = hijo.GetDimensions();
					fondoFicha = Math.Max(fondoFicha, d.Y + d.Height);
				}
			}
			bool fichaCabe = fondoFicha <= interior.Y + interior.Height + 0.5f;
			float escalaLetra = 0.8f * mundo.FactorLetraFicha;
			bool fichaLegible = escalaLetra >= ReflowVertical.EscalaMinimaLegible - 0.001f;

			CalculatedStyle pestana = mundo.GetDimensions();
			CalculatedStyle lista = mundo.ListaDificultad.GetDimensions();
			bool listaDentro = lista.Y + lista.Height <= pestana.Y + pestana.Height + 0.5f;
			float total = mundo.ListaDificultad.GetTotalHeight();
			bool scrollCoherente = mundo.ScrollDificultadVisible == (total > lista.Height + 0.5f);

			Registro.Linea("AUTOPRUEBA ESPACIADO/mundo-cabe (" + nombreRes + "/" + nombreIdioma + ") - ficha: factor de paso " +
				mundo.FactorPasoFicha.ToString("0.000") + ", letra " + escalaLetra.ToString("0.000") + ", renglones ocultos " +
				mundo.RenglonesOcultosFicha + ", fondo del ultimo renglon y=" + (int)fondoFicha + " vs caja " +
				(int)(interior.Y + interior.Height) + " -> " + (fichaCabe ? "OK: cabe." : "FALLO: la ficha se sale de su caja.") +
				" " + (fichaLegible ? "OK: legible." : "FALLO LEGIBILIDAD: letra de la ficha por debajo del minimo.") +
				" Dificultad: lista " + (int)lista.Height + " px de alto para " + (int)total + " px de contenido, barra=" +
				mundo.ScrollDificultadVisible + " -> " + (listaDentro ? "OK: dentro de la pestaña." : "FALLO: la lista se sale de la pestaña.") +
				" " + (scrollCoherente ? "OK: barra solo si hace falta." : "FALLO: barra de desplazamiento incoherente con el contenido."));
		}

		/// <summary>
		/// Cierre de la legibilidad de "Zoom" (requirement 0446b3c9, v0.7.0): la escala REAL con la
		/// que se esta dibujando "Zoom" tras el reflujo nunca puede bajar de
		/// <see cref="ReflowVertical.EscalaMinimaLegible"/>, y su caja tiene que seguir por encima del
		/// borde inferior de la pestaña (la regla 2 de <see cref="AuditarArbol"/> ya cubre el solape con
		/// los renglones de encima). Se registra tambien el tamaño aproximado en pixeles de pantalla
		/// (escala x UIScale x alto de linea de la fuente) para poder compararlo con la medicion de
		/// pixeles de la bitacora.
		/// </summary>
		private static void ComprobarLegibilidadZoom(string nombreRes, string nombreIdioma)
		{
			PestanaMapa mapa = PanelTerrakeepSystem.Panel != null && PanelTerrakeepSystem.Panel.Exploracion != null
				? PanelTerrakeepSystem.Panel.Exploracion.Mapa : null;
			if (mapa == null || mapa.EtiquetaZoom == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/zoom (" + nombreRes + "/" + nombreIdioma + "): no se encontro PestanaMapa.");
				return;
			}

			float escala = mapa.EscalaZoomActual;
			CalculatedStyle zoom = mapa.EtiquetaZoom.GetDimensions();
			CalculatedStyle pestana = mapa.GetDimensions();
			float textoAlto = FontAssets.MouseText.Value.MeasureString("Zoom").Y * escala;
			bool legible = escala >= ReflowVertical.EscalaMinimaLegible - 0.001f;
			bool dentro = zoom.Y + textoAlto <= pestana.Y + pestana.Height + 0.5f;
			Registro.Linea("AUTOPRUEBA ESPACIADO/zoom (" + nombreRes + "/" + nombreIdioma + ") - escala real de \"Zoom\"=" +
				escala.ToString("0.000") + " (minimo legible " + ReflowVertical.EscalaMinimaLegible.ToString("0.00") +
				", UIScale " + Main.UIScale.ToString("0.00") + ", ~" + (textoAlto * Main.UIScale).ToString("0.0") +
				" px de linea en pantalla) -> " + (legible ? "OK: legible." : "FALLO LEGIBILIDAD: por debajo del minimo.") +
				" Borde inferior del texto y=" + (int)(zoom.Y + textoAlto) + " vs pestaña " + (int)(pestana.Y + pestana.Height) +
				" -> " + (dentro ? "OK: dentro." : "FALLO: se sale por debajo de la pestaña."));
		}

		/// <summary>Lanza una busqueda real (mena de cobre, la usa tambien AutopruebaExploracion:
		/// existe en cualquier mundo generado, sin depender de Calamity) para que la columna de
		/// resultados tenga filas REALES que medir, no el aviso vacio de "aqui saldran".</summary>
		private static void PrepararBusqueda(string nombreRes, string nombreIdioma)
		{
			PestanaBusqueda busqueda = PanelTerrakeepSystem.Panel != null && PanelTerrakeepSystem.Panel.Exploracion != null
				? PanelTerrakeepSystem.Panel.Exploracion.Busqueda : null;
			if (busqueda == null) {
				Registro.Linea("AUTOPRUEBA ESPACIADO/busqueda (" + nombreRes + "/" + nombreIdioma +
					"): no se encontro PestanaBusqueda.");
				return;
			}

			bool encontrado = busqueda.SeleccionarPorEtiqueta("Cobre");
			if (encontrado) {
				busqueda.Buscar();
			}
			Registro.Linea("AUTOPRUEBA ESPACIADO/busqueda (" + nombreRes + "/" + nombreIdioma +
				") - objetivo \"Cobre\" seleccionado=" + encontrado + ", busqueda lanzada.");
		}

		/// <summary>No hace nada por si misma: solo alarga la espera antes del siguiente paso
		/// (<see cref="Avanzar"/> pone <c>_espera</c> a <see cref="FotogramasEntrePasos"/> justo
		/// ANTES de ejecutar la accion de la cola, asi que lo que se asigne aqui dentro gana).</summary>
		private static void EsperarFotogramas(int fotogramas)
		{
			_espera = fotogramas;
		}

		/// <summary>
		/// Recorre TODO el arbol de <paramref name="raiz"/> y comprueba, con la geometria YA
		/// dibujada (nunca la que se penso al construir), dos cosas para cada
		/// <see cref="EtiquetaTk"/>/<see cref="BotonTk"/> con texto que encuentra: (1) que su propio
		/// texto, medido con la fuente y la escala REALES, no mide mas que la caja que tiene
		/// asignada ("ningun texto mas grande que su caja"), y (2) que no se solapa con ningun otro
		/// elemento medible que comparta su mismo padre DIRECTO - comparar solo hermanos evita el
		/// falso positivo obvio de "el hijo esta dentro del padre" (contencion normal, no un fallo).
		/// <para />
		/// Generico a proposito: en vez de escribir a mano una comprobacion por cada pestaña nueva
		/// (el patron que ya usan Aviso/Buffs/Builds, mas especificas porque conocen la fila exacta
		/// que miden), este metodo barre cualquier pestaña con las mismas dos reglas. No sustituye a
		/// las comprobaciones especificas donde ya existen (conocen la relacion REAL entre elementos,
		/// no solo "se tocan los rectangulos"): las complementa en las pestañas que no tenian
		/// ninguna medicion todavia.
		/// </summary>
		private static int AuditarArbol(UIElement raiz, string contexto)
		{
			var fuente = FontAssets.MouseText.Value;
			var medibles = new List<(UIElement Elemento, string Texto, float Escala)>();

			raiz.ExecuteRecursively(elemento => {
				EtiquetaTk etiqueta = elemento as EtiquetaTk;
				if (etiqueta != null) {
					string texto = etiqueta.TextoActual;
					if (!string.IsNullOrEmpty(texto)) {
						medibles.Add((etiqueta, texto, etiqueta.Escala));
					}
					return;
				}
				BotonTk boton = elemento as BotonTk;
				if (boton != null && !string.IsNullOrEmpty(boton.Texto)) {
					medibles.Add((boton, boton.Texto, boton.EscalaTexto));
				}
			});

			int fallosDesborde = 0;
			foreach (var m in medibles) {
				CalculatedStyle dim = m.Elemento.GetDimensions();
				if (dim.Width <= 0f) {
					continue;
				}
				float anchoTexto = fuente.MeasureString(m.Texto).X * m.Escala;
				if (anchoTexto > dim.Width + 1.5f) {
					fallosDesborde++;
					Registro.Linea("AUTOPRUEBA ESPACIADO/" + contexto + " - FALLO DESBORDE: \"" +
						m.Texto.Replace("\n", " | ") + "\" (" + m.Elemento.GetType().Name + ") mide " +
						anchoTexto.ToString("0.0") + "px en caja de " + dim.Width.ToString("0.0") + "px.");
				}
			}

			int fallosSolape = 0;
			var porPadre = new Dictionary<UIElement, List<(UIElement Elemento, string Texto)>>();
			foreach (var m in medibles) {
				UIElement padre = m.Elemento.Parent;
				if (padre == null) {
					continue;
				}
				List<(UIElement Elemento, string Texto)> lista;
				if (!porPadre.TryGetValue(padre, out lista)) {
					lista = new List<(UIElement, string)>();
					porPadre[padre] = lista;
				}
				lista.Add((m.Elemento, m.Texto));
			}
			foreach (var grupo in porPadre.Values) {
				for (int i = 0; i < grupo.Count; i++) {
					CalculatedStyle a = grupo[i].Elemento.GetDimensions();
					if (a.Width <= 0f || a.Height <= 0f) {
						continue;
					}
					for (int j = i + 1; j < grupo.Count; j++) {
						CalculatedStyle b = grupo[j].Elemento.GetDimensions();
						if (b.Width <= 0f || b.Height <= 0f) {
							continue;
						}
						bool solapan = a.X < b.X + b.Width - 0.5f && a.X + a.Width - 0.5f > b.X &&
							a.Y < b.Y + b.Height - 0.5f && a.Y + a.Height - 0.5f > b.Y;
						if (solapan) {
							fallosSolape++;
							Registro.Linea("AUTOPRUEBA ESPACIADO/" + contexto + " - FALLO SOLAPE: \"" +
								grupo[i].Texto.Replace("\n", " | ") + "\" (x=" + (int)a.X + " y=" + (int)a.Y +
								" " + (int)a.Width + "x" + (int)a.Height + ") se solapa con \"" +
								grupo[j].Texto.Replace("\n", " | ") + "\" (x=" + (int)b.X + " y=" + (int)b.Y +
								" " + (int)b.Width + "x" + (int)b.Height + ").");
						}
					}
				}
			}

			// --- Regla 3 (16-sep-2026): texto contra BLOQUE hermano que contiene texto ---------
			// Bug real que las dos reglas de arriba dejaron pasar hasta que el usuario lo vio en su
			// propia partida: la nota de ayuda de Personaje/Inventario cruzaba por encima del
			// mini-panel "Editar objeto". La regla 2 solo compara textos que comparten el MISMO padre
			// directo, y ese mini-panel es un contenedor (PanelHerramientasLibreriaTk) sin texto
			// propio: ni el contenedor entraba en `medibles`, ni sus etiquetas internas ("Editar
			// objeto", "Cantidad", "Aplicar"...) eran hermanas de la nota - el par nunca se comparaba,
			// por construccion. Es una clase entera de bug, no un caso suelto: cualquier texto que
			// invada un panel vecino con contenido queda invisible para una comparacion que solo mira
			// hermanos directos. Aqui, para cada texto medible M, se miran los hermanos de M que NO
			// son medibles pero que contienen al menos un texto medible en su subarbol (un bloque con
			// contenido real, no un fondo decorativo sin texto - ese filtro es lo que evita el falso
			// positivo de "etiqueta sobre su propio fondo"), y se compara la caja de M contra la caja
			// del bloque entero Y contra cada texto de dentro. Un solape con el bloque ya cuenta como
			// fallo aunque no toque letra: es exactamente lo que el jugador ve como "se solapa".
			var esMedible = new HashSet<UIElement>();
			foreach (var m in medibles) {
				esMedible.Add(m.Elemento);
			}
			var mediblesDentroDe = new Dictionary<UIElement, List<(UIElement Elemento, string Texto)>>();
			foreach (var m in medibles) {
				UIElement ancestro = m.Elemento.Parent;
				while (ancestro != null && ancestro != raiz) {
					List<(UIElement Elemento, string Texto)> lista;
					if (!mediblesDentroDe.TryGetValue(ancestro, out lista)) {
						lista = new List<(UIElement, string)>();
						mediblesDentroDe[ancestro] = lista;
					}
					lista.Add((m.Elemento, m.Texto));
					ancestro = ancestro.Parent;
				}
			}

			int fallosBloque = 0;
			foreach (var m in medibles) {
				UIElement padre = m.Elemento.Parent;
				if (padre == null) {
					continue;
				}
				CalculatedStyle dimA = m.Elemento.GetDimensions();
				if (dimA.Width <= 0f || dimA.Height <= 0f) {
					continue;
				}
				// "a" real: el ancho VISIBLE de verdad (el texto renderizado, medido con la MISMA
				// fuente/escala que la regla 1 de desborde de arriba), no la caja declarada del
				// contenedor. Bug real de la propia autoprueba (29-sep-2026, requirement
				// 0446b3c9): una EtiquetaTk se dimensiona a menudo con un ancho de sobra para el
				// peor caso (un idioma mas largo, un numero mas grande) - "Tintes de pelo
				// encontrados..." (Personaje > Apariencia) declara 900px de caja pero el texto
				// real renderizado mide ~650px, y marcaba FALLO SOLAPE-BLOQUE contra el panel del
				// muñeco aunque el texto de verdad se queda claramente a la izquierda (confirmado
				// contra la captura real del juego, no solo con numeros). Nunca se AGRANDA el
				// ancho (Math.Min con el declarado): si la caja declarada fuera mas ESTRECHA que
				// el texto, eso ya lo marca la regla 1 (desborde) por separado.
				float anchoRealA = Math.Min(dimA.Width, fuente.MeasureString(m.Texto).X * m.Escala);
				CalculatedStyle a = new CalculatedStyle(dimA.X, dimA.Y, anchoRealA, dimA.Height);
				foreach (UIElement hermano in padre.Children) {
					if (hermano == m.Elemento || esMedible.Contains(hermano)) {
						continue;
					}
					List<(UIElement Elemento, string Texto)> dentro;
					if (!mediblesDentroDe.TryGetValue(hermano, out dentro) || dentro.Count == 0) {
						continue; // hermano sin texto dentro: fondo/decoracion/slots, no un bloque con contenido
					}
					CalculatedStyle b = CajaVisibleDe(hermano);
					if (b.Width <= 0f || b.Height <= 0f || !SeSolapan(a, b)) {
						continue;
					}
					fallosBloque++;
					string pisados = "";
					foreach (var d in dentro) {
						CalculatedStyle c = d.Elemento.GetDimensions();
						if (c.Width > 0f && c.Height > 0f && SeSolapan(a, c)) {
							pisados += (pisados.Length == 0 ? "" : ", ") + "\"" + d.Texto.Replace("\n", " | ") + "\"";
						}
					}
					Registro.Linea("AUTOPRUEBA ESPACIADO/" + contexto + " - FALLO SOLAPE-BLOQUE: \"" +
						m.Texto.Replace("\n", " | ") + "\" (x=" + (int)a.X + " y=" + (int)a.Y + " " + (int)a.Width + "x" +
						(int)a.Height + ") invade el bloque hermano " + hermano.GetType().Name + " (x=" + (int)b.X +
						" y=" + (int)b.Y + " " + (int)b.Width + "x" + (int)b.Height + ", " + dentro.Count +
						" texto(s) dentro)" + (pisados.Length == 0 ? " sin llegar a pisar letra." : ", pisando: " + pisados + "."));
				}
			}

			Registro.Linea("AUTOPRUEBA ESPACIADO/" + contexto + " - " + medibles.Count +
				" elemento(s) con texto medido(s) -> " +
				(fallosDesborde + fallosSolape + fallosBloque == 0
					? "OK, nada desborda ni se solapa"
					: (fallosDesborde + " desborde(s), " + fallosSolape + " solape(s), " + fallosBloque + " solape(s) de bloque")) + ".");

			return fallosDesborde + fallosSolape + fallosBloque;
		}

		/// <summary>Mismo criterio de solape que la regla 2 (medio pixel de tolerancia por los
		/// redondeos del motor), sacado a un metodo para que las reglas 2 y 3 no puedan divergir.</summary>
		private static bool SeSolapan(CalculatedStyle a, CalculatedStyle b)
		{
			return a.X < b.X + b.Width - 0.5f && a.X + a.Width - 0.5f > b.X &&
				a.Y < b.Y + b.Height - 0.5f && a.Y + a.Height - 0.5f > b.Y;
		}

		/// <summary>
		/// Caja que de verdad OCUPA <paramref name="elemento"/> en pantalla, para la regla 3 de
		/// mas arriba (bloque hermano). Un <c>UIElement</c> A SECAS (el tipo exacto, no una
		/// subclase) no pinta nada por si mismo: su <c>DrawSelf</c> base esta vacio (confirmado en
		/// el codigo decompilado real de <c>Terraria.UI.UIElement</c>, sin ningun campo
		/// <c>BackgroundColor</c> que ese metodo pueda leer) - es un contenedor puramente de
		/// layout, y este mod los usa a menudo con <c>Height.Set(x, 1f)</c> ("llenar el hueco que
		/// sobre", patron real, ver <c>PestanaAlmacenes._rejilla</c>), bastante mas alto que su
		/// contenido de verdad. Comparar contra esa caja INFLADA es un falso positivo real
		/// (requirement 0446b3c9, 29-sep-2026): "Hucha: 2 de 40 ranuras ocupadas." iba DEBAJO de la
		/// rejilla de objetos, en el hueco vacio que le sobra a <c>_rejilla</c>, y marcaba
		/// "invade" un bloque con el que no coincide ni un pixel (confirmado contra la captura real
		/// del juego). Para ese caso concreto se usa en su lugar la UNION de las cajas de los hijos
		/// DIRECTOS: el relleno vacio deja de contar, pero cualquier contenido real (texto,
		/// ranuras de objetos, paneles anidados) lo sigue cubriendo entero.
		/// <para />
		/// Para cualquier OTRA cosa (<c>UIPanel</c> y cualquier subclase que pueda pintar su
		/// propio fondo en toda su caja, como el panel de vista previa del muñeco de Apariencia)
		/// se sigue usando la caja declarada COMPLETA, sin tocar: ahi la caja declarada SI es la
		/// superficie visible real, y un texto que se meta debajo de su fondo (aunque no toque
		/// ningun texto suyo) seria un solape de verdad.
		/// </summary>
		private static CalculatedStyle CajaVisibleDe(UIElement elemento)
		{
			CalculatedStyle propia = elemento.GetDimensions();
			if (elemento.GetType() != typeof(UIElement)) {
				return propia;
			}

			bool alguno = false;
			float x0 = 0f, y0 = 0f, x1 = 0f, y1 = 0f;
			foreach (UIElement hijo in elemento.Children) {
				CalculatedStyle c = hijo.GetDimensions();
				if (c.Width <= 0f || c.Height <= 0f) {
					continue;
				}
				if (!alguno) {
					x0 = c.X;
					y0 = c.Y;
					x1 = c.X + c.Width;
					y1 = c.Y + c.Height;
					alguno = true;
				} else {
					x0 = Math.Min(x0, c.X);
					y0 = Math.Min(y0, c.Y);
					x1 = Math.Max(x1, c.X + c.Width);
					y1 = Math.Max(y1, c.Y + c.Height);
				}
			}
			return alguno ? new CalculatedStyle(x0, y0, x1 - x0, y1 - y0) : propia;
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
