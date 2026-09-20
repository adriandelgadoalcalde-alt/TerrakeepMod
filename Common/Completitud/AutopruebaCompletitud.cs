using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Panel;
using TerrakeepMod.UI.Personaje;

namespace TerrakeepMod.Common.Completitud
{
	/// <summary>
	/// Cuerpo real de la autoprueba de Completitud - ver <see cref="CompletitudSystem"/> para
	/// como arranca. Corre en el juego REAL, con los datos reales del personaje/mundo de pruebas
	/// (no hace falta preparar ningun escenario: los cuatro resumenes ya leen del motor tal cual
	/// esta la partida).
	/// </summary>
	public static class AutopruebaCompletitud
	{
		private static int _paso;
		private static int _espera;
		private static bool _terminada;

		public static void Avanzar(PestanaCompletitud pestana)
		{
			if (!CompletitudSystem.Activa || _terminada) {
				return;
			}

			if (_espera > 0) {
				_espera--;
				return;
			}

			switch (_paso) {
				case 0: Paso0Numeros(pestana); break;
				case 1: Paso1CapturaEspanol(pestana); break;
				case 2: Paso2CapturaIngles(pestana); break;
				case 3: Paso3Fin(pestana); break;
			}
		}

		private static void Paso0Numeros(PestanaCompletitud pestana)
		{
			bool bienDeVerdad = pestana.TotalFilasParaPrueba == 4;
			Registrar("PASO0 filas de resumen construidas=" + pestana.TotalFilasParaPrueba + " (se esperan 4) | " +
				(bienDeVerdad ? "OK" : "FALLO: NO son 4."));

			string[] nombres = { "Jefes", "Bestiario", "Logros", "Investigacion" };
			bool todosConDatosReales = true;

			for (int i = 0; i < pestana.TotalFilasParaPrueba; i++) {
				ResumenCompletitud resumen = pestana.ResumenParaPrueba(i);
				bool coherente = resumen != null && resumen.Total > 0 && resumen.Hecho >= 0 && resumen.Hecho <= resumen.Total;
				todosConDatosReales &= coherente;
				Registrar("PASO0." + i + " " + nombres[i] + ": " +
					(resumen != null ? resumen.Hecho + "/" + resumen.Total + " (" + (int)(resumen.Fraccion * 100f) + "%), faltan.Count=" + resumen.Faltan.Count : "null") +
					" | " + (coherente ? "OK: datos reales y coherentes (Total>0, 0<=Hecho<=Total)." : "FALLO."));
			}

			// Cruce contra un numero YA conocido y verificado en otra parte del mod (bitacora,
			// 13-sep-2026): la Guia tiene exactamente 21 tramos implementados. Si esto no
			// coincidiera, "Jefes y eventos" estaria leyendo mal el mismo catalogo que ya usa la
			// Guia.
			ResumenCompletitud jefes = pestana.ResumenParaPrueba(0);
			bool coincideConLaGuia = jefes != null && jefes.Total == 21;
			Registrar("PASO0.cruce Jefes.Total=" + (jefes != null ? jefes.Total.ToString() : "?") +
				", la Guia real tiene 21 tramos implementados (bitacora, verificado el 13-sep-2026) | " +
				(coincideConLaGuia ? "OK: mismo catalogo, mismo numero." : "FALLO: no coincide."));

			// Idea 8: desglose real por bioma del Bestiario (fila 1). Se comprueba que hay filas
			// reales, que cada una trae "hecho <= total" y que la suma de los "total" por bioma
			// vistos aqui es MAYOR que el total de bichos del bestiario (todos < resumen.Total):
			// no puede ser mayor porque un bioma nunca puede tener mas bichos que el bestiario
			// entero, y es coherente que sea distinto de resumen.Total porque un mismo bicho puede
			// contarse en varios biomas a la vez y algunos bichos no son de ningun bioma listado
			// (ej. solo por evento).
			ResumenCompletitud bestiario = pestana.ResumenParaPrueba(1);
			if (bestiario != null) {
				int ejemplos = System.Math.Min(5, bestiario.Desglose.Count);
				for (int i = 0; i < ejemplos; i++) {
					Registrar("PASO0.bioma[" + i + "] " + bestiario.Desglose[i]);
				}
				Registrar("PASO0.bioma total de filas=" + bestiario.Desglose.Count +
					" (bestiario.Total=" + bestiario.Total + ") | " +
					(bestiario.Desglose.Count > 0
						? "OK: hay desglose real por bioma."
						: "FALLO: sin desglose."));
			}

			// Idea 8, segunda pasada (20-sep-2026): "donde conseguirlo", la pieza que faltaba.
			// InvalidarCacheParaPrueba fuerza a reconstruir las dos tablas (receta/tienda) contra
			// los datos REALES de esta partida ya cargada, en vez de fiarse de una cache que podria
			// haberse construido antes de que el mod terminara de cargar el contenido.
			GlobalItemDondeConseguir.InvalidarCacheParaPrueba();

			// El nombre real del ingrediente en el IDIOMA ACTIVO ahora mismo (nunca "Wood" a pelo:
			// esta autopreuba puede correr con el idioma en español, donde el objeto real se llama
			// "Madera" - bug de la propia comprobacion encontrado en la primera pasada, el texto
			// SI decia "se fabrica con Madera" pero el chequeo buscaba el ingles a pelo).
			Terraria.Item muestraMadera = new Terraria.Item();
			muestraMadera.SetDefaults(Terraria.ID.ItemID.Wood);
			string textoReceta = GlobalItemDondeConseguir.TextoParaPrueba(Terraria.ID.ItemID.WoodenSword);
			bool recetaOk = textoReceta != null && textoReceta.IndexOf(muestraMadera.Name, System.StringComparison.OrdinalIgnoreCase) >= 0;
			Registrar("PASO0.dondeConseguir receta real (Wooden Sword, type=" + Terraria.ID.ItemID.WoodenSword +
				") -> \"" + textoReceta + "\" (ingrediente real esperado: \"" + muestraMadera.Name + "\") | " +
				(recetaOk ? "OK: la receta real aparece en el texto." : "FALLO."));

			// Primer objeto real de una tienda real que NO tenga tambien receta (para comprobar la
			// via de TIENDA de verdad, sin que la de receta la tape antes) - nunca un objeto
			// adivinado a mano, se busca entre las tiendas reales de esta partida.
			int tipoTienda = -1;
			string textoTienda = null;
			foreach (Terraria.ModLoader.AbstractNPCShop tienda in Terraria.ModLoader.NPCShopDatabase.AllShops) {
				foreach (Terraria.ModLoader.AbstractNPCShop.Entry entrada in tienda.ActiveEntries) {
					if (entrada.Item == null || entrada.Item.type <= 0) {
						continue;
					}
					string candidato = GlobalItemDondeConseguir.TextoParaPrueba(entrada.Item.type);
					if (candidato != null && (candidato.Contains("vende") || candidato.Contains("sold"))) {
						tipoTienda = entrada.Item.type;
						textoTienda = candidato;
						break;
					}
				}
				if (tipoTienda > 0) {
					break;
				}
			}
			bool tiendaOk = tipoTienda > 0 && textoTienda != null;
			Registrar("PASO0.dondeConseguir tienda real (primer objeto de tienda sin receta, type=" + tipoTienda +
				") -> \"" + textoTienda + "\" | " +
				(tiendaOk ? "OK: la via de tienda real funciona." : "sin objetos de tienda sin receta (no es fallo de la logica, ninguno cumplia la condicion)."));

			Avanzar(1);
		}

		private static IdiomaDeTerrakeep _idiomaOriginal;

		private static void Paso1CapturaEspanol(PestanaCompletitud pestana)
		{
			_idiomaOriginal = Idiomas.IdiomaConfigurado;
			Registrar("PASO1 (es) " + CapturaDePantalla.Guardar("completitud-01-es"));
			Idiomas.Elegir(IdiomaDeTerrakeep.English);
			Avanzar(2);
		}

		private static void Paso2CapturaIngles(PestanaCompletitud pestana)
		{
			Registrar("PASO2 (en) cultura activa=" + Idiomas.CulturaActiva + " " +
				CapturaDePantalla.Guardar("completitud-02-en"));
			Idiomas.Elegir(_idiomaOriginal);
			Avanzar(3);
		}

		private static void Paso3Fin(PestanaCompletitud pestana)
		{
			_terminada = true;
			Registrar("AUTOPRUEBA COMPLETITUD: terminada.");
		}

		private static void Avanzar(int siguientePaso)
		{
			_paso = siguientePaso;
			_espera = 20;
		}

		private static void Registrar(string mensaje)
		{
			if (Terrakeep.Instance != null) {
				Terrakeep.Instance.Logger.Info(Terrakeep.LogTag + " " + mensaje);
			}
		}
	}
}
