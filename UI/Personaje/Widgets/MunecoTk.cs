using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;
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
			Main.PlayerRenderer.DrawPlayer(Main.Camera, _muneco,
				posicion + Main.screenPosition, 0f, Vector2.Zero, 0f, escala);
		}
	}
}
