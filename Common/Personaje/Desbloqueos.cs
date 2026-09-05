using System;
using System.Collections.Generic;
using Terraria;

namespace TerrakeepMod.Common.Personaje
{
	/// <summary>Un desbloqueo permanente del personaje, enganchado al campo real de
	/// <see cref="Player"/> que lo guarda.</summary>
	public class Desbloqueo
	{
		public readonly string Nombre;
		public readonly string Descripcion;
		public readonly Func<Player, bool> Leer;
		public readonly Action<Player, bool> Escribir;

		/// <summary>Nombre del campo real de Player, para poder citarlo en el log de las
		/// pruebas y que la evidencia sea comprobable.</summary>
		public readonly string CampoReal;

		public Desbloqueo(string nombre, string campoReal, string descripcion,
			Func<Player, bool> leer, Action<Player, bool> escribir)
		{
			Nombre = nombre;
			CampoReal = campoReal;
			Descripcion = descripcion;
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
				new Desbloqueo("6º ranura de accesorio", "extraAccessory",
					"Corazon de Demonio. Solo surte efecto en modo Experto o superior.",
					j => j.extraAccessory, (j, v) => j.extraAccessory = v),

				new Desbloqueo("Antorchas de bioma desbloqueadas", "unlockedBiomeTorches",
					"Torch God's Favor. Sin esto, la casilla de abajo no hace nada.",
					j => j.unlockedBiomeTorches, (j, v) => j.unlockedBiomeTorches = v),

				new Desbloqueo("Antorchas de bioma activadas", "UsingBiomeTorches",
					"Propiedad: guarda en builderAccStatus[11] y exige el desbloqueo de arriba.",
					j => j.UsingBiomeTorches, (j, v) => j.UsingBiomeTorches = v),

				new Desbloqueo("Pan de artesano comido", "ateArtisanBread",
					"Artisan Loaf: permite crear cerca de cualquier estacion.",
					j => j.ateArtisanBread, (j, v) => j.ateArtisanBread = v),

				new Desbloqueo("Cristal de Egida usado", "usedAegisCrystal",
					"Aegis Crystal: +defensa permanente.",
					j => j.usedAegisCrystal, (j, v) => j.usedAegisCrystal = v),

				new Desbloqueo("Fruta de Egida usada", "usedAegisFruit",
					"Aegis Fruit: +vida permanente.",
					j => j.usedAegisFruit, (j, v) => j.usedAegisFruit = v),

				new Desbloqueo("Cristal arcano usado", "usedArcaneCrystal",
					"Arcane Crystal: +regeneracion de mana permanente.",
					j => j.usedArcaneCrystal, (j, v) => j.usedArcaneCrystal = v),

				new Desbloqueo("Perla galactica usada", "usedGalaxyPearl",
					"Galaxy Pearl: +suerte permanente.",
					j => j.usedGalaxyPearl, (j, v) => j.usedGalaxyPearl = v),

				new Desbloqueo("Gusano de goma usado", "usedGummyWorm",
					"Gummy Worm: +pesca permanente.",
					j => j.usedGummyWorm, (j, v) => j.usedGummyWorm = v),

				new Desbloqueo("Ambrosia usada", "usedAmbrosia",
					"Ambrosia: +velocidad de mineria y recoleccion permanente.",
					j => j.usedAmbrosia, (j, v) => j.usedAmbrosia = v),

				new Desbloqueo("Evento DD2 superado", "downedDD2EventAnyDifficulty",
					"Ejercito Antiguo derrotado alguna vez, en cualquier dificultad.",
					j => j.downedDD2EventAnyDifficulty, (j, v) => j.downedDD2EventAnyDifficulty = v),

				new Desbloqueo("Supercarrito desbloqueado", "unlockedSuperCart",
					"Mechanical Cart: mejora permanente de las vagonetas.",
					j => j.unlockedSuperCart, (j, v) => j.unlockedSuperCart = v),

				new Desbloqueo("Supercarrito activado", "enabledSuperCart",
					"Exige el desbloqueo de arriba (Player.UsingSuperCart comprueba los dos).",
					j => j.enabledSuperCart, (j, v) => j.enabledSuperCart = v)
			};
		}

		public static void Descargar()
		{
			_lista = null;
		}
	}
}
