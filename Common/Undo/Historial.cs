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
				Item a = _copias[k];
				Item b = otro._copias[k];
				if (a.type != b.type || a.stack != b.stack || a.prefix != b.prefix || a.favorited != b.favorited) {
					return false;
				}
			}
			return true;
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
