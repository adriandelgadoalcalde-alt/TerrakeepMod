using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.GuiaV2;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Indicador de DIRECCION de la Guía en la pantalla de juego, sin abrir ningun menu: una esfera
	/// con una flecha que gira hacia la siguiente parada, la distancia y a qué apunta (nombre corto
	/// del jefe, bioma o estructura).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Punto 4 del encargo del usuario (02-oct-2026), literal: "¿La brújula es lo que te guía? Lo
	/// digo porque la gracia es que te guíe desde la pantalla principal y no desde ningún menú", y su
	/// aclaración: "La guía no..... sino las indicaciones". Por eso aqui NO hay tareas, ni objetos que
	/// faltan, ni resumen de la parada: eso sigue en el panel. Solo flecha + distancia + destino, un
	/// aviso breve al cambiar de destino o al llegar, tooltip con el detalle y clic para abrir la
	/// parada (opcional). Misma idea que la rueda-brujula del HUD de StarvekeepMod
	/// (<c>widgets/brujulavisualhud.lua</c>): aguja hacia el objetivo, distancia dentro, el nombre
	/// largo en el tooltip.
	/// </para>
	/// <para>
	/// El destino es el MISMO que el de la marca del mapa (<see cref="UbicacionGuia.Siguiente"/>):
	/// no hay un segundo calculo de posicion que pueda discrepar.
	/// </para>
	/// <para>
	/// <b>Donde va (sin tapar la interfaz de vanilla).</b> Medidas reales del codigo decompilado de
	/// tModLoader 1.4.4.9, en pantalla LOGICA (la capa se dibuja con la escala de interfaz):
	/// monedas y municion del inventario abierto acaban en x≈563 (<c>Main.DrawInventory</c>: rotulo de
	/// municion en 532, ranuras de 52·0,6) y la vida empieza en <c>screenWidth - 300</c> (corazones
	/// clasicos y elegantes) o <c>- 322</c> (barras), con el minimapa debajo a partir de y=90. La
	/// franja de arriba entre las dos queda libre en vanilla, y deja a su izquierda las barras de
	/// Calamity (rabia/adrenalina/vuelo, al 36-41 % del ancho por defecto en
	/// <c>CalamityClientConfig</c>). La escala de interfaz maxima garantiza una pantalla logica de al
	/// menos 800 de ancho; con 16:9 nunca baja de 1066, donde la franja mide 158 px. Si no cabe
	/// (pantallas casi 4:3 o tamaño grande), se pone junto al personaje.
	/// </para>
	/// </remarks>
	public class IndicadorGuiaHud : ModSystem
	{
		public const string NombreCapa = "TerrakeepMod: Indicador de la Guía";

		// Medidas a tamaño 100 %, en pantalla logica.
		public const float Esfera = 40f;
		public const float HuecoTexto = 6f;
		public const float AnchoTexto = 96f;
		public const float Alto = 44f;
		public const float Relleno = 3f;
		public const float MargenTextoDerecho = 6f;

		/// <summary>Borde derecho de lo que ocupa el inventario abierto (monedas/municion y su rotulo),
		/// con margen.</summary>
		public const float BordeInventario = 576f;

		/// <summary>Distancia desde el borde derecho a la que empieza la vida (corazones/barras), con
		/// margen.</summary>
		public const float MargenVida = 332f;

		public const float Arriba = 6f;

		/// <summary>
		/// Con el inventario ABIERTO el juego pinta una fila de cuatro iconos a la izquierda de la vida
		/// (x de <c>screenWidth - 450</c> a <c>- 290</c>, y de 40 a 70 en pantalla logica; medido en las
		/// capturas reales de F3b a 1280, 1600, 1920 y 2560 con UIScale normal y maxima). En ese caso el
		/// indicador baja a esta altura, en la misma columna: por debajo de esa fila y a la izquierda
		/// de la vida y del minimapa (que empieza en x = <c>screenWidth - 304</c> con su marco).
		/// </summary>
		public const float ArribaConInventario = 74f;

		private const int DuracionAviso = 240;

		// ---- estado a la vista (lo leen la autoprueba y el tooltip) ----------------------------
		/// <summary>"oculto", "en_ruta", "llegado", "sin_rumbo".</summary>
		public static string Estado { get; private set; } = "oculto";
		public static Rectangle UltimoRectangulo { get; private set; }
		/// <summary>Angulo de la flecha en radianes (0 = este, PI/2 = abajo, como la pantalla).</summary>
		public static float UltimoAngulo { get; private set; }
		public static float UltimaDistancia { get; private set; }
		public static string UltimoNombre { get; private set; } = "";
		public static string UltimaColocacion { get; private set; } = "";
		public static string UltimoAviso { get; private set; } = "";
		public static int FotogramasDibujado;
		public static int Avisos;

		private static string _claveDestino;
		private static string _aviso;
		private static int _avisoFotogramas;
		private static bool _llegadoAvisado;
		private static bool _ratonEncima;

		public static bool Activado => AjustesConfig.Instance == null || AjustesConfig.Instance.IndicadorGuia;

		public static float EscalaAjustes => AjustesConfig.Instance == null ? 1f :
			MathHelper.Clamp(AjustesConfig.Instance.TamanoIndicador / 100f, 0.75f, 1.5f);

		public override void OnWorldUnload()
		{
			_claveDestino = null;
			_aviso = null;
			_avisoFotogramas = 0;
			_llegadoAvisado = false;
			Estado = "oculto";
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			// Justo ANTES del texto del raton: por encima de los marcadores que el juego pinta sobre el
			// mundo (estandartes de vivienda con el inventario abierto, que en la captura real de F3b
			// con Calamity tapaban el indicador) y por debajo de tooltips y cursor.
			int indice = layers.FindIndex(c => c.Name == "Vanilla: Mouse Text");
			if (indice == -1) {
				indice = layers.FindIndex(c => c.Name == "Vanilla: Resource Bars");
				if (indice != -1) indice++;
			}
			if (indice == -1) {
				return;
			}
			layers.Insert(indice, new LegacyGameInterfaceLayer(NombreCapa, DibujarCapa, InterfaceScaleType.UI));
		}

		// =========================================================================================
		// Estado y avisos (cada fotograma de juego)
		// =========================================================================================

		public override void UpdateUI(GameTime gameTime)
		{
			if (_avisoFotogramas > 0) {
				_avisoFotogramas--;
			}
			if (!GuiaV2Sistema.HayGuia || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}
			UbicacionGuia.Objetivo o = UbicacionGuia.Siguiente;
			string clave = o != null ? o.ParadaId + "|" + o.Origen : (GuiaV2Sistema.Resumen?.Siguiente?.Parada.Id ?? "");
			bool primero = false;
			if (clave != _claveDestino) {
				primero = _claveDestino == null;
				_claveDestino = clave;
				_llegadoAvisado = false;
				if (!primero && clave.Length > 0) {
					Avisar(Idiomas.Texto("GuiaV2.Indicador.NuevoDestino", o != null ? NombreCorto(o) : TituloSiguiente()));
				}
			}
			if (o == null) {
				return;
			}
			float distancia = DistanciaCasillas(o);
			float radio = RadioLlegada(o);
			if (primero) {
				// Primera lectura tras entrar al mundo: si ya estas ahi (la primera parada suele ser
				// el punto de aparicion), no se avisa de una "llegada" que no ha ocurrido.
				_llegadoAvisado = distancia <= radio;
			}
			else if (distancia <= radio && !_llegadoAvisado) {
				_llegadoAvisado = true;
				Avisar(Idiomas.Texto("GuiaV2.Indicador.HasLlegado", NombreCorto(o)));
			}
			else if (distancia > radio * 1.6f + 6f) {
				_llegadoAvisado = false;
			}
		}

		private static void Avisar(string texto)
		{
			_aviso = texto;
			UltimoAviso = texto;
			_avisoFotogramas = DuracionAviso;
			Avisos++;
			if (Activado && !Main.gameMenu) {
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: indicador - aviso \"" + texto + "\".");
		}

		// =========================================================================================
		// Calculos (publicos para la autoprueba)
		// =========================================================================================

		public static Vector2 CentroObjetivo(UbicacionGuia.Objetivo o) => o.Tile * 16f + new Vector2(8f, 8f);

		public static float DistanciaCasillas(UbicacionGuia.Objetivo o) =>
			Vector2.Distance(Main.LocalPlayer.Center, CentroObjetivo(o)) / 16f;

		public static float AnguloHacia(UbicacionGuia.Objetivo o)
		{
			Vector2 d = CentroObjetivo(o) - Main.LocalPlayer.Center;
			return (float)Math.Atan2(d.Y, d.X);
		}

		/// <summary>Cuando se considera que has llegado: dentro de la mitad de la zona (bioma situado
		/// por su firma) o a 12 casillas de un punto exacto.</summary>
		public static float RadioLlegada(UbicacionGuia.Objetivo o) => Math.Max(12f, o.RadioTiles * 0.5f);

		/// <summary>Nombre CORTO del destino: el jefe si lo hay; si no, el bioma o la estructura.</summary>
		public static string NombreCorto(UbicacionGuia.Objetivo o)
		{
			if (!string.IsNullOrEmpty(o.NombreNpc)) {
				return o.NombreNpc;
			}
			if (o.Npc > 0) {
				string n = Lang.GetNPCNameValue(o.Npc);
				if (!string.IsNullOrEmpty(n)) {
					return n;
				}
			}
			string l = !string.IsNullOrEmpty(o.Lugar) ? o.Lugar : o.Titulo;
			return string.IsNullOrEmpty(l) ? "" : char.ToUpper(l[0]) + l.Substring(1);
		}

		private static string TituloSiguiente()
		{
			Parada p = GuiaV2Sistema.Resumen?.Siguiente?.Parada;
			return p != null ? GuiaV2Sistema.PlanoLocal(p.Titulo) : "";
		}

		public static string TextoDistancia(float casillas)
		{
			int n = casillas < 100f ? (int)Math.Round(casillas / 5f) * 5 : (int)Math.Round(casillas / 10f) * 10;
			return Idiomas.Texto("GuiaV2.Indicador.Distancia", Math.Max(n, 1));
		}

		/// <summary>Rectangulo del indicador en pantalla LOGICA y como se ha decidido.</summary>
		public static Rectangle Colocar(float escala, float alto, out string como)
		{
			float w = (Esfera + HuecoTexto + AnchoTexto + Relleno * 2f) * escala;
			float h = Math.Max(Alto * escala, alto);
			float ancho = Main.screenWidth;
			PosicionIndicadorGuia pos = AjustesConfig.Instance != null ? AjustesConfig.Instance.PosicionIndicador : PosicionIndicadorGuia.Automatica;
			if (pos == PosicionIndicadorGuia.Automatica) {
				float izq = BordeInventario, der = ancho - MargenVida;
				if (der - izq >= w) {
					como = "franja superior libre (" + (int)izq + "-" + (int)der + ")";
					float fx = (izq + der) / 2f - w / 2f, fy = Main.playerInventory ? ArribaConInventario : Arriba;
					como += EvitarIndicadorModo(ref fx, ref fy, w, h, izq);
					return new Rectangle((int)fx, (int)fy, (int)w, (int)h);
				}
				pos = PosicionIndicadorGuia.JuntoAlPersonaje;
				como = "junto al personaje (la franja de arriba mide " + (int)(der - izq) + " px, demasiado estrecha)";
			}
			else {
				como = pos == PosicionIndicadorGuia.ArribaCentro ? "arriba al centro (ajuste)" : "junto al personaje (ajuste)";
			}
			float x, y;
			if (pos == PosicionIndicadorGuia.ArribaCentro) {
				x = ancho / 2f - w / 2f;
				y = Main.playerInventory ? ArribaConInventario : Arriba;
				como += EvitarIndicadorModo(ref x, ref y, w, h, 4f);
			}
			else {
				// Del mundo a la pantalla logica: zoom del juego y luego escala de interfaz
				// (PlayerInput.SetZoom_UI no toca screenPosition, solo el tamaño y el raton).
				// A la IZQUIERDA del personaje y a su media altura: encima de la cabeza va el medidor de
				// aire (Main.DrawInterface_Resources_Breath: Top - 100; con el inventario abierto en
				// pantallas de menos de 1000 de alto, a la altura de los pies), justo debajo la barra
				// de sigilo de Calamity (50 %, 55,8 %), y a la derecha, en las pantallas estrechas,
				// el borde inferior del minimapa. Visto en las capturas reales de F3b a 800x720: encima
				// salian las burbujas a los lados, debajo pisaba aire y sigilo, a la derecha el minimapa.
				Player j = Main.LocalPlayer;
				Vector2 izquierda = Vector2.Transform(j.TopLeft - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;
				Vector2 centro = Vector2.Transform(j.Center - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;
				x = izquierda.X - 30f - w;
				y = centro.Y - h / 2f;
			}
			x = MathHelper.Clamp(x, 4f, Math.Max(4f, ancho - w - 4f));
			y = MathHelper.Clamp(y, 4f, Math.Max(4f, Main.screenHeight - h - 4f));
			return new Rectangle((int)x, (int)y, (int)w, (int)h);
		}

		/// <summary>Margen alrededor del indicador de dificultad de Calamity.</summary>
		public const float MargenIndicadorModo = 6f;

		/// <summary>
		/// Con el inventario abierto, Calamity pinta su indicador de dificultad (el icono redondo del
		/// modo, 74x74) a la izquierda de la vida, justo en la franja superior: a 1280x720 con la escala
		/// de interfaz maxima (pantalla logica de 1066) el indicador caia encima (captura real
		/// <c>guiav2-hud-inventario-1280x720-max.png</c> de F3b con Calamity). Si se pisan, primero se
		/// corre a la IZQUIERDA lo justo, sin entrar en el inventario (<paramref name="izq"/>); si no
		/// cabe, baja por DEBAJO del icono, en la misma columna (ahi no pinta nada el juego).
		/// </summary>
		/// <returns>Texto para <see cref="UltimaColocacion"/> ("" si no hacia falta).</returns>
		public static string EvitarIndicadorModo(ref float x, ref float y, float w, float h, float izq)
		{
			Rectangle? area = ReflexionCalamity.AreaIndicadorModo();
			if (area == null) {
				return "";
			}
			Rectangle zona = area.Value;
			zona.Inflate((int)MargenIndicadorModo, (int)MargenIndicadorModo);
			if (!new Rectangle((int)x, (int)y, (int)w, (int)h).Intersects(zona)) {
				return "";
			}
			if (zona.Left - w >= izq) {
				x = zona.Left - w;
				return "; a la izquierda del indicador de dificultad de Calamity";
			}
			y = zona.Bottom;
			return "; debajo del indicador de dificultad de Calamity";
		}

		/// <summary>Si el indicador va (o iria) en la franja superior libre.</summary>
		private static bool CabeEnLaFranja()
		{
			PosicionIndicadorGuia pos = AjustesConfig.Instance != null ? AjustesConfig.Instance.PosicionIndicador : PosicionIndicadorGuia.Automatica;
			float w = (Esfera + HuecoTexto + AnchoTexto + Relleno * 2f) * EscalaAjustes;
			return pos == PosicionIndicadorGuia.Automatica && Main.screenWidth - MargenVida - BordeInventario >= w;
		}

		private static bool DebeVerse()
		{
			return Activado && GuiaV2Sistema.HayGuia && !Main.gameMenu && !Main.hideUI && !Main.mapFullscreen &&
				!Main.ingameOptionsWindow && Main.InGameUI.CurrentState == null && !CaptureManager.Instance.Active &&
				// Dialogo con un NPC o cartel: su caja ocupa el centro de arriba (desde y = 100).
				string.IsNullOrEmpty(Main.npcChatText) && Main.editSign == false &&
				// Cofre o tienda abiertos: su rejilla ocupa la mitad izquierda bajo el inventario, donde
				// podria caer la posicion de reserva junto al personaje.
				!(Main.playerInventory && (Main.LocalPlayer.chest != -1 || Main.npcShop > 0) && !CabeEnLaFranja()) &&
				Main.LocalPlayer != null && Main.LocalPlayer.active && !Main.LocalPlayer.ghost &&
				GuiaV2Sistema.Resumen != null && GuiaV2Sistema.Resumen.Siguiente != null;
		}

		// =========================================================================================
		// Dibujo
		// =========================================================================================

		private static bool DibujarCapa()
		{
			if (!DebeVerse()) {
				Estado = "oculto";
				_ratonEncima = false;
				return true;
			}
			try {
				Dibujar(Main.spriteBatch);
			}
			catch (Exception e) {
				// Nunca puede tirar el HUD de una partida.
				RegistroGuia.Error(Terrakeep.LogTag + " Guia v2: EXCEPCION dibujando el indicador: " + e);
			}
			return true;
		}

		private static void Dibujar(SpriteBatch sb)
		{
			float escala = EscalaAjustes;
			UbicacionGuia.Objetivo o = UbicacionGuia.Siguiente;
			string nombre;
			string linea2;
			Color colorLinea2;
			Color colorEsfera;
			if (o == null) {
				Estado = "sin_rumbo";
				nombre = TituloSiguiente();
				linea2 = Idiomas.Texto("GuiaV2.Indicador.SinRumbo");
				colorLinea2 = EstiloTk.TextoSuave;
				colorEsfera = new Color(120, 125, 140);
				UltimaDistancia = -1f;
			}
			else {
				nombre = NombreCorto(o);
				float distancia = DistanciaCasillas(o);
				UltimaDistancia = distancia;
				UltimoAngulo = AnguloHacia(o);
				bool llegado = distancia <= RadioLlegada(o);
				Estado = llegado ? "llegado" : "en_ruta";
				linea2 = llegado ? Idiomas.Texto("GuiaV2.Indicador.EstasAhi") : TextoDistancia(distancia);
				colorLinea2 = llegado ? new Color(140, 235, 140) : new Color(225, 225, 235);
				colorEsfera = llegado ? new Color(110, 200, 110) : new Color(70, 80, 125);
			}
			UltimoNombre = nombre;

			// ---- reparto del texto ANTES de colocar la caja: si el destino necesita dos lineas, la
			// caja crece en vez de montar la distancia encima (captura real de F3b: "Jungla en el /
			// subsuelo" pisaba "~110 casillas" y se salia por abajo) ----
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			float altoLinea = fuente.MeasureString("Ay").Y;
			// Margen propio a la derecha del texto (revision visual F4: "Azote del Desierto" quedaba a
			// 2 px del borde de la caja).
			float anchoTexto = (AnchoTexto - MargenTextoDerecho) * escala;
			float medido = fuente.MeasureString(nombre).X;
			float escNombre = medido > 0f ? Math.Min(0.78f * escala, anchoTexto / medido) : 0.78f * escala;
			string nombreDibujado = nombre;
			if (escNombre < 0.62f * escala) {
				escNombre = 0.62f * escala;
				nombreDibujado = EtiquetaTk.PartirEnLineas(nombre, anchoTexto, escNombre);
			}
			int lineas = nombreDibujado.Split('\n').Length;
			float altoNombre = altoLinea * escNombre * lineas;
			float escLinea2 = 0.68f * escala;
			float medido2 = fuente.MeasureString(linea2).X;
			if (medido2 * escLinea2 > anchoTexto && medido2 > 0f) {
				escLinea2 = anchoTexto / medido2;
			}
			// La fuente deja hueco debajo de cada linea: la segunda cuenta con 0,8 de su alto.
			float altoTotal = altoNombre + altoLinea * escLinea2 * 0.8f;
			Rectangle r = Colocar(escala, altoTotal + Relleno * 2f * escala, out string como);
			UltimoRectangulo = r;
			UltimaColocacion = como;
			FotogramasDibujado++;

			AtenderRaton(r, o);

			// Fondo con el aspecto nativo de las cajas del juego.
			Color fondo = new Color(33, 43, 79) * (_ratonEncima ? 0.95f : 0.82f);
			Utils.DrawInvBG(sb, r, fondo);

			GraphicsDevice gd = sb.GraphicsDevice;
			float lado = Esfera * escala;
			Vector2 centro = new Vector2(r.X + Relleno * escala + lado / 2f, r.Y + r.Height / 2f);
			Texture2D disco = TexturasMarca.Disco(gd);
			sb.Draw(disco, centro, null, colorEsfera, 0f, new Vector2(disco.Width / 2f, disco.Height / 2f), lado / disco.Width, SpriteEffects.None, 0f);
			if (Estado == "en_ruta") {
				Texture2D flecha = TexturasMarca.Flecha(gd);
				float e = lado * 0.78f / flecha.Width;
				sb.Draw(flecha, centro, null, CapaMarcaGuia.ColorMarca, UltimoAngulo, new Vector2(flecha.Width / 2f, flecha.Height / 2f), e, SpriteEffects.None, 0f);
			}
			else if (Estado == "llegado") {
				Texture2D diana = TexturasMarca.Diana(gd);
				sb.Draw(diana, centro, null, Color.White, 0f, new Vector2(diana.Width / 2f, diana.Height / 2f), lado * 0.6f / diana.Width, SpriteEffects.None, 0f);
			}
			else {
				Vector2 t = FontAssets.MouseText.Value.MeasureString("?") * 0.9f * escala;
				EscribirTk.Dibujar(sb, "?", centro - t / 2f + new Vector2(0f, 2f * escala), Color.White, 0.9f * escala);
			}

			// Texto: destino y distancia, centrados en vertical en la caja.
			float xTexto = r.X + (Relleno + Esfera + HuecoTexto) * escala;
			float yTexto = r.Y + (r.Height - altoTotal) / 2f + 1f * escala;
			EscribirTk.Dibujar(sb, nombreDibujado, new Vector2(xTexto, yTexto), o == null ? EstiloTk.TextoSuave : CapaMarcaGuia.ColorMarca, escNombre);
			EscribirTk.Dibujar(sb, linea2, new Vector2(xTexto, yTexto + altoNombre), colorLinea2, escLinea2);

			// Aviso breve debajo (cambio de destino / has llegado), que se desvanece.
			if (_avisoFotogramas > 0 && !string.IsNullOrEmpty(_aviso)) {
				float alfa = Math.Min(1f, _avisoFotogramas / 30f);
				float escAviso = 0.66f * escala;
				string texto = EtiquetaTk.PartirEnLineas(_aviso, r.Width - 12f, escAviso);
				Vector2 tam = FontAssets.MouseText.Value.MeasureString(texto) * escAviso;
				Rectangle caja = new Rectangle(r.X, r.Bottom + 3, r.Width, (int)(tam.Y + 8f));
				Utils.DrawInvBG(sb, caja, new Color(60, 50, 20) * 0.85f * alfa);
				EscribirTk.Dibujar(sb, texto, new Vector2(caja.X + 6f, caja.Y + 5f), new Color(255, 225, 140) * alfa, escAviso);
			}

			if (_ratonEncima) {
				string tip = o != null ? CapaMarcaGuia.TextoTooltip(o, true) : Idiomas.Texto("GuiaV2.Indicador.TooltipSinRumbo", nombre);
				Main.instance.MouseText(tip + "\n" + Idiomas.Texto("GuiaV2.Indicador.Pulsa"));
			}
		}

		private static void AtenderRaton(Rectangle r, UbicacionGuia.Objetivo o)
		{
			bool dentro = r.Contains(new Point(Main.mouseX, Main.mouseY)) && !PlayerInput.IgnoreMouseInterface;
			_ratonEncima = dentro;
			if (!dentro) {
				return;
			}
			Main.LocalPlayer.mouseInterface = true;
			if (Main.mouseLeft && Main.mouseLeftRelease) {
				Main.mouseLeftRelease = false;
				Pulsar("clic en el indicador");
			}
		}

		/// <summary>Abre la parada en el panel (Guía > Ruta). Publico para la autoprueba, que pasa por
		/// el mismo camino que el clic.</summary>
		public static void Pulsar(string origen)
		{
			string id = UbicacionGuia.Siguiente != null ? UbicacionGuia.Siguiente.ParadaId : GuiaV2Sistema.Resumen?.Siguiente?.Parada.Id;
			SoundEngine.PlaySound(SoundID.MenuTick);
			if (!string.IsNullOrEmpty(id)) {
				ContenidoGuiaV2.ParadaSeleccionada = id;
			}
			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Guia, "indicador de la Guía (" + origen + ")");
			PanelTerrakeepSystem.Panel?.GuiaV2?.CambiarVista(ContenidoGuiaV2.Vista.Ruta);
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: indicador pulsado (" + origen + ") -> parada " + (id ?? "-") + " en el panel.");
		}
	}
}
