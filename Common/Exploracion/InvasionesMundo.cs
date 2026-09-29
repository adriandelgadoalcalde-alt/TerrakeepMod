using Terraria;
using Terraria.ID;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Undo;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Paridad con Terrakeep escritorio 3.3.0 (commit <c>cc1d4ddc</c>, idea 1 del catalogo de
	/// funciones: "banderas de invasion Goblinos/Legion Helada/Piratas editables"): marcar o
	/// desmarcar como VENCIDA cada una de las tres invasiones clasicas del mundo cargado, en vivo.
	/// </summary>
	/// <remarks>
	/// Se porta el comportamiento, no el codigo: escritorio parchea los tres booleanos del bloque de
	/// banderas de la cabecera del <c>.wld</c> (<c>DownedGoblinArmy</c>/<c>DownedFrostLegion</c>/
	/// <c>DownedPirates</c>). Dentro del juego son los campos estaticos REALES que el motor escribe
	/// al guardar ese mismo bloque (<c>WorldFile.SaveWorld_Version2</c>, decompilado:
	/// <c>NPC.downedGoblins</c>, <c>NPC.downedFrost</c> -ojo: no "downedFrostLegion"- y
	/// <c>NPC.downedPirates</c>), y los mismos que ya lee la Guia (<c>BanderasGuia</c>), asi que el
	/// cambio se ve al instante en la Guia y queda grabado en el mundo en el siguiente guardado -
	/// el mismo aviso de permanencia que ya enseña la ficha para la dificultad.
	/// <para />
	/// Mismo limite honesto que el resto de ediciones de mundo del mod
	/// (<c>SelectorCofreMundoTk</c>, <c>RebobinarSystem</c>): solo en partida de UN jugador. En un
	/// cliente multijugador estos campos los decide el servidor y un cambio local no se sincroniza.
	/// </remarks>
	public static class InvasionesMundo
	{
		public enum Invasion
		{
			Goblins,
			LegionEscarcha,
			Piratas
		}

		/// <summary>Las tres, en el mismo orden que la casilla de escritorio.</summary>
		public static readonly Invasion[] Todas = { Invasion.Goblins, Invasion.LegionEscarcha, Invasion.Piratas };

		/// <summary>Valor REAL ahora mismo del campo del motor.</summary>
		public static bool Vencida(Invasion invasion)
		{
			switch (invasion) {
				case Invasion.Goblins:
					return NPC.downedGoblins;
				case Invasion.LegionEscarcha:
					return NPC.downedFrost;
				default:
					return NPC.downedPirates;
			}
		}

		private static void Escribir(Invasion invasion, bool valor)
		{
			switch (invasion) {
				case Invasion.Goblins:
					NPC.downedGoblins = valor;
					break;
				case Invasion.LegionEscarcha:
					NPC.downedFrost = valor;
					break;
				default:
					NPC.downedPirates = valor;
					break;
			}
		}

		/// <summary>Nombre oficial completo (el del Bestiario/logros), resuelto por clave para que
		/// siga al idioma en vivo.</summary>
		public static string Nombre(Invasion invasion)
		{
			return Idiomas.Texto("Exploracion.Mundo.Invasiones.Nombre." + invasion);
		}

		/// <summary>Rotulo corto para el boton de la ficha (el nombre completo va en el tooltip).</summary>
		public static string NombreCorto(Invasion invasion)
		{
			return Idiomas.Texto("Exploracion.Mundo.Invasiones.Corto." + invasion);
		}

		/// <summary>Por que no se puede tocar ahora mismo, o null si se puede.</summary>
		public static string MotivoParaNoPoder()
		{
			if (!MundoActual.HayMundo) {
				return Idiomas.Texto("Exploracion.Dificultad.SinMundo");
			}
			if (Main.netMode != NetmodeID.SinglePlayer) {
				return Idiomas.Texto("Exploracion.Mundo.Invasiones.SoloUnJugador");
			}
			return null;
		}

		/// <summary>
		/// Invierte la bandera de <paramref name="invasion"/> y la deja en el historial de Terrakeep
		/// (Ctrl+Z la devuelve a como estaba), igual que la dificultad. Devuelve false, sin tocar
		/// nada, si <see cref="MotivoParaNoPoder"/> dice que no se puede.
		/// </summary>
		public static bool Alternar(Invasion invasion, string origen)
		{
			string motivo = MotivoParaNoPoder();
			if (motivo != null) {
				RegistroExploracion.Aviso(Terrakeep.LogTag + " Invasiones: se rechaza cambiar " + invasion + ". " + motivo);
				return false;
			}

			bool antes = Vencida(invasion);
			bool despues = !antes;
			Escribir(invasion, despues);

			Invasion cerrada = invasion;
			Historial.CambiarValor(
				Idiomas.Texto(despues ? "Exploracion.Mundo.Invasiones.HistorialVencida" : "Exploracion.Mundo.Invasiones.HistorialPendiente",
					Nombre(invasion)),
				antes, despues, (bool valor) => Escribir(cerrada, valor));

			RegistroExploracion.Linea(Terrakeep.LogTag + " Invasiones: " + invasion + " " + antes + " -> " + despues +
				" via " + origen + " (NPC.downedGoblins=" + NPC.downedGoblins + ", NPC.downedFrost=" + NPC.downedFrost +
				", NPC.downedPirates=" + NPC.downedPirates + ").");
			return true;
		}
	}
}
