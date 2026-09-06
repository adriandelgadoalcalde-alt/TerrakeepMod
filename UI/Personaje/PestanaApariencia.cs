using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Pestaña "Apariencia": peinado, tinte de pelo, variante de piel/genero y los siete colores
	/// del personaje.
	/// <para />
	/// Aqui no hace falta ningun renderizador propio ni ningun "muñeco": el juego ya dibuja al
	/// jugador leyendo justo estos campos, asi que cualquier cambio se ve al momento en la
	/// partida. Es la ventaja gorda de editar en vivo frente a la app de escritorio.
	/// <para />
	/// Detalle real de esta version: <c>Player.hair</c> puede llegar hasta
	/// <c>HairLoader.Count - 1</c> (165 de vanilla mas los peinados que añadan los mods), no hasta
	/// el <c>Main.maxHairStyles</c> de vanilla; y <c>Player.skinVariant</c> tiene 12 valores
	/// (<c>PlayerVariantID.Count</c>), que incluyen los dos de maniqui.
	/// </summary>
	public class PestanaApariencia : UIElement
	{
		private List<int> _sombreadoresTinte;
		private List<string> _nombresTinte;

		public PestanaApariencia()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			PersonajeVivo.CargarTintesPelo(out _sombreadoresTinte, out _nombresTinte);

			ConstruirSelectores();
			ConstruirColores();
		}

		private void ConstruirSelectores()
		{
			SelectorTk peinado = new SelectorTk(() => Idiomas.Texto("Personaje.Apariencia.Peinado"),
				() => Idiomas.Texto("Personaje.Apariencia.PeinadoValor",
					PersonajeVivo.Jugador.hair, PersonajeVivo.TotalPeinados - 1),
				paso => {
					Player jugador = PersonajeVivo.Jugador;
					int total = PersonajeVivo.TotalPeinados;
					jugador.hair = ((jugador.hair + paso) % total + total) % total;
				},
				120f, 10, 360f);
			peinado.Left.Set(0f, 0f);
			peinado.Top.Set(0f, 0f);
			Append(peinado);

			SelectorTk variante = new SelectorTk(() => Idiomas.Texto("Personaje.Apariencia.VarianteEtiqueta"),
				() => PersonajeVivo.NombreVariante(PersonajeVivo.Jugador.skinVariant),
				paso => {
					Player jugador = PersonajeVivo.Jugador;
					int total = PlayerVariantID.Count;
					jugador.skinVariant = ((jugador.skinVariant + paso) % total + total) % total;
				},
				120f, 0, 360f);
			variante.Left.Set(380f, 0f);
			variante.Top.Set(0f, 0f);
			Append(variante);

			SelectorTk tinte = new SelectorTk(() => Idiomas.Texto("Personaje.Apariencia.Tinte"),
				TextoTinte,
				paso => {
					Player jugador = PersonajeVivo.Jugador;
					int total = _sombreadoresTinte.Count;
					int posicion = _sombreadoresTinte.IndexOf(jugador.hairDye);
					if (posicion < 0) {
						posicion = 0;
					}
					posicion = ((posicion + paso) % total + total) % total;
					jugador.hairDye = _sombreadoresTinte[posicion];
				},
				120f, 5, 460f);
			tinte.Left.Set(0f, 0f);
			tinte.Top.Set(34f, 0f);
			Append(tinte);

			EtiquetaTk nota = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Apariencia.NotaTintes", _sombreadoresTinte.Count),
				0.72f, 900f, 18f);
			nota.ColorTexto = EstiloTk.TextoSuave;
			nota.Left.Set(0f, 0f);
			nota.Top.Set(70f, 0f);
			Append(nota);
		}

		private string TextoTinte()
		{
			int sombreador = PersonajeVivo.Jugador.hairDye;
			int posicion = _sombreadoresTinte.IndexOf(sombreador);
			if (posicion < 0) {
				return Idiomas.Texto("Personaje.Apariencia.TinteDesconocido", sombreador);
			}
			return _nombresTinte[posicion];
		}

		private void ConstruirColores()
		{
			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Apariencia.CabeceraColores"),
				0.8f, 700f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(96f, 0f);
			Append(titulo);

			float arriba = 122f;
			float paso = 30f;
			int fila = 0;

			Anadir("Pelo", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.hairColor, c => PersonajeVivo.Jugador.hairColor = c);
			Anadir("Piel", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.skinColor, c => PersonajeVivo.Jugador.skinColor = c);
			Anadir("Ojos", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.eyeColor, c => PersonajeVivo.Jugador.eyeColor = c);
			Anadir("Camisa", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.shirtColor, c => PersonajeVivo.Jugador.shirtColor = c);
			Anadir("CamisetaInterior", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.underShirtColor, c => PersonajeVivo.Jugador.underShirtColor = c);
			Anadir("Pantalones", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.pantsColor, c => PersonajeVivo.Jugador.pantsColor = c);
			Anadir("Zapatos", arriba + paso * fila++,
				() => PersonajeVivo.Jugador.shoeColor, c => PersonajeVivo.Jugador.shoeColor = c);

			EtiquetaTk aviso = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Apariencia.NotaColores"),
				0.75f, 900f, 20f);
			aviso.ColorTexto = EstiloTk.TextoSuave;
			aviso.Left.Set(0f, 0f);
			aviso.Top.Set(arriba + paso * fila + 10f, 0f);
			Append(aviso);
		}

		/// <summary>Añade una fila de color. Recibe la CLAVE de localizacion, no el texto.</summary>
		private void Anadir(string clave, float arriba,
			System.Func<Color> leer, System.Action<Color> escribir)
		{
			FilaColorTk fila = new FilaColorTk(
				() => Idiomas.Texto("Personaje.Apariencia.Color." + clave), leer, escribir);
			fila.Left.Set(0f, 0f);
			fila.Top.Set(arriba, 0f);
			Append(fila);
		}
	}
}
