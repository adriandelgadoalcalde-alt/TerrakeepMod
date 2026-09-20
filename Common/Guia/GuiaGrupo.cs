using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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

		// -------------------------------------------------------------------------------------
		// Idea 9 (corregida el 20-sep-2026, segunda pasada): "repartir builds por clase sin
		// solaparse" - la pieza que faltaba del catalogo. Las CINCO claves son las mismas de
		// CatalogoBuilds (melee/ranged/mage/summoner/rogue, esta ultima solo con Calamity), no una
		// lista inventada aparte - EtiquetaClase ya las traduce.
		// -------------------------------------------------------------------------------------

		/// <summary>Las claves de clase reales, en el orden en que se ofrecen al repartir. "rogue"
		/// se filtra fuera si Calamity no esta cargado (mismo criterio que ya usa CatalogoBuilds
		/// para no ofrecer una clase que la partida no tiene).</summary>
		public static IReadOnlyList<string> ClavesClase()
		{
			List<string> claves = new List<string> { "melee", "ranged", "mage", "summoner" };
			if (CatalogoGuia.HayCalamity) {
				claves.Add("rogue");
			}
			return claves;
		}

		/// <summary>
		/// La clave de clase real del ARMA que el jugador lleva en la mano ahora mismo - mismo
		/// criterio que <see cref="EstadoJugadorGuia.DanoDelMejorArma"/> (el arma de mas daño real,
		/// sin herramientas/accesorios/municion), pero mirando de que <c>DamageClass</c> es en vez
		/// de cuanto daño hace. Null si no lleva ningun arma real encima o si su clase no es
		/// ninguna de las cinco que CatalogoBuilds conoce (p.ej. herramientas con daño, o una clase
		/// de un mod que el catalogo de builds no cubre).
		/// </summary>
		public static string ClaveClaseDetectada(Player jugador, out string nombreArma)
		{
			nombreArma = "";
			if (jugador == null || !jugador.active) {
				return null;
			}

			int mejorDano = 0;
			string mejorClave = null;
			string mejorNombre = "";
			Item[] inventario = jugador.inventory;
			int tope = System.Math.Min(58, inventario.Length);
			for (int i = 0; i < tope; i++) {
				Item objeto = inventario[i];
				if (objeto == null || objeto.IsAir || objeto.damage <= 0) {
					continue;
				}
				if (objeto.accessory || objeto.ammo != AmmoID.None) {
					continue;
				}
				string clave = ClaveDeDamageClass(objeto);
				if (clave == null) {
					continue;
				}
				int dano = jugador.GetWeaponDamage(objeto);
				if (dano > mejorDano) {
					mejorDano = dano;
					mejorClave = clave;
					mejorNombre = objeto.Name;
				}
			}
			nombreArma = mejorNombre;
			return mejorClave;
		}

		/// <summary>
		/// Traduce el objeto a una de las cinco claves de <see cref="ClavesClase"/>.
		/// </summary>
		/// <remarks>
		/// <b>Bug real encontrado con la propia autoprueba</b> (diagnostico en vivo: una Espada
		/// Corta de Cobre real salia con <c>DamageType=MeleeNoSpeedDamageClass</c>, no
		/// <c>MeleeDamageClass</c>): comparar <c>objeto.DamageType == DamageClass.Melee</c> con
		/// igualdad EXACTA fallaba para cualquier variante real de la misma familia (hay varias
		/// clases "de cuerpo a cuerpo" reales en vanilla: <c>Melee</c>, <c>MeleeNoSpeed</c>,
		/// <c>SummonMeleeSpeed</c>...). El API publico real y correcto para "esto cuenta como esa
		/// clase" es <c>Item.CountsAsClass(DamageClass)</c> (confirmado en el decompilado,
		/// <c>Item.cs:52047</c>) - el mismo metodo que usa el propio <c>Item.melee</c> interno del
		/// motor. Rogue no tiene equivalente vanilla - se identifica por el NOMBRE real de su tipo
		/// (<c>CalamityMod.RogueDamageClass</c>, confirmado en el decompilado de Calamity), ya que
		/// este proyecto no referencia el ensamblado de Calamity para poder compilar sin el.
		/// </remarks>
		private static string ClaveDeDamageClass(Item objeto)
		{
			if (objeto.CountsAsClass(DamageClass.Melee)) {
				return "melee";
			}
			if (objeto.CountsAsClass(DamageClass.Ranged)) {
				return "ranged";
			}
			if (objeto.CountsAsClass(DamageClass.Magic)) {
				return "mage";
			}
			if (objeto.CountsAsClass(DamageClass.Summon)) {
				return "summoner";
			}
			if (CatalogoGuia.HayCalamity && objeto.DamageType != null &&
				objeto.DamageType.GetType().Name == "RogueDamageClass") {
				return "rogue";
			}
			return null;
		}

		/// <summary>Un miembro del grupo (incluido el jugador LOCAL - "el grupo" es todo el mundo
		/// conectado, no solo los companeros) con su clase real detectada y, si hace falta, una
		/// clase SUGERIDA para que nadie repita.</summary>
		public struct AsignacionClase
		{
			/// <summary>-1 para el jugador local, si no el indice real en <c>Main.player</c>.</summary>
			public int Indice;
			public string Nombre;
			/// <summary>Clave real detectada por el arma en la mano ahora mismo, o null si no
			/// lleva ninguna reconocible.</summary>
			public string ClaveDetectada;
			public string NombreArma;
			/// <summary>true si otro miembro del grupo tiene detectada la MISMA clave - el
			/// solapamiento real que la idea 9 pide evitar.</summary>
			public bool Solapa;
			/// <summary>Clave que se sugiere para que este miembro no repita con nadie - igual a
			/// <see cref="ClaveDetectada"/> si ya es unica en el grupo, o la primera clase libre si
			/// no. Null solo si hay mas miembros que clases disponibles (no hay ninguna libre que
			/// sugerir de verdad).</summary>
			public string ClaveSugerida;
		}

		/// <summary>
		/// El reparto real de clases del grupo ENTERO (jugador local + companeros conectados),
		/// para que ninguna clase quede cubierta por mas de uno "sin solaparse" (la pieza exacta
		/// que pedia el catalogo). Vacio en partida de un jugador - no hay nadie con quien repartir.
		/// </summary>
		/// <remarks>
		/// Reparto DETERMINISTA en dos pasadas, sin adivinar nada: primero cada miembro se queda
		/// con su clase detectada si todavia esta libre (por orden: jugador local, luego
		/// companeros por indice); los que no tienen clase detectada, o cuya clase detectada ya se
		/// la quedo otro antes, reciben la primera clase de <see cref="ClavesClase"/> que siga
		/// libre - esa regla es la misma que aplicaria una persona repartiendo roles a mano.
		/// </remarks>
		public static List<AsignacionClase> RepartoClases()
		{
			List<AsignacionClase> miembros = new List<AsignacionClase>();
			if (Main.netMode == NetmodeID.SinglePlayer) {
				return miembros;
			}

			string nombreArmaLocal;
			miembros.Add(new AsignacionClase {
				Indice = -1,
				Nombre = Main.LocalPlayer != null ? Main.LocalPlayer.name : "",
				ClaveDetectada = ClaveClaseDetectada(Main.LocalPlayer, out nombreArmaLocal),
				NombreArma = nombreArmaLocal
			});
			foreach (int indice in IndicesConectados()) {
				string nombreArma;
				Player jugador = Main.player[indice];
				miembros.Add(new AsignacionClase {
					Indice = indice,
					Nombre = jugador != null ? jugador.name : "",
					ClaveDetectada = ClaveClaseDetectada(jugador, out nombreArma),
					NombreArma = nombreArma
				});
			}

			IReadOnlyList<string> claves = ClavesClase();
			HashSet<string> ocupadas = new HashSet<string>();

			// Primera pasada: cada uno se queda con su clave detectada SI todavia esta libre.
			for (int m = 0; m < miembros.Count; m++) {
				AsignacionClase actual = miembros[m];
				if (actual.ClaveDetectada != null && !ocupadas.Contains(actual.ClaveDetectada)) {
					ocupadas.Add(actual.ClaveDetectada);
					actual.ClaveSugerida = actual.ClaveDetectada;
					actual.Solapa = false;
					miembros[m] = actual;
				}
			}

			// Segunda pasada: al resto (sin clase detectada, o repetida) se le sugiere la primera
			// clase que siga libre.
			for (int m = 0; m < miembros.Count; m++) {
				AsignacionClase actual = miembros[m];
				if (actual.ClaveSugerida != null) {
					continue;
				}
				actual.Solapa = actual.ClaveDetectada != null;
				string libre = null;
				for (int c = 0; c < claves.Count; c++) {
					if (!ocupadas.Contains(claves[c])) {
						libre = claves[c];
						break;
					}
				}
				if (libre != null) {
					ocupadas.Add(libre);
					actual.ClaveSugerida = libre;
				}
				miembros[m] = actual;
			}

			return miembros;
		}
	}
}
