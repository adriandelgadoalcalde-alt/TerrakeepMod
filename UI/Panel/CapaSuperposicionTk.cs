using System;
using Terraria.UI;

namespace TerrakeepMod.UI.Panel
{
	/// <summary>
	/// <b>La capa donde viven los desplegables del panel</b> (hoy, el selector de prefijo de
	/// <c>EditorPrefijoTk</c>): un elemento vacio del tamaño de la zona de contenido del marco,
	/// colgado como <b>ULTIMO hijo</b> del marco, que presta su sitio a un unico elemento flotante
	/// cada vez.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que hace falta.</b> Un desplegable colgado del propio boton que lo abre tiene tres
	/// problemas reales, los tres reportados por el usuario con captura sobre el selector de
	/// prefijo de la Libreria:
	/// </para>
	/// <list type="number">
	/// <item><description><b>Se dibuja fuera del panel.</b> Ningun <c>UIElement</c> de vanilla
	/// recorta a sus hijos salvo que se le ponga <c>OverflowHidden</c>, asi que un hijo colocado
	/// con un <c>Left</c> mayor que el ancho de su padre se pinta igual, encima del MUNDO del
	/// juego. Posicionarlo respecto a <c>Main.screenWidth</c>/<c>Height</c> (lo que hacia antes)
	/// solo garantiza que cabe en la VENTANA, no que caiga dentro del panel.</description></item>
	/// <item><description><b>Los controles de mas abajo del marco lo tapan.</b>
	/// <c>UIElement.DrawChildren</c> dibuja en el orden en que se hizo <c>Append</c>, y el pie del
	/// marco (la ayuda y el boton "Cerrar") se añade DESPUES de la zona de contenido - o sea que
	/// cualquier cosa que salga del contenido queda por debajo del boton de cerrar, por muy
	/// "flotante" que sea.</description></item>
	/// <item><description><b>No recibe ni la rueda ni los clics del raton.</b>
	/// <c>UIElement.GetElementAt</c> (el que usa <c>UserInterface</c> para repartir clics y rueda,
	/// codigo real decompilado) solo <b>desciende</b> a un hijo si el hijo <c>ContainsPoint</c> el
	/// punto del raton - y para llegar a ese hijo antes ha tenido que descender por todos sus
	/// ancestros, que tambien tienen que contenerlo. Un desplegable de 240x220 colgado de un boton
	/// de 176x26 queda, para el raton, en tierra de nadie: se ve, pero el motor nunca le entrega
	/// el evento. Eso, y no la barra, era la causa real de que "el scroll no funcione".</description></item>
	/// </list>
	/// <para>
	/// Todo eso se arregla de raiz con esta capa: el desplegable sigue viviendo <b>dentro</b> del
	/// arbol de interfaz del panel (no se cuelga de <c>Main.InGameUI</c> ni de ninguna capa por
	/// fuera), pero como hijo de un elemento que (a) ocupa exactamente la zona util del marco, asi
	/// que colocarse dentro de ella es colocarse dentro del panel; (b) es el ULTIMO hijo del marco,
	/// asi que se dibuja por encima de todo lo demas, boton "Cerrar" incluido; y (c) contiene de
	/// verdad el punto del raton, asi que <c>GetElementAt</c> desciende hasta el desplegable y la
	/// rueda y los clics llegan.
	/// </para>
	/// <para>
	/// <b>Transparente al raton mientras esta vacia.</b> Con <c>IgnoresMouseInteraction</c> a true,
	/// <c>GetElementAt</c> la salta entera (y a sus hijos con ella, ver el codigo real), asi que
	/// una capa vacia no roba ni un clic al panel de debajo. En cuanto hay algo dentro se apaga ese
	/// flag: entonces la capa se comporta como el fondo de un menu modal - lo que caiga fuera del
	/// desplegable lo recibe ella, y lo cierra, en vez de atravesarlo hasta el control de debajo.
	/// </para>
	/// </remarks>
	public class CapaSuperposicionTk : UIElement
	{
		/// <summary>
		/// La capa que tiene algo flotando AHORA MISMO, si es que hay alguna.
		/// <para />
		/// Existe por un hueco real del sistema de eventos: las ranuras de objeto de este mod
		/// (<c>SlotObjetoVanilla</c>, <c>SlotCatalogoLibreria</c>, <c>SlotSeleccionTk</c>,
		/// <c>SlotPapeleraTk</c>) NO se enteran por <c>UIElement</c> de que hay un desplegable
		/// encima: gestionan el raton a mano dentro de su <c>DrawSelf</c>
		/// (<c>ContainsPoint(Main.MouseScreen)</c> + <c>ItemSlot.Handle</c>), que es la forma real de
		/// vanilla y la que les da coger/soltar/apilar gratis. Sin esta consulta, un clic en una fila
		/// del desplegable ADEMAS cogeria o soltaria el objeto de la ranura que quedara justo debajo.
		/// </summary>
		private static CapaSuperposicionTk _activa;

		/// <summary>true si hay un desplegable abierto y el punto dado cae dentro de su capa - o sea,
		/// si ese clic es para el desplegable (o para cerrarlo) y NO para lo que haya debajo.</summary>
		public static bool TapaAlRaton(Microsoft.Xna.Framework.Vector2 punto)
		{
			return _activa != null && _activa._contenido != null && _activa.ContainsPoint(punto);
		}

		private UIElement _contenido;
		private Action _alQuitar;

		public CapaSuperposicionTk()
		{
			// Ancho/alto los fija quien la coloca (PanelTerrakeepState, con las mismas medidas que la
			// zona de contenido). MaxWidth/MaxHeight se quedan en su Fill por defecto a proposito: el
			// padre real es el marco entero, que es grande - el recorte silencioso que arruino el
			// popup de prefijo (240x220 recortado a 176x26, ver EditorPrefijoTk) pasa cuando el padre
			// es MAS PEQUEÑO que el hijo, que es justo lo que esta capa evita.
			IgnoresMouseInteraction = true;
		}

		/// <summary>El elemento flotante que hay ahora mismo, o null.</summary>
		public UIElement Contenido => _contenido;

		/// <summary>true si hay algo flotando ahora mismo.</summary>
		public bool Ocupada => _contenido != null;

		/// <summary>
		/// Pone <paramref name="contenido"/> a flotar en esta capa (quitando antes lo que hubiera).
		/// <paramref name="alQuitar"/> se llama cuando se quite, sea por quien sea (clic fuera,
		/// cambio de pestaña, otro desplegable) para que su dueño se entere y limpie su estado.
		/// </summary>
		public void Mostrar(UIElement contenido, Action alQuitar)
		{
			if (contenido == null) {
				return;
			}

			Quitar(null);

			_contenido = contenido;
			_alQuitar = alQuitar;
			_activa = this;
			IgnoresMouseInteraction = false;
			Append(contenido);
			Recalculate();
		}

		/// <summary>
		/// Quita lo que haya flotando. Con <paramref name="contenido"/> a null quita lo que sea;
		/// con un elemento concreto solo lo quita si es EXACTAMENTE ese (asi un desplegable que se
		/// cierra tarde no se lleva por delante al que ya lo sustituyo).
		/// </summary>
		public void Quitar(UIElement contenido)
		{
			if (_contenido == null) {
				return;
			}
			if (contenido != null && !ReferenceEquals(contenido, _contenido)) {
				return;
			}

			UIElement fuera = _contenido;
			Action aviso = _alQuitar;
			_contenido = null;
			_alQuitar = null;
			if (ReferenceEquals(_activa, this)) {
				_activa = null;
			}
			IgnoresMouseInteraction = true;
			RemoveChild(fuera);

			// El aviso va al FINAL y con el estado ya limpio: el dueño casi siempre respondera
			// llamando a su propio Cerrar(), que vuelve a pasar por aqui - con _contenido ya a null
			// esa segunda vuelta no hace nada, en vez de entrar en bucle.
			if (aviso != null) {
				aviso();
			}
		}

		/// <summary>
		/// Al cerrarse el panel no puede quedar un desplegable a medias: <c>UserInterface.SetState</c>
		/// llama a <c>Deactivate()</c> del estado saliente, que baja recursivamente por todo el arbol
		/// llamando a este <c>OnDeactivate</c> (codigo real decompilado). Aqui es donde se garantiza
		/// que <see cref="TapaAlRaton"/> no se quede diciendo que si para siempre por una capa de un
		/// panel que ya no existe - lo que dejaria las ranuras de objeto sordas al raton.
		/// </summary>
		public override void OnDeactivate()
		{
			base.OnDeactivate();
			Quitar(null);
		}

		/// <summary>Clic en la propia capa (o sea, FUERA del desplegable): se cierra, que es lo que
		/// hace cualquier menu desplegable del mundo. Los clics sobre el desplegable llegan aqui
		/// tambien, porque <c>UIElement.LeftClick</c> burbujea hacia el padre, pero entonces
		/// <c>evt.Target</c> no es esta capa y no se toca nada.</summary>
		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (evt.Target == this) {
				Quitar(null);
			}
		}

		/// <summary>Igual que el clic izquierdo: el derecho fuera del desplegable tambien lo cierra.</summary>
		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			if (evt.Target == this) {
				Quitar(null);
			}
		}

		/// <summary>
		/// La capa del panel en la que vive <paramref name="desde"/>, o null si ese elemento no
		/// esta colgado de un panel que tenga una (por ejemplo, montado suelto en una prueba).
		/// Sube hasta la raiz del arbol real (<c>Parent == null</c>, o sea el <c>UIState</c>) y
		/// busca desde ahi, sin que el widget que la pide tenga que conocer al panel.
		/// </summary>
		public static CapaSuperposicionTk Buscar(UIElement desde)
		{
			if (desde == null) {
				return null;
			}

			UIElement raiz = desde;
			while (raiz.Parent != null) {
				raiz = raiz.Parent;
			}

			CapaSuperposicionTk encontrada = null;
			raiz.ExecuteRecursively(elemento => {
				if (encontrada == null && elemento is CapaSuperposicionTk) {
					encontrada = (CapaSuperposicionTk)elemento;
				}
			});
			return encontrada;
		}
	}
}
