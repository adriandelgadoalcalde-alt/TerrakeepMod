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
	/// <b>Calamity, actualizado el 15-sep-2026: ya tiene su propio arbol, no solo el hueco para
	/// el.</b> La decision original (ver el historial de git de este comentario si hace falta el
	/// texto exacto) fue quedarse solo en vanilla porque nada de Calamity se podia verificar con
	/// el mismo rigor que el juego decompilado: <c>DownedBossSystem</c> es un tipo PROPIO del mod,
	/// sin referencia de compilacion (<c>build.txt</c> no trae <c>CalamityMod</c> en
	/// <c>dllReferences</c> a proposito, es un mod OPCIONAL de la partida) y sus NPC no tienen un
	/// id fijo como los de vanilla. Las dos cosas ya tienen solucion real, no un parche:
	/// <list type="bullet">
	/// <item>Las <b>37 banderas</b> de jefe que usa este arbol (de las 44 propiedades publicas
	/// que tiene <c>CalamityMod.DownedBossSystem</c> en la version 2.2.2 instalada - confirmado
	/// DECOMPILANDO el <c>.tmod</c> real con <c>ilspycmd</c>, no de memoria) se leen por REFLEXION
	/// en <see cref="BanderasGuia.AgregarBanderasCalamity"/>, con el mismo contrato honesto que
	/// cualquier bandera desconocida: si una actualizacion de Calamity renombra una, se queda "no
	/// evaluable", nunca se inventa un valor.</item>
	/// <item>Los NPC y objetos de Calamity que usa el <c>.json</c> (jefes, objetos de invocacion)
	/// se citan por su "pid" real (<c>"CalamityMod/NombreInterno"</c>, el mismo formato que ya usan
	/// <c>CatalogoBuilds</c>/<c>ArbolLibreria</c>) y se resuelven a su <c>type</c> real de ESTA
	/// partida con la API publica <c>ModContent.TryFind</c> - ver <see cref="ResolverNpcMod"/> y
	/// <see cref="ResolverItemMod"/>. De ahi para abajo (<c>EvaluadorGuia.StatsDeJefe</c>,
	/// <c>EstadoGuia.LecturaDeJefe</c>) un jefe de Calamity es indistinguible de uno vanilla: los
	/// dos son un <c>NPC.type</c> normal, con su vida/daño/defensa REALES de esta partida leidos
	/// en vivo (nunca una cifra copiada de la wiki), asi que los propios modos de dificultad de
	/// Calamity (Revengeance, Death) quedan cubiertos solos, sin logica aparte.</item>
	/// </list>
	/// El orden de progresion (que jefe va antes de cual, que es obligatorio y que es opcional) sí
	/// viene de fuera del motor -no hay forma de decompilarlo, es una decision de diseño del mod-
	/// y se investigo contra la Calamity Mod Wiki oficial (<c>calamitymod.wiki.gg</c>,
	/// <c>Guide:Mod_progression</c> y la pagina de cada jefe) cruzada con el propio codigo
	/// decompilado para confirmar que jefe destraba a cual (p. ej. Profaned Guardians->Providence,
	/// confirmado tanto en la wiki como en el drop real de <c>ProfanedCore</c>). Detalle completo
	/// de la investigacion y de cada hueco cerrado en bitacora.md, 15-sep-2026.
	/// <para />
	/// <see cref="AmbitoGuia"/> ya existia desde el primer dia para esto exactamente: el modelo no
	/// ha cambiado, solo se ha rellenado con datos reales.
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

			// "jefeFinalMod" resuelve un NPC de un mod (Calamity) por nombre - ver ResolverNpcMod.
			int jefeFinalMod = ResolverNpcMod(Cadena(nodo, "jefeFinalMod"));
			if (jefeFinalMod > 0) {
				tramo.JefeFinal = jefeFinalMod;
			}

			// El jefe que cierra el tramo de la maldad del mundo depende del mundo REAL, no de una
			// preferencia: Terraria genera corrupcion o carmesi (WorldGen.crimson) y ahi cambia el
			// jefe. Se lee del juego, no se supone.
			int carmesi = Entero(nodo, "jefeFinalCarmesi", 0);
			if (carmesi > 0 && Terraria.WorldGen.crimson) {
				tramo.JefeFinal = carmesi;
			}
			int carmesiMod = ResolverNpcMod(Cadena(nodo, "jefeFinalModCarmesi"));
			if (carmesiMod > 0 && Terraria.WorldGen.crimson) {
				tramo.JefeFinal = carmesiMod;
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

			// "jefeMod" resuelve un NPC de un mod (Calamity) por nombre - ver ResolverNpcMod.
			int jefeMod = ResolverNpcMod(Cadena(nodo, "jefeMod"));
			if (jefeMod > 0) {
				paso.Jefe = jefeMod;
			}

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

			// "banderaCarmesi": igual que "jefeFinalCarmesi" en el tramo, para una bandera de
			// requisito que cambia segun Corrupcion/Carmesi (p. ej. HiveMindOPerforator de
			// Calamity: downedHiveMind en Corrupcion, downedPerforator en Carmesi - las DOS
			// existen de verdad, a diferencia de vanilla, donde downedBoss2 es una unica bandera
			// compartida por el Devorador y el Cerebro).
			string banderaCarmesi = Cadena(nodo, "banderaCarmesi");
			if (!string.IsNullOrEmpty(banderaCarmesi) && Terraria.WorldGen.crimson) {
				requisito.Bandera = banderaCarmesi;
			}

			JArray ids = nodo["ids"] as JArray;
			if (ids != null) {
				List<int> lista = new List<int>();
				foreach (JToken t in ids) {
					lista.Add((int)t);
				}
				requisito.Ids = lista.ToArray();
			}

			// "idMod"/"idsMod" resuelven un objeto de un mod (Calamity) por nombre, igual que
			// "jefeMod" con los NPC - ver ResolverItemMod. Se AÑADEN a los "id"/"ids" de vanilla
			// que ya hubiera (nunca los reemplazan), asi que un requisito puede mezclar objetos
			// vanilla y de mod en la misma lista de "objeto_cualquiera".
			int idMod = ResolverItemMod(Cadena(nodo, "idMod"));
			if (idMod > 0) {
				requisito.Id = idMod;
			}
			JArray idsMod = nodo["idsMod"] as JArray;
			if (idsMod != null) {
				List<int> lista = requisito.Ids != null ? new List<int>(requisito.Ids) : new List<int>();
				foreach (JToken t in idsMod) {
					int resuelto = ResolverItemMod(t.ToString());
					if (resuelto > 0) {
						lista.Add(resuelto);
					}
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

		/// <summary>
		/// Resuelve un NPC de un mod por su "pid" real (formato "NombreDelMod/NombreInterno", el
		/// mismo que ya usan <c>CatalogoBuilds</c>/<c>ArbolLibreria</c>) al <c>NPC.type</c> real que
		/// tiene ESTA partida.
		/// </summary>
		/// <remarks>
		/// <b>Por que hace falta esto y no basta con un int en el .json, como en vanilla.</b> Un
		/// NPC de Calamity NO tiene un id fijo: tModLoader se lo asigna en cuanto el mod carga, y
		/// ese numero puede cambiar entre partidas (segun que otros mods esten instalados y en que
		/// orden carguen). Guardar un int a pelo en el <c>.json</c> apuntaria a un NPC distinto -o a
		/// ninguno- segun la partida. <c>ModContent.TryFind&lt;ModNPC&gt;</c> es la API PUBLICA real
		/// de tModLoader para esto (<c>Terraria.ModLoader.ModContent.cs</c> decompilado): busca la
		/// plantilla por su nombre de mod + nombre interno, sin que este proyecto necesite
		/// referenciar <c>CalamityMod.dll</c> en el <c>.csproj</c> (a diferencia de las banderas de
		/// <see cref="BanderasGuia"/>, que si hacen falta por reflexion porque leen un campo PROPIO
		/// de Calamity, no del propio tModLoader).
		/// <para />
		/// Se llama una vez por tramo/paso durante <see cref="Construir"/>, con el <c>.tmod</c> de
		/// Calamity ya cargado del todo (PostSetupContent, igual que el resto del catalogo) - el
		/// resultado queda cacheado en <see cref="TramoGuia.JefeFinal"/>/<see cref="PasoGuia.Jefe"/>
		/// como un int normal, y de ahi para abajo (<c>EvaluadorGuia.StatsDeJefe</c>,
		/// <c>EstadoGuia.LecturaDeJefe</c>) no hay ninguna diferencia entre un jefe vanilla y uno de
		/// mod: los dos son <c>ContentSamples.NpcsByNetId[tipo]</c> como cualquier otro.
		/// </remarks>
		private static int ResolverNpcMod(string pid)
		{
			if (string.IsNullOrEmpty(pid)) {
				return 0;
			}

			int barra = pid.IndexOf('/');
			if (barra < 0) {
				_avisos.Add("jefeMod/jefeFinalMod mal formado (falta \"Mod/NombreInterno\"): \"" + pid + "\"");
				return 0;
			}

			string nombreMod = pid.Substring(0, barra);
			string nombreInterno = pid.Substring(barra + 1);

			// Sin el mod instalado no es un aviso: es justo lo esperable en una partida sin
			// Calamity, y CatalogoGuia.Tramos ya filtra estos tramos fuera en ese caso.
			if (!ModLoader.HasMod(nombreMod)) {
				return 0;
			}

			ModNPC encontrado;
			if (!ModContent.TryFind(nombreMod, nombreInterno, out encontrado) || encontrado == null) {
				_avisos.Add("no se encontro el NPC \"" + pid + "\" (¿nombre interno cambiado en una " +
					"actualizacion del mod?)");
				return 0;
			}

			return encontrado.Type;
		}

		/// <summary>Igual que <see cref="ResolverNpcMod"/> pero para un objeto (<c>ModItem</c>).</summary>
		private static int ResolverItemMod(string pid)
		{
			if (string.IsNullOrEmpty(pid)) {
				return 0;
			}

			int barra = pid.IndexOf('/');
			if (barra < 0) {
				_avisos.Add("idMod/idsMod mal formado (falta \"Mod/NombreInterno\"): \"" + pid + "\"");
				return 0;
			}

			string nombreMod = pid.Substring(0, barra);
			string nombreInterno = pid.Substring(barra + 1);

			if (!ModLoader.HasMod(nombreMod)) {
				return 0;
			}

			ModItem encontrado;
			if (!ModContent.TryFind(nombreMod, nombreInterno, out encontrado) || encontrado == null) {
				_avisos.Add("no se encontro el objeto \"" + pid + "\" (¿nombre interno cambiado en una " +
					"actualizacion del mod?)");
				return 0;
			}

			return encontrado.Type;
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
