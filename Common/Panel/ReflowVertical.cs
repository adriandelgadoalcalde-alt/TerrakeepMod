using System;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// Aritmetica pura (cero dependencias de Terraria/FNA) para comprimir un flujo vertical de UI
	/// cuando el alto REAL disponible es menor que el que el flujo necesita a tamaño natural.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Causa raiz real de los tres defectos de 1366x768@150% que encontro la auditoria sistematica
	/// de UIScale/resolucion del 29-sep-2026 (bitacora.md, requirement 0446b3c9): en esa unica
	/// combinacion, el marco pierde a la vez su tope de ancho de 1080 UI-px Y su tope habitual de
	/// alto (ninguno de los dos MaxWidth/MaxHeight llega a aplicar, porque la fraccion del 96%/94%
	/// sobre una resolucion fisica pequeña dividida entre un UIScale alto ya da un numero menor que
	/// esos topes) - el marco sale mas pequeño en las DOS dimensiones que el tamaño "de referencia"
	/// para el que estaban calibradas las posiciones ABSOLUTAS de varios bloques de texto de ayuda
	/// (<c>PestanaInventario</c>, <c>PestanaMapa</c>, <c>ContenidoAjustes</c>), que se salian del
	/// hueco real o se solapaban entre si en vez de encoger con el.
	/// </para>
	/// <para>
	/// Vive fuera de cualquier <c>UIElement</c> a proposito: es la unica pieza de este arreglo que
	/// se puede probar de verdad sin el motor (ver <c>TerrakeepMod.Tests</c>), enlazada (no
	/// copiada) en <c>TerrakeepMod.LogicaPura</c> con el mismo patron real que ya usa
	/// <c>Common/Libreria/GramaticaBusqueda.cs</c>.
	/// </para>
	/// </remarks>
	public static class ReflowVertical
	{
		/// <summary>
		/// Factor (entre <paramref name="escalaMinima"/> y 1) por el que hay que multiplicar TANTO
		/// la posicion vertical de cada elemento de un flujo COMO su escala de texto para que el
		/// flujo entero quepa en el alto real disponible, sin que ningun elemento se solape con el
		/// siguiente.
		/// </summary>
		/// <remarks>
		/// Aplicar el MISMO factor a la posicion y a la escala (no solo a la posicion) es lo que
		/// garantiza matematicamente que no haya solape: si se comprimiera solo la posicion dejando
		/// el alto real de cada elemento fijo, el ULTIMO elemento del flujo podria seguir saliendose
		/// por el borde inferior (su alto sin comprimir no "cabe" en el hueco que le deja la nueva
		/// posicion comprimida). Escalando las dos cosas a la vez, el flujo entero (posiciones Y
		/// alturas) se reduce de forma uniforme, y un flujo que cabia sin solaparse a escala 1 sigue
		/// sin solaparse a cualquier escala menor.
		/// </remarks>
		/// <param name="altoNecesario">Alto total que ocupa el flujo a tamaño natural (escala 1).</param>
		/// <param name="altoDisponible">Alto real que hay de verdad para el flujo ahora mismo.</param>
		/// <param name="escalaMinima">Suelo duro: por debajo de esto el texto se volveria
		/// ilegible, y es preferible que se note que algo esta muy apretado a que desaparezca -
		/// mismo criterio que <c>PanelTerrakeepState.AjustarEscalaDeLasPestanas</c>.</param>
		public static float FactorDeCompresion(float altoNecesario, float altoDisponible, float escalaMinima = 0.55f)
		{
			if (altoNecesario <= 0f || altoDisponible <= 0f || altoDisponible >= altoNecesario) {
				return 1f;
			}

			float factor = altoDisponible / altoNecesario;
			return factor < escalaMinima ? escalaMinima : factor;
		}
	}
}
