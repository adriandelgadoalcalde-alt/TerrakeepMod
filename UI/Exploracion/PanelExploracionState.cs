using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// Panel de Exploracion (WS6, tecla <b>P</b>): mini-mapa navegable, busqueda en el mundo real
	/// y datos/dificultad del mundo, todo sobre la partida cargada.
	/// <para />
	/// Misma familia visual que los paneles de Personaje (K) y Builds (L): el mismo marco, la
	/// misma paleta (<see cref="EstiloTk"/>), la misma barra de pestañas y el mismo boton de
	/// cerrar abajo a la derecha.
	/// </summary>
	/// <remarks>
	/// La MECANICA de apertura (atajo, <c>IngameFancyUI</c>, salto al mapa vanilla) vive entera en
	/// <see cref="PanelExploracionSystem"/> y el CONTENIDO aqui, a proposito: cuando los seis
	/// paneles del mod se fusionen en uno solo con pestañas, lo que hay que mover son estos
	/// <c>UIElement</c>, y la mecanica se tira entera.
	/// </remarks>
	public class PanelExploracionState : UIState
	{
		private const float AnchoMaximo = 1080f;
		private const float AltoMaximo = 700f;
		private const float AltoCabecera = 92f;
		private const float AltoBarraPestanas = 34f;
		private const float AltoPie = 42f;

		private UIPanel _marco;
		private UIElement _contenedor;
		private readonly List<BotonTk> _botonesPestana = new List<BotonTk>();
		private readonly List<string> _nombresPestana = new List<string>();
		private UIElement _pestanaActual;
		private int _indicePestana;

		private bool _medidasRegistradas;

		/// <summary>Pestaña abierta la ultima vez. Se guarda entre aperturas para que reabrir el
		/// panel vuelva a donde estabas.</summary>
		public static int UltimaPestana;

		/// <summary>La pestaña del mapa, para que la autoprueba pueda mirar el mini-mapa.</summary>
		public PestanaMapa Mapa { get; private set; }

		/// <summary>La pestaña de busqueda.</summary>
		public PestanaBusqueda Busqueda { get; private set; }

		/// <summary>La pestaña del mundo.</summary>
		public PestanaMundo Mundo { get; private set; }

		public override void OnInitialize()
		{
			_marco = new UIPanel();
			_marco.Width.Set(0f, 0.96f);
			_marco.MaxWidth.Set(AnchoMaximo, 0f);
			_marco.Height.Set(0f, 0.94f);
			_marco.MaxHeight.Set(AltoMaximo, 0f);
			_marco.HAlign = 0.5f;
			_marco.VAlign = 0.5f;
			_marco.BackgroundColor = EstiloTk.FondoPanel;
			_marco.SetPadding(10f);
			Append(_marco);

			_marco.Append(new CabeceraExploracion());

			ConstruirBarraPestanas();

			_contenedor = new UIElement();
			_contenedor.Width.Set(0f, 1f);
			_contenedor.Top.Set(AltoCabecera + AltoBarraPestanas + 8f, 0f);
			_contenedor.Height.Set(-(AltoCabecera + AltoBarraPestanas + 8f + AltoPie), 1f);
			_marco.Append(_contenedor);

			EtiquetaTk pie = new EtiquetaTk(
				() => "Arrastra el mini-mapa para moverte y usa la rueda para acercar. " +
					"Todo lo que ves sale de la partida cargada.",
				0.75f, 700f, 22f);
			pie.ColorTexto = EstiloTk.TextoSuave;
			pie.Left.Set(2f, 0f);
			pie.VAlign = 1f;
			_marco.Append(pie);

			BotonTk cerrar = new BotonTk("Cerrar (P)", 0.85f);
			cerrar.Width.Set(150f, 0f);
			cerrar.Height.Set(34f, 0f);
			cerrar.HAlign = 1f;
			cerrar.VAlign = 1f;
			cerrar.AlPulsar += () => PanelExploracionSystem.CerrarPanel("botón Cerrar");
			_marco.Append(cerrar);

			CambiarPestana(UltimaPestana);
		}

		private void ConstruirBarraPestanas()
		{
			_nombresPestana.Add("Mapa");
			_nombresPestana.Add("Búsqueda");
			_nombresPestana.Add("Este mundo");

			float ancho = 150f;
			float separacion = 6f;

			for (int i = 0; i < _nombresPestana.Count; i++) {
				int indice = i;
				BotonTk boton = new BotonTk(_nombresPestana[i], 0.85f);
				boton.Width.Set(ancho, 0f);
				boton.Height.Set(AltoBarraPestanas, 0f);
				boton.Left.Set(i * (ancho + separacion), 0f);
				boton.Top.Set(AltoCabecera, 0f);
				boton.AlPulsar += () => CambiarPestana(indice);
				_botonesPestana.Add(boton);
				_marco.Append(boton);
			}
		}

		/// <summary>Cambia de pestaña. Publico porque lo usa tambien la autoprueba.</summary>
		public void CambiarPestana(int indice)
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

			// Cada pestaña se construye de cero al entrar, igual que en el panel de Personaje: es
			// la unica forma de que ninguna referencia se quede apuntando a datos de otra partida.
			_pestanaActual = CrearPestana(indice);
			if (_pestanaActual != null) {
				_pestanaActual.Width.Set(0f, 1f);
				_pestanaActual.Height.Set(0f, 1f);
				_contenedor.Append(_pestanaActual);
			}

			_contenedor.Recalculate();

			RegistroExploracion.Linea(Terrakeep.LogTag + " Pestaña activa: \"" + _nombresPestana[indice] + "\".");
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

		/// <summary>Nombre de la pestaña abierta, para el log de las pruebas.</summary>
		public string NombrePestanaActual =>
			_indicePestana >= 0 && _indicePestana < _nombresPestana.Count
				? _nombresPestana[_indicePestana]
				: "(ninguna)";

		public int TotalPestanas => _nombresPestana.Count;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Mismo motivo que en el panel de Personaje (fallo real encontrado por WS1):
			// IngameFancyUI.OpenUIState pone Main.playerInventory = false, y con eso
			// Player.dropItemCheck vacia cada tick el objeto que lleves cogido con el raton. Aqui
			// no hay ranuras de objeto, pero el panel se puede abrir con algo ya cogido.
			Main.playerInventory = true;

			if (Main.gameMenu) {
				PanelExploracionSystem.CerrarPanel("se ha salido de la partida");
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			RegistrarMedidasUnaVez();
		}

		/// <summary>Deja en el log, una sola vez por apertura, las medidas reales del panel ya
		/// dibujado: es la evidencia de que esta puesto en pantalla de verdad y no solo construido
		/// en memoria.</summary>
		private void RegistrarMedidasUnaVez()
		{
			if (_medidasRegistradas || _marco == null) {
				return;
			}

			CalculatedStyle dim = _marco.GetDimensions();
			if (dim.Width <= 0f) {
				return;
			}

			_medidasRegistradas = true;
			RegistroExploracion.Linea(
				Terrakeep.LogTag + " Panel de Exploracion dibujado. Marco en coordenadas de pantalla: " +
				"x=" + (int)dim.X + " y=" + (int)dim.Y + " w=" + (int)dim.Width + " h=" + (int)dim.Height +
				". Resolucion " + Main.screenWidth + "x" + Main.screenHeight +
				", escala de interfaz " + Main.UIScale + ".");
		}

		/// <summary>Primer elemento del panel del tipo pedido. Lo usa la autoprueba para llegar a
		/// los controles sin tener que exponerlos uno a uno.</summary>
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

		/// <summary>Recuento y medidas reales de lo que hay puesto en la pestaña abierta.</summary>
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
