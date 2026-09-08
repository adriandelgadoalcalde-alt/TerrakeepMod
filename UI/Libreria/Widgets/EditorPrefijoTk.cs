using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Prefijos;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;
using Terrakeep.Core.Data;

namespace TerrakeepMod.UI.Libreria.Widgets
{
	/// <summary>
	/// Cambia el PREFIJO (<c>Item.prefix</c>) del objeto seleccionado en <see cref="SlotSeleccionTk"/>.
	/// Un boton pequeño ("Prefijo: X") que al pulsarlo despliega una lista de los prefijos
	/// LEGALES para ESE objeto en concreto, agrupados igual que en la app de escritorio hermana
	/// (<see cref="CatalogoPrefijosLegales"/>, que reutiliza <c>Terrakeep.Core</c> tal cual
	/// - ver su cabecera para de donde sale la legalidad real).
	/// </summary>
	/// <remarks>
	/// <b>Como se cambia el prefijo SIN acumular multiplicadores.</b> <c>Item.Prefix(int)</c>
	/// multiplica las estadisticas ACTUALES del objeto (daño, tiempo de uso...) por las del
	/// prefijo pedido - llamarlo dos veces seguidas sobre el mismo objeto compondria los bonos
	/// (visto en el codigo real, <c>Terraria\Item.cs:1334</c>). La forma real y correcta de vanilla
	/// de CAMBIAR un prefijo ya puesto es la misma que usa el propio Puesto de Reforma
	/// (<c>Main.cs</c>, boton de reforjar: <c>reforgeItem.ResetPrefix(); reforgeItem.Prefix(-2);</c>):
	/// <see cref="Item.ResetPrefix"/> primero (vuelve las estadisticas a su base real, preservando
	/// tipo/pila/favorito) y solo entonces <c>Item.Prefix(idExacto)</c> con el prefijo elegido.
	/// </remarks>
	public class EditorPrefijoTk : UIElement
	{
		private const float AnchoPopup = 240f;
		private const float AltoPopup = 220f;

		private readonly Func<Item> _proveedor;
		private readonly BotonTk _botonToggle;
		private readonly float _ancho;
		private UIPanel _popup;
		private UIList _lista;
		private UIScrollbar _scroll;
		private CapaSuperposicionTk _capa;
		private float _anchoPopupReal = AnchoPopup;
		private bool _abierto;
		private Item _itemDelPopup;

		public EditorPrefijoTk(Func<Item> proveedor, float ancho = 150f)
		{
			_proveedor = proveedor;
			_ancho = ancho;

			Width.Set(ancho, 0f);
			Height.Set(26f, 0f);

			_botonToggle = new BotonTk("", 0.68f);
			_botonToggle.Width.Set(ancho, 0f);
			_botonToggle.Height.Set(26f, 0f);
			_botonToggle.Ayuda = () => Idiomas.Texto("Libreria.EditorPrefijo.Ayuda");
			_botonToggle.AlPulsar += Alternar;
			Append(_botonToggle);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			Item objeto = _proveedor != null ? _proveedor() : null;
			bool hay = objeto != null && !objeto.IsAir && CatalogoPrefijosLegales.Listo;

			_botonToggle.Habilitado = hay;
			_botonToggle.FijarTexto(TextoBoton(objeto));

			// Si el objeto seleccionado cambia (se arrastra otro al recuadro, o se vacia) mientras
			// el popup esta abierto, se cierra: la lista que tendria delante dejaria de valer para
			// el objeto nuevo.
			if (_abierto && !ReferenceEquals(objeto, _itemDelPopup)) {
				Cerrar();
			}
			if (!hay && _abierto) {
				Cerrar();
			}
		}

		private string TextoBoton(Item objeto)
		{
			if (objeto == null || objeto.IsAir) {
				return Idiomas.Texto("Libreria.EditorPrefijo.BotonSinObjetivo");
			}
			string nombrePrefijo = objeto.prefix > 0
				? CatalogoPrefijosLegales.NombrePrefijo(objeto.prefix)
				: Idiomas.Texto("Libreria.Prefijo.Ninguno");
			return Idiomas.Texto("Libreria.EditorPrefijo.Boton", nombrePrefijo);
		}

		/// <summary>Abre el popup por su ruta real, para que la autoprueba pueda listar sus
		/// botones. No hace nada si ya estaba abierto o no hay objeto seleccionado.</summary>
		public void AbrirParaAutoprueba()
		{
			if (!_abierto) {
				Abrir();
			}
		}

		/// <summary>Los botones de prefijo del popup ABIERTO ahora mismo (el primero, "Ninguno",
		/// incluido), o una lista vacia si el popup no esta abierto. Los devuelve en el mismo orden
		/// en que estan en la lista, para que la autoprueba pueda pulsar uno por su ruta REAL
		/// (<c>BotonTk.LeftClick</c>).</summary>
		public List<BotonTk> BotonesPopupParaAutoprueba()
		{
			List<BotonTk> botones = new List<BotonTk>();
			if (_popup == null) {
				return botones;
			}
			_popup.ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (boton != null) {
					botones.Add(boton);
				}
			});
			return botones;
		}

		/// <summary>true si el popup esta abierto ahora mismo. Lo lee la autoprueba.</summary>
		public bool PopupAbierto => _abierto;

		/// <summary>SOLO DIAGNOSTICO: geometria real YA CALCULADA del popup y su lista, para el log
		/// de la autoprueba - demuestra si el popup ocupa el sitio real que deberia o si algo se ha
		/// quedado con tamaño 0 sin que ningun recuento de botones lo delate.</summary>
		public string DiagnosticoGeometria()
		{
			if (_popup == null) {
				return "(popup null)";
			}
			CalculatedStyle p = _popup.GetDimensions();
			CalculatedStyle l = _lista != null ? _lista.GetDimensions() : default;
			CalculatedStyle b = _botonToggle.GetDimensions();
			CalculatedStyle c = _capa != null ? _capa.GetInnerDimensions() : default;
			return "boton x=" + (int)b.X + " y=" + (int)b.Y + " " + (int)b.Width + "x" + (int)b.Height
				+ "; popup x=" + (int)p.X + " y=" + (int)p.Y + " " + (int)p.Width + "x" + (int)p.Height
				+ "; lista x=" + (int)l.X + " y=" + (int)l.Y + " " + (int)l.Width + "x" + (int)l.Height
				+ "; capa x=" + (int)c.X + " y=" + (int)c.Y + " " + (int)c.Width + "x" + (int)c.Height
				+ " (" + (_capa != null ? "el popup cuelga de la capa del panel" : "SIN capa: colgado de si mismo") + ")"
				+ "; dentroDeLaCapa=" + DentroDeLaCapa
				+ "; filas internas=" + (_lista != null ? _lista.Count : 0)
				+ "; alturaInternaTotal=" + (_lista != null ? _lista.GetTotalHeight() : 0f)
				+ "; " + DiagnosticoScroll()
				+ "; Main.screenWidth=" + Main.screenWidth + " Main.screenHeight=" + Main.screenHeight;
		}

		/// <summary>SOLO DIAGNOSTICO: estado real de la barra de scroll del popup.</summary>
		public string DiagnosticoScroll()
		{
			if (_scroll == null) {
				return "scroll (null)";
			}
			return "scroll ViewPosition=" + _scroll.ViewPosition.ToString("0.0")
				+ " ViewSize=" + _scroll.ViewSize.ToString("0.0")
				+ " MaxViewSize=" + _scroll.MaxViewSize.ToString("0.0")
				+ " CanScroll=" + _scroll.CanScroll
				+ "; primeraFila y=" + PrimeraFilaY.ToString("0.0");
		}

		/// <summary>true si el popup abierto cabe ENTERO dentro de la capa del panel (o sea, dentro
		/// del panel). Es la comprobacion que delata el bug original: antes se salia hacia el mundo
		/// del juego. Sin popup abierto, false.</summary>
		public bool DentroDeLaCapa {
			get {
				if (_popup == null || _capa == null) {
					return false;
				}
				CalculatedStyle p = _popup.GetDimensions();
				CalculatedStyle c = _capa.GetInnerDimensions();
				return p.X >= c.X - 0.5f && p.Y >= c.Y - 0.5f
					&& p.X + p.Width <= c.X + c.Width + 0.5f
					&& p.Y + p.Height <= c.Y + c.Height + 0.5f;
			}
		}

		/// <summary>Posicion Y REAL, en pantalla, de la primera fila de la lista. Cambia cuando el
		/// scroll se mueve de verdad: es la prueba de que el contenido visible se ha desplazado, no
		/// solo de que un numero interno haya cambiado.</summary>
		public float PrimeraFilaY {
			get {
				if (_lista == null) {
					return 0f;
				}
				foreach (UIElement fila in _lista) {
					return fila.GetDimensions().Y;
				}
				return 0f;
			}
		}

		/// <summary>Posicion actual de la barra de scroll del popup (0 = arriba del todo).</summary>
		public float PosicionScroll => _scroll != null ? _scroll.ViewPosition : 0f;

		/// <summary>Rectangulo real, ya calculado, del popup abierto (vacio si no hay ninguno).</summary>
		public CalculatedStyle RectanguloPopup => _popup != null ? _popup.GetDimensions() : default;

		/// <summary>true si <paramref name="elemento"/> es el popup abierto o algo colgado de el. Lo
		/// usa la autoprueba para comprobar que el motor entrega de verdad el raton al desplegable
		/// (<c>UIElement.GetElementAt</c>, la misma llamada que hace <c>UserInterface</c>).</summary>
		public bool EsDelPopup(UIElement elemento)
		{
			for (UIElement actual = elemento; actual != null; actual = actual.Parent) {
				if (ReferenceEquals(actual, _popup)) {
					return true;
				}
			}
			return false;
		}

		/// <summary>true si la lista de prefijos NO cabe entera y hace falta scroll de verdad.</summary>
		public bool NecesitaScroll => _scroll != null && _scroll.CanScroll;

		/// <summary>Centro real, en coordenadas de pantalla, del popup abierto. Lo usa la autoprueba
		/// para preguntarle al motor que elemento hay bajo ese punto por la ruta REAL
		/// (<c>UIElement.GetElementAt</c>, la misma que usa <c>UserInterface</c>).</summary>
		public Microsoft.Xna.Framework.Vector2 CentroPopup {
			get {
				if (_popup == null) {
					return Microsoft.Xna.Framework.Vector2.Zero;
				}
				CalculatedStyle p = _popup.GetDimensions();
				return new Microsoft.Xna.Framework.Vector2(p.X + p.Width / 2f, p.Y + p.Height / 2f);
			}
		}

		/// <summary>Nombres de las filas VISIBLES ahora mismo dentro del recorte real del popup, en
		/// orden. Con el scroll movido tiene que cambiar de verdad; es la evidencia en texto que
		/// acompaña a las dos capturas.</summary>
		public List<string> FilasVisibles()
		{
			List<string> visibles = new List<string>();
			if (_lista == null) {
				return visibles;
			}

			CalculatedStyle vista = _lista.GetDimensions();
			foreach (UIElement fila in _lista) {
				CalculatedStyle d = fila.GetDimensions();
				if (d.Y + d.Height <= vista.Y || d.Y >= vista.Y + vista.Height) {
					continue;
				}
				BotonTk boton = fila as BotonTk;
				visibles.Add(boton != null ? boton.Texto : "(" + fila.GetType().Name + ")");
			}
			return visibles;
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
			Item objeto = _proveedor != null ? _proveedor() : null;
			if (objeto == null || objeto.IsAir || !CatalogoPrefijosLegales.Listo) {
				return;
			}

			_itemDelPopup = objeto;
			ConstruirPopup(objeto);
			_abierto = true;
		}

		private void Cerrar()
		{
			// El estado propio se limpia ANTES de tocar la capa: quitar de la capa dispara su aviso
			// "me han quitado esto", que vuelve a entrar aqui - con todo ya a null esa segunda vuelta
			// no hace nada, en vez de rebotar en bucle entre los dos.
			UIPanel popup = _popup;
			CapaSuperposicionTk capa = _capa;
			_popup = null;
			_lista = null;
			_scroll = null;
			_capa = null;
			_itemDelPopup = null;
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

		/// <summary>Reconstruye el popup entero para <paramref name="objeto"/>: los grupos legales
		/// dependen del objeto concreto, asi que no tiene sentido guardar un popup entre aperturas.</summary>
		private void ConstruirPopup(Item objeto)
		{
			// ---------------------------------------------------------------------------------
			// DONDE VIVE EL POPUP: en la capa de superposicion del panel, NUNCA colgado del boton.
			//
			// Los tres bugs que reporto el usuario con captura (el desplegable dibujandose sobre el
			// MUNDO a la derecha del panel, el boton "Cerrar (O)" tapandolo, y el scroll sin
			// responder) eran tres sintomas del mismo error de raiz: colgarlo del boton que lo abre.
			// El detalle completo esta en la cabecera de CapaSuperposicionTk; en corto:
			//   - ningun UIElement recorta a sus hijos (salvo OverflowHidden), asi que un hijo
			//     colocado mas alla del ancho de su padre se dibuja igual, encima del mundo;
			//   - el marco dibuja a sus hijos en el orden de Append, y el pie ("Cerrar") va despues
			//     de la zona de contenido, asi que todo lo que salga del contenido queda por debajo;
			//   - UIElement.GetElementAt (el que reparte clics y rueda) solo desciende a un hijo si
			//     TODOS sus ancestros contienen el punto del raton: un popup de 240x220 que sobresale
			//     de un boton de 176x26 no recibia ni un evento, de ahi que la rueda no hiciera nada.
			// La capa arregla los tres de una vez: sigue dentro del arbol del panel, ocupa la zona
			// util del marco (asi que acotarse a ella es no salirse del panel), es el ultimo hijo del
			// marco (se dibuja por encima de todo) y contiene de verdad el punto del raton.
			// ---------------------------------------------------------------------------------
			_capa = CapaSuperposicionTk.Buscar(this);

			// El area REAL donde puede vivir el popup: la capa (zona util del marco) si la hay, y si
			// no - montado suelto en una prueba, sin panel alrededor - la ventana entera, como antes.
			CalculatedStyle area = _capa != null
				? _capa.GetInnerDimensions()
				: new CalculatedStyle(0f, 0f, Main.screenWidth, Main.screenHeight);

			// Tamaño REAL, fijado a mano en las cuatro propiedades. UIElement.MaxWidth/MaxHeight
			// valen StyleDimension.Fill POR DEFECTO (100% del padre): cuando el padre era el propio
			// boton "Prefijo: X" (176x26 px) eso RECORTABA el popup a 176x26 pese a pedirle 240x220
			// con Width/Height, sin ningun aviso ni excepcion - solo se veian ~14px de la primera
			// fila. Ahora el padre es la capa, que es grande, pero se siguen fijando explicitamente:
			// es el tamaño que este control quiere, no algo heredado de quien lo tenga colgado.
			// Ademas se acotan al area real, para que en una ventana diminuta el popup se ENCOJA en
			// vez de desbordarse (Math.Min normal, no una funcion del motor: aqui se decide un
			// numero, no se le pide nada a nadie).
			float anchoReal = Math.Min(AnchoPopup, area.Width);
			float altoReal = Math.Min(AltoPopup, area.Height);
			_anchoPopupReal = anchoReal;

			_popup = new UIPanel();
			_popup.Width.Set(anchoReal, 0f);
			_popup.Height.Set(altoReal, 0f);
			_popup.MaxWidth.Set(anchoReal, 0f);
			_popup.MaxHeight.Set(altoReal, 0f);
			_popup.BackgroundColor = EstiloTk.FondoCaja;
			// Borde visible, a diferencia del resto de cajas del panel: esto flota POR ENCIMA de
			// otros controles, y sin una linea que lo separe se lee como si formara parte de lo que
			// hay debajo.
			_popup.BorderColor = EstiloTk.BordeSobre * 0.55f;
			_popup.SetPadding(6f);

			// ---- Colocacion, en coordenadas de PANTALLA y acotada al area ----------------------
			// Preferencia (encargo explicito del usuario en su dia): a la DERECHA del boton, con el
			// borde superior alineado. Si ahi no cabe DENTRO DEL PANEL, a la izquierda; y pase lo que
			// pase, el ultimo acotado deja el popup dentro del area - nunca desbordando hacia el
			// mundo del juego, que es lo que se veia en la captura del usuario.
			CalculatedStyle boton = _botonToggle.GetDimensions();
			const float Separacion = 4f;

			float x = boton.X + boton.Width + Separacion;
			if (x + anchoReal > area.X + area.Width) {
				x = boton.X - anchoReal - Separacion;
			}
			if (x + anchoReal > area.X + area.Width) {
				x = area.X + area.Width - anchoReal;
			}
			if (x < area.X) {
				x = area.X;
			}

			float y = boton.Y;
			if (y + altoReal > area.Y + area.Height) {
				y = area.Y + area.Height - altoReal;
			}
			if (y < area.Y) {
				y = area.Y;
			}

			// Left/Top son relativos al INTERIOR del padre (UIElement.Recalculate usa
			// Parent.GetInnerDimensions, codigo real), asi que la posicion de pantalla que se acaba
			// de calcular se pasa a coordenadas del padre restandole el origen del area.
			_popup.Left.Set(x - area.X, 0f);
			_popup.Top.Set(y - area.Y, 0f);

			_lista = new UIList();
			_lista.Width.Set(-20f, 1f);
			_lista.Height.Set(0f, 1f);
			_lista.ListPadding = 3f;
			// Sin esto, UIList ORDENA sus elementos con List.Sort y UIElement.CompareTo, que devuelve
			// 0 para todos: List.Sort no es estable, asi que con muchas filas (aqui hay 66 reales)
			// puede permutarlas y dejar cada prefijo bajo una cabecera que no es la suya. Lo dice la
			// documentacion del propio UIList: "if elements are added in order, you can use an empty
			// sort method to preserve the original order".
			_lista.ManualSortMethod = elementos => { };
			_popup.Append(_lista);

			_scroll = new UIScrollbar();
			_scroll.Width.Set(16f, 0f);
			_scroll.Height.Set(0f, 1f);
			_scroll.HAlign = 1f;
			_popup.Append(_scroll);
			// SetScrollbar deja la barra ya sincronizada con el alto REAL de la lista (llama a
			// UpdateScrollbar, que hace el SetView de verdad con GetInnerDimensions().Height y el
			// alto interno acumulado). No hace falta - ni conviene - inventarse un SetView(100,1000)
			// a mano: seria un dato falso que el primer Recalculate pisa igualmente.
			_lista.SetScrollbar(_scroll);

			// La rueda sobre la LISTA la maneja la propia UIList. Este enganche cubre el resto de la
			// superficie del popup (el margen, y sobre todo la franja de la barra de scroll), donde
			// si no la rueda no haria nada. Se mira el ORIGEN del evento (evt.Target) y no el hover:
			// si el evento nacio dentro de la lista, ella ya lo ha aplicado y solo esta burbujeando
			// hacia arriba (UIList.ScrollWheel llama a base.ScrollWheel, codigo real), asi que
			// aplicarlo otra vez aqui desplazaria el doble.
			_popup.OnScrollWheel += (evento, elemento) => {
				if (_scroll == null || _lista == null || VieneDeLaLista(evento.Target)) {
					return;
				}
				_scroll.ViewPosition -= evento.ScrollWheelValue;
			};

			// "Ninguno" (quitar el prefijo) siempre arriba del todo, fuera de cualquier grupo.
			_lista.Add(CrearFilaPrefijo(0, objeto, Idiomas.Texto("Libreria.Prefijo.Ninguno"), ""));

			string metaAnterior = null;
			foreach (var entrada in CatalogoPrefijosLegales.GruposLegales(objeto)) {
				string nombreMeta = Idiomas.EnEspanol ? entrada.Meta.NameEs : entrada.Meta.NameEn;
				if (nombreMeta != metaAnterior) {
					metaAnterior = nombreMeta;
					_lista.Add(CrearCabecera(nombreMeta));
				}

				string nombreGrupo = Idiomas.EnEspanol ? entrada.Grupo.NameEs : entrada.Grupo.NameEn;
				_lista.Add(CrearCabecera("  " + nombreGrupo, EstiloTk.TextoSuave));

				foreach (int id in entrada.Ids) {
					string nombre = CatalogoPrefijosLegales.NombrePrefijo(id);
					string efecto = CatalogoPrefijosLegales.Efecto(id);
					_lista.Add(CrearFilaPrefijo(id, objeto, nombre, efecto));
				}
			}

			// Se cuelga de la capa AL FINAL, con el contenido ya dentro, y se recalcula desde el
			// propio popup: Recalculate baja de padres a hijos, asi que esta unica llamada deja la
			// lista, sus 60 y pico filas y la barra de scroll con sus medidas reales de una vez
			// (UIList.Recalculate termina llamando a UpdateScrollbar con el alto real acumulado).
			if (_capa != null) {
				_capa.Mostrar(_popup, Cerrar);
			}
			else {
				Append(_popup);
			}
			_popup.Recalculate();
		}

		/// <summary>true si <paramref name="objetivo"/> es la lista del popup o cualquier cosa
		/// colgada de ella (una fila, una cabecera).</summary>
		private bool VieneDeLaLista(UIElement objetivo)
		{
			for (UIElement actual = objetivo; actual != null; actual = actual.Parent) {
				if (ReferenceEquals(actual, _lista)) {
					return true;
				}
			}
			return false;
		}

		private EtiquetaTk CrearCabecera(string texto, Color? color = null)
		{
			string capturado = texto;
			EtiquetaTk etiqueta = new EtiquetaTk(() => capturado, 0.72f, _anchoPopupReal - 24f, 18f);
			etiqueta.ColorTexto = color ?? EstiloTk.TextoAviso;
			return etiqueta;
		}

		private BotonTk CrearFilaPrefijo(int prefixId, Item objeto, string nombre, string efecto)
		{
			BotonTk fila = new BotonTk(nombre, 0.7f);
			fila.Width.Set(0f, 1f);
			fila.Height.Set(22f, 0f);
			fila.Activo = objeto.prefix == prefixId;
			fila.Ayuda = () => efecto;
			fila.AlPulsar += () => AplicarPrefijo(objeto, prefixId);
			return fila;
		}

		/// <summary>
		/// Aplica el prefijo elegido de verdad, sin acumular multiplicadores (ver la nota de
		/// cabecera: <c>ResetPrefix</c> primero, <c>Prefix(id)</c> despues) y deja el cambio
		/// deshacible con Ctrl+Z, igual que el resto del panel.
		/// </summary>
		private void AplicarPrefijo(Item objeto, int prefixId)
		{
			if (objeto == null || objeto.IsAir) {
				return;
			}

			int antes = objeto.prefix;
			if (antes == prefixId) {
				Cerrar();
				return;
			}

			AplicarPrefijoReal(objeto, prefixId);
			int aplicadoDeVerdad = objeto.prefix;

			Historial.CambiarValor(
				Idiomas.Texto("Libreria.EditorPrefijo.Historial", objeto.Name,
					CatalogoPrefijosLegales.NombrePrefijo(antes), CatalogoPrefijosLegales.NombrePrefijo(aplicadoDeVerdad)),
				antes, aplicadoDeVerdad, (int valor) => AplicarPrefijoReal(objeto, valor));

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag +
				" Libreria: prefijo cambiado en vivo sobre \"" + objeto.Name + "\": " + antes +
				" -> " + objeto.prefix + " (pedido " + prefixId + ").");

			Cerrar();
		}

		/// <summary>El unico sitio que toca <c>Item.prefix</c> de verdad: reset + aplicar, para que
		/// tanto el clic real como Deshacer/Rehacer pasen por el MISMO camino y nunca compongan
		/// multiplicadores sobre un prefijo anterior.</summary>
		private static void AplicarPrefijoReal(Item objeto, int prefixId)
		{
			objeto.ResetPrefix();
			if (prefixId > 0) {
				objeto.Prefix(prefixId);
			}
		}
	}
}
