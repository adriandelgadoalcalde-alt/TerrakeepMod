using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TerrakeepMod.Common;
using TerrasavrNative.Core.Model;

namespace TerrakeepMod.UI
{
	/// <summary>
	/// Panel minimo de WS0. No es funcionalidad de Terrakeep: existe solo para demostrar, dentro
	/// del juego real y de principio a fin, que la cadena entera funciona:
	/// <list type="number">
	/// <item>el mod compila y carga con <c>dllReferences = TerrasavrNative.Core</c>;</item>
	/// <item>se abre a pantalla completa con <c>IngameFancyUI.OpenUIState</c>, el mismo mecanismo
	/// que usan el bestiario y los menus de ajustes del propio juego;</item>
	/// <item>un <c>ItemSlot</c> nativo de vanilla muestra y deja manipular
	/// <c>Main.LocalPlayer.inventory[0]</c> EN VIVO;</item>
	/// <item>codigo de TerrasavrNative.Core se ejecuta de verdad sobre ese objeto real.</item>
	/// </list>
	/// Todo lo que pasa aqui queda registrado en tModLoader-Logs\client.log con el prefijo
	/// <c>[Terrakeep]</c>, que es la evidencia real de la prueba.
	/// </summary>
	public class PanelPruebaState : UIState
	{
		private const int IndiceSlotProbado = 0;

		private SlotObjetoVanilla _slot;
		private UIText _descripcion;

		// Ultimo estado conocido del objeto del slot, para detectar y registrar cambios reales
		// provocados por el usuario (coger el objeto con el raton, soltar otro encima...).
		private int _ultimoTipo = -1;
		private int _ultimaPila = -1;
		private bool _coordenadasRegistradas;

		public override void OnInitialize()
		{
			UIPanel marco = new UIPanel();
			marco.Width.Set(420f, 0f);
			marco.Height.Set(260f, 0f);
			marco.HAlign = 0.5f;
			marco.VAlign = 0.5f;
			marco.BackgroundColor = new Color(33, 43, 79) * 0.9f;
			Append(marco);

			UIText titulo = new UIText("Terrakeep - panel de prueba (WS0)", 0.9f, true);
			titulo.HAlign = 0.5f;
			titulo.Top.Set(12f, 0f);
			marco.Append(titulo);

			UIText subtitulo = new UIText("Ranura 1 del inventario, en vivo:", 0.75f);
			subtitulo.HAlign = 0.5f;
			subtitulo.Top.Set(58f, 0f);
			marco.Append(subtitulo);

			_slot = new SlotObjetoVanilla(Main.LocalPlayer.inventory, IndiceSlotProbado, ItemSlot.Context.InventoryItem, 1f);
			_slot.HAlign = 0.5f;
			_slot.Top.Set(92f, 0f);
			marco.Append(_slot);

			_descripcion = new UIText("", 0.75f);
			_descripcion.HAlign = 0.5f;
			_descripcion.Top.Set(158f, 0f);
			marco.Append(_descripcion);

			UITextPanel<string> cerrar = new UITextPanel<string>("Cerrar", 0.8f, false);
			cerrar.Width.Set(130f, 0f);
			cerrar.Height.Set(36f, 0f);
			cerrar.HAlign = 0.5f;
			cerrar.Top.Set(196f, 0f);
			cerrar.OnLeftClick += (evento, elemento) => PanelPruebaSystem.CerrarPanel("boton Cerrar");
			marco.Append(cerrar);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			Item objeto = _slot.ObjetoActual;

			// Codigo REAL de TerrasavrNative.Core ejecutandose sobre el objeto vivo del jugador.
			// Es la parte que demuestra que el DLL net8 del repo hermano no solo esta empaquetado
			// sino que carga y corre dentro del runtime .NET 8 de tModLoader.
			GameItem comoCore = new GameItem { Id = objeto.type, Count = objeto.stack };
			string resumenCore = comoCore.IsEmpty
				? "Core: ranura vacia (GameItem.IsEmpty = true)"
				: $"Core: GameItem(Id={comoCore.Id}, Count={comoCore.Count}), IsCalamity={comoCore.IsCalamity}";

			_descripcion.SetText(objeto.IsAir
				? "(vacia) - " + resumenCore
				: $"{objeto.Name} x{objeto.stack} (type={objeto.type}) - {resumenCore}");

			if (objeto.type != _ultimoTipo || objeto.stack != _ultimaPila) {
				// Solo se registra el cambio, no el estado en cada frame: si no, el log se
				// llenaria con 60 lineas por segundo y dejaria de servir como evidencia.
				if (_ultimoTipo != -1) {
					Terrakeep.Instance.Logger.Info(
						$"{Terrakeep.LogTag} ItemSlot: el contenido de inventory[{IndiceSlotProbado}] ha CAMBIADO en vivo: " +
						$"antes type={_ultimoTipo} stack={_ultimaPila} -> ahora type={objeto.type} stack={objeto.stack} " +
						$"(\"{objeto.Name}\"). Objeto en el raton: type={Main.mouseItem.type} stack={Main.mouseItem.stack}.");
				}
				_ultimoTipo = objeto.type;
				_ultimaPila = objeto.stack;
			}

			if (!_coordenadasRegistradas) {
				CalculatedStyle dim = _slot.GetDimensions();
				if (dim.Width > 0f) {
					_coordenadasRegistradas = true;
					Terrakeep.Instance.Logger.Info(
						$"{Terrakeep.LogTag} ItemSlot dibujado. Rectangulo en coordenadas de pantalla del juego: " +
						$"x={(int)dim.X} y={(int)dim.Y} w={(int)dim.Width} h={(int)dim.Height} " +
						$"centro=({(int)(dim.X + dim.Width / 2f)},{(int)(dim.Y + dim.Height / 2f)}). " +
						$"Resolucion actual: {Main.screenWidth}x{Main.screenHeight}.");
				}
			}
		}
	}
}
