using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// <b>Idea 1 del catálogo de funciones ("Entrenador de jefe / arena de ensayo"):</b> desde la
	/// lectura de jefe de la Guía, "Practicar" - foto del personaje y de una zona de arena,
	/// teletransporte, invocar al jefe real de esta partida (vida/daño ya escalados por el modo de
	/// dificultad, los mismos números que enseña <see cref="EvaluadorGuia.StatsDeJefe"/>), y al
	/// acabar (ganes, mueras o canceles) restaurarlo TODO - personaje, terreno de la zona, y la
	/// bandera de "derrotado" del propio jefe, para que practicar nunca cierre un tramo de la Guía
	/// por accidente.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por qué este roster concreto y no "cualquier jefe".</b> Investigado con el decompilado
	/// real de <c>NPC.cs</c> (el bloque de muerte de cada tipo de jefe) antes de escribir nada:
	/// TODOS estos jefes solo tocan su propia bandera <c>downedXxx</c> al morir, nada más - excepto
	/// el <b>Muro de Carne</b>, cuyo bloque de muerte llama a <c>WorldGen.StartHardmode()</c>: una
	/// conversión PERMANENTE de tiles de TODO el mundo (menas nuevas, franja de Corrupción/Hallow)
	/// que ninguna foto de zona puede deshacer. <b>LÍMITE REAL, no una decisión de alcance</b>: el
	/// Muro de Carne se excluye a propósito de <see cref="Roster"/> y no aparece nunca como
	/// practicable, con este mismo motivo documentado en <see cref="EsPracticable"/>. El resto de
	/// jefes post-Muro de Carne (mecánicos, Plantera, Golem, Cultista, Luna) quedan FUERA de esta
	/// ronda por un motivo real distinto (no un límite, un alcance): cada uno arrastra más
	/// interacciones cruzadas sin auditar todavía una a una con el mismo cuidado (p.ej. Plantera
	/// abre el Templo, el Culto repite la aparición de las Torres) - quedan documentados como
	/// candidatos reales para la próxima ronda, con el mismo método de auditoría ya aplicado aquí.
	/// </para>
	/// <para>
	/// <b>Por qué se restaura también la bandera de "derrotado".</b> El catálogo pide "al acabar,
	/// restaurar todo" - y la propia autoprueba de la Guía (<c>AutopruebaGuia.MarcarXDerrotadoDeMentira</c>)
	/// ya demuestra que tocar <c>NPC.downedBossN</c>/<c>downedQueenBee</c> a mano y devolverlo es
	/// seguro y real (son campos públicos simples, sin lógica oculta detrás del set). Sin esto,
	/// "practicar" y de verdad ganar cerraría el tramo real de la Guía por accidente - justo lo que
	/// una ARENA DE ENSAYO no debería hacer nunca.
	/// </para>
	/// <para>
	/// <b>Por qué también se fotografía el terreno de la zona.</b> Reutiliza el mismo struct y la
	/// misma técnica ya construida y verificada para la idea 10 ("rebobinar el mundo"): un jefe
	/// real puede destruir bloques de verdad durante la pelea (el Devorador de Mundos escarba
	/// tierra, Esqueletrón puede romper bloques con sus manos) - sin esto, "practicar" dejaría
	/// cráteres permanentes en la zona de la arena.
	/// </para>
	/// </remarks>
	public static class EntrenadorJefe
	{
		public readonly struct EntradaRoster
		{
			public readonly int Tipo;
			public readonly Func<bool> LeerDerrotado;
			public readonly Action<bool> EscribirDerrotado;

			public EntradaRoster(int tipo, Func<bool> leer, Action<bool> escribir)
			{
				Tipo = tipo;
				LeerDerrotado = leer;
				EscribirDerrotado = escribir;
			}
		}

		/// <summary>El subconjunto real investigado y confirmado seguro (ver el XMLdoc de la
		/// clase). El Muro de Carne NUNCA se añade aquí - es un límite real, no un olvido.</summary>
		public static readonly EntradaRoster[] Roster = {
			new EntradaRoster(NPCID.EyeofCthulhu, () => NPC.downedBoss1, v => NPC.downedBoss1 = v),
			new EntradaRoster(NPCID.EaterofWorldsHead, () => NPC.downedBoss2, v => NPC.downedBoss2 = v),
			new EntradaRoster(NPCID.BrainofCthulhu, () => NPC.downedBoss2, v => NPC.downedBoss2 = v),
			new EntradaRoster(NPCID.QueenBee, () => NPC.downedQueenBee, v => NPC.downedQueenBee = v),
			new EntradaRoster(NPCID.SkeletronHead, () => NPC.downedBoss3, v => NPC.downedBoss3 = v),
		};

		/// <summary>true si <paramref name="tipoNpc"/> es un jefe del roster seguro - lo usa
		/// <see cref="TerrakeepMod.UI.Guia.ContenidoGuia"/> para decidir si enseña el botón
		/// "Practicar" para el jefe del paso actual.</summary>
		public static bool EsPracticable(int tipoNpc)
		{
			return BuscarEnRoster(tipoNpc, out _);
		}

		private static bool BuscarEnRoster(int tipoNpc, out EntradaRoster entrada)
		{
			for (int i = 0; i < Roster.Length; i++) {
				if (Roster[i].Tipo == tipoNpc) {
					entrada = Roster[i];
					return true;
				}
			}
			entrada = default;
			return false;
		}

		// --- Radio y posicion fija de la arena (ver el XMLdoc de la clase para el porque) --------
		public const int RadioArenaTiles = 70;
		private const int ArenaTileX = 100;
		private const int ArenaTileY = 150;

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
			}
		}

		private static bool _activa;
		private static bool _terminada;
		private static int _tipoJefe;
		private static EntradaRoster _entradaRoster;
		private static int _jefeWhoAmI = -1;

		private static Vector2 _posicionOriginal;
		private static float _vidaOriginal;
		private static int _manaOriginal;
		private static int[] _buffTypeOriginal;
		private static int[] _buffTimeOriginal;
		private static bool _derrotadoOriginalDelJefe;

		private static TileGuardado[] _fotoArena;
		private static int _origenXArena, _origenYArena, _anchoArena, _altoArena;

		private static int _fotogramaInicio;
		private static int _danoAlJefe;
		private static int _danoRecibido;
		private static string _ultimoInforme = "";
		private static bool _ultimaVictoria;

		/// <summary>true mientras hay una practica en marcha (jefe vivo, esperando que gane o
		/// pierda el jugador, o que se cancele a mano).</summary>
		public static bool Activa => _activa;

		/// <summary>true justo despues de terminar una practica (gane, pierda o se cancele), hasta
		/// que <see cref="LimpiarInforme"/> lo quite - para que la UI pueda enseñar el informe
		/// final aunque ya no haya sesion activa.</summary>
		public static bool Terminada => _terminada;

		public static bool UltimaVictoria => _ultimaVictoria;
		public static string UltimoInforme => _ultimoInforme;
		public static int TipoJefeActivo => _tipoJefe;

		/// <summary>Segundos reales que lleva viva la practica actual. Publico para la UI en vivo Y
		/// para la autoprueba.</summary>
		public static float SegundosTranscurridos =>
			_activa ? (Main.GameUpdateCount - (uint)_fotogramaInicio) / 60f : 0f;

		public static int DanoAlJefeAhora => _danoAlJefe;
		public static int DanoRecibidoAhora => _danoRecibido;

		/// <summary>Empieza una practica de verdad. Devuelve false (sin tocar nada) si el tipo no
		/// esta en el roster seguro, si ya hay una practica en marcha o si no hay jugador real.</summary>
		public static bool Iniciar(int tipoJefe)
		{
			if (_activa || !BuscarEnRoster(tipoJefe, out EntradaRoster entrada)) {
				return false;
			}

			Player jugador = Main.LocalPlayer;
			if (jugador == null || !jugador.active) {
				return false;
			}

			// --- foto del jugador ---------------------------------------------------------------
			_posicionOriginal = jugador.position;
			_vidaOriginal = jugador.statLife;
			_manaOriginal = jugador.statMana;
			_buffTypeOriginal = (int[])jugador.buffType.Clone();
			_buffTimeOriginal = (int[])jugador.buffTime.Clone();

			// --- foto de la bandera de "derrotado" del jefe -------------------------------------
			_entradaRoster = entrada;
			_derrotadoOriginalDelJefe = entrada.LeerDerrotado();

			// --- foto del terreno de la arena (misma tecnica que la idea 10, "rebobinar") -------
			int desde = Math.Max(10, ArenaTileX - RadioArenaTiles);
			int hasta = Math.Min(Main.maxTilesX - 10, ArenaTileX + RadioArenaTiles);
			int arriba = Math.Max(10, ArenaTileY - RadioArenaTiles);
			int abajo = Math.Min(Main.maxTilesY - 10, ArenaTileY + RadioArenaTiles);
			int ancho = hasta - desde;
			int alto = abajo - arriba;

			TileGuardado[] foto = new TileGuardado[ancho * alto];
			for (int x = 0; x < ancho; x++) {
				for (int y = 0; y < alto; y++) {
					foto[y * ancho + x] = TileGuardado.Leer(Main.tile[desde + x, arriba + y]);
				}
			}
			_fotoArena = foto;
			_origenXArena = desde;
			_origenYArena = arriba;
			_anchoArena = ancho;
			_altoArena = alto;

			// --- teletransporte real, vida y mana al maximo (practica limpia, no arrastrar daño
			// de antes de entrar) --------------------------------------------------------------
			Vector2 destino = new Vector2((ArenaTileX + 0.5f) * 16f, (ArenaTileY + 0.5f) * 16f);
			jugador.Teleport(destino, 1);
			jugador.statLife = jugador.statLifeMax2;
			jugador.statMana = jugador.statManaMax2;

			// --- invocar al jefe de verdad, con las stats reales de esta partida (el mismo
			// NPC.SpawnBoss que usan los objetos de invocacion reales de vanilla) --------------
			int antes = -1;
			for (int i = 0; i < Main.npc.Length; i++) {
				if (!Main.npc[i].active) {
					antes = i;
					break;
				}
			}
			NPC.SpawnBoss((int)destino.X, (int)destino.Y, tipoJefe, jugador.whoAmI);
			_jefeWhoAmI = -1;
			for (int i = 0; i < Main.npc.Length; i++) {
				if (Main.npc[i].active && Main.npc[i].type == tipoJefe) {
					_jefeWhoAmI = i;
					break;
				}
			}
			if (_jefeWhoAmI < 0 && antes >= 0 && Main.npc[antes].active) {
				// EaterofWorlds/BrainofCthulhu pueden spawnear con el tipo pedido pero, si el
				// bioma no encaja del todo, con un tipo hermano - se acepta el NPC nuevo que haya
				// aparecido en el hueco que estaba libre antes, en vez de dejar _jefeWhoAmI en -1
				// y no poder hacer seguimiento real del combate.
				_jefeWhoAmI = antes;
			}

			_tipoJefe = tipoJefe;
			_fotogramaInicio = (int)Main.GameUpdateCount;
			_danoAlJefe = 0;
			_danoRecibido = 0;
			_activa = true;
			_terminada = false;

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " EntrenadorJefe: practica iniciada contra tipo=" +
				tipoJefe + " (whoAmI=" + _jefeWhoAmI + "), foto de " + (ancho * alto) + " tiles desde (" +
				desde + ", " + arriba + "), derrotadoOriginal=" + _derrotadoOriginalDelJefe + ".");
			return true;
		}

		/// <summary>Cancela la practica AHORA MISMO (el jugador no ha ganado ni perdido) y restaura
		/// todo. Publico para el boton "Cancelar" y para la autoprueba.</summary>
		public static void Cancelar()
		{
			if (!_activa) {
				return;
			}
			Finalizar(victoria: false, cancelado: true);
		}

		/// <summary>Se llama cada fotograma real, desde <see cref="GuiaSystem.UpdateUI"/> - igual
		/// que <c>BrujulaGuia.Actualizar</c>, corre siempre que hay partida, este o no abierta la
		/// pestaña de la Guia.</summary>
		public static void Actualizar()
		{
			if (!_activa) {
				return;
			}

			Player jugador = Main.LocalPlayer;
			bool jefeMuerto = _jefeWhoAmI < 0 || !Main.npc[_jefeWhoAmI].active || Main.npc[_jefeWhoAmI].type != _tipoJefe;
			bool jugadorMuerto = jugador == null || !jugador.active || jugador.dead;

			if (jefeMuerto) {
				Finalizar(victoria: true, cancelado: false);
			}
			else if (jugadorMuerto) {
				Finalizar(victoria: false, cancelado: false);
			}
		}

		private static void Finalizar(bool victoria, bool cancelado)
		{
			Player jugador = Main.LocalPlayer;

			// Si el jefe seguia vivo (cancelado a mano o el jugador murio con el jefe todavia en
			// pie), se retira de verdad - una practica no deja un jefe real suelto por el mundo.
			if (_jefeWhoAmI >= 0 && _jefeWhoAmI < Main.npc.Length && Main.npc[_jefeWhoAmI].active &&
				Main.npc[_jefeWhoAmI].type == _tipoJefe) {
				Main.npc[_jefeWhoAmI].active = false;
				Main.npc[_jefeWhoAmI].life = 0;
			}

			// Restaura la bandera de "derrotado" SIEMPRE, gane o pierda - ver el XMLdoc de la
			// clase para el porque: practicar nunca puede cerrar un tramo real de la Guia.
			_entradaRoster.EscribirDerrotado(_derrotadoOriginalDelJefe);

			// Restaura el terreno de la arena.
			if (_fotoArena != null) {
				for (int x = 0; x < _anchoArena; x++) {
					for (int y = 0; y < _altoArena; y++) {
						_fotoArena[y * _anchoArena + x].Escribir(Main.tile[_origenXArena + x, _origenYArena + y]);
					}
				}
				WorldGen.RangeFrame(_origenXArena, _origenYArena, _origenXArena + _anchoArena, _origenYArena + _altoArena);
				Main.refreshMap = true;
			}

			// Restaura al jugador - posicion, vida, mana y buffs, tal cual estaban antes de
			// empezar. Si el jugador murio de verdad durante la practica, Player.KillMe ya lo
			// revive el motor por su cuenta (misma pantalla de "reaparecer" de siempre); aqui solo
			// se le devuelve al sitio y al estado de antes de entrar en la arena.
			if (jugador != null) {
				jugador.Teleport(_posicionOriginal, 1);
				jugador.statLife = (int)Math.Min(_vidaOriginal, jugador.statLifeMax2);
				jugador.statMana = Math.Min(_manaOriginal, jugador.statManaMax2);
				if (_buffTypeOriginal != null && _buffTimeOriginal != null) {
					for (int i = 0; i < jugador.buffType.Length && i < _buffTypeOriginal.Length; i++) {
						jugador.buffType[i] = _buffTypeOriginal[i];
						jugador.buffTime[i] = _buffTimeOriginal[i];
					}
				}
			}

			float segundos = Math.Max(0.1f, (Main.GameUpdateCount - (uint)_fotogramaInicio) / 60f);
			float dps = _danoAlJefe / segundos;

			_ultimaVictoria = victoria;
			_ultimoInforme = cancelado
				? Idiomas.Texto("Guia.Entrenador.InformeCancelado", (int)segundos)
				: Idiomas.Texto(victoria ? "Guia.Entrenador.InformeVictoria" : "Guia.Entrenador.InformeDerrota",
					(int)dps, _danoRecibido, (int)segundos);

			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " EntrenadorJefe: practica terminada. victoria=" +
				victoria + " cancelado=" + cancelado + " danoAlJefe=" + _danoAlJefe + " danoRecibido=" +
				_danoRecibido + " segundos=" + segundos.ToString("0.0") + " dps=" + dps.ToString("0.0") +
				". Bandera derrotado restaurada a " + _derrotadoOriginalDelJefe + ".");

			_activa = false;
			_terminada = true;
			_jefeWhoAmI = -1;
			_fotoArena = null;
		}

		/// <summary>Quita el informe final de pantalla (el jugador ya lo ha leido / ha cerrado el
		/// panel). Publico para el boton "Cerrar" del informe.</summary>
		public static void LimpiarInforme()
		{
			_terminada = false;
			_ultimoInforme = "";
		}

		/// <summary>Solo para los dos ganchos reales (<see cref="EntrenadorJefeGlobalNPC"/>/
		/// <see cref="EntrenadorJefeModPlayer"/>): suma daño hecho al jefe de la practica activa.</summary>
		public static void RegistrarDanoAlJefe(int whoAmINpc, int dano)
		{
			if (_activa && whoAmINpc == _jefeWhoAmI && dano > 0) {
				_danoAlJefe += dano;
			}
		}

		/// <summary>Solo para <see cref="EntrenadorJefeModPlayer"/>: suma daño recibido por el
		/// jugador local mientras la practica esta activa.</summary>
		public static void RegistrarDanoRecibido(int dano)
		{
			if (_activa && dano > 0) {
				_danoRecibido += dano;
			}
		}
	}
}
