using Terraria.GameInput;
using Terraria.ModLoader;

namespace TerrakeepMod.Common
{
	/// <summary>
	/// Via recomendada por tModLoader para leer atajos de teclado (ver el comentario de
	/// <c>ModKeybind</c> en el codigo del propio juego: "It is suggested to access the keybind
	/// status only in ModPlayer.ProcessTriggers").
	/// <para />
	/// Se mantiene ademas de la lectura en <see cref="PanelPruebaSystem.UpdateUI"/> porque esta
	/// no se ejecuta cuando el panel ya esta abierto en algunos estados de la interfaz; entre las
	/// dos cubren abrir y cerrar. El guarda por fotograma de
	/// <see cref="PanelPruebaSystem.AlternarPanel"/> evita que la misma pulsacion cuente dos veces.
	/// </summary>
	public class PanelPruebaPlayer : ModPlayer
	{
		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Terrakeep.AbrirPanelKeybind != null && Terrakeep.AbrirPanelKeybind.JustPressed) {
				PanelPruebaSystem.AlternarPanel("atajo de teclado (ModPlayer.ProcessTriggers)");
			}
		}
	}
}
