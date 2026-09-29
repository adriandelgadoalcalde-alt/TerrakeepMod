using System;
using Terraria;

namespace TerrakeepMod.Common.Panel
{
	/// <summary>
	/// Arnes SOLO DE VERIFICACION (no es codigo de produccion, no se activa nunca sin la variable
	/// de entorno): recorre las ocho areas del panel unico, una detras de otra, para la auditoria
	/// en vivo de UIScale/resolucion pedida en el encargo del 29-sep-2026 (requirement
	/// <c>0446b3c9-9108-4c1d-be03-66e050d7d1d6</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que existe en vez de pulsaciones de teclado reales.</b> Medido en esta misma sesion,
	/// con diagnostico dedicado: con la ventana del juego confirmada en primer plano real
	/// (<c>GetForegroundWindow() == hWnd</c>) y <c>SetWindowPos(HWND_TOPMOST)</c> aplicado,
	/// <c>PostMessage</c> con <c>WM_KEYDOWN</c>/<c>WM_KEYUP</c> directo al <c>hWnd</c> del juego NO
	/// abrio el panel (ni con una pulsacion ni con dos, esperando hasta 45s tras cargar el mundo) -
	/// cero lineas "Pestaña activa" en el log, que <see cref="TerrakeepMod.UI.Panel.PanelTerrakeepState.CambiarArea"/>
	/// escribe SIEMPRE que se abre una pestaña. Esto confirma, con una prueba mas estricta que la
	/// que documenta la entrada del mismo dia en <c>bitacora.md</c> (que solo comprobaba si el
	/// personaje se habia desplazado 1 tile, algo que tambien puede deberse al parpadeo normal de
	/// fisica del juego), la conclusion YA establecida el 7-sep-2026 tras cuatro vias distintas:
	/// FNA/SDL2 en esta instalacion de tModLoader lee el teclado por un camino que ignora
	/// sistematicamente la entrada sintetica generada en espacio de usuario
	/// (<c>keybd_event</c>/<c>SendInput</c>/<c>PostMessage</c>/<c>WH_JOURNALPLAYBACK</c>, las cuatro
	/// probadas). Este arnes evita el bloqueo llamando DIRECTAMENTE al mismo metodo de produccion
	/// que ya usa la barra de pestañas al recibir un clic real
	/// (<see cref="PanelTerrakeepSystem.AbrirEnArea"/>), exactamente igual que ya hacen
	/// <see cref="AutopruebaSoak"/> y una decena mas de arneses de este mismo fichero de sistema -
	/// no es una via nueva, es la ya establecida para todo lo que no se puede disparar con teclado
	/// sintetico en este entorno.
	/// </para>
	/// <para>
	/// Activado por la variable de entorno <see cref="Variable"/>. El script externo
	/// (<c>scripts</c> del repo o el arnes de la sesion de verificacion) solo tiene que vigilar el
	/// log en busca de la linea <c>AUDITORIA UISCALE: LISTO</c> de cada area y fotografiar la
	/// ventana del juego (nunca la pantalla completa) dentro de la ventana de
	/// <see cref="FotogramasPorArea"/> fotogramas que se deja abierta esa area antes de pasar a la
	/// siguiente.
	/// </para>
	/// </remarks>
	public static class AutopruebaAuditoriaUiScale
	{
		public const string Variable = "TERRAKEEP_AUDITORIA_UISCALE";

		private const int FotogramasDeEsperaInicial = 180;
		private const int FotogramasPorArea = 240;

		private static readonly TerrakeepMod.Common.Panel.AreaTerrakeep[] Orden = {
			TerrakeepMod.Common.Panel.AreaTerrakeep.Personaje,
			TerrakeepMod.Common.Panel.AreaTerrakeep.Libreria,
			TerrakeepMod.Common.Panel.AreaTerrakeep.Builds,
			TerrakeepMod.Common.Panel.AreaTerrakeep.Investigacion,
			TerrakeepMod.Common.Panel.AreaTerrakeep.Exploracion,
			TerrakeepMod.Common.Panel.AreaTerrakeep.Ajustes,
			TerrakeepMod.Common.Panel.AreaTerrakeep.Guia,
			TerrakeepMod.Common.Panel.AreaTerrakeep.Album
		};

		private static bool _comprobada;
		private static bool _activa;
		private static bool _terminada;
		private static int _fotogramasEnMundo;
		private static int _indiceArea = -1;
		private static int _fotogramasEnArea;

		public static void Avanzar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
				if (_activa) {
					RegistroPanel.Linea(Terrakeep.LogTag + " AUDITORIA UISCALE: " + Variable +
						" detectada. DisplayWidth=" + Main.screenWidth + " DisplayHeight=" + Main.screenHeight +
						" UIScale=" + Main.UIScale + ".");
				}
			}

			if (!_activa || _terminada) {
				return;
			}

			if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
				_fotogramasEnMundo = 0;
				return;
			}

			_fotogramasEnMundo++;
			if (_fotogramasEnMundo < FotogramasDeEsperaInicial) {
				return;
			}

			if (_indiceArea < 0) {
				_indiceArea = 0;
				AbrirAreaActual();
				return;
			}

			_fotogramasEnArea++;
			// A mitad del tiempo que el area se deja abierta: el layout ya ha corrido de sobra
			// (Update() de PestanaMapa, por ejemplo, reancla/reescala su bloque de texto cada
			// fotograma) y todavia queda margen antes de pasar a la siguiente area.
			if (_fotogramasEnArea == FotogramasPorArea / 2) {
				CapturarAreaActual();
			}
			if (_fotogramasEnArea < FotogramasPorArea) {
				return;
			}

			_indiceArea++;
			if (_indiceArea >= Orden.Length) {
				PanelTerrakeepSystem.CerrarPanel("auditoria UIScale: fin de la pasada, cierre limpio");
				_terminada = true;
				RegistroPanel.Linea(Terrakeep.LogTag + " AUDITORIA UISCALE: TERMINADA (" + Orden.Length + " areas recorridas).");
				return;
			}

			AbrirAreaActual();
		}

		private static void AbrirAreaActual()
		{
			TerrakeepMod.Common.Panel.AreaTerrakeep area = Orden[_indiceArea];
			PanelTerrakeepSystem.AbrirEnArea(area, "auditoria UIScale, area " + (_indiceArea + 1) + "/" + Orden.Length);
			_fotogramasEnArea = 0;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUDITORIA UISCALE: LISTO " + area +
				" (" + (_indiceArea + 1) + "/" + Orden.Length + "). PanelAbierto=" + PanelTerrakeepSystem.PanelAbierto +
				" AreaAbierta=" + PanelTerrakeepSystem.AreaAbierta +
				" UIScale=" + Main.UIScale + " Resolucion=" + Main.screenWidth + "x" + Main.screenHeight + ".");
		}

		/// <summary>
		/// Fotografia el AREA ACTUAL con el back buffer del propio motor (ver CapturaDePantalla),
		/// nombrada con la resolucion/UIScale reales para poder comparar varias pasadas sin que un
		/// nombre pise al anterior. Se llama unos fotogramas DESPUES de <see cref="AbrirAreaActual"/>
		/// (nunca en el mismo fotograma: hace falta que el layout ya haya corrido al menos una vez
		/// con el area nueva puesta) - <see cref="Avanzar"/> la dispara a mitad del tiempo que esa
		/// area se deja abierta.</summary>
		private static void CapturarAreaActual()
		{
			TerrakeepMod.Common.Panel.AreaTerrakeep area = Orden[_indiceArea];
			string escala = Main.UIScale.ToString("0.00").Replace(",", "_").Replace(".", "_");
			string nombre = "uiscale-" + area + "-" + Main.screenWidth + "x" + Main.screenHeight + "-" + escala;
			RegistroPanel.Linea(Terrakeep.LogTag + " AUDITORIA UISCALE: " + CapturaDePantalla.Guardar(nombre));
		}
	}
}
