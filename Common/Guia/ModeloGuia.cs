using System.Collections.Generic;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// A que progresion pertenece un tramo. El discriminador existe <b>desde el primer dia</b>
	/// aunque hoy solo haya datos de vanilla: la decision sobre Calamity (ver la cabecera de
	/// <see cref="CatalogoGuia"/> y la bitacora) es que su arbol se añade como DATOS, no
	/// reescribiendo el cerebro, y eso solo es cierto si el modelo ya sabe distinguirlos.
	/// </summary>
	public enum AmbitoGuia
	{
		/// <summary>Solo cuando NO hay Calamity cargado.</summary>
		Vanilla = 0,

		/// <summary>Solo con Calamity cargado. Hoy no hay ningun tramo asi (trabajo futuro).</summary>
		Calamity = 1,

		/// <summary>Vale en las dos.</summary>
		Ambos = 2
	}

	/// <summary>
	/// Las clases de requisito que el evaluador sabe comprobar contra el estado REAL de la
	/// partida. Es un vocabulario <b>cerrado</b> a proposito.
	/// </summary>
	/// <remarks>
	/// Este es el reparto de responsabilidades del formato de datos, y conviene entenderlo antes
	/// de tocar nada:
	/// <list type="bullet">
	/// <item>el <c>.json</c> dice QUE hace falta (un requisito de este tipo, con estos
	/// parametros) y en que orden;</item>
	/// <item>el C# dice COMO se comprueba, leyendo el juego en marcha.</item>
	/// </list>
	/// Por eso añadir un paso o un tramo nuevo es editar el <c>.json</c>, y solo añadir una
	/// <b>clase nueva</b> de requisito obliga a tocar <see cref="EvaluadorGuia"/>. Un tipo que no
	/// se reconozca no revienta nada: se marca como no evaluable y se dice en el log (nunca se
	/// da por cumplido, que seria mentirle al jugador).
	/// <para />
	/// Ningun requisito guarda numeros que el motor ya sepa (vida de un jefe, defensa de una
	/// pieza de armadura): esos se leen en vivo de <c>ContentSamples</c>. Lo que guarda el
	/// <c>.json</c> son los umbrales de DISEÑO, y los que vienen de una condicion real del motor
	/// llevan citada su fuente en el propio archivo.
	/// </remarks>
	public enum TipoRequisito
	{
		/// <summary>Sin reconocer (tipo desconocido en el .json).</summary>
		Desconocido = 0,

		/// <summary>Cristales de vida consumidos (<c>Player.ConsumedLifeCrystals</c>).</summary>
		CristalesVida,

		/// <summary>Vida maxima (<c>Player.statLifeMax</c>).</summary>
		VidaMaxima,

		/// <summary>Defensa actual (<c>Player.statDefense</c>).</summary>
		Defensa,

		/// <summary>Cuantos NPC del pueblo hay vivos en el mundo.</summary>
		NpcsPueblo,

		/// <summary>Un NPC del pueblo concreto, por su id.</summary>
		Npc,

		/// <summary>Un objeto concreto en el inventario, por id y cantidad.</summary>
		Objeto,

		/// <summary>Cualquiera de una lista de objetos (id alternativos del mismo nivel).</summary>
		ObjetoCualquiera,

		/// <summary>Daño del mejor arma que se lleve encima.</summary>
		DanoArma,

		/// <summary>Llevar encima un gancho de verdad (<c>Main.projHook</c>).</summary>
		Gancho,

		/// <summary>Una bandera permanente del mundo o del jugador (ver <see cref="BanderasGuia"/>).</summary>
		Bandera
	}

	/// <summary>Un requisito suelto de un paso.</summary>
	public class RequisitoGuia
	{
		public TipoRequisito Tipo = TipoRequisito.Desconocido;

		/// <summary>Texto original del tipo, tal cual venia en el .json. Se conserva para poder
		/// decir en el log QUE tipo no se reconocio, en vez de un "Desconocido" mudo.</summary>
		public string TipoBruto = "";

		/// <summary>Umbral numerico (cristales, defensa, vecinos, daño...).</summary>
		public int Valor = 1;

		/// <summary>Id de objeto o de NPC, segun el tipo.</summary>
		public int Id;

		/// <summary>Ids alternativos para <see cref="TipoRequisito.ObjetoCualquiera"/>.</summary>
		public int[] Ids;

		/// <summary>Cantidad pedida para los requisitos de objeto.</summary>
		public int Cantidad = 1;

		/// <summary>Nombre de la bandera para <see cref="TipoRequisito.Bandera"/>.</summary>
		public string Bandera = "";

		/// <summary>
		/// true si el requisito <b>no bloquea</b>: cuenta para el medidor de preparacion y se
		/// enseña, pero el paso se puede dar por hecho sin el.
		/// <para />
		/// Es la diferencia entre "el juego no te deja" y "te vas a llevar un disgusto": la arena
		/// y las pociones son lo segundo, y mentir ahi seria convertir la guia en una lista de
		/// tareas obligatorias, justo lo contrario de lo que se pide.
		/// </summary>
		public bool Recomendado;
	}

	/// <summary>Un paso de la guia: un objetivo concreto, con su porque y sus requisitos.</summary>
	public class PasoGuia
	{
		/// <summary>Clave interna estable. Es la ultima parte de sus claves de localizacion:
		/// <c>Guia.Paso.&lt;Clave&gt;.Titulo</c> / <c>.Porque</c> / <c>.Como</c>.</summary>
		public string Clave = "";

		/// <summary>Clave del tramo al que pertenece (la rellena el catalogo al cargar).</summary>
		public string Tramo = "";

		/// <summary>Clave de zona: <c>Guia.Zona.&lt;Zona&gt;</c>. Es una DIRECCION general (un
		/// bioma, una capa), nunca unas coordenadas.</summary>
		public string Zona = "";

		/// <summary>Capa del mundo hacia la que apunta el paso, para poder decir "estas arriba,
		/// hay que bajar" con las fronteras REALES del mundo. Ver <see cref="CapaMundo"/>.</summary>
		public CapaMundo Capa = CapaMundo.Cualquiera;

		/// <summary>Id de NPC del jefe al que apunta el paso (0 = ninguno). Sirve para sacar del
		/// propio juego su vida/daño/defensa REALES ya escalados al modo de la partida.</summary>
		public int Jefe;

		public readonly List<RequisitoGuia> Requisitos = new List<RequisitoGuia>();

		/// <summary>Titulo del objetivo, traducido. Propiedad y no campo: el catalogo se cachea y
		/// un texto ya resuelto se quedaria congelado en el idioma que hubiera al cargarlo (misma
		/// leccion que ya dejo escrita el arbol de la Libreria).</summary>
		public string Titulo => Idiomas.Texto("Guia.Paso." + Clave + ".Titulo");

		/// <summary>El PORQUE: que mecanica del juego hay detras de este objetivo.</summary>
		public string Porque => Idiomas.Texto("Guia.Paso." + Clave + ".Porque");

		/// <summary>El COMO: por donde se empieza, sin destripar nada.</summary>
		public string Como => Idiomas.Texto("Guia.Paso." + Clave + ".Como");

		/// <summary>Nombre de la zona, traducido.</summary>
		public string ZonaLegible =>
			string.IsNullOrEmpty(Zona) ? "" : Idiomas.Texto("Guia.Zona." + Zona);
	}

	/// <summary>Capas reales del mundo, con las fronteras que usa el propio juego.</summary>
	public enum CapaMundo
	{
		Cualquiera = 0,
		Superficie = 1,
		Subterraneo = 2,
		Cavernas = 3,
		Infierno = 4
	}

	/// <summary>Un tramo de progresion: varios pasos que terminan en un hito (normalmente un jefe).</summary>
	public class TramoGuia
	{
		public string Clave = "";
		public int Orden;
		public AmbitoGuia Ambito = AmbitoGuia.Vanilla;

		/// <summary>Id de NPC del jefe que cierra el tramo (0 = ninguno).</summary>
		public int JefeFinal;

		/// <summary>
		/// false = el tramo esta en el arbol como MAPA (se enseña en "lo que viene despues") pero
		/// todavia no tiene requisitos evaluables.
		/// <para />
		/// Existe para poder entregar el arbol completo de progresion sin fingir que la guia ya
		/// sabe medir tramos que no se han convertido en datos. Lo que no esta implementado se
		/// dice, no se disimula.
		/// </summary>
		public bool Implementado;

		public readonly List<PasoGuia> Pasos = new List<PasoGuia>();

		public string Nombre => Idiomas.Texto("Guia.Tramo." + Clave + ".Nombre");

		/// <summary>Una linea de que va el tramo, para la lista de "lo que viene despues".</summary>
		public string Resumen => Idiomas.Texto("Guia.Tramo." + Clave + ".Resumen");
	}

	/// <summary>Como ha salido la comprobacion de un requisito contra la partida real.</summary>
	public class ResultadoRequisito
	{
		public RequisitoGuia Requisito;

		/// <summary>true si esta cumplido.</summary>
		public bool Cumplido;

		/// <summary>Valor que tiene el jugador ahora mismo (para "3 / 4").</summary>
		public int Actual;

		/// <summary>Valor que hace falta.</summary>
		public int Pedido;

		/// <summary>Linea ya montada y traducida ("Vecinos en el pueblo: 3 de 4").</summary>
		public string Linea = "";

		/// <summary>true si el evaluador no supo comprobarlo (tipo desconocido). Nunca cuenta
		/// como cumplido.</summary>
		public bool NoEvaluable;
	}
}
