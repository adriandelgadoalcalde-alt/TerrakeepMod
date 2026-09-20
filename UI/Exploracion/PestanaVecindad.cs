using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.UI.Guia;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Exploracion
{
	/// <summary>
	/// <b>Idea 5 del catálogo de funciones ("Planificador de felicidad de NPCs"):</b> la felicidad
	/// REAL de cada NPC de pueblo activo ahora mismo, leyendo <c>Main.ShopHelper</c> - el mismo
	/// motor con el que vanilla calcula lo que un vecino te contesta a "¿cómo te sientes aquí?" -
	/// para todos a la vez, sin tener que hablar uno a uno.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por qué es de solo lectura, nunca mueve ni reasigna a nadie.</b> Investigado ANTES de
	/// escribir código (disciplina de dos fases; mismo criterio real ya aplicado por el proyecto
	/// hermano TerrakeepTrainer con el personaje real: nunca tocar datos de partida sin poder
	/// verificarlo con cuidado). <c>ShopHelper.ProcessMood</c> (decompilado,
	/// <c>Terraria.GameContent.ShopHelper</c>) resuelve la preferencia de bioma de cada NPC con
	/// <c>BiomePreferenceListTrait.ModifyShopPrice</c>, que llama a
	/// <c>preference.Biome.IsInBiome(info.player)</c> - el bioma del JUGADOR ahora mismo, leído de
	/// sus propios flags de zona (<c>Player.ZoneForest</c> y compañía, ya calculados por el motor
	/// cada fotograma en la posición REAL del jugador). No hay ninguna sobrecarga pública que
	/// acepte "calcula el bioma como si estuvieras en la posición X" - la única forma de leer la
	/// felicidad que tendría un NPC en OTRA casa sería teletransportar de verdad al jugador (o
	/// reimplementar desde cero la detección de bioma del motor, con su propio riesgo real de
	/// desincronizarse de la versión real si vanilla la cambia). <b>LÍMITE REAL</b>: por eso esta
	/// pestaña muestra la felicidad de cada NPC EXACTAMENTE como la calcula el juego ahora mismo
	/// (precisa de verdad para los que están cerca; los lejanos llevan un aviso honesto en vez de
	/// fingir precisión que no hay), y no propone recolocaciones automáticas - hacerlo bien
	/// exigiría la capacidad de "simular" una posición sin moverla de verdad, que no existe como
	/// API seguraaquí.
	/// </para>
	/// <para>
	/// <b>Radio de confianza.</b> <see cref="RadioTilesFiable"/> (60 tiles, generoso: una casa
	/// vanilla completa cabe de sobra) - dentro de ese radio del NPC, se asume que el jugador está
	/// literalmente en su casa o muy cerca, así que el bioma que lee el motor SÍ es el real de esa
	/// vivienda. Más lejos, el informe puede estar leyendo el bioma de donde esté el jugador AHORA,
	/// no el de la casa del NPC - se avisa en vez de callarlo.
	/// </para>
	/// </remarks>
	public class PestanaVecindad : UIElement
	{
		/// <summary>Tiles dentro de los cuales se confía en que el bioma leído es el real de la
		/// casa del NPC (ver el XMLdoc de la clase).</summary>
		private const float RadioTilesFiable = 60f;

		private const int FotogramasEntreRefrescos = 30;

		private UIPanel _caja;
		private UIList _lista;
		private UIScrollbar _scroll;
		private EtiquetaTk _resumen;
		private int _contadorRefresco;
		private int _totalNpcs;

		public PestanaVecindad()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			_resumen = new EtiquetaTk(() => Idiomas.Texto("Exploracion.Vecindad.Resumen", _totalNpcs),
				0.8f, 700f, 22f);
			_resumen.ColorTexto = EstiloTk.TextoSuave;
			Append(_resumen);

			_caja = new UIPanel();
			_caja.Width.Set(0f, 1f);
			_caja.Top.Set(28f, 0f);
			_caja.Height.Set(-28f, 1f);
			_caja.BackgroundColor = EstiloTk.FondoCaja;
			_caja.BorderColor = new Color(0, 0, 0, 0);
			_caja.SetPadding(6f);
			Append(_caja);

			_lista = new UIList();
			_lista.Width.Set(-24f, 1f);
			_lista.Height.Set(0f, 1f);
			_lista.ListPadding = 6f;
			// Mismo motivo real que ya documentan ContenidoGuia/EditorPrefijoTk: List.Sort no es
			// estable y UIList lo usa por defecto - con varios NPC reales la lista saldría
			// barajada.
			_lista.ManualSortMethod = elementos => { };
			_caja.Append(_lista);

			_scroll = new UIScrollbar();
			_scroll.Width.Set(16f, 0f);
			_scroll.Height.Set(0f, 1f);
			_scroll.HAlign = 1f;
			_caja.Append(_scroll);
			_lista.SetScrollbar(_scroll);

			Refrescar();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (++_contadorRefresco >= FotogramasEntreRefrescos) {
				_contadorRefresco = 0;
				Refrescar();
			}
		}

		/// <summary>Vuelve a leer todos los NPC de pueblo activos y su felicidad real. Público para
		/// que la autoprueba pueda forzarlo sin esperar el temporizador.</summary>
		public void Refrescar()
		{
			_lista.Clear();
			_totalNpcs = 0;

			Player jugador = Main.LocalPlayer;
			if (jugador == null) {
				return;
			}

			List<NPC> vecinos = new List<NPC>();
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active || !npc.townNPC) {
					continue;
				}
				if (NPCID.Sets.IsTownPet[npc.type]) {
					continue;
				}
				vecinos.Add(npc);
			}

			// Orden estable por nombre real - nunca por el orden interno de Main.npc[], que cambia
			// de una partida a otra sin ningún significado para el jugador.
			vecinos.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

			_totalNpcs = vecinos.Count;
			if (vecinos.Count == 0) {
				_lista.Add(NuevaLinea(() => Idiomas.Texto("Exploracion.Vecindad.Ninguno"), EstiloTk.TextoSuave, 0.8f));
				return;
			}

			foreach (NPC npc in vecinos) {
				AnadirFilaNpc(jugador, npc);
			}
		}

		private void AnadirFilaNpc(Player jugador, NPC npc)
		{
			ShoppingSettings ajustes = Main.ShopHelper.GetShoppingSettings(jugador, npc);
			float distanciaTiles = Vector2.Distance(jugador.Center, npc.Center) / 16f;
			bool fiable = distanciaTiles <= RadioTilesFiable;
			bool sinCasa = npc.homeTileX < 0 && npc.homeTileY < 0;

			// Verde/rojo/gris con el mismo criterio real ya centralizado en EstiloTk (idea 8/
			// FilaRequisitoTk): mas barato que el precio base = contento, mas caro = descontento.
			Color colorPrecio = ajustes.PriceAdjustment < 0.999 ? EstiloTk.Correcto
				: (ajustes.PriceAdjustment > 1.001 ? EstiloTk.Peligro : EstiloTk.Neutro);

			string nombre = npc.FullName;
			string porcentaje = Idiomas.Texto("Exploracion.Vecindad.Precio", (int)System.Math.Round(ajustes.PriceAdjustment * 100.0));

			_lista.Add(NuevaLinea(() => nombre + "  ·  " + porcentaje, colorPrecio, 0.8f));

			string informe = string.IsNullOrEmpty(ajustes.HappinessReport)
				? Idiomas.Texto("Exploracion.Vecindad.SinInforme")
				: ajustes.HappinessReport;
			ParrafoTk parrafoInforme = new ParrafoTk(() => informe, 0.7f);
			parrafoInforme.ColorTexto = EstiloTk.TextoSuave;
			_lista.Add(parrafoInforme);

			if (sinCasa) {
				_lista.Add(NuevaLinea(() => Idiomas.Texto("Exploracion.Vecindad.SinCasa"), EstiloTk.TextoAviso, 0.68f));
			}
			else if (!fiable) {
				_lista.Add(NuevaLinea(() => Idiomas.Texto("Exploracion.Vecindad.Lejos", (int)distanciaTiles),
					EstiloTk.TextoAviso, 0.68f));
			}

			UIElement hueco = new UIElement();
			hueco.Width.Set(0f, 1f);
			hueco.Height.Set(6f, 0f);
			_lista.Add(hueco);
		}

		private static UIElement NuevaLinea(System.Func<string> texto, Color color, float escala)
		{
			ParrafoTk linea = new ParrafoTk(texto, escala);
			linea.ColorTexto = color;
			return linea;
		}

		/// <summary>Cuántos NPC de pueblo se están enseñando ahora mismo. Lo lee la autoprueba.</summary>
		public int TotalNpcsParaPrueba => _totalNpcs;
	}
}
