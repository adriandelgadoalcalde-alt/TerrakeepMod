using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Guia;
using TerrakeepMod.UI.Panel;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Punto de entrada del area "Guia": carga el arbol de progresion, registra su atajo y deja
	/// en el log lo que ha cargado.
	/// </summary>
	/// <remarks>
	/// Sigue el mismo reparto que las seis areas anteriores: <b>la mecanica de apertura no vive
	/// aqui</b> (la tiene toda <see cref="PanelTerrakeepSystem"/>, que es el unico sitio del mod
	/// que llama a <c>IngameFancyUI</c>); esta clase solo registra el <c>ModKeybind</c> y lo
	/// expone para que el panel unico lo LEA. Asi el <c>SembradorDeAtajos</c> de WS7 -que recorre
	/// las claves del perfil que empiezan por <c>TerrakeepMod/</c>- lo recoge sin tocarlo.
	/// </remarks>
	public class GuiaSystem : ModSystem
	{
		private static ModKeybind _atajo;
		private static bool _arbolConstruido;

		/// <summary>El atajo de la Guia. Lo registra este sistema y lo lee
		/// <see cref="PanelTerrakeepSystem"/>.</summary>
		public static ModKeybind Atajo => _atajo;

		/// <summary>El contenido de la Guia montado ahora mismo, o null. Lo usa la autoprueba.</summary>
		public static ContenidoGuia PanelActual {
			get {
				PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
				return panel != null ? panel.Guia : null;
			}
		}

		/// <summary>true si el panel esta abierto Y en la pestaña de la Guia.</summary>
		public static bool PanelAbierto => PanelTerrakeepSystem.AreaAbiertaEs(AreaTerrakeep.Guia);

		public override void Load()
		{
			RegistroGuia.Mod = Mod;

			// Los .json hay que leerlos mientras el .tmod sigue abierto (hallazgo de WS4).
			CatalogoGuia.LeerArchivo(Mod);

			if (!Main.dedServ) {
				// G no esta asignada a nada en los controles de fabrica de Terraria: se comprobo
				// leyendo el preset real "Redigit's Pick" del tModLoader.dll instalado
				// (PlayerInput.cs, case PresetProfiles.Redigit), donde las teclas ocupadas son
				// W A S D, Espacio, Escape, E, LeftShift, LeftControl, R, H, J, B, Tab, M, +, -,
				// AvPag/RePag, 0-9, OemPlus/OemMinus, C y F1-F4. Las otras seis areas de este mod
				// ya usan K, O, L, I, P y J. Reasignable en Ajustes > Controles.
				_atajo = KeybindLoader.RegisterKeybind(Mod, "AbrirGuia", Keys.G);
			}
		}

		public override void PostSetupContent()
		{
			// A estas alturas ya han cargado todos los mods, asi que ModLoader.HasMod("CalamityMod")
			// dice la verdad y ContentSamples esta completo (hace falta para resolver los nombres
			// de objeto y las stats reales de los jefes).
			CatalogoGuia.Construir();
			_arbolConstruido = true;

			RegistroGuia.Linea(Terrakeep.LogTag + " Guia: arbol de progresion cargado. " +
				CatalogoGuia.Resumen + ". Calamity cargado=" + CatalogoGuia.HayCalamity + ".");

			foreach (string aviso in CatalogoGuia.Avisos) {
				RegistroGuia.Aviso(Terrakeep.LogTag + " Guia: AVISO de datos - " + aviso);
			}
		}

		public override void OnLocalizationsLoaded()
		{
			// El arbol no guarda ningun texto ya resuelto (todos los nombres son propiedades que
			// preguntan por clave), asi que no hay nada que reconstruir al cambiar de idioma. Se
			// deja el hook con esta nota a proposito: es justo el fallo que mordio en la Libreria
			// y en Investigacion, y conviene que quede dicho que aqui NO puede pasar.
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}
			AutopruebaGuia.Avanzar();
			AutopruebaGrupo.Avanzar();
			BrujulaGuia.Actualizar();
			// Idea 1 del catalogo de funciones ("entrenador de jefe"): corre siempre que hay
			// partida, este o no la pestaña de la Guia abierta - mismo motivo real que BrujulaGuia,
			// una practica en marcha no puede depender de que el jugador no cambie de pestaña.
			EntrenadorJefe.Actualizar();
		}

		public override void OnWorldUnload()
		{
			// Los marcadores de la brujula son coordenadas de ESTE mundo: en otro no significan nada.
			BrujulaGuia.AlSalirDelMundo();
			// Idea 1: si el jugador cierra la partida con una practica en marcha, se cancela y se
			// restaura todo ANTES de que el mundo deje de existir - nunca dejar una practica a
			// medias colgando de un mundo que ya no esta cargado.
			if (EntrenadorJefe.Activa) {
				EntrenadorJefe.Cancelar();
			}
		}

		public override void Unload()
		{
			_atajo = null;
			_arbolConstruido = false;
			RegistroGuia.Mod = null;
			CatalogoGuia.Descargar();
			BrujulaGuia.Descargar();
		}

		/// <summary>Abre el panel en la pestaña de la Guia, o lo cierra si ya estaba ahi.</summary>
		public static void AlternarPanel(string origen)
		{
			PanelTerrakeepSystem.AlternarArea(AreaTerrakeep.Guia, origen);
		}

		public static void AbrirPanel(string origen)
		{
			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				return;
			}

			if (!_arbolConstruido) {
				CatalogoGuia.Construir();
				_arbolConstruido = true;
			}

			PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Guia, origen);
		}
	}
}
