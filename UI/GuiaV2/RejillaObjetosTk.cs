using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;
using Terrakeep.Core.Guia.V2;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.GuiaV2;
using TerrakeepMod.UI.Libreria;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.GuiaV2
{
	/// <summary>
	/// Rejilla de sprites de objetos (las piezas de una etapa de la escalera de equipo): cada celda
	/// es el sprite real sobre una ranura, con una marca verde si ya lo tienes. Pasar el raton dice
	/// el nombre, el papel y la nota de la fuente; pulsar abre la ficha "cómo conseguirlo".
	/// Envuelve en filas segun el ancho real.
	/// </summary>
	public class RejillaObjetosTk : UIElement
	{
		public const float Lado = 40f;
		private const float Hueco = 4f;

		public sealed class Celda
		{
			public string Ref;
			public int Tipo;
			public string Ayuda;
		}

		private readonly List<Celda> _celdas = new List<Celda>();
		private int _sobre = -1;
		private float _anchoUltimo = -1f;

		public RejillaObjetosTk(IEnumerable<OpcionEquipo> opciones, Dictionary<string, string> leyenda)
		{
			Width.Set(0f, 1f);
			Height.Set(Lado, 0f);
			foreach (OpcionEquipo o in opciones) {
				_celdas.Add(new Celda { Ref = o.Ref, Tipo = GuiaV2Sistema.TipoObjeto(o.Ref), Ayuda = AyudaDe(o, leyenda) });
			}
		}

		public RejillaObjetosTk(IEnumerable<string> refs)
		{
			Width.Set(0f, 1f);
			Height.Set(Lado, 0f);
			foreach (string r in refs) {
				_celdas.Add(new Celda { Ref = r, Tipo = GuiaV2Sistema.TipoObjeto(r), Ayuda = "" });
			}
		}

		public int Cuantas => _celdas.Count;
		public IReadOnlyList<Celda> Celdas => _celdas;

		private static string AyudaDe(OpcionEquipo o, Dictionary<string, string> leyenda)
		{
			List<string> partes = new List<string>();
			if (!string.IsNullOrEmpty(o.Rol)) partes.Add(o.Rol);
			if (!string.IsNullOrEmpty(o.Conjunto)) partes.Add(o.Conjunto);
			if (!string.IsNullOrEmpty(o.Nota)) partes.Add(GuiaV2Sistema.PlanoLocal(o.Nota));
			foreach (string m in o.Marcas) {
				string clave = m.Length > 0 ? m.Substring(0, 1) : m;
				string sig;
				if (leyenda != null && leyenda.TryGetValue(clave, out sig)) {
					partes.Add(m + ": " + sig);
				}
			}
			return string.Join("\n", partes);
		}

		private int PorFila(float ancho) => Math.Max(1, (int)((ancho + Hueco) / (Lado + Hueco)));

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			float ancho = GetInnerDimensions().Width;
			if (ancho > 0f && Math.Abs(ancho - _anchoUltimo) > 0.5f) {
				_anchoUltimo = ancho;
				int filas = (_celdas.Count + PorFila(ancho) - 1) / PorFila(ancho);
				float alto = Math.Max(1, filas) * (Lado + Hueco) - Hueco;
				if (Math.Abs(Height.Pixels - alto) > 0.1f) {
					Height.Set(alto, 0f);
					if (Parent != null) Parent.Recalculate();
				}
			}
			_sobre = IsMouseHovering ? CeldaEn(Main.MouseScreen) : -1;
			if (_sobre >= 0) {
				Main.LocalPlayer.mouseInterface = true;
				Celda c = _celdas[_sobre];
				string nombre = GuiaV2Sistema.NombreObjeto(c.Ref);
				int tienes = c.Tipo > 0 ? ProveedorEstadoGuiaV2Mod.CuantosPoseeDe(Main.LocalPlayer, c.Tipo) : 0;
				// Estado SIEMPRE explicito (lo tienes / te falta), no solo cuando lo tienes.
				// Sprite + ID de cada objeto, como la escalera de la guia HTML del usuario ("ID 65" en
				// vanilla, "CalamityMod/Nombre" en Calamity); en la rejilla solo cabe el sprite.
				string id = c.Ref != null && c.Ref.StartsWith("Terraria/", StringComparison.Ordinal) && c.Tipo > 0 && c.Tipo < ItemID.Count
					? "ID " + c.Tipo : c.Ref;
				BotonTk.PedirTooltip(nombre + "  (" + Idiomas.Texto(tienes > 0 ? "GuiaV2.Objeto.LoTienes" : "GuiaV2.Objeto.TeFalta") + ")" +
					"\n" + id +
					(string.IsNullOrEmpty(c.Ayuda) ? "" : "\n" + c.Ayuda) + "\n" + Idiomas.Texto("GuiaV2.Enlace.ObjetoCorto"));
			}
		}

		/// <summary>Centro en pantalla de la celda indicada (autoprueba).</summary>
		public Vector2 CentroCelda(int i)
		{
			CalculatedStyle d = GetInnerDimensions();
			int pf = PorFila(d.Width);
			return new Vector2(d.X + (i % pf) * (Lado + Hueco) + Lado / 2f, d.Y + (i / pf) * (Lado + Hueco) + Lado / 2f);
		}

		private int CeldaEn(Vector2 punto)
		{
			CalculatedStyle d = GetInnerDimensions();
			int pf = PorFila(d.Width);
			float x = punto.X - d.X, y = punto.Y - d.Y;
			if (x < 0 || y < 0) return -1;
			int col = (int)(x / (Lado + Hueco)), fila = (int)(y / (Lado + Hueco));
			if (col >= pf || x - col * (Lado + Hueco) > Lado || y - fila * (Lado + Hueco) > Lado) return -1;
			int i = fila * pf + col;
			return i < _celdas.Count ? i : -1;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			int i = CeldaEn(evt.MousePosition);
			if (i >= 0) {
				Pulsar(i);
			}
		}

		public void Pulsar(int i)
		{
			SoundEngine.PlaySound(SoundID.MenuTick);
			if (NavegacionGuia.AbrirObjeto != null) {
				NavegacionGuia.AbrirObjeto(_celdas[i].Ref, 1);
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle d = GetInnerDimensions();
			int pf = PorFila(d.Width);
			Texture2D fondo = TextureAssets.InventoryBack.Value;
			for (int i = 0; i < _celdas.Count; i++) {
				Celda c = _celdas[i];
				float x = d.X + (i % pf) * (Lado + Hueco), y = d.Y + (i / pf) * (Lado + Hueco);
				Rectangle r = new Rectangle((int)x, (int)y, (int)Lado, (int)Lado);
				bool tiene = c.Tipo > 0 && ProveedorEstadoGuiaV2Mod.CuantosPoseeDe(Main.LocalPlayer, c.Tipo) > 0;
				Color tinte = i == _sobre ? new Color(200, 220, 255) : (tiene ? new Color(150, 230, 170) : new Color(120, 130, 190));
				spriteBatch.Draw(fondo, r, tinte * 0.9f);
				if (c.Tipo > 0) {
					IconoObjetoTk.Dibujar(spriteBatch, c.Tipo, new Vector2(x + Lado / 2f, y + Lado / 2f), Lado - 10f, Color.White);
				}
				else {
					EscribirTk.Dibujar(spriteBatch, "?", new Vector2(x + Lado / 2f - 5f, y + 8f), EstiloTk.Neutro, 0.9f);
				}
				if (tiene) {
					Rectangle m = new Rectangle(r.Right - 11, r.Y + 3, 8, 8);
					spriteBatch.Draw(TextureAssets.MagicPixel.Value, m, EstiloTk.Correcto);
				}
			}
		}
	}
}
