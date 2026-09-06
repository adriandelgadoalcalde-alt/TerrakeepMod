using System.Collections.Generic;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// <b>Contenido</b> del area de Exploracion (WS6): mini-mapa navegable, busqueda en el mundo
	/// real y datos/dificultad del mundo, todo sobre la partida cargada.
	/// </summary>
	/// <remarks>
	/// Sale de la antigua <c>PanelExploracionState</c> al fusionar los seis paneles del mod en uno
	/// solo. WS6 ya habia dejado separada la MECANICA de apertura (atajo, <c>IngameFancyUI</c>,
	/// salto al mapa vanilla) en <see cref="PanelExploracionSystem"/>, asi que mover esto ha sido
	/// exactamente lo que se preveia: bajar la cabecera, la barra de sub-pestañas y el contenedor
	/// de un <c>UIState</c> a un <c>UIElement</c> corriente.
	/// </remarks>
	public class ContenidoExploracion : UIElement
	{
		private const float AltoCabecera = 84f;
		private const float AltoBarraPestanas = 30f;
		private const float SeparacionPestanas = 6f;

		private UIElement _contenedor;
		private readonly List<BotonTk> _botonesPestana = new List<BotonTk>();
		private readonly List<string> _clavesPestana = new List<string>();
		private UIElement _pestanaActual;
		private int _indicePestana;

		/// <summary>Sub-pestaña abierta la ultima vez. Se guarda entre aperturas para que volver al
		/// area de Exploracion devuelva a donde estabas.</summary>
		public static int UltimaPestana;

		/// <summary>La sub-pestaña del mapa, para que la autoprueba pueda mirar el mini-mapa.</summary>
		public PestanaMapa Mapa { get; private set; }

		/// <summary>La sub-pestaña de busqueda.</summary>
		public PestanaBusqueda Busqueda { get; private set; }

		/// <summary>La sub-pestaña del mundo.</summary>
		public PestanaMundo Mundo { get; private set; }

		public ContenidoExploracion()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			Append(new CabeceraExploracion());

			ConstruirBarraPestanas();

			_contenedor = new UIElement();
			_contenedor.Width.Set(0f, 1f);
			_contenedor.Top.Set(AltoCabecera + AltoBarraPestanas + 8f, 0f);
			_contenedor.Height.Set(-(AltoCabecera + AltoBarraPestanas + 8f), 1f);
			Append(_contenedor);

			CambiarPestana(UltimaPestana);
		}

		private void ConstruirBarraPestanas()
		{
			_clavesPestana.Add("Mapa");
			_clavesPestana.Add("Busqueda");
			_clavesPestana.Add("Mundo");

			// Mismo criterio que en el area de Personaje: ancho en porcentaje, no en pixeles fijos,
			// para que la barra se estire con el panel. Tres pestañas a un tercio cada una se veian
			// desproporcionadas al lado de las seis de Personaje, asi que se acotan a un ancho
			// razonable y se alinean a la izquierda, como las de arriba.
			float fraccion = 1f / 6f;

			for (int i = 0; i < _clavesPestana.Count; i++) {
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

		/// <summary>Cambia de sub-pestaña. Publico porque lo usan tambien la autoprueba y la lista
		/// de resultados de la busqueda (al pulsar un resultado se salta al mapa).</summary>
		public void CambiarPestana(int indice)
		{
			if (indice < 0 || indice >= _clavesPestana.Count) {
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

			// Cada pestaña se construye de cero al entrar, igual que en el area de Personaje: es la
			// unica forma de que ninguna referencia se quede apuntando a datos de otra partida.
			_pestanaActual = CrearPestana(indice);
			if (_pestanaActual != null) {
				_pestanaActual.Width.Set(0f, 1f);
				_pestanaActual.Height.Set(0f, 1f);
				_contenedor.Append(_pestanaActual);
			}

			_contenedor.Recalculate();

			RegistroExploracion.Linea(Terrakeep.LogTag + " Pestaña activa: \"" + NombrePestana(indice) + "\".");
		}

		/// <summary>Rotulo traducido de una de las tres sub-pestañas.</summary>
		private string NombrePestana(int indice)
		{
			if (indice < 0 || indice >= _clavesPestana.Count) {
				return "";
			}
			return Idiomas.Texto("Exploracion.Pestana." + _clavesPestana[indice]);
		}

		private UIElement CrearPestana(int indice)
		{
			switch (indice) {
				case 0:
					Mapa = new PestanaMapa();
					return Mapa;
				case 1:
					Busqueda = new PestanaBusqueda();
					return Busqueda;
				case 2:
					Mundo = new PestanaMundo();
					return Mundo;
				default:
					return null;
			}
		}

		/// <summary>Nombre de la sub-pestaña abierta, para el log de las pruebas.</summary>
		public string NombrePestanaActual =>
			_indicePestana >= 0 && _indicePestana < _clavesPestana.Count
				? NombrePestana(_indicePestana)
				: "(ninguna)";

		public int TotalPestanas => _clavesPestana.Count;

		/// <summary>Primer elemento del tipo pedido dentro de este area. Lo usa la autoprueba para
		/// llegar a los controles sin tener que exponerlos uno a uno.</summary>
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

		/// <summary>
		/// Pulsa DE VERDAD el boton cuyo texto empiece por <paramref name="texto"/>, disparando su
		/// <c>OnLeftClick</c> con un <see cref="UIMouseEvent"/> colocado en su centro real de
		/// pantalla. Devuelve la descripcion de lo pulsado, o null si no habia tal boton.
		/// </summary>
		/// <remarks>
		/// Es el mismo camino que recorre un clic de raton una vez que <c>UserInterface</c> ha
		/// resuelto sobre que elemento cae, y es la forma que uso WS4 para probar sus pildoras: la
		/// alternativa (llamar al metodo que el boton llama) no demostraria que el boton este
		/// realmente conectado ni que ocupe sitio en pantalla.
		/// </remarks>
		public string PulsarBoton(string texto)
		{
			BotonTk encontrado = null;
			ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (encontrado == null && boton != null && boton.Texto != null && boton.Texto.StartsWith(texto)) {
					encontrado = boton;
				}
			});

			if (encontrado == null) {
				return null;
			}

			CalculatedStyle dim = encontrado.GetDimensions();
			Microsoft.Xna.Framework.Vector2 centro =
				new Microsoft.Xna.Framework.Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			encontrado.LeftClick(new UIMouseEvent(encontrado, centro));

			return "\"" + encontrado.Texto + "\" (habilitado=" + encontrado.Habilitado +
				", en x=" + (int)dim.X + " y=" + (int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height +
				", clic en " + (int)centro.X + "," + (int)centro.Y + ")";
		}

		/// <summary>Recuento y medidas reales de lo que hay puesto en la sub-pestaña abierta.</summary>
		public string InformePestanaActual()
		{
			if (_pestanaActual == null) {
				return "sin pestaña";
			}

			int elementos = 0;
			int botones = 0;
			_pestanaActual.ExecuteRecursively(elemento => {
				elementos++;
				if (elemento is BotonTk) {
					botones++;
				}
			});

			CalculatedStyle dim = _pestanaActual.GetDimensions();
			return elementos + " elementos (" + botones + " botones); area x=" + (int)dim.X +
				" y=" + (int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height;
		}
	}
}
