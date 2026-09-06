using Terraria;
using Terraria.UI;
using TerrakeepMod.Common.Personaje;
using TerrakeepMod.UI.Personaje.Widgets;

namespace TerrakeepMod.UI.Personaje
{
	/// <summary>
	/// Cabecera del panel: nombre, vida, mana y dinero del personaje vivo.
	/// <para />
	/// El dinero NO se guarda en ningun campo del jugador: en Terraria las monedas son objetos
	/// normales, asi que hay que contarlas recorriendo el inventario y los cuatro almacenes. Se
	/// hace con <c>Utils.CoinsCount</c>, la misma funcion que usa el juego para el cartel de
	/// "dinero cercano", asi que el total coincide con el suyo.
	/// </summary>
	public class CabeceraPersonaje : UIElement
	{
		private CampoTextoTk _campoNombre;

		public CabeceraPersonaje()
		{
			Width.Set(0f, 1f);
			Height.Set(66f, 0f);

			ConstruirNombre();
			ConstruirVidaYMana();
			ConstruirDinero();
		}

		private void ConstruirNombre()
		{
			EtiquetaTk etiqueta = new EtiquetaTk(() => "Nombre", 0.85f, 60f, 24f);
			etiqueta.ColorTexto = EstiloTk.TextoSuave;
			etiqueta.Left.Set(0f, 0f);
			etiqueta.Top.Set(6f, 0f);
			Append(etiqueta);

			_campoNombre = new CampoTextoTk("(sin nombre)", 20);
			_campoNombre.Width.Set(190f, 0f);
			_campoNombre.Height.Set(28f, 0f);
			_campoNombre.Left.Set(62f, 0f);
			_campoNombre.Top.Set(0f, 0f);
			_campoNombre.FijarTextoSilencioso(Main.LocalPlayer.name);
			_campoNombre.AlCambiar += texto => {
				// Se escribe en vivo sobre el jugador real. El nombre del ARCHIVO .plr no cambia
				// (lo fija PlayerFileData al crearlo), solo el nombre que se ve en la partida y
				// el que se guardara dentro del archivo en el siguiente guardado.
				Main.LocalPlayer.name = texto;
			};
			_campoNombre.AlConfirmar += texto => {
				Terrakeep.Instance.Logger.Info(
					$"{Terrakeep.LogTag} Nombre del jugador cambiado en vivo a \"{texto}\" " +
					$"(Player.name real = \"{Main.LocalPlayer.name}\").");
			};
			Append(_campoNombre);
		}

		private void ConstruirVidaYMana()
		{
			SelectorTk vida = new SelectorTk("Vida maxima",
				() => Main.LocalPlayer.statLifeMax + " (efectiva " + Main.LocalPlayer.statLifeMax2 + ")",
				paso => {
					Player jugador = Main.LocalPlayer;
					jugador.statLifeMax = PersonajeVivo.Acotar(
						jugador.statLifeMax + paso * 20, 100, PersonajeVivo.VidaMaximaBase);
					if (jugador.statLife > jugador.statLifeMax) {
						jugador.statLife = jugador.statLifeMax;
					}
				},
				110f, 5, 320f);
			vida.Left.Set(270f, 0f);
			vida.Top.Set(0f, 0f);
			Append(vida);

			SelectorTk mana = new SelectorTk("Mana maximo",
				() => Main.LocalPlayer.statManaMax + " (efectivo " + Main.LocalPlayer.statManaMax2 + ")",
				paso => {
					Player jugador = Main.LocalPlayer;
					jugador.statManaMax = PersonajeVivo.Acotar(
						jugador.statManaMax + paso * 20, 0, PersonajeVivo.ManaMaximoBase);
					if (jugador.statMana > jugador.statManaMax) {
						jugador.statMana = jugador.statManaMax;
					}
				},
				110f, 5, 320f);
			mana.Left.Set(270f, 0f);
			mana.Top.Set(32f, 0f);
			Append(mana);

			EtiquetaTk actuales = new EtiquetaTk(
				() => "Ahora: " + Main.LocalPlayer.statLife + "/" + Main.LocalPlayer.statLifeMax2
					+ " vida, " + Main.LocalPlayer.statMana + "/" + Main.LocalPlayer.statManaMax2 + " mana",
				0.85f, 340f, 24f);
			actuales.Left.Set(610f, 0f);
			actuales.Top.Set(6f, 0f);
			Append(actuales);

			BotonTk llenar = new BotonTk("Llenar vida y mana", 0.8f);
			llenar.Width.Set(170f, 0f);
			llenar.Height.Set(28f, 0f);
			llenar.Left.Set(610f, 0f);
			llenar.Top.Set(32f, 0f);
			llenar.AlPulsar += () => {
				Player jugador = Main.LocalPlayer;
				jugador.statLife = jugador.statLifeMax2;
				jugador.statMana = jugador.statManaMax2;
				Terrakeep.Instance.Logger.Info(
					$"{Terrakeep.LogTag} Vida y mana rellenados en vivo: " +
					$"statLife={jugador.statLife}/{jugador.statLifeMax2}, " +
					$"statMana={jugador.statMana}/{jugador.statManaMax2}.");
			};
			Append(llenar);
		}

		private void ConstruirDinero()
		{
			EtiquetaTk dinero = new EtiquetaTk(TextoDinero, 0.85f, 620f, 24f);
			dinero.Left.Set(0f, 0f);
			dinero.Top.Set(34f, 0f);
			Append(dinero);

			// El aviso de "todo esto se escribe en vivo sobre el personaje cargado" ya NO va aqui:
			// al fusionar los seis paneles en uno, ese texto pasó al pie comun del panel unico
			// (PanelTerrakeepState), que es donde cada area deja su linea de ayuda. Tenerlo aqui
			// ademas costaba 22 px de alto de cabecera que ahora usan las ranuras.
		}

		private static string TextoDinero()
		{
			long enInventario;
			long enAlmacenes;
			long total = PersonajeVivo.DineroTotal(out enInventario, out enAlmacenes);

			return "Dinero: " + PersonajeVivo.FormatearDinero(total)
				+ "  (inventario " + PersonajeVivo.FormatearDinero(enInventario)
				+ " | almacenes " + PersonajeVivo.FormatearDinero(enAlmacenes) + ")";
		}
	}
}
