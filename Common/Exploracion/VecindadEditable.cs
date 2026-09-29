using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terrakeep.Core.Data;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Undo;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Paridad con Terrakeep escritorio 3.3.0 (commit <c>ec916e8b</c>, idea 1 del catalogo de
	/// funciones: "añadir/quitar NPCs de pueblo del mundo, ya no solo lectura"): traer un vecino de
	/// la lista oficial que todavia no vive en el mundo, o echar a uno que si vive, EN VIVO.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Mismo criterio que escritorio, adaptado al motor.</b> Escritorio empalma la seccion de NPC
	/// del <c>.wld</c> y deja al recien llegado en el punto de aparicion del mundo, SIN casa
	/// (<c>Homeless=true</c>), "igual que un NPC real que se acaba de mudar". Aqui es literalmente
	/// eso: <c>NPC.NewNPC</c> en <c>Main.spawnTileX/Y</c> con <c>homeless = true</c> - el mismo
	/// estado en el que el propio juego deja a un vecino recien llegado o desalojado
	/// (<c>WorldGen.kickOut</c>/<c>moveRoom</c>, decompilado), asi que el motor le busca casa solo
	/// con su rutina normal (<c>WorldGen.SpawnTownNPC</c>) en cuanto haya una libre.
	/// </para>
	/// <para>
	/// <b>Echar</b> retira al NPC del mundo sin matarlo (<c>active = false</c>): ni botin, ni
	/// mensaje de muerte, ni tumba - lo mismo que quitar su entrada de la lista en escritorio. Si el
	/// jugador sigue cumpliendo sus condiciones de llegada, el propio juego puede volver a mudarlo mas
	/// adelante; eso es comportamiento vanilla, no un fallo.
	/// </para>
	/// <para>
	/// <b>Deshacer.</b> Las dos acciones quedan en el historial de Terrakeep (Ctrl+Z). Deshacer
	/// "echar" lo devuelve con su MISMO nombre propio y en el sitio donde estaba, pero sin casa
	/// asignada (la casa la reasigna el motor) - no se inventa un estado que el juego no guarda.
	/// </para>
	/// <para>
	/// Solo en partida de UN jugador, igual que los cofres del mundo y el rebobinado: en un cliente
	/// multijugador los NPC los decide el servidor y un cambio local no se sincroniza.
	/// </para>
	/// </remarks>
	public static class VecindadEditable
	{
		/// <summary>La lista oficial de vecinos (40 tipos), compartida con escritorio.</summary>
		public static IReadOnlyList<int> Roster => VanillaTownNpcRoster.Ids;

		/// <summary>Por que no se puede traer/echar ahora mismo, o null si se puede.</summary>
		public static string MotivoParaNoPoder()
		{
			if (!MundoActual.HayMundo) {
				return Idiomas.Texto("Exploracion.Dificultad.SinMundo");
			}
			if (Main.netMode != NetmodeID.SinglePlayer) {
				return Idiomas.Texto("Exploracion.Vecindad.SoloUnJugador");
			}
			return null;
		}

		/// <summary>Tipos de la lista oficial que no tienen ahora mismo ningun NPC activo en el
		/// mundo, en el orden de la lista.</summary>
		public static List<int> Faltan()
		{
			var presentes = new List<int>();
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc != null && npc.active) {
					presentes.Add(npc.type);
				}
			}
			return VecinosQueFaltan.Calcular(Roster, presentes);
		}

		/// <summary>Nombre del TIPO de NPC ("Guía", "Mercader"...), en el idioma del juego.</summary>
		public static string NombreDeTipo(int tipo)
		{
			return Lang.GetNPCNameValue(tipo);
		}

		/// <summary>Trae un vecino de <paramref name="tipo"/> al punto de aparicion, sin casa. Queda en
		/// el historial. Devuelve false sin tocar nada si no se puede.</summary>
		public static bool Traer(int tipo, string origen)
		{
			string motivo = MotivoParaNoPoder();
			if (motivo != null || NPC.AnyNPCs(tipo)) {
				RegistroExploracion.Aviso(Terrakeep.LogTag + " Vecindad: no se trae el tipo " + tipo + ". " +
					(motivo ?? "ya hay uno en el mundo"));
				return false;
			}

			Vector2 posicion = new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f);
			if (!Aparecer(tipo, posicion, null)) {
				return false;
			}

			int cerrado = tipo;
			Historial.CambiarValor(
				Idiomas.Texto("Exploracion.Vecindad.HistorialTraer", NombreDeTipo(tipo)),
				false, true, (bool presente) => {
					if (presente) {
						Aparecer(cerrado, posicion, null);
					} else {
						Retirar(cerrado);
					}
				});

			RegistroExploracion.Linea(Terrakeep.LogTag + " Vecindad: traido " + NombreDeTipo(tipo) + " (tipo " + tipo +
				") al punto de aparicion (" + Main.spawnTileX + ", " + Main.spawnTileY + "), sin casa, via " + origen + ".");
			return true;
		}

		/// <summary>Retira del mundo al NPC <paramref name="npc"/> (sin matarlo). Queda en el
		/// historial. Devuelve false sin tocar nada si no se puede.</summary>
		public static bool Echar(NPC npc, string origen)
		{
			string motivo = MotivoParaNoPoder();
			if (motivo != null || npc == null || !npc.active) {
				RegistroExploracion.Aviso(Terrakeep.LogTag + " Vecindad: no se echa al NPC. " + (motivo ?? "ya no esta activo"));
				return false;
			}

			int tipo = npc.type;
			string nombrePropio = npc.GivenName;
			Vector2 posicion = npc.position;
			string etiqueta = npc.FullName;

			npc.active = false;

			Historial.CambiarValor(
				Idiomas.Texto("Exploracion.Vecindad.HistorialEchar", etiqueta),
				true, false, (bool presente) => {
					if (presente) {
						Aparecer(tipo, posicion, nombrePropio);
					} else {
						Retirar(tipo);
					}
				});

			RegistroExploracion.Linea(Terrakeep.LogTag + " Vecindad: echado " + etiqueta + " (tipo " + tipo + ") via " + origen + ".");
			return true;
		}

		private static bool Aparecer(int tipo, Vector2 posicion, string nombrePropio)
		{
			if (NPC.AnyNPCs(tipo)) {
				return true;
			}
			int indice = NPC.NewNPC(new EntitySource_SpawnNPC("Terrakeep: Exploración > Vecindad"),
				(int)posicion.X, (int)posicion.Y, tipo);
			if (indice < 0 || indice >= Main.maxNPCs) {
				RegistroExploracion.Aviso(Terrakeep.LogTag + " Vecindad: NPC.NewNPC no encontro hueco para el tipo " + tipo + ".");
				return false;
			}
			NPC nuevo = Main.npc[indice];
			nuevo.homeless = true;
			if (!string.IsNullOrEmpty(nombrePropio)) {
				nuevo.GivenName = nombrePropio;
			}
			return true;
		}

		private static void Retirar(int tipo)
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc != null && npc.active && npc.type == tipo) {
					npc.active = false;
					return;
				}
			}
		}
	}
}
