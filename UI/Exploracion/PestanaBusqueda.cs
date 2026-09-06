using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// Pestaña "Búsqueda": elige que buscar y enseña donde esta, en el mundo REAL de la partida.
	/// </summary>
	/// <remarks>
	/// La busqueda no la hace esta pestaña: la lleva <see cref="BuscadorMundo"/> desde
	/// <see cref="PanelExploracionSystem"/>, troceada por fotogramas, para que el juego no se
	/// quede clavado mientras se recorren millones de tiles. Aqui solo se elige el objetivo, se
	/// enseña el progreso y se listan los resultados.
	/// </remarks>
	public class PestanaBusqueda : UIElement
	{
		private const float AnchoIzquierda = 330f;

		private string _categoria;
		private ObjetivoBusqueda _seleccionado;
		private bool _soloExplorado = true;

		private UIList _listaObjetivos;
		private UIList _listaResultados;
		private readonly List<BotonTk> _pildorasCategoria = new List<BotonTk>();

		/// <summary>Las filas de objetivo que hay pintadas ahora, con el objetivo que representa
		/// cada una. Se guarda aparte para no tener que reconocerlas por su texto.</summary>
		private readonly List<KeyValuePair<ObjetivoBusqueda, BotonTk>> _filasObjetivo =
			new List<KeyValuePair<ObjetivoBusqueda, BotonTk>>();

		/// <summary>Objetivo elegido ahora mismo.</summary>
		public ObjetivoBusqueda Seleccionado => _seleccionado;

		/// <summary>Categoria elegida ahora mismo.</summary>
		public string Categoria => _categoria;

		public PestanaBusqueda()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			List<string> categorias = CatalogoObjetivos.Categorias();
			_categoria = categorias.Count > 0 ? categorias[0] : "";

			ConstruirIzquierda(categorias);
			ConstruirDerecha();

			RellenarObjetivos();
			RellenarResultados();
		}

		private void ConstruirIzquierda(List<string> categorias)
		{
			UIElement izquierda = new UIElement();
			izquierda.Width.Set(AnchoIzquierda, 0f);
			izquierda.Height.Set(0f, 1f);
			Append(izquierda);

			float y = 0f;
			float ancho = AnchoIzquierda / 2f - 4f;

			for (int i = 0; i < categorias.Count; i++) {
				string categoria = categorias[i];
				BotonTk pildora = new BotonTk(Idiomas.Texto("Exploracion.Categoria." + categoria), 0.8f);
				pildora.Clave = categoria;
				pildora.Left.Set((i % 2) * (ancho + 8f), 0f);
				pildora.Top.Set(y + (i / 2) * 32f, 0f);
				pildora.Width.Set(ancho, 0f);
				pildora.Height.Set(28f, 0f);
				pildora.Activo = categoria == _categoria;
				pildora.AlPulsar += () => SeleccionarCategoria(categoria);
				_pildorasCategoria.Add(pildora);
				izquierda.Append(pildora);
			}

			y += ((categorias.Count + 1) / 2) * 32f + 10f;

			UIPanel caja = new UIPanel();
			caja.Width.Set(0f, 1f);
			caja.Top.Set(y, 0f);
			caja.Height.Set(-(y + 78f), 1f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(6f);
			izquierda.Append(caja);

			_listaObjetivos = new UIList();
			_listaObjetivos.Width.Set(-24f, 1f);
			_listaObjetivos.Height.Set(0f, 1f);
			_listaObjetivos.ListPadding = 4f;
			caja.Append(_listaObjetivos);

			UIScrollbar barra = new UIScrollbar();
			barra.Width.Set(20f, 0f);
			barra.Height.Set(0f, 1f);
			barra.HAlign = 1f;
			barra.SetView(100f, 1000f);
			caja.Append(barra);
			_listaObjetivos.SetScrollbar(barra);

			AlternadorTk soloExplorado = new AlternadorTk(
				() => Idiomas.Texto("Exploracion.SoloExplorado"),
				() => _soloExplorado,
				valor => _soloExplorado = valor);
			soloExplorado.Ayuda = () => Idiomas.Texto("Exploracion.SoloExploradoAyuda");
			soloExplorado.Width.Set(0f, 1f);
			soloExplorado.Height.Set(30f, 0f);
			soloExplorado.Top.Set(-72f, 1f);
			izquierda.Append(soloExplorado);

			BotonTk buscar = new BotonTk(Idiomas.Texto("Exploracion.Buscar"), 0.9f);
			buscar.Ayuda = () => Idiomas.Texto("Exploracion.BuscarAyuda");
			buscar.Width.Set(0f, 1f);
			buscar.Height.Set(36f, 0f);
			buscar.Top.Set(-36f, 1f);
			buscar.AlPulsar += Buscar;
			izquierda.Append(buscar);
		}

		private void ConstruirDerecha()
		{
			UIElement derecha = new UIElement();
			derecha.Width.Set(-(AnchoIzquierda + 12f), 1f);
			derecha.Height.Set(0f, 1f);
			derecha.HAlign = 1f;
			Append(derecha);

			EtiquetaTk estado = new EtiquetaTk(TextoEstado, 0.85f, 640f, 24f);
			estado.Top.Set(0f, 0f);
			derecha.Append(estado);

			EtiquetaTk detalle = new EtiquetaTk(TextoDetalle, 0.75f, 640f, 22f);
			detalle.ColorTexto = EstiloTk.TextoSuave;
			detalle.Top.Set(24f, 0f);
			derecha.Append(detalle);

			BarraProgresoExploracion progreso = new BarraProgresoExploracion(
				() => PanelExploracionSystem.Buscador.EnMarcha ? PanelExploracionSystem.Buscador.Progreso : 0f);
			progreso.Width.Set(0f, 1f);
			progreso.Height.Set(10f, 0f);
			progreso.Top.Set(48f, 0f);
			derecha.Append(progreso);

			UIPanel caja = new UIPanel();
			caja.Width.Set(0f, 1f);
			caja.Top.Set(64f, 0f);
			caja.Height.Set(-64f, 1f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(6f);
			derecha.Append(caja);

			_listaResultados = new UIList();
			_listaResultados.Width.Set(-24f, 1f);
			_listaResultados.Height.Set(0f, 1f);
			_listaResultados.ListPadding = 3f;
			caja.Append(_listaResultados);

			UIScrollbar barra = new UIScrollbar();
			barra.Width.Set(20f, 0f);
			barra.Height.Set(0f, 1f);
			barra.HAlign = 1f;
			barra.SetView(100f, 1000f);
			caja.Append(barra);
			_listaResultados.SetScrollbar(barra);
		}

		private string TextoEstado()
		{
			BuscadorMundo buscador = PanelExploracionSystem.Buscador;
			if (buscador.EnMarcha) {
				return Idiomas.Texto("Exploracion.Buscando",
					buscador.Objetivo != null ? buscador.Objetivo.EtiquetaLegible() : "",
					(int)(buscador.Progreso * 100f));
			}
			if (buscador.Terminada && buscador.Objetivo != null) {
				return Idiomas.Texto("Exploracion.ZonasCon",
					buscador.Resultados.Count, buscador.Objetivo.EtiquetaLegible());
			}
			return Idiomas.Texto("Exploracion.EligeQueBuscar");
		}

		private string TextoDetalle()
		{
			BuscadorMundo buscador = PanelExploracionSystem.Buscador;
			if (buscador.Objetivo == null) {
				return Idiomas.Texto("Exploracion.DetalleInicial", MundoActual.TotalTiles.ToString("N0"));
			}
			if (buscador.EnMarcha) {
				return Idiomas.Texto("Exploracion.DetalleEnMarcha",
					buscador.TilesEncontrados, buscador.TilesMirados.ToString("N0"));
			}
			return Idiomas.Texto("Exploracion.DetalleTerminada",
				buscador.TilesEncontrados.ToString("N0"),
				buscador.TilesMirados.ToString("N0"),
				buscador.MilisegundosGastados.ToString("0.0"),
				Idiomas.Texto(buscador.SoloExplorado
					? "Exploracion.AmbitoExplorado"
					: "Exploracion.AmbitoTodo"));
		}

		private void SeleccionarCategoria(string categoria)
		{
			_categoria = categoria;
			foreach (BotonTk pildora in _pildorasCategoria) {
				pildora.Activo = pildora.Clave == categoria;
			}
			RellenarObjetivos();
		}

		private void RellenarObjetivos()
		{
			_listaObjetivos.Clear();
			_filasObjetivo.Clear();
			List<ObjetivoBusqueda> objetivos = CatalogoObjetivos.DeCategoria(_categoria);

			if (objetivos.Count == 0) {
				_listaObjetivos.Add(FilaTexto(
					() => Idiomas.Texto("Exploracion.CategoriaVacia"), EstiloTk.TextoSuave));
				_seleccionado = null;
				return;
			}

			if (_seleccionado == null || _seleccionado.Categoria != _categoria) {
				_seleccionado = objetivos[0];
			}

			foreach (ObjetivoBusqueda objetivo in objetivos) {
				ObjetivoBusqueda actual = objetivo;
				BotonTk fila = new BotonTk(objetivo.EtiquetaLegible(), 0.8f);
				fila.Width.Set(0f, 1f);
				fila.Height.Set(30f, 0f);
				fila.Activo = objetivo == _seleccionado;
				fila.Ayuda = () => DescribirObjetivo(actual);
				fila.AlPulsar += () => Seleccionar(actual);
				_listaObjetivos.Add(fila);
				_filasObjetivo.Add(new KeyValuePair<ObjetivoBusqueda, BotonTk>(actual, fila));
			}
		}

		private static string DescribirObjetivo(ObjetivoBusqueda objetivo)
		{
			switch (objetivo.Clase) {
				case ClaseDeObjetivo.Cofres:
					return Idiomas.Texto("Exploracion.AyudaCofres");
				case ClaseDeObjetivo.Npcs:
					return Idiomas.Texto("Exploracion.AyudaNpcs");
				case ClaseDeObjetivo.Liquido:
					return Idiomas.Texto("Exploracion.AyudaLiquido");
				case ClaseDeObjetivo.Pared:
					return Idiomas.Texto("Exploracion.AyudaPared", string.Join(", ", objetivo.Tipos));
				default:
					return Idiomas.Texto("Exploracion.AyudaTile", string.Join(", ", objetivo.Tipos));
			}
		}

		private void Seleccionar(ObjetivoBusqueda objetivo)
		{
			_seleccionado = objetivo;
			foreach (KeyValuePair<ObjetivoBusqueda, BotonTk> fila in _filasObjetivo) {
				fila.Value.Activo = fila.Key == objetivo;
			}
		}

		/// <summary>Lanza la busqueda del objetivo elegido. Publico porque lo usa la autoprueba.</summary>
		public void Buscar()
		{
			if (_seleccionado == null) {
				return;
			}
			PanelExploracionSystem.Buscar(_seleccionado, _soloExplorado, "botón \"Buscar en el mundo\"");
		}

		/// <summary>
		/// Elige objetivo por su etiqueta. Devuelve true si lo encontro.
		/// </summary>
		/// <remarks>
		/// La comparacion ignora mayusculas y tildes, y admite que se le pase solo un trozo del
		/// nombre. No es por comodidad: la usa la autoprueba, que recibe el nombre por una variable
		/// de entorno, y el entorno de un proceso hijo lanzado desde PowerShell no viaja en UTF-8
		/// (se vio en la primera verificacion real: "Cobre / Estaño" llegaba como
		/// "Cobre / EstaÃ±o" y no casaba con nada). Comparando sin tildes, "Cobre" basta.
		/// </remarks>
		public bool SeleccionarPorEtiqueta(string etiqueta)
		{
			string buscado = SinTildes(etiqueta);
			if (buscado.Length == 0) {
				return false;
			}

			foreach (ObjetivoBusqueda objetivo in CatalogoObjetivos.Todos) {
				if (!objetivo.Resuelto) {
					continue;
				}
				string propia = SinTildes(objetivo.Etiqueta);
				string legible = SinTildes(objetivo.EtiquetaLegible());
				if (propia == buscado || legible == buscado ||
					propia.StartsWith(buscado) || legible.StartsWith(buscado)) {
					SeleccionarCategoria(objetivo.Categoria);
					Seleccionar(objetivo);
					return true;
				}
			}
			return false;
		}

		/// <summary>Minusculas y sin tildes, para comparar nombres sin depender de la codificacion.</summary>
		private static string SinTildes(string texto)
		{
			if (string.IsNullOrEmpty(texto)) {
				return "";
			}

			string descompuesto = texto.Normalize(System.Text.NormalizationForm.FormD);
			System.Text.StringBuilder limpio = new System.Text.StringBuilder(descompuesto.Length);
			foreach (char letra in descompuesto) {
				if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(letra)
					!= System.Globalization.UnicodeCategory.NonSpacingMark) {
					limpio.Append(char.ToLowerInvariant(letra));
				}
			}
			return limpio.ToString().Normalize(System.Text.NormalizationForm.FormC).Trim();
		}

		/// <summary>Vuelve a pintar la lista de resultados. Se llama sola cuando cambia el numero
		/// de resultados.</summary>
		private void RellenarResultados()
		{
			_listaResultados.Clear();
			List<ResultadoBusqueda> resultados = PanelExploracionSystem.Buscador.Resultados;

			if (resultados.Count == 0) {
				_listaResultados.Add(FilaTexto(
					() => Idiomas.Texto(PanelExploracionSystem.Buscador.Terminada
						? "Exploracion.SinResultados"
						: "Exploracion.AquiSaldran"),
					EstiloTk.TextoSuave));
				return;
			}

			foreach (ResultadoBusqueda resultado in resultados) {
				ResultadoBusqueda actual = resultado;
				BotonTk fila = new BotonTk(
					Idiomas.Texto("Exploracion.FilaResultado",
						(int)resultado.Tile.X, (int)resultado.Tile.Y,
						resultado.Etiqueta, (int)resultado.DistanciaAlJugador),
					0.75f);
				fila.Width.Set(0f, 1f);
				fila.Height.Set(26f, 0f);
				fila.Ayuda = () => Idiomas.Texto("Exploracion.AyudaResultado");
				fila.AlPulsar += () => IrAlResultado(actual);
				_listaResultados.Add(fila);
			}
		}

		private void IrAlResultado(ResultadoBusqueda resultado)
		{
			// Con el panel unico, el area de Exploracion ya no es la UIState: se le pregunta al
			// sistema del panel, que sabe que contenido tiene montado en la pestaña abierta.
			ContenidoExploracion panel = PanelExploracionSystem.Panel;
			if (panel == null) {
				return;
			}

			// Se salta a la pestaña del mapa y se centra alli: es lo que uno espera al pulsar un
			// resultado, y ademas deja el mini-mapa listo por si luego se quiere el mapa grande.
			panel.CambiarPestana(0);
			if (panel.Mapa != null && panel.Mapa.Mapa != null) {
				panel.Mapa.Mapa.CentrarEn(resultado.Tile, 3f);
			}

			RegistroExploracion.Linea(Terrakeep.LogTag + " Resultado pulsado: \"" + resultado.Etiqueta +
				"\" en el tile (" + (int)resultado.Tile.X + ", " + (int)resultado.Tile.Y + "); " +
				"el mini-mapa se centra ahi.");
		}

		private static UIElement FilaTexto(System.Func<string> texto, Color color)
		{
			EtiquetaTk etiqueta = new EtiquetaTk(texto, 0.75f, 400f, 24f);
			etiqueta.ColorTexto = color;
			etiqueta.Width.Set(0f, 1f);
			return etiqueta;
		}

		private int _resultadosPintados = -1;
		private bool _enMarchaPintado;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			// La lista se repinta solo cuando cambia de verdad: reconstruir 150 botones cada
			// fotograma seria tirar tiempo, y ademas rompe el arrastre de la barra de scroll.
			int ahora = PanelExploracionSystem.Buscador.Resultados.Count;
			bool enMarcha = PanelExploracionSystem.Buscador.EnMarcha;
			if (ahora != _resultadosPintados || enMarcha != _enMarchaPintado) {
				_resultadosPintados = ahora;
				_enMarchaPintado = enMarcha;
				RellenarResultados();
			}
		}
	}

	/// <summary>Barra de progreso sencilla, con los colores del panel.</summary>
	public class BarraProgresoExploracion : UIElement
	{
		private readonly System.Func<float> _progreso;

		public BarraProgresoExploracion(System.Func<float> progreso)
		{
			_progreso = progreso;
			IgnoresMouseInteraction = true;
		}

		protected override void DrawSelf(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
		{
			float valor = _progreso();
			if (valor <= 0f) {
				return;
			}

			CalculatedStyle dim = GetDimensions();
			Rectangle fondo = new Rectangle((int)dim.X, (int)dim.Y, (int)dim.Width, (int)dim.Height);
			spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, fondo, new Color(20, 26, 48));

			Rectangle relleno = new Rectangle(fondo.X, fondo.Y, (int)(fondo.Width * valor), fondo.Height);
			spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, relleno, EstiloTk.BotonActivo);
		}
	}
}
