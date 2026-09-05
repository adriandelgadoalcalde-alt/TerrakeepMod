using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Undo;

namespace TerrakeepMod.UI.Ajustes
{
	/// <summary>
	/// Panel de Ajustes de Terrakeep (tecla <b>J</b>). Dos cosas, las dos de WS7:
	/// <list type="bullet">
	/// <item>el selector de idioma ES / EN / "el del juego", que cambia el idioma EN VIVO y lo
	/// demuestra sobre si mismo: todos los textos de este panel se repintan al vuelo;</item>
	/// <item>el estado del historial de deshacer/rehacer, con sus dos botones, que es la misma
	/// pila que usaran los demas paneles del mod.</item>
	/// </list>
	/// <b>Todo el texto de este panel sale del sistema de localizacion oficial de tModLoader</b>
	/// (<c>Localization\*_Mods.TerrakeepMod.hjson</c> + <c>Language.GetTextValue</c>, envuelto en
	/// <see cref="Idiomas.Texto"/>). No hay ni una cadena fija en pantalla: es el ejemplo real al
	/// que tienen que migrar los demas workstreams, explicado en <c>Localization\README.md</c>.
	/// </summary>
	public class PanelAjustesState : UIState
	{
		private UIText _titulo;
		private UIText _etiquetaIdioma;
		private UIText _culturaActiva;
		private UIText _etiquetaHistorial;
		private UIText _estadoHistorial;

		private UITextPanel<string> _botonSeguirElJuego;
		private UITextPanel<string> _botonEspanol;
		private UITextPanel<string> _botonIngles;
		private UITextPanel<string> _botonDeshacer;
		private UITextPanel<string> _botonRehacer;
		private UITextPanel<string> _botonCerrar;

		private static readonly Color ColorBotonNormal = new Color(63, 82, 151) * 0.9f;
		private static readonly Color ColorBotonActivo = new Color(90, 145, 90) * 0.95f;

		public override void OnInitialize()
		{
			UIPanel marco = new UIPanel();
			marco.Width.Set(520f, 0f);
			marco.Height.Set(400f, 0f);
			marco.HAlign = 0.5f;
			marco.VAlign = 0.5f;
			marco.BackgroundColor = new Color(33, 43, 79) * 0.92f;
			Append(marco);

			_titulo = new UIText("", 0.95f, true);
			_titulo.HAlign = 0.5f;
			_titulo.Top.Set(14f, 0f);
			marco.Append(_titulo);

			_etiquetaIdioma = new UIText("", 0.8f);
			_etiquetaIdioma.HAlign = 0.5f;
			_etiquetaIdioma.Top.Set(58f, 0f);
			marco.Append(_etiquetaIdioma);

			_botonSeguirElJuego = CrearBoton(marco, 0f, 88f, 150f);
			_botonSeguirElJuego.OnLeftClick += (evento, elemento) => Elegir(IdiomaDeTerrakeep.SeguirElJuego);

			_botonEspanol = CrearBoton(marco, 0.5f, 88f, 150f);
			_botonEspanol.OnLeftClick += (evento, elemento) => Elegir(IdiomaDeTerrakeep.Espanol);

			_botonIngles = CrearBoton(marco, 1f, 88f, 150f);
			_botonIngles.OnLeftClick += (evento, elemento) => Elegir(IdiomaDeTerrakeep.English);

			_culturaActiva = new UIText("", 0.72f);
			_culturaActiva.HAlign = 0.5f;
			_culturaActiva.Top.Set(140f, 0f);
			marco.Append(_culturaActiva);

			_etiquetaHistorial = new UIText("", 0.8f, true);
			_etiquetaHistorial.HAlign = 0.5f;
			_etiquetaHistorial.Top.Set(190f, 0f);
			marco.Append(_etiquetaHistorial);

			_estadoHistorial = new UIText("", 0.72f);
			_estadoHistorial.HAlign = 0.5f;
			_estadoHistorial.Top.Set(222f, 0f);
			marco.Append(_estadoHistorial);

			_botonDeshacer = CrearBoton(marco, 0.15f, 254f, 200f);
			_botonDeshacer.OnLeftClick += (evento, elemento) => HistorialSystem.DeshacerConAviso("boton Deshacer del panel de Ajustes");

			_botonRehacer = CrearBoton(marco, 0.85f, 254f, 200f);
			_botonRehacer.OnLeftClick += (evento, elemento) => HistorialSystem.RehacerConAviso("boton Rehacer del panel de Ajustes");

			_botonCerrar = CrearBoton(marco, 0.5f, 330f, 180f);
			_botonCerrar.OnLeftClick += (evento, elemento) => AjustesSystem.CerrarPanel("boton Cerrar");

			RefrescarTextos();
		}

		/// <summary>
		/// Al abrirse, el panel se engancha al aviso de cambio de idioma. Es el hilo completo:
		/// selector -&gt; <see cref="Idiomas.Elegir"/> -&gt; <c>LanguageManager.SetLanguage</c>
		/// -&gt; <c>ModSystem.OnLocalizationsLoaded</c> -&gt; <see cref="Idiomas.Cambiado"/> -&gt;
		/// este metodo. Asi el panel tambien se entera si el idioma lo cambia el menu del juego,
		/// no solo si lo cambia el propio selector.
		/// </summary>
		public override void OnActivate()
		{
			base.OnActivate();
			Idiomas.Cambiado += RefrescarTextos;
			RefrescarTextos();
		}

		public override void OnDeactivate()
		{
			Idiomas.Cambiado -= RefrescarTextos;
			base.OnDeactivate();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			// El historial puede cambiar desde fuera del panel (Ctrl+Z, otro panel del mod), asi
			// que su estado se repinta cada fotograma. Los textos de idioma no: esos solo cambian
			// cuando cambia el idioma, y de eso avisa el evento.
			RefrescarHistorial();
		}

		private void Elegir(IdiomaDeTerrakeep idioma)
		{
			Idiomas.Elegir(idioma);
			RefrescarTextos();
		}

		private void RefrescarTextos()
		{
			_titulo.SetText(Idiomas.Texto("Ajustes.Titulo"));
			_etiquetaIdioma.SetText(Idiomas.Texto("Ajustes.Idioma"));
			_culturaActiva.SetText(Idiomas.Texto("Ajustes.CulturaActiva", Idiomas.CulturaActiva));
			_etiquetaHistorial.SetText(Idiomas.Texto("Ajustes.Historial"));

			_botonSeguirElJuego.SetText(Idiomas.Texto("Ajustes.IdiomaSeguirElJuego"));
			_botonEspanol.SetText(Idiomas.Texto("Ajustes.IdiomaEspanol"));
			_botonIngles.SetText(Idiomas.Texto("Ajustes.IdiomaIngles"));
			_botonCerrar.SetText(Idiomas.Texto("Ajustes.Cerrar"));

			IdiomaDeTerrakeep actual = Idiomas.IdiomaConfigurado;
			_botonSeguirElJuego.BackgroundColor = actual == IdiomaDeTerrakeep.SeguirElJuego ? ColorBotonActivo : ColorBotonNormal;
			_botonEspanol.BackgroundColor = actual == IdiomaDeTerrakeep.Espanol ? ColorBotonActivo : ColorBotonNormal;
			_botonIngles.BackgroundColor = actual == IdiomaDeTerrakeep.English ? ColorBotonActivo : ColorBotonNormal;

			RefrescarHistorial();
		}

		private void RefrescarHistorial()
		{
			PilaDeSnapshots pila = Historial.Pila;

			_estadoHistorial.SetText(pila.Cuenta == 0
				? Idiomas.Texto("Ajustes.HistorialVacio")
				: Idiomas.Texto("Ajustes.HistorialEntradas", pila.Cuenta));

			_botonDeshacer.SetText(pila.PuedeDeshacer
				? Idiomas.Texto("Ajustes.DeshacerAlgo", pila.EtiquetaDeshacer)
				: Idiomas.Texto("Ajustes.Deshacer"));
			_botonRehacer.SetText(pila.PuedeRehacer
				? Idiomas.Texto("Ajustes.RehacerAlgo", pila.EtiquetaRehacer)
				: Idiomas.Texto("Ajustes.Rehacer"));

			// Sin CanUndo/CanRedo no hay forma de que el boton se vea desactivado en la UI de
			// Terraria (los UITextPanel no tienen estado "deshabilitado"), asi que se apaga el
			// color: gris apagado cuando no hay nada que deshacer/rehacer.
			_botonDeshacer.BackgroundColor = pila.PuedeDeshacer ? ColorBotonNormal : ColorBotonNormal * 0.45f;
			_botonRehacer.BackgroundColor = pila.PuedeRehacer ? ColorBotonNormal : ColorBotonNormal * 0.45f;
		}

		private static UITextPanel<string> CrearBoton(UIElement padre, float hAlign, float top, float ancho)
		{
			UITextPanel<string> boton = new UITextPanel<string>("", 0.72f, false);
			boton.Width.Set(ancho, 0f);
			boton.Height.Set(38f, 0f);
			boton.HAlign = hAlign;
			boton.Top.Set(top, 0f);
			boton.BackgroundColor = ColorBotonNormal;
			padre.Append(boton);
			return boton;
		}
	}
}
