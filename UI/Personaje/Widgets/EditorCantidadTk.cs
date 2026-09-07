using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.Common.Undo;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// Edita en vivo la CANTIDAD (<c>Item.stack</c>) del objeto que el raton este pasando por
	/// encima ahora mismo, dentro de cualquier <see cref="SlotObjetoVanilla"/> del area de
	/// Personaje. Terraria vainilla no tiene ningun control asi (para bajar una pila de 999
	/// pociones a 50 solo existe soltar de una en una o partir por la mitad), asi que es un
	/// control propio del mod, con la estetica ya usada (<see cref="BotonTk"/>,
	/// <see cref="CampoTextoTk"/>).
	/// <para />
	/// <b>Como sabe sobre que objeto esta actuando.</b> No hace falta ningun clic especial ni
	/// tocar <see cref="SlotObjetoVanilla"/> (lo esta editando en paralelo otro agente para el
	/// tooltip): cada <see cref="Update"/> recorre el arbol de <paramref name="raiz"/> buscando un
	/// <see cref="SlotObjetoVanilla"/> con <c>IsMouseHovering</c> a true y una pila mayor que 1, y
	/// se queda "enganchado" a ese hasta que el raton pase por encima de OTRO slot apilable -
	/// pasar el raton por hueco vacio de por medio (por ejemplo para venir hasta este mismo
	/// control) no lo suelta, que es precisamente lo que hace falta para poder escribir el numero
	/// sin perder de vista que objeto se esta editando.
	/// <para />
	/// <see cref="SlotObjetoVanilla.ObjetoActual"/> devuelve el <see cref="Item"/> REAL que vive
	/// en el array del jugador (es una clase, no una estructura): escribir en su <c>.stack</c> lo
	/// cambia ahi mismo, sin necesitar el array ni el indice de origen.
	/// </summary>
	public class EditorCantidadTk : UIElement
	{
		/// <summary>Ancho real de la etiqueta, en pixeles. El nombre del objeto se recorta
		/// MIDIENDO con la fuente real del juego hasta que el texto entero quepa aqui - no un
		/// numero fijo de caracteres: con nombres largos o con el juego en ingles (la plantilla es
		/// mas larga: "Quantity of ..." vs "Cantidad de ...") un recorte por caracteres se sale
		/// del hueco igual, y este panel ya se ha encontrado varias veces con exactamente ese fallo
		/// (ver bitacora.md, "diez pasadas de revision visual") por no medir con la fuente real.</summary>
		private const float AnchoEtiqueta = 360f;

		private const float EscalaEtiqueta = 0.75f;

		private readonly UIElement _raiz;
		private SlotObjetoVanilla _objetivo;

		private readonly EtiquetaTk _etiqueta;
		private readonly BotonTk _menos;
		private readonly CampoTextoTk _campo;
		private readonly BotonTk _mas;
		private readonly BotonTk _aplicar;

		/// <summary>Objeto sobre el que actuaria ahora mismo un clic en "-"/"+"/Aplicar, o null si
		/// no hay ninguno enganchado todavia. Lo lee la autoprueba.</summary>
		public Item ObjetivoActual => _objetivo != null ? _objetivo.ObjetoActual : null;

		/// <summary>Los tres botones y el campo, expuestos para que la autoprueba los pulse por su
		/// ruta REAL (<c>BotonTk.LeftClick</c>), igual que hace <c>PanelTerrakeepState.PulsarBoton</c>
		/// con los botones del marco.</summary>
		public BotonTk BotonMenos => _menos;
		public BotonTk BotonMas => _mas;
		public BotonTk BotonAplicar => _aplicar;
		public CampoTextoTk Campo => _campo;

		/// <param name="raiz">Elemento en el que buscar el slot con el raton encima. Es
		/// <see cref="ContenidoPersonaje"/> en si (no una de sus sub-pestañas): como cambia de
		/// sub-pestaña reconstruyendo <c>_pestanaActual</c> entero, buscar siempre desde la raiz
		/// estable es lo unico que sigue viendo el arbol correcto tras un cambio de pestaña.</param>
		public EditorCantidadTk(UIElement raiz)
		{
			_raiz = raiz;

			// Ancho fijo, no en porcentaje: los hijos se colocan con desplazamientos en pixeles
			// desde este mismo origen (igual que el resto de filas del panel), y un ancho al 100%
			// del contenedor dejaria el rectangulo de este elemento mucho mas ancho que su
			// contenido real cuando se coloca con un Left desplazado (ver ConstruirHerramientas).
			Width.Set(580f, 0f);
			Height.Set(26f, 0f);

			_etiqueta = new EtiquetaTk(TextoEtiqueta, EscalaEtiqueta, AnchoEtiqueta, 24f);
			_etiqueta.ColorTexto = EstiloTk.TextoSuave;
			_etiqueta.Left.Set(0f, 0f);
			_etiqueta.Top.Set(4f, 0f);
			Append(_etiqueta);

			_menos = new BotonTk("-", 0.85f);
			_menos.Width.Set(26f, 0f);
			_menos.Height.Set(26f, 0f);
			_menos.Left.Set(AnchoEtiqueta + 6f, 0f);
			_menos.AlPulsar += () => Ajustar(-1);
			Append(_menos);

			_campo = new CampoTextoTk(() => Idiomas.Texto("Personaje.Herramientas.CantidadPista"), 5, 0.8f);
			_campo.SoloNumeros = true;
			_campo.Width.Set(56f, 0f);
			_campo.Height.Set(26f, 0f);
			_campo.Left.Set(AnchoEtiqueta + 38f, 0f);
			_campo.AlConfirmar += _ => Aplicar();
			Append(_campo);

			_mas = new BotonTk("+", 0.85f);
			_mas.Width.Set(26f, 0f);
			_mas.Height.Set(26f, 0f);
			_mas.Left.Set(AnchoEtiqueta + 100f, 0f);
			_mas.AlPulsar += () => Ajustar(1);
			Append(_mas);

			_aplicar = new BotonTk("", 0.78f);
			_aplicar.Width.Set(80f, 0f);
			_aplicar.Height.Set(26f, 0f);
			_aplicar.Left.Set(AnchoEtiqueta + 132f, 0f);
			_aplicar.AlPulsar += Aplicar;
			Append(_aplicar);

			foreach (BotonTk boton in new[] { _menos, _mas, _aplicar }) {
				boton.Ayuda = () => Idiomas.Texto("Personaje.Herramientas.CantidadAyuda");
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			BuscarObjetivo();

			Item objeto = ObjetivoActual;
			bool hay = objeto != null && !objeto.IsAir && objeto.stack > 1;

			_menos.Habilitado = hay;
			_mas.Habilitado = hay;
			_aplicar.Habilitado = hay;
			_aplicar.FijarTexto(Idiomas.Texto("Personaje.Herramientas.CantidadAplicar"));

			// Mientras el jugador esta escribiendo no se le pisa lo que lleva tecleado.
			if (hay && !_campo.Enfocado) {
				_campo.FijarTextoSilencioso(objeto.stack.ToString());
			}
			else if (!hay) {
				_campo.FijarTextoSilencioso("");
			}
		}

		/// <summary>
		/// Busca un slot apilable con el raton encima. Si lo encuentra se engancha a el; si no,
		/// se queda con el que tuviera antes (asi se puede venir hasta este control sin perder de
		/// vista que objeto se esta editando).
		/// </summary>
		private void BuscarObjetivo()
		{
			if (_raiz == null) {
				return;
			}

			_raiz.ExecuteRecursively(elemento => {
				SlotObjetoVanilla slot = elemento as SlotObjetoVanilla;
				if (slot != null && slot.IsMouseHovering && !slot.ObjetoActual.IsAir && slot.ObjetoActual.stack > 1) {
					_objetivo = slot;
				}
			});

			// Si el objeto enganchado desaparecio (se trasteo, se vendio, se tiro...) se suelta,
			// para no seguir mostrando datos de un objeto que ya no esta.
			if (_objetivo != null && _objetivo.ObjetoActual.IsAir) {
				_objetivo = null;
			}
		}

		private string TextoEtiqueta()
		{
			Item objeto = ObjetivoActual;
			if (objeto == null || objeto.IsAir || objeto.stack <= 1) {
				return RecortarAAncho(Idiomas.Texto("Personaje.Herramientas.CantidadSinObjetivo"));
			}

			string nombre = objeto.Name ?? "";
			string texto = Idiomas.Texto("Personaje.Herramientas.CantidadObjetivo", nombre, objeto.stack, objeto.maxStack);

			// Recorte MIDIENDO con la fuente real, no un numero fijo de caracteres: la plantilla
			// cambia de largo con el idioma ("Cantidad de..." vs "Quantity of...") y un objeto
			// puede tener un nombre muy largo (con prefijo). Se quita de 4 en 4 caracteres del
			// NOMBRE (dejando sitio para los "...") hasta que la linea entera quepa en el ancho
			// real de la etiqueta - ver la nota de <see cref="AnchoEtiqueta"/>.
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			while (nombre.Length > 3 && fuente.MeasureString(texto).X * EscalaEtiqueta > AnchoEtiqueta) {
				nombre = nombre.Substring(0, nombre.Length - 4) + "...";
				texto = Idiomas.Texto("Personaje.Herramientas.CantidadObjetivo", nombre, objeto.stack, objeto.maxStack);
			}

			return texto;
		}

		/// <summary>Recorta un texto SIN partes dinamicas al ancho real de la etiqueta, midiendo
		/// con la fuente real del juego. Se usa para el aviso de "sin objetivo".</summary>
		private static string RecortarAAncho(string texto)
		{
			DynamicSpriteFont fuente = FontAssets.MouseText.Value;
			while (texto.Length > 3 && fuente.MeasureString(texto).X * EscalaEtiqueta > AnchoEtiqueta) {
				texto = texto.Substring(0, texto.Length - 4) + "...";
			}
			return texto;
		}

		private void Ajustar(int delta)
		{
			Item objeto = ObjetivoActual;
			if (objeto == null || objeto.IsAir) {
				return;
			}

			EscribirCantidad(objeto, objeto.stack + delta);
		}

		private void Aplicar()
		{
			Item objeto = ObjetivoActual;
			if (objeto == null || objeto.IsAir) {
				return;
			}

			EscribirCantidad(objeto, _campo.ComoEntero(objeto.stack));
		}

		/// <summary>
		/// Escribe la cantidad de verdad, acotada al maximo REAL del objeto (nunca a un tope fijo
		/// de 999: hay objetos con <c>maxStack</c> mas bajo, y algunos mods traen mas alto), y deja
		/// el cambio deshacible con Ctrl+Z igual que el resto del panel.
		/// </summary>
		private void EscribirCantidad(Item objeto, int pedido)
		{
			int antes = objeto.stack;
			int nuevo = PersonajeVivo.Acotar(pedido, 1, objeto.maxStack);

			if (nuevo == antes) {
				_campo.FijarTextoSilencioso(nuevo.ToString());
				return;
			}

			objeto.stack = nuevo;
			_campo.FijarTextoSilencioso(nuevo.ToString());

			// CambiarValor y no CambiarObjetos: el objetivo es el Item REAL que ya vive en el
			// array del jugador (referencia, no copia), asi que no hace falta el array ni el
			// indice de origen para deshacer - solo saber volver a escribir el numero de antes.
			Item objetoCerrado = objeto;
			Historial.CambiarValor(
				Idiomas.Texto("Personaje.Herramientas.CantidadHistorial", objeto.Name, antes, nuevo),
				antes, nuevo, (int valor) => { objetoCerrado.stack = valor; });

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag +
				" Cantidad cambiada en vivo: \"" + objeto.Name + "\" stack " + antes + " -> " + nuevo +
				" (maxStack=" + objeto.maxStack + ").");
		}
	}
}
