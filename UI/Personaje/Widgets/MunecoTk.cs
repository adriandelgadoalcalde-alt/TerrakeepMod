using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.Common.Personaje;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Vista previa en vivo del personaje: dibuja un muñeco con el renderer REAL del juego
	/// (<c>Main.PlayerRenderer</c>), el mismo que usa la pantalla de selección de personaje
	/// (<c>Terraria.GameContent.UI.Elements.UICharacter</c>) y el propio Maniquí de vanilla
	/// (<c>Terraria.GameContent.Tile_Entities.TEDisplayDoll</c>) — comprobados los dos con
	/// <c>ilspycmd</c> sobre el <c>tModLoader.dll</c> instalado antes de escribir esto, tal
	/// como pide la disciplina del repositorio.
	/// <para />
	/// El muñeco es un <see cref="Player"/> PROPIO, creado una sola vez con <c>new Player()</c>:
	/// es el mismo patrón que usa <c>TEDisplayDoll</c> (el Maniquí real de cada partida) y no
	/// hace falta nada más elaborado porque el constructor de <c>Player</c> ya deja
	/// <c>armor</c>/<c>dye</c>/<c>inventory</c> con un <c>new Item()</c> (aire) en cada ranura.
	/// Nunca se toca <see cref="Main.LocalPlayer"/>: cada fotograma solo se LEE para copiar el
	/// pelo, la variante, los siete colores y, si toca enseñar armadura, las mismas referencias
	/// de <see cref="Item"/> que ya lleva puestas (compartir la referencia para dibujar no las
	/// modifica).
	/// <para />
	/// <c>isDisplayDollOrInanimate = true</c> es el mismo campo que pone <c>TEDisplayDoll</c>:
	/// sin él, código de vanilla que compara <c>whoAmI == Main.myPlayer</c> trataría a este
	/// muñeco (cuyo <c>whoAmI</c> nunca se ha tocado y vale 0, el mismo índice que el jugador
	/// real en partida de un jugador) como si fuera el jugador de verdad.
	/// </summary>
	public class MunecoTk : UIElement
	{
		private readonly Player _muneco = new Player();
		private readonly Item[] _armaduraVacia = CrearArrayDeAire(PersonajeVivo.SlotsEquipo);
		private readonly Item[] _tinteVacio = CrearArrayDeAire(PersonajeVivo.SlotsTinte);

		/// <summary>Si se enseña el equipo puesto o el personaje "desnudo" (solo pelo, piel y
		/// colores). No se guarda como campo del muñeco: <c>PestanaApariencia</c> lo controla con
		/// un <see cref="AlternadorTk"/> propio.</summary>
		public bool ConArmadura = true;

		public MunecoTk()
		{
			UseImmediateMode = true;
			OverrideSamplerState = SamplerState.PointClamp;
		}

		private static Item[] CrearArrayDeAire(int n)
		{
			Item[] array = new Item[n];
			for (int i = 0; i < n; i++) {
				array[i] = new Item();
			}
			return array;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			Sincronizar();
			ComprobarInmunidadALaLuzReal();
		}

		/// <summary>Copia del jugador real solo lo que hace falta para el dibujado: pelo, tinte,
		/// variante, los siete colores y (si toca) el equipo puesto. Barato -sin serializar nada-
		/// para que se pueda llamar en cada fotograma sin retraso perceptible.</summary>
		private void Sincronizar()
		{
			if (!PersonajeVivo.HayJugador) {
				return;
			}

			Player jugador = PersonajeVivo.Jugador;
			Player muneco = _muneco;

			muneco.hair = jugador.hair;
			muneco.hairDye = jugador.hairDye;
			muneco.skinVariant = jugador.skinVariant;
			muneco.hairColor = jugador.hairColor;
			muneco.skinColor = jugador.skinColor;
			muneco.eyeColor = jugador.eyeColor;
			muneco.shirtColor = jugador.shirtColor;
			muneco.underShirtColor = jugador.underShirtColor;
			muneco.pantsColor = jugador.pantsColor;
			muneco.shoeColor = jugador.shoeColor;
			muneco.direction = 1;

			if (ConArmadura) {
				for (int i = 0; i < PersonajeVivo.SlotsEquipo; i++) {
					muneco.armor[i] = jugador.armor[i];
				}
				for (int i = 0; i < PersonajeVivo.SlotsTinte; i++) {
					muneco.dye[i] = jugador.dye[i];
				}
				muneco.hideVisibleAccessory = jugador.hideVisibleAccessory;
			}
			else {
				for (int i = 0; i < PersonajeVivo.SlotsEquipo; i++) {
					muneco.armor[i] = _armaduraVacia[i];
				}
				for (int i = 0; i < PersonajeVivo.SlotsTinte; i++) {
					muneco.dye[i] = _tinteVacio[i];
				}
			}

			// Mismos cinco pasos que TEDisplayDoll.Draw antes de renderizar su Maniqui: sin
			// isDisplayDollOrInanimate a true, ResetEffects()/PlayerFrame() tratarian a este
			// muñeco como si fuera el jugador de verdad en cuanto whoAmI coincidiera con
			// Main.myPlayer (los dos valen 0 en una partida de un jugador).
			muneco.isDisplayDollOrInanimate = true;
			muneco.ResetEffects();
			muneco.ResetVisibleAccessories();
			muneco.UpdateDyes();
			muneco.DisplayDollUpdate();
			muneco.UpdateSocialShadow();
			muneco.PlayerFrame();
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (!PersonajeVivo.HayJugador) {
				return;
			}

			CalculatedStyle dim = GetDimensions();
			if (dim.Width < 40f || dim.Height < 60f) {
				// Hueco demasiado pequeño (ventana muy estrecha): mejor no dibujar nada a
				// dibujar un muñeco recortado o con una escala absurda.
				return;
			}

			// Escala para que el muñeco (alto real ~56 px de sprite, Player.height=42 de
			// hitbox) quepa con aire dentro del hueco disponible, igual que hace
			// UICharacter.characterScale con las fichas de la lista de personajes.
			float escala = MathHelper.Clamp((dim.Height - 20f) / 84f, 0.6f, 2.2f);

			// Misma cuenta que UICharacter.GetPlayerPosition: centrado horizontal, con el
			// origen del sprite (los pies) apoyado cerca del borde inferior del hueco.
			Vector2 posicion = new Vector2(
				dim.X + dim.Width * 0.5f - _muneco.width * 0.5f * escala,
				dim.Y + dim.Height - 12f - _muneco.height * escala);

			// Igual que UICharacter: la posicion que espera DrawPlayer es de "mundo", y sumar
			// Main.screenPosition a una posicion ya en coordenadas de pantalla/UI hace que la
			// camara la vuelva a proyectar exactamente al mismo sitio de pantalla.
			//
			// Confirmado el usuario tenia razon, y con DOS causas reales distintas (decompilado
			// tModLoader.dll instalado con ilspycmd, no el de referencia - las dos conviven y las
			// arregla el mismo cambio de abajo):
			//
			// 1) EL PARPADEO (piel/ojos/ropa/armadura): Terraria.DataStructures.PlayerDrawSet.
			//    BoringSetup_2 guarda el "position + Main.screenPosition" que se le pasa a
			//    DrawPlayer en su campo Position y con el muestrea Lighting.GetColorClamped(...)
			//    para cada color. O sea: ilumina el muneco con la luz REAL del mundo en el tile
			//    que hay detras del hueco de pantalla donde se dibuja - que al sumar
			//    Main.screenPosition (la camara sigue al jugador real) es siempre el entorno
			//    inmediato del jugador de verdad. Antorchas, ciclo dia/noche y bioma cambian ese
			//    color cada fotograma: de ahi el parpadeo.
			// 2) EL PELO "NEGRO" (causa aparte, mas grave, no depende de si hay poca luz cerca):
			//    Terraria.Player.GetHairColor() -el metodo que SI usa PlayerDrawSet para
			//    colorHair, linea aparte de la del resto de colores- muestrea la luz en
			//    "this.position" (el campo .position del propio Player que se dibuja), NO en el
			//    Position del punto 1. Y "_muneco" es un Player propio al que MunecoTk NUNCA le ha
			//    puesto un .position (Sincronizar() solo copia pelo/tinte/variante/colores/equipo,
			//    nunca posicion) - se queda en Vector2.Zero de fabrica, o sea tile (0,0), la
			//    esquina del mapa, un tile que no esta iluminado NUNCA por nada real. Por eso el
			//    pelo salia negro SIEMPRE, sin importar donde estuviera el jugador real ni si
			//    parpadeaba el resto del muneco - un bug independiente del parpadeo, solo que con
			//    el mismo arreglo de abajo.
			//
			// La propia Terraria.Lighting ya trae la salida para las dos: comprobadas las CINCO
			// sobrecargas de GetColor/GetColorClamped (las que usa el punto 1 Y la de dos
			// argumentos que usa GetHairColor), la primera linea de todas es
			// "if (Main.gameMenu) return oldColor;" (o "return Color.White;" en la de dos
			// argumentos, que es literalmente la que consume GetHairColor) - en los dos casos
			// devuelve el color de entrada sin tocar el motor de luces real, ignorando el tile que
			// se le haya pasado. Es exactamente por lo que UICharacter (la ficha de la pantalla de
			// seleccion de personaje, que corre con Main.gameMenu=true porque ahi ni siquiera hay
			// mundo cargado) sale siempre a color pleno, pelo incluido. Aqui, en cambio, el panel
			// se abre EN PARTIDA con Main.gameMenu=false, así que si se hace lo mismo (forzarlo a
			// true solo alrededor de este DrawPlayer, restaurandolo enseguida) se consigue la
			// misma "luz blanca plena" de la ficha de personaje para TODOS los colores a la vez,
			// sin tocar ningun override oscuro del motor de luces ni tener que arreglar el .position
			// que le falta a "_muneco" por separado. Revisado tambien
			// Terraria.DataStructures.PlayerDrawLayers: el UNICO sitio que mira Main.gameMenu es
			// el pierna/pose de reposo (fuerza el fotograma de "de pie"), inofensivo y hasta
			// deseable para una vista previa estatica. El cambio es sincrono (sin await de por
			// medio) y se deshace en el mismo hilo antes de que ningun otro sistema del juego
			// pueda leerlo, con try/finally por si algun mod (Calamity incluido) lanzara una
			// excepcion dibujando una capa.
			bool gameMenuAnterior = Main.gameMenu;
			Main.gameMenu = true;
			try {
				Main.PlayerRenderer.DrawPlayer(Main.Camera, _muneco,
					posicion + Main.screenPosition, 0f, Vector2.Zero, 0f, escala);
			}
			finally {
				Main.gameMenu = gameMenuAnterior;
			}
		}

		// ==================================================================================
		// SOLO ARNES DE PRUEBAS a partir de aqui: demuestra en el juego real que el arreglo de
		// arriba (forzar Main.gameMenu=true alrededor de DrawPlayer) deja al muñeco inmune a la
		// luz real del mundo, no solo "se supone que si" por lo que dice el codigo decompilado.
		// No hace nada si no esta puesta la variable de entorno - jugando normal esto es un
		// no-op total (la unica llamada nueva en Update() sale por el primer if).
		// ==================================================================================
		private const string VariableAutopruebaLuz = "TERRAKEEP_AUTOTEST_MUNECO_LUZ";
		private static bool _luzComprobada;
		private static bool _luzActiva;
		private static bool _luzTerminada;
		private static int _luzFotogramas;
		private static Color? _luzPixelAntes;

		/// <summary>
		/// Reproduce EXACTAMENTE el escenario que denuncio el usuario ("mi personaje esta al lado
		/// de una antorcha... el menu tambien lo refleja") pero de forma controlada y sin depender
		/// de la geografia de ningun mundo de prueba concreto: en vez de mover al jugador real
		/// hasta encontrar una antorcha, se inyecta luz blanca MUY fuerte con
		/// <see cref="Lighting.AddLight(int,int,float,float,float)"/> - la misma funcion que usa
		/// una antorcha real cada fotograma - directamente en el tile que
		/// <c>Terraria.DataStructures.PlayerDrawSet.BoringSetup_2</c> muestrea para colorear el
		/// muñeco (formula decompilada y documentada en <see cref="DrawSelf"/>: X centrado, Y a
		/// mitad de altura). Si el pixel real renderizado en pantalla es IDENTICO antes y despues
		/// de encender esa "antorcha pegada al muñeco", el arreglo es inmune a la luz de verdad -
		/// no solo sobre el papel.
		/// </summary>
		private void ComprobarInmunidadALaLuzReal()
		{
			if (!_luzComprobada) {
				_luzComprobada = true;
				_luzActiva = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableAutopruebaLuz));
			}
			if (!_luzActiva || _luzTerminada || !PersonajeVivo.HayJugador) {
				return;
			}

			CalculatedStyle dim = GetDimensions();
			if (dim.Width < 40f || dim.Height < 60f) {
				return;
			}

			_luzFotogramas++;

			float escala = MathHelper.Clamp((dim.Height - 20f) / 84f, 0.6f, 2.2f);
			Vector2 posicion = new Vector2(
				dim.X + dim.Width * 0.5f - _muneco.width * 0.5f * escala,
				dim.Y + dim.Height - 12f - _muneco.height * escala);
			Vector2 mundo = posicion + Main.screenPosition;
			int tileX = (int)((mundo.X + _muneco.width * 0.5f) / 16f);
			int tileY = (int)((mundo.Y + _muneco.height * 0.5f) / 16f);

			// A partir del fotograma 30, "antorcha" clavada en ese tile cada fotograma: da tiempo
			// de sobra (40 fotogramas seguidos) a que el motor de luces la propague antes de la
			// segunda lectura del fotograma 70, aunque esta llamada (en Update, fase de logica) y
			// el propio motor de luces (que corre en su propio paso) no esten perfectamente
			// sincronizados fotograma a fotograma.
			if (_luzFotogramas > 30) {
				Lighting.AddLight(tileX, tileY, 4f, 4f, 4f);
			}

			if (_luzFotogramas != 25 && _luzFotogramas != 70) {
				return;
			}

			// Lee el fotograma YA presentado (el mismo truco que ya uso y verifico
			// CapturaDePantalla.Guardar: llamado desde la fase de logica/UpdateUI, nunca desde
			// dentro de un Draw en marcha) en el punto medio de la caja del muñeco - torso/piel,
			// visible tanto con armadura como sin ella en el equipo de prueba real.
			Vector2 pantalla = dim.Position() + new Vector2(dim.Width * 0.5f, dim.Height * 0.4f);
			Color pixel = LeerPixelDeLaPantalla((int)pantalla.X, (int)pantalla.Y);
			Color luzRealEnElTile = Lighting.GetColor(tileX, tileY);

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA MUÑECO/luz - fotograma " + _luzFotogramas +
				" tile=(" + tileX + "," + tileY + ") Lighting.GetColor real ahi=" + luzRealEnElTile +
				" pixel renderizado del muñeco en pantalla=" + pixel);

			if (_luzFotogramas == 25) {
				_luzPixelAntes = pixel;
				return;
			}

			bool ok = _luzPixelAntes.HasValue && pixel == _luzPixelAntes.Value;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA MUÑECO/luz RESULTADO: " +
				(ok ? "OK" : "FALLO") + " - pixel sin la antorcha forzada=" + _luzPixelAntes +
				", pixel con la antorcha forzada en el mismo tile=" + pixel +
				(ok ? " (identicos pese a la luz real distinta: el muñeco es inmune)"
					 : " (DISTINTOS: el muñeco sigue reflejando la luz real del mundo)"));
			_luzTerminada = true;
		}

		private static Color LeerPixelDeLaPantalla(int x, int y)
		{
			GraphicsDevice dispositivo = Main.instance.GraphicsDevice;
			PresentationParameters parametros = dispositivo.PresentationParameters;
			if (x < 0 || y < 0 || x >= parametros.BackBufferWidth || y >= parametros.BackBufferHeight) {
				return Color.Transparent;
			}
			Color[] uno = new Color[1];
			dispositivo.GetBackBufferData(new Rectangle(x, y, 1, 1), uno, 0, 1);
			return uno[0];
		}
	}
}
