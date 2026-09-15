using Terraria;
using Terraria.ID;
using Terrakeep.Core.Guia;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// La mitad "en vivo" del cerebro unico de la Guia (consolidacion T1,
	/// I+D-PROXIMOS-PASOS-FAMILIA-KEEP.md, 16-sep-2026). Implementa
	/// <see cref="IGuideStateProvider"/> leyendo el juego en marcha a traves de
	/// <see cref="EstadoJugadorGuia"/>/<see cref="BanderasGuia"/> - la logica de DESPACHO y de
	/// ARITMETICA (que antes vivia duplicada aqui mismo, en <c>EvaluadorGuia.cs</c>) ahora vive UNA
	/// sola vez en <c>Terrakeep.Core.Guia.GuideEvaluationEngine</c>, compartida con Terrakeep de
	/// escritorio (ver <c>DesktopGuideStateProvider</c> en ese mismo proyecto).
	/// </summary>
	/// <remarks>
	/// Las cuatro capacidades (<c>Has*</c>) son SIEMPRE true aqui: <see cref="EstadoJugadorGuia"/>
	/// ya se degrada sola a ceros/false cuando no hay partida activa (<c>HayPartida</c>), asi que
	/// sin partida el requisito sale "no cumplido" en vez de "no evaluable" - EXACTAMENTE el
	/// comportamiento que tenia el evaluador del mod antes de esta consolidacion (nunca marcaba
	/// nada NoEvaluable por falta de partida, solo por tipo desconocido). Por eso este proveedor
	/// nunca llega a llamar a <see cref="MotivoSinPartidaEnMarcha"/> ni a
	/// <see cref="MotivoBanderaDesconocida"/> de verdad: el motor solo los pide cuando la
	/// capacidad correspondiente es false, y aqui no lo es nunca.
	/// </remarks>
	public sealed class ProveedorEstadoGuiaMod : IGuideStateProvider
	{
		public bool HasCharacterData => true;
		public bool HasWorldData => true;
		public bool HasInventoryData => true;
		public bool HasLiveGameData => true;

		public int CristalesVida => EstadoJugadorGuia.CristalesVida;
		public int VidaMaxima => EstadoJugadorGuia.VidaMaxima;
		public int Defensa => EstadoJugadorGuia.Defensa;
		public int NpcsDelPueblo() => EstadoJugadorGuia.NpcsDelPueblo();
		public bool HayNpc(int id) => EstadoJugadorGuia.HayNpc(id);
		public int CuantosLleva(int id) => EstadoJugadorGuia.CuantosLleva(id);
		public int DanoDelMejorArma(out string nombre) => EstadoJugadorGuia.DanoDelMejorArma(out nombre);
		public bool LlevaGancho(out string nombre) => EstadoJugadorGuia.LlevaGancho(out nombre);

		public bool BanderaConocida(string bandera) => BanderasGuia.Existe(bandera);
		public bool? ValorBandera(string bandera) => BanderasGuia.Valor(bandera);
		public string MotivoBanderaDesconocida(string bandera) => null;

		/// <summary>
		/// Nombre de un objeto <b>en el idioma del juego</b>, del propio juego. Sale de
		/// <c>ContentSamples.ItemsByType</c>, la tabla de muestras que tModLoader rellena al cargar
		/// el contenido: asi el nombre es el mismo que ve el jugador en su tooltip.
		/// </summary>
		public string NombreDeObjeto(int tipo)
		{
			Item muestra;
			if (tipo > 0 && ContentSamples.ItemsByType.TryGetValue(tipo, out muestra) && muestra != null) {
				return muestra.Name;
			}
			return "#" + tipo;
		}

		/// <summary>Nombre de un NPC, igual, de <c>ContentSamples.NpcsByNetId</c>.</summary>
		public string NombreDeNpc(int tipo)
		{
			NPC muestra;
			if (ContentSamples.NpcsByNetId.TryGetValue(tipo, out muestra) && muestra != null) {
				return muestra.FullName;
			}
			return "#" + tipo;
		}

		public string MotivoSinPartidaEnMarcha(TipoRequisitoGuia tipo) => null;
	}
}
