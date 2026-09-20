using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// TM4 del catálogo de rediseño visual ("Guía con checklist de sprites"): el icono REAL de
	/// cabeza del jefe, el mismo <c>TextureAssets.NpcHeadBoss</c> con el que vanilla dibuja el
	/// marcador de jefe del mapa - nunca un dibujo propio ni un sustituto generico.
	/// </summary>
	/// <remarks>
	/// <c>NPCID.Sets.BossHeadTextures[tipo]</c> es -1 (por defecto de fabrica, ver su propio
	/// XMLdoc real en <c>NPCID.cs</c>) para cualquier NPC que no tenga icono de jefe - la inmensa
	/// mayoria. <see cref="Resolver"/> nunca lanza y devuelve null en ese caso (y en cualquier tipo
	/// fuera de rango, p.ej. si Calamity no esta cargado y el tipo pedido es suyo): quien lo llama
	/// ya sabe que null significa "sin icono, no dibujar nada" - nunca un placeholder inventado.
	/// </remarks>
	public static class IconoJefe
	{
		public static Texture2D Resolver(int tipoNpc)
		{
			if (tipoNpc <= 0 || tipoNpc >= NPCID.Sets.BossHeadTextures.Length) {
				return null;
			}

			int indice = NPCID.Sets.BossHeadTextures[tipoNpc];
			if (indice < 0 || indice >= TextureAssets.NpcHeadBoss.Length) {
				return null;
			}

			return TextureAssets.NpcHeadBoss[indice].Value;
		}
	}
}
