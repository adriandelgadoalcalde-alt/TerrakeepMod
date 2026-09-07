using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Builds;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Builds
{
	/// <summary>
	/// <b>Contenido</b> del area de Builds (WS4): equipo recomendado por etapa y clase, con marca
	/// de "ya lo tienes" contra el inventario REAL del jugador y un boton de auto-equipar que
	/// <b>solo mueve</b> objetos que ya posee.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Sale de la antigua <c>PanelBuildsState</c> al fusionar los seis paneles en uno solo. Aparte
	/// de bajar de <c>UIState</c> a <c>UIElement</c>, aqui se corrigio la inconsistencia estetica
	/// mas gorda que aparecio al ver las seis piezas juntas: <b>Builds era el unico area que no
	/// usaba los widgets compartidos</b>. Tenia su propio marco de 980x600 fijo (los demas se
	/// estiran al 96%/94% con tope 1080x700), sus botones eran <c>UITextPanel</c> pelados sin
	/// animacion ni sonido, y repetia a mano los colores de <see cref="EstiloTk"/> con literales
	/// copiados. Ahora todo son <see cref="BotonTk"/> y la paleta comun, asi que hereda gratis la
	/// animacion de hover del widget compartido.
	/// </para>
	/// <para>
	/// Lo que NO ha cambiado es la logica: el catalogo, la resolucion de pid, "ya lo tienes" y el
	/// auto-equipar siguen siendo exactamente los de WS4.
	/// </para>
	/// </remarks>
	public class ContenidoBuilds : UIElement
	{
		private const int FotogramasEntreRefrescos = 15;

		/// <summary>Alto MINIMO de la cabecera (subtitulo + una sola linea de resumen). Ver
		/// <see cref="RecalcularCabecera"/>: crece de verdad cuando el mensaje de resultado no
		/// cabe en una linea, nunca se queda en un numero fijo que recorte texto.</summary>
		private const float AltoCabeceraBase = 52f;
		private const float TopResumen = 20f;
		private const float EscalaResumen = 0.78f;
		private const float AltoFila = EstiloTk.AltoPestana;
		private const float SeparacionFilas = 4f;
		private const float AltoPie = 40f;

		private UIPanel _cabecera;
		private UIElement _filaFuentes;
		private UIElement _filaEtapas;
		private UIElement _filaClases;
		private UIElement _filaLoadout;
		private UIElement _cuerpo;
		private EtiquetaTk _resumen;
		private EtiquetaTk _subtitulo;
		private BotonTk _botonAutoEquipar;
		private readonly List<BotonTk> _botonesLoadoutObjetivo = new List<BotonTk>();

		/// <summary>Alto REAL de la cabecera ahora mismo (>= <see cref="AltoCabeceraBase"/>).
		/// Lo ajusta <see cref="RecalcularCabecera"/> a lo que el mensaje de resultado ocupe de
		/// verdad, nunca al reves.</summary>
		private float _altoCabecera = AltoCabeceraBase;

		/// <summary>Texto de <see cref="_resumen"/> YA partido en las lineas que le tocan este
		/// fotograma (ver <see cref="RecalcularCabecera"/>). Se recalcula cada fotograma porque el
		/// mensaje, el idioma y el ancho real de la ventana pueden cambiar en cualquier momento.</summary>
		private string _resumenPartido = "";

		/// <summary>Parametros de la ULTIMA llamada real a <see cref="ColocarFilas"/>, para que
		/// <see cref="RecalcularCabecera"/> pueda repetirla con los mismos datos cuando el alto de
		/// la cabecera cambia, sin tener que rehacer todo <see cref="Reconstruir"/>.</summary>
		private bool _hayFilaFuentes;
		private bool _hayFilaLoadout;

		private int _indiceFuente;
		private int _indiceEtapa;
		private string _claveClase = "melee";
		private string _textoResumen = "";

		/// <summary>
		/// Mensaje de resultado de la ULTIMA pulsacion de "Auto-equipar", y cuantos fotogramas le
		/// quedan en pantalla antes de volver a la leyenda de colores normal.
		/// <para />
		/// <b>El bug real que arreglan estos dos campos</b>: antes, el resultado se escribia en
		/// <c>_textoResumen</c> (el mismo campo que la leyenda de colores), pero <see cref="Update"/>
		/// llama a <see cref="RefrescarPosesion"/> cada <see cref="FotogramasEntreRefrescos"/>
		/// fotogramas (15, o sea cada 0,25 s a 60 fps) para que "ya lo tienes" no se quede desfasado,
		/// y <see cref="ActualizarResumen"/> reescribe <c>_textoResumen</c> con la leyenda en CADA
		/// llamada. El resultado real: el mensaje de "movidos=X, ya colocados=Y..." se pisaba solo
		/// con la leyenda antes de un cuarto de segundo, un tiempo demasiado corto para leerlo -
		/// visualmente indistinguible de "el boton no ha hecho nada", que es justo lo que reporto el
		/// usuario probando el mod de verdad ("build se sigue sin aplicar"). Auto-equipar SI escribia
		/// en los arrays reales del jugador (ver <see cref="AutoEquipar"/>), pero la unica prueba
		/// visible de ello dentro del panel desaparecia antes de que un humano pudiera leerla.
		/// <para />
		/// Con estos dos campos, aparte, el color y el texto (ver <see cref="MostrarResultadoAutoEquipar"/>)
		/// dependen de que paso de verdad: si no se movio NADA porque el jugador no tiene ni un solo
		/// objeto de la build (auto-equipar solo MUEVE lo que ya posees, nunca crea nada - ver el
		/// XMLdoc de <see cref="AutoEquipar"/>), se enseña un aviso claro en vez de un resumen generico
		/// con todo a cero que se leeria igual que un fallo silencioso.
		/// </summary>
		private string _mensajeResultado = "";
		private int _fotogramasMensajeResultado;
		private Color _colorMensajeResultado = EstiloTk.TextoSuave;

		/// <summary>Cuanto tiempo se enseña el mensaje de resultado antes de volver a la leyenda:
		/// 6 segundos a 60 fps. Mucho mas que el cuarto de segundo que duraba antes del arreglo, y de
		/// sobra para leer una frase de una linea.</summary>
		private const int DuracionMensajeResultado = 60 * 6;

		/// <summary>
		/// A cual de los tres conjuntos de equipo (0/1/2) va a parar el auto-equipar. -1 significa
		/// "todavia sin fijar": se inicializa al conjunto ACTIVO del jugador la primera vez que
		/// hace falta (ver <see cref="LoadoutObjetivoValido"/>), no antes, porque en el constructor
		/// el jugador puede no estar listo del todo.
		/// </summary>
		private int _loadoutObjetivo = -1;

		private const float ArribaPrimeraFila = 28f;

		/// <summary>
		/// Paso minimo entre filas de objeto.
		/// <para />
		/// Era 45 (el lado de la ranura a escala 0,85 mas un pixel de aire) y no bastaba: a
		/// 1600x900 el juego usa escala de interfaz 1,47 y la pantalla logica se queda en 1090x613,
		/// asi que al cuerpo le sobran ~218 px para SIETE filas de accesorio (mas la fila extra de
		/// fuentes que añade Calamity). Con 45 salian cinco y media y las dos ultimas quedaban
		/// cortadas por abajo - visto en una captura real con Calamity a esa resolucion. Ahora,
		/// cuando el paso baja de 45, la RANURA se encoge con el (hasta 0,55) igual que en la
		/// pestaña de Equipo, en vez de dejar filas fuera.
		/// </summary>
		private const float PasoMinimo = 30f;

		private const float PasoMaximo = 52f;

		/// <summary>Escala de ranura ideal y minima.</summary>
		private const float EscalaSlotMaxima = 0.85f;
		private const float EscalaSlotMinima = 0.55f;

		private readonly List<SlotCatalogoBuild> _slots = new List<SlotCatalogoBuild>();
		private readonly List<UIText> _etiquetasSlot = new List<UIText>();
		private readonly List<FilaObjeto> _filas = new List<FilaObjeto>();
		private int _filasMaximas;
		private float _altoColocado;
		private int _contadorRefresco;
		private bool _coordenadasRegistradas;

		/// <summary>Fuente seleccionada (Vanilla / Calamity), o null si no hay catalogo.</summary>
		public FuenteBuilds FuenteActual
		{
			get
			{
				IReadOnlyList<FuenteBuilds> fuentes = CatalogoBuilds.Fuentes;
				return fuentes.Count == 0 ? null : fuentes[Utils.Clamp(_indiceFuente, 0, fuentes.Count - 1)];
			}
		}

		public EtapaBuild EtapaActual
		{
			get
			{
				FuenteBuilds fuente = FuenteActual;
				if (fuente == null || fuente.Etapas.Count == 0) {
					return null;
				}
				return fuente.Etapas[Utils.Clamp(_indiceEtapa, 0, fuente.Etapas.Count - 1)];
			}
		}

		public ClaseBuild ClaseActual
		{
			get
			{
				EtapaBuild etapa = EtapaActual;
				if (etapa == null || etapa.Clases.Count == 0) {
					return null;
				}
				return etapa.BuscarClase(_claveClase) ?? etapa.Clases[0];
			}
		}

		public ContenidoBuilds()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			_cabecera = new UIPanel();
			_cabecera.Width.Set(0f, 1f);
			_cabecera.Height.Set(_altoCabecera, 0f);
			_cabecera.BackgroundColor = EstiloTk.FondoCaja;
			_cabecera.BorderColor = new Color(0, 0, 0, 0);
			_cabecera.SetPadding(8f);
			Append(_cabecera);

			_subtitulo = new EtiquetaTk(() => _subtituloTexto, 0.85f, 900f, 24f);
			_subtitulo.Left.Set(0f, 0f);
			_subtitulo.Top.Set(0f, 0f);
			_cabecera.Append(_subtitulo);

			// Mientras el mensaje de resultado del ultimo auto-equipar siga vivo
			// (_fotogramasMensajeResultado > 0) se enseña el, no la leyenda de colores - ver el
			// comentario de esos dos campos mas arriba para el porque.
			//
			// El texto se PARTE en lineas (EtiquetaTk.PartirEnLineas, mismo patron real que
			// PestanaMundo.RecalcularAviso) y la cabecera CRECE para que quepan enteras - nunca se
			// recorta con "...". Con el detalle real de "sin sitio" (ver ConstruirDetalleSinSitio)
			// el mensaje puede llegar a enumerar varios objetos y su motivo, mas largo que
			// cualquier texto que enseñara este campo antes; un recorte con "..." (probado primero)
			// dejaba el motivo real ilegible a media frase, que es justo el problema que este mismo
			// mensaje queria arreglar. Ver <see cref="RecalcularCabecera"/>.
			_resumen = new EtiquetaTk(() => _resumenPartido, EscalaResumen, 900f, 22f);
			_resumen.Width.Set(0f, 1f);
			_resumen.ColorTexto = EstiloTk.TextoSuave;
			_resumen.Left.Set(0f, 0f);
			_resumen.Top.Set(TopResumen, 0f);
			_cabecera.Append(_resumen);

			_filaFuentes = NuevaFila();
			_filaEtapas = NuevaFila();
			_filaClases = NuevaFila();
			_filaLoadout = NuevaFila();

			_cuerpo = new UIElement();
			_cuerpo.Width.Set(0f, 1f);
			// Sin esto, una clase con muchos objetos se sale del marco del panel y pinta por
			// encima del pie. OverflowHidden recorta con el rectangulo de tijera del propio motor.
			_cuerpo.OverflowHidden = true;
			Append(_cuerpo);

			_botonAutoEquipar = new BotonTk(Idiomas.Texto("Builds.AutoEquipar"), EstiloTk.EscalaBoton);
			_botonAutoEquipar.Width.Set(210f, 0f);
			_botonAutoEquipar.Height.Set(34f, 0f);
			_botonAutoEquipar.VAlign = 1f;
			_botonAutoEquipar.Ayuda = () => Idiomas.Texto("Builds.AutoEquiparAyuda");
			_botonAutoEquipar.AlPulsar += EjecutarAutoEquipar;
			Append(_botonAutoEquipar);

			Reconstruir();
		}

		private string _subtituloTexto = "";

		private UIElement NuevaFila()
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(AltoFila, 0f);
			Append(fila);
			return fila;
		}

		/// <summary>Rehace las pildoras y las tres columnas con el filtro seleccionado ahora.</summary>
		private void Reconstruir()
		{
			_slots.Clear();
			_etiquetasSlot.Clear();
			_filas.Clear();
			_filasMaximas = 0;
			_altoColocado = 0f;
			_filaFuentes.RemoveAllChildren();
			_filaEtapas.RemoveAllChildren();
			_filaClases.RemoveAllChildren();
			_filaLoadout.RemoveAllChildren();
			_cuerpo.RemoveAllChildren();

			FuenteBuilds fuente = FuenteActual;
			if (fuente == null) {
				_subtituloTexto = Idiomas.Texto("Builds.SinCatalogo");
				ColocarFilas(false, false);
				Recalculate();
				return;
			}

			// Valores de partida; RefrescarPosesion los deja con los numeros reales al final.
			ActualizarResumen(0, 0);

			// Fila 1: fuente de datos. Solo se enseña si hay mas de una (Calamity sin instalar
			// deja una sola y la fila sobra).
			IReadOnlyList<FuenteBuilds> fuentes = CatalogoBuilds.Fuentes;
			bool hayFilaFuentes = fuentes.Count > 1;
			if (hayFilaFuentes) {
				List<string> etiquetas = new List<string>();
				foreach (FuenteBuilds f in fuentes) {
					etiquetas.Add(f.Etiqueta);
				}
				PintarPildoras(_filaFuentes, etiquetas, _indiceFuente, indice => {
					_indiceFuente = indice;
					_indiceEtapa = 0;
					Reconstruir();
				});
			}

			// Fila del conjunto de DESTINO (a cual de los 3 loadouts va el auto-equipar).
			// Independiente de fuente/etapa/clase, asi que se pinta siempre que hay catalogo, sin
			// esperar a que exista una clase seleccionable.
			PintarSelectorLoadoutObjetivo();

			// Fila 2: etapa de progresion.
			List<string> etapas = new List<string>();
			foreach (EtapaBuild e in fuente.Etapas) {
				etapas.Add(e.Etiqueta);
			}
			PintarPildoras(_filaEtapas, etapas, _indiceEtapa, indice => {
				_indiceEtapa = indice;
				Reconstruir();
			});

			// Fila 3: clase.
			EtapaBuild etapa = EtapaActual;
			if (etapa == null) {
				ColocarFilas(hayFilaFuentes, true);
				Recalculate();
				return;
			}

			List<string> clases = new List<string>();
			int indiceClase = 0;
			for (int i = 0; i < etapa.Clases.Count; i++) {
				clases.Add(etapa.Clases[i].Etiqueta);
				if (etapa.Clases[i].Clave == _claveClase) {
					indiceClase = i;
				}
			}
			PintarPildoras(_filaClases, clases, indiceClase, indice => {
				_claveClase = etapa.Clases[indice].Clave;
				Reconstruir();
			});

			ClaseBuild clase = ClaseActual;
			if (clase == null) {
				ColocarFilas(hayFilaFuentes, true);
				Recalculate();
				return;
			}

			PintarColumna(0, "Builds.Armadura", clase.Armadura);
			PintarColumna(1, "Builds.Armas", clase.Armas);
			PintarColumna(2, "Builds.Accesorios", clase.Accesorios);

			ColocarFilas(hayFilaFuentes, true);
			Recalculate();
			ColocarFilasDeObjetos();
			RefrescarPosesion();
		}

		/// <summary>
		/// Coloca las filas de pildoras y el cuerpo. Se hace aqui y no en el constructor porque la
		/// fila de fuentes puede no existir (sin Calamity) y la de conjunto de destino tampoco sin
		/// catalogo cargado, y dejar su hueco vacio era un agujero de 34 px en mitad del panel.
		/// </summary>
		private void ColocarFilas(bool hayFilaFuentes, bool hayFilaLoadout)
		{
			_hayFilaFuentes = hayFilaFuentes;
			_hayFilaLoadout = hayFilaLoadout;

			float y = _altoCabecera + 6f;

			_filaFuentes.Top.Set(y, 0f);
			_filaFuentes.Height.Set(hayFilaFuentes ? AltoFila : 0f, 0f);
			if (hayFilaFuentes) {
				y += AltoFila + SeparacionFilas;
			}

			_filaEtapas.Top.Set(y, 0f);
			y += AltoFila + SeparacionFilas;

			_filaClases.Top.Set(y, 0f);
			y += AltoFila + SeparacionFilas;

			_filaLoadout.Top.Set(y, 0f);
			_filaLoadout.Height.Set(hayFilaLoadout ? AltoFila : 0f, 0f);
			y += (hayFilaLoadout ? AltoFila : 0f) + 8f;

			_cuerpo.Top.Set(y, 0f);
			_cuerpo.Height.Set(-(y + AltoPie), 1f);
		}

		/// <summary>
		/// Una fila de pildoras. Son <see cref="BotonTk"/> normales (no <c>UITextPanel</c> pelados
		/// como antes), asi que comparten animacion, sonido y paleta con todos los demas botones
		/// del mod. El ancho se reparte en porcentaje para que la fila se estire con el panel.
		/// </summary>
		private void PintarPildoras(UIElement fila, List<string> etiquetas, int seleccionada, Action<int> alPulsar)
		{
			if (etiquetas.Count == 0) {
				return;
			}

			float fraccion = 1f / etiquetas.Count;

			for (int i = 0; i < etiquetas.Count; i++) {
				int indice = i;   // copia local: sin esto todas las lambdas compartirian la variable
				BotonTk pildora = new BotonTk(EstiloInvestigacionAcortar(etiquetas[i], 34), 0.75f);
				pildora.EsPestana = true;
				pildora.Activo = i == seleccionada;
				pildora.Width.Set(-EstiloTk.SeparacionPestanas, fraccion);
				pildora.Height.Set(AltoFila, 0f);
				pildora.Left.Set(0f, i * fraccion);
				string completa = etiquetas[i];
				pildora.Ayuda = () => completa;
				pildora.AlPulsar += () => alPulsar(indice);
				fila.Append(pildora);
			}
		}

		/// <summary>Una de las tres columnas. Recibe la CLAVE de localizacion del titulo, no el
		/// texto ya resuelto, para que cambie con el idioma sin reabrir el panel.</summary>
		private void PintarColumna(int columna, string claveTitulo, List<ObjetoBuild> objetos)
		{
			const float separacion = 14f;
			float fraccion = 1f / 3f;

			UIElement contenedor = new UIElement();
			contenedor.Width.Set(-separacion, fraccion);
			contenedor.Height.Set(0f, 1f);
			contenedor.Left.Set(0f, columna * fraccion);
			_cuerpo.Append(contenedor);

			EtiquetaTk cabecera = new EtiquetaTk(() => Idiomas.Texto(claveTitulo), 0.85f, 200f, 24f);
			contenedor.Append(cabecera);

			int fila = 0;
			foreach (ObjetoBuild objeto in objetos) {
				SlotCatalogoBuild slot = new SlotCatalogoBuild(objeto);
				slot.Left.Set(0f, 0f);
				contenedor.Append(slot);
				_slots.Add(slot);

				UIText etiqueta = new UIText(EstiloInvestigacionAcortar(objeto.Nombre, 26), 0.72f);
				etiqueta.Left.Set(54f, 0f);
				contenedor.Append(etiqueta);
				_etiquetasSlot.Add(etiqueta);

				string extra = objeto.Resuelto
					? (string.IsNullOrEmpty(objeto.PrefijoRecomendado)
						? ""
						: Idiomas.Texto("Builds.PrefijoSugerido", objeto.PrefijoRecomendado))
					: Idiomas.Texto("Builds.NoExisteAqui");
				UIText pie = null;
				if (!string.IsNullOrEmpty(extra)) {
					pie = new UIText(EstiloInvestigacionAcortar(extra, 34), 0.62f);
					pie.Left.Set(54f, 0f);
					pie.TextColor = objeto.Resuelto ? EstiloTk.Neutro : EstiloTk.Peligro;
					contenedor.Append(pie);
				}

				_filas.Add(new FilaObjeto(slot, etiqueta, pie, fila));
				fila++;
				if (fila > _filasMaximas) {
					_filasMaximas = fila;
				}
			}

			if (objetos.Count == 0) {
				EtiquetaTk vacio = new EtiquetaTk(() => Idiomas.Texto("Builds.Nada"), 0.7f, 120f, 22f);
				vacio.Top.Set(28f, 0f);
				vacio.ColorTexto = EstiloTk.Neutro;
				contenedor.Append(vacio);
			}
		}

		/// <summary>Una fila de objeto del catalogo: la ranura, su nombre y su pie, mas en que
		/// posicion va dentro de su columna. Se guardan para poder recolocarlas cuando se sabe el
		/// alto REAL del cuerpo, que en el constructor todavia no existe.</summary>
		private class FilaObjeto
		{
			public readonly SlotCatalogoBuild Slot;
			public readonly UIElement Nombre;
			public readonly UIElement Pie;
			public readonly int Indice;

			public FilaObjeto(SlotCatalogoBuild slot, UIElement nombre, UIElement pie, int indice)
			{
				Slot = slot;
				Nombre = nombre;
				Pie = pie;
				Indice = indice;
			}
		}

		/// <summary>
		/// Reparte las filas de objeto por el alto que de verdad tiene el cuerpo.
		/// <para />
		/// Con un paso fijo NO vale, y se vio en una captura real: la clase de accesorios mas larga
		/// tiene 7 objetos, y en cuanto aparece la fila de fuentes (que solo sale con Calamity
		/// instalado) se comen 34 px mas y el septimo quedaba cortado por abajo. Aqui el paso se
		/// calcula con el hueco real y se acota entre <see cref="PasoMinimo"/> (el lado de la
		/// ranura, 44, mas 1 px de aire) y <see cref="PasoMaximo"/>, para que con pocas filas no se
		/// separen tanto que parezcan sueltas.
		/// </summary>
		private void ColocarFilasDeObjetos()
		{
			float alto = _cuerpo.GetDimensions().Height;
			if (alto <= 0f || _filas.Count == 0) {
				return;
			}

			float paso = PasoMaximo;
			if (_filasMaximas > 0) {
				paso = (alto - ArribaPrimeraFila) / _filasMaximas;
			}
			if (paso > PasoMaximo) {
				paso = PasoMaximo;
			}
			if (paso < PasoMinimo) {
				paso = PasoMinimo;
			}

			// La ranura solo encoge cuando el paso se queda por debajo de su tamaño natural.
			float escalaSlot = (paso - 1f) / 52f;
			if (escalaSlot > EscalaSlotMaxima) {
				escalaSlot = EscalaSlotMaxima;
			}
			if (escalaSlot < EscalaSlotMinima) {
				escalaSlot = EscalaSlotMinima;
			}

			// Con las filas apretadas, el prefijo sugerido de debajo del nombre ya no cabe: se
			// esconde en vez de pintarse encima de la fila siguiente.
			bool cabeElPie = paso >= 44f;

			foreach (FilaObjeto f in _filas) {
				float y = ArribaPrimeraFila + f.Indice * paso;
				f.Slot.Escala = escalaSlot;
				f.Slot.Top.Set(y, 0f);
				f.Nombre.Top.Set(y + (paso - 20f) / 2f, 0f);
				if (f.Pie != null) {
					f.Pie.Top.Set(y + 26f, 0f);
					if (cabeElPie) {
						f.Pie.Left.Set(54f, 0f);
					}
					else {
						// Fuera de la vista: OverflowHidden del cuerpo lo recorta.
						f.Pie.Left.Set(-4000f, 0f);
					}
				}
			}

			_altoColocado = alto;
			_cuerpo.Recalculate();
		}

		// Se corta con "..." de tres puntos normales y no con el caracter "…": la fuente del juego
		// solo tiene el juego de caracteres con el que se genero, y un caracter que no esta hace
		// reventar a DynamicSpriteFont al medir el texto. Es la misma funcion que ya tenia
		// EstiloInvestigacion; se llama a esa para no tener dos copias.
		private static string EstiloInvestigacionAcortar(string texto, int maximo)
		{
			return Investigacion.EstiloInvestigacion.Acortar(texto, maximo);
		}

		/// <summary>Selecciona una clase por su clave (melee/ranged/mage/summoner/rogue) y rehace
		/// el area. La usa el arnes de pruebas para fijar la clase sin simular clics.</summary>
		public void SeleccionarClase(string clave)
		{
			if (string.IsNullOrEmpty(clave)) {
				return;
			}
			_claveClase = clave;
			Reconstruir();
		}

		/// <summary>
		/// Pulsa de verdad la pildora de clase que hay en la posicion indicada, disparando su
		/// <c>OnLeftClick</c> con <c>UIElement.LeftClick</c> (el mismo camino exacto que recorre
		/// un clic de raton una vez resuelto sobre que elemento cae). Devuelve la etiqueta de la
		/// pildora pulsada, o null si no hay ninguna en esa posicion.
		/// </summary>
		public string PulsarPildoraClase(int indice)
		{
			int i = 0;
			foreach (UIElement hijo in _filaClases.Children) {
				if (i++ != indice) {
					continue;
				}
				BotonTk pildora = hijo as BotonTk;
				if (pildora == null) {
					return null;
				}
				string etiqueta = pildora.Texto;
				pildora.LeftClick(new UIMouseEvent(pildora, pildora.GetDimensions().Center()));
				return etiqueta;
			}
			return null;
		}

		/// <summary>Selecciona una fuente por su clave ("vanilla" / "calamity"). Devuelve false si
		/// esa fuente no esta disponible en esta partida.</summary>
		public bool SeleccionarFuente(string clave)
		{
			if (string.IsNullOrEmpty(clave)) {
				return false;
			}

			IReadOnlyList<FuenteBuilds> fuentes = CatalogoBuilds.Fuentes;
			for (int i = 0; i < fuentes.Count; i++) {
				if (fuentes[i].Clave == clave) {
					_indiceFuente = i;
					_indiceEtapa = 0;
					Reconstruir();
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Pinta las pildoras de "a que conjunto de equipo va el auto-equipar" (1/2/3). Es
		/// independiente de fuente/etapa/clase: se rehace entera en cada <see cref="Reconstruir"/>,
		/// igual que las demas filas, pero su seleccion (<see cref="_loadoutObjetivo"/>) sobrevive
		/// a la reconstruccion porque es un campo aparte, no algo que se lea de la pildora.
		/// </summary>
		private void PintarSelectorLoadoutObjetivo()
		{
			_botonesLoadoutObjetivo.Clear();

			Player jugador = Main.LocalPlayer;
			if (jugador == null) {
				return;
			}

			int total = jugador.Loadouts.Length;
			int objetivo = LoadoutObjetivoValido(jugador);

			List<string> etiquetas = new List<string>();
			for (int i = 0; i < total; i++) {
				etiquetas.Add(Idiomas.Texto("Builds.ConjuntoDestinoPildora", i + 1));
			}

			PintarPildoras(_filaLoadout, etiquetas, objetivo, indice => {
				_loadoutObjetivo = indice;
				ActualizarBotonesLoadoutObjetivo();
			});

			foreach (UIElement hijo in _filaLoadout.Children) {
				BotonTk boton = hijo as BotonTk;
				if (boton == null) {
					continue;
				}
				boton.Ayuda = () => Idiomas.Texto("Builds.ConjuntoDestinoAyuda");
				_botonesLoadoutObjetivo.Add(boton);
			}

			ActualizarBotonesLoadoutObjetivo();
		}

		/// <summary>
		/// Refresca el rotulo (con la marca de "activo" si toca) y el resaltado de las pildoras de
		/// conjunto de destino. Hace falta en cada Update y no solo al reconstruir: el jugador
		/// puede cambiar de conjunto ACTIVO con las teclas del propio juego mientras el panel de
		/// Builds sigue abierto, y la marca tiene que seguirlo (igual que ya hace
		/// <c>PestanaEquipo.ActualizarBotonesLoadout</c> en el panel de Personaje).
		/// </summary>
		private void ActualizarBotonesLoadoutObjetivo()
		{
			Player jugador = Main.LocalPlayer;
			if (jugador == null) {
				return;
			}

			for (int i = 0; i < _botonesLoadoutObjetivo.Count; i++) {
				bool esActivo = EquipoJugador.EsLoadoutActivo(jugador, i);
				string etiqueta = Idiomas.Texto("Builds.ConjuntoDestinoPildora", i + 1);
				if (esActivo) {
					// El espacio va aqui y no dentro del .hjson: ningun valor de esos archivos
					// puede empezar ni acabar con espacio (tModLoader los reescribe y se lo come).
					etiqueta += " " + Idiomas.Texto("Builds.ConjuntoActivoMarca");
				}
				_botonesLoadoutObjetivo[i].FijarTexto(EstiloInvestigacionAcortar(etiqueta, 34));
				_botonesLoadoutObjetivo[i].Activo = i == _loadoutObjetivo;
			}
		}

		/// <summary>
		/// El conjunto de destino elegido (0/1/2), ya dentro de rango. Si todavia no se ha tocado
		/// el selector (<see cref="_loadoutObjetivo"/> == -1, recien abierto el panel), se fija al
		/// conjunto ACTIVO del jugador ahora mismo: es el comportamiento mas util por defecto,
		/// coincide con "se ve al instante" sin que el jugador tenga que tocar nada.
		/// </summary>
		private int LoadoutObjetivoValido(Player jugador)
		{
			int total = jugador.Loadouts.Length;
			if (_loadoutObjetivo < 0 || _loadoutObjetivo >= total) {
				_loadoutObjetivo = jugador.CurrentLoadoutIndex;
			}
			return _loadoutObjetivo;
		}

		/// <summary>El conjunto de destino elegido ahora mismo (0/1/2). Lo usa la autoprueba.</summary>
		public int LoadoutObjetivo => _loadoutObjetivo;

		/// <summary>Texto del ultimo mensaje de resultado de auto-equipar (vacio si nunca se pulso
		/// el boton en esta sesion del panel). Lo usa la autoprueba para confirmar SIN capturas que
		/// el mensaje sigue vivo varios fotogramas despues del clic, no solo en el instante.</summary>
		public string MensajeResultado => _mensajeResultado;

		/// <summary>Fotogramas que le quedan al mensaje de resultado antes de volver a la leyenda de
		/// colores. 0 = ya no se enseña (o nunca se pulso el boton).</summary>
		public int FotogramasMensajeResultado => _fotogramasMensajeResultado;

		/// <summary>Fija el conjunto de destino sin pulsar ninguna pildora. Lo usa la autoprueba
		/// para preparar el escenario sin depender de coordenadas de clic.</summary>
		public void SeleccionarLoadoutObjetivo(int indice)
		{
			_loadoutObjetivo = indice;
			ActualizarBotonesLoadoutObjetivo();
		}

		/// <summary>
		/// Pulsa de verdad la pildora de conjunto de destino en la posicion indicada, con
		/// <c>UIElement.LeftClick</c> (el mismo camino que <see cref="PulsarPildoraClase"/> ya usa
		/// para las pildoras de clase). Devuelve false si no hay ninguna en esa posicion.
		/// </summary>
		public bool PulsarPildoraLoadoutObjetivo(int indice)
		{
			int i = 0;
			foreach (UIElement hijo in _filaLoadout.Children) {
				if (i++ != indice) {
					continue;
				}
				BotonTk pildora = hijo as BotonTk;
				if (pildora == null) {
					return false;
				}
				pildora.LeftClick(new UIMouseEvent(pildora, pildora.GetDimensions().Center()));
				return true;
			}
			return false;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// Antes que nada: el mensaje de resultado (o la leyenda) puede haber cambiado de largo
			// este mismo fotograma (nuevo auto-equipar, cuenta atras que expira, cambio de idioma en
			// vivo desde Ajustes, redimensionar la ventana...), asi que la cabecera se ajusta ANTES
			// de que nada mas de este fotograma dependa de su alto real.
			RecalcularCabecera();

			// La posesion cambia mientras el panel esta abierto (el jugador puede mover cosas con
			// los slots del propio juego), pero recorrer los ~200 slots de todos los contenedores
			// 60 veces por segundo no aporta nada. Cada 15 fotogramas (4 veces/s) va sobrado.
			if (++_contadorRefresco >= FotogramasEntreRefrescos) {
				_contadorRefresco = 0;
				RefrescarPosesion();
			}

			// El alto real del cuerpo no existe hasta que el motor ha recalculado el arbol, y cambia
			// si el jugador redimensiona la ventana. Se recolocan las filas cuando cambia.
			if (System.Math.Abs(_cuerpo.GetDimensions().Height - _altoColocado) > 1f) {
				ColocarFilasDeObjetos();
			}

			// El rotulo del boton se fija al construirlo, asi que se vuelve a poner cada fotograma
			// para que cambie en vivo con el selector de idioma del area de Ajustes. Las pildoras no
			// lo necesitan (se rehacen enteras con Reconstruir) ni las cabeceras de columna
			// (son EtiquetaTk, que ya piden su texto en cada dibujado).
			_botonAutoEquipar.FijarTexto(Idiomas.Texto("Builds.AutoEquipar"));

			// La marca de "conjunto activo" de las pildoras de destino puede cambiar sola: el
			// jugador puede pulsar las teclas de conjunto de equipo del propio juego mientras el
			// panel de Builds sigue abierto.
			ActualizarBotonesLoadoutObjetivo();

			// Cuenta atras del mensaje de resultado de auto-equipar (ver el comentario de
			// _mensajeResultado). El color se refuerza cada fotograma en vez de solo al fijar el
			// mensaje porque _resumen.ColorTexto es un campo mutable compartido con la leyenda.
			if (_fotogramasMensajeResultado > 0) {
				_fotogramasMensajeResultado--;
				_resumen.ColorTexto = _colorMensajeResultado;
			}
			else {
				_resumen.ColorTexto = EstiloTk.TextoSuave;
			}

			RegistrarCoordenadasUnaVez();
		}

		/// <summary>
		/// Deja en el log el rectangulo real en pantalla de los primeros slots. Es la misma
		/// evidencia con la que se cerro WS0: demuestra que los <c>ItemSlot</c> no solo se han
		/// creado, sino que el motor de UI les ha dado una posicion y un tamaño reales dentro de
		/// la pantalla del juego, o sea que estan DIBUJADOS.
		/// </summary>
		private void RegistrarCoordenadasUnaVez()
		{
			if (_coordenadasRegistradas || _slots.Count == 0) {
				return;
			}

			CalculatedStyle marco = GetDimensions();
			if (marco.Width <= 0f) {
				return;
			}

			CalculatedStyle primero = _slots[0].GetDimensions();
			if (primero.Width <= 0f) {
				return;
			}

			_coordenadasRegistradas = true;

			System.Text.StringBuilder texto = new System.Text.StringBuilder();
			texto.Append($"{Terrakeep.LogTag} Area de Builds dibujada. Zona: x={(int)marco.X} y={(int)marco.Y} " +
				$"w={(int)marco.Width} h={(int)marco.Height}. Resolucion actual: {Main.screenWidth}x{Main.screenHeight}. " +
				$"{_slots.Count} slots de catalogo. Primeros: ");
			for (int i = 0; i < _slots.Count && i < 4; i++) {
				CalculatedStyle d = _slots[i].GetDimensions();
				texto.Append($"[\"{_slots[i].Objeto.Nombre}\" x={(int)d.X} y={(int)d.Y} w={(int)d.Width} h={(int)d.Height} " +
					$"loTiene={_slots[i].LoTiene}] ");
			}
			RegistroBuilds.Linea(texto.ToString());
		}

		/// <summary>Recalcula "ya lo tienes" de cada slot contra los contenedores reales.</summary>
		public void RefrescarPosesion()
		{
			Player jugador = Main.LocalPlayer;
			if (jugador == null) {
				return;
			}

			int tiene = 0;
			int resueltos = 0;

			for (int i = 0; i < _slots.Count; i++) {
				SlotCatalogoBuild slot = _slots[i];
				UbicacionObjeto donde = slot.Objeto.Resuelto ? EquipoJugador.Buscar(jugador, slot.Objeto.Tipo) : null;
				slot.LoTiene = donde != null;
				slot.DondeLoTiene = donde?.NombreContenedor;

				if (slot.Objeto.Resuelto) {
					resueltos++;
				}
				if (slot.LoTiene) {
					tiene++;
				}

				if (i < _etiquetasSlot.Count) {
					_etiquetasSlot[i].TextColor = !slot.Objeto.Resuelto ? EstiloTk.Peligro
						: (slot.LoTiene ? EstiloTk.Correcto : EstiloTk.Neutro);
				}
			}

			ActualizarResumen(tiene, resueltos);
		}

		/// <summary>
		/// Las dos lineas de la cabecera. Van repartidas en dos a proposito: en una sola, con la
		/// leyenda de colores y el recuento juntos, el texto se salia del marco por la derecha en
		/// una ventana de 800 px (visto en una captura real del juego).
		/// </summary>
		private void ActualizarResumen(int tiene, int resueltos)
		{
			Player jugador = Main.LocalPlayer;
			// El separador NO va dentro de la clave: ningun valor de los .hjson puede empezar o
			// acabar con espacio (ver la nota de scripts/gen_hjson y la bitacora: tModLoader
			// reescribe los archivos y se los come).
			string ranuras = jugador == null
				? ""
				: "  ·  " + Idiomas.Texto("Builds.RanurasAccesorio",
					EquipoJugador.SlotsAccesorioDisponibles(jugador));
			_subtituloTexto = Idiomas.Texto("Builds.Tienes", tiene, resueltos, ranuras);
			_textoResumen = Idiomas.Texto("Builds.Leyenda");
		}

		/// <summary>Boton "Auto-equipar". Solo mueve objetos que el jugador ya tiene, al conjunto
		/// de equipo elegido en el selector de destino (ver <see cref="PintarSelectorLoadoutObjetivo"/>).</summary>
		public void EjecutarAutoEquipar()
		{
			ClaseBuild clase = ClaseActual;
			EtapaBuild etapa = EtapaActual;
			FuenteBuilds fuente = FuenteActual;
			Player jugador = Main.LocalPlayer;
			if (clase == null || jugador == null) {
				return;
			}

			int objetivo = LoadoutObjetivoValido(jugador);
			ResultadoAutoEquipar resultado = AutoEquipar.Ejecutar(jugador, clase, objetivo);
			AutoEquipar.Registrar(jugador, clase, resultado,
				$"{fuente?.Etiqueta} / {etapa?.Etiqueta}", objetivo);

			RefrescarPosesion();
			MostrarResultadoAutoEquipar(resultado);
		}

		/// <summary>
		/// Fija el mensaje y el color que se van a enseñar tras pulsar "Auto-equipar" (ver
		/// <see cref="_mensajeResultado"/>).
		/// <para />
		/// Desde el cambio de diseño que trae el equipo que no tienes del catalogo (ver el XMLdoc
		/// de <see cref="AutoEquipar"/>), "nada que aplicar" solo pasa de verdad si NINGUN objeto de
		/// la build existe en esta partida (mod de origen no instalado): todo lo demas resuelto cae
		/// en movido, ya colocado, creado o sin sitio.
		/// </para>
		/// <para>
		/// <b>El caso que investigo esta sesion</b> (reportado por el usuario con capturas reales:
		/// "Furia solar" salia "sin sitio" justo al lado de "ranuras de accesorio disponibles: 5",
		/// que parecia una contradiccion): el comportamiento era correcto (el arma necesitaba hueco
		/// en la MOCHILA, no en accesorios - dos recursos distintos; mochila llena de verdad,
		/// verificado en el sandbox), pero el panel no decia POR QUE sin obligar a mirar el log. Por
		/// eso, cuando hay algun "sin sitio", el mensaje ahora incluye el motivo real de cada uno
		/// (<see cref="ResultadoAutoEquipar.DetalleSinSitio"/>), no solo el numero agregado.
		/// </para>
		/// </summary>
		private void MostrarResultadoAutoEquipar(ResultadoAutoEquipar resultado)
		{
			bool nada = resultado.Movidos == 0 && resultado.YaColocados == 0 &&
				resultado.Creados == 0 && resultado.SinSitio == 0;

			if (nada) {
				// Solo pasa si TODOS los objetos de la build son NoResueltos (mod no instalado): con
				// el cambio de diseño, cualquier objeto resuelto acaba movido, ya colocado, creado o
				// sin sitio - nunca "nada que hacer" de verdad.
				_mensajeResultado = Idiomas.Texto("Builds.NadaQueMover");
				_colorMensajeResultado = EstiloTk.TextoAviso;
			}
			else if (resultado.SinSitio > 0) {
				// Algo no cupo (mochila llena / slots de accesorio ocupados o incompatibles / dato
				// de catalogo raro): aviso real, con el motivo de cada objeto para que no parezca un
				// fallo silencioso ni una contradiccion con la cabecera de ranuras disponibles.
				_mensajeResultado = Idiomas.Texto("Builds.ResultadoAutoEquipar", resultado.Resumen) +
					" " + ConstruirDetalleSinSitio(resultado);
				_colorMensajeResultado = EstiloTk.Peligro;
			}
			else {
				// Caso normal: se movio y/o creo algo, o ya estaba todo lo que el jugador tiene
				// colocado en su sitio (aplicar dos veces seguidas la misma build es idempotente).
				_mensajeResultado = Idiomas.Texto("Builds.ResultadoAutoEquipar", resultado.Resumen);
				_colorMensajeResultado = EstiloTk.Correcto;
			}

			_fotogramasMensajeResultado = DuracionMensajeResultado;
		}

		/// <summary>Texto completo (sin partir) que le toca enseñar a <see cref="_resumen"/> ahora
		/// mismo: el mensaje de resultado mientras siga vivo, o la leyenda de colores.</summary>
		private string TextoResumenCompleto() =>
			_fotogramasMensajeResultado > 0 ? _mensajeResultado : _textoResumen;

		/// <summary>
		/// Parte <see cref="TextoResumenCompleto"/> en las lineas que hagan falta para el ancho REAL
		/// de <see cref="_resumen"/> (mismo <see cref="EtiquetaTk.PartirEnLineas"/> que ya usa
		/// <c>PestanaMundo.RecalcularAviso</c>), mide su alto con la fuente real, y CRECE
		/// <see cref="_cabecera"/> hasta ese alto en vez de recortar nada - el texto siempre se lee
		/// entero, sea cual sea su longitud, el idioma activo o la resolucion de la ventana.
		/// <para />
		/// Se llama cada fotograma (ver <see cref="Update"/>) porque las tres cosas de las que
		/// depende pueden cambiar en cualquier momento: el mensaje (nuevo auto-equipar, cuenta
		/// atras que expira y vuelve a la leyenda corta), el idioma (Ajustes cambia en vivo) y el
		/// ancho (el jugador redimensiona la ventana).
		/// </summary>
		private void RecalcularCabecera()
		{
			if (_cabecera == null || _resumen == null) {
				return;
			}

			float anchoInterior = _resumen.GetDimensions().Width;
			if (anchoInterior <= 0f) {
				// Layout todavia no calculado (primerisimo fotograma): se reintenta solo, como en
				// PestanaMundo.RecalcularAviso.
				return;
			}

			string partido = EtiquetaTk.PartirEnLineas(TextoResumenCompleto(), anchoInterior, EscalaResumen);
			_resumenPartido = partido;

			float altoResumen = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(partido).Y * EscalaResumen;
			float altoNecesario = TopResumen + altoResumen + 8f;
			float altoNuevo = altoNecesario > AltoCabeceraBase ? altoNecesario : AltoCabeceraBase;

			if (System.Math.Abs(altoNuevo - _altoCabecera) > 0.5f) {
				_altoCabecera = altoNuevo;
				_cabecera.Height.Set(_altoCabecera, 0f);
				ColocarFilas(_hayFilaFuentes, _hayFilaLoadout);
				Recalculate();
			}
		}

		/// <summary>Une el motivo REAL de cada objeto "sin sitio" (ver <see cref="CausaSinSitio"/>)
		/// en una sola frase localizada, en vez de dejar que el jugador tenga que ir al log a
		/// averiguar por que.</summary>
		private static string ConstruirDetalleSinSitio(ResultadoAutoEquipar resultado)
		{
			if (resultado.DetalleSinSitio.Count == 0) {
				return "";
			}

			List<string> partes = new List<string>();
			foreach (ItemSinSitio item in resultado.DetalleSinSitio) {
				partes.Add(Idiomas.Texto("Builds.SinSitioItem", item.Nombre, TextoCausaSinSitio(item.Causa)));
			}

			return Idiomas.Texto("Builds.SinSitioDetalle", string.Join(", ", partes));
		}

		private static string TextoCausaSinSitio(CausaSinSitio causa)
		{
			switch (causa) {
				case CausaSinSitio.MochilaLlena:
					return Idiomas.Texto("Builds.CausaSinSitio.MochilaLlena");
				case CausaSinSitio.SlotAccesorioOcupado:
					return Idiomas.Texto("Builds.CausaSinSitio.SlotAccesorio");
				case CausaSinSitio.NoEsPiezaDeArmadura:
					return Idiomas.Texto("Builds.CausaSinSitio.NoArmadura");
				default:
					return "";
			}
		}

		/// <summary>
		/// Pulsa DE VERDAD el boton "Auto-equipar", disparando su <c>OnLeftClick</c> con
		/// <c>UIElement.LeftClick</c> en su centro real de pantalla - el mismo camino exacto que
		/// recorre un clic de raton real, y no una llamada directa a <see cref="EjecutarAutoEquipar"/>
		/// que se salte el boton. Lo usa la autoprueba para reproducir el camino EXACTO de un usuario
		/// real (abrir panel -> pestaña Builds -> elegir build -> elegir conjunto -> pulsar el boton),
		/// en vez de invocar la logica por dentro y dar por hecho que el clic real llega igual.
		/// </summary>
		public void PulsarBotonAutoEquipar()
		{
			CalculatedStyle dim = _botonAutoEquipar.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			_botonAutoEquipar.LeftClick(new UIMouseEvent(_botonAutoEquipar, centro));
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
