using System;
using Microsoft.Xna.Framework;
using Terraria;
using TerrakeepMod.UI.Exploracion;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Autoprueba de WS6: acciona el panel de Exploracion de punta a punta sin que nadie toque el
	/// teclado, y deja en el log de evidencia el ANTES y el DESPUES de cada paso, leidos siempre
	/// de los campos reales del juego.
	/// </summary>
	/// <remarks>
	/// Es una maquina de estados que avanza un paso por fotograma con esperas entre medias, no una
	/// funcion que lo hace todo de una: casi nada de lo que hay que comprobar aqui es cierto en el
	/// mismo fotograma en que se pide. El mini-mapa no ha dibujado ni un trozo hasta que pasa por
	/// <c>Draw</c>, la busqueda esta troceada a proposito y tarda varios fotogramas, y la capa del
	/// mapa vanilla solo se ejecuta mientras el mapa esta realmente abierto. Comprobarlo en el
	/// acto daria verdes falsos.
	/// <para />
	/// Solo se activa con <see cref="PanelExploracionSystem.VariableAutoprueba"/> puesta en el
	/// entorno; jugando normalmente este codigo no corre nunca.
	/// </remarks>
	public static class AutopruebaExploracion
	{
		private static bool _enMarcha;
		private static int _paso;
		private static int _espera;
		private static int _fotogramasEsperandoBusqueda;
		private static int _modoOriginal = -1;

		/// <summary>Objetivo de tiles que se ha buscado; se vuelve a buscar antes de saltar al mapa
		/// vanilla para que lo que se dibuje alli sean sus marcadores.</summary>
		private static string _objetivoDeTiles = "Cobre";

		public static void Arrancar()
		{
			if (_enMarcha) {
				return;
			}

			_enMarcha = true;
			_paso = 0;
			_espera = 0;
			_modoOriginal = Main.GameMode;

			string marca = Environment.GetEnvironmentVariable(PanelExploracionSystem.VariableMarca);
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6: " +
				PanelExploracionSystem.VariableAutoprueba + " detectada. Marca de ejecucion: " + marca);
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/estado inicial: mundo \"" +
				MundoActual.Nombre + "\" " + MundoActual.TamanoLegible +
				", modo " + MundoActual.ModoDeJuegoLegible + " (Main.GameMode=" + Main.GameMode +
				", expertMode=" + Main.expertMode + ", masterMode=" + Main.masterMode + ")" +
				", jugador \"" + Main.LocalPlayer.name + "\" difficulty=" + Main.LocalPlayer.difficulty +
				", Main.netMode=" + Main.netMode + ", Main.autoSave=" + Main.autoSave +
				", Main.mapEnabled=" + Main.mapEnabled + ", Main.mapReady=" + Main.mapReady +
				", explorado " + MundoActual.PorcentajeExplorado().ToString("0.0") + "%.");
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/catalogo: " + CatalogoObjetivos.Resumen());
		}

		/// <summary>Un paso por fotograma. Lo llama <see cref="PanelExploracionSystem.UpdateUI"/>.</summary>
		public static void Avanzar()
		{
			if (!_enMarcha) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}

			try {
				Ejecutar();
			}
			catch (Exception e) {
				RegistroExploracion.Error(Terrakeep.LogTag + " AUTOPRUEBA WS6: EXCEPCION en el paso " +
					_paso + ": " + e);
				_enMarcha = false;
			}
		}

		private static void Ejecutar()
		{
			PanelExploracionState panel = PanelExploracionSystem.Panel;

			switch (_paso) {
				case 0:
					SembrarMapaDePrueba();
					PanelExploracionSystem.AbrirPanel("autoprueba (" + PanelExploracionSystem.VariableAutoprueba + ")");
					Siguiente(15);
					break;

				case 1:
					panel.CambiarPestana(0);
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/1 - pestaña \"" +
						panel.NombrePestanaActual + "\": " + panel.InformePestanaActual());
					// Margen largo a proposito: tras marcar el mapa como sucio, el juego lo repinta
					// a trozos con un presupuesto de 5 ms por fotograma (bucle de DrawToMap_Section
					// en Main.DoDraw), no de golpe.
					Siguiente(240);
					break;

				case 2:
					// El mini-mapa ya ha pasado por Draw muchas veces: aqui es donde se sabe si ha
					// dibujado trozos REALES de mapTarget o si no habia nada.
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/2 - " + panel.Mapa.Mapa.Informe());
					ComprobarPixelDelMapa();
					panel.Mapa.Mapa.EncuadrarMundo();
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/2 - tras \"Ver el mundo entero\": " +
						panel.Mapa.Mapa.Informe());
					Siguiente(10);
					break;

				case 3:
					panel.Mapa.Mapa.Acercar(4f);
					panel.Mapa.Mapa.CentrarEnJugador();
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/3 - tras acercar y centrar en el jugador: " +
						panel.Mapa.Mapa.Informe() + ". Jugador en el tile " + MundoActual.PosicionDelJugador +
						"; en pantalla el jugador cae en " + Redondear(panel.Mapa.Mapa.TileAPantalla(Main.LocalPlayer.Center / 16f)) +
						" (el centro del mini-mapa tiene que ser ese punto).");
					Siguiente(10);
					break;

				case 4:
					panel.CambiarPestana(1);
					string pedido = Environment.GetEnvironmentVariable(PanelExploracionSystem.VariableBuscar);
					if (string.IsNullOrEmpty(pedido)) {
						pedido = "Cobre";
					}
					_objetivoDeTiles = pedido;
					bool encontrado = panel.Busqueda.SeleccionarPorEtiqueta(pedido);
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/4 - pestaña \"" +
						panel.NombrePestanaActual + "\": " + panel.InformePestanaActual() +
						". Objetivo pedido \"" + pedido + "\" -> " +
						(encontrado ? "seleccionado (\"" + panel.Busqueda.Seleccionado.EtiquetaLegible() + "\")" : "NO existe en esta partida"));
					// Se busca en TODO el mundo, no solo en lo explorado: un personaje de prueba
					// recien creado no ha explorado nada y la prueba no demostraria nada.
					PanelExploracionSystem.Buscar(panel.Busqueda.Seleccionado, false, "autoprueba");
					_fotogramasEsperandoBusqueda = 0;
					Siguiente(1);
					break;

				case 5:
					_fotogramasEsperandoBusqueda++;
					if (PanelExploracionSystem.Buscador.EnMarcha && _fotogramasEsperandoBusqueda < 1800) {
						return;   // se queda en este paso hasta que termine
					}
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/5 - busqueda terminada en " +
						_fotogramasEsperandoBusqueda + " fotogramas: " + PanelExploracionSystem.Buscador.Resumen());
					Siguiente(5);
					break;

				case 6:
					if (PanelExploracionSystem.Buscador.Resultados.Count > 0) {
						ResultadoBusqueda primero = PanelExploracionSystem.Buscador.Resultados[0];
						panel.CambiarPestana(0);
						panel.Mapa.Mapa.CentrarEn(primero.Tile, 3f);
						RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/6 - mini-mapa centrado en el " +
							"primer resultado (" + (int)primero.Tile.X + ", " + (int)primero.Tile.Y + "): " +
							panel.Mapa.Mapa.Informe());
					}
					else {
						RegistroExploracion.Aviso(Terrakeep.LogTag + " AUTOPRUEBA WS6/6 - la busqueda no encontro nada, " +
							"no hay resultado que enseñar en el mapa.");
					}
					Siguiente(20);
					break;

				case 7:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/7 - con los marcadores puestos: " +
						panel.Mapa.Mapa.Informe());
					// Los dos barridos de entidades, que no recorren el mundo.
					BuscarPorEtiqueta(panel, "Cofres y cómodas");
					BuscarPorEtiqueta(panel, "NPC vivos ahora mismo");
					Siguiente(5);
					break;

				case 8:
					ProbarDificultad(panel);
					Siguiente(10);
					break;

				case 9:
					// Se rehace la busqueda de tiles para que lo que salte al mapa vanilla sean sus
					// marcadores (decenas de zonas) y no los tres NPC del paso anterior.
					BuscarPorEtiqueta(panel, _objetivoDeTiles);
					panel.CambiarPestana(0);
					// La espera es imprescindible para que el informe del paso siguiente diga algo:
					// el mini-mapa se acaba de construir de cero al cambiar de pestaña y no ha
					// pasado por Draw ni una vez, asi que sus contadores estarian a cero.
					Siguiente(90);
					break;

				case 10:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/10 - antes de saltar al mapa: " +
						MarcadoresExploracion.Resultados.Count + " marcadores de \"" + MarcadoresExploracion.Titulo +
						"\" puestos, y " + panel.Mapa.Mapa.Informe());
					Siguiente(20);
					break;

				case 11:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/11 - el mini-mapa mira al tile (" +
						(int)panel.Mapa.Mapa.CentroTile.X + ", " + (int)panel.Mapa.Mapa.CentroTile.Y + ") con escala " +
						panel.Mapa.Mapa.Escala.ToString("0.00") + "; se pulsa \"Ver en el mapa del juego\" para " +
						"llevarse esa misma vista al mapa vanilla.");
					panel.Mapa.PulsarVerEnElMapa();
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/11 - pulsado: " +
						"Main.mapFullscreen=" + Main.mapFullscreen +
						", Main.InGameUI.CurrentState=" + (Main.InGameUI.CurrentState != null ? Main.InGameUI.CurrentState.GetType().Name : "(null)") +
						" (tiene que ser null: con el mapa abierto el juego no dibuja interfaz de mods).");
					Siguiente(90);
					break;

				case 12:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/12 - con el mapa vanilla abierto: " +
						"Main.mapFullscreen=" + Main.mapFullscreen +
						", mapFullscreenPos=(" + (int)Main.mapFullscreenPos.X + ", " + (int)Main.mapFullscreenPos.Y + ")" +
						", mapFullscreenScale=" + Main.mapFullscreenScale.ToString("0.00") +
						". La capa de mapa de Terrakeep ha dibujado marcadores en " +
						CapaMapaExploracion.FotogramasDibujados + " fotogramas (ultimo: " +
						CapaMapaExploracion.DibujadosUltimoFotograma + " marcadores).");
					// Se cierra el mapa igual que lo cerraria el jugador con Esc o con el icono.
					Main.mapFullscreen = false;
					Siguiente(20);
					break;

				case 13:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/13 - tras cerrar el mapa: " +
						"panel abierto de nuevo=" + PanelExploracionSystem.PanelAbierto +
						", InGameUI.CurrentState=" + (Main.InGameUI.CurrentState != null ? Main.InGameUI.CurrentState.GetType().Name : "(null)") + ".");
					Siguiente(5);
					break;

				case 14:
					RestaurarDificultad();
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6 COMPLETA.");
					_enMarcha = false;
					break;
			}
		}

		private static void BuscarPorEtiqueta(PanelExploracionState panel, string etiqueta)
		{
			panel.CambiarPestana(1);
			if (!panel.Busqueda.SeleccionarPorEtiqueta(etiqueta)) {
				RegistroExploracion.Aviso(Terrakeep.LogTag + " AUTOPRUEBA WS6: no hay ningun objetivo que case con \"" +
					etiqueta + "\".");
				return;
			}
			PanelExploracionSystem.Buscar(panel.Busqueda.Seleccionado, false, "autoprueba (" + etiqueta + ")");
		}

		/// <summary>
		/// Prueba el cambio de dificultad de las dos formas que importan: una que TIENE que
		/// funcionar y otra que TIENE que quedar bloqueada por la salvaguarda del modo Viaje.
		/// </summary>
		private static void ProbarDificultad(PanelExploracionState panel)
		{
			panel.CambiarPestana(2);
			PestanaMundo mundo = panel.Mundo;

			// 1) El modo Viaje con un personaje que no es de Viaje: tiene que salir bloqueado, no
			//    aplicado. Es la salvaguarda concreta que exigia el plan.
			string motivoViaje = DificultadMundo.MotivoParaNoPoder(3);
			int antesDeIntentarViaje = Main.GameMode;
			mundo.Elegir(3);
			mundo.Confirmar();
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/8a - intento de poner modo VIAJE con un " +
				"personaje difficulty=" + Main.LocalPlayer.difficulty + ": motivo=\"" + motivoViaje + "\". " +
				"Main.GameMode antes=" + antesDeIntentarViaje + ", ahora=" + Main.GameMode +
				" -> " + (Main.GameMode == antesDeIntentarViaje ? "OK, NO se ha tocado nada." : "MAL: se ha cambiado."));

			// 2) Un cambio que si se puede hacer: al modo que no sea el actual.
			int destino = Main.GameMode == 0 ? 1 : 0;
			mundo.Elegir(destino);
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/8b - elegido el modo \"" +
				MundoActual.NombreDeModo(destino) + "\" (paso 1 de 2). ModoElegido del panel=" + mundo.ModoElegido +
				" -> " + (mundo.ModoElegido == destino ? "OK" : "MAL") +
				". Main.GameMode sigue siendo " + Main.GameMode + " (todavia no se ha confirmado).");

			mundo.Confirmar();
			bool bien = Main.GameMode == destino;
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/8c - confirmado (paso 2 de 2). " +
				"Main.GameMode=" + Main.GameMode + " (esperado " + destino + ")" +
				", Main.expertMode=" + Main.expertMode +
				", Main.masterMode=" + Main.masterMode +
				", Main.GameModeInfo.IsExpertMode=" + Main.GameModeInfo.IsExpertMode +
				", ActiveWorldFileData.GameMode=" + Main.ActiveWorldFileData.GameMode +
				" -> " + (bien ? "OK" : "MAL"));

			// Se comprueba tambien que ha entrado en el historial de WS7 y que deshacerlo funciona.
			string deshecho = Undo.Historial.Deshacer();
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/8d - deshacer del historial: \"" +
				deshecho + "\". Main.GameMode=" + Main.GameMode +
				", expertMode=" + Main.expertMode +
				" -> " + (Main.GameMode == _modoOriginal ? "OK, ha vuelto al modo original." : "MAL"));

			// Y se rehace, para dejar la prueba habiendo demostrado el cambio de verdad.
			string rehecho = Undo.Historial.Rehacer();
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/8e - rehacer: \"" + rehecho +
				"\". Main.GameMode=" + Main.GameMode + ", expertMode=" + Main.expertMode + ", masterMode=" + Main.masterMode + ".");
		}

		/// <summary>
		/// Deja el mundo de prueba como estaba. No es cosmetico: el cambio de modo se graba en el
		/// <c>.wld</c> en el siguiente guardado, y una prueba no puede dejar el mundo de prueba
		/// distinto para la siguiente ejecucion.
		/// </summary>
		private static void RestaurarDificultad()
		{
			if (_modoOriginal < 0 || Main.GameMode == _modoOriginal) {
				RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/14 - el mundo ya esta en su modo " +
					"original (" + MundoActual.ModoDeJuegoLegible + "), no hay nada que restaurar.");
				return;
			}

			int antes = Main.GameMode;
			Main.GameMode = _modoOriginal;
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/14 - modo del mundo restaurado: " +
				MundoActual.NombreDeModo(antes) + " -> " + MundoActual.ModoDeJuegoLegible +
				" (Main.GameMode=" + Main.GameMode + ", expertMode=" + Main.expertMode +
				", masterMode=" + Main.masterMode + ").");
		}

		/// <summary>
		/// SOLO ARNES DE PRUEBAS. Marca como descubierto un rectangulo de mapa alrededor del punto
		/// de aparicion.
		/// </summary>
		/// <remarks>
		/// Hace falta porque el personaje de prueba es sintetico y no ha jugado nunca: su mapa esta
		/// entero a oscuras, y con el mapa vacio el mini-mapa dibujaria correctamente... nada, que
		/// no demuestra nada. Es exactamente el mismo tipo de preparacion de escenario que hace WS4
		/// sembrando objetos en el inventario antes de probar "ya lo tienes", y se activa solo con
		/// la variable de entorno puesta: la funcionalidad del mod no revela mapa jamas.
		/// <para />
		/// Se usa <c>Main.Map.Update(x, y, luz)</c>, que es el metodo publico que usa el propio
		/// juego para descubrir mapa (por dentro llama a <c>MapHelper.CreateMapTile</c>), y luego
		/// se marca el mapa como sucio para que el motor repinte sus <c>mapTarget</c>.
		/// </remarks>
		private static void SembrarMapaDePrueba()
		{
			string texto = Environment.GetEnvironmentVariable("TERRAKEEP_WS6_REVELAR");
			int radio;
			if (string.IsNullOrEmpty(texto) || !int.TryParse(texto, out radio) || radio <= 0) {
				return;
			}

			int centroX = Main.spawnTileX;
			int centroY = Main.spawnTileY;
			int desde = centroX - radio;
			int hasta = centroX + radio;
			int arriba = centroY - radio / 2;
			int abajo = centroY + radio / 2;

			if (desde < 10) { desde = 10; }
			if (arriba < 10) { arriba = 10; }
			if (hasta > Main.maxTilesX - 10) { hasta = Main.maxTilesX - 10; }
			if (abajo > Main.maxTilesY - 10) { abajo = Main.maxTilesY - 10; }

			int revelados = 0;
			for (int x = desde; x < hasta; x++) {
				for (int y = arriba; y < abajo; y++) {
					Main.Map.Update(x, y, 255);
					revelados++;
				}
			}

			Main.refreshMap = true;
			Main.updateMap = true;

			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/0 - ESCENARIO DE PRUEBA: revelados " +
				revelados + " tiles de mapa alrededor de la aparicion (" + desde + ".." + hasta + " x " +
				arriba + ".." + abajo + ") con Main.Map.Update, y mapa marcado para repintar. " +
				"Esto NO es funcionalidad del mod: el personaje de prueba es sintetico y no ha explorado nada.");
		}

		/// <summary>
		/// Lee un pixel REAL de la textura del mapa del juego en la posicion del jugador y lo deja
		/// en el log.
		/// </summary>
		/// <remarks>
		/// Es la prueba directa de la correspondencia que usa el mini-mapa: el pixel
		/// <c>(x % 2000, y % 1800)</c> de la casilla <c>[x / 2000, y / 1800]</c> de
		/// <c>Main.instance.mapTarget</c> es el tile <c>(x, y)</c> del mundo. Si esa cuenta
		/// estuviera mal, el mini-mapa se veria desplazado y aqui saldria un pixel transparente en
		/// un sitio que si esta explorado.
		/// </remarks>
		private static void ComprobarPixelDelMapa()
		{
			try {
				if (Main.instance == null || Main.instance.mapTarget == null) {
					return;
				}

				int x = (int)(Main.LocalPlayer.Center.X / 16f);
				int y = (int)(Main.LocalPlayer.Center.Y / 16f);
				int k = x / Main.textureMaxWidth;
				int l = y / Main.textureMaxHeight;

				Microsoft.Xna.Framework.Graphics.RenderTarget2D trozo = Main.instance.mapTarget[k, l];
				if (trozo == null || trozo.IsDisposed) {
					RegistroExploracion.Aviso(Terrakeep.LogTag + " AUTOPRUEBA WS6/2 - no hay textura de mapa en la " +
						"casilla [" + k + ", " + l + "].");
					return;
				}

				Color[] pixel = new Color[1];
				trozo.GetData(0, new Rectangle(x % Main.textureMaxWidth, y % Main.textureMaxHeight, 1, 1), pixel, 0, 1);

				RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/2 - pixel real de la textura del mapa " +
					"del juego en el tile del jugador (" + x + ", " + y + ") = casilla mapTarget[" + k + ", " + l + "] " +
					"pixel (" + (x % Main.textureMaxWidth) + ", " + (y % Main.textureMaxHeight) + "): " +
					"RGBA(" + pixel[0].R + ", " + pixel[0].G + ", " + pixel[0].B + ", " + pixel[0].A + ")" +
					" -> " + (pixel[0].A > 0 ? "OK: hay mapa dibujado ahi de verdad." : "vacio (esa zona no esta explorada).") +
					" El mapa del juego dice IsRevealed=" + (Main.Map != null && Main.Map.IsRevealed(x, y)) + ".");
			}
			catch (Exception e) {
				RegistroExploracion.Aviso(Terrakeep.LogTag + " AUTOPRUEBA WS6/2 - no se pudo leer el pixel del " +
					"mapTarget: " + e.Message);
			}
		}

		private static string Redondear(Vector2 punto)
		{
			return "(" + (int)punto.X + ", " + (int)punto.Y + ")";
		}

		private static void Siguiente(int esperaEnFotogramas)
		{
			_paso++;
			_espera = esperaEnFotogramas;
		}
	}
}
