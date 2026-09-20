using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// <b>TM1 del catálogo de rediseño visual ("Pestañas con sprite real del juego"):</b> los 8
	/// iconos reales de la barra de pestañas del panel único, uno por <c>AreaTerrakeep</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>"Personaje" NO usa la cabeza real del jugador - investigado y descartado con evidencia
	/// real, no por pereza.</b> El primer intento fue <c>Main.MapPlayerRenderer.DrawPlayerHead</c>
	/// (el renderer REAL con el que vanilla dibuja los iconos de jugador del mapa/minimapa,
	/// <c>BotonTk.IconoCabezaJugador</c> - todavía existe en el código por si algún día se resuelve
	/// bien). Probado en el juego real: sale un borrón negro solido, nunca la cabeza. Causa real,
	/// confirmada decompilando <c>MapHeadRenderer.RenderDrawData</c> (tModLoader.dll instalado):
	/// dibuja con <c>Main.spriteBatch</c> Y aplica un pase de <c>Main.pixelShader</c> (el shader de
	/// color de piel/tinte del jugador), asumiendo el mismo contexto de <c>SpriteBatch.Begin()</c>
	/// con el que vanilla lo llama desde su propio bucle de dibujado del HUD - un contexto que el
	/// <c>Draw</c> de este panel, con su propio <c>SpriteBatch.Begin()</c> normal sin ese shader
	/// atado, no reproduce. Arreglarlo de verdad exigiría abrir un <c>SpriteBatch.Begin()</c>
	/// aparte con los parámetros exactos que esa función espera - riesgo real de romper el resto
	/// del dibujado del panel para un icono de 20x20px, así que se descarta por ahora. (El otro
	/// renderer real del motor, <c>Main.PlayerRenderer.DrawPlayer</c> - el cuerpo entero, ya usado
	/// con éxito por <c>MunecoTk</c> en la pestaña Apariencia - SÍ es seguro, pero un cuerpo entero
	/// encogido a un icono de pestaña se ve mal a ese tamaño; se descarta también por estética, no
	/// por riesgo técnico.)
	/// </para>
	/// <para>
	/// El primer intento de icono ESTATICO de repuesto, <c>Bestiary/Portrait_Front</c>, tampoco
	/// sirvió - probado en el juego real, sale un marco vacío (es el BORDE del retrato del
	/// Bestiario, no el contenido: la silueta real que vanilla pinta encima es otra textura
	/// aparte). En su lugar, "Personaje" usa <c>Images/Heart</c>: el corazón real con el que el
	/// propio juego representa la vitalidad del personaje (el mismo icono del cristal de vida al
	/// caer al suelo) - temático de verdad (la pestaña Personaje empieza mostrando vida/maná) y
	/// confirmado relleno y reconocible en una captura real.
	/// </para>
	/// </remarks>
	/// <remarks>
	/// <para>
	/// <b>Rutas investigadas contra el <c>Content/</c> real de la instalación de Steam antes de
	/// escribir esto</b> (nunca adivinadas por el nombre): son texturas legales para el mod porque
	/// corre DENTRO de tModLoader con el juego base instalado - se piden con
	/// <c>Main.Assets.Request</c>, nunca se redistribuye ningún archivo.
	/// </para>
	/// <para>
	/// <c>ChestStack_0</c> (cofre real, animación de "cofre lleno"), <c>Craft</c> (icono real de
	/// "se puede fabricar aquí"), <c>Bestiary/Button_Search</c> (la lupa REAL del buscador del
	/// Bestiario/Modo Viaje), <c>Camera_0</c> (icono real de cámara, el mismo concepto que ya usa
	/// vanilla en su propia UI) son todos iconos de UN SOLO concepto, ya recortados a tamaño de
	/// icono - se dibujan tal cual, sin recortar ningún fotograma de una hoja de sprites.
	/// </para>
	/// <para>
	/// <c>Map_0</c> y <c>Research_GearA</c> son la mejor aproximación real encontrada para
	/// Exploración/Ajustes - no hay un icono vanilla de "mapa genérico" ni de "engranaje genérico"
	/// de un solo concepto (el menú de opciones de vanilla es todo texto, sin icono de engranaje;
	/// el mapa se abre por atajo, sin botón con icono propio). Quedan sujetas a verificación visual
	/// real en el juego (captura + inspección de píxeles) antes de darlas por buenas del todo.
	/// </para>
	/// <para>
	/// El icono del Guía se resuelve en vivo con <c>TownNPCProfiles.GetHeadIndexSafe</c> (el mismo
	/// mecanismo real con el que el propio juego decide qué icono de cabeza usar para el marcador
	/// de un NPC de pueblo en el mapa) sobre un ejemplar de muestra de <c>NPCID.Guide</c> - la
	/// cabeza REAL del NPC Guía, no un libro genérico inventado.
	/// </para>
	/// </remarks>
	public static class IconosPestanas
	{
		private static Asset<Texture2D> _personaje;
		private static Asset<Texture2D> _libreria;
		private static Asset<Texture2D> _builds;
		private static Asset<Texture2D> _investigacion;
		private static Asset<Texture2D> _exploracion;
		private static Asset<Texture2D> _ajustes;
		private static Asset<Texture2D> _guia;
		private static Asset<Texture2D> _album;
		private static int _indiceCabezaGuia = -2; // -2 = todavia no resuelto, -1 = no se pudo resolver

		public static Texture2D Personaje() => Cargar(ref _personaje, "Images/Heart");
		public static Texture2D Libreria() => Cargar(ref _libreria, "Images/UI/ChestStack_0");
		public static Texture2D Builds() => Cargar(ref _builds, "Images/UI/Craft");
		public static Texture2D Investigacion() => Cargar(ref _investigacion, "Images/UI/Bestiary/Button_Search");
		public static Texture2D Exploracion() => Cargar(ref _exploracion, "Images/Map_0");
		public static Texture2D Ajustes() => Cargar(ref _ajustes, "Images/UI/Creative/Research_GearA");
		public static Texture2D Album() => Cargar(ref _album, "Images/UI/Camera_0");

		public static Texture2D Guia()
		{
			if (_indiceCabezaGuia == -2) {
				_indiceCabezaGuia = ResolverIndiceCabezaGuia();
			}
			if (_indiceCabezaGuia < 0) {
				return null;
			}
			return Cargar(ref _guia, "Images/NPC_Head_" + _indiceCabezaGuia);
		}

		/// <summary>El índice real de <see cref="TextureAssets.NpcHead"/> para el NPC Guía,
		/// resuelto con la misma API que usa el propio juego para el marcador del mapa. -1 si no se
		/// pudo resolver (nunca lanza: es dibujado de UI, no debe poder tumbar el panel).</summary>
		private static int ResolverIndiceCabezaGuia()
		{
			try {
				NPC muestra = new NPC();
				muestra.SetDefaults(NPCID.Guide);
				return TownNPCProfiles.GetHeadIndexSafe(muestra);
			}
			catch {
				return -1;
			}
		}

		private static Texture2D Cargar(ref Asset<Texture2D> campo, string ruta)
		{
			// Mismo patron real (sin comprobar IsLoaded) que ya usa BotonTk.CargarTexturas - Request
			// con el modo por defecto del mod ya es sincrono, comprobado en produccion.
			if (campo == null) {
				campo = Main.Assets.Request<Texture2D>(ruta);
			}
			return campo.Value;
		}

		/// <summary>Se llama al descargar el mod: las texturas del juego no deben quedarse
		/// referenciadas desde un ensamblado que se va.</summary>
		public static void Descargar()
		{
			_personaje = null;
			_libreria = null;
			_builds = null;
			_investigacion = null;
			_exploracion = null;
			_ajustes = null;
			_guia = null;
			_album = null;
			_indiceCabezaGuia = -2;
		}
	}
}
