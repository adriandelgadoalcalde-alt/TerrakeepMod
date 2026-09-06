using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// El icono de Terrakeep en el HUD del juego, <b>en la fila de iconos que el propio Terraria
	/// pone junto al inventario</b> (papelera, bestiario, emotes). Un clic abre el panel.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Donde va, y por que ahi.</b> Esos tres iconos no son una fila generica que se pueda
	/// extender: son tres metodos privados de <c>Terraria.Main</c>
	/// (<c>DrawTrashItemSlot</c>, <c>DrawBestiaryIcon</c>, <c>DrawEmoteBubblesButton</c>) llamados
	/// en cadena desde <c>Main.DrawInventory()</c>, con <b>coordenadas fijas escritas a mano</b> -
	/// no dependen de <c>Main.screenWidth</c> ni de <c>Main.mapStyle</c>, comprobado en el codigo
	/// decompilado del <c>tModLoader.dll</c> instalado (v2026.7.3.0). Los rectangulos reales son:
	/// </para>
	/// <list type="bullet">
	/// <item>papelera: (448, 258), del tamaño de <c>InventoryBack</c> a escala 0.85;</item>
	/// <item>bestiario: (498, 278, 30, 30);</item>
	/// <item>emotes: (534, 278, 30, 30).</item>
	/// </list>
	/// <para>
	/// El icono de Terrakeep continua esa fila en (<b>570</b>, 278, 30, 30), respetando la misma
	/// separacion de 6 px que hay entre el bestiario y los emotes. Y replica los mismos
	/// desplazamientos que el juego aplica a esa fila cuando hay un cofre o una tienda abiertos
	/// (<c>num2 += 168; num += 5;</c>) y cuando se esta renombrando un cofre
	/// (<c>if (editChest) num2 += 24;</c>), para no quedarse descolgado.
	/// </para>
	/// <para>
	/// <b>Como se dibuja.</b> Con <c>ModSystem.ModifyInterfaceLayers</c>, insertando una
	/// <c>LegacyGameInterfaceLayer</c> justo DESPUES de <c>"Vanilla: Inventory"</c> y con
	/// <c>InterfaceScaleType.UI</c>, que es la escala de esa capa.
	/// <c>ModSystem.PreDrawInterface</c> <b>no existe</b> en esta version, y
	/// <c>PostDrawInterface</c> esta desaconsejada por el propio XML-doc de tModLoader (y ademas
	/// cuelga de la capa 34, "Vanilla: Mouse Text").
	/// </para>
	/// <para>
	/// <b>Consecuencia real, y es la correcta:</b> la capa <c>"Vanilla: Fancy UI"</c> es la 14 y
	/// devuelve false siempre que hay un panel de <c>IngameFancyUI</c> abierto, lo que corta el
	/// recorrido de capas ahi mismo. El inventario es la 28, o sea que va despues: <b>el icono se
	/// ve con el panel CERRADO y desaparece con el panel abierto</b>. Es justo lo que hace falta
	/// (el panel ocupa la pantalla entera y oscurece el fondo, un icono flotando encima seria
	/// ruido), y es la razon de que en la practica el icono sirva para ABRIR: para cerrar estan el
	/// boton "Cerrar" del propio panel y la tecla. El clic llama igualmente a
	/// <c>AlternarArea</c>, o sea que si alguna vez se viera con el panel abierto, lo cerraria.
	/// </para>
	/// <para>
	/// <b>La animacion</b> es la misma que la de <c>BotonTk</c> y sale del mismo sitio real
	/// (<c>Main.DrawSettingButton</c>): escala 0.8 -&gt; 0.96 a 0.02 por fotograma, sonido
	/// <c>SoundID.MenuTick</c> solo al entrar el raton, y "hundido" al pulsar. El fotograma 1 de la
	/// textura (la version iluminada) se usa con el raton encima, exactamente igual que hace
	/// <c>Main.DrawBestiaryIcon</c> con <c>value.Frame(2, 1, flag ? 1 : 0)</c>.
	/// </para>
	/// </remarks>
	public class IconoHudTerrakeep : ModSystem
	{
		/// <summary>Ruta del asset dentro del <c>.tmod</c>.</summary>
		public const string RutaTextura = "TerrakeepMod/Assets/IconoTerrakeep";

		/// <summary>Nombre de la capa de interfaz. Sale en la lista que ven los demas mods.</summary>
		public const string NombreCapa = "TerrakeepMod: Icono de Terrakeep";

		private const int Lado = 30;
		private const int XBase = 570;
		private const int YBase = 278;

		// Constantes de animacion, las mismas de Main.DrawSettingButton.
		private const float EscalaReposo = 0.8f;
		private const float EscalaSobre = 0.96f;
		private const float PasoPorFotograma = 0.02f;

		private static Asset<Texture2D> _textura;
		private static bool _ratonEncima;
		private static float _escala = EscalaReposo;

		/// <summary>Ultimo rectangulo en el que se dibujo el icono. Es la evidencia real de donde
		/// esta en pantalla; la lee la verificacion final.</summary>
		public static Rectangle UltimoRectangulo;

		/// <summary>Fotogramas en los que el icono se ha llegado a dibujar de verdad.</summary>
		public static int FotogramasDibujado;

		/// <summary>Veces que se ha abierto el panel desde el icono.</summary>
		public static int VecesPulsado;

		public override void Load()
		{
			if (!Main.dedServ) {
				// ImmediateLoad porque el icono se dibuja en el HUD y no puede permitirse un
				// fotograma con la textura a medio cargar.
				_textura = ModContent.Request<Texture2D>(RutaTextura, AssetRequestMode.ImmediateLoad);
			}
		}

		public override void Unload()
		{
			_textura = null;
			UI.Personaje.Widgets.BotonTk.Descargar();
		}

		/// <summary>
		/// Mete la capa del icono justo detras de la del inventario de vanilla.
		/// </summary>
		/// <remarks>
		/// <c>Main.DrawInterface</c> hace una copia fresca de la lista en cada fotograma y
		/// <c>SystemLoader.ModifyInterfaceLayers</c> pone <c>Active = true</c> en todas antes de
		/// llamar a los mods, asi que no hay estado que conservar entre fotogramas. Se comprueba
		/// siempre <c>indice != -1</c>: otro mod podria haber quitado esa capa.
		/// </remarks>
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int indice = layers.FindIndex(capa => capa.Name == "Vanilla: Inventory");
			if (indice == -1) {
				return;
			}

			layers.Insert(indice + 1, new LegacyGameInterfaceLayer(
				NombreCapa, DibujarYAtender, InterfaceScaleType.UI));
		}

		/// <summary>Rectangulo del icono AHORA, con los mismos desplazamientos que el juego aplica
		/// a la fila del bestiario y los emotes.</summary>
		public static Rectangle RectanguloAhora()
		{
			int x = XBase;
			int y = YBase;

			// Codigo real de Main.DrawBestiaryIcon / DrawEmoteBubblesButton.
			if ((Main.LocalPlayer != null && Main.LocalPlayer.chest != -1 || Main.npcShop > 0) && !Main.recBigList) {
				y += 168;
				x += 5;
			}
			if (Main.editChest) {
				y += 24;
			}

			return new Rectangle(x, y, Lado, Lado);
		}

		private static bool DibujarYAtender()
		{
			// Los iconos de esta fila son parte del inventario: el juego los dibuja desde
			// Main.DrawInventory, que solo corre si Main.playerInventory es true. Se respeta.
			if (_textura == null || !Main.playerInventory || Main.gameMenu ||
				Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_ratonEncima = false;
				_escala = EscalaReposo;
				return true;
			}

			Rectangle zona = RectanguloAhora();
			UltimoRectangulo = zona;
			FotogramasDibujado++;

			AtenderRaton(zona);
			Dibujar(zona);
			return true;
		}

		/// <summary>
		/// Hover y clic, con el patron REAL de vanilla para un rectangulo dibujado a mano (codigo
		/// de <c>Main.DrawBestiaryIcon</c>): <c>Contains(mouseX, mouseY)</c>, respetar
		/// <c>PlayerInput.IgnoreMouseInterface</c>, poner <c>mouseInterface = true</c> para que el
		/// clic no llegue al mundo, y consumir el clic con <c>mouseLeftRelease = false</c>.
		/// </summary>
		private static void AtenderRaton(Rectangle zona)
		{
			bool dentro = zona.Contains(new Point(Main.mouseX, Main.mouseY)) &&
				!PlayerInput.IgnoreMouseInterface;

			if (dentro) {
				if (!_ratonEncima) {
					// Solo al ENTRAR, igual que Main.DrawSettingButton: if (!mouseOver) PlaySound(12).
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
				_ratonEncima = true;
				Main.LocalPlayer.mouseInterface = true;

				if (Main.mouseLeft && Main.mouseLeftRelease) {
					Main.mouseLeftRelease = false;
					Pulsar("icono del HUD");
				}
			}
			else {
				_ratonEncima = false;
			}

			if (_ratonEncima) {
				if (_escala < EscalaSobre) {
					_escala = Math.Min(EscalaSobre, _escala + PasoPorFotograma);
				}
			}
			else if (_escala > EscalaReposo) {
				_escala = Math.Max(EscalaReposo, _escala - PasoPorFotograma);
			}
		}

		/// <summary>Lo que hace el clic. Separado para que la autoprueba pueda recorrer el mismo
		/// camino sin depender de que haya un raton fisico.</summary>
		private static void Pulsar(string origen)
		{
			SoundEngine.PlaySound(SoundID.MenuTick);
			_escala = EscalaReposo;
			VecesPulsado++;

			// Abre por donde se dejo el panel la ultima vez, que es lo que uno espera de un icono
			// sin pestaña propia. Con el panel abierto lo cerraria (ver el comentario de la clase:
			// en la practica no llega a verse abierto, porque la capa no se dibuja).
			PanelTerrakeepSystem.AlternarArea(UI.Panel.PanelTerrakeepState.UltimaArea, origen);
		}

		private static void Dibujar(Rectangle zona)
		{
			float avance = (_escala - EscalaReposo) / (EscalaSobre - EscalaReposo);
			int crece = (int)(2f * avance);

			Rectangle destino = new Rectangle(
				zona.X - crece, zona.Y - crece, zona.Width + crece * 2, zona.Height + crece * 2);

			// Dos fotogramas en una fila, y se recortan 2 px, exactamente como hace vanilla con el
			// icono del bestiario: value.Frame(2, 1, flag ? 1 : 0) con Width -= 2; Height -= 2.
			Rectangle origen = _textura.Value.Frame(2, 1, _ratonEncima ? 1 : 0);
			origen.Width -= 2;
			origen.Height -= 2;

			Main.spriteBatch.Draw(_textura.Value, destino, origen, Color.White);

			if (_ratonEncima) {
				Main.instance.MouseText("Terrakeep  (" +
					PanelTerrakeepSystem.TeclaDe(AreaTerrakeep.Personaje) + ")");
			}
		}

		// -------------------------------------------------------------------------------------
		// Arnes de pruebas
		// -------------------------------------------------------------------------------------

		/// <summary>
		/// SOLO ARNES DE PRUEBAS: recorre el mismo camino que un clic real sobre el icono
		/// (comprobacion de que el punto cae dentro del rectangulo incluida) y devuelve la
		/// descripcion de lo que ha pasado. No llama al metodo de abrir por atajo: pasa por
		/// <see cref="Pulsar"/>, que es lo que ejecuta el clic de verdad.
		/// </summary>
		public static string ProbarClicEn(int x, int y)
		{
			Rectangle zona = RectanguloAhora();
			bool dentro = zona.Contains(new Point(x, y));
			if (!dentro) {
				return "el punto (" + x + ", " + y + ") NO cae dentro del icono " + Describir(zona);
			}

			Pulsar("icono del HUD (autoprueba)");
			return "clic en (" + x + ", " + y + ") sobre el icono " + Describir(zona) +
				" -> panel abierto=" + PanelTerrakeepSystem.PanelAbierto +
				", pestaña=\"" + UI.Panel.PanelTerrakeepState.NombresDeArea[(int)PanelTerrakeepSystem.AreaAbierta] + "\"";
		}

		public static string Describir(Rectangle zona)
		{
			return "x=" + zona.X + " y=" + zona.Y + " " + zona.Width + "x" + zona.Height;
		}
	}
}
