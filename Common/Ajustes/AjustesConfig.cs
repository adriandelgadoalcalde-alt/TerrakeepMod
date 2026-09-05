using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TerrakeepMod.Common.Ajustes
{
	/// <summary>
	/// Idioma en el que Terrakeep muestra su interfaz.
	/// </summary>
	public enum IdiomaDeTerrakeep
	{
		/// <summary>El que tenga el juego. Es lo que se espera por defecto de un mod bien
		/// educado: no cambia nada que el usuario no haya pedido.</summary>
		SeguirElJuego,

		/// <summary>Espanol (es-ES).</summary>
		Espanol,

		/// <summary>Ingles (en-US).</summary>
		English
	}

	/// <summary>
	/// Configuracion persistente de Terrakeep. <c>ModConfig</c> es el mecanismo oficial de
	/// tModLoader para esto: el propio motor serializa esta clase a
	/// <c>Documents\My Games\Terraria\tModLoader\ModConfigs\TerrakeepMod_Ajustes.json</c>, la
	/// recarga sola al arrancar y le da una pantalla de ajustes en el menu del juego
	/// (Mods &gt; Terrakeep &gt; Configuracion) sin escribir ni una linea de interfaz.
	/// <para />
	/// <b><see cref="ConfigScope.ClientSide"/></b> y no <c>ServerSide</c>: es una preferencia
	/// personal de quien juega (en que idioma quiere ver el panel), no una regla de partida que
	/// deba imponerse a todos los jugadores de un servidor.
	/// <para />
	/// <b>El campo estatico <see cref="Instance"/> lo rellena tModLoader solo</b>: "tModLoader
	/// will automatically assign (and later unload) this instance to a static field named
	/// Instance in the class prior to calling this method" (documentacion de
	/// <c>ModConfig.OnLoaded</c> en el <c>tModLoader.dll</c> real instalado, v2026.7.3.0). No hay
	/// que instanciarla ni buscarla.
	/// </summary>
	public class AjustesConfig : ModConfig
	{
		public static AjustesConfig Instance;

		public override ConfigScope Mode {
			get { return ConfigScope.ClientSide; }
		}

		/// <summary>
		/// Idioma de la interfaz de Terrakeep. Cambiarlo desde aqui o desde el panel de Ajustes
		/// (tecla J) tiene el mismo efecto: los dos caminos acaban en
		/// <see cref="Idiomas.Aplicar"/>.
		/// </summary>
		[DefaultValue(IdiomaDeTerrakeep.SeguirElJuego)]
		public IdiomaDeTerrakeep Idioma { get; set; }

		/// <summary>
		/// Atajos del mod a los que ya se les ha puesto su tecla por defecto alguna vez (ver
		/// <see cref="SembradorDeAtajos"/>, y el porque en su documentacion). Se guarda para no
		/// volver a ponersela a un atajo al que el usuario se la quito a proposito.
		/// </summary>
		public List<string> AtajosYaSembrados { get; set; }

		/// <summary>
		/// Obligatorio en cuanto un <c>ModConfig</c> tiene un tipo por referencia: tModLoader
		/// clona la configuracion para comparar valores y decidir si hace falta recargar, y el
		/// <c>MemberwiseClone</c> por defecto compartiria la MISMA lista entre el original y la
		/// copia ("Modders need to override this method if their config contains reference types.
		/// Failure to do so will lead to bugs", documentacion real de <c>ModConfig.Clone</c>).
		/// </summary>
		public override ModConfig Clone()
		{
			AjustesConfig copia = (AjustesConfig)base.Clone();
			copia.AtajosYaSembrados = AtajosYaSembrados != null
				? new List<string>(AtajosYaSembrados)
				: new List<string>();
			return copia;
		}

		/// <summary>
		/// Se llama cada vez que la configuracion pasa a estar lista o cambia: al cargar el mod,
		/// al tocar la pantalla de configuracion del juego y tras cada
		/// <c>SaveChanges</c> del panel de Ajustes.
		/// <para />
		/// A proposito NO se aplica el idioma aqui cuando todavia se estan cargando los mods:
		/// <c>OnLoaded</c>/<c>OnChanged</c> pueden dispararse en mitad de la carga, y cambiar el
		/// idioma implica recargar TODAS las traducciones del juego y de todos los mods. De eso
		/// se encarga <see cref="AjustesSystem"/>, que espera a que el juego este ya en marcha.
		/// </summary>
		public override void OnChanged()
		{
			Idiomas.AplicarSiElJuegoYaEstaEnMarcha(Idioma, "ModConfig.OnChanged");
		}
	}
}
