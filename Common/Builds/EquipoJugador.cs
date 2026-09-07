using System.Collections.Generic;
using Terraria;
using Terraria.UI;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>Donde esta un objeto concreto dentro de los contenedores del jugador.</summary>
	public sealed class UbicacionObjeto
	{
		/// <summary>Array VIVO del juego donde esta el objeto (no una copia).</summary>
		public Item[] Contenedor;

		public int Indice;

		/// <summary>Nombre legible del contenedor, para el log y la interfaz.</summary>
		public string NombreContenedor;

		public override string ToString() => $"{NombreContenedor}[{Indice}]";
	}

	/// <summary>
	/// Lectura de los contenedores reales del jugador: donde tiene cada objeto ("ya lo tienes")
	/// y cuantos slots de accesorio le caben de verdad en esta partida.
	/// </summary>
	public static class EquipoJugador
	{
		/// <summary>Primer indice de accesorio dentro de <c>Player.armor</c>.
		/// (0-2 = casco/pechera/grebas, 3-9 = accesorios, 10-19 = vanity; documentado en el
		/// propio XMLdoc de <c>Player.armor</c> del tModLoader instalado).</summary>
		public const int PrimerSlotAccesorio = 3;

		/// <summary>Slots de accesorio sin contar los extra (Corazon de Demonio / modo Maestro).</summary>
		public const int SlotsAccesorioBase = 5;

		/// <summary>Ultimo indice util del inventario "de mochila" (0-49). Del 50 al 57 son
		/// monedas y munición, y el 58 es el slot auxiliar que usa la interfaz.</summary>
		public const int UltimoSlotMochila = 49;

		/// <summary>
		/// Cuantos slots de accesorio tiene REALMENTE este jugador ahora mismo: los 5 de siempre
		/// mas los que le den el Corazon de Demonio (experto) y el modo Maestro. Se calcula con
		/// la propia funcion del juego (<c>Player.GetAmountOfExtraAccessorySlotsToShow</c>) en vez
		/// de suponer un maximo fijo.
		/// </summary>
		public static int SlotsAccesorioDisponibles(Player jugador)
		{
			return SlotsAccesorioBase + jugador.GetAmountOfExtraAccessorySlotsToShow();
		}

		/// <summary>
		/// El array de armadura/accesorios de UN conjunto de equipo concreto (0/1/2), sea cual sea
		/// el que este activo ahora mismo.
		/// <para />
		/// Igual que descubrio WS1 para la pestaña de Equipo (ver <c>PestanaEquipo</c>) y esta
		/// verificado contra <c>EquipmentLoadout.Swap</c> del tModLoader instalado: el conjunto
		/// ACTIVO no vive en <c>Player.Loadouts[]</c>, vive "suelto" en <c>Player.armor</c>. Los
		/// otros dos conjuntos SI viven en <c>Player.Loadouts[i].Armor</c> mientras no estan
		/// activos. Escribir aqui es la unica forma de dejar equipo listo en un conjunto que el
		/// jugador no lleva puesto ahora mismo, sin tener que cambiar de conjunto para hacerlo.
		/// </summary>
		public static Item[] ArmorDe(Player jugador, int indiceLoadout)
		{
			int total = jugador.Loadouts.Length;
			int i = indiceLoadout < 0 ? 0 : (indiceLoadout >= total ? total - 1 : indiceLoadout);
			return i == jugador.CurrentLoadoutIndex ? jugador.armor : jugador.Loadouts[i].Armor;
		}

		/// <summary>true si el indice de conjunto pedido es el que el jugador lleva puesto ahora
		/// mismo (o sea, si escribir en el se ve al instante sobre el personaje).</summary>
		public static bool EsLoadoutActivo(Player jugador, int indiceLoadout) =>
			indiceLoadout == jugador.CurrentLoadoutIndex;

		/// <summary>
		/// Todos los contenedores donde se considera que el jugador "tiene" un objeto: inventario,
		/// el equipo ACTIVO (incluida la ropa social), los otros dos conjuntos de equipo guardados
		/// en <c>Player.Loadouts</c> (para no decir "no lo tienes" de un objeto que en realidad
		/// esta en un conjunto que ahora mismo no lleva puesto) y los cuatro almacenes portatiles -
		/// Hucha, Caja fuerte, Forja del Defensor y Boveda del Vacio.
		/// </summary>
		public static IEnumerable<KeyValuePair<string, Item[]>> Contenedores(Player jugador)
		{
			yield return new KeyValuePair<string, Item[]>("inventario", jugador.inventory);
			yield return new KeyValuePair<string, Item[]>("equipo", jugador.armor);
			for (int i = 0; i < jugador.Loadouts.Length; i++) {
				if (i == jugador.CurrentLoadoutIndex) {
					continue; // este es justo el que se acaba de dar arriba como "equipo"
				}
				yield return new KeyValuePair<string, Item[]>($"conjunto {i + 1}", jugador.Loadouts[i].Armor);
			}
			if (jugador.bank?.item != null) {
				yield return new KeyValuePair<string, Item[]>("hucha", jugador.bank.item);
			}
			if (jugador.bank2?.item != null) {
				yield return new KeyValuePair<string, Item[]>("caja fuerte", jugador.bank2.item);
			}
			if (jugador.bank3?.item != null) {
				yield return new KeyValuePair<string, Item[]>("forja", jugador.bank3.item);
			}
			if (jugador.bank4?.item != null) {
				yield return new KeyValuePair<string, Item[]>("boveda del vacio", jugador.bank4.item);
			}
		}

		/// <summary>
		/// Busca la primera copia de <paramref name="tipo"/> entre los contenedores del jugador.
		/// Devuelve null si no la tiene. Es la base del "ya lo tienes" del panel.
		/// </summary>
		public static UbicacionObjeto Buscar(Player jugador, int tipo)
		{
			if (tipo <= 0) {
				return null;
			}

			foreach (KeyValuePair<string, Item[]> par in Contenedores(jugador)) {
				Item[] arr = par.Value;
				int limite = par.Value == jugador.inventory ? 58 : arr.Length;
				for (int i = 0; i < limite; i++) {
					if (arr[i] != null && arr[i].type == tipo && arr[i].stack > 0) {
						return new UbicacionObjeto { Contenedor = arr, Indice = i, NombreContenedor = par.Key };
					}
				}
			}

			return null;
		}

		/// <summary>true si el jugador tiene ese objeto en algun sitio.</summary>
		public static bool Tiene(Player jugador, int tipo) => Buscar(jugador, tipo) != null;

		/// <summary>
		/// Slot de <c>Player.armor</c> al que corresponde una pieza de armadura: 0 casco,
		/// 1 pechera, 2 grebas. -1 si el objeto no es ninguna de las tres.
		/// <para />
		/// Se decide por los campos reales del objeto (<c>headSlot</c>/<c>bodySlot</c>/
		/// <c>legSlot</c>) y no por el orden en que vengan en el JSON: asi funciona igual aunque
		/// el catalogo liste las piezas desordenadas.
		/// </summary>
		public static int SlotArmaduraDe(Item objeto)
		{
			if (objeto == null || objeto.IsAir) {
				return -1;
			}
			if (objeto.headSlot >= 0) {
				return 0;
			}
			if (objeto.bodySlot >= 0) {
				return 1;
			}
			if (objeto.legSlot >= 0) {
				return 2;
			}
			return -1;
		}

		/// <summary>
		/// Primer slot de accesorio donde cabe <paramref name="objeto"/> dentro de
		/// <paramref name="destino"/> (el conjunto de equipo elegido: <c>jugador.armor</c> si es el
		/// activo, o <c>jugador.Loadouts[n].Armor</c> si no), o -1 si no hay ninguno. Solo
		/// considera slots VACIOS (auto-equipar no desaloja accesorios que ya llevas puestos) y
		/// respeta las reglas del propio juego con <c>ItemSlot.AccCheck</c> (comprobado que opera
		/// solo sobre el array que se le pasa, sin depender de que sea el del jugador activo), que
		/// es lo que impide duplicados y dos pares de alas a la vez.
		/// </summary>
		public static int PrimerSlotAccesorioLibre(Player jugador, Item[] destino, Item objeto)
		{
			int disponibles = SlotsAccesorioDisponibles(jugador);
			for (int i = 0; i < disponibles; i++) {
				int slot = PrimerSlotAccesorio + i;
				if (!destino[slot].IsAir) {
					continue;
				}
				// AccCheck devuelve TRUE cuando NO se puede equipar (ver su XMLdoc real).
				if (ItemSlot.AccCheck(destino, objeto, slot)) {
					continue;
				}
				return slot;
			}
			return -1;
		}

		/// <summary>true si el objeto ya esta puesto en uno de los slots de accesorio activos de
		/// <paramref name="destino"/>.</summary>
		public static bool AccesorioYaPuesto(Player jugador, Item[] destino, int tipo)
		{
			int disponibles = SlotsAccesorioDisponibles(jugador);
			for (int i = 0; i < disponibles; i++) {
				if (destino[PrimerSlotAccesorio + i].type == tipo) {
					return true;
				}
			}
			return false;
		}

		/// <summary>Primer hueco vacio de la mochila (0-49), o -1 si esta llena.</summary>
		public static int PrimerHuecoMochila(Player jugador)
		{
			for (int i = 0; i <= UltimoSlotMochila; i++) {
				if (jugador.inventory[i].IsAir) {
					return i;
				}
			}
			return -1;
		}

		/// <summary>true si el objeto ya esta en la mochila (0-49).</summary>
		public static bool EnMochila(Player jugador, int tipo)
		{
			for (int i = 0; i <= UltimoSlotMochila; i++) {
				if (jugador.inventory[i].type == tipo && jugador.inventory[i].stack > 0) {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Intercambia dos posiciones de dos arrays vivos del juego. Es la unica operacion que
		/// hace auto-equipar: nunca crea ni destruye objetos, solo los cambia de sitio.
		/// </summary>
		public static void Intercambiar(Item[] origen, int indiceOrigen, Item[] destino, int indiceDestino)
		{
			if (ReferenceEquals(origen, destino) && indiceOrigen == indiceDestino) {
				return;
			}
			Item temporal = destino[indiceDestino];
			destino[indiceDestino] = origen[indiceOrigen];
			origen[indiceOrigen] = temporal;
		}
	}
}
