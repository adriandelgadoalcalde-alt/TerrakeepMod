using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using TerrakeepMod.Common.Guia;
using TerrakeepMod.Common.Panel;

namespace TerrakeepMod.Common.Hitos
{
	/// <summary>
	/// Capturas automáticas de hito: en cuanto un tramo de la Guía (obligatorio u opcional) se
	/// cierra DE VERDAD durante la partida, dispara una captura real
	/// (<see cref="AlbumHitos.Registrar"/>, que a su vez usa la misma técnica de
	/// <c>CapturaDePantalla</c>) sin que el jugador tenga que acordarse de pulsar nada.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por qué esto vive aparte de <see cref="GuiaSystem"/> y no dentro.</b> La Guía es "leer y
	/// enseñar el estado de la partida"; esto es "reaccionar a un cambio de estado". Son
	/// responsabilidades distintas (la Guía seguiría funcionando exactamente igual sin esto, y esto
	/// no necesita saber nada de pestañas ni de brújulas), así que se separan, igual que ya está
	/// separado <c>BrujulaGuia</c> de <c>EstadoGuia</c>.
	/// </para>
	/// <para>
	/// <b>Cómo se distingue "se ha cerrado AHORA" de "ya estaba cerrado al cargar la partida".</b> La
	/// Guía no dispara eventos: todo se recalcula en vivo cada vez que se pregunta (ver la cabecera
	/// de <see cref="EstadoGuia"/>). Por eso este sistema toma su propia fotografía del estado de
	/// cada tramo (<see cref="EstadoGuia.TramoSuperado"/>) la PRIMERA vez que hay una partida activa
	/// tras entrar al mundo, y solo cuenta como hito un tramo que pasa de no-superado a superado
	/// DESPUÉS de esa fotografía - nunca en la fotografía misma. Sin esto, cargar una partida ya
	/// avanzada (la mayoría de banderas <c>downed*</c> ya a <c>true</c>) llenaría el álbum de hitos
	/// falsos en el primer fotograma de cada sesión.
	/// </para>
	/// <para>
	/// La fotografía se vuelve a tomar cada vez que hay una partida nueva (otro personaje, otro
	/// mundo, o simplemente volver a entrar): un tramo ya cerrado en una partida real nunca vuelve a
	/// disparar una captura en una sesión futura, porque sigue estando "superado" en la fotografía
	/// de esa sesión también.
	/// </para>
	/// <para>
	/// <b>Verificación real, sin arnés nuevo</b>: <c>AutopruebaGuia</c> (<c>TERRAKEEP_AUTOTEST_GUIA</c>)
	/// ya fuerza, una a una, las banderas reales de cada tramo (p.ej. <c>NPC.downedBoss1 = true</c>
	/// en <c>MatarElOjoEnFalso</c>) para demostrar que el cerebro de la Guía reacciona bien. Con este
	/// sistema activo (siempre lo está, no depende de ninguna variable de entorno), esa misma
	/// ejecución basta para demostrar TAMBIÉN que las capturas de hito se disparan solas: no hace
	/// falta escribir un arnés aparte, ver <c>terrakeep-hitos-evidencia.log</c> y la carpeta
	/// <c>terrakeep-hitos</c> de esa misma carpeta de guardado tras una pasada.
	/// </para>
	/// </remarks>
	public class HitosSystem : ModSystem
	{
		private static ModKeybind _atajo;

		/// <summary>El atajo del álbum. Lo registra este sistema y lo lee
		/// <see cref="PanelTerrakeepSystem"/>, igual que hace cada área con el suyo.</summary>
		public static ModKeybind Atajo => _atajo;

		/// <summary>true si ya se tomó la fotografía de esta partida.</summary>
		private static bool _lineaBase;

		/// <summary>Estado de "tramo superado" en la fotografía (o el más reciente ya visto), por
		/// clave de tramo.</summary>
		private static readonly Dictionary<string, bool> _superadoAntes = new Dictionary<string, bool>();

		public override void Load()
		{
			if (!Main.dedServ) {
				// U: comprobado libre con un grep real de todos los RegisterKeybind del mod antes de
				// elegirla - las demás áreas del panel único ya usan K/O/L/I/P/J/G (ver GuiaSystem) y
				// Deshacer/Rehacer usan Z/Y (ver HistorialSystem).
				_atajo = KeybindLoader.RegisterKeybind(Mod, "AbrirAlbum", Keys.U);
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ) {
				return;
			}

			// Verificacion EN EL JUEGO REAL de la pestaña Album (que abre por clic real y que no se
			// solapa nada). Ir primero y sin condiciones, mismo criterio que el resto de autopruebas
			// del mod: una excepcion aqui no debe abortar la deteccion de hitos de abajo.
			AutopruebaHitos.Avanzar();

			if (!EstadoJugadorGuia.HayPartida) {
				// Fuera de partida (menú, personaje sin cargar todavía...): se olvida la fotografía.
				// La próxima vez que haya partida activa - aunque sea la misma, tras volver al menú
				// y entrar otra vez - se vuelve a tomar desde cero, que es exactamente lo correcto:
				// "cero" aquí significa "el estado real de ESA partida", no "nada superado todavía".
				_lineaBase = false;
				return;
			}

			List<TramoGuia> tramos = CatalogoGuia.Tramos;

			if (!_lineaBase) {
				_lineaBase = true;
				_superadoAntes.Clear();
				for (int i = 0; i < tramos.Count; i++) {
					TramoGuia t = tramos[i];
					_superadoAntes[t.Clave] = EstadoGuia.TramoSuperado(t);
				}
				return;
			}

			for (int i = 0; i < tramos.Count; i++) {
				TramoGuia t = tramos[i];
				bool superadoAhora = EstadoGuia.TramoSuperado(t);

				bool superadoAntes;
				_superadoAntes.TryGetValue(t.Clave, out superadoAntes);

				if (superadoAhora && !superadoAntes) {
					string resultado = AlbumHitos.Registrar(t.Clave, t.Nombre());
					RegistroHitos.Linea(Terrakeep.LogTag + " " + resultado);
				}

				_superadoAntes[t.Clave] = superadoAhora;
			}
		}

		public override void OnWorldUnload()
		{
			_lineaBase = false;
		}

		public override void Unload()
		{
			_atajo = null;
			_lineaBase = false;
			_superadoAntes.Clear();
		}

		/// <summary>Abre el panel en la pestaña del álbum, o lo cierra si ya estaba ahí.</summary>
		public static void AlternarPanel(string origen)
		{
			PanelTerrakeepSystem.AlternarArea(AreaTerrakeep.Album, origen);
		}
	}
}
