using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>Cuentas de lo que hizo (y de lo que no pudo hacer) una pasada de auto-equipar.</summary>
	public sealed class ResultadoAutoEquipar
	{
		/// <summary>Objetos movidos de verdad a su sitio.</summary>
		public int Movidos;

		/// <summary>Objetos que ya estaban donde tenian que estar.</summary>
		public int YaColocados;

		/// <summary>Objetos de la build que el jugador NO tiene en ningun sitio. Auto-equipar no
		/// los crea (ver el comentario de <see cref="AutoEquipar"/>).</summary>
		public int NoPoseidos;

		/// <summary>Objetos que el jugador tiene pero que no cupieron (mochila llena, todos los
		/// slots de accesorio ocupados, o conflicto de reglas del juego).</summary>
		public int SinSitio;

		/// <summary>Objetos del catalogo que no existen en esta partida (mod no instalado).</summary>
		public int NoResueltos;

		/// <summary>Una linea por objeto, para dejar en el log lo que paso exactamente.</summary>
		public readonly List<string> Detalle = new List<string>();

		public string Resumen =>
			Idiomas.Texto("Builds.Resumen", Movidos, YaColocados, NoPoseidos, SinSitio, NoResueltos);
	}

	/// <summary>
	/// Auto-equipar del panel de Builds: coloca en su sitio el equipo de la build seleccionada.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Decision de diseño: solo MUEVE, nunca crea objetos.</b> Es lo mismo que hace el
	/// "Auto-equipar" de la app de escritorio Terrakeep, y es lo unico honesto dentro de una
	/// partida en curso: dar objetos de la nada convertiria una herramienta de organizacion en un
	/// generador de trampas, y ademas arruinaria la progresion que este mismo catalogo describe.
	/// Lo que el jugador no tenga se cuenta en <see cref="ResultadoAutoEquipar.NoPoseidos"/> y se
	/// enseña en el panel como "no lo tienes", nada mas.
	/// </para>
	/// <para>
	/// <b>Tampoco cambia prefijos.</b> El catalogo trae un prefijo recomendado para algunas armas
	/// ("Legendary", "Godly"...), pero reforjar gratis seria otra vez crear valor de la nada. El
	/// prefijo recomendado se enseña como texto informativo y el objeto real se mueve con el
	/// prefijo que ya tuviera.
	/// </para>
	/// <para>
	/// <b>Sobre el loadout</b>: se escribe sobre el conjunto de equipo que elige quien llama a
	/// <see cref="Ejecutar"/> (<paramref name="loadoutObjetivo"/>, 0/1/2) usando
	/// <see cref="EquipoJugador.ArmorDe"/>. Si es el conjunto ACTIVO eso significa escribir
	/// directo en <c>Player.armor</c>, que el jugador lleva puesto ahora mismo y por tanto se ve
	/// en el acto; si es otro, se escribe en <c>Player.Loadouts[n].Armor</c>, que solo se hace
	/// visible cuando el jugador cambia a ese conjunto (<c>Player.TrySwitchingLoadout</c>). Ver la
	/// pestaña Equipo del panel de Personaje (<c>PestanaEquipo</c>), que descubrio y dejo probado
	/// este mismo comportamiento contra el <c>EquipmentLoadout.Swap</c> real del juego instalado.
	/// </para>
	/// </remarks>
	public static class AutoEquipar
	{
		public static ResultadoAutoEquipar Ejecutar(Player jugador, ClaseBuild build, int loadoutObjetivo)
		{
			ResultadoAutoEquipar resultado = new ResultadoAutoEquipar();
			if (jugador == null || build == null) {
				return resultado;
			}

			Item[] destino = EquipoJugador.ArmorDe(jugador, loadoutObjetivo);

			ColocarArmadura(jugador, destino, build, resultado);
			ColocarAccesorios(jugador, destino, build, resultado);
			ColocarArmas(jugador, build, resultado);

			if (resultado.Movidos > 0) {
				// Lo que hace el propio juego despues de tocar el inventario: recalcular que se
				// puede fabricar. Sin esto la lista de crafteo se queda desfasada hasta el
				// siguiente movimiento manual.
				Recipe.FindRecipes();
				SoundEngine.PlaySound(SoundID.Grab);
			}

			return resultado;
		}

		private static void ColocarArmadura(Player jugador, Item[] destino, ClaseBuild build, ResultadoAutoEquipar resultado)
		{
			foreach (ObjetoBuild objeto in build.Armadura) {
				if (!Comprobado(objeto, resultado)) {
					continue;
				}

				// Se necesita un ejemplar del objeto para leer headSlot/bodySlot/legSlot. Si el
				// jugador lo tiene, se usa el suyo (respeta cualquier cambio de un mod); si no,
				// uno de muestra solo para saber a que slot iria.
				UbicacionObjeto donde = EquipoJugador.Buscar(jugador, objeto.Tipo);
				Item ejemplar = donde != null ? donde.Contenedor[donde.Indice] : Muestra(objeto.Tipo);

				int slot = EquipoJugador.SlotArmaduraDe(ejemplar);
				if (slot < 0) {
					resultado.SinSitio++;
					resultado.Detalle.Add($"{objeto.Nombre}: no es una pieza de armadura (sin headSlot/bodySlot/legSlot)");
					continue;
				}

				if (destino[slot].type == objeto.Tipo) {
					resultado.YaColocados++;
					resultado.Detalle.Add($"{objeto.Nombre}: ya puesto en equipo[{slot}]");
					continue;
				}

				if (donde == null) {
					resultado.NoPoseidos++;
					resultado.Detalle.Add($"{objeto.Nombre}: no lo tienes");
					continue;
				}

				string origen = donde.ToString();
				EquipoJugador.Intercambiar(donde.Contenedor, donde.Indice, destino, slot);
				resultado.Movidos++;
				resultado.Detalle.Add($"{objeto.Nombre}: {origen} -> equipo[{slot}]");
			}
		}

		private static void ColocarAccesorios(Player jugador, Item[] destino, ClaseBuild build, ResultadoAutoEquipar resultado)
		{
			foreach (ObjetoBuild objeto in build.Accesorios) {
				if (!Comprobado(objeto, resultado)) {
					continue;
				}

				if (EquipoJugador.AccesorioYaPuesto(jugador, destino, objeto.Tipo)) {
					resultado.YaColocados++;
					resultado.Detalle.Add($"{objeto.Nombre}: ya equipado");
					continue;
				}

				UbicacionObjeto donde = EquipoJugador.Buscar(jugador, objeto.Tipo);
				if (donde == null) {
					resultado.NoPoseidos++;
					resultado.Detalle.Add($"{objeto.Nombre}: no lo tienes");
					continue;
				}

				Item ejemplar = donde.Contenedor[donde.Indice];
				int slot = EquipoJugador.PrimerSlotAccesorioLibre(jugador, destino, ejemplar);
				if (slot < 0) {
					resultado.SinSitio++;
					resultado.Detalle.Add($"{objeto.Nombre}: sin slot de accesorio libre " +
						$"({EquipoJugador.SlotsAccesorioDisponibles(jugador)} disponibles) o incompatible con lo que ya llevas");
					continue;
				}

				string origen = donde.ToString();
				EquipoJugador.Intercambiar(donde.Contenedor, donde.Indice, destino, slot);
				resultado.Movidos++;
				resultado.Detalle.Add($"{objeto.Nombre}: {origen} -> equipo[{slot}] (accesorio)");
			}
		}

		private static void ColocarArmas(Player jugador, ClaseBuild build, ResultadoAutoEquipar resultado)
		{
			foreach (ObjetoBuild objeto in build.Armas) {
				if (!Comprobado(objeto, resultado)) {
					continue;
				}

				if (EquipoJugador.EnMochila(jugador, objeto.Tipo)) {
					resultado.YaColocados++;
					resultado.Detalle.Add($"{objeto.Nombre}: ya en la mochila");
					continue;
				}

				UbicacionObjeto donde = EquipoJugador.Buscar(jugador, objeto.Tipo);
				if (donde == null) {
					resultado.NoPoseidos++;
					resultado.Detalle.Add($"{objeto.Nombre}: no lo tienes");
					continue;
				}

				int hueco = EquipoJugador.PrimerHuecoMochila(jugador);
				if (hueco < 0) {
					resultado.SinSitio++;
					resultado.Detalle.Add($"{objeto.Nombre}: la mochila esta llena");
					continue;
				}

				string origen = donde.ToString();
				EquipoJugador.Intercambiar(donde.Contenedor, donde.Indice, jugador.inventory, hueco);
				resultado.Movidos++;
				resultado.Detalle.Add($"{objeto.Nombre}: {origen} -> inventario[{hueco}]");
			}
		}

		private static bool Comprobado(ObjetoBuild objeto, ResultadoAutoEquipar resultado)
		{
			if (objeto.Resuelto) {
				return true;
			}
			resultado.NoResueltos++;
			resultado.Detalle.Add($"{objeto.Nombre}: el objeto \"{objeto.Pid}\" no existe en esta partida");
			return false;
		}

		/// <summary>Ejemplar de solo lectura de un objeto, para consultar sus campos.</summary>
		public static Item Muestra(int tipo)
		{
			Item item = new Item();
			item.SetDefaults(tipo);
			return item;
		}

		/// <summary>Vuelca el detalle completo en el log del juego, que es la evidencia real de
		/// que auto-equipar hizo lo que dice haber hecho.</summary>
		public static void Registrar(Player jugador, ClaseBuild build, ResultadoAutoEquipar resultado,
			string etiquetaBuild, int loadoutObjetivo)
		{
			bool activo = EquipoJugador.EsLoadoutActivo(jugador, loadoutObjetivo);
			StringBuilder texto = new StringBuilder();
			texto.Append($"{Terrakeep.LogTag} AUTO-EQUIPAR \"{etiquetaBuild}\" / {build.Etiqueta}: {resultado.Resumen}. ");
			texto.Append($"Conjunto de destino={loadoutObjetivo + 1}/{jugador.Loadouts.Length} " +
				$"({(activo ? "ACTIVO ahora mismo, se ve al instante" : "no activo, se guarda en Player.Loadouts[" + loadoutObjetivo + "].Armor")}), ");
			texto.Append($"Conjunto activo real (CurrentLoadoutIndex)={jugador.CurrentLoadoutIndex}, ");
			texto.Append($"slots de accesorio disponibles={EquipoJugador.SlotsAccesorioDisponibles(jugador)}.");
			RegistroBuilds.Linea(texto.ToString());

			foreach (string linea in resultado.Detalle) {
				RegistroBuilds.Linea($"{Terrakeep.LogTag}   - {linea}");
			}

			Item[] destino = EquipoJugador.ArmorDe(jugador, loadoutObjetivo);
			RegistroBuilds.Linea(
				$"{Terrakeep.LogTag}   Conjunto {loadoutObjetivo + 1} tras auto-equipar: {EstadoEquipo(jugador, destino)}");
		}

		/// <summary>Foto de un conjunto de equipo concreto, en una linea, para el log.</summary>
		public static string EstadoEquipo(Player jugador, Item[] destino)
		{
			StringBuilder texto = new StringBuilder();
			string[] nombres = { "casco", "pechera", "grebas" };
			for (int i = 0; i < 3; i++) {
				texto.Append($"{nombres[i]}=\"{Describir(destino[i])}\" ");
			}
			int disponibles = EquipoJugador.SlotsAccesorioDisponibles(jugador);
			for (int i = 0; i < disponibles; i++) {
				texto.Append($"acc{i + 1}=\"{Describir(destino[EquipoJugador.PrimerSlotAccesorio + i])}\" ");
			}
			return texto.ToString().TrimEnd();
		}

		private static string Describir(Item item)
		{
			return item == null || item.IsAir ? "" : $"{item.Name}#{item.type}";
		}
	}
}
