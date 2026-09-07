using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using TerrakeepMod.Common.Investigacion;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Investigacion
{
	/// <summary>
	/// Una fila del arbol de carpetas: sangria por nivel, marca de desplegado, nombre, el "x/N"
	/// de lo investigado y una barra fina con el mismo progreso.
	/// <para />
	/// El arbol es el de la Libreria (construido con <c>TerrasavrNative.Core</c>, ver
	/// <see cref="CatalogoInvestigacion"/>), asi que las carpetas se llaman igual aqui, en la
	/// Libreria del mod y en la app de escritorio.
	/// </summary>
	public class FilaCarpetaInvestigacion : UIElement
	{
		/// <summary>Alto MINIMO de una fila (una sola linea de nombre). Ver
		/// <see cref="ActualizarLayout"/>: crece de verdad si el nombre no cabe en una linea, nunca
		/// se queda en un numero fijo que recorte texto.</summary>
		public const float AltoMinimo = 26f;

		private const float EscalaNombre = 0.85f;
		private const float SangriaPorNivel = 14f;
		private const float AnchoBarra = 54f;
		private const float AnchoRecuento = 68f;

		public readonly CarpetaInvestigacion Carpeta;

		/// <summary>La marca el panel: true si es la carpeta que se esta viendo a la derecha.</summary>
		public bool Seleccionada;

		/// <summary>Clic izquierdo sobre la fila.</summary>
		public event Action<CarpetaInvestigacion> AlPulsar;

		/// <summary>Nombre YA envuelto (<see cref="EtiquetaTk.PartirEnLineas"/>) al ancho real de
		/// esta fila. Lo calcula <see cref="ActualizarLayout"/>.</summary>
		private string _nombrePartido;

		/// <summary>Alto real (con la fuente real) que ocupa <see cref="_nombrePartido"/> ya
		/// envuelto. Ver <see cref="ActualizarLayout"/>.</summary>
		private float _altoTexto = AltoMinimo - 6f;

		public FilaCarpetaInvestigacion(CarpetaInvestigacion carpeta)
		{
			Carpeta = carpeta;
			_nombrePartido = carpeta.Nombre;
			Width.Set(0f, 1f);
			Height.Set(AltoMinimo, 0f);

			OnLeftClick += (evento, elemento) => {
				if (AlPulsar != null) {
					AlPulsar(Carpeta);
				}
			};
		}

		/// <summary>
		/// Mide el nombre ENVUELTO (nunca recortado con "..." como antes - el codigo viejo cortaba
		/// por NUMERO DE CARACTERES, ni siquiera medido con la fuente real) al ancho REAL de esta
		/// fila, y devuelve el alto que necesita. Lo llama
		/// <see cref="ContenidoInvestigacion.AjustarAltoFilasCarpeta"/> cada fotograma (el ancho
		/// disponible cambia con la resolucion de la ventana), que es quien de verdad fija
		/// <see cref="UIElement.Height"/> y recoloca la lista - esta fila solo mide.
		/// </summary>
		public float ActualizarLayout()
		{
			CalculatedStyle dim = GetDimensions();
			if (dim.Width <= 0f) {
				// Layout todavia no calculado este fotograma: se reintenta solo, sin tocar nada.
				return Height.Pixels > 0f ? Height.Pixels : AltoMinimo;
			}

			float sangria = 4f + Carpeta.Profundidad * SangriaPorNivel;
			float anchoTextoDisponible = dim.Width - sangria - 16f - AnchoRecuento - AnchoBarra - 12f;
			if (anchoTextoDisponible < 20f) {
				anchoTextoDisponible = 20f;
			}

			_nombrePartido = EtiquetaTk.PartirEnLineas(Carpeta.Nombre, anchoTextoDisponible, EscalaNombre);
			_altoTexto = FontAssets.MouseText.Value.MeasureString(_nombrePartido).Y * EscalaNombre;

			float altoNecesario = _altoTexto + 6f;
			return altoNecesario > AltoMinimo ? altoNecesario : AltoMinimo;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle dim = GetDimensions();
			Rectangle marco = dim.ToRectangle();
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			if (Seleccionada) {
				spriteBatch.Draw(pixel, marco, EstiloInvestigacion.FilaSeleccionada);
			}
			else if (IsMouseHovering) {
				spriteBatch.Draw(pixel, marco, EstiloInvestigacion.FilaSobre);
			}

			if (IsMouseHovering) {
				// Sin esto el clic atraviesa el panel y el jugador ataca o coloca bloques detras.
				Main.LocalPlayer.mouseInterface = true;
			}

			float sangria = 4f + Carpeta.Profundidad * SangriaPorNivel;

			// La marca de desplegar es "+"/"-" y no una flecha bonita a proposito: la fuente del
			// juego solo tiene los caracteres con los que se genero, y uno que no este hace
			// reventar a DynamicSpriteFont al medir la cadena.
			string marca = Carpeta.EsHoja ? "  " : (Carpeta.Desplegada ? "-" : "+");
			Utils.DrawBorderString(spriteBatch, marca,
				new Vector2(dim.X + sangria, dim.Y + 3f), EstiloInvestigacion.SinEmpezar, 0.85f);

			Color colorNombre = Carpeta.Hechos >= Carpeta.Total && Carpeta.Total > 0
				? EstiloInvestigacion.Hecho
				: Color.White;
			Utils.DrawBorderString(spriteBatch, _nombrePartido,
				new Vector2(dim.X + sangria + 16f, dim.Y + 3f), colorNombre, EscalaNombre);

			// Recuento y barra, pegados al borde derecho y centrados verticalmente en el alto REAL
			// de la fila (que puede ser mayor que AltoMinimo si el nombre se envolvio).
			string recuento = Carpeta.Hechos + "/" + Carpeta.Total;
			Vector2 tamano = FontAssets.MouseText.Value.MeasureString(recuento) * 0.75f;
			float xBarra = dim.X + dim.Width - AnchoBarra - 4f;
			float yCentro = dim.Y + dim.Height / 2f;
			Utils.DrawBorderString(spriteBatch, recuento,
				new Vector2(xBarra - 8f - tamano.X, yCentro - tamano.Y / 2f),
				EstiloInvestigacion.ColorDeEstado(Carpeta.Hechos, Carpeta.Total), 0.75f);

			Rectangle canal = new Rectangle((int)xBarra, (int)(yCentro - 4f), (int)AnchoBarra, 8);
			spriteBatch.Draw(pixel, canal, EstiloInvestigacion.CanalBarra);
			if (Carpeta.Total > 0 && Carpeta.Hechos > 0) {
				int relleno = (int)Math.Round(canal.Width * Math.Min(1f, Carpeta.Hechos / (float)Carpeta.Total));
				if (relleno > 0) {
					spriteBatch.Draw(pixel, new Rectangle(canal.X, canal.Y, relleno, canal.Height),
						EstiloInvestigacion.ColorDeEstado(Carpeta.Hechos, Carpeta.Total));
				}
			}
		}
	}
}
