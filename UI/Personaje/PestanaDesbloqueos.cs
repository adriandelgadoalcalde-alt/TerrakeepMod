using System.Collections.Generic;
using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Pestaña "Desbloqueos": los 13 marcadores permanentes del personaje (ver
	/// <see cref="Desbloqueos"/> para la lista y de donde sale).
	/// <para />
	/// Cada casilla lee y escribe el campo real de <see cref="Player"/> sin intermediarios, asi
	/// que refleja siempre la verdad aunque el juego lo cambie por su cuenta (por ejemplo al
	/// comerse un Pan de Artesano dentro de la partida con el panel abierto).
	/// </summary>
	public class PestanaDesbloqueos : UIElement
	{
		public PestanaDesbloqueos()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Desbloqueos.Titulo", Desbloqueos.Lista.Count),
				0.85f, 900f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(0f, 0f);
			Append(titulo);

			List<Desbloqueo> lista = Desbloqueos.Lista;
			// Dos columnas al 50% del ancho REAL, no de 500 px fijos: con 500 las dos columnas
			// sumaban 980 px y en una ventana de 800 la de la derecha se salia por encima del borde
			// del marco. Visto en una captura real del juego.
			const float FraccionColumna = 0.5f;
			float alto = 32f;
			int porColumna = (lista.Count + 1) / 2;

			for (int i = 0; i < lista.Count; i++) {
				Desbloqueo desbloqueo = lista[i];

				AlternadorTk casilla = new AlternadorTk(() => desbloqueo.Nombre,
					() => desbloqueo.Leer(PersonajeVivo.Jugador),
					valor => desbloqueo.Escribir(PersonajeVivo.Jugador, valor));

				casilla.Ayuda = () => desbloqueo.CampoReal + ": " + desbloqueo.Descripcion;
				casilla.Width.Set(-14f, FraccionColumna);
				casilla.Height.Set(28f, 0f);
				casilla.Left.Set(0f, (i / porColumna) * FraccionColumna);
				casilla.Top.Set(30f + (i % porColumna) * alto, 0f);

				casilla.AlCambiar += valor => {
					Player jugador = PersonajeVivo.Jugador;
					Terrakeep.Instance.Logger.Info(
						$"{Terrakeep.LogTag} Desbloqueo cambiado en vivo: Player.{desbloqueo.CampoReal} " +
						$"-> {valor}. Valor real releido del jugador: {desbloqueo.Leer(jugador)}.");
				};

				Append(casilla);
			}

			EtiquetaTk nota = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Desbloqueos.Nota"),
				0.75f, 900f, 20f);
			nota.ColorTexto = EstiloTk.TextoSuave;
			nota.Left.Set(0f, 0f);
			nota.Top.Set(30f + porColumna * alto + 12f, 0f);
			Append(nota);
		}
	}
}
