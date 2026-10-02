using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terrakeep.Core.Guia;
using TerrakeepMod.Common.Guia;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Fuente de estado EN VIVO de la Guia v2 (<see cref="IGuideStateProviderV2"/>): lo mismo que
	/// la v1 (<see cref="ProveedorEstadoGuiaMod"/>, al que delega) mas inventario completo, huchas,
	/// equipo de los tres conjuntos, mejoras permanentes (vanilla y <c>CalamityPlayer</c>), estado
	/// de mundo de Calamity y el modo real de la partida.
	/// </summary>
	/// <remarks>
	/// Todo se lee del juego en marcha (Main.LocalPlayer, NPC.downed*, CalamityWorld por
	/// reflexion): no hay ningun archivo de por medio, a diferencia del escritorio, que lee .plr,
	/// .wld, .tplr y .twld. Por eso aqui nunca hay "sin datos" con partida cargada.
	/// </remarks>
	public sealed class ProveedorEstadoGuiaV2Mod : IGuideStateProviderV2
	{
		private readonly ProveedorEstadoGuiaMod _v1 = new ProveedorEstadoGuiaMod();

		/// <summary>Mejoras permanentes vanilla → campo real de <c>Player</c> (Player.cs 1.4.4.9).</summary>
		private static readonly Dictionary<string, System.Func<Player, bool>> MejorasVanilla =
			new Dictionary<string, System.Func<Player, bool>> {
				{ "demonHeart", p => p.extraAccessory },
				{ "torchGod", p => p.unlockedBiomeTorches },
				{ "artisanBread", p => p.ateArtisanBread },
				{ "aegisCrystal", p => p.usedAegisCrystal },
				{ "aegisFruit", p => p.usedAegisFruit },
				{ "arcaneCrystal", p => p.usedArcaneCrystal },
				{ "galaxyPearl", p => p.usedGalaxyPearl },
				{ "gummyWorm", p => p.usedGummyWorm },
				{ "ambrosia", p => p.usedAmbrosia },
			};

		private static bool HayPartida => EstadoJugadorGuia.HayPartida;
		private static Player Jugador => Main.LocalPlayer;

		// ---- v1, delegado ----------------------------------------------------------------------
		public bool HasCharacterData => _v1.HasCharacterData;
		public bool HasWorldData => _v1.HasWorldData;
		public bool HasInventoryData => _v1.HasInventoryData;
		public bool HasLiveGameData => _v1.HasLiveGameData;
		public int CristalesVida => _v1.CristalesVida;
		public int VidaMaxima => _v1.VidaMaxima;
		public int Defensa => _v1.Defensa;
		public int NpcsDelPueblo() => _v1.NpcsDelPueblo();
		public int CuantosLleva(int id) => _v1.CuantosLleva(id);
		public int DanoDelMejorArma(out string nombre) => _v1.DanoDelMejorArma(out nombre);
		public bool BanderaConocida(string bandera) => _v1.BanderaConocida(bandera);
		public bool? ValorBandera(string bandera) => _v1.ValorBandera(bandera);
		public string MotivoBanderaDesconocida(string bandera) => _v1.MotivoBanderaDesconocida(bandera);
		public string NombreDeObjeto(int tipo) => _v1.NombreDeObjeto(tipo);
		public string NombreDeNpc(int tipo) => _v1.NombreDeNpc(tipo);
		public string MotivoSinPartidaEnMarcha(TipoRequisitoGuia tipo) => _v1.MotivoSinPartidaEnMarcha(tipo);

		/// <summary>NPC presente en el mundo. Los NPC vanilla de la tabla pueden venir con id NEGATIVO
		/// (variantes por netID reales, NPCID.cs): se compara tambien contra <c>netID</c>.</summary>
		public bool HayNpc(int id)
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc != null && npc.active && (npc.type == id || npc.netID == id)) {
					return true;
				}
			}
			return false;
		}

		/// <summary>Gancho en la mochila (v1) o puesto en su ranura de equipo (miscEquips[4]).</summary>
		public bool LlevaGancho(out string nombre)
		{
			if (_v1.LlevaGancho(out nombre)) {
				return true;
			}
			if (!HayPartida) {
				return false;
			}
			Item gancho = Jugador.miscEquips[4];
			if (gancho != null && !gancho.IsAir && gancho.shoot > 0 && gancho.shoot < Main.projHook.Length && Main.projHook[gancho.shoot]) {
				nombre = gancho.Name;
				return true;
			}
			return false;
		}

		// ---- v2 ---------------------------------------------------------------------------------
		public int CuantosPosee(int id) => CuantosPoseeDe(Jugador, id);

		/// <summary>Unidades del objeto en CUALQUIER sitio del personaje: inventario (incluidas
		/// monedas y municion), raton, equipo activo, los otros conjuntos de equipo, equipo variado
		/// (mascota, gancho, montura...) y las cuatro huchas (hucha, caja fuerte, forja defensiva y
		/// bolsa del vacio).</summary>
		public static int CuantosPoseeDe(Player p, int id)
		{
			if (p == null || !p.active || id <= 0 || Main.gameMenu) {
				return 0;
			}
			int total = Contar(p.inventory, id) + Contar(p.armor, id) + Contar(p.miscEquips, id);
			if (p.whoAmI == Main.myPlayer && Main.mouseItem != null && !Main.mouseItem.IsAir && Main.mouseItem.type == id) {
				total += Main.mouseItem.stack;
			}
			if (p.Loadouts != null) {
				for (int i = 0; i < p.Loadouts.Length; i++) {
					// El conjunto ACTIVO vive en player.armor; su copia en Loadouts esta desfasada.
					if (i != p.CurrentLoadoutIndex && p.Loadouts[i] != null) {
						total += Contar(p.Loadouts[i].Armor, id);
					}
				}
			}
			total += Contar(p.bank != null ? p.bank.item : null, id);
			total += Contar(p.bank2 != null ? p.bank2.item : null, id);
			total += Contar(p.bank3 != null ? p.bank3.item : null, id);
			total += Contar(p.bank4 != null ? p.bank4.item : null, id);
			return total;
		}

		private static int Contar(Item[] items, int id)
		{
			if (items == null) {
				return 0;
			}
			int total = 0;
			for (int i = 0; i < items.Length; i++) {
				Item it = items[i];
				if (it != null && !it.IsAir && it.type == id) {
					total += it.stack;
				}
			}
			return total;
		}

		/// <summary>Puesto en el conjunto activo: armadura (0-2) y accesorios funcionales (3-9).</summary>
		public bool LlevaEquipado(int id)
		{
			if (!HayPartida || id <= 0) {
				return false;
			}
			for (int i = 0; i < 10 && i < Jugador.armor.Length; i++) {
				Item it = Jugador.armor[i];
				if (it != null && !it.IsAir && it.type == id) {
					return true;
				}
			}
			return false;
		}

		public bool MejoraConocida(string clave) =>
			clave != null && (MejorasVanilla.ContainsKey(clave) || ReflexionCalamity.MejoraConocida(clave));

		public bool? MejoraPermanente(string clave)
		{
			if (!HayPartida || clave == null) {
				return null;
			}
			System.Func<Player, bool> leer;
			if (MejorasVanilla.TryGetValue(clave, out leer)) {
				return leer(Jugador);
			}
			return ReflexionCalamity.Mejora(Jugador, clave);
		}

		public bool EstadoMundoConocido(string clave) =>
			clave == "mundoCarmesi" || ReflexionCalamity.EstadoMundoConocido(clave);

		public bool? EstadoMundo(string clave)
		{
			if (!HayPartida || clave == null) {
				return null;
			}
			if (clave == "mundoCarmesi") {
				return WorldGen.crimson;
			}
			return ReflexionCalamity.EstadoMundo(clave);
		}

		public int FrutasVida => HayPartida ? Jugador.ConsumedLifeFruit : 0;

		public int ManaMaxima => HayPartida ? Jugador.statManaMax : 0;

		public ModoPartida Modo {
			get {
				if (!HayPartida) {
					return null;
				}
				bool? rev = ReflexionCalamity.Cargado ? ReflexionCalamity.EstadoMundo("revenge") : null;
				bool? death = ReflexionCalamity.Cargado ? ReflexionCalamity.EstadoMundo("death") : null;
				return new ModoPartida(Main.GameMode, rev, death);
			}
		}
	}
}
