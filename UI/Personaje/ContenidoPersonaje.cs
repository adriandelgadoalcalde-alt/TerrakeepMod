using System.Collections.Generic;
using Terraria.UI;
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

		private UIElement _contenedor;
		private readonly List<BotonTk> _botonesPestana = new List<BotonTk>();
		private readonly List<string> _nombresPestana = new List<string>();
		private UIElement _pestanaActual;
		private int _indicePestana;

		/// <summary>Indice de la sub-pestaña abierta la ultima vez. Se guarda entre aperturas para
		/// que volver al area de Personaje devuelva a donde estabas.</summary>
		public static int UltimaPestana;

		public ContenidoPersonaje()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			Append(new CabeceraPersonaje());

			ConstruirBarraPestanas();

			_contenedor = new UIElement();
			_contenedor.Width.Set(0f, 1f);
			_contenedor.Top.Set(AltoCabecera + AltoBarraPestanas + 8f, 0f);
			_contenedor.Height.Set(-(AltoCabecera + AltoBarraPestanas + 8f), 1f);
			Append(_contenedor);

			CambiarPestana(PersonajeVivo.Acotar(UltimaPestana, 0, 5));
		}

		private void ConstruirBarraPestanas()
		{
			_nombresPestana.Add("Inventario");
			_nombresPestana.Add("Almacenes");
			_nombresPestana.Add("Equipo");
			_nombresPestana.Add("Buffs");
			_nombresPestana.Add("Apariencia");
			_nombresPestana.Add("Desbloqueos");

			// Anchos en PORCENTAJE, no en pixeles fijos: el panel se estira con la pantalla y en
			// una ventana pequeña (800x720 en la maquina de pruebas) seis botones de 150 px fijos
			// se salian del marco.
			float fraccion = 1f / _nombresPestana.Count;

			for (int i = 0; i < _nombresPestana.Count; i++) {
				int indice = i;
				BotonTk boton = new BotonTk(_nombresPestana[i], 0.8f);
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
			if (indice < 0 || indice >= _nombresPestana.Count) {
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

			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Pestaña activa: \"{_nombresPestana[indice]}\".");
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
			_indicePestana >= 0 && _indicePestana < _nombresPestana.Count
				? _nombresPestana[_indicePestana]
				: "(ninguna)";

		/// <summary>Numero de sub-pestañas. Lo usa la autoprueba para recorrerlas todas.</summary>
		public int TotalPestanas => _nombresPestana.Count;

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
