using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Builds;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Guia
{
	/// <summary>
	/// <b>Contenido</b> del area "Guia": la brujula en tiempo real.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Dos columnas. A la izquierda, el <b>objetivo de ahora mismo</b>: que es, hacia donde cae,
	/// el medidor de preparacion, el POR QUE (que mecanica del juego hay detras) y el COMO. A la
	/// derecha, en una lista con scroll, <b>lo que te falta</b> requisito a requisito con sus
	/// numeros reales, la <b>lectura del jefe</b> cuando el paso apunta a uno, y la <b>hoja de
	/// ruta</b> de lo que viene despues.
	/// </para>
	/// <para>
	/// <b>Todo se reconstruye cuando cambia el paso, y los numeros se leen cada fotograma.</b> No
	/// hay ningun evento al que engancharse: el jugador puede equiparse algo, recoger monedas o
	/// ver llegar a un vecino con el panel abierto, y eso tiene que verse en el acto. Es el mismo
	/// criterio de <see cref="EtiquetaTk"/> y del area de Ajustes, y ademas evita depender de que
	/// el enganche llegue a hacerse (WS6 documento que un elemento colgado con el <c>UIState</c>
	/// ya activo se puede saltar <c>OnInitialize</c>/<c>OnActivate</c>).
	/// </para>
	/// </remarks>
	public class ContenidoGuia : UIElement
	{
		private const float FraccionIzquierda = 0.5f;
		private const float SeparacionColumnas = 8f;

		private UIPanel _cajaObjetivo;
		private UIPanel _cajaDetalle;
		private UIList _listaObjetivo;
		private UIScrollbar _scrollObjetivo;
		private UIList _lista;
		private UIScrollbar _scroll;

		private MedidorPreparacionTk _medidor;

		/// <summary>Clave del paso con el que se monto el contenido. Si cambia, se rehace.</summary>
		private string _claveMontada = "sin montar";

		public ContenidoGuia()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirColumnas();
			ConstruirPanelEntrenador();
			Reconstruir();
		}

		/// <summary>El paso que se esta enseñando ahora mismo (null = no queda ninguno). Lo lee la
		/// autoprueba.</summary>
		public PasoGuia PasoEnPantalla { get; private set; }

		/// <summary>El tramo del paso en pantalla.</summary>
		public TramoGuia TramoEnPantalla { get; private set; }

		/// <summary>El medidor de preparacion montado, para que la autoprueba lea su valor real.</summary>
		public MedidorPreparacionTk Medidor => _medidor;

		/// <summary>La barra de scroll de la columna derecha (requisitos/jefe/hoja de ruta), para
		/// que la autoprueba pueda bajar del todo y capturar la tira de "lo que viene" (TM4) aunque
		/// quede fuera de la vista inicial.</summary>
		public UIScrollbar ScrollDetalle => _scroll;

		/// <summary>Idea 1 (entrenador de jefe): el boton de accion del panel de estado/informe
		/// ("Cancelar" mientras hay practica, "Cerrar" con el informe ya listo), para que la
		/// autoprueba pueda pulsarlo de verdad.</summary>
		public BotonTk BotonAccionEntrenadorParaPrueba => _botonAccionEntrenador;

		private void ConstruirColumnas()
		{
			_cajaObjetivo = new UIPanel();
			_cajaObjetivo.Width.Set(-SeparacionColumnas / 2f, FraccionIzquierda);
			_cajaObjetivo.Height.Set(0f, 1f);
			_cajaObjetivo.BackgroundColor = EstiloTk.FondoCaja;
			_cajaObjetivo.BorderColor = new Color(0, 0, 0, 0);
			_cajaObjetivo.SetPadding(10f);
			Append(_cajaObjetivo);

			_listaObjetivo = new UIList();
			_listaObjetivo.Width.Set(-22f, 1f);
			_listaObjetivo.Height.Set(0f, 1f);
			_listaObjetivo.ListPadding = 2f;
			_listaObjetivo.ManualSortMethod = elementos => { };
			_cajaObjetivo.Append(_listaObjetivo);

			_scrollObjetivo = new UIScrollbar();
			_scrollObjetivo.HAlign = 1f;
			_scrollObjetivo.Height.Set(0f, 1f);
			_scrollObjetivo.SetView(100f, 1000f);
			_cajaObjetivo.Append(_scrollObjetivo);
			_listaObjetivo.SetScrollbar(_scrollObjetivo);

			_cajaDetalle = new UIPanel();
			_cajaDetalle.Left.Set(SeparacionColumnas / 2f, FraccionIzquierda);
			_cajaDetalle.Width.Set(-SeparacionColumnas / 2f, 1f - FraccionIzquierda);
			_cajaDetalle.Height.Set(0f, 1f);
			_cajaDetalle.BackgroundColor = EstiloTk.FondoCaja;
			_cajaDetalle.BorderColor = new Color(0, 0, 0, 0);
			_cajaDetalle.SetPadding(10f);
			Append(_cajaDetalle);

			_lista = new UIList();
			_lista.Width.Set(-22f, 1f);
			_lista.Height.Set(0f, 1f);
			_lista.ListPadding = 4f;
			// UIList ordena sus elementos con List.Sort + UIElement.CompareTo, que devuelve 0 para
			// todos, y List.Sort NO es estable: con unos cuantos elementos deja de respetar el
			// orden de insercion y la lista sale barajada. Es el mismo fallo real que ya mordio en
			// el desplegable de prefijos y en las carpetas de la Libreria (ver bitacora.md), y el
			// mismo arreglo.
			_lista.ManualSortMethod = elementos => { };
			_cajaDetalle.Append(_lista);

			_scroll = new UIScrollbar();
			_scroll.HAlign = 1f;
			_scroll.Height.Set(0f, 1f);
			_scroll.SetView(100f, 1000f);
			_cajaDetalle.Append(_scroll);
			_lista.SetScrollbar(_scroll);
		}

		// -------------------------------------------------------------------------------------
		// Idea 1 del catalogo de funciones: panel de estado/informe del "entrenador de jefe",
		// SIEMPRE encima de las dos columnas (practicar es un estado de toda la pantalla, no algo
		// que quepa dentro de una lista con scroll) - nunca colgado de CapaSuperposicionTk (esta
		// no compite por el mismo hueco que ningun otro desplegable del area).
		// -------------------------------------------------------------------------------------

		private const float EscalaTextoEntrenador = 0.76f;
		private const float AltoMinimoPanelEntrenador = 64f;
		private const float AnchoBotonEntrenador = 120f;

		private UIPanel _panelEntrenador;
		private EtiquetaTk _textoEntrenador;
		private BotonTk _botonAccionEntrenador;
		private string _textoEntrenadorPartido = "";

		private void ConstruirPanelEntrenador()
		{
			_panelEntrenador = new UIPanel();
			_panelEntrenador.Width.Set(-40f, 1f);
			_panelEntrenador.Height.Set(AltoMinimoPanelEntrenador, 0f);
			_panelEntrenador.HAlign = 0.5f;
			_panelEntrenador.VAlign = 1f;
			_panelEntrenador.Top.Set(-6f, 0f);
			_panelEntrenador.BackgroundColor = EstiloTk.FondoCaja * 1.06f;
			_panelEntrenador.BorderColor = EstiloTk.BordeSobre * 0.7f;
			_panelEntrenador.SetPadding(10f);
			// No se añade aqui: Update() lo cuelga/descuelga de este UIElement segun si hay
			// practica activa o informe pendiente de leer - mismo motivo real que el alternador de
			// fuente de TM5 (ContenidoBuilds), un UIPanel dibuja su fondo siempre que este en el
			// arbol, Height a 0 no basta para "ocultarlo" de verdad con garantias.

			// El texto se ENVUELVE de verdad (ver ActualizarPanelEntrenador, con
			// EtiquetaTk.PartirEnLineas) y el panel entero CRECE para que quepa - la primera
			// version dejaba el texto en una sola linea con un ancho fijo, y con el informe real
			// ("¡Práctica superada! DPS medio: ... duración: ... s. No cuenta como derrota real:
			// el tramo sigue como estaba.") se salia del panel por la derecha, encima del propio
			// boton - visto en una captura real (guia-entrenador-informe-victoria.png antes del
			// arreglo). Mismo bug de fondo, mismo arreglo, que "lo que viene" en TM4.
			_textoEntrenador = new EtiquetaTk(() => _textoEntrenadorPartido, EscalaTextoEntrenador, 900f, 22f);
			_textoEntrenador.Width.Set(-(AnchoBotonEntrenador + 14f), 1f);
			_panelEntrenador.Append(_textoEntrenador);

			_botonAccionEntrenador = new BotonTk("", 0.78f);
			_botonAccionEntrenador.Width.Set(AnchoBotonEntrenador, 0f);
			_botonAccionEntrenador.Height.Set(30f, 0f);
			_botonAccionEntrenador.HAlign = 1f;
			_botonAccionEntrenador.AlPulsar += () => {
				if (EntrenadorJefe.Activa) {
					EntrenadorJefe.Cancelar();
				}
				else {
					EntrenadorJefe.LimpiarInforme();
				}
			};
			_panelEntrenador.Append(_botonAccionEntrenador);
		}

		private string TextoEntrenador()
		{
			if (EntrenadorJefe.Activa) {
				return Idiomas.Texto("Guia.Entrenador.EnMarcha", (int)EntrenadorJefe.SegundosTranscurridos,
					EntrenadorJefe.DanoAlJefeAhora, EntrenadorJefe.DanoRecibidoAhora);
			}
			return EntrenadorJefe.UltimoInforme;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Se rehace solo cuando cambia el PASO (o el idioma, que ya obliga al marco a rehacer
			// el area entera). Los numeros de dentro se leen cada fotograma por su cuenta, asi que
			// no hay que reconstruir nada para que la barra se mueva. La CLAVE tambien incluye
			// cuantos companeros hay conectados (idea 9): a diferencia de un numero que cambia solo
			// (eso lo lee cada fila por su cuenta cada fotograma), que alguien se una o se
			// desconecte a mitad de paso cambia CUANTAS filas hay que dibujar, y eso si necesita
			// reconstruir la lista - unirse/desconectarse no es frecuente, comprobar el recuento
			// cada fotograma no tiene coste real.
			//
			// Bug real encontrado con la propia autoprueba (captura
			// grupo-observador-companero-visible.png: "Que te falta" y "Grupo" se veian
			// superpuestos, texto encima de texto): Reconstruir() calculaba SU PROPIA version de
			// _claveMontada (sin el sufijo "|grupo=") en vez de reutilizar Clave(), asi que las dos
			// nunca coincidian con un companero conectado - clave != _claveMontada era SIEMPRE
			// cierto, y Reconstruir() (que empieza por _lista.Clear()) se llamaba TODOS los
			// fotogramas sin parar. Cada FilaRequisitoTk nueva nace con su alto por defecto
			// (24f) hasta que su propio Reajustar() (en Update()/DrawSelf()) lo corrige - pero la
			// lista se volvia a vaciar y reconstruir antes de que ese ajuste llegara a influir en
			// donde caen las filas siguientes, así que nunca llegaba a asentarse. Ahora las dos
			// lecturas (aqui y en Reconstruir()) usan la MISMA Clave(), no pueden volver a
			// divergir.
			TramoGuia tramo;
			PasoGuia paso = EstadoGuia.PasoActual(out tramo);
			string clave = Clave(paso, tramo);
			if (clave != _claveMontada) {
				PasoEnPantalla = paso;
				TramoEnPantalla = tramo;
				Reconstruir();
			}

			ActualizarPanelEntrenador();
		}

		private void ActualizarPanelEntrenador()
		{
			bool debeVerse = EntrenadorJefe.Activa || EntrenadorJefe.Terminada;
			if (debeVerse && _panelEntrenador.Parent == null) {
				Append(_panelEntrenador);
			}
			else if (!debeVerse && _panelEntrenador.Parent != null) {
				RemoveChild(_panelEntrenador);
			}
			if (!debeVerse) {
				return;
			}

			_botonAccionEntrenador.FijarTexto(Idiomas.Texto(EntrenadorJefe.Activa
				? "Guia.Entrenador.Cancelar"
				: "Guia.Entrenador.Cerrar"));

			// Envuelve el texto real contra el ancho REAL de la etiqueta (mismo patron que
			// RecalcularCabecera en ContenidoBuilds/FilaRequisitoTk.Reajustar) y crece el panel
			// entero a lo que haga falta - nunca al reves.
			float anchoInterior = _textoEntrenador.GetDimensions().Width;
			if (anchoInterior <= 0f) {
				return;
			}

			_textoEntrenadorPartido = EtiquetaTk.PartirEnLineas(TextoEntrenador(), anchoInterior, EscalaTextoEntrenador);

			float altoTexto = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(_textoEntrenadorPartido).Y *
				EscalaTextoEntrenador;
			float altoNecesario = altoTexto + 20f + 8f;
			float altoNuevo = System.Math.Max(AltoMinimoPanelEntrenador, altoNecesario);
			if (System.Math.Abs(_panelEntrenador.Height.Pixels - altoNuevo) > 0.5f) {
				_panelEntrenador.Height.Set(altoNuevo, 0f);
				Recalculate();
			}
		}

		/// <summary>La UNICA formula real de la clave "esto es lo que hay montado ahora mismo".
		/// <c>Update()</c> y <see cref="Reconstruir"/> tienen que usar exactamente esta misma
		/// funcion - ver el porque en el comentario real de <c>Update()</c> (bug ya encontrado y
		/// arreglado: dos formulas ligeramente distintas nunca llegaban a coincidir).</summary>
		private static string Clave(PasoGuia paso, TramoGuia tramo)
		{
			return (paso != null ? tramo.Clave + "/" + paso.Clave : "(ninguno)")
				+ "|grupo=" + GuiaGrupo.IndicesConectados().Count;
		}

		/// <summary>Vuelve a montar las dos columnas con el paso actual. Publico para que la
		/// autoprueba pueda forzarlo tras cambiar el estado del jugador en vivo.</summary>
		public void Reconstruir()
		{
			TramoGuia tramo;
			PasoGuia paso = EstadoGuia.PasoActual(out tramo);
			PasoEnPantalla = paso;
			TramoEnPantalla = tramo;
			_claveMontada = Clave(paso, tramo);

			MontarObjetivo(paso, tramo);
			MontarDetalle(paso, tramo);

			_cajaObjetivo.Recalculate();
			_cajaDetalle.Recalculate();
		}

		// -------------------------------------------------------------------------------------
		// Columna izquierda: el objetivo
		// -------------------------------------------------------------------------------------

		/// <summary>
		/// Monta la columna del objetivo.
		/// </summary>
		/// <remarks>
		/// <b>Va en una <c>UIList</c> y no con posiciones calculadas a mano, y hay un motivo real
		/// detras.</b> La primera version reservaba un hueco fijo para el parrafo del "por que"
		/// (120 px) y colocaba el "por donde se empieza" debajo. En la primera captura del juego se
		/// vio lo que tenia que pasar: el parrafo ocupa lo que ocupa segun el idioma, la resolucion
		/// y la escala de interfaz - ocho lineas en ingles a 800x720 - y el rotulo de abajo se
		/// pintaba ENCIMA de sus dos ultimas lineas. Ningun dato del log lo habria dicho; se vio
		/// mirando la imagen, que es exactamente para lo que existen las capturas en este proyecto.
		/// Con una <c>UIList</c>, cada elemento se coloca debajo del anterior segun el alto REAL del
		/// de arriba (y <see cref="ParrafoTk"/> ajusta el suyo), asi que no hay ningun hueco fijo
		/// que se pueda quedar corto.
		/// </remarks>
		private void MontarObjetivo(PasoGuia paso, TramoGuia tramo)
		{
			_listaObjetivo.Clear();
			_medidor = null;

			AnadirAObjetivo(new EtiquetaTk(() => Idiomas.Texto("Guia.ObjetivoActual"), 0.82f, 400f, 22f),
				EstiloTk.TextoSuave);

			if (paso == null) {
				AnadirAObjetivo(new ParrafoTk(() => Idiomas.Texto("Guia.SinObjetivo"), 0.85f), Color.White);
				return;
			}

			PasoGuia elPaso = paso;
			TramoGuia elTramo = tramo;

			// TM4 (catalogo de rediseño visual): tarjeta con el sprite REAL del jefe cuando el paso
			// (o su tramo) apunta a uno - IconoJefe.Resolver nunca inventa un icono, devuelve null
			// para cualquier NPC sin cabeza de jefe registrada (la inmensa mayoria de pasos, que no
			// son "vencer a X" sino equipo/exploracion/npcs) y en ese caso se enseña el titulo
			// simple de siempre, sin tarjeta - honesto con lo que de verdad hay que mostrar.
			int jefeDelTitulo = elPaso.Jefe != 0 ? elPaso.Jefe : (elTramo != null ? elTramo.JefeFinal : 0);
			Texture2D iconoDelTitulo = jefeDelTitulo > 0 ? IconoJefe.Resolver(jefeDelTitulo) : null;

			if (iconoDelTitulo != null) {
				TarjetaObjetivoTk tarjeta = new TarjetaObjetivoTk(() => elPaso.Titulo(), () => iconoDelTitulo);
				tarjeta.ColorTitulo = Color.White;
				_listaObjetivo.Add(tarjeta);
			}
			else {
				AnadirAObjetivo(new ParrafoTk(() => elPaso.Titulo(), 1.0f), Color.White);
			}

			// Idea 1 del catalogo de funciones ("entrenador de jefe"): solo para el subconjunto
			// real investigado y confirmado seguro (ver EntrenadorJefe.Roster) - nunca para el
			// Muro de Carne ni para ningun jefe fuera del roster, que simplemente no enseñan el
			// boton.
			if (EntrenadorJefe.EsPracticable(jefeDelTitulo)) {
				int elJefeAPracticar = jefeDelTitulo;
				BotonTk botonPracticar = new BotonTk(Idiomas.Texto("Guia.Entrenador.Practicar"), 0.78f);
				botonPracticar.Width.Set(220f, 0f);
				botonPracticar.Height.Set(30f, 0f);
				botonPracticar.Ayuda = () => Idiomas.Texto("Guia.Entrenador.PracticarAyuda");
				botonPracticar.AlPulsar += () => EntrenadorJefe.Iniciar(elJefeAPracticar);
				_listaObjetivo.Add(botonPracticar);

				UIElement huecoTrasPracticar = new UIElement();
				huecoTrasPracticar.Width.Set(0f, 1f);
				huecoTrasPracticar.Height.Set(6f, 0f);
				_listaObjetivo.Add(huecoTrasPracticar);
			}

			// ParrafoTk y no EtiquetaTk: EtiquetaTk no envuelve, y estas dos lineas son las que
			// mas cambian de largo entre idiomas ("Estás donde toca: la superficie" / "You are
			// where you need to be: the surface"). En la primera captura la de preparacion llegaba
			// justo a tocar la barra de scroll.
			AnadirAObjetivo(new ParrafoTk(() => EstadoGuia.Direccion(elPaso), 0.76f),
				EstiloTk.TextoAviso);

			// --- marcador de mapa (la brujula, "nunca GPS"): solo para las zonas que tienen uno
			// real (ver BrujulaGuia). Nunca dice DONDE esta si no se ha explorado - solo si el mapa
			// ya puede o no señalarlo, para no fingir una precision que no existe.
			if (BrujulaGuia.ZonaTieneBrujula(elPaso.Zona)) {
				AnadirAObjetivo(new ParrafoTk(() => {
					switch (BrujulaGuia.EstadoParaZona(elPaso.Zona)) {
						case BrujulaGuia.EstadoBrujula.Marcado: return Idiomas.Texto("Guia.Brujula.Marcado");
						case BrujulaGuia.EstadoBrujula.Buscando: return Idiomas.Texto("Guia.Brujula.Buscando");
						default: return Idiomas.Texto("Guia.Brujula.SinExplorar");
					}
				}, 0.72f), EstiloTk.Neutro);
			}

			// --- el medidor de preparacion ---------------------------------------------------
			_medidor = new MedidorPreparacionTk(() => {
				int cumplidos, total;
				return EvaluadorGuia.Preparacion(elPaso, out cumplidos, out total);
			});
			_listaObjetivo.Add(_medidor);

			AnadirAObjetivo(new ParrafoTk(() => {
				int cumplidos, total;
				float f = EvaluadorGuia.Preparacion(elPaso, out cumplidos, out total);
				return Idiomas.Texto("Guia.Preparacion", (int)(f * 100f + 0.5f), cumplidos, total);
			}, 0.74f), EstiloTk.TextoSuave);

			// --- el POR QUE -------------------------------------------------------------------
			AnadirHueco(_listaObjetivo, 6f);
			AnadirAObjetivo(new EtiquetaTk(() => Idiomas.Texto("Guia.PorQue"), 0.8f, 400f, 22f),
				EstiloTk.TextoAviso);
			AnadirAObjetivo(new ParrafoTk(() => elPaso.Porque(), 0.78f), Color.White);

			// --- el COMO ----------------------------------------------------------------------
			AnadirHueco(_listaObjetivo, 6f);
			AnadirAObjetivo(new EtiquetaTk(() => Idiomas.Texto("Guia.Como"), 0.8f, 400f, 22f),
				EstiloTk.TextoAviso);
			AnadirAObjetivo(new ParrafoTk(() => elPaso.Como(), 0.78f), EstiloTk.TextoSuave);

			// El tramo al que pertenece: situa el objetivo dentro del juego entero.
			AnadirHueco(_listaObjetivo, 8f);
			AnadirAObjetivo(new EtiquetaTk(() => Idiomas.Texto("Guia.EnElTramo", elTramo.Nombre()),
				0.72f, 500f, 20f), EstiloTk.Neutro);
		}

		/// <summary>Cuelga un elemento de la columna del objetivo con su color, sea etiqueta o
		/// parrafo (los dos tienen <c>ColorTexto</c>, pero no un antepasado comun que lo declare).</summary>
		private void AnadirAObjetivo(UIElement elemento, Color color)
		{
			EtiquetaTk etiqueta = elemento as EtiquetaTk;
			if (etiqueta != null) {
				etiqueta.ColorTexto = color;
			}
			ParrafoTk parrafo = elemento as ParrafoTk;
			if (parrafo != null) {
				parrafo.ColorTexto = color;
			}
			_listaObjetivo.Add(elemento);
		}

		private static void AnadirHueco(UIList lista, float alto)
		{
			UIElement hueco = new UIElement();
			hueco.Width.Set(0f, 1f);
			hueco.Height.Set(alto, 0f);
			lista.Add(hueco);
		}

		// -------------------------------------------------------------------------------------
		// Columna derecha: requisitos, jefe y hoja de ruta
		// -------------------------------------------------------------------------------------

		private void MontarDetalle(PasoGuia paso, TramoGuia tramo)
		{
			_lista.Clear();

			// El aviso de Calamity va EL PRIMERO cuando toca: es lo que evita que alguien lea todo
			// lo de abajo pensando que vale para su partida cuando no vale.
			if (CatalogoGuia.HayCalamity) {
				AnadirTitulo("Guia.AvisoCalamityTitulo", EstiloTk.TextoAviso);
				AnadirParrafo(() => Idiomas.Texto("Guia.AvisoCalamity"), EstiloTk.TextoAviso, 0.76f);
			}

			if (paso != null) {
				AnadirTitulo("Guia.QueTeFalta", EstiloTk.TextoAviso);
				for (int i = 0; i < paso.Requisitos.Count; i++) {
					_lista.Add(new FilaRequisitoTk(paso.Requisitos[i]));
				}

				MontarGrupo(paso);

				int jefe = paso.Jefe != 0 ? paso.Jefe : (tramo != null ? tramo.JefeFinal : 0);
				if (jefe > 0) {
					AnadirTitulo("Guia.LecturaDelJefe", EstiloTk.TextoAviso);
					int elJefe = jefe;
					AnadirParrafo(() => EstadoGuia.LecturaDeJefe(elJefe), EstiloTk.TextoSuave, 0.76f);
				}

				if (tramo != null && tramo.Pasos.Count > 1) {
					AnadirTitulo("Guia.EsteTramo", EstiloTk.TextoAviso);
					for (int i = 0; i < tramo.Pasos.Count; i++) {
						PasoGuia p = tramo.Pasos[i];
						bool hecho = EvaluadorGuia.PasoCompletado(p);
						bool esElActual = ReferenceEquals(p, paso);
						_lista.Add(NuevaLinea(
							() => (hecho ? "" : (esElActual ? "> " : "")) + p.Titulo(),
							hecho ? EstiloTk.Correcto : (esElActual ? Color.White : EstiloTk.Neutro),
							0.75f));
					}
				}
			}

			// --- objetivo opcional --------------------------------------------------------------
			// Un tramo OPCIONAL (Reina Abeja, Reina Slime, Duque Pezhongo/Emperatriz) nunca
			// sustituye al objetivo obligatorio de arriba, pero sin esto sus pasos (con su propio
			// umbral de daño y sus objetos recomendados) no tendrian NINGUN sitio donde enseñarse -
			// la hoja de ruta de abajo solo da el resumen en dos lineas del tramo entero, nunca sus
			// pasos. Se enseña como un bloque aparte, siempre despues del obligatorio.
			TramoGuia tramoOpcional;
			PasoGuia pasoOpcional = EstadoGuia.PasoOpcionalActual(out tramoOpcional);
			if (pasoOpcional != null) {
				PasoGuia elPasoOpcional = pasoOpcional;
				TramoGuia elTramoOpcional = tramoOpcional;

				AnadirTitulo("Guia.ObjetivoOpcionalTitulo", EstiloTk.TextoAviso);
				AnadirParrafo(() => Idiomas.Texto("Guia.ObjetivoOpcionalEnTramo",
					elTramoOpcional.Nombre(), elPasoOpcional.Titulo()), Color.White, 0.78f);

				for (int i = 0; i < pasoOpcional.Requisitos.Count; i++) {
					_lista.Add(new FilaRequisitoTk(pasoOpcional.Requisitos[i]));
				}

				int jefeOpcional = elPasoOpcional.Jefe != 0 ? elPasoOpcional.Jefe : elTramoOpcional.JefeFinal;
				if (jefeOpcional > 0) {
					int elJefeOpcional = jefeOpcional;
					AnadirParrafo(() => EstadoGuia.LecturaDeJefe(elJefeOpcional), EstiloTk.TextoSuave, 0.74f);
				}
			}

			// --- hoja de ruta -----------------------------------------------------------------
			// TM4 del catalogo de rediseño visual: tira HORIZONTAL de hasta 3 tarjetas pequeñas
			// (icono real del jefe si lo tiene + nombre corto del tramo), en vez de la lista
			// vertical de nombre+resumen completo de antes. Se deja fuera a proposito el resumen
			// largo de cada tramo (t.Resumen()): investigado ya en la ronda del commit parcial de
			// TM4 que esos resumenes varian mucho de largo entre idiomas/tramos, y meterlos dentro
			// de una columna de 1/3 de ancho arriesgaba el mismo tipo de desborde que este mismo
			// archivo ya evito una vez con el parrafo del "por que" (ver el XMLdoc de
			// MontarObjetivo) - un nombre corto de tramo no tiene ese riesgo real.
			List<TramoGuia> porDelante = EstadoGuia.TramosPorDelante(tramo);
			if (porDelante.Count > 0) {
				AnadirTitulo("Guia.LoQueViene", EstiloTk.TextoAviso);
				_lista.Add(ConstruirTiraLoQueViene(porDelante));
				if (porDelante.Count > MaximoLoQueViene) {
					AnadirParrafo(() => Idiomas.Texto("Guia.LoQueVieneMas", porDelante.Count - MaximoLoQueViene),
						EstiloTk.TextoSuave, 0.7f);
				}
			}
		}

		/// <summary>
		/// Idea 9 del catalogo de funciones: "que le falta a cada companero conectado" para ESTE
		/// mismo paso, justo debajo de "Que te falta" del jugador local. Vacio en partida de un
		/// jugador (<see cref="GuiaGrupo.IndicesConectados"/> ya lo deja vacio ahi) - no es un
		/// aviso, es que no hay grupo del que hablar.
		/// </summary>
		/// <remarks>
		/// Solo los tipos de requisito que dependen de CADA jugador (<see
		/// cref="GuiaGrupo.EsEvaluablePorJugador"/>): cristales de vida, vida maxima, defensa,
		/// objeto/objeto-cualquiera, dano de arma y gancho. Los de estado del MUNDO (vecino en el
		/// pueblo, enemigo activo, bandera) son iguales para todo el grupo y ya se ven una vez
		/// arriba - repetirlos por companero seria ruido sin dato nuevo.
		/// <para />
		/// El objeto/material SI se puede evaluar de un companero real porque su mochila principal
		/// SI llega sincronizada a este cliente en multijugador de verdad - ver la cabecera de
		/// <see cref="GuiaGrupo"/> para la cita exacta del decompilado (<c>PlayerItemSlotID.
		/// CanRelay</c>).
		/// </remarks>
		private void MontarGrupo(PasoGuia paso)
		{
			List<int> companeros = GuiaGrupo.IndicesConectados();
			if (companeros.Count == 0) {
				return;
			}

			AnadirTitulo("Guia.Grupo.Titulo", EstiloTk.TextoAviso);
			for (int c = 0; c < companeros.Count; c++) {
				int indice = companeros[c];
				_lista.Add(NuevaLinea(
					() => Main.player[indice] != null && Main.player[indice].active
						? Main.player[indice].name
						: Idiomas.Texto("Guia.Grupo.Desconectado"),
					Color.White, 0.8f));

				for (int i = 0; i < paso.Requisitos.Count; i++) {
					if (!GuiaGrupo.EsEvaluablePorJugador(paso.Requisitos[i].Tipo)) {
						continue;
					}
					_lista.Add(new FilaRequisitoTk(paso.Requisitos[i], 0.78f, () => Main.player[indice]));
				}
			}

			MontarRepartoClases();
		}

		/// <summary>
		/// Idea 9 del catalogo de funciones, pieza que faltaba: "repartir builds por clase sin
		/// solaparse". Una fila por cada miembro del grupo (jugador local incluido - "el grupo" es
		/// todo el mundo conectado) con su clase real detectada por el arma que lleva en la mano
		/// ahora mismo, y si dos o mas comparten clase, una clase SUGERIDA distinta para cada uno
		/// que la comparte - nunca inventada, siempre una de las cinco reales de CatalogoBuilds
		/// (ver <see cref="GuiaGrupo.RepartoClases"/>).
		/// </summary>
		private void MontarRepartoClases()
		{
			List<GuiaGrupo.AsignacionClase> reparto = GuiaGrupo.RepartoClases();
			if (reparto.Count == 0) {
				return;
			}

			// El TEXTO de cada fila vuelve a leer GuiaGrupo.RepartoClases() ENTERO en cada
			// fotograma (no se guarda el "AsignacionClase" de ahora mismo en un closure): si
			// alguien se cambia de arma a mitad de partida, el reparto tiene que reflejarlo en el
			// acto, igual que "Que te falta" ya hace con sus propios numeros - mismo criterio que
			// EtiquetaTk. El COLOR se calcula una sola vez al montar la fila (mismo criterio ya
			// usado un poco mas abajo en "Este tramo, paso a paso": si el solape cambiara a mitad
			// de partida, el color se pondria al dia en el siguiente Reconstruir - no hace falta
			// que sea live fotograma a fotograma, el texto ya lo dice con palabras).
			AnadirTitulo("Guia.Grupo.Reparto.Titulo", EstiloTk.TextoAviso);
			for (int m = 0; m < reparto.Count; m++) {
				int indiceMiembro = m;
				_lista.Add(NuevaLinea(
					() => TextoReparto(ObtenerMiembroDelReparto(indiceMiembro)),
					reparto[m].Solapa ? EstiloTk.TextoAviso : Color.White,
					0.76f));
			}
		}

		/// <summary>El miembro <paramref name="indice"/> del reparto de clases DE AHORA MISMO (una
		/// llamada nueva a <see cref="GuiaGrupo.RepartoClases"/> cada vez, nunca un valor guardado
		/// de cuando se monto la lista) - o un valor vacio si el grupo cambio de tamaño entre
		/// fotogramas (alguien se desconecto justo ahora).</summary>
		private static GuiaGrupo.AsignacionClase ObtenerMiembroDelReparto(int indice)
		{
			List<GuiaGrupo.AsignacionClase> reparto = GuiaGrupo.RepartoClases();
			return indice >= 0 && indice < reparto.Count ? reparto[indice] : default;
		}

		private static string TextoReparto(GuiaGrupo.AsignacionClase asignacion)
		{
			string nombre = asignacion.Indice == -1 && Main.LocalPlayer != null
				? Main.LocalPlayer.name
				: (Main.player[asignacion.Indice] != null && Main.player[asignacion.Indice].active
					? Main.player[asignacion.Indice].name
					: asignacion.Nombre);

			if (asignacion.ClaveDetectada == null) {
				return asignacion.ClaveSugerida != null
					? Idiomas.Texto("Guia.Grupo.Reparto.SinArma", nombre, CatalogoBuilds.EtiquetaClase(asignacion.ClaveSugerida))
					: Idiomas.Texto("Guia.Grupo.Reparto.SinArmaNiHueco", nombre);
			}

			if (!asignacion.Solapa) {
				return Idiomas.Texto("Guia.Grupo.Reparto.Ok", nombre, CatalogoBuilds.EtiquetaClase(asignacion.ClaveDetectada));
			}

			return asignacion.ClaveSugerida != null
				? Idiomas.Texto("Guia.Grupo.Reparto.Solapa", nombre, CatalogoBuilds.EtiquetaClase(asignacion.ClaveDetectada),
					CatalogoBuilds.EtiquetaClase(asignacion.ClaveSugerida))
				: Idiomas.Texto("Guia.Grupo.Reparto.SolapaSinHueco", nombre, CatalogoBuilds.EtiquetaClase(asignacion.ClaveDetectada));
		}

		private const float LadoIconoLoQueViene = 32f;
		private const float SeparacionTarjetasLoQueViene = 8f;
		private const int MaximoLoQueViene = 3;

		/// <summary>
		/// Tarjeta pequeña de "lo que viene" (TM4): icono ENCIMA, titulo de una sola linea DEBAJO,
		/// centrados los dos - no la reutilizacion directa de <see cref="TarjetaObjetivoTk"/>
		/// (icono a la izquierda, titulo <see cref="ParrafoTk"/> ENVOLVIENDO a la derecha) porque
		/// eso fue justo el bug real que encontro la propia autoprueba
		/// (<c>scripts\verificar-guia.ps1</c>, "NO CABE: hay texto fuera de su caja",
		/// linea "Esqueletron" saliendose -14,5 px de su caja): en una columna de 1/3 de ancho el
		/// icono a la izquierda no deja sitio de sobra, y <c>PartirEnLineas</c> no puede partir una
		/// palabra suelta mas ancha que la caja - nunca la recorta, la deja salirse.
		/// <para />
		/// Aqui, en vez de envolver, el titulo se dibuja SIEMPRE en una sola linea con una escala
		/// que se REDUCE a mano si hace falta (la misma tecnica ya probada en
		/// <c>TarjetaEdicionFlotanteTk</c> para el nombre del objeto de TM2: medir con la fuente
		/// real y multiplicar la escala por <c>anchoDisponible / anchoMedido</c>), recalculada en
		/// cada <c>DrawSelf</c> contra el ancho REAL de la columna - nunca un numero adivinado, y
		/// nunca puede desbordar porque la escala se ajusta al ancho que haya de verdad, con un
		/// suelo (<see cref="EscalaMinimaTitulo"/>) para que un nombre muy largo no encoja hasta ser
		/// ilegible.
		/// </para>
		/// </summary>
		private sealed class TarjetaLoQueVieneTk : UIElement
		{
			private const float EscalaBaseTitulo = 0.68f;
			private const float EscalaMinimaTitulo = 0.46f;
			private const float HuecoIconoTitulo = 4f;

			private readonly Func<Texture2D> _icono;
			private readonly Func<string> _titulo;

			public TarjetaLoQueVieneTk(Func<string> titulo, Func<Texture2D> icono)
			{
				_titulo = titulo;
				_icono = icono;
				Width.Set(0f, 1f);
				Height.Set(LadoIconoLoQueViene + HuecoIconoTitulo + FontAssets.MouseText.Value.LineSpacing * EscalaBaseTitulo, 0f);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dim = GetDimensions();
				if (dim.Width <= 0f) {
					return;
				}

				Texture2D textura = _icono != null ? _icono() : null;
				if (textura != null) {
					float xIcono = dim.X + (dim.Width - LadoIconoLoQueViene) / 2f;
					Rectangle destino = new Rectangle((int)xIcono, (int)dim.Y, (int)LadoIconoLoQueViene, (int)LadoIconoLoQueViene);
					spriteBatch.Draw(textura, destino, Color.White);
				}

				string texto = _titulo != null ? (_titulo() ?? "") : "";
				if (texto.Length == 0) {
					return;
				}

				DynamicSpriteFont fuente = FontAssets.MouseText.Value;
				float escala = EscalaBaseTitulo;
				Vector2 tamano = fuente.MeasureString(texto) * escala;
				if (tamano.X > dim.Width) {
					escala = System.Math.Max(EscalaMinimaTitulo, escala * (dim.Width / tamano.X));
					tamano = fuente.MeasureString(texto) * escala;
				}

				float x = dim.X + System.Math.Max(0f, (dim.Width - tamano.X) / 2f);
				float y = dim.Y + LadoIconoLoQueViene + HuecoIconoTitulo;
				Utils.DrawBorderString(spriteBatch, texto, new Vector2(x, y), Color.White, escala);
			}
		}

		private UIElement ConstruirTiraLoQueViene(List<TramoGuia> porDelante)
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(LadoIconoLoQueViene + 4f + FontAssets.MouseText.Value.LineSpacing * 0.68f, 0f);

			int mostradas = System.Math.Min(MaximoLoQueViene, porDelante.Count);
			float fraccion = 1f / mostradas;

			for (int i = 0; i < mostradas; i++) {
				TramoGuia t = porDelante[i];
				int jefe = t.JefeFinal;
				Texture2D icono = jefe > 0 ? IconoJefe.Resolver(jefe) : null;

				TarjetaLoQueVieneTk tarjeta = new TarjetaLoQueVieneTk(() => t.Nombre(), () => icono);
				tarjeta.Left.Set(0f, i * fraccion);
				tarjeta.Width.Set(-SeparacionTarjetasLoQueViene, fraccion);
				fila.Append(tarjeta);
			}

			return fila;
		}

		private void AnadirTitulo(string clave, Color color)
		{
			AnadirHueco(_lista, 8f);
			_lista.Add(NuevaLinea(() => Idiomas.Texto(clave), color, 0.82f));
		}

		private void AnadirParrafo(System.Func<string> texto, Color color, float escala)
		{
			ParrafoTk parrafo = new ParrafoTk(texto, escala);
			parrafo.ColorTexto = color;
			_lista.Add(parrafo);
		}

		private static UIElement NuevaLinea(System.Func<string> texto, Color color, float escala)
		{
			ParrafoTk linea = new ParrafoTk(texto, escala);
			linea.ColorTexto = color;
			return linea;
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

		/// <summary>Todos los parrafos montados ahora mismo. Lo usa la autoprueba para volcar el
		/// texto visible y comprobar que ninguna linea se sale de su caja.</summary>
		public List<ParrafoTk> Parrafos()
		{
			List<ParrafoTk> salida = new List<ParrafoTk>();
			ExecuteRecursively(elemento => {
				ParrafoTk parrafo = elemento as ParrafoTk;
				if (parrafo != null) {
					salida.Add(parrafo);
				}
			});
			return salida;
		}

		/// <summary>Todas las filas de requisito montadas ahora mismo, en orden. Lo usa la
		/// autoprueba para volcar el estado real de cada una.</summary>
		public List<FilaRequisitoTk> FilasDeRequisito()
		{
			List<FilaRequisitoTk> filas = new List<FilaRequisitoTk>();
			ExecuteRecursively(elemento => {
				FilaRequisitoTk fila = elemento as FilaRequisitoTk;
				if (fila != null) {
					filas.Add(fila);
				}
			});
			return filas;
		}
	}
}
