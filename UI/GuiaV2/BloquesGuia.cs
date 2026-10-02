using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.GuiaV2
{
	/// <summary>
	/// Convierte los bloques de contenido largo de la guia (articulos del manual, fichas de "Estoy
	/// perdido"/"He encontrado algo raro", zonas) en elementos de la UI del juego. Todos los tipos
	/// del modelo (<c>Bloque.tipo</c>): titulo, parrafo, lista, tabla, aviso, cajas/caja, flujo,
	/// esquema y fuentes.
	/// </summary>
	/// <remarks>
	/// Las TABLAS no se pintan como rejilla: en una columna de juego de 500-900 px una tabla de 3
	/// columnas con texto largo seria ilegible. Cada fila se pinta como una tarjeta: la primera
	/// celda de titulo y el resto como "Cabecera: valor". Es la misma informacion, en un formato que
	/// cabe a 1280x720 con cualquier escala de interfaz.
	/// </remarks>
	public static class BloquesGuia
	{
		public static void Anadir(Action<UIElement> anadir, IEnumerable<Bloque> bloques)
		{
			foreach (Bloque b in bloques) {
				UIElement e = Crear(b);
				if (e != null) {
					anadir(e);
				}
			}
		}

		public static UIElement Crear(Bloque b)
		{
			switch (b.Tipo) {
				case "titulo":
					return Texto("**" + b.Texto + "**", 0.9f, EstiloTk.TextoAviso, EstiloTk.TextoAviso);
				case "parrafo":
					return Texto(b.Texto, 0.78f, Color.White, null);
				case "lista": {
					PilaTk pila = new PilaTk { Relleno = 0f, Separacion = 2f };
					for (int i = 0; i < b.Items.Count; i++) {
						string marca = b.Numerada ? (i + 1) + ". " : "• ";
						pila.Append(Texto("**" + marca + "**" + b.Items[i], 0.76f, Color.White, EstiloTk.TextoAviso));
					}
					return pila;
				}
				case "tabla": {
					PilaTk pila = new PilaTk { Relleno = 0f, Separacion = 4f };
					foreach (List<string> fila in b.Filas) {
						PilaTk tarjeta = new PilaTk { Relleno = 6f, Separacion = 1f, Fondo = EstiloTk.FondoCaja * 0.7f };
						for (int c = 0; c < fila.Count; c++) {
							if (c == 0) {
								string cab0 = b.Cabeceras.Count > 0 ? b.Cabeceras[0] : "";
								tarjeta.Append(Texto((cab0.Length > 0 ? "**" + cab0 + ":** " : "") + fila[0], 0.8f, Color.White, EstiloTk.TextoAviso));
							}
							else if (!string.IsNullOrWhiteSpace(fila[c])) {
								string cab = c < b.Cabeceras.Count ? b.Cabeceras[c] : "";
								tarjeta.Append(Texto((cab.Length > 0 ? "**" + cab + ":** " : "") + fila[c], 0.74f, EstiloTk.TextoSuave, EstiloTk.TextoAviso));
							}
						}
						pila.Append(tarjeta);
					}
					return pila;
				}
				case "aviso": {
					Color barra = b.Estilo == "destacado" ? EstiloTk.TextoAviso : (b.Estilo == "suave" ? EstiloTk.Correcto : new Color(150, 205, 255));
					PilaTk caja = new PilaTk { Relleno = 7f, Fondo = EstiloTk.FondoCaja * 0.85f, BarraIzquierda = barra };
					if (!string.IsNullOrEmpty(b.Titulo)) {
						caja.Append(Texto("**" + b.Titulo + "**", 0.8f, barra, barra));
					}
					caja.Append(Texto(b.Texto, 0.76f, Color.White, null));
					return caja;
				}
				case "cajas": {
					PilaTk pila = new PilaTk { Relleno = 0f, Separacion = 5f };
					foreach (Bloque hijo in b.Bloques) {
						UIElement e = Crear(hijo);
						if (e != null) pila.Append(e);
					}
					return pila;
				}
				case "caja": {
					PilaTk caja = new PilaTk { Relleno = 7f, Separacion = 3f, Fondo = EstiloTk.FondoCaja * 0.7f };
					if (!string.IsNullOrEmpty(b.Titulo)) {
						caja.Append(Texto("**" + b.Titulo + "**", 0.82f, Color.White, EstiloTk.TextoAviso));
					}
					if (!string.IsNullOrEmpty(b.Texto)) {
						caja.Append(Texto(b.Texto, 0.76f, Color.White, null));
					}
					foreach (Bloque hijo in b.Bloques) {
						UIElement e = Crear(hijo);
						if (e != null) caja.Append(e);
					}
					return caja;
				}
				case "flujo": {
					StringBuilder sb = new StringBuilder();
					for (int i = 0; i < b.Items.Count; i++) {
						if (i > 0) sb.Append("  **→**  ");
						sb.Append(b.Items[i]);
					}
					PilaTk caja = new PilaTk { Relleno = 7f, Fondo = EstiloTk.FondoCaja * 0.6f };
					if (!string.IsNullOrEmpty(b.Titulo)) caja.Append(Texto("**" + b.Titulo + "**", 0.8f, Color.White, EstiloTk.TextoAviso));
					caja.Append(Texto(sb.ToString(), 0.76f, Color.White, EstiloTk.TextoAviso));
					return caja;
				}
				case "esquema": {
					PilaTk caja = new PilaTk { Relleno = 7f, Separacion = 0f, Fondo = new Color(15, 20, 40) * 0.85f };
					if (!string.IsNullOrEmpty(b.Titulo)) caja.Append(Texto("**" + b.Titulo + "**", 0.8f, Color.White, EstiloTk.TextoAviso));
					foreach (string linea in b.Items) {
						caja.Append(Texto(linea, 0.72f, EstiloTk.TextoSuave, null));
					}
					return caja;
				}
				case "fuentes":
					return Fuentes(b.Fuentes);
				default:
					return string.IsNullOrEmpty(b.Texto) ? null : Texto(b.Texto, 0.78f, Color.White, null);
			}
		}

		public static TextoRicoTk Texto(string marcado, float escala, Color color, Color? negrita)
		{
			string fijo = marcado ?? "";
			TextoRicoTk t = new TextoRicoTk(() => fijo, escala);
			t.ColorTexto = color;
			if (negrita.HasValue) {
				t.ColorNegrita = negrita.Value;
			}
			return t;
		}

		/// <summary>Lista de fuentes (wiki oficial): cada una abre su pagina en el navegador.</summary>
		public static UIElement Fuentes(List<Fuente> fuentes)
		{
			if (fuentes == null || fuentes.Count == 0) {
				return null;
			}
			PilaTk pila = new PilaTk { Relleno = 0f, Separacion = 1f };
			pila.Append(Texto("**" + Idiomas.Texto("GuiaV2.Fuentes") + "**", 0.7f, EstiloTk.TextoSuave, EstiloTk.TextoSuave));
			foreach (Fuente f in fuentes) {
				pila.Append(new EnlaceWebTk(f.TextoVisible(), f.UrlResuelta()));
			}
			return pila;
		}
	}

	/// <summary>Enlace a una pagina web (fuentes): pinta el texto y abre la URL al pulsar.</summary>
	public class EnlaceWebTk : UIElement
	{
		private readonly string _url;
		private readonly TextoRicoTk _texto;

		public EnlaceWebTk(string texto, string url)
		{
			_url = url;
			Width.Set(0f, 1f);
			_texto = new TextoRicoTk(() => "» " + texto, 0.66f);
			_texto.SinEnlaces = true;
			_texto.ColorTexto = TextoRicoTk.ColorEnlace;
			Append(_texto);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Math.Abs(Height.Pixels - _texto.Height.Pixels) > 0.1f) {
				Height.Set(_texto.Height.Pixels, 0f);
				Recalculate();
			}
			_texto.ColorTexto = IsMouseHovering ? Color.White : TextoRicoTk.ColorEnlace;
			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
				BotonTk.PedirTooltip(Idiomas.Texto("GuiaV2.Enlace.Web", _url));
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			SoundEngine.PlaySound(SoundID.MenuTick);
			try {
				Utils.OpenToURL(_url);
			}
			catch (Exception) {
				// Sin navegador: no hay nada mas que hacer, la URL ya se ve en el tooltip.
			}
		}
	}
}
