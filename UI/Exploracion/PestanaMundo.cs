using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// Pestaña "Este mundo": la ficha del mundo cargado y el cambio de dificultad EN VIVO.
	/// </summary>
	/// <remarks>
	/// El cambio de dificultad va en <b>dos pasos a proposito</b> (elegir modo -&gt; confirmar).
	/// No es un capricho de interfaz: a diferencia de la app de escritorio, que no escribe nada
	/// hasta que se lo pides, aqui el cambio queda grabado en el mundo en el siguiente guardado y
	/// no hay ventana de "descartar". El aviso de lo que eso significa esta SIEMPRE a la vista,
	/// antes de tocar nada, no en un dialogo que aparece despues.
	/// </remarks>
	public class PestanaMundo : UIElement
	{
		/// <summary>
		/// Reparto de las dos columnas, en PORCENTAJE del ancho real y no en pixeles fijos.
		/// <para />
		/// Antes eran 430 px fijos para la ficha, y lo que quedaba para la dificultad. En una
		/// ventana de 800 px eso dejaba ~318 px a la derecha, y ahi no cabian ni los botones de modo
		/// (140 px x 3 = 444) ni las tres lineas del aviso de permanencia (460 px cada una): las
		/// tres cosas se salian del marco por la derecha. Se vio en una captura real del juego.
		/// </summary>
		private const float FraccionIzquierda = 0.52f;

		/// <summary>Separacion entre las dos columnas.</summary>
		private const float SeparacionColumnas = 12f;

		/// <summary>Publicas para que la autoprueba de espaciado pueda repetir la MISMA medicion de
		/// texto real que usa <see cref="RecalcularAviso"/>, de forma independiente (a partir del
		/// texto sin partir y del ancho interior real de la caja), sin duplicar el numero a mano.</summary>
		public const float EscalaAviso = 0.72f;
		public const float EscalaAvisoSecundario = 0.68f;
		private const float PaddingCajaAviso = 8f;
		private const float SeparacionEntreAvisos = 6f;
		private const float SeparacionTrasCajaAviso = 10f;
		private const float AltoBotonConfirmar = 38f;
		private const float SeparacionTrasConfirmar = 8f;

		private int _modoElegido = -1;
		private readonly List<KeyValuePair<int, BotonTk>> _botonesModo = new List<KeyValuePair<int, BotonTk>>();
		private BotonTk _confirmar;
		private string _ultimoMensaje = "";

		private UIElement _derecha;
		private UIPanel _cajaAviso;
		private EtiquetaTk _etiquetaAviso;
		private EtiquetaTk _etiquetaEfecto;
		private EtiquetaTk _etiquetaDeshacer;
		private EtiquetaTk _mensaje;
		private float _yAntesDeAviso;

		/// <summary>Modo que esta elegido (pendiente de confirmar), o -1 si ninguno.</summary>
		public int ModoElegido => _modoElegido;

		/// <summary>El rectangulo naranja de aviso de permanencia, para que la autoprueba de
		/// espaciado pueda medir su geometria REAL (<c>GetDimensions</c>/<c>GetInnerDimensions</c>)
		/// y comprobar que el texto de verdad cabe dentro, no solo confiar en el propio calculo que
		/// ya lo coloca.</summary>
		public UIPanel CajaAviso => _cajaAviso;

		/// <summary>La ultima de las tres etiquetas del aviso (la que marca el borde inferior real
		/// del contenido), para la misma comprobacion.</summary>
		public EtiquetaTk EtiquetaDeshacer => _etiquetaDeshacer;

		/// <summary>El boton de confirmar, para comprobar que tampoco se solapa con la caja de
		/// aviso de encima.</summary>
		public BotonTk BotonConfirmar => _confirmar;

		public PestanaMundo()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirFicha();
			ConstruirDificultad();
			RecalcularAviso();
		}

		private void ConstruirFicha()
		{
			UIPanel caja = new UIPanel();
			caja.Width.Set(-SeparacionColumnas, FraccionIzquierda);
			caja.Height.Set(0f, 1f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(12f);
			Append(caja);

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.Ficha"), 0.95f, 380f, 26f);
			caja.Append(titulo);

			float y = 34f;
			y = Dato(caja, y, "Nombre", () => MundoActual.Nombre);
			y = Dato(caja, y, "Semilla", () => MundoActual.Semilla);
			y = Dato(caja, y, "Tamano", () => MundoActual.TamanoLegible);
			y = Dato(caja, y, "Tiles", () => MundoActual.TotalTiles.ToString("N0"));
			y = Dato(caja, y, "Modo", () => Idiomas.Texto("Exploracion.Mundo.ModoValor",
				MundoActual.ModoDeJuegoLegible, MundoActual.ModoDeJuego));
			y = Dato(caja, y, "Progreso", () => Idiomas.Texto(MundoActual.EsHardmode
				? "Exploracion.Hardmode"
				: "Exploracion.PreHardmode"));
			y = Dato(caja, y, "MalDelMundo", () => MundoActual.MalDelMundo);
			y = Dato(caja, y, "SemillasSecretas", () => MundoActual.SemillasSecretas);
			y = Dato(caja, y, "Aparicion",
				() => Idiomas.Texto("Exploracion.Mundo.Tile", MundoActual.PuntoDeAparicion));
			y = Dato(caja, y, "EstasEn",
				() => Idiomas.Texto("Exploracion.Mundo.Tile", MundoActual.PosicionDelJugador));
			y = Dato(caja, y, "Explorado",
				() => Idiomas.Texto("Exploracion.Mundo.Porcentaje",
					MundoActual.PorcentajeExplorado().ToString("0.0")));
			Dato(caja, y, "Autoguardado", () => Idiomas.Texto(Main.autoSave
				? "Exploracion.Mundo.Activado"
				: "Exploracion.Mundo.Desactivado"));
		}

		/// <summary>Una fila "rotulo: valor" de la ficha. Recibe la CLAVE de localizacion del
		/// rotulo, no el texto ya resuelto.</summary>
		private static float Dato(UIElement padre, float y, string clave, System.Func<string> valor)
		{
			EtiquetaTk nombre = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.Dato." + clave), 0.8f, 150f, 22f);
			nombre.ColorTexto = EstiloTk.TextoSuave;
			nombre.Top.Set(y, 0f);
			padre.Append(nombre);

			EtiquetaTk contenido = new EtiquetaTk(valor, 0.8f, 250f, 22f);
			contenido.Left.Set(140f, 0f);
			contenido.Top.Set(y, 0f);
			padre.Append(contenido);

			return y + 24f;
		}

		private void ConstruirDificultad()
		{
			UIElement derecha = new UIElement();
			derecha.Width.Set(0f, 1f - FraccionIzquierda);
			derecha.Height.Set(0f, 1f);
			derecha.HAlign = 1f;
			Append(derecha);
			_derecha = derecha;

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.Dificultad"), 0.95f, 500f, 26f);
			derecha.Append(titulo);

			EtiquetaTk actual = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.AhoraMismo", MundoActual.ModoDeJuegoLegible),
				0.85f, 500f, 24f);
			actual.Top.Set(30f, 0f);
			derecha.Append(actual);

			// DOS por fila y en porcentaje: tres botones de 140 px fijos no caben en la columna
			// derecha de una ventana de 800 (el tercero se salia del marco).
			float y = 62f;
			int i = 0;
			foreach (int modo in MundoActual.ModosDisponibles()) {
				int valor = modo;
				BotonTk boton = new BotonTk(MundoActual.NombreDeModo(modo), 0.85f);
				boton.Clave = modo.ToString();
				boton.Left.Set(0f, (i % 2) * 0.5f);
				boton.Top.Set(y + (i / 2) * 40f, 0f);
				boton.Width.Set(-6f, 0.5f);
				boton.Height.Set(34f, 0f);
				boton.AlPulsar += () => Elegir(valor);
				derecha.Append(boton);
				_botonesModo.Add(new KeyValuePair<int, BotonTk>(valor, boton));
				i++;
			}
			y += ((i + 1) / 2) * 40f + 8f;
			_yAntesDeAviso = y;

			// El aviso de permanencia va SIEMPRE visible, antes de tocar nada. Su altura NO es fija:
			// las tres frases se parten en las lineas que quepan en el ancho real de la caja (en una
			// sola linea median ~460 px y se salian por la derecha del marco, captura real del juego),
			// y el numero de lineas cambia con el idioma y con el estado del autoguardado (la frase
			// de permanencia es mas larga con el autoguardado apagado). Con una altura fija de 120 px
			// el texto se salia igualmente por ABAJO del propio rectangulo (visto en captura real):
			// aqui se mide el texto YA partido con la fuente real y se ajusta la caja entera (y lo
			// que va debajo, el boton de confirmar y el mensaje) a la altura que de verdad hace falta,
			// recalculado cada fotograma en <see cref="RecalcularAviso"/>.
			_cajaAviso = new UIPanel();
			_cajaAviso.Width.Set(0f, 1f);
			_cajaAviso.Top.Set(y, 0f);
			_cajaAviso.Height.Set(120f, 0f);
			_cajaAviso.BackgroundColor = new Color(92, 60, 30) * 0.92f;
			_cajaAviso.BorderColor = new Color(0, 0, 0, 0);
			_cajaAviso.SetPadding(PaddingCajaAviso);
			derecha.Append(_cajaAviso);

			_etiquetaAviso = new EtiquetaTk(
				() => TextoEnLineas("⚠  " + DificultadMundo.AvisoDePermanencia(), _cajaAviso, EscalaAviso),
				EscalaAviso, 0f, 22f);
			_etiquetaAviso.Width.Set(0f, 1f);
			_etiquetaAviso.ColorTexto = EstiloTk.TextoAviso;
			_cajaAviso.Append(_etiquetaAviso);

			_etiquetaEfecto = new EtiquetaTk(
				() => TextoEnLineas(DificultadMundo.AvisoDeEfecto(), _cajaAviso, EscalaAvisoSecundario),
				EscalaAvisoSecundario, 0f, 22f);
			_etiquetaEfecto.Width.Set(0f, 1f);
			_etiquetaEfecto.ColorTexto = new Color(235, 220, 200);
			_cajaAviso.Append(_etiquetaEfecto);

			_etiquetaDeshacer = new EtiquetaTk(
				() => TextoEnLineas(Idiomas.Texto("Exploracion.Mundo.AvisoDeshacer"), _cajaAviso, EscalaAvisoSecundario),
				EscalaAvisoSecundario, 0f, 22f);
			_etiquetaDeshacer.ColorTexto = new Color(235, 220, 200);
			_etiquetaDeshacer.Width.Set(0f, 1f);
			_cajaAviso.Append(_etiquetaDeshacer);

			_confirmar = new BotonTk(Idiomas.Texto("Exploracion.Mundo.EligeModo"), 0.85f);
			_confirmar.Width.Set(0f, 1f);
			_confirmar.Height.Set(AltoBotonConfirmar, 0f);
			_confirmar.Habilitado = false;
			_confirmar.AlPulsar += Confirmar;
			derecha.Append(_confirmar);

			_mensaje = new EtiquetaTk(() => _ultimoMensaje, 0.75f, 500f, 44f);
			_mensaje.ColorTexto = EstiloTk.TextoAviso;
			derecha.Append(_mensaje);

			// Con la altura real de la caja de aviso todavia sin calcular (depende de
			// GetInnerDimensions, que solo existe tras el primer Recalculate del arbol), se deja el
			// primer ajuste de verdad para el primer Update: RecalcularAviso() se reintenta sola cada
			// fotograma hasta que el ancho interior deja de ser cero.
		}

		/// <summary>
		/// Mide con la fuente REAL el texto ya partido de los tres avisos, ajusta la altura de
		/// <see cref="_cajaAviso"/> a lo que de verdad ocupan (nunca una cifra fija), y recoloca el
		/// boton de confirmar y el mensaje de resultado justo debajo. Se llama cada fotograma porque
		/// el texto puede cambiar (idioma, autoguardado) y el ancho de la caja tambien (redimensionar
		/// la ventana).
		/// </summary>
		private void RecalcularAviso()
		{
			if (_cajaAviso == null) {
				return;
			}

			float anchoInterior = _cajaAviso.GetInnerDimensions().Width;
			if (anchoInterior <= 0f) {
				// El layout todavia no se ha calculado (primerisimo fotograma): se reintenta solo en
				// el Update siguiente, sin tocar nada mientras tanto.
				return;
			}

			string avisoPartido = EtiquetaTk.PartirEnLineas(
				"⚠  " + DificultadMundo.AvisoDePermanencia(), anchoInterior, EscalaAviso);
			string efectoPartido = EtiquetaTk.PartirEnLineas(
				DificultadMundo.AvisoDeEfecto(), anchoInterior, EscalaAvisoSecundario);
			string deshacerPartido = EtiquetaTk.PartirEnLineas(
				Idiomas.Texto("Exploracion.Mundo.AvisoDeshacer"), anchoInterior, EscalaAvisoSecundario);

			var fuente = Terraria.GameContent.FontAssets.MouseText.Value;
			float altoAviso = fuente.MeasureString(avisoPartido).Y * EscalaAviso;
			float altoEfecto = fuente.MeasureString(efectoPartido).Y * EscalaAvisoSecundario;
			float altoDeshacer = fuente.MeasureString(deshacerPartido).Y * EscalaAvisoSecundario;

			float yEfecto = altoAviso + SeparacionEntreAvisos;
			float yDeshacer = yEfecto + altoEfecto + SeparacionEntreAvisos;
			float alturaInterior = yDeshacer + altoDeshacer;
			float alturaCaja = alturaInterior + PaddingCajaAviso * 2f;

			_etiquetaAviso.Top.Set(0f, 0f);
			_etiquetaEfecto.Top.Set(yEfecto, 0f);
			_etiquetaDeshacer.Top.Set(yDeshacer, 0f);
			_cajaAviso.Height.Set(alturaCaja, 0f);

			float yTrasCaja = _yAntesDeAviso + alturaCaja + SeparacionTrasCajaAviso;
			_confirmar.Top.Set(yTrasCaja, 0f);
			_mensaje.Top.Set(yTrasCaja + AltoBotonConfirmar + SeparacionTrasConfirmar, 0f);

			if (_derecha != null) {
				_derecha.Recalculate();
			}
		}

		/// <summary>
		/// Parte un texto en lineas que quepan en el ancho REAL del elemento, midiendolas con la
		/// fuente con la que se van a dibujar. Se hace en cada dibujado y no una vez porque el
		/// ancho depende de la resolucion y de la escala de interfaz del jugador, y porque el texto
		/// cambia con el idioma y con el estado del autoguardado.
		/// </summary>
		private static string TextoEnLineas(string texto, UIElement caja, float escala)
		{
			return EtiquetaTk.PartirEnLineas(texto, caja.GetInnerDimensions().Width, escala);
		}

		/// <summary>Elige un modo (primer paso). Publico porque lo usa la autoprueba.</summary>
		public void Elegir(int modo)
		{
			string motivo = DificultadMundo.MotivoParaNoPoder(modo);
			if (motivo != null) {
				_modoElegido = -1;
				_ultimoMensaje = Idiomas.Texto("Exploracion.Mundo.NoSePuede", motivo);
				RefrescarBotones();
				RegistroExploracion.Linea(Terrakeep.LogTag + " Dificultad: modo \"" +
					MundoActual.NombreDeModo(modo) + "\" NO disponible. " + motivo);
				return;
			}

			_modoElegido = modo;
			_ultimoMensaje = Idiomas.Texto("Exploracion.Mundo.VasAPasar",
				MundoActual.ModoDeJuegoLegible, MundoActual.NombreDeModo(modo));
			RefrescarBotones();
		}

		/// <summary>Aplica el modo elegido (segundo paso). Publico para la autoprueba.</summary>
		public void Confirmar()
		{
			if (_modoElegido < 0) {
				return;
			}

			string motivo;
			int pedido = _modoElegido;
			if (DificultadMundo.Aplicar(pedido, "panel de Exploración (dos pasos)", out motivo)) {
				_ultimoMensaje = Idiomas.Texto("Exploracion.Mundo.Hecho",
					MundoActual.NombreDeModo(pedido));
			}
			else {
				_ultimoMensaje = Idiomas.Texto("Exploracion.Mundo.NoSePudo", motivo);
			}

			_modoElegido = -1;
			RefrescarBotones();
		}

		private void RefrescarBotones()
		{
			foreach (KeyValuePair<int, BotonTk> par in _botonesModo) {
				bool esElActual = par.Key == MundoActual.ModoDeJuego;
				string motivo = DificultadMundo.MotivoParaNoPoder(par.Key);

				par.Value.Activo = esElActual || par.Key == _modoElegido;
				par.Value.Habilitado = motivo == null;
				int modoDelBoton = par.Key;
				par.Value.Ayuda = () => modoDelBoton == MundoActual.ModoDeJuego
					? Idiomas.Texto("Exploracion.Mundo.AyudaModoActual")
					: (DificultadMundo.MotivoParaNoPoder(modoDelBoton)
						?? Idiomas.Texto("Exploracion.Mundo.AyudaElegirModo"));
			}

			if (_confirmar == null) {
				return;
			}

			if (_modoElegido < 0) {
				_confirmar.FijarTexto(Idiomas.Texto("Exploracion.Mundo.EligeModo"));
				_confirmar.Habilitado = false;
			}
			else {
				_confirmar.FijarTexto(Idiomas.Texto("Exploracion.Mundo.Confirmar",
					MundoActual.NombreDeModo(_modoElegido)));
				_confirmar.Habilitado = true;
			}

			// Los rotulos de los botones de modo se fijan al construirlos: se vuelven a poner para
			// que cambien en vivo con el selector de idioma del area de Ajustes.
			foreach (KeyValuePair<int, BotonTk> par in _botonesModo) {
				par.Value.FijarTexto(MundoActual.NombreDeModo(par.Key));
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			// El modo del mundo puede cambiar sin pasar por aqui (deshacer con Ctrl+Z, otro panel),
			// asi que los botones se recalculan solos.
			RefrescarBotones();
			// Igual que los botones: el aviso de permanencia puede cambiar de numero de lineas sin
			// pasar por aqui (idioma, autoguardado), asi que su altura tambien se recalcula sola.
			RecalcularAviso();
		}
	}
}
