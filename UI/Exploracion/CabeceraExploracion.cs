using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
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
		/// <summary>Ancho real de la columna derecha (Explorado / Estas en el tile / Mapa listo),
		/// el mismo numero que ya usaban sus tres <c>Left.Set(-262f, 1f)</c>, ahora con nombre para
		/// poder calcular cuanto puede ocupar la columna izquierda sin invadirla.</summary>
		private const float AnchoColumnaDerecha = 262f;

		/// <summary>Hueco real entre las dos columnas.</summary>
		private const float Separacion = 10f;

		public CabeceraExploracion()
		{
			Width.Set(0f, 1f);
			// 98 y no 84: al bajar "mapa" (ver mas abajo, arreglo real de espaciado) la columna
			// derecha necesita 88px reales desde el borde superior de esta cabecera (10 de
			// padding + 78 de contenido) mas los 10 de padding inferior. Con 84 se quedaba corto
			// y "mapa" se saldria por debajo del propio marco de la cabecera. OJO: este numero
			// esta DUPLICADO a proposito en ContenidoExploracion.AltoCabecera (UIElement no puede
			// preguntarle su alto a un hijo antes de construirlo) - si se vuelve a tocar este
			// valor, tocar tambien el de alli.
			Height.Set(98f, 0f);

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
			//
			// Ancho en PORCENTAJE (columna izquierda entera, hasta donde empieza la columna
			// derecha) y no 640/700 px fijos: con esos numeros fijos, a 800x720 (la ventana mas
			// estrecha) las dos cajas medían mas que el hueco real que dejaba la columna derecha
			// (reservada con AnchoColumnaDerecha mas abajo) y se solapaban con "Explorado"/"Estas
			// en el tile" - encontrado por la autopruena de espaciado ampliada (13-sep-2026), en
			// las cuatro combinaciones a 800x720. AnchoColumnaDerecha+Separacion es exactamente el
			// hueco que le deja libre la columna derecha, calculado una sola vez para no repetir
			// el numero.
			float anchoColumnaIzquierda = -(AnchoColumnaDerecha + Separacion);

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Titulo"), 1.05f, 640f, 30f);
			titulo.Width.Set(anchoColumnaIzquierda, 1f);
			titulo.Left.Set(2f, 0f);
			titulo.Top.Set(0f, 0f);
			caja.Append(titulo);

			EtiquetaTk mundo = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.LineaMundo",
					MundoActual.Nombre, MundoActual.TamanoLegible, MundoActual.ModoDeJuegoLegible) +
					(MundoActual.EsHardmode ? "  ·  " + Idiomas.Texto("Exploracion.Hardmode") : ""),
				0.85f, 700f, 24f);
			mundo.Width.Set(anchoColumnaIzquierda, 1f);
			mundo.ColorTexto = EstiloTk.TextoSuave;
			mundo.Left.Set(2f, 0f);
			mundo.Top.Set(32f, 0f);
			caja.Append(mundo);

			EtiquetaTk explorado = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.Explorado",
					MundoActual.PorcentajeExplorado().ToString("0.0")),
				0.9f, 260f, 26f);
			explorado.Left.Set(-AnchoColumnaDerecha, 1f);
			explorado.Top.Set(2f, 0f);
			caja.Append(explorado);

			EtiquetaTk posicion = new EtiquetaTk(
				() => Idiomas.Texto("Exploracion.EstasEn", MundoActual.PosicionDelJugador),
				0.8f, 260f, 24f);
			posicion.ColorTexto = EstiloTk.TextoSuave;
			posicion.Left.Set(-AnchoColumnaDerecha, 1f);
			posicion.Top.Set(32f, 0f);
			caja.Append(posicion);

			EtiquetaTk mapa = new EtiquetaTk(
				() => Idiomas.Texto(Main.mapReady
					? "Exploracion.MapaListo"
					: "Exploracion.MapaSinGenerar"),
				0.75f, 300f, 20f);
			mapa.ColorTexto = Main.mapReady ? EstiloTk.TextoSuave : EstiloTk.TextoAviso;
			mapa.Left.Set(-AnchoColumnaDerecha, 1f);
			// 58 y no 52: "posicion" (Top=32, alto 24) termina en y=56 - con 52 "mapa" empezaba 4px
			// ANTES de que terminara, un solape real y fijo (no dependia de la resolucion: paso
			// en las SEIS combinaciones probadas) encontrado por la autopruena de espaciado
			// ampliada (13-sep-2026).
			mapa.Top.Set(58f, 0f);
			caja.Append(mapa);
		}
	}
}
