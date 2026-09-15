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
				// Un tramo OPCIONAL nunca se convierte en "tu objetivo ahora mismo": el juego no lo
				// exige, y bloquear el camino obligatorio detras de un jefe opcional (por tener
				// menor Orden, p.ej. la Reina Abeja antes que Esqueletron) seria justo lo contrario
				// de "opcional". Tiene su propio hueco aparte, ver <see cref="PasoOpcionalActual"/>.
				if (!t.Implementado || t.Pasos.Count == 0 || t.Opcional) {
					continue;
				}

				PasoGuia paso = PrimerPasoPendiente(t);
				if (paso != null) {
					tramo = t;
					return paso;
				}
			}
			return null;
		}

		/// <summary>
		/// El objetivo OPCIONAL de ahora mismo: el primer paso pendiente del primer tramo opcional
		/// (por Orden) que todavia no este superado. Nunca sustituye al objetivo obligatorio - vive
		/// en su propio hueco de la interfaz - pero sin esto los pasos que se escriben para un
		/// tramo opcional (dano_arma, objeto recomendado...) se quedarian sin ningun sitio donde
		/// enseñarse: la hoja de ruta solo da el resumen en dos lineas del tramo, nunca sus pasos.
		/// </summary>
		public static PasoGuia PasoOpcionalActual(out TramoGuia tramo)
		{
			tramo = null;
			if (!EstadoJugadorGuia.HayPartida) {
				return null;
			}

			List<TramoGuia> tramos = CatalogoGuia.Tramos;
			for (int i = 0; i < tramos.Count; i++) {
				TramoGuia t = tramos[i];
				if (!t.Implementado || t.Pasos.Count == 0 || !t.Opcional) {
					continue;
				}

				PasoGuia paso = PrimerPasoPendiente(t);
				if (paso != null) {
					tramo = t;
					return paso;
				}
			}
			return null;
		}

		/// <summary>
		/// true si el tramo esta CERRADO del todo: implementado, con pasos, y el ultimo de ellos ya
		/// completado. Es la misma formula que ya usaba <see cref="TramosPorDelante"/> solo para los
		/// opcionales, sacada aqui para que <c>HitosSystem</c> (capturas automaticas de hito) la
		/// reutilice tal cual en vez de duplicarla: un tramo obligatorio y uno opcional se dan por
		/// "cerrado" exactamente igual, solo cambia si bloquean o no el objetivo de ahora mismo.
		/// </summary>
		public static bool TramoSuperado(TramoGuia t)
		{
			return t != null && t.Implementado && t.Pasos.Count > 0 &&
				EvaluadorGuia.PasoCompletado(t.Pasos[t.Pasos.Count - 1]);
		}

		/// <summary>
		/// El primer paso PENDIENTE de un tramo, o null si ya esta superado del todo. Comun a
		/// <see cref="PasoActual"/> y <see cref="PasoOpcionalActual"/>.
		/// </summary>
		/// <remarks>
		/// Un paso YA SUPERADO no se vuelve a abrir solo porque un requisito de mas atras deje de
		/// cumplirse en vivo. Es un caso real, no de laboratorio: te quitas la armadura del
		/// prehardmode al conseguir una mejor, sueltas el arma inicial del inventario, cambias de
		/// arma entre un jefe y el siguiente dentro del MISMO tramo... y todo eso son requisitos de
		/// pasos que ya diste por superados hace tiempo. Un paso cuyo UNICO requisito obligatorio es
		/// una bandera (persistida, el motor nunca la vuelve a poner a false) cierra ese paso PARA
		/// SIEMPRE en cuanto se cumple una vez - a diferencia de "dano_arma" o "defensa", que se
		/// releen en vivo y pueden volver a fallar sin que eso signifique retroceder de verdad.
		/// <para />
		/// Por eso el "suelo" del tramo no es solo su ULTIMO paso (ese era el primer arreglo, y
		/// bastaba mientras cada tramo tenia como mucho un paso de arma y uno de jefe): es el paso
		/// anclado-por-bandera MAS AVANZADO que ya este cumplido, sea o no el ultimo. Se vio hacer
		/// falta de verdad al ampliar un tramo a CUATRO pasos (Cultista + Torres, dos jefes seguidos
		/// en el mismo tramo): matar al Cultista y quitarte el arma antes de ir a las torres volvia
		/// a enseñar "arma para el Cultista", con el Cultista llevando rato muerto.
		/// </remarks>
		private static PasoGuia PrimerPasoPendiente(TramoGuia t)
		{
			int suelo = -1;
			for (int j = 0; j < t.Pasos.Count; j++) {
				if (EstaAncladoPorBandera(t.Pasos[j]) && EvaluadorGuia.PasoCompletado(t.Pasos[j])) {
					suelo = j;
				}
			}

			if (suelo == t.Pasos.Count - 1) {
				// El paso anclado mas avanzado es el ULTIMO del tramo: el tramo entero se da por
				// hecho, sin mirar nada de mas atras (el caso ya conocido).
				return null;
			}

			for (int j = suelo + 1; j < t.Pasos.Count; j++) {
				if (!EvaluadorGuia.PasoCompletado(t.Pasos[j])) {
					return t.Pasos[j];
				}
			}
			return null;
		}

		/// <summary>
		/// true si TODOS los requisitos obligatorios del paso son de tipo <see cref="TipoRequisito.Bandera"/>
		/// (y hay al menos uno). Un paso asi, una vez cumplido, no se puede "des-cumplir" nunca -
		/// el motor no vuelve a poner esa bandera a false - a diferencia de un paso que dependa de
		/// daño de arma, defensa o cualquier otro campo que se relee en vivo.
		/// </summary>
		private static bool EstaAncladoPorBandera(PasoGuia paso)
		{
			bool hayObligatorio = false;
			for (int i = 0; i < paso.Requisitos.Count; i++) {
				RequisitoGuia r = paso.Requisitos[i];
				if (r.Recomendado) {
					continue;
				}
				hayObligatorio = true;
				if (r.Tipo != TipoRequisito.Bandera) {
					return false;
				}
			}
			return hayObligatorio;
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
					// Los tramos OPCIONALES no cuentan para "desde": PasoActual los salta siempre
					// (nunca son "el tramo activo"), asi que tampoco deben empujar el punto de
					// partida de la hoja de ruta - si contaran, un opcional implementado con Orden
					// alto (p.ej. JefesOpcionalesTardios, Orden 10) esconderia tramos obligatorios
					// de Orden menor que siguieran pendientes.
					if (tramos[i].Implementado && !tramos[i].Opcional && tramos[i].Orden > desde) {
						desde = tramos[i].Orden;
					}
				}
			}

			for (int i = 0; i < tramos.Count; i++) {
				TramoGuia t = tramos[i];

				if (t.Opcional) {
					// Un opcional IMPLEMENTADO (con requisitos evaluables de verdad) se enseña
					// mientras no este superado - se comprueba con su propia bandera, igual que
					// cualquier otro tramo - y deja de aparecer en cuanto el jugador lo cierra,
					// pase lo que pase con su Orden. Uno SIN IMPLEMENTAR (todavia mapa puro) no
					// tiene datos con los que decidirlo, asi que se enseña siempre.
					bool superado = TramoSuperado(t);
					if (!superado) {
						salida.Add(t);
					}
					continue;
				}

				// Un tramo OBLIGATORIO sin implementar (todavia mapa puro, sin requisitos
				// evaluables) nunca se sabe dar por hecho - no hay datos que leer del motor para
				// decidirlo, a proposito. Por eso se enseña SIEMPRE en la hoja de ruta, pase lo
				// que pase con "desde": si solo se mirara el Orden, en cuanto el camino obligatorio
				// adelanta su numero (ya paso con Moon Lord, visto en una captura real: la columna
				// quedo completamente vacia) desapareceria para siempre. Uno ya IMPLEMENTADO sigue
				// dependiendo solo de "desde", porque para esos si hay una bandera real que
				// demuestra si estan superados.
				if (t.Orden > desde || !t.Implementado) {
					salida.Add(t);
				}
			}
			return salida;
		}

		/// <summary>
		/// La linea de direccion del paso: el bioma/zona, si el paso apunta a una capa concreta si
		/// hay que bajar/subir o ya estas en ella, y (solo para la Mazmorra, ver
		/// <see cref="LadoHorizontalDelPaso"/>) si esta a tu izquierda o a tu derecha. Todo con
		/// fronteras y posiciones REALES del motor, nunca una coordenada.
		/// </summary>
		public static string Direccion(PasoGuia paso)
		{
			if (paso == null) {
				return "";
			}

			string zona = paso.ZonaLegible();
			string baseTexto;
			if (paso.Capa == CapaMundo.Cualquiera || !EstadoJugadorGuia.HayPartida) {
				baseTexto = zona;
			}
			else {
				CapaMundo aqui = EstadoJugadorGuia.CapaDelJugador();
				if (aqui == paso.Capa) {
					baseTexto = Idiomas.Texto("Guia.Direccion.YaEstas", zona);
				}
				else {
					baseTexto = aqui < paso.Capa
						? Idiomas.Texto("Guia.Direccion.Baja", zona)
						: Idiomas.Texto("Guia.Direccion.Sube", zona);
				}
			}

			string lado = LadoHorizontalDelPaso(paso);
			return lado == null ? baseTexto : Idiomas.Texto("Guia.Direccion.ConLado", baseTexto, lado);
		}

		/// <summary>Tolerancia, en tiles, por debajo de la cual no se dice "izquierda" ni "derecha":
		/// ya estas lo bastante cerca en horizontal como para que un lado no signifique nada util
		/// (y para que el aviso no titubee entre los dos según te muevas un paso).</summary>
		private const float ToleranciaHorizontalTiles = 100f;

		/// <summary>
		/// Izquierda/derecha REAL hacia el objetivo del paso, o null si no hay ninguna posicion
		/// fiable del motor para ese paso (la inmensa mayoria de zonas: ver el porque abajo).
		/// </summary>
		/// <remarks>
		/// <b>Por que solo la Mazmorra, de momento.</b> <c>Main.dungeonX</c> es un campo REAL del
		/// motor, fijado por el propio generador de mundo y guardado/leido sin condicion alguna en
		/// todo formato de <c>.wld</c> (<c>WorldFile.cs</c>, escritura en la linea ~1262, lectura en
		/// ~2021 y ~3376): un ancla horizontal exacta y siempre disponible para cualquier mundo,
		/// nuevo o viejo. La Jungla, el Templo Lihzahrd y el bioma de Nieve NO tienen un equivalente:
		/// <c>GenVars.jungleOriginX</c> (<c>WorldBuilding/GenVars.cs</c>) existe, pero es una
		/// variable de PASE DE GENERACION - se pone a 0 al empezar `WorldGen.jungle()` y solo vale
		/// mientras el mundo se esta generando en ESTE proceso; en un mundo ya existente que se
		/// carga en una sesion nueva (el caso normal) esa clase ni siquiera se ha tocado, asi que
		/// leerla mentiria. Confirmarlo con el codigo real de <c>WorldGen.cs</c> (lineas ~8053-8252)
		/// evito construir una brujula horizontal falsa sobre un dato que no sobrevive a cerrar el
		/// juego. La forma honesta de saber donde esta la Jungla/Nieve en un mundo YA CARGADO es
		/// muestrear tiles de verdad (el mismo patron de bajo nivel que ya usa
		/// <see cref="Common.Exploracion.BuscadorMundo"/> con <c>Main.tile.GetData&lt;T&gt;()</c>),
		/// cacheando el resultado una vez por partida en vez de cada fotograma - trabajo real,
		/// pendiente y documentado en la bitacora, no un vacio por descuido.
		/// </remarks>
		private static string LadoHorizontalDelPaso(PasoGuia paso)
		{
			if (paso.Zona != "Mazmorra" || !EstadoJugadorGuia.HayPartida) {
				return null;
			}

			float difTiles = Main.dungeonX - EstadoJugadorGuia.Jugador.Center.X / 16f;
			if (System.Math.Abs(difTiles) <= ToleranciaHorizontalTiles) {
				return null;
			}
			return difTiles < 0f
				? Idiomas.Texto("Guia.Direccion.Izquierda")
				: Idiomas.Texto("Guia.Direccion.Derecha");
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
