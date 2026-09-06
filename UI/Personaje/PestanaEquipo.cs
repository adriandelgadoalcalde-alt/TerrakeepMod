using System.Collections.Generic;
using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Pestaña "Equipo": armadura, accesorios, vanidad, tintes y el equipo especial
	/// (mascota, mascota de luz, vagoneta, montura y gancho), con los tres conjuntos de equipo
	/// (loadouts) del personaje.
	/// <para />
	/// Como funcionan de verdad los loadouts en esta version (leido en el juego instalado, no
	/// supuesto): el conjunto ACTIVO no vive en <c>Player.Loadouts[CurrentLoadoutIndex]</c> sino
	/// en <c>Player.armor</c> / <c>Player.dye</c> / <c>Player.hideVisibleAccessory</c>.
	/// <c>EquipmentLoadout.Swap</c> intercambia elemento a elemento con esos arrays, asi que
	/// mientras un conjunto esta activo su entrada en <c>Loadouts[]</c> esta VACIA (guarda el
	/// conjunto que estaba antes). Por eso esta pestaña cambia de conjunto llamando a la API
	/// oficial <c>Player.TrySwitchingLoadout(i)</c> y edita siempre <c>Player.armor</c>: asi el
	/// juego mantiene coherente todo lo demas (sonido, particulas, red, y el aviso a los mods via
	/// <c>PlayerLoader.OnEquipmentLoadoutSwitched</c>).
	/// <para />
	/// Como <c>Swap</c> intercambia ELEMENTOS y no reasigna los arrays, las ranuras de esta
	/// pestaña siguen valiendo despues de cambiar de conjunto: no hay que reconstruirlas.
	/// </summary>
	public class PestanaEquipo : UIElement
	{
		/// <summary>Escala de ranura ideal, la que se usa si la ventana da de si.</summary>
		private const float EscalaMaxima = 0.75f;

		/// <summary>Escala minima antes de que las ranuras dejen de leerse.</summary>
		private const float EscalaMinima = 0.58f;

		/// <summary>Alto de la zona de arriba (selector de conjunto + leyenda de columnas).</summary>
		private const float ArribaRejilla = 68f;

		/// <summary>Filas de la columna izquierda: 3 de armadura + 7 de accesorio.</summary>
		private const int FilasEquipo = 10;

		/// <summary>Rotulo de cada fila: las tres piezas de armadura por su clave propia y los
		/// siete accesorios numerados con una sola clave con parametro.</summary>
		private static string NombreFila(int fila)
		{
			switch (fila) {
				case 0: return Idiomas.Texto("Personaje.Equipo.Cabeza");
				case 1: return Idiomas.Texto("Personaje.Equipo.Pecho");
				case 2: return Idiomas.Texto("Personaje.Equipo.Piernas");
				default: return Idiomas.Texto("Personaje.Equipo.Accesorio", fila - 2);
			}
		}

		private static readonly string[] ClavesMisc = {
			"Mascota", "MascotaLuz", "Vagoneta", "Montura", "Gancho"
		};

		// Contexto de ItemSlot de cada ranura de Player.miscEquips, en su orden real
		// (0 Pet, 1 Light Pet, 2 Minecart, 3 Mount, 4 Hook).
		private static readonly int[] ContextosMisc = {
			ItemSlot.Context.EquipPet,
			ItemSlot.Context.EquipLight,
			ItemSlot.Context.EquipMinecart,
			ItemSlot.Context.EquipMount,
			ItemSlot.Context.EquipGrapple
		};

		private readonly List<BotonTk> _botonesLoadout = new List<BotonTk>();
		private readonly List<FilaEquipo> _filasEquipo = new List<FilaEquipo>();
		private readonly List<FilaMisc> _filasMisc = new List<FilaMisc>();

		private EtiquetaTk _leyendaEquipo;
		private EtiquetaTk _leyendaMisc;
		private EtiquetaTk _tituloMisc;
		private EtiquetaTk _resumenAccesorios;

		/// <summary>Paso con el que estan colocadas las filas ahora mismo, para no recolocarlas en
		/// cada fotograma.</summary>
		private float _pasoColocado = -1f;

		/// <summary>Columnas en las que estan repartidas ahora mismo las 10 filas de equipo.</summary>
		private int _columnasColocadas = -1;

		/// <summary>Sitio que hay que dejarle al rotulo de cada fila ("Accesorio 7  (no activa)" es
		/// lo mas largo que sale ahi).</summary>
		private const float AnchoRotuloFila = 170f;

		public PestanaEquipo()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirSelectorLoadout();
			ConstruirEquipo();
			ConstruirMisc();
		}

		private void ConstruirSelectorLoadout()
		{
			Player jugador = PersonajeVivo.Jugador;

			EtiquetaTk etiqueta = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Equipo.Conjunto"), 0.85f, 170f, 22f);
			etiqueta.ColorTexto = EstiloTk.TextoSuave;
			etiqueta.Left.Set(0f, 0f);
			etiqueta.Top.Set(4f, 0f);
			Append(etiqueta);

			for (int i = 0; i < jugador.Loadouts.Length; i++) {
				int indice = i;
				BotonTk boton = new BotonTk(Idiomas.Texto("Personaje.Equipo.ConjuntoN", i + 1), 0.8f);
				boton.Width.Set(110f, 0f);
				boton.Height.Set(28f, 0f);
				boton.Left.Set(170f + i * 116f, 0f);
				boton.Top.Set(0f, 0f);
				boton.AlPulsar += () => CambiarLoadout(indice);
				_botonesLoadout.Add(boton);
				Append(boton);
			}

			// La nota tecnica larga se ha ido al TOOLTIP de los tres botones: como EtiquetaTk
			// medía ~900 px, se salia del marco por la derecha y encima pisaba las cabeceras de la
			// columna de equipo especial (se leia "PlayerSpe$ialTequipmentut"). Visto en una
			// captura real del juego, no leyendo codigo.
			for (int i = 0; i < _botonesLoadout.Count; i++) {
				_botonesLoadout[i].Ayuda = () => Idiomas.Texto("Personaje.Equipo.Nota");
			}

			ActualizarBotonesLoadout();
		}

		private void ActualizarBotonesLoadout()
		{
			int actual = PersonajeVivo.Jugador.CurrentLoadoutIndex;
			for (int i = 0; i < _botonesLoadout.Count; i++) {
				_botonesLoadout[i].Activo = i == actual;
			}
		}

		private void CambiarLoadout(int indice)
		{
			Player jugador = PersonajeVivo.Jugador;
			int anterior = jugador.CurrentLoadoutIndex;
			string cabezaAntes = PersonajeVivo.DescribirObjeto(jugador.armor[0]);

			jugador.TrySwitchingLoadout(indice);

			ActualizarBotonesLoadout();

			Terrakeep.Instance.Logger.Info(
				$"{Terrakeep.LogTag} Cambio de conjunto de equipo: pedido {indice}, " +
				$"CurrentLoadoutIndex antes={anterior} ahora={jugador.CurrentLoadoutIndex}. " +
				$"armor[0] antes={cabezaAntes} ahora={PersonajeVivo.DescribirObjeto(jugador.armor[0])}.");
		}

		private void ConstruirEquipo()
		{
			Player jugador = PersonajeVivo.Jugador;

			// Una sola linea de leyenda en vez de tres cabeceras encima de cada columna: con el
			// paso real entre ranuras (~35-41 px) las palabras "Equipado", "Vanidad" y "Tinte" se
			// pisaban unas con otras y se leia "EquipaVanidaTinte". Visto en una captura real.
			_leyendaEquipo = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Equipo.Leyenda"), 0.72f, 340f, 20f);
			_leyendaEquipo.ColorTexto = EstiloTk.TextoSuave;
			_leyendaEquipo.Left.Set(0f, 0f);
			_leyendaEquipo.Top.Set(ArribaRejilla - 20f, 0f);
			Append(_leyendaEquipo);

			for (int i = 0; i < PersonajeVivo.SlotsTinte; i++) {
				bool esArmadura = i < 3;
				int contextoEquipo = esArmadura
					? ItemSlot.Context.EquipArmor
					: ItemSlot.Context.EquipAccessory;
				int contextoVanidad = esArmadura
					? ItemSlot.Context.EquipArmorVanity
					: ItemSlot.Context.EquipAccessoryVanity;

				int indiceFila = i;
				EtiquetaTk nombre = new EtiquetaTk(() => TextoFila(indiceFila), 0.78f, 240f, 20f);
				Append(nombre);

				_filasEquipo.Add(new FilaEquipo(
					RejillaSlots.Uno(this, jugador.armor, i, contextoEquipo, EscalaMaxima, 0f, 0f),
					RejillaSlots.Uno(this, jugador.armor, 10 + i, contextoVanidad, EscalaMaxima, 0f, 0f),
					RejillaSlots.Uno(this, jugador.dye, i, ItemSlot.Context.EquipDye, EscalaMaxima, 0f, 0f),
					nombre));
			}
		}

		/// <summary>Las tres ranuras de una fila de equipo mas su rotulo.</summary>
		private sealed class FilaEquipo
		{
			public readonly SlotObjetoVanilla Equipado;
			public readonly SlotObjetoVanilla Vanidad;
			public readonly SlotObjetoVanilla Tinte;
			public readonly EtiquetaTk Nombre;

			public FilaEquipo(SlotObjetoVanilla equipado, SlotObjetoVanilla vanidad,
				SlotObjetoVanilla tinte, EtiquetaTk nombre)
			{
				Equipado = equipado;
				Vanidad = vanidad;
				Tinte = tinte;
				Nombre = nombre;
			}
		}

		/// <summary>Las dos ranuras de una fila de equipo especial mas su rotulo.</summary>
		private sealed class FilaMisc
		{
			public readonly SlotObjetoVanilla Puesto;
			public readonly SlotObjetoVanilla Tinte;
			public readonly EtiquetaTk Nombre;

			public FilaMisc(SlotObjetoVanilla puesto, SlotObjetoVanilla tinte, EtiquetaTk nombre)
			{
				Puesto = puesto;
				Tinte = tinte;
				Nombre = nombre;
			}
		}

		/// <summary>
		/// Texto de la fila. Marca las ranuras de accesorio que el personaje todavia no tiene
		/// activas: en vanilla hay 5 de base y la 6ª solo cuenta con el Corazon de Demonio
		/// (<c>Player.extraAccessory</c>) y en Experto o superior; el juego lo recalcula cada tick
		/// en <c>Player.ResetEffects</c> dejando el resultado en <c>extraAccessorySlots</c>.
		/// </summary>
		private static string TextoFila(int fila)
		{
			if (fila < 3) {
				return NombreFila(fila);
			}

			Player jugador = PersonajeVivo.Jugador;
			int accesoriosActivos = 5 + jugador.extraAccessorySlots;
			int numeroAccesorio = fila - 2;

			return numeroAccesorio <= accesoriosActivos
				? NombreFila(fila)
				: NombreFila(fila) + Idiomas.Texto("Personaje.Equipo.NoActiva");
		}

		private void ConstruirMisc()
		{
			Player jugador = PersonajeVivo.Jugador;

			_tituloMisc = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Equipo.Especial"), 0.85f, 220f, 22f);
			_tituloMisc.ColorTexto = EstiloTk.TextoSuave;
			Append(_tituloMisc);

			_leyendaMisc = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Equipo.LeyendaMisc"), 0.72f, 260f, 20f);
			_leyendaMisc.ColorTexto = EstiloTk.TextoSuave;
			Append(_leyendaMisc);

			for (int i = 0; i < PersonajeVivo.SlotsMisc; i++) {
				// La variable del for se comparte entre todas las iteraciones, asi que hay que
				// copiarla antes de capturarla en la lambda de la etiqueta.
				int indice = i;

				EtiquetaTk nombre = new EtiquetaTk(
					() => Idiomas.Texto("Personaje.Equipo.Misc." + ClavesMisc[indice]), 0.78f, 200f, 20f);
				Append(nombre);

				_filasMisc.Add(new FilaMisc(
					RejillaSlots.Uno(this, jugador.miscEquips, i, ContextosMisc[i], EscalaMaxima, 0f, 0f),
					RejillaSlots.Uno(this, jugador.miscDyes, i, ItemSlot.Context.EquipMiscDye,
						EscalaMaxima, 0f, 0f),
					nombre));
			}

			// Texto CORTO: el largo ("... (la 6ª exige el Corazon de Demonio y modo Experto)") medía
			// ~520 px, empezaba pasada la mitad del panel y se salia por la derecha. Lo que explica
			// la condicion se ha quedado en el tooltip de la propia fila, que ya dice "(no activa)".
			_resumenAccesorios = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Equipo.RanurasActivas",
					5 + PersonajeVivo.Jugador.extraAccessorySlots),
				0.75f, 300f, 20f);
			_resumenAccesorios.ColorTexto = EstiloTk.TextoSuave;
			Append(_resumenAccesorios);
		}

		/// <summary>
		/// Coloca las dos columnas con el alto REAL que le haya tocado a la pestaña.
		/// <para />
		/// No se puede hacer con medidas fijas y es un fallo real, visto en una captura del juego a
		/// 800x720: la columna izquierda tiene 10 filas (3 de armadura + 7 de accesorio) y, con la
		/// escala 0,75 de siempre, la 7ª se salia POR ABAJO del marco y pintaba encima del pie y del
		/// boton de cerrar. Como el alto disponible depende de la resolucion y de la escala de
		/// interfaz del jugador, lo que se ajusta es la ESCALA de las ranuras, acotada entre 0,58 y
		/// 0,75. Es la misma leccion que ya se aplico al espaciado de Builds.
		/// </summary>
		private void ColocarColumnas()
		{
			CalculatedStyle propias = GetDimensions();
			float alto = propias.Height;
			if (alto <= 0f) {
				return;
			}

			float disponible = alto - ArribaRejilla - 4f;

			// Cuantas columnas hacen falta para que las 10 filas quepan sin bajar de la escala
			// minima. A 800x720 sale 1; a 1600x900 el juego usa escala de interfaz 1,47 y la
			// pantalla logica se queda en 1090x613, o sea MENOS alto util que a 720 y mas ancho -
			// ahi salen 2 y las filas se reparten. Fallo real visto en una captura a esa
			// resolucion: con una sola columna el accesorio 7 volvia a salirse por abajo.
			int columnas = 1;
			int filasPorColumna = FilasEquipo;
			float escala = EscalaMinima;
			while (columnas <= 2) {
				filasPorColumna = (FilasEquipo + columnas - 1) / columnas;
				escala = (disponible / filasPorColumna - RejillaSlots.Separacion) / 52f;
				if (escala >= EscalaMinima) {
					break;
				}
				columnas++;
			}
			if (columnas > 2) {
				columnas = 2;
				filasPorColumna = (FilasEquipo + 1) / 2;
			}
			if (escala > EscalaMaxima) {
				escala = EscalaMaxima;
			}
			if (escala < EscalaMinima) {
				escala = EscalaMinima;
			}

			float paso = RejillaSlots.Paso(escala);
			if (columnas == _columnasColocadas && System.Math.Abs(paso - _pasoColocado) < 0.5f) {
				return;
			}
			_pasoColocado = paso;
			_columnasColocadas = columnas;

			float anchoColumna = paso * 3f + AnchoRotuloFila;
			float izquierdaMisc = columnas * anchoColumna + 20f;

			for (int i = 0; i < _filasEquipo.Count; i++) {
				FilaEquipo fila = _filasEquipo[i];
				float x = (i / filasPorColumna) * anchoColumna;
				float y = ArribaRejilla + (i % filasPorColumna) * paso;
				Colocar(fila.Equipado, escala, x, y);
				Colocar(fila.Vanidad, escala, x + paso, y);
				Colocar(fila.Tinte, escala, x + paso * 2f, y);
				fila.Nombre.Left.Set(x + paso * 3f + 6f, 0f);
				fila.Nombre.Top.Set(y + (paso - 22f) / 2f, 0f);
			}

			_leyendaEquipo.Top.Set(ArribaRejilla - 20f, 0f);

			_tituloMisc.Left.Set(izquierdaMisc, 0f);
			_tituloMisc.Top.Set(ArribaRejilla - 42f, 0f);
			_leyendaMisc.Left.Set(izquierdaMisc, 0f);
			_leyendaMisc.Top.Set(ArribaRejilla - 20f, 0f);

			for (int i = 0; i < _filasMisc.Count; i++) {
				FilaMisc fila = _filasMisc[i];
				float y = ArribaRejilla + i * paso;
				Colocar(fila.Puesto, escala, izquierdaMisc, y);
				Colocar(fila.Tinte, escala, izquierdaMisc + paso, y);
				fila.Nombre.Left.Set(izquierdaMisc + paso * 2f + 6f, 0f);
				fila.Nombre.Top.Set(y + (paso - 22f) / 2f, 0f);
			}

			_resumenAccesorios.Left.Set(izquierdaMisc, 0f);
			_resumenAccesorios.Top.Set(ArribaRejilla + PersonajeVivo.SlotsMisc * paso + 12f, 0f);

			Recalculate();
		}

		private static void Colocar(SlotObjetoVanilla slot, float escala, float x, float y)
		{
			slot.Escala = escala;
			slot.Left.Set(x, 0f);
			slot.Top.Set(y, 0f);
		}

		public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
		{
			base.Update(gameTime);

			// El jugador puede cambiar de conjunto con las teclas del juego mientras el panel
			// esta abierto; los botones tienen que seguirlo. Y su rotulo se vuelve a pedir para que
			// cambie con el idioma sin reabrir el panel.
			ActualizarBotonesLoadout();
			for (int i = 0; i < _botonesLoadout.Count; i++) {
				_botonesLoadout[i].FijarTexto(Idiomas.Texto("Personaje.Equipo.ConjuntoN", i + 1));
			}

			// El alto real de la pestaña no existe hasta que el motor ha recalculado el arbol, y
			// cambia si el jugador redimensiona la ventana.
			ColocarColumnas();
		}
	}
}
