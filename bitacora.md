# Bitácora de TerrakeepMod

Historial real de lo que se ha hecho y de los obstáculos que aparecieron. Misma disciplina que
el repo hermano `Terrasavr-Native`: se escribe aquí lo que costó, no solo lo que salió bien.

---

## 6-sep-2026 — WS0: cimientos

Primer workstream del plan (`C:\Users\adrian\.claude\plans\streamed-leaping-balloon.md`).
Objetivo: que exista un mod real, compilable e instalable, que reutilice `TerrasavrNative.Core`
y que demuestre en el juego que la cadena entera funciona. **Ninguna funcionalidad de Terrakeep
todavía**: eso son WS1..WS7.

### Lo que se hizo

1. **`TerrasavrNative.Core` pasa a doble destino** (`net10.0;net8.0`) en el repo hermano
   (commit `adeb5283` allí). Sin tocar ni una línea de C#: Core no tiene dependencias externas y
   no usa sintaxis posterior a C# 12. Se dejó a propósito **sin `LangVersion` explícito**, para
   que cualquier feature de C# 13/14 que se cuele en el futuro rompa la build de net8.0 en el
   acto en vez de romper el mod en silencio mucho después.
2. **Proyecto del mod creado** con la estructura real de tModLoader (`build.txt`,
   `description.txt`, `.csproj` importando `..\tModLoader.targets`, `Localization\*.hjson`,
   `lib\TerrasavrNative.Core.dll`). Las plantillas se sacaron del código decompilado real
   (`Terraria.ModLoader.Templates.*` dentro de `tModLoader-Decompiled\tModLoader\`), no de
   memoria.
3. **Panel de prueba**: `ModKeybind` (tecla **K** por defecto) → `IngameFancyUI.OpenUIState` →
   `UIPanel` con un `ItemSlot` **nativo de vanilla** enganchado directamente a
   `Main.LocalPlayer.inventory[0]`, más código de Core ejecutándose sobre ese objeto real.

### Verificado de verdad

| Qué | Cómo | Resultado |
|---|---|---|
| Core compila para los dos destinos | `dotnet build TerrasavrNative.slnx` | 0 errores, genera `net10.0\` y `net8.0\` |
| El DLL net8 es net8 de verdad | cargarlo con reflexión desde PowerShell | pide `System.Runtime 8.0.0.0` |
| No se rompió la app de escritorio | `dotnet test` Core y ViewModels | **368/368** y **329/329** |
| El mod compila con el compilador REAL de tModLoader | `scripts\compilar.ps1` (fase 2, sin `-eac`) | "Compilation finished with 0 errors and 0 warnings" |
| Se genera un `.tmod` real | `Mods\TerrakeepMod.tmod` | 104.558 bytes |
| El `.tmod` lleva Core dentro | `node tmod-extract.js` | contiene `lib/TerrasavrNative.Core.dll` |
| **El mod carga en tModLoader real y Core se EJECUTA en su runtime .NET 8** | `scripts\verificar-en-juego.ps1 -Servidor` | ver `evidencia\ws0-servidor-server.log.txt` |

La línea que lo demuestra, sacada del `server.log` real del juego:

```
[TerrakeepMod]: [Terrakeep] Mod cargado. Prueba de humo de TerrasavrNative.Core:
GameItem(Id=3389).IsEmpty=False, .IsCalamity=False,
ensamblado real = TerrasavrNative.Core, Version=1.0.0.0
```

Son propiedades **calculadas en tiempo de ejecución**, no constantes: si fueran `const`, el
compilador las habría copiado en el sitio y la línea no probaría nada. Ese era el bloqueo
técnico real que señalaba el plan (un DLL net10 no carga en el runtime .NET 8 de tModLoader), y
queda cerrado.

### Lo que NO se pudo verificar, y por qué

**El panel de prueba NO se ha llegado a ver funcionando en el cliente gráfico.** No es un
problema del mod: la máquina no estaba en condiciones de ejecutarlo.

Encadenado, lo que pasó:

1. **La estación de trabajo de Windows estaba BLOQUEADA** toda la sesión (`LogonUI` en
   ejecución, comprobado). Consecuencias medidas, no supuestas:
   - `Graphics.CopyFromScreen` falla con "Controlador no válido" → no hay capturas de pantalla.
   - Los clics reales de ratón no llegan a ninguna aplicación.
2. **El sistema no tiene NINGUNA salida de audio disponible.** Comprobado por COM con
   `IMMDeviceEnumerator`: `GetDefaultAudioEndpoint` devuelve `0x80070490` (ERROR_NOT_FOUND) y
   `EnumAudioEndpoints(eRender, ALL)` devuelve **cero** endpoints en cualquier estado (los
   auriculares y el monitor están apagados/desconectados; los *drivers* sí están, los
   *endpoints* no).
3. Por eso `SoundEngine.Initialize()` lanza `NoAudioHardwareException` y llama a
   `Utils.ShowFancyErrorMessage(..., 10002)`. **El juego se queda en un diálogo modal a pantalla
   completa que solo se cierra con un clic de ratón** (`UIErrorMessage` no tiene ningún atajo de
   teclado, se leyó su código) **y eso ocurre ANTES de cargar los mods**. El cliente nunca llegó
   ni a intentar cargar TerrakeepMod.

Intentos hechos para salvarlo, y por qué fallaron:

- **Clic sintético** (`PostMessage` de `WM_ACTIVATE` + `WM_MOUSEMOVE` + `WM_LBUTTONDOWN/UP` al
  HWND real, en las coordenadas del botón "Continuar" calculadas a partir del código de
  `UIErrorMessage`: centro (379, 627) con área de cliente 1280×720). No sirvió. La razón está en
  `Main.cs:17092`: `hasFocus = IsActive;` y justo debajo, si no hay foco, el juego pone
  `mouseLeftRelease = false` **cada fotograma**, así que el clic nunca cuenta como pulsación
  nueva. Sin sesión de escritorio no se puede dar foco real.
- **Driver de audio ficticio** (`SDL_AUDIODRIVER=dummy`). No sirve: FAudio 22.9 en Windows usa
  su backend nativo WASAPI, no el subsistema de audio de SDL.

**Se paró ahí, siguiendo la regla de no insistir dos veces por la misma causa.** Descartado
también instalar un driver de audio virtual: sería un cambio invasivo a nivel de sistema, que
la regla de autonomía técnica del usuario excluye expresamente.

Queda **`scripts\verificar-en-juego.ps1`** (sin `-Servidor`) listo para cerrar esta parte de un
solo comando en cuanto haya sesión de escritorio y audio: lanza el cliente con `-skipselect` y
la variable `TERRAKEEP_AUTOTEST`, con lo que el mod entra al mundo de prueba y abre el panel
**él solo**, sin depender de que nadie pulse ninguna tecla, y deja en `client.log` el objeto
real del inventario y el rectángulo en pantalla del `ItemSlot`.

### Efecto colateral en el repo hermano

El arnés de UI Automation de la app de escritorio (`dotnet run --project
TerrasavrNative.App.Tests`) **tampoco se pudo completar**, por la misma causa. Se colgó
indefinidamente dentro de
`UIAutomationClient!System.Windows.Automation.SelectionItemPattern.Select()` (pila real obtenida
con `dotnet-stack report`, CPU congelada durante más de una hora), y sus **dos únicas** líneas
`FALLO` son las dos de la prueba `UI-BLOQUEADA`, que es precisamente la que hace clics reales de
ratón. Las demás ~60 comprobaciones que llegó a ejecutar pasaron. Queda anotado también en la
bitácora del repo hermano como pendiente de repetir con la sesión desbloqueada.

### Decisiones técnicas tomadas aquí (no estaban en el plan)

- **`side = NoSync`** en vez de `Client`. Es lo correcto para una herramienta local (no añade
  contenido que sincronizar), y además tiene una ventaja práctica grande: con `NoSync` el mod
  carga también en el **servidor dedicado**, que arranca sin ventana y sin audio. Eso es lo que
  ha permitido verificar la carga del mod y de Core de forma automática y reproducible pese al
  entorno.
- **Compilación en dos fases** (`scripts\compilar.ps1`). El `dotnet build` normal falla siempre
  en esta máquina: `tMLMod.targets` invoca `dotnet tModLoader.dll -server -build` usando el
  `dotnet` **del PATH**, que aquí es el SDK 10.0.400 sin ningún runtime .NET 8 instalado
  (`dotnet --list-runtimes`: 6.0.36, 9.0.14, 10.0.3, 10.0.11), y `tModLoader.runtimeconfig.json`
  exige `Microsoft.NETCore.App 8.0.0` → MSB3073, código -2147450730. El script usa el runtime
  que trae el propio tModLoader en `dotnet\dotnet.exe`, que es el mismo que usa su lanzador
  oficial.
- **Sin `-eac`** en la fase de empaquetado, para que compile el Roslyn interno de tModLoader y
  no se reutilice el DLL de MSBuild. Es lo que de verdad prueba que el código compila en las
  condiciones reales del juego (`LanguageVersion.Preview`, sin usings implícitos, nullable
  desactivado).
- **El `.csproj` repite `TargetFramework`/`LangVersion`** aunque ya los fije `tMLMod.targets`:
  allí van dentro de un `Condition="'$(BuildMod)' == 'true'"`, así que con `-p:BuildMod=false`
  (fase 1 del script) el proyecto se quedaba sin TFM y el SDK abortaba con NETSDK1013.
- **La clase raíz se llama `Terrakeep`, no `TerrakeepMod`**. La plantilla oficial mete la clase
  `Mod` en un namespace con su mismo nombre, lo que obliga a cualificar cada referencia desde
  los demás archivos. El nombre real del mod lo da la carpeta, no la clase.
- **Sandbox de pruebas aislado** en `Documents\My Games\Terraria\tModLoader-TerrakeepWS0` vía
  `-tmlsavedirectory`, con una copia de `prueba.plr` y un mundo pequeño generado desde cero
  (`TerrakeepPrueba`, 4200×1200, clásico). Los personajes y mundos reales del usuario no se han
  tocado en ningún momento. Sin `enabled.json` con Calamity, así que las pruebas cargan solo
  este mod y arrancan rápido.
- **La evidencia es el log del juego, no capturas.** Todas las líneas del mod llevan el prefijo
  `[Terrakeep]` para poder filtrarlas con un grep limpio. Es más fiable que una captura incluso
  con escritorio disponible, y aquí era además la única vía.

### Detalle suelto, sin resolver (no bloquea nada)

`TerrasavrNative.Core` **no consigue leer `prueba.plr`**: `PlrBodySerializer.ReadServers` lanza
`EndOfStreamException`. Se detectó de pasada al intentar averiguar el nombre y la dificultad del
personaje de prueba. **No es una regresión del cambio de multi-targeting** (no se tocó nada de
C#), y el juego sí carga ese personaje sin problema. Queda anotado por si le interesa al repo
hermano; no se investigó más porque se sale de WS0.
