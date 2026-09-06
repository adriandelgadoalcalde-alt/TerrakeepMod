using Terraria.GameInput;
using Terraria.ModLoader;

namespace TerrakeepMod.Common
{
	/// <summary>
	/// Via recomendada por tModLoader para leer atajos de teclado (ver el comentario de
	/// <c>ModKeybind</c> en el codigo del propio juego: "It is suggested to access the keybind
	/// status only in ModPlayer.ProcessTriggers").
	/// <para />
	/// Se mantiene ademas de la lectura en <c>PanelTerrakeepSystem.UpdateUI</c> porque esta no se
	/// ejecuta cuando el panel ya esta abierto en algunos estados de la interfaz; entre las dos
	/// cubren abrir y cerrar. El guarda por fotograma de
	/// <c>PanelTerrakeepSystem.AlternarArea</c> evita que la misma pulsacion cuente dos veces.
	/// <para />
	/// Solo se duplica la tecla PRINCIPAL (la K, area de Personaje): es la que tiene que poder
	/// cerrar el panel pase lo que pase. Las otras cinco se leen unicamente en el
	/// <c>UpdateUI</c> del sistema del panel.
	/// </summary>
	public class PanelPruebaPlayer : ModPlayer
	{
		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Terrakeep.AbrirPanelKeybind != null && Terrakeep.AbrirPanelKeybind.JustPressed) {
				TerrakeepMod.Common.Panel.PanelTerrakeepSystem.AlternarArea(
					TerrakeepMod.Common.Panel.AreaTerrakeep.Personaje,
					"atajo de teclado (ModPlayer.ProcessTriggers)");
			}
		}
	}
}
