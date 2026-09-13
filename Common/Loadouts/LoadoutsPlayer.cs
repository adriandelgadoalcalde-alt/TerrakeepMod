using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerrakeepMod.Common.Loadouts
{
	/// <summary>
	/// Un conjunto de equipo guardado por el USUARIO, mas alla de los tres nativos del juego
	/// (<c>Player.Loadouts</c>, un array fijo de exactamente 3 - <c>Player.cs</c> decompilado,
	/// linea ~57422: <c>new EquipmentLoadout[3]</c>, tamaño fijo del motor, no algo que un mod
	/// pueda ampliar sin reescribir media docena de sitios del juego que lo dan por hecho). Esto
	/// es la pieza "guardar mas de 3" del encargo: una foto propia del mod, con su nombre, que se
	/// puede aplicar sobre CUALQUIERA de los tres conjuntos nativos cuando haga falta.
	/// </summary>
	public sealed class PresetLoadout
	{
		public string Nombre = "";
		public Item[] Armadura;
		public Item[] Tintes;
		public bool[] Ocultar;

		/// <summary>Fotografia el conjunto ACTIVO ahora mismo (los arrays vivos del jugador,
		/// clonados - nunca la referencia real).</summary>
		public static PresetLoadout DesdeElActivo(Player jugador, string nombre)
		{
			PresetLoadout preset = new PresetLoadout {
				Nombre = nombre,
				Armadura = new Item[jugador.armor.Length],
				Tintes = new Item[jugador.dye.Length],
				Ocultar = new bool[jugador.hideVisibleAccessory.Length]
			};

			for (int i = 0; i < preset.Armadura.Length; i++) {
				preset.Armadura[i] = jugador.armor[i] != null ? jugador.armor[i].Clone() : new Item();
			}
			for (int i = 0; i < preset.Tintes.Length; i++) {
				preset.Tintes[i] = jugador.dye[i] != null ? jugador.dye[i].Clone() : new Item();
			}
			for (int i = 0; i < preset.Ocultar.Length; i++) {
				preset.Ocultar[i] = jugador.hideVisibleAccessory[i];
			}

			return preset;
		}

		/// <summary>
		/// Escribe este preset sobre el conjunto ACTIVO (los arrays vivos del jugador). Se aplica
		/// siempre al activo y nunca a <c>Player.Loadouts[i]</c> directamente a proposito: mientras
		/// un conjunto esta activo su entrada en <c>Loadouts[]</c> esta VACIA de verdad (el dato
		/// real vive en <c>Player.armor</c>/<c>.dye</c>/<c>.hideVisibleAccessory</c> hasta el
		/// proximo <c>TrySwitchingLoadout</c> - mismo hallazgo que ya documenta <see
		/// cref="TerrakeepMod.UI.Personaje.PestanaEquipo"/>), asi que para aplicar un preset al
		/// conjunto N el jugador primero cambia a el con los botones "Conjunto" que ya existen
		/// (quedando activo) y LUEGO aplica el preset aqui - dos pasos reales, ningun atajo que
		/// escriba en un sitio que el juego va a pisar en el siguiente cambio de conjunto.
		/// </summary>
		public void AplicarSobreElActivo(Player jugador)
		{
			int cuentaArmadura = System.Math.Min(Armadura.Length, jugador.armor.Length);
			for (int i = 0; i < cuentaArmadura; i++) {
				jugador.armor[i] = Armadura[i].Clone();
			}
			int cuentaTintes = System.Math.Min(Tintes.Length, jugador.dye.Length);
			for (int i = 0; i < cuentaTintes; i++) {
				jugador.dye[i] = Tintes[i].Clone();
			}
			int cuentaOcultar = System.Math.Min(Ocultar.Length, jugador.hideVisibleAccessory.Length);
			for (int i = 0; i < cuentaOcultar; i++) {
				jugador.hideVisibleAccessory[i] = Ocultar[i];
			}
		}

		public TagCompound Guardar()
		{
			List<TagCompound> armadura = new List<TagCompound>(Armadura.Length);
			foreach (Item objeto in Armadura) {
				armadura.Add(ItemIO.Save(objeto));
			}
			List<TagCompound> tintes = new List<TagCompound>(Tintes.Length);
			foreach (Item objeto in Tintes) {
				tintes.Add(ItemIO.Save(objeto));
			}

			return new TagCompound {
				["Nombre"] = Nombre,
				["Armadura"] = armadura,
				["Tintes"] = tintes,
				["Ocultar"] = new List<bool>(Ocultar)
			};
		}

		public static PresetLoadout Cargar(TagCompound tag)
		{
			PresetLoadout preset = new PresetLoadout { Nombre = tag.GetString("Nombre") };

			IList<TagCompound> armadura = tag.GetList<TagCompound>("Armadura");
			preset.Armadura = new Item[armadura.Count];
			for (int i = 0; i < armadura.Count; i++) {
				preset.Armadura[i] = ItemIO.Load(armadura[i]);
			}

			IList<TagCompound> tintes = tag.GetList<TagCompound>("Tintes");
			preset.Tintes = new Item[tintes.Count];
			for (int i = 0; i < tintes.Count; i++) {
				preset.Tintes[i] = ItemIO.Load(tintes[i]);
			}

			IList<bool> ocultar = tag.GetList<bool>("Ocultar");
			preset.Ocultar = new bool[ocultar.Count];
			for (int i = 0; i < ocultar.Count; i++) {
				preset.Ocultar[i] = ocultar[i];
			}

			return preset;
		}
	}

	/// <summary>
	/// Persistencia real (guardada dentro del propio <c>.plr</c> del personaje, via el mecanismo
	/// oficial <c>ModPlayer.SaveData</c>/<c>LoadData</c> - ni un archivo aparte ni nada fuera del
	/// personaje) de las dos piezas de "gestion de loadouts" de este encargo:
	/// <list type="bullet">
	/// <item>Nombres personalizados de los tres conjuntos NATIVOS (el motor no guarda ninguno -
	/// <c>EquipmentLoadout</c>, decompilado, no tiene ningun campo de nombre).</item>
	/// <item>Los presets EXTRA del usuario (<see cref="PresetLoadout"/>), sin limite fijo.</item>
	/// </list>
	/// </summary>
	public class LoadoutsPlayer : ModPlayer
	{
		/// <summary>Nombre personalizado de cada uno de los tres conjuntos nativos, o null si el
		/// jugador no le ha puesto ninguno (se enseña "Conjunto N" por defecto).</summary>
		public readonly string[] Nombres = new string[3];

		public readonly List<PresetLoadout> Presets = new List<PresetLoadout>();

		public override void SaveData(TagCompound tag)
		{
			List<string> nombres = new List<string>(3);
			for (int i = 0; i < Nombres.Length; i++) {
				nombres.Add(Nombres[i] ?? "");
			}
			tag["Nombres"] = nombres;

			List<TagCompound> presets = new List<TagCompound>(Presets.Count);
			foreach (PresetLoadout preset in Presets) {
				presets.Add(preset.Guardar());
			}
			tag["Presets"] = presets;
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("Nombres")) {
				IList<string> nombres = tag.GetList<string>("Nombres");
				for (int i = 0; i < Nombres.Length && i < nombres.Count; i++) {
					Nombres[i] = string.IsNullOrEmpty(nombres[i]) ? null : nombres[i];
				}
			}

			Presets.Clear();
			if (tag.ContainsKey("Presets")) {
				foreach (TagCompound presetTag in tag.GetList<TagCompound>("Presets")) {
					Presets.Add(PresetLoadout.Cargar(presetTag));
				}
			}
		}
	}
}
