namespace TerrakeepMod.Common.Personaje
{
	/// <summary>
	/// Paridad con Terrakeep escritorio 3.3.0 (commit <c>09195ce0</c>, "Editor de objeto: controles
	/// rapidos +10/+100/MAX respetando el maxStack real"): la aritmetica de los tres botones rapidos
	/// del editor de cantidad, sin nada de Terraria/FNA para poder probarla en
	/// <c>TerrakeepMod.Tests</c> (mismo patron que <c>ReflowVertical</c> y <c>GramaticaBusqueda</c>,
	/// enlazado tal cual desde <c>TerrakeepMod.LogicaPura</c>).
	/// <para />
	/// Se porta el COMPORTAMIENTO, no el codigo WPF: en escritorio el tope salia de un catalogo
	/// extraido de <c>Item.cs</c> (<c>vanilla_max_stack.json</c> + <c>stats.maxStack</c> de Calamity)
	/// porque alli no hay juego cargado. Dentro del juego el tope es <c>Item.maxStack</c> del propio
	/// objeto vivo - el dato real, de vanilla o de cualquier mod, sin catalogo que mantener. Lo unico
	/// que hay que garantizar aqui es lo mismo que en escritorio: sumar nunca pasa del maximo real,
	/// nunca baja de 1 y nunca desborda un <c>int</c>.
	/// </summary>
	public static class CantidadRapida
	{
		/// <summary>Los dos saltos fijos de escritorio ("+10" y "+100").</summary>
		public const int PasoCorto = 10;
		public const int PasoLargo = 100;

		/// <summary>Tope real saneado: un <c>maxStack</c> menor que 1 (dato roto de un mod) se trata
		/// como 1, igual que un objeto no apilable.</summary>
		public static int Maximo(int maxStack)
		{
			return maxStack < 1 ? 1 : maxStack;
		}

		/// <summary>Suma <paramref name="delta"/> a <paramref name="actual"/> acotado a
		/// [1, <see cref="Maximo"/>]. Se hace en <c>long</c> para que un <c>maxStack</c> enorme de
		/// algun mod mas un salto no pueda dar la vuelta a negativo.</summary>
		public static int Sumar(int actual, int delta, int maxStack)
		{
			long tope = Maximo(maxStack);
			long nuevo = (long)actual + delta;
			if (nuevo < 1) {
				nuevo = 1;
			}
			if (nuevo > tope) {
				nuevo = tope;
			}
			return (int)nuevo;
		}

		/// <summary>true si todavia cabe al menos una unidad mas: es cuando los tres botones rapidos
		/// tienen algo que hacer (con la pila ya llena se deshabilitan, en vez de "funcionar" sin
		/// cambiar nada).</summary>
		public static bool PuedeSubir(int actual, int maxStack)
		{
			return actual < Maximo(maxStack);
		}
	}
}
