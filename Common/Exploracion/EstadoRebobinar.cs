using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// El estado REAL de "Rebobinar" (idea 10 del catálogo): la foto de tiles/cofres, aparte de
	/// cualquier widget de interfaz.
	/// </summary>
	/// <remarks>
	/// <b>Bug real reportado por el usuario, en vivo, dos veces (21-sep-2026, ver bitacora.md):</b>
	/// "Rebobinar" dejaba de servir para su propósito real ("marca antes de una pelea/construcción,
	/// juega con el panel cerrado, rebobina si algo sale mal") porque la foto vivía en campos de
	/// INSTANCIA de <see cref="TerrakeepMod.UI.Exploracion.PestanaRebobinar"/>, un <c>UIElement</c> -
	/// y <c>PanelTerrakeepSystem</c> crea un <see cref="TerrakeepMod.UI.Panel.PanelTerrakeepState"/>
	/// NUEVO cada vez que se abre el panel (<c>_panel = new PanelTerrakeepState();</c>) y lo
	/// descarta al cerrar (<c>_panel = null;</c>) - confirmado leyendo <c>PanelTerrakeepSystem.
	/// AbrirEnArea</c>/<c>CerrarPanel</c>. Cerrar el panel para ir a pelear (que es EXACTAMENTE lo
	/// que hace cualquier jugador real tras marcar) tiraba la foto entera sin avisar: la próxima vez
	/// que se abría el panel, <c>PestanaRebobinar</c> nacía de cero con <c>HayFoto=false</c>.
	/// <para/>
	/// <b>Arreglo real</b>: mismo patrón YA establecido en este mismo mod para el deshacer de
	/// objetos (<see cref="TerrakeepMod.Common.Undo.Historial"/>/<c>PilaDeSnapshots</c>, estático,
	/// limpiado solo por <c>HistorialSystem.OnWorldLoad</c>/<c>OnWorldUnload</c>) - la foto vive
	/// AQUÍ, en una clase estática ajena a cualquier ciclo de vida de interfaz, y
	/// <see cref="RebobinarSystem"/> es el <c>ModSystem</c> que (a) la limpia solo al cambiar de
	/// mundo/personaje (las coordenadas de tile y los índices de <c>Main.chest[]</c> de la foto
	/// son de ESE mundo en concreto, no valdrían en otro) y (b) recalcula
	/// <see cref="DiferentesAhora"/> cada <see cref="FotogramasEntreComparaciones"/> fotogramas
	/// SIEMPRE que haya un mundo cargado, con el panel abierto o cerrado - antes solo pasaba dentro
	/// de <c>PestanaRebobinar.Update</c>, que solo corre con esa pestaña concreta visible.
	/// <see cref="TerrakeepMod.UI.Exploracion.PestanaRebobinar"/> pasa a ser una vista fina sobre
	/// esta clase: mismos nombres públicos que antes (<c>HayFoto</c>, <c>DiferentesAhora</c>,
	/// <c>Marcar()</c>...), así que la autoprueba y el resto del panel no necesitan cambiar nada.
	/// </remarks>
	public static class EstadoRebobinar
	{
		/// <summary>Radio en tiles alrededor del jugador que se fotografía.</summary>
		public const int RadioTiles = 100;

		public const int FotogramasEntreComparaciones = 30;

		/// <summary>Foto de UN tile: solo datos propios (un <c>Tile</c> es un accesor ligero sobre
		/// arrays compartidos desde 1.4.4+, copiar el struct tal cual seguiría apuntando a la misma
		/// celda viva - ver el XMLdoc original de <c>PestanaRebobinar</c> en el historial de git para
		/// el porqué completo, investigado con el decompilado real).</summary>
		public struct TileGuardado
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

		/// <summary>Foto de UN cofre real del mundo dentro del área marcada.</summary>
		public struct CofreGuardado
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

		private static TileGuardado[] _foto;
		private static CofreGuardado[] _cofres;
		private static int _origenX, _origenY, _ancho, _alto;
		private static DateTime _momento;
		private static int _diferentesAhora = -1;
		private static int _cofresDistintos = -1;

		public static bool HayFoto => _foto != null;
		public static int DiferentesAhora => _diferentesAhora;
		public static (int x, int y, int ancho, int alto) AreaFoto => (_origenX, _origenY, _ancho, _alto);
		public static int CofresEnFoto => _cofres != null ? _cofres.Length : 0;
		public static int CofresDistintosAhora => _cofresDistintos;
		public static DateTime Momento => _momento;

		public static int IndiceDeCofreEnFoto(int posicion) =>
			_cofres != null && posicion >= 0 && posicion < _cofres.Length ? _cofres[posicion].Indice : -1;

		/// <summary>Vacía la foto entera. Solo lo llama <see cref="RebobinarSystem"/> al cambiar de
		/// mundo/personaje - las coordenadas de tile y los índices de <c>Main.chest[]</c> de una
		/// foto son de UN mundo concreto, escribirlos en otro corromperia datos ajenos.</summary>
		public static void Limpiar()
		{
			_foto = null;
			_cofres = null;
			_origenX = _origenY = _ancho = _alto = 0;
			_diferentesAhora = -1;
			_cofresDistintos = -1;
		}

		/// <summary>Toma la foto de verdad, centrada en el jugador.</summary>
		public static void Marcar()
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

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " Rebobinar: foto tomada, " + (ancho * alto) +
				" tiles reales y " + _cofres.Length + " cofres reales desde (" + desde + ", " + arriba +
				") hasta (" + hasta + ", " + abajo + ").");
		}

		/// <summary>Cuenta cuántos tiles/cofres reales del área fotografiada son distintos AHORA
		/// MISMO. Llamado desde <see cref="RebobinarSystem.PostUpdateEverything"/> cada
		/// <see cref="FotogramasEntreComparaciones"/> fotogramas, con o sin panel abierto - antes
		/// solo corria dentro de <c>PestanaRebobinar.Update</c>, que se paraba con el panel
		/// cerrado.</summary>
		public static void RecalcularDiferencia()
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

		/// <summary>Devuelve de verdad los tiles/cofres del área a como estaban en la foto.</summary>
		public static (int tiles, int cofres) Rebobinar()
		{
			if (_foto == null || Main.netMode != NetmodeID.SinglePlayer) {
				return (0, 0);
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

			WorldGen.RangeFrame(_origenX, _origenY, _origenX + _ancho, _origenY + _alto);
			Main.refreshMap = true;

			_diferentesAhora = 0;
			_cofresDistintos = 0;

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " Rebobinar: area (" + _origenX + ", " + _origenY +
				") " + _ancho + "x" + _alto + " restaurada de verdad, con " + (_cofres != null ? _cofres.Length : 0) +
				" cofres. Tiles distintos antes de restaurar: " + distintosAntes + ", cofres distintos antes: " +
				cofresDistintosAntes + ".");

			return (distintosAntes, cofresDistintosAntes);
		}
	}
}
