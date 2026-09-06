using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Investigacion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Investigacion
{
	/// <summary>
	/// Panel de Investigacion (WS5), tecla <b>I</b>: cuanto llevas investigado de cada carpeta de
	/// la Libreria y como completarlo, escribiendo por la API oficial del Modo Viaje.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Esta clase es <b>solo el marco</b>: el fondo, el titulo, la nota de la tecla y el boton de
	/// cerrar. Todo el contenido esta en <see cref="ContenidoInvestigacion"/>, que es un
	/// <c>UIElement</c> suelto. La separacion es deliberada: cuando los seis paneles del mod se
	/// fusionen en uno solo con pestañas y una unica tecla, se tira esta clase entera y el
	/// contenido se cuelga de la pestaña sin tocarlo.
	/// </para>
	/// <para>
	/// Las medidas y los colores son los mismos que los del panel de Personaje (WS1): mismo marco
	/// al 96%/94% de la pantalla con tope de 1080x700, mismo <see cref="EstiloTk.FondoPanel"/>,
	/// mismo boton de cerrar abajo a la derecha con la tecla entre parentesis. El mod tiene que
	/// verse como una sola aplicacion.
	/// </para>
	/// </remarks>
	public class PanelInvestigacionState : UIState
	{
		private const float AnchoMaximo = 1080f;
		private const float AltoMaximo = 700f;
		private const float AltoCabecera = 54f;
		private const float AltoPie = 42f;

		private UIPanel _marco;
		private ContenidoInvestigacion _contenido;
		private bool _medidasRegistradas;

		/// <summary>El contenido real del panel. Lo usa la autoprueba.</summary>
		public ContenidoInvestigacion Contenido => _contenido;

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

			UIText titulo = new UIText("Terrakeep - Investigacion", 1f, true);
			titulo.Left.Set(2f, 0f);
			titulo.Top.Set(2f, 0f);
			_marco.Append(titulo);

			EtiquetaTk nota = new EtiquetaTk(NotaCabecera, 0.75f, 700f, 20f);
			nota.ColorTexto = EstiloTk.TextoSuave;
			nota.Left.Set(2f, 0f);
			nota.Top.Set(30f, 0f);
			_marco.Append(nota);

			_contenido = new ContenidoInvestigacion();
			_contenido.Top.Set(AltoCabecera, 0f);
			_contenido.Height.Set(-(AltoCabecera + AltoPie), 1f);
			_marco.Append(_contenido);

			BotonTk cerrar = new BotonTk("Cerrar (I)", 0.85f);
			cerrar.Width.Set(150f, 0f);
			cerrar.Height.Set(34f, 0f);
			cerrar.HAlign = 1f;
			cerrar.VAlign = 1f;
			cerrar.AlPulsar += () => PanelInvestigacionSystem.CerrarPanel("boton Cerrar");
			_marco.Append(cerrar);
		}

		private static string NotaCabecera()
		{
			string tecla = PanelInvestigacionSystem.TeclaMenuDelJuego();
			string donde = tecla == null
				? "el menu del Modo Viaje del juego"
				: "el menu del Modo Viaje del juego (tecla " + tecla + ")";
			return "Se escribe por la via oficial del juego, asi que " + donde +
				" refleja lo mismo. Ctrl+Z deshace cualquier accion de aqui.";
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Si el personaje desaparece (salir al menu, morir en extremo...) el panel no tiene
			// nada que enseñar y se cierra solo antes de petar leyendo nulos. Mismo cuidado que el
			// panel de Personaje de WS1.
			if (Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				PanelInvestigacionSystem.CerrarPanel("el jugador ha dejado de estar disponible");
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			RegistrarMedidasUnaVez();
		}

		/// <summary>Deja en el log, una sola vez por apertura, las medidas reales del panel ya
		/// dibujado. Es la evidencia de que esta puesto de verdad en pantalla y no solo construido
		/// en memoria - mismo criterio con el que se cerraron WS0, WS1 y WS4.</summary>
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
			RegistroInvestigacion.Linea(
				$"{Terrakeep.LogTag} Panel de Investigacion dibujado. Marco en coordenadas de pantalla: " +
				$"x={(int)dim.X} y={(int)dim.Y} w={(int)dim.Width} h={(int)dim.Height}. " +
				$"Resolucion {Main.screenWidth}x{Main.screenHeight}, escala de interfaz {Main.UIScale}. " +
				$"Contenido: {_contenido.Informe()}");
		}
	}
}
