using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Libreria.Widgets
{
	/// <summary>
	/// El mini-panel de edicion de objeto: papelera + un recuadro donde ARRASTRAR un objeto real
	/// para "seleccionarlo" + editor de cantidad + editor de prefijo sobre ese mismo objeto
	/// seleccionado. Encargo explicito del usuario tras probar el mod: "el editor de stack no hay
	/// forma de usarlo comodamente ya que se selecciona pasando por encima el raton...
	/// deberiamos poder clicar/arrastrar y que quede seleccionado" + "que puedas cambiar prefijo
	/// a un arma colocandola alli... un recuadro donde puedas arrastrar el arma o el objeto y eso
	/// sera lo que este seleccionado para editar".
	/// </summary>
	/// <remarks>
	/// Nacio en <see cref="ContenidoLibreria"/> (hueco vacio de abajo a la derecha, junto a la
	/// rejilla de destino, que se dejo con un ancho FIJO - <c>ColumnasDestino</c> columnas, no en
	/// porcentaje - precisamente para dejar sitio real a este panel al lado, sea cual sea la
	/// resolucion de la ventana) y se REUTILIZA TAL CUAL en las pestañas de Personaje que
	/// enseñan objetos reales (Inventario/Almacenes/Equipo - encargo explicito del usuario:
	/// "el mismo menu de edicion que en Libreria... a ponerlo todo junto abajo a la derecha"),
	/// en el hueco libre de cada una. El nombre se ha quedado con "Libreria" por no mover el
	/// archivo sin necesidad (sigue siendo generico: no sabe nada de en que pestaña vive).
	/// <para />
	/// <b>La papelera es la MISMA clase que ya usa Personaje</b> (<see cref="SlotPapeleraTk"/>,
	/// reutilizada tal cual, sin tocarla: es generica, no sabe nada de Personaje ni de Libreria).
	/// <b>El editor de cantidad tambien es el MISMO</b> (<see cref="EditorCantidadTk"/>), en su
	/// segundo modo (explicito + compacto) añadido para esto, enganchado al objeto de
	/// <see cref="SlotSeleccionTk"/> en vez de al hover.
	/// </remarks>
	public class PanelHerramientasLibreriaTk : UIElement
	{
		/// <summary>Ancho fijo real del panel, en pixeles. Publico para que quien lo coloque desde
		/// fuera (Libreria, Personaje) pueda calcular el hueco que necesita dejarle sin tener que
		/// repetir el numero a mano.</summary>
		public const float Ancho = 176f;

		/// <summary>Alto fijo real del panel, en pixeles. Ver <see cref="Ancho"/>.</summary>
		public const float Alto = 156f;

		private readonly EtiquetaTk _titulo;
		private readonly SlotPapeleraTk _papelera;
		private readonly SlotSeleccionTk _seleccion;
		private readonly EditorCantidadTk _editorCantidad;
		private readonly EditorPrefijoTk _editorPrefijo;

		/// <summary>El slot de seleccion por arrastre, expuesto para la autoprueba.</summary>
		public SlotSeleccionTk Seleccion => _seleccion;

		/// <summary>El editor de cantidad compacto, expuesto para la autoprueba (mismos botones
		/// reales que el de Personaje, <c>BotonTk.LeftClick</c>).</summary>
		public EditorCantidadTk EditorCantidad => _editorCantidad;

		/// <summary>La papelera, expuesta para la autoprueba.</summary>
		public SlotPapeleraTk Papelera => _papelera;

		/// <summary>El editor de prefijo, expuesto para la autoprueba.</summary>
		public EditorPrefijoTk EditorPrefijo => _editorPrefijo;

		public PanelHerramientasLibreriaTk()
		{
			// Alto FIJO, no en porcentaje: el contenido interno se coloca con desplazamientos
			// absolutos desde este mismo origen (papelera, recuadro, editor de cantidad, editor de
			// prefijo), y ninguno de ellos depende de un porcentaje del padre - fijarlo evita tener
			// que hacer encajar este numero con el alto real del padre en el punto donde se cuelgue
			// (ver ConstruirZonaDestino en ContenidoLibreria).
			Width.Set(Ancho, 0f);
			Height.Set(Alto, 0f);

			_titulo = new EtiquetaTk(() => Idiomas.Texto("Libreria.Herramientas.Titulo"), 0.75f, Ancho, 18f);
			_titulo.ColorTexto = EstiloTk.TextoSuave;
			Append(_titulo);

			float filaUno = 22f;

			EtiquetaTk etiquetaPapelera = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Herramientas.Papelera"), 0.62f, 70f, 14f);
			etiquetaPapelera.ColorTexto = EstiloTk.TextoSuave;
			etiquetaPapelera.Top.Set(filaUno, 0f);
			Append(etiquetaPapelera);

			_papelera = new SlotPapeleraTk(0.55f);
			_papelera.Left.Set(0f, 0f);
			_papelera.Top.Set(filaUno + 14f, 0f);
			Append(_papelera);

			// Left=64, no 46: con 46 la etiqueta "Seleccionar" quedaba pegada a "Papelera" (se veian
			// casi tocandose / "Papelera Selecciona" en una captura real) - 64 le deja hueco real de
			// sobra a "Papelera" (8 letras a escala 0.62) antes de que empiece la siguiente.
			EtiquetaTk etiquetaSeleccion = new EtiquetaTk(
				() => Idiomas.Texto("Libreria.EditorPrefijo.RecuadroTitulo"), 0.62f, 100f, 14f);
			etiquetaSeleccion.ColorTexto = EstiloTk.TextoSuave;
			etiquetaSeleccion.Left.Set(64f, 0f);
			etiquetaSeleccion.Top.Set(filaUno, 0f);
			Append(etiquetaSeleccion);

			_seleccion = new SlotSeleccionTk(0.55f);
			_seleccion.Left.Set(64f, 0f);
			_seleccion.Top.Set(filaUno + 14f, 0f);
			Append(_seleccion);

			// El "34" de aqui NO es solo el alto del recuadro (52*0.55=~29px): cuando el recuadro
			// esta vacio, SlotSeleccionTk dibuja debajo la pista "arrastra aqui"
			// (rect.Bottom+2, ~12px de texto a escala 0.6) - con solo 34px de hueco esa pista se
			// solapaba con la fila de cantidad de justo debajo, bug real visto en una captura
			// (el "arrastra aqui" y el "cantidad" del campo de texto se pisaban). Se sube a 46
			// (29 del recuadro + 2 + ~12 del texto + margen) para dejarle sitio real.
			float filaDos = filaUno + 14f + 46f + 8f;

			_editorCantidad = new EditorCantidadTk(() => _seleccion.ObjetoActual, Ancho);
			_editorCantidad.Top.Set(filaDos, 0f);
			Append(_editorCantidad);

			float filaTres = filaDos + 26f + 6f;

			_editorPrefijo = new EditorPrefijoTk(() => _seleccion.ObjetoActual, Ancho);
			_editorPrefijo.Top.Set(filaTres, 0f);
			Append(_editorPrefijo);
		}
	}
}
