using System;
using System.Text;
using Terraria;

namespace TerrakeepMod.Common.Undo
{
	/// <summary>
	/// Foto de un subconjunto de ranuras de un array de objetos REAL del juego
	/// (<c>Main.LocalPlayer.inventory</c>, <c>.armor</c>, <c>.bank.item</c>, el contenido de un
	/// cofre de <c>Main.chest[i].item</c>...). Guarda el array de destino, que ranuras se tocaron
	/// y una copia profunda de lo que habia en cada una.
	/// </summary>
	public sealed class SnapshotDeObjetos
	{
		private readonly Item[] _destino;
		private readonly int[] _indices;
		private readonly Item[] _copias;

		private SnapshotDeObjetos(Item[] destino, int[] indices, Item[] copias)
		{
			_destino = destino;
			_indices = indices;
			_copias = copias;
		}

		/// <summary>Ranuras que cubre esta foto.</summary>
		public int Cuenta {
			get { return _indices.Length; }
		}

		/// <summary>
		/// Toma la foto. <c>Item.Clone()</c> es copia profunda de verdad (clona tambien el
		/// <c>ModItem</c> y los <c>GlobalItem</c>, comprobado en el <c>tModLoader.dll</c> real
		/// instalado): sin ese clon guardariamos la MISMA referencia que sigue viva en el
		/// inventario y la "foto" cambiaria sola al editar el objeto.
		/// </summary>
		public static SnapshotDeObjetos Tomar(Item[] destino, params int[] indices)
		{
			if (destino == null) {
				throw new ArgumentNullException("destino");
			}
			if (indices == null || indices.Length == 0) {
				throw new ArgumentException("Hay que indicar al menos una ranura.", "indices");
			}

			Item[] copias = new Item[indices.Length];
			for (int k = 0; k < indices.Length; k++) {
				int i = indices[k];
				if (i < 0 || i >= destino.Length) {
					throw new ArgumentOutOfRangeException("indices", "Ranura " + i + " fuera del array (" + destino.Length + ").");
				}
				// Una ranura vacia en Terraria nunca es null, es un Item con type 0; pero un array
				// recien creado por otro codigo si podria traer nulls, asi que se cubre.
				copias[k] = destino[i] != null ? destino[i].Clone() : new Item();
			}

			return new SnapshotDeObjetos(destino, (int[])indices.Clone(), copias);
		}

		/// <summary>Toma la foto del array ENTERO.</summary>
		public static SnapshotDeObjetos TomarTodo(Item[] destino)
		{
			if (destino == null) {
				throw new ArgumentNullException("destino");
			}

			int[] indices = new int[destino.Length];
			for (int i = 0; i < indices.Length; i++) {
				indices[i] = i;
			}
			return Tomar(destino, indices);
		}

		/// <summary>
		/// Vuelca la foto sobre los datos reales del juego. Se vuelve a clonar en cada volcado a
		/// proposito: la foto tiene que seguir siendo valida para deshacer y rehacer las veces que
		/// haga falta, y si se entregara la propia copia, el juego la mutaria a partir de ese
		/// momento y la foto dejaria de representar lo que representaba.
		/// </summary>
		public void Aplicar()
		{
			for (int k = 0; k < _indices.Length; k++) {
				_destino[_indices[k]] = _copias[k].Clone();
			}
		}

		/// <summary>
		/// true si las dos fotos representan el mismo contenido. Se comparan solo los campos que
		/// una edicion de Terrakeep puede cambiar (que objeto es, cuantos hay, con que modificador
		/// y si esta marcado como favorito), no los ~90 campos de <see cref="Item"/>: el resto los
		/// deriva <c>SetDefaults</c> del propio tipo y compararlos solo daria falsos positivos.
		/// </summary>
		public bool MismoContenidoQue(SnapshotDeObjetos otro)
		{
			if (otro == null || otro._copias.Length != _copias.Length) {
				return false;
			}

			for (int k = 0; k < _copias.Length; k++) {
				if (!MismoContenido(_copias[k], otro._copias[k])) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Los mismos cuatro campos que compara <see cref="MismoContenidoQue"/>, pero para dos
		/// <see cref="Item"/> sueltos (no ranuras de un array). La usa tambien
		/// <see cref="Historial.CambiarObjetoDeSlotConCeldas"/> para las "celdas" auxiliares que no
		/// viven en ningun array (<c>Main.mouseItem</c>, <c>Player.trashItem</c>...).
		/// </summary>
		public static bool MismoContenido(Item a, Item b)
		{
			bool aVacio = a == null || a.IsAir;
			bool bVacio = b == null || b.IsAir;
			if (aVacio || bVacio) {
				return aVacio == bVacio;
			}
			return a.type == b.type && a.stack == b.stack && a.prefix == b.prefix && a.favorited == b.favorited;
		}

		/// <summary>Resumen legible para el log de pruebas. No se usa en la interfaz.</summary>
		public string Describir()
		{
			StringBuilder sb = new StringBuilder();
			for (int k = 0; k < _indices.Length; k++) {
				if (sb.Length > 0) {
					sb.Append(", ");
				}
				Item copia = _copias[k];
				sb.Append("[").Append(_indices[k]).Append("]=");
				sb.Append(copia.IsAir
					? "vacio"
					: copia.Name + " x" + copia.stack + " (type=" + copia.type + " prefix=" + copia.prefix + ")");
			}
			return sb.ToString();
		}
	}

	/// <summary>
	/// Punto de entrada del deshacer/rehacer de Terrakeep dentro del juego (WS7).
	/// <para />
	/// <b>Para los demas workstreams</b>: no toqueis <see cref="PilaDeSnapshots"/> a mano. Envolved
	/// vuestra edicion con uno de los metodos de esta clase y ya queda deshacible:
	/// <code>
	/// Historial.CambiarObjetos("Mover objeto a la ranura 10",
	///     Main.LocalPlayer.inventory, new[] { 5, 9 },
	///     () => { /* la edicion real, tal cual la teniais */ });
	/// </code>
	/// Para cualquier otro dato que no sea un array de objetos (un bool de desbloqueo, el color de
	/// pelo, la dificultad del mundo) esta <see cref="CambiarValor{T}"/>.
	/// <para />
	/// Los botones de vuestra interfaz cuelgan de <see cref="Pila"/>:
	/// <c>Historial.Pila.PuedeDeshacer</c> / <c>.EtiquetaDeshacer</c> / <c>.Deshacer()</c>.
	/// </summary>
	public static class Historial
	{
		/// <summary>
		/// El historial de la sesion. Se vacia solo al entrar o salir de un mundo
		/// (<see cref="HistorialSystem"/>): las fotos guardan referencias a los arrays reales de
		/// la partida, y <c>Player.inventory</c> se reasigna al cargar otro personaje.
		/// </summary>
		public static PilaDeSnapshots Pila { get; private set; }

		static Historial()
		{
			Pila = new PilaDeSnapshots();
		}

		/// <summary>
		/// Ejecuta <paramref name="edicion"/> sobre las ranuras indicadas y lo deja deshacible.
		/// Toma la foto de antes, ejecuta, toma la foto de despues y registra la entrada.
		/// <para />
		/// Las ranuras hay que declararlas ANTES porque la foto de "antes" se toma con ellas: si
		/// la edicion toca una ranura que no esta en la lista, ese cambio no sera deshacible (y
		/// eso es un fallo de quien llama, no de aqui). Ante la duda, pasar de mas.
		/// </summary>
		/// <returns>true si se registro la entrada; false si <paramref name="edicion"/> no
		/// cambio nada (no se ensucia el historial con entradas vacias).</returns>
		public static bool CambiarObjetos(string etiqueta, Item[] destino, int[] indices, Action edicion)
		{
			if (edicion == null) {
				throw new ArgumentNullException("edicion");
			}

			SnapshotDeObjetos antes = SnapshotDeObjetos.Tomar(destino, indices);
			edicion();
			SnapshotDeObjetos despues = SnapshotDeObjetos.Tomar(destino, indices);

			if (antes.MismoContenidoQue(despues)) {
				return false;
			}

			Pila.Registrar(new EntradaSnapshot<SnapshotDeObjetos>(
				etiqueta, antes, despues, (SnapshotDeObjetos foto) => foto.Aplicar()));
			return true;
		}

		/// <summary>Igual que <see cref="CambiarObjetos"/> pero sobre el array entero.</summary>
		public static bool CambiarTodosLosObjetos(string etiqueta, Item[] destino, Action edicion)
		{
			int[] indices = new int[destino.Length];
			for (int i = 0; i < indices.Length; i++) {
				indices[i] = i;
			}
			return CambiarObjetos(etiqueta, destino, indices, edicion);
		}

		/// <summary>
		/// Una "celda" de un unico objeto que NO vive en ningun array: <c>Main.mouseItem</c> (lo
		/// que se lleva cogido con el raton) o <c>Player.trashItem</c> (la papelera de vanilla),
		/// por ejemplo. A diferencia de <see cref="SnapshotDeObjetos"/> no hay una referencia
		/// estable a la que apuntar - cada campo hay que leerlo y escribirlo por su cuenta.
		/// </summary>
		public sealed class CeldaDeObjeto
		{
			public readonly Func<Item> Leer;
			public readonly Action<Item> Escribir;

			public CeldaDeObjeto(Func<Item> leer, Action<Item> escribir)
			{
				Leer = leer;
				Escribir = escribir;
			}
		}

		/// <summary>
		/// Igual que <see cref="CambiarObjetos"/> para UNA sola ranura de un array, pero cubriendo
		/// ADEMAS una o mas <paramref name="celdas"/> auxiliares, y sin etiqueta fija (se deriva de
		/// como quedo la ranura, con una funcion que recibe el objeto de antes y el de despues).
		/// <para />
		/// <b>Por que hacia falta esto y no bastaba con la ranura sola (hallazgo real, no un
		/// gusto de diseño).</b> <c>ItemSlot.Handle</c> casi nunca mueve un objeto SOLO dentro de
		/// la ranura que se le pasa: un clic normal lo intercambia con <c>Main.mouseItem</c> (lo
		/// que llevas "en la mano"), y un Mayus+clic de la papelera rapida lo manda a
		/// <c>Player.trashItem</c> - los dos son campos sueltos, no ranuras de ningun array, y
		/// <c>ItemSlot.cs</c> real (decompilado, 692-1080) los toca DIRECTAMENTE, nunca a traves
		/// del array que se le paso. Si el historial solo vigilara la ranura, "deshacer" un
		/// SEGUNDO clic (soltar en la ranura B lo que se cogio de la ranura A) devolveria la
		/// ranura B a estar vacia sin devolver el objeto a ningun sitio - el objeto desaparece de
		/// verdad, no queda "como estaba", que es justo lo que WS7 prometio que nunca pasaria.
		/// Envolver tambien <c>Main.mouseItem</c> y <c>Player.trashItem</c> en la misma foto hace
		/// que deshacer cualquier clic devuelva TODO a donde estaba, mano y papelera incluidas.
		/// </summary>
		/// <returns>true si se registro la entrada; false si <paramref name="edicion"/> no cambio
		/// nada (no se ensucia el historial con entradas vacias).</returns>
		public static bool CambiarObjetoDeSlotConCeldas(Item[] destino, int indice,
			CeldaDeObjeto[] celdas, Func<Item, Item, string> etiqueta, Action edicion)
		{
			if (edicion == null) {
				throw new ArgumentNullException("edicion");
			}
			if (etiqueta == null) {
				throw new ArgumentNullException("etiqueta");
			}
			if (celdas == null) {
				celdas = new CeldaDeObjeto[0];
			}

			Item antesItem = destino[indice] != null ? destino[indice].Clone() : new Item();
			SnapshotDeObjetos antesSlot = SnapshotDeObjetos.Tomar(destino, indice);
			Item[] antesCeldas = ClonarCeldas(celdas);

			edicion();

			SnapshotDeObjetos despuesSlot = SnapshotDeObjetos.Tomar(destino, indice);
			Item[] despuesCeldas = ClonarCeldas(celdas);

			bool cambioAlgo = !antesSlot.MismoContenidoQue(despuesSlot);
			for (int k = 0; !cambioAlgo && k < celdas.Length; k++) {
				cambioAlgo = !SnapshotDeObjetos.MismoContenido(antesCeldas[k], despuesCeldas[k]);
			}
			if (!cambioAlgo) {
				return false;
			}

			Item despuesItem = destino[indice] != null ? destino[indice].Clone() : new Item();
			string texto = etiqueta(antesItem, despuesItem);

			EstadoRanuraConCeldas antes = new EstadoRanuraConCeldas(antesSlot, antesCeldas, celdas);
			EstadoRanuraConCeldas despues = new EstadoRanuraConCeldas(despuesSlot, despuesCeldas, celdas);

			Pila.Registrar(new EntradaSnapshot<EstadoRanuraConCeldas>(
				texto, antes, despues, (EstadoRanuraConCeldas estado) => estado.Aplicar()));
			return true;
		}

		private static Item[] ClonarCeldas(CeldaDeObjeto[] celdas)
		{
			Item[] copia = new Item[celdas.Length];
			for (int k = 0; k < celdas.Length; k++) {
				Item leido = celdas[k].Leer();
				copia[k] = leido != null ? leido.Clone() : new Item();
			}
			return copia;
		}

		/// <summary>Foto conjunta de una ranura de array MAS sus celdas auxiliares, para poder
		/// aplicar las dos cosas atomicamente al deshacer o rehacer.</summary>
		private sealed class EstadoRanuraConCeldas
		{
			private readonly SnapshotDeObjetos _slot;
			private readonly Item[] _celdas;
			private readonly CeldaDeObjeto[] _definicionCeldas;

			public EstadoRanuraConCeldas(SnapshotDeObjetos slot, Item[] celdas, CeldaDeObjeto[] definicionCeldas)
			{
				_slot = slot;
				_celdas = celdas;
				_definicionCeldas = definicionCeldas;
			}

			public void Aplicar()
			{
				_slot.Aplicar();
				for (int k = 0; k < _celdas.Length; k++) {
					_definicionCeldas[k].Escribir(_celdas[k].Clone());
				}
			}
		}

		/// <summary>
		/// Version generica para datos que no son objetos: se le dan el valor de antes, el de
		/// despues y la funcion que sabe escribirlo. Sirve igual para un bool de desbloqueo, un
		/// int de dificultad o una estructura entera de apariencia.
		/// </summary>
		public static void CambiarValor<T>(string etiqueta, T antes, T despues, Action<T> aplicar)
		{
			Pila.Registrar(new EntradaSnapshot<T>(etiqueta, antes, despues, aplicar));
		}

		/// <summary>Deshace y devuelve el rotulo de lo deshecho (null si no habia nada).</summary>
		public static string Deshacer()
		{
			return Pila.Deshacer();
		}

		/// <summary>Rehace y devuelve el rotulo de lo rehecho (null si no habia nada).</summary>
		public static string Rehacer()
		{
			return Pila.Rehacer();
		}
	}
}
