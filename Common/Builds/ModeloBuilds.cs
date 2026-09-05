using System.Collections.Generic;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>
	/// Un objeto concreto del catalogo de builds, ya RESUELTO a un id real del juego.
	/// <para />
	/// El JSON de origen solo trae un "pid" (nombre interno) y los nombres para mostrar; el id
	/// numerico no se puede guardar en el archivo porque los ids de los mods cambian de una
	/// sesion a otra. Ver <see cref="CatalogoBuilds"/> para como se resuelve.
	/// </summary>
	public sealed class ObjetoBuild
	{
		/// <summary>Nombre interno tal cual viene del JSON. Sin "/" es vanilla ("MoltenHelmet");
		/// con "/" es de un mod ("CalamityMod/SmokingComet").</summary>
		public string Pid;

		/// <summary>Nombre en español del catalogo (el que se enseña en la interfaz).</summary>
		public string NombreEs;

		/// <summary>Nombre en ingles del catalogo (respaldo si no hay traduccion).</summary>
		public string NombreEn;

		/// <summary>Prefijo RECOMENDADO por el catalogo (nombre interno vanilla, ej.
		/// "Legendary"), o null. Es solo informativo: auto-equipar nunca cambia el prefijo del
		/// objeto real del jugador (ver <see cref="AutoEquipar"/>).</summary>
		public string PrefijoRecomendado;

		/// <summary>Id real de <c>Item.type</c> en esta partida, o 0 si el pid no se pudo
		/// resolver (tipicamente: es de Calamity y Calamity no esta cargado).</summary>
		public int Tipo;

		/// <summary>true si el pid se resolvio a un objeto real de esta partida.</summary>
		public bool Resuelto => Tipo > 0;

		/// <summary>Nombre para mostrar: el español del catalogo, con el ingles de respaldo.</summary>
		public string Nombre => !string.IsNullOrEmpty(NombreEs) ? NombreEs
			: (!string.IsNullOrEmpty(NombreEn) ? NombreEn : Pid);
	}

	/// <summary>Las tres listas de equipo de una clase concreta dentro de una etapa.</summary>
	public sealed class ClaseBuild
	{
		/// <summary>Clave del JSON: melee / ranged / mage / summoner / rogue (rogue solo en
		/// Calamity).</summary>
		public string Clave;

		/// <summary>Nombre de la clase en español, para las pildoras de la interfaz.</summary>
		public string Etiqueta;

		public readonly List<ObjetoBuild> Armadura = new List<ObjetoBuild>();
		public readonly List<ObjetoBuild> Armas = new List<ObjetoBuild>();
		public readonly List<ObjetoBuild> Accesorios = new List<ObjetoBuild>();

		/// <summary>Recorre las tres listas seguidas, en el orden en que las enseña el panel.</summary>
		public IEnumerable<ObjetoBuild> Todos()
		{
			foreach (ObjetoBuild o in Armadura) {
				yield return o;
			}
			foreach (ObjetoBuild o in Armas) {
				yield return o;
			}
			foreach (ObjetoBuild o in Accesorios) {
				yield return o;
			}
		}
	}

	/// <summary>Una etapa de progresion (pre-hardmode, hardmode temprano, final del juego).</summary>
	public sealed class EtapaBuild
	{
		public string Clave;
		public string Etiqueta;
		public readonly List<ClaseBuild> Clases = new List<ClaseBuild>();

		public ClaseBuild BuscarClase(string clave)
		{
			foreach (ClaseBuild c in Clases) {
				if (c.Clave == clave) {
					return c;
				}
			}
			return null;
		}
	}

	/// <summary>
	/// Un archivo de builds entero (builds.json = vanilla, builds_calamity.json = Calamity).
	/// </summary>
	public sealed class FuenteBuilds
	{
		public string Clave;
		public string Etiqueta;
		public readonly List<EtapaBuild> Etapas = new List<EtapaBuild>();

		/// <summary>Cuantos de sus objetos se resolvieron a un id real de esta partida.</summary>
		public int Resueltos;

		/// <summary>Cuantos objetos tiene en total (resueltos o no).</summary>
		public int Total;

		/// <summary>Cuantos de sus pid son de un mod (llevan "/"), resueltos o no.</summary>
		public int DeMod;

		/// <summary>Cuantos pid de mod se resolvieron de verdad.</summary>
		public int DeModResueltos;

		/// <summary>
		/// true si merece la pena enseñar esta fuente en el panel.
		/// <para />
		/// No basta con "resolvio algo": <c>builds_calamity.json</c> apoya su progresion en
		/// bastante equipo VANILLA (Molten, Escudo de obsidiana, Velo estelar...), asi que sin
		/// Calamity instalado igualmente resuelve unas decenas de objetos y se ofreceria una
		/// pestaña "Calamity" con casi todo en rojo. Comprobado en el juego real: 35 de 154. Por
		/// eso, si la fuente trae pid de mod, se exige que al menos uno de ELLOS se haya resuelto.
		/// </summary>
		public bool Utilizable => Resueltos > 0 && (DeMod == 0 || DeModResueltos > 0);
	}
}
