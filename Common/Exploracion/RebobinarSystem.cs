using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Ata <see cref="EstadoRebobinar"/> al ciclo de vida real del MUNDO (no al del panel - ver el
	/// XMLdoc de <see cref="EstadoRebobinar"/> para el bug real que esto arregla).
	/// </summary>
	public class RebobinarSystem : ModSystem
	{
		private int _contador;

		public override void OnWorldLoad()
		{
			EstadoRebobinar.Limpiar();
		}

		public override void OnWorldUnload()
		{
			EstadoRebobinar.Limpiar();
		}

		/// <summary>
		/// Recalcula la diferencia real cada <see cref="EstadoRebobinar.FotogramasEntreComparaciones"/>
		/// fotogramas SIEMPRE que haya un mundo cargado - <c>PostUpdateEverything</c> corre en el
		/// bucle de simulación del mundo (confirmado: es donde ya cuelgan otros sistemas de este
		/// mismo mod que necesitan seguir vivos con el panel cerrado), no en el de la interfaz, así
		/// que nunca depende de qué pestaña tenga abierta el jugador ni de si el panel está abierto.
		/// </summary>
		public override void PostUpdateEverything()
		{
			if (Main.dedServ || Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}

			if (++_contador < EstadoRebobinar.FotogramasEntreComparaciones) {
				return;
			}
			_contador = 0;
			EstadoRebobinar.RecalcularDiferencia();
		}
	}
}
