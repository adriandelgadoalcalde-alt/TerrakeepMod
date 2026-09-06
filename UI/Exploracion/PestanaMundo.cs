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
		private const float AnchoIzquierda = 430f;

		private int _modoElegido = -1;
		private readonly List<KeyValuePair<int, BotonTk>> _botonesModo = new List<KeyValuePair<int, BotonTk>>();
		private BotonTk _confirmar;
		private string _ultimoMensaje = "";

		/// <summary>Modo que esta elegido (pendiente de confirmar), o -1 si ninguno.</summary>
		public int ModoElegido => _modoElegido;

		public PestanaMundo()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirFicha();
			ConstruirDificultad();
		}

		private void ConstruirFicha()
		{
			UIPanel caja = new UIPanel();
			caja.Width.Set(AnchoIzquierda, 0f);
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
			derecha.Width.Set(-(AnchoIzquierda + 12f), 1f);
			derecha.Height.Set(0f, 1f);
			derecha.HAlign = 1f;
			Append(derecha);

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.Dificultad"), 0.95f, 500f, 26f);
			derecha.Append(titulo);

			EtiquetaTk actual = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.AhoraMismo", MundoActual.ModoDeJuegoLegible),
				0.85f, 500f, 24f);
			actual.Top.Set(30f, 0f);
			derecha.Append(actual);

			float y = 62f;
			float ancho = 140f;
			int i = 0;
			foreach (int modo in MundoActual.ModosDisponibles()) {
				int valor = modo;
				BotonTk boton = new BotonTk(MundoActual.NombreDeModo(modo), 0.85f);
				boton.Clave = modo.ToString();
				boton.Left.Set((i % 3) * (ancho + 8f), 0f);
				boton.Top.Set(y + (i / 3) * 40f, 0f);
				boton.Width.Set(ancho, 0f);
				boton.Height.Set(34f, 0f);
				boton.AlPulsar += () => Elegir(valor);
				derecha.Append(boton);
				_botonesModo.Add(new KeyValuePair<int, BotonTk>(valor, boton));
				i++;
			}
			y += ((i + 2) / 3) * 40f + 8f;

			// El aviso de permanencia va SIEMPRE visible, antes de tocar nada.
			UIPanel cajaAviso = new UIPanel();
			cajaAviso.Width.Set(0f, 1f);
			cajaAviso.Top.Set(y, 0f);
			cajaAviso.Height.Set(96f, 0f);
			cajaAviso.BackgroundColor = new Color(92, 60, 30) * 0.92f;
			cajaAviso.BorderColor = new Color(0, 0, 0, 0);
			cajaAviso.SetPadding(8f);
			derecha.Append(cajaAviso);

			EtiquetaTk aviso = new EtiquetaTk(() => "⚠  " + DificultadMundo.AvisoDePermanencia(), 0.78f, 460f, 24f);
			aviso.ColorTexto = EstiloTk.TextoAviso;
			cajaAviso.Append(aviso);

			EtiquetaTk efecto = new EtiquetaTk(() => DificultadMundo.AvisoDeEfecto(), 0.72f, 460f, 22f);
			efecto.ColorTexto = new Color(235, 220, 200);
			efecto.Top.Set(24f, 0f);
			cajaAviso.Append(efecto);

			EtiquetaTk deshacer = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Mundo.AvisoDeshacer"),
				0.72f, 460f, 22f);
			deshacer.ColorTexto = new Color(235, 220, 200);
			deshacer.Top.Set(46f, 0f);
			cajaAviso.Append(deshacer);

			y += 106f;

			_confirmar = new BotonTk(Idiomas.Texto("Exploracion.Mundo.EligeModo"), 0.85f);
			_confirmar.Width.Set(0f, 1f);
			_confirmar.Height.Set(38f, 0f);
			_confirmar.Top.Set(y, 0f);
			_confirmar.Habilitado = false;
			_confirmar.AlPulsar += Confirmar;
			derecha.Append(_confirmar);
			y += 46f;

			EtiquetaTk mensaje = new EtiquetaTk(() => _ultimoMensaje, 0.75f, 500f, 44f);
			mensaje.ColorTexto = EstiloTk.TextoAviso;
			mensaje.Top.Set(y, 0f);
			derecha.Append(mensaje);
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
		}
	}
}
