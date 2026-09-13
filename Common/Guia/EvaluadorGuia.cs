using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// El cerebro: dado el estado REAL de la partida, dice si cada requisito esta cumplido y con
	/// que numeros.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Todo lo que se enseña sale de aqui, y todo lo de aqui sale del juego en marcha.</b> No
	/// hay ni una cifra guardada sobre el jugador: cada comprobacion vuelve a leer el campo real
	/// del motor (ver <see cref="EstadoJugadorGuia"/>, que cita uno a uno de donde sale cada
	/// dato). Es lo que permite que el panel se actualice solo mientras juegas, sin ningun evento
	/// ni ninguna cache que se pueda quedar desfasada - el mismo criterio con el que se hizo
	/// <c>EtiquetaTk</c>.
	/// </para>
	/// <para>
	/// Un requisito cuyo tipo no se reconozca se marca <see cref="ResultadoRequisito.NoEvaluable"/>
	/// y <b>nunca</b> cuenta como cumplido. Prefiero un "esto no lo se comprobar" visible a un
	/// falso verde: una guia que dice "ya estas listo" sin estarlo es peor que no tener guia.
	/// </para>
	/// </remarks>
	public static class EvaluadorGuia
	{
		/// <summary>Comprueba un requisito contra la partida real.</summary>
		public static ResultadoRequisito Evaluar(RequisitoGuia requisito)
		{
			ResultadoRequisito r = new ResultadoRequisito { Requisito = requisito };
			if (requisito == null) {
				r.NoEvaluable = true;
				r.Linea = Idiomas.Texto("Guia.Req.NoEvaluable", "(null)");
				return r;
			}

			switch (requisito.Tipo) {
				case TipoRequisito.CristalesVida:
					Contar(r, EstadoJugadorGuia.CristalesVida, requisito.Valor, "Guia.Req.CristalesVida");
					break;

				case TipoRequisito.VidaMaxima:
					Contar(r, EstadoJugadorGuia.VidaMaxima, requisito.Valor, "Guia.Req.VidaMaxima");
					break;

				case TipoRequisito.Defensa:
					Contar(r, EstadoJugadorGuia.Defensa, requisito.Valor, "Guia.Req.Defensa");
					break;

				case TipoRequisito.NpcsPueblo:
					Contar(r, EstadoJugadorGuia.NpcsDelPueblo(), requisito.Valor, "Guia.Req.NpcsPueblo");
					break;

				case TipoRequisito.Npc:
					EvaluarNpc(r, requisito);
					break;

				case TipoRequisito.NpcActivo:
					EvaluarNpcActivo(r, requisito);
					break;

				case TipoRequisito.Objeto:
					EvaluarObjeto(r, requisito);
					break;

				case TipoRequisito.ObjetoCualquiera:
					EvaluarObjetoCualquiera(r, requisito);
					break;

				case TipoRequisito.DanoArma:
					EvaluarDanoArma(r, requisito);
					break;

				case TipoRequisito.Gancho:
					EvaluarGancho(r);
					break;

				case TipoRequisito.Bandera:
					EvaluarBandera(r, requisito);
					break;

				default:
					r.NoEvaluable = true;
					r.Linea = Idiomas.Texto("Guia.Req.NoEvaluable",
						string.IsNullOrEmpty(requisito.TipoBruto) ? "?" : requisito.TipoBruto);
					break;
			}

			return r;
		}

		/// <summary>Los requisitos de un paso, ya evaluados, en el orden del .json.</summary>
		public static List<ResultadoRequisito> Evaluar(PasoGuia paso)
		{
			List<ResultadoRequisito> lista = new List<ResultadoRequisito>();
			if (paso == null) {
				return lista;
			}
			for (int i = 0; i < paso.Requisitos.Count; i++) {
				lista.Add(Evaluar(paso.Requisitos[i]));
			}
			return lista;
		}

		/// <summary>
		/// true si el paso se puede dar por hecho: todos sus requisitos <b>obligatorios</b> estan
		/// cumplidos. Los recomendados no bloquean (ver <see cref="RequisitoGuia.Recomendado"/>).
		/// Un paso sin ningun requisito obligatorio no se completa nunca solo: seria un paso que
		/// no mide nada, y el catalogo lo avisa al cargar.
		/// </summary>
		public static bool PasoCompletado(PasoGuia paso)
		{
			if (paso == null) {
				return false;
			}

			bool hayObligatorio = false;
			for (int i = 0; i < paso.Requisitos.Count; i++) {
				RequisitoGuia requisito = paso.Requisitos[i];
				if (requisito.Recomendado) {
					continue;
				}
				hayObligatorio = true;
				if (!Evaluar(requisito).Cumplido) {
					return false;
				}
			}
			return hayObligatorio;
		}

		/// <summary>
		/// Medidor de preparacion de un paso, de 0 a 1.
		/// <para />
		/// Un requisito obligatorio pesa el doble que uno recomendado, y cada uno aporta su
		/// progreso PARCIAL (3 vecinos de 4 son 0,75 de ese requisito, no un cero). Asi la barra
		/// se mueve mientras juegas en vez de dar saltos de todo o nada, que es lo que hace que
		/// se entienda de un vistazo cuanto falta de verdad.
		/// </summary>
		public static float Preparacion(PasoGuia paso, out int cumplidos, out int totalObligatorios)
		{
			cumplidos = 0;
			totalObligatorios = 0;
			if (paso == null || paso.Requisitos.Count == 0) {
				return 0f;
			}

			float suma = 0f;
			float pesoTotal = 0f;

			for (int i = 0; i < paso.Requisitos.Count; i++) {
				RequisitoGuia requisito = paso.Requisitos[i];
				ResultadoRequisito resultado = Evaluar(requisito);

				float peso = requisito.Recomendado ? 1f : 2f;
				pesoTotal += peso;
				suma += peso * Fraccion(resultado);

				if (!requisito.Recomendado) {
					totalObligatorios++;
					if (resultado.Cumplido) {
						cumplidos++;
					}
				}
			}

			return pesoTotal <= 0f ? 0f : suma / pesoTotal;
		}

		private static float Fraccion(ResultadoRequisito resultado)
		{
			if (resultado.Cumplido) {
				return 1f;
			}
			if (resultado.NoEvaluable || resultado.Pedido <= 0) {
				return 0f;
			}
			float f = resultado.Actual / (float)resultado.Pedido;
			return f < 0f ? 0f : (f > 1f ? 1f : f);
		}

		// -------------------------------------------------------------------------------------

		private static void Contar(ResultadoRequisito r, int actual, int pedido, string clave)
		{
			r.Actual = actual;
			r.Pedido = pedido;
			r.Cumplido = actual >= pedido;
			r.Linea = Idiomas.Texto(clave, actual, pedido);
		}

		private static void EvaluarNpc(ResultadoRequisito r, RequisitoGuia requisito)
		{
			bool hay = EstadoJugadorGuia.HayNpc(requisito.Id);
			r.Actual = hay ? 1 : 0;
			r.Pedido = 1;
			r.Cumplido = hay;
			r.Linea = Idiomas.Texto("Guia.Req.Npc", NombreDeNpc(requisito.Id));
		}

		/// <summary>Igual que <see cref="EvaluarNpc"/> pero con el texto honesto para un enemigo
		/// hostil (nunca del pueblo): dice si esta activo en el mundo, no si "vive contigo".</summary>
		private static void EvaluarNpcActivo(ResultadoRequisito r, RequisitoGuia requisito)
		{
			bool hay = EstadoJugadorGuia.HayNpc(requisito.Id);
			r.Actual = hay ? 1 : 0;
			r.Pedido = 1;
			r.Cumplido = hay;
			r.Linea = Idiomas.Texto("Guia.Req.NpcActivo", NombreDeNpc(requisito.Id));
		}

		private static void EvaluarObjeto(ResultadoRequisito r, RequisitoGuia requisito)
		{
			int lleva = EstadoJugadorGuia.CuantosLleva(requisito.Id);
			r.Actual = lleva;
			r.Pedido = requisito.Cantidad;
			r.Cumplido = lleva >= requisito.Cantidad;
			r.Linea = requisito.Cantidad > 1
				? Idiomas.Texto("Guia.Req.ObjetoVarios", NombreDeObjeto(requisito.Id), lleva, requisito.Cantidad)
				: Idiomas.Texto("Guia.Req.Objeto", NombreDeObjeto(requisito.Id));
		}

		private static void EvaluarObjetoCualquiera(ResultadoRequisito r, RequisitoGuia requisito)
		{
			int mejor = 0;
			string nombre = "";
			if (requisito.Ids != null) {
				for (int i = 0; i < requisito.Ids.Length; i++) {
					int lleva = EstadoJugadorGuia.CuantosLleva(requisito.Ids[i]);
					if (lleva > mejor) {
						mejor = lleva;
						nombre = NombreDeObjeto(requisito.Ids[i]);
					}
					if (string.IsNullOrEmpty(nombre) && i == 0) {
						nombre = NombreDeObjeto(requisito.Ids[i]);
					}
				}
			}

			r.Actual = mejor;
			r.Pedido = requisito.Cantidad;
			r.Cumplido = mejor >= requisito.Cantidad;
			r.Linea = Idiomas.Texto("Guia.Req.ObjetoCualquiera", ListaDeObjetos(requisito.Ids), mejor, requisito.Cantidad);
		}

		private static void EvaluarDanoArma(ResultadoRequisito r, RequisitoGuia requisito)
		{
			string nombre;
			int dano = EstadoJugadorGuia.DanoDelMejorArma(out nombre);
			r.Actual = dano;
			r.Pedido = requisito.Valor;
			r.Cumplido = dano >= requisito.Valor;
			r.Linea = string.IsNullOrEmpty(nombre)
				? Idiomas.Texto("Guia.Req.DanoArmaSinArma", requisito.Valor)
				: Idiomas.Texto("Guia.Req.DanoArma", nombre, dano, requisito.Valor);
		}

		private static void EvaluarGancho(ResultadoRequisito r)
		{
			string nombre;
			bool lleva = EstadoJugadorGuia.LlevaGancho(out nombre);
			r.Actual = lleva ? 1 : 0;
			r.Pedido = 1;
			r.Cumplido = lleva;
			r.Linea = lleva
				? Idiomas.Texto("Guia.Req.GanchoSi", nombre)
				: Idiomas.Texto("Guia.Req.GanchoNo");
		}

		private static void EvaluarBandera(ResultadoRequisito r, RequisitoGuia requisito)
		{
			if (!BanderasGuia.Existe(requisito.Bandera)) {
				r.NoEvaluable = true;
				r.Pedido = 1;
				r.Linea = Idiomas.Texto("Guia.Req.NoEvaluable", "bandera " + requisito.Bandera);
				return;
			}

			bool valor = BanderasGuia.Valor(requisito.Bandera);
			r.Actual = valor ? 1 : 0;
			r.Pedido = 1;
			r.Cumplido = valor;
			r.Linea = Idiomas.Texto("Guia.Bandera." + requisito.Bandera);
		}

		// -------------------------------------------------------------------------------------

		/// <summary>
		/// Nombre de un objeto <b>en el idioma del juego</b>, del propio juego. Sale de
		/// <c>ContentSamples.ItemsByType</c>, que es la tabla de muestras que tModLoader rellena
		/// al cargar el contenido: asi el nombre es el mismo que ve el jugador en su tooltip y no
		/// hay que traducir nada en el mod.
		/// </summary>
		public static string NombreDeObjeto(int tipo)
		{
			Item muestra;
			if (tipo > 0 && ContentSamples.ItemsByType.TryGetValue(tipo, out muestra) && muestra != null) {
				return muestra.Name;
			}
			return "#" + tipo;
		}

		/// <summary>Nombre de un NPC, igual, de <c>ContentSamples.NpcsByNetId</c>.</summary>
		public static string NombreDeNpc(int tipo)
		{
			NPC muestra;
			if (ContentSamples.NpcsByNetId.TryGetValue(tipo, out muestra) && muestra != null) {
				return muestra.FullName;
			}
			return "#" + tipo;
		}

		private static string ListaDeObjetos(int[] ids)
		{
			if (ids == null || ids.Length == 0) {
				return "?";
			}

			System.Text.StringBuilder texto = new System.Text.StringBuilder();
			for (int i = 0; i < ids.Length; i++) {
				if (i > 0) {
					texto.Append(" / ");
				}
				texto.Append(NombreDeObjeto(ids[i]));
			}
			return texto.ToString();
		}

		/// <summary>
		/// Vida, daño y defensa REALES de un jefe en ESTA partida, escalados por el propio motor.
		/// </summary>
		/// <remarks>
		/// No es una tabla copiada de ningun sitio: se coge la muestra del NPC
		/// (<c>ContentSamples.NpcsByNetId</c>), se clona y se le llama a su propio
		/// <c>NPC.ScaleStats(jugadores, Main.GameModeInfo, null)</c> - el metodo publico real que
		/// usa el juego cuando el jefe aparece de verdad, con los multiplicadores de modo
		/// (<c>GameModeData</c>: normal x1, experto x2 vida y daño, maestro x3) y el ajuste por
		/// numero de jugadores. Por eso el numero que enseña la guia es el que el jugador se va a
		/// encontrar, y no "la vida del wiki".
		/// </remarks>
		public static bool StatsDeJefe(int tipoNpc, out int vida, out int dano, out int defensa)
		{
			vida = 0;
			dano = 0;
			defensa = 0;

			NPC muestra;
			if (tipoNpc <= 0 || !ContentSamples.NpcsByNetId.TryGetValue(tipoNpc, out muestra) || muestra == null) {
				return false;
			}

			NPC copia = new NPC();
			copia.SetDefaults(tipoNpc);
			copia.ScaleStats(null, Main.GameModeInfo, null);

			vida = copia.lifeMax;
			dano = copia.damage;
			defensa = copia.defense;
			return true;
		}
	}
}
