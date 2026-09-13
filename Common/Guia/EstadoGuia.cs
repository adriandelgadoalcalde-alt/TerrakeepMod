using System.Collections.Generic;
using Terraria;
using TerrakeepMod.Common.Ajustes;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// La brujula: dado el arbol y la partida real, dice <b>cual es el objetivo de ahora mismo</b>,
	/// como de preparado estas para el y que direccion general hay que coger.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Brujula, no GPS.</b> Lo unico que sale de aqui sobre "donde" es una CAPA del mundo
	/// (superficie / subterraneo / cavernas / infierno) comparada con la capa en la que estas, y
	/// esas fronteras son las del propio juego (<c>Main.worldSurface</c>, <c>Main.rockLayer</c>,
	/// <c>Main.UnderworldLayer</c>). Nunca una coordenada, nunca "el cofre esta en X,Y": eso
	/// convertiria la exploracion en una lista de la compra, que es justo lo que no se quiere.
	/// </para>
	/// <para>
	/// El objetivo actual es <b>el primer paso no completado</b> del primer tramo no completado.
	/// No hay estado guardado en ninguna parte: se recalcula de la partida cada vez que se pide,
	/// asi que no se puede quedar desincronizado ni hace falta migrar nada entre versiones. El
	/// coste es recorrer unos pocos pasos y leer campos del jugador, lo mismo que ya paga
	/// cualquier <c>EtiquetaTk</c> del panel en cada fotograma.
	/// </para>
	/// </remarks>
	public static class EstadoGuia
	{
		/// <summary>El paso en el que esta el jugador ahora mismo, o null si no queda ninguno (todo
		/// lo implementado esta hecho).</summary>
		public static PasoGuia PasoActual()
		{
			TramoGuia tramo;
			return PasoActual(out tramo);
		}

		/// <summary>Igual, devolviendo tambien su tramo.</summary>
		public static PasoGuia PasoActual(out TramoGuia tramo)
		{
			tramo = null;
			if (!EstadoJugadorGuia.HayPartida) {
				return null;
			}

			List<TramoGuia> tramos = CatalogoGuia.Tramos;
			for (int i = 0; i < tramos.Count; i++) {
				TramoGuia t = tramos[i];
				if (!t.Implementado || t.Pasos.Count == 0) {
					continue;
				}

				// Un tramo YA SUPERADO no se vuelve a abrir solo porque un paso de mas atras deje
				// de cumplirse en vivo. Es un caso real, no de laboratorio: te quitas la armadura
				// del prehardmode al conseguir una mejor, sueltas el arma inicial del inventario...
				// y todo eso son requisitos de pasos que ya diste por superados hace tiempo. El
				// ULTIMO paso de cada tramo es, por convencion del propio .json, el que cierra el
				// tramo de verdad (su requisito suele ser la bandera del jefe, que el motor nunca
				// pone a false otra vez), asi que si ese ya esta cumplido el tramo entero se da por
				// hecho sin mirar los de mas atras. Mientras el tramo siga EN CURSO, sí importa cual
				// de sus pasos anteriores falta, para poder señalar el que toca de verdad.
				if (EvaluadorGuia.PasoCompletado(t.Pasos[t.Pasos.Count - 1])) {
					continue;
				}

				for (int j = 0; j < t.Pasos.Count; j++) {
					if (!EvaluadorGuia.PasoCompletado(t.Pasos[j])) {
						tramo = t;
						return t.Pasos[j];
					}
				}
			}
			return null;
		}

		/// <summary>Los pasos del tramo actual con su estado, para poder enseñar el camino entero y
		/// no solo el escalon de al lado.</summary>
		public static List<PasoGuia> PasosDelTramoActual(out TramoGuia tramo)
		{
			PasoActual(out tramo);
			return tramo != null ? tramo.Pasos : new List<PasoGuia>();
		}

		/// <summary>
		/// Lo que viene despues: los tramos que quedan por delante del actual, con su nombre y su
		/// resumen. Es la parte de "hoja de ruta" - no mide nada, solo dice hacia donde va el
		/// juego para que nunca haya un "y ahora que".
		/// </summary>
		public static List<TramoGuia> TramosPorDelante(TramoGuia actual)
		{
			List<TramoGuia> salida = new List<TramoGuia>();
			List<TramoGuia> tramos = CatalogoGuia.Tramos;

			int desde;
			if (actual != null) {
				desde = actual.Orden;
			}
			else {
				// Sin tramo activo puede ser que no haya partida, o que TODOS los tramos
				// IMPLEMENTADOS ya esten superados (PasoActual devuelve null en los dos casos). En
				// el segundo, "lo que viene despues" no puede volver a listar desde el principio -
				// se vio literalmente en una captura real: con Esqueletron recien cerrado, la hoja
				// de ruta empezaba otra vez en "Antes del primer jefe". El punto de partida real es
				// el Orden mas alto entre los tramos implementados (esten o no terminados), no 0.
				desde = 0;
				for (int i = 0; i < tramos.Count; i++) {
					if (tramos[i].Implementado && tramos[i].Orden > desde) {
						desde = tramos[i].Orden;
					}
				}
			}

			for (int i = 0; i < tramos.Count; i++) {
				if (tramos[i].Orden > desde) {
					salida.Add(tramos[i]);
				}
			}
			return salida;
		}

		/// <summary>
		/// La linea de direccion del paso: el bioma/zona y, si el paso apunta a una capa concreta,
		/// si hay que bajar, subir o ya estas en ella. Todo con las fronteras reales del mundo.
		/// </summary>
		public static string Direccion(PasoGuia paso)
		{
			if (paso == null) {
				return "";
			}

			string zona = paso.ZonaLegible;
			if (paso.Capa == CapaMundo.Cualquiera || !EstadoJugadorGuia.HayPartida) {
				return zona;
			}

			CapaMundo aqui = EstadoJugadorGuia.CapaDelJugador();
			if (aqui == paso.Capa) {
				return Idiomas.Texto("Guia.Direccion.YaEstas", zona);
			}
			return aqui < paso.Capa
				? Idiomas.Texto("Guia.Direccion.Baja", zona)
				: Idiomas.Texto("Guia.Direccion.Sube", zona);
		}

		/// <summary>
		/// El medidor de preparacion frente a un jefe, explicado con numeros REALES de esta
		/// partida: su vida y su defensa ya escaladas al modo de juego, el daño de tu mejor arma
		/// pasado por tu propio personaje, y cuantos golpes sale eso con la formula del motor
		/// (<c>daño - defensa*0.5</c>, minimo 1).
		/// <para />
		/// Es la pieza que contesta "¿por que no me esta saliendo?" con datos en vez de con un
		/// "sube de nivel": si tu espada le quita 1 por golpe, se ve en el acto que el problema no
		/// es que juegues mal.
		/// </summary>
		public static string LecturaDeJefe(int tipoNpc)
		{
			int vida, dano, defensa;
			if (tipoNpc <= 0 || !EvaluadorGuia.StatsDeJefe(tipoNpc, out vida, out dano, out defensa)) {
				return "";
			}

			string nombreJefe = EvaluadorGuia.NombreDeNpc(tipoNpc);
			string nombreArma;
			int danoArma = EstadoJugadorGuia.DanoDelMejorArma(out nombreArma);

			string cabecera = Idiomas.Texto("Guia.Jefe.Stats", nombreJefe, vida, defensa, dano,
				MundoActualModo());

			if (danoArma <= 0) {
				return cabecera + "\n" + Idiomas.Texto("Guia.Jefe.SinArma");
			}

			// La formula real de NPC.HitModifiers.GetDamage: el daño recibido por un NPC se reduce
			// en defensa * DefenseEffectiveness, que para los NPC es SIEMPRE 0,5 (campo real
			// "DefenseEffectiveness = MultipliableFloat.One * 0.5f"), con un minimo de 1.
			int porGolpe = danoArma - (int)(defensa * 0.5f);
			if (porGolpe < 1) {
				porGolpe = 1;
			}
			int golpes = (vida + porGolpe - 1) / porGolpe;

			return cabecera + "\n" + Idiomas.Texto("Guia.Jefe.TuArma", nombreArma, danoArma, porGolpe, golpes);
		}

		/// <summary>Nombre del modo de juego real de esta partida, para citarlo junto a las cifras
		/// del jefe (son distintas en experto y en maestro).</summary>
		public static string MundoActualModo()
		{
			if (Main.masterMode) {
				return Idiomas.Texto("Guia.Modo.Maestro");
			}
			if (Main.expertMode) {
				return Idiomas.Texto("Guia.Modo.Experto");
			}
			if (Main.GameMode == 3) {
				return Idiomas.Texto("Guia.Modo.Viaje");
			}
			return Idiomas.Texto("Guia.Modo.Normal");
		}
	}
}
