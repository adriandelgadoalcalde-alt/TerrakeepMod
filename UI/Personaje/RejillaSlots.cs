using System.Collections.Generic;
using Terraria;
using Terraria.UI;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Ayudas para colocar rejillas y filas de <see cref="SlotObjetoVanilla"/> sin repetir la
	/// misma aritmetica en cada pestaña.
	/// <para />
	/// Todos los slots apuntan al array VIVO que se les pasa (<c>Player.inventory</c>,
	/// <c>Player.bank.item</c>, <c>Player.armor</c>...), nunca a una copia: arrastrar un objeto
	/// aqui lo mueve de verdad en la partida.
	/// </summary>
	public static class RejillaSlots
	{
		/// <summary>Separacion entre slots, en pixeles a escala 1.</summary>
		public const float Separacion = 2f;

		/// <summary>Paso entre dos slots contiguos para una escala dada.</summary>
		public static float Paso(float escala)
		{
			return 52f * escala + Separacion;
		}

		/// <summary>
		/// Coloca <paramref name="cantidad"/> slots consecutivos en una rejilla.
		/// </summary>
		/// <returns>La lista de slots creados, en el mismo orden que en el array.</returns>
		public static List<SlotObjetoVanilla> Rejilla(UIElement destino, Item[] inventario,
			int primero, int cantidad, int contexto, int columnas, float escala,
			float izquierda, float arriba)
		{
			List<SlotObjetoVanilla> creados = new List<SlotObjetoVanilla>(cantidad);
			float paso = Paso(escala);

			for (int i = 0; i < cantidad; i++) {
				int indice = primero + i;
				if (indice < 0 || indice >= inventario.Length) {
					continue;
				}

				SlotObjetoVanilla slot = new SlotObjetoVanilla(inventario, indice, contexto, escala);
				slot.Left.Set(izquierda + (i % columnas) * paso, 0f);
				slot.Top.Set(arriba + (i / columnas) * paso, 0f);
				destino.Append(slot);
				creados.Add(slot);
			}

			return creados;
		}

		/// <summary>Coloca un solo slot en una posicion concreta.</summary>
		public static SlotObjetoVanilla Uno(UIElement destino, Item[] inventario, int indice,
			int contexto, float escala, float izquierda, float arriba)
		{
			SlotObjetoVanilla slot = new SlotObjetoVanilla(inventario, indice, contexto, escala);
			slot.Left.Set(izquierda, 0f);
			slot.Top.Set(arriba, 0f);
			destino.Append(slot);
			return slot;
		}
	}
}
