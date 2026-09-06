using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Panel;

namespace TerrakeepMod.Common.Menus
{
	/// <summary>
	/// SOLO ARNES DE PRUEBAS: recorre lo que se ve de Terrakeep <b>ANTES de entrar en ninguna
	/// partida</b>, o sea en los menus del propio tModLoader: la lista de <b>Mods</b> (icono,
	/// nombre y version), la ficha de <b>Mas informacion</b> (donde se lee <c>description.txt</c>)
	/// y la pantalla de <b>Configuracion de Mods</b> que tModLoader genera sola a partir del
	/// <c>ModConfig</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que hacia falta.</b> Todas las autopruebas del mod hasta ahora entran directas al
	/// mundo con <c>-skipselect</c>, asi que nadie habia mirado nunca lo primero que ve quien
	/// instala el mod. Aqui no hay panel propio: lo que se comprueba es interfaz DE tModLoader
	/// rellenada con datos nuestros.
	/// </para>
	/// <para>
	/// <b>Como se navega.</b> Los botones del menu principal de Terraria no son <c>UIElement</c>
	/// (los pinta <c>Main.DrawMenu</c> a mano), asi que para llegar a la lista de Mods se hace lo
	/// MISMO que hace el boton del juego: <c>Main.menuMode = 10000</c>
	/// (<c>Interface.modsMenuID</c>, leido en el <c>tModLoader.dll</c> instalado; ahi
	/// <c>Interface.ModLoaderMenus</c> hace <c>Main.MenuUI.SetState(modsMenu)</c>). A partir de
	/// ahi ya SI hay <c>UIElement</c> de verdad, asi que los dos botones de la ficha del mod se
	/// pulsan con <c>UIElement.LeftClick</c>, que es el mismo criterio de produccion que usa el
	/// resto del proyecto.
	/// </para>
	/// <para>
	/// <b>Reflexion.</b> <c>UIModItem</c> y <c>UIModConfig</c> son <c>internal</c> de tModLoader,
	/// asi que para LOCALIZAR el elemento de Terrakeep y sus dos botones se usa reflexion. Lo que
	/// se acciona despues es el boton de verdad por su ruta de clic real; la reflexion solo sirve
	/// para encontrarlo, igual que un dedo encuentra el boton en la pantalla.
	/// </para>
	/// </remarks>
	public static class AutopruebaMenus
	{
		/// <summary>Variable de entorno que la activa.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_MENUS";

		/// <summary>Archivo de evidencia propio, dentro de la carpeta de guardado de la prueba.</summary>
		public const string NombreArchivo = "terrakeep-menus-evidencia.log";

		/// <summary>Ids reales de los menus de tModLoader (constantes de <c>Interface</c>, que es
		/// <c>internal</c>: se copian aqui con su nombre para que se sepa de donde salen).</summary>
		private const int MenuMods = 10000;
		private const int MenuListaDeConfigs = 10027;
		private const int MenuConfig = 10024;
		private const int MenuInfoDelMod = 10008;

		private const int FotogramasEntrePasos = 30;
		private const int FotogramasDeEspera = 240;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMenu;
		private static int _paso;
		private static int _espera;
		private static bool _archivoIniciado;

		/// <summary>Descripcion leida en el paso 2. Hay que guardarla: en el paso 4 la pantalla ya
		/// es <c>UIModInfo</c> y la ficha del mod ya no esta en el arbol de la interfaz.</summary>
		private static string _descripcion;

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
			}

			if (!_activa || _terminada) {
				return;
			}

			// Al reves que las demas autopruebas: esta solo corre MIENTRAS se esta en los menus.
			if (!Main.gameMenu) {
				return;
			}

			_fotogramasEnMenu++;
			if (_fotogramasEnMenu < FotogramasDeEspera) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			try {
				Paso(_paso++);
			}
			catch (Exception e) {
				Linea("AUTOPRUEBA MENUS: EXCEPCION en el paso " + (_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: Arrancar(); break;
				case 1: AbrirListaDeMods(); break;
				case 2: MirarFichaDelMod(); break;
				case 3: PulsarMasInformacion(); break;
				case 4: MirarDescripcion(); break;
				case 5: VolverAListaDeMods(); break;
				case 6: PulsarConfiguracion(); break;
				case 7: MirarListaDeConfigs(); break;
				case 8: AbrirConfigDeTerrakeep(); break;
				case 9: MirarPantallaDeConfig(); break;
				default: Terminar(); break;
			}
		}

		// -------------------------------------------------------------------------------------

		private static void Arrancar()
		{
			Linea("AUTOPRUEBA MENUS: " + Variable + " detectada. Main.menuMode=" + Main.menuMode +
				", mods cargados=" + ModLoader.Mods.Length + ", resolucion " +
				Main.screenWidth + "x" + Main.screenHeight + ", idioma del juego=" +
				(Terraria.Localization.Language.ActiveCulture != null
					? Terraria.Localization.Language.ActiveCulture.Name : "?") + ".");
		}

		/// <summary>Lo mismo que hace el boton "Administrar mods" del menu Taller del juego.</summary>
		private static void AbrirListaDeMods()
		{
			Main.menuMode = MenuMods;
			Linea("MENU/1 - pedido Main.menuMode=" + MenuMods + " (Interface.modsMenuID), que es lo " +
				"que hace el boton \"Administrar mods\" del menu Taller.");
		}

		/// <summary>
		/// La ficha de Terrakeep dentro de la lista: icono, rotulo y que botones tiene.
		/// </summary>
		private static void MirarFichaDelMod()
		{
			Linea("MENU/2 - estado del menu: menuMode=" + Main.menuMode + ", MenuUI.CurrentState=" +
				NombreDelEstado());

			UIElement ficha = BuscarFichaDeTerrakeep();
			if (ficha == null) {
				Linea("MENU/2 - NO se encontro la ficha de TerrakeepMod en la lista.");
				Linea("MENU/2 - " + CapturaDePantalla.Guardar("menu-mods"));
				return;
			}

			CalculatedStyle dim = ficha.GetDimensions();
			Linea("MENU/2 - ficha encontrada: " + ficha.GetType().FullName +
				" en x=" + (int)dim.X + " y=" + (int)dim.Y + " " +
				(int)dim.Width + "x" + (int)dim.Height + ". Rotulo: \"" + TextoDe(ficha, "_modName") + "\".");

			Linea("MENU/2 - ICONO: " + DescribirIcono(ficha));

			// Se lee AQUI y no en el paso 4: alli la pantalla ya es UIModInfo y esta ficha ya no
			// existe en el arbol de la interfaz (la primera version lo intentaba alli y decia "no
			// se pudo leer la descripcion").
			object local = Campo(ficha, "_mod");
			object propiedades = local != null ? Campo(local, "properties") : null;
			_descripcion = propiedades != null ? Campo(propiedades, "description") as string : null;
			Linea("MENU/2 - botones de la ficha: mas informacion=" +
				(Campo(ficha, "_moreInfoButton") != null) + ", configuracion=" +
				(Campo(ficha, "_configButton") != null) + ".");
			Linea("MENU/2 - " + CapturaDePantalla.Guardar("menu-mods"));
		}

		/// <summary>
		/// Que textura lleva de verdad el icono de la ficha. Es lo que decide si el mod sale con su
		/// arte propio o con el icono generico de tModLoader: <c>UIModItem.OnInitialize</c> hace
		/// <c>ModLoader.GetModIcon(mod.modFile) ?? Mod.PlaceholderModIcon</c>, asi que basta con
		/// mirar el nombre del asset que acabo dentro.
		/// </summary>
		private static string DescribirIcono(UIElement ficha)
		{
			object icono = Campo(ficha, "_modIcon");
			if (icono == null) {
				return "la ficha no tiene _modIcon";
			}

			object asset = Campo(icono, "_texture") ?? Propiedad(icono, "Texture");
			if (asset == null) {
				return "no se pudo leer la textura del icono (" + icono.GetType().FullName + ")";
			}

			string nombre = Propiedad(asset, "Name") as string;
			object valor = Propiedad(asset, "Value");
			object ancho = valor != null ? Propiedad(valor, "Width") : null;
			object alto = valor != null ? Propiedad(valor, "Height") : null;

			// La comparacion buena no es por nombre sino por REFERENCIA contra el propio
			// Mod.PlaceholderModIcon, que es literalmente lo que pone UIModItem.OnInitialize cuando
			// ModLoader.GetModIcon devuelve null: "GetModIcon(_mod.modFile) ?? Mod.PlaceholderModIcon".
			object placeholder = typeof(Mod).GetField("PlaceholderModIcon", BindingFlags.Static | Todos) != null
				? typeof(Mod).GetField("PlaceholderModIcon", BindingFlags.Static | Todos).GetValue(null)
				: null;
			bool esElPlaceholder = placeholder != null && ReferenceEquals(placeholder, asset);

			return "asset \"" + nombre + "\", " + ancho + "x" + alto + " px. " +
				(esElPlaceholder
					? "MAL: es Mod.PlaceholderModIcon, o sea que tModLoader NO encontro icon.png."
					: "OK: NO es Mod.PlaceholderModIcon, o sea que es el icon.png del propio mod." +
					  (placeholder == null ? " (no se pudo leer el placeholder para comparar)" : ""));
		}

		private static void PulsarMasInformacion()
		{
			UIElement ficha = BuscarFichaDeTerrakeep();
			UIElement boton = ficha != null ? Campo(ficha, "_moreInfoButton") as UIElement : null;
			if (boton == null) {
				Linea("MENU/3 - no hay boton de mas informacion.");
				return;
			}

			CalculatedStyle dim = boton.GetDimensions();
			boton.LeftClick(new UIMouseEvent(boton, dim.Center()));
			Linea("MENU/3 - CLIC REAL en el boton de mas informacion (x=" + (int)dim.X + " y=" +
				(int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height + ").");
		}

		/// <summary>
		/// La ficha de "Mas informacion" es donde se lee <c>description.txt</c>
		/// (<c>UIModItem.ShowMoreInfo</c> le pasa <c>_mod.properties.description</c> a
		/// <c>Interface.modInfo.Show</c>). Se comprueba que lo que hay dentro es la descripcion de
		/// verdad y no la plantilla.
		/// </summary>
		private static void MirarDescripcion()
		{
			Linea("MENU/4 - estado del menu: menuMode=" + Main.menuMode + " (esperado " +
				MenuInfoDelMod + "), MenuUI.CurrentState=" + NombreDelEstado());

			string descripcion = _descripcion;
			if (descripcion == null) {
				Linea("MENU/4 - no se pudo leer la descripcion del mod.");
			}
			else {
				string[] lineas = descripcion.Replace("\r", "").Split('\n');
				Linea("MENU/4 - description.txt: " + descripcion.Length + " caracteres, " +
					lineas.Length + " lineas. Primera: \"" + lineas[0] + "\".");
				Linea("MENU/4 - ¿sigue siendo la plantilla de WS0? " +
					(descripcion.IndexOf("panel de prueba", StringComparison.OrdinalIgnoreCase) >= 0 ||
					 descripcion.IndexOf("{ModVersion}", StringComparison.Ordinal) >= 0
						? "SI, hay que cambiarla."
						: "NO: es texto real del mod."));
			}

			Linea("MENU/4 - " + CapturaDePantalla.Guardar("menu-info"));
		}

		private static void VolverAListaDeMods()
		{
			Main.menuMode = MenuMods;
			Linea("MENU/5 - de vuelta a la lista de mods.");
		}

		private static void PulsarConfiguracion()
		{
			UIElement ficha = BuscarFichaDeTerrakeep();
			UIElement boton = ficha != null ? Campo(ficha, "_configButton") as UIElement : null;
			if (boton == null) {
				Linea("MENU/6 - la ficha NO tiene boton de configuracion (UIModItem solo lo pone si " +
					"ConfigManager.Configs conoce al mod).");
				return;
			}

			CalculatedStyle dim = boton.GetDimensions();
			boton.LeftClick(new UIMouseEvent(boton, dim.Center()));
			Linea("MENU/6 - CLIC REAL en el boton de configuracion (x=" + (int)dim.X + " y=" +
				(int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height + ").");
		}

		private static void MirarListaDeConfigs()
		{
			Linea("MENU/7 - estado del menu: menuMode=" + Main.menuMode + " (esperado " +
				MenuListaDeConfigs + "), MenuUI.CurrentState=" + NombreDelEstado());
			Linea("MENU/7 - " + CapturaDePantalla.Guardar("menu-lista-configs"));
		}

		/// <summary>
		/// Abre la configuracion de Terrakeep pulsando su boton en la lista. Los botones de esa
		/// lista son <c>UIButton&lt;LocalizedText&gt;</c>, asi que se busca el que lleve el nombre
		/// que declara el propio <c>ModConfig</c>.
		/// </summary>
		private static void AbrirConfigDeTerrakeep()
		{
			UIElement encontrado = null;
			string buscado = TextoDelConfig();

			Recorrer(Main.MenuUI.CurrentState, elemento => {
				if (encontrado != null || !elemento.GetType().Name.StartsWith("UIButton")) {
					return;
				}
				object texto = Propiedad(elemento, "Text");
				string legible = texto == null ? null : (Propiedad(texto, "Value") as string ?? texto.ToString());
				if (legible != null && legible.IndexOf(buscado, StringComparison.OrdinalIgnoreCase) >= 0) {
					encontrado = elemento;
				}
			});

			if (encontrado == null) {
				Linea("MENU/8 - no se encontro el boton \"" + buscado + "\" en la lista de configs.");
				return;
			}

			CalculatedStyle dim = encontrado.GetDimensions();
			encontrado.LeftClick(new UIMouseEvent(encontrado, dim.Center()));
			Linea("MENU/8 - CLIC REAL en el boton \"" + buscado + "\" (x=" + (int)dim.X + " y=" +
				(int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height + ").");
		}

		/// <summary>
		/// La pantalla que genera tModLoader a partir del <c>ModConfig</c>: que campos ha puesto y
		/// con que rotulos. Es donde se comprueba que <c>AtajosYaSembrados</c> NO sale.
		/// </summary>
		private static void MirarPantallaDeConfig()
		{
			Linea("MENU/9 - estado del menu: menuMode=" + Main.menuMode + " (esperado " +
				MenuConfig + "), MenuUI.CurrentState=" + NombreDelEstado());

			List<string> campos = new List<string>();
			List<string> rotulos = new List<string>();

			Recorrer(Main.MenuUI.CurrentState, elemento => {
				if (!EsConfigElement(elemento)) {
					return;
				}

				object miembro = Propiedad(elemento, "MemberInfo");
				string nombre = miembro != null ? Propiedad(miembro, "Name") as string : null;
				if (nombre != null) {
					campos.Add(nombre);
				}

				object funcion = Propiedad(elemento, "TextDisplayFunction") as Func<string>;
				if (funcion is Func<string>) {
					rotulos.Add(((Func<string>)funcion)());
				}
			});

			Linea("MENU/9 - campos que ha generado tModLoader: " +
				(campos.Count == 0 ? "(ninguno)" : string.Join(", ", campos)));
			Linea("MENU/9 - rotulos que se estan enseñando: " +
				(rotulos.Count == 0 ? "(ninguno)" : "\"" + string.Join("\" | \"", rotulos) + "\""));

			bool saleElInterno = campos.Contains("AtajosYaSembrados");
			Linea("MENU/9 - ¿sale el campo interno AtajosYaSembrados? " +
				(saleElInterno
					? "SI -> MAL: tendria que estar oculto con [JsonIgnore]."
					: "NO -> OK: oculto con [JsonIgnore], y sigue guardandose por el campo privado " +
					  "con [JsonProperty]."));

			bool saleElIdioma = campos.Contains("Idioma");
			Linea("MENU/9 - ¿sale el selector de idioma? " +
				(saleElIdioma ? "SI -> OK." : "NO -> MAL, es el unico ajuste real del mod."));

			Linea("MENU/9 - " + CapturaDePantalla.Guardar("menu-config"));
		}

		private static void Terminar()
		{
			_terminada = true;
			Linea("AUTOPRUEBA MENUS COMPLETA.");
		}

		// -------------------------------------------------------------------------------------
		// Utilidades
		// -------------------------------------------------------------------------------------

		private static string NombreDelEstado()
		{
			return Main.MenuUI != null && Main.MenuUI.CurrentState != null
				? Main.MenuUI.CurrentState.GetType().FullName
				: "(null)";
		}

		/// <summary>Texto que declara el propio <c>ModConfig</c> para su boton.</summary>
		private static string TextoDelConfig()
		{
			return Terraria.Localization.Language.GetTextValue(
				"Mods.TerrakeepMod.Configs.AjustesConfig.DisplayName");
		}

		private static bool EsConfigElement(UIElement elemento)
		{
			Type tipo = elemento.GetType();
			while (tipo != null) {
				if (tipo.Name == "ConfigElement") {
					return true;
				}
				tipo = tipo.BaseType;
			}
			return false;
		}

		/// <summary>
		/// La ficha (<c>UIModItem</c>) de TerrakeepMod dentro de la lista. Se identifica por su
		/// propiedad <c>ModName</c>, que es el nombre INTERNO del mod, no por el rotulo que se ve
		/// (que cambia con el idioma y lleva la version pegada).
		/// </summary>
		private static UIElement BuscarFichaDeTerrakeep()
		{
			UIElement encontrada = null;
			Recorrer(Main.MenuUI != null ? Main.MenuUI.CurrentState : null, elemento => {
				if (encontrada != null || elemento.GetType().Name != "UIModItem") {
					return;
				}
				string nombre = Propiedad(elemento, "ModName") as string;
				if (nombre == "TerrakeepMod") {
					encontrada = elemento;
				}
			});
			return encontrada;
		}

		/// <summary>Recorre el arbol de la interfaz. No se usa <c>ExecuteRecursively</c> porque hay
		/// que empezar en un <c>UIState</c> que puede ser null.</summary>
		private static void Recorrer(UIElement raiz, Action<UIElement> accion)
		{
			if (raiz == null) {
				return;
			}
			accion(raiz);
			foreach (UIElement hijo in raiz.Children) {
				Recorrer(hijo, accion);
			}
		}

		private static string TextoDe(object duenio, string campo)
		{
			object elemento = Campo(duenio, campo);
			object texto = elemento != null ? Propiedad(elemento, "Text") : null;
			return texto == null ? "(?)" : texto.ToString();
		}

		private const BindingFlags Todos =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private static object Campo(object duenio, string nombre)
		{
			if (duenio == null) {
				return null;
			}
			Type tipo = duenio.GetType();
			while (tipo != null) {
				FieldInfo campo = tipo.GetField(nombre, Todos);
				if (campo != null) {
					return campo.GetValue(duenio);
				}
				tipo = tipo.BaseType;
			}
			return null;
		}

		private static object Propiedad(object duenio, string nombre)
		{
			if (duenio == null) {
				return null;
			}
			Type tipo = duenio.GetType();
			while (tipo != null) {
				PropertyInfo propiedad = tipo.GetProperty(nombre, Todos);
				if (propiedad != null && propiedad.CanRead) {
					return propiedad.GetValue(duenio);
				}
				tipo = tipo.BaseType;
			}
			return Campo(duenio, nombre);
		}

		private static void Linea(string mensaje)
		{
			string linea = Terrakeep.LogTag + " " + mensaje;
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(linea);
			}

			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta, "# Evidencia de los menus de tModLoader - " +
						DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
				}
				File.AppendAllText(ruta, "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " +
					linea + Environment.NewLine);
			}
			catch (Exception) {
				// La evidencia del log del juego ya esta escrita.
			}
		}
	}
}
