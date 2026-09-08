using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;
using Terrakeep.Core.Data;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE PRUEBAS: verifica en el juego real el reparto de las TRES columnas de la
	/// pestaña Buffs (buffs activos / arbol de carpetas / resultados de "Añadir"), el encargo del
	/// 8-sep-2026 ("la columna del centro esta super apretada... encontrar un equilibrio entre las
	/// 3 columnas", reportado con captura).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que un arnes nuevo y no una ampliacion de <see cref="AutopruebaEspaciado"/>.</b> Hay
	/// varios agentes trabajando a la vez en este repo (Libreria/Categorias, Apariencia, editor de
	/// prefijo) y esa autoprueba, su enganche en <c>PanelTerrakeepSystem</c> y la lista de
	/// <c>CapturaDePantalla</c> son archivos COMPARTIDOS. Este arnes es autonomo a proposito -
	/// trae su propio <c>ModSystem</c>, su propia variable de entorno y su propio guardado de
	/// captura- para no tocar ni un archivo que otro pueda estar editando en paralelo. La captura
	/// es la misma tecnica ya documentada en <see cref="CapturaDePantalla"/>
	/// (<c>GetBackBufferData</c> + <c>SaveAsPng</c>, la unica que funciona con FNA/Direct3D), no
	/// una via nueva sin verificar.
	/// </para>
	/// <para>
	/// <b>Que mide de verdad</b>, con la geometria YA dibujada y la fuente real, no con lo que se
	/// penso al construir:
	/// <list type="number">
	/// <item>Los anchos reales de las tres columnas y su suma, para dejar por escrito el reparto en
	/// cada resolucion e idioma.</item>
	/// <item>Por cada fila del arbol: que su nombre ya envuelto NO se salga de su caja (nunca
	/// desborda, nunca se recorta), y en cuantas LINEAS acaba - que es la medida real de "esta
	/// apretado": una categoria que necesita dos lineas para "Offensive (37)" es exactamente lo que
	/// se veia en la captura del usuario.</item>
	/// <item>Que la ruta ("Raiz &gt; ...") tampoco se salga de la columna de carpetas.</item>
	/// <item>Que las filas de resultados de la columna derecha sigan sin desbordar ni solaparse
	/// despues de haberle quitado ancho para dar-selo al arbol (una regresion muy facil de meter
	/// aqui).</item>
	/// </list>
	/// </para>
	/// <para>
	/// Las dos resoluciones son las pedidas en el encargo: 1600x900 y 800x720, esta ultima el
	/// minimo real que admite el motor (<c>Main.minScreenW</c>/<c>minScreenH</c>). Se cambian en
	/// vivo con <c>Main.SetDisplayMode</c>, el mismo metodo publico que usa el menu de resolucion
	/// de vanilla.
	/// </para>
	/// </remarks>
	public class AutopruebaColumnasBuffsSystem : ModSystem
	{
		public override void UpdateUI(GameTime gameTime)
		{
			AutopruebaColumnasBuffs.Avanzar();
		}
	}

	public static class AutopruebaColumnasBuffs
	{
		public const string Variable = "TERRAKEEP_AUTOTEST_COLBUFFS";

		private const int FotogramasDeEspera = 180;
		private const int FotogramasEntrePasos = 10;
		private const int FotogramasTrasResolucion = 30;

		/// <summary>Escala con la que se dibuja el nombre de una fila de carpeta
		/// (<c>FilaCarpetaBuffTk.EscalaTexto</c>), para medir aqui lo mismo que se ve.</summary>
		private const float EscalaNombreCarpeta = 0.8f;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _espera;
		private static readonly Queue<Action> _acciones = new Queue<Action>();
		private static bool _colaConstruida;
		private static int _fallos;

		private static readonly (int Ancho, int Alto, string Nombre)[] Resoluciones = {
			(1600, 900, "1600x900"),
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
				Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS: " + _acciones.Count + " pasos en cola. " +
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
				Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS: EXCEPCION: " + e);
				_terminada = true;
			}
		}

		private static void ConstruirCola()
		{
			_acciones.Enqueue(PoblarBuffsDePrueba);

			foreach (var resolucion in Resoluciones) {
				var res = resolucion;
				_acciones.Enqueue(() => CambiarResolucion(res.Ancho, res.Alto, res.Nombre));

				foreach (var idioma in Idiomas_) {
					var idi = idioma;
					_acciones.Enqueue(() => CambiarIdioma(idi.Idioma, idi.Nombre));
					_acciones.Enqueue(() => AbrirBuffs(res.Nombre, idi.Nombre));

					// 1) La RAIZ del arbol: las 6 categorias curadas + "Indice" + las carpetas de
					//    mod. Es literalmente lo que se ve en la captura del reporte.
					_acciones.Enqueue(() => IrALaRaiz());
					_acciones.Enqueue(() => Medir(res.Nombre, idi.Nombre, "raiz"));

					// 2) Un nivel hondo por el camino de nombres MAS LARGOS: fuerza a la vez la ruta
					//    mas larga (breadcrumb) y los nombres de subcarpeta mas exigentes.
					_acciones.Enqueue(() => AbrirCarpetaMasLarga());
					_acciones.Enqueue(() => Medir(res.Nombre, idi.Nombre, "hondo"));
				}
			}
		}

		// -----------------------------------------------------------------------------------

		private static PestanaBuffs Buffs()
		{
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			return personaje != null ? personaje.BuscarPrimero<PestanaBuffs>() : null;
		}

		/// <summary>
		/// 30 buffs activos a la vez (fuerza scroll real en la columna izquierda), incluido el de
		/// nombre mas largo de TODO el juego cargado - medido con la fuente real, no adivinado -,
		/// que es el que mas presiona el reparto de columnas.
		/// </summary>
		private static void PoblarBuffsDePrueba()
		{
			Player jugador = Main.LocalPlayer;
			int puestos = 0;
			var fuente = FontAssets.MouseText.Value;
			string nombreMasLargo = null;
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
				}
			}

			Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS - buffs de prueba puestos: " + puestos +
				" (CountBuffs=" + jugador.CountBuffs() + "). Nombre mas largo real=\"" +
				nombreMasLargo + "\" (" + anchoMasLargo.ToString("0") + "px sin escalar).");
		}

		private static void CambiarResolucion(int ancho, int alto, string nombreRes)
		{
			Main.SetDisplayMode(ancho, alto, false);
			Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS - resolucion pedida " + nombreRes +
				" -> real tras SetDisplayMode: " + Main.screenWidth + "x" + Main.screenHeight +
				", UIScale=" + Main.UIScale + ".");
			_espera = FotogramasTrasResolucion;
		}

		private static void CambiarIdioma(IdiomaDeTerrakeep idioma, string nombreIdioma)
		{
			bool cambio = Idiomas.Aplicar(idioma, "autoprueba de columnas de Buffs");
			Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS - idioma pedido " + nombreIdioma +
				" -> cultura activa ahora: " + Idiomas.CulturaActiva + " (cambio real=" + cambio + ").");
		}

		private static void AbrirBuffs(string nombreRes, string nombreIdioma)
		{
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje,
				"autoprueba de columnas de Buffs (" + nombreRes + "/" + nombreIdioma + ")");
			ContenidoPersonaje personaje = PanelTerrakeepSystem.Panel != null
				? PanelTerrakeepSystem.Panel.Personaje : null;
			// Indice 3 = "Buffs", mismo orden que ContenidoPersonaje.ClavesPestana.
			personaje?.IrAPestana(3);
		}

		private static void IrALaRaiz()
		{
			PestanaBuffs buffs = Buffs();
			if (buffs == null) {
				return;
			}
			// Volver a la raiz sin simular clics: el arnes ya tiene AbrirCarpetaParaPrueba para
			// bajar, y reconstruir el arbol desde arriba es tan simple como pedir la raiz.
			buffs.IrALaRaizParaPrueba();
			// La lista de resultados de la derecha tambien tiene que tener filas REALES que medir:
			// sin carpeta ni busqueda solo enseña el aviso de "elige una carpeta".
			buffs.BuscarParaPrueba("a");
		}

		/// <summary>
		/// Baja un nivel a la carpeta raiz CON SUBCARPETAS cuyo hijo de nombre mas largo sea el mas
		/// exigente de todos. Un primer intento elegia la raiz de nombre mas largo a secas y caia en
		/// una categoria curada (que no tiene hijas): el arbol se quedaba con 0 filas y el paso no
		/// verificaba nada del segundo nivel. Aqui interesa justo lo contrario - un nivel con filas
		/// de verdad y con la ruta ("Raiz &gt; ...") ya larga.
		/// </summary>
		private static void AbrirCarpetaMasLarga()
		{
			PestanaBuffs buffs = Buffs();
			if (buffs == null) {
				return;
			}

			ArbolBuffs.ConstruirSiHaceFalta();
			CategoryTreeNodeData mejor = null;
			float mejorAncho = -1f;
			var fuente = FontAssets.MouseText.Value;

			foreach (CategoryTreeNodeData n in ArbolBuffs.Raices) {
				if (n.Children == null || n.Children.Count == 0) {
					continue;
				}
				foreach (CategoryTreeNodeData hijo in n.Children) {
					float ancho = fuente.MeasureString(hijo.Name ?? "").X;
					if (ancho > mejorAncho) {
						mejorAncho = ancho;
						mejor = n;
					}
				}
			}

			if (mejor != null) {
				buffs.AbrirCarpetaParaPrueba(mejor);
			}
			else {
				Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS - aviso: ninguna carpeta raiz tiene " +
					"subcarpetas, el paso \"hondo\" no puede medir un segundo nivel.");
			}
		}

		/// <summary>
		/// El paso que de verdad verifica: mide las tres columnas y cada fila del arbol con la
		/// geometria YA dibujada, cuenta fallos y deja una captura real.
		/// </summary>
		private static void Medir(string nombreRes, string nombreIdioma, string momento)
		{
			PestanaBuffs buffs = Buffs();
			string etiqueta = "(" + nombreRes + "/" + nombreIdioma + "/" + momento + ")";

			if (buffs == null) {
				Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS " + etiqueta + ": NO se encontro PestanaBuffs.");
				_fallos++;
				return;
			}

			var fuente = FontAssets.MouseText.Value;

			float anchoPestana = buffs.GetDimensions().Width;
			float anchoActivos = Ancho(buffs.ColumnaActivos);
			float anchoAnadir = Ancho(buffs.ColumnaAnadir);
			float anchoCarpetas = Ancho(buffs.ColumnaCarpetas);
			float anchoResultados = Ancho(buffs.ColumnaResultados);

			Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS " + etiqueta + " - REPARTO: pestaña=" +
				anchoPestana.ToString("0") + "px | Activos=" + anchoActivos.ToString("0") +
				"px | Añadir=" + anchoAnadir.ToString("0") + "px (arbol=" + anchoCarpetas.ToString("0") +
				"px + resultados=" + anchoResultados.ToString("0") + "px).");

			// --- Filas del arbol de carpetas ------------------------------------------------
			int filas = 0;
			int enUnaLinea = 0;
			int fallosFila = 0;
			foreach (FilaCarpetaBuffTk fila in buffs.FilasCarpetaParaPrueba) {
				filas++;
				float disponible = fila.AnchoTextoDisponible;
				float medido = fuente.MeasureString(fila.NombrePartido).X * EscalaNombreCarpeta;
				int lineas = fila.LineasDelNombre;
				if (lineas == 1) {
					enUnaLinea++;
				}

				bool desborda = medido > disponible + 0.5f;
				bool recortado = fila.NombrePartido.Contains("...");
				if (desborda || recortado) {
					fallosFila++;
					Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS " + etiqueta + " - CARPETA FILA " +
						filas + ": FALLO. \"" + fila.NombrePartido.Replace("\n", " | ") + "\" mide " +
						medido.ToString("0.0") + "px con " + disponible.ToString("0.0") +
						"px disponibles (desborda=" + desborda + ", recortado=" + recortado + ").");
				}
				else {
					Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS " + etiqueta + " - carpeta " + filas +
						": \"" + fila.NombrePartido.Replace("\n", " | ") + "\" -> " + lineas +
						" linea(s), " + medido.ToString("0") + "px de " + disponible.ToString("0") +
						"px, pedia " + fila.AnchoParaUnaLinea.ToString("0") + "px de fila para una sola.");
				}
			}

			// --- La ruta ("Raiz > ...") ------------------------------------------------------
			int fallosRuta = 0;
			if (buffs.RutaCarpetasParaPrueba != null) {
				CalculatedStyle dimRuta = buffs.RutaCarpetasParaPrueba.GetDimensions();
				string textoRuta = buffs.RutaCarpetasParaPrueba.TextoActual;
				float anchoRuta = fuente.MeasureString(textoRuta).X * 0.68f;
				if (dimRuta.Width > 0f && anchoRuta > dimRuta.Width + 0.5f) {
					fallosRuta++;
					Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS " + etiqueta + " - FALLO: la ruta \"" +
						textoRuta.Replace("\n", " | ") + "\" mide " + anchoRuta.ToString("0.0") +
						"px en caja de " + dimRuta.Width.ToString("0.0") + "px.");
				}
			}

			// --- Filas de resultados (que no se haya roto la columna derecha) ----------------
			int fallosResultados = 0;
			foreach (var f in buffs.FilasResultadoParaPrueba) {
				CalculatedStyle dimFila = f.Fila.GetDimensions();
				CalculatedStyle dimNombre = f.Nombre.GetDimensions();
				CalculatedStyle dimAplicar = f.Aplicar.GetDimensions();
				Vector2 tamano = fuente.MeasureString(f.Nombre.TextoActual) * 0.8f;

				bool desbordeAncho = tamano.X > dimNombre.Width + 0.5f;
				bool invadeLinea2 = (dimNombre.Y + tamano.Y) > dimAplicar.Y + 0.5f;
				bool filaBaja = (dimAplicar.Y + dimAplicar.Height) > (dimFila.Y + dimFila.Height + 0.5f);

				if (desbordeAncho || invadeLinea2 || filaBaja) {
					fallosResultados++;
					Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS " + etiqueta + " - RESULTADO: FALLO. \"" +
						f.Nombre.TextoActual.Replace("\n", " | ") + "\" (desbordeAncho=" + desbordeAncho +
						", invadeLinea2=" + invadeLinea2 + ", filaBaja=" + filaBaja + ").");
				}
			}

			// --- Filas de activos (la columna izquierda tampoco puede haberse roto) ----------
			int fallosActivos = 0;
			foreach (var f in buffs.FilasActivasParaPrueba) {
				CalculatedStyle dimNombre = f.Nombre.GetDimensions();
				CalculatedStyle dimTiempo = f.Tiempo.GetDimensions();
				CalculatedStyle dimQuitar = f.Quitar.GetDimensions();
				Vector2 tamano = fuente.MeasureString(f.Nombre.TextoActual) * 0.8f;

				if (tamano.X > dimNombre.Width + 0.5f ||
					(dimTiempo.X + dimTiempo.Width) > dimQuitar.X + 0.5f) {
					fallosActivos++;
				}
			}

			int total = fallosFila + fallosRuta + fallosResultados + fallosActivos;
			_fallos += total;

			Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS " + etiqueta + " - RESUMEN: " + filas +
				" carpetas (" + enUnaLinea + " en UNA sola linea), " +
				buffs.FilasResultadoParaPrueba.Count + " resultados, " +
				buffs.FilasActivasParaPrueba.Count + " activos -> " +
				(total == 0 ? "OK, nada desbordado ni recortado en ninguna de las 3 columnas"
					: (total + " FALLO(S)")) + ".");

			Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS " + etiqueta + " - " +
				Captura("colbuffs-" + nombreRes + "-" + nombreIdioma + "-" + momento));
		}

		private static float Ancho(UIElement elemento)
		{
			return elemento != null ? elemento.GetDimensions().Width : -1f;
		}

		private static void Terminar()
		{
			_terminada = true;
			Registro.Linea("AUTOPRUEBA COLUMNAS-BUFFS COMPLETA. Fallos totales: " + _fallos + ".");
		}

		/// <summary>
		/// Misma tecnica que <see cref="CapturaDePantalla.Guardar"/> (la unica que funciona en una
		/// aplicacion acelerada por GPU: <c>GetBackBufferData</c> + <c>SaveAsPng</c>), repetida aqui
		/// para que este arnes no dependa de la lista de variables permitidas de ese archivo
		/// compartido - ver el <c>remarks</c> de la clase.
		/// </summary>
		private static string Captura(string nombre)
		{
			try {
				GraphicsDevice dispositivo = Main.instance.GraphicsDevice;
				PresentationParameters parametros = dispositivo.PresentationParameters;
				int ancho = parametros.BackBufferWidth;
				int alto = parametros.BackBufferHeight;
				if (ancho <= 0 || alto <= 0) {
					return "captura imposible: el back buffer mide " + ancho + "x" + alto;
				}

				Color[] pixeles = new Color[ancho * alto];
				dispositivo.GetBackBufferData(pixeles);

				string carpeta = Path.Combine(Main.SavePath, CapturaDePantalla.Carpeta);
				Directory.CreateDirectory(carpeta);
				string ruta = Path.Combine(carpeta, nombre + ".png");

				using (Texture2D textura = new Texture2D(dispositivo, ancho, alto)) {
					textura.SetData(pixeles);
					using (FileStream archivo = File.Create(ruta)) {
						textura.SaveAsPng(archivo, ancho, alto);
					}
				}

				return "captura real del back buffer guardada en \"" + ruta + "\" (" + ancho + "x" + alto + ")";
			}
			catch (Exception e) {
				return "captura fallida: " + e.GetType().Name + ": " + e.Message;
			}
		}

		private static class Registro
		{
			public const string NombreArchivo = "terrakeep-columnas-buffs-evidencia.log";
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
							"# Evidencia del reparto de las 3 columnas de la pestaña Buffs - " +
							DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
					}
					File.AppendAllText(ruta, "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + linea + Environment.NewLine);
				}
				catch (Exception) {
					// El log del juego ya tiene la evidencia: no vale la pena tumbar nada por esto.
				}
			}
		}
	}
}
