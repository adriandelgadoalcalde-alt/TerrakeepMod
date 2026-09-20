using Microsoft.Xna.Framework;
using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Panel;
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

			// Left=74, no 64: 64 dejaba hueco real de sobra al TEXTO de "Papelera" (8 letras a
			// escala 0.62, ~50px), pero la CAJA declarada de esa etiqueta mide 70px (0 a 70) - 6px
			// mas que el propio hueco de 64, asi que las dos CAJAS se seguian solapando aunque el
			// texto no llegara a tocarse de verdad. Encontrado por la autopruena de espaciado
			// ampliada (13-sep-2026), que compara cajas reales, no solo texto: "cajas" es
			// literalmente parte del encargo ("que no se solape texto/cajas/opciones"). 74 deja 4px
			// de margen real tras el borde de la caja de "Papelera" (70).
			EtiquetaTk etiquetaSeleccion = new EtiquetaTk(
				() => Idiomas.Texto("Libreria.EditorPrefijo.RecuadroTitulo"), 0.62f, 100f, 14f);
			etiquetaSeleccion.ColorTexto = EstiloTk.TextoSuave;
			etiquetaSeleccion.Left.Set(74f, 0f);
			etiquetaSeleccion.Top.Set(filaUno, 0f);
			Append(etiquetaSeleccion);

			_seleccion = new SlotSeleccionTk(0.55f);
			_seleccion.Left.Set(74f, 0f);
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

		// =====================================================================================
		// TM2 del catalogo de rediseño visual ("Editor de objeto flotante"): en cuanto hay algo en
		// el recuadro de seleccion, TarjetaEdicionFlotanteTk aparece junto a este mini-panel con el
		// objeto a 2x, su nombre en el color real de su rareza y un aviso de "mejor prefijo" ya
		// calculado - ver el XMLdoc completo de esa clase para el porque de cada decision de diseño
		// (sobre todo por que NO reemplaza el arrastre a SlotSeleccionTk y por que su prefijo es
		// solo informativo, no un EditorPrefijoTk anidado).
		// =====================================================================================

		private TarjetaEdicionFlotanteTk _tarjetaFlotante;
		private bool _tarjetaFlotanteAbierta;

		/// <summary>La tarjeta flotante, si se ha llegado a crear (solo la primera vez que hay algo
		/// seleccionado). Expuesta para que la autoprueba pueda leerla sin duplicar la logica de
		/// mostrar/ocultar.</summary>
		public TarjetaEdicionFlotanteTk TarjetaFlotante => _tarjetaFlotante;

		/// <summary>true si la tarjeta flotante esta visible ahora mismo. Lo lee la autoprueba.</summary>
		public bool TarjetaFlotanteAbierta => _tarjetaFlotanteAbierta;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			ActualizarTarjetaFlotante();
		}

		/// <summary>
		/// Muestra/esconde la tarjeta flotante segun si <see cref="SlotSeleccionTk.ObjetoActual"/>
		/// tiene algo ahora mismo - nunca un evento de clic que rastrear, ver el XMLdoc de
		/// <see cref="TarjetaEdicionFlotanteTk"/> para el porque real de esta decision.
		/// </summary>
		private void ActualizarTarjetaFlotante()
		{
			bool hayObjeto = _seleccion.ObjetoActual != null && !_seleccion.ObjetoActual.IsAir;
			CapaSuperposicionTk capa = CapaSuperposicionTk.Buscar(this);
			if (capa == null) {
				// Montado suelto (una prueba sin panel alrededor, p.ej.): sin capa no hay donde
				// flotar la tarjeta con seguridad, se deja sin mostrar en vez de arriesgar el
				// mismo desborde real que la propia CapaSuperposicionTk existe para evitar.
				return;
			}

			if (!hayObjeto) {
				if (_tarjetaFlotanteAbierta) {
					capa.Quitar(_tarjetaFlotante);
					_tarjetaFlotanteAbierta = false;
				}
				return;
			}

			if (_tarjetaFlotante == null) {
				_tarjetaFlotante = new TarjetaEdicionFlotanteTk(() => _seleccion.ObjetoActual);
			}

			PosicionarTarjetaFlotante(capa);

			// Bug real encontrado y arreglado con la propia autoprueba (ver bitacora.md): la tarjeta
			// y el desplegable de EditorPrefijoTk (del MISMO mini-panel, no anidado dentro de la
			// tarjeta - ver el XMLdoc de TarjetaEdicionFlotanteTk para el porque de esa decision) se
			// disputan la MISMA CapaSuperposicionTk, que solo admite un contenido a la vez. Sin esta
			// comprobacion, la tarjeta volvia a intentar mostrarse en el fotograma siguiente a que el
			// desplegable de prefijo se abriera (capa.Ocupada ya por el popup) y capa.Mostrar hace
			// Quitar(null) ANTES de colgar lo nuevo - expulsando al popup que el jugador acababa de
			// abrir. Aqui se cede el turno: si la capa la ocupa algo que NO es esta misma tarjeta, se
			// espera a que se libere sola (el desplegable la suelta el solita al cerrarse) en vez de
			// arrebatarsela.
			if (capa.Ocupada && !ReferenceEquals(capa.Contenido, _tarjetaFlotante)) {
				return;
			}

			if (!_tarjetaFlotanteAbierta) {
				capa.Mostrar(_tarjetaFlotante, () => _tarjetaFlotanteAbierta = false);
				_tarjetaFlotanteAbierta = true;
			}
		}

		/// <summary>
		/// Coloca la tarjeta "pegada" a este mini-panel (a su izquierda, con el borde superior
		/// alineado; si no cabe ahí dentro del area util del marco, acotada al area, nunca
		/// desbordando hacia el mundo) - el MISMO patron real, calculo a calculo, que ya usa y
		/// tiene probado <see cref="EditorPrefijoTk"/> para su propio popup.
		/// </summary>
		private void PosicionarTarjetaFlotante(CapaSuperposicionTk capa)
		{
			CalculatedStyle area = capa.GetInnerDimensions();
			CalculatedStyle esteMiniPanel = GetDimensions();
			const float Separacion = 8f;

			float x = esteMiniPanel.X - TarjetaEdicionFlotanteTk.Ancho - Separacion;
			if (x < area.X) {
				x = esteMiniPanel.X + esteMiniPanel.Width + Separacion;
			}
			if (x + TarjetaEdicionFlotanteTk.Ancho > area.X + area.Width) {
				x = area.X + area.Width - TarjetaEdicionFlotanteTk.Ancho;
			}
			if (x < area.X) {
				x = area.X;
			}

			float y = esteMiniPanel.Y;
			if (y + TarjetaEdicionFlotanteTk.Alto > area.Y + area.Height) {
				y = area.Y + area.Height - TarjetaEdicionFlotanteTk.Alto;
			}
			if (y < area.Y) {
				y = area.Y;
			}

			// Left/Top son relativos al INTERIOR del padre real (la propia capa) - la posicion de
			// pantalla que se acaba de calcular se pasa a coordenadas del padre restandole el
			// origen del area, mismo criterio que EditorPrefijoTk.ConstruirPopup.
			_tarjetaFlotante.Left.Set(x - area.X, 0f);
			_tarjetaFlotante.Top.Set(y - area.Y, 0f);
		}
	}
}
