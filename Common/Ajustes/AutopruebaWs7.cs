using Terraria;
using Terraria.ID;
using TerrakeepMod.Common.Undo;

namespace TerrakeepMod.Common.Ajustes
{
	/// <summary>
	/// Autoprueba de WS7: ejecuta en el juego REAL, sin que nadie pulse nada, los dos entregables
	/// del workstream, y deja en <c>tModLoader-Logs\client.log</c> (prefijo <c>[Terrakeep]</c>)
	/// la evidencia de cada paso.
	/// <para />
	/// Se dispara con la variable de entorno <see cref="AjustesSystem.VariableAutoprueba"/>, que
	/// pone <c>scripts\verificar-ws7.ps1</c>. Sin ella, este codigo no se ejecuta nunca.
	/// <para />
	/// Toca el inventario del personaje de PRUEBA del sandbox
	/// (<c>tModLoader-TerrakeepWS0\</c>), nunca uno real del usuario, y deja el inventario tal y
	/// como lo encontro al terminar.
	/// </summary>
	public static class AutopruebaWs7
	{
		// Ranuras del inventario que usa la prueba. La 5 y la 9 estan dentro de la barra rapida,
		// que en el personaje sintetico de pruebas esta vacia entera.
		private const int RanuraOrigen = 5;
		private const int RanuraDestino = 9;

		public static void Ejecutar()
		{
			Registrar("AUTOPRUEBA WS7: empieza. Idioma configurado=" + Idiomas.IdiomaConfigurado +
				", cultura activa=" + Idiomas.CulturaActiva + ".");

			ProbarDeshacerRehacer();
			ProbarIdiomaEnVivo();

			Registrar("AUTOPRUEBA WS7: terminada.");
		}

		/// <summary>
		/// Caso real y concreto: se pone un objeto de verdad en la ranura 5, se MUEVE a la 9 a
		/// traves del historial, se deshace (tiene que volver a la 5) y se rehace (tiene que
		/// volver a la 9). Cada paso se lee del inventario real del jugador, no de la foto.
		/// </summary>
		private static void ProbarDeshacerRehacer()
		{
			Item[] inventario = Main.LocalPlayer.inventory;
			Historial.Pila.Limpiar();

			// Estado de partida: una espada de cobre en la ranura 5, la 9 vacia.
			inventario[RanuraOrigen] = new Item();
			inventario[RanuraOrigen].SetDefaults(ItemID.CopperShortsword);
			inventario[RanuraOrigen].stack = 1;
			inventario[RanuraDestino] = new Item();

			Registrar("HISTORIAL/1 estado inicial: " + Estado(inventario));

			// La edicion real, envuelta por el historial. Esta es exactamente la forma en que los
			// demas workstreams tienen que envolver sus ediciones para que sean deshacibles.
			bool registrada = Historial.CambiarObjetos(
				"Mover objeto de la ranura " + (RanuraOrigen + 1) + " a la " + (RanuraDestino + 1),
				inventario,
				new int[] { RanuraOrigen, RanuraDestino },
				delegate {
					inventario[RanuraDestino] = inventario[RanuraOrigen];
					inventario[RanuraOrigen] = new Item();
				});

			Registrar("HISTORIAL/2 tras la accion (registrada=" + registrada + "): " + Estado(inventario) +
				" | puedeDeshacer=" + Historial.Pila.PuedeDeshacer +
				", etiqueta=\"" + Historial.Pila.EtiquetaDeshacer + "\"");

			string deshecha = Historial.Deshacer();
			Registrar("HISTORIAL/3 tras DESHACER (\"" + deshecha + "\"): " + Estado(inventario) +
				" | " + Veredicto(inventario, RanuraOrigen, "el objeto ha vuelto a su ranura original"));

			string rehecha = Historial.Rehacer();
			Registrar("HISTORIAL/4 tras REHACER (\"" + rehecha + "\"): " + Estado(inventario) +
				" | " + Veredicto(inventario, RanuraDestino, "el objeto ha vuelto a la ranura de destino"));

			// Se deja el inventario como estaba antes de la prueba.
			Historial.Deshacer();
			inventario[RanuraOrigen] = new Item();
			inventario[RanuraDestino] = new Item();
			Historial.Pila.Limpiar();
			Registrar("HISTORIAL/5 limpieza: " + Estado(inventario) +
				" | historial vaciado (entradas=" + Historial.Pila.Cuenta + ").");
		}

		/// <summary>
		/// Abre el panel de Ajustes y cambia el idioma en vivo, dejando en el log el MISMO texto
		/// (la clave propia del mod "Ajustes.Titulo") resuelto en cada idioma. Si el texto cambia,
		/// la recarga de traducciones ha ocurrido de verdad.
		/// </summary>
		private static void ProbarIdiomaEnVivo()
		{
			IdiomaDeTerrakeep original = Idiomas.IdiomaConfigurado;

			AjustesSystem.AbrirPanel("autoprueba WS7 (" + AjustesSystem.VariableAutoprueba + ")");

			Registrar("IDIOMA/1 de partida: configurado=" + original +
				", cultura=" + Idiomas.CulturaActiva +
				", Ajustes.Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\"" +
				", Ajustes.Deshacer=\"" + Idiomas.Texto("Ajustes.Deshacer") + "\"");

			Idiomas.Elegir(IdiomaDeTerrakeep.English);
			Registrar("IDIOMA/2 tras elegir English: cultura=" + Idiomas.CulturaActiva +
				", Ajustes.Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\"" +
				", Ajustes.Deshacer=\"" + Idiomas.Texto("Ajustes.Deshacer") + "\"");

			Idiomas.Elegir(IdiomaDeTerrakeep.Espanol);
			Registrar("IDIOMA/3 tras elegir Espanol: cultura=" + Idiomas.CulturaActiva +
				", Ajustes.Titulo=\"" + Idiomas.Texto("Ajustes.Titulo") + "\"" +
				", Ajustes.Deshacer=\"" + Idiomas.Texto("Ajustes.Deshacer") + "\"");

			Registrar("IDIOMA/4 persistencia: AjustesConfig.Instance.Idioma=" +
				(AjustesConfig.Instance != null ? AjustesConfig.Instance.Idioma.ToString() : "(config null)") +
				" - guardado por ModConfig, se recordara en la siguiente partida.");
		}

		private static string Estado(Item[] inventario)
		{
			return "inventory[" + RanuraOrigen + "]=" + Describir(inventario[RanuraOrigen]) +
				", inventory[" + RanuraDestino + "]=" + Describir(inventario[RanuraDestino]);
		}

		private static string Describir(Item objeto)
		{
			if (objeto == null || objeto.IsAir) {
				return "vacio";
			}
			return "\"" + objeto.Name + "\" x" + objeto.stack + " (type=" + objeto.type + ")";
		}

		private static string Veredicto(Item[] inventario, int ranuraEsperada, string queSeEsperaba)
		{
			int otra = ranuraEsperada == RanuraOrigen ? RanuraDestino : RanuraOrigen;
			bool bien = inventario[ranuraEsperada] != null
				&& inventario[ranuraEsperada].type == ItemID.CopperShortsword
				&& (inventario[otra] == null || inventario[otra].IsAir);
			return (bien ? "OK: " : "FALLO: NO ") + queSeEsperaba + " (ranura " + ranuraEsperada + ").";
		}

		private static void Registrar(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
			}
		}
	}
}
