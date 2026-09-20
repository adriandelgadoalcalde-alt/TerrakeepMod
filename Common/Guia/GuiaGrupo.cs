using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Idea 9 del catalogo de funciones: "guia de grupo multijugador" - que le falta a CADA
	/// companero conectado para el paso de ahora mismo, no solo al jugador local.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Corregido el 20-sep-2026.</b> Una primera pasada de esta misma noche habia dado esto por
	/// LIMITE REAL ("la mochila de otro jugador no es visible en general"), con una investigacion
	/// incompleta: solo se habia encontrado UN sitio real que dispara <c>NetMessage.SendData(5, ...)</c>
	/// (el "quick stack" a un cofre), y se asumio que era el unico. Al reinvestigar con la pista
	/// concreta de mirar el protocolo en vez de asumir que hacen falta dos ventanas graficas, el
	/// decompilado real ensena bastante mas:
	/// </para>
	/// <list type="bullet">
	/// <item><c>Terraria.Main.TrySyncingMyPlayer()</c> (<c>Main.cs</c>, llamada cada
	/// actualizacion de red desde <c>Main.DoUpdate</c>) compara las 59 ranuras del inventario del
	/// jugador local contra un snapshot (<c>IsNetStateDifferent</c>) y reenvia cualquier ranura que
	/// cambie via <c>NetMessage.SendData(5, ...)</c> - no es un envio puntual, es continuo.</item>
	/// <item>El servidor, al recibir ese mensaje, solo lo RETRANSMITE a los demas clientes si
	/// <c>Terraria.ID.PlayerItemSlotID.CanRelay[ranura]</c> es true para esa ranura concreta
	/// (<c>MessageBuffer.cs</c>, case 5: <c>if (canRelay[num27]) NetMessage.TrySendData(5, -1,
	/// whoAmI, ...)</c>).</item>
	/// <item>Y <c>PlayerItemSlotID.cs</c> dice, campo a campo, que <c>Inventory0</c> (las 58
	/// ranuras de la mochila principal) SI tiene <c>canNetRelay: true</c> - igual que armadura,
	/// tintes, accesorios misceláneos y las tres loadouts. Lo UNICO que no se retransmite es el
	/// cofre de cerdito (Bank1), la caja fuerte (Bank2), la forja del defensor (Bank3) y el objeto
	/// en la papelera.</item>
	/// </list>
	/// <para>
	/// En cristiano: en multijugador real, la mochila principal de CADA jugador conectado SI llega
	/// sincronizada de verdad a <c>Main.player[i].inventory[]</c> en todos los demas clientes, todo
	/// el rato, sin que nadie tenga que abrir nada - vanilla simplemente no dibuja ninguna
	/// interfaz para leerla, que es un limite de INTERFAZ, no de DATOS. Por eso esta guia de grupo
	/// SI puede evaluar requisitos de objeto/material de un companero, y no solo lo que se ve
	/// puesto encima (defensa, arma en la mano, vida/mana maximos).
	/// </para>
	/// </remarks>
	public static class GuiaGrupo
	{
		/// <summary>Companeros conectados de verdad ahora mismo: activos, distintos del jugador
		/// local. Vacio siempre en una partida de un jugador.</summary>
		public static List<int> IndicesConectados()
		{
			List<int> lista = new List<int>();
			if (Main.netMode == NetmodeID.SinglePlayer) {
				return lista;
			}
			for (int i = 0; i < Main.maxPlayers; i++) {
				if (i == Main.myPlayer) {
					continue;
				}
				Player jugador = Main.player[i];
				if (jugador != null && jugador.active) {
					lista.Add(i);
				}
			}
			return lista;
		}

		/// <summary>
		/// true si este tipo de requisito tiene sentido evaluado POR JUGADOR. Cristales de vida,
		/// vida maxima, defensa, objeto/objeto-cualquiera, dano de arma y gancho SI dependen de
		/// cada jugador. NpcsPueblo/Npc/NpcActivo/Bandera son estado del MUNDO (igual para todo el
		/// grupo, ya se muestran una vez en "Que te falta" del jugador local) - repetirlos por cada
		/// companero no anadiria ningun dato nuevo, solo ruido.
		/// </summary>
		public static bool EsEvaluablePorJugador(TipoRequisito tipo)
		{
			switch (tipo) {
				case TipoRequisito.CristalesVida:
				case TipoRequisito.VidaMaxima:
				case TipoRequisito.Defensa:
				case TipoRequisito.Objeto:
				case TipoRequisito.ObjetoCualquiera:
				case TipoRequisito.DanoArma:
				case TipoRequisito.Gancho:
					return true;
				default:
					return false;
			}
		}
	}
}
