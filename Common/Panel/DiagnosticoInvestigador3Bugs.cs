using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ModLoader;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;
using TerrakeepMod.UI.Exploracion;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// SOLO ARNES DE INVESTIGACION (26-sep-2026), NUNCA activo jugando normal: solo corre con la
	/// variable de entorno <see cref="Variable"/> puesta. Reproduce con evidencia REAL MEDIDA (no
	/// "deberia fallar") los tres pendientes del encargo "3 pendientes reales, nunca investigados a
	/// fondo": (1) parpadeo de letras al renombrar un conjunto en Personaje/Conjuntos, (2) segmentos
	/// del anillo de progreso "X/Y" de Builds posiblemente desalineados de la circunferencia, (3)
	/// parpadeo de contenido/scroll en Exploracion/Vecindad.
	/// <para />
	/// Archivo NUEVO: no toca NINGUN archivo de produccion existente. Usa solo API PUBLICA ya
	/// expuesta (<c>PanelTerrakeepSystem</c>, <c>ContenidoBuilds.AnilloProgresoParaPrueba</c>,
	/// <c>ContenidoExploracion.Vecindad</c>, <c>PestanaVecindad.TotalNpcsParaPrueba</c>) mas
	/// reflexion de solo LECTURA/INVOCACION sobre campos y metodos privados que YA EXISTEN (nunca
	/// cambia su firma, su visibilidad ni su comportamiento real - es exactamente la misma tecnica
	/// que el propio proyecto ya documenta como "real" en <c>PestanaConjuntos.RenombrarParaPrueba</c>
	/// y en el XMLdoc de <c>AplicarPresetParaPrueba</c> ("BotonTk.LeftMouseDown, la misma via real
	/// que ya usa el resto del mod para probar botones"), aplicada aqui desde FUERA del archivo de
	/// produccion en vez de desde un metodo interno ya expuesto).
	/// </summary>
	public class DiagnosticoInvestigador3Bugs : ModSystem
	{
		public const string Variable = "TERRAKEEP_INVESTIGACION_3BUGS";

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

		private static void Avanzar(int siguiente)
		{
			_paso = siguiente;
			_espera = 5;
		}

		private static void Log(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " INVESTIGACION-3BUGS: " + mensaje);
			}
		}

		// ------------------------------------------------------------------------------------
		// Reflexion minima de solo lectura/invocacion sobre miembros PRIVADOS ya existentes.
		// Nunca cambia una firma ni un comportamiento: solo lee/llama lo que ya hay.
		// ------------------------------------------------------------------------------------

		private static object Priv(object obj, string campo)
		{
			if (obj == null) {
				return null;
			}
			FieldInfo f = obj.GetType().GetField(campo,
				BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			return f?.GetValue(obj);
		}

		private static void SetPriv(object obj, string campo, object valor)
		{
			if (obj == null) {
				return;
			}
			FieldInfo f = obj.GetType().GetField(campo,
				BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			f?.SetValue(obj, valor);
		}

		private static object PrivStatic(Type t, string campo)
		{
			FieldInfo f = t.GetField(campo, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			return f?.GetValue(null);
		}

		private static object Invocar(object obj, string metodo, params object[] args)
		{
			if (obj == null) {
				return null;
			}
			MethodInfo m = obj.GetType().GetMethod(metodo,
				BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			return m?.Invoke(obj, args);
		}

		// ------------------------------------------------------------------------------------
		// Captura propia del back buffer real (misma tecnica que Common/Panel/CapturaDePantalla.cs,
		// duplicada aqui a proposito: ese archivo es produccion y su lista "Permitida" es un
		// whitelist cerrado que no hay que tocar para una investigacion puntual).
		// ------------------------------------------------------------------------------------

		private static Color[] _ultimosPixeles;
		private static int _ultimoAncho;
		private static int _ultimoAlto;

		private static string GuardarCaptura(string nombre)
		{
			try {
				GraphicsDevice dispositivo = Main.instance.GraphicsDevice;
				PresentationParameters parametros = dispositivo.PresentationParameters;
				int ancho = parametros.BackBufferWidth;
				int alto = parametros.BackBufferHeight;
				if (ancho <= 0 || alto <= 0) {
					return "captura imposible: back buffer " + ancho + "x" + alto;
				}
				Color[] pixeles = new Color[ancho * alto];
				dispositivo.GetBackBufferData(pixeles);

				string carpeta = Path.Combine(Main.SavePath, "terrakeep-investigacion-3bugs");
				Directory.CreateDirectory(carpeta);
				string ruta = Path.Combine(carpeta, nombre + ".png");
				using (Texture2D textura = new Texture2D(dispositivo, ancho, alto)) {
					textura.SetData(pixeles);
					using (FileStream archivo = File.Create(ruta)) {
						textura.SaveAsPng(archivo, ancho, alto);
					}
				}

				_ultimosPixeles = pixeles;
				_ultimoAncho = ancho;
				_ultimoAlto = alto;
				return "captura real guardada en \"" + ruta + "\"";
			}
			catch (Exception e) {
				return "captura fallida: " + e.GetType().Name + ": " + e.Message;
			}
		}

		private static Color PixelEn(float x, float y)
		{
			if (_ultimosPixeles == null) {
				return Color.Transparent;
			}
			int px = (int)Math.Round(x);
			int py = (int)Math.Round(y);
			if (px < 0 || py < 0 || px >= _ultimoAncho || py >= _ultimoAlto) {
				return Color.Transparent;
			}
			return _ultimosPixeles[py * _ultimoAncho + px];
		}

		private static float DistanciaColor(Color a, Color b)
		{
			float dr = a.R - b.R;
			float dg = a.G - b.G;
			float db = a.B - b.B;
			return (float)Math.Sqrt(dr * dr + dg * dg + db * db);
		}

		// ------------------------------------------------------------------------------------
		// Maquina de pasos
		// ------------------------------------------------------------------------------------

		private static void EjecutarPaso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); Avanzar(1); break;
				case 1: AbrirConjuntos(); Avanzar(2); break;
				case 2: PrepararRenombrar(); Avanzar(3); break;
				case 3:
					if (ObservarBlinkRenombrar()) { Avanzar(4); } else { _espera = 0; }
					break;
				case 4: IrABuilds(); Avanzar(5); break;
				case 5: ForzarEscenarioAnillo(); Avanzar(6); break;
				case 6: MedirAnillo(); Avanzar(7); break;
				case 7: IrAVecindad(); Avanzar(8); break;
				case 8:
					if (ObservarVecindad()) { Avanzar(9); } else { _espera = 0; }
					break;
				case 9: Terminar(); break;
			}
		}

		private static void Arrancar()
		{
			PlayerInput.CurrentInputMode = InputMode.Mouse;
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "investigacion 3 bugs");
			Log("PASO0: panel abierto en Personaje.");
		}

		private static void AbrirConjuntos()
		{
			PanelTerrakeepSystem.Panel?.Personaje?.IrAPestana(6);
			Log("PASO1: IrAPestana(6) = Conjuntos.");
		}

		// -------------------- BUG 1: renombrar conjuntos parpadea letras --------------------

		private static PestanaConjuntos _pestanaConjuntos;
		private static CampoTextoTk _campoRenombrar;
		private static float _saltoPxEsperado;

		private static void PrepararRenombrar()
		{
			var contenido = PanelTerrakeepSystem.Panel?.Personaje;
			if (contenido == null) {
				Log("PASO2 FALLO: ContenidoPersonaje nulo (el panel no esta en Personaje).");
				return;
			}

			object pestanaObj = Priv(contenido, "_pestanaActual");
			_pestanaConjuntos = pestanaObj as PestanaConjuntos;
			if (_pestanaConjuntos == null) {
				Log("PASO2 FALLO: la sub-pestaña activa no es PestanaConjuntos (es " +
					(pestanaObj?.GetType().FullName ?? "null") + ").");
				return;
			}

			Invocar(_pestanaConjuntos, "EmpezarRenombrar", 0);

			object filasObj = Priv(_pestanaConjuntos, "_filasNativas");
			IList filas = filasObj as IList;
			if (filas == null || filas.Count == 0) {
				Log("PASO2 FALLO: _filasNativas vacio o inaccesible.");
				return;
			}
			object fila0 = filas[0];
			FieldInfo campoField = fila0.GetType().GetField("CampoEdicion",
				BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			_campoRenombrar = campoField?.GetValue(fila0) as CampoTextoTk;
			if (_campoRenombrar == null) {
				Log("PASO2 FALLO: CampoEdicion nulo tras EmpezarRenombrar(0).");
				return;
			}
			SetPriv(_campoRenombrar, "_enfocado", true);

			string texto = _campoRenombrar.Texto;
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			const float escala = 0.8f; // misma escala real que usa EmpezarRenombrar al construir el CampoTextoTk
			Vector2 sinCursor = fuente.MeasureString(texto) * escala;
			Vector2 conCursor = fuente.MeasureString(texto + "|") * escala;
			float dimWidth = _campoRenombrar.GetDimensions().Width;
			float posSinCursor = (dimWidth - sinCursor.X) / 2f;
			float posConCursor = (dimWidth - conCursor.X) / 2f;
			_saltoPxEsperado = posSinCursor - posConCursor;

			Log("PASO2: EmpezarRenombrar(0) invocado (reflexion sobre el metodo PRIVADO real, mismo " +
				"camino que pulsar \"Renombrar\"), _enfocado forzado a true (reflexion sobre el campo " +
				"PRIVADO real que fija OnLeftClick). Texto real del campo=\"" + texto + "\", " +
				"campo.GetDimensions().Width=" + dimWidth.ToString("0.000") + ", " +
				"FontAssets.MouseText.Value.MeasureString(\"" + texto + "\")*0.8=" + sinCursor.X.ToString("0.000") +
				"px, MeasureString(\"" + texto + "|\")*0.8=" + conCursor.X.ToString("0.000") + "px, " +
				"delta REAL medido con la fuente real del juego=" + (conCursor.X - sinCursor.X).ToString("0.000") +
				"px -> segun la formula real de CampoTextoTk.DrawSelf (posicion.X = dim.X + (dim.Width - " +
				"tamano.X)/2), el texto entero se desplaza horizontalmente " + _saltoPxEsperado.ToString("0.000") +
				"px cada vez que '_cursorVisible' cambia de estado (cada 20 fotogramas, ~3 veces/segundo).");
		}

		private static int _subFrameBlink;
		private static bool _cursorAnteriorConocido;
		private static bool _cursorAnterior;
		private static int _transicionesVistas;

		private static bool ObservarBlinkRenombrar()
		{
			if (_campoRenombrar == null) {
				Log("PASO3: sin campo de edicion (PASO2 fallo antes) - se omite la observacion.");
				return true;
			}

			object cv = Priv(_campoRenombrar, "_cursorVisible");
			bool cursorVisible = cv is bool b && b;

			bool transicion = _cursorAnteriorConocido && cursorVisible != _cursorAnterior;
			if (transicion) {
				_transicionesVistas++;
				string etiqueta = "renombrar-transicion" + _transicionesVistas + (cursorVisible ? "-cursorON" : "-cursorOFF");
				string resultado = GuardarCaptura(etiqueta);
				Log("PASO3 f=" + _subFrameBlink + ": TRANSICION real de '_cursorVisible' a " +
					(cursorVisible ? "VISIBLE" : "invisible") + " (numero " + _transicionesVistas + ") - " + resultado);
			}

			_cursorAnterior = cursorVisible;
			_cursorAnteriorConocido = true;
			_subFrameBlink++;

			if (_transicionesVistas >= 3 || _subFrameBlink >= 90) {
				Log("PASO3 RESUMEN BUG1: " + _transicionesVistas + " transiciones reales de cursor observadas en " +
					_subFrameBlink + " fotogramas dentro del campo de renombrar abierto de verdad (EmpezarRenombrar " +
					"real). Salto horizontal esperado del texto por cada transicion=" + _saltoPxEsperado.ToString("0.000") +
					"px (medido con la fuente real, ver PASO2). Comparar las capturas renombrar-transicionN-cursorON.png " +
					"vs -cursorOFF.png en terrakeep-capturas del sandbox: el texto (no solo el cursor) deberia " +
					"aparecer desplazado horizontalmente entre una y otra.");
				return true;
			}
			return false;
		}

		// -------------------- BUG 2: anillo de progreso de Builds --------------------

		private static void IrABuilds()
		{
			PanelTerrakeepSystem.Panel?.CambiarArea(AreaTerrakeep.Builds, "investigacion 3 bugs");
			Log("PASO4: cambiado a Builds.");
		}

		private static void ForzarEscenarioAnillo()
		{
			// El personaje de pruebas real da 0/13 (no lleva nada del catalogo puesto) - eso solo
			// ejercita segmentos VACIOS. Para probar tambien el LIMITE lleno/vacio real (el caso
			// "6 de 13" del encargo), se sobre-escriben por reflexion los DOS campos privados que
			// ya lee _fraccion en cada Draw (nunca se toca su logica, solo el VALOR de los datos,
			// mismo criterio que el resto de este arnes). El Avanzar(6) que sigue a este paso deja
			// varios fotogramas de margen (_espera=5) para que el back buffer capturado en el PASO
			// siguiente sea ya el fotograma NUEVO (con 6/13) y no el fotograma viejo ya presentado
			// (mismo aviso real que documenta CapturaDePantalla: "lo que se captura es el fotograma
			// ANTERIOR").
			var contenidoBuilds = PanelTerrakeepSystem.Panel?.Builds;
			SetPriv(contenidoBuilds, "_objetosQueTiene", 6);
			SetPriv(contenidoBuilds, "_objetosResueltos", 13);
			Log("PASO5: forzado por reflexion _objetosQueTiene=6 _objetosResueltos=13 sobre la instancia real de ContenidoBuilds (mismos campos que ya lee _fraccion en cada Draw).");
		}

		private static void MedirAnillo()
		{
			var anillo = PanelTerrakeepSystem.Panel?.Builds?.AnilloProgresoParaPrueba;
			if (anillo == null) {
				Log("PASO6 FALLO: AnilloProgresoParaPrueba nulo (el panel no esta en Builds o el anillo no se construyo).");
				return;
			}

			var dim = anillo.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			float radio = dim.Width / 2f;

			object segObj = PrivStatic(anillo.GetType(), "Segmentos");
			int segmentos = segObj != null ? Convert.ToInt32(segObj) : 28;

			object fraccionObj = Priv(anillo, "_fraccion");
			var fraccionFunc = fraccionObj as Func<float>;
			float fraccion = fraccionFunc != null ? fraccionFunc() : 0f;

			object textoObj = Priv(anillo, "_textoCentro");
			var textoFunc = textoObj as Func<string>;
			string textoCentro = textoFunc != null ? (textoFunc() ?? "") : "";

			Color colorLleno = anillo.ColorLleno;
			Color colorVacio = anillo.ColorVacio;

			string capturaResultado = GuardarCaptura("anillo-builds-" + textoCentro.Replace("/", "de"));

			int aciertos = 0;
			int fallos = 0;
			for (int i = 0; i < segmentos; i++) {
				float t = (float)i / segmentos;
				float angulo = -MathHelper.PiOver2 + t * MathHelper.TwoPi;
				Vector2 direccion = new Vector2((float)Math.Cos(angulo), (float)Math.Sin(angulo));
				Vector2 punto = centro + direccion * radio;
				// GetDimensions() devuelve coordenadas LOGICAS de la UI (antes de Main.UIScaleMatrix);
				// el back buffer capturado son pixeles REALES del dispositivo. Con Main.UIScale != 1
				// (config.json real del usuario: 1.4666667) hay que multiplicar por el factor real
				// para samplear el pixel correcto - mismo dato real que ya documenta el propio
				// proyecto (DiagnosticoTituloYVecindad, UIScale=1.4666667).
				Vector2 puntoPantalla = punto * Main.UIScale;
				Color esperado = t < fraccion ? colorLleno : colorVacio;
				Color real = PixelEn(puntoPantalla.X, puntoPantalla.Y);
				float dist = DistanciaColor(real, esperado);
				bool ok = dist < 60f; // margen generoso por antialiasing/borde del pixel exacto
				if (ok) { aciertos++; } else { fallos++; }

				Log("PASO6 anillo seg=" + i + "/" + segmentos + " t=" + t.ToString("0.000") +
					" esperado=" + (t < fraccion ? "LLENO" : "vacio") +
					" puntoLogico=(" + punto.X.ToString("0.0") + "," + punto.Y.ToString("0.0") + ")" +
					" puntoPantalla=(" + puntoPantalla.X.ToString("0.0") + "," + puntoPantalla.Y.ToString("0.0") + ")" +
					" pixelReal=RGBA(" + real.R + "," + real.G + "," + real.B + "," + real.A + ")" +
					" distanciaColor=" + dist.ToString("0.0") + " | " + (ok ? "OK" : "MAL: no coincide con lo esperado"));
			}

			Log("PASO6 RESUMEN BUG2: texto central real=\"" + textoCentro + "\" fraccion real=" + fraccion.ToString("0.000") +
				" centro=(" + centro.X.ToString("0.0") + "," + centro.Y.ToString("0.0") + ") radio=" + radio.ToString("0.0") +
				" segmentos=" + segmentos + " -> " + aciertos + "/" + segmentos + " puntos coinciden con el color esperado, " +
				fallos + " fallan. " + capturaResultado);
		}

		// -------------------- BUG 3: Exploracion / Vecindad --------------------

		private static void IrAVecindad()
		{
			PanelTerrakeepSystem.Panel?.CambiarArea(AreaTerrakeep.Exploracion, "investigacion 3 bugs");
			PanelTerrakeepSystem.Panel?.Exploracion?.CambiarPestana(3);
			Log("PASO7: cambiado a Exploracion/Vecindad.");
		}

		private static int _subFrameVecindad;
		private static int _hashItem0Anterior;
		private static float _viewPositionAnterior;
		private static int _cambiosDeIdentidad;
		private static int _resetsDeScroll;

		private static bool ObservarVecindad()
		{
			var vecindad = PanelTerrakeepSystem.Panel?.Exploracion?.Vecindad;
			if (vecindad == null) {
				Log("PASO8: PestanaVecindad nula - se omite la observacion.");
				return true;
			}

			var lista = Priv(vecindad, "_lista") as UIList;
			if (lista == null) {
				Log("PASO8: campo _lista inaccesible - se omite la observacion.");
				return true;
			}

			if (_subFrameVecindad == 0) {
				float alturaContenido = lista.GetTotalHeight();
				float alturaHueco = lista.GetDimensions().Height;
				Log("PASO8 inicio: NPCs de pueblo activos=" + vecindad.TotalNpcsParaPrueba + " lista.Count=" + lista.Count +
					" GetTotalHeight=" + alturaContenido.ToString("0.0") + " hueco visible=" + alturaHueco.ToString("0.0") +
					" ViewPosition inicial=" + lista.ViewPosition.ToString("0.00"));

				bool escrollable = alturaContenido > alturaHueco + 1f;
				if (escrollable) {
					lista.ViewPosition = 15f;
					Log("PASO8: contenido mas alto que el hueco visible - ViewPosition forzado a 15 para vigilar " +
						"si un refresco lo resetea sin que el usuario toque nada. ViewPosition tras forzarlo=" +
						lista.ViewPosition.ToString("0.00") + ".");
				}
				else {
					Log("PASO8: con " + vecindad.TotalNpcsParaPrueba + " NPC(s) de pueblo activos en este sandbox, el " +
						"contenido NO llega a ser mas alto que el hueco visible (scrollbar inactivo) - la parte " +
						"\"reset de scroll\" de la hipotesis queda como LIMITE REAL de ESTE sandbox concreto (pocos " +
						"NPCs reclutados). Se sigue vigilando igualmente la RECONSTRUCCION completa cada 30 fotogramas.");
				}

				_hashItem0Anterior = -1;
				_cambiosDeIdentidad = 0;
				_resetsDeScroll = 0;
				_viewPositionAnterior = lista.ViewPosition;
			}

			int hashActual = lista._items.Count > 0 ? lista._items[0].GetHashCode() : -1;
			bool cambioIdentidad = _subFrameVecindad > 0 && hashActual != _hashItem0Anterior;
			bool cambioViewPosition = _subFrameVecindad > 0 && Math.Abs(lista.ViewPosition - _viewPositionAnterior) > 0.01f;
			if (cambioIdentidad) {
				_cambiosDeIdentidad++;
			}
			if (cambioViewPosition) {
				_resetsDeScroll++;
			}

			if (_subFrameVecindad % 15 == 0 || cambioIdentidad || cambioViewPosition) {
				Log("PASO8 f=" + _subFrameVecindad + " Count=" + lista.Count +
					" TotalHeight=" + lista.GetTotalHeight().ToString("0.0") +
					" ViewPosition=" + lista.ViewPosition.ToString("0.00") + " hashItem0=" + hashActual +
					(cambioIdentidad ? " -> RECONSTRUIDA (la identidad del primer UIElement de la lista cambio: Clear()+Add() crearon objetos NUEVOS)" : "") +
					(cambioViewPosition ? " -> VIEWPOSITION CAMBIO SOLO (sin ninguna interaccion del usuario en este diagnostico)" : ""));
			}

			_hashItem0Anterior = hashActual;
			_viewPositionAnterior = lista.ViewPosition;
			_subFrameVecindad++;

			if (_subFrameVecindad >= 95) {
				Log("PASO8 RESUMEN BUG3: en " + _subFrameVecindad + " fotogramas seguidos con la pestaña Vecindad " +
					"abierta y SIN tocar nada, hubo " + _cambiosDeIdentidad + " reconstrucciones completas de la " +
					"lista (se esperan ~3, una cada 30 fotogramas = PestanaVecindad.FotogramasEntreRefrescos) y " +
					_resetsDeScroll + " cambios de ViewPosition no provocados por este diagnostico.");
				return true;
			}
			return false;
		}

		private static void Terminar()
		{
			PanelTerrakeepSystem.CerrarPanel("investigacion 3 bugs terminada");
			_terminada = true;
			Log("INVESTIGACION 3 BUGS: terminada.");
		}
	}
}
