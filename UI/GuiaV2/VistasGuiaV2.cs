using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.GuiaV2;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.GuiaV2
{
	public partial class ContenidoGuiaV2
	{
		private const float FraccionColumnaIzquierda = 0.34f;

		/// <summary>Lista principal de la vista montada ahora (autoprueba).</summary>
		public ListaTk ListaPrincipal { get; private set; }

		/// <summary>Lista de la columna izquierda en Ruta/Manual (autoprueba).</summary>
		public ListaTk ListaIzquierda { get; private set; }

		private UIElement CrearVista(Vista v)
		{
			ListaIzquierda = null;
			switch (v) {
				case Vista.MiGuia: return VistaMiGuia();
				case Vista.Ruta: return VistaRuta();
				case Vista.Equipo: return VistaEquipo();
				case Vista.Manual: return VistaManual();
				case Vista.Perdido: return VistaFichas(GuiaV2Sistema.Doc.Problemas, "GuiaV2.Perdido.Titulo", "GuiaV2.Perdido.Intro");
				case Vista.Raro: return VistaFichas(GuiaV2Sistema.Doc.Hallazgos, "GuiaV2.Raro.Titulo", "GuiaV2.Raro.Intro");
				case Vista.Busqueda: return VistaBusqueda();
				default: return VistaMiGuia();
			}
		}

		private ListaTk UnaColumna(out UIPanel caja)
		{
			caja = CajaBase();
			ListaTk lista = ListaTk.ConScroll(caja);
			ListaPrincipal = lista;
			return lista;
		}

		private static TextoRicoTk Titulo(string texto, float escala = 0.92f)
		{
			return BloquesGuia.Texto("**" + texto + "**", escala, EstiloTk.TextoAviso, EstiloTk.TextoAviso);
		}

		private static TextoRicoTk Parrafo(string marcado, float escala = 0.78f, Color? color = null)
		{
			return BloquesGuia.Texto(marcado, escala, color ?? Color.White, null);
		}

		private static string Era(Parada p)
		{
			CapituloRuta c = GuiaV2Sistema.Doc.Capitulos.FirstOrDefault(x => x.Id == p.Capitulo);
			return c != null ? c.Titulo : p.Capitulo;
		}

		private static ResultadoParada ResultadoDe(string paradaId)
		{
			ResumenGuiaV2 r = GuiaV2Sistema.Resumen;
			return r != null ? r.Paradas.FirstOrDefault(p => p.Parada.Id == paradaId) : null;
		}

		// =========================================================================================
		// Mi guía
		// =========================================================================================

		private UIElement VistaMiGuia()
		{
			UIPanel caja;
			ListaTk lista = UnaColumna(out caja);
			GuiaV2Doc doc = GuiaV2Sistema.Doc;
			ResumenGuiaV2 r = GuiaV2Sistema.Resumen;

			// ---- cabecera: titulo de la guia y modo real ----
			lista.Add(BloquesGuia.Texto("**" + doc.Titulo + "**", 1.0f, Color.White, Color.White));
			lista.Add(Parrafo(Idiomas.Texto("GuiaV2.MiGuia.Partida", ModosLegibles(r != null ? r.ModosActivos : null),
				GuiaV2Sistema.NombreClase(GuiaV2Sistema.Clase), GuiaV2Sistema.ClaseElegidaAMano
					? Idiomas.Texto("GuiaV2.MiGuia.ClaseElegida") : Idiomas.Texto("GuiaV2.MiGuia.ClasePropuesta")), 0.72f, EstiloTk.TextoSuave));

			// ---- progreso ----
			if (r != null) {
				lista.Add(DibujoTk.Barra(() => GuiaV2Sistema.Resumen != null && GuiaV2Sistema.Resumen.Paradas.Count > 0
					? GuiaV2Sistema.Resumen.ParadasCompletadas / (float)GuiaV2Sistema.Resumen.Paradas.Count : 0f, EstiloTk.Correcto));
				lista.Add(Parrafo(Idiomas.Texto("GuiaV2.MiGuia.Progreso", r.ParadasCompletadas, r.Paradas.Count, r.TareasHechas, r.TareasTotales,
					r.Paradas.Count(p => p.Aplazada)), 0.72f, EstiloTk.TextoSuave));
			}

			// ---- tu siguiente parada ----
			ResultadoParada sig = r != null ? r.Siguiente : null;
			lista.Hueco(4f);
			PilaTk tarjeta = new PilaTk { Fondo = EstiloTk.BotonActivo * 0.28f, Borde = EstiloTk.BordeSobre * 0.45f, Relleno = 10f, Separacion = 4f };
			if (sig == null) {
				tarjeta.Append(Titulo(Idiomas.Texto("GuiaV2.MiGuia.RutaTerminada")));
				tarjeta.Append(Parrafo(Idiomas.Texto("GuiaV2.MiGuia.RutaTerminadaTexto")));
			}
			else {
				Parada p = sig.Parada;
				tarjeta.Append(BloquesGuia.Texto("**" + Idiomas.Texto("GuiaV2.MiGuia.Siguiente") + "**  ·  " +
					Idiomas.Texto("GuiaV2.Ruta.Numero", sig.Indice + 1, doc.Paradas.Count) + " · " + Era(p) +
					(string.IsNullOrEmpty(p.Etiqueta) ? "" : " · " + p.Etiqueta), 0.7f, EstiloTk.TextoAviso, EstiloTk.TextoAviso));
				tarjeta.Append(BloquesGuia.Texto("**" + p.Titulo + "**", 1.02f, Color.White, Color.White));
				if (p.VidaObjetivo != null) {
					tarjeta.Append(LineaVida(p.VidaObjetivo));
				}
				if (!string.IsNullOrEmpty(p.Donde)) {
					tarjeta.Append(Parrafo("**" + Idiomas.Texto("GuiaV2.Ruta.Donde") + "** " + p.Donde, 0.76f));
				}
				tarjeta.Append(LineaMapa(p, true));
				if (!string.IsNullOrEmpty(p.Preparate)) {
					tarjeta.Append(Parrafo(p.Preparate, 0.76f, EstiloTk.TextoSuave));
				}
				tarjeta.Append(Parrafo(Idiomas.Texto("GuiaV2.MiGuia.TareasDeParada", sig.TareasHechas, sig.Tareas.Count), 0.72f, EstiloTk.TextoSuave));

				FilaBotonesTk botones = new FilaBotonesTk(32f);
				botones.AnchoMaximo = 230f;
				string idSig = p.Id;
				botones.Anadir(Idiomas.Texto("GuiaV2.MiGuia.AbrirParada"), () => { ParadaSeleccionada = idSig; CambiarVista(Vista.Ruta); });
				if (UbicacionGuia.Siguiente != null && UbicacionGuia.Siguiente.ParadaId == idSig) {
					botones.Anadir(Idiomas.Texto("GuiaV2.VerEnMapa"), () => UbicacionGuia.VerEnElMapa(UbicacionGuia.Siguiente, false, "Mi guía"));
				}
				tarjeta.Append(botones);
			}
			lista.Add(tarjeta);

			if (sig != null) {
				Parada p = sig.Parada;
				// ---- la guia te guia: lo que te falta y como conseguirlo ----
				List<(string Ref, int Cantidad, string Motivo)> faltan = QueFalta(p);
				lista.Hueco(6f);
				lista.Add(Titulo(Idiomas.Texto("GuiaV2.MiGuia.TeFalta")));
				if (faltan.Count == 0) {
					lista.Add(Parrafo(Idiomas.Texto(p.Necesitas.Count == 0 && (p.Invocacion == null || p.Invocacion.Objeto == null)
						? "GuiaV2.MiGuia.NadaQueReunir" : "GuiaV2.MiGuia.LoTienesTodo"), 0.76f, EstiloTk.Correcto));
				}
				else {
					lista.Add(Parrafo(Idiomas.Texto("GuiaV2.MiGuia.TeFaltaAyuda"), 0.7f, EstiloTk.TextoSuave));
					foreach ((string Ref, int Cantidad, string Motivo) f in faltan) {
						lista.Add(new ObjetoFilaTk(f.Ref, f.Cantidad, f.Motivo));
					}
				}

				// ---- tareas pendientes ----
				List<ResultadoTarea> pendientes = sig.Tareas.Where(t => !t.Hecha).Take(4).ToList();
				if (pendientes.Count > 0) {
					lista.Hueco(6f);
					lista.Add(Titulo(Idiomas.Texto("GuiaV2.MiGuia.Pendientes")));
					foreach (ResultadoTarea t in pendientes) {
						lista.Add(new TareaFilaTk(t, sig.Tareas.IndexOf(t) + 1));
					}
				}

				// ---- avisos de esta parada para tu modo ----
				foreach (AvisoModo a in sig.AvisosActivos) {
					lista.Add(Aviso(a.Texto, EstiloTk.TextoAviso));
				}
			}

			// ---- tu etapa de equipo ----
			EtapaEscalera etapa = r != null ? GuiaV2Sistema.Evaluador.EtapaActual(GuiaV2Sistema.Clase, r) : null;
			if (etapa != null) {
				lista.Hueco(6f);
				lista.Add(Titulo(Idiomas.Texto("GuiaV2.MiGuia.TuEquipo", GuiaV2Sistema.NombreClase(GuiaV2Sistema.Clase))));
				lista.Add(Parrafo("**" + Idiomas.Texto("GuiaV2.Equipo.Momento") + "** " + etapa.Momento, 0.76f));
				List<OpcionEquipo> todas = etapa.Armas.Concat(etapa.Armadura).Concat(etapa.Accesorios).ToList();
				lista.Add(new RejillaObjetosTk(todas, doc.LeyendaEscaleras));
				FilaBotonesTk b = new FilaBotonesTk(30f);
				b.AnchoMaximo = 260f;
				b.Anadir(Idiomas.Texto("GuiaV2.MiGuia.VerEscalera"), () => CambiarVista(Vista.Equipo));
				lista.Add(b);
			}

			// ---- avisos generales del modo ----
			if (r != null && r.AvisosGenerales.Count > 0) {
				lista.Hueco(6f);
				lista.Add(Titulo(Idiomas.Texto("GuiaV2.MiGuia.Avisos")));
				foreach (AvisoModo a in r.AvisosGenerales) {
					lista.Add(Aviso(a.Texto, new Color(150, 205, 255)));
				}
			}

			// ---- accesos rapidos (los cuatro de la portada de la guia del usuario) ----
			lista.Hueco(6f);
			lista.Add(Titulo(Idiomas.Texto("GuiaV2.MiGuia.Brujula")));
			FilaBotonesTk rapidos = new FilaBotonesTk(32f, 0.74f);
			rapidos.Anadir(Idiomas.Texto("GuiaV2.Pestana.Perdido"), () => CambiarVista(Vista.Perdido));
			rapidos.Anadir(Idiomas.Texto("GuiaV2.Pestana.Raro"), () => CambiarVista(Vista.Raro));
			if (GuiaV2Sistema.ArticuloPorId("mapa") != null) {
				rapidos.Anadir(Idiomas.Texto("GuiaV2.MiGuia.Mapa"), () => { ArticuloSeleccionado = "mapa"; CambiarVista(Vista.Manual); });
			}
			if (doc.Articulos.Count > 0) {
				string primero = doc.Articulos[0].Id;
				rapidos.Anadir(Idiomas.Texto("GuiaV2.MiGuia.EmpiezaAqui"), () => { ArticuloSeleccionado = primero; CambiarVista(Vista.Manual); });
			}
			lista.Add(rapidos);

			// ---- referencia y creditos ----
			lista.Hueco(6f);
			lista.Add(Parrafo(string.IsNullOrEmpty(doc.Referencia.Calamity)
				? Idiomas.Texto("GuiaV2.MiGuia.ReferenciaVanilla", doc.Referencia.Terraria, doc.Referencia.FechaInvestigacion)
				: Idiomas.Texto("GuiaV2.MiGuia.Referencia", doc.Referencia.Terraria, doc.Referencia.Calamity, doc.Referencia.FechaInvestigacion),
				0.64f, EstiloTk.Neutro));
			if (!string.IsNullOrEmpty(doc.Creditos)) {
				lista.Add(Parrafo(doc.Creditos, 0.64f, EstiloTk.Neutro));
			}
			return caja;
		}

		/// <summary>Lo que te falta de verdad para una parada: invocador y "necesitas" (de tu clase)
		/// que no tienes en ningun sitio del personaje.</summary>
		public static List<(string Ref, int Cantidad, string Motivo)> QueFalta(Parada p)
		{
			List<(string, int, string)> salida = new List<(string, int, string)>();
			HashSet<string> vistos = new HashSet<string>();
			if (p.Invocacion != null && !string.IsNullOrEmpty(p.Invocacion.Objeto)) {
				Anadir(p.Invocacion.Objeto, 1, Idiomas.Texto("GuiaV2.Motivo.Invocador"));
			}
			foreach (ObjetoNecesario n in p.Necesitas) {
				if (!GuiaV2Evaluador.AplicaAClase(n.Clases, GuiaV2Sistema.Clase)) continue;
				Anadir(n.Ref, n.Cantidad, MotivoLegible(n.Motivo));
			}
			return salida;

			void Anadir(string rf, int cantidad, string motivo)
			{
				if (!vistos.Add(rf)) return;
				int tipo = GuiaV2Sistema.TipoObjeto(rf);
				if (tipo > 0 && ProveedorEstadoGuiaV2Mod.CuantosPoseeDe(Main.LocalPlayer, tipo) >= Math.Max(1, cantidad)) return;
				salida.Add((rf, Math.Max(1, cantidad), motivo));
			}
		}

		private static string MotivoLegible(string motivo)
		{
			if (string.IsNullOrEmpty(motivo)) return "";
			string clave = "GuiaV2.Motivo." + motivo;
			string t = Idiomas.Texto(clave);
			return t == clave || t.StartsWith("Mods.", StringComparison.Ordinal) ? motivo : t;
		}

		private static UIElement Aviso(string marcado, Color barra)
		{
			PilaTk caja = new PilaTk { Relleno = 7f, Fondo = EstiloTk.FondoCaja * 0.85f, BarraIzquierda = barra };
			caja.Append(Parrafo(marcado, 0.74f));
			return caja;
		}

		private static UIElement LineaVida(VidaObjetivo v)
		{
			return new TextoRicoTk(() => {
				int vida = GuiaV2Sistema.Proveedor.VidaMaxima;
				string etiqueta = Idiomas.Texto(v.TrasMejora ? "GuiaV2.Ruta.VidaTras" : "GuiaV2.Ruta.Vida");
				string estado = vida <= 0 ? "" : vida >= v.Min
					? " · " + Idiomas.Texto("GuiaV2.Ruta.VidaOk", vida)
					: " · " + Idiomas.Texto("GuiaV2.Ruta.VidaFalta", vida);
				return "**" + etiqueta + "** " + (string.IsNullOrEmpty(v.Texto) ? v.Min + "-" + v.Max : v.Texto) + estado;
			}, 0.76f);
		}

		/// <summary>"En tu mapa: ..." de una parada: si se puede situar en ESTE mundo, con que
		/// precision y hacia donde queda.</summary>
		private UIElement LineaMapa(Parada p, bool esSiguiente)
		{
			PilaTk pila = new PilaTk { Relleno = 0f, Separacion = 2f };
			UbicacionGuia.Objetivo o = esSiguiente && UbicacionGuia.Siguiente != null && UbicacionGuia.Siguiente.ParadaId == p.Id
				? UbicacionGuia.Siguiente : UbicacionGuia.ResolverParada(p);
			TextoRicoTk t = new TextoRicoTk(() => {
				UbicacionGuia.Objetivo actual = esSiguiente && UbicacionGuia.Siguiente != null && UbicacionGuia.Siguiente.ParadaId == p.Id
					? UbicacionGuia.Siguiente : o;
				if (actual == null) {
					return p.Ubicaciones.Count == 0 ? Idiomas.Texto("GuiaV2.Mapa.SinUbicacion") : Idiomas.Texto("GuiaV2.Mapa.NoExiste");
				}
				string precision = actual.Aproximada ? Idiomas.Texto("GuiaV2.Mapa.LineaAproximada", actual.Lugar)
					: Idiomas.Texto("GuiaV2.Mapa.LineaExacta", actual.Lugar);
				string marca = esSiguiente
					? (UbicacionGuia.Visible ? Idiomas.Texto("GuiaV2.Mapa.Marcada") : Idiomas.Texto("GuiaV2.Mapa.MarcaOculta"))
					: "";
				return "**" + Idiomas.Texto("GuiaV2.Mapa.EnTuMapa") + "** " + precision + " " + UbicacionGuia.DireccionDesdeJugador(actual) +
					(marca.Length > 0 ? " " + marca : "");
			}, 0.72f);
			t.ColorTexto = o == null ? EstiloTk.Neutro : (o.Aproximada ? EstiloTk.TextoAviso : EstiloTk.Correcto);
			t.ColorNegrita = EstiloTk.TextoSuave;
			pila.Append(t);
			if (o != null && !esSiguiente) {
				FilaBotonesTk b = new FilaBotonesTk(28f, 0.72f);
				b.AnchoMaximo = 200f;
				b.Anadir(Idiomas.Texto("GuiaV2.VerEnMapa"), () => UbicacionGuia.VerEnElMapa(o, true, "Ruta: parada " + p.Id));
				pila.Append(b);
			}
			return pila;
		}

		// =========================================================================================
		// Ruta
		// =========================================================================================

		/// <summary>Detalle de la parada en pantalla (autoprueba).</summary>
		public string ParadaEnPantalla { get; private set; }

		private UIElement VistaRuta()
		{
			GuiaV2Doc doc = GuiaV2Sistema.Doc;
			ResumenGuiaV2 r = GuiaV2Sistema.Resumen;
			if (ParadaSeleccionada == null || GuiaV2Sistema.ParadaPorId(ParadaSeleccionada) == null) {
				ParadaSeleccionada = r != null && r.Siguiente != null ? r.Siguiente.Parada.Id : doc.Paradas[0].Id;
			}

			UIElement raiz = new UIElement();

			UIPanel izq = CajaBase();
			izq.Width.Set(-4f, FraccionColumnaIzquierda);
			izq.Height.Set(0f, 1f);
			raiz.Append(izq);
			ListaTk listaIzq = ListaTk.ConScroll(izq);
			listaIzq.ListPadding = 3f;
			ListaIzquierda = listaIzq;

			UIPanel der = CajaBase();
			der.Left.Set(4f, FraccionColumnaIzquierda);
			der.Width.Set(-4f, 1f - FraccionColumnaIzquierda);
			der.Height.Set(0f, 1f);
			raiz.Append(der);
			ListaTk listaDer = ListaTk.ConScroll(der);
			ListaPrincipal = listaDer;

			// ---- columna izquierda: todas las paradas, agrupadas por era ----
			string capitulo = null;
			int indiceSeleccion = 0, n = 0;
			for (int i = 0; i < doc.Paradas.Count; i++) {
				Parada p = doc.Paradas[i];
				if (p.Capitulo != capitulo) {
					capitulo = p.Capitulo;
					CapituloRuta c = doc.Capitulos.FirstOrDefault(x => x.Id == capitulo);
					listaIzq.Add(BloquesGuia.Texto("**" + (c != null ? c.Titulo : capitulo) + "**", 0.74f, EstiloTk.TextoAviso, EstiloTk.TextoAviso));
					n++;
				}
				int numero = i + 1;
				string id = p.Id;
				FilaNavegableTk fila = new FilaNavegableTk(() => p.Titulo,
					() => string.IsNullOrEmpty(p.Etiqueta) ? Idiomas.Texto("GuiaV2.Tipo." + p.Tipo) : p.Etiqueta,
					() => { ParadaSeleccionada = id; CambiarVista(Vista.Ruta, true); }, 0.72f);
				fila.Clave = id;
				fila.Seleccionada = () => ParadaSeleccionada == id;
				fila.Marca = () => {
					ResultadoParada rp = ResultadoDe(id);
					if (rp == null) return numero.ToString();
					if (rp.Completada) return "ok";
					if (rp.Aplazada) return "--";
					return numero.ToString();
				};
				fila.ColorMarca = () => {
					ResultadoParada rp = ResultadoDe(id);
					if (rp != null && rp.Completada) return EstiloTk.Correcto;
					if (rp != null && rp.Aplazada) return EstiloTk.Neutro;
					ResumenGuiaV2 rr = GuiaV2Sistema.Resumen;
					return rr != null && rr.Siguiente != null && rr.Siguiente.Parada.Id == id ? EstiloTk.TextoAviso : EstiloTk.TextoSuave;
				};
				listaIzq.Add(fila);
				if (id == ParadaSeleccionada) {
					indiceSeleccion = n;
				}
				n++;
			}

			RellenarDetalle(listaDer, ParadaSeleccionada);

			// Lleva la columna izquierda hasta la parada seleccionada (aprox: 40 px por fila).
			_scrollIzqPendiente = Math.Max(0f, indiceSeleccion * 40f - 60f);
			return raiz;
		}

		private float _scrollIzqPendiente = -1f;

		protected override void DrawSelf(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (_scrollIzqPendiente >= 0f && ListaIzquierda != null && ListaIzquierda.Barra != null && ListaIzquierda.GetTotalHeight() > 0f) {
				ListaIzquierda.Barra.ViewPosition = _scrollIzqPendiente;
				_scrollIzqPendiente = -1f;
			}
		}

		private void RellenarDetalle(ListaTk lista, string paradaId)
		{
			lista.Clear();
			lista.IrArriba();
			GuiaV2Doc doc = GuiaV2Sistema.Doc;
			Parada p = GuiaV2Sistema.ParadaPorId(paradaId);
			if (p == null) {
				return;
			}
			ParadaEnPantalla = p.Id;
			int indice = doc.Paradas.IndexOf(p);
			ResultadoParada rp = ResultadoDe(p.Id);
			ClaseGuia clase = GuiaV2Sistema.Clase;

			// ---- cabecera ----
			PilaTk cab = new PilaTk { Fondo = EstiloTk.BotonActivo * 0.25f, Relleno = 10f, Separacion = 3f };
			cab.Append(BloquesGuia.Texto("**" + Idiomas.Texto("GuiaV2.Ruta.Numero", indice + 1, doc.Paradas.Count) + " · " + Era(p) + "**" +
				(string.IsNullOrEmpty(p.Etiqueta) ? "" : "  ·  " + p.Etiqueta), 0.7f, EstiloTk.TextoAviso, EstiloTk.TextoAviso));
			cab.Append(BloquesGuia.Texto("**" + p.Titulo + "**", 1.05f, Color.White, Color.White));
			cab.Append(new TextoRicoTk(() => {
				ResultadoParada x = ResultadoDe(p.Id);
				if (x == null) return "";
				string estado = x.Completada ? Idiomas.Texto(x.MarcadaAMano && !(x.CompletadaCuando != null && x.CompletadaCuando.Estado == EstadoCondicion.Cumplida)
						? "GuiaV2.Ruta.HechaAMano" : "GuiaV2.Ruta.Hecha")
					: x.Aplazada ? Idiomas.Texto("GuiaV2.Ruta.Aplazada") : Idiomas.Texto("GuiaV2.Ruta.Pendiente");
				return estado + " · " + Idiomas.Texto("GuiaV2.Ruta.TareasHechas", x.TareasHechas, x.Tareas.Count);
			}, 0.7f) { ColorTexto = EstiloTk.TextoSuave });
			if (p.VidaObjetivo != null) {
				cab.Append(LineaVida(p.VidaObjetivo));
			}
			if (!string.IsNullOrEmpty(p.Donde)) {
				cab.Append(Parrafo("**" + Idiomas.Texto("GuiaV2.Ruta.Donde") + "** " + p.Donde, 0.76f));
			}
			cab.Append(LineaMapa(p, rp != null && GuiaV2Sistema.Resumen != null && GuiaV2Sistema.Resumen.Siguiente == rp));
			if (p.Jefes.Count > 0) {
				cab.Append(Parrafo(string.Join("   ", p.Jefes.Select(j => "{n:" + j + "}")), 0.84f));
			}
			lista.Add(cab);

			if (rp != null) {
				foreach (AvisoModo a in rp.AvisosActivos) {
					lista.Add(Aviso(a.Texto, EstiloTk.TextoAviso));
				}
			}

			// ---- como empezar este encuentro ----
			if (p.Invocacion != null && (p.Invocacion.Objeto != null || !string.IsNullOrEmpty(p.Invocacion.Donde) || !string.IsNullOrEmpty(p.Invocacion.Notas))) {
				PilaTk inv = new PilaTk { Fondo = EstiloTk.FondoCaja * 0.9f, Relleno = 8f, Separacion = 3f, BarraIzquierda = new Color(150, 205, 255) };
				inv.Append(Titulo(Idiomas.Texto("GuiaV2.Ruta.ComoEmpezar"), 0.84f));
				if (!string.IsNullOrEmpty(p.Invocacion.Objeto)) {
					inv.Append(new ObjetoFilaTk(p.Invocacion.Objeto, 1, Idiomas.Texto("GuiaV2.Motivo.Invocador")));
				}
				if (!string.IsNullOrEmpty(p.Invocacion.Donde)) inv.Append(Parrafo(p.Invocacion.Donde, 0.76f));
				if (!string.IsNullOrEmpty(p.Invocacion.Notas)) inv.Append(Parrafo(p.Invocacion.Notas, 0.74f, EstiloTk.TextoSuave));
				lista.Add(inv);
			}

			// ---- 1 · Preparate ----
			lista.Hueco(4f);
			lista.Add(Titulo(Idiomas.Texto("GuiaV2.Ruta.Preparate")));
			if (!string.IsNullOrEmpty(p.Preparate)) {
				lista.Add(Parrafo(p.Preparate));
			}
			string matiz;
			if (p.PreparatePorClase.TryGetValue(GuiaV2Evaluador.ClaveClase(clase), out matiz) && !string.IsNullOrEmpty(matiz)) {
				lista.Add(Parrafo("**" + Idiomas.Texto("GuiaV2.Ruta.ParaTuClase", GuiaV2Sistema.NombreClase(clase)) + "** " + matiz, 0.76f));
			}
			List<ObjetoNecesario> necesitas = p.Necesitas.Where(x => GuiaV2Evaluador.AplicaAClase(x.Clases, clase)).ToList();
			if (necesitas.Count > 0) {
				lista.Add(Parrafo("**" + Idiomas.Texto("GuiaV2.Ruta.Necesitas") + "**", 0.76f));
				foreach (ObjetoNecesario o in necesitas) {
					lista.Add(new ObjetoFilaTk(o.Ref, o.Cantidad, MotivoLegible(o.Motivo)));
				}
			}
			EtapaEscalera etapa = GuiaV2Sistema.Evaluador.EtapaParaParada(clase, p.Id);
			if (etapa != null) {
				// Agrupado y rotulado igual que la vista Equipo (Armas / Armadura por conjunto /
				// Accesorios / Otros): antes era UNA rejilla de ~30 iconos mezclados y sin rotulos,
				// imposible de leer sin pasar el raton por cada uno (revision de capturas de F3b).
				PilaTk equipo = new PilaTk { Fondo = EstiloTk.FondoCaja * 0.75f, Relleno = 9f, Separacion = 4f };
				equipo.Append(Parrafo("**" + Idiomas.Texto("GuiaV2.Ruta.EquipoRecomendado", GuiaV2Sistema.NombreClase(clase)) + "** " + etapa.Momento, 0.76f));
				AnadirGrupo(equipo, "GuiaV2.Equipo.Armas", etapa.Armas, doc);
				AnadirArmadura(equipo, etapa.Armadura, doc);
				AnadirGrupo(equipo, "GuiaV2.Equipo.Accesorios", etapa.Accesorios, doc);
				AnadirGrupo(equipo, "GuiaV2.Equipo.Otros", etapa.Otros, doc);
				if (!string.IsNullOrEmpty(etapa.Nota)) {
					equipo.Append(Parrafo("**" + Idiomas.Texto("GuiaV2.Equipo.Despues") + "** " + etapa.Nota, 0.72f, EstiloTk.TextoSuave));
				}
				lista.Add(equipo);
			}

			// ---- 2 · Haz esto, en este orden ----
			if (rp != null && rp.Tareas.Count > 0) {
				lista.Hueco(4f);
				lista.Add(Titulo(Idiomas.Texto("GuiaV2.Ruta.HazEsto")));
				for (int i = 0; i < rp.Tareas.Count; i++) {
					lista.Add(new TareaFilaTk(rp.Tareas[i], i + 1));
				}
			}

			// ---- 3 · Durante el combate / la exploracion ----
			if (!string.IsNullOrEmpty(p.Combate)) {
				lista.Hueco(4f);
				lista.Add(Titulo(Idiomas.Texto("GuiaV2.Ruta.Combate")));
				lista.Add(Parrafo(p.Combate));
			}
			// ---- 4 · Lo que acaba de desbloquearse ----
			if (!string.IsNullOrEmpty(p.Desbloquea)) {
				lista.Hueco(4f);
				lista.Add(Titulo(Idiomas.Texto("GuiaV2.Ruta.Desbloquea")));
				lista.Add(Parrafo(p.Desbloquea));
			}
			// ---- Listo para seguir cuando… ----
			if (!string.IsNullOrEmpty(p.ListoCuando)) {
				lista.Hueco(4f);
				PilaTk listo = new PilaTk { Fondo = new Color(90, 75, 30) * 0.55f, Relleno = 9f, Separacion = 3f, BarraIzquierda = EstiloTk.TextoAviso };
				listo.Append(Titulo(Idiomas.Texto("GuiaV2.Ruta.ListoCuando"), 0.84f));
				listo.Append(Parrafo(p.ListoCuando, 0.78f));
				lista.Add(listo);
			}
			// ---- No vendas / conserva ----
			if (!string.IsNullOrEmpty(p.Conserva)) {
				lista.Hueco(4f);
				lista.Add(Titulo(Idiomas.Texto("GuiaV2.Ruta.Conserva"), 0.84f));
				lista.Add(Parrafo(p.Conserva, 0.76f));
				if (p.ConservaObjetos.Count > 0) {
					lista.Add(new RejillaObjetosTk(p.ConservaObjetos));
				}
			}
			UIElement fuentes = BloquesGuia.Fuentes(p.Fuentes);
			if (fuentes != null) {
				lista.Hueco(4f);
				lista.Add(fuentes);
			}

			// ---- acciones ----
			lista.Hueco(6f);
			FilaBotonesTk acciones = new FilaBotonesTk(32f, 0.76f);
			BotonTk hecha = acciones.Anadir("", () => {
				ResultadoParada x = ResultadoDe(p.Id);
				GuiaV2Sistema.MarcarParada(p.Id, x == null || !x.MarcadaAMano);
				GuiaV2Sistema.Reevaluar();
			});
			hecha.Clave = "hecha";
			_botonHecha = hecha;
			if (p.Opcional) {
				BotonTk aplazar = acciones.Anadir("", () => {
					ResultadoParada x = ResultadoDe(p.Id);
					GuiaV2Sistema.AplazarParada(p.Id, x == null || !x.Aplazada);
					GuiaV2Sistema.Reevaluar();
				});
				aplazar.Clave = "aplazar";
				_botonAplazar = aplazar;
			}
			else {
				_botonAplazar = null;
			}
			if (indice > 0) {
				string ant = doc.Paradas[indice - 1].Id;
				acciones.Anadir(Idiomas.Texto("GuiaV2.Ruta.Anterior"), () => { ParadaSeleccionada = ant; CambiarVista(Vista.Ruta, true); });
			}
			if (indice < doc.Paradas.Count - 1) {
				string sigId = doc.Paradas[indice + 1].Id;
				acciones.Anadir(Idiomas.Texto("GuiaV2.Ruta.SiguienteBoton"), () => { ParadaSeleccionada = sigId; CambiarVista(Vista.Ruta, true); });
			}
			_paradaBotones = p.Id;
			lista.Add(acciones);
			lista.Add(Parrafo(Idiomas.Texto("GuiaV2.Ruta.NotaHecha"), 0.64f, EstiloTk.Neutro));
		}

		private BotonTk _botonHecha;
		private BotonTk _botonAplazar;
		private string _paradaBotones;

		/// <summary>Textos vivos de "Hecha"/"Aplazar" (cambian al pulsarlos sin rehacer la vista).</summary>
		private void ActualizarBotonesParada()
		{
			if (_paradaBotones == null || VistaActual != Vista.Ruta) {
				return;
			}
			ResultadoParada x = ResultadoDe(_paradaBotones);
			if (_botonHecha != null) {
				bool auto = x != null && x.CompletadaCuando != null && x.CompletadaCuando.Estado == EstadoCondicion.Cumplida;
				_botonHecha.FijarTexto(Idiomas.Texto(auto ? "GuiaV2.Ruta.BotonHechaAuto" : (x != null && x.MarcadaAMano ? "GuiaV2.Ruta.BotonDeshacer" : "GuiaV2.Ruta.BotonHecha")));
				_botonHecha.Habilitado = !auto || (x != null && x.MarcadaAMano);
			}
			if (_botonAplazar != null) {
				_botonAplazar.FijarTexto(Idiomas.Texto(x != null && x.Aplazada ? "GuiaV2.Ruta.BotonRetomar" : "GuiaV2.Ruta.BotonAplazar"));
			}
		}

		protected override void DrawChildren(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
		{
			ActualizarBotonesParada();
			base.DrawChildren(spriteBatch);
		}

		// =========================================================================================
		// Equipo
		// =========================================================================================

		private UIElement VistaEquipo()
		{
			UIPanel caja;
			ListaTk lista = UnaColumna(out caja);
			GuiaV2Doc doc = GuiaV2Sistema.Doc;
			ClaseGuia clase = GuiaV2Sistema.Clase;
			EscaleraClase escalera = doc.Escaleras.FirstOrDefault(e => e.Clase == GuiaV2Evaluador.ClaveClase(clase));

			lista.Add(Titulo(Idiomas.Texto("GuiaV2.Equipo.Titulo", GuiaV2Sistema.NombreClase(clase)), 1.0f));
			lista.Add(Parrafo(Idiomas.Texto("GuiaV2.Equipo.Intro"), 0.72f, EstiloTk.TextoSuave));
			if (escalera == null || escalera.Etapas.Count == 0) {
				lista.Add(Parrafo(Idiomas.Texto("GuiaV2.Equipo.SinEscalera"), 0.78f, EstiloTk.Neutro));
				return caja;
			}
			EtapaEscalera actual = GuiaV2Sistema.Resumen != null ? GuiaV2Sistema.Evaluador.EtapaActual(clase, GuiaV2Sistema.Resumen) : null;
			int indiceActual = 0, i = 0;
			foreach (EtapaEscalera e in escalera.Etapas) {
				bool esActual = actual != null && e.Id == actual.Id;
				if (esActual) indiceActual = lista.Count;
				PilaTk tarjeta = new PilaTk {
					Fondo = esActual ? EstiloTk.BotonActivo * 0.3f : EstiloTk.FondoCaja * 0.75f,
					Borde = esActual ? EstiloTk.BordeSobre * 0.6f : (Color?)null, Relleno = 9f, Separacion = 4f,
				};
				Parada desde = GuiaV2Sistema.ParadaPorId(e.Desde);
				tarjeta.Append(BloquesGuia.Texto("**" + e.Momento + "**" + (esActual ? "   " + Idiomas.Texto("GuiaV2.Equipo.Ahora") : "") +
					(desde != null ? "  ·  " + Idiomas.Texto("GuiaV2.Equipo.Desde") + " {p:" + desde.Id + "}" : ""), 0.84f,
					Color.White, esActual ? EstiloTk.TextoAviso : Color.White));
				AnadirGrupo(tarjeta, "GuiaV2.Equipo.Armas", e.Armas, doc);
				AnadirArmadura(tarjeta, e.Armadura, doc);
				AnadirGrupo(tarjeta, "GuiaV2.Equipo.Accesorios", e.Accesorios, doc);
				AnadirGrupo(tarjeta, "GuiaV2.Equipo.Otros", e.Otros, doc);
				if (!string.IsNullOrEmpty(e.Nota)) {
					tarjeta.Append(Parrafo("**" + Idiomas.Texto("GuiaV2.Equipo.Despues") + "** " + e.Nota, 0.72f, EstiloTk.TextoSuave));
				}
				UIElement f = BloquesGuia.Fuentes(e.Fuentes);
				if (f != null) tarjeta.Append(f);
				lista.Add(tarjeta);
				i++;
			}
			// Leyenda de marcas.
			if (doc.LeyendaEscaleras.Count > 0) {
				lista.Hueco(4f);
				lista.Add(Titulo(Idiomas.Texto("GuiaV2.Equipo.Leyenda"), 0.8f));
				foreach (KeyValuePair<string, string> par in doc.LeyendaEscaleras) {
					lista.Add(Parrafo("**" + par.Key + "** " + par.Value, 0.7f, EstiloTk.TextoSuave));
				}
			}
			_indiceEquipoActual = indiceActual;
			return caja;
		}

		private int _indiceEquipoActual = -1;

		private static void AnadirGrupo(PilaTk tarjeta, string clave, List<OpcionEquipo> opciones, GuiaV2Doc doc)
		{
			if (opciones == null || opciones.Count == 0) return;
			tarjeta.Append(Parrafo("**" + Idiomas.Texto(clave) + "**", 0.72f, EstiloTk.TextoSuave));
			tarjeta.Append(new RejillaObjetosTk(opciones, doc.LeyendaEscaleras));
		}

		private static void AnadirArmadura(PilaTk tarjeta, List<OpcionEquipo> opciones, GuiaV2Doc doc)
		{
			if (opciones == null || opciones.Count == 0) return;
			tarjeta.Append(Parrafo("**" + Idiomas.Texto("GuiaV2.Equipo.Armadura") + "**", 0.72f, EstiloTk.TextoSuave));
			foreach (IGrouping<string, OpcionEquipo> g in opciones.GroupBy(o => o.Conjunto ?? "")) {
				if (g.Key.Length > 0) {
					tarjeta.Append(Parrafo(g.Key, 0.68f, EstiloTk.Neutro));
				}
				tarjeta.Append(new RejillaObjetosTk(g, doc.LeyendaEscaleras));
			}
		}

		// =========================================================================================
		// Manual
		// =========================================================================================

		public string ArticuloEnPantalla { get; private set; }

		private UIElement VistaManual()
		{
			GuiaV2Doc doc = GuiaV2Sistema.Doc;
			if (ArticuloSeleccionado == null || GuiaV2Sistema.ArticuloPorId(ArticuloSeleccionado) == null) {
				ArticuloSeleccionado = doc.Articulos.Count > 0 ? doc.Articulos[0].Id : null;
			}
			UIElement raiz = new UIElement();
			UIPanel izq = CajaBase();
			izq.Width.Set(-4f, 0.3f);
			izq.Height.Set(0f, 1f);
			raiz.Append(izq);
			ListaTk listaIzq = ListaTk.ConScroll(izq);
			listaIzq.ListPadding = 3f;
			ListaIzquierda = listaIzq;

			UIPanel der = CajaBase();
			der.Left.Set(4f, 0.3f);
			der.Width.Set(-4f, 0.7f);
			der.Height.Set(0f, 1f);
			raiz.Append(der);
			ListaTk listaDer = ListaTk.ConScroll(der);
			ListaPrincipal = listaDer;

			foreach (Articulo a in doc.Articulos) {
				string id = a.Id;
				FilaNavegableTk fila = new FilaNavegableTk(() => a.Titulo, null, () => {
					ArticuloSeleccionado = id;
					RellenarArticulo(listaDer, id);
				}, 0.72f);
				fila.Clave = id;
				fila.Seleccionada = () => ArticuloSeleccionado == id;
				listaIzq.Add(fila);
			}
			RellenarArticulo(listaDer, ArticuloSeleccionado);
			return raiz;
		}

		private void RellenarArticulo(ListaTk lista, string id)
		{
			lista.Clear();
			lista.IrArriba();
			Articulo a = GuiaV2Sistema.ArticuloPorId(id);
			if (a == null) return;
			ArticuloEnPantalla = id;
			lista.Add(BloquesGuia.Texto("**" + a.Titulo + "**", 1.05f, Color.White, Color.White));
			if (!string.IsNullOrEmpty(a.Subtitulo)) {
				lista.Add(Parrafo(a.Subtitulo, 0.74f, EstiloTk.TextoSuave));
			}
			BloquesGuia.Anadir(e => lista.Add(e), a.Bloques);
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: articulo \"" + id + "\" (" + a.Bloques.Count + " bloques).");
		}

		// =========================================================================================
		// Estoy perdido / He encontrado algo raro
		// =========================================================================================

		private UIElement VistaFichas(List<EntradaFicha> fichas, string claveTitulo, string claveIntro)
		{
			UIPanel caja;
			ListaTk lista = UnaColumna(out caja);
			lista.Add(Titulo(Idiomas.Texto(claveTitulo), 1.0f));
			lista.Add(Parrafo(Idiomas.Texto(claveIntro), 0.72f, EstiloTk.TextoSuave));
			foreach (EntradaFicha f in fichas) {
				PilaTk tarjeta = new PilaTk { Fondo = EstiloTk.FondoCaja * 0.8f, Relleno = 9f, Separacion = 3f };
				tarjeta.Append(BloquesGuia.Texto("**" + f.Titulo + "**", 0.86f, Color.White, EstiloTk.TextoAviso));
				BloquesGuia.Anadir(e => tarjeta.Append(e), f.Bloques);
				List<string> enlaces = new List<string>();
				foreach (string p in f.Paradas) enlaces.Add("{p:" + p + "}");
				foreach (string z in f.Zonas) enlaces.Add("{z:" + z + "}");
				if (enlaces.Count > 0) {
					tarjeta.Append(Parrafo("**" + Idiomas.Texto("GuiaV2.Relacionado") + "** " + string.Join(" · ", enlaces), 0.72f, EstiloTk.TextoSuave));
				}
				UIElement fu = BloquesGuia.Fuentes(f.Fuentes);
				if (fu != null) tarjeta.Append(fu);
				lista.Add(tarjeta);
			}
			return caja;
		}

		// =========================================================================================
		// Busqueda
		// =========================================================================================

		public int ResultadosBusqueda { get; private set; }

		private UIElement VistaBusqueda()
		{
			UIPanel caja;
			ListaTk lista = UnaColumna(out caja);
			GuiaV2Doc doc = GuiaV2Sistema.Doc;
			string q = GramaticaBusqueda.Plegar(_busquedaMontada);
			string[] palabras = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			Func<string, bool> casa = texto => {
				string t = GramaticaBusqueda.Plegar(GuiaV2Sistema.PlanoLocal(texto));
				return palabras.All(w => t.Contains(w));
			};
			int total = 0;
			const int Maximo = 80;

			lista.Add(Titulo(Idiomas.Texto("GuiaV2.Busqueda.Titulo", _busquedaMontada), 0.96f));

			// Paradas
			foreach (Parada p in doc.Paradas) {
				if (total >= Maximo) break;
				string dondeCasa = null;
				if (casa(p.Titulo)) dondeCasa = Idiomas.Texto("GuiaV2.Busqueda.EnTitulo");
				else if (p.Tareas.Any(t => casa(t.Texto))) dondeCasa = Idiomas.Texto("GuiaV2.Busqueda.EnTareas");
				else if (casa(p.Preparate + " " + p.Combate + " " + p.Desbloquea + " " + p.ListoCuando + " " + p.Conserva + " " + p.Donde))
					dondeCasa = Idiomas.Texto("GuiaV2.Busqueda.EnTexto");
				if (dondeCasa == null) continue;
				string id = p.Id;
				string sub = Idiomas.Texto("GuiaV2.Busqueda.Parada") + " · " + Era(p) + " · " + dondeCasa;
				FilaNavegableTk f = new FilaNavegableTk(() => p.Titulo, () => sub, () => { ParadaSeleccionada = id; LimpiarBusqueda(); CambiarVista(Vista.Ruta); });
				f.Clave = "parada:" + id;
				f.Marca = () => "P";
				lista.Add(f);
				total++;
			}
			// Articulos y fichas
			foreach (Articulo a in doc.Articulos) {
				if (total >= Maximo) break;
				if (!casa(a.Titulo) && !a.Bloques.Any(b => casa(TextoDeBloque(b)))) continue;
				string id = a.Id;
				FilaNavegableTk f = new FilaNavegableTk(() => a.Titulo, () => Idiomas.Texto("GuiaV2.Busqueda.Articulo"), () => { ArticuloSeleccionado = id; LimpiarBusqueda(); CambiarVista(Vista.Manual); });
				f.Clave = "articulo:" + id;
				f.Marca = () => "M";
				lista.Add(f);
				total++;
			}
			foreach ((List<EntradaFicha> fichas, Vista vista, string clave) in new[] {
				(doc.Problemas, Vista.Perdido, "GuiaV2.Pestana.Perdido"), (doc.Hallazgos, Vista.Raro, "GuiaV2.Pestana.Raro") }) {
				foreach (EntradaFicha e in fichas) {
					if (total >= Maximo) break;
					if (!casa(e.Titulo) && !e.Bloques.Any(b => casa(TextoDeBloque(b)))) continue;
					Vista destino = vista;
					FilaNavegableTk f = new FilaNavegableTk(() => e.Titulo, () => Idiomas.Texto(clave), () => { LimpiarBusqueda(); CambiarVista(destino); });
					f.Marca = () => "?";
					lista.Add(f);
					total++;
				}
			}
			// Zonas
			foreach (Zona z in doc.Zonas) {
				if (total >= Maximo) break;
				if (!casa(z.Nombre) && !casa(z.Resumen)) continue;
				string id = z.Id;
				FilaNavegableTk f = new FilaNavegableTk(() => z.Nombre, () => Idiomas.Texto("GuiaV2.Busqueda.Zona"), () => AbrirFichaZona(id));
				f.Marca = () => "Z";
				lista.Add(f);
				total++;
			}
			// Objetos citados por la guia (tabla de referencias)
			if (GuiaV2Sistema.Refs != null) {
				foreach (KeyValuePair<string, RefObjeto> par in GuiaV2Sistema.Refs.Objetos) {
					if (total >= Maximo) break;
					string nombre = Idiomas.EnEspanol ? par.Value.Es : par.Value.En;
					string plegado = GramaticaBusqueda.Plegar(nombre + " " + par.Value.En);
					if (!palabras.All(w => plegado.Contains(w))) continue;
					if (GuiaV2Sistema.TipoObjeto(par.Key) <= 0) continue;
					string rf = par.Key;
					lista.Add(new ObjetoFilaTk(rf, 1, null));
					total++;
				}
			}
			if (total == 0) {
				lista.Add(Parrafo(Idiomas.Texto("GuiaV2.Busqueda.Nada"), 0.78f, EstiloTk.Neutro));
			}
			else if (total >= Maximo) {
				lista.Add(Parrafo(Idiomas.Texto("GuiaV2.Busqueda.Muchos", Maximo), 0.7f, EstiloTk.Neutro));
			}
			ResultadosBusqueda = total;
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: busqueda \"" + _busquedaMontada + "\" -> " + total + " resultados.");
			return caja;
		}

		private void LimpiarBusqueda()
		{
			_buscador.FijarTextoSilencioso("");
			_busquedaMontada = "";
		}

		private static string TextoDeBloque(Bloque b)
		{
			return b.Titulo + " " + b.Texto + " " + string.Join(" ", b.Items) + " " +
				string.Join(" ", b.Filas.Select(f => string.Join(" ", f))) + " " + string.Join(" ", b.Bloques.Select(TextoDeBloque));
		}
	}
}
