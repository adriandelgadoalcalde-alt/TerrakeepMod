using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Personaje
{
	/// <summary>
	/// Capa de acceso al personaje VIVO (<see cref="Main.LocalPlayer"/>). No guarda copias de
	/// nada: cada llamada lee o escribe el objeto real del juego en ese instante.
	/// <para />
	/// Todos los tamaños y nombres de campo de aqui estan comprobados contra el
	/// <c>tModLoader.dll</c> REALMENTE instalado (v2026.7.3.0) con <c>ilspycmd</c>, no contra la
	/// referencia decompilada vieja (v1.4.4.9) del repo hermano, porque entre las dos versiones
	/// hay diferencias reales de API (ya se comio una WS0).
	/// </summary>
	public static class PersonajeVivo
	{
		// ---- Reparto real de Player.inventory (comprobado: `inventory = new Item[59]`) ----

		/// <summary>Numero de ranuras del inventario principal (las 10x5 de la mochila).</summary>
		public const int SlotsPrincipales = 50;

		/// <summary>Primera ranura de monedas. Son 4: cobre, plata, oro y platino.</summary>
		public const int PrimerSlotMonedas = 50;

		/// <summary>Primera ranura de municion. Son 4.</summary>
		public const int PrimerSlotMunicion = 54;

		/// <summary>Ranura 58: la usa el juego como comodin, no se enseña.</summary>
		public const int SlotComodin = 58;

		/// <summary>Ranuras de cada uno de los 4 almacenes (Hucha, Caja fuerte, Forja, Boveda).</summary>
		public const int SlotsAlmacen = 40;

		/// <summary>Ranuras reales de <c>Player.armor</c>: 0-2 armadura, 3-9 accesorios,
		/// 10-12 vanidad de armadura, 13-19 vanidad de accesorios.</summary>
		public const int SlotsEquipo = 20;

		/// <summary>Ranuras de <c>Player.dye</c>. El tinte i vale a la vez para
		/// <c>armor[i]</c> y para <c>armor[10 + i]</c>.</summary>
		public const int SlotsTinte = 10;

		/// <summary>Ranuras de <c>Player.miscEquips</c>: mascota, mascota de luz, vagoneta,
		/// montura y gancho, en ese orden.</summary>
		public const int SlotsMisc = 5;

		/// <summary>Vida maxima que se puede alcanzar sin objetos permanentes (5 corazones de
		/// base + 15 cristales de vida x20).</summary>
		public const int VidaMaximaBase = 500;

		/// <summary>Mana maximo alcanzable con estrellas caidas.</summary>
		public const int ManaMaximoBase = 200;

		public static Player Jugador => Main.LocalPlayer;

		public static bool HayJugador =>
			Main.LocalPlayer != null && Main.LocalPlayer.active && !Main.gameMenu;

		// ---------------------------------------------------------------- almacenes

		/// <summary>Nombres de los 4 almacenes, en el mismo orden que
		/// <see cref="ObtenerAlmacen"/>.</summary>
		public static readonly string[] NombresAlmacen = {
			"Hucha", "Caja fuerte", "Forja del Defensor", "Boveda del Vacio"
		};

		/// <summary>Array vivo de objetos del almacen indicado (0..3).</summary>
		public static Item[] ObtenerAlmacen(int indice)
		{
			Player jugador = Jugador;
			switch (indice) {
				case 0: return jugador.bank.item;
				case 1: return jugador.bank2.item;
				case 2: return jugador.bank3.item;
				default: return jugador.bank4.item;
			}
		}

		// ---------------------------------------------------------------- dinero

		/// <summary>
		/// Dinero total en cobre equivalente. Recorre el inventario ENTERO (las monedas no viven
		/// solo en las ranuras 50-53: tambien se apilan en la mochila) y los 4 almacenes, que es
		/// como lo cuenta el propio juego con <see cref="Utils.CoinsCount"/>.
		/// </summary>
		public static long DineroTotal(out long enInventario, out long enAlmacenes)
		{
			bool desbordado;
			Player jugador = Jugador;

			enInventario = Utils.CoinsCount(out desbordado, jugador.inventory, SlotComodin);
			enAlmacenes = Utils.CoinsCount(out desbordado, jugador.bank.item)
				+ Utils.CoinsCount(out desbordado, jugador.bank2.item)
				+ Utils.CoinsCount(out desbordado, jugador.bank3.item)
				+ Utils.CoinsCount(out desbordado, jugador.bank4.item);

			return enInventario + enAlmacenes;
		}

		/// <summary>Pasa una cantidad en cobre a "1 plat 20 oro 3 plata 5 cobre".</summary>
		public static string FormatearDinero(long cobre)
		{
			if (cobre <= 0L) {
				return "0 cobre";
			}

			int[] partes = Utils.CoinsSplit(cobre);
			StringBuilder sb = new StringBuilder();
			AnexarMoneda(sb, partes[3], "plat");
			AnexarMoneda(sb, partes[2], "oro");
			AnexarMoneda(sb, partes[1], "plata");
			AnexarMoneda(sb, partes[0], "cobre");
			return sb.ToString();
		}

		private static void AnexarMoneda(StringBuilder sb, int cantidad, string nombre)
		{
			if (cantidad <= 0) {
				return;
			}
			if (sb.Length > 0) {
				sb.Append(' ');
			}
			sb.Append(cantidad).Append(' ').Append(nombre);
		}

		// ---------------------------------------------------------------- buffs

		/// <summary>Numero de ranuras de buff del jugador. En esta version son 44 mas las que
		/// añadan los mods (<c>Player.maxBuffs => 44 + BuffLoader.extraPlayerBuffCount</c>), asi
		/// que NO se puede dar por fijo: se lee del array real.</summary>
		public static int RanurasBuff => Jugador.buffType.Length;

		/// <summary>Ultimo id de buff valido (incluye los de mods cargados).</summary>
		public static int UltimoIdBuff => BuffLoader.BuffCount - 1;

		/// <summary>Convierte ticks a un texto corto tipo "3:20" o "permanente".</summary>
		public static string FormatearTiempoBuff(int ticks)
		{
			if (ticks < 0) {
				return "permanente";
			}

			int segundosTotales = ticks / 60;
			if (segundosTotales >= 3600) {
				return (segundosTotales / 3600) + " h " + ((segundosTotales % 3600) / 60) + " min";
			}
			return (segundosTotales / 60) + ":" + (segundosTotales % 60).ToString("00");
		}

		/// <summary>Nombre legible de un buff, o "" si el id no existe.</summary>
		public static string NombreBuff(int tipo)
		{
			if (tipo <= 0 || tipo >= BuffLoader.BuffCount) {
				return "";
			}
			string nombre = Lang.GetBuffName(tipo);
			return string.IsNullOrEmpty(nombre) ? ("Buff " + tipo) : nombre;
		}

		// ---------------------------------------------------------------- apariencia

		/// <summary>Numero total de peinados, incluidos los que añadan los mods.</summary>
		public static int TotalPeinados => HairLoader.Count;

		/// <summary>Nombre legible de una variante de piel/genero
		/// (<c>Player.skinVariant</c>, valores de <see cref="PlayerVariantID"/>).</summary>
		public static string NombreVariante(int variante)
		{
			switch (variante) {
				case PlayerVariantID.MaleStarter: return "Hombre - inicial";
				case PlayerVariantID.MaleSticker: return "Hombre - pegatina";
				case PlayerVariantID.MaleGangster: return "Hombre - gangster";
				case PlayerVariantID.MaleCoat: return "Hombre - abrigo";
				case PlayerVariantID.FemaleStarter: return "Mujer - inicial";
				case PlayerVariantID.FemaleSticker: return "Mujer - pegatina";
				case PlayerVariantID.FemaleGangster: return "Mujer - gangster";
				case PlayerVariantID.FemaleCoat: return "Mujer - abrigo";
				case PlayerVariantID.MaleDress: return "Hombre - vestido";
				case PlayerVariantID.FemaleDress: return "Mujer - vestido";
				case PlayerVariantID.MaleDisplayDoll: return "Hombre - maniqui";
				case PlayerVariantID.FemaleDisplayDoll: return "Mujer - maniqui";
				default: return "Variante " + variante;
			}
		}

		private static List<int> _tintesPeloItem;
		private static List<string> _tintesPeloNombre;

		/// <summary>
		/// Lista de tintes de pelo REALES disponibles, en el orden del identificador de sombreador
		/// que guarda <c>Player.hairDye</c>. La posicion 0 es siempre "ninguno".
		/// <para />
		/// No hay ninguna tabla publica con los tintes de pelo: <c>HairShaderDataSet</c> tiene el
		/// contador (<c>_shaderDataCount</c>) declarado <c>protected internal</c>. Lo que si es
		/// publico es <c>ContentSamples.ItemsByType</c>, y cada objeto tinte de pelo lleva su
		/// propio identificador de sombreador en <c>Item.hairDye</c> (es literalmente de donde lo
		/// copia el juego: <c>Player.cs</c> hace <c>hairDye = item.hairDye;</c>). Recorriendo los
		/// objetos se obtiene la lista exacta, con nombres de verdad, y ademas cubre los tintes de
		/// cualquier mod cargado sin tocar nada.
		/// </summary>
		public static void CargarTintesPelo(out List<int> sombreadores, out List<string> nombres)
		{
			if (_tintesPeloItem == null) {
				_tintesPeloItem = new List<int> { 0 };
				_tintesPeloNombre = new List<string> { "Ninguno" };

				SortedDictionary<int, string> encontrados = new SortedDictionary<int, string>();
				foreach (KeyValuePair<int, Item> par in ContentSamples.ItemsByType) {
					Item muestra = par.Value;
					if (muestra != null && muestra.hairDye > 0 && !encontrados.ContainsKey(muestra.hairDye)) {
						encontrados[muestra.hairDye] = muestra.Name;
					}
				}

				foreach (KeyValuePair<int, string> par in encontrados) {
					_tintesPeloItem.Add(par.Key);
					_tintesPeloNombre.Add(par.Value);
				}
			}

			sombreadores = _tintesPeloItem;
			nombres = _tintesPeloNombre;
		}

		/// <summary>Se llama al descargar el mod: los catalogos cacheados no pueden sobrevivir a
		/// una recarga de mods porque los ids cambian.</summary>
		public static void Descargar()
		{
			_tintesPeloItem = null;
			_tintesPeloNombre = null;
		}

		// ---------------------------------------------------------------- utilidades

		public static int Acotar(int valor, int minimo, int maximo)
		{
			if (valor < minimo) {
				return minimo;
			}
			return valor > maximo ? maximo : valor;
		}

		/// <summary>Descripcion corta del objeto para el log de las pruebas.</summary>
		public static string DescribirObjeto(Item objeto)
		{
			if (objeto == null || objeto.IsAir) {
				return "(vacio)";
			}
			return "\"" + objeto.Name + "\" type=" + objeto.type + " stack=" + objeto.stack
				+ " prefix=" + objeto.prefix;
		}
	}
}
