using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;

namespace TerrakeepMod.Common.Ajustes
{
	/// <summary>
	/// Da de alta en el perfil de controles del jugador la tecla por defecto de cada atajo del
	/// mod, la primera vez que ese atajo aparece.
	///
	/// <para />
	/// <b>Por que hace falta esto</b> (hallazgo real de WS7, verificado en el juego y en el
	/// codigo del <c>tModLoader.dll</c> instalado, v2026.7.3.0): <b>la tecla que se le pasa a
	/// <c>KeybindLoader.RegisterKeybind(mod, nombre, Keys.X)</c> NO se aplica sola.</b> Un
	/// <c>ModKeybind</c> recien registrado nace sin ninguna tecla asignada. La cadena real es:
	/// <list type="number">
	/// <item><c>KeyConfiguration.SetupKeys()</c> mete cada atajo de mod en el diccionario del
	/// perfil <b>con la lista de teclas VACIA</b>;</item>
	/// <item><c>PlayerInput.Reset(...)</c>, que es quien reparte las teclas por defecto, solo
	/// conoce los atajos de vanilla - no toca los de mods;</item>
	/// <item><c>PlayerInputProfile.Load(...)</c> -&gt; <c>ReadPreferences</c> solo copia lo que ya
	/// estuviera guardado en <c>input profiles.json</c>, y un atajo nuevo no esta;</item>
	/// <item>el UNICO sitio de todo tModLoader que lee <c>ModKeybind.DefaultBinding</c> es la
	/// pantalla de Controles (<c>UIManageControls</c>), al pulsar "Restablecer".</item>
	/// </list>
	/// Medido en el juego real: con el mod cargado y jugando, los cuatro atajos del mod salian
	/// <c>[]</c> (AbrirPanel, AbrirAjustes, Deshacer, Rehacer) mientras un atajo vanilla como
	/// <c>QuickHeal</c> salia <c>[H]</c>. Es decir, <b>ningun atajo de Terrakeep funcionaba de
	/// fabrica</b>, ni siquiera la tecla K de WS0 (que nunca se llego a probar: aquel panel se
	/// abrio por la autoprueba, no por el teclado).
	///
	/// <para />
	/// <b>Como se arregla, sin pisar al usuario.</b> Se llama a
	/// <c>PlayerInputProfile.CopyIndividualModKeybindSettingsFrom</c>, que es publico y es
	/// exactamente lo que ejecuta el boton "Restablecer" de la pantalla de Controles (saca el
	/// default del propio <c>ModKeybind</c>; el perfil que se le pasa no lo llega a usar). Se
	/// hace <b>una sola vez por atajo</b>, y queda anotado en
	/// <see cref="AjustesConfig.AtajosYaSembrados"/>: si despues el usuario le quita la tecla a
	/// mano, no se le vuelve a poner nunca.
	///
	/// <para />
	/// Cubre TODOS los atajos del mod, no solo los de WS7: se recorren las claves del perfil que
	/// empiezan por <c>TerrakeepMod/</c>, asi que los atajos de los demas workstreams quedan
	/// arreglados tambien sin tocar ni uno de sus archivos.
	/// </summary>
	public static class SembradorDeAtajos
	{
		private const string PrefijoDelMod = "TerrakeepMod/";

		private static bool _hecho;

		/// <summary>
		/// Se intenta en cada fotograma hasta que sale, porque los atajos de un mod no aparecen en
		/// el perfil hasta que <c>PlayerInput</c> procesa el <c>reinitialize</c> que deja pendiente
		/// la carga de mods, y eso puede tardar varios segundos (mismo hueco que documento WS0).
		/// </summary>
		public static void SembrarSiHaceFalta()
		{
			if (_hecho || Main.dedServ) {
				return;
			}

			PlayerInputProfile perfil = PlayerInput.CurrentProfile;
			if (perfil == null || !perfil.InputModes.ContainsKey(InputMode.Keyboard)) {
				return;
			}

			KeyConfiguration teclado = perfil.InputModes[InputMode.Keyboard];
			List<string> nuestros = new List<string>();
			foreach (string clave in teclado.KeyStatus.Keys) {
				if (clave.StartsWith(PrefijoDelMod)) {
					nuestros.Add(clave);
				}
			}

			if (nuestros.Count == 0) {
				return;   // Todavia no; se reintenta al fotograma siguiente.
			}

			_hecho = true;

			AjustesConfig config = AjustesConfig.Instance;
			List<string> yaSembrados = config != null && config.AtajosYaSembrados != null
				? config.AtajosYaSembrados
				: new List<string>();

			List<string> sembradosAhora = new List<string>();
			foreach (string clave in nuestros) {
				if (teclado.KeyStatus[clave].Count > 0 || yaSembrados.Contains(clave)) {
					continue;
				}

				// El parametro del perfil es el que exige la firma; el metodo no lo usa para nada,
				// saca la tecla del DefaultBinding del propio ModKeybind. Se pasa un perfil
				// original cualquiera, como hace la pantalla de Controles del juego.
				foreach (KeyValuePair<string, PlayerInputProfile> original in PlayerInput.OriginalProfiles) {
					perfil.CopyIndividualModKeybindSettingsFrom(original.Value, InputMode.Keyboard, clave);
					break;
				}
				sembradosAhora.Add(clave);
			}

			if (sembradosAhora.Count == 0) {
				Registrar("Atajos: nada que sembrar (ya tenian tecla o ya se habian sembrado antes).");
				return;
			}

			// Que quede en input profiles.json, para que el usuario lo vea y lo pueda cambiar en
			// Ajustes > Controles como cualquier otro atajo.
			PlayerInput.Save();

			if (config != null) {
				if (config.AtajosYaSembrados == null) {
					config.AtajosYaSembrados = new List<string>();
				}
				config.AtajosYaSembrados.AddRange(sembradosAhora);
				config.SaveChanges();
			}

			List<string> resumen = new List<string>();
			foreach (string clave in nuestros) {
				resumen.Add(clave + "=[" + string.Join("+", teclado.KeyStatus[clave]) + "]");
			}
			Registrar("Atajos sembrados por primera vez (" + sembradosAhora.Count + "): " +
				string.Join(", ", resumen) + ". Guardado en input profiles.json y anotado en el ModConfig.");
		}

		private static void Registrar(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
			}
		}
	}
}
