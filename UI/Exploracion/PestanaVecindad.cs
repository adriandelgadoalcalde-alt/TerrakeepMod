using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.UI.Guia;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// <b>Idea 5 del catálogo de funciones ("Planificador de felicidad de NPCs"):</b> la felicidad
	/// REAL de cada NPC de pueblo activo ahora mismo, leyendo <c>Main.ShopHelper</c> - el mismo
	/// motor con el que vanilla calcula lo que un vecino te contesta a "¿cómo te sientes aquí?" -
	/// para todos a la vez, sin tener que hablar uno a uno.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por qué es de solo lectura, nunca mueve ni reasigna a nadie.</b> Investigado ANTES de
	/// escribir código (disciplina de dos fases; mismo criterio real ya aplicado por el proyecto
	/// hermano TerrakeepTrainer con el personaje real: nunca tocar datos de partida sin poder
	/// verificarlo con cuidado). <c>ShopHelper.ProcessMood</c> (decompilado,
	/// <c>Terraria.GameContent.ShopHelper</c>) resuelve la preferencia de bioma de cada NPC con
	/// <c>BiomePreferenceListTrait.ModifyShopPrice</c>, que llama a
	/// <c>preference.Biome.IsInBiome(info.player)</c> - el bioma del JUGADOR ahora mismo, leído de
	/// sus propios flags de zona (<c>Player.ZoneForest</c> y compañía, ya calculados por el motor
	/// cada fotograma en la posición REAL del jugador). No hay ninguna sobrecarga pública que
	/// acepte "calcula el bioma como si estuvieras en la posición X" - la única forma de leer la
	/// felicidad que tendría un NPC en OTRA casa sería teletransportar de verdad al jugador (o
	/// reimplementar desde cero la detección de bioma del motor, con su propio riesgo real de
	/// desincronizarse de la versión real si vanilla la cambia). <b>LÍMITE REAL</b>: por eso esta
	/// pestaña muestra la felicidad de cada NPC EXACTAMENTE como la calcula el juego ahora mismo
	/// (precisa de verdad para los que están cerca; los lejanos llevan un aviso honesto en vez de
	/// fingir precisión que no hay), y no propone recolocaciones automáticas - hacerlo bien
	/// exigiría la capacidad de "simular" una posición sin moverla de verdad, que no existe como
	/// API seguraaquí.
	/// </para>
	/// <para>
	/// <b>Radio de confianza.</b> <see cref="RadioTilesFiable"/> (60 tiles, generoso: una casa
	/// vanilla completa cabe de sobra) - dentro de ese radio del NPC, se asume que el jugador está
	/// literalmente en su casa o muy cerca, así que el bioma que lee el motor SÍ es el real de esa
	/// vivienda. Más lejos, el informe puede estar leyendo el bioma de donde esté el jugador AHORA,
	/// no el de la casa del NPC - se avisa en vez de callarlo.
	/// </para>
	/// </remarks>
	public class PestanaVecindad : UIElement
	{
		/// <summary>Tiles dentro de los cuales se confía en que el bioma leído es el real de la
		/// casa del NPC (ver el XMLdoc de la clase).</summary>
		private const float RadioTilesFiable = 60f;

		private const int FotogramasEntreRefrescos = 30;

		/// <summary>
		/// Reapertura de BUG 3 (28-sep-2026): el arreglo del 26-sep congelaba la distancia en la
		/// instantanea SOLO dentro de <see cref="RadioTilesFiable"/> (<c>fiable ? -1 : ...</c>) - fuera
		/// de ese radio seguia comparando por el tile ENTERO exacto (<c>(int)distanciaTiles</c>), y los
		/// NPC de pueblo caminan solos sin parar (IA de vanilla). Confirmado con evidencia real medida
		/// (arnes <c>DiagnosticoInvestigador3Bugs.cs</c> PASO8/PASO9, jugador alejado &gt;60 tiles de
		/// verdad): <b>29 reconstrucciones completas en 30 segundos</b> (practicamente CADA ciclo de
		/// <see cref="FotogramasEntreRefrescos"/>, de principio a fin del tramo en que el NPC estuvo
		/// caminando) - el mismo parpadeo que BUG3 ya arreglo para el caso cercano, ahora en el caso
		/// COMUN (vecino lejos/paseando) en vez del raro que se probo entonces. Cuantizar a multiplos de
		/// esta constante (en vez de al tile exacto) hace falta que el NPC se mueva una distancia
		/// REALMENTE significativa para la UI (nadie necesita saber si esta a 87 o 88 tiles) antes de
		/// contar como cambio real - sin tocar la logica de reconstruccion en si (opcion A, ya elegida
		/// por el arreglo original: seguir comparando instantaneas completas, nunca reconciliar filas
		/// sueltas, ver bitacora.md "BUG 3"). Medido en vivo, mismo arnes/mismo NPC caminando, jugador
		/// alejado 90 tiles 30s seguidos: paso=3 bajo de 29 a 10 reconstrucciones en 30s (un NPC
		/// caminando cruza un bucket de 3 tiles casi cada ciclo de refresco - mejora real pero todavia
		/// muy perceptible); <b>8</b> (el valor real elegido) lo baja a SOLO 3 reconstrucciones en 30s,
		/// muy espaciadas entre si (~18-20s de diferencia, ya no "cada ciclo") - la etiqueta "Lejos"
		/// sigue redondeando al tile, el usuario nunca necesita saber si un vecino esta a 41 u a 47
		/// tiles, solo se refresca con mucha menos frecuencia. Ver bitacora.md para las 3 mediciones
		/// completas (sin cuantizar / paso=3 / paso=8).
		/// </summary>
		private const float PasoDistanciaLejosTiles = 8f;

		private UIPanel _caja;
		private UIList _lista;
		private UIScrollbar _scroll;
		private EtiquetaTk _resumen;
		private BotonTk _botonMarcar;
		private DesplegableTk _traer;
		private int _contadorRefresco;

		/// <summary>Tipos de la lista oficial que no viven ahora mismo en el mundo (ver
		/// <see cref="VecindadEditable.Faltan"/>), recalculados con el mismo temporizador que la lista
		/// - nunca cada fotograma - para el desplegable "Traer vecino".</summary>
		private List<int> _faltan = new List<int>();
		private int _totalNpcs;

		/// <summary>
		/// Ultimo "resumen" real de lo que se enseño (nombre+precio+informe de animo+aviso de
		/// casa/distancia de CADA NPC, en el mismo orden ya estable de <see cref="Refrescar"/>) -
		/// permite comparar antes de reconstruir. Investigado (hueco de cobertura real, ver
		/// bitacora.md "BUG 3"): <c>Refrescar()</c> se llamaba cada <see cref="FotogramasEntreRefrescos"/>
		/// SIN condicion de cambio real, y por como clampea <c>UIScrollbar.SetView</c> cada
		/// <c>UIList.Add()</c> (decompilado), reconstruir la lista entera sin necesidad reseteaba el
		/// scroll del usuario y creaba parpadeo de identidad de objeto (evidencia real: 3
		/// reconstrucciones en 95 fotogramas SIN tocar nada). Null = todavia no se ha construido
		/// ninguna instantanea (fuerza la primera construccion real).
		/// </summary>
		private string _instantaneaAnterior;

		/// <summary>Datos ya calculados de un vecino para un refresco - se calculan UNA sola vez
		/// por NPC (nunca dos, ni para comparar ni para pintar la fila) para que la instantanea de
		/// comparacion y lo que de verdad se pinta nunca puedan divergir.</summary>
		private struct DatosNpc
		{
			public NPC Npc;
			public ShoppingSettings Ajustes;
			public float DistanciaTiles;
			public bool Fiable;
			public bool SinCasa;
		}

		public PestanaVecindad()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			_resumen = new EtiquetaTk(() => Idiomas.Texto("Exploracion.Vecindad.Resumen", _totalNpcs),
				0.8f, 700f, 22f);
			// Ancho en fraccion (lo que dejan libre los dos controles de la derecha), no 700 fijos:
			// con el desplegable "Traer vecino" la cabecera ya no tiene sitio para una caja tan ancha.
			_resumen.Width.Set(-(AnchoControlCabecera * 2f + SeparacionCabecera * 2f), 1f);
			_resumen.ColorTexto = EstiloTk.TextoSuave;
			Append(_resumen);

			// Paridad con Terrakeep escritorio 3.3.0 (commit ec916e8b): traer un vecino de la lista
			// oficial que todavia no vive en el mundo. La primera opcion es el propio rotulo; elegir
			// cualquier otra lo trae al punto de aparicion, sin casa (ver VecindadEditable).
			_traer = new DesplegableTk(OpcionesTraer, () => 0, ElegirTraer, 0.72f);
			_traer.Width.Set(AnchoControlCabecera, 0f);
			_traer.Height.Set(24f, 0f);
			_traer.HAlign = 1f;
			_traer.Left.Set(-(AnchoControlCabecera + SeparacionCabecera), 0f);
			_traer.BotonToggle.Ayuda = () => VecindadEditable.MotivoParaNoPoder()
				?? Idiomas.Texto("Exploracion.Vecindad.TraerAyuda", _faltan.Count);
			Append(_traer);

			// Re-lectura literal del catalogo (20-sep-2026): la idea 5 pedia ADEMAS "marcar las
			// casas en el minimapa", pieza real que faltaba (las "recolocaciones" siguen siendo el
			// LIMITE REAL ya documentado arriba - esto es distinto: no simula nada, solo pinta
			// donde YA esta de verdad cada casa). Reutiliza el mismo sistema real de marcadores
			// que ya usa la pestaña "Búsqueda" (MarcadoresExploracion.Fijar +
			// CapaMapaExploracion), nunca un mecanismo aparte.
			_botonMarcar = new BotonTk(Idiomas.Texto("Exploracion.Vecindad.MarcarEnMapa"), 0.72f);
			_botonMarcar.Width.Set(AnchoControlCabecera, 0f);
			_botonMarcar.Height.Set(24f, 0f);
			_botonMarcar.HAlign = 1f;
			_botonMarcar.Ayuda = () => Idiomas.Texto("Exploracion.Vecindad.MarcarEnMapaAyuda");
			_botonMarcar.AlPulsar += MarcarCasasEnElMapa;
			Append(_botonMarcar);

			_caja = new UIPanel();
			_caja.Width.Set(0f, 1f);
			_caja.Top.Set(28f, 0f);
			_caja.Height.Set(-28f, 1f);
			_caja.BackgroundColor = EstiloTk.FondoCaja;
			_caja.BorderColor = new Color(0, 0, 0, 0);
			_caja.SetPadding(6f);
			Append(_caja);

			_lista = new UIList();
			_lista.Width.Set(-24f, 1f);
			_lista.Height.Set(0f, 1f);
			_lista.ListPadding = 6f;
			// Mismo motivo real que ya documentan ContenidoGuia/EditorPrefijoTk: List.Sort no es
			// estable y UIList lo usa por defecto - con varios NPC reales la lista saldría
			// barajada.
			_lista.ManualSortMethod = elementos => { };
			_caja.Append(_lista);

			_scroll = new UIScrollbar();
			_scroll.Width.Set(16f, 0f);
			_scroll.Height.Set(0f, 1f);
			_scroll.HAlign = 1f;
			_caja.Append(_scroll);
			_lista.SetScrollbar(_scroll);

			Refrescar();
		}

		private const float AnchoControlCabecera = 200f;
		private const float SeparacionCabecera = 8f;

		private IReadOnlyList<string> OpcionesTraer()
		{
			var opciones = new List<string>(_faltan.Count + 1);
			opciones.Add(_faltan.Count == 0
				? Idiomas.Texto("Exploracion.Vecindad.TraerNinguno")
				: Idiomas.Texto("Exploracion.Vecindad.Traer"));
			foreach (int tipo in _faltan) {
				opciones.Add(VecindadEditable.NombreDeTipo(tipo));
			}
			return opciones;
		}

		private void ElegirTraer(int indice)
		{
			if (indice <= 0 || indice > _faltan.Count) {
				return;
			}
			if (VecindadEditable.Traer(_faltan[indice - 1], "Exploración > Vecindad > Traer vecino")) {
				_contadorRefresco = FotogramasEntreRefrescos; // refresco inmediato, sin esperar al temporizador
			}
		}

		/// <summary>Tipos que faltan ahora mismo, para la autoprueba.</summary>
		public IReadOnlyList<int> FaltanParaPrueba => _faltan;

		/// <summary>El desplegable "Traer vecino", para la autoprueba.</summary>
		public DesplegableTk DesplegableTraer => _traer;

		public override void Update(GameTime gameTime)
		{
			_traer.BotonToggle.Habilitado = VecindadEditable.MotivoParaNoPoder() == null;
			base.Update(gameTime);
			if (++_contadorRefresco >= FotogramasEntreRefrescos) {
				_contadorRefresco = 0;
				Refrescar();
			}
		}

		/// <summary>Vuelve a leer todos los NPC de pueblo activos y su felicidad real, pero SOLO
		/// reconstruye la lista visual (<c>_lista.Clear()</c> + filas nuevas) cuando lo que se
		/// enseñaría de verdad cambió desde el último refresco - ver <see cref="_instantaneaAnterior"/>.
		/// Público para que la autoprueba pueda forzarlo sin esperar el temporizador.</summary>
		public void Refrescar()
		{
			_faltan = MundoActual.HayMundo ? VecindadEditable.Faltan() : new List<int>();
			Player jugador = Main.LocalPlayer;
			if (jugador == null) {
				// Sin jugador no hay nada real que leer (ni ShopHelper ni posición) - se limpia una
				// sola vez, nunca en bucle, para no generar el mismo parpadeo que se está arreglando.
				if (_totalNpcs != 0 || _lista.Count != 0) {
					_lista.Clear();
					_totalNpcs = 0;
				}
				_instantaneaAnterior = null;
				return;
			}

			List<NPC> vecinos = new List<NPC>();
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active || !npc.townNPC) {
					continue;
				}
				if (NPCID.Sets.IsTownPet[npc.type]) {
					continue;
				}
				vecinos.Add(npc);
			}

			// Orden estable por nombre real - nunca por el orden interno de Main.npc[], que cambia
			// de una partida a otra sin ningún significado para el jugador.
			vecinos.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

			// Datos reales de CADA vecino, calculados una sola vez (nunca dos: la instantánea de
			// comparación y la fila pintada usan exactamente los mismos valores, así no pueden
			// divergir entre sí).
			List<DatosNpc> datos = new List<DatosNpc>(vecinos.Count);
			string instantanea = vecinos.Count == 0 ? "0" : "";
			foreach (NPC npc in vecinos) {
				ShoppingSettings ajustes = Main.ShopHelper.GetShoppingSettings(jugador, npc);
				float distanciaTiles = Vector2.Distance(jugador.Center, npc.Center) / 16f;
				bool fiable = distanciaTiles <= RadioTilesFiable;
				bool sinCasa = npc.homeTileX < 0 && npc.homeTileY < 0;
				datos.Add(new DatosNpc {
					Npc = npc, Ajustes = ajustes, DistanciaTiles = distanciaTiles, Fiable = fiable, SinCasa = sinCasa
				});

				// Todo lo que de verdad se enseña en pantalla por cada NPC (mismo texto/condiciones
				// que AnadirFilaNpc pinta abajo) - si nada de esto cambia, no hay ningún motivo real
				// para tirar la lista y reconstruirla. Distancia "no fiable" CUANTIZADA a multiplos de
				// PasoDistanciaLejosTiles (ver su XMLdoc): el NPC tiene que alejarse/acercarse una
				// cantidad real antes de que cuente como cambio, en vez de cualquier tile exacto que
				// cruce su propio paseo constante.
				int distanciaParaInstantanea = fiable ? -1
					: (int)(System.Math.Round(distanciaTiles / PasoDistanciaLejosTiles) * PasoDistanciaLejosTiles);
				instantanea += npc.whoAmI + "|" + npc.FullName + "|" +
					(int)System.Math.Round(ajustes.PriceAdjustment * 100.0) + "|" +
					ajustes.HappinessReport + "|" + sinCasa + "|" +
					distanciaParaInstantanea + ";";
			}

			if (instantanea == _instantaneaAnterior) {
				return;
			}
			_instantaneaAnterior = instantanea;

			_lista.Clear();
			_totalNpcs = vecinos.Count;
			if (vecinos.Count == 0) {
				_lista.Add(NuevaLinea(() => Idiomas.Texto("Exploracion.Vecindad.Ninguno"), EstiloTk.TextoSuave, 0.8f));
				return;
			}

			foreach (DatosNpc d in datos) {
				AnadirFilaNpc(d.Npc, d.Ajustes, d.DistanciaTiles, d.Fiable, d.SinCasa);
			}
		}

		/// <summary>
		/// Idea 5, pieza que faltaba: pinta un marcador real en la casa de CADA vecino activo
		/// ahora mismo, reutilizando el mismo sistema de marcadores real que ya usa la pestaña
		/// "Búsqueda" (mini-mapa Y mapa vanilla a pantalla completa). Usa
		/// <c>NPC.homeTileX/homeTileY</c> (la casa asignada REAL, la misma que el juego consulta
		/// para el "sin casa asignada" de <see cref="AnadirFilaNpc"/>) - nunca la posición actual
		/// del NPC, que puede estar paseando lejos de casa.
		/// </summary>
		private void MarcarCasasEnElMapa()
		{
			Player jugador = Main.LocalPlayer;
			if (jugador == null) {
				return;
			}

			List<ResultadoBusqueda> marcadores = new List<ResultadoBusqueda>();
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active || !npc.townNPC || NPCID.Sets.IsTownPet[npc.type]) {
					continue;
				}
				bool sinCasa = npc.homeTileX < 0 && npc.homeTileY < 0;
				Vector2 tileCasa = sinCasa
					? new Vector2(npc.Center.X / 16f, npc.Center.Y / 16f)
					: new Vector2(npc.homeTileX, npc.homeTileY);

				marcadores.Add(new ResultadoBusqueda {
					Tile = tileCasa,
					Cantidad = 1,
					Etiqueta = npc.FullName,
					DistanciaAlJugador = Vector2.Distance(jugador.Center, tileCasa * 16f) / 16f,
					TipoNpc = npc.type,
					FrameNpc = npc.frame
				});
			}

			MarcadoresExploracion.Fijar(Idiomas.Texto("Exploracion.Vecindad.Titulo"), marcadores, EstiloTk.Correcto);

			RegistroExploracion.Linea(Terrakeep.LogTag + " Vecindad: \"marcar en el mapa\" - " +
				marcadores.Count + " casas reales marcadas (NPC.homeTileX/homeTileY).");
		}

		private void AnadirFilaNpc(NPC npc, ShoppingSettings ajustes, float distanciaTiles, bool fiable, bool sinCasa)
		{
			// Verde/rojo/gris con el mismo criterio real ya centralizado en EstiloTk (idea 8/
			// FilaRequisitoTk): mas barato que el precio base = contento, mas caro = descontento.
			Color colorPrecio = ajustes.PriceAdjustment < 0.999 ? EstiloTk.Correcto
				: (ajustes.PriceAdjustment > 1.001 ? EstiloTk.Peligro : EstiloTk.Neutro);

			string nombre = npc.FullName;
			string porcentaje = Idiomas.Texto("Exploracion.Vecindad.Precio", (int)System.Math.Round(ajustes.PriceAdjustment * 100.0));

			_lista.Add(new FilaVecinoTk(npc, NuevaLinea(() => nombre + "  ·  " + porcentaje, colorPrecio, 0.8f), this));

			string informe = string.IsNullOrEmpty(ajustes.HappinessReport)
				? Idiomas.Texto("Exploracion.Vecindad.SinInforme")
				: ajustes.HappinessReport;
			ParrafoTk parrafoInforme = new ParrafoTk(() => informe, 0.7f);
			parrafoInforme.ColorTexto = EstiloTk.TextoSuave;
			_lista.Add(parrafoInforme);

			if (sinCasa) {
				_lista.Add(NuevaLinea(() => Idiomas.Texto("Exploracion.Vecindad.SinCasa"), EstiloTk.TextoAviso, 0.68f));
			}
			else if (!fiable) {
				_lista.Add(NuevaLinea(() => Idiomas.Texto("Exploracion.Vecindad.Lejos", (int)distanciaTiles),
					EstiloTk.TextoAviso, 0.68f));
			}

			UIElement hueco = new UIElement();
			hueco.Width.Set(0f, 1f);
			hueco.Height.Set(6f, 0f);
			_lista.Add(hueco);
		}

		private static UIElement NuevaLinea(System.Func<string> texto, Color color, float escala)
		{
			ParrafoTk linea = new ParrafoTk(texto, escala);
			linea.ColorTexto = color;
			return linea;
		}

		/// <summary>Cuántos NPC de pueblo se están enseñando ahora mismo. Lo lee la autoprueba.</summary>
		public int TotalNpcsParaPrueba => _totalNpcs;

		/// <summary>El boton "Echar" de la fila del vecino de tipo <paramref name="tipo"/>, o null si
		/// esa fila no se esta enseñando ahora mismo. Para la autoprueba.</summary>
		public BotonTk BotonEcharDe(int tipo)
		{
			BotonTk encontrado = null;
			_lista.ExecuteRecursively(elemento => {
				FilaVecinoTk fila = elemento as FilaVecinoTk;
				if (encontrado == null && fila != null && fila.Tipo == tipo) {
					encontrado = fila.BotonEchar;
				}
			});
			return encontrado;
		}

		internal void AlEcharDesdeFila()
		{
			_contadorRefresco = FotogramasEntreRefrescos; // refresco inmediato
		}

		/// <summary>
		/// Primera linea de cada vecino: nombre + precio a la izquierda y el boton "Echar" a la
		/// derecha (paridad con el "✕" por fila de escritorio, commit ec916e8b). El alto sigue al
		/// del parrafo (si el nombre parte en dos lineas en una ventana estrecha, la fila crece con
		/// el, nunca se pisa con la de abajo).
		/// </summary>
		private sealed class FilaVecinoTk : UIElement
		{
			private const float AnchoBoton = 74f;
			private const float AltoBoton = 22f;

			private readonly UIElement _texto;
			private readonly BotonTk _echar;

			public BotonTk BotonEchar => _echar;

			/// <summary>Tipo de NPC de esta fila.</summary>
			public int Tipo { get; }

			public FilaVecinoTk(NPC npc, UIElement texto, PestanaVecindad dueno)
			{
				Width.Set(0f, 1f);
				Height.Set(AltoBoton, 0f);

				_texto = texto;
				_texto.Width.Set(-(AnchoBoton + 8f), 1f);
				Append(_texto);

				Tipo = npc.type;
				int indice = npc.whoAmI;
				string nombre = npc.FullName;
				_echar = new BotonTk(Idiomas.Texto("Exploracion.Vecindad.Echar"), 0.7f);
				_echar.Width.Set(AnchoBoton, 0f);
				_echar.Height.Set(AltoBoton, 0f);
				_echar.HAlign = 1f;
				_echar.Ayuda = () => VecindadEditable.MotivoParaNoPoder()
					?? Idiomas.Texto("Exploracion.Vecindad.EcharAyuda", nombre);
				int tipo = npc.type;
				_echar.AlPulsar += () => {
					// El hueco de Main.npc[] se puede reutilizar para OTRO NPC entre dos refrescos de la
					// lista: solo se echa si sigue siendo el mismo tipo de vecino que enseña esta fila.
					NPC actual = Main.npc[indice];
					if (actual == null || !actual.active || actual.type != tipo) {
						dueno.AlEcharDesdeFila();
						return;
					}
					if (VecindadEditable.Echar(actual, "Exploración > Vecindad > Echar")) {
						dueno.AlEcharDesdeFila();
					}
				};
				Append(_echar);
			}

			public override void Update(GameTime gameTime)
			{
				_echar.Habilitado = VecindadEditable.MotivoParaNoPoder() == null;
				_echar.FijarTexto(Idiomas.Texto("Exploracion.Vecindad.Echar"));
				base.Update(gameTime);
				float alto = System.Math.Max(AltoBoton, _texto.Height.Pixels);
				if (System.Math.Abs(Height.Pixels - alto) >= 0.5f) {
					Height.Set(alto, 0f);
					if (Parent != null) {
						Parent.Recalculate();
					}
				}
			}
		}
	}
}
