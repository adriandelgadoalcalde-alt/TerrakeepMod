using Terraria.ModLoader;

namespace TerrakeepMod.Common.Prefijos
{
	/// <summary>
	/// Ciclo de vida de <see cref="CatalogoPrefijosLegales"/>. Se carga eager, igual que
	/// <see cref="SistemaMejorPrefijo"/> y por el mismo motivo real: los .json solo se pueden leer
	/// con el .tmod todavia abierto (<c>Mod.GetFileBytes</c> revienta con "File not open" en
	/// cuanto termina la carga), asi que no se puede esperar a que alguien abra el picker de
	/// prefijo de la Libreria para leerlos por primera vez.
	/// </summary>
	public class SistemaPrefijosLegales : ModSystem
	{
		public override void Load()
		{
			CatalogoPrefijosLegales.LeerArchivos(Mod);
		}

		public override void PostSetupContent()
		{
			CatalogoPrefijosLegales.Resolver();
		}

		public override void Unload()
		{
			CatalogoPrefijosLegales.Descargar();
		}
	}
}
