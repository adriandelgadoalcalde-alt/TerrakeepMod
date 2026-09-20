using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terrakeep.Core.Model;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Builds
{
	/// <summary>Resultado real de aplicar un código de build (ver <see cref="CodigoDeBuild.Importar"/>).</summary>
	public sealed class ResultadoImportarCodigo
	{
		/// <summary>false si el propio texto no era un código válido - ver <see cref="MensajeError"/>.
		/// Con true, el código se pudo LEER (aunque algún objeto suelto no se pudiera aplicar).</summary>
		public bool Ok;

		public int Movidos;
		public int Creados;
		public int YaColocados;
		public int SinSitio;

		/// <summary>Id de objeto del código que no existe en ESTA versión del juego (típicamente: el
		/// código se generó con un mod cargado que aquí no lo está).</summary>
		public int NoReconocidos;

		public int TintesMovidos;

		/// <summary>Solo si <see cref="Ok"/> es false: por qué no se pudo ni leer.</summary>
		public string MensajeError;

		public readonly List<string> Detalle = new List<string>();
	}

	/// <summary>
	/// <b>Idea 7 del catálogo de funciones ("Códigos de build dentro del juego"):</b> importar/
	/// exportar el conjunto de equipo activo con <c>Terrakeep.Core.Model.BuildCode</c> - el mismo
	/// códec real que ya usa (o usará) la app de escritorio hermana, compilado para net8.0 y
	/// referenciado en este mod desde el primer día (<c>lib/Terrakeep.Core.dll</c>) pero sin
	/// ningún punto de entrada que lo llamara todavía.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Las 10 ranuras del código son, por posición, EXACTAMENTE las de <c>Player.armor[0..9]</c></b>
	/// (0 casco, 1 pechera, 2 grebas, 3-9 accesorios 1-7 - ver <see cref="EquipoJugador.ArmorDe"/>):
	/// es el mismo orden que exige <c>BuildCode.Encode</c> ("Cabeza/Cuerpo/Piernas/Accesorio 1-7",
	/// mensaje real de su propia validación), así que no hace falta reordenar nada entre el array
	/// del jugador y el códec. Los 10 tintes van en <c>Player.dye[]</c>, en las mismas posiciones.
	/// </para>
	/// <para>
	/// <b>LÍMITE REAL, investigado antes de escribir una sola línea (disciplina de dos fases del
	/// proyecto): los OBJETOS de un mod (Calamity incluido) no se codifican todavía.</b>
	/// <c>BuildCode.Encode</c> solo acepta un <c>int ItemId</c> por ranura, y el <c>Item.type</c> de
	/// un objeto de mod lo asigna tModLoader EN CALIENTE al cargar los mods - no es estable de una
	/// sesión a otra ni entre dos máquinas con mods en otro orden, así que codificarlo tal cual
	/// produciría un código que apunta a un objeto DISTINTO (o a ninguno) la próxima vez. La app de
	/// escritorio resuelve esto para objetos con un catálogo de ids sintéticos propio
	/// (<c>Terrakeep.Core/Calamity/CalamityCatalog.cs</c> + <c>CalamityItemCodec.cs</c> del repo
	/// hermano <c>Terrasavr-Native</c>, cientos de entradas) que este mod NO tiene portado todavía -
	/// a diferencia de los PREFIJOS de Pícaro de Calamity, que sí (<see cref="CatalogoPrefijoPicaro"/>,
	/// 21 entradas, ya integrado en <c>AutoEquipar</c>). Un objeto de mod se omite del código
	/// exportado (ranura vacía) en vez de codificar un id que mentiría; ver
	/// <see cref="Exportar"/>/<see cref="CodificarSlot"/> para el detalle y el aviso real que deja en
	/// el resultado.
	/// </para>
	/// <para>
	/// <b>Nunca destruye nada, mismo criterio que <see cref="AutoEquipar"/>.</b> Un objeto que ya
	/// tengas (en cualquier contenedor, ver <see cref="EquipoJugador.Buscar"/>) se MUEVE a su sitio;
	/// uno que no tengas se CREA igual que <c>AutoEquipar.CrearDesdeLibreria</c>
	/// (<c>Item.SetDefaults</c> + el prefijo real del código); si el slot de destino ya lleva puesta
	/// otra cosa, esa otra cosa se desplaza a la mochila en vez de perderse. Los TINTES son la única
	/// excepción real: nunca se crean (serían un atajo gratis y una trampa de verdad para un objeto
	/// puramente cosmético que además no cuesta reponer) - si no los tienes, esa ranura se deja tal
	/// cual estaba.
	/// </para>
	/// <para>
	/// <b>Una ranura VACÍA en el código nunca borra lo que ya llevas puesto ahí.</b> Se podría
	/// argumentar que una reproducción fiel debería vaciar ese accesorio para igualar exactamente la
	/// build exportada, pero eso exigiría poder "perder de vista" un objeto que sí llevabas puesto
	/// sin que el código dijera nada de él - el mismo tipo de sorpresa que la Marca Keep prohíbe.
	/// </para>
	/// </remarks>
	public static class CodigoDeBuild
	{
		private static readonly string[] NombresSlot = {
			"Builds.Codigo.Slot.Cabeza", "Builds.Codigo.Slot.Cuerpo", "Builds.Codigo.Slot.Piernas",
			"Builds.Codigo.Slot.Accesorio1", "Builds.Codigo.Slot.Accesorio2", "Builds.Codigo.Slot.Accesorio3",
			"Builds.Codigo.Slot.Accesorio4", "Builds.Codigo.Slot.Accesorio5", "Builds.Codigo.Slot.Accesorio6",
			"Builds.Codigo.Slot.Accesorio7"
		};

		/// <summary>
		/// Codifica el conjunto de equipo <paramref name="loadoutObjetivo"/> (0/1/2) del jugador en
		/// un código <c>TKBUILD1:...</c>. <paramref name="omitidos"/> sale con cuántas de las 10
		/// ranuras llevaban un objeto de mod que no se pudo codificar (ver el LÍMITE REAL del XMLdoc
		/// de la clase) - 0 en cualquier partida sin mods de objetos equipados, el caso normal.
		/// </summary>
		public static string Exportar(Player jugador, int loadoutObjetivo, out int omitidos)
		{
			omitidos = 0;
			Item[] armadura = EquipoJugador.ArmorDe(jugador, loadoutObjetivo);

			List<BuildCodeSlot> slots = new List<BuildCodeSlot>(BuildCode.SlotCount);
			List<int> tintes = new List<int>(BuildCode.SlotCount);

			for (int i = 0; i < BuildCode.SlotCount; i++) {
				slots.Add(CodificarSlot(armadura[i], ref omitidos));

				Item tinte = jugador.dye != null && i < jugador.dye.Length ? jugador.dye[i] : null;
				bool tinteCodificable = tinte != null && !tinte.IsAir && tinte.ModItem == null;
				tintes.Add(tinteCodificable ? tinte.type : 0);
			}

			return BuildCode.Encode(slots, tintes);
		}

		private static BuildCodeSlot CodificarSlot(Item pieza, ref int omitidos)
		{
			if (pieza == null || pieza.IsAir) {
				return BuildCodeSlot.Empty;
			}

			if (pieza.ModItem != null) {
				// LÍMITE REAL documentado en el XMLdoc de la clase: sin catálogo de ids sintéticos
				// de objeto, un objeto de mod no se puede codificar de forma estable. Se omite
				// (ranura vacía) en vez de escribir un Item.type que mañana sería otra cosa.
				omitidos++;
				return BuildCodeSlot.Empty;
			}

			return new BuildCodeSlot(pieza.type, ResolverPrefijoDelObjeto(pieza));
		}

		private static ItemPrefix ResolverPrefijoDelObjeto(Item pieza)
		{
			if (pieza.prefix <= 0) {
				return ItemPrefix.None;
			}
			if (pieza.prefix < PrefixID.Count) {
				return ItemPrefix.Vanilla((byte)pieza.prefix);
			}

			int? sintetico = CatalogoPrefijoPicaro.ResolverIdSintetico(pieza.prefix);
			// Un prefijo de mod SIN entrada en la tabla (de otro mod, o de una version de Calamity
			// que renombro la clase) se codifica sin prefijo en vez de tumbar el codigo entero: el
			// objeto en si sigue siendo vanilla-identificable, solo se pierde el matiz del prefijo.
			return sintetico.HasValue ? ItemPrefix.CalamitySynthetic(sintetico.Value) : ItemPrefix.None;
		}

		/// <summary>
		/// Aplica un código <c>TKBUILD1:...</c> al conjunto de equipo <paramref name="loadoutObjetivo"/>
		/// (0/1/2) del jugador. Ver el XMLdoc de la clase para las garantías reales (nunca destruye,
		/// tintes nunca se crean, ranura vacía nunca borra).
		/// </summary>
		public static ResultadoImportarCodigo Importar(Player jugador, string codigo, int loadoutObjetivo)
		{
			ResultadoImportarCodigo resultado = new ResultadoImportarCodigo();

			IReadOnlyList<BuildCodeSlot> items;
			IReadOnlyList<int> tintes;
			BuildCodeError? error;
			if (!BuildCode.TryDecode(codigo ?? "", out items, out tintes, out error)) {
				resultado.Ok = false;
				resultado.MensajeError = TextoDeError(error);
				return resultado;
			}

			resultado.Ok = true;
			Item[] destino = EquipoJugador.ArmorDe(jugador, loadoutObjetivo);
			int disponibles = EquipoJugador.SlotsAccesorioDisponibles(jugador);

			for (int i = 0; i < BuildCode.SlotCount && i < items.Count; i++) {
				BuildCodeSlot slot = items[i];
				if (slot.ItemId <= 0) {
					continue; // Ranura vacía en el código: nunca borra lo que ya llevas puesto ahí.
				}
				if (i >= EquipoJugador.PrimerSlotAccesorio &&
					i - EquipoJugador.PrimerSlotAccesorio >= disponibles) {
					resultado.SinSitio++;
					resultado.Detalle.Add(Idiomas.Texto(NombresSlot[i]) +
						": " + Idiomas.Texto("Builds.Codigo.SlotNoDesbloqueado"));
					continue;
				}
				AplicarSlot(jugador, destino, i, slot, resultado);
			}

			for (int i = 0; i < BuildCode.SlotCount && i < tintes.Count; i++) {
				AplicarTinte(jugador, i, tintes[i], resultado);
			}

			if (resultado.Movidos > 0 || resultado.Creados > 0 || resultado.TintesMovidos > 0) {
				Recipe.FindRecipes();
				SoundEngine.PlaySound(SoundID.Grab);
			}

			return resultado;
		}

		private static void AplicarSlot(Player jugador, Item[] destino, int indice, BuildCodeSlot slot,
			ResultadoImportarCodigo resultado)
		{
			int tipoReal = slot.ItemId;
			if (tipoReal <= 0 || tipoReal >= ItemID.Count) {
				resultado.NoReconocidos++;
				resultado.Detalle.Add(Idiomas.Texto(NombresSlot[indice]) + ": #" + tipoReal + " " +
					Idiomas.Texto("Builds.Codigo.IdNoReconocido"));
				return;
			}

			int? prefijoReal = ResolverPrefijoReal(slot.Prefix);

			UbicacionObjeto donde = EquipoJugador.Buscar(jugador, tipoReal);
			if (donde != null) {
				Item poseido = donde.Contenedor[donde.Indice];
				bool mismoSitio = ReferenceEquals(donde.Contenedor, destino) && donde.Indice == indice;
				bool prefijoCoincide = poseido.prefix == (prefijoReal ?? 0);

				if (mismoSitio && prefijoCoincide) {
					resultado.YaColocados++;
					return;
				}

				string origen = donde.ToString();
				EquipoJugador.Intercambiar(donde.Contenedor, donde.Indice, destino, indice);
				resultado.Movidos++;
				resultado.Detalle.Add(Idiomas.Texto(NombresSlot[indice]) + ": " + origen +
					(prefijoCoincide ? "" : " (" + Idiomas.Texto("Builds.Codigo.PrefijoDistinto") + ")"));
				return;
			}

			// No lo tiene en ningún sitio: se crea, igual que AutoEquipar.CrearDesdeLibreria.
			if (!destino[indice].IsAir) {
				int hueco = EquipoJugador.PrimerHuecoMochila(jugador);
				if (hueco < 0) {
					resultado.SinSitio++;
					resultado.Detalle.Add(Idiomas.Texto(NombresSlot[indice]) + ": " +
						Idiomas.Texto("Builds.Codigo.MochilaLlena"));
					return;
				}
				jugador.inventory[hueco] = destino[indice];
			}

			Item nuevo = new Item();
			nuevo.SetDefaults(tipoReal);
			if (prefijoReal.HasValue) {
				nuevo.Prefix(prefijoReal.Value);
			}
			nuevo.stack = 1;
			destino[indice] = nuevo;
			resultado.Creados++;
		}

		private static void AplicarTinte(Player jugador, int indice, int tipoTinte, ResultadoImportarCodigo resultado)
		{
			if (tipoTinte <= 0 || jugador.dye == null || indice >= jugador.dye.Length) {
				return;
			}
			if (tipoTinte >= ItemID.Count) {
				return; // Id de tinte no reconocido: se ignora en silencio, no es tan critico como el equipo.
			}

			Item actual = jugador.dye[indice];
			if (actual != null && !actual.IsAir && actual.type == tipoTinte) {
				return; // Ya puesto.
			}

			// Los tintes NUNCA se crean (ver el XMLdoc de la clase): si no lo tienes, esta ranura se
			// deja tal cual.
			UbicacionObjeto donde = EquipoJugador.Buscar(jugador, tipoTinte);
			if (donde == null) {
				return;
			}

			EquipoJugador.Intercambiar(donde.Contenedor, donde.Indice, jugador.dye, indice);
			resultado.TintesMovidos++;
		}

		private static int? ResolverPrefijoReal(ItemPrefix prefijo)
		{
			if (prefijo.IsNone) {
				return null;
			}
			if (prefijo.IsCalamity) {
				return CatalogoPrefijoPicaro.ResolverPrefijoReal(prefijo.SyntheticId);
			}
			return prefijo.VanillaId > 0 && prefijo.VanillaId < PrefixID.Count ? (int?)prefijo.VanillaId : null;
		}

		private static string TextoDeError(BuildCodeError? error)
		{
			switch (error) {
				case BuildCodeError.Corrupt:
					return Idiomas.Texto("Builds.Codigo.ErrorCorrupto");
				case BuildCodeError.UnsupportedVersion:
					return Idiomas.Texto("Builds.Codigo.ErrorVersion");
				default:
					return Idiomas.Texto("Builds.Codigo.ErrorFormato");
			}
		}
	}
}
