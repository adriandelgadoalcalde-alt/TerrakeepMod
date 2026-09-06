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
		private const float AltoCabecera = 52f;
		private const float AltoFila = EstiloTk.AltoPestana;
		private const float SeparacionFilas = 4f;
		private const float AltoPie = 40f;

		private UIElement _filaFuentes;
		private UIElement _filaEtapas;
		private UIElement _filaClases;
		private UIElement _cuerpo;
		private EtiquetaTk _resumen;
		private EtiquetaTk _subtitulo;
		private BotonTk _botonAutoEquipar;

		private int _indiceFuente;
		private int _indiceEtapa;
		private string _claveClase = "melee";
		private string _textoResumen = "";

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

			UIPanel cabecera = new UIPanel();
			cabecera.Width.Set(0f, 1f);
			cabecera.Height.Set(AltoCabecera, 0f);
			cabecera.BackgroundColor = EstiloTk.FondoCaja;
			cabecera.BorderColor = new Color(0, 0, 0, 0);
			cabecera.SetPadding(8f);
			Append(cabecera);

			_subtitulo = new EtiquetaTk(() => _subtituloTexto, 0.85f, 900f, 24f);
			_subtitulo.Left.Set(0f, 0f);
			_subtitulo.Top.Set(0f, 0f);
			cabecera.Append(_subtitulo);

			_resumen = new EtiquetaTk(() => _textoResumen, 0.78f, 900f, 22f);
			_resumen.ColorTexto = EstiloTk.TextoSuave;
			_resumen.Left.Set(0f, 0f);
			_resumen.Top.Set(20f, 0f);
			cabecera.Append(_resumen);

			_filaFuentes = NuevaFila();
			_filaEtapas = NuevaFila();
			_filaClases = NuevaFila();

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
			_cuerpo.RemoveAllChildren();

			FuenteBuilds fuente = FuenteActual;
			if (fuente == null) {
				_subtituloTexto = Idiomas.Texto("Builds.SinCatalogo");
				ColocarFilas(false);
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
				ColocarFilas(hayFilaFuentes);
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
				ColocarFilas(hayFilaFuentes);
				Recalculate();
				return;
			}

			PintarColumna(0, "Builds.Armadura", clase.Armadura);
			PintarColumna(1, "Builds.Armas", clase.Armas);
			PintarColumna(2, "Builds.Accesorios", clase.Accesorios);

			ColocarFilas(hayFilaFuentes);
			Recalculate();
			ColocarFilasDeObjetos();
			RefrescarPosesion();
		}

		/// <summary>
		/// Coloca las tres filas de pildoras y el cuerpo. Se hace aqui y no en el constructor
		/// porque la fila de fuentes puede no existir (sin Calamity), y dejar su hueco vacio era un
		/// agujero de 34 px en mitad del panel.
		/// </summary>
		private void ColocarFilas(bool hayFilaFuentes)
		{
			float y = AltoCabecera + 6f;

			_filaFuentes.Top.Set(y, 0f);
			_filaFuentes.Height.Set(hayFilaFuentes ? AltoFila : 0f, 0f);
			if (hayFilaFuentes) {
				y += AltoFila + SeparacionFilas;
			}

			_filaEtapas.Top.Set(y, 0f);
			y += AltoFila + SeparacionFilas;

			_filaClases.Top.Set(y, 0f);
			y += AltoFila + 8f;

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

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

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
			string ranuras = jugador == null
				? ""
				: Idiomas.Texto("Builds.RanurasAccesorio",
					EquipoJugador.SlotsAccesorioDisponibles(jugador));
			_subtituloTexto = Idiomas.Texto("Builds.Tienes", tiene, resueltos, ranuras);
			_textoResumen = Idiomas.Texto("Builds.Leyenda");
		}

		/// <summary>Boton "Auto-equipar". Solo mueve objetos que el jugador ya tiene.</summary>
		public void EjecutarAutoEquipar()
		{
			ClaseBuild clase = ClaseActual;
			EtapaBuild etapa = EtapaActual;
			FuenteBuilds fuente = FuenteActual;
			if (clase == null || Main.LocalPlayer == null) {
				return;
			}

			ResultadoAutoEquipar resultado = AutoEquipar.Ejecutar(Main.LocalPlayer, clase);
			AutoEquipar.Registrar(Main.LocalPlayer, clase, resultado,
				$"{fuente?.Etiqueta} / {etapa?.Etiqueta}");

			RefrescarPosesion();
			_textoResumen = Idiomas.Texto("Builds.ResultadoAutoEquipar", resultado.Resumen);
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
