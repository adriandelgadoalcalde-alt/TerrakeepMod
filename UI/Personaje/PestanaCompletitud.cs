using System.Collections.Generic;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Completitud;
using TerrakeepMod.UI.Guia;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Sub-pestaña nueva de Personaje: "vista de qué falta para el 100%" del encargo - cuatro
	/// resúmenes reales (jefes/eventos de la Guía, bestiario, logros, objetos investigados), cada
	/// uno con su barra y su lista de lo que falta cuando tiene sentido enseñarla. Ver
	/// <see cref="EstadoCompletitud"/> para de dónde sale cada dato real.
	/// </summary>
	public class PestanaCompletitud : UIElement
	{
		private const float AltoFila = 120f;

		private readonly List<FilaResumen> _filas = new List<FilaResumen>();
		private int _fotogramasHastaRefrescar;

		private sealed class FilaResumen
		{
			public EtiquetaTk Titulo;
			public MedidorPreparacionTk Medidor;
			public EtiquetaTk Numeros;
			public string TextoNumeros = "";
			public UIPanel CajaFaltan;
			public UIList ListaFaltan;
			public System.Func<ResumenCompletitud> Leer;
			public string ClaveTitulo;
			public bool ConListaDeFaltantes;
		}

		public PestanaCompletitud()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirFila(0, "Completitud.Jefes", EstadoCompletitud.Jefes, conLista: true);
			// Idea 8 del catalogo de funciones ("checklist de coleccionista"): la fila de Bestiario
			// ahora SI tiene lista - no de nombres de bichos (eso seguiria siendo spoiler, ver el
			// comentario real de EstadoCompletitud.Bestiario), sino del desglose real por bioma que
			// ese mismo metodo deja en ResumenCompletitud.Desglose.
			ConstruirFila(1, "Completitud.Bestiario", EstadoCompletitud.Bestiario, conLista: true);
			ConstruirFila(2, "Completitud.Logros", EstadoCompletitud.Logros, conLista: true);
			ConstruirFila(3, "Completitud.Investigacion", EstadoCompletitud.Investigacion, conLista: false);

			Refrescar();
		}

		private void ConstruirFila(int indice, string claveTitulo, System.Func<ResumenCompletitud> leer, bool conLista)
		{
			FilaResumen fila = new FilaResumen { Leer = leer, ClaveTitulo = claveTitulo, ConListaDeFaltantes = conLista };
			float arriba = indice * AltoFila;

			fila.Titulo = new EtiquetaTk(() => Idiomas.Texto(claveTitulo), 0.85f, 500f, 24f);
			fila.Titulo.ColorTexto = EstiloTk.TextoSuave;
			fila.Titulo.Left.Set(0f, 0f);
			fila.Titulo.Top.Set(arriba, 0f);
			Append(fila.Titulo);

			// Ancho fijo, anclado al borde derecho con un desplazamiento en PIXELES PUROS (-260,
			// 1f = "260px antes del 100% del ancho del padre") - NUNCA HAlign a la vez que un Left
			// con fraccion, que suma los dos desplazamientos y empuja el elemento fuera del marco
			// visible (mismo bug real que ya paso con VAlign+Top en PestanaConjuntos, visto de
			// nuevo aqui en la primera captura: los numeros no aparecian por ningun lado).
			fila.Numeros = new EtiquetaTk(() => fila.TextoNumeros, 0.8f, 260f, 24f);
			fila.Numeros.Left.Set(-260f, 1f);
			fila.Numeros.Top.Set(arriba, 0f);
			fila.Numeros.Centrado = false;
			Append(fila.Numeros);

			fila.Medidor = new MedidorPreparacionTk(() => UltimaFraccion(fila), 16f);
			fila.Medidor.Left.Set(0f, 0f);
			fila.Medidor.Width.Set(0f, 1f);
			fila.Medidor.Top.Set(arriba + 26f, 0f);
			Append(fila.Medidor);

			if (conLista) {
				fila.CajaFaltan = new UIPanel();
				fila.CajaFaltan.Left.Set(0f, 0f);
				fila.CajaFaltan.Width.Set(0f, 1f);
				fila.CajaFaltan.Top.Set(arriba + 48f, 0f);
				fila.CajaFaltan.Height.Set(AltoFila - 52f, 0f);
				fila.CajaFaltan.BackgroundColor = EstiloTk.FondoCaja;
				Append(fila.CajaFaltan);

				fila.ListaFaltan = new UIList();
				fila.ListaFaltan.Width.Set(-24f, 1f);
				fila.ListaFaltan.Height.Set(0f, 1f);
				fila.ListaFaltan.ListPadding = 3f;
				fila.CajaFaltan.Append(fila.ListaFaltan);

				UIScrollbar barra = new UIScrollbar();
				barra.HAlign = 1f;
				barra.Height.Set(0f, 1f);
				barra.SetView(100f, 1000f);
				fila.CajaFaltan.Append(barra);
				fila.ListaFaltan.SetScrollbar(barra);
			}

			_filas.Add(fila);
		}

		// Cache de la ultima lectura de cada fila (recalcular ResumenCompletitud recorre miles de
		// objetos para Investigacion/Bestiario - no tiene sentido hacerlo en cada DrawSelf del
		// medidor, que pide su fraccion cada fotograma).
		private readonly Dictionary<FilaResumen, ResumenCompletitud> _ultimaLectura = new Dictionary<FilaResumen, ResumenCompletitud>();

		private float UltimaFraccion(FilaResumen fila)
		{
			return _ultimaLectura.TryGetValue(fila, out ResumenCompletitud r) && r != null ? r.Fraccion : 0f;
		}

		private void Refrescar()
		{
			foreach (FilaResumen fila in _filas) {
				ResumenCompletitud resumen = fila.Leer();
				_ultimaLectura[fila] = resumen;

				fila.TextoNumeros = Idiomas.Texto("Completitud.Numeros", resumen.Hecho, resumen.Total,
					(int)(resumen.Fraccion * 100f));

				if (fila.ListaFaltan == null) {
					continue;
				}

				fila.ListaFaltan.Clear();

				// El desglose (idea 8, subtotales por categoria) tiene prioridad sobre "lo que
				// falta" cuando el resumen trae los dos rellenados con sentidos distintos - hoy
				// solo pasa con Bestiario, que trae Desglose y deja Faltan vacio a proposito.
				if (resumen.Desglose.Count > 0) {
					foreach (string linea in resumen.Desglose) {
						fila.ListaFaltan.Add(TextoFila(linea));
					}
					continue;
				}

				if (resumen.Faltan.Count == 0) {
					fila.ListaFaltan.Add(TextoFila(Idiomas.Texto("Completitud.Completo")));
					continue;
				}
				foreach (string nombre in resumen.Faltan) {
					fila.ListaFaltan.Add(TextoFila("- " + nombre));
				}
			}
		}

		private static EtiquetaTk TextoFila(string texto)
		{
			EtiquetaTk etiqueta = new EtiquetaTk(() => texto, 0.72f, 900f, 20f);
			etiqueta.Left.Set(4f, 0f);
			return etiqueta;
		}

		public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
		{
			base.Update(gameTime);

			// Recalcular las cuatro fuentes (sobre todo Investigacion, que recorre TODOS los
			// tipos de objeto) en cada fotograma seria trabajo de sobra para un numero que solo
			// cambia cuando el jugador mata algo, investiga algo o desbloquea un logro - ninguno
			// de los cuales pasa mas de una vez por fotograma. Cada 30 fotogramas (0,5s) es de
			// sobra para que se sienta "en vivo" sin recorrer miles de objetos 60 veces por
			// segundo.
			if (--_fotogramasHastaRefrescar <= 0) {
				_fotogramasHastaRefrescar = 30;
				Refrescar();
			}

			// Va FUERA del bloque de arriba, sin condiciones: la autoprueba lleva su propio
			// contador de espera (mismo patron que AutopruebaConjuntos) y no tiene que depender
			// del throttle de refresco visual.
			Common.Completitud.AutopruebaCompletitud.Avanzar(this);
		}

		/// <summary>Numero de filas de resumen construidas. Lo usa la autoprueba.</summary>
		internal int TotalFilasParaPrueba => _filas.Count;

		/// <summary>Ultimo resumen leido de la fila N. Lo usa la autoprueba para comprobar contra
		/// el dato real sin tener que leerlo dos veces por su cuenta.</summary>
		internal ResumenCompletitud ResumenParaPrueba(int indice) =>
			_ultimaLectura.TryGetValue(_filas[indice], out ResumenCompletitud r) ? r : null;

		internal void RefrescarParaPrueba() => Refrescar();
	}
}
