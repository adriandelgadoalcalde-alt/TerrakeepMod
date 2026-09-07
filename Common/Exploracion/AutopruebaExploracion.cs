using System;
using Microsoft.Xna.Framework;
using Terraria;
using TerrakeepMod.Common.Panel;
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

		/// <summary>Estado del arrastre del mini-mapa con raton sintetico (pasos 17-22).</summary>
		private static int _ratonX;
		private static int _ratonY;
		private static Vector2 _centroAntesDelArrastre;
		private static float _escalaAlArrastrar;

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
			ContenidoExploracion panel = PanelExploracionSystem.Panel;

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
					ComprobarAtajo();
					// Con un CLIC REAL sobre el boton, no llamando al metodo por detras.
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/2 - clic real en el boton " +
						panel.PulsarBoton("Ver el mundo entero") + " -> " + panel.Mapa.Mapa.Informe());
					Siguiente(10);
					break;

				case 3:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/3 - clic real en " +
						panel.PulsarBoton("Acercar") + " y en " + panel.PulsarBoton("Centrar en mí") + ".");
					panel.Mapa.Mapa.Acercar(4f);
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
					// recien creado no ha explorado nada y la prueba no demostraria nada. La casilla
					// se acciona con un clic real sobre el alternador, no cambiando el campo.
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/4 - clic real en la casilla " +
						"\"Solo en lo que ya he explorado\" para apagarla: " + PulsarAlternador(panel));
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/4 - clic real en el boton " +
						panel.PulsarBoton("Buscar en el mundo"));
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

						// Comprobacion REAL del nombre bajo el cursor (sprites/tarea 3). La
						// coordenada del resultado es el CENTROIDE de su celda de 25x25 (media de
						// todos los tiles del objetivo que hay dentro: ver Acumular en
						// BuscadorMundo), y una veta tiene huecos de piedra/tierra entre medias, asi
						// que el centroide en si puede no ser mena. Se prueba por eso sobre el
						// PRIMER tile de la celda que SI es del objetivo buscado de verdad.
						int tx = (int)primero.Tile.X;
						int ty = (int)primero.Tile.Y;
						Vector2? tileDeVerdad = PrimerTileDelObjetivo(tx, ty, PanelExploracionSystem.Buscador.Objetivo);
						if (tileDeVerdad.HasValue) {
							int vx = (int)tileDeVerdad.Value.X;
							int vy = (int)tileDeVerdad.Value.Y;
							RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/6 - nombre real bajo un " +
								"tile CONFIRMADO del objetivo buscado, en (" + vx + ", " + vy + "): \"" +
								PestanaMapa.NombreBajoElCursor(vx, vy) + "\".");
						}
						else {
							RegistroExploracion.Aviso(Terrakeep.LogTag + " AUTOPRUEBA WS6/6 - no se encontro un tile " +
								"del objetivo dentro de su propia celda (no deberia pasar).");
						}

						// Y sobre un NPC vivo conocido: tiene que devolver su nombre, no el del
						// fondo que hubiera debajo.
						if (Main.npc != null) {
							for (int i = 0; i < Main.npc.Length; i++) {
								NPC npc = Main.npc[i];
								if (npc != null && npc.active && npc.townNPC) {
									int nx = (int)(npc.Center.X / 16f);
									int ny = (int)(npc.Center.Y / 16f);
									RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/6 - nombre real " +
										"bajo el tile del NPC \"" + npc.GivenOrTypeName + "\" (" + nx + ", " + ny +
										"): \"" + PestanaMapa.NombreBajoElCursor(nx, ny) + "\".");
									break;
								}
							}
						}
					}
					else {
						RegistroExploracion.Aviso(Terrakeep.LogTag + " AUTOPRUEBA WS6/6 - la busqueda no encontro nada, " +
							"no hay resultado que enseñar en el mapa.");
					}
					// El cambio de pestaña de arriba (CambiarPestana(0)) no se ve todavia: hace
					// falta esperar un fotograma para que se dibuje y se presente de verdad antes
					// de la captura del paso siguiente (CapturaDePantalla coge el fotograma YA
					// presentado, no el que se acaba de pedir).
					Siguiente(10);
					break;

				case 7:
					// Captura real de lo que hay en pantalla (sprites de la tarea 2): el mini-mapa
					// con los marcadores.
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/7 - " +
						CapturaDePantalla.Guardar("ws6-minimapa-iconos"));
					panel.CambiarPestana(1);
					Siguiente(10);
					break;

				case 8:
					// Y la lista de resultados, con sus iconos.
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/8 - " +
						CapturaDePantalla.Guardar("ws6-resultados-iconos"));
					Siguiente(5);
					break;

				case 9:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/9 - con los marcadores puestos: " +
						panel.Mapa.Mapa.Informe());
					// Los dos barridos de entidades, que no recorren el mundo.
					BuscarPorEtiqueta(panel, "Cofres y cómodas");
					BuscarPorEtiqueta(panel, "NPC vivos ahora mismo");
					Siguiente(5);
					break;

				case 10:
					ProbarDificultad(panel);
					Siguiente(10);
					break;

				case 11:
					// Se rehace la busqueda de tiles para que lo que salte al mapa vanilla sean sus
					// marcadores (decenas de zonas) y no los tres NPC del paso anterior.
					BuscarPorEtiqueta(panel, _objetivoDeTiles);
					panel.CambiarPestana(0);
					// La espera es imprescindible para que el informe del paso siguiente diga algo:
					// el mini-mapa se acaba de construir de cero al cambiar de pestaña y no ha
					// pasado por Draw ni una vez, asi que sus contadores estarian a cero.
					Siguiente(90);
					break;

				case 12:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/12 - antes de saltar al mapa: " +
						MarcadoresExploracion.Resultados.Count + " marcadores de \"" + MarcadoresExploracion.Titulo +
						"\" puestos, y " + panel.Mapa.Mapa.Informe());
					Siguiente(20);
					break;

				case 13:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/13 - el mini-mapa mira al tile (" +
						(int)panel.Mapa.Mapa.CentroTile.X + ", " + (int)panel.Mapa.Mapa.CentroTile.Y + ") con escala " +
						panel.Mapa.Mapa.Escala.ToString("0.00") + "; se pulsa \"Ver en el mapa del juego\" para " +
						"llevarse esa misma vista al mapa vanilla.");
					panel.Mapa.PulsarVerEnElMapa();
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/13 - pulsado: " +
						"Main.mapFullscreen=" + Main.mapFullscreen +
						", Main.InGameUI.CurrentState=" + (Main.InGameUI.CurrentState != null ? Main.InGameUI.CurrentState.GetType().Name : "(null)") +
						" (tiene que ser null: con el mapa abierto el juego no dibuja interfaz de mods).");
					Siguiente(90);
					break;

				case 14:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/14 - con el mapa vanilla abierto: " +
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

				case 15:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/15 - tras cerrar el mapa: " +
						"panel abierto de nuevo=" + PanelExploracionSystem.PanelAbierto +
						", InGameUI.CurrentState=" + (Main.InGameUI.CurrentState != null ? Main.InGameUI.CurrentState.GetType().Name : "(null)") + ".");
					Siguiente(5);
					break;

				case 16:
					RestaurarDificultad();
					Siguiente(5);
					break;

				// ---------------------------------------------------------------------------
				// Arrastre del mini-mapa con RATON SINTETICO (PanelExploracionSystem.
				// FijarRatonSintetico), no con un clic fisico real: se inyecta en
				// PostUpdateInput, el UNICO sitio del motor que escribe Main.mouseLeft cada
				// fotograma (comprobado con ilspycmd), asi que el propio juego calcula
				// Main.mouseLeftRelease a partir de ese valor exactamente igual que con un
				// clic de verdad. Reportado por el usuario jugando: "no te puedes mover por
				// el mapa arrastrando, solo apuntando con el zoom".
				// ---------------------------------------------------------------------------

				case 17:
					panel.CambiarPestana(0);
					Siguiente(10);
					break;

				case 18: {
					Terraria.UI.CalculatedStyle dim = panel.Mapa.Mapa.GetDimensions();
					_ratonX = (int)(dim.X + dim.Width / 2f);
					_ratonY = (int)(dim.Y + dim.Height / 2f);
					_centroAntesDelArrastre = panel.Mapa.Mapa.CentroTile;
					_escalaAlArrastrar = panel.Mapa.Mapa.Escala;
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/18 - preparando el arrastre: " +
						"raton sintetico en (" + _ratonX + ", " + _ratonY + ") sin pulsar todavia, sobre el mini-mapa " +
						"(area x=" + (int)dim.X + " y=" + (int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height + "). " +
						"Centro antes del arrastre: (" + (int)_centroAntesDelArrastre.X + ", " + (int)_centroAntesDelArrastre.Y +
						"), escala " + _escalaAlArrastrar.ToString("0.000") + ".");
					// La POSICION se fija en el propio MiniMapaTk (RatonSinteticoParaPrueba), no en
					// Main.mouseX/mouseY: comprobado exhaustivamente (ver bitacora.md, entrada de
					// hoy) que Main.DrawInterface reescribe Main.mouseX/mouseY desde el hardware
					// real varias veces por fotograma en sitios que ningun gancho publico de
					// ModSystem intercepta todos - con cero raton fisico en la maquina de pruebas
					// siempre acababa en (0, 0) por mucho que se reescribiera desde
					// PostUpdateInput/PostDrawInterface (con contadores reales: 953 y 840 llamadas
					// respectivamente, o sea que SI se invocaban, y aun asi (0, 0)). El BOTON si se
					// puede fijar con fiabilidad de esa forma (unico sitio de escritura real,
					// PlayerInput.UpdateInput()), y es justo lo que prueba que la deteccion de
					// flanco propia (_botonAbajoAnterior) funciona.
					MiniMapaTk.RatonSinteticoParaPrueba = true;
					MiniMapaTk.PosicionSinteticaParaPrueba = new Vector2(_ratonX, _ratonY);
					PanelExploracionSystem.FijarRatonSintetico(_ratonX, _ratonY, false);
					Siguiente(5);
					break;
				}

				case 19:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/19 - pulsando el boton izquierdo " +
						"(sintetico) sobre el mini-mapa, sin mover el raton todavia.");
					PanelExploracionSystem.FijarRatonSintetico(_ratonX, _ratonY, true);
					Siguiente(5);
					break;

				case 20:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/20 - tras pulsar: " +
						"Main.mouseLeft=" + Main.mouseLeft + ", MiniMapaTk.Arrastrando=" + panel.Mapa.Mapa.Arrastrando +
						" -> " + (panel.Mapa.Mapa.Arrastrando ? "OK, el arrastre ha arrancado." : "MAL: no ha arrancado.") +
						". Centro todavia en (" + (int)panel.Mapa.Mapa.CentroTile.X + ", " + (int)panel.Mapa.Mapa.CentroTile.Y +
						") (no se ha movido el raton todavia, tiene que seguir igual).");
					// Se mueve el raton sintetico mantiendo el boton pulsado: esto es el ARRASTRE.
					_ratonX += 130;
					_ratonY += 70;
					MiniMapaTk.PosicionSinteticaParaPrueba = new Vector2(_ratonX, _ratonY);
					PanelExploracionSystem.FijarRatonSintetico(_ratonX, _ratonY, true);
					Siguiente(8);
					break;

				case 21: {
					Vector2 centroEsperado = _centroAntesDelArrastre - new Vector2(130, 70) / _escalaAlArrastrar;
					Vector2 centroReal = panel.Mapa.Mapa.CentroTile;
					float distancia = Vector2.Distance(centroEsperado, centroReal);
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/21 - tras mover el raton sintetico " +
						"130x70 px con el boton pulsado: centro esperado (" + (int)centroEsperado.X + ", " + (int)centroEsperado.Y +
						"), centro real (" + (int)centroReal.X + ", " + (int)centroReal.Y + "), distancia " +
						distancia.ToString("0.0") + " tiles -> " +
						(distancia < 2f ? "OK: el mapa se ha desplazado arrastrando." : "MAL: el mapa NO se ha movido como se esperaba."));
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/21 - " +
						CapturaDePantalla.Guardar("ws6-arrastre-minimapa"));
					// Se suelta el boton.
					PanelExploracionSystem.FijarRatonSintetico(_ratonX, _ratonY, false);
					Siguiente(5);
					break;
				}

				case 22:
					MiniMapaTk.RatonSinteticoParaPrueba = false;
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/22 - tras soltar: " +
						"MiniMapaTk.Arrastrando=" + panel.Mapa.Mapa.Arrastrando +
						" -> " + (!panel.Mapa.Mapa.Arrastrando ? "OK, el arrastre ha terminado." : "MAL: se ha quedado arrastrando."));
					PanelExploracionSystem.ApagarRatonSintetico();
					Siguiente(5);
					break;

				case 23:
					RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6 COMPLETA.");
					_enMarcha = false;
					break;
			}
		}

		/// <summary>
		/// Busca, dentro de la celda de <see cref="BuscadorMundo.LadoCelda"/> centrada en el
		/// centroide que devuelve un resultado, el primer tile que SI es de verdad el objetivo:
		/// el centroide es la MEDIA de las coordenadas de todos los tiles de la celda
		/// (<c>BuscadorMundo.Acumular</c>), y una veta tiene huecos de piedra/tierra entre medias,
		/// asi que el centroide en si puede no ser mena. null si no aparece ninguno (no deberia
		/// pasar: el propio contador de la busqueda dice que ahi hay al menos uno).
		/// </summary>
		private static Vector2? PrimerTileDelObjetivo(int centroX, int centroY, ObjetivoBusqueda objetivo)
		{
			if (objetivo == null) {
				return null;
			}
			// Cofres y NPC no son centroides: su Tile YA es la posicion real.
			if (objetivo.Clase == ClaseDeObjetivo.Cofres || objetivo.Clase == ClaseDeObjetivo.Npcs) {
				return new Vector2(centroX, centroY);
			}

			int radio = BuscadorMundo.LadoCelda;
			for (int dx = -radio; dx <= radio; dx++) {
				for (int dy = -radio; dy <= radio; dy++) {
					int x = centroX + dx;
					int y = centroY + dy;
					if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) {
						continue;
					}

					Tile tile = Main.tile[x, y];
					bool acierto;
					if (objetivo.Clase == ClaseDeObjetivo.Liquido) {
						acierto = tile.LiquidAmount > 0 && tile.LiquidType == objetivo.Liquido;
					}
					else if (objetivo.Clase == ClaseDeObjetivo.Pared) {
						acierto = objetivo.Tipos.Contains(tile.WallType);
					}
					else {
						acierto = tile.HasTile && objetivo.Tipos.Contains(tile.TileType);
					}

					if (acierto) {
						return new Vector2(x, y);
					}
				}
			}
			return null;
		}

		private static void BuscarPorEtiqueta(ContenidoExploracion panel, string etiqueta)
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
		private static void ProbarDificultad(ContenidoExploracion panel)
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
				RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/16 - el mundo ya esta en su modo " +
					"original (" + MundoActual.ModoDeJuegoLegible + "), no hay nada que restaurar.");
				return;
			}

			int antes = Main.GameMode;
			Main.GameMode = _modoOriginal;
			RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/16 - modo del mundo restaurado: " +
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

		/// <summary>
		/// Acciona con un clic REAL la casilla "Solo en lo que ya he explorado" del panel, si esta
		/// marcada, para que la busqueda recorra el mundo entero.
		/// </summary>
		private static string PulsarAlternador(ContenidoExploracion panel)
		{
			UI.Personaje.Widgets.AlternadorTk casilla =
				panel.BuscarPrimero<UI.Personaje.Widgets.AlternadorTk>();
			if (casilla == null) {
				return "no se encontro la casilla";
			}

			bool antes = casilla.Valor;
			if (antes) {
				Terraria.UI.CalculatedStyle dim = casilla.GetDimensions();
				Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
				casilla.LeftClick(new Terraria.UI.UIMouseEvent(casilla, centro));
			}
			return "estaba en " + antes + ", ahora " + casilla.Valor +
				(antes ? " (clic dado)" : " (ya estaba apagada, no hacia falta)");
		}

		/// <summary>
		/// Deja en el log la tecla que tiene asignada CADA atajo del mod, leida del perfil de
		/// controles real del jugador.
		/// </summary>
		/// <remarks>
		/// Es la unica forma de saber si la tecla P de este panel esta puesta de verdad: un
		/// <c>ModKeybind</c> recien registrado nace SIN tecla y solo se la pone el
		/// <c>SembradorDeAtajos</c> de WS7 (ver su documentacion y la bitacora). Se compara con un
		/// atajo de vanilla para que se vea que se esta leyendo el sitio bueno.
		/// </remarks>
		private static void ComprobarAtajo()
		{
			try {
				Terraria.GameInput.PlayerInputProfile perfil = Terraria.GameInput.PlayerInput.CurrentProfile;
				if (perfil == null || !perfil.InputModes.ContainsKey(Terraria.GameInput.InputMode.Keyboard)) {
					return;
				}

				Terraria.GameInput.KeyConfiguration teclado = perfil.InputModes[Terraria.GameInput.InputMode.Keyboard];
				System.Collections.Generic.List<string> nuestros = new System.Collections.Generic.List<string>();
				foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.List<string>> par in teclado.KeyStatus) {
					if (par.Key.StartsWith("TerrakeepMod/")) {
						nuestros.Add(par.Key + "=[" + string.Join("+", par.Value) + "]");
					}
				}

				RegistroExploracion.Linea(Terrakeep.LogTag + " AUTOPRUEBA WS6/2 - teclas asignadas a los atajos del " +
					"mod: " + string.Join(", ", nuestros) + ". Para comparar, un atajo VANILLA: QuickHeal=[" +
					string.Join("+", teclado.KeyStatus["QuickHeal"]) + "].");
			}
			catch (Exception e) {
				RegistroExploracion.Aviso(Terrakeep.LogTag + " AUTOPRUEBA WS6/2 - no se pudieron leer los atajos: " + e.Message);
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
