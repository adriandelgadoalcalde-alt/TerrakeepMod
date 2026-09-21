using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.UI.Personaje.Widgets
{
	/// <summary>
	/// TM5 del catálogo de rediseño visual ("Builds: filtros en dos filas, no en cuatro"): un
	/// desplegable GENÉRICO - un botón con la opción actual que, al pulsarlo, despliega la lista
	/// completa de opciones y aplica la elegida. Pensado para reemplazar una fila de píldoras
	/// cuando las opciones son pocas y de una sola elección a la vez (nunca "cualquiera de estas"),
	/// como la etapa de progresión de Builds.
	/// </summary>
	/// <remarks>
	/// <b>Por qué no se llama <c>SelectorTk</c>, como lo nombra el propio catálogo.</b> Ese nombre
	/// YA estaba cogido: <c>SelectorTk</c> es un widget real y distinto que ya existe desde WS1
	/// (fila "etiqueta [-] valor [+]" de paso, usado en <c>PestanaApariencia</c>/
	/// <c>CabeceraPersonaje</c>) - se descubrió al compilar (la primera versión de este archivo
	/// sobrescribió sin querer el de verdad, con errores reales de compilación en los sitios que ya
	/// lo usaban como prueba). Restaurado ese original desde git, este widget nuevo se queda con un
	/// nombre real distinto.
	/// <para />
	/// El desplegable cuelga de <see cref="CapaSuperposicionTk"/>, el mismo mecanismo ya probado por
	/// <see cref="EditorPrefijoTk"/> (nunca colgado del propio botón que lo abre - ver el XMLdoc de
	/// esa clase para los tres bugs reales que ese patrón ya resolvió: desplegable dibujándose sobre
	/// el mundo, botón "Cerrar" tapándolo, rueda del ratón sin responder).
	/// </remarks>
	public class DesplegableTk : UIElement
	{
		/// <summary>Ancho MINIMO del popup - de sobra para opciones cortas ("Cuerpo a cuerpo", "1",
		/// "2"...). Ya NO es un tope fijo: ver <see cref="AnchoPopupReal"/>.</summary>
		private const float AnchoPopupMinimo = 260f;
		private const float AltoFilaPopup = 26f;
		private const float AltoPopupMaximo = 220f;

		/// <summary>Escala de las filas del popup en reposo - la misma que pasa <see cref="ConstruirPopup"/>
		/// a cada <see cref="BotonTk"/> de opcion. Hace falta como constante (no solo el valor
		/// literal en el sitio de la creacion) porque <see cref="AnchoPopupReal"/> mide el texto ANTES
		/// de construir los botones, con esta misma escala.</summary>
		private const float EscalaFilaPopup = 0.75f;

		/// <summary>Por debajo de esta escala el texto de una fila del popup se vuelve ilegible - el
		/// mismo suelo que ya usa <c>PanelTerrakeepState.AjustarEscalaDeLasPestanas</c>. Solo se
		/// llega aqui si NI SIQUIERA el ancho maximo disponible de la superposicion basta para la
		/// opcion mas larga a escala normal (pantalla muy estrecha) - un caso limite real, no el
		/// camino habitual.</summary>
		private const float EscalaFilaMinima = 0.55f;

		private readonly Func<IReadOnlyList<string>> _opciones;
		private readonly Func<int> _seleccionActual;
		private readonly Action<int> _alElegir;
		private readonly BotonTk _botonToggle;

		/// <summary>Escala real de las filas del popup ABIERTO ahora mismo - normalmente
		/// <see cref="EscalaFilaPopup"/>, solo baja hasta <see cref="EscalaFilaMinima"/> si ni el
		/// ancho maximo disponible alcanza para la opcion mas larga. La lee la autoprueba para
		/// demostrar con un numero que las filas caben de verdad, no solo "porque no salio recortado
		/// en esta captura".</summary>
		public float EscalaFilaActual { get; private set; } = EscalaFilaPopup;

		private UIPanel _popup;
		private UIList _lista;
		private UIScrollbar _scroll;
		private CapaSuperposicionTk _capa;
		private bool _abierto;

		public DesplegableTk(Func<IReadOnlyList<string>> opciones, Func<int> seleccionActual, Action<int> alElegir,
			float escalaTexto = 0.8f)
		{
			_opciones = opciones;
			_seleccionActual = seleccionActual;
			_alElegir = alElegir;

			Width.Set(0f, 1f);
			Height.Set(30f, 0f);

			_botonToggle = new BotonTk("", escalaTexto);
			_botonToggle.Width.Set(0f, 1f);
			_botonToggle.Height.Set(0f, 1f);
			_botonToggle.AlPulsar += Alternar;
			Append(_botonToggle);
		}

		/// <summary>true si el desplegable esta abierto ahora mismo. Lo lee la autoprueba.</summary>
		public bool Abierto => _abierto;

		/// <summary>El boton que muestra la opcion actual, para que la autoprueba pueda pulsarlo de
		/// verdad y medir su geometria real.</summary>
		public BotonTk BotonToggle => _botonToggle;

		/// <summary>Las filas reales del desplegable ABIERTO ahora mismo, en orden - vacia si esta
		/// cerrado. Misma utilidad que <c>EditorPrefijoTk.BotonesPopupParaAutoprueba</c>.</summary>
		public List<BotonTk> FilasParaAutoprueba()
		{
			List<BotonTk> filas = new List<BotonTk>();
			if (_popup == null) {
				return filas;
			}
			_popup.ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (boton != null) {
					filas.Add(boton);
				}
			});
			return filas;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			IReadOnlyList<string> opciones = _opciones != null ? _opciones() : null;
			int actual = _seleccionActual != null ? _seleccionActual() : -1;
			string texto = opciones != null && actual >= 0 && actual < opciones.Count
				? opciones[actual]
				: Idiomas.Texto("Widgets.Desplegable.SinOpciones");
			_botonToggle.FijarTexto(texto);
			_botonToggle.Habilitado = opciones != null && opciones.Count > 0;

			if (_abierto && (opciones == null || opciones.Count == 0)) {
				Cerrar();
			}
		}

		private void Alternar()
		{
			if (_abierto) {
				Cerrar();
			}
			else {
				Abrir();
			}
		}

		private void Abrir()
		{
			IReadOnlyList<string> opciones = _opciones != null ? _opciones() : null;
			if (opciones == null || opciones.Count == 0) {
				return;
			}

			ConstruirPopup(opciones);
			_abierto = true;
		}

		private void Cerrar()
		{
			UIPanel popup = _popup;
			CapaSuperposicionTk capa = _capa;
			_popup = null;
			_lista = null;
			_scroll = null;
			_capa = null;
			_abierto = false;

			if (popup == null) {
				return;
			}
			if (capa != null) {
				capa.Quitar(popup);
			}
			else {
				RemoveChild(popup);
			}
		}

		private void ConstruirPopup(IReadOnlyList<string> opciones)
		{
			// Mismo patron real, con el mismo motivo, que EditorPrefijoTk.ConstruirPopup: la capa de
			// superposicion del panel, nunca colgado del propio boton.
			_capa = CapaSuperposicionTk.Buscar(this);

			CalculatedStyle area = _capa != null
				? _capa.GetInnerDimensions()
				: new CalculatedStyle(0f, 0f, Main.screenWidth, Main.screenHeight);

			float altoReal = Math.Min(AltoPopupMaximo, Math.Max(AltoFilaPopup * 2f, opciones.Count * (AltoFilaPopup + 3f) + 12f));
			altoReal = Math.Min(altoReal, area.Height);

			// Bug real reportado por el usuario (con captura, más de una vez - ver bitacora.md):
			// un ancho FIJO de 260px cortaba etiquetas reales largas como "Fin del juego (post
			// Lunatic Cultist / Moon Lord)" dentro de la lista - el boton cerrado (ancho completo
			// del panel) no tenia el problema, pero el popup desplegado si. Igual que
			// RecalcularCabecera/AjustarEscalaDeLasPestanas en otros sitios de este mismo panel: la
			// caja se adapta al contenido real, nunca se recorta el texto. Se mide la opcion MAS
			// LARGA con la fuente real (no solo la seleccionada) y el popup crece hasta acogerla,
			// topado por el hueco real disponible de la superposicion.
			const float MargenFila = 16f; // mismo margen interno que BotonTk.DibujarTexto reparte a cada lado
			const float AnchoScroll = 20f; // UIScrollbar (16px) + su hueco real en UIList.Width.Set(-20f, 1f)
			const float PaddingPopup = 12f; // SetPadding(6f) a cada lado

			float anchoOpcionMax = 0f;
			for (int i = 0; i < opciones.Count; i++) {
				float ancho = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(opciones[i]).X * EscalaFilaPopup;
				if (ancho > anchoOpcionMax) {
					anchoOpcionMax = ancho;
				}
			}

			float anchoNecesario = anchoOpcionMax + MargenFila * 2f + AnchoScroll + PaddingPopup;
			float anchoDisponible = Math.Max(AnchoPopupMinimo, area.Width);
			float anchoReal = Math.Max(AnchoPopupMinimo, Math.Min(anchoNecesario, anchoDisponible));

			// Caso limite real: ni el hueco MAXIMO disponible de la superposicion alcanza para la
			// opcion mas larga a escala normal (pantalla muy estrecha). Ultimo recurso, igual que
			// AjustarEscalaDeLasPestanas: encoger el texto de las filas, nunca recortarlo.
			EscalaFilaActual = EscalaFilaPopup;
			if (anchoNecesario > anchoDisponible && anchoOpcionMax > 0f) {
				float anchoTextoDisponible = anchoDisponible - MargenFila * 2f - AnchoScroll - PaddingPopup;
				float factor = anchoTextoDisponible / anchoOpcionMax;
				EscalaFilaActual = Math.Max(EscalaFilaMinima, EscalaFilaPopup * factor);
			}

			_popup = new UIPanel();
			_popup.Width.Set(anchoReal, 0f);
			_popup.Height.Set(altoReal, 0f);
			_popup.MaxWidth.Set(anchoReal, 0f);
			_popup.MaxHeight.Set(altoReal, 0f);
			_popup.BackgroundColor = EstiloTk.FondoCaja;
			_popup.BorderColor = EstiloTk.BordeSobre * 0.55f;
			_popup.SetPadding(6f);

			CalculatedStyle boton = _botonToggle.GetDimensions();
			const float Separacion = 4f;

			float x = boton.X;
			if (x + anchoReal > area.X + area.Width) {
				x = area.X + area.Width - anchoReal;
			}
			if (x < area.X) {
				x = area.X;
			}

			float y = boton.Y + boton.Height + Separacion;
			if (y + altoReal > area.Y + area.Height) {
				y = boton.Y - altoReal - Separacion;
			}
			if (y < area.Y) {
				y = area.Y;
			}

			_popup.Left.Set(x - area.X, 0f);
			_popup.Top.Set(y - area.Y, 0f);

			_lista = new UIList();
			_lista.Width.Set(-20f, 1f);
			_lista.Height.Set(0f, 1f);
			_lista.ListPadding = 3f;
			_lista.ManualSortMethod = elementos => { };
			_popup.Append(_lista);

			_scroll = new UIScrollbar();
			_scroll.Width.Set(16f, 0f);
			_scroll.Height.Set(0f, 1f);
			_scroll.HAlign = 1f;
			_popup.Append(_scroll);
			_lista.SetScrollbar(_scroll);

			_popup.OnScrollWheel += (evento, elemento) => {
				if (_scroll == null || _lista == null || VieneDeLaLista(evento.Target)) {
					return;
				}
				_scroll.ViewPosition -= evento.ScrollWheelValue;
			};

			int actual = _seleccionActual != null ? _seleccionActual() : -1;
			for (int i = 0; i < opciones.Count; i++) {
				int indice = i;
				BotonTk fila = new BotonTk(opciones[i], EscalaFilaActual);
				fila.Width.Set(0f, 1f);
				fila.Height.Set(AltoFilaPopup, 0f);
				fila.Activo = i == actual;
				fila.AlPulsar += () => {
					_alElegir?.Invoke(indice);
					Cerrar();
				};
				_lista.Add(fila);
			}

			if (_capa != null) {
				_capa.Mostrar(_popup, Cerrar);
			}
			else {
				Append(_popup);
			}
			_popup.Recalculate();
		}

		private bool VieneDeLaLista(UIElement objetivo)
		{
			for (UIElement actual = objetivo; actual != null; actual = actual.Parent) {
				if (ReferenceEquals(actual, _lista)) {
					return true;
				}
			}
			return false;
		}
	}
}
