using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>Daño hecho al jefe de la práctica activa (idea 1) - mismos dos ganchos reales que
	/// ya usa <see cref="MedidorDanio"/> (<c>OnHitByItem</c>/<c>OnHitByProjectile</c>), aparte a
	/// propósito: <see cref="MedidorDanio"/> es un contador GENÉRICO de ventana móvil de 10s para
	/// el chip de cabecera, esto necesita el TOTAL exacto de un intento concreto, con su propio
	/// inicio y final.</summary>
	public class EntrenadorJefeGlobalNPC : GlobalNPC
	{
		public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
		{
			if (ReferenceEquals(player, Main.LocalPlayer)) {
				EntrenadorJefe.RegistrarDanoAlJefe(npc.whoAmI, damageDone);
			}
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (projectile != null && projectile.owner >= 0 && projectile.owner < Main.maxPlayers &&
				ReferenceEquals(Main.player[projectile.owner], Main.LocalPlayer)) {
				EntrenadorJefe.RegistrarDanoAlJefe(npc.whoAmI, damageDone);
			}
		}
	}

	/// <summary>Daño recibido por el jugador local mientras la práctica está activa (idea 1).</summary>
	public class EntrenadorJefeModPlayer : ModPlayer
	{
		public override void PostHurt(Player.HurtInfo info)
		{
			if (ReferenceEquals(Player, Main.LocalPlayer)) {
				EntrenadorJefe.RegistrarDanoRecibido(info.Damage);
			}
		}
	}
}
