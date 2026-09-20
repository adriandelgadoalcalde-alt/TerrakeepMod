using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// <b>Idea 2 del catálogo de funciones ("DPS-metro y registro de combate en vivo"):</b> daño
	/// hecho por el jugador LOCAL en los últimos 10 segundos reales. Es justo el dato que
	/// <c>EstadoJugadorGuia</c> documenta como "el requisito que en escritorio queda sin datos
	/// porque no se puede medir sin partida" - aquí SÍ hay una partida real cargada.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por qué un <c>GlobalNPC</c> y no leer <c>NPC.life</c> antes/después</b>: <c>OnHitByItem</c>/
	/// <c>OnHitByProjectile</c> ya traen <c>damageDone</c>, el daño REAL tras defensa/resistencias -
	/// el mismo número que enseña el numerito flotante del propio juego - así que no hace falta
	/// reconstruirlo a mano ni arriesgarse a contar de más con un NPC que muere a mitad de golpe
	/// (vida ya en 0 antes de leerla). <c>OnHitByItem</c> "se llama en el cliente que hace el daño"
	/// (XMLdoc real de <c>GlobalNPC</c>), así que en cooperativo esto no cuenta el daño de nadie
	/// más - aun así se filtra explícitamente por <c>Main.LocalPlayer</c> por si acaso, nunca
	/// confiando solo en el comentario del motor.
	/// </para>
	/// <para>
	/// <b>Por qué <c>Main.GameUpdateCount</c> y no <c>DateTime.Now</c>/<c>GameTime</c></b>: es un
	/// contador REAL del motor que avanza un paso fijo por actualización lógica (60/s, el mismo
	/// paso con el que ya cuentan fotogramas el resto de temporizadores del mod, p.ej.
	/// <c>FotogramasEntreRefrescos</c> de <c>ContenidoBuilds</c>) - inmune a que el framerate de
	/// dibujado varíe, y sin la complicación de zonas horarias o de que el reloj del sistema cambie.
	/// </para>
	/// <para>
	/// <b>Es un singleton, a propósito.</b> No hay ningún campo por instancia (ni <c>InstancePerEntity</c>
	/// que pedirlo): el registro es GLOBAL a la partida, no por NPC, así que el comportamiento por
	/// defecto de <see cref="GlobalNPC"/> (una sola instancia compartida) es exactamente lo que hace
	/// falta.
	/// </para>
	/// </remarks>
	public class MedidorDanio : GlobalNPC
	{
		/// <summary>10 segundos reales a 60 fotogramas/s, el mismo paso fijo de <c>Main.GameUpdateCount</c>.</summary>
		private const uint VentanaFotogramas = 600;

		private static readonly Queue<(uint Fotograma, int Dano)> _golpes = new Queue<(uint, int)>();
		private static int _sumaVentana;

		public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
		{
			RegistrarSiEsElJugadorLocal(player, damageDone);
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (projectile == null || projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
				// Proyectil sin dueño jugador (de un NPC, de una trampa...): no es dano del jugador
				// local, nunca se cuenta.
				return;
			}
			RegistrarSiEsElJugadorLocal(Main.player[projectile.owner], damageDone);
		}

		private static void RegistrarSiEsElJugadorLocal(Player jugador, int dano)
		{
			if (jugador == null || dano <= 0 || !ReferenceEqualsLocal(jugador)) {
				return;
			}
			_golpes.Enqueue((Main.GameUpdateCount, dano));
			_sumaVentana += dano;
			Purgar();
		}

		/// <summary>
		/// Comparación explícita contra <c>Main.LocalPlayer</c> - nunca <c>player.whoAmI == Main.myPlayer</c>
		/// a secas, que en servidor dedicado (<c>Main.myPlayer</c> sin sentido real, <c>Main.LocalPlayer</c>
		/// sigue siendo la referencia correcta de "el jugador de ESTE cliente") daría un resultado
		/// distinto - el mismo criterio que ya usa el resto del mod para "el jugador de esta sesión".
		/// </summary>
		private static bool ReferenceEqualsLocal(Player jugador)
		{
			return ReferenceEquals(jugador, Main.LocalPlayer);
		}

		private static void Purgar()
		{
			uint ahora = Main.GameUpdateCount;
			while (_golpes.Count > 0 && ahora - _golpes.Peek().Fotograma > VentanaFotogramas) {
				_sumaVentana -= _golpes.Dequeue().Dano;
			}
		}

		/// <summary>Daño por segundo en los últimos 10 segundos reales (media sobre una ventana
		/// FIJA de 10s, no "desde el primer golpe"), o 0 si no ha habido ningún golpe en ese
		/// tiempo.</summary>
		public static float DpsUltimos10s()
		{
			Purgar();
			return _golpes.Count == 0 ? 0f : _sumaVentana / (VentanaFotogramas / 60f);
		}

		/// <summary>true si ha habido al menos un golpe dentro de la ventana de 10s - para que quien
		/// enseñe el número pueda distinguir "0 DPS de verdad" (golpeaste y no hiciste daño, rarísimo)
		/// de "sin datos, no has golpeado nada todavía".</summary>
		public static bool HayDatosRecientes()
		{
			Purgar();
			return _golpes.Count > 0;
		}

		/// <summary>SOLO PARA AUTOPRUEBAS: vacía el registro para empezar una medición desde
		/// cero sin depender de que el mundo se acabe de cargar.</summary>
		public static void ReiniciarParaPrueba()
		{
			_golpes.Clear();
			_sumaVentana = 0;
		}

		/// <summary>
		/// SOLO PARA AUTOPRUEBAS: registra un golpe sin pasar por <c>Main.LocalPlayer</c> ni por un
		/// <c>NPC</c> objetivo real - la propia autoprueba YA corre como el jugador local de la
		/// partida sintética, así que fabricar un combate real (spawnear un NPC, simular fotograma a
		/// fotograma el golpe de un arma) no ejercitaría nada que esto no ejercite ya: la cola, la
		/// ventana de 10s (<see cref="Purgar"/>) y la aritmética de <see cref="DpsUltimos10s"/> son
		/// exactamente el mismo código que corre desde <see cref="OnHitByItem"/>/
		/// <see cref="OnHitByProjectile"/>, solo que estos dos son pegamento de una línea cada uno
		/// (filtrar por el jugador local y pasar <c>damageDone</c> tal cual, sin transformarlo).
		/// </summary>
		public static void RegistrarGolpeParaPrueba(int dano)
		{
			_golpes.Enqueue((Main.GameUpdateCount, dano));
			_sumaVentana += dano;
			Purgar();
		}
	}
}
