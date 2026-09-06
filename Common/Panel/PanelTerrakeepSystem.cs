using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Builds;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.Common.Investigacion;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>Las seis areas del panel unico, en el orden en el que salen en la barra de
	/// pestañas (el mismo que la aplicacion de escritorio hermana).</summary>
	public enum AreaTerrakeep
	{
		Personaje = 0,
		Libreria = 1,
		Builds = 2,
		Investigacion = 3,
		Exploracion = 4,
		Ajustes = 5
	}

	/// <summary>
	/// Abre, cierra y cambia de pestaña el <b>panel unico</b> de Terrakeep. Es el unico sitio del
	/// mod que llama a <c>IngameFancyUI</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Que fue de las seis teclas.</b> Se conservan las seis, con el mismo nombre de atajo que
	/// ya tenian (asi el <c>input profiles.json</c> del usuario sigue valiendo), pero ahora todas
	/// abren el MISMO panel: cada una lo abre <b>directamente en su pestaña</b>. Ademas:
	/// </para>
	/// <list type="bullet">
	/// <item>con el panel cerrado, la tecla lo abre en su area;</item>
	/// <item>con el panel abierto en OTRA area, la tecla salta a la suya sin cerrar nada;</item>
	/// <item>con el panel abierto en SU area, la tecla lo cierra.</item>
	/// </list>
	/// <para>
	/// Es lo que hace que las cinco teclas antiguas sigan siendo utiles en vez de quedarse muertas
	/// o de abrir paneles duplicados. La tecla principal sigue siendo la <b>K</b>
	/// (<c>Terrakeep.AbrirPanelKeybind</c>, atajo "AbrirPanel"), que abre en Personaje.
	/// </para>
	/// <para>
	/// El registro de cada <c>ModKeybind</c> se ha dejado donde estaba (cada <c>ModSystem</c> de
	/// area registra el suyo) a proposito: lo que se ha centralizado aqui es solo la LECTURA. Asi
	/// el <see cref="SembradorDeAtajos"/> de WS7 -que recorre las claves del perfil que empiezan
	/// por <c>TerrakeepMod/</c>- sigue funcionando exactamente igual, sin tocarlo.
	/// </para>
	/// </remarks>
	public class PanelTerrakeepSystem : ModSystem
	{
		private static PanelTerrakeepState _panel;
		private static uint _ultimoFotogramaAlternado;

		/// <summary>true si el panel de Terrakeep esta abierto ahora mismo.</summary>
		public static bool PanelAbierto =>
			Main.InGameUI != null && Main.InGameUI.CurrentState is PanelTerrakeepState;

		/// <summary>La instancia abierta ahora mismo, o null.</summary>
		public static PanelTerrakeepState Panel => PanelAbierto ? _panel : null;

		/// <summary>Area abierta ahora mismo (solo tiene sentido con el panel abierto).</summary>
		public static AreaTerrakeep AreaAbierta =>
			_panel != null ? _panel.AreaActual : AreaTerrakeep.Personaje;

		/// <summary>true si el panel esta abierto Y ademas en el area indicada. Es lo que usan los
		/// sistemas de cada area para saber si "su" panel esta a la vista.</summary>
		public static bool AreaAbiertaEs(AreaTerrakeep area)
		{
			return PanelAbierto && _panel != null && _panel.AreaActual == area;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// La autoprueba va PRIMERO y sin condiciones, por lo que ya documento WS0: una
			// excepcion leyendo un atajo abortaria el resto del metodo y la autoprueba no llegaria
			// a dispararse nunca.
			AutopruebaPanelUnico.Avanzar();
			AutopruebaIdiomas.Avanzar();

			ComprobarAtajos();

			if (PanelAbierto) {
				// Se reafirma en cada fotograma: si algo del juego lo volviera a poner a false,
				// Player.dropItemCheck vaciaria el objeto que se lleva cogido con el raton.
				PanelTerrakeepState.MantenerInventarioAbierto();
			}
		}

		public override void Unload()
		{
			_panel = null;
		}

		// -------------------------------------------------------------------------------------
		// Atajos
		// -------------------------------------------------------------------------------------

		/// <summary>
		/// El <c>ModKeybind</c> de cada area, con su nombre de atajo real.
		/// <para />
		/// Se resuelven en cada consulta y no se cachean: los <c>ModSystem</c> de cada area
		/// registran el suyo en su propio <c>Load()</c>/<c>OnModLoad()</c> y el orden entre
		/// sistemas no esta garantizado.
		/// </summary>
		public static ModKeybind AtajoDe(AreaTerrakeep area)
		{
			switch (area) {
				case AreaTerrakeep.Personaje: return Terrakeep.AbrirPanelKeybind;
				case AreaTerrakeep.Libreria: return PanelLibreriaSystem.Atajo;
				case AreaTerrakeep.Builds: return PanelBuildsSystem.Atajo;
				case AreaTerrakeep.Investigacion: return PanelInvestigacionSystem.Atajo;
				case AreaTerrakeep.Exploracion: return PanelExploracionSystem.Atajo;
				case AreaTerrakeep.Ajustes: return AjustesSystem.AbrirAjustesKeybind;
				default: return null;
			}
		}

		/// <summary>
		/// Nombre interno del atajo de cada area. Son los nombres HISTORICOS con los que cada
		/// workstream registro el suyo, y no se cambian a proposito: el perfil de controles del
		/// usuario (<c>input profiles.json</c>) los guarda por nombre, asi que renombrarlos le
		/// borraria las teclas que tuviera puestas.
		/// </summary>
		public static string NombreDelAtajo(AreaTerrakeep area)
		{
			switch (area) {
				case AreaTerrakeep.Personaje: return "AbrirPanel";
				case AreaTerrakeep.Libreria: return "AbrirLibreria";
				case AreaTerrakeep.Builds: return "AbrirBuilds";
				case AreaTerrakeep.Investigacion: return "AbrirInvestigacion";
				case AreaTerrakeep.Exploracion: return "AbrirExploracion";
				case AreaTerrakeep.Ajustes: return "AbrirAjustes";
				default: return null;
			}
		}

		/// <summary>
		/// Clave con la que <c>PlayerInput</c> indexa el atajo de un area. Es la misma que usa
		/// <c>ModKeybind.JustPressed</c> por dentro
		/// (<c>PlayerInput.Triggers.JustPressed.KeyStatus[FullName]</c>), y se construye a mano
		/// porque <c>ModKeybind.FullName</c> <b>no es accesible desde un mod</b> en esta version
		/// (comprobado: el compilador de tModLoader lo rechaza con CS1061).
		/// </summary>
		public static string ClaveDeAtajo(AreaTerrakeep area)
		{
			string nombre = NombreDelAtajo(area);
			return nombre == null ? null : "TerrakeepMod/" + nombre;
		}

		/// <summary>
		/// Tecla que tiene asignada AHORA MISMO el atajo de un area, tal como la ve el juego. Se
		/// lee del propio <c>ModKeybind</c>, no de una constante: si el usuario la reasigna en
		/// Ajustes &gt; Controles, el boton "Cerrar (X)" y el tooltip de la pestaña cambian solos.
		/// </summary>
		public static string TeclaDe(AreaTerrakeep area)
		{
			ModKeybind atajo = AtajoDe(area);
			if (atajo == null) {
				return "?";
			}

			List<string> teclas;
			try {
				teclas = atajo.GetAssignedKeys();
			}
			catch (KeyNotFoundException) {
				// El MISMO hueco que ya documento WS0 para ModKeybind.JustPressed, y aqui muerde
				// mucho mas fuerte: GetAssignedKeys indexa por dentro el diccionario del perfil de
				// controles, que no conoce los atajos de mods hasta que PlayerInput procesa su
				// reinitialize pendiente, y eso tarda varios segundos al entrar en una partida.
				//
				// Sin este catch la excepcion sube por PanelTerrakeepState.RefrescarTextos ->
				// CambiarArea -> UIElement.Activate y ABORTA LA CONSTRUCCION ENTERA DEL PANEL:
				// abrirlo en los primeros segundos de un mundo lo dejaba a medias, sin contenido.
				// Se vio en el juego real con la autoprueba de idiomas, no leyendo codigo.
				return "sin tecla";
			}

			return teclas == null || teclas.Count == 0 ? "sin tecla" : string.Join("+", teclas);
		}

		/// <summary>
		/// Lee las seis teclas del mod. Va envuelto en try/catch por lo que ya documento WS0:
		/// <c>ModKeybind.JustPressed</c> indexa <c>PlayerInput.Triggers.JustPressed.KeyStatus</c>
		/// por el nombre completo del atajo, y ese diccionario tarda unos fotogramas en conocer los
		/// atajos de mods (<c>PlayerInput.reinitialize</c>), asi que puede lanzar
		/// <c>KeyNotFoundException</c> al principio de una partida. Se autocorrige solo.
		/// </summary>
		public static void ComprobarAtajos()
		{
			// Con el chat abierto las teclas son texto, no atajos.
			if (Main.drawingPlayerChat) {
				return;
			}

			for (int i = 0; i < PanelTerrakeepState.NombresDeArea.Length; i++) {
				AreaTerrakeep area = (AreaTerrakeep)i;
				ModKeybind atajo = AtajoDe(area);
				if (atajo == null) {
					continue;
				}

				try {
					if (atajo.JustPressed) {
						AlternarArea(area, "atajo de teclado (tecla " + TeclaDe(area) + ")");
					}
				}
				catch (KeyNotFoundException) {
					// Hueco conocido entre que el mod carga y que PlayerInput procesa su
					// reinitialize pendiente. Se autocorrige al fotograma siguiente.
				}
			}
		}

		// -------------------------------------------------------------------------------------
		// Abrir / cerrar / cambiar de pestaña
		// -------------------------------------------------------------------------------------

		/// <summary>
		/// Comportamiento de una tecla de area: abre en esa pestaña, salta a ella si el panel ya
		/// estaba abierto en otra, o cierra si ya estabas en ella.
		/// </summary>
		public static void AlternarArea(AreaTerrakeep area, string origen)
		{
			// Guarda contra el doble disparo: el atajo principal se consulta desde dos sitios
			// (este UpdateUI y ModPlayer.ProcessTriggers, la via recomendada por tModLoader). Sin
			// esto, una sola pulsacion abriria y cerraria el panel en el mismo fotograma.
			if (_ultimoFotogramaAlternado == Main.GameUpdateCount) {
				return;
			}
			_ultimoFotogramaAlternado = Main.GameUpdateCount;

			if (!PanelAbierto) {
				AbrirEnArea(area, origen);
				return;
			}

			if (_panel != null && _panel.AreaActual == area) {
				CerrarPanel(origen);
				return;
			}

			IrAArea(area, origen);
		}

		/// <summary>Abre el panel (o salta de pestaña si ya estaba abierto) en el area indicada.</summary>
		public static void AbrirEnArea(AreaTerrakeep area, string origen)
		{
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}

			if (PanelAbierto) {
				IrAArea(area, origen);
				return;
			}

			// Con el mapa vanilla abierto no se dibuja ninguna interfaz de mod (Main.DoDraw hace
			// return antes de la interfaz si mapFullscreen), asi que abrir el panel ahi seria
			// abrirlo a ciegas: se cierra el mapa primero. Hallazgo real de WS6.
			if (Main.mapFullscreen) {
				Main.mapFullscreen = false;
			}

			// Se construye un UIState nuevo cada vez a proposito: OnInitialize solo se ejecuta una
			// vez por instancia, y lo que se monta dentro captura los arrays reales del jugador,
			// que el juego reasigna al cargar otro personaje.
			PanelTerrakeepState.UltimaArea = area;
			_panel = new PanelTerrakeepState();

			// Mismo mecanismo que usan el bestiario, el menu de emotes y los menus de ajustes del
			// propio juego: oculta el resto de la interfaz y toma el control.
			IngameFancyUI.OpenUIState(_panel);
			PanelTerrakeepState.MantenerInventarioAbierto();

			// RegistrarEnArea ya escribe en el log del juego en todos los casos; llamar ademas al
			// Logger dejaria la linea repetida en el client.log de las areas con archivo propio.
			RegistrarEnArea(area,
				$"{Terrakeep.LogTag} PANEL ABIERTO via {origen}. Pestaña: \"{NombreDe(area)}\". " +
				$"Jugador: \"{Main.LocalPlayer.name}\". Mundo: \"{Main.worldName}\". " +
				$"Main.inFancyUI={Main.inFancyUI}, " +
				$"InGameUI.CurrentState={Main.InGameUI.CurrentState?.GetType().FullName}");
		}

		/// <summary>Cambia de pestaña con el panel ya abierto.</summary>
		public static void IrAArea(AreaTerrakeep area, string origen)
		{
			if (!PanelAbierto || _panel == null || _panel.AreaActual == area) {
				return;
			}
			_panel.CambiarArea(area, origen);
		}

		public static void CerrarPanel(string origen)
		{
			if (!PanelAbierto) {
				return;
			}

			// Fuera del panel nadie dibuja el objeto que se lleva "cogido" con el raton, asi que se
			// devuelve al inventario antes de cerrar.
			PanelTerrakeepState.DevolverObjetoDelRaton();

			AreaTerrakeep area = _panel != null ? _panel.AreaActual : AreaTerrakeep.Personaje;
			RegistrarEnArea(area, $"{Terrakeep.LogTag} PANEL CERRADO via {origen}. " +
				$"Pestaña al cerrar: \"{NombreDe(area)}\".");

			IngameFancyUI.Close();
			_panel = null;
		}

		private static string NombreDe(AreaTerrakeep area)
		{
			int i = (int)area;
			return i >= 0 && i < PanelTerrakeepState.NombresDeArea.Length
				? PanelTerrakeepState.NombresDeArea[i]
				: "?";
		}

		/// <summary>
		/// Manda una linea al registro de evidencia del area indicada.
		/// <para />
		/// Cada workstream monto su propio archivo de evidencia dentro de la carpeta de guardado de
		/// la prueba (la solucion de WS4 al <c>client.log</c> compartido). Al fusionar los paneles,
		/// las lineas de abrir/cerrar/cambiar de pestaña las genera un solo sitio, asi que hay que
		/// repartirlas al registro que corresponda para que las autopruebas de cada area sigan
		/// encontrando su evidencia donde la esperan.
		/// </summary>
		public static void RegistrarEnArea(AreaTerrakeep area, string mensaje)
		{
			switch (area) {
				case AreaTerrakeep.Libreria:
					RegistroLibreria.Linea(mensaje);
					break;
				case AreaTerrakeep.Builds:
					RegistroBuilds.Linea(mensaje);
					break;
				case AreaTerrakeep.Investigacion:
					RegistroInvestigacion.Linea(mensaje);
					break;
				case AreaTerrakeep.Exploracion:
					RegistroExploracion.Linea(mensaje);
					break;
				default:
					// Personaje y Ajustes no tienen archivo propio: su evidencia es el client.log.
					if (Terrakeep.Instance != null) {
						Terrakeep.Instance.Logger.Info(mensaje);
					}
					break;
			}
		}
	}
}
