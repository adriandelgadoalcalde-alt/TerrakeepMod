using System;
using System.Collections.Generic;

namespace TerrakeepMod.Common.Undo
{
	/// <summary>
	/// Una accion deshacible ya registrada en <see cref="PilaDeSnapshots"/>. La pila no sabe NADA
	/// del dominio: solo sabe pedir "vuelve al estado de antes" o "vuelve al estado de despues".
	/// </summary>
	public interface IEntradaDeshacer
	{
		/// <summary>Rotulo legible de la accion ("Mover objeto de la ranura 6 a la 10").</summary>
		string Etiqueta { get; }

		/// <summary>Reescribe los datos reales con la foto tomada ANTES de la accion.</summary>
		void AplicarAntes();

		/// <summary>Reescribe los datos reales con la foto tomada DESPUES de la accion.</summary>
		void AplicarDespues();
	}

	/// <summary>
	/// Entrada concreta: guarda dos fotos completas del subconjunto de datos que toco la accion
	/// (<typeparamref name="T"/>) y la funcion que sabe volcar una de esas fotos sobre los datos
	/// reales.
	/// <para />
	/// <b>Por que dos fotos y no una operacion inversa.</b> El modelo de la app de escritorio
	/// (<c>Terrakeep.App/Services/UndoStack.cs</c>) guarda dos closures <c>Undo</c>/
	/// <c>Redo</c> encadenadas. Eso funciona alli porque el editor es el UNICO que toca el
	/// personaje: entre pulsar "deshacer" y "rehacer" no puede haber pasado nada mas. Dentro de
	/// una partida en marcha esa suposicion es falsa - el jugador recoge objetos, los NPCs
	/// atacan, el autoguardado escribe, los buffs caducan solos. Una closure del tipo "quita 1 a
	/// la pila del slot 3" aplicada sobre un estado que ya cambio corrompe los datos en silencio.
	/// Una foto completa del subconjunto, no: como mucho pisa un cambio ajeno, pero siempre deja
	/// un estado coherente y explicable.
	/// <para />
	/// Es exactamente el mismo patron que ya usa la propia app de escritorio para su deshacer
	/// local de 6 segundos al vaciar un contenedor (<c>ContainerViewModel.ClearAll</c> guarda un
	/// array con los objetos tal cual estaban, no una lista de operaciones que revertir).
	/// </summary>
	public sealed class EntradaSnapshot<T> : IEntradaDeshacer
	{
		private readonly T _antes;
		private readonly T _despues;
		private readonly Action<T> _aplicar;

		public EntradaSnapshot(string etiqueta, T antes, T despues, Action<T> aplicar)
		{
			if (string.IsNullOrWhiteSpace(etiqueta)) {
				throw new ArgumentException("Toda entrada del historial necesita un rotulo.", "etiqueta");
			}
			if (aplicar == null) {
				throw new ArgumentNullException("aplicar");
			}

			Etiqueta = etiqueta;
			_antes = antes;
			_despues = despues;
			_aplicar = aplicar;
		}

		public string Etiqueta { get; private set; }

		public void AplicarAntes()
		{
			_aplicar(_antes);
		}

		public void AplicarDespues()
		{
			_aplicar(_despues);
		}
	}

	/// <summary>
	/// Historial de deshacer/rehacer por snapshots, valido para CUALQUIER dato: objetos, buffs,
	/// desbloqueos, apariencia, tiles del mundo... La pila no conoce ninguno de esos tipos, solo
	/// <see cref="IEntradaDeshacer"/>.
	/// <para />
	/// Pensada para que los demas workstreams (Personaje, Builds, Exploracion) puedan colgar sus
	/// botones "Deshacer"/"Rehacer" de <see cref="PuedeDeshacer"/>/<see cref="PuedeRehacer"/> y
	/// de <see cref="EtiquetaDeshacer"/>/<see cref="EtiquetaRehacer"/> sin reimplementar nada.
	/// El punto de entrada real esta en <see cref="Historial"/>.
	/// </summary>
	/// <remarks>
	/// <b>Donde vive esta clase y por que.</b> No depende de Terraria ni de nada: seria
	/// perfectamente portable a <c>Terrakeep.Core</c>. Se ha dejado en el mod a proposito
	/// (ver README de <c>Common/Undo/</c>): lo unico realmente reutilizable serian estas ~100
	/// lineas, mientras que TODO lo que las hace utiles aqui (clonar <c>Item</c>, saber que un
	/// array es <c>Player.inventory</c>, limpiar el historial al cambiar de mundo) necesita
	/// tipos de Terraria y no podria acompañarlas. A cambio, meterlas en Core obligaria a
	/// regenerar y re-empaquetar <c>lib\Terrakeep.Core.dll</c> con
	/// <c>scripts\actualizar-core.ps1</c> en cada retoque del historial.
	/// </remarks>
	public sealed class PilaDeSnapshots
	{
		/// <summary>
		/// Tope de entradas guardadas. Cada entrada retiene copias profundas de objetos vivos del
		/// juego, asi que la pila no puede crecer sin limite durante una partida larga. Al pasarse,
		/// se descarta la entrada MAS ANTIGUA (que es la que menos probable es que se deshaga).
		/// </summary>
		public const int TopeDeEntradas = 50;

		// _entradas[0] es la mas antigua. _indice = cuantas entradas, contando desde el principio,
		// estan aplicadas ahora mismo. Deshacer() lo baja en 1; Rehacer() lo sube en 1. Registrar
		// una entrada nueva con _indice < Count trunca la cola de rehacer, igual que hace
		// cualquier editor real (y que la UndoStack de la app de escritorio).
		private readonly List<IEntradaDeshacer> _entradas = new List<IEntradaDeshacer>();
		private int _indice;

		/// <summary>Se dispara cada vez que cambia algo del historial (registro, deshacer,
		/// rehacer o limpieza), para que las UI que muestren botones se refresquen.</summary>
		public event Action Cambiado;

		public bool PuedeDeshacer {
			get { return _indice > 0; }
		}

		public bool PuedeRehacer {
			get { return _indice < _entradas.Count; }
		}

		/// <summary>Rotulo de lo que haria <see cref="Deshacer"/> ahora mismo, o null.</summary>
		public string EtiquetaDeshacer {
			get { return PuedeDeshacer ? _entradas[_indice - 1].Etiqueta : null; }
		}

		/// <summary>Rotulo de lo que haria <see cref="Rehacer"/> ahora mismo, o null.</summary>
		public string EtiquetaRehacer {
			get { return PuedeRehacer ? _entradas[_indice].Etiqueta : null; }
		}

		/// <summary>Numero de entradas vivas (deshechas incluidas).</summary>
		public int Cuenta {
			get { return _entradas.Count; }
		}

		/// <summary>
		/// Registra una accion YA EJECUTADA. La entrada tiene que traer las dos fotos: la de antes
		/// (tomada justo antes de tocar nada) y la de despues (tomada justo despues). Lo normal es
		/// no llamar aqui directamente sino usar <see cref="Historial"/>, que se encarga de tomar
		/// las dos fotos en el orden correcto.
		/// </summary>
		public void Registrar(IEntradaDeshacer entrada)
		{
			if (entrada == null) {
				throw new ArgumentNullException("entrada");
			}

			if (_indice < _entradas.Count) {
				_entradas.RemoveRange(_indice, _entradas.Count - _indice);
			}

			_entradas.Add(entrada);
			_indice++;

			if (_entradas.Count > TopeDeEntradas) {
				_entradas.RemoveAt(0);
				_indice--;
			}

			Notificar();
		}

		/// <summary>
		/// Deshace la ultima accion: vuelca sobre los datos reales la foto de ANTES.
		/// Devuelve el rotulo de lo deshecho, o null si no habia nada que deshacer.
		/// </summary>
		public string Deshacer()
		{
			if (!PuedeDeshacer) {
				return null;
			}

			_indice--;
			IEntradaDeshacer entrada = _entradas[_indice];
			entrada.AplicarAntes();
			Notificar();
			return entrada.Etiqueta;
		}

		/// <summary>
		/// Rehace la siguiente accion deshecha: vuelca la foto de DESPUES que se capturo en el
		/// momento de la accion original. Devuelve su rotulo, o null si no hay nada que rehacer.
		/// </summary>
		public string Rehacer()
		{
			if (!PuedeRehacer) {
				return null;
			}

			IEntradaDeshacer entrada = _entradas[_indice];
			entrada.AplicarDespues();
			_indice++;
			Notificar();
			return entrada.Etiqueta;
		}

		/// <summary>
		/// Tira el historial entero. Obligatorio al cambiar de personaje o de mundo: las fotos
		/// guardan referencias a los arrays reales de la partida anterior (por ejemplo
		/// <c>Player.inventory</c>, que se reasigna al cargar otro personaje), asi que deshacer
		/// despues de un cambio de partida escribiria sobre datos que ya no son los que se editaron.
		/// </summary>
		public void Limpiar()
		{
			if (_entradas.Count == 0 && _indice == 0) {
				return;
			}

			_entradas.Clear();
			_indice = 0;
			Notificar();
		}

		private void Notificar()
		{
			Action manejador = Cambiado;
			if (manejador != null) {
				manejador();
			}
		}
	}
}
