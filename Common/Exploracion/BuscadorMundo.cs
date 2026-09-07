using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Map;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>Un sitio del mundo donde hay lo que se buscaba.</summary>
	public class ResultadoBusqueda
	{
		/// <summary>Centro del hallazgo, en coordenadas de TILE (que es lo que espera
		/// <c>MapOverlayDrawContext.Draw</c>, no coordenadas de mundo).</summary>
		public Vector2 Tile;

		/// <summary>Cuantos tiles del objetivo hay en esa zona (1 para cofres y NPC).</summary>
		public int Cantidad;

		/// <summary>Texto que se enseña en la lista y en el tooltip del mapa.</summary>
		public string Etiqueta;

		/// <summary>Distancia al jugador en tiles, recalculada al ordenar la lista.</summary>
		public float DistanciaAlJugador;

		/// <summary>Para <see cref="ClaseDeObjetivo.Npcs"/>: tipo del NPC vivo, para pedir su
		/// textura real (<see cref="IconoResultado"/>). -1 en cualquier otra clase de resultado.</summary>
		public int TipoNpc = -1;

		/// <summary>Recorte de animacion que tenia ese NPC en el instante de la busqueda, capturado
		/// de <c>NPC.frame</c> (el motor ya lo mantiene al dia mientras el NPC esta vivo: no hace
		/// falta calcularlo a mano).</summary>
		public Rectangle FrameNpc;
	}

	/// <summary>
	/// Busca cosas en el mundo REAL leyendo los arrays de datos de tiles del motor, sin bloquear
	/// la partida.
	/// </summary>
	/// <remarks>
	/// <b>Como lee el mundo.</b> No se usa <c>Main.tile[x, y]</c>: ese indexador devuelve un
	/// <c>Tile</c>, que en esta version es una <i>struct</i> puente que vuelve a indexar los
	/// arrays reales en cada propiedad. Se usa directamente la API publica
	/// <c>Main.tile.GetData&lt;T&gt;()</c>, que devuelve el array plano de cada campo
	/// (<c>TileTypeData</c>, <c>TileWallWireStateData</c>, <c>WallTypeData</c>,
	/// <c>LiquidData</c> - todos en el namespace <c>Terraria</c>, no en
	/// <c>Terraria.DataStructures</c>). El indice es <c>y + x * Height</c> (codigo real de
	/// <c>Tilemap</c>), o sea que recorrer una columna entera es lectura secuencial de memoria:
	/// por eso el barrido va por columnas y no por filas.
	/// <para />
	/// <b>Por que troceado por fotogramas y no en un hilo.</b> El plan admitia las dos. Se ha
	/// elegido trocear porque el hilo aparte no aporta nada aqui y si trae problemas: el juego
	/// muta esos mismos arrays mientras corre (bloques que se rompen, arena que cae, liquidos), y
	/// leerlos desde otro hilo daria lecturas a medias sin ninguna garantia, ademas de que los
	/// arrays se reasignan al cambiar de mundo. Troceando, todo el trabajo ocurre en el hilo del
	/// juego, entre dos actualizaciones, y no hay ni una condicion de carrera. El coste medido
	/// del barrido completo es de decenas de milisegundos, asi que con un presupuesto de 2 ms por
	/// fotograma un mundo pequeño se termina en menos de medio segundo sin que se note un tiron.
	/// <para />
	/// <b>Agrupado en zonas.</b> Un mundo tiene cientos de miles de tiles de cobre; una lista de
	/// tiles sueltos no le sirve a nadie. Los hallazgos se acumulan en una rejilla de celdas de
	/// <see cref="LadoCelda"/> tiles, y lo que se devuelve son las zonas con mas cantidad. Es lo
	/// que uno quiere ver en un mapa: "una veta gorda aqui", no 4.000 puntos.
	/// </remarks>
	public sealed class BuscadorMundo
	{
		/// <summary>Lado en tiles de la celda de agrupacion.</summary>
		public const int LadoCelda = 25;

		/// <summary>Cuantas zonas se devuelven como mucho.</summary>
		public const int MaximoResultados = 150;

		private Terraria.TileTypeData[] _tipos;
		private Terraria.WallTypeData[] _paredes;
		private Terraria.LiquidData[] _liquidos;
		private Terraria.TileWallWireStateData[] _estado;
		private WorldMap _mapa;

		private int _ancho;
		private int _alto;
		private int _columna;

		private ObjetivoBusqueda _objetivo;
		private bool _soloExplorado;
		private bool[] _esObjetivo;      // indexado por tipo de tile/pared: O(1) por tile
		private int _liquidoBuscado = -1;

		private Dictionary<int, int> _conteoPorCelda;
		private Dictionary<int, long> _sumaX;
		private Dictionary<int, long> _sumaY;

		private long _tilesMirados;
		private long _tilesEncontrados;
		private readonly Stopwatch _cronometro = new Stopwatch();
		private double _milisegundosGastados;

		/// <summary>true mientras queda mundo por recorrer.</summary>
		public bool EnMarcha { get; private set; }

		/// <summary>true cuando la ultima busqueda termino entera.</summary>
		public bool Terminada { get; private set; }

		/// <summary>0..1.</summary>
		public float Progreso => _ancho == 0 ? 0f : (float)_columna / _ancho;

		public ObjetivoBusqueda Objetivo => _objetivo;

		public bool SoloExplorado => _soloExplorado;

		public long TilesMirados => _tilesMirados;

		public long TilesEncontrados => _tilesEncontrados;

		public double MilisegundosGastados => _milisegundosGastados;

		/// <summary>Resultados de la ultima busqueda terminada, ya ordenados.</summary>
		public List<ResultadoBusqueda> Resultados { get; private set; } = new List<ResultadoBusqueda>();

		/// <summary>
		/// Empieza una busqueda. Captura AQUI las referencias a los arrays del motor y el tamaño
		/// del mundo: si la partida se descarga a mitad, se sigue leyendo memoria valida (los
		/// arrays siguen vivos mientras los tengamos referenciados) en vez de petar con un indice
		/// fuera de rango contra un mundo nuevo mas pequeño.
		/// </summary>
		public void Empezar(ObjetivoBusqueda objetivo, bool soloExplorado)
		{
			Cancelar();

			_objetivo = objetivo;
			_soloExplorado = soloExplorado;
			Resultados = new List<ResultadoBusqueda>();
			// Se ponen a cero AQUI y no solo en la rama del barrido de tiles: si no, una busqueda
			// de cofres o de NPC arrastraria en su resumen los numeros de la busqueda anterior.
			_tilesMirados = 0;
			_tilesEncontrados = 0;
			_milisegundosGastados = 0.0;
			_ancho = 0;
			_columna = 0;

			if (objetivo == null || !objetivo.Resuelto || Main.tile.Width == 0) {
				Terminada = true;
				return;
			}

			// Los barridos de entidades no recorren el mundo: son arrays cortos.
			if (objetivo.Clase == ClaseDeObjetivo.Cofres) {
				Resultados = BuscarCofres();
				Ordenar();
				Terminada = true;
				return;
			}
			if (objetivo.Clase == ClaseDeObjetivo.Npcs) {
				Resultados = BuscarNpcs();
				Ordenar();
				Terminada = true;
				return;
			}

			_tipos = Main.tile.GetData<Terraria.TileTypeData>();
			_estado = Main.tile.GetData<Terraria.TileWallWireStateData>();
			_paredes = Main.tile.GetData<Terraria.WallTypeData>();
			_liquidos = Main.tile.GetData<Terraria.LiquidData>();
			_mapa = Main.Map;
			_ancho = Main.tile.Width;
			_alto = Main.tile.Height;

			_esObjetivo = null;
			_liquidoBuscado = -1;

			if (objetivo.Clase == ClaseDeObjetivo.Liquido) {
				_liquidoBuscado = objetivo.Liquido;
			}
			else {
				int mayor = 0;
				foreach (int tipo in objetivo.Tipos) {
					if (tipo > mayor) {
						mayor = tipo;
					}
				}
				_esObjetivo = new bool[mayor + 1];
				foreach (int tipo in objetivo.Tipos) {
					_esObjetivo[tipo] = true;
				}
			}

			_conteoPorCelda = new Dictionary<int, int>();
			_sumaX = new Dictionary<int, long>();
			_sumaY = new Dictionary<int, long>();
			_columna = 0;
			_tilesMirados = 0;
			_tilesEncontrados = 0;
			_milisegundosGastados = 0.0;
			EnMarcha = true;
			Terminada = false;
		}

		public void Cancelar()
		{
			EnMarcha = false;
			Terminada = false;
			_columna = 0;
			_conteoPorCelda = null;
			_sumaX = null;
			_sumaY = null;
			// Se sueltan las referencias a los arrays del motor: son los del mundo, no queremos
			// que el buscador los mantenga vivos despues de salir de la partida.
			_tipos = null;
			_estado = null;
			_paredes = null;
			_liquidos = null;
			_mapa = null;
		}

		/// <summary>
		/// Avanza el barrido lo que quepa en <paramref name="presupuestoMs"/> milisegundos. Se
		/// llama una vez por fotograma.
		/// </summary>
		public void Avanzar(double presupuestoMs)
		{
			if (!EnMarcha) {
				return;
			}

			_cronometro.Restart();
			bool porLiquido = _liquidoBuscado >= 0;
			bool porPared = _objetivo.Clase == ClaseDeObjetivo.Pared;

			while (_columna < _ancho) {
				int baseColumna = _columna * _alto;
				for (int y = 0; y < _alto; y++) {
					int i = baseColumna + y;
					bool acierto;

					if (porLiquido) {
						acierto = _liquidos[i].Amount > 0 && _liquidos[i].LiquidType == _liquidoBuscado;
					}
					else if (porPared) {
						ushort tipo = _paredes[i].Type;
						acierto = tipo < _esObjetivo.Length && _esObjetivo[tipo];
					}
					else {
						// HasTile es imprescindible: un tile sin bloque puede conservar un Type
						// residual del bloque que hubo antes, y sin esta comprobacion saldrian
						// vetas fantasma donde ya no queda nada.
						acierto = _estado[i].HasTile
							&& _tipos[i].Type < _esObjetivo.Length && _esObjetivo[_tipos[i].Type];
					}

					if (!acierto) {
						continue;
					}

					if (_soloExplorado && (_mapa == null || !_mapa.IsRevealed(_columna, y))) {
						continue;
					}

					Acumular(_columna, y);
				}

				_tilesMirados += _alto;
				_columna++;

				// El reloj se mira cada 8 columnas: preguntarlo por cada tile costaria mas que el
				// propio barrido.
				if ((_columna & 7) == 0 && _cronometro.Elapsed.TotalMilliseconds >= presupuestoMs) {
					break;
				}
			}

			_cronometro.Stop();
			_milisegundosGastados += _cronometro.Elapsed.TotalMilliseconds;

			if (_columna >= _ancho) {
				Rematar();
			}
		}

		private void Acumular(int x, int y)
		{
			_tilesEncontrados++;

			int celdaX = x / LadoCelda;
			int celdaY = y / LadoCelda;
			int clave = celdaY * 100000 + celdaX;

			int cuenta;
			if (_conteoPorCelda.TryGetValue(clave, out cuenta)) {
				_conteoPorCelda[clave] = cuenta + 1;
				_sumaX[clave] = _sumaX[clave] + x;
				_sumaY[clave] = _sumaY[clave] + y;
			}
			else {
				_conteoPorCelda[clave] = 1;
				_sumaX[clave] = x;
				_sumaY[clave] = y;
			}
		}

		private void Rematar()
		{
			EnMarcha = false;
			Terminada = true;

			List<ResultadoBusqueda> zonas = new List<ResultadoBusqueda>();
			foreach (KeyValuePair<int, int> par in _conteoPorCelda) {
				int cuenta = par.Value;
				zonas.Add(new ResultadoBusqueda {
					Tile = new Vector2((float)_sumaX[par.Key] / cuenta, (float)_sumaY[par.Key] / cuenta),
					Cantidad = cuenta,
					Etiqueta = _objetivo.EtiquetaLegible() + " x" + cuenta
				});
			}

			// Primero las zonas con mas cantidad (una veta de 300 vale mas que una de 2), y de esas
			// nos quedamos con las mejores; luego se ordena por cercania para enseñarlas.
			zonas.Sort((ResultadoBusqueda a, ResultadoBusqueda b) => b.Cantidad.CompareTo(a.Cantidad));
			if (zonas.Count > MaximoResultados) {
				zonas.RemoveRange(MaximoResultados, zonas.Count - MaximoResultados);
			}

			Resultados = zonas;
			Ordenar();

			_conteoPorCelda = null;
			_sumaX = null;
			_sumaY = null;
		}

		/// <summary>Ordena por cercania al jugador y rellena la distancia.</summary>
		private void Ordenar()
		{
			Vector2 jugador = Main.LocalPlayer != null && Main.LocalPlayer.active
				? Main.LocalPlayer.Center / 16f
				: new Vector2(Main.maxTilesX / 2f, Main.maxTilesY / 2f);

			foreach (ResultadoBusqueda resultado in Resultados) {
				resultado.DistanciaAlJugador = Vector2.Distance(resultado.Tile, jugador);
			}
			Resultados.Sort((ResultadoBusqueda a, ResultadoBusqueda b) =>
				a.DistanciaAlJugador.CompareTo(b.DistanciaAlJugador));
		}

		/// <summary>
		/// Cofres y comodas del mundo. <c>Main.chest</c> es un array de 8.000 posiciones con
		/// huecos a null: barrerlo entero cuesta microsegundos, no hace falta trocear nada.
		/// </summary>
		private List<ResultadoBusqueda> BuscarCofres()
		{
			List<ResultadoBusqueda> lista = new List<ResultadoBusqueda>();
			if (Main.chest == null) {
				return lista;
			}

			for (int i = 0; i < Main.chest.Length; i++) {
				Chest cofre = Main.chest[i];
				if (cofre == null) {
					continue;
				}
				if (_soloExplorado && (Main.Map == null || !Main.Map.IsRevealed(cofre.x, cofre.y))) {
					continue;
				}

				int objetos = 0;
				string primero = null;
				if (cofre.item != null) {
					for (int k = 0; k < cofre.item.Length; k++) {
						Item objeto = cofre.item[k];
						if (objeto != null && !objeto.IsAir) {
							objetos++;
							if (primero == null) {
								primero = objeto.Name;
							}
						}
					}
				}

				string nombre = string.IsNullOrEmpty(cofre.name) ? "Cofre" : cofre.name;
				lista.Add(new ResultadoBusqueda {
					Tile = new Vector2(cofre.x + 1f, cofre.y + 1f),
					Cantidad = objetos,
					Etiqueta = nombre + " (" + objetos + " objetos" +
						(primero != null ? ", " + primero + "..." : "") + ")"
				});
			}

			return lista;
		}

		/// <summary>NPC vivos ahora mismo. Igual de barato: 200 y pico posiciones.</summary>
		private List<ResultadoBusqueda> BuscarNpcs()
		{
			List<ResultadoBusqueda> lista = new List<ResultadoBusqueda>();
			if (Main.npc == null) {
				return lista;
			}

			for (int i = 0; i < Main.npc.Length; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active || npc.type <= 0) {
					continue;
				}

				string nombre = npc.GivenOrTypeName;
				if (npc.boss) {
					nombre += " (JEFE)";
				}
				else if (npc.townNPC) {
					nombre += " (habitante)";
				}

				lista.Add(new ResultadoBusqueda {
					Tile = npc.Center / 16f,
					Cantidad = 1,
					Etiqueta = nombre + " - " + npc.life + "/" + npc.lifeMax + " de vida",
					TipoNpc = npc.type,
					FrameNpc = npc.frame
				});
			}

			return lista;
		}

		/// <summary>Resumen de la ultima busqueda, para el log de evidencia.</summary>
		public string Resumen()
		{
			if (_objetivo == null) {
				return "sin busqueda";
			}

			string cabecera = "\"" + _objetivo.EtiquetaLegible() + "\" (" + _objetivo.Clase +
				(_objetivo.Tipos.Count > 0 ? ", tipos=" + string.Join("/", _objetivo.Tipos) : "") + ")" +
				(_soloExplorado ? " solo en lo explorado" : " en TODO el mundo");

			if (EnMarcha) {
				return cabecera + ": en marcha, columna " + _columna + " de " + _ancho +
					" (" + (int)(Progreso * 100f) + "%).";
			}

			if (_objetivo.Clase == ClaseDeObjetivo.Cofres || _objetivo.Clase == ClaseDeObjetivo.Npcs) {
				return cabecera + ": " + Resultados.Count + " encontrados (barrido directo del array " +
					(_objetivo.Clase == ClaseDeObjetivo.Cofres ? "Main.chest" : "Main.npc") + ", sin recorrer tiles).";
			}

			return cabecera + ": " + _tilesEncontrados + " tiles encontrados en " +
				_tilesMirados + " mirados, agrupados en " + Resultados.Count + " zonas, " +
				Math.Round(_milisegundosGastados, 1) + " ms de CPU en total.";
		}
	}
}
