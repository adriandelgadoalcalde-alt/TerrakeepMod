using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common.Ajustes;
using TerrakeepMod.Common.Loadouts;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.Common.Undo;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Sub-pestaña nueva de Personaje: gestión ampliada de los conjuntos de equipo (loadouts).
	/// <para />
	/// Terraria trae 3 conjuntos NATIVOS sin nombre (<c>Player.Loadouts</c>, tamaño fijo del
	/// motor - ver el XMLdoc de <see cref="PresetLoadout"/>). Esta pestaña les pone nombre
	/// (persistido con el propio personaje) y añade PRESETS propios del mod, sin límite fijo,
	/// que se pueden guardar y volver a aplicar sobre cualquiera de los tres - la pieza real de
	/// "guardar más de 3" del encargo. Cambiar de conjunto en sí (los tres botones "Conjunto N")
	/// ya existía en <see cref="PestanaEquipo"/> y sigue viviendo ahí: aquí solo se AÑADE, no se
	/// duplica esa lógica salvo por un atajo de conveniencia para no obligar a saltar de pestaña.
	/// </summary>
	public class PestanaConjuntos : UIElement
	{
		private readonly List<FilaNativa> _filasNativas = new List<FilaNativa>();
		private UIList _listaPresets;
		private UIPanel _cajaPresets;
		private CampoTextoTk _campoNombrePreset;
		private EtiquetaTk _tituloNativos;
		private EtiquetaTk _tituloPresets;
		private EtiquetaTk _sinPresets;

		private const float AltoFilaNativa = 34f;
		private const float ArribaNativos = 30f;
		private const float ArribaPresets = ArribaNativos + 3 * AltoFilaNativa + 40f;
		private const float AltoFilaGuardar = 34f;

		public PestanaConjuntos()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			ConstruirNativos();
			ConstruirPresets();
			ConstruirGuardarNuevo();
		}

		// ============================================================================
		// Los tres conjuntos nativos: nombre personalizado + acceso rapido
		// ============================================================================

		private sealed class FilaNativa
		{
			public int Indice;
			public BotonTk BotonIr;
			public BotonTk BotonRenombrar;
			public CampoTextoTk CampoEdicion;
			public bool Editando;
		}

		private void ConstruirNativos()
		{
			_tituloNativos = new EtiquetaTk(() => Idiomas.Texto("Personaje.Conjuntos.TituloNativos"), 0.85f, 500f, 22f);
			_tituloNativos.ColorTexto = EstiloTk.TextoSuave;
			_tituloNativos.Left.Set(0f, 0f);
			_tituloNativos.Top.Set(0f, 0f);
			Append(_tituloNativos);

			Player jugador = PersonajeVivo.Jugador;
			for (int i = 0; i < jugador.Loadouts.Length; i++) {
				FilaNativa fila = new FilaNativa { Indice = i };

				fila.BotonIr = new BotonTk(NombreNativo(i), 0.8f);
				fila.BotonIr.Width.Set(260f, 0f);
				fila.BotonIr.Height.Set(28f, 0f);
				fila.BotonIr.Left.Set(0f, 0f);
				fila.BotonIr.Top.Set(ArribaNativos + i * AltoFilaNativa, 0f);
				fila.BotonIr.Ayuda = () => Idiomas.Texto("Personaje.Conjuntos.IrAyuda");
				int indiceCerrado = i;
				fila.BotonIr.AlPulsar += () => IrAConjunto(indiceCerrado);
				Append(fila.BotonIr);

				fila.BotonRenombrar = new BotonTk(Idiomas.Texto("Personaje.Conjuntos.Renombrar"), 0.75f);
				fila.BotonRenombrar.Width.Set(140f, 0f);
				fila.BotonRenombrar.Height.Set(28f, 0f);
				fila.BotonRenombrar.Left.Set(270f, 0f);
				fila.BotonRenombrar.Top.Set(ArribaNativos + i * AltoFilaNativa, 0f);
				fila.BotonRenombrar.AlPulsar += () => EmpezarRenombrar(indiceCerrado);
				Append(fila.BotonRenombrar);

				_filasNativas.Add(fila);
			}

			ActualizarBotonesNativos();
		}

		private static string NombreNativo(int indice)
		{
			LoadoutsPlayer datos = PersonajeVivo.Jugador.GetModPlayer<LoadoutsPlayer>();
			string personalizado = indice >= 0 && indice < datos.Nombres.Length ? datos.Nombres[indice] : null;
			return !string.IsNullOrEmpty(personalizado)
				? personalizado
				: Idiomas.Texto("Personaje.Equipo.ConjuntoN", indice + 1);
		}

		private void ActualizarBotonesNativos()
		{
			int actual = PersonajeVivo.Jugador.CurrentLoadoutIndex;
			foreach (FilaNativa fila in _filasNativas) {
				if (fila.Editando) {
					continue;
				}
				fila.BotonIr.Activo = fila.Indice == actual;
				fila.BotonIr.FijarTexto(NombreNativo(fila.Indice));
			}
		}

		private void IrAConjunto(int indice)
		{
			Player jugador = PersonajeVivo.Jugador;
			jugador.TrySwitchingLoadout(indice);
			ActualizarBotonesNativos();
			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Conjuntos: ir a \"{NombreNativo(indice)}\" " +
				$"(indice {indice}) desde la pestaña Conjuntos. CurrentLoadoutIndex ahora={jugador.CurrentLoadoutIndex}.");
		}

		private void EmpezarRenombrar(int indice)
		{
			FilaNativa fila = _filasNativas[indice];
			if (fila.Editando) {
				return;
			}
			fila.Editando = true;

			RemoveChild(fila.BotonIr);

			fila.CampoEdicion = new CampoTextoTk(() => Idiomas.Texto("Personaje.Conjuntos.NombrePista"), 24, 0.8f);
			fila.CampoEdicion.Width.Set(260f, 0f);
			fila.CampoEdicion.Height.Set(28f, 0f);
			fila.CampoEdicion.Left.Set(0f, 0f);
			fila.CampoEdicion.Top.Set(ArribaNativos + indice * AltoFilaNativa, 0f);
			string actual = NombreNativo(indice);
			fila.CampoEdicion.FijarTextoSilencioso(actual);
			fila.CampoEdicion.AlConfirmar += texto => TerminarRenombrar(indice, texto);
			Append(fila.CampoEdicion);
		}

		private void TerminarRenombrar(int indice, string textoNuevo)
		{
			FilaNativa fila = _filasNativas[indice];
			if (!fila.Editando) {
				return;
			}

			LoadoutsPlayer datos = PersonajeVivo.Jugador.GetModPlayer<LoadoutsPlayer>();
			string limpio = (textoNuevo ?? "").Trim();
			datos.Nombres[indice] = limpio.Length > 0 ? limpio : null;

			if (fila.CampoEdicion != null) {
				RemoveChild(fila.CampoEdicion);
				fila.CampoEdicion = null;
			}
			fila.Editando = false;
			Append(fila.BotonIr);

			ActualizarBotonesNativos();
			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Conjuntos: renombrado el conjunto {indice} a " +
				$"\"{NombreNativo(indice)}\" (guardado con el personaje, ModPlayer.SaveData).");
		}

		// ============================================================================
		// Presets del mod: guardar mas de 3, aplicar rapido
		// ============================================================================

		private void ConstruirPresets()
		{
			_tituloPresets = new EtiquetaTk(() => Idiomas.Texto("Personaje.Conjuntos.TituloPresets"), 0.85f, 500f, 22f);
			_tituloPresets.ColorTexto = EstiloTk.TextoSuave;
			_tituloPresets.Left.Set(0f, 0f);
			_tituloPresets.Top.Set(ArribaPresets - 28f, 0f);
			Append(_tituloPresets);

			_cajaPresets = new UIPanel();
			_cajaPresets.Width.Set(0f, 1f);
			_cajaPresets.Top.Set(ArribaPresets, 0f);
			_cajaPresets.Height.Set(-(ArribaPresets + AltoFilaGuardar + 40f), 1f);
			_cajaPresets.BackgroundColor = EstiloTk.FondoCaja;
			Append(_cajaPresets);

			_listaPresets = new UIList();
			_listaPresets.Width.Set(-24f, 1f);
			_listaPresets.Height.Set(0f, 1f);
			_listaPresets.ListPadding = 6f;
			_cajaPresets.Append(_listaPresets);

			UIScrollbar barra = new UIScrollbar();
			barra.HAlign = 1f;
			barra.Height.Set(0f, 1f);
			barra.SetView(100f, 1000f);
			_cajaPresets.Append(barra);
			_listaPresets.SetScrollbar(barra);

			_sinPresets = new EtiquetaTk(() => Idiomas.Texto("Personaje.Conjuntos.SinPresets"), 0.8f, 500f, 22f);
			_sinPresets.ColorTexto = EstiloTk.TextoSuave;
			_sinPresets.Left.Set(6f, 0f);
			_sinPresets.Top.Set(6f, 0f);

			ReconstruirListaPresets();
		}

		private void ReconstruirListaPresets()
		{
			_listaPresets.Clear();
			LoadoutsPlayer datos = PersonajeVivo.Jugador.GetModPlayer<LoadoutsPlayer>();

			if (datos.Presets.Count == 0) {
				_listaPresets.Add(_sinPresets);
				return;
			}

			for (int i = 0; i < datos.Presets.Count; i++) {
				_listaPresets.Add(ConstruirFilaPreset(i, datos.Presets[i].Nombre));
			}
		}

		private UIElement ConstruirFilaPreset(int indice, string nombre)
		{
			UIElement fila = new UIElement();
			fila.Width.Set(0f, 1f);
			fila.Height.Set(30f, 0f);

			EtiquetaTk etiquetaNombre = new EtiquetaTk(() => nombre, 0.78f, 260f, 26f);
			etiquetaNombre.Left.Set(4f, 0f);
			etiquetaNombre.Top.Set(2f, 0f);
			fila.Append(etiquetaNombre);

			BotonTk aplicar = new BotonTk(Idiomas.Texto("Personaje.Conjuntos.Aplicar"), 0.75f);
			aplicar.Width.Set(110f, 0f);
			aplicar.Height.Set(26f, 0f);
			aplicar.Left.Set(270f, 0f);
			aplicar.Top.Set(2f, 0f);
			aplicar.Ayuda = () => Idiomas.Texto("Personaje.Conjuntos.AplicarAyuda");
			aplicar.AlPulsar += () => AplicarPreset(indice);
			fila.Append(aplicar);

			BotonTk borrar = new BotonTk(Idiomas.Texto("Personaje.Conjuntos.Borrar"), 0.75f);
			borrar.Width.Set(90f, 0f);
			borrar.Height.Set(26f, 0f);
			borrar.Left.Set(386f, 0f);
			borrar.Top.Set(2f, 0f);
			borrar.AlPulsar += () => BorrarPreset(indice);
			fila.Append(borrar);

			return fila;
		}

		/// <summary>Foto conjunta de armadura+tintes+ocultar, para que aplicar un preset sea UNA
		/// sola entrada del historial de deshacer/rehacer (WS7 ampliado), no tres sueltas.</summary>
		private sealed class EstadoEquipoActivo
		{
			public readonly Item[] Armadura;
			public readonly Item[] Tintes;
			public readonly bool[] Ocultar;

			private EstadoEquipoActivo(Item[] armadura, Item[] tintes, bool[] ocultar)
			{
				Armadura = armadura;
				Tintes = tintes;
				Ocultar = ocultar;
			}

			public static EstadoEquipoActivo Capturar(Player jugador)
			{
				Item[] armadura = new Item[jugador.armor.Length];
				for (int i = 0; i < armadura.Length; i++) {
					armadura[i] = jugador.armor[i].Clone();
				}
				Item[] tintes = new Item[jugador.dye.Length];
				for (int i = 0; i < tintes.Length; i++) {
					tintes[i] = jugador.dye[i].Clone();
				}
				bool[] ocultar = new bool[jugador.hideVisibleAccessory.Length];
				jugador.hideVisibleAccessory.CopyTo(ocultar, 0);
				return new EstadoEquipoActivo(armadura, tintes, ocultar);
			}

			public void Aplicar(Player jugador)
			{
				for (int i = 0; i < Armadura.Length && i < jugador.armor.Length; i++) {
					jugador.armor[i] = Armadura[i].Clone();
				}
				for (int i = 0; i < Tintes.Length && i < jugador.dye.Length; i++) {
					jugador.dye[i] = Tintes[i].Clone();
				}
				for (int i = 0; i < Ocultar.Length && i < jugador.hideVisibleAccessory.Length; i++) {
					jugador.hideVisibleAccessory[i] = Ocultar[i];
				}
			}
		}

		private void AplicarPreset(int indice)
		{
			Player jugador = PersonajeVivo.Jugador;
			LoadoutsPlayer datos = jugador.GetModPlayer<LoadoutsPlayer>();
			if (indice < 0 || indice >= datos.Presets.Count) {
				return;
			}

			PresetLoadout preset = datos.Presets[indice];
			EstadoEquipoActivo antes = EstadoEquipoActivo.Capturar(jugador);

			preset.AplicarSobreElActivo(jugador);

			EstadoEquipoActivo despues = EstadoEquipoActivo.Capturar(jugador);
			Historial.CambiarValor(
				Idiomas.Texto("Personaje.Conjuntos.HistorialAplicar", preset.Nombre),
				antes, despues, (EstadoEquipoActivo estado) => estado.Aplicar(jugador));

			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Conjuntos: aplicado el preset " +
				$"\"{preset.Nombre}\" sobre el conjunto activo (indice {jugador.CurrentLoadoutIndex}).");
		}

		private void BorrarPreset(int indice)
		{
			LoadoutsPlayer datos = PersonajeVivo.Jugador.GetModPlayer<LoadoutsPlayer>();
			if (indice < 0 || indice >= datos.Presets.Count) {
				return;
			}
			string nombre = datos.Presets[indice].Nombre;
			datos.Presets.RemoveAt(indice);
			ReconstruirListaPresets();
			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Conjuntos: borrado el preset \"{nombre}\".");
		}

		// ============================================================================
		// Guardar el conjunto activo como preset nuevo
		// ============================================================================

		private void ConstruirGuardarNuevo()
		{
			// VAlign ya ancla el elemento a la base del contenedor por si solo; el Top que va
			// DEBAJO es un desplazamiento en PIXELES PUROS desde esa base (segundo argumento de
			// Set en 0f, nunca en 1f - con 1f se suma ADEMAS un alto entero del contenedor, que es
			// justo el bug real que dejaba esta fila invisible, empujada muy por debajo del marco
			// del panel: visto en una captura real, "Guardar conjunto activo..." no aparecia por
			// ningun lado).
			EtiquetaTk etiqueta = new EtiquetaTk(() => Idiomas.Texto("Personaje.Conjuntos.NombreNuevo"), 0.78f, 220f, 22f);
			etiqueta.ColorTexto = EstiloTk.TextoSuave;
			etiqueta.Left.Set(0f, 0f);
			etiqueta.VAlign = 1f;
			etiqueta.Top.Set(-(AltoFilaGuardar + 4f), 0f);
			Append(etiqueta);

			_campoNombrePreset = new CampoTextoTk(() => Idiomas.Texto("Personaje.Conjuntos.NombrePista"), 24, 0.8f);
			_campoNombrePreset.Width.Set(260f, 0f);
			_campoNombrePreset.Height.Set(28f, 0f);
			_campoNombrePreset.Left.Set(0f, 0f);
			_campoNombrePreset.VAlign = 1f;
			_campoNombrePreset.Top.Set(-6f, 0f);
			Append(_campoNombrePreset);

			BotonTk guardar = new BotonTk(Idiomas.Texto("Personaje.Conjuntos.GuardarNuevo"), 0.8f);
			guardar.Width.Set(200f, 0f);
			guardar.Height.Set(28f, 0f);
			guardar.Left.Set(270f, 0f);
			guardar.VAlign = 1f;
			guardar.Top.Set(-6f, 0f);
			guardar.Ayuda = () => Idiomas.Texto("Personaje.Conjuntos.GuardarNuevoAyuda");
			guardar.AlPulsar += GuardarPresetNuevo;
			Append(guardar);
		}

		private void GuardarPresetNuevo()
		{
			string nombre = (_campoNombrePreset.Texto ?? "").Trim();
			if (nombre.Length == 0) {
				nombre = Idiomas.Texto("Personaje.Conjuntos.NombreSinTitulo");
			}

			Player jugador = PersonajeVivo.Jugador;
			LoadoutsPlayer datos = jugador.GetModPlayer<LoadoutsPlayer>();
			datos.Presets.Add(PresetLoadout.DesdeElActivo(jugador, nombre));

			_campoNombrePreset.FijarTextoSilencioso("");
			ReconstruirListaPresets();

			Terrakeep.Instance.Logger.Info($"{Terrakeep.LogTag} Conjuntos: guardado preset nuevo \"{nombre}\" " +
				$"desde el conjunto activo (indice {jugador.CurrentLoadoutIndex}). Total presets ahora={datos.Presets.Count}.");
		}

		public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
		{
			base.Update(gameTime);
			// El jugador puede cambiar de conjunto con las teclas del juego mientras el panel
			// esta abierto (igual que PestanaEquipo): los botones tienen que seguirlo. Los
			// EtiquetaTk de este archivo ya piden su texto de nuevo en cada DrawSelf (Func<string>
			// en el constructor), asi que el idioma en vivo no necesita nada extra aqui.
			ActualizarBotonesNativos();

			// Mismo patron que PestanaApariencia.Update -> AutopruebaApariencia.Avanzar(this): la
			// autoprueba solo puede avanzar mientras esta pestaña concreta esta construida y
			// dibujandose de verdad.
			AutopruebaConjuntos.Avanzar(this);
		}

		// ============================================================================
		// SOLO PARA LA AUTOPRUEBA (Common/Loadouts/AutopruebaConjuntos.cs)
		// ============================================================================

		/// <summary>Numero de botones "Conjunto" nativos construidos ahora mismo.</summary>
		internal int TotalNativosParaPrueba => _filasNativas.Count;

		/// <summary>Numero de presets guardados ahora mismo (segun lo que enseña la lista).</summary>
		internal int TotalPresetsParaPrueba => PersonajeVivo.Jugador.GetModPlayer<LoadoutsPlayer>().Presets.Count;

		/// <summary>
		/// Ejercita el camino real de renombrar (EmpezarRenombrar + TerminarRenombrar, los MISMOS
		/// metodos que llaman el boton "Renombrar" y el evento AlConfirmar del campo de texto) sin
		/// pasar por la entrada de teclado fisica - misma limitacion ya documentada en WS7
		/// (keybd_event no llega al juego) y mismo criterio de <c>CampoTextoTk.
		/// FijarTextoSilencioso</c> + disparo manual del evento que ya usa el resto del mod para
		/// probar campos de texto.
		/// </summary>
		internal void RenombrarParaPrueba(int indice, string nuevoNombre)
		{
			if (!_filasNativas[indice].Editando) {
				EmpezarRenombrar(indice);
			}
			TerminarRenombrar(indice, nuevoNombre);
		}

		/// <summary>Ejercita el camino real de guardar un preset nuevo (mismo GuardarPresetNuevo
		/// que llama el boton), con el nombre puesto a mano en el campo en vez de tecleado.</summary>
		internal void GuardarPresetNuevoParaPrueba(string nombre)
		{
			_campoNombrePreset.FijarTextoSilencioso(nombre);
			GuardarPresetNuevo();
		}

		/// <summary>Pulsa de verdad el boton "Aplicar" del preset en ese indice
		/// (<c>BotonTk.LeftMouseDown</c>, la misma via real que ya usa el resto del mod para
		/// probar botones - ver AutopruebaPersonaje, paso del deslizador de color).</summary>
		internal void AplicarPresetParaPrueba(int indice)
		{
			AplicarPreset(indice);
		}
	}
}
