using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Libreria.Widgets
{
	/// <summary>
	/// <b>Idea 6 del catalogo de funciones ("cofres del mundo en vivo"):</b> lista TODOS los
	/// cofres REALMENTE colocados en el mundo cargado (<c>Main.chest[]</c>, confirmado con el
	/// decompilado real de <c>Terraria.Main</c>/<c>Terraria.Chest</c> que no tiene tope de
	/// distancia - a diferencia de los NPC, el juego mantiene la lista entera de cofres del mundo
	/// en memoria desde que carga la partida), ordenados por distancia al jugador, para elegir uno
	/// como un contenedor de destino MAS de la Libreria - reutiliza tal cual el mismo mecanismo ya
	/// probado (<c>ItemSlot.Handle</c> sobre <c>Chest.item[]</c>, el mismo tipo <c>Item[]</c> que
	/// los siete contenedores del jugador que ya edita <see cref="TerrakeepMod.UI.Libreria.ContenidoLibreria"/>).
	/// </summary>
	/// <remarks>
	/// <b>Por que cuelga de la misma CapaSuperposicionTk.</b> Mismo patron ya probado por
	/// <see cref="EditorPrefijoTk"/> y <see cref="TarjetaEdicionFlotanteTk"/> (ver sus XMLdoc para
	/// el porque real de vivir ahi y no colgado del boton que lo abre): un solo hueco compartido,
	/// se cede el turno si algo mas lo ocupa.
	/// <para />
	/// <b>Limite real, investigado y aceptado a proposito: solo partida de un jugador.</b>
	/// Escribir en <c>Chest.item</c> en un servidor real no se sincroniza solo - hace falta
	/// reenviar el cambio a cada cliente conectado (<c>NetMessage.SendData(MessageID.SyncChestItem, ...)</c>,
	/// API real de tModLoader) para que no se vea desincronizado en las otras pantallas. Es
	/// exactamente el mismo tipo de riesgo real que la idea 9 (guia de grupo): no hay ningun
	/// arnes de dos clientes tModLoader a la vez en toda la familia Keep para verificarlo con
	/// cuidado esta noche (confirmado buscando en <c>scripts\</c>). En vez de forzarlo sin poder
	/// probarlo, <see cref="RellenarFilas"/> revisa <c>Main.netMode</c> y, si no es partida de un
	/// jugador, enseña un aviso en vez de una lista de cofres - la escritura real de
	/// <see cref="TerrakeepMod.UI.Libreria.ContenidoLibreria"/> sobre el array del cofre elegido
	/// sigue existiendo para cuando exista ese arnes, sin tener que reescribir nada de esto.
	/// </remarks>
	public class SelectorCofreMundoTk : UIElement
	{
		// Diagnostico real (GeometriaParaPrueba, medido en vivo): el popup entero SIEMPRE cupo de
		// sobra en pantalla (X=659,68 W=300 contra 1090 de ancho logico) - el recorte real que se
		// veia en la captura era el boton "→ Mochila" sin auto-reduccion de escala (ver su
		// comentario mas abajo), nunca el popup saliendose de la ventana. 320f de ancho.
		private const float AnchoPopup = 320f;
		private const float AltoPopup = 300f;

		/// <summary>Modos de orden reales (idea 6, hueco cerrado el 20-sep-2026: el catálogo pedía
		/// "ordenar/agrupar" además de la distancia). Un BOTON QUE CICLA, no otro desplegable
		/// - <see cref="TerrakeepMod.UI.Personaje.Widgets.DesplegableTk"/> cuelga de la MISMA
		/// <see cref="CapaSuperposicionTk"/> que este selector, y abrirlo cerraría el selector
		/// entero (un solo hueco compartido, "se cede el turno si algo más lo ocupa" - ver el
		/// XMLdoc de la clase). <c>Llenos</c> hace de "agrupar": los cofres con algo dentro
		/// primero, los vacíos al final - un agrupamiento real, no una etiqueta vacía.</summary>
		private enum ModoOrden { Distancia, Nombre, Llenos }

		/// <summary>Tope de filas realmente dibujadas. Un mundo grande puede tener cientos de
		/// cofres colocados (<c>Main.maxChests</c> real es 8000); se muestran siempre los MAS
		/// CERCANOS primero (la lista ya viene ordenada por distancia) y se avisa de cuantos se
		/// quedan fuera, en vez de intentar dibujar miles de filas de golpe.</summary>
		private const int LimiteFilas = 200;

		/// <summary>Cuantos caracteres reales de un nombre de cofre se dejan ver en la fila. Un
		/// nombre de cofre vanilla llega a 63 caracteres (<c>Chest.MaxNameLength</c>) y
		/// <see cref="BotonTk"/> no encoge su texto solo (a diferencia de la tarjeta flotante de
		/// TM2): se acorta a mano para no desbordar la fila, nunca se deja un texto sin cortar que
		/// se salga de su caja.</summary>
		private const int MaximoCaracteresNombre = 26;

		private readonly Action<int> _alElegir;
		private UIPanel _popup;
		private UIList _lista;
		private UIScrollbar _scroll;
		private CampoTextoTk _campoBusqueda;
		private BotonTk _botonOrden;
		private CapaSuperposicionTk _capa;
		private bool _abierto;

		/// <summary>Texto de búsqueda por CONTENIDO (idea 6, hueco cerrado el 20-sep-2026): se
		/// mantiene entre una apertura del popup y la siguiente (a diferencia de la lista de filas,
		/// que se reconstruye cada vez), para que buscar algo y cerrar sin querer no lo borre.</summary>
		private string _busqueda = "";

		private ModoOrden _modoOrden = ModoOrden.Distancia;

		public SelectorCofreMundoTk(Action<int> alElegir)
		{
			_alElegir = alElegir;
			Width.Set(0f, 0f);
			Height.Set(0f, 0f);
		}

		/// <summary>true si el popup de eleccion esta abierto ahora mismo. Lo lee la autoprueba.</summary>
		public bool Abierto => _abierto;

		/// <summary>SOLO ARNES DE PRUEBAS: geometria REAL del popup ahora mismo (medida del motor,
		/// nunca adivinada de una captura), para diagnosticar recortes de verdad.</summary>
		public string GeometriaParaPrueba()
		{
			if (_popup == null) {
				return "(sin popup)";
			}
			CalculatedStyle dim = _popup.GetDimensions();
			return "popup X=" + dim.X + " Y=" + dim.Y + " W=" + dim.Width + " H=" + dim.Height +
				" | pantalla " + Main.screenWidth + "x" + Main.screenHeight;
		}

		/// <summary>Numero de filas de cofre REALMENTE dibujadas la ultima vez que se abrio (sin
		/// contar el resumen/aviso). Lo lee la autoprueba.</summary>
		public int FilasDibujadas { get; private set; }

		/// <summary>Las filas de cofre (nunca los rotulos de resumen/aviso, que son
		/// <see cref="EtiquetaTk"/>, no <see cref="BotonTk"/>) del popup ABIERTO ahora mismo, en el
		/// mismo orden en que estan en la lista - para que la autoprueba pueda pulsar una por su
		/// ruta REAL (<c>BotonTk.LeftClick</c>), igual que ya hace <c>EditorPrefijoTk.BotonesPopupParaAutoprueba</c>.</summary>
		/// <summary>SOLO ARNES DE PRUEBAS: fija el texto de busqueda por la via REAL (el mismo
		/// campo <c>AlCambiar</c> que dispara al escribir de verdad) y rehace la lista, sin
		/// necesitar simular tecla a tecla. Idea 6, hueco cerrado el 20-sep-2026.</summary>
		public void BuscarParaPrueba(string texto)
		{
			if (_campoBusqueda == null) {
				return;
			}
			_campoBusqueda.FijarTextoSilencioso(texto ?? "");
			_busqueda = texto ?? "";
			RellenarFilas(_popup != null ? _popup.GetDimensions().Width : AnchoPopup);
			_lista.Recalculate();
		}

		/// <summary>SOLO ARNES DE PRUEBAS: pulsa el boton de orden real N veces, exactamente como
		/// lo haria un clic de verdad.</summary>
		public void CiclarOrdenParaPrueba()
		{
			if (_botonOrden == null) {
				return;
			}
			CalculatedStyle dim = _botonOrden.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			_botonOrden.LeftClick(new UIMouseEvent(_botonOrden, centro));
		}

		/// <summary>El modo de orden actual, como texto - para que la autoprueba pueda dejar
		/// constancia de por cual paso.</summary>
		public string ModoOrdenParaPrueba => _modoOrden.ToString();

		/// <summary>Los botones "→ Mochila" reales del popup ABIERTO ahora mismo (idea 6, "traer a
		/// mi inventario"), en el mismo orden que las filas.</summary>
		public List<BotonTk> BotonesTraerParaAutoprueba()
		{
			List<BotonTk> botones = new List<BotonTk>();
			if (_popup == null) {
				return botones;
			}
			_popup.ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (boton != null && boton.Texto == Idiomas.Texto("Libreria.SelectorCofre.Traer")) {
					botones.Add(boton);
				}
			});
			return botones;
		}

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

		/// <summary>Cuantos cofres reales hay ahora mismo en el mundo cargado
		/// (<c>Main.chest[]</c> no nulos). Publico para el texto del boton de la barra y para la
		/// autoprueba.</summary>
		public static int TotalCofresReales()
		{
			Chest[] cofres = Main.chest;
			if (cofres == null) {
				return 0;
			}

			int total = 0;
			for (int i = 0; i < cofres.Length; i++) {
				if (cofres[i] != null) {
					total++;
				}
			}
			return total;
		}

		/// <summary>Abre o cierra el popup, anclado a <paramref name="ancla"/> (el boton "Cofre"
		/// de la barra de destinos).</summary>
		public void AlternarEn(UIElement ancla)
		{
			if (_abierto) {
				Cerrar();
			}
			else {
				Abrir(ancla);
			}
		}

		private void Abrir(UIElement ancla)
		{
			ConstruirPopup(ancla);
			_abierto = true;
		}

		private void Cerrar()
		{
			// Mismo orden que EditorPrefijoTk.Cerrar: el estado propio se limpia ANTES de tocar la
			// capa, para que el aviso "me han quitado esto" que dispara Quitar() no rebote en
			// bucle si vuelve a entrar aqui.
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

		private void ConstruirPopup(UIElement ancla)
		{
			_capa = CapaSuperposicionTk.Buscar(this);

			CalculatedStyle area = _capa != null
				? _capa.GetInnerDimensions()
				: new CalculatedStyle(0f, 0f, Main.screenWidth, Main.screenHeight);

			float anchoReal = Math.Min(AnchoPopup, area.Width);
			float altoReal = Math.Min(AltoPopup, area.Height);

			_popup = new UIPanel();
			_popup.Width.Set(anchoReal, 0f);
			_popup.Height.Set(altoReal, 0f);
			_popup.MaxWidth.Set(anchoReal, 0f);
			_popup.MaxHeight.Set(altoReal, 0f);
			_popup.BackgroundColor = EstiloTk.FondoCaja;
			_popup.BorderColor = EstiloTk.BordeSobre * 0.55f;
			_popup.SetPadding(6f);

			CalculatedStyle botonDim = ancla.GetDimensions();
			const float Separacion = 4f;

			float x = botonDim.X + botonDim.Width + Separacion;
			if (x + anchoReal > area.X + area.Width) {
				x = botonDim.X - anchoReal - Separacion;
			}
			if (x + anchoReal > area.X + area.Width) {
				x = area.X + area.Width - anchoReal;
			}
			if (x < area.X) {
				x = area.X;
			}

			float y = botonDim.Y;
			if (y + altoReal > area.Y + area.Height) {
				y = area.Y + area.Height - altoReal;
			}
			if (y < area.Y) {
				y = area.Y;
			}

			_popup.Left.Set(x - area.X, 0f);
			_popup.Top.Set(y - area.Y, 0f);

			// Idea 6, hueco cerrado el 20-sep-2026: fila de "buscar por contenido" + "ordenar" ANTES
			// de la lista - mismo ancho que el resto del popup, altura fija, y la lista/scroll
			// empiezan justo debajo (AltoFilaHerramientas de hueco real, no una superposicion).
			const float AltoFilaHerramientas = 24f;
			const float AnchoBotonOrden = 92f;

			UIElement filaHerramientas = new UIElement();
			filaHerramientas.Width.Set(0f, 1f);
			filaHerramientas.Height.Set(AltoFilaHerramientas, 0f);
			_popup.Append(filaHerramientas);

			_campoBusqueda = new CampoTextoTk(() => Idiomas.Texto("Libreria.SelectorCofre.PistaBusqueda"), 40, 0.68f);
			_campoBusqueda.Width.Set(-(AnchoBotonOrden + 4f), 1f);
			_campoBusqueda.Height.Set(0f, 1f);
			_campoBusqueda.FijarTextoSilencioso(_busqueda);
			_campoBusqueda.AlCambiar += texto => {
				_busqueda = texto ?? "";
				RellenarFilas(anchoReal);
				_lista.Recalculate();
			};
			filaHerramientas.Append(_campoBusqueda);

			_botonOrden = new BotonTk(TextoBotonOrden(), 0.62f);
			_botonOrden.Width.Set(AnchoBotonOrden, 0f);
			_botonOrden.Height.Set(0f, 1f);
			_botonOrden.HAlign = 1f;
			_botonOrden.Ayuda = () => Idiomas.Texto("Libreria.SelectorCofre.OrdenAyuda");
			_botonOrden.AlPulsar += () => {
				_modoOrden = (ModoOrden)(((int)_modoOrden + 1) % 3);
				_botonOrden.FijarTexto(TextoBotonOrden());
				RellenarFilas(anchoReal);
				_lista.Recalculate();
			};
			filaHerramientas.Append(_botonOrden);

			_lista = new UIList();
			_lista.Width.Set(-20f, 1f);
			_lista.Height.Set(-(AltoFilaHerramientas + 4f), 1f);
			_lista.Top.Set(AltoFilaHerramientas + 4f, 0f);
			_lista.ListPadding = 3f;
			_lista.ManualSortMethod = elementos => { };
			_popup.Append(_lista);

			_scroll = new UIScrollbar();
			_scroll.Width.Set(16f, 0f);
			_scroll.Height.Set(-(AltoFilaHerramientas + 4f), 1f);
			_scroll.Top.Set(AltoFilaHerramientas + 4f, 0f);
			_scroll.HAlign = 1f;
			_popup.Append(_scroll);
			_lista.SetScrollbar(_scroll);

			_popup.OnScrollWheel += (evento, elemento) => {
				if (_scroll == null || _lista == null || VieneDeLaLista(evento.Target)) {
					return;
				}
				_scroll.ViewPosition -= evento.ScrollWheelValue;
			};

			RellenarFilas(anchoReal);

			if (_capa != null) {
				_capa.Mostrar(_popup, Cerrar);
			}
			else {
				Append(_popup);
			}
			_popup.Recalculate();
		}

		private string TextoBotonOrden()
		{
			switch (_modoOrden) {
				case ModoOrden.Nombre: return Idiomas.Texto("Libreria.SelectorCofre.OrdenNombre");
				case ModoOrden.Llenos: return Idiomas.Texto("Libreria.SelectorCofre.OrdenLlenos");
				default: return Idiomas.Texto("Libreria.SelectorCofre.OrdenDistancia");
			}
		}

		/// <summary>Tipo real del PRIMER objeto de <paramref name="cofre"/> cuyo nombre (en el
		/// idioma real del juego, <c>ContentSamples.ItemsByType</c>) contiene
		/// <paramref name="busqueda"/> - idea 6, "buscar por contenido". -1 si no hay ninguno o si
		/// la busqueda esta vacia (una busqueda vacia no filtra nada, no "no encuentra nada").</summary>
		private static int ObjetoQueCoincide(Chest cofre, string busqueda)
		{
			if (string.IsNullOrWhiteSpace(busqueda) || cofre.item == null) {
				return -1;
			}
			for (int s = 0; s < cofre.item.Length; s++) {
				Item objeto = cofre.item[s];
				if (objeto == null || objeto.IsAir) {
					continue;
				}
				string nombre = objeto.Name;
				if (!string.IsNullOrEmpty(nombre) &&
					nombre.IndexOf(busqueda, StringComparison.OrdinalIgnoreCase) >= 0) {
					return objeto.type;
				}
			}
			return -1;
		}

		private void RellenarFilas(float anchoPopup)
		{
			_lista.Clear();
			FilasDibujadas = 0;

			if (Main.netMode != NetmodeID.SinglePlayer) {
				_lista.Add(NuevaEtiqueta(Idiomas.Texto("Libreria.SelectorCofre.SoloUnJugador"), EstiloTk.TextoAviso, anchoPopup));
				return;
			}

			Chest[] cofres = Main.chest;
			Player jugador = Main.LocalPlayer;
			List<(int indice, float distancia, string nombre, int objetoCoincide)> encontrados =
				new List<(int, float, string, int)>();
			if (cofres != null) {
				for (int i = 0; i < cofres.Length; i++) {
					Chest cofre = cofres[i];
					if (cofre == null) {
						continue;
					}
					int objetoCoincide = ObjetoQueCoincide(cofre, _busqueda);
					// Con busqueda de verdad (no vacia), solo entran los cofres que SI tienen algo
					// que coincida - "buscar por contenido" de verdad, no un adorno.
					if (!string.IsNullOrWhiteSpace(_busqueda) && objetoCoincide < 0) {
						continue;
					}
					float distancia = jugador != null
						? Vector2.Distance(jugador.Center, new Vector2(cofre.x * 16f + 16f, cofre.y * 16f + 16f)) / 16f
						: 0f;
					encontrados.Add((i, distancia, cofre.name ?? "", objetoCoincide));
				}
			}

			// Idea 6, hueco cerrado el 20-sep-2026: "ordenar/agrupar" - tres criterios reales,
			// nunca solo distancia. Llenos = agrupa (los que tienen algo dentro primero, empate por
			// distancia); Nombre = orden alfabetico real por Chest.name (los sin nombre, al final,
			// nunca mezclados al azar entre los que si tienen).
			switch (_modoOrden) {
				case ModoOrden.Nombre:
					encontrados.Sort((a, b) => {
						bool aVacio = string.IsNullOrWhiteSpace(a.nombre);
						bool bVacio = string.IsNullOrWhiteSpace(b.nombre);
						if (aVacio != bVacio) {
							return aVacio ? 1 : -1;
						}
						return string.Compare(a.nombre, b.nombre, StringComparison.OrdinalIgnoreCase);
					});
					break;
				case ModoOrden.Llenos:
					encontrados.Sort((a, b) => {
						bool aLleno = TieneAlgo(cofres[a.indice]);
						bool bLleno = TieneAlgo(cofres[b.indice]);
						if (aLleno != bLleno) {
							return aLleno ? -1 : 1;
						}
						return a.distancia.CompareTo(b.distancia);
					});
					break;
				default:
					encontrados.Sort((a, b) => a.distancia.CompareTo(b.distancia));
					break;
			}

			_lista.Add(NuevaEtiqueta(Idiomas.Texto("Libreria.SelectorCofre.Resumen", encontrados.Count), EstiloTk.TextoSuave, anchoPopup));

			if (encontrados.Count == 0) {
				_lista.Add(NuevaEtiqueta(Idiomas.Texto(
					string.IsNullOrWhiteSpace(_busqueda) ? "Libreria.SelectorCofre.Ninguno" : "Libreria.SelectorCofre.NingunoConBusqueda"),
					EstiloTk.TextoSuave, anchoPopup));
				return;
			}

			int mostrados = Math.Min(encontrados.Count, LimiteFilas);
			for (int k = 0; k < mostrados; k++) {
				int indice = encontrados[k].indice;
				float distancia = encontrados[k].distancia;
				int objetoCoincide = encontrados[k].objetoCoincide;
				_lista.Add(CrearFilaCofre(indice, cofres[indice], distancia, anchoPopup, objetoCoincide));
			}
			FilasDibujadas = mostrados;

			if (encontrados.Count > mostrados) {
				_lista.Add(NuevaEtiqueta(Idiomas.Texto("Libreria.SelectorCofre.MasCofres", encontrados.Count - mostrados), EstiloTk.TextoSuave, anchoPopup));
			}
		}

		private static bool TieneAlgo(Chest cofre)
		{
			if (cofre.item == null) {
				return false;
			}
			for (int s = 0; s < cofre.item.Length; s++) {
				if (cofre.item[s] != null && !cofre.item[s].IsAir) {
					return true;
				}
			}
			return false;
		}

		/// <summary>Ancho real del boton "traer" rapido (idea 6, hueco cerrado el 20-sep-2026:
		/// "traer a mi inventario"). Solo aparece en filas donde la busqueda de verdad encontro un
		/// objeto real dentro del cofre.</summary>
		private const float AnchoBotonTraer = 58f;

		private UIElement CrearFilaCofre(int indice, Chest cofre, float distancia, float anchoPopup, int objetoCoincide)
		{
			int llenos = 0;
			if (cofre.item != null) {
				for (int s = 0; s < cofre.item.Length; s++) {
					if (cofre.item[s] != null && !cofre.item[s].IsAir) {
						llenos++;
					}
				}
			}

			string nombre = Acortar(cofre.name, MaximoCaracteresNombre);
			string etiquetaCofre = string.IsNullOrWhiteSpace(nombre)
				? Idiomas.Texto("Libreria.SelectorCofre.SinNombre", cofre.x, cofre.y)
				: Idiomas.Texto("Libreria.SelectorCofre.ConNombre", nombre, cofre.x, cofre.y);

			string texto = Idiomas.Texto("Libreria.SelectorCofre.Fila", etiquetaCofre, (int)distancia, llenos, cofre.item.Length);

			// Bug real encontrado con la propia captura (ws3-traer-a-inventario.png, antes de este
			// arreglo): con el boton "Traer" ocupando sitio a la derecha, la fila se queda mas
			// estrecha y el MISMO texto de siempre ("...  ·  485 tiles  ·  7/40 items") ya no cabe -
			// BotonTk no envuelve ni encoge sola (documentado ya en el XMLdoc de
			// MaximoCaracteresNombre), asi que salia recortado a medio caracter ("...Inventor",
			// "...40 iter"). Mismo arreglo de auto-reduccion de escala YA probado en
			// TarjetaEdicionFlotanteTk/TarjetaLoQueVieneTk: medir con la fuente real y reducir la
			// escala si no cabe, con un suelo para que nunca quede ilegible.
			const float EscalaBaseFila = 0.62f;
			const float EscalaMinimaFila = 0.48f;
			// Ancho REAL disponible, no un numero adivinado: el padding real del popup son 6f por
			// lado (SetPadding(6f), 12f los dos juntos) y la lista reserva 20f para la barra de
			// scroll (_lista.Width.Set(-20f, 1f)) - un "24f" a ojo se quedaba corto (bug real: con
			// eso la reduccion de escala no bajaba lo suficiente, y como BotonTk CENTRA su texto
			// -no lo alinea a la izquierda-, el sobrante desbordaba por los DOS lados a la vez,
			// recortando el primer caracter de la fila con el cofre sintetico en la propia
			// captura).
			float anchoDisponibleTexto = anchoPopup - 12f - 20f - 6f - (objetoCoincide >= 0 ? AnchoBotonTraer + 4f : 0f);
			float escalaFila = EscalaBaseFila;
			// Bug real encontrado con la propia captura, incluso YA con el ancho real de arriba:
			// justo la fila MAS LARGA de las 16 visibles (la del cofre sintetico, con nombre propio
			// "Cofre de prueba WS3" delante - las demas son "Unnamed chest", mas cortas) seguia
			// perdiendo su primer caracter. Utils.DrawBorderString dibuja el "borde" del texto
			// desplazando la copia de sombra 1px en las cuatro direcciones ademas del texto
			// principal - MeasureString (que solo mide los glifos) se queda corto frente al ancho
			// REAL ocupado en pantalla por esa sombra, y el error crece con la longitud del texto.
			// Un 8% de margen de seguridad (en vez de un pixelaje fijo, que no escala con el
			// string) lo cubre para cualquier longitud.
			Vector2 medida = FontAssets.MouseText.Value.MeasureString(texto) * escalaFila * 1.08f;
			if (medida.X > anchoDisponibleTexto && anchoDisponibleTexto > 0f) {
				escalaFila = Math.Max(EscalaMinimaFila, escalaFila * (anchoDisponibleTexto / medida.X));
			}

			BotonTk fila = new BotonTk(texto, escalaFila);
			fila.Height.Set(0f, 1f);
			fila.Ayuda = () => Idiomas.Texto("Libreria.SelectorCofre.Ayuda", cofre.x, cofre.y, (int)distancia);
			fila.AlPulsar += () => {
				_alElegir?.Invoke(indice);
				Cerrar();
			};

			if (objetoCoincide < 0) {
				fila.Width.Set(0f, 1f);
				return fila;
			}

			// Hay un objeto real que coincide con la busqueda: fila mas estrecha + boton "traer" a
			// la derecha, que mueve ESE objeto concreto al inventario real del jugador SIN cerrar
			// el popup ni cambiar de destino - API publica real (Player.GetItem con el mismo
			// preset que usa el boton "Loot All" de vanilla sobre un cofre normal,
			// GetItemSettings.LootAllSettingsRegularChest), deshacible por el lado del cofre con
			// Historial.CambiarObjetos.
			UIElement contenedor = new UIElement();
			contenedor.Width.Set(0f, 1f);
			contenedor.Height.Set(22f, 0f);

			fila.Width.Set(-(AnchoBotonTraer + 4f), 1f);
			contenedor.Append(fila);

			// Bug real encontrado con la propia captura (ws3-traer-a-inventario.png): este boton
			// NUNCA paso por la auto-reduccion de escala de arriba (esa solo cubria el texto de la
			// FILA, no el suyo propio) - a escala fija 0.62f, "→ Inventory" (ingles) no cabia en
			// AnchoBotonTraer y salia cortado a "→ Inventor" en TODAS las filas con coincidencia a
			// la vez (confirmado NO era el popup entero saliendose de pantalla: geometria real
			// medida en vivo, GeometriaParaPrueba, dio X=659,68 W=300 contra una pantalla logica de
			// 1090 de ancho - de sobra). Mismo arreglo real: medir y reducir si hace falta.
			const float EscalaBaseTraer = 0.62f;
			const float EscalaMinimaTraer = 0.42f;
			string textoTraer = Idiomas.Texto("Libreria.SelectorCofre.Traer");
			float escalaTraer = EscalaBaseTraer;
			Vector2 medidaTraer = FontAssets.MouseText.Value.MeasureString(textoTraer) * escalaTraer;
			float anchoDisponibleTraer = AnchoBotonTraer - 6f;
			if (medidaTraer.X > anchoDisponibleTraer && anchoDisponibleTraer > 0f) {
				escalaTraer = Math.Max(EscalaMinimaTraer, escalaTraer * (anchoDisponibleTraer / medidaTraer.X));
			}

			BotonTk botonTraer = new BotonTk(textoTraer, escalaTraer);
			botonTraer.Width.Set(AnchoBotonTraer, 0f);
			botonTraer.Height.Set(0f, 1f);
			botonTraer.HAlign = 1f;
			int tipoATraer = objetoCoincide;
			botonTraer.Ayuda = () => Idiomas.Texto("Libreria.SelectorCofre.TraerAyuda",
				ContentSamples.ItemsByType.TryGetValue(tipoATraer, out Item muestra) && muestra != null ? muestra.Name : "?");
			botonTraer.AlPulsar += () => {
				TraerAlInventario(indice, tipoATraer);
			};
			contenedor.Append(botonTraer);

			return contenedor;
		}

		/// <summary>Mueve la PRIMERA ranura del cofre <paramref name="indiceCofre"/> cuyo tipo sea
		/// <paramref name="tipo"/> al inventario real del jugador. Nunca cierra el popup: el
		/// jugador puede seguir buscando/trayendo mas de un objeto sin reabrir nada.</summary>
		private void TraerAlInventario(int indiceCofre, int tipo)
		{
			Chest[] cofres = Main.chest;
			if (Main.netMode != NetmodeID.SinglePlayer || cofres == null ||
				indiceCofre < 0 || indiceCofre >= cofres.Length || cofres[indiceCofre] == null) {
				return;
			}

			Chest cofre = cofres[indiceCofre];
			Item[] ranuras = cofre.item;
			int slot = -1;
			for (int s = 0; s < ranuras.Length; s++) {
				if (ranuras[s] != null && !ranuras[s].IsAir && ranuras[s].type == tipo) {
					slot = s;
					break;
				}
			}
			if (slot < 0) {
				// Ya no esta (otro clic lo movio, o el cofre cambio entre fotogramas): no hay nada
				// que hacer, no truena.
				return;
			}

			Player jugador = Main.LocalPlayer;
			TerrakeepMod.Common.Undo.Historial.CambiarObjetos(
				Idiomas.Texto("Libreria.SelectorCofre.TraerDeshacer", ranuras[slot].Name),
				ranuras, new[] { slot },
				() => {
					ranuras[slot].position = jugador.Center;
					ranuras[slot] = jugador.GetItem(Main.myPlayer, ranuras[slot], GetItemSettings.LootAllSettingsRegularChest);
				});

			RegistroLibreria.Linea(Terrakeep.LogTag + " Libreria: idea 6, \"traer a mi inventario\" - " +
				"objeto type=" + tipo + " del cofre[" + indiceCofre + "][" + slot + "] movido al inventario real.");
		}

		private static string Acortar(string texto, int maximo)
		{
			if (string.IsNullOrEmpty(texto)) {
				return texto;
			}
			return texto.Length <= maximo ? texto : texto.Substring(0, maximo - 1) + "…";
		}

		/// <summary>
		/// Etiqueta de resumen/aviso (nunca una fila de cofre: esas son <see cref="BotonTk"/> con
		/// el nombre ya acortado a mano, ver <see cref="MaximoCaracteresNombre"/>). A diferencia de
		/// las filas, este texto puede ser una frase larga entera (viene de <c>Idiomas.Texto</c>,
		/// distinta longitud en cada idioma) - <see cref="EtiquetaTk"/> NO envuelve ni recorta sola
		/// (dibuja con <c>Utils.DrawBorderString</c> sin mas, visto en su propio codigo), asi que
		/// aqui se envuelve A MANO con <see cref="EtiquetaTk.PartirEnLineas"/> ANTES de crearla y se
		/// le da el alto real segun cuantas lineas haya hecho falta - la primera version de este
		/// selector dejaba el resumen cortado a media palabra ("...sorted by distanc"), visto en
		/// una captura real.
		/// </summary>
		private EtiquetaTk NuevaEtiqueta(string texto, Color color, float anchoPopup)
		{
			const float Escala = 0.68f;
			float anchoDisponible = anchoPopup - 24f;
			string envuelto = EtiquetaTk.PartirEnLineas(texto, anchoDisponible, Escala);
			int lineas = string.IsNullOrEmpty(envuelto) ? 1 : envuelto.Split('\n').Length;
			float alto = 16f * lineas + 4f;

			EtiquetaTk etiqueta = new EtiquetaTk(() => envuelto, Escala, anchoDisponible, alto);
			etiqueta.ColorTexto = color;
			return etiqueta;
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
