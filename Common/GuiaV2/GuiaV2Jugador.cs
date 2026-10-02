using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terrakeep.Core.Guia.V2;

namespace TerrakeepMod.Common.GuiaV2
{
	/// <summary>
	/// Progreso MANUAL de la Guia v2 (casillas marcadas, paradas hechas/aplazadas y clase elegida),
	/// guardado DENTRO del personaje: <c>ModPlayer.SaveData</c> lo escribe en su .tplr con el mismo
	/// JSON que usa el escritorio (<see cref="GuiaV2ProgresoJson"/>), asi que viaja con el personaje
	/// y nunca se queda solo en memoria (encargo del usuario, 02-oct-2026).
	/// </summary>
	/// <remarks>
	/// Una entrada por guia ("calamity", "vanilla"): el mismo personaje puede jugar con y sin
	/// Calamity y cada guia conserva lo suyo. Si un JSON guardado no se puede leer, NO se pisa: se
	/// conserva tal cual (<see cref="_ilegibles"/>) y se vuelve a guardar igual.
	/// </remarks>
	public class GuiaV2Jugador : ModPlayer
	{
		private const string Prefijo = "guiaV2.";

		private readonly Dictionary<string, GuiaV2ProgresoManual> _progresos = new Dictionary<string, GuiaV2ProgresoManual>();
		private readonly Dictionary<string, string> _ilegibles = new Dictionary<string, string>();

		public static GuiaV2Jugador Local {
			get {
				Player p = Main.LocalPlayer;
				if (p == null || !p.active || Main.gameMenu) {
					return null;
				}
				GuiaV2Jugador j;
				return p.TryGetModPlayer(out j) ? j : null;
			}
		}

		/// <summary>Progreso de la guia indicada (lo crea vacio si no existe).</summary>
		public GuiaV2ProgresoManual Progreso(string guia)
		{
			GuiaV2ProgresoManual p;
			if (!_progresos.TryGetValue(guia ?? "", out p)) {
				p = new GuiaV2ProgresoManual { Guia = guia ?? "" };
				_progresos[guia ?? ""] = p;
			}
			return p;
		}

		/// <summary>Cuantas veces ha cambiado el progreso manual (lo lee el sistema para reevaluar
		/// en el acto en vez de esperar al siguiente medio segundo).</summary>
		public static int Cambios;

		public static void AvisarCambio()
		{
			Cambios++;
		}

		public override void SaveData(TagCompound tag)
		{
			foreach (KeyValuePair<string, GuiaV2ProgresoManual> par in _progresos) {
				if (_ilegibles.ContainsKey(par.Key)) {
					continue;
				}
				par.Value.Actualizado = System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
				tag[Prefijo + par.Key] = GuiaV2ProgresoJson.Serializar(par.Value);
			}
			foreach (KeyValuePair<string, string> par in _ilegibles) {
				tag[Prefijo + par.Key] = par.Value;
			}
		}

		public override void LoadData(TagCompound tag)
		{
			_progresos.Clear();
			_ilegibles.Clear();
			foreach (KeyValuePair<string, object> par in tag) {
				if (!par.Key.StartsWith(Prefijo, System.StringComparison.Ordinal)) {
					continue;
				}
				string guia = par.Key.Substring(Prefijo.Length);
				string json = par.Value as string;
				bool valido;
				GuiaV2ProgresoManual p = GuiaV2ProgresoJson.Deserializar(json, guia, out valido);
				if (valido) {
					_progresos[guia] = p;
				}
				else if (json != null) {
					_ilegibles[guia] = json;
				}
			}
		}
	}
}
