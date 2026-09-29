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

		/// <summary>
		/// Reparto de una ficha de renglones de paso fijo (Exploracion &gt; Este mundo) en un hueco que
		/// puede ser mas bajo de lo que necesita, priorizando la LEGIBILIDAD sin permitir nunca un
		/// solape:
		/// <list type="number">
		/// <item>Si ni apretando el interlineado hasta <paramref name="fraccionPasoMinimo"/> cabe
		/// todo, se ocultan primero los renglones <paramref name="colapsable"/> (en la ficha, los que
		/// ya repite la cabecera de Exploracion: nombre, tamaño, modo, posicion y explorado).</item>
		/// <item>Con lo que queda visible, se aprieta el INTERLINEADO (cabecera y paso) en la
		/// proporcion justa, sin tocar la letra, mientras el paso no baje de
		/// <paramref name="fraccionPasoMinimo"/> de su valor natural.</item>
		/// <item>Solo si aun asi no cabe, la letra se reduce en la MISMA proporcion que el paso a
		/// partir de ese punto (asi el glifo sigue cabiendo en su renglon igual que al paso minimo).</item>
		/// </list>
		/// Devuelve el factor que hay que aplicar a la cabecera y al paso; con el, lo ocupado
		/// (cabecera + renglones visibles) nunca pasa de <paramref name="altoDisponible"/>.
		/// </summary>
		/// <param name="visible">Salida: que renglones se enseñan (mismo largo que <paramref name="colapsable"/>).</param>
		/// <param name="factorLetra">Salida: factor sobre la escala base de la letra (1 = sin tocar).</param>
		public static float DistribuirRenglones(float altoCabecera, float paso, bool[] colapsable, float altoDisponible,
			float fraccionPasoMinimo, bool[] visible, out float factorLetra)
		{
			int total = colapsable.Length;
			int colapsables = 0;
			for (int i = 0; i < total; i++) {
				visible[i] = true;
				if (colapsable[i]) {
					colapsables++;
				}
			}

			float necesario = altoCabecera + total * paso;
			if (altoDisponible > 0f && colapsables > 0 && necesario * fraccionPasoMinimo > altoDisponible) {
				for (int i = 0; i < total; i++) {
					if (colapsable[i]) {
						visible[i] = false;
					}
				}
				necesario = altoCabecera + (total - colapsables) * paso;
			}

			float factor = altoDisponible <= 0f || necesario <= 0f || altoDisponible >= necesario
				? 1f
				: altoDisponible / necesario;
			factorLetra = factor >= fraccionPasoMinimo ? 1f : factor / fraccionPasoMinimo;
			return factor;
		}

		/// <summary>
		/// Escala de texto mas pequeña que el mod usa para texto que hay que LEER (no un suelo de
		/// emergencia): 0,68, la de los avisos secundarios de Exploracion (<c>PestanaMundo.
		/// EscalaAvisoSecundario</c>), el titulo de tramo de la Guia, la ruta de carpetas de Buffs o
		/// las filas del selector de cofres. A UIScale 100% son ~14 px de glifo real, el minimo que
		/// ya se midio como "legible" en el resto del mod (bitacora.md, 29-sep-2026).
		/// </summary>
		public const float EscalaMinimaLegible = 0.68f;

		/// <summary>
		/// Variante de <see cref="FactorDeCompresion"/> para un flujo cuyo ULTIMO renglon no puede
		/// encogerse por debajo de <paramref name="escalaMinimaUltima"/> (caso real: "Zoom" de
		/// Exploracion &gt; Mapa, que a 1366x768@150% quedaba a 11 px - requirement 0446b3c9). Si
		/// comprimir todo con el mismo factor dejaria ese renglon por debajo de su minimo, se le
		/// fija en el minimo y se reparte el hueco que QUEDA entre el resto del flujo: el resto se
		/// comprime algo mas, pero el total (resto comprimido + ultimo renglon a su minimo) sigue
		/// cabiendo en <paramref name="altoDisponible"/> - la misma garantia de "sin solape" que
		/// <see cref="FactorDeCompresion"/>, mientras el resto no toque su propio suelo duro.
		/// </summary>
		/// <param name="altoNaturalTotal">Alto del flujo entero a escala natural (incluye el ultimo).</param>
		/// <param name="altoNaturalUltimo">Alto del ultimo renglon a su escala base.</param>
		/// <param name="escalaBaseUltimo">Escala base del ultimo renglon.</param>
		/// <param name="escalaMinimaUltimo">Escala por debajo de la cual no se deja bajar el ultimo.</param>
		/// <param name="altoDisponible">Alto real disponible para el flujo.</param>
		/// <param name="escalaUltimo">Escala ABSOLUTA con la que hay que dibujar el ultimo renglon.</param>
		/// <param name="escalaMinima">Suelo duro del resto del flujo (el de siempre).</param>
		/// <returns>Factor a aplicar a la posicion y a la escala del RESTO del flujo; la posicion del
		/// ultimo renglon es la suma de los altos del resto por este mismo factor.</returns>
		public static float FactorConUltimoMinimo(float altoNaturalTotal, float altoNaturalUltimo,
			float escalaBaseUltimo, float escalaMinimaUltimo, float altoDisponible, out float escalaUltimo,
			float escalaMinima = 0.55f)
		{
			float factor = FactorDeCompresion(altoNaturalTotal, altoDisponible, escalaMinima);
			float minimo = escalaMinimaUltimo < escalaBaseUltimo ? escalaMinimaUltimo : escalaBaseUltimo;
			if (escalaBaseUltimo <= 0f || escalaBaseUltimo * factor >= minimo) {
				escalaUltimo = escalaBaseUltimo * factor;
				return factor;
			}

			escalaUltimo = minimo;
			float altoUltimo = altoNaturalUltimo * (minimo / escalaBaseUltimo);
			float altoResto = altoNaturalTotal - altoNaturalUltimo;
			float huecoResto = altoDisponible - altoUltimo;
			if (huecoResto <= 0f) {
				// Ni siquiera el ultimo renglon cabe solo: el resto se va a su suelo duro (nunca
				// "1", que FactorDeCompresion devuelve para datos no validos).
				return altoResto > 0f ? escalaMinima : 1f;
			}
			return FactorDeCompresion(altoResto, huecoResto, escalaMinima);
		}
	}
}
