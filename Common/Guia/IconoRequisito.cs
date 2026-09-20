using Terraria.ID;
using Terrakeep.Core.Guia;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// TM4 del catálogo de rediseño visual ("Guía con checklist de sprites"): resuelve el
	/// <c>Item.type</c> REAL a dibujar junto a un requisito de objeto, para <see cref="TerrakeepMod.UI.Guia.FilaRequisitoTk"/>.
	/// </summary>
	/// <remarks>
	/// <b>El hueco real que dejó pendiente el commit parcial de TM4</b> ("no hay una resolución de
	/// 'id real de esta partida' ya expuesta fuera de <c>ProveedorEstadoGuiaMod</c>"): investigado
	/// con calma, resulta que SÍ la hay, solo que no es la misma para vanilla que para mod.
	/// <see cref="RequisitoGuia.Id"/> (vanilla, del propio <c>.json</c> de la Guía) ya es un
	/// <c>Item.type</c> real, sin ninguna resolución que hacer. <see cref="RequisitoGuia.IdMod"/>
	/// (Calamity y compañía) es un NOMBRE con formato <c>"NombreDelMod/NombreDelObjeto"</c> - el
	/// MISMO formato y el MISMO truco real ya usado en este mismo mod para resolver tiles de mod
	/// (<c>Common/Exploracion/ObjetivosBusqueda.Resolver</c>, <c>TileID.Search.TryGetId</c>):
	/// <c>ItemID.Search</c> es el diccionario real de ReLogic que <c>ModItem.Register</c> rellena
	/// con <c>ItemID.Search.Add(FullName, Type)</c> (código real del <c>tModLoader.dll</c>
	/// instalado) - funciona igual de bien si el mod no está cargado (simplemente no resuelve, sin
	/// reventar).
	/// </remarks>
	public static class IconoRequisito
	{
		/// <summary>El <c>Item.type</c> real a dibujar para este requisito, o -1 si no aplica (no es
		/// de tipo objeto) o no se pudo resolver (id de mod no cargado en esta partida).</summary>
		public static int TipoDeObjeto(RequisitoGuia requisito)
		{
			if (requisito == null) {
				return -1;
			}

			if (requisito.Tipo == TipoRequisitoGuia.Objeto) {
				if (!string.IsNullOrEmpty(requisito.IdMod)) {
					return ItemID.Search.TryGetId(requisito.IdMod, out int tipoMod) ? tipoMod : -1;
				}
				return requisito.Id > 0 ? requisito.Id : -1;
			}

			// "Cualquiera de estos": se enseña el PRIMERO que resuelva - un icono real de un
			// candidato valido, nunca "el primero de la lista aunque no exista en esta partida".
			if (requisito.Tipo == TipoRequisitoGuia.ObjetoCualquiera) {
				if (requisito.IdsMod != null) {
					foreach (string nombre in requisito.IdsMod) {
						if (!string.IsNullOrEmpty(nombre) && ItemID.Search.TryGetId(nombre, out int tipoMod) && tipoMod > 0) {
							return tipoMod;
						}
					}
				}
				if (requisito.Ids != null) {
					foreach (int id in requisito.Ids) {
						if (id > 0) {
							return id;
						}
					}
				}
			}

			return -1;
		}
	}
}
