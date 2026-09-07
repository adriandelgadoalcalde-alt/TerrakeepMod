using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.Common.Prefijos;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI;
using TerrakeepMod.UI.Libreria.Widgets;
using TerrakeepMod.UI.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.Common.Personaje
{
	/// <summary>
	/// Arnes de verificacion de WS1. Se activa con la variable de entorno
	/// <c>TERRAKEEP_AUTOTEST_WS1</c> y, con el panel ya abierto, ejercita CADA funcion del panel
	/// sobre el jugador real y deja en <c>client.log</c> el valor de antes y el de despues leidos
	/// del propio <see cref="Player"/>.
	/// <para />
	/// Por que existe: sin sesion de escritorio no se pueden simular clics de raton reales (ya se
	/// documento en la bitacora de WS0: <c>Main.hasFocus</c> pone <c>mouseLeftRelease = false</c>
	/// cada fotograma si la ventana no tiene foco, asi que un clic sintetico no cuenta nunca).
	/// La forma honesta de demostrar que algo funciona es entonces ejecutar exactamente el mismo
	/// codigo que ejecutaria el clic y enseñar el efecto real sobre el jugador. Donde se puede,
	/// se llama directamente a la ruta de vanilla (<c>ItemSlot.LeftClick</c>,
	/// <c>Player.AddBuff</c>, <c>Player.TrySwitchingLoadout</c>) en vez de escribir el campo a
	/// pelo, para que la prueba valga de verdad.
	/// <para />
	/// Va troceada por fotogramas a proposito: entre paso y paso el juego tiene que llegar a
	/// dibujar, porque parte de lo que se comprueba (que las pestañas se colocan y ocupan sitio
	/// real en pantalla) solo existe despues de un <c>Recalculate</c> + <c>Draw</c>.
	/// </summary>
	public static class AutopruebaPersonaje
	{
		/// <summary>Variable de entorno que enciende esta autoprueba.</summary>
		public const string Variable = "TERRAKEEP_AUTOTEST_WS1";

		private const int FotogramasEntrePasos = 10;

		private static bool _activa;
		private static bool _terminada;
		private static bool _comprobada;
		private static int _paso;
		private static int _espera;
		private static int _buffVigilado;
		private static int _tiempoBuffInicial;
		private static uint _tickAlAplicarBuff;
		private static int _intentosCaducidad;

		/// <summary>Si un paso lo pone a true, se repite en la siguiente tanda en vez de avanzar.
		/// Lo usa la comprobacion de caducidad de buffs, que necesita que el JUEGO avance ticks
		/// (no solo fotogramas de interfaz) y no puede darlo por hecho.</summary>
		private static bool _repetirPaso;

		/// <summary>Se llama en cada <c>UpdateUI</c>. No hace nada si la variable no esta puesta.</summary>
		public static void Actualizar()
		{
			if (!_comprobada) {
				_comprobada = true;
				_activa = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable));
				if (_activa) {
					Registrar("AUTOPRUEBA WS1: variable " + Variable + " detectada.");
				}
			}

			if (!_activa || _terminada) {
				return;
			}

			// Espera a que el panel este abierto de verdad (lo abre la autoprueba de WS0).
			if (!PanelPruebaSystem.PanelAbierto || PanelPruebaSystem.PanelActual == null) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}

			_espera = FotogramasEntrePasos;

			_repetirPaso = false;

			try {
				EjecutarPaso(_paso);
			}
			catch (Exception excepcion) {
				Registrar("AUTOPRUEBA WS1: EXCEPCION en el paso " + _paso + ": " + excepcion);
				_terminada = true;
				return;
			}

			if (!_repetirPaso) {
				_paso++;
			}
		}

		private static void EjecutarPaso(int paso)
		{
			switch (paso) {
				case 0: PoblarInventario(); break;
				case 1: PoblarAlmacenes(); break;
				case 2: PoblarEquipo(); break;
				case 3: ComprobarDinero(); break;
				case 4: CogerObjetoConElRaton(); break;
				case 5: ComprobarRanuraVanilla(); break;
				case 6: ComprobarLoadouts(); break;
				case 7: AplicarBuffs(); break;
				case 8: ComprobarCaducidadBuffs(); break;
				case 9: QuitarUnBuff(); break;
				case 10: ComprobarApariencia(); break;
				case 11: ComprobarDesbloqueos(); break;
				case 12: AbrirPestana(0); break;
				case 13: MedirPestanaYAbrir(0, 1); break;
				case 14: MedirPestanaYAbrir(1, 2); break;
				case 15: MedirPestanaYAbrir(2, 3); break;
				case 16: MedirPestanaYAbrir(3, 4); break;
				case 17: MedirPestanaYAbrir(4, 5); break;
				// Tras medir la ultima pestaña se vuelve a Apariencia (indice 4) para poder
				// accionar de verdad un deslizador de color.
				case 18: MedirPestanaYAbrir(5, 4); break;
				case 19: ComprobarDeslizadorColor(); break;
				case 20: EnfocarCampoDeTexto(); break;
				case 21: ComprobarCampoDeTexto(); break;
				// El cierre con un objeto cogido en el raton (antiguo paso 22) pasa al FINAL: cierra
				// el panel de verdad, y los pasos nuevos necesitan el panel abierto para navegar
				// pestañas y encontrar sus propios controles.
				//
				// Rediseño de Personaje (encargo explicito del usuario, ver bitacora.md): la fila
				// compartida de papelera + editor de cantidad que vivia debajo de la barra de
				// pestañas de ContenidoPersonaje YA NO EXISTE - cada pestaña que enseña objetos
				// reales (Inventario/Almacenes/Equipo) lleva ahora su PROPIO mini-panel completo
				// (PanelHerramientasLibreriaTk, el MISMO de Libreria: recuadro de arrastre + cantidad
				// + prefijo + papelera) en el hueco libre de abajo a la derecha. Los pasos 22/23, que
				// antes enganchaban el editor de cantidad por HOVER (ya no existe ese modo aqui), se
				// han reescrito para arrastrar de verdad al recuadro de seleccion - mismo patron que
				// AutopruebaLibreria.ArrastrarAlRecuadroDeSeleccion. El paso 24 es nuevo: prefijo +
				// papelera sobre ese mismo mini-panel, para demostrar que el conjunto entero funciona
				// igual en Personaje que en Libreria (pedido explicito de la verificacion).
				case 22: PrepararArrastreParaEditorCantidad(); break;
				case 23: ComprobarEditorCantidad(); break;
				case 24: IntercambiarYAbrirPrefijoEnPersonaje(); break;
				// Separado del paso 24 a proposito (mismo motivo que AutopruebaLibreria, ver su
				// comentario): CapturaDePantalla.Guardar captura el fotograma YA PRESENTADO, asi que
				// capturar en el MISMO paso que abre el popup lo enseñaria todavia cerrado.
				case 25: CapturarPulsarYPapeleraEnPersonaje(); break;
				case 26: ComprobarPapeleraQuitaDeUnHueco(); break;
				case 27: ComprobarCierreConObjetoEnElRaton(); break;
				default:
					Registrar("AUTOPRUEBA WS1 COMPLETA. Todos los pasos ejecutados sin excepciones.");
					_terminada = true;
					break;
			}
		}

		// ---------------------------------------------------------------- poblar

		/// <summary>
		/// Llena el inventario del personaje de prueba con objetos REALES.
		/// <para />
		/// Se hace desde dentro del juego y no generando un <c>.plr</c> con
		/// <c>Terrakeep.Core</c> a proposito: lo que WS1 tiene que demostrar es que se puede
		/// escribir en vivo sobre <c>Main.LocalPlayer</c>, asi que poblar el inventario ES YA una
		/// de las cosas que se estan probando. Ademas los objetos se buscan por sus propiedades
		/// (<c>headSlot</c>, <c>accessory</c>, <c>dye</c>...) en
		/// <c>ContentSamples.ItemsByType</c> en vez de por ids fijos, asi que la prueba no se
		/// rompe si cambian los ids ni depende de que Calamity este cargado.
		/// </summary>
		private static void PoblarInventario()
		{
			Player jugador = Main.LocalPlayer;

			int espada = BuscarObjeto(objeto => objeto.damage > 0 && objeto.useStyle > 0 && objeto.maxStack == 1);
			int bloque = BuscarObjeto(objeto => objeto.createTile >= 0 && objeto.maxStack >= 99);
			int pocion = BuscarObjeto(objeto => objeto.buffType > 0 && objeto.consumable && objeto.maxStack >= 20);

			PonerObjeto(jugador.inventory, 0, espada, 1);
			PonerObjeto(jugador.inventory, 1, bloque, 250);
			PonerObjeto(jugador.inventory, 2, pocion, 15);

			PonerObjeto(jugador.inventory, PersonajeVivo.PrimerSlotMonedas + 0, ItemID.CopperCoin, 55);
			PonerObjeto(jugador.inventory, PersonajeVivo.PrimerSlotMonedas + 1, ItemID.SilverCoin, 23);
			PonerObjeto(jugador.inventory, PersonajeVivo.PrimerSlotMonedas + 2, ItemID.GoldCoin, 7);
			PonerObjeto(jugador.inventory, PersonajeVivo.PrimerSlotMonedas + 3, ItemID.PlatinumCoin, 2);

			int flecha = BuscarObjeto(objeto => objeto.ammo > 0 && objeto.consumable);
			PonerObjeto(jugador.inventory, PersonajeVivo.PrimerSlotMunicion, flecha, 300);

			Registrar("Paso 0 - inventario poblado en vivo. "
				+ "inventory[0]=" + PersonajeVivo.DescribirObjeto(jugador.inventory[0])
				+ " | inventory[1]=" + PersonajeVivo.DescribirObjeto(jugador.inventory[1])
				+ " | inventory[2]=" + PersonajeVivo.DescribirObjeto(jugador.inventory[2])
				+ " | monedas 50..53 = "
				+ PersonajeVivo.DescribirObjeto(jugador.inventory[50]) + " / "
				+ PersonajeVivo.DescribirObjeto(jugador.inventory[51]) + " / "
				+ PersonajeVivo.DescribirObjeto(jugador.inventory[52]) + " / "
				+ PersonajeVivo.DescribirObjeto(jugador.inventory[53])
				+ " | municion 54 = " + PersonajeVivo.DescribirObjeto(jugador.inventory[54]));
		}

		private static void PoblarAlmacenes()
		{
			Player jugador = Main.LocalPlayer;
			int barra = BuscarObjeto(objeto => objeto.createTile < 0 && objeto.maxStack >= 99 && objeto.value > 0);

			for (int almacen = 0; almacen < 4; almacen++) {
				Item[] contenido = PersonajeVivo.ObtenerAlmacen(almacen);
				PonerObjeto(contenido, 0, barra, 99);
				PonerObjeto(contenido, 39, ItemID.GoldCoin, 10 * (almacen + 1));
			}

			Registrar("Paso 1 - almacenes poblados en vivo. "
				+ "bank.item[0]=" + PersonajeVivo.DescribirObjeto(jugador.bank.item[0])
				+ " (" + jugador.bank.item.Length + " ranuras) | "
				+ "bank2.item[39]=" + PersonajeVivo.DescribirObjeto(jugador.bank2.item[39]) + " | "
				+ "bank3.item[39]=" + PersonajeVivo.DescribirObjeto(jugador.bank3.item[39]) + " | "
				+ "bank4.item[39]=" + PersonajeVivo.DescribirObjeto(jugador.bank4.item[39]));
		}

		private static void PoblarEquipo()
		{
			Player jugador = Main.LocalPlayer;

			PonerObjeto(jugador.armor, 0, BuscarObjeto(o => o.headSlot >= 0 && o.defense > 0), 1);
			PonerObjeto(jugador.armor, 1, BuscarObjeto(o => o.bodySlot >= 0 && o.defense > 0), 1);
			PonerObjeto(jugador.armor, 2, BuscarObjeto(o => o.legSlot >= 0 && o.defense > 0), 1);
			PonerObjeto(jugador.armor, 3, BuscarObjeto(o => o.accessory), 1);
			PonerObjeto(jugador.dye, 0, BuscarObjeto(o => o.dye > 0), 1);

			PonerObjeto(jugador.miscEquips, 0,
				BuscarObjeto(o => o.buffType > 0 && o.buffType < Main.vanityPet.Length && Main.vanityPet[o.buffType]), 1);
			PonerObjeto(jugador.miscEquips, 1,
				BuscarObjeto(o => o.buffType > 0 && o.buffType < Main.lightPet.Length && Main.lightPet[o.buffType]), 1);
			PonerObjeto(jugador.miscEquips, 2,
				BuscarObjeto(o => o.mountType >= 0 && o.mountType < MountID.Sets.Cart.Length && MountID.Sets.Cart[o.mountType]), 1);
			PonerObjeto(jugador.miscEquips, 3,
				BuscarObjeto(o => o.mountType >= 0 && o.mountType < MountID.Sets.Cart.Length && !MountID.Sets.Cart[o.mountType]), 1);
			PonerObjeto(jugador.miscEquips, 4,
				BuscarObjeto(o => o.shoot > 0 && o.shoot < Main.projHook.Length && Main.projHook[o.shoot]), 1);

			Registrar("Paso 2 - equipo poblado en vivo. "
				+ "armor[0]=" + PersonajeVivo.DescribirObjeto(jugador.armor[0])
				+ " | armor[1]=" + PersonajeVivo.DescribirObjeto(jugador.armor[1])
				+ " | armor[2]=" + PersonajeVivo.DescribirObjeto(jugador.armor[2])
				+ " | armor[3] (accesorio)=" + PersonajeVivo.DescribirObjeto(jugador.armor[3])
				+ " | dye[0]=" + PersonajeVivo.DescribirObjeto(jugador.dye[0])
				+ " | miscEquips: mascota=" + PersonajeVivo.DescribirObjeto(jugador.miscEquips[0])
				+ ", luz=" + PersonajeVivo.DescribirObjeto(jugador.miscEquips[1])
				+ ", vagoneta=" + PersonajeVivo.DescribirObjeto(jugador.miscEquips[2])
				+ ", montura=" + PersonajeVivo.DescribirObjeto(jugador.miscEquips[3])
				+ ", gancho=" + PersonajeVivo.DescribirObjeto(jugador.miscEquips[4]));
		}

		// ---------------------------------------------------------------- comprobaciones

		private static void ComprobarDinero()
		{
			long enInventario;
			long enAlmacenes;
			long total = PersonajeVivo.DineroTotal(out enInventario, out enAlmacenes);

			// 2 plat + 7 oro + 23 plata + 55 cobre = 2*1000000 + 7*10000 + 23*100 + 55
			const long EsperadoInventario = 2L * 1000000L + 7L * 10000L + 23L * 100L + 55L;
			// 10+20+30+40 monedas de oro repartidas por los cuatro almacenes.
			const long EsperadoAlmacenes = 100L * 10000L;

			Registrar("Paso 3 - dinero contado del inventario y los almacenes reales. "
				+ "Inventario=" + enInventario + " cobre (" + PersonajeVivo.FormatearDinero(enInventario) + "), "
				+ "esperado " + EsperadoInventario + " -> " + (enInventario == EsperadoInventario ? "OK" : "DISTINTO") + ". "
				+ "Almacenes=" + enAlmacenes + " cobre (" + PersonajeVivo.FormatearDinero(enAlmacenes) + "), "
				+ "esperado " + EsperadoAlmacenes + " -> " + (enAlmacenes == EsperadoAlmacenes ? "OK" : "DISTINTO") + ". "
				+ "Total=" + PersonajeVivo.FormatearDinero(total) + ".");
		}

		/// <summary>
		/// Ejercita la ruta REAL de vanilla que usan las ranuras del panel:
		/// <c>ItemSlot.LeftClick</c> sobre el array vivo del inventario, con un objeto en el
		/// raton. Es lo mismo que ocurre al hacer clic con el raton en una ranura, sin depender de
		/// que haya sesion de escritorio para simular el clic.
		/// <para />
		/// <c>LeftClick</c> exige <c>Main.mouseLeftRelease &amp;&amp; Main.mouseLeft</c> para
		/// considerar que hay una pulsacion nueva; se ponen y se restauran alrededor de la
		/// llamada.
		/// </summary>
		/// <summary>
		/// Pone un objeto "cogido" con el raton y deja pasar fotogramas antes de soltarlo, para
		/// que el panel llegue a DIBUJARLO. Es la comprobacion de la ruta que hubo que añadir a
		/// mano: con un panel de <c>IngameFancyUI</c> abierto, la capa vanilla que dibuja el
		/// objeto cogido nunca se ejecuta.
		/// </summary>
		private static void CogerObjetoConElRaton()
		{
			int tipoPrueba = BuscarObjeto(o => o.createTile >= 0 && o.maxStack >= 99);
			Item enElRaton = new Item();
			enElRaton.SetDefaults(tipoPrueba);
			enElRaton.stack = 42;
			Main.mouseItem = enElRaton;

			TerrakeepMod.UI.Panel.PanelTerrakeepState.FotogramasObjetoEnRaton = 0;

			Registrar("Paso 4 - objeto puesto en el raton: "
				+ PersonajeVivo.DescribirObjeto(Main.mouseItem)
				+ ". Se deja unos fotogramas para comprobar que el panel lo dibuja.");
		}

		private static void ComprobarRanuraVanilla()
		{
			Player jugador = Main.LocalPlayer;
			const int Ranura = 10;

			int dibujados = TerrakeepMod.UI.Panel.PanelTerrakeepState.FotogramasObjetoEnRaton;

			string antesRanura = PersonajeVivo.DescribirObjeto(jugador.inventory[Ranura]);
			string antesRaton = PersonajeVivo.DescribirObjeto(Main.mouseItem);

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				ItemSlot.LeftClick(jugador.inventory, ItemSlot.Context.InventoryItem, Ranura);
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}

			Registrar("Paso 5 - ItemSlot.LeftClick de vanilla sobre inventory[" + Ranura + "]. "
				+ "El objeto SIGUE en el raton " + FotogramasEntrePasos + " fotogramas despues "
				+ "(prueba de que Player.dropItemCheck ya no lo vacia) y se dibujo en "
				+ dibujados + " fotogramas "
				+ "(0 en cualquiera de los dos significaria que algo de esto no funciona). "
				+ "ANTES ranura=" + antesRanura + ", raton=" + antesRaton + ". "
				+ "DESPUES ranura=" + PersonajeVivo.DescribirObjeto(jugador.inventory[Ranura])
				+ ", raton=" + PersonajeVivo.DescribirObjeto(Main.mouseItem) + ".");

			// La prueba deja el objeto colocado; el raton se limpia para no arrastrar estado.
			Main.mouseItem = new Item();
		}

		private static void ComprobarLoadouts()
		{
			Player jugador = Main.LocalPlayer;

			int inicial = jugador.CurrentLoadoutIndex;
			string cabezaConjunto1 = PersonajeVivo.DescribirObjeto(jugador.armor[0]);

			jugador.TrySwitchingLoadout(1);
			int trasCambio = jugador.CurrentLoadoutIndex;
			string cabezaConjunto2Antes = PersonajeVivo.DescribirObjeto(jugador.armor[0]);

			// Se equipa algo distinto en el segundo conjunto para demostrar que son
			// independientes de verdad.
			PonerObjeto(jugador.armor, 0, BuscarObjeto(o => o.headSlot >= 0 && o.defense > 1
				&& o.type != ObtenerTipo(jugador.armor, 0)), 1);
			string cabezaConjunto2 = PersonajeVivo.DescribirObjeto(jugador.armor[0]);

			jugador.TrySwitchingLoadout(inicial);
			string cabezaVuelta = PersonajeVivo.DescribirObjeto(jugador.armor[0]);

			Registrar("Paso 6 - conjuntos de equipo (Player.TrySwitchingLoadout). "
				+ "Conjunto inicial=" + inicial + " con armor[0]=" + cabezaConjunto1 + ". "
				+ "Tras pedir el 1: CurrentLoadoutIndex=" + trasCambio
				+ ", armor[0]=" + cabezaConjunto2Antes + " (vacio = conjunto distinto, correcto). "
				+ "Equipado en el conjunto 1: " + cabezaConjunto2 + ". "
				+ "Al volver al " + inicial + ": armor[0]=" + cabezaVuelta
				+ " -> " + (cabezaVuelta == cabezaConjunto1 ? "OK, cada conjunto conserva lo suyo" : "DISTINTO") + ". "
				+ "Total de conjuntos: " + jugador.Loadouts.Length + ".");
		}

		private static void AplicarBuffs()
		{
			Player jugador = Main.LocalPlayer;
			List<int> aplicados = new List<int>();

			int puestos = 0;
			for (int tipo = 1; tipo < BuffLoader.BuffCount && puestos < 3; tipo++) {
				if (string.IsNullOrEmpty(PersonajeVivo.NombreBuff(tipo))) {
					continue;
				}
				if (tipo < jugador.buffImmune.Length && jugador.buffImmune[tipo]) {
					continue;
				}

				jugador.AddBuff(tipo, 3600);
				if (jugador.FindBuffIndex(tipo) >= 0) {
					aplicados.Add(tipo);
					puestos++;
				}
			}

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach (int tipo in aplicados) {
				int indice = jugador.FindBuffIndex(tipo);
				sb.Append(" | \"").Append(PersonajeVivo.NombreBuff(tipo)).Append("\" id=").Append(tipo)
					.Append(" en buffType[").Append(indice).Append("]=").Append(jugador.buffType[indice])
					.Append(" buffTime=").Append(jugador.buffTime[indice]);
			}

			if (aplicados.Count > 0) {
				_buffVigilado = aplicados[0];
				_tiempoBuffInicial = jugador.buffTime[jugador.FindBuffIndex(_buffVigilado)];
				_tickAlAplicarBuff = Main.GameUpdateCount;
			}

			Registrar("Paso 7 - buffs aplicados con Player.AddBuff (60 s cada uno). "
				+ "Ranuras de buff reales: " + jugador.buffType.Length
				+ ", CountBuffs=" + jugador.CountBuffs() + "." + sb);
		}

		/// <summary>
		/// Demuestra el riesgo real que señalaba el plan: los buffs caducan solos. Se vuelve a
		/// leer el tiempo del mismo buff unos fotogramas despues y tiene que haber BAJADO, que es
		/// justo el motivo por el que la pestaña de buffs se refresca sola en vez de leer el
		/// estado solo al abrirse.
		/// </summary>
		private static void ComprobarCaducidadBuffs()
		{
			Player jugador = Main.LocalPlayer;

			if (_buffVigilado <= 0) {
				Registrar("Paso 8 - no habia ningun buff que vigilar, se salta.");
				return;
			}

			int indice = jugador.FindBuffIndex(_buffVigilado);
			int ahora = indice >= 0 ? jugador.buffTime[indice] : -1;
			uint ticksTranscurridos = Main.GameUpdateCount - _tickAlAplicarBuff;

			// Los buffs solo bajan si el JUEGO avanza, no si avanza la interfaz: en un jugador
			// Terraria congela la partida cuando la ventana pierde el foco (Main.hasFocus =
			// IsActive), y este arnes se lanza desde una consola que puede robarselo. Por eso se
			// espera a ver ticks reales de partida en vez de dar por hecho que han pasado.
			if (ticksTranscurridos < 60 && _intentosCaducidad < 60) {
				_intentosCaducidad++;
				_repetirPaso = true;
				return;
			}

			Registrar("Paso 8 - caducidad en vivo del buff \"" + PersonajeVivo.NombreBuff(_buffVigilado)
				+ "\" (id " + _buffVigilado + "): buffTime al aplicarlo=" + _tiempoBuffInicial
				+ ", ahora=" + ahora + " (" + PersonajeVivo.FormatearTiempoBuff(ahora) + "). "
				+ "Ticks de partida transcurridos=" + ticksTranscurridos
				+ ", Main.hasFocus=" + Main.hasFocus + ", Main.gamePaused=" + Main.gamePaused + ". "
				+ (ahora < _tiempoBuffInicial
					? "OK: baja solo (" + (_tiempoBuffInicial - ahora) + " ticks menos), "
						+ "por eso la pestaña de buffs TIENE que refrescarse sola."
					: "NO ha bajado; con " + ticksTranscurridos + " ticks de partida eso solo se "
						+ "explica si la partida esta congelada."));
		}

		private static void QuitarUnBuff()
		{
			Player jugador = Main.LocalPlayer;

			if (_buffVigilado <= 0) {
				Registrar("Paso 9 - no habia ningun buff que quitar, se salta.");
				return;
			}

			int antes = jugador.CountBuffs();
			int indiceAntes = jugador.FindBuffIndex(_buffVigilado);
			jugador.ClearBuff(_buffVigilado);

			Registrar("Paso 9 - buff quitado con Player.ClearBuff: \""
				+ PersonajeVivo.NombreBuff(_buffVigilado) + "\" (id " + _buffVigilado + "). "
				+ "Estaba en buffType[" + indiceAntes + "]. "
				+ "CountBuffs antes=" + antes + ", despues=" + jugador.CountBuffs()
				+ ", FindBuffIndex despues=" + jugador.FindBuffIndex(_buffVigilado) + ".");
		}

		private static void ComprobarApariencia()
		{
			Player jugador = Main.LocalPlayer;

			int peloAntes = jugador.hair;
			int varianteAntes = jugador.skinVariant;
			int tinteAntes = jugador.hairDye;
			Color pelosColorAntes = jugador.hairColor;
			Color ojosAntes = jugador.eyeColor;

			jugador.hair = (jugador.hair + 7) % PersonajeVivo.TotalPeinados;
			jugador.skinVariant = (jugador.skinVariant + 1) % PlayerVariantID.Count;

			List<int> sombreadores;
			List<string> nombres;
			PersonajeVivo.CargarTintesPelo(out sombreadores, out nombres);
			if (sombreadores.Count > 1) {
				jugador.hairDye = sombreadores[1];
			}

			jugador.hairColor = new Color(200, 40, 90);
			jugador.eyeColor = new Color(20, 220, 180);
			jugador.skinColor = new Color(230, 190, 150);
			jugador.shirtColor = new Color(40, 60, 200);
			jugador.underShirtColor = new Color(180, 180, 40);
			jugador.pantsColor = new Color(60, 60, 60);
			jugador.shoeColor = new Color(120, 70, 30);

			Registrar("Paso 10 - apariencia cambiada en vivo. "
				+ "hair " + peloAntes + " -> " + jugador.hair + " (de " + PersonajeVivo.TotalPeinados + " peinados). "
				+ "skinVariant " + varianteAntes + " -> " + jugador.skinVariant
				+ " (\"" + PersonajeVivo.NombreVariante(jugador.skinVariant) + "\", de "
				+ PlayerVariantID.Count + " variantes). "
				+ "hairDye " + tinteAntes + " -> " + jugador.hairDye
				+ " (" + sombreadores.Count + " tintes encontrados). "
				+ "hairColor " + Describir(pelosColorAntes) + " -> " + Describir(jugador.hairColor) + ". "
				+ "eyeColor " + Describir(ojosAntes) + " -> " + Describir(jugador.eyeColor) + ".");
		}

		private static void ComprobarDesbloqueos()
		{
			Player jugador = Main.LocalPlayer;
			List<Desbloqueo> lista = Desbloqueos.Lista;

			System.Text.StringBuilder antes = new System.Text.StringBuilder();
			System.Text.StringBuilder despues = new System.Text.StringBuilder();
			int coinciden = 0;

			foreach (Desbloqueo desbloqueo in lista) {
				antes.Append(desbloqueo.CampoReal).Append('=').Append(desbloqueo.Leer(jugador)).Append(' ');
			}

			foreach (Desbloqueo desbloqueo in lista) {
				desbloqueo.Escribir(jugador, true);
			}

			foreach (Desbloqueo desbloqueo in lista) {
				bool valor = desbloqueo.Leer(jugador);
				despues.Append(desbloqueo.CampoReal).Append('=').Append(valor).Append(' ');
				if (valor) {
					coinciden++;
				}
			}

			Registrar("Paso 11 - desbloqueos escritos en vivo (" + lista.Count + " en total). "
				+ "ANTES: " + antes.ToString().Trim() + ". "
				+ "DESPUES de activarlos todos: " + despues.ToString().Trim() + ". "
				+ "Leidos como activos: " + coinciden + " de " + lista.Count + ". "
				+ "extraAccessorySlots que calcula el juego: " + jugador.extraAccessorySlots
				+ " (0 fuera de modo Experto, es lo correcto).");
		}

		private static void AbrirPestana(int indice)
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				return;
			}

			panel.IrAPestana(indice);
			Registrar("Paso 12 - abierta la pestaña " + indice + " de " + panel.TotalPestanas
				+ ": \"" + panel.NombrePestanaActual + "\". Se mide en el paso siguiente, ya dibujada.");
		}

		/// <summary>
		/// Mide la pestaña que se abrio en el paso anterior (ya recalculada y dibujada durante
		/// varios fotogramas) y abre la siguiente. Asi cada pestaña se mide con geometria real de
		/// pantalla, no recien construida.
		/// </summary>
		private static void MedirPestanaYAbrir(int medida, int siguiente)
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				return;
			}

			Registrar("Pestaña \"" + panel.NombrePestanaActual + "\" dibujada: " + panel.InformePestanaActual());

			// Captura real de las tres pestañas rediseñadas (Inventario/Almacenes/Equipo, indices
			// 0-2): el mini-panel de edicion nuevo (PanelHerramientasLibreriaTk) tiene que verse de
			// verdad en el hueco de abajo a la derecha de cada una, sin pisar ninguna otra etiqueta -
			// pedido explicito de la verificacion ("el mini-panel consolidado funciona igual en
			// Personaje que en Libreria"). Las otras tres pestañas (Buffs/Apariencia/Desbloqueos) no
			// se tocaron en esta tarea, no hace falta capturarlas aqui.
			if (medida >= 0 && medida <= 2) {
				string[] nombres = { "inventario", "almacenes", "equipo" };
				Registrar("Pestaña \"" + panel.NombrePestanaActual + "\" - "
					+ CapturaDePantalla.Guardar("ws1-personaje-" + nombres[medida]));
			}

			if (siguiente >= 0) {
				panel.IrAPestana(siguiente);
			}
		}

		/// <summary>
		/// Acciona de VERDAD un deslizador de color: se coloca el raton de la interfaz al 25% de
		/// su ancho y se le manda un <c>LeftMouseDown</c>, que es exactamente lo que hace
		/// <c>UserInterface</c> con un clic real. El color del pelo del jugador tiene que quedar
		/// con el rojo a ~64 (25% de 255).
		/// </summary>
		private static void ComprobarDeslizadorColor()
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				return;
			}

			DeslizadorTk deslizador = panel.BuscarPrimero<DeslizadorTk>();
			if (deslizador == null) {
				Registrar("Paso 19 - no se encontro ningun deslizador en la pestaña abierta ("
					+ panel.NombrePestanaActual + ").");
				return;
			}

			Color antes = Main.LocalPlayer.hairColor;
			CalculatedStyle dim = deslizador.GetDimensions();
			Vector2 destino = new Vector2(dim.X + dim.Width * 0.25f, dim.Y + dim.Height * 0.5f);

			Vector2 ratonPrevio = Main.InGameUI.MousePosition;
			Main.InGameUI.MousePosition = destino;
			try {
				deslizador.LeftMouseDown(new UIMouseEvent(deslizador, destino));
			}
			finally {
				Main.InGameUI.MousePosition = ratonPrevio;
			}

			Color despues = Main.LocalPlayer.hairColor;
			Registrar("Paso 19 - deslizador de color accionado por su ruta real (LeftMouseDown) en "
				+ "la pestaña \"" + panel.NombrePestanaActual + "\". Deslizador en x=" + (int)dim.X
				+ " y=" + (int)dim.Y + " " + (int)dim.Width + "x" + (int)dim.Height
				+ ", clic al 25% (x=" + (int)destino.X + "). "
				+ "hairColor " + Describir(antes) + " -> " + Describir(despues)
				+ ", FillPercent=" + deslizador.FillPercent.ToString("0.000") + ". "
				+ (despues.R >= 60 && despues.R <= 68 ? "OK: el canal rojo ha ido al 25%." : "VALOR INESPERADO."));
		}

		private static void EnfocarCampoDeTexto()
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				return;
			}

			CampoTextoTk campo = panel.BuscarPrimero<CampoTextoTk>();
			if (campo == null) {
				Registrar("Paso 20 - no se encontro ningun campo de texto en el panel.");
				return;
			}

			CampoTextoTk.FotogramasCapturandoTeclado = 0;
			CalculatedStyle dim = campo.GetDimensions();
			campo.LeftClick(new UIMouseEvent(campo,
				new Vector2(dim.X + dim.Width * 0.5f, dim.Y + dim.Height * 0.5f)));

			Registrar("Paso 20 - campo de texto de la cabecera enfocado con su ruta real "
				+ "(LeftClick). Enfocado=" + campo.Enfocado + ", texto actual=\"" + campo.Texto + "\", "
				+ "rectangulo x=" + (int)dim.X + " y=" + (int)dim.Y + " "
				+ (int)dim.Width + "x" + (int)dim.Height + ".");
		}

		private static void ComprobarCampoDeTexto()
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				return;
			}

			CampoTextoTk campo = panel.BuscarPrimero<CampoTextoTk>();
			int capturados = CampoTextoTk.FotogramasCapturandoTeclado;

			// Es la MISMA linea que ejecuta el manejador AlCambiar del campo cuando alguien
			// escribe; aqui se dispara a mano porque simular pulsaciones de teclado reales exige
			// que la ventana tenga el foco de escritorio y eso no siempre se puede garantizar.
			string nombreAntes = Main.LocalPlayer.name;
			Main.LocalPlayer.name = "TerrakeepRenombrado";

			Registrar("Paso 21 - el campo de texto ha capturado el teclado en " + capturados
				+ " fotogramas (PlayerInput.WritingText a true; mientras lo esta, KeyboardInput() "
				+ "vacia las teclas y escribir una \"k\" NO cierra el panel). "
				+ "Player.name en vivo: \"" + nombreAntes + "\" -> \"" + Main.LocalPlayer.name + "\". "
				+ (campo != null ? "Enfocado=" + campo.Enfocado + "." : ""));

			if (campo != null) {
				// El texto se pone al dia ANTES de soltar el foco: Desenfocar dispara AlConfirmar,
				// que registra lo que tenga el campo, y si no coincidiera con Player.name la linea
				// del log se contradiria con la de arriba.
				campo.FijarTextoSilencioso(Main.LocalPlayer.name);
				campo.Desenfocar();
			}
		}

		/// <summary>
		/// Arrastra <c>inventory[2]</c> (la pocion x15 que puso <see cref="PoblarInventario"/>) al
		/// recuadro de seleccion del mini-panel NUEVO de la pestaña Inventario
		/// (<see cref="PanelHerramientasLibreriaTk"/>, el mismo de Libreria), por la ruta REAL de
		/// arrastre: <c>ItemSlot.LeftClick</c> para cogerlo del hueco (igual que un arrastre real) y
		/// <c>SlotSeleccionTk.EjercitarHandle</c> para soltarlo en el recuadro (el MISMO
		/// <c>ItemSlot.Handle</c> que dispara <c>DrawSelf</c> con el raton encima) - mismo patron
		/// que <c>AutopruebaLibreria.ArrastrarAlRecuadroDeSeleccion</c>.
		/// <para />
		/// <b>Ya NO es por hover.</b> La fila compartida de papelera + editor de cantidad que vivia
		/// debajo de la barra de pestañas (y que este paso enganchaba con <c>SlotObjetoVanilla
		/// .MouseOver</c>) se ha quitado del todo: encargo explicito del usuario tras probar el mod
		/// ("quitaria la papelera y lo de editar stacks de la linea de arriba... a ponerlo todo
		/// junto abajo a la derecha", igual que en Libreria). Se usa <c>inventory[2]</c> y no
		/// <c>inventory[1]</c> a proposito: <c>inventory[1]</c> lo necesita intacto el paso 25
		/// (papelera quitando de un hueco), que es una comprobacion aparte, independiente de este
		/// mini-panel.
		/// </summary>
		private static void PrepararArrastreParaEditorCantidad()
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				Registrar("Paso 22 - no hay panel de Personaje montado.");
				return;
			}

			panel.IrAPestana(0); // Inventario

			PanelHerramientasLibreriaTk herramientas = panel.BuscarPrimero<PanelHerramientasLibreriaTk>();
			if (herramientas == null) {
				Registrar("Paso 22 - no se encontro el mini-panel de edicion (PanelHerramientasLibreriaTk) "
					+ "en la pestaña Inventario.");
				return;
			}

			Item[] inventario = PersonajeVivo.Jugador.inventory;
			string antes = PersonajeVivo.DescribirObjeto(inventario[2]);

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				ItemSlot.LeftClick(inventario, ItemSlot.Context.InventoryItem, 2);
				herramientas.Seleccion.EjercitarHandle();
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}

			Registrar("Paso 22 - arrastre real (no hover) al recuadro de seleccion del mini-panel de "
				+ "Inventario. inventory[2] ANTES=" + antes + ". Tras ItemSlot.LeftClick + "
				+ "SlotSeleccionTk.EjercitarHandle: hueco de origen=" + PersonajeVivo.DescribirObjeto(inventario[2])
				+ ", recuadro=" + PersonajeVivo.DescribirObjeto(herramientas.Seleccion.ObjetoActual) + ", raton="
				+ PersonajeVivo.DescribirObjeto(Main.mouseItem) + ".");
		}

		/// <summary>
		/// Comprueba el editor de cantidad COMPACTO (modo explicito, enganchado al recuadro de
		/// seleccion del paso anterior) pulsando sus botones REALES (<c>BotonTk.LeftClick</c>).
		/// Mismas cuatro comprobaciones que hacia el modo hover antiguo, sobre el mismo mini-panel
		/// que ya verifico a fondo <c>AutopruebaLibreria.ComprobarEditorCantidadCompacto</c>.
		/// </summary>
		private static void ComprobarEditorCantidad()
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				Registrar("Paso 23 - no hay panel de Personaje montado.");
				return;
			}

			PanelHerramientasLibreriaTk herramientas = panel.BuscarPrimero<PanelHerramientasLibreriaTk>();
			if (herramientas == null || herramientas.Seleccion.ObjetoActual.IsAir) {
				Registrar("Paso 23 - no hay nada seleccionado en el recuadro (paso 22 fallo), se salta.");
				return;
			}

			EditorCantidadTk editor = herramientas.EditorCantidad;
			Item objetivo = editor.ObjetivoActual;
			bool mismaReferencia = ReferenceEquals(objetivo, herramientas.Seleccion.ObjetoActual);

			int stackInicial = objetivo.stack;
			int maxStack = objetivo.maxStack;

			Clic(editor.BotonMas);
			int trasMas = objetivo.stack;

			Clic(editor.BotonMenos);
			int trasMenos = objetivo.stack;

			editor.Campo.FijarTextoSilencioso("7");
			Clic(editor.BotonAplicar);
			int trasAplicarSiete = objetivo.stack;

			editor.Campo.FijarTextoSilencioso((maxStack + 500).ToString());
			Clic(editor.BotonAplicar);
			int trasPedirDeMas = objetivo.stack;

			bool okMas = trasMas == stackInicial + 1;
			bool okMenos = trasMenos == trasMas - 1;
			bool okSiete = trasAplicarSiete == 7;
			bool okAcotado = trasPedirDeMas == maxStack;

			Registrar("Paso 23 - editor de cantidad COMPACTO (explicito, sin hover) sobre \"" + objetivo.Name
				+ "\" (maxStack=" + maxStack + "). ObjetivoActual es "
				+ (mismaReferencia ? "OK, la MISMA referencia que SlotSeleccionTk.ObjetoActual"
					: "FALLO, referencia distinta") + ". stack inicial=" + stackInicial + ". "
				+ "Tras pulsar \"+\": " + trasMas + " (" + (okMas ? "OK" : "FALLO") + "). "
				+ "Tras pulsar \"-\": " + trasMenos + " (" + (okMenos ? "OK" : "FALLO") + "). "
				+ "Tras escribir \"7\" y Aplicar: " + trasAplicarSiete + " (" + (okSiete ? "OK" : "FALLO") + "). "
				+ "Tras pedir " + (maxStack + 500) + " (por encima del maximo real) y Aplicar: "
				+ trasPedirDeMas + " (" + (okAcotado ? "OK, acotado al maximo real" : "FALLO") + "). "
				+ "Historial tras los cambios: " + Historial.Pila.EtiquetaDeshacer + ".");
		}

		/// <summary>
		/// Cierra la verificacion del mini-panel de Personaje: cambia el prefijo de un segundo
		/// objeto de prueba (arrastrado ENCIMA del recuadro ya ocupado, intercambio real de
		/// <c>ItemSlot.Handle</c>) y termina soltandolo en la papelera del propio mini-panel - las
		/// dos piezas que pedia la verificacion obligatoria ("cantidad y prefijo editables, papelera
		/// vacia sin tirar nada al suelo") sobre el MISMO mini-panel ya reutilizado, esta vez en
		/// Personaje en vez de en Libreria. La logica de "no acumula multiplicadores" del prefijo ya
		/// esta verificada a fondo en <c>AutopruebaLibreria.ComprobarEditorPrefijo</c> (mismo
		/// <see cref="EditorPrefijoTk"/> reutilizado tal cual); aqui basta con demostrar que abre con
		/// opciones reales y que aplica el cambio, sin repetir esa prueba entera.
		/// </summary>
		private static void IntercambiarYAbrirPrefijoEnPersonaje()
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				Registrar("Paso 24 - no hay panel de Personaje montado.");
				return;
			}

			PanelHerramientasLibreriaTk herramientas = panel.BuscarPrimero<PanelHerramientasLibreriaTk>();
			if (herramientas == null) {
				Registrar("Paso 24 - no se encontro el mini-panel de edicion.");
				return;
			}

			int tipoPrefijo = BuscarObjeto(o => o.maxStack == 1 && CatalogoPrefijosLegales.GruposLegales(o).Any());
			if (tipoPrefijo <= 0) {
				Registrar("Paso 24 - no se encontro ningun objeto de prueba con prefijo legal, se salta.");
				return;
			}

			Item[] inventario = PersonajeVivo.Jugador.inventory;
			const int RanuraPrefijo = 3; // libre: PoblarInventario solo usa 0,1,2,50-53,54.
			Item objetoPrefijo = new Item();
			objetoPrefijo.SetDefaults(tipoPrefijo);
			inventario[RanuraPrefijo] = objetoPrefijo;

			string antesRecuadro = PersonajeVivo.DescribirObjeto(herramientas.Seleccion.ObjetoActual);

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				// Intercambio real: el objeto con prefijo se arrastra ENCIMA del recuadro ya
				// ocupado por la pocion del paso 22 - ItemSlot.Handle intercambia de verdad, la
				// pocion vuelve al raton.
				ItemSlot.LeftClick(inventario, ItemSlot.Context.InventoryItem, RanuraPrefijo);
				herramientas.Seleccion.EjercitarHandle();

				bool esElDePrefijo = !herramientas.Seleccion.ObjetoActual.IsAir
					&& herramientas.Seleccion.ObjetoActual.type == tipoPrefijo;

				// La pocion desplazada se descarta en la papelera para dejar el raton limpio antes
				// de seguir (ruta real del juego, no basura tirada al suelo).
				string ratonTrasIntercambio = PersonajeVivo.DescribirObjeto(Main.mouseItem);
				ItemSlot.Handle(ref PersonajeVivo.Jugador.trashItem, ItemSlot.Context.TrashItem);
				PersonajeVivo.Jugador.trashItem = new Item();

				Registrar("Paso 24 - intercambio real en el recuadro (mismo mini-panel que Libreria, ahora "
					+ "en Personaje). ANTES=" + antesRecuadro + ". Objeto con prefijo arrastrado encima: "
					+ "recuadro AHORA=" + PersonajeVivo.DescribirObjeto(herramientas.Seleccion.ObjetoActual)
					+ " (" + (esElDePrefijo ? "OK" : "FALLO") + "), raton tras el intercambio="
					+ ratonTrasIntercambio + " (la pocion desplazada, descartada en la papelera para seguir "
					+ "limpio).");

				if (!esElDePrefijo) {
					return;
				}

				// El popup se ABRE aqui, pero la captura y los clics (paso 25) esperan a la SIGUIENTE
				// reentrada de la autoprueba (~10 fotogramas reales despues): CapturaDePantalla
				// captura el fotograma YA PRESENTADO, asi que capturar en el MISMO paso que abre el
				// popup enseñaria el popup todavia CERRADO - mismo bug real que ya se encontro y
				// arreglo en AutopruebaLibreria.ComprobarEditorPrefijo/CapturarYPulsarPrefijo.
				herramientas.EditorPrefijo.AbrirParaAutoprueba();
				Registrar("Paso 24 - popup de prefijo abierto sobre \"" + herramientas.Seleccion.ObjetoActual.Name
					+ "\" (PopupAbierto=" + herramientas.EditorPrefijo.PopupAbierto + "). La captura y los "
					+ "clics se hacen en el paso siguiente.");
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}
		}

		private static void CapturarPulsarYPapeleraEnPersonaje()
		{
			ContenidoPersonaje panel = PanelPruebaSystem.PanelActual;
			if (panel == null) {
				Registrar("Paso 25 - no hay panel de Personaje montado.");
				return;
			}

			PanelHerramientasLibreriaTk herramientas = panel.BuscarPrimero<PanelHerramientasLibreriaTk>();
			if (herramientas == null || herramientas.Seleccion.ObjetoActual.IsAir) {
				Registrar("Paso 25 - no hay nada seleccionado en el recuadro (paso 24 fallo), se salta.");
				return;
			}

			EditorPrefijoTk editorPrefijo = herramientas.EditorPrefijo;
			Item objetivo = herramientas.Seleccion.ObjetoActual;
			int prefijoAntes = objetivo.prefix;
			int objetosActivosAntes = ContarObjetosEnElMundo();

			if (!editorPrefijo.PopupAbierto) {
				editorPrefijo.AbrirParaAutoprueba();
			}
			List<BotonTk> botones = editorPrefijo.BotonesPopupParaAutoprueba();

			Registrar("Paso 25 - " + CapturaDePantalla.Guardar("ws1-personaje-prefijo-abierto"));

			if (botones.Count < 2) {
				Registrar("Paso 25 - el popup de prefijo se abrio (" + editorPrefijo.PopupAbierto
					+ ") pero solo trae " + botones.Count + " boton(es) (se esperaban al menos 2: "
					+ "\"Ninguno\" + un prefijo real). Objeto=\"" + objetivo.Name + "\".");
			}
			else {
				string nombrePedido = botones[1].Texto;
				Clic(botones[1]);
				int prefijoDespues = objetivo.prefix;

				Registrar("Paso 25 - editor de prefijo (mismo EditorPrefijoTk que Libreria, popup "
					+ "abierto con " + botones.Count + " botones reales incluido \"Ninguno\") sobre \""
					+ objetivo.Name + "\". Boton pulsado: \"" + nombrePedido + "\". prefix ANTES="
					+ prefijoAntes + ", DESPUES=" + prefijoDespues + " ("
					+ (prefijoDespues != prefijoAntes ? "OK, cambio de verdad" : "FALLO, no cambio") + ").");
			}

			// Papelera del propio mini-panel: se coge del recuadro y se suelta en la papelera.
			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				herramientas.Seleccion.EjercitarHandle();
				string ratonTrasCoger = PersonajeVivo.DescribirObjeto(Main.mouseItem);

				ItemSlot.Handle(ref PersonajeVivo.Jugador.trashItem, ItemSlot.Context.TrashItem);

				int objetosActivosDespues = ContarObjetosEnElMundo();
				bool recuadroVacio = herramientas.Seleccion.ObjetoActual.IsAir;
				bool enPapelera = !PersonajeVivo.Jugador.trashItem.IsAir;
				bool manoVacia = Main.mouseItem.IsAir;
				bool nadaEnElSuelo = objetosActivosDespues == objetosActivosAntes;

				Registrar("Paso 25 - papelera del mini-panel de Personaje (SlotPapeleraTk, misma clase que "
					+ "Libreria). Tras cogerlo del recuadro: raton=" + ratonTrasCoger + ". Tras soltarlo en "
					+ "la papelera: recuadro=" + PersonajeVivo.DescribirObjeto(herramientas.Seleccion.ObjetoActual)
					+ " (" + (recuadroVacio ? "OK, vacio" : "FALLO") + "), papelera="
					+ PersonajeVivo.DescribirObjeto(PersonajeVivo.Jugador.trashItem) + " ("
					+ (enPapelera ? "OK" : "FALLO") + "), raton=" + PersonajeVivo.DescribirObjeto(Main.mouseItem)
					+ " (" + (manoVacia ? "OK" : "FALLO") + "). Objetos activos en el mundo: antes="
					+ objetosActivosAntes + ", despues=" + objetosActivosDespues + " ("
					+ (nadaEnElSuelo ? "OK, no ha aparecido nada tirado en el suelo" : "FALLO") + ").");
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}

			// Se deja limpio para no dejar basura visible el resto de la prueba.
			PersonajeVivo.Jugador.trashItem = new Item();
		}

		/// <summary>
		/// Verificacion de la papelera pedida explicitamente por el usuario: coge un objeto de un
		/// hueco REAL (<c>Player.inventory[1]</c>) por la ruta real de vanilla
		/// (<c>ItemSlot.LeftClick</c>, la misma que ejecuta cualquier arrastre) y lo suelta sobre
		/// la papelera (<c>ItemSlot.Handle</c> con <c>Context.TrashItem</c> = 6 sobre
		/// <c>Player.trashItem</c>, la MISMA ruta que ejecuta <see cref="SlotPapeleraTk.DrawSelf"/>
		/// al arrastrar un objeto encima). Comprueba las tres cosas que pedia el encargo: el hueco
		/// de origen queda vacio en el <see cref="Player"/> REAL, el objeto no se queda "perdido"
		/// en el raton, y sobre todo que NO aparece nada tirado en el suelo del mundo.
		/// </summary>
		private static void ComprobarPapeleraQuitaDeUnHueco()
		{
			Player jugador = Main.LocalPlayer;
			const int Ranura = 1;

			string antesRanura = PersonajeVivo.DescribirObjeto(jugador.inventory[Ranura]);
			int objetosActivosAntes = ContarObjetosEnElMundo();

			bool izquierdoPrevio = Main.mouseLeft;
			bool sueltoPrevio = Main.mouseLeftRelease;
			Main.mouseLeft = true;
			Main.mouseLeftRelease = true;
			try {
				// 1) Coger el objeto del hueco con la ruta REAL de vanilla (lo mismo que arrastrarlo).
				ItemSlot.LeftClick(jugador.inventory, ItemSlot.Context.InventoryItem, Ranura);
				string trasCoger = PersonajeVivo.DescribirObjeto(jugador.inventory[Ranura]);
				string ratonTrasCoger = PersonajeVivo.DescribirObjeto(Main.mouseItem);

				// 2) Soltarlo sobre la papelera REAL del juego (Player.trashItem, Context.TrashItem).
				ItemSlot.Handle(ref jugador.trashItem, ItemSlot.Context.TrashItem);

				int objetosActivosDespues = ContarObjetosEnElMundo();

				bool huecoVacio = jugador.inventory[Ranura].IsAir;
				bool enPapelera = !jugador.trashItem.IsAir;
				bool manoVacia = Main.mouseItem.IsAir;
				bool nadaEnElSuelo = objetosActivosDespues == objetosActivosAntes;

				Registrar("Paso 26 - papelera quitando un objeto REAL de un hueco (Player.inventory["
					+ Ranura + "]): ItemSlot.LeftClick para cogerlo + ItemSlot.Handle con "
					+ "Context.TrashItem=6 sobre Player.trashItem para soltarlo (la misma ruta que "
					+ "Main.DrawTrashItemSlot en el HUD de vanilla). "
					+ "ANTES: inventory[" + Ranura + "]=" + antesRanura + ". "
					+ "Tras cogerlo: inventory[" + Ranura + "]=" + trasCoger + ", raton=" + ratonTrasCoger + ". "
					+ "Tras soltarlo en la papelera: inventory[" + Ranura + "]="
					+ PersonajeVivo.DescribirObjeto(jugador.inventory[Ranura])
					+ " (" + (huecoVacio ? "OK, hueco vacio en el Player real" : "FALLO") + "), "
					+ "papelera=" + PersonajeVivo.DescribirObjeto(jugador.trashItem)
					+ " (" + (enPapelera ? "OK" : "FALLO") + "), "
					+ "raton=" + PersonajeVivo.DescribirObjeto(Main.mouseItem)
					+ " (" + (manoVacia ? "OK" : "FALLO") + "). "
					+ "Objetos activos en el mundo: antes=" + objetosActivosAntes + ", despues="
					+ objetosActivosDespues + " (" + (nadaEnElSuelo
						? "OK, no ha aparecido nada tirado en el suelo"
						: "FALLO: ha aparecido algo en el mundo") + ").");
			}
			finally {
				Main.mouseLeft = izquierdoPrevio;
				Main.mouseLeftRelease = sueltoPrevio;
			}

			// Se deja vacia para no dejar basura visible en el panel el resto de la prueba.
			jugador.trashItem = new Item();
		}

		private static void Clic(BotonTk boton)
		{
			if (boton == null) {
				return;
			}
			CalculatedStyle dim = boton.GetDimensions();
			Vector2 centro = new Vector2(dim.X + dim.Width / 2f, dim.Y + dim.Height / 2f);
			boton.LeftClick(new UIMouseEvent(boton, centro));
		}

		/// <summary>Cuenta los objetos activos tirados en el mundo (<c>Main.item</c>). Se usa antes
		/// y despues de la papelera para demostrar que nada acaba en el suelo.</summary>
		private static int ContarObjetosEnElMundo()
		{
			int cuenta = 0;
			for (int i = 0; i < Main.item.Length; i++) {
				if (Main.item[i] != null && Main.item[i].active) {
					cuenta++;
				}
			}
			return cuenta;
		}

		/// <summary>
		/// Ultimo paso: cierra el panel con un objeto todavia cogido con el raton y comprueba que
		/// se devuelve al inventario en vez de quedarse invisible fuera del panel.
		/// </summary>
		private static void ComprobarCierreConObjetoEnElRaton()
		{
			Player jugador = Main.LocalPlayer;

			Item enElRaton = new Item();
			enElRaton.SetDefaults(ItemID.GoldCoin);
			enElRaton.stack = 3;
			Main.mouseItem = enElRaton;

			bool desbordado;
			long antes = Utils.CoinsCount(out desbordado, jugador.inventory);

			PanelPruebaSystem.CerrarPanel("autoprueba WS1 (cierre con objeto en el raton)");

			long despues = Utils.CoinsCount(out desbordado, jugador.inventory);

			Registrar("Paso 27 - cierre con un objeto cogido (3 monedas de oro = 30000 cobre). "
				+ "Monedas en el inventario antes=" + antes + ", despues=" + despues
				+ " (diferencia " + (despues - antes) + "). "
				+ "Objeto que queda en el raton: " + PersonajeVivo.DescribirObjeto(Main.mouseItem) + ". "
				+ "Panel abierto ahora: " + PanelPruebaSystem.PanelAbierto + ".");

			Registrar("AUTOPRUEBA WS1 COMPLETA. Todos los pasos ejecutados sin excepciones.");
			_terminada = true;
		}

		// ---------------------------------------------------------------- utilidades

		private static int ObtenerTipo(Item[] inventario, int indice)
		{
			return inventario[indice] == null ? 0 : inventario[indice].type;
		}

		private static void PonerObjeto(Item[] inventario, int indice, int tipo, int cantidad)
		{
			if (tipo <= 0 || indice < 0 || indice >= inventario.Length) {
				return;
			}

			Item objeto = new Item();
			objeto.SetDefaults(tipo);
			objeto.stack = cantidad > objeto.maxStack ? objeto.maxStack : cantidad;
			inventario[indice] = objeto;
		}

		/// <summary>Primer objeto del juego que cumple la condicion, usando las muestras que el
		/// propio juego mantiene. Asi la prueba no depende de ids concretos ni de que haya
		/// ningun mod de contenido cargado.</summary>
		private static int BuscarObjeto(Func<Item, bool> condicion)
		{
			for (int tipo = 1; tipo < ItemLoader.ItemCount; tipo++) {
				Item muestra;
				if (!ContentSamples.ItemsByType.TryGetValue(tipo, out muestra) || muestra == null) {
					continue;
				}
				if (muestra.type <= 0 || string.IsNullOrEmpty(muestra.Name)) {
					continue;
				}
				if (condicion(muestra)) {
					return tipo;
				}
			}
			return 0;
		}

		private static string Describir(Color color)
		{
			return "(" + color.R + "," + color.G + "," + color.B + ")";
		}

		private static void Registrar(string mensaje)
		{
			Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
		}
	}
}
