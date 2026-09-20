using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
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
		private const float AnchoPopup = 320f;
		private const float AltoPopup = 260f;

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
		private CapaSuperposicionTk _capa;
		private bool _abierto;

		public SelectorCofreMundoTk(Action<int> alElegir)
		{
			_alElegir = alElegir;
			Width.Set(0f, 0f);
			Height.Set(0f, 0f);
		}

		/// <summary>true si el popup de eleccion esta abierto ahora mismo. Lo lee la autoprueba.</summary>
		public bool Abierto => _abierto;

		/// <summary>Numero de filas de cofre REALMENTE dibujadas la ultima vez que se abrio (sin
		/// contar el resumen/aviso). Lo lee la autoprueba.</summary>
		public int FilasDibujadas { get; private set; }

		/// <summary>Las filas de cofre (nunca los rotulos de resumen/aviso, que son
		/// <see cref="EtiquetaTk"/>, no <see cref="BotonTk"/>) del popup ABIERTO ahora mismo, en el
		/// mismo orden en que estan en la lista - para que la autoprueba pueda pulsar una por su
		/// ruta REAL (<c>BotonTk.LeftClick</c>), igual que ya hace <c>EditorPrefijoTk.BotonesPopupParaAutoprueba</c>.</summary>
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

			RellenarFilas(anchoReal);

			if (_capa != null) {
				_capa.Mostrar(_popup, Cerrar);
			}
			else {
				Append(_popup);
			}
			_popup.Recalculate();
		}

		private void RellenarFilas(float anchoPopup)
		{
			FilasDibujadas = 0;

			if (Main.netMode != NetmodeID.SinglePlayer) {
				_lista.Add(NuevaEtiqueta(Idiomas.Texto("Libreria.SelectorCofre.SoloUnJugador"), EstiloTk.TextoAviso, anchoPopup));
				return;
			}

			Chest[] cofres = Main.chest;
			Player jugador = Main.LocalPlayer;
			List<(int indice, float distancia)> encontrados = new List<(int, float)>();
			if (cofres != null) {
				for (int i = 0; i < cofres.Length; i++) {
					Chest cofre = cofres[i];
					if (cofre == null) {
						continue;
					}
					float distancia = jugador != null
						? Vector2.Distance(jugador.Center, new Vector2(cofre.x * 16f + 16f, cofre.y * 16f + 16f)) / 16f
						: 0f;
					encontrados.Add((i, distancia));
				}
			}

			// Orden por distancia (el mas cercano primero): es el orden que de verdad importa para
			// esta lista - "el cofre en el que estoy parado ahora mismo" arriba del todo.
			encontrados.Sort((a, b) => a.distancia.CompareTo(b.distancia));

			_lista.Add(NuevaEtiqueta(Idiomas.Texto("Libreria.SelectorCofre.Resumen", encontrados.Count), EstiloTk.TextoSuave, anchoPopup));

			if (encontrados.Count == 0) {
				_lista.Add(NuevaEtiqueta(Idiomas.Texto("Libreria.SelectorCofre.Ninguno"), EstiloTk.TextoSuave, anchoPopup));
				return;
			}

			int mostrados = Math.Min(encontrados.Count, LimiteFilas);
			for (int k = 0; k < mostrados; k++) {
				int indice = encontrados[k].indice;
				float distancia = encontrados[k].distancia;
				_lista.Add(CrearFilaCofre(indice, cofres[indice], distancia, anchoPopup));
			}
			FilasDibujadas = mostrados;

			if (encontrados.Count > mostrados) {
				_lista.Add(NuevaEtiqueta(Idiomas.Texto("Libreria.SelectorCofre.MasCofres", encontrados.Count - mostrados), EstiloTk.TextoSuave, anchoPopup));
			}
		}

		private BotonTk CrearFilaCofre(int indice, Chest cofre, float distancia, float anchoPopup)
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

			BotonTk fila = new BotonTk(texto, 0.62f);
			fila.Width.Set(0f, 1f);
			fila.Height.Set(22f, 0f);
			fila.Ayuda = () => Idiomas.Texto("Libreria.SelectorCofre.Ayuda", cofre.x, cofre.y, (int)distancia);
			fila.AlPulsar += () => {
				_alElegir?.Invoke(indice);
				Cerrar();
			};
			return fila;
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
