using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Pestaña "Buffs": los buffs activos del jugador y la forma de añadir o quitar cualquiera.
	/// <para />
	/// Es la unica pestaña con un problema de refresco de verdad, y estaba previsto en el plan:
	/// <b>los buffs caducan solos</b>. El juego hace <c>buffTime[i]--</c> cada tick y, cuando
	/// llega a cero, <c>DelBuff</c> DESPLAZA el resto del array hacia arriba. Es decir, no basta
	/// con leer el estado al abrir: los tiempos cambian 60 veces por segundo y los indices se
	/// mueven bajo los pies.
	/// <para />
	/// Se resuelve con dos medidas:
	/// <list type="bullet">
	/// <item>cada fila se identifica por el TIPO de buff, no por el indice; el tiempo se busca en
	/// cada dibujado con <c>Player.FindBuffIndex(tipo)</c>;</item>
	/// <item>en cada <c>Update</c> se compara una firma del array <c>buffType</c> y, si ha
	/// cambiado (ha caducado uno, o el juego ha metido otro), la lista se reconstruye sola.</item>
	/// </list>
	/// Para añadir buffs se usa la API oficial <c>Player.AddBuff</c> y no una escritura a pelo de
	/// los arrays: es la que respeta las inmunidades, los limites y los enganches de los mods.
	/// </summary>
	public class PestanaBuffs : UIElement
	{
		private const int SegundosPorDefecto = 600;
		private const int MaximoResultados = 12;

		private UIList _listaActivos;
		private UIList _listaResultados;
		private CampoTextoTk _campoBusqueda;
		private CampoTextoTk _campoDuracion;
		private BotonTk _botonQuitarTodos;
		private readonly List<BotonTk> _botonesQuitar = new List<BotonTk>();
		private readonly List<BotonTk> _botonesAplicar = new List<BotonTk>();
		private int _firmaActivos = -1;

		public PestanaBuffs()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirActivos();
			ConstruirAnadir();

			ReconstruirActivos();
			ReconstruirResultados("");
		}

		// ---------------------------------------------------------------- buffs activos

		private void ConstruirActivos()
		{
			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Activos",
					PersonajeVivo.Jugador.CountBuffs(), PersonajeVivo.RanurasBuff),
				0.85f, 400f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(0f, 0f);
			Append(titulo);

			UIPanel caja = new UIPanel();
			caja.Width.Set(470f, 0f);
			caja.Height.Set(-58f, 1f);
			caja.Top.Set(26f, 0f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			Append(caja);

			_listaActivos = new UIList();
			_listaActivos.Width.Set(-24f, 1f);
			_listaActivos.Height.Set(0f, 1f);
			_listaActivos.ListPadding = 4f;
			caja.Append(_listaActivos);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Height.Set(0f, 1f);
			barra.SetView(100f, 1000f);
			caja.Append(barra);
			_listaActivos.SetScrollbar(barra);

			_botonQuitarTodos = new BotonTk(Idiomas.Texto("Personaje.Buffs.QuitarTodos"), 0.8f);
			BotonTk quitarTodos = _botonQuitarTodos;
			quitarTodos.Width.Set(150f, 0f);
			quitarTodos.Height.Set(28f, 0f);
			quitarTodos.Left.Set(0f, 0f);
			quitarTodos.VAlign = 1f;
			quitarTodos.AlPulsar += QuitarTodos;
			Append(quitarTodos);
		}

		private void ReconstruirActivos()
		{
			Player jugador = PersonajeVivo.Jugador;
			_listaActivos.Clear();
			_botonesQuitar.Clear();

			for (int i = 0; i < jugador.buffType.Length; i++) {
				int tipo = jugador.buffType[i];
				if (tipo <= 0) {
					continue;
				}
				_listaActivos.Add(CrearFilaBuff(tipo));
			}

			_firmaActivos = FirmaActivos();
		}

		private UIElement CrearFilaBuff(int tipo)
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(36f, 0f);

			IconoBuffTk icono = new IconoBuffTk(() => tipo, 32f);
			icono.Left.Set(2f, 0f);
			icono.Top.Set(2f, 0f);
			fila.Append(icono);

			EtiquetaTk nombre = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.NombreConId",
					PersonajeVivo.NombreBuff(tipo), tipo), 0.8f, 240f, 20f);
			nombre.Left.Set(40f, 0f);
			nombre.Top.Set(8f, 0f);
			fila.Append(nombre);

			EtiquetaTk tiempo = new EtiquetaTk(() => TextoTiempo(tipo), 0.8f, 90f, 20f);
			tiempo.Left.Set(280f, 0f);
			tiempo.Top.Set(8f, 0f);
			fila.Append(tiempo);

			BotonTk quitar = new BotonTk(Idiomas.Texto("Personaje.Buffs.Quitar"), 0.75f);
			_botonesQuitar.Add(quitar);
			quitar.Width.Set(70f, 0f);
			quitar.Height.Set(26f, 0f);
			quitar.Left.Set(370f, 0f);
			quitar.Top.Set(4f, 0f);
			quitar.AlPulsar += () => QuitarBuff(tipo);
			fila.Append(quitar);

			return fila;
		}

		/// <summary>Tiempo restante, buscado por tipo en cada dibujado. Si el buff ya no esta,
		/// devuelve "-" y el <c>Update</c> reconstruira la lista en ese mismo fotograma.</summary>
		private static string TextoTiempo(int tipo)
		{
			Player jugador = PersonajeVivo.Jugador;
			int indice = jugador.FindBuffIndex(tipo);
			if (indice < 0) {
				return "-";
			}
			return PersonajeVivo.FormatearTiempoBuff(jugador.buffTime[indice]);
		}

		private void QuitarBuff(int tipo)
		{
			Player jugador = PersonajeVivo.Jugador;
			int indice = jugador.FindBuffIndex(tipo);
			jugador.ClearBuff(tipo);

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Buff quitado en vivo: \"{PersonajeVivo.NombreBuff(tipo)}\" (id {tipo}), " +
				$"estaba en buffType[{indice}]. FindBuffIndex despues = {jugador.FindBuffIndex(tipo)}.");

			ReconstruirActivos();
		}

		private void QuitarTodos()
		{
			Player jugador = PersonajeVivo.Jugador;
			int quitados = 0;

			for (int i = 0; i < jugador.buffType.Length; i++) {
				if (jugador.buffType[i] > 0) {
					jugador.DelBuff(i);
					quitados++;
					// DelBuff desplaza el resto del array hacia arriba, asi que hay que volver a
					// mirar la MISMA posicion en la siguiente vuelta.
					i--;
				}
			}

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Buffs quitados en vivo: {quitados}. " +
				$"CountBuffs despues = {jugador.CountBuffs()}.");

			ReconstruirActivos();
		}

		// ---------------------------------------------------------------- añadir buffs

		private void ConstruirAnadir()
		{
			float izquierda = 490f;

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Anadir"), 0.85f, 300f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(izquierda, 0f);
			titulo.Top.Set(0f, 0f);
			Append(titulo);

			EtiquetaTk etiquetaBusqueda = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Buscar"), 0.8f, 70f, 20f);
			etiquetaBusqueda.ColorTexto = EstiloTk.TextoSuave;
			etiquetaBusqueda.Left.Set(izquierda, 0f);
			etiquetaBusqueda.Top.Set(32f, 0f);
			Append(etiquetaBusqueda);

			_campoBusqueda = new CampoTextoTk(() => Idiomas.Texto("Personaje.Buffs.PistaBusqueda"), 30);
			_campoBusqueda.Width.Set(230f, 0f);
			_campoBusqueda.Height.Set(28f, 0f);
			_campoBusqueda.Left.Set(izquierda + 66f, 0f);
			_campoBusqueda.Top.Set(26f, 0f);
			_campoBusqueda.AlCambiar += ReconstruirResultados;
			Append(_campoBusqueda);

			EtiquetaTk etiquetaDuracion = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Segundos"), 0.8f, 90f, 20f);
			etiquetaDuracion.ColorTexto = EstiloTk.TextoSuave;
			etiquetaDuracion.Left.Set(izquierda + 306f, 0f);
			etiquetaDuracion.Top.Set(32f, 0f);
			Append(etiquetaDuracion);

			_campoDuracion = new CampoTextoTk(() => SegundosPorDefecto.ToString(), 6);
			_campoDuracion.SoloNumeros = true;
			_campoDuracion.FijarTextoSilencioso(SegundosPorDefecto.ToString());
			_campoDuracion.Width.Set(80f, 0f);
			_campoDuracion.Height.Set(28f, 0f);
			_campoDuracion.Left.Set(izquierda + 380f, 0f);
			_campoDuracion.Top.Set(26f, 0f);
			Append(_campoDuracion);

			UIPanel caja = new UIPanel();
			caja.Width.Set(470f, 0f);
			caja.Height.Set(-92f, 1f);
			caja.Left.Set(izquierda, 0f);
			caja.Top.Set(60f, 0f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			Append(caja);

			_listaResultados = new UIList();
			_listaResultados.Width.Set(-24f, 1f);
			_listaResultados.Height.Set(0f, 1f);
			_listaResultados.ListPadding = 4f;
			caja.Append(_listaResultados);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Height.Set(0f, 1f);
			barra.SetView(100f, 1000f);
			caja.Append(barra);
			_listaResultados.SetScrollbar(barra);

			EtiquetaTk nota = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.Nota"),
				0.72f, 470f, 18f);
			nota.ColorTexto = EstiloTk.TextoSuave;
			nota.Left.Set(izquierda, 0f);
			nota.VAlign = 1f;
			Append(nota);
		}

		private void ReconstruirResultados(string filtro)
		{
			_listaResultados.Clear();
			_botonesAplicar.Clear();

			string busqueda = (filtro ?? "").Trim();
			int idPedido;
			bool esNumero = int.TryParse(busqueda, out idPedido);
			int encontrados = 0;

			for (int tipo = 1; tipo < BuffLoader.BuffCount && encontrados < MaximoResultados; tipo++) {
				string nombre = PersonajeVivo.NombreBuff(tipo);
				if (string.IsNullOrEmpty(nombre)) {
					continue;
				}

				bool coincide = busqueda.Length == 0
					|| (esNumero && tipo == idPedido)
					|| nombre.IndexOf(busqueda, StringComparison.OrdinalIgnoreCase) >= 0;

				if (!coincide) {
					continue;
				}

				_listaResultados.Add(CrearFilaResultado(tipo, nombre));
				encontrados++;
			}

			if (encontrados == 0) {
				EtiquetaTk vacio = new EtiquetaTk(
					() => Idiomas.Texto("Personaje.Buffs.SinResultados"), 0.8f, 300f, 24f);
				vacio.ColorTexto = EstiloTk.TextoSuave;
				_listaResultados.Add(vacio);
			}
		}

		private UIElement CrearFilaResultado(int tipo, string nombre)
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(36f, 0f);

			IconoBuffTk icono = new IconoBuffTk(() => tipo, 32f);
			icono.Left.Set(2f, 0f);
			icono.Top.Set(2f, 0f);
			fila.Append(icono);

			EtiquetaTk etiqueta = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Buffs.NombreConId", nombre, tipo), 0.8f, 290f, 20f);
			etiqueta.Left.Set(40f, 0f);
			etiqueta.Top.Set(8f, 0f);
			fila.Append(etiqueta);

			BotonTk anadir = new BotonTk(Idiomas.Texto("Personaje.Buffs.Aplicar"), 0.75f);
			_botonesAplicar.Add(anadir);
			anadir.Width.Set(80f, 0f);
			anadir.Height.Set(26f, 0f);
			anadir.Left.Set(350f, 0f);
			anadir.Top.Set(4f, 0f);
			anadir.AlPulsar += () => AplicarBuff(tipo);
			fila.Append(anadir);

			return fila;
		}

		private void AplicarBuff(int tipo)
		{
			Player jugador = PersonajeVivo.Jugador;
			int segundos = PersonajeVivo.Acotar(_campoDuracion.ComoEntero(SegundosPorDefecto), 1, 99999);
			int ticks = segundos * 60;

			jugador.AddBuff(tipo, ticks);

			int indice = jugador.FindBuffIndex(tipo);
			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Buff aplicado en vivo con Player.AddBuff: " +
				$"\"{PersonajeVivo.NombreBuff(tipo)}\" (id {tipo}) durante {segundos} s ({ticks} ticks). " +
				$"Resultado real: buffType[{indice}]=" +
				(indice >= 0 ? jugador.buffType[indice].ToString() : "(no puesto)") +
				", buffTime[" + indice + "]=" +
				(indice >= 0 ? jugador.buffTime[indice].ToString() : "-") + ".");

			ReconstruirActivos();
		}

		// ---------------------------------------------------------------- refresco en vivo

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Los buffs caducan solos: si el conjunto de tipos activos ha cambiado desde el
			// ultimo fotograma, la lista de la izquierda se rehace sola.
			if (FirmaActivos() != _firmaActivos) {
				ReconstruirActivos();
			}

			// Los rotulos de los botones se fijan al construirlos, asi que hay que volver a
			// ponerlos para que cambien en vivo con el selector de idioma del area de Ajustes.
			if (_botonQuitarTodos != null) {
				_botonQuitarTodos.FijarTexto(Idiomas.Texto("Personaje.Buffs.QuitarTodos"));
			}
			for (int i = 0; i < _botonesQuitar.Count; i++) {
				_botonesQuitar[i].FijarTexto(Idiomas.Texto("Personaje.Buffs.Quitar"));
			}
			for (int i = 0; i < _botonesAplicar.Count; i++) {
				_botonesAplicar[i].FijarTexto(Idiomas.Texto("Personaje.Buffs.Aplicar"));
			}
		}

		private static int FirmaActivos()
		{
			int[] tipos = PersonajeVivo.Jugador.buffType;
			int firma = 17;
			for (int i = 0; i < tipos.Length; i++) {
				firma = firma * 31 + tipos[i];
			}
			return firma;
		}
	}
}
