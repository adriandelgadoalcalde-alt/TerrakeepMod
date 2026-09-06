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
		private const float Escala = 0.75f;

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

			EtiquetaTk aviso = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Equipo.Nota"),
				0.72f, 900f, 18f);
			aviso.ColorTexto = EstiloTk.TextoSuave;
			aviso.Left.Set(0f, 0f);
			aviso.Top.Set(30f, 0f);
			Append(aviso);

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
			float paso = RejillaSlots.Paso(Escala);
			float arriba = 74f;

			Cabecera("Personaje.Equipo.Equipado", 0f, arriba - 22f);
			Cabecera("Personaje.Equipo.Vanidad", paso, arriba - 22f);
			Cabecera("Personaje.Equipo.Tinte", paso * 2f, arriba - 22f);

			for (int i = 0; i < PersonajeVivo.SlotsTinte; i++) {
				float y = arriba + i * paso;

				bool esArmadura = i < 3;
				int contextoEquipo = esArmadura
					? ItemSlot.Context.EquipArmor
					: ItemSlot.Context.EquipAccessory;
				int contextoVanidad = esArmadura
					? ItemSlot.Context.EquipArmorVanity
					: ItemSlot.Context.EquipAccessoryVanity;

				RejillaSlots.Uno(this, jugador.armor, i, contextoEquipo, Escala, 0f, y);
				RejillaSlots.Uno(this, jugador.armor, 10 + i, contextoVanidad, Escala, paso, y);
				RejillaSlots.Uno(this, jugador.dye, i, ItemSlot.Context.EquipDye, Escala, paso * 2f, y);

				int indiceFila = i;
				EtiquetaTk nombre = new EtiquetaTk(() => TextoFila(indiceFila), 0.78f, 240f, 20f);
				nombre.Left.Set(paso * 3f + 6f, 0f);
				nombre.Top.Set(y + 12f, 0f);
				Append(nombre);
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
			float paso = RejillaSlots.Paso(Escala);
			float izquierda = paso * 3f + 260f;
			float arriba = 74f;

			Cabecera("Personaje.Equipo.Especial", izquierda, arriba - 44f);
			Cabecera("Personaje.Equipo.Puesto", izquierda, arriba - 22f);
			Cabecera("Personaje.Equipo.Tinte", izquierda + paso, arriba - 22f);

			for (int i = 0; i < PersonajeVivo.SlotsMisc; i++) {
				float y = arriba + i * paso;
				// La variable del for se comparte entre todas las iteraciones, asi que hay que
				// copiarla antes de capturarla en la lambda de la etiqueta.
				int indice = i;

				RejillaSlots.Uno(this, jugador.miscEquips, i, ContextosMisc[i], Escala, izquierda, y);
				RejillaSlots.Uno(this, jugador.miscDyes, i, ItemSlot.Context.EquipMiscDye, Escala,
					izquierda + paso, y);

				EtiquetaTk nombre = new EtiquetaTk(
					() => Idiomas.Texto("Personaje.Equipo.Misc." + ClavesMisc[indice]), 0.78f, 200f, 20f);
				nombre.Left.Set(izquierda + paso * 2f + 6f, 0f);
				nombre.Top.Set(y + 12f, 0f);
				Append(nombre);
			}

			EtiquetaTk resumen = new EtiquetaTk(
				() => Idiomas.Texto("Personaje.Equipo.RanurasActivas",
					5 + PersonajeVivo.Jugador.extraAccessorySlots),
				0.75f, 520f, 20f);
			resumen.ColorTexto = EstiloTk.TextoSuave;
			resumen.Left.Set(izquierda, 0f);
			resumen.Top.Set(arriba + 5f * paso + 16f, 0f);
			Append(resumen);
		}

		/// <summary>Rotulo de columna. Recibe la CLAVE de localizacion, no el texto ya resuelto:
		/// asi cambia con el idioma sin reabrir el panel.</summary>
		private void Cabecera(string clave, float izquierda, float arriba)
		{
			EtiquetaTk etiqueta = new EtiquetaTk(() => Idiomas.Texto(clave), 0.78f, 180f, 20f);
			etiqueta.ColorTexto = EstiloTk.TextoSuave;
			etiqueta.Left.Set(izquierda, 0f);
			etiqueta.Top.Set(arriba, 0f);
			Append(etiqueta);
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
		}
	}
}
