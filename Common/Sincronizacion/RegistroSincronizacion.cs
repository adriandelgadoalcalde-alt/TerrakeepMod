using Terraria.ModLoader;

namespace TerrakeepMod.Common.Sincronizacion
{
	/// <summary>Log de evidencia de la sincronización con la app de escritorio. Solo el log del
	/// juego (no hace falta un archivo propio: esta pieza no compite por un client.log compartido
	/// con ningún otro workstream en paralelo).</summary>
	public static class RegistroSincronizacion
	{
		public static void Linea(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " Sincronizacion: " + mensaje);
			}
		}
	}
}
