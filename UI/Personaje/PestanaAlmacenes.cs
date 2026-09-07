using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Libreria.Widgets;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Pestaña "Almacenes": los cuatro contenedores personales del jugador, 40 ranuras cada uno.
	/// <para />
	/// En <see cref="Player"/> no son arrays sueltos sino cuatro <see cref="Chest"/>:
	/// <c>bank</c> (Hucha), <c>bank2</c> (Caja fuerte), <c>bank3</c> (Forja del Defensor) y
	/// <c>bank4</c> (Boveda del Vacio). El array vivo de objetos es <c>.item</c> en cada uno.
	/// <para />
	/// La Boveda del Vacio usa el contexto <c>VoidItem</c> en vez de <c>BankItem</c>, que es lo
	/// que hace el propio juego: tiene su propio fondo de ranura y sus propias reglas.
	/// </summary>
	/// <remarks>
	/// <b>El mini-panel de edicion</b> (<see cref="PanelHerramientasLibreriaTk"/>, el mismo de
	/// Libreria) va a la derecha de la rejilla de 10x4: con <c>Escala</c>=0.9 la rejilla mide
	/// 10*48,8=488px, y el mismo hueco real que ya aprovecha <see cref="PestanaInventario"/> para
	/// su version sigue libre aqui, sin ninguna otra etiqueta que lo dispute (a diferencia de
	/// Inventario, donde la etiqueta de "Ocupacion" obligo a ponerlo DEBAJO en vez de al lado).
	/// </remarks>
	public class PestanaAlmacenes : UIElement
	{
		private const float Escala = 0.9f;

		private readonly List<BotonTk> _botones = new List<BotonTk>();
		private UIElement _rejilla;
		private PanelHerramientasLibreriaTk _herramientas;
		private int _almacenActual;

		public PestanaAlmacenes()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			for (int i = 0; i < PersonajeVivo.ClavesAlmacen.Length; i++) {
				int indice = i;
				BotonTk boton = new BotonTk(PersonajeVivo.NombreAlmacen(i), 0.8f);
				boton.Width.Set(160f, 0f);
				boton.Height.Set(30f, 0f);
				boton.Left.Set(i * 166f, 0f);
				boton.Top.Set(0f, 0f);
				boton.AlPulsar += () => Mostrar(indice);
				_botones.Add(boton);
				Append(boton);
			}

			_rejilla = new UIElement();
			_rejilla.Width.Set(0f, 1f);
			_rejilla.Height.Set(-40f, 1f);
			_rejilla.Top.Set(40f, 0f);
			Append(_rejilla);

			EtiquetaTk resumen = new EtiquetaTk(TextoResumen, 0.8f, 900f, 20f);
			resumen.ColorTexto = EstiloTk.TextoSuave;
			resumen.Left.Set(0f, 0f);
			resumen.Top.Set(40f + 24f + 4f * RejillaSlots.Paso(Escala) + 10f, 0f);
			Append(resumen);

			float derecha = 10f * RejillaSlots.Paso(Escala) + 30f;
			_herramientas = new PanelHerramientasLibreriaTk();
			_herramientas.Left.Set(derecha, 0f);
			_herramientas.Top.Set(40f + 24f, 0f);
			Append(_herramientas);

			Mostrar(0);
		}

		private void Mostrar(int indice)
		{
			_almacenActual = indice;
			for (int i = 0; i < _botones.Count; i++) {
				_botones[i].Activo = i == indice;
			}

			_rejilla.RemoveAllChildren();

			string campo = indice == 0 ? "bank" : "bank" + (indice + 1);
			EtiquetaTk titulo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Almacenes.Titulo",
					PersonajeVivo.NombreAlmacen(indice), PersonajeVivo.SlotsAlmacen, campo),
				0.85f, 600f, 22f);
			titulo.ColorTexto = EstiloTk.TextoSuave;
			titulo.Left.Set(0f, 0f);
			titulo.Top.Set(0f, 0f);
			_rejilla.Append(titulo);

			// La Boveda del Vacio (bank4) tiene contexto propio en vanilla, con su fondo morado.
			int contexto = indice == 3 ? ItemSlot.Context.VoidItem : ItemSlot.Context.BankItem;

			RejillaSlots.Rejilla(_rejilla, PersonajeVivo.ObtenerAlmacen(indice), 0,
				PersonajeVivo.SlotsAlmacen, contexto, 10, Escala, 0f, 24f);

			_rejilla.Recalculate();

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Almacen mostrado: \"{PersonajeVivo.NombreAlmacen(indice)}\" " +
				$"(Player.{campo}.item, {PersonajeVivo.ObtenerAlmacen(indice).Length} ranuras reales), " +
				$"contexto de ItemSlot = {contexto}.");
		}

		private string TextoResumen()
		{
			Item[] almacen = PersonajeVivo.ObtenerAlmacen(_almacenActual);
			int ocupadas = 0;
			for (int i = 0; i < almacen.Length; i++) {
				if (!almacen[i].IsAir) {
					ocupadas++;
				}
			}
			return Idiomas.Texto("Personaje.Almacenes.Ocupacion",
				PersonajeVivo.NombreAlmacen(_almacenActual), ocupadas, almacen.Length);
		}

		/// <summary>Los cuatro botones de almacen tienen texto fijo desde que se construyen; se
		/// vuelve a poner cada fotograma para que cambien con el idioma sin reabrir el panel.</summary>
		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			for (int i = 0; i < _botones.Count; i++) {
				_botones[i].FijarTexto(PersonajeVivo.NombreAlmacen(i));
			}
		}
	}
}
