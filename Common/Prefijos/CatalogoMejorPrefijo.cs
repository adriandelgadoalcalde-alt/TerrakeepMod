using System;
using System.IO;
using Terraria.ID;
using Terraria.ModLoader;
using Terrakeep.Core.Data;

namespace TerrakeepMod.Common.Prefijos
{
	/// <summary>
	/// Envoltorio en vivo de <c>Assets/best_prefix.json</c> (copiado tal cual del repo hermano
	/// <c>Terrasavr-Native</c>, <c>Terrakeep.App/Assets/calamity/<b>best_prefix_tml.json</b></c>) -
	/// la tabla real de "mejor prefijo posible" por objeto. Mismo criterio y mismo generador
	/// (<c>scripts/generar-mejor-prefijo.py</c>) que la tabla de la app de escritorio, pero
	/// calculada contra el arbol de <b>tModLoader 1.4.4.9</b>, que es el juego real donde corre
	/// este mod, en vez de contra el de Terraria 1.4.5.8, que es el de la app.
	/// </summary>
	/// <remarks>
	/// <b>Se parsea con <see cref="BestPrefixCatalog"/> de <c>Terrakeep.Core</c> tal cual</b>,
	/// el mismo codigo que usa la app de escritorio (<c>BestPrefixCatalog.LoadFromStream</c>): nada
	/// reimplementado, mismo patron que ya siguio WS4 con <c>builds.json</c>/<c>CatalogoBuilds</c>.
	/// <para />
	/// <b>Por que los bytes se leen en <c>Load()</c> y se parsean en <c>PostSetupContent()</c></b>:
	/// <c>TmodFile.GetStream</c> lanza <c>IOException("File not open")</c> una vez cerrado el
	/// <c>.tmod</c>, cosa que ya pasa al terminar la carga. Leer los bytes durante <c>Load()</c>
	/// (archivo abierto seguro) y parsearlos despues evita depender de ese detalle.
	/// <para />
	/// <b>Los valores de la tabla son <c>Terraria.ID.PrefixID</c> planos</b>, tanto para vanilla
	/// como para Calamity (el generador solo excluye a proposito los 21 prefijos REALES de
	/// Calamity - "Impecable" y compañia para armas Picaro -, que necesitarian resolver un
	/// <c>ModPrefix</c> registrado en tiempo de ejecucion; ver la cabecera de
	/// <c>BestPrefixCatalog.cs</c> y <c>scripts/generar-mejor-prefijo.py</c> en el repo hermano).
	/// Por eso <see cref="MejorPrefijo"/> devuelve directamente un <c>byte</c> asignable sin mas a
	/// <c>Item.prefix</c>, sin ninguna resolucion adicional contra <c>ModPrefix</c>.
	/// <para />
	/// <b>Por que la tabla del mod NO es la misma que la de la app de escritorio</b> (hallazgo del
	/// 8-sep-2026, a raiz de que NINGUN baculo de invocacion enseñaba su etiqueta): `PrefixID.Count`
	/// es 98 en Terraria 1.4.5.8 (separo `PrefixesForSummons` de `PrefixesForMagic` y añadio
	/// 85 Fabled..97 Scraggling, solo para invocacion) pero es <b>85</b> en el `tModLoader.dll`
	/// REAL instalado (`Terraria\ID\PrefixID.cs` decompilado: `public static readonly int
	/// Count = 85;`, o sea ids validos 0..84) - aqui Magia e Invocacion comparten un unico pool
	/// (`PrefixesForMagicAndSummons`, tope real 83 Mythical), y ahi caen tambien las armas de
	/// invocacion de MOD (`SummonDamageClass.GetPrefixInheritance(dc) => dc == DamageClass.Magic`).
	/// La tabla vieja, generada contra 1.4.5.8, traia 149 entradas (todas de invocacion: 46 vanilla
	/// + 103 de Calamity, mas 1 caso vanilla en 95 Eager) con un valor que <b>no existe</b> en este
	/// juego, asi que se descartaban aqui y esas armas se quedaban sin etiqueta. Ya no: la tabla se
	/// genera contra 1.4.4.9 y todas tienen su prefijo real (83 Mythical en casi todas, 60 Demonic
	/// en el Baculo de cuchillas, que no tiene retroceso).
	/// <para />
	/// La comprobacion de rango de <see cref="MejorPrefijo"/> (`valor &lt; PrefixID.Count`) se
	/// queda como guardarrail: hoy no descarta nada, pero un valor fuera de rango reventaria
	/// `Lang.prefix[valor]` con `IndexOutOfRangeException` y pondria un `Item.prefix` invalido.
	/// </remarks>
	public static class CatalogoMejorPrefijo
	{
		/// <summary>Nombre interno real del mod Calamity (carpeta del mod, el que usa
		/// <c>Mod.Name</c> - comprobado ya en <c>CatalogoBuilds</c>/<c>ArbolLibreria</c> con el
		/// formato de pid "CalamityMod/NombreInterno").</summary>
		private const string NombreModCalamity = "CalamityMod";

		private const string Ruta = "Assets/best_prefix.json";

		private static byte[] _bytes;
		private static BestPrefixCatalog _catalogo;

		/// <summary>El mod, para poder escribir en su log. Lo rellena
		/// <c>SistemaMejorPrefijo.Load()</c>: no vale <c>Terrakeep.Instance</c> porque tModLoader
		/// ejecuta los <c>ModSystem.Load()</c> ANTES de <c>Mod.Load()</c> (mismo hallazgo que ya
		/// dejo escrito <c>RegistroLibreria</c>).</summary>
		private static Mod _mod;

		/// <summary>true si la tabla ya esta resuelta contra el contenido de esta partida.</summary>
		public static bool Listo {
			get { return _catalogo != null; }
		}

		/// <summary>
		/// Lee el .json de dentro del .tmod. Hay que llamarlo con el archivo del mod todavia
		/// abierto, o sea durante la carga (<c>Mod.Load</c> / <c>ModSystem.Load</c>).
		/// </summary>
		public static void LeerArchivo(Mod mod)
		{
			_mod = mod;
			try {
				if (!mod.FileExists(Ruta)) {
					LogAviso($"{Terrakeep.LogTag} MejorPrefijo: no se encontro {Ruta} dentro del .tmod.");
					return;
				}
				_bytes = mod.GetFileBytes(Ruta);
			}
			catch (Exception ex) {
				LogError($"{Terrakeep.LogTag} MejorPrefijo: fallo leyendo {Ruta}: {ex.Message}");
			}
		}

		/// <summary>
		/// Parsea el archivo ya leido. No necesita resolver nada contra el contenido de la
		/// partida (a diferencia de <c>CatalogoBuilds</c>): las claves vanilla ya son el
		/// <c>Item.type</c> real y las de Calamity se resuelven en el momento en
		/// <see cref="MejorPrefijo"/> contra el <c>ModItem</c> real de cada tipo. Se llama igual
		/// desde <c>PostSetupContent</c>, por coherencia con el resto de catalogos del mod.
		/// </summary>
		public static void Resolver()
		{
			_catalogo = null;
			if (_bytes == null) {
				return;
			}

			try {
				using (MemoryStream flujo = new MemoryStream(_bytes, false)) {
					_catalogo = BestPrefixCatalog.LoadFromStream(flujo);
				}
				LogLinea($"{Terrakeep.LogTag} MejorPrefijo: tabla cargada desde {Ruta}.");
			}
			catch (Exception ex) {
				LogError($"{Terrakeep.LogTag} MejorPrefijo: no se pudo parsear {Ruta}: {ex}");
			}
		}

		/// <summary>
		/// El mejor <c>Terraria.ID.PrefixID</c> real para ese <c>Item.type</c>, o null si la tabla
		/// no tiene entrada para el (objeto sin prefijo util, como un bloque, o un mod que no sea
		/// Calamity). Vanilla se busca por id numerico; Calamity por el nombre interno real del
		/// <c>ModItem</c> (<c>ModItem.Name</c>, el mismo que usa <c>ItemLoader.GetItem(tipo).Name</c>
		/// y que coincide con la mitad derecha del pid "CalamityMod/NombreInterno").
		/// </summary>
		public static byte? MejorPrefijo(int tipo)
		{
			if (_catalogo == null || tipo <= 0 || tipo >= ItemLoader.ItemCount) {
				return null;
			}

			byte? bruto;
			ModItem deMod = ItemLoader.GetItem(tipo);
			if (deMod == null) {
				// ItemLoader.GetItem devuelve null para todo lo vanilla (codigo real del
				// tModLoader.dll instalado: "if (type < ItemID.Count) return null"), igual que ya
				// documento CatalogoVivo.
				bruto = _catalogo.BestVanillaPrefix(tipo);
			}
			else if (deMod.Mod != null && deMod.Mod.Name == NombreModCalamity) {
				bruto = _catalogo.BestCalamityPrefix(deMod.Name);
			}
			else {
				return null;
			}

			if (!bruto.HasValue) {
				return null;
			}

			// Defensa real (ver el hallazgo de la cabecera): un valor de 1.4.5.8 que no exista en
			// el PrefixID de este 1.4.4.9 no se aplica ni se enseña.
			if (bruto.Value >= PrefixID.Count) {
				return null;
			}

			return bruto;
		}

		public static void Descargar()
		{
			_bytes = null;
			_catalogo = null;
			_mod = null;
		}

		// El registro propio de WS3 (RegistroLibreria) solo escribe a archivo durante SU
		// autoprueba; esta tabla la usa tambien el Inventario, asi que aqui basta con el log del
		// propio mod (igual que hace Idiomas.cs para cosas que no son de un panel concreto).
		private static Mod ModUtil {
			get { return _mod ?? Terrakeep.Instance; }
		}

		private static void LogLinea(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Info(linea);
			}
		}

		private static void LogAviso(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Warn(linea);
			}
		}

		private static void LogError(string linea)
		{
			if (ModUtil != null) {
				ModUtil.Logger.Error(linea);
			}
		}
	}
}
