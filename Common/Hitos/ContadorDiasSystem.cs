using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerrakeepMod.Common.Hitos
{
	/// <summary>
	/// <b>Idea 3 del catálogo de funciones ("Diario de partida automático"):</b> cuántos amaneceres
	/// (<c>Main.dayTime</c> false→true) ha visto ESTE MUNDO desde que se generó, persistido en el
	/// propio <c>.wld</c> con el mecanismo real de <c>ModSystem.SaveWorldData</c>/<c>LoadWorldData</c>
	/// - el mismo con el que cualquier mod guarda datos propios de mundo.
	/// </summary>
	/// <remarks>
	/// <b>Por qué hace falta un contador propio.</b> Investigado ANTES de escribir código
	/// (disciplina de dos fases del proyecto): vanilla no expone ningún "día del mundo" ya hecho.
	/// <c>Main.time</c>/<c>Main.dayTime</c> solo dan la posición DENTRO del día/noche actual, y la
	/// fase de luna (<c>Main.moonPhase</c>) avanza un paso por día pero es CÍCLICA (0-7, vuelve a
	/// empezar) - no sirve como contador monótono de días reales transcurridos. <c>WorldFile.cs</c>
	/// (decompilado, <c>Downloads\tModLoader-Decompiled</c>) tampoco guarda ninguna "edad" del
	/// mundo en ningún campo real. Por eso hace falta contarlo desde cero, con la misma técnica que
	/// ya usa cualquier mod para datos propios persistidos de mundo.
	/// </remarks>
	public class ContadorDiasSystem : ModSystem
	{
		private int _dias;
		private bool _diaAnterior;
		private bool _hayDiaAnterior;

		/// <summary>Amaneceres vistos desde que se generó este mundo. 0 en un mundo recién creado
		/// que todavía no ha visto salir el sol ni una vez (o en un mundo que ya existía ANTES de
		/// que este contador se añadiera al mod - honesto: no se inventa una cifra retroactiva).</summary>
		public static int DiasTranscurridos =>
			ModContent.GetInstance<ContadorDiasSystem>()?._dias ?? 0;

		public override void PostUpdateTime()
		{
			bool ahora = Main.dayTime;

			// La primera lectura de la sesion (_hayDiaAnterior todavia false) solo fija el punto de
			// partida - nunca cuenta un amanecer "de mentira" solo por haber cargado el mundo en
			// pleno dia.
			if (_hayDiaAnterior && !_diaAnterior && ahora) {
				_dias++;
			}
			_diaAnterior = ahora;
			_hayDiaAnterior = true;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			tag["diasTranscurridos"] = _dias;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			_dias = tag.GetInt("diasTranscurridos");
		}

		public override void OnWorldUnload()
		{
			_hayDiaAnterior = false;
		}
	}
}
