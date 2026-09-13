using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Terraria.ModLoader;

namespace TerrakeepMod.Common.Guia
{
	/// <summary>
	/// Lee <c>Assets/guia_progresion.json</c> y lo convierte en el arbol de tramos y pasos que
	/// usa la Guia.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por que los bytes se leen en <c>Load()</c> y se parsean despues</b>: es el mismo
	/// hallazgo que ya dejaron escrito WS4 y <c>CatalogoMejorPrefijo</c> -
	/// <c>TmodFile.GetStream</c> lanza <c>IOException("File not open")</c> en cuanto el
	/// <c>.tmod</c> se cierra, cosa que pasa al terminar la carga del mod.
	/// </para>
	/// <para>
	/// <b>La decision sobre Calamity, y por que es esta.</b> Con Calamity cargado la progresion de
	/// Terraria cambia de arriba abajo: no es "unos cuantos jefes mas", es otro arbol. Su
	/// <c>DownedBossSystem</c> decompilado tiene <b>43 banderas</b> de jefe propias
	/// (<c>downedDesertScourge</c>, <c>downedHiveMind</c>, <c>downedPerforator</c>,
	/// <c>downedProvidence</c>, <c>downedExoMechs</c>...), el primer jefe deja de ser el Ojo de
	/// Cthulhu, y encima el mod mete sus propios modos de dificultad (Revengeance, Death) que
	/// cambian las cifras con las que se mide si estas preparado. Nada de eso se puede verificar
	/// con el mismo rigor que lo de vanilla leyendo el juego decompilado: son datos de diseño del
	/// mod, no condiciones del motor.
	/// <para />
	/// Asi que esta primera fase se queda <b>en vanilla, y lo dice</b>. La Guia detecta Calamity
	/// igual que ya lo hace <c>CatalogoMejorPrefijo</c> (<c>ModLoader.HasMod("CalamityMod")</c>)
	/// y avisa en el panel de que lo que esta leyendo es la progresion de vanilla, en vez de
	/// enseñar un orden que con ese mod puesto seria <b>falso</b>. Inventarse un arbol de
	/// Calamity a medias seria exactamente el "analogo falso solo por completar la lista" que el
	/// estandar de la marca prohibe.
	/// <para />
	/// Lo que si se ha hecho desde el primer dia es dejar el camino abierto: <see cref="AmbitoGuia"/>
	/// existe en el modelo, el <c>.json</c> ya lo lleva por tramo, y <see cref="Tramos"/> filtra
	/// por el. Añadir Calamity sera añadir tramos al <c>.json</c> y banderas a
	/// <see cref="BanderasGuia"/>, no reescribir el cerebro.
	/// </para>
	/// </remarks>
	public static class CatalogoGuia
	{
		/// <summary>Nombre interno real del mod Calamity (el de su carpeta, que es lo que devuelve
		/// <c>Mod.Name</c>) - el mismo que ya usan <c>CatalogoMejorPrefijo</c> y <c>CatalogoBuilds</c>.</summary>
		public const string NombreModCalamity = "CalamityMod";

		private const string Ruta = "Assets/guia_progresion.json";

		private static byte[] _bytes;
		private static List<TramoGuia> _tramos;
		private static string _resumen = "(sin cargar)";
		private static readonly List<string> _avisos = new List<string>();

		/// <summary>true si el arbol ya esta cargado.</summary>
		public static bool Listo => _tramos != null;

		/// <summary>Linea de resumen de la ultima carga, para el log de evidencia.</summary>
		public static string Resumen => _resumen;

		/// <summary>Problemas encontrados al cargar (tipos de requisito sin reconocer, pasos sin
		/// ningun requisito obligatorio...). Vacio = todo limpio.</summary>
		public static IEnumerable<string> Avisos => _avisos;

		/// <summary>true si Calamity esta cargado en esta partida.</summary>
		public static bool HayCalamity => ModLoader.HasMod(NombreModCalamity);

		/// <summary>Los tramos que aplican a esta partida, ya ordenados.</summary>
		public static List<TramoGuia> Tramos {
			get {
				List<TramoGuia> salida = new List<TramoGuia>();
				if (_tramos == null) {
					return salida;
				}

				bool calamity = HayCalamity;
				for (int i = 0; i < _tramos.Count; i++) {
					TramoGuia tramo = _tramos[i];
					if (tramo.Ambito == AmbitoGuia.Ambos
						|| (tramo.Ambito == AmbitoGuia.Calamity && calamity)
						|| (tramo.Ambito == AmbitoGuia.Vanilla && !calamity)) {
						salida.Add(tramo);
						continue;
					}

					// Con Calamity puesto, los tramos de vanilla SE SIGUEN ENSEÑANDO: el arbol de
					// vanilla no desaparece por instalar el mod, solo deja de ser el orden
					// completo. Lo que hace el panel es decirlo con todas las letras (ver
					// ContenidoGuia.ConstruirAvisoCalamity) en vez de quedarse en blanco.
					if (tramo.Ambito == AmbitoGuia.Vanilla && calamity) {
						salida.Add(tramo);
					}
				}

				salida.Sort((a, b) => a.Orden.CompareTo(b.Orden));
				return salida;
			}
		}

		/// <summary>Lee los bytes del .json de dentro del .tmod. Hay que llamarlo durante la carga
		/// del mod, con el archivo todavia abierto.</summary>
		public static void LeerArchivo(Mod mod)
		{
			try {
				if (!mod.FileExists(Ruta)) {
					RegistroGuia.Aviso(Terrakeep.LogTag + " Guia: no se encontro " + Ruta + " dentro del .tmod.");
					return;
				}
				_bytes = mod.GetFileBytes(Ruta);
			}
			catch (Exception ex) {
				RegistroGuia.Error(Terrakeep.LogTag + " Guia: fallo leyendo " + Ruta + ": " + ex.Message);
			}
		}

		/// <summary>Parsea lo ya leido. Se llama desde <c>PostSetupContent</c>, como el resto de
		/// catalogos del mod.</summary>
		public static void Construir()
		{
			_tramos = null;
			_avisos.Clear();

			if (_bytes == null) {
				_resumen = "sin datos (no se pudo leer " + Ruta + ")";
				return;
			}

			try {
				JObject raiz = JObject.Parse(Encoding.UTF8.GetString(_bytes));
				List<TramoGuia> tramos = new List<TramoGuia>();

				JArray arrayTramos = raiz["tramos"] as JArray;
				if (arrayTramos != null) {
					foreach (JToken t in arrayTramos) {
						TramoGuia tramo = LeerTramo(t as JObject);
						if (tramo != null) {
							tramos.Add(tramo);
						}
					}
				}

				tramos.Sort((a, b) => a.Orden.CompareTo(b.Orden));
				_tramos = tramos;

				int pasos = 0;
				int requisitos = 0;
				int implementados = 0;
				for (int i = 0; i < tramos.Count; i++) {
					if (tramos[i].Implementado) {
						implementados++;
					}
					pasos += tramos[i].Pasos.Count;
					for (int j = 0; j < tramos[i].Pasos.Count; j++) {
						requisitos += tramos[i].Pasos[j].Requisitos.Count;
					}
				}

				_resumen = tramos.Count + " tramos (" + implementados + " con requisitos evaluables), " +
					pasos + " pasos, " + requisitos + " requisitos, " + _avisos.Count + " avisos";
			}
			catch (Exception ex) {
				_tramos = null;
				_resumen = "fallo parseando " + Ruta + ": " + ex.Message;
				RegistroGuia.Error(Terrakeep.LogTag + " Guia: " + _resumen);
			}
		}

		private static TramoGuia LeerTramo(JObject nodo)
		{
			if (nodo == null) {
				return null;
			}

			TramoGuia tramo = new TramoGuia {
				Clave = Cadena(nodo, "clave"),
				Orden = Entero(nodo, "orden", 0),
				Ambito = Ambito(Cadena(nodo, "ambito")),
				JefeFinal = Entero(nodo, "jefeFinal", 0),
				Implementado = Booleano(nodo, "implementado", false),
				Opcional = Booleano(nodo, "opcional", false)
			};

			// El jefe que cierra el tramo de la maldad del mundo depende del mundo REAL, no de una
			// preferencia: Terraria genera corrupcion o carmesi (WorldGen.crimson) y ahi cambia el
			// jefe. Se lee del juego, no se supone.
			int carmesi = Entero(nodo, "jefeFinalCarmesi", 0);
			if (carmesi > 0 && Terraria.WorldGen.crimson) {
				tramo.JefeFinal = carmesi;
			}

			JArray pasos = nodo["pasos"] as JArray;
			if (pasos != null) {
				int orden = 0;
				foreach (JToken p in pasos) {
					PasoGuia paso = LeerPaso(p as JObject, tramo.Clave, ++orden);
					if (paso != null) {
						tramo.Pasos.Add(paso);
					}
				}
			}

			if (tramo.Implementado && tramo.Pasos.Count == 0) {
				_avisos.Add("el tramo \"" + tramo.Clave + "\" se declara implementado pero no tiene ningun paso");
			}

			return tramo;
		}

		private static PasoGuia LeerPaso(JObject nodo, string tramo, int orden)
		{
			if (nodo == null) {
				return null;
			}

			PasoGuia paso = new PasoGuia {
				Clave = Cadena(nodo, "clave"),
				Tramo = tramo,
				Zona = Cadena(nodo, "zona"),
				Capa = Capa(Cadena(nodo, "capa")),
				Jefe = Entero(nodo, "jefe", 0)
			};

			JArray requisitos = nodo["requisitos"] as JArray;
			bool hayObligatorio = false;
			if (requisitos != null) {
				foreach (JToken r in requisitos) {
					RequisitoGuia requisito = LeerRequisito(r as JObject, paso.Clave);
					if (requisito == null) {
						continue;
					}
					paso.Requisitos.Add(requisito);
					if (!requisito.Recomendado) {
						hayObligatorio = true;
					}
				}
			}

			if (!hayObligatorio) {
				// Un paso sin requisitos obligatorios no se completaria NUNCA y dejaria la guia
				// atascada en el para siempre. Es un error de datos, y se dice.
				_avisos.Add("el paso \"" + paso.Clave + "\" (tramo " + tramo +
					") no tiene ningun requisito obligatorio: nunca se daria por hecho");
			}

			return paso;
		}

		private static RequisitoGuia LeerRequisito(JObject nodo, string paso)
		{
			if (nodo == null) {
				return null;
			}

			string bruto = Cadena(nodo, "tipo");
			RequisitoGuia requisito = new RequisitoGuia {
				TipoBruto = bruto,
				Tipo = Tipo(bruto),
				Valor = Entero(nodo, "valor", 1),
				Id = Entero(nodo, "id", 0),
				Cantidad = Entero(nodo, "cantidad", 1),
				Bandera = Cadena(nodo, "bandera"),
				Recomendado = Booleano(nodo, "recomendado", false)
			};

			JArray ids = nodo["ids"] as JArray;
			if (ids != null) {
				List<int> lista = new List<int>();
				foreach (JToken t in ids) {
					lista.Add((int)t);
				}
				requisito.Ids = lista.ToArray();
			}

			if (requisito.Tipo == TipoRequisito.Desconocido) {
				_avisos.Add("requisito de tipo desconocido \"" + bruto + "\" en el paso \"" + paso +
					"\": se enseñara como no evaluable, nunca como cumplido");
			}
			if (requisito.Tipo == TipoRequisito.Bandera && !BanderasGuia.Existe(requisito.Bandera)) {
				_avisos.Add("bandera desconocida \"" + requisito.Bandera + "\" en el paso \"" + paso + "\"");
			}

			return requisito;
		}

		// -------------------------------------------------------------------------------------

		private static TipoRequisito Tipo(string texto)
		{
			switch (texto) {
				case "cristales_vida": return TipoRequisito.CristalesVida;
				case "vida_maxima": return TipoRequisito.VidaMaxima;
				case "defensa": return TipoRequisito.Defensa;
				case "npcs_pueblo": return TipoRequisito.NpcsPueblo;
				case "npc": return TipoRequisito.Npc;
				case "npc_activo": return TipoRequisito.NpcActivo;
				case "objeto": return TipoRequisito.Objeto;
				case "objeto_cualquiera": return TipoRequisito.ObjetoCualquiera;
				case "dano_arma": return TipoRequisito.DanoArma;
				case "gancho": return TipoRequisito.Gancho;
				case "bandera": return TipoRequisito.Bandera;
				default: return TipoRequisito.Desconocido;
			}
		}

		private static AmbitoGuia Ambito(string texto)
		{
			switch (texto) {
				case "calamity": return AmbitoGuia.Calamity;
				case "ambos": return AmbitoGuia.Ambos;
				default: return AmbitoGuia.Vanilla;
			}
		}

		private static CapaMundo Capa(string texto)
		{
			switch (texto) {
				case "superficie": return CapaMundo.Superficie;
				case "subterraneo": return CapaMundo.Subterraneo;
				case "cavernas": return CapaMundo.Cavernas;
				case "infierno": return CapaMundo.Infierno;
				default: return CapaMundo.Cualquiera;
			}
		}

		private static string Cadena(JObject nodo, string clave)
		{
			JToken token = nodo[clave];
			return token == null || token.Type == JTokenType.Null ? "" : token.ToString();
		}

		private static int Entero(JObject nodo, string clave, int porDefecto)
		{
			JToken token = nodo[clave];
			return token == null || token.Type == JTokenType.Null ? porDefecto : (int)token;
		}

		private static bool Booleano(JObject nodo, string clave, bool porDefecto)
		{
			JToken token = nodo[clave];
			return token == null || token.Type == JTokenType.Null ? porDefecto : (bool)token;
		}

		public static void Descargar()
		{
			_bytes = null;
			_tramos = null;
			_avisos.Clear();
			_resumen = "(descargado)";
			BanderasGuia.Descargar();
		}
	}
}
