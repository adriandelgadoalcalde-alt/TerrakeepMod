using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// <b>Idea 10 del catalogo de funciones ("rebobinar el mundo"):</b> marca una foto REAL de los
	/// tiles de un cuadrado alrededor del jugador (terreno, paredes, liquidos, pendientes, pintura,
	/// cables) Y de los cofres reales que caigan dentro (el propio catalogo pide "tiles + cofres en
	/// un radio", no solo terreno) y lo devuelve todo tal cual estaba cuando se pulsa "Rebobinar" -
	/// un deshacer de zona, del mismo tipo que ya existe para objetos
	/// (<see cref="TerrakeepMod.Common.Undo.PilaDeSnapshots"/>) pero aplicado al mundo.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que una foto propia y no <c>Tile</c> tal cual.</b> Investigado ANTES de escribir
	/// codigo, decompilando <c>Terraria.Tile</c> real: desde el refactor de memoria de tiles de
	/// 1.4.4+, un <c>Tile</c> NO es un dato independiente - es un accesor ligero que LEE Y ESCRIBE
	/// sobre arrays compartidos de verdad (<c>Get&lt;TileTypeData&gt;()</c> y companyia,
	/// confirmado en el decompilado real). Guardar <c>Tile snapshot = Main.tile[x, y];</c> no
	/// copiaria ningun dato: seguiria apuntando a la MISMA celda viva, asi que cambiaria con ella.
	/// Por eso <see cref="TileGuardado"/> es un struct PROPIO que copia el VALOR real de cada campo
	/// (todos con getter Y setter publicos, confirmados uno a uno en el decompilado:
	/// <c>TileType</c>, <c>WallType</c>, <c>HasTile</c>, <c>IsActuated</c>, <c>Slope</c>,
	/// <c>IsHalfBlock</c>, <c>TileColor</c>, <c>WallColor</c>, <c>LiquidType</c>,
	/// <c>LiquidAmount</c>, los cuatro cables) - eso si es un dato independiente de verdad.
	/// </para>
	/// <para>
	/// <b>Por que solo partida de un jugador.</b> Escribir tiles en un servidor real no basta con
	/// tocar el array local: hace falta reenviar el cambio a cada cliente conectado
	/// (<c>NetMessage.SendTileSquare</c>, API real de tModLoader) o el mundo se ve distinto en
	/// cada pantalla. Es el mismo tipo de riesgo, investigado y documentado igual, que la idea 6
	/// (cofres del mundo) y la idea 9 (guia de grupo): sin un arnes de dos clientes tModLoader en
	/// la familia para verificarlo con cuidado esta noche, <see cref="Rebobinar"/> se niega en
	/// multijugador (<c>Main.netMode != NetmodeID.SinglePlayer</c>) en vez de escribir a ciegas.
	/// </para>
	/// <para>
	/// <b>Por que un radio acotado.</b> <see cref="RadioTiles"/> (100 tiles, un cuadrado de
	/// 201x201 = 40401 celdas) es memoria y tiempo triviales (un puñado de bytes por celda,
	/// milisegundos de verdad para copiar) sin arriesgarse a fotografiar el mundo entero -
	/// "rebobinar donde estas trabajando ahora", no "rebobinar el mundo entero de golpe".
	/// </para>
	/// </remarks>
	public class PestanaRebobinar : UIElement
	{
		/// <summary>Radio en tiles alrededor del jugador que se fotografia. Ver el XMLdoc de la
		/// clase para el porque de este numero concreto.</summary>
		public const int RadioTiles = 100;

		private const int FotogramasEntreComparaciones = 30;

		/// <summary>Foto de UN tile: solo datos propios (ver el XMLdoc de la clase para el porque
		/// de no guardar <c>Tile</c> tal cual).</summary>
		private struct TileGuardado
		{
			public ushort TileType;
			public ushort WallType;
			public bool HasTile;
			public bool IsActuated;
			public SlopeType Slope;
			public bool IsHalfBlock;
			public byte TileColor;
			public byte WallColor;
			public int LiquidType;
			public byte LiquidAmount;
			public bool RedWire;
			public bool GreenWire;
			public bool BlueWire;
			public bool YellowWire;

			public static TileGuardado Leer(Tile tile)
			{
				TileGuardado g = default;
				g.TileType = tile.TileType;
				g.WallType = tile.WallType;
				g.HasTile = tile.HasTile;
				g.IsActuated = tile.IsActuated;
				g.Slope = tile.Slope;
				g.IsHalfBlock = tile.IsHalfBlock;
				g.TileColor = tile.TileColor;
				g.WallColor = tile.WallColor;
				g.LiquidType = tile.LiquidType;
				g.LiquidAmount = tile.LiquidAmount;
				g.RedWire = tile.RedWire;
				g.GreenWire = tile.GreenWire;
				g.BlueWire = tile.BlueWire;
				g.YellowWire = tile.YellowWire;
				return g;
			}

			public readonly void Escribir(Tile tile)
			{
				tile.TileType = TileType;
				tile.WallType = WallType;
				tile.HasTile = HasTile;
				tile.IsActuated = IsActuated;
				tile.Slope = Slope;
				tile.IsHalfBlock = IsHalfBlock;
				tile.TileColor = TileColor;
				tile.WallColor = WallColor;
				tile.LiquidType = LiquidType;
				tile.LiquidAmount = LiquidAmount;
				tile.RedWire = RedWire;
				tile.GreenWire = GreenWire;
				tile.BlueWire = BlueWire;
				tile.YellowWire = YellowWire;
			}

			public readonly bool Igual(Tile tile)
			{
				return tile.TileType == TileType && tile.WallType == WallType && tile.HasTile == HasTile
					&& tile.IsActuated == IsActuated && tile.Slope == Slope && tile.IsHalfBlock == IsHalfBlock
					&& tile.TileColor == TileColor && tile.WallColor == WallColor && tile.LiquidType == LiquidType
					&& tile.LiquidAmount == LiquidAmount && tile.RedWire == RedWire && tile.GreenWire == GreenWire
					&& tile.BlueWire == BlueWire && tile.YellowWire == YellowWire;
			}
		}

		/// <summary>Foto de UN cofre real del mundo dentro del area marcada (el catalogo pide
		/// "tiles + cofres en un radio", no solo terreno). Guarda el INDICE en <c>Main.chest[]</c>
		/// y su posicion, para comprobar al restaurar que sigue siendo el MISMO cofre (uno
		/// destruido y otro nuevo colocado despues podria reciclar el mismo indice - ver
		/// <see cref="CofreGuardado.MismoCofre"/>) y no escribir encima de un cofre distinto.</summary>
		private struct CofreGuardado
		{
			public int Indice;
			public int X;
			public int Y;
			public Item[] Objetos;

			public static CofreGuardado Leer(int indice, Chest cofre)
			{
				Item[] copia = new Item[cofre.item.Length];
				for (int i = 0; i < copia.Length; i++) {
					copia[i] = cofre.item[i] != null ? cofre.item[i].Clone() : new Item();
				}
				return new CofreGuardado { Indice = indice, X = cofre.x, Y = cofre.y, Objetos = copia };
			}

			public readonly bool MismoCofre(out Chest cofre)
			{
				cofre = Indice >= 0 && Main.chest != null && Indice < Main.chest.Length ? Main.chest[Indice] : null;
				return cofre != null && cofre.x == X && cofre.y == Y;
			}

			public readonly void Escribir()
			{
				if (!MismoCofre(out Chest cofre)) {
					return;
				}
				int tope = Math.Min(Objetos.Length, cofre.item.Length);
				for (int i = 0; i < tope; i++) {
					cofre.item[i] = Objetos[i].Clone();
				}
			}

			public readonly bool Igual()
			{
				if (!MismoCofre(out Chest cofre)) {
					return false;
				}
				int tope = Math.Min(Objetos.Length, cofre.item.Length);
				for (int i = 0; i < tope; i++) {
					Item actual = cofre.item[i];
					Item guardado = Objetos[i];
					bool vacioActual = actual == null || actual.IsAir;
					bool vacioGuardado = guardado == null || guardado.IsAir;
					if (vacioActual != vacioGuardado) {
						return false;
					}
					if (!vacioActual && (actual.type != guardado.type || actual.stack != guardado.stack || actual.prefix != guardado.prefix)) {
						return false;
					}
				}
				return true;
			}
		}

		private TileGuardado[] _foto;
		private CofreGuardado[] _cofres;
		private int _origenX, _origenY, _ancho, _alto;
		private DateTime _momento;
		private int _diferentesAhora = -1;
		private int _cofresDistintos = -1;
		private int _contadorRefresco;

		private EtiquetaTk _estado;
		private EtiquetaTk _diferencia;
		private EtiquetaTk _avisoMultijugador;
		private BotonTk _botonMarcar;
		private BotonTk _botonRebobinar;
		private string _ultimoResultado = "";

		/// <summary>true si hay una foto guardada ahora mismo. Publico para la autoprueba.</summary>
		public bool HayFoto => _foto != null;

		/// <summary>Cuantos tiles reales han cambiado desde que se marco la foto, o -1 si todavia
		/// no se ha calculado/no hay foto. Publico para la autoprueba.</summary>
		public int DiferentesAhora => _diferentesAhora;

		/// <summary>Esquina superior izquierda real (en tiles) de la ultima foto marcada. Publico
		/// para la autoprueba.</summary>
		public (int x, int y, int ancho, int alto) AreaFoto => (_origenX, _origenY, _ancho, _alto);

		/// <summary>Cuantos cofres reales cayeron dentro de la ultima foto. Publico para la
		/// autoprueba.</summary>
		public int CofresEnFoto => _cofres != null ? _cofres.Length : 0;

		/// <summary>Cuantos de esos cofres tienen contenido distinto AHORA MISMO, o -1 si no se ha
		/// calculado/no hay foto. Publico para la autoprueba.</summary>
		public int CofresDistintosAhora => _cofresDistintos;

		/// <summary>Indice real en <c>Main.chest[]</c> del cofre N-esimo de la ultima foto (para
		/// que la autoprueba pueda tocarlo sin duplicar la logica de busqueda). -1 si no existe.</summary>
		public int IndiceDeCofreEnFoto(int posicion) =>
			_cofres != null && posicion >= 0 && posicion < _cofres.Length ? _cofres[posicion].Indice : -1;

		public PestanaRebobinar()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			EtiquetaTk titulo = new EtiquetaTk(() => Idiomas.Texto("Exploracion.Rebobinar.Titulo"), 0.95f, 700f, 26f);
			Append(titulo);

			EtiquetaTk explicacion = new EtiquetaTk(
				() => EtiquetaTk.PartirEnLineas(Idiomas.Texto("Exploracion.Rebobinar.Explicacion", RadioTiles * 2 + 1), 700f, 0.75f),
				0.75f, 700f, 40f);
			explicacion.Top.Set(30f, 0f);
			explicacion.ColorTexto = EstiloTk.TextoSuave;
			Append(explicacion);

			// El texto devuelve "" en partida de un jugador (no solo se encoge la caja): EtiquetaTk.
			// DrawSelf dibuja el texto que le den SIN mirar su Height (visto en su propio codigo,
			// solo usa Height para el layout de quien la lea) - un primer intento que solo achicaba
			// la caja con Height.Set(0f) en Update() dejaba el aviso de multijugador VISIBLE igual
			// en partida de un jugador, bug real visto en una captura (ws6-rebobinar-antes.png).
			_avisoMultijugador = new EtiquetaTk(
				() => Main.netMode == NetmodeID.SinglePlayer
					? ""
					: EtiquetaTk.PartirEnLineas(Idiomas.Texto("Exploracion.Rebobinar.SoloUnJugador"), 700f, 0.72f),
				0.72f, 700f, 22f);
			_avisoMultijugador.Top.Set(76f, 0f);
			_avisoMultijugador.ColorTexto = EstiloTk.TextoAviso;
			Append(_avisoMultijugador);

			_botonMarcar = new BotonTk(Idiomas.Texto("Exploracion.Rebobinar.Marcar"), 0.85f);
			_botonMarcar.Top.Set(108f, 0f);
			_botonMarcar.Width.Set(220f, 0f);
			_botonMarcar.Height.Set(34f, 0f);
			_botonMarcar.AlPulsar += Marcar;
			Append(_botonMarcar);

			// El texto es "Rebobinar ahora"/"Rewind now", NUNCA solo "Rebobinar" a secas: bug real
			// visto en la propia autoprueba (log real) - con el mismo texto que la pestaña, el
			// buscador de botones por texto de la autoprueba (panel.PulsarBoton, que recorre TODO
			// el panel, no solo esta pestaña) encontraba antes el boton de la BARRA DE PESTAÑAS
			// ("Rebobinar" para cambiar de pestaña) en vez de este, reconstruyendo la pestaña entera
			// de cero (CrearPestana crea una PestanaRebobinar NUEVA) y perdiendo la foto ya tomada.
			_botonRebobinar = new BotonTk(Idiomas.Texto("Exploracion.Rebobinar.Rebobinar"), 0.85f);
			_botonRebobinar.Top.Set(108f, 0f);
			_botonRebobinar.Left.Set(232f, 0f);
			_botonRebobinar.Width.Set(220f, 0f);
			_botonRebobinar.Height.Set(34f, 0f);
			_botonRebobinar.Habilitado = false;
			_botonRebobinar.AlPulsar += Rebobinar;
			Append(_botonRebobinar);

			_estado = new EtiquetaTk(() => TextoEstado(), 0.78f, 700f, 22f);
			_estado.Top.Set(150f, 0f);
			Append(_estado);

			// Alto de DOS lineas, no una: cuando la foto trae cofres, TextoDiferencia() añade una
			// segunda linea real ("N cofres, M distintos") con un '\n' - Utils.DrawBorderString (el
			// mismo camino que ya usa PestanaMundo para su aviso de permanencia) SI dibuja saltos de
			// linea, pero EtiquetaTk no reserva sitio solo porque el texto crezca: hay que darselo
			// a mano o la fila de "resultado" de abajo se solaparia con la segunda linea.
			_diferencia = new EtiquetaTk(() => TextoDiferencia(), 0.78f, 700f, 44f);
			_diferencia.Top.Set(174f, 0f);
			_diferencia.ColorTexto = EstiloTk.TextoSuave;
			Append(_diferencia);

			EtiquetaTk resultado = new EtiquetaTk(() => _ultimoResultado, 0.75f, 700f, 44f);
			resultado.Top.Set(222f, 0f);
			resultado.ColorTexto = EstiloTk.TextoAviso;
			Append(resultado);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			bool unJugador = Main.netMode == NetmodeID.SinglePlayer;
			_botonMarcar.Habilitado = unJugador;
			_botonRebobinar.Habilitado = unJugador && HayFoto;

			if (++_contadorRefresco >= FotogramasEntreComparaciones) {
				_contadorRefresco = 0;
				RecalcularDiferencia();
			}
		}

		private string TextoEstado()
		{
			if (!HayFoto) {
				return Idiomas.Texto("Exploracion.Rebobinar.SinMarcar");
			}
			int segundos = (int)(DateTime.UtcNow - _momento).TotalSeconds;
			return Idiomas.Texto("Exploracion.Rebobinar.Marcada", _origenX + _ancho / 2, _origenY + _alto / 2, segundos);
		}

		private string TextoDiferencia()
		{
			if (!HayFoto) {
				return "";
			}
			if (_diferentesAhora < 0) {
				return Idiomas.Texto("Exploracion.Rebobinar.Calculando");
			}

			string tiles = _diferentesAhora == 0
				? Idiomas.Texto("Exploracion.Rebobinar.SinCambios")
				: Idiomas.Texto("Exploracion.Rebobinar.Cambiados", _diferentesAhora, _ancho * _alto);

			if (CofresEnFoto == 0) {
				return tiles;
			}

			// Los cofres se enseñan SIEMPRE que la foto tenga alguno, aunque haya 0 tiles distintos:
			// es informacion real que el jugador querria ver igual (ej. abrio uno y no toco nada
			// mas del terreno).
			return tiles + "\n" + Idiomas.Texto("Exploracion.Rebobinar.Cofres", CofresEnFoto, _cofresDistintos);
		}

		/// <summary>Toma la foto de verdad, centrada en el jugador. Publico para la autoprueba.</summary>
		public void Marcar()
		{
			if (Main.netMode != NetmodeID.SinglePlayer) {
				return;
			}

			Player jugador = Main.LocalPlayer;
			int centroX = (int)(jugador.Center.X / 16f);
			int centroY = (int)(jugador.Center.Y / 16f);

			int desde = Math.Max(10, centroX - RadioTiles);
			int hasta = Math.Min(Main.maxTilesX - 10, centroX + RadioTiles);
			int arriba = Math.Max(10, centroY - RadioTiles);
			int abajo = Math.Min(Main.maxTilesY - 10, centroY + RadioTiles);

			int ancho = hasta - desde;
			int alto = abajo - arriba;
			if (ancho <= 0 || alto <= 0) {
				return;
			}

			TileGuardado[] foto = new TileGuardado[ancho * alto];
			for (int x = 0; x < ancho; x++) {
				for (int y = 0; y < alto; y++) {
					foto[y * ancho + x] = TileGuardado.Leer(Main.tile[desde + x, arriba + y]);
				}
			}

			// Cofres reales cuya esquina cae dentro del area (Chest.x/y es SIEMPRE la esquina
			// superior izquierda real del cofre, confirmado en el decompilado de idea 6) - el
			// catalogo pide "tiles + cofres en un radio", no solo terreno.
			List<CofreGuardado> cofres = new List<CofreGuardado>();
			if (Main.chest != null) {
				for (int i = 0; i < Main.chest.Length; i++) {
					Chest cofre = Main.chest[i];
					if (cofre == null || cofre.x < desde || cofre.x >= hasta || cofre.y < arriba || cofre.y >= abajo) {
						continue;
					}
					cofres.Add(CofreGuardado.Leer(i, cofre));
				}
			}

			_foto = foto;
			_cofres = cofres.ToArray();
			_origenX = desde;
			_origenY = arriba;
			_ancho = ancho;
			_alto = alto;
			_momento = DateTime.UtcNow;
			_diferentesAhora = 0;
			_cofresDistintos = 0;
			_ultimoResultado = Idiomas.Texto("Exploracion.Rebobinar.Marcado", ancho, alto, desde, arriba, _cofres.Length);

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " Rebobinar: foto tomada, " + (ancho * alto) +
				" tiles reales y " + _cofres.Length + " cofres reales desde (" + desde + ", " + arriba +
				") hasta (" + hasta + ", " + abajo + ").");
		}

		/// <summary>Cuenta cuantos tiles reales del area fotografiada son distintos AHORA MISMO.
		/// Publico para la autoprueba.</summary>
		public void RecalcularDiferencia()
		{
			if (_foto == null) {
				_diferentesAhora = -1;
				_cofresDistintos = -1;
				return;
			}

			int distintos = 0;
			for (int x = 0; x < _ancho; x++) {
				for (int y = 0; y < _alto; y++) {
					if (!_foto[y * _ancho + x].Igual(Main.tile[_origenX + x, _origenY + y])) {
						distintos++;
					}
				}
			}
			_diferentesAhora = distintos;

			int cofresDistintos = 0;
			if (_cofres != null) {
				for (int i = 0; i < _cofres.Length; i++) {
					if (!_cofres[i].Igual()) {
						cofresDistintos++;
					}
				}
			}
			_cofresDistintos = cofresDistintos;
		}

		/// <summary>Devuelve de verdad los tiles del area a como estaban en la foto. Publico para
		/// la autoprueba.</summary>
		public void Rebobinar()
		{
			if (_foto == null || Main.netMode != NetmodeID.SinglePlayer) {
				return;
			}

			RecalcularDiferencia();
			int distintosAntes = _diferentesAhora;
			int cofresDistintosAntes = _cofresDistintos;

			for (int x = 0; x < _ancho; x++) {
				for (int y = 0; y < _alto; y++) {
					_foto[y * _ancho + x].Escribir(Main.tile[_origenX + x, _origenY + y]);
				}
			}
			if (_cofres != null) {
				for (int i = 0; i < _cofres.Length; i++) {
					_cofres[i].Escribir();
				}
			}

			// Reencuadra los sprites segun sus vecinos (esquinas/uniones) y refresca el minimapa -
			// el mismo par que ya usa el propio motor tras una edicion masiva de tiles. Nunca se
			// llama a NetMessage.SendTileSquare aqui: en partida de un jugador no hay a quien
			// reenviarselo (Main.netMode ya esta comprobado arriba), y en multijugador esta funcion
			// ni siquiera llega a ejecutarse (ver el XMLdoc de la clase).
			WorldGen.RangeFrame(_origenX, _origenY, _origenX + _ancho, _origenY + _alto);
			Main.refreshMap = true;

			_diferentesAhora = 0;
			_cofresDistintos = 0;
			_ultimoResultado = Idiomas.Texto("Exploracion.Rebobinar.Rebobinado", distintosAntes, _ancho * _alto,
				cofresDistintosAntes, _cofres != null ? _cofres.Length : 0);

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " Rebobinar: area (" + _origenX + ", " + _origenY +
				") " + _ancho + "x" + _alto + " restaurada de verdad, con " + (_cofres != null ? _cofres.Length : 0) +
				" cofres. Tiles distintos antes de restaurar: " + distintosAntes + ", cofres distintos antes: " +
				cofresDistintosAntes + ".");
		}
	}
}
