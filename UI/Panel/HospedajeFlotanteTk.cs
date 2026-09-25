using Microsoft.Xna.Framework;
using Terraria.UI;

namespace TerrakeepMod.UI.Panel
{
	/// <summary>
	/// El hueco donde flota contenido PERSISTENTE del panel (hoy, <c>TarjetaEdicionFlotanteTk</c>
	/// de la Librería/Personaje): al contrario que <see cref="CapaSuperposicionTk"/> (pensada para
	/// desplegables de un solo uso que SÍ deben cerrarse con un clic fuera), este hospedaje nunca
	/// cierra su contenido por su cuenta ni intercepta el clic que cae fuera de él - lo deja pasar
	/// a lo que haya debajo (catálogo, árbol, búsqueda...).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Investigación real (bitacora.md, "editor atrapado"/"trampa tarjeta flotante"):</b> antes
	/// de esta clase, <c>TarjetaEdicionFlotanteTk</c> vivía dentro de la misma
	/// <see cref="CapaSuperposicionTk"/> que usa el desplegable de prefijo. Esa capa se construye
	/// del MISMO tamaño que toda la zona de contenido del panel (a propósito, para que un
	/// desplegable pueda posicionarse en cualquier punto de esa zona sin salirse) y, en cuanto
	/// aloja algo, se comporta como el fondo de un menú modal: cualquier clic dentro de su
	/// rectángulo que no caiga sobre el propio desplegable la cierra (correcto para un desplegable
	/// de un solo uso) - pero para un editor PERSISTENTE como la tarjeta, eso significaba que
	/// CUALQUIER clic dentro de la zona de contenido de la Librería (la rejilla de catálogo, el
	/// árbol de carpetas, la búsqueda) quedaba inalcanzable mientras hubiera algo en el recuadro de
	/// selección, y encima la tarjeta se reabría sola el fotograma siguiente
	/// (<c>PanelHerramientasLibreriaTk.ActualizarTarjetaFlotante</c> la vuelve a mostrar en cuanto
	/// ve que <c>SlotSeleccionTk.ObjetoActual</c> sigue lleno) - un bucle sin ningún clic real que
	/// dejara interactuar con el resto del panel.
	/// </para>
	/// <para>
	/// <b>Por qué no basta con "no cerrarla" (dejar <c>CapaSuperposicionTk.LeftClick</c> sin
	/// tocar el contenido).</b> Investigado leyendo <c>UIElement.GetElementAt</c> REAL
	/// (decompilado con <c>ilspycmd</c> sobre <c>tModLoader.dll</c> instalado, no supuesto): un
	/// contenedor NO modal (<c>IgnoresMouseInteraction = false</c>) que cubre toda la zona de
	/// contenido sigue absorbiendo cualquier clic que no caiga sobre uno de sus hijos, porque
	/// <c>GetElementAt</c> devuelve el propio contenedor como "objetivo" cuando ninguno de sus
	/// hijos contiene el punto (<c>return uIElement.GetElementAt(point) ?? uIElement;</c> en el
	/// padre que lo evalúa) - o sea que aunque no cerrara nada, el clic seguiría sin llegar nunca
	/// al catálogo de debajo. <c>GetElementAt</c> NO es <c>virtual</c> (confirmado en el mismo
	/// volcado), así que no se puede sobrescribir a mano en una subclase para cambiar ese
	/// comportamiento. Lo que SÍ es <c>virtual</c> es <see cref="ContainsPoint"/>: aquí se
	/// sobrescribe para que este hospedaje solo "contenga" el punto del ratón cuando cae dentro
	/// del hijo real que aloja - nunca porque cae en su propia zona completa. Con eso, un clic
	/// fuera del hijo hace que <c>GetElementAt</c> lo salte por completo y siga bajando por el
	/// resto de hijos del marco (<c>_contenedor</c>), llegando de verdad al catálogo/árbol/
	/// búsqueda.
	/// </para>
	/// <para>
	/// <b>Efecto colateral bueno, no buscado a propósito pero verificado:</b> mientras la tarjeta
	/// vivía en la capa modal de zona completa, <c>CapaSuperposicionTk.TapaAlRaton</c> (que
	/// <c>SlotSeleccionTk.DrawSelf</c> consulta antes de dejar arrastrar) devolvía <c>true</c> para
	/// CUALQUIER punto de la zona de contenido mientras la tarjeta estuviera abierta - incluido el
	/// propio recuadro de selección, así que ni siquiera se podía arrastrar el objeto de vuelta
	/// fuera para cerrarla (el mecanismo de cierre que documenta el XMLdoc de
	/// <c>TarjetaEdicionFlotanteTk</c>). Al vivir aquí, en un hospedaje ajeno a
	/// <c>CapaSuperposicionTk</c>, ese bloqueo desaparece: el recuadro vuelve a responder al
	/// arrastre con la tarjeta abierta.
	/// </para>
	/// </remarks>
	public class HospedajeFlotanteTk : UIElement
	{
		private UIElement _contenido;

		/// <summary>El contenido flotante que hay ahora mismo, o null.</summary>
		public UIElement Contenido => _contenido;

		/// <summary>true si hay algo alojado ahora mismo.</summary>
		public bool Ocupada => _contenido != null;

		/// <summary>
		/// Aloja <paramref name="contenido"/> (quitando antes lo que hubiera). A diferencia de
		/// <see cref="CapaSuperposicionTk.Mostrar"/>, no hay callback "al quitar": este hospedaje
		/// nunca cierra su contenido por su cuenta (ni por clic fuera, ni por perder el foco), así
		/// que quien lo llama es siempre quien decide cuándo esconderlo con <see cref="Quitar"/>.
		/// </summary>
		public void Mostrar(UIElement contenido)
		{
			if (contenido == null || ReferenceEquals(contenido, _contenido)) {
				return;
			}

			Quitar(null);

			_contenido = contenido;
			Append(contenido);
			Recalculate();
		}

		/// <summary>
		/// Quita lo que haya alojado. Con <paramref name="contenido"/> a null quita lo que sea; con
		/// un elemento concreto solo lo quita si es EXACTAMENTE ese (mismo criterio de seguridad que
		/// <see cref="CapaSuperposicionTk.Quitar"/>).
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
			_contenido = null;
			RemoveChild(fuera);
		}

		/// <summary>Igual que <see cref="CapaSuperposicionTk.OnDeactivate"/>: al cerrar el panel no
		/// puede quedar contenido huérfano colgado aquí.</summary>
		public override void OnDeactivate()
		{
			base.OnDeactivate();
			Quitar(null);
		}

		/// <summary>
		/// Solo "contiene" el punto del ratón si cae dentro del hijo real que aloja ahora mismo -
		/// nunca porque cae en su propia zona completa (que mide lo mismo que la zona de contenido
		/// entera del panel, por conveniencia de coordenadas, igual que <c>CapaSuperposicionTk</c>).
		/// Ver el XMLdoc de la clase para el porqué exacto (código real de
		/// <c>UIElement.GetElementAt</c>, no <c>virtual</c>, decompilado antes de escribir esto).
		/// </summary>
		public override bool ContainsPoint(Vector2 point)
		{
			return _contenido != null && _contenido.ContainsPoint(point);
		}

		/// <summary>
		/// El hospedaje del panel en el que vive <paramref name="desde"/>, o null si ese elemento
		/// no está colgado de un panel que tenga uno (por ejemplo, montado suelto en una prueba).
		/// Mismo patrón que <see cref="CapaSuperposicionTk.Buscar"/>: sube hasta la raíz del árbol
		/// real y busca desde ahí, sin que el widget que lo pide tenga que conocer al panel.
		/// </summary>
		public static HospedajeFlotanteTk Buscar(UIElement desde)
		{
			if (desde == null) {
				return null;
			}

			UIElement raiz = desde;
			while (raiz.Parent != null) {
				raiz = raiz.Parent;
			}

			HospedajeFlotanteTk encontrado = null;
			raiz.ExecuteRecursively(elemento => {
				if (encontrado == null && elemento is HospedajeFlotanteTk) {
					encontrado = (HospedajeFlotanteTk)elemento;
				}
			});
			return encontrado;
		}
	}
}
