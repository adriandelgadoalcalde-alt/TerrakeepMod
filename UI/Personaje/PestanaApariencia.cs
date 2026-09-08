using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Pestaña "Apariencia": peinado, tinte de pelo, variante de piel/genero y los siete colores
	/// del personaje.
	/// <para />
	/// Aqui no hace falta ningun renderizador propio ni ningun "muñeco": el juego ya dibuja al
	/// jugador leyendo justo estos campos, asi que cualquier cambio se ve al momento en la
	/// partida. Es la ventaja gorda de editar en vivo frente a la app de escritorio.
	/// <para />
	/// Detalle real de esta version: <c>Player.hair</c> puede llegar hasta
	/// <c>HairLoader.Count - 1</c> (165 de vanilla mas los peinados que añadan los mods), no hasta
	/// el <c>Main.maxHairStyles</c> de vanilla; y <c>Player.skinVariant</c> tiene 12 valores
	/// (<c>PlayerVariantID.Count</c>), que incluyen los dos de maniqui.
	/// </summary>
	public class PestanaApariencia : UIElement
	{
		/// <summary>Donde empieza la columna del muñeco: justo a la derecha del ancho fijo real
		/// que usan los selectores y <see cref="FilaColorTk"/> (que llegan hasta ~740 px). En una
		/// ventana muy estrecha (por debajo de esto no cabe ni el contenido de la izquierda) la
		/// columna sale con ancho negativo y <see cref="MunecoTk"/> deja de dibujarse sola, sin
		/// pisar nada.</summary>
		private const float IzquierdaVista = 750f;

		/// <summary>Alto del boton de "Deshacer cambios" (y del de Cerrar del marco, justo debajo).</summary>
		private const float AltoBoton = 34f;

		/// <summary>Ancho del boton de "Deshacer cambios". Mas ancho que el de Cerrar (160) porque su
		/// rotulo es mas largo: medido con la fuente real, "Deshacer cambios" a escala 0,8 pide unos
		/// 200 px y con 160 se saldria de su caja.</summary>
		private const float AnchoBotonDeshacer = 210f;

		private List<int> _sombreadoresTinte;
		private List<string> _nombresTinte;
		private MunecoTk _muneco;
		private SelectorTk _selectorTinte;
		private BotonTk _botonDeshacer;

		/// <summary>
		/// Como estaba la apariencia del personaje al ENTRAR en esta pestaña. Es lo que restaura el
		/// boton "Deshacer cambios". Se toma en el constructor, y como
		/// <see cref="ContenidoPersonaje"/> construye la pestaña de cero cada vez que se entra en
		/// ella, la foto es siempre "lo que habia justo al abrir Apariencia" y nada mas.
		/// </summary>
		private readonly FotoApariencia _fotoAlEntrar;

		/// <summary>Los identificadores de sombreador de la lista de tintes, en el mismo orden en el
		/// que los recorren las flechas. Lo lee la autoprueba.</summary>
		public List<int> SombreadoresTinte => _sombreadoresTinte;

		/// <summary>Nombres reales de los tintes, en el mismo orden. Lo lee la autoprueba.</summary>
		public List<string> NombresTinte => _nombresTinte;

		/// <summary>La vista previa en vivo. La lee la autoprueba para comparar el muñeco con el
		/// jugador real.</summary>
		public MunecoTk Muneco => _muneco;

		/// <summary>El selector de tinte. La autoprueba lo usa para pulsar sus flechas DE VERDAD,
		/// por la misma ruta que un clic de raton.</summary>
		public SelectorTk SelectorTinte => _selectorTinte;

		/// <summary>El selector de peinado (el primero de la fila de arriba). Lo usa la autoprueba.</summary>
		public SelectorTk SelectorPeinado { get; private set; }

		/// <summary>El selector de variante/genero. Lo usa la autoprueba.</summary>
		public SelectorTk SelectorVariante { get; private set; }

		/// <summary>El boton "Deshacer cambios". Lo pulsa la autoprueba por su ruta real.</summary>
		public BotonTk BotonDeshacer => _botonDeshacer;

		/// <summary>Como estaba la apariencia al entrar en la pestaña, en texto. Para el log.</summary>
		public string FotoAlEntrarDescrita =>
			_fotoAlEntrar != null ? _fotoAlEntrar.Describir() : "(sin foto)";

		/// <summary>true si el personaje esta exactamente como cuando se abrio la pestaña.</summary>
		public bool AparienciaComoAlEntrar => !HayCambios();

		/// <summary>La apariencia del personaje AHORA MISMO, en texto. Para el log.</summary>
		public static string AparienciaActualDescrita()
		{
			FotoApariencia foto = FotoApariencia.Tomar(PersonajeVivo.Jugador);
			return foto != null ? foto.Describir() : "(sin jugador)";
		}

		public PestanaApariencia()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			PersonajeVivo.CargarTintesPelo(out _sombreadoresTinte, out _nombresTinte);
			_fotoAlEntrar = FotoApariencia.Tomar(PersonajeVivo.Jugador);

			ConstruirSelectores();
			ConstruirColores();
			ConstruirVistaPrevia();
			ConstruirBotonDeshacer();
		}

		private void ConstruirSelectores()
		{
			SelectorTk peinado = new SelectorTk(() => Idiomas.Texto("Personaje.Apariencia.Peinado"),
				() => Idiomas.Texto("Personaje.Apariencia.PeinadoValor",
					PersonajeVivo.Jugador.hair, PersonajeVivo.TotalPeinados - 1),
				paso => {
					Player jugador = PersonajeVivo.Jugador;
					int total = PersonajeVivo.TotalPeinados;
					jugador.hair = ((jugador.hair + paso) % total + total) % total;
				},
				120f, 10, 360f);
			peinado.Left.Set(0f, 0f);
			peinado.Top.Set(0f, 0f);
			Append(peinado);
			SelectorPeinado = peinado;

			SelectorTk variante = new SelectorTk(() => Idiomas.Texto("Personaje.Apariencia.VarianteEtiqueta"),
				() => PersonajeVivo.NombreVariante(PersonajeVivo.Jugador.skinVariant),
				paso => {
					Player jugador = PersonajeVivo.Jugador;
					int total = PlayerVariantID.Count;
					jugador.skinVariant = ((jugador.skinVariant + paso) % total + total) % total;
				},
				120f, 0, 360f);
			variante.Left.Set(380f, 0f);
			variante.Top.Set(0f, 0f);
			Append(variante);
			SelectorVariante = variante;

			SelectorTk tinte = new SelectorTk(() => Idiomas.Texto("Personaje.Apariencia.Tinte"),
				TextoTinte,
				paso => {
					Player jugador = PersonajeVivo.Jugador;
					int total = _sombreadoresTinte.Count;
					int posicion = _sombreadoresTinte.IndexOf(jugador.hairDye);
					if (posicion < 0) {
						posicion = 0;
					}
					posicion = ((posicion + paso) % total + total) % total;
					jugador.hairDye = _sombreadoresTinte[posicion];
				},
				120f, 5, 460f);
			tinte.Left.Set(0f, 0f);
			tinte.Top.Set(34f, 0f);
			Append(tinte);
			_selectorTinte = tinte;

			EtiquetaTk nota = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Apariencia.NotaTintes", _sombreadoresTinte.Count),
				0.72f, 900f, 18f);
			nota.ColorTexto = EstiloTk.TextoSuave;
			nota.Left.Set(0f, 0f);
			nota.Top.Set(70f, 0f);
			Append(nota);
		}

		private string TextoTinte()
		{
			int sombreador = PersonajeVivo.Jugador.hairDye;
			int posicion = _sombreadoresTinte.IndexOf(sombreador);
			if (posicion < 0) {
				return Idiomas.Texto("Personaje.Apariencia.TinteDesconocido", sombreador);
			}
			return _nombresTinte[posicion];
		}

		private void ConstruirColores()
		{
			// Cuatro etiquetas y no una sola con espacios: la version anterior era una unica cadena
			// ("Colores          R                         V                         A") y sus letras
			// NO caian encima de sus deslizadores - se vio en una captura real, con la R a la
			// izquierda del primer deslizador y la B casi encima del tercero. Ahora cada letra va
			// CENTRADA sobre su deslizador, en las mismas coordenadas que usa FilaColorTk.
			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Apariencia.Colores"), 0.8f, 160f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(96f, 0f);
			Append(titulo);

			string[] canales = { "Rojo", "Verde", "Azul" };
			for (int c = 0; c < canales.Length; c++) {
				string clave = canales[c];
				EtiquetaTk letra = new EtiquetaTk(
					() => Idiomas.Texto("Personaje.Apariencia.Canal." + clave), 0.8f,
					FilaColorTk.AnchoDeslizador, 22f);
				letra.Centrado = true;
				letra.ColorTexto = EstiloTk.TextoSuave;
				letra.Left.Set(FilaColorTk.IzquierdaDeslizador(c), 0f);
				letra.Top.Set(96f, 0f);
				Append(letra);
			}

			float arriba = 122f;
			float paso = 30f;
			int fila = 0;

			Anadir("Pelo", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.hairColor, c => PersonajeVivo.Jugador.hairColor = c);
			Anadir("Piel", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.skinColor, c => PersonajeVivo.Jugador.skinColor = c);
			Anadir("Ojos", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.eyeColor, c => PersonajeVivo.Jugador.eyeColor = c);
			Anadir("Camisa", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.shirtColor, c => PersonajeVivo.Jugador.shirtColor = c);
			Anadir("CamisetaInterior", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.underShirtColor, c => PersonajeVivo.Jugador.underShirtColor = c);
			Anadir("Pantalones", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.pantsColor, c => PersonajeVivo.Jugador.pantsColor = c);
			Anadir("Zapatos", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.shoeColor, c => PersonajeVivo.Jugador.shoeColor = c);

			// Aqui vivia una linea suelta ("Los cambios se ven al instante sobre el personaje: el
			// juego lo dibuja leyendo estos mismos campos."). Se ha quitado por dos motivos, los dos
			// vistos en una captura real del juego a 1600x900:
			//
			// 1) NO CABIA. Con la escala de interfaz 1,47 el area de contenido del panel se queda en
			//    312 px de alto y esa etiqueta caia en el 352, o sea 40 px por DEBAJO del area,
			//    encima de la linea de ayuda del pie del marco. Las dos frases se dibujaban una
			//    sobre otra y no se leia ninguna de las dos.
			// 2) Era REDUNDANTE. Esa linea de ayuda del pie ("Todo lo que toques aquí se escribe al
			//    instante sobre el personaje cargado.", Panel.Ayuda.Personaje) dice exactamente lo
			//    mismo y esta siempre a la vista en toda el area de Personaje.
			//
			// La clave Personaje.Apariencia.NotaColores se ha quitado tambien de
			// scripts/generar-localizacion.py: no se deja una clave sin usar.
		}

		/// <summary>Añade una fila de color. Recibe la CLAVE de localizacion, no el texto.</summary>
		private void Anadir(string clave, float arriba,
			System.Func<Color> leer, System.Action<Color> escribir)
		{
			FilaColorTk fila = new FilaColorTk(
				() => Idiomas.Texto("Personaje.Apariencia.Color." + clave), leer, escribir);
			fila.Left.Set(0f, 0f);
			fila.Top.Set(arriba, 0f);
			Append(fila);
		}

		/// <summary>
		/// El hueco que quedaba vacio a la derecha (bajo el logo decorativo de fondo del panel):
		/// un muñeco en vivo, dibujado con el renderer REAL del juego
		/// (<see cref="MunecoTk"/> -> <c>Main.PlayerRenderer</c>, el mismo que usa la pantalla
		/// de seleccion de personaje y el Maniqui de vanilla), con un alternador para verlo con
		/// o sin la armadura puesta. Se actualiza solo -no hay que pulsar nada- porque
		/// <see cref="MunecoTk"/> relee <c>Main.LocalPlayer</c> en cada fotograma.
		/// </summary>
		private void ConstruirVistaPrevia()
		{
			UIPanel caja = new UIPanel();
			caja.Left.Set(IzquierdaVista, 0f);
			caja.Width.Set(-(IzquierdaVista + 20f), 1f);
			caja.Top.Set(0f, 0f);
			// Deja libre la franja de abajo del area de contenido para el boton "Deshacer cambios",
			// que va justo encima del "Cerrar" del marco y alineado con el.
			caja.Height.Set(-(AltoBoton + 8f), 1f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.SetPadding(8f);
			Append(caja);

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Apariencia.Vista"), 0.8f, 300f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(0f, 0f);
			caja.Append(titulo);

			_muneco = new MunecoTk();
			_muneco.Width.Set(0f, 1f);
			_muneco.Top.Set(26f, 0f);
			_muneco.Height.Set(-(26f + 34f), 1f);
			caja.Append(_muneco);

			AlternadorTk alternador = new AlternadorTk(
				() => Idiomas.Texto("Personaje.Apariencia.VerConArmadura"),
				() => _muneco.ConArmadura,
				valor => _muneco.ConArmadura = valor);
			alternador.Ayuda = () => Idiomas.Texto("Personaje.Apariencia.VerConArmaduraAyuda");
			alternador.Width.Set(0f, 1f);
			alternador.Top.Set(-30f, 1f);
			caja.Append(alternador);
		}

		/// <summary>
		/// El boton "Deshacer cambios": devuelve la apariencia a como estaba al ENTRAR en esta
		/// pestaña (peinado, tinte, variante y los siete colores). No es el deshacer general de
		/// Terrakeep (Ctrl+Z, que va accion por accion de toda la sesion): es un "como estaba
		/// cuando entre aqui" de una sola pulsada, que es lo que se pidio.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <b>Donde va.</b> Pegado al boton "Cerrar" del marco: mismo borde derecho, mismo alto y
		/// justo encima (el pie del marco mide 44 px y el "Cerrar" ocupa los 34 de abajo, asi que
		/// entre los dos quedan los 10 px de separacion del propio marco). No se ha metido DENTRO de
		/// esa fila del pie, aunque cabria de sobra por ancho, porque la linea de ayuda del pie es
		/// una <see cref="EtiquetaTk"/> de 820 px que no ignora el raton y se comeria los clics de
		/// la mitad izquierda del boton; y porque ese pie es del marco comun de las seis areas
		/// (<c>PanelTerrakeepState</c>), no de esta pestaña.
		/// </para>
		/// <para>
		/// <b>Se apaga solo</b> mientras no haya nada que deshacer, en vez de dejar que se pulse y
		/// no haga nada: asi el jugador ve de un vistazo si ha tocado algo desde que entro, y el
		/// historial general no se llena de entradas vacias.
		/// </para>
		/// <para>
		/// <b>Snapshot puro, no closures encadenadas</b>: se guarda el ESTADO entero de la
		/// apariencia (once campos), no una lista de operaciones que revertir. Es el mismo criterio
		/// que ya razona <c>EntradaSnapshot&lt;T&gt;</c> para todo el historial del mod (y el que usa
		/// <c>ContainerViewModel.ClearAll</c> en la app de escritorio hermana): dentro de una partida
		/// en marcha, entre que se toma la foto y que se restaura pueden haber pasado mil cosas, y
		/// una foto completa siempre deja un estado coherente.
		/// </para>
		/// </remarks>
		private void ConstruirBotonDeshacer()
		{
			_botonDeshacer = new BotonTk(Idiomas.Texto("Personaje.Apariencia.Deshacer"),
				EstiloTk.EscalaBoton);
			_botonDeshacer.Width.Set(AnchoBotonDeshacer, 0f);
			_botonDeshacer.Height.Set(AltoBoton, 0f);
			_botonDeshacer.HAlign = 1f;
			_botonDeshacer.Top.Set(-AltoBoton, 1f);
			_botonDeshacer.Ayuda = TextoAyudaDeshacer;
			_botonDeshacer.AlPulsar += DeshacerCambiosDeApariencia;
			Append(_botonDeshacer);
		}

		private string TextoAyudaDeshacer()
		{
			return HayCambios()
				? Idiomas.Texto("Personaje.Apariencia.DeshacerAyuda")
				: Idiomas.Texto("Personaje.Apariencia.DeshacerNada");
		}

		/// <summary>true si la apariencia del personaje se ha movido de la foto que se tomo al
		/// entrar en la pestaña.</summary>
		private bool HayCambios()
		{
			return _fotoAlEntrar != null && !_fotoAlEntrar.CuadraCon(PersonajeVivo.Jugador);
		}

		/// <summary>
		/// Restaura la foto y deja la accion registrada en el historial general del mod, para que el
		/// propio Ctrl+Z pueda deshacer el deshacer. La foto de "antes" se toma aqui mismo, justo
		/// antes de tocar nada, exactamente igual que hace <c>Historial.CambiarObjetos</c>.
		/// </summary>
		private void DeshacerCambiosDeApariencia()
		{
			if (!PersonajeVivo.HayJugador || _fotoAlEntrar == null || !HayCambios()) {
				return;
			}

			Player jugador = PersonajeVivo.Jugador;
			FotoApariencia antes = FotoApariencia.Tomar(jugador);
			_fotoAlEntrar.Aplicar(jugador);
			FotoApariencia despues = FotoApariencia.Tomar(jugador);

			Historial.CambiarValor(Idiomas.Texto("Personaje.Apariencia.DeshacerAccion"),
				antes, despues, foto => foto.Aplicar(PersonajeVivo.Jugador));

			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Apariencia: deshechos los cambios de " +
				$"esta visita a la pestaña. Antes: {antes.Describir()}. Restaurado: {despues.Describir()}.");
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			if (_botonDeshacer != null) {
				// Se refresca en cada fotograma y no solo al tocar un control: la apariencia tambien
				// puede cambiarla el propio juego o el Ctrl+Z general mientras la pestaña esta
				// abierta, y el boton tiene que enterarse igual.
				_botonDeshacer.FijarTexto(Idiomas.Texto("Personaje.Apariencia.Deshacer"));
				_botonDeshacer.Habilitado = HayCambios();
			}

			AutopruebaApariencia.Avanzar(this);
		}
	}

	/// <summary>
	/// Foto de TODA la apariencia de un personaje: los once campos que edita esta pestaña
	/// (peinado, tinte, variante y los siete colores). Es lo que restaura el boton
	/// "Deshacer cambios".
	/// </summary>
	/// <remarks>
	/// Son once valores de tipo valor (tres <c>int</c> y siete <see cref="Color"/>), asi que la
	/// foto es una copia de verdad sin necesidad de clonar nada: al reves que con un
	/// <c>Item</c>, aqui no hay ninguna referencia compartida que pueda cambiar por debajo. Por
	/// eso <see cref="Historial.CambiarValor{T}"/> le vale tal cual y no hizo falta nada como
	/// <c>SnapshotDeObjetos</c>.
	/// </remarks>
	internal sealed class FotoApariencia
	{
		private int _peinado;
		private int _tinte;
		private int _variante;
		private Color _pelo;
		private Color _piel;
		private Color _ojos;
		private Color _camisa;
		private Color _camisetaInterior;
		private Color _pantalones;
		private Color _zapatos;

		private FotoApariencia() { }

		public static FotoApariencia Tomar(Player jugador)
		{
			if (jugador == null) {
				return null;
			}

			FotoApariencia foto = new FotoApariencia();
			foto._peinado = jugador.hair;
			foto._tinte = jugador.hairDye;
			foto._variante = jugador.skinVariant;
			foto._pelo = jugador.hairColor;
			foto._piel = jugador.skinColor;
			foto._ojos = jugador.eyeColor;
			foto._camisa = jugador.shirtColor;
			foto._camisetaInterior = jugador.underShirtColor;
			foto._pantalones = jugador.pantsColor;
			foto._zapatos = jugador.shoeColor;
			return foto;
		}

		public void Aplicar(Player jugador)
		{
			if (jugador == null) {
				return;
			}

			jugador.hair = _peinado;
			jugador.hairDye = _tinte;
			jugador.skinVariant = _variante;
			jugador.hairColor = _pelo;
			jugador.skinColor = _piel;
			jugador.eyeColor = _ojos;
			jugador.shirtColor = _camisa;
			jugador.underShirtColor = _camisetaInterior;
			jugador.pantsColor = _pantalones;
			jugador.shoeColor = _zapatos;
		}

		/// <summary>true si el personaje sigue exactamente como cuando se tomo la foto.</summary>
		public bool CuadraCon(Player jugador)
		{
			if (jugador == null) {
				return true;
			}

			return jugador.hair == _peinado
				&& jugador.hairDye == _tinte
				&& jugador.skinVariant == _variante
				&& jugador.hairColor == _pelo
				&& jugador.skinColor == _piel
				&& jugador.eyeColor == _ojos
				&& jugador.shirtColor == _camisa
				&& jugador.underShirtColor == _camisetaInterior
				&& jugador.pantsColor == _pantalones
				&& jugador.shoeColor == _zapatos;
		}

		/// <summary>Resumen legible para el log. No se enseña en la interfaz.</summary>
		public string Describir()
		{
			return "peinado=" + _peinado + " tinte=" + _tinte + " variante=" + _variante +
				" pelo=" + Corto(_pelo) + " piel=" + Corto(_piel) + " ojos=" + Corto(_ojos) +
				" camisa=" + Corto(_camisa) + " camiseta=" + Corto(_camisetaInterior) +
				" pantalon=" + Corto(_pantalones) + " zapatos=" + Corto(_zapatos);
		}

		private static string Corto(Color color)
		{
			return color.R + "," + color.G + "," + color.B;
		}
	}

	// =========================================================================================
	// SOLO ARNES DE PRUEBAS a partir de aqui. Sin la variable de entorno esto es un no-op total:
	// la unica llamada nueva en el Update de la pestaña sale por el primer if.
	//
	// Vive en este archivo -y no en Common/Panel/Autoprueba*.cs- a proposito: hay mas agentes
	// trabajando a la vez en este repositorio y esta tarea solo tiene permiso sobre
	// PestanaApariencia.cs y MunecoTk.cs. Nada de aqui toca ningun archivo ajeno.
	// =========================================================================================

	/// <summary>
	/// Comprueba EN EL JUEGO REAL que un tinte de pelo se aplica y se ve: recorre varios tintes
	/// distintos pulsando de verdad las flechas del selector, deja el color que calcula el juego
	/// para el jugador real y para el muñeco de la vista previa, y guarda una captura real del
	/// back buffer con cada uno.
	/// </summary>
	internal static class AutopruebaApariencia
	{
		public const string Variable = "TERRAKEEP_AUTOTEST_APARIENCIA";

		/// <summary>Fotogramas de cortesia antes del primer paso. La autoprueba del panel unico
		/// (que es la que abre el panel y entra en Apariencia) todavia esta haciendo sus pasos
		/// 31-37 sobre esta misma pestaña; se le deja terminar en vez de pelearse con ella.</summary>
		private const int FotogramasDeCortesia = 260;

		private const int FotogramasEntrePasos = 20;

		private static bool _comprobada;
		private static bool _activa;
		private static bool _terminada;
		private static int _fotogramas;
		private static int _paso;
		private static int _espera;
		private static bool _monedasDadas;

		/// <summary>Tintes con los que se prueba, por id de OBJETO real de vanilla (no por id de
		/// sombreador, que depende de los mods cargados). Se han elegido tres con comportamientos
		/// distintos a proposito: uno de color fijo, uno que depende del estado del jugador y uno
		/// que es un sombreador de verdad y no un simple color.</summary>
		private static readonly int[] ObjetosTinte = {
			ItemID.PartyHairDye,    // color fijo (244,22,175): si este no se ve, no se ve ninguno
			ItemID.ManaHairDye,     // depende de statMana/statManaMax2 del Player QUE SE DIBUJA:
			                        // es uno de los tres que salian mal en la vista previa antes del
			                        // arreglo (blanco (250,255,255) en vez de azul (50,75,255))
			ItemID.TwilightHairDye  // sombreador REAL (GameShaders.Hair.Apply), no un color
		};

		private static readonly string[] NombresCortos = { "party", "mana", "twilight" };

		public static void Avanzar(PestanaApariencia pestana)
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(
					System.Environment.GetEnvironmentVariable(Variable));
			}
			if (!_activa || _terminada || pestana == null || !PersonajeVivo.HayJugador) {
				return;
			}

			_fotogramas++;

			// Las monedas se dan MUCHO antes del primer paso, no dentro de el: MunecoTk se
			// sincroniza en el mismo Update (es hijo de esta pestaña, y base.Update va primero), asi
			// que dandolas en el paso 0 la tabla de colores se imprimiria con el muñeco todavia sin
			// verlas. Salio "DISTINTOS" en el tinte de dinero por esto exactamente, en una pasada
			// real - no es una precaucion teorica.
			if (!_monedasDadas && _fotogramas > 30) {
				_monedasDadas = true;
				DarMonedasDePrueba();
			}

			if (_fotogramas < FotogramasDeCortesia) {
				return;
			}
			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			try {
				Paso(pestana, _paso++);
			}
			catch (System.Exception e) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(PestanaApariencia pestana, int paso)
		{
			// Pasos 0..6: los tintes (uno "aplicar" y un "capturar" por cada uno de los tres).
			if (paso == 0) {
				Arrancar(pestana);
				return;
			}

			int indice = (paso - 1) / 2;
			if (indice < ObjetosTinte.Length) {
				if ((paso - 1) % 2 == 0) {
					AplicarTinte(pestana, ObjetosTinte[indice]);
				}
				else {
					Capturar(pestana, NombresCortos[indice]);
				}
				return;
			}

			// Pasos 7 en adelante: el boton "Deshacer cambios" y el personaje real.
			switch (paso - 1 - ObjetosTinte.Length * 2) {
				case 0: CambiarVariasCosas(pestana); break;
				case 1: Capturar(pestana, "antes-de-deshacer"); break;
				case 2: PulsarDeshacer(pestana); break;
				case 3: ComprobarDeshecho(pestana); break;
				case 4: Capturar(pestana, "despues-de-deshacer"); break;
				case 5: DejarTinteYCerrarElPanel(pestana); break;
				default:
					_terminada = true;
					RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA COMPLETA.");
					break;
			}
		}

		// -------------------------------------------------------------------------------------

		private static void Arrancar(PestanaApariencia pestana)
		{
			Player jugador = PersonajeVivo.Jugador;
			Player muneco = pestana.Muneco != null ? pestana.Muneco.Jugador : null;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA: " + Variable +
				" detectada. Jugador \"" + jugador.name + "\", hairDye=" + jugador.hairDye +
				", hair=" + jugador.hair + ", head=" + jugador.head +
				", hairColor=" + jugador.hairColor +
				", statLife=" + jugador.statLife + "/" + jugador.statLifeMax2 +
				", statMana=" + jugador.statMana + "/" + jugador.statManaMax2 +
				", position=" + jugador.position + ", team=" + jugador.team);

			if (muneco != null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA: muñeco de la vista " +
					"previa -> hairDye=" + muneco.hairDye + ", hair=" + muneco.hair +
					", head=" + muneco.head + ", hairColor=" + muneco.hairColor +
					", statLife=" + muneco.statLife + "/" + muneco.statLifeMax2 +
					", statMana=" + muneco.statMana + "/" + muneco.statManaMax2 +
					", position=" + muneco.position + ", team=" + muneco.team +
					", isDisplayDollOrInanimate=" + muneco.isDisplayDollOrInanimate +
					", skinVariant=" + muneco.skinVariant);
			}
			else {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA: no hay MunecoTk.");
			}

			List<int> sombreadores = pestana.SombreadoresTinte;
			List<string> nombres = pestana.NombresTinte;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA: la lista tiene " +
				(sombreadores != null ? sombreadores.Count : 0) + " entradas (incluida \"ninguno\").");

			if (sombreadores == null) {
				return;
			}

			// El color que el juego calcularia para CADA tinte, con el jugador real y con el
			// muñeco. Es literalmente la llamada que hace Player.GetHairColor, que es de donde
			// PlayerDrawSet saca el colorHair con el que se pinta el pelo.
			for (int i = 0; i < sombreadores.Count; i++) {
				int id = sombreadores[i];
				string colorJugador = GameShaders.Hair.GetColor(id, jugador, Color.White).ToString();
				string colorMuneco = muneco != null
					? GameShaders.Hair.GetColor(id, muneco, Color.White).ToString()
					: "(sin muñeco)";
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/tinte " + i +
					" sombreador=" + id + " \"" + nombres[i] + "\"" +
					" -> color con el JUGADOR=" + colorJugador +
					" | color con el MUÑECO=" + colorMuneco +
					(colorJugador == colorMuneco ? " (iguales)" : " (DISTINTOS)"));
			}
		}

		/// <summary>
		/// Le mete al personaje de prueba 30 monedas de platino. No es un capricho: el "Tinte de
		/// dinero" colorea el pelo segun lo que lleves encima, y con la cartera a cero da el mismo
		/// color (226,118,76) tanto al jugador como al muñeco, asi que la comparacion de ese tinte
		/// no demostraria nada. Con 30 platino el color pasa al gris de "rico" (161,172,173) y la
		/// linea del tinte 4 de la tabla ya distingue de verdad si el muñeco ve el inventario.
        /// Solo corre dentro del sandbox de la autoprueba.
		/// </summary>
		private static void DarMonedasDePrueba()
		{
			Player jugador = PersonajeVivo.Jugador;
			if (jugador.inventory[PersonajeVivo.PrimerSlotMonedas].type == ItemID.PlatinumCoin) {
				return;
			}

			Item monedas = new Item();
			monedas.SetDefaults(ItemID.PlatinumCoin);
			monedas.stack = 30;
			jugador.inventory[PersonajeVivo.PrimerSlotMonedas] = monedas;
		}

		/// <summary>
		/// Deja puesto el tinte del objeto indicado pulsando DE VERDAD la flecha "&gt;" del selector
		/// tantas veces como haga falta (la misma ruta que un clic de raton), nunca escribiendo
		/// <c>hairDye</c> a mano: lo que hay que demostrar es que la interfaz lo aplica.
		/// </summary>
		private static void AplicarTinte(PestanaApariencia pestana, int idObjeto)
		{
			int objetivo = SombreadorDelObjeto(idObjeto);
			string nombreObjeto = Lang.GetItemNameValue(idObjeto);

			if (objetivo <= 0) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA: el tinte \"" +
					nombreObjeto + "\" (objeto " + idObjeto + ") no tiene sombreador registrado; " +
					"se salta.");
				return;
			}

			BotonTk flecha = BuscarFlecha(pestana.SelectorTinte, ">");
			if (flecha == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA: no se encontro la " +
					"flecha \">\" del selector de tinte.");
				return;
			}

			CalculatedStyle dim = flecha.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);

			int vueltas = 0;
			int maximo = pestana.SombreadoresTinte.Count + 2;
			while (PersonajeVivo.Jugador.hairDye != objetivo && vueltas < maximo) {
				flecha.LeftClick(new UIMouseEvent(flecha, centro));
				vueltas++;
			}

			Player jugador = PersonajeVivo.Jugador;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA - " + vueltas +
				" CLICS REALES en \">\" (en " + (int)centro.X + "," + (int)centro.Y + ") para llegar a \"" +
				nombreObjeto + "\": hairDye=" + jugador.hairDye + " (objetivo " + objetivo + ") " +
				(jugador.hairDye == objetivo ? "-> OK" : "-> NO CUADRA") +
				". Rotulo del selector: \"" + pestana.SelectorTinte.ValorActual + "\"" +
				". Color de pelo que devuelve el juego para el JUGADOR: " +
				jugador.GetHairColor(false));
		}

		private static void Capturar(PestanaApariencia pestana, string sufijo)
		{
			Player jugador = PersonajeVivo.Jugador;
			MunecoTk muneco = pestana.Muneco;
			Player munecoJugador = muneco != null ? muneco.Jugador : null;

			string caja = "(sin muñeco)";
			if (muneco != null) {
				CalculatedStyle dim = muneco.GetDimensions();
				// La interfaz se dibuja con InterfaceScaleType.UI, o sea escalada por Main.UIScale:
				// para poder buscar estos pixeles en el PNG (que sale del back buffer, sin escalar)
				// hay que multiplicar. Se dejan las dos por si acaso.
				caja = "UI x=" + (int)dim.X + " y=" + (int)dim.Y + " " +
					(int)dim.Width + "x" + (int)dim.Height +
					" | back buffer x=" + (int)(dim.X * Main.UIScale) + " y=" + (int)(dim.Y * Main.UIScale) +
					" " + (int)(dim.Width * Main.UIScale) + "x" + (int)(dim.Height * Main.UIScale);
			}

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/" + sufijo +
				" - jugador.hairDye=" + jugador.hairDye +
				", muñeco.hairDye=" + (munecoJugador != null ? munecoJugador.hairDye.ToString() : "?") +
				", color del pelo del JUGADOR=" + jugador.GetHairColor(false) +
				", color del pelo del MUÑECO=" +
				(munecoJugador != null ? munecoJugador.GetHairColor(false).ToString() : "?") +
				". Caja del muñeco: " + caja + ". " +
				CapturaDePantalla.Guardar("apariencia-tinte-" + sufijo));
		}

		// ----------------------------------------------------- boton "Deshacer cambios"

		/// <summary>Estado de la apariencia justo antes de tocar nada en el bloque del deshacer.
		/// No se usa para comparar (para eso esta la foto de la propia pestaña): es para el log.</summary>
		private static string _aparienciaAntesDeCambiar;

		/// <summary>
		/// Cambia varias cosas de la apariencia a la vez: peinado y variante con CLICS REALES en
		/// sus flechas, y los siete colores escribiendolos en el jugador (los deslizadores de color
		/// se manejan arrastrando el raton, que no se puede simular desde aqui; lo que hay que
		/// demostrar es que el boton los DEVUELVE, y para eso da igual quien los haya movido).
		/// </summary>
		private static void CambiarVariasCosas(PestanaApariencia pestana)
		{
			Player jugador = PersonajeVivo.Jugador;
			_aparienciaAntesDeCambiar = PestanaApariencia.AparienciaActualDescrita();

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/deshacer - foto tomada al " +
				"ENTRAR en la pestaña: " + pestana.FotoAlEntrarDescrita);
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/deshacer - estado justo " +
				"antes de cambiar nada mas: " + _aparienciaAntesDeCambiar +
				". Boton \"Deshacer cambios\" habilitado=" +
				(pestana.BotonDeshacer != null ? pestana.BotonDeshacer.Habilitado.ToString() : "?") +
				" (los tintes de los pasos de arriba ya lo habian cambiado, asi que tiene que estar " +
				"habilitado).");

			int clicsPeinado = PulsarVeces(BuscarFlecha(pestana.SelectorPeinado, ">"), 3);
			int clicsVariante = PulsarVeces(BuscarFlecha(pestana.SelectorVariante, ">"), 2);

			jugador.hairColor = new Color(10, 200, 40);
			jugador.skinColor = new Color(200, 180, 250);
			jugador.eyeColor = new Color(250, 250, 10);
			jugador.shirtColor = new Color(10, 10, 10);
			jugador.underShirtColor = new Color(250, 10, 10);
			jugador.pantsColor = new Color(10, 250, 250);
			jugador.shoeColor = new Color(120, 0, 120);

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/deshacer - " + clicsPeinado +
				" clics reales en la flecha del peinado y " + clicsVariante + " en la de variante, " +
				"mas los siete colores reescritos. Estado ahora: " +
				PestanaApariencia.AparienciaActualDescrita() +
				". Boton habilitado=" +
				(pestana.BotonDeshacer != null ? pestana.BotonDeshacer.Habilitado.ToString() : "?"));
		}

		private static void PulsarDeshacer(PestanaApariencia pestana)
		{
			BotonTk boton = pestana.BotonDeshacer;
			if (boton == null) {
				RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/deshacer: no hay boton.");
				return;
			}

			CalculatedStyle dim = boton.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			boton.LeftClick(new UIMouseEvent(boton, centro));

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/deshacer - CLIC REAL en \"" +
				boton.Texto + "\" (x=" + (int)dim.X + " y=" + (int)dim.Y + " " +
				(int)dim.Width + "x" + (int)dim.Height + ", clic en " +
				(int)centro.X + "," + (int)centro.Y + ").");
		}

		private static void ComprobarDeshecho(PestanaApariencia pestana)
		{
			string ahora = PestanaApariencia.AparienciaActualDescrita();
			string alEntrar = pestana.FotoAlEntrarDescrita;
			bool ok = pestana.AparienciaComoAlEntrar && ahora == alEntrar;

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/deshacer RESULTADO: " +
				(ok ? "OK" : "FALLO") +
				" - al entrar: " + alEntrar +
				" | despues de deshacer: " + ahora +
				(ok ? " (identicos campo a campo)" : " (NO CUADRAN)") +
				". Boton habilitado ahora=" +
				(pestana.BotonDeshacer != null ? pestana.BotonDeshacer.Habilitado.ToString() : "?") +
				" (tiene que estar apagado: ya no queda nada que deshacer)." +
				" Historial general: puede deshacer=" + Historial.Pila.PuedeDeshacer +
				", ultima entrada=\"" + (Historial.Pila.EtiquetaDeshacer ?? "(ninguna)") + "\".");
		}

		private static int PulsarVeces(BotonTk boton, int veces)
		{
			if (boton == null) {
				return 0;
			}
			CalculatedStyle dim = boton.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			for (int i = 0; i < veces; i++) {
				boton.LeftClick(new UIMouseEvent(boton, centro));
			}
			return veces;
		}

		// ----------------------------------------------------- el personaje REAL, sin panel

		private static int _fotogramasSinPanel;

		/// <summary>
		/// Ultimo bloque: deja puesto un tinte bien visible, CIERRA el panel y captura al personaje
		/// de verdad en el mundo, para demostrar que el tinte no se ve solo en la vista previa.
		/// <para />
		/// Con el panel cerrado esta pestaña ya no recibe <c>Update</c> (deja de estar montada), asi
		/// que el ultimo paso se engancha a <c>Main.OnTickForInternalCodeOnly</c>, un evento publico
		/// de vanilla que corre en la fase de LOGICA de cada fotograma normal de partida
		/// (<c>Main.DoUpdate</c>, justo despues de <c>gamePaused = false</c>) - o sea, exactamente el
		/// mismo momento en el que <c>CapturaDePantalla</c> ya sabe leer el back buffer desde
		/// <c>UpdateUI</c>, con el fotograma anterior ya presentado. Se desengancha en cuanto termina.
		/// <para />
		/// Dos hooks descartados antes, los dos por un motivo real y medido, no por gusto:
		/// <c>Main.OnPreDraw</c> saco una imagen COMPLETAMENTE NEGRA (ese evento vive ya dentro de
		/// <c>Main.DoDraw</c>, despues de <c>InitTargets</c>/<c>ReleaseTargets</c>, cuando el back
		/// buffer ya no tiene el fotograma presentado); y <c>Main.OnTickForThirdPartySoftwareOnly</c>
		/// saco una imagen ATRASADA UN SEGUNDO, porque en el cliente ese evento solo se dispara en la
		/// rama de "la ventana no tiene el foco" de <c>Main.DoUpdate</c> - la misma en la que el juego
		/// sigue actualizando a toda velocidad pero deja de dibujar.
		/// <para />
		/// Por eso ademas la cuenta atras solo avanza con <c>Main.hasFocus</c>: sin foco no hay
		/// fotogramas nuevos que capturar.
		/// </summary>
		private static void DejarTinteYCerrarElPanel(PestanaApariencia pestana)
		{
			AplicarTinte(pestana, ItemID.PartyHairDye);

			_fotogramasSinPanel = 0;
			Main.OnTickForInternalCodeOnly += CapturarPersonajeReal;
			PanelTerrakeepSystem.CerrarPanel("autoprueba de apariencia: ver el personaje real");

			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/personaje-real - panel " +
				"cerrado con el tinte festivo puesto (hairDye=" + PersonajeVivo.Jugador.hairDye +
				"). Panel abierto=" + PanelTerrakeepSystem.PanelAbierto + ".");
		}

		private static void CapturarPersonajeReal()
		{
			if (!Main.hasFocus) {
				return;
			}

			// Solo para que la foto del personaje real se vea limpia: al cerrar un panel de
			// IngameFancyUI el juego deja el inventario de vanilla abierto (y ademas queda flotando
			// la burbuja de emote que se lanza al cerrar), y las dos cosas tapan medio personaje.
			Main.playerInventory = false;
			Main.LocalPlayer.emoteTime = 0;

			// Y luz: si la partida de prueba cae de noche, el personaje sale casi en negro y la foto
			// no demuestra nada. Es la misma llamada que usa una antorcha de verdad
			// (Lighting.AddLight), sobre el tile del propio jugador.
			Vector2 centro = Main.LocalPlayer.Center;
			Lighting.AddLight((int)(centro.X / 16f), (int)(centro.Y / 16f), 2f, 2f, 2f);

			_fotogramasSinPanel++;
			if (_fotogramasSinPanel < 400) {
				return;
			}

			Main.OnTickForInternalCodeOnly -= CapturarPersonajeReal;

			Player jugador = Main.LocalPlayer;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA/personaje-real - " +
				"SIN panel: hairDye=" + jugador.hairDye + ", hair=" + jugador.hair +
				", head=" + jugador.head +
				", color de pelo que usa el renderer (Player.GetHairColor) = " +
				jugador.GetHairColor(false) +
				", el jugador se dibuja en pantalla en " +
				(jugador.position - Main.screenPosition) + ". " +
				CapturaDePantalla.Guardar("apariencia-personaje-real"));

			_terminada = true;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUTOPRUEBA APARIENCIA COMPLETA.");
		}

		/// <summary>Identificador de sombreador de pelo de un objeto tinte real, sacado del propio
		/// objeto (que es de donde lo copia el juego: <c>Player.hairDye = item.hairDye</c>).</summary>
		private static int SombreadorDelObjeto(int idObjeto)
		{
			Item muestra;
			if (ContentSamples.ItemsByType.TryGetValue(idObjeto, out muestra) && muestra != null) {
				return muestra.hairDye;
			}
			return -1;
		}

		/// <summary>El <see cref="BotonTk"/> de una de las flechas del selector, buscado dentro del
		/// propio selector (no por todo el panel: hay tres selectores con las mismas flechas).</summary>
		private static BotonTk BuscarFlecha(SelectorTk selector, string texto)
		{
			if (selector == null) {
				return null;
			}
			BotonTk encontrado = null;
			selector.ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (encontrado == null && boton != null && boton.Texto == texto) {
					encontrado = boton;
				}
			});
			return encontrado;
		}
	}
}
