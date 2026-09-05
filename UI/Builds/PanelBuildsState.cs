using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Builds;

namespace TerrakeepMod.UI.Builds
{
	/// <summary>
	/// Panel "Builds" (WS4): equipo recomendado por etapa y clase, con marca de "ya lo tienes"
	/// contra el inventario REAL del jugador y un boton de auto-equipar que <b>solo mueve</b>
	/// objetos que ya posee.
	/// </summary>
	/// <remarks>
	/// Sigue el patron ya probado en WS0: se abre con <c>IngameFancyUI.OpenUIState</c> sobre una
	/// instancia NUEVA cada vez (ver <see cref="Common.Builds.PanelBuildsSystem"/>), y todos los
	/// slots se dibujan con el <c>ItemSlot</c> nativo de vanilla.
	/// </remarks>
	public class PanelBuildsState : UIState
	{
		private const float AnchoMarco = 980f;
		private const float AltoMarco = 600f;
		private const int FotogramasEntreRefrescos = 15;

		private static readonly Color ColorTiene = new Color(140, 235, 160);
		private static readonly Color ColorNoTiene = new Color(170, 170, 180);
		private static readonly Color ColorAusente = new Color(210, 130, 130);
		private static readonly Color PildoraActiva = new Color(70, 120, 200);
		private static readonly Color PildoraInactiva = new Color(40, 50, 90);

		private UIPanel _marco;
		private UIElement _filaFuentes;
		private UIElement _filaEtapas;
		private UIElement _filaClases;
		private UIElement _cuerpo;
		private UIText _resumen;
		private UIText _subtitulo;

		private int _indiceFuente;
		private int _indiceEtapa;
		private string _claveClase = "melee";

		private readonly List<SlotCatalogoBuild> _slots = new List<SlotCatalogoBuild>();
		private readonly List<UIText> _etiquetasSlot = new List<UIText>();
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

		public override void OnInitialize()
		{
			_marco = new UIPanel();
			_marco.Width.Set(AnchoMarco, 0f);
			_marco.Height.Set(AltoMarco, 0f);
			_marco.HAlign = 0.5f;
			_marco.VAlign = 0.5f;
			_marco.BackgroundColor = new Color(33, 43, 79) * 0.94f;
			Append(_marco);

			UIText titulo = new UIText("Terrakeep - Builds", 1f, true);
			titulo.HAlign = 0.5f;
			titulo.Top.Set(8f, 0f);
			_marco.Append(titulo);

			_subtitulo = new UIText("", 0.75f);
			_subtitulo.HAlign = 0.5f;
			_subtitulo.Top.Set(42f, 0f);
			_marco.Append(_subtitulo);

			_filaFuentes = NuevaFila(70f);
			_filaEtapas = NuevaFila(108f);
			_filaClases = NuevaFila(146f);

			_cuerpo = new UIElement();
			_cuerpo.Width.Set(0f, 1f);
			_cuerpo.Height.Set(330f, 0f);
			_cuerpo.Top.Set(186f, 0f);
			_marco.Append(_cuerpo);

			_resumen = new UIText("", 0.8f);
			_resumen.HAlign = 0.5f;
			_resumen.Top.Set(524f, 0f);
			_marco.Append(_resumen);

			UITextPanel<string> autoEquipar = new UITextPanel<string>("Auto-equipar", 0.9f, false);
			autoEquipar.Width.Set(210f, 0f);
			autoEquipar.Height.Set(40f, 0f);
			autoEquipar.Left.Set(-230f, 0.5f);
			autoEquipar.Top.Set(548f, 0f);
			autoEquipar.BackgroundColor = new Color(50, 110, 70);
			autoEquipar.OnLeftClick += (evento, elemento) => EjecutarAutoEquipar();
			_marco.Append(autoEquipar);

			UITextPanel<string> cerrar = new UITextPanel<string>("Cerrar", 0.9f, false);
			cerrar.Width.Set(210f, 0f);
			cerrar.Height.Set(40f, 0f);
			cerrar.Left.Set(20f, 0.5f);
			cerrar.Top.Set(548f, 0f);
			cerrar.OnLeftClick += (evento, elemento) => PanelBuildsSystem.CerrarPanel("boton Cerrar");
			_marco.Append(cerrar);

			Reconstruir();
		}

		private UIElement NuevaFila(float arriba)
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(34f, 0f);
			fila.Top.Set(arriba, 0f);
			_marco.Append(fila);
			return fila;
		}

		/// <summary>Rehace las pildoras y las tres columnas con el filtro seleccionado ahora.</summary>
		private void Reconstruir()
		{
			_slots.Clear();
			_etiquetasSlot.Clear();
			_filaFuentes.RemoveAllChildren();
			_filaEtapas.RemoveAllChildren();
			_filaClases.RemoveAllChildren();
			_cuerpo.RemoveAllChildren();

			FuenteBuilds fuente = FuenteActual;
			if (fuente == null) {
				_subtitulo.SetText("No hay ningun catalogo de builds cargado.");
				Recalculate();
				return;
			}

			_subtitulo.SetText("Equipo recomendado. Verde = ya lo tienes; gris = no lo tienes (auto-equipar no lo crea).");

			// Fila 1: fuente de datos. Solo se enseña si hay mas de una (Calamity sin instalar
			// deja una sola y la fila sobra).
			IReadOnlyList<FuenteBuilds> fuentes = CatalogoBuilds.Fuentes;
			if (fuentes.Count > 1) {
				List<string> etiquetas = new List<string>();
				foreach (FuenteBuilds f in fuentes) {
					etiquetas.Add(f.Etiqueta);
				}
				PintarPildoras(_filaFuentes, etiquetas, _indiceFuente, 170f, indice => {
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
			PintarPildoras(_filaEtapas, etapas, _indiceEtapa, 300f, indice => {
				_indiceEtapa = indice;
				Reconstruir();
			});

			// Fila 3: clase.
			EtapaBuild etapa = EtapaActual;
			if (etapa == null) {
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
			PintarPildoras(_filaClases, clases, indiceClase, 165f, indice => {
				_claveClase = etapa.Clases[indice].Clave;
				Reconstruir();
			});

			ClaseBuild clase = ClaseActual;
			if (clase == null) {
				Recalculate();
				return;
			}

			PintarColumna(0, "Armadura", clase.Armadura);
			PintarColumna(1, "Armas", clase.Armas);
			PintarColumna(2, "Accesorios", clase.Accesorios);

			Recalculate();
			RefrescarPosesion();
		}

		private void PintarPildoras(UIElement fila, List<string> etiquetas, int seleccionada, float ancho, System.Action<int> alPulsar)
		{
			if (etiquetas.Count == 0) {
				return;
			}

			const float separacion = 8f;
			float total = etiquetas.Count * ancho + (etiquetas.Count - 1) * separacion;
			float inicio = -total / 2f;

			for (int i = 0; i < etiquetas.Count; i++) {
				int indice = i; // copia local: sin esto todas las lambdas compartirian la variable
				UITextPanel<string> pildora = new UITextPanel<string>(Acortar(etiquetas[i], 34), 0.72f, false);
				pildora.Width.Set(ancho, 0f);
				pildora.Height.Set(32f, 0f);
				pildora.Left.Set(inicio + i * (ancho + separacion), 0.5f);
				pildora.BackgroundColor = i == seleccionada ? PildoraActiva : PildoraInactiva;
				pildora.OnLeftClick += (evento, elemento) => alPulsar(indice);
				fila.Append(pildora);
			}
		}

		private void PintarColumna(int columna, string titulo, List<ObjetoBuild> objetos)
		{
			const float anchoColumna = 300f;
			const float separacion = 14f;
			float izquierda = -(3f * anchoColumna + 2f * separacion) / 2f + columna * (anchoColumna + separacion);

			UIElement contenedor = new UIElement();
			contenedor.Width.Set(anchoColumna, 0f);
			contenedor.Height.Set(0f, 1f);
			contenedor.Left.Set(izquierda, 0.5f);
			_cuerpo.Append(contenedor);

			UIText cabecera = new UIText(titulo, 0.85f, true);
			cabecera.HAlign = 0.5f;
			contenedor.Append(cabecera);

			float y = 34f;
			foreach (ObjetoBuild objeto in objetos) {
				SlotCatalogoBuild slot = new SlotCatalogoBuild(objeto);
				slot.Top.Set(y, 0f);
				slot.Left.Set(0f, 0f);
				contenedor.Append(slot);
				_slots.Add(slot);

				UIText etiqueta = new UIText(Acortar(objeto.Nombre, 26), 0.72f);
				etiqueta.Left.Set(54f, 0f);
				etiqueta.Top.Set(y + 6f, 0f);
				contenedor.Append(etiqueta);
				_etiquetasSlot.Add(etiqueta);

				string extra = objeto.Resuelto
					? (string.IsNullOrEmpty(objeto.PrefijoRecomendado) ? "" : "prefijo sugerido: " + objeto.PrefijoRecomendado)
					: "no existe en esta partida";
				if (!string.IsNullOrEmpty(extra)) {
					UIText pie = new UIText(Acortar(extra, 34), 0.62f);
					pie.Left.Set(54f, 0f);
					pie.Top.Set(y + 26f, 0f);
					pie.TextColor = objeto.Resuelto ? ColorNoTiene : ColorAusente;
					contenedor.Append(pie);
				}

				y += 54f;
			}

			if (objetos.Count == 0) {
				UIText vacio = new UIText("(nada)", 0.7f);
				vacio.Top.Set(34f, 0f);
				vacio.TextColor = ColorNoTiene;
				contenedor.Append(vacio);
			}
		}

		// Se corta con "..." de tres puntos normales y no con el caracter "…": la fuente del juego
		// solo tiene el juego de caracteres con el que se genero, y un caracter que no esta hace
		// reventar a DynamicSpriteFont al medir el texto.
		private static string Acortar(string texto, int maximo)
		{
			if (string.IsNullOrEmpty(texto) || texto.Length <= maximo) {
				return texto ?? "";
			}
			return texto.Substring(0, System.Math.Max(1, maximo - 3)) + "...";
		}

		/// <summary>Selecciona una clase por su clave (melee/ranged/mage/summoner/rogue) y rehace
		/// el panel. La usa el arnes de pruebas para fijar la clase sin simular clics.</summary>
		public void SeleccionarClase(string clave)
		{
			if (string.IsNullOrEmpty(clave)) {
				return;
			}
			_claveClase = clave;
			Reconstruir();
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

			CalculatedStyle marco = _marco.GetDimensions();
			if (marco.Width <= 0f) {
				return;
			}

			CalculatedStyle primero = _slots[0].GetDimensions();
			if (primero.Width <= 0f) {
				return;
			}

			_coordenadasRegistradas = true;

			System.Text.StringBuilder texto = new System.Text.StringBuilder();
			texto.Append($"{Terrakeep.LogTag} Panel Builds dibujado. Marco: x={(int)marco.X} y={(int)marco.Y} " +
				$"w={(int)marco.Width} h={(int)marco.Height}. Resolucion actual: {Main.screenWidth}x{Main.screenHeight}. " +
				$"{_slots.Count} slots de catalogo. Primeros: ");
			for (int i = 0; i < _slots.Count && i < 4; i++) {
				CalculatedStyle d = _slots[i].GetDimensions();
				texto.Append($"[\"{_slots[i].Objeto.Nombre}\" x={(int)d.X} y={(int)d.Y} w={(int)d.Width} h={(int)d.Height} " +
					$"loTiene={_slots[i].LoTiene}] ");
			}
			Terrakeep.Instance.Logger.Info(texto.ToString());
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
					_etiquetasSlot[i].TextColor = !slot.Objeto.Resuelto ? ColorAusente
						: (slot.LoTiene ? ColorTiene : ColorNoTiene);
				}
			}

			_resumen?.SetText($"Tienes {tiene} de {resueltos} objetos de esta build " +
				$"(slots de accesorio disponibles: {EquipoJugador.SlotsAccesorioDisponibles(jugador)}).");
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
			_resumen?.SetText($"Auto-equipar: {resultado.Resumen}");
		}
	}
}
