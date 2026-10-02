using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Guia;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Lectura POR REFLEXION del estado de Calamity que la Guia v2 necesita y que no son banderas de
	/// jefe (esas ya las lee <see cref="BanderasGuia"/>): modo Revengeance/Death, esquemas de los
	/// laboratorios, mejoras permanentes del jugador y centros de los laboratorios de Draedon.
	/// </summary>
	/// <remarks>
	/// Mismo motivo que <see cref="BanderasGuia"/>: Calamity es un mod OPCIONAL, sin referencia de
	/// compilacion, asi que la unica via es preguntar al ensamblado ya cargado por nombre. Todos los
	/// nombres salen del decompilado real de Calamity 2.2.4
	/// (<c>Downloads\Keep\tModLoader-Decompiled\CalamityMod-2.2.4\</c>):
	/// <list type="bullet">
	/// <item><c>CalamityMod.World.CalamityWorld</c>: <c>revenge</c>, <c>death</c>,
	/// <c>TalkedToDraedon</c>, <c>HasGeneratedLuminitePlanetoids</c> y los <c>*LabCenter</c>
	/// (<c>Vector2</c> en coordenadas de mundo, pixeles).</item>
	/// <item><c>CalamityMod.CustomRecipes.RecipeUnlockHandler</c>: <c>HasFound*Schematic</c>,
	/// <c>HasUnlockedT1..T5ArsenalRecipes</c>.</item>
	/// <item><c>CalamityMod.World.Abyss.AtLeftSideOfWorld</c>.</item>
	/// <item><c>CalamityMod.CalPlayer.CalamityPlayer</c>: los campos que <c>SaveData</c> guarda en
	/// la lista <c>boost</c> (<c>CalamityPlayer.cs:2677</c> y siguientes). La tabla de abajo es la
	/// correspondencia clave-guardada → campo real, copiada de ese metodo.</item>
	/// </list>
	/// Un nombre que no exista en la version instalada devuelve null: la condicion queda "sin
	/// datos"/no evaluable, nunca inventada.
	/// </remarks>
	public static class ReflexionCalamity
	{
		/// <summary>Clave de la lista <c>boost</c> de <c>CalamityPlayer.SaveData</c> → campo real.</summary>
		public static readonly Dictionary<string, string> CamposMejora = new Dictionary<string, string> {
			{ "extraAccessoryML", "extraAccessoryML" },
			{ "etherealCore", "eCore" },
			{ "miracleFruit", "mFruit" },
			{ "bloodOrange", "sTangerine" },
			{ "elderBerry", "tCloudberry" },
			{ "dragonFruit", "sStrawberry" },
			{ "phantomHeart", "pHeart" },
			{ "cometShard", "cShard" },
			{ "revJam", "revJamDrop" },
			{ "rageOne", "rageBoostOne" },
			{ "rageTwo", "rageBoostTwo" },
			{ "rageThree", "rageBoostThree" },
			{ "adrenalineOne", "adrenalineBoostOne" },
			{ "adrenalineTwo", "adrenalineBoostTwo" },
			{ "adrenalineThree", "adrenalineBoostThree" },
			{ "HasTalkedAtCodebreaker", "HasTalkedAtCodebreaker" },
			{ "HasCraftedDraedonsForge", "HasCraftedDraedonsForge" },
		};

		/// <summary>Clave de estado de mundo (vocabulario de <c>CalamityEstadoGuardado.EstadosMundoConocidos</c>)
		/// → (tipo, miembro estatico).</summary>
		private static readonly Dictionary<string, (string Tipo, string Miembro)> MiembrosMundo =
			new Dictionary<string, (string, string)> {
				{ "revenge", ("CalamityMod.World.CalamityWorld", "revenge") },
				{ "death", ("CalamityMod.World.CalamityWorld", "death") },
				{ "TalkedToDraedon", ("CalamityMod.World.CalamityWorld", "TalkedToDraedon") },
				{ "HasGeneratedLuminitePlanetoids", ("CalamityMod.World.CalamityWorld", "HasGeneratedLuminitePlanetoids") },
				{ "abyssSide", ("CalamityMod.World.Abyss", "AtLeftSideOfWorld") },
				{ "HasFoundSunkenSeaSchematic", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasFoundSunkenSeaSchematic") },
				{ "HasFoundPlanetoidSchematic", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasFoundPlanetoidSchematic") },
				{ "HasFoundJungleSchematic", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasFoundJungleSchematic") },
				{ "HasFoundHellSchematic", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasFoundHellSchematic") },
				{ "HasFoundIceSchematic", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasFoundIceSchematic") },
				{ "HasUnlockedT1ArsenalRecipes", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasUnlockedT1ArsenalRecipes") },
				{ "HasUnlockedT2ArsenalRecipes", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasUnlockedT2ArsenalRecipes") },
				{ "HasUnlockedT3ArsenalRecipes", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasUnlockedT3ArsenalRecipes") },
				{ "HasUnlockedT4ArsenalRecipes", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasUnlockedT4ArsenalRecipes") },
				{ "HasUnlockedT5ArsenalRecipes", ("CalamityMod.CustomRecipes.RecipeUnlockHandler", "HasUnlockedT5ArsenalRecipes") },
			};

		private static Assembly _ensamblado;
		private static bool _ensambladoBuscado;
		private static readonly Dictionary<string, MemberInfo> _cacheMiembros = new Dictionary<string, MemberInfo>();
		private static readonly Dictionary<string, FieldInfo> _cacheCamposJugador = new Dictionary<string, FieldInfo>();
		private static Type _tipoCalamityPlayer;

		public static bool Cargado => CatalogoGuia.HayCalamity && Ensamblado != null;

		private static Assembly Ensamblado {
			get {
				if (!_ensambladoBuscado) {
					_ensambladoBuscado = true;
					if (CatalogoGuia.HayCalamity) {
						Mod calamity = ModLoader.GetMod(CatalogoGuia.NombreModCalamity);
						_ensamblado = calamity != null ? calamity.GetType().Assembly : null;
					}
				}
				return _ensamblado;
			}
		}

		private static MemberInfo Miembro(string tipo, string nombre)
		{
			string clave = tipo + "::" + nombre;
			MemberInfo m;
			if (_cacheMiembros.TryGetValue(clave, out m)) {
				return m;
			}
			m = null;
			Type t = Ensamblado != null ? Ensamblado.GetType(tipo) : null;
			if (t != null) {
				const BindingFlags banderas = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
				m = (MemberInfo)t.GetField(nombre, banderas) ?? t.GetProperty(nombre, banderas);
			}
			_cacheMiembros[clave] = m;
			return m;
		}

		private static object Leer(MemberInfo m)
		{
			try {
				FieldInfo f = m as FieldInfo;
				if (f != null) {
					return f.GetValue(null);
				}
				PropertyInfo p = m as PropertyInfo;
				return p != null ? p.GetValue(null) : null;
			}
			catch (Exception) {
				return null;
			}
		}

		public static bool EstadoMundoConocido(string clave)
		{
			(string, string) d;
			return Cargado && clave != null && MiembrosMundo.TryGetValue(clave, out d) && Miembro(d.Item1, d.Item2) != null;
		}

		/// <summary>Valor real del estado de mundo de Calamity, o null si no se puede leer.</summary>
		public static bool? EstadoMundo(string clave)
		{
			(string Tipo, string Miembro) d;
			if (!Cargado || clave == null || !MiembrosMundo.TryGetValue(clave, out d)) {
				return null;
			}
			MemberInfo m = Miembro(d.Tipo, d.Miembro);
			object v = m != null ? Leer(m) : null;
			return v is bool b ? b : (bool?)null;
		}

		/// <summary>
		/// Zona (pantalla LOGICA) del indicador de dificultad que Calamity pinta junto a la vida con el
		/// inventario abierto (<c>CalamityMod.UI.ModeIndicator.ModeIndicatorUI</c>, capa "Mode Indicator
		/// UI" con escala de interfaz; solo se dibuja con <c>Main.playerInventory</c>). Se lee su
		/// <c>MainClickArea</c> real; si no se pudiera, la formula del decompilado de la 2.2.4
		/// (<c>DrawCenter</c> = (screenWidth - 400 - ancho de la fila/2, 82) + 37, marco de 74x74) con
		/// una sola dificultad por fila. Null sin Calamity o con el inventario cerrado.
		/// </summary>
		public static Rectangle? AreaIndicadorModo()
		{
			if (!Cargado || !Main.playerInventory) {
				return null;
			}
			MemberInfo m = Miembro("CalamityMod.UI.ModeIndicator.ModeIndicatorUI", "MainClickArea");
			object v = m != null ? Leer(m) : null;
			if (v is Rectangle r && r.Width > 0 && r.Height > 0) {
				return r;
			}
			return new Rectangle(Main.screenWidth - 400, 82, 74, 74);
		}

		/// <summary>Centro de un laboratorio de Draedon en CASILLAS (CalamityWorld.*LabCenter esta
		/// en pixeles de mundo). Null si no existe o no se genero (0,0).</summary>
		public static (int X, int Y)? CentroLaboratorio(string clave)
		{
			if (!Cargado || string.IsNullOrEmpty(clave) || !clave.EndsWith("LabCenter", StringComparison.Ordinal)) {
				return null;
			}
			MemberInfo m = Miembro("CalamityMod.World.CalamityWorld", clave);
			object v = m != null ? Leer(m) : null;
			if (v is Vector2 vec && (vec.X > 0f || vec.Y > 0f)) {
				return ((int)(vec.X / 16f), (int)(vec.Y / 16f));
			}
			return null;
		}

		private static FieldInfo CampoJugador(string clave)
		{
			string campo;
			if (!Cargado || clave == null || !CamposMejora.TryGetValue(clave, out campo)) {
				return null;
			}
			FieldInfo f;
			if (_cacheCamposJugador.TryGetValue(campo, out f)) {
				return f;
			}
			if (_tipoCalamityPlayer == null) {
				_tipoCalamityPlayer = Ensamblado.GetType("CalamityMod.CalPlayer.CalamityPlayer");
			}
			f = _tipoCalamityPlayer != null
				? _tipoCalamityPlayer.GetField(campo, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				: null;
			_cacheCamposJugador[campo] = f;
			return f;
		}

		public static bool MejoraConocida(string clave) => CampoJugador(clave) != null;

		/// <summary>Valor real de la mejora en el <c>CalamityPlayer</c> del jugador indicado.</summary>
		public static bool? Mejora(Player jugador, string clave)
		{
			FieldInfo f = CampoJugador(clave);
			if (f == null || jugador == null || !jugador.active) {
				return null;
			}
			ModPlayer instancia = InstanciaCalamityPlayer(jugador);
			if (instancia == null) {
				return null;
			}
			try {
				object v = f.GetValue(instancia);
				return v is bool b ? b : (bool?)null;
			}
			catch (Exception) {
				return null;
			}
		}

		private static ModPlayer InstanciaCalamityPlayer(Player jugador)
		{
			foreach (ModPlayer mp in jugador.ModPlayers) {
				if (mp != null && mp.Mod != null && mp.Mod.Name == CatalogoGuia.NombreModCalamity && mp.Name == "CalamityPlayer") {
					return mp;
				}
			}
			return null;
		}

		public static void Descargar()
		{
			_ensamblado = null;
			_ensambladoBuscado = false;
			_tipoCalamityPlayer = null;
			_cacheMiembros.Clear();
			_cacheCamposJugador.Clear();
		}
	}
}
