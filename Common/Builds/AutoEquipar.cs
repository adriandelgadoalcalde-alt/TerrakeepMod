using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Prefijos;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>Cuentas de lo que hizo (y de lo que no pudo hacer) una pasada de auto-equipar.</summary>
	public sealed class ResultadoAutoEquipar
	{
		/// <summary>Objetos movidos de verdad a su sitio.</summary>
		public int Movidos;

		/// <summary>Objetos que ya estaban donde tenian que estar.</summary>
		public int YaColocados;

		/// <summary>Objetos de la build que el jugador NO tenia en ningun sitio y que se han
		/// CREADO del catalogo (misma ruta real que <c>ContenidoLibreria.PedirObjeto</c>: mejor
		/// prefijo incluido) y colocado en el conjunto de destino. Ver el XMLdoc de
		/// <see cref="AutoEquipar"/> para el porque de este cambio de diseño.</summary>
		public int Creados;

		/// <summary>Objetos que el jugador ya tenia (o que se acaban de crear) pero que no
		/// cupieron: mochila llena, todos los slots de accesorio ocupados/incompatibles, o
		/// conflicto de reglas del juego. Auto-equipar nunca DESTRUYE nada para hacer sitio.</summary>
		public int SinSitio;

		/// <summary>Objetos del catalogo que no existen en esta partida (mod no instalado).</summary>
		public int NoResueltos;

		/// <summary>Una linea por objeto, para dejar en el log lo que paso exactamente.</summary>
		public readonly List<string> Detalle = new List<string>();

		/// <summary>
		/// Una entrada por objeto que acabo en <see cref="SinSitio"/>, con la causa REAL como valor
		/// (no texto suelto) para que el panel pueda enseñar un motivo localizado sin tener que
		/// analizar el texto de <see cref="Detalle"/> (que es solo para el log, en español fijo).
		/// <para />
		/// Nace de investigar un caso real reportado por el usuario: un arma que SI poseia salio
		/// "sin sitio" justo al lado de la cabecera "ranuras de accesorio disponibles: 5", que se
		/// leia como una contradiccion aunque no lo era (el arma necesita hueco en la MOCHILA, no
		/// en accesorios - dos recursos distintos). El comportamiento era correcto (mochila llena
		/// de verdad, comprobado en el sandbox); lo que estaba mal era que el panel no decia POR QUE
		/// sin obligar a mirar el log.
		/// </summary>
		public readonly List<ItemSinSitio> DetalleSinSitio = new List<ItemSinSitio>();

		public string Resumen =>
			Idiomas.Texto("Builds.Resumen", Movidos, YaColocados, Creados, SinSitio, NoResueltos);
	}

	/// <summary>Motivo real por el que un objeto concreto no cupo (ver <see cref="ResultadoAutoEquipar.DetalleSinSitio"/>).</summary>
	public enum CausaSinSitio
	{
		/// <summary>La mochila (0-49) esta llena: no habia hueco para moverlo o crearlo ahi.</summary>
		MochilaLlena,

		/// <summary>Todos los slots de accesorio activos estan ocupados, o el objeto es
		/// incompatible con lo que ya llevas puesto (<c>ItemSlot.AccCheck</c>).</summary>
		SlotAccesorioOcupado,

		/// <summary>El objeto no tiene <c>headSlot</c>/<c>bodySlot</c>/<c>legSlot</c>: el catalogo
		/// lo cataloga como armadura pero el objeto real no lo es. Caso de datos, no de recursos.</summary>
		NoEsPiezaDeArmadura,
	}

	/// <summary>Un objeto concreto que acabo en <see cref="ResultadoAutoEquipar.SinSitio"/>.</summary>
	public sealed class ItemSinSitio
	{
		public string Nombre;
		public CausaSinSitio Causa;
	}

	/// <summary>
	/// Auto-equipar del panel de Builds: coloca en su sitio el equipo de la build seleccionada.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Cambio de diseño (pedido explicito del usuario, tras probar el mod en su partida real):
	/// "el tema de builds esta bien que te diga los objetos que tienes pero si no los tienes que
	/// lo aplique directamente desde la libreria"</b>. La decision original era que auto-equipar
	/// solo MOVIA objetos ya poseidos y nunca creaba nada (mismo criterio que la app de escritorio
	/// Terrakeep, pensado para no convertir una herramienta de organizacion en un generador de
	/// trampas). El usuario decidio explicitamente lo contrario para este panel: lo que NO tenga
	/// se trae del CATALOGO, por la MISMA ruta real que ya usa
	/// <c>ContenidoLibreria.PedirObjeto</c> (<c>Item.SetDefaults</c> + el mejor prefijo real de
	/// <see cref="CatalogoMejorPrefijo"/>, nada reimplementado) y se coloca en el conjunto de
	/// destino igual que un objeto que ya poseyera. Se cuenta aparte en
	/// <see cref="ResultadoAutoEquipar.Creados"/> (nunca mezclado con <c>Movidos</c>) para que el
	/// panel pueda seguir siendo transparente sobre que paso de verdad: el texto de ayuda del boton
	/// (<c>Builds.AutoEquiparAyuda</c>) ya avisa de este comportamiento en vez de prometer lo
	/// contrario, que es el aviso que pidio el propio usuario ("no lo dejes sin avisar de alguna
	/// forma").
	/// </para>
	/// <para>
	/// <b>Nunca DESTRUYE nada para hacer sitio.</b> Si crear un objeto exigiria desplazar del
	/// conjunto de destino algo que no cupiera despues en la mochila (armadura ya puesta y mochila
	/// llena a la vez), no se crea nada y se cuenta como <see cref="ResultadoAutoEquipar.SinSitio"/>
	/// - la misma garantia que ya tenia mover objetos existentes.
	/// </para>
	/// <para>
	/// <b>Prefijo</b>: los objetos MOVIDOS (ya poseidos) se mueven con el prefijo que ya tuvieran,
	/// sin reforjar nada gratis. Los objetos CREADOS salen con el mismo "mejor prefijo" real que ya
	/// aplica la Libreria al cogerlos del catalogo (<see cref="CatalogoMejorPrefijo"/>), no con el
	/// prefijo "recomendado" informativo de <c>ObjetoBuild.PrefijoRecomendado</c> (ese sigue siendo
	/// solo texto en el panel).
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

			if (resultado.Movidos > 0 || resultado.Creados > 0) {
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
					RegistrarSinSitio(resultado, objeto, CausaSinSitio.NoEsPiezaDeArmadura,
						"no es una pieza de armadura (sin headSlot/bodySlot/legSlot)");
					continue;
				}

				if (destino[slot].type == objeto.Tipo) {
					resultado.YaColocados++;
					resultado.Detalle.Add($"{objeto.Nombre}: ya puesto en equipo[{slot}]");
					continue;
				}

				if (donde != null) {
					string origen = donde.ToString();
					EquipoJugador.Intercambiar(donde.Contenedor, donde.Indice, destino, slot);
					resultado.Movidos++;
					resultado.Detalle.Add($"{objeto.Nombre}: {origen} -> equipo[{slot}]");
					continue;
				}

				// No lo tiene: se trae del catalogo (ver el XMLdoc de la clase). Si el slot de
				// destino ya lleva puesta OTRA pieza (no el mismo tipo: eso ya lo descarto el "ya
				// puesto" de arriba), esa otra pieza se desplaza a la mochila en vez de destruirse
				// - igual que un Intercambiar normal, pero aqui no hay un "donde" real del que
				// sacarlo.
				if (!destino[slot].IsAir) {
					int huecoDesplazado = EquipoJugador.PrimerHuecoMochila(jugador);
					if (huecoDesplazado < 0) {
						RegistrarSinSitio(resultado, objeto, CausaSinSitio.MochilaLlena,
							$"se iba a crear, pero no habia hueco en la mochila para mover lo que ya llevabas en equipo[{slot}]");
						continue;
					}
					jugador.inventory[huecoDesplazado] = destino[slot];
				}

				destino[slot] = CrearDesdeLibreria(objeto.Tipo);
				resultado.Creados++;
				resultado.Detalle.Add($"{objeto.Nombre}: creado del catalogo -> equipo[{slot}]");
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
				if (donde != null) {
					Item ejemplar = donde.Contenedor[donde.Indice];
					int slotPoseido = EquipoJugador.PrimerSlotAccesorioLibre(jugador, destino, ejemplar);
					if (slotPoseido < 0) {
						RegistrarSinSitio(resultado, objeto, CausaSinSitio.SlotAccesorioOcupado,
							$"sin slot de accesorio libre ({EquipoJugador.SlotsAccesorioDisponibles(jugador)} disponibles) " +
							"o incompatible con lo que ya llevas");
						continue;
					}

					string origen = donde.ToString();
					EquipoJugador.Intercambiar(donde.Contenedor, donde.Indice, destino, slotPoseido);
					resultado.Movidos++;
					resultado.Detalle.Add($"{objeto.Nombre}: {origen} -> equipo[{slotPoseido}] (accesorio)");
					continue;
				}

				// No lo tiene: se trae del catalogo. PrimerSlotAccesorioLibre solo devuelve slots
				// VACIOS (ver su XMLdoc), asi que aqui nunca hace falta desplazar nada.
				Item nuevo = CrearDesdeLibreria(objeto.Tipo);
				int slot = EquipoJugador.PrimerSlotAccesorioLibre(jugador, destino, nuevo);
				if (slot < 0) {
					RegistrarSinSitio(resultado, objeto, CausaSinSitio.SlotAccesorioOcupado,
						$"se iba a crear, pero no hay slot de accesorio libre ({EquipoJugador.SlotsAccesorioDisponibles(jugador)} " +
						"disponibles) o es incompatible con lo que ya llevas");
					continue;
				}

				destino[slot] = nuevo;
				resultado.Creados++;
				resultado.Detalle.Add($"{objeto.Nombre}: creado del catalogo -> equipo[{slot}] (accesorio)");
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
				int hueco = EquipoJugador.PrimerHuecoMochila(jugador);
				if (hueco < 0) {
					RegistrarSinSitio(resultado, objeto, CausaSinSitio.MochilaLlena,
						donde != null ? "la mochila esta llena" : "se iba a crear, pero la mochila esta llena");
					continue;
				}

				if (donde != null) {
					string origen = donde.ToString();
					EquipoJugador.Intercambiar(donde.Contenedor, donde.Indice, jugador.inventory, hueco);
					resultado.Movidos++;
					resultado.Detalle.Add($"{objeto.Nombre}: {origen} -> inventario[{hueco}]");
					continue;
				}

				jugador.inventory[hueco] = CrearDesdeLibreria(objeto.Tipo);
				resultado.Creados++;
				resultado.Detalle.Add($"{objeto.Nombre}: creado del catalogo -> inventario[{hueco}]");
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

		/// <summary>Cuenta un "sin sitio" a la vez en el log (texto libre en español, como el resto
		/// de <see cref="ResultadoAutoEquipar.Detalle"/>) y en <see cref="ResultadoAutoEquipar.DetalleSinSitio"/>
		/// (causa estructurada, para que el panel pueda enseñar un motivo localizado de verdad).</summary>
		private static void RegistrarSinSitio(ResultadoAutoEquipar resultado, ObjetoBuild objeto, CausaSinSitio causa, string textoLog)
		{
			resultado.SinSitio++;
			resultado.Detalle.Add($"{objeto.Nombre}: {textoLog}");
			resultado.DetalleSinSitio.Add(new ItemSinSitio { Nombre = objeto.Nombre, Causa = causa });
		}

		/// <summary>
		/// Crea un ejemplar nuevo de <paramref name="tipo"/> exactamente por la MISMA ruta real que
		/// <c>ContenidoLibreria.PedirObjeto</c> usa para coger un objeto del catalogo de la
		/// Libreria: <c>Item.SetDefaults</c> y, si el catalogo de mejor prefijo tiene entrada para
		/// este tipo, <c>Item.Prefix</c> con el. Nada reimplementado a mano.
		/// </summary>
		private static Item CrearDesdeLibreria(int tipo)
		{
			Item nuevo = new Item();
			nuevo.SetDefaults(tipo);

			byte? mejorPrefijo = CatalogoMejorPrefijo.MejorPrefijo(tipo);
			if (mejorPrefijo.HasValue) {
				nuevo.Prefix(mejorPrefijo.Value);
			}

			nuevo.stack = 1;
			return nuevo;
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
