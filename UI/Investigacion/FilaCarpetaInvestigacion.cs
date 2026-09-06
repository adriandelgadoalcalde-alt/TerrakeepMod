using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using TerrakeepMod.Common.Investigacion;

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
		/// <summary>Alto de una fila. Fijo, para que la lista sea regular.</summary>
		public const float Alto = 26f;

		private const float SangriaPorNivel = 14f;
		private const float AnchoBarra = 54f;
		private const float AnchoRecuento = 68f;

		public readonly CarpetaInvestigacion Carpeta;

		/// <summary>La marca el panel: true si es la carpeta que se esta viendo a la derecha.</summary>
		public bool Seleccionada;

		/// <summary>Clic izquierdo sobre la fila.</summary>
		public event Action<CarpetaInvestigacion> AlPulsar;

		public FilaCarpetaInvestigacion(CarpetaInvestigacion carpeta)
		{
			Carpeta = carpeta;
			Width.Set(0f, 1f);
			Height.Set(Alto, 0f);

			OnLeftClick += (evento, elemento) => {
				if (AlPulsar != null) {
					AlPulsar(Carpeta);
				}
			};
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

			float anchoTextoDisponible = dim.Width - sangria - 16f - AnchoRecuento - AnchoBarra - 12f;
			int caracteres = Math.Max(6, (int)(anchoTextoDisponible / 7.2f));
			string nombre = EstiloInvestigacion.Acortar(Carpeta.Nombre, caracteres);

			Color colorNombre = Carpeta.Hechos >= Carpeta.Total && Carpeta.Total > 0
				? EstiloInvestigacion.Hecho
				: Color.White;
			Utils.DrawBorderString(spriteBatch, nombre,
				new Vector2(dim.X + sangria + 16f, dim.Y + 3f), colorNombre, 0.85f);

			// Recuento y barra, pegados al borde derecho.
			string recuento = Carpeta.Hechos + "/" + Carpeta.Total;
			Vector2 tamano = FontAssets.MouseText.Value.MeasureString(recuento) * 0.75f;
			float xBarra = dim.X + dim.Width - AnchoBarra - 4f;
			Utils.DrawBorderString(spriteBatch, recuento,
				new Vector2(xBarra - 8f - tamano.X, dim.Y + 4f),
				EstiloInvestigacion.ColorDeEstado(Carpeta.Hechos, Carpeta.Total), 0.75f);

			Rectangle canal = new Rectangle((int)xBarra, (int)(dim.Y + 9f), (int)AnchoBarra, 8);
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
