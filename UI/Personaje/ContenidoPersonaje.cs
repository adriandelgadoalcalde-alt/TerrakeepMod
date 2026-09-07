using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// <b>Contenido</b> del area de Personaje: edicion EN VIVO de <c>Main.LocalPlayer</c>.
	/// <para />
	/// No hay ningun archivo de por medio ni ninguna copia intermedia: cada ranura, cada casilla
	/// y cada deslizador apunta al campo real del jugador que hay cargado en la partida, asi que
	/// lo que se toca aqui se ve al instante en el juego (y al reves: si el juego cambia algo, el
	/// panel lo enseña sin tener que reabrirlo).
	/// </summary>
	/// <remarks>
	/// Sale de la antigua <c>PanelPersonajeState</c> (WS1) al fusionar los seis paneles del mod en
	/// uno solo: aqui se ha quedado TODO el contenido real (cabecera del personaje + las seis
	/// sub-pestañas), y la mecanica de apertura (marco, <c>IngameFancyUI</c>, atajo, boton de
	/// cerrar) vive ahora una sola vez en <see cref="Panel.PanelTerrakeepState"/> y
	/// <c>PanelTerrakeepSystem</c>.
	/// </remarks>
	public class ContenidoPersonaje : UIElement
	{
		private const float AltoCabecera = 90f;
		private const float AltoBarraPestanas = 30f;
		private const float SeparacionPestanas = 6f;

		/// <summary>
		/// Alto de la fila de herramientas (papelera + editor de cantidad), compartida por las
		/// seis sub-pestañas aunque solo tenga sentido en las tres que enseñan objetos reales
		/// (Inventario, Almacenes, Equipo) - igual que en vanilla, donde la fila de iconos del HUD
		/// (bestiario, emotes, papelera) esta siempre puesta y no depende de que haya nada que
		/// tirar en ese instante.
		/// </summary>
		private const float AltoHerramientas = 34f;

		private UIElement _contenedor;
		private SlotPapeleraTk _papelera;
		private EditorCantidadTk _editorCantidad;
		private readonly List<BotonTk> _botonesPestana = new List<BotonTk>();
		private UIElement _pestanaActual;
		private int _indicePestana;

		/// <summary>Nombres internos de las seis sub-pestañas: son la ultima parte de su clave de
		/// localizacion (<c>Personaje.Pestana.&lt;clave&gt;</c>), no texto que se enseñe.</summary>
		public static readonly string[] ClavesPestana = {
			"Inventario", "Almacenes", "Equipo", "Buffs", "Apariencia", "Desbloqueos"
		};

		private static string NombrePestana(int indice)
		{
			if (indice < 0 || indice >= ClavesPestana.Length) {
				return "";
			}
			return Idiomas.Texto("Personaje.Pestana." + ClavesPestana[indice]);
		}

		/// <summary>Indice de la sub-pestaña abierta la ultima vez. Se guarda entre aperturas para
		/// que volver al area de Personaje devuelva a donde estabas.</summary>
		public static int UltimaPestana;

		public ContenidoPersonaje()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			Append(new CabeceraPersonaje());

			ConstruirBarraPestanas();
			ConstruirHerramientas();

			float arribaContenido = AltoCabecera + AltoBarraPestanas + AltoHerramientas + 12f;
			_contenedor = new UIElement();
			_contenedor.Width.Set(0f, 1f);
			_contenedor.Top.Set(arribaContenido, 0f);
			_contenedor.Height.Set(-arribaContenido, 1f);
			Append(_contenedor);

			CambiarPestana(PersonajeVivo.Acotar(UltimaPestana, 0, 5));
		}

		/// <summary>
		/// La papelera REAL de vanilla (<see cref="SlotPapeleraTk"/>) y el editor de cantidad
		/// (<see cref="EditorCantidadTk"/>), en una fila compartida por las seis sub-pestañas -
		/// asi que sirven tanto si el objeto que se quiere borrar o redimensionar esta en el
		/// Inventario como en un Almacen o en el Equipo, sin tener que repetir el control seis
		/// veces. Va DEBAJO de la barra de pestañas y no dentro de <see cref="SlotObjetoVanilla"/>
		/// a proposito: esa clase la esta tocando en paralelo otro agente (tooltip).
		/// </summary>
		private void ConstruirHerramientas()
		{
			float arriba = AltoCabecera + AltoBarraPestanas + 6f;

			EtiquetaTk etiquetaPapelera = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Herramientas.Papelera"), 0.75f, 90f, 24f);
			etiquetaPapelera.ColorTexto = EstiloTk.TextoSuave;
			etiquetaPapelera.Left.Set(0f, 0f);
			etiquetaPapelera.Top.Set(arriba + 6f, 0f);
			Append(etiquetaPapelera);

			// Escala 0.55 (~29x29) para que quepa en el alto de la fila junto a los botones del
			// editor de cantidad (26 px), sin dejar de leerse como el icono real del juego.
			_papelera = new SlotPapeleraTk(0.55f);
			_papelera.Left.Set(84f, 0f);
			_papelera.Top.Set(arriba, 0f);
			Append(_papelera);

			_editorCantidad = new EditorCantidadTk(this);
			_editorCantidad.Left.Set(130f, 0f);
			_editorCantidad.Top.Set(arriba, 0f);
			Append(_editorCantidad);
		}

		private void ConstruirBarraPestanas()
		{
			// Anchos en PORCENTAJE, no en pixeles fijos: el panel se estira con la pantalla y en
			// una ventana pequeña (800x720 en la maquina de pruebas) seis botones de 150 px fijos
			// se salian del marco.
			float fraccion = 1f / ClavesPestana.Length;

			for (int i = 0; i < ClavesPestana.Length; i++) {
				int indice = i;
				BotonTk boton = new BotonTk(NombrePestana(i), 0.8f);
				boton.EsPestana = true;
				boton.Width.Set(-SeparacionPestanas, fraccion);
				boton.Height.Set(AltoBarraPestanas, 0f);
				boton.Left.Set(0f, i * fraccion);
				boton.Top.Set(AltoCabecera, 0f);
				boton.AlPulsar += () => CambiarPestana(indice);
				_botonesPestana.Add(boton);
				Append(boton);
			}
		}

		private void CambiarPestana(int indice)
		{
			if (indice < 0 || indice >= ClavesPestana.Length) {
				return;
			}

			_indicePestana = indice;
			UltimaPestana = indice;

			for (int i = 0; i < _botonesPestana.Count; i++) {
				_botonesPestana[i].Activo = i == indice;
			}

			if (_pestanaActual != null) {
				_contenedor.RemoveChild(_pestanaActual);
				_pestanaActual = null;
			}

			// Cada pestaña se construye de cero al entrar en ella, igual que el propio panel se
			// construye de cero en cada apertura: es la unica forma de garantizar que ninguna
			// referencia se quede apuntando a un array viejo del jugador.
			_pestanaActual = CrearPestana(indice);
			if (_pestanaActual != null) {
				_pestanaActual.Width.Set(0f, 1f);
				_pestanaActual.Height.Set(0f, 1f);
				_contenedor.Append(_pestanaActual);
			}

			_contenedor.Recalculate();

			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Pestaña activa: \"{NombrePestana(indice)}\".");
		}

		/// <summary>Los rotulos de las seis sub-pestañas se vuelven a pedir en cada fotograma, para
		/// que cambien en vivo con el selector de idioma del area de Ajustes.</summary>
		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			for (int i = 0; i < _botonesPestana.Count; i++) {
				_botonesPestana[i].FijarTexto(NombrePestana(i));
			}
		}

		private static UIElement CrearPestana(int indice)
		{
			switch (indice) {
				case 0: return new PestanaInventario();
				case 1: return new PestanaAlmacenes();
				case 2: return new PestanaEquipo();
				case 3: return new PestanaBuffs();
				case 4: return new PestanaApariencia();
				case 5: return new PestanaDesbloqueos();
				default: return null;
			}
		}

		/// <summary>Nombre de la sub-pestaña abierta, para el log de las pruebas.</summary>
		public string NombrePestanaActual =>
			_indicePestana >= 0 && _indicePestana < ClavesPestana.Length
				? NombrePestana(_indicePestana)
				: "(ninguna)";

		/// <summary>Numero de sub-pestañas. Lo usa la autoprueba para recorrerlas todas.</summary>
		public int TotalPestanas => ClavesPestana.Length;

		/// <summary>Cambia de sub-pestaña desde fuera (autoprueba).</summary>
		public void IrAPestana(int indice)
		{
			CambiarPestana(indice);
		}

		/// <summary>
		/// Recuento y medidas REALES de lo que hay puesto en la sub-pestaña abierta, ya
		/// recalculado. Es la evidencia de que la pestaña no solo se ha construido en memoria sino
		/// que ocupa sitio de verdad en la pantalla del juego.
		/// </summary>
		public string InformePestanaActual()
		{
			if (_pestanaActual == null) {
				return "sin pestaña";
			}

			int elementos = 0;
			int ranuras = 0;
			CalculatedStyle primera = new CalculatedStyle();
			CalculatedStyle ultima = new CalculatedStyle();

			_pestanaActual.ExecuteRecursively(elemento => {
				elementos++;
				if (elemento is SlotObjetoVanilla) {
					CalculatedStyle dim = elemento.GetDimensions();
					if (ranuras == 0) {
						primera = dim;
					}
					ultima = dim;
					ranuras++;
				}
			});

			CalculatedStyle marcoPestana = _pestanaActual.GetDimensions();
			string detalleRanuras = ranuras == 0
				? "sin ranuras de objeto"
				: ranuras + " ranuras de objeto, la 1ª en x=" + (int)primera.X + " y=" + (int)primera.Y
					+ " " + (int)primera.Width + "x" + (int)primera.Height
					+ ", la ultima en x=" + (int)ultima.X + " y=" + (int)ultima.Y;

			return elementos + " elementos, " + detalleRanuras
				+ "; area de la pestaña x=" + (int)marcoPestana.X + " y=" + (int)marcoPestana.Y
				+ " " + (int)marcoPestana.Width + "x" + (int)marcoPestana.Height;
		}

		/// <summary>Primer elemento del tipo pedido, buscando por todo el arbol de este area. Lo
		/// usa la autoprueba para llegar a los controles propios (deslizadores, campos de texto)
		/// sin tener que exponerlos uno a uno.</summary>
		public T BuscarPrimero<T>() where T : UIElement
		{
			T encontrado = null;
			ExecuteRecursively(elemento => {
				if (encontrado == null && elemento is T) {
					encontrado = (T)elemento;
				}
			});
			return encontrado;
		}
	}
}
