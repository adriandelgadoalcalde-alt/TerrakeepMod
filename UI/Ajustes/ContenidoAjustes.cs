using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Ajustes
{
	/// <summary>
	/// <b>Contenido</b> del area de Ajustes (WS7): idioma en vivo, estado del historial de
	/// deshacer/rehacer con sus dos botones, y la lista real de atajos de teclado del mod.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Todo el texto de este area sale del sistema de localizacion oficial de tModLoader</b>
	/// (<c>Localization\*_Mods.TerrakeepMod.hjson</c> + <c>Language.GetTextValue</c>, envuelto en
	/// <see cref="Idiomas.Texto"/>). Es el ejemplo real al que tienen que migrar los demas
	/// paneles, explicado en <c>Localization\README.md</c>.
	/// </para>
	/// <para>
	/// Sale de la antigua <c>PanelAjustesState</c> al fusionar los seis paneles. Como Builds, este
	/// area usaba <c>UITextPanel</c> pelados con colores propios; ahora son <see cref="BotonTk"/>
	/// y la paleta de <see cref="EstiloTk"/>, asi que hereda la animacion de hover comun.
	/// </para>
	/// </remarks>
	public class ContenidoAjustes : UIElement
	{
		private const float AltoCaja = 118f;
		private const float Separacion = 10f;

		private EtiquetaTk _culturaActiva;
		private EtiquetaTk _estadoHistorial;

		private BotonTk _botonSeguirElJuego;
		private BotonTk _botonEspanol;
		private BotonTk _botonIngles;
		private BotonTk _botonDeshacer;
		private BotonTk _botonRehacer;

		public ContenidoAjustes()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirCajaIdioma(0f);
			ConstruirCajaHistorial(AltoCaja + Separacion);
			ConstruirCajaAtajos(2f * (AltoCaja + Separacion));

			RefrescarTextos();
		}

		/// <summary>Una caja con el mismo aspecto que las de las demas areas del panel.</summary>
		private UIPanel NuevaCaja(float arriba, float alto, string titulo)
		{
			UIPanel caja = new UIPanel();
			caja.Width.Set(0f, 1f);
			caja.Height.Set(alto, 0f);
			caja.Top.Set(arriba, 0f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(10f);
			Append(caja);

			EtiquetaTk cabecera = new EtiquetaTk(() => titulo, 0.9f, 400f, 26f);
			caja.Append(cabecera);
			return caja;
		}

		private void ConstruirCajaIdioma(float arriba)
		{
			UIPanel caja = NuevaCaja(arriba, AltoCaja, Idiomas.Texto("Ajustes.Idioma"));

			_botonSeguirElJuego = CrearBoton(caja, 0f, 32f);
			_botonSeguirElJuego.AlPulsar += () => Elegir(IdiomaDeTerrakeep.SeguirElJuego);

			_botonEspanol = CrearBoton(caja, 1f / 3f, 32f);
			_botonEspanol.AlPulsar += () => Elegir(IdiomaDeTerrakeep.Espanol);

			_botonIngles = CrearBoton(caja, 2f / 3f, 32f);
			_botonIngles.AlPulsar += () => Elegir(IdiomaDeTerrakeep.English);

			_culturaActiva = new EtiquetaTk(() => Idiomas.Texto("Ajustes.CulturaActiva", Idiomas.CulturaActiva),
				0.75f, 800f, 22f);
			_culturaActiva.ColorTexto = EstiloTk.TextoSuave;
			_culturaActiva.Top.Set(72f, 0f);
			caja.Append(_culturaActiva);
		}

		private void ConstruirCajaHistorial(float arriba)
		{
			UIPanel caja = NuevaCaja(arriba, AltoCaja, Idiomas.Texto("Ajustes.Historial"));

			_estadoHistorial = new EtiquetaTk(TextoEstadoHistorial, 0.78f, 800f, 22f);
			_estadoHistorial.ColorTexto = EstiloTk.TextoSuave;
			_estadoHistorial.Top.Set(28f, 0f);
			caja.Append(_estadoHistorial);

			_botonDeshacer = CrearBoton(caja, 0f, 54f, 0.5f);
			_botonDeshacer.AlPulsar += () =>
				HistorialSystem.DeshacerConAviso("botón Deshacer del área de Ajustes");

			_botonRehacer = CrearBoton(caja, 0.5f, 54f, 0.5f);
			_botonRehacer.AlPulsar += () =>
				HistorialSystem.RehacerConAviso("botón Rehacer del área de Ajustes");
		}

		/// <summary>
		/// Lista de atajos REALES del mod, leidos del perfil de controles del jugador. No son
		/// constantes escritas a mano: si el usuario reasigna una tecla en Ajustes &gt; Controles,
		/// aqui se ve la que tiene de verdad. Es nuevo de la fusion: ahora que las cinco teclas
		/// antiguas son atajos directos a una pestaña, hacia falta un sitio donde verlas.
		/// </summary>
		private void ConstruirCajaAtajos(float arriba)
		{
			UIPanel caja = NuevaCaja(arriba, AltoCaja + 44f, "Atajos de teclado");

			EtiquetaTk nota = new EtiquetaTk(
				() => "Cada tecla abre Terrakeep directamente en su pestaña. Reasignables en " +
					"Ajustes > Controles del juego.",
				0.72f, 900f, 20f);
			nota.ColorTexto = EstiloTk.TextoSuave;
			nota.Top.Set(26f, 0f);
			caja.Append(nota);

			EtiquetaTk lista = new EtiquetaTk(TextoAtajos, 0.75f, 900f, 24f);
			lista.Top.Set(50f, 0f);
			caja.Append(lista);

			EtiquetaTk historial = new EtiquetaTk(
				() => "Deshacer/Rehacer: " + TeclaDe("Deshacer") + " y " + TeclaDe("Rehacer") +
					" (con Ctrl pulsado).",
				0.75f, 900f, 24f);
			historial.ColorTexto = EstiloTk.TextoSuave;
			historial.Top.Set(76f, 0f);
			caja.Append(historial);

			EtiquetaTk icono = new EtiquetaTk(
				() => "También se abre con el icono de Terrakeep del inventario, arriba a la izquierda.",
				0.75f, 900f, 24f);
			icono.ColorTexto = EstiloTk.TextoSuave;
			icono.Top.Set(102f, 0f);
			caja.Append(icono);
		}

		private static string TextoAtajos()
		{
			return "Personaje " + TeclaDe("AbrirPanel") +
				"  ·  Librería " + TeclaDe("AbrirLibreria") +
				"  ·  Builds " + TeclaDe("AbrirBuilds") +
				"  ·  Investigación " + TeclaDe("AbrirInvestigacion") +
				"  ·  Exploración " + TeclaDe("AbrirExploracion") +
				"  ·  Ajustes " + TeclaDe("AbrirAjustes");
		}

		/// <summary>Tecla real que tiene asignada un atajo del mod ahora mismo, leida del perfil de
		/// controles del propio juego. Nunca un valor supuesto.</summary>
		private static string TeclaDe(string nombreDelAtajo)
		{
			PlayerInputProfile perfil = PlayerInput.CurrentProfile;
			if (perfil == null || !perfil.InputModes.ContainsKey(InputMode.Keyboard)) {
				return "[?]";
			}

			KeyConfiguration teclado = perfil.InputModes[InputMode.Keyboard];
			string clave = "TerrakeepMod/" + nombreDelAtajo;
			if (!teclado.KeyStatus.ContainsKey(clave)) {
				return "[?]";
			}

			List<string> teclas = teclado.KeyStatus[clave];
			return teclas == null || teclas.Count == 0 ? "[sin tecla]" : "[" + string.Join("+", teclas) + "]";
		}

		private BotonTk CrearBoton(UIElement padre, float izquierdaFraccion, float arriba, float fraccionAncho = 1f / 3f)
		{
			BotonTk boton = new BotonTk("", EstiloTk.EscalaBoton);
			boton.Width.Set(-EstiloTk.SeparacionPestanas, fraccionAncho);
			boton.Height.Set(34f, 0f);
			boton.Left.Set(0f, izquierdaFraccion);
			boton.Top.Set(arriba, 0f);
			padre.Append(boton);
			return boton;
		}

		/// <summary>
		/// Los textos se vuelven a pedir en cada fotograma, no al recibir un evento.
		/// <para />
		/// La version anterior (<c>PanelAjustesState</c>) se enganchaba a
		/// <see cref="Idiomas.Cambiado"/> desde <c>OnActivate</c>, que en un <c>UIState</c> corre
		/// seguro. Dentro del panel unico esto ya no es un <c>UIState</c> sino un elemento que se
		/// cuelga de una pestaña cuando el estado YA esta activo, y WS6 documento que en ese caso
		/// el motor se puede saltar <c>OnInitialize</c>/<c>OnActivate</c>: el enganche no se haria
		/// y el selector de idioma dejaria de repintarse. Preguntar cada fotograma cuesta cinco
		/// <c>Language.GetTextValue</c> y no puede quedarse desfasado - es el mismo criterio con
		/// el que se hizo <see cref="EtiquetaTk"/>.
		/// </summary>
		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			RefrescarTextos();
		}

		private void Elegir(IdiomaDeTerrakeep idioma)
		{
			Idiomas.Elegir(idioma);
			RefrescarTextos();
		}

		private void RefrescarTextos()
		{
			_botonSeguirElJuego.FijarTexto(Idiomas.Texto("Ajustes.IdiomaSeguirElJuego"));
			_botonEspanol.FijarTexto(Idiomas.Texto("Ajustes.IdiomaEspanol"));
			_botonIngles.FijarTexto(Idiomas.Texto("Ajustes.IdiomaIngles"));

			IdiomaDeTerrakeep actual = Idiomas.IdiomaConfigurado;
			_botonSeguirElJuego.Activo = actual == IdiomaDeTerrakeep.SeguirElJuego;
			_botonEspanol.Activo = actual == IdiomaDeTerrakeep.Espanol;
			_botonIngles.Activo = actual == IdiomaDeTerrakeep.English;

			RefrescarHistorial();
		}

		private static string TextoEstadoHistorial()
		{
			PilaDeSnapshots pila = Historial.Pila;
			return pila.Cuenta == 0
				? Idiomas.Texto("Ajustes.HistorialVacio")
				: Idiomas.Texto("Ajustes.HistorialEntradas", pila.Cuenta);
		}

		private void RefrescarHistorial()
		{
			PilaDeSnapshots pila = Historial.Pila;

			_botonDeshacer.FijarTexto(pila.PuedeDeshacer
				? Idiomas.Texto("Ajustes.DeshacerAlgo", pila.EtiquetaDeshacer)
				: Idiomas.Texto("Ajustes.Deshacer"));
			_botonRehacer.FijarTexto(pila.PuedeRehacer
				? Idiomas.Texto("Ajustes.RehacerAlgo", pila.EtiquetaRehacer)
				: Idiomas.Texto("Ajustes.Rehacer"));

			// Ahora que son BotonTk, "no hay nada que deshacer" se dice con el estado real del
			// widget (fondo apagado, texto gris y sin reaccionar al raton) en vez de bajandole el
			// alfa al color de fondo a mano, que es lo que hacia falta con un UITextPanel pelado.
			_botonDeshacer.Habilitado = pila.PuedeDeshacer;
			_botonRehacer.Habilitado = pila.PuedeRehacer;
		}

		/// <summary>Primer elemento del tipo pedido dentro de este area. Lo usa la autoprueba.</summary>
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
