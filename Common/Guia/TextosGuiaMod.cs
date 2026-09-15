using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Los textos YA RESUELTOS de un <see cref="PasoGuia"/>/<see cref="TramoGuia"/>/
	/// <see cref="ResultadoRequisito"/>, como metodos de extension.
	/// </summary>
	/// <remarks>
	/// Consolidacion T1 (I+D-PROXIMOS-PASOS-FAMILIA-KEEP.md, 16-sep-2026): antes de esta ronda,
	/// <c>PasoGuia</c>/<c>TramoGuia</c> eran clases PROPIAS del mod con estas mismas propiedades
	/// (<c>Titulo</c>, <c>Porque</c>...) calculadas con <see cref="Idiomas.Texto"/> directamente.
	/// Ahora esos tipos son <c>Terrakeep.Core.Guia.PasoGuia</c>/<c>TramoGuia</c> (ver
	/// <c>ModeloGuia.cs</c>, alias de tipo global), y Core NO puede tener esas propiedades: no
	/// conoce ningun idioma a proposito (misma razon por la que <c>ResultadoRequisitoGuia</c> trae
	/// <c>TextoClave</c>/<c>TextoArgs</c> en vez de un texto ya resuelto - ver la cabecera de
	/// <c>GuideModel.cs</c> en Terrakeep.Core). Aqui, en el mod (que SI conoce el idioma, via
	/// <see cref="Idiomas"/>), se recupera la misma ergonomia como metodos de extension: el unico
	/// cambio real en cada sitio que los usaba fue añadir un par de parentesis
	/// (<c>paso.Titulo</c> -&gt; <c>paso.Titulo()</c>).
	/// </remarks>
	public static class TextosGuiaMod
	{
		/// <summary>Titulo del objetivo, traducido.</summary>
		public static string Titulo(this PasoGuia paso) => Idiomas.Texto("Guia.Paso." + paso.Clave + ".Titulo");

		/// <summary>El PORQUE: que mecanica del juego hay detras de este objetivo.</summary>
		public static string Porque(this PasoGuia paso) => Idiomas.Texto("Guia.Paso." + paso.Clave + ".Porque");

		/// <summary>El COMO: por donde se empieza, sin destripar nada.</summary>
		public static string Como(this PasoGuia paso) => Idiomas.Texto("Guia.Paso." + paso.Clave + ".Como");

		/// <summary>Nombre de la zona, traducido.</summary>
		public static string ZonaLegible(this PasoGuia paso) =>
			string.IsNullOrEmpty(paso.Zona) ? "" : Idiomas.Texto("Guia.Zona." + paso.Zona);

		public static string Nombre(this TramoGuia tramo) => Idiomas.Texto("Guia.Tramo." + tramo.Clave + ".Nombre");

		/// <summary>Una linea de que va el tramo, para la lista de "lo que viene despues".</summary>
		public static string Resumen(this TramoGuia tramo) => Idiomas.Texto("Guia.Tramo." + tramo.Clave + ".Resumen");

		/// <summary>Linea ya montada y traducida ("Vecinos en el pueblo: 3 de 4"), a partir de
		/// <c>TextoClave</c>/<c>TextoArgs</c> - el mismo <see cref="Idiomas.Texto(string, object[])"/>
		/// que ya usaba <c>EvaluadorGuia</c> antes de la consolidacion, solo que ahora la clave y los
		/// argumentos vienen resueltos por <c>GuideEvaluationEngine</c> en vez de montados aqui.</summary>
		public static string Linea(this ResultadoRequisito resultado) =>
			Idiomas.Texto(resultado.TextoClave, resultado.TextoArgs);
	}
}
