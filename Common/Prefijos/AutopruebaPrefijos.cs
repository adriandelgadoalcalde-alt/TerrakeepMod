using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Libreria;
using TerrakeepMod.UI.Libreria;

namespace TerrakeepMod.Common.Prefijos
{
	/// <summary>
	/// Arnes de verificacion PROPIO de los dos problemas reales de prefijos (indicador de calidad +
	/// auto-aplicar al coger de la Libreria). Sandbox y variable de entorno propios
	/// (<see cref="Variable"/>) para no pisar la autoprueba de WS3 ni la de ningun otro workstream
	/// que este corriendo en paralelo sobre este mismo repositorio.
	/// </summary>
	/// <remarks>
	/// Escribe SIEMPRE a un archivo propio dentro de <c>Main.SavePath</c>
	/// (<c>terrakeep-prefijos-evidencia.log</c>), igual que ya hacen WS3/WS4, y solo cuando la
	/// variable de entorno esta puesta: jugando normal no deja nada suelto.
	/// </remarks>
	public static class AutopruebaPrefijos
	{
		public const string Variable = "TERRAKEEP_AUTOTEST_PREFIJOS";
		private const string NombreArchivo = "terrakeep-prefijos-evidencia.log";

		/// <summary>Objeto de prueba: una espada vanilla con entrada REAL en best_prefix.json
		/// (81 Legendary), la misma familia que sugiere el propio encargo.</summary>
		private const int TipoDePrueba = ItemID.CopperShortsword;

		/// <summary>Prefijo vanilla valido para espadas pero DELIBERADAMENTE no el mejor (5 =
		/// Sharp, solo sube el critico: nunca lo rechaza <c>CanApplyPrefix</c> por "sin efecto
		/// visible", a diferencia de otros que dependen de un valor base concreto), para poder
		/// probar el indicador con un objeto subOptimo real.</summary>
		private const int PrefijoSubOptimo = PrefixID.Sharp;

		private const int FotogramasEntrePasos = 10;

		private static bool _activa;
		private static bool _comprobada;
		private static bool _terminada;
		private static int _paso;
		private static int _espera;
		private static int _fotogramasEnMundo;
		private static bool _archivoIniciado;

		public static void Actualizar()
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
			if (_fotogramasEnMundo < 120) {   // da tiempo a que el mundo termine de entrar de verdad
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}
			_espera = FotogramasEntrePasos;

			try {
				EjecutarPaso(_paso);
			}
			catch (Exception e) {
				Registrar("AUTOPRUEBA PREFIJOS: EXCEPCION en el paso " + _paso + ": " + e);
				_terminada = true;
				return;
			}

			_paso++;
		}

		private static void EjecutarPaso(int paso)
		{
			switch (paso) {
				case 0: ComprobarTablaLista(); break;
				case 1: ComprobarBaculosDeInvocacion(); break;
				case 2: AbrirLibreria(); break;
				case 3: CogerConAutoPrefijo(); break;
				case 4: IndicadorConPrefijoSubOptimo(); break;
				case 5: IndicadorConElMejorPrefijoYaPuesto(); break;
				case 6: AuditarCoberturaCompleta(); break;
				default:
					Registrar("AUTOPRUEBA PREFIJOS COMPLETA. Todos los pasos ejecutados sin excepciones.");
					_terminada = true;
					break;
			}
		}

		// =========================================================================================

		private static void ComprobarTablaLista()
		{
			Registrar("Paso 0 - CatalogoMejorPrefijo.Listo=" + CatalogoMejorPrefijo.Listo + ". " +
				(CatalogoMejorPrefijo.Listo ? "OK." : "FALLO: la tabla no llego a resolverse."));
		}

		/// <summary>
		/// El problema real que reporto el usuario el 8-sep-2026: NINGUN baculo enseñaba su
		/// etiqueta de "mejor prefijo posible". Eran los de INVOCACION (los de magia si iban), y la
		/// causa era que <c>best_prefix.json</c> se generaba contra Terraria 1.4.5.8, donde
		/// invocacion tiene pool propio (85 Fabled..97), prefijos que en este tModLoader 1.4.4.9 no
		/// existen (`PrefixID.Count` = 85): <see cref="CatalogoMejorPrefijo"/> los descartaba por su
		/// guardarrail de rango y no pintaba nada. Ahora la tabla se genera contra 1.4.4.9, donde
		/// Magia e Invocacion comparten pool (tope 83 Mythical).
		/// <para />
		/// Cada valor esperado esta verificado contra la wiki oficial en una revision ANTERIOR a
		/// 1.4.5 (que es la version de este juego): Baculo de slime -> "its best possible modifier
		/// is Mythical"; Baculo de cuchillas (retroceso 0) -> "Its best modifiers are Demonic,
		/// Deadly, Mystic, or Hurtful"; Baculo optico -> "Its best modifiers are Mythical, Furious,
		/// or Godly".
		/// </summary>
		private static void ComprobarBaculosDeInvocacion()
		{
			// Smolstar (4758) es el nombre interno real del Baculo de cuchillas / Blade Staff: es el
			// unico de los cinco con retroceso 0, y por eso su mejor prefijo real NO es Mythical.
			int[] tipos = { ItemID.SlimeStaff, ItemID.OpticStaff, ItemID.PygmyStaff, ItemID.FlinxStaff, ItemID.Smolstar };
			byte[] esperados = { PrefixID.Mythical, PrefixID.Mythical, PrefixID.Mythical, PrefixID.Mythical, PrefixID.Demonic };

			for (int i = 0; i < tipos.Length; i++) {
				byte? mejor = CatalogoMejorPrefijo.MejorPrefijo(tipos[i]);

				// Y que la linea del tooltip aparece DE VERDAD por la ruta real de produccion, no
				// solo que la tabla tiene el dato: el objeto sin prefijo tiene que recibir el aviso.
				Item item = new Item();
				item.SetDefaults(tipos[i]);
				List<TooltipLine> tooltips = new List<TooltipLine>();
				ModContent.GetInstance<GlobalItemMejorPrefijo>().ModifyTooltips(item, tooltips);
				TooltipLine linea = BuscarLineaDelIndicador(tooltips);

				bool ok = mejor.HasValue && mejor.Value == esperados[i] && linea != null;
				Registrar("Paso 1." + i + " - baculo de invocacion \"" + item.Name + "\" (type=" + tipos[i] +
					", damage=" + item.damage + ", mana=" + item.mana + ", knockBack=" + item.knockBack +
					", CanHavePrefixes=" + item.CanHavePrefixes() + "): MejorPrefijo=" +
					(mejor.HasValue ? mejor.Value + " (" + Lang.prefix[mejor.Value].Value + ")" : "null") +
					", esperado " + esperados[i] + " (" + Lang.prefix[esperados[i]].Value + "). Linea de tooltip: " +
					(linea != null ? "\"" + linea.Text + "\"" : "(ninguna)") + ". " +
					(ok ? "OK." : "FALLO."));
			}
		}

		/// <summary>
		/// Auditoria de cobertura OBJETO A OBJETO contra el propio juego, no contra el .json: para
		/// cada <c>Item.type</c> real cargado (vanilla + los mods que haya), compara lo que dice
		/// <c>Item.CanHavePrefixes()</c> - la funcion REAL del motor - con si la tabla tiene o no
		/// una sugerencia. Deja los dos recuentos y una muestra de cada desajuste.
		/// <para />
		/// Los dos lados no tienen por que cuadrar al 100% y eso NO es un fallo: `CanHavePrefixes()`
		/// mira ademas `maxStack == 1 || AllowReforgeForStackableItem` y `ammo == 0`, condiciones
		/// que la tabla offline no modela (un arma apilable puede admitir prefijo en el juego y no
		/// tenerlo tabulado). Lo que si seria un defecto es lo contrario: sugerir un prefijo para
		/// un objeto que el motor no deja prefijar nunca.
		/// </summary>
		private static void AuditarCoberturaCompleta()
		{
			int total = 0, puede = 0, puedeConEntrada = 0, noPuedeConEntrada = 0;
			List<string> sinEntrada = new List<string>();
			List<string> sobrantes = new List<string>();

			for (int tipo = 1; tipo < ItemLoader.ItemCount; tipo++) {
				Item item = new Item();
				try {
					item.SetDefaults(tipo);
				}
				catch (Exception) {
					continue;   // algun ModItem de terceros puede reventar en SetDefaults; no es cosa nuestra
				}
				if (item.IsAir) {
					continue;
				}
				total++;

				bool admite = item.CanHavePrefixes();
				bool tabulado = CatalogoMejorPrefijo.MejorPrefijo(tipo).HasValue;
				if (admite) {
					puede++;
					if (tabulado) {
						puedeConEntrada++;
					}
					else if (sinEntrada.Count < 40) {
						sinEntrada.Add(item.Name + " (" + tipo + ", maxStack=" + item.maxStack +
							", ammo=" + item.ammo + ", damage=" + item.damage + ", accessory=" + item.accessory + ")");
					}
				}
				else if (tabulado) {
					noPuedeConEntrada++;
					if (sobrantes.Count < 40) {
						sobrantes.Add(item.Name + " (" + tipo + ", maxStack=" + item.maxStack +
							", ammo=" + item.ammo + ", damage=" + item.damage + ", accessory=" + item.accessory +
							", vanity=" + item.vanity + ")");
					}
				}
			}

			Registrar("Paso 6 - AUDITORIA DE COBERTURA sobre los " + total + " tipos de objeto reales " +
				"cargados (ItemLoader.ItemCount=" + ItemLoader.ItemCount + "): " + puede +
				" admiten prefijo segun Item.CanHavePrefixes() del propio motor, y de esos " +
				puedeConEntrada + " tienen sugerencia en la tabla (" +
				(puede > 0 ? (100.0 * puedeConEntrada / puede).ToString("0.0") : "0") + "%). " +
				"Objetos con sugerencia que el motor NO deja prefijar: " + noPuedeConEntrada + ".");
			Registrar("Paso 6 - admiten prefijo y NO tienen sugerencia (" +
				(puede - puedeConEntrada) + " en total, muestra de " + sinEntrada.Count + "): " +
				string.Join(" | ", sinEntrada));
			Registrar("Paso 6 - tienen sugerencia y el motor NO los deja prefijar (" + noPuedeConEntrada +
				" en total, muestra de " + sobrantes.Count + "): " + string.Join(" | ", sobrantes));
		}

		private static void AbrirLibreria()
		{
			PanelLibreriaSystem.AbrirPanel("autopruebaPrefijos");
			Registrar("Paso 2 - panel de Libreria abierto. PanelActual=" +
				(PanelLibreriaSystem.PanelActual != null ? "listo" : "null") + ".");
		}

		private static void CogerConAutoPrefijo()
		{
			ContenidoLibreria contenido = PanelLibreriaSystem.PanelActual;
			if (contenido == null) {
				Registrar("Paso 3 - FALLO: el panel de Libreria no esta montado.");
				return;
			}

			byte? esperado = CatalogoMejorPrefijo.MejorPrefijo(TipoDePrueba);
			Main.mouseItem = new Item();

			contenido.PedirObjeto(TipoDePrueba, false);

			Item enElRaton = Main.mouseItem;
			bool ok = esperado.HasValue && enElRaton != null && !enElRaton.IsAir
				&& enElRaton.type == TipoDePrueba && enElRaton.prefix == esperado.Value;

			Registrar("Paso 3 - cogido \"" + CatalogoVivo.Nombre(TipoDePrueba) + "\" (type=" + TipoDePrueba +
				") del catalogo de la Libreria via ContenidoLibreria.PedirObjeto (la ruta REAL de " +
				"produccion). Mejor prefijo esperado segun la tabla: " +
				(esperado.HasValue ? esperado.Value + " (" + Lang.prefix[esperado.Value].Value + ")" : "ninguno") +
				". Item.prefix REAL tras cogerlo: " + (enElRaton != null ? enElRaton.prefix.ToString() : "(sin item)") +
				(enElRaton != null && enElRaton.prefix > 0 && enElRaton.prefix < PrefixID.Count
					? " (" + Lang.prefix[enElRaton.prefix].Value + ")" : "") +
				". " + (ok ? "OK: el objeto salio del catalogo con su mejor prefijo real ya puesto."
					: "FALLO: el prefijo aplicado no coincide con el esperado."));
		}

		private static void IndicadorConPrefijoSubOptimo()
		{
			byte? mejor = CatalogoMejorPrefijo.MejorPrefijo(TipoDePrueba);

			Item item = new Item();
			item.SetDefaults(TipoDePrueba);
			item.Prefix(PrefijoSubOptimo);

			List<TooltipLine> tooltips = new List<TooltipLine>();
			ModContent.GetInstance<GlobalItemMejorPrefijo>().ModifyTooltips(item, tooltips);

			TooltipLine linea = BuscarLineaDelIndicador(tooltips);
			bool ok = mejor.HasValue && item.prefix == PrefijoSubOptimo && linea != null
				&& linea.Text.IndexOf(Lang.prefix[mejor.Value].Value, StringComparison.Ordinal) >= 0;

			Registrar("Paso 4 - indicador con prefijo SUBOPTIMO a proposito: \"" + item.Name +
				"\" con item.prefix=" + item.prefix + " (" + Lang.prefix[item.prefix].Value +
				"), mejor real=" + (mejor.HasValue ? mejor.Value + " (" + Lang.prefix[mejor.Value].Value + ")" : "ninguno") +
				". Linea de tooltip añadida: " + (linea != null ? "\"" + linea.Text + "\" (color override=" +
					(linea.OverrideColor.HasValue ? linea.OverrideColor.Value.ToString() : "ninguno") + ")" : "(ninguna)") +
				". " + (ok ? "OK: aparece el aviso con el prefijo real que le falta."
					: "FALLO: no aparecio la linea esperada."));
		}

		private static void IndicadorConElMejorPrefijoYaPuesto()
		{
			byte? mejor = CatalogoMejorPrefijo.MejorPrefijo(TipoDePrueba);
			if (!mejor.HasValue) {
				Registrar("Paso 5 - no hay mejor prefijo tabulado para el objeto de prueba; no se puede " +
					"comprobar el caso \"ya optimo\".");
				return;
			}

			Item item = new Item();
			item.SetDefaults(TipoDePrueba);
			item.Prefix(mejor.Value);

			List<TooltipLine> tooltips = new List<TooltipLine>();
			ModContent.GetInstance<GlobalItemMejorPrefijo>().ModifyTooltips(item, tooltips);

			TooltipLine linea = BuscarLineaDelIndicador(tooltips);
			bool ok = item.prefix == mejor.Value && linea == null;

			Registrar("Paso 5 - indicador con el MEJOR prefijo ya puesto (" + mejor.Value + " " +
				Lang.prefix[mejor.Value].Value + "): item.prefix quedo en " + item.prefix + ". Linea de " +
				"tooltip añadida: " + (linea != null ? "\"" + linea.Text + "\" (no deberia aparecer)" : "(ninguna)") +
				". " + (ok ? "OK: sin aviso, el prefijo ya es el optimo."
					: "FALLO: no deberia haber aparecido ninguna linea."));

			// Deja el raton limpio para no ensuciar el resto de la sesion de prueba.
			Main.mouseItem = new Item();
		}

		private static TooltipLine BuscarLineaDelIndicador(List<TooltipLine> tooltips)
		{
			foreach (TooltipLine linea in tooltips) {
				if (linea.Name == "TerrakeepMejorPrefijo") {
					return linea;
				}
			}
			return null;
		}

		private static void Registrar(string linea)
		{
			string completa = Terrakeep.LogTag + " " + linea;
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(completa);
			}
			EscribirEnArchivo(completa);
		}

		private static void EscribirEnArchivo(string linea)
		{
			try {
				string ruta = Path.Combine(Main.SavePath, NombreArchivo);
				if (!_archivoIniciado) {
					_archivoIniciado = true;
					File.WriteAllText(ruta,
						$"# Evidencia de la autoprueba de Prefijos - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
				}
				File.AppendAllText(ruta, $"[{DateTime.Now:HH:mm:ss.fff}] {linea}{Environment.NewLine}");
			}
			catch (Exception) {
				// La evidencia del log del juego ya esta escrita; si el archivo no se puede
				// escribir (permisos, disco), no vale la pena tumbar nada por ello.
			}
		}
	}
}
