using System;
using System.Collections.Generic;
using Terraria;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Personaje
{
	/// <summary>Un desbloqueo permanente del personaje, enganchado al campo real de
	/// <see cref="Player"/> que lo guarda.</summary>
	public class Desbloqueo
	{
		public readonly Func<Player, bool> Leer;
		public readonly Action<Player, bool> Escribir;

		/// <summary>Nombre del campo real de Player. Sirve para dos cosas: citarlo en el log de
		/// las pruebas (para que la evidencia sea comprobable) y, como es unico y estable, hacer
		/// de CLAVE de localizacion de esta entrada.</summary>
		public readonly string CampoReal;

		/// <summary>Rotulo de la casilla, traducido al idioma activo. Es una propiedad y no un
		/// campo porque la lista se construye una sola vez y se cachea: guardado ya resuelto se
		/// quedaria con el idioma que hubiera en ese momento.</summary>
		public string Nombre => Idiomas.Texto("Personaje.Desbloqueos." + CampoReal + ".Nombre");

		/// <summary>Explicacion que se ve en el tooltip de la casilla, traducida.</summary>
		public string Descripcion => Idiomas.Texto("Personaje.Desbloqueos." + CampoReal + ".Descripcion");

		public Desbloqueo(string campoReal, Func<Player, bool> leer, Action<Player, bool> escribir)
		{
			CampoReal = campoReal;
			Leer = leer;
			Escribir = escribir;
		}
	}

	/// <summary>
	/// Los 13 desbloqueos permanentes que guarda un personaje de Terraria.
	/// <para />
	/// La lista NO se ha copiado de la referencia decompilada vieja (v1.4.4.9): se ha sacado del
	/// <c>tModLoader.dll</c> instalado (v2026.7.3.0), leyendo el orden exacto en el que
	/// <c>Player.LoadPlayer_*</c> los deserializa del <c>.plr</c> - que es la definicion
	/// autoritativa de "que cosas permanentes tiene un personaje". Son estos, en ese orden:
	/// <c>extraAccessory</c>, <c>unlockedBiomeTorches</c>, <c>UsingBiomeTorches</c>,
	/// <c>ateArtisanBread</c>, <c>usedAegisCrystal</c>, <c>usedAegisFruit</c>,
	/// <c>usedArcaneCrystal</c>, <c>usedGalaxyPearl</c>, <c>usedGummyWorm</c>,
	/// <c>usedAmbrosia</c>, <c>downedDD2EventAnyDifficulty</c>, <c>unlockedSuperCart</c> y
	/// <c>enabledSuperCart</c>.
	/// <para />
	/// Dos avisos reales que se ven leyendo el codigo del juego, no suponiendo:
	/// <list type="bullet">
	/// <item><c>UsingBiomeTorches</c> NO es un campo: es una propiedad que guarda en
	/// <c>builderAccStatus[11]</c> y que ademas devuelve false si <c>unlockedBiomeTorches</c> es
	/// false. Por eso activarla sola no hace nada visible: hay que desbloquearlas antes.</item>
	/// <item><c>enabledSuperCart</c> depende igual de <c>unlockedSuperCart</c>
	/// (<c>Player.UsingSuperCart</c> comprueba las dos).</item>
	/// </list>
	/// </summary>
	public static class Desbloqueos
	{
		private static List<Desbloqueo> _lista;

		public static List<Desbloqueo> Lista
		{
			get
			{
				if (_lista == null) {
					_lista = Construir();
				}
				return _lista;
			}
		}

		private static List<Desbloqueo> Construir()
		{
			return new List<Desbloqueo> {
				new Desbloqueo("extraAccessory",
					j => j.extraAccessory, (j, v) => j.extraAccessory = v),

				new Desbloqueo("unlockedBiomeTorches",
					j => j.unlockedBiomeTorches, (j, v) => j.unlockedBiomeTorches = v),

				new Desbloqueo("UsingBiomeTorches",
					j => j.UsingBiomeTorches, (j, v) => j.UsingBiomeTorches = v),

				new Desbloqueo("ateArtisanBread",
					j => j.ateArtisanBread, (j, v) => j.ateArtisanBread = v),

				new Desbloqueo("usedAegisCrystal",
					j => j.usedAegisCrystal, (j, v) => j.usedAegisCrystal = v),

				new Desbloqueo("usedAegisFruit",
					j => j.usedAegisFruit, (j, v) => j.usedAegisFruit = v),

				new Desbloqueo("usedArcaneCrystal",
					j => j.usedArcaneCrystal, (j, v) => j.usedArcaneCrystal = v),

				new Desbloqueo("usedGalaxyPearl",
					j => j.usedGalaxyPearl, (j, v) => j.usedGalaxyPearl = v),

				new Desbloqueo("usedGummyWorm",
					j => j.usedGummyWorm, (j, v) => j.usedGummyWorm = v),

				new Desbloqueo("usedAmbrosia",
					j => j.usedAmbrosia, (j, v) => j.usedAmbrosia = v),

				new Desbloqueo("downedDD2EventAnyDifficulty",
					j => j.downedDD2EventAnyDifficulty, (j, v) => j.downedDD2EventAnyDifficulty = v),

				new Desbloqueo("unlockedSuperCart",
					j => j.unlockedSuperCart, (j, v) => j.unlockedSuperCart = v),

				new Desbloqueo("enabledSuperCart",
					j => j.enabledSuperCart, (j, v) => j.enabledSuperCart = v)
			};
		}

		public static void Descargar()
		{
			_lista = null;
		}
	}
}
