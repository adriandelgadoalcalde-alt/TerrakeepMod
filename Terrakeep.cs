using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terrakeep.Core.Model;

namespace TerrakeepMod
{
	/// <summary>
	/// Clase raiz del mod. WS0 (cimientos): lo unico que hace todavia es registrar el atajo de
	/// teclado que abre el panel de prueba y dejar constancia en el log de que la cadena entera
	/// esta montada (mod cargado + Terrakeep.Core cargado y ejecutandose de verdad dentro
	/// del runtime .NET 8 de tModLoader).
	/// <para />
	/// Las funcionalidades reales de Terrakeep (inventario, librería, mapa, etc.) son los
	/// workstreams WS1-WS7, todavia sin empezar. Ver bitacora.md y CLAUDE.md de esta carpeta.
	/// </summary>
	/// <remarks>
	/// La clase se llama <c>Terrakeep</c> y no <c>TerrakeepMod</c> (que es el nombre del mod, o
	/// sea el de la carpeta) a proposito: la plantilla de tModLoader mete la clase Mod dentro de
	/// un namespace con su mismo nombre, y eso obliga luego a cualificar cada referencia desde
	/// los demas archivos para deshacer la ambiguedad tipo/namespace. El nombre real del mod lo
	/// determina la carpeta (ModCompile.ReadBuildInfo hace Path.GetFileName del directorio), no
	/// esta clase, asi que renombrarla no cambia nada de cara al juego.
	/// </remarks>
	public class Terrakeep : Mod
	{
		/// <summary>Prefijo comun de todas las lineas de este mod en tModLoader-Logs\client.log.
		/// Sirve para poder filtrar la evidencia real de las pruebas con un grep limpio.</summary>
		public const string LogTag = "[Terrakeep]";

		/// <summary>Instancia viva del mod. La rellena <see cref="Load"/>.</summary>
		public static Terrakeep Instance { get; private set; }

		/// <summary>Atajo que abre y cierra el panel. Solo existe en cliente.</summary>
		public static ModKeybind AbrirPanelKeybind { get; private set; }

		public override void Load()
		{
			Instance = this;

			if (!Main.dedServ) {
				// K no esta asignada a nada por defecto en Terraria, asi que no pisa ningun
				// control vanilla. El usuario puede reasignarla en Ajustes > Controles.
				AbrirPanelKeybind = KeybindLoader.RegisterKeybind(this, "AbrirPanel", Keys.K);
			}

			// Prueba de humo REAL del dllReferences: se instancia un tipo de
			// Terrakeep.Core y se llama a un miembro calculado suyo. Tiene que ser una
			// llamada de verdad, no leer una constante: las const de C# se copian en tiempo de
			// compilacion y no demostrarian que el DLL llega a cargarse en runtime.
			GameItem sonda = new GameItem { Id = 3389, Count = 1 };
			Logger.Info($"{LogTag} Mod cargado. Prueba de humo de Terrakeep.Core: " +
				$"GameItem(Id={sonda.Id}).IsEmpty={sonda.IsEmpty}, .IsCalamity={sonda.IsCalamity}, " +
				$"ensamblado real = {typeof(GameItem).Assembly.FullName}");
		}

		public override void Unload()
		{
			AbrirPanelKeybind = null;
			Instance = null;
		}
	}
}
