using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Exploracion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// Cabecera comun del panel de Exploracion: que mundo es, de que tamaño, en que modo, cuanto
	/// se ha explorado y donde esta el jugador. Todo se vuelve a preguntar en cada fotograma
	/// (<see cref="EtiquetaTk"/>), asi que no puede quedarse desfasado ni cambiando de modo con el
	/// panel abierto.
	/// </summary>
	public class CabeceraExploracion : UIElement
	{
		public CabeceraExploracion()
		{
			Width.Set(0f, 1f);
			Height.Set(84f, 0f);

			UIPanel caja = new UIPanel();
			caja.Width.Set(0f, 1f);
			caja.Height.Set(0f, 1f);
			caja.BackgroundColor = EstiloTk.FondoCaja;
			caja.BorderColor = new Color(0, 0, 0, 0);
			caja.SetPadding(10f);
			Append(caja);

			// Sin el prefijo "Terrakeep ·": el panel unico ya se identifica una sola vez en su pie,
			// y repetir la marca en cada area era ruido (inconsistencia real detectada al ver las
			// seis piezas juntas: solo Exploracion lo hacia).
			EtiquetaTk titulo = new EtiquetaTk(
				() => "Exploración del mundo", 1.05f, 640f, 30f);
			titulo.Left.Set(2f, 0f);
			titulo.Top.Set(0f, 0f);
			caja.Append(titulo);

			EtiquetaTk mundo = new EtiquetaTk(
				() => "\"" + MundoActual.Nombre + "\"  ·  " + MundoActual.TamanoLegible +
					"  ·  " + MundoActual.ModoDeJuegoLegible +
					(MundoActual.EsHardmode ? "  ·  Hardmode" : ""),
				0.85f, 700f, 24f);
			mundo.ColorTexto = EstiloTk.TextoSuave;
			mundo.Left.Set(2f, 0f);
			mundo.Top.Set(32f, 0f);
			caja.Append(mundo);

			EtiquetaTk explorado = new EtiquetaTk(
				() => "Explorado: " + MundoActual.PorcentajeExplorado().ToString("0.0") + " %",
				0.9f, 260f, 26f);
			explorado.Left.Set(-262f, 1f);
			explorado.Top.Set(2f, 0f);
			caja.Append(explorado);

			EtiquetaTk posicion = new EtiquetaTk(
				() => "Estás en el tile " + MundoActual.PosicionDelJugador,
				0.8f, 260f, 24f);
			posicion.ColorTexto = EstiloTk.TextoSuave;
			posicion.Left.Set(-262f, 1f);
			posicion.Top.Set(32f, 0f);
			caja.Append(posicion);

			EtiquetaTk mapa = new EtiquetaTk(
				() => Main.mapReady
					? "Mapa del juego listo"
					: "El mapa del juego todavía no está generado",
				0.75f, 300f, 20f);
			mapa.ColorTexto = Main.mapReady ? EstiloTk.TextoSuave : EstiloTk.TextoAviso;
			mapa.Left.Set(-262f, 1f);
			mapa.Top.Set(52f, 0f);
			caja.Append(mapa);
		}
	}
}
