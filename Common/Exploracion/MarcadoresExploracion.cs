using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace TerrakeepMod.Common.Exploracion
{
	/// <summary>
	/// Los marcadores que Terrakeep pinta encima del mapa: los mismos en el mini-mapa del panel y
	/// en el mapa vanilla a pantalla completa.
	/// </summary>
	/// <remarks>
	/// Es estatico a proposito: el panel y la <see cref="CapaMapaExploracion"/> son dos objetos
	/// con vidas distintas (el panel se destruye al cerrarlo, la capa del mapa vive lo que dure
	/// el mod), y los dos tienen que enseñar exactamente lo mismo. Ademas es justo lo que hace
	/// util al boton "Ver en el mapa": los resultados sobreviven al cierre del panel.
	/// </remarks>
	public static class MarcadoresExploracion
	{
		/// <summary>Lo que se esta enseñando ahora mismo. Lo rellena la busqueda.</summary>
		public static List<ResultadoBusqueda> Resultados = new List<ResultadoBusqueda>();

		/// <summary>Color de los marcadores, segun de que eran los resultados.</summary>
		public static Color Color = new Color(255, 210, 120);

		/// <summary>Que se busco, para el rotulo del mapa.</summary>
		public static string Titulo = "";

		public static void Fijar(string titulo, List<ResultadoBusqueda> resultados, Color color)
		{
			Titulo = titulo ?? "";
			Resultados = resultados ?? new List<ResultadoBusqueda>();
			Color = color;
		}

		public static void Limpiar()
		{
			Titulo = "";
			Resultados = new List<ResultadoBusqueda>();
		}

		public static bool HayAlgo => Resultados != null && Resultados.Count > 0;
	}

	/// <summary>
	/// Los iconos que dibuja el mod, generados por codigo.
	/// </summary>
	/// <remarks>
	/// Se generan en vez de traer un .png en <c>Assets\</c> por dos razones practicas: son dos
	/// rombos de 15x15 (un archivo binario en el repositorio para eso no compensa) y asi el color
	/// se decide en tiempo de dibujado. Se crean <b>perezosamente, en el primer dibujado</b>: ahi
	/// estamos con seguridad en el hilo grafico y con el dispositivo ya listo, cosa que no se
	/// puede dar por hecha durante la carga del mod.
	/// </remarks>
	public static class IconosExploracion
	{
		private const int Lado = 15;

		private static Texture2D _rombo;
		private static Texture2D _anillo;

		/// <summary>Rombo relleno con borde negro. Es el marcador de un hallazgo.</summary>
		public static Texture2D Rombo(GraphicsDevice dispositivo)
		{
			if (_rombo == null || _rombo.IsDisposed) {
				_rombo = Generar(dispositivo, true);
			}
			return _rombo;
		}

		/// <summary>Rombo hueco. Marca la posicion del jugador.</summary>
		public static Texture2D Anillo(GraphicsDevice dispositivo)
		{
			if (_anillo == null || _anillo.IsDisposed) {
				_anillo = Generar(dispositivo, false);
			}
			return _anillo;
		}

		public static void Descargar()
		{
			if (_rombo != null && !_rombo.IsDisposed) {
				_rombo.Dispose();
			}
			if (_anillo != null && !_anillo.IsDisposed) {
				_anillo.Dispose();
			}
			_rombo = null;
			_anillo = null;
		}

		/// <summary>
		/// Pinta el rombo pixel a pixel. La "distancia" de un rombo es |dx| + |dy|: los pixeles
		/// hasta el radio son el interior (blanco, para poder teñirlo despues con el color que
		/// haga falta), el borde exterior va en negro para que se lea sobre cualquier fondo del
		/// mapa, que es lo que hacen tambien los iconos de vanilla.
		/// </summary>
		private static Texture2D Generar(GraphicsDevice dispositivo, bool relleno)
		{
			Texture2D textura = new Texture2D(dispositivo, Lado, Lado);
			Color[] pixeles = new Color[Lado * Lado];
			int centro = Lado / 2;

			for (int y = 0; y < Lado; y++) {
				for (int x = 0; x < Lado; x++) {
					int distancia = System.Math.Abs(x - centro) + System.Math.Abs(y - centro);
					Color color;

					if (distancia > centro) {
						color = Color.Transparent;
					}
					else if (distancia > centro - 2) {
						color = new Color(0, 0, 0, 220);
					}
					else if (relleno || distancia > centro - 4) {
						color = Color.White;
					}
					else {
						color = Color.Transparent;
					}

					pixeles[y * Lado + x] = color;
				}
			}

			textura.SetData(pixeles);
			return textura;
		}
	}
}
