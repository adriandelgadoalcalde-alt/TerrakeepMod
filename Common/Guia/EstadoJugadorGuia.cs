using Terraria;
using Terraria.ID;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Lo que la guia necesita saber del jugador y del mundo, leido <b>en vivo</b> del juego en
	/// marcha. Ni un solo dato de aqui esta guardado ni cacheado: todos son campos reales del
	/// motor consultados en el momento.
	/// </summary>
	/// <remarks>
	/// Cada lectura cita el campo exacto del juego del que sale. No es adorno: la mitad de los
	/// umbrales del arbol de progresion son condiciones REALES del motor (la aparicion nocturna
	/// del Ojo de Cthulhu mira <c>ConsumedLifeCrystals</c> y <c>statDefense</c>, no "200 de vida"
	/// como suele contarse), y si la guia leyera una cosa distinta de la que mira el juego,
	/// diria que estas listo cuando no lo estas.
	/// </remarks>
	public static class EstadoJugadorGuia
	{
		/// <summary>Las 58 ranuras que recorre el propio motor cuando mira "lo que llevas encima"
		/// (mochila 0-49 + monedas 50-53 + municion 54-57). Es el mismo <c>for (int j = 0; j &lt;
		/// 58; j++)</c> que hay en <c>NPC.SpawnAllowed_Merchant</c> y compañia, copiado de ahi a
		/// proposito para que la guia cuente lo mismo que cuenta el juego.</summary>
		public const int RanurasQueMiraElJuego = 58;

		public static Player Jugador => Main.LocalPlayer;

		public static bool HayPartida =>
			!Main.gameMenu && Main.LocalPlayer != null && Main.LocalPlayer.active;

		/// <summary>
		/// Cristales de vida consumidos.
		/// </summary>
		/// <remarks>
		/// <b>Ojo con este campo, que engaña.</b> <c>Player.ConsumedLifeCrystals</c> NO es
		/// "tu vida máxima dividida entre 20": es un <b>contador guardado de verdad</b>
		/// (<c>consumedLifeCrystals</c>, con tope 15 en su setter) que solo sube cuando el jugador
		/// <b>usa</b> un Cristal de Vida (<c>Player.ItemCheck</c>, <c>sItem.type == 29</c>). La
		/// formula <c>(statLifeMax - 100) / 20</c> aparece una sola vez en todo el motor, al
		/// convertir un personaje de una version antigua que todavia no guardaba el contador.
		/// <para />
		/// Y es justo el que hay que leer, porque es el que mira el juego para decidir si el Ojo de
		/// Cthulhu aparece de noche. La diferencia no es teorica en ESTE mod: con Terrakeep se
		/// puede subir la vida maxima a mano, y eso <b>no</b> mueve el contador. Si la guia
		/// preguntara por <c>statLifeMax</c> diria "ya lo tienes" a alguien que va a pasarse las
		/// noches esperando a un jefe que no va a venir. Se comprobo en el juego real: poner
		/// <c>statLifeMax = 200</c> deja <c>ConsumedLifeCrystals</c> en 0.
		/// </remarks>
		public static int CristalesVida => HayPartida ? Jugador.ConsumedLifeCrystals : 0;

		public static int VidaMaxima => HayPartida ? Jugador.statLifeMax2 : 0;

		/// <summary>Defensa REAL de ahora mismo (<c>Player.statDefense</c>), que ya incluye la
		/// armadura, los accesorios y los buffs activos.</summary>
		public static int Defensa => HayPartida ? (int)Jugador.statDefense : 0;

		/// <summary>Cuantos NPC del pueblo hay vivos en el mundo. Mismo recuento que hace
		/// <c>Main.UpdateTime_StartNight</c> para decidir si el Ojo de Cthulhu puede aparecer
		/// solo: recorre <c>Main.npc[0..199]</c> y cuenta <c>active &amp;&amp; townNPC</c>.</summary>
		public static int NpcsDelPueblo()
		{
			int cuenta = 0;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc != null && npc.active && npc.townNPC) {
					cuenta++;
				}
			}
			return cuenta;
		}

		/// <summary>true si ese tipo de NPC del pueblo esta vivo en el mundo.</summary>
		public static bool HayNpc(int tipo)
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc != null && npc.active && npc.type == tipo) {
					return true;
				}
			}
			return false;
		}

		/// <summary>Cuantas unidades de ese objeto lleva encima el jugador.</summary>
		public static int CuantosLleva(int tipo)
		{
			if (!HayPartida || tipo <= 0) {
				return 0;
			}

			int total = 0;
			Item[] inventario = Jugador.inventory;
			int tope = System.Math.Min(RanurasQueMiraElJuego, inventario.Length);
			for (int i = 0; i < tope; i++) {
				Item objeto = inventario[i];
				if (objeto != null && !objeto.IsAir && objeto.type == tipo) {
					total += objeto.stack;
				}
			}
			return total;
		}

		/// <summary>
		/// Daño del mejor arma que se lleve encima, <b>ya pasado por el propio jugador</b>:
		/// <c>Player.GetWeaponDamage(Item)</c> es el metodo real del motor que aplica los
		/// multiplicadores de clase, la armadura y los accesorios. Usarlo en vez de
		/// <c>Item.damage</c> pelado es lo que hace que el numero coincida con el que el jugador
		/// ve en el tooltip del arma.
		/// <para />
		/// No cuentan como arma las herramientas que solo pican/talan sin daño util, ni los
		/// accesorios, ni la municion.
		/// </summary>
		public static int DanoDelMejorArma(out string nombre)
		{
			nombre = "";
			if (!HayPartida) {
				return 0;
			}

			int mejor = 0;
			Item[] inventario = Jugador.inventory;
			int tope = System.Math.Min(RanurasQueMiraElJuego, inventario.Length);
			for (int i = 0; i < tope; i++) {
				Item objeto = inventario[i];
				if (objeto == null || objeto.IsAir || objeto.damage <= 0) {
					continue;
				}
				if (objeto.accessory || objeto.ammo != AmmoID.None) {
					continue;
				}

				int dano = Jugador.GetWeaponDamage(objeto);
				if (dano > mejor) {
					mejor = dano;
					nombre = objeto.Name;
				}
			}
			return mejor;
		}

		/// <summary>
		/// true si lleva encima un gancho de verdad.
		/// <para />
		/// No es una lista de ids escrita a mano: se pregunta al motor. <c>Main.projHook</c> es el
		/// array de bools, indexado por tipo de proyectil, que el propio juego usa para saber si
		/// un proyectil es un gancho, y <c>Item.shoot</c> es el proyectil que dispara el objeto.
		/// Asi vale cualquier gancho, tambien los de mods, sin tocar nada.
		/// </summary>
		public static bool LlevaGancho(out string nombre)
		{
			nombre = "";
			if (!HayPartida) {
				return false;
			}

			Item[] inventario = Jugador.inventory;
			int tope = System.Math.Min(RanurasQueMiraElJuego, inventario.Length);
			for (int i = 0; i < tope; i++) {
				Item objeto = inventario[i];
				if (objeto == null || objeto.IsAir || objeto.shoot <= 0) {
					continue;
				}
				if (objeto.shoot < Main.projHook.Length && Main.projHook[objeto.shoot]) {
					nombre = objeto.Name;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// En que capa del mundo esta el jugador, con las fronteras REALES que usa el juego
		/// (<c>Main.worldSurface</c>, <c>Main.rockLayer</c>, <c>Main.UnderworldLayer</c>). Es lo
		/// que permite dar una direccion honesta ("estas en la superficie, esto esta mas abajo")
		/// sin dar ninguna coordenada.
		/// </summary>
		public static CapaMundo CapaDelJugador()
		{
			if (!HayPartida) {
				return CapaMundo.Cualquiera;
			}

			double y = Jugador.position.Y / 16.0;
			if (y >= Main.UnderworldLayer) {
				return CapaMundo.Infierno;
			}
			if (y >= Main.rockLayer) {
				return CapaMundo.Cavernas;
			}
			if (y >= Main.worldSurface) {
				return CapaMundo.Subterraneo;
			}
			return CapaMundo.Superficie;
		}
	}
}
