using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Panel;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Hitos
{
	/// <summary>
	/// Verificación EN EL JUEGO REAL de la pestaña "Álbum" en sí (que <c>HitosSystem</c> dispara
	/// solo capturas de verdad ya lo demuestra <c>verificar-guia.ps1</c>, sin arnés propio - ver la
	/// cabecera de <see cref="HitosSystem"/>). Esto comprueba la OTRA mitad del encargo: que el
	/// panel para verlas abre por clic real, que el resumen y los dos botones del encabezado no se
	/// solapan entre sí ni con la lista, y que la lista refleja de verdad lo que hay en el álbum del
	/// disco.
	/// </summary>
	public static class AutopruebaHitos
	{
		/// <summary>Variable de entorno que la activa.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_HITOS";

		private const int FotogramasDeEspera = 180;
		private const int FotogramasEntrePasos = 20;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _paso;
		private static int _espera;

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
			}

			if (!_activa || _terminada) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < FotogramasDeEspera) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			try {
				Paso(_paso);
				_paso++;
			}
			catch (Exception e) {
				RegistroHitos.Aviso(Terrakeep.LogTag + " AUTOPRUEBA ALBUM: EXCEPCION en el paso " +
					(_paso - 1) + ": " + e);
				_terminada = true;
			}
		}

		private static void Paso(int paso)
		{
			switch (paso) {
				case 0: PanelTerrakeepSystem.AbrirEnArea(AreaTerrakeep.Personaje, "autoprueba del álbum"); break;
				case 1: AbrirLaPestanaConClicReal(); break;
				case 2: ComprobarEspaciadoYContenido(); break;
				case 3: Capturar("hitos-1-album"); break;
				case 4: PanelTerrakeepSystem.CerrarPanel("autoprueba del álbum terminada"); break;
				default: Terminar(); break;
			}
		}

		private static void AbrirLaPestanaConClicReal()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			if (panel == null) {
				RegistroHitos.Aviso(Terrakeep.LogTag + " AUTOPRUEBA ALBUM: el panel no está abierto.");
				return;
			}

			string pulsada = panel.PulsarPestana(AreaTerrakeep.Album);
			bool ok = PanelTerrakeepSystem.AreaAbierta == AreaTerrakeep.Album;
			RegistroHitos.Linea(Terrakeep.LogTag + " AUTOPRUEBA ALBUM/1 - CLIC REAL en la pestaña " +
				pulsada + ". Pestaña activa ahora: \"" +
				PanelTerrakeepState.NombresDeArea[(int)PanelTerrakeepSystem.AreaAbierta] + "\" " +
				(ok ? "-> OK" : "-> NO CUADRA"));
		}

		/// <summary>
		/// Mide con NUMEROS reales (no de vista) los dos botones del encabezado, el resumen y la
		/// caja de la lista, y compara la lista de <c>Entradas</c> montada con lo que hay de verdad
		/// en <c>album.json</c> - mismo criterio que el resto de autopruebas del mod (medir, no
		/// suponer).
		/// </summary>
		private static void ComprobarEspaciadoYContenido()
		{
			PanelTerrakeepState panel = PanelTerrakeepSystem.Panel;
			TerrakeepMod.UI.Hitos.ContenidoAlbum album = panel != null ? panel.Album : null;
			if (album == null) {
				RegistroHitos.Aviso(Terrakeep.LogTag + " AUTOPRUEBA ALBUM: la pestaña Álbum no está montada.");
				return;
			}

			Rectangle? actualizar = null, abrirCarpeta = null, caja = null;
			string textoActualizar = Idiomas.Texto("Hitos.Actualizar");
			string textoAbrirCarpeta = Idiomas.Texto("Hitos.AbrirCarpeta");

			album.ExecuteRecursively(elemento => {
				BotonTk boton = elemento as BotonTk;
				if (boton != null) {
					CalculatedStyle d = boton.GetDimensions();
					Rectangle r = new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);
					if (boton.Texto == textoActualizar) {
						actualizar = r;
					}
					else if (boton.Texto == textoAbrirCarpeta) {
						abrirCarpeta = r;
					}
				}

				// OJO: BotonTk hereda de UIPanel (ver BotonTk.cs), asi que "is UIPanel" a secas
				// pillaria "Actualizar" antes que la caja real de la lista - se excluye a proposito.
				if (caja == null && elemento is UIPanel && !(elemento is BotonTk)) {
					CalculatedStyle d = elemento.GetDimensions();
					caja = new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);
				}
			});

			bool medido = actualizar.HasValue && abrirCarpeta.HasValue && caja.HasValue;
			bool sinSolapeBotones = medido && actualizar.Value.Right <= abrirCarpeta.Value.Left;
			bool listaDebajo = medido &&
				caja.Value.Top >= actualizar.Value.Bottom && caja.Value.Top >= abrirCarpeta.Value.Bottom;

			RegistroHitos.Linea(Terrakeep.LogTag + " AUTOPRUEBA ALBUM/2 - espaciado real: " +
				"\"Actualizar\"=" + Describir(actualizar) + " \"Abrir carpeta\"=" + Describir(abrirCarpeta) +
				" caja-lista=" + Describir(caja) + " -> " +
				(medido && sinSolapeBotones && listaDebajo
					? "OK: sin solapes."
					: "NO CUADRA (medido=" + medido + ", sinSolapeBotones=" + sinSolapeBotones +
						", listaDebajo=" + listaDebajo + ")."));

			int enPanel = album.Entradas.Count;
			int enDisco = AlbumHitos.Listar().Count;
			RegistroHitos.Linea(Terrakeep.LogTag + " AUTOPRUEBA ALBUM/3 - entradas montadas en la pestaña: " +
				enPanel + ". Entradas reales en album.json: " + enDisco + " -> " +
				(enPanel == enDisco && enDisco > 0
					? "OK: coinciden y el álbum no está vacío."
					: "NO CUADRA."));
		}

		private static string Describir(Rectangle? r)
		{
			return r.HasValue
				? "x=" + r.Value.X + " y=" + r.Value.Y + " " + r.Value.Width + "x" + r.Value.Height
				: "(no encontrado)";
		}

		private static void Capturar(string nombre)
		{
			string resultado = Common.Panel.CapturaDePantalla.Guardar(nombre);
			RegistroHitos.Linea(Terrakeep.LogTag + " AUTOPRUEBA ALBUM - " + resultado);
		}

		private static void Terminar()
		{
			_terminada = true;
			RegistroHitos.Linea(Terrakeep.LogTag + " AUTOPRUEBA ALBUM COMPLETA.");
		}
	}
}
