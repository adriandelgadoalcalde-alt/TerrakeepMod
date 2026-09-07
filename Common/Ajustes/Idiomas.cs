using System;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace TerrakeepMod.Common.Ajustes
{
	/// <summary>
	/// Todo lo relacionado con el idioma de Terrakeep: como se resuelve un texto, como se cambia
	/// de idioma en vivo y como se recuerda entre partidas.
	///
	/// <para />
	/// <b>1. De donde salen los textos.</b> Del mecanismo oficial de tModLoader, no de un sistema
	/// propio: un archivo <c>.hjson</c> por idioma en <c>Localization\</c>
	/// (<c>es-ES_Mods.TerrakeepMod.hjson</c>, <c>en-US_Mods.TerrakeepMod.hjson</c>) y
	/// <c>Language.GetTextValue("Mods.TerrakeepMod.&lt;clave&gt;")</c> para leerlos. El motor los
	/// carga solo: <c>LocalizationLoader.Autoload</c> registra en el arranque todas las claves del
	/// idioma por defecto, y <c>LocalizationLoader.LoadModTranslations(cultura)</c> vuelca encima
	/// las del idioma activo cada vez que se cambia (metodos reales del <c>tModLoader.dll</c>
	/// instalado, v2026.7.3.0).
	///
	/// <para />
	/// <b>2. Como se cambia en vivo.</b> Con <c>LanguageManager.Instance.SetLanguage(cultura)</c>,
	/// la MISMA llamada publica que usa el menu de idioma del propio juego. Recarga en el acto los
	/// textos de vanilla y los de todos los mods (<c>SetLanguage</c> -&gt; <c>ReloadLanguage</c>
	/// -&gt; <c>LoadActiveCultureTranslationsFromSources</c>) y avisa por
	/// <c>ModSystem.OnLocalizationsLoaded</c>, que es donde el panel se repinta. Sin reiniciar
	/// nada.
	///
	/// <para />
	/// <b>Efecto que hay que conocer</b>: no existe en tModLoader un "idioma solo para mi mod".
	/// <c>LocalizedText</c> guarda un unico valor, el de la cultura activa, asi que poner
	/// Terrakeep en ingles pone TODO el juego en ingles. Por eso el valor por defecto de
	/// <see cref="AjustesConfig.Idioma"/> es <see cref="IdiomaDeTerrakeep.SeguirElJuego"/>: si el
	/// usuario no elige nada, Terrakeep no le toca el idioma a nadie.
	/// </summary>
	public static class Idiomas
	{
		/// <summary>Prefijo comun de todas las claves de este mod. Lo impone tModLoader: las
		/// claves de un mod viven bajo <c>Mods.&lt;NombreDelMod&gt;.</c>, y el nombre del mod es
		/// el de la carpeta (<c>TerrakeepMod</c>).</summary>
		public const string Prefijo = "Mods.TerrakeepMod.";

		private static bool _elJuegoYaEstaEnMarcha;

		/// <summary>Se dispara cuando el idioma acaba de cambiar de verdad, para que las
		/// interfaces abiertas vuelvan a pedir sus textos.</summary>
		public static event Action Cambiado;

		/// <summary>Idioma configurado ahora mismo (o el valor por defecto si el
		/// <see cref="AjustesConfig"/> todavia no esta cargado).</summary>
		public static IdiomaDeTerrakeep IdiomaConfigurado {
			get {
				return AjustesConfig.Instance != null
					? AjustesConfig.Instance.Idioma
					: IdiomaDeTerrakeep.SeguirElJuego;
			}
		}

		/// <summary>
		/// true si el juego esta en español ahora mismo.
		/// <para />
		/// Lo necesitan las poquisimas piezas cuyo texto NO sale de los <c>.hjson</c> sino de una
		/// tabla que solo existe en español (las 121 categorias de Calamity que trae
		/// <c>Terrakeep.Core</c>): con esto pueden elegir su alternativa en ingles en vez de
		/// enseñar español dentro de una interfaz en ingles. Para todo lo demas, la via es
		/// <see cref="Texto"/>.
		/// </summary>
		public static bool EnEspanol {
			get {
				return Language.ActiveCulture != null &&
					Language.ActiveCulture.Name != null &&
					Language.ActiveCulture.Name.StartsWith("es");
			}
		}

		/// <summary>Nombre de la cultura activa del juego ("es-ES", "en-US"...).</summary>
		public static string CulturaActiva {
			get {
				return Language.ActiveCulture != null ? Language.ActiveCulture.Name : "(sin cultura)";
			}
		}

		/// <summary>
		/// Resuelve una clave RELATIVA del mod ("Ajustes.Titulo") por el camino oficial. Si la
		/// clave no existe en ningun <c>.hjson</c>, tModLoader devuelve la clave completa tal
		/// cual, que es justo lo que se quiere: se ve en pantalla que falta, en vez de un hueco.
		/// </summary>
		public static string Texto(string claveRelativa)
		{
			return Language.GetTextValue(Prefijo + claveRelativa);
		}

		/// <summary>Igual que <see cref="Texto"/> con sustituciones ({0}, {1}...).</summary>
		public static string Texto(string claveRelativa, params object[] argumentos)
		{
			return Language.GetTextValue(Prefijo + claveRelativa, argumentos);
		}

		/// <summary>Cultura de Terraria correspondiente a cada opcion del mod. Para
		/// <see cref="IdiomaDeTerrakeep.SeguirElJuego"/> devuelve null: no hay nada que forzar.</summary>
		public static GameCulture CulturaDe(IdiomaDeTerrakeep idioma)
		{
			switch (idioma) {
				case IdiomaDeTerrakeep.Espanol:
					return GameCulture.FromCultureName(GameCulture.CultureName.Spanish);
				case IdiomaDeTerrakeep.English:
					return GameCulture.FromCultureName(GameCulture.CultureName.English);
				default:
					return null;
			}
		}

		/// <summary>
		/// Marca que el juego ya esta corriendo (lo llama <see cref="AjustesSystem"/> en su primer
		/// fotograma) y aplica el idioma guardado. Antes de esto, cualquier peticion de cambio se
		/// ignora: recargar las traducciones en mitad de la carga de mods no es seguro.
		/// </summary>
		public static void ElJuegoYaEstaEnMarcha()
		{
			if (_elJuegoYaEstaEnMarcha) {
				return;
			}
			_elJuegoYaEstaEnMarcha = true;
			Aplicar(IdiomaConfigurado, "arranque (idioma recordado por ModConfig)");
		}

		/// <summary>Aplica el idioma solo si el juego ya arranco del todo.</summary>
		public static void AplicarSiElJuegoYaEstaEnMarcha(IdiomaDeTerrakeep idioma, string origen)
		{
			if (_elJuegoYaEstaEnMarcha) {
				Aplicar(idioma, origen);
			}
		}

		/// <summary>
		/// Cambia el idioma del juego EN VIVO si hace falta. Devuelve true si de verdad cambio.
		/// </summary>
		public static bool Aplicar(IdiomaDeTerrakeep idioma, string origen)
		{
			GameCulture objetivo = CulturaDe(idioma);
			if (objetivo == null || Language.ActiveCulture == objetivo) {
				return false;
			}

			string antes = CulturaActiva;
			// Misma llamada publica que el menu de idioma de vanilla. Recarga los textos de
			// Terraria y los de todos los mods de golpe.
			LanguageManager.Instance.SetLanguage(objetivo);

			Registrar("Idioma cambiado EN VIVO via " + origen + ": " + antes + " -> " + CulturaActiva +
				". Prueba real de la recarga, clave propia del mod \"Ajustes.Titulo\" = \"" +
				Texto("Ajustes.Titulo") + "\".");

			Avisar();
			return true;
		}

		/// <summary>
		/// Guarda la eleccion del usuario en el <see cref="AjustesConfig"/> y la aplica. Es lo que
		/// hace el selector ES/EN del panel de Ajustes.
		/// <para />
		/// Para un config <c>ClientSide</c> estando ya dentro de la partida, <c>SaveChanges</c>
		/// (metodo publico real de <c>ModConfig</c> en esta version) hace lo correcto: escribe el
		/// JSON, lo recarga y llama a <c>OnChanged</c>. En un config <c>ServerSide</c> desde un
		/// cliente habria que tratar <c>ConfigSaveResult.RequestSentToServer</c>, pero este no lo
		/// es.
		/// </summary>
		public static void Elegir(IdiomaDeTerrakeep idioma)
		{
			AjustesConfig config = AjustesConfig.Instance;
			if (config == null) {
				Registrar("No se puede guardar el idioma: AjustesConfig.Instance todavia es null.");
				return;
			}

			if (config.Idioma == idioma) {
				// Aunque no cambie la preferencia, puede que el idioma del juego se haya movido
				// por otro lado; se reaplica para dejarlo coherente.
				Aplicar(idioma, "seleccion repetida en el panel de Ajustes");
				return;
			}

			config.Idioma = idioma;
			ConfigSaveResult resultado = config.SaveChanges();
			Registrar("Idioma guardado en ModConfig (persistente entre partidas): " + idioma +
				". Resultado de SaveChanges: " + resultado + ".");

			// SaveChanges ya llama a OnChanged -> AplicarSiElJuegoYaEstaEnMarcha, pero solo cuando
			// no se esta en el menu principal. Se reaplica aqui para cubrir los dos casos sin
			// depender de por que rama entro.
			Aplicar(idioma, "selector del panel de Ajustes");
		}

		/// <summary>La llama <see cref="AjustesSystem.OnLocalizationsLoaded"/>: el idioma pudo
		/// cambiarlo tambien el propio menu del juego, no solo este mod.</summary>
		public static void Avisar()
		{
			Action manejador = Cambiado;
			if (manejador != null) {
				manejador();
			}
		}

		private static void Registrar(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
			}
		}
	}
}
