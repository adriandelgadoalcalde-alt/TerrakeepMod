using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.GuiaV2;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Guia;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.GuiaV2
{
	/// <summary>
	/// Pestaña "Guía" del panel (Guía v2, 02-oct-2026): la misma estructura que la Guía de Terrakeep
	/// de escritorio y que la guia HTML del usuario, adaptada a la UI nativa del juego.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Sub-pestañas: <b>Mi guía</b> (siguiente parada, lo que te falta, progreso, avisos del modo,
	/// tu etapa de equipo), <b>Ruta</b> (las paradas con todas sus secciones), <b>Equipo</b> (la
	/// escalera de tu clase), <b>Manual</b> (capitulos largos), <b>Estoy perdido</b>, <b>Algo
	/// raro</b> y <b>Brújula</b> (la guia v1 de siempre, con el entrenador de jefe y la guia de
	/// grupo, que la v2 no sustituye). Arriba: progreso, selector de clase y buscador.
	/// </para>
	/// <para>
	/// El contenido es el MISMO que el del escritorio (incrustado en Terrakeep.Core.dll); el estado
	/// es el de la partida EN VIVO (<see cref="GuiaV2Sistema"/>). Pulsar cualquier objeto abre su
	/// ficha "cómo conseguirlo" (receta con estacion y si ya la tienes, botin, bolsa, vendedor o
	/// "en el mundo"), y desde ahi "Coger en la Librería" lleva a la Librería con el objeto ya
	/// buscado (punto 3 del encargo del usuario).
	/// </para>
	/// </remarks>
	public partial class ContenidoGuiaV2 : UIElement
	{
		private const float AltoCabecera = 30f;
		private const float AltoPestanas = 30f;
		private const float Separacion = 6f;

		public enum Vista { MiGuia = 0, Ruta, Equipo, Manual, Perdido, Raro, Brujula, Busqueda }

		private static readonly string[] ClavesPestana = { "MiGuia", "Ruta", "Equipo", "Manual", "Perdido", "Raro", "Brujula" };

		/// <summary>Donde estaba el jugador la ultima vez (sobrevive a cerrar y abrir el panel).</summary>
		public static Vista UltimaVista = Vista.MiGuia;
		public static string ParadaSeleccionada;
		public static string ArticuloSeleccionado;

		private readonly UIElement _contenedor;
		private readonly FilaBotonesTk _pestanas;
		private readonly BotonTk _botonClase;
		private readonly CampoTextoTk _buscador;
		private readonly EtiquetaTk _resumen;
		private UIElement _vistaActual;
		private VentanaGuiaTk _ventana;
		private readonly List<(string Ref, int Cantidad)> _historialFichas = new List<(string, int)>();
		private int _versionMontada = -1;
		private string _busquedaMontada = "";

		public Vista VistaActual { get; private set; }

		/// <summary>La guia v1 (Brújula) cuando esa sub-pestaña esta abierta; la usan sus autopruebas.</summary>
		public ContenidoGuia Brujula => _vistaActual as ContenidoGuia;

		/// <summary>La ventana modal abierta ahora mismo (ficha de objeto o de zona), o null.</summary>
		public VentanaGuiaTk Ventana => _ventana != null && _ventana.Parent != null ? _ventana : null;

		/// <summary>Referencia del objeto cuya ficha esta abierta (autoprueba).</summary>
		public string FichaObjetoAbierta { get; private set; }

		public ContenidoGuiaV2()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			// ---- cabecera: progreso · clase · buscador ----
			_resumen = new EtiquetaTk(TextoResumen, 0.74f, 900f, AltoCabecera);
			_resumen.ColorTexto = EstiloTk.TextoSuave;
			_resumen.Width.Set(-400f, 1f);
			_resumen.Top.Set(4f, 0f);
			Append(_resumen);

			_botonClase = new BotonTk("", 0.74f);
			_botonClase.Width.Set(176f, 0f);
			_botonClase.Height.Set(AltoCabecera, 0f);
			_botonClase.Left.Set(-392f, 1f);
			_botonClase.Ayuda = () => Idiomas.Texto("GuiaV2.ClaseAyuda", GuiaV2Sistema.NombreClase(GuiaV2Sistema.ClasePropuesta()));
			_botonClase.AlPulsar += SiguienteClase;
			Append(_botonClase);

			_buscador = new CampoTextoTk(() => Idiomas.Texto("GuiaV2.BuscarPista"), 40, 0.74f);
			_buscador.Width.Set(208f, 0f);
			_buscador.Height.Set(AltoCabecera, 0f);
			_buscador.Left.Set(-208f, 1f);
			_buscador.AlCambiar += t => Buscar(t);
			Append(_buscador);

			// ---- sub-pestañas ----
			_pestanas = new FilaBotonesTk(AltoPestanas, 0.78f);
			_pestanas.Top.Set(AltoCabecera + Separacion, 0f);
			for (int i = 0; i < ClavesPestana.Length; i++) {
				Vista v = (Vista)i;
				BotonTk b = _pestanas.Anadir(Idiomas.Texto("GuiaV2.Pestana." + ClavesPestana[i]), () => CambiarVista(v));
				b.EsPestana = true;
			}
			Append(_pestanas);

			_contenedor = new UIElement();
			_contenedor.Width.Set(0f, 1f);
			_contenedor.Top.Set(AltoCabecera + AltoPestanas + Separacion * 2f, 0f);
			_contenedor.Height.Set(-(AltoCabecera + AltoPestanas + Separacion * 2f), 1f);
			Append(_contenedor);

			NavegacionGuia.AbrirObjeto = AbrirFichaObjeto;
			NavegacionGuia.AbrirZona = AbrirFichaZona;
			NavegacionGuia.AbrirParada = id => { ParadaSeleccionada = id; CambiarVista(Vista.Ruta); };
			NavegacionGuia.AbrirArticulo = id => { ArticuloSeleccionado = id; CambiarVista(Vista.Manual); };

			if (GuiaV2Sistema.Resumen == null && GuiaV2Sistema.HayGuia) {
				GuiaV2Sistema.Reevaluar();
			}

			Vista inicial = UltimaVista;
			// La autoprueba de la guia v1 recorre la Brujula: se abre ahi para no tocarla.
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaGuia.Variable)) ||
				!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaGrupo.VariableObservador)) ||
				!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AutopruebaGrupo.VariableCompanero))) {
				inicial = Vista.Brujula;
			}
			CambiarVista(inicial);
		}

		private string TextoResumen()
		{
			if (!GuiaV2Sistema.HayGuia) {
				return Idiomas.Texto("GuiaV2.SinGuiaCorto");
			}
			ResumenGuiaV2 r = GuiaV2Sistema.Resumen;
			string titulo = Idiomas.Texto(GuiaV2Sistema.IdGuia == "calamity" ? "GuiaV2.Ambito.Calamity" : "GuiaV2.Ambito.Vanilla");
			if (r == null) {
				return titulo;
			}
			return Idiomas.Texto("GuiaV2.Resumen", titulo, r.ParadasCompletadas, r.Paradas.Count, r.TareasHechas, r.TareasTotales);
		}

		public static string ModosLegibles(IReadOnlyList<string> modos)
		{
			if (modos == null || modos.Count == 0) {
				return Idiomas.Texto("GuiaV2.Modo.SinMundo");
			}
			List<string> s = new List<string>();
			foreach (string m in modos) {
				s.Add(Idiomas.Texto("GuiaV2.Modo." + m));
			}
			return string.Join(" + ", s);
		}

		private void SiguienteClase()
		{
			List<ClaseGuia> clases = GuiaV2Sistema.ClasesDisponibles();
			if (clases.Count == 0) {
				return;
			}
			int i = clases.IndexOf(GuiaV2Sistema.Clase);
			GuiaV2Sistema.ElegirClase(clases[(i + 1) % clases.Count]);
			GuiaV2Sistema.Reevaluar();
			if (VistaActual != Vista.Brujula && VistaActual != Vista.Busqueda) {
				CambiarVista(VistaActual, true);
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			_botonClase.FijarTexto(Idiomas.Texto("GuiaV2.ClaseBoton", GuiaV2Sistema.NombreClase(GuiaV2Sistema.Clase)));
			_botonClase.Habilitado = GuiaV2Sistema.HayGuia;
			// Con todo un mismo ancho a 1280x720 y UIScale alta, la cabecera se aprieta: el resumen
			// cede primero (se mide y se baja su escala), nunca se solapa con el selector.
			_resumen.EscalaTexto = Math.Max(0.55f, Math.Min(0.74f, (_resumen.GetDimensions().Width - 6f) /
				Math.Max(1f, FontAssets.MouseText.Value.MeasureString(_resumen.TextoActual).X)));

			for (int i = 0; i < _pestanas.Botones.Count; i++) {
				_pestanas.Botones[i].Activo = (int)VistaActual == i;
			}

			// Mi guía se rehace cuando cambia la evaluacion (siguiente parada, tareas hechas...).
			if (VistaActual == Vista.MiGuia && _versionMontada != GuiaV2Sistema.Version && Ventana == null) {
				CambiarVista(Vista.MiGuia, true);
			}
		}

		/// <summary>Cambia de sub-pestaña (publico: navegacion y autoprueba).</summary>
		public void CambiarVista(Vista v, bool conservarScroll = false)
		{
			float scroll = 0f;
			ListaTk listaVieja = conservarScroll ? PrimeraLista(_vistaActual) : null;
			if (listaVieja != null && listaVieja.Barra != null) {
				scroll = listaVieja.Barra.ViewPosition;
			}

			if (_vistaActual != null) {
				_contenedor.RemoveChild(_vistaActual);
			}
			VistaActual = v;
			if (v != Vista.Busqueda) {
				UltimaVista = v;
			}
			_versionMontada = GuiaV2Sistema.Version;

			UIElement nueva;
			if (v == Vista.Brujula) {
				nueva = new ContenidoGuia();
			}
			else if (!GuiaV2Sistema.HayGuia) {
				nueva = VistaSinGuia();
			}
			else {
				try {
					nueva = CrearVista(v);
				}
				catch (Exception e) {
					RegistroGuia.Error(Terrakeep.LogTag + " Guia v2: EXCEPCION montando la vista " + v + ": " + e);
					nueva = VistaError(e.Message);
				}
			}
			nueva.Width.Set(0f, 1f);
			nueva.Height.Set(0f, 1f);
			_vistaActual = nueva;
			_contenedor.Append(nueva);
			_contenedor.Recalculate();

			if (conservarScroll) {
				_scrollIzqPendiente = -1f;
				ListaTk listaNueva = PrimeraLista(nueva);
				if (listaNueva != null && listaNueva.Barra != null) {
					listaNueva.Recalculate();
					listaNueva.Barra.ViewPosition = scroll;
				}
			}
			else {
				RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: sub-pestaña \"" + v + "\"" +
					(v == Vista.Ruta ? " (parada " + (ParadaSeleccionada ?? "-") + ")" : "") +
					(v == Vista.Manual ? " (articulo " + (ArticuloSeleccionado ?? "-") + ")" : "") + ".");
			}
		}

		private static ListaTk PrimeraLista(UIElement raiz)
		{
			if (raiz == null) return null;
			ListaTk encontrada = null;
			raiz.ExecuteRecursively(e => { if (encontrada == null && e is ListaTk l) encontrada = l; });
			return encontrada;
		}

		private UIElement VistaSinGuia()
		{
			UIPanel caja = CajaBase();
			ListaTk lista = ListaTk.ConScroll(caja);
			string motivo = GuiaV2Sistema.MotivoSinGuia;
			lista.Add(BloquesGuia.Texto("**" + Idiomas.Texto("GuiaV2.SinGuiaTitulo") + "**", 0.95f, EstiloTk.TextoAviso, EstiloTk.TextoAviso));
			lista.Add(BloquesGuia.Texto(Idiomas.Texto(motivo == "error" ? "GuiaV2.SinGuiaError" : "GuiaV2.SinGuiaVanilla"), 0.8f, Color.White, null));
			FilaBotonesTk b = new FilaBotonesTk(32f);
			b.AnchoMaximo = 260f;
			b.Anadir(Idiomas.Texto("GuiaV2.IrABrujula"), () => CambiarVista(Vista.Brujula));
			lista.Add(b);
			return caja;
		}

		private UIElement VistaError(string mensaje)
		{
			UIPanel caja = CajaBase();
			ListaTk lista = ListaTk.ConScroll(caja);
			lista.Add(BloquesGuia.Texto(Idiomas.Texto("GuiaV2.ErrorVista", mensaje), 0.8f, EstiloTk.Peligro, null));
			return caja;
		}

		public static UIPanel CajaBase()
		{
			UIPanel caja = new UIPanel();
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(10f);
			return caja;
		}

		// ---- buscador -----------------------------------------------------------------------------

		private void Buscar(string texto)
		{
			string t = (texto ?? "").Trim();
			if (t.Length >= 2) {
				_busquedaMontada = t;
				CambiarVista(Vista.Busqueda);
			}
			else if (VistaActual == Vista.Busqueda) {
				CambiarVista(UltimaVista);
			}
		}

		/// <summary>Fija el texto del buscador desde codigo (autoprueba) y busca.</summary>
		public void FijarBusqueda(string texto)
		{
			_buscador.FijarTextoSilencioso(texto ?? "");
			Buscar(texto);
		}

		// ---- ventanas: ficha de objeto y ficha de zona -------------------------------------------

		private void CerrarVentana()
		{
			if (_ventana != null && _ventana.Parent != null) {
				RemoveChild(_ventana);
			}
			_ventana = null;
			FichaObjetoAbierta = null;
		}

		/// <summary>Abre la ficha "cómo conseguirlo" de un objeto (desde cualquier enlace o fila).</summary>
		public void AbrirFichaObjeto(string referencia, int cantidad)
		{
			AbrirFichaObjeto(referencia, cantidad, true);
		}

		private void AbrirFichaObjeto(string referencia, int cantidad, bool apilar)
		{
			if (string.IsNullOrEmpty(referencia)) {
				return;
			}
			if (apilar && FichaObjetoAbierta != null) {
				_historialFichas.Add((FichaObjetoAbierta, 1));
			}
			else if (apilar) {
				_historialFichas.Clear();
			}
			CerrarVentana();

			ObtencionGuia.Ficha ficha = ObtencionGuia.Construir(referencia, cantidad);
			string sub = SubtituloObjeto(referencia, ficha.Tipo);
			VentanaGuiaTk v = new VentanaGuiaTk(() => "**" + GuiaV2Sistema.NombreObjeto(referencia) + "**", () => sub);
			v.IconoObjeto = () => ficha.Tipo;

			foreach (ObtencionGuia.Linea l in ficha.Lineas) {
				if (l.EsTitulo) {
					v.Lista.Hueco(4f);
				}
				TextoRicoTk t = BloquesGuia.Texto(l.EsTitulo ? "**" + l.Texto + "**" : l.Texto, l.Escala, l.Color, l.EsTitulo ? l.Color : (Color?)null);
				v.Lista.Add(t);
			}

			// Usos dentro de la guia: en que paradas aparece (para entender POR QUE lo quieres).
			List<string> usos = ParadasQueCitan(referencia);
			if (usos.Count > 0) {
				v.Lista.Hueco(4f);
				v.Lista.Add(BloquesGuia.Texto("**" + Idiomas.Texto("GuiaV2.Ficha.EnLaGuia") + "**", 0.86f, EstiloTk.TextoAviso, EstiloTk.TextoAviso));
				List<string> enlaces = new List<string>();
				foreach (string p in usos) {
					enlaces.Add("{p:" + p + "}");
				}
				v.Lista.Add(BloquesGuia.Texto(string.Join(" · ", enlaces), 0.76f, Color.White, null));
			}

			if (ficha.Tipo > 0) {
				BotonTk libreria = v.Botones.Anadir(Idiomas.Texto("GuiaV2.Ficha.CogerEnLibreria"), () => CogerEnLaLibreria(referencia, ficha.Tipo));
				libreria.Ayuda = () => Idiomas.Texto("GuiaV2.Ficha.CogerEnLibreriaAyuda");
			}
			if (ficha.PideAltar) {
				v.Botones.Anadir(Idiomas.Texto("GuiaV2.Ficha.VerAltar"), () => {
					(int X, int Y)? a = MundoGuiaVivo.AltarMasCercano();
					if (a.HasValue) {
						UbicacionGuia.VerEnElMapa(new UbicacionGuia.Objetivo {
							Tile = new Vector2(a.Value.X, a.Value.Y), Titulo = GuiaV2Sistema.NombreEstacion("Terraria/Tile/DemonAltar"),
							Lugar = GuiaV2Sistema.NombreEstacion("Terraria/Tile/DemonAltar"), Origen = "punto:altar",
						}, true, "ficha de objeto: altar mas cercano");
					}
					else {
						Main.NewText(Idiomas.Texto("GuiaV2.Ficha.SinAltar"), EstiloTk.TextoAviso);
					}
				});
			}
			if (_historialFichas.Count > 0) {
				v.Botones.Anadir(Idiomas.Texto("GuiaV2.Atras"), () => {
					(string Ref, int Cantidad) previa = _historialFichas[_historialFichas.Count - 1];
					_historialFichas.RemoveAt(_historialFichas.Count - 1);
					AbrirFichaObjeto(previa.Ref, previa.Cantidad, false);
				});
			}
			v.AnadirCerrar();
			v.AlCerrar += () => { _ventana = null; FichaObjetoAbierta = null; _historialFichas.Clear(); };

			_ventana = v;
			FichaObjetoAbierta = referencia;
			Append(v);
			Recalculate();
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: FICHA de objeto abierta: " + referencia + " (tipo " + ficha.Tipo +
				", " + ficha.Lineas.Count + " lineas, altar=" + ficha.PideAltar + "): " + LineasPlanas(ficha));
		}

		private static string LineasPlanas(ObtencionGuia.Ficha f)
		{
			List<string> s = new List<string>();
			foreach (ObtencionGuia.Linea l in f.Lineas) {
				s.Add(GuiaV2Sistema.PlanoLocal(l.Texto));
			}
			return string.Join(" | ", s);
		}

		private static string SubtituloObjeto(string referencia, int tipo)
		{
			RefObjeto o = null;
			if (GuiaV2Sistema.Refs != null) {
				GuiaV2Sistema.Refs.Objetos.TryGetValue(referencia, out o);
			}
			string origen = referencia.StartsWith("Terraria/", StringComparison.Ordinal) ? "Terraria" : referencia.Split('/')[0];
			string otroIdioma = o != null ? (Idiomas.EnEspanol ? o.En : o.Es) : "";
			return origen + (tipo > 0 ? " · ID " + tipo : "") + (otroIdioma.Length > 0 ? " · " + otroIdioma : "") +
				(o != null && o.FuenteEs == "sin traduccion" ? " · " + Idiomas.Texto("GuiaV2.Ficha.SinTraduccion") : "");
		}

		private static List<string> ParadasQueCitan(string referencia)
		{
			List<string> salida = new List<string>();
			if (GuiaV2Sistema.Doc == null) {
				return salida;
			}
			string token = "{o:" + referencia;
			foreach (Parada p in GuiaV2Sistema.Doc.Paradas) {
				bool cita = (p.Invocacion != null && p.Invocacion.Objeto == referencia) || p.ConservaObjetos.Contains(referencia);
				foreach (ObjetoNecesario n in p.Necesitas) cita |= n.Ref == referencia;
				foreach (Tarea t in p.Tareas) cita |= t.Texto.Contains(token);
				cita |= p.Preparate.Contains(token) || p.Desbloquea.Contains(token) || p.Conserva.Contains(token);
				if (cita) {
					salida.Add(p.Id);
				}
				if (salida.Count >= 8) break;
			}
			return salida;
		}

		/// <summary>
		/// Atajo "Coger en la Librería": abre la pestaña Librería con el objeto ya buscado. Busca por
		/// su NOMBRE en el idioma del juego (lo que el jugador reconoce); si el nombre no sirve para
		/// la gramatica de la Librería (coma = O, menos de 2 letras), por su id exacto (#id).
		/// </summary>
		public void CogerEnLaLibreria(string referencia, int tipo)
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null || tipo <= 0) {
				return;
			}
			string nombre = Lang.GetItemNameValue(tipo);
			string consulta = string.IsNullOrEmpty(nombre) || nombre.IndexOf(',') >= 0 || nombre.Trim().Length < 2 ? "#" + tipo : nombre;
			CerrarVentana();
			panel.CambiarArea(AreaTerrakeep.Libreria, "Guía v2: Coger en la Librería (" + referencia + ")");
			ArbolLibreria.ConstruirSiHaceFalta();
			if (panel.Libreria != null) {
				panel.Libreria.IrALaRaiz();
				panel.Libreria.FijarBusqueda(consulta);
			}
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: COGER EN LA LIBRERIA " + referencia + " (tipo " + tipo +
				") -> busqueda \"" + consulta + "\", libreria abierta=" + (panel.Libreria != null) + ".");
		}

		/// <summary>Ficha de una zona (bioma, capa o estructura) con su resumen, su contenido y
		/// "Ver en el mapa" sobre el mundo real.</summary>
		public void AbrirFichaZona(string id)
		{
			Zona z = GuiaV2Sistema.ZonaPorId(id);
			if (z == null) {
				return;
			}
			CerrarVentana();
			_historialFichas.Clear();
			VentanaGuiaTk v = new VentanaGuiaTk(() => "**" + z.Nombre + "**",
				() => Idiomas.Texto("GuiaV2.Zona.Sub", Idiomas.Texto("GuiaV2.Capa." + z.Capa)));
			if (!string.IsNullOrEmpty(z.Resumen)) {
				v.Lista.Add(BloquesGuia.Texto(z.Resumen, 0.8f, Color.White, null));
			}
			BloquesGuia.Anadir(e => v.Lista.Add(e), z.Bloques);
			UbicacionGuia.Objetivo o = UbicacionGuia.ResolverZona(id);
			v.Lista.Hueco(4f);
			v.Lista.Add(BloquesGuia.Texto(o == null
				? Idiomas.Texto("GuiaV2.Zona.NoExiste")
				: (o.Aproximada ? Idiomas.Texto("GuiaV2.Zona.Aproximada") : Idiomas.Texto("GuiaV2.Zona.Situada")) + " " + UbicacionGuia.DireccionDesdeJugador(o),
				0.76f, o == null ? EstiloTk.Neutro : EstiloTk.Correcto, null));
			UIElement fuentes = BloquesGuia.Fuentes(z.Fuentes);
			if (fuentes != null) {
				v.Lista.Add(fuentes);
			}
			if (o != null) {
				v.Botones.Anadir(Idiomas.Texto("GuiaV2.VerEnMapa"), () => UbicacionGuia.VerEnElMapa(o, true, "ficha de zona " + id));
			}
			v.AnadirCerrar();
			v.AlCerrar += () => { _ventana = null; };
			_ventana = v;
			Append(v);
			Recalculate();
			RegistroGuia.Linea(Terrakeep.LogTag + " Guia v2: FICHA de zona abierta: " + id + " -> " +
				(o != null ? "(" + (int)o.Tile.X + ", " + (int)o.Tile.Y + ") aproximada=" + o.Aproximada : "no existe en este mundo") + ".");
		}
	}
}
