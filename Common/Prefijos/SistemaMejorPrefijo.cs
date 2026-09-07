using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Prefijos
{
	/// <summary>
	/// Ciclo de vida de <see cref="CatalogoMejorPrefijo"/>: lo carga en cuanto arranca el mod, sin
	/// depender de que nadie abra ningun panel. Hace falta que sea asi (a diferencia de
	/// <c>ArbolLibreria</c>/<c>CatalogoBuilds</c>, perezosos) porque el indicador de calidad de
	/// prefijo (<see cref="GlobalItemMejorPrefijo"/>) tiene que funcionar en CUALQUIER tooltip del
	/// juego real, incluido el Inventario normal, se haya abierto alguna vez la Libreria o no.
	/// </summary>
	public class SistemaMejorPrefijo : ModSystem
	{
		public override void Load()
		{
			// El .json hay que leerlo con el .tmod todavia abierto (ver CatalogoMejorPrefijo).
			CatalogoMejorPrefijo.LeerArchivo(Mod);
		}

		public override void PostSetupContent()
		{
			// Aqui ya han registrado su contenido TODOS los mods: ItemLoader.GetItem ya resuelve
			// el ModItem real de cualquier objeto de Calamity, que es lo que necesita
			// CatalogoMejorPrefijo.MejorPrefijo para las claves de nombre interno.
			CatalogoMejorPrefijo.Resolver();
		}

		public override void Unload()
		{
			CatalogoMejorPrefijo.Descargar();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}
			// Arnes de verificacion PROPIO de este problema, variable de entorno propia (ver
			// AutopruebaPrefijos) para no pisar la autoprueba de WS3 ni la de ningun otro
			// workstream en marcha en paralelo sobre este mismo repositorio.
			AutopruebaPrefijos.Actualizar();
		}
	}
}
