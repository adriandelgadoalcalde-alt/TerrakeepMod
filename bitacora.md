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

## WS0 cerrado de verdad: panel visto en el cliente gráfico real (6-sep-2026)

La entrega anterior de WS0 dejó sin confirmar la mitad grafica (pantalla bloqueada + sin salida
de audio en la máquina en ese momento). Al desbloquear la sesión y reintentar, aparecieron DOS
problemas reales, los dos corregidos y verificados:

**1. El "personaje de prueba" del sandbox era una copia LITERAL del `prueba.plr` real del
usuario** (confirmado con `cmp` byte a byte - mismos 3808 bytes) - ya sabíamos por la auditoría
de la app de escritorio que ese archivo concreto está corrupto, y aquí se vio la consecuencia
real: tModLoader entero petaba al hacer spawn con `NullReferenceException` en
`Terraria.GameContent.Creative.CreativePowers.GodmodePower.ApplyLoadedDataToOutOfPlayerFields`
(vía `CreativePowerManager.ApplyLoadedDataToPlayer` → `Player.SetPlayerDataToOutOfClassFields`)
- nada que ver con `TerrakeepMod`, el stack no lo menciona en ningún sitio. Corregido generando
un personaje sintético limpio (`TerrakeepPrueba.plr`, `PlrFile.Write` de
`TerrasavrNative.Core` sobre un `PlrCharacter` nuevo, dificultad 0) - nunca derivado de ningún
archivo real del usuario, y el script de verificación se actualizó para usarlo en vez de
`prueba`.

**2. Bug real en `PanelPruebaSystem.UpdateUI`**: con el personaje limpio, el juego ya no petaba,
pero el panel seguía sin abrirse - `client.log` mostraba una "Excepción silenciosa" repetida:
`KeyNotFoundException` en `ModKeybind.JustPressed` (indexa
`PlayerInput.Triggers.JustPressed.KeyStatus` por `"TerrakeepMod/AbrirPanel"`). Investigado con
`ilspycmd` sobre el **`tModLoader.dll` real instalado** (v2026.7.3.0 - la referencia decompilada
del repo hermano, `tModLoader-Decompiled\`, es de la versión 1.4.4.9, bastante más antigua; para
dudas de API de esta versión concreta, decompilar el `.dll` instalado directamente, más fiable
que la referencia vieja): `PlayerInput.Triggers` solo se rellena con los atajos base de vanilla
en `Main.Initialize()`, **antes de que ningún mod cargue**; los atajos de mods se añaden después
vía `PlayerInput.reinitialize` (activado por `ModContent.cs:541`), que el motor solo consume en
su siguiente `PlayerInput.UpdateInput()`. Con `-skipselect` entrando directo a una partida, ese
hueco se ha visto persistir varios segundos. El bug real no era el hueco en sí (se autocorrige
solo) sino que la excepción abortaba el resto de `UpdateUI` **antes** de llegar a
`ActualizarAutoprueba()` (la comprobación del atajo estaba primero) - así que la autoprueba
nunca llegaba a dispararse por mucho que pasaran los 180 fotogramas de espera. Corregido: la
autoprueba va primero e incondicional, y la comprobación del atajo se protege con
`try/catch(KeyNotFoundException)`.

**Evidencia real final** (`client.log`, filtrado por `[Terrakeep]`):
```
PANEL ABIERTO via autoprueba (TERRAKEEP_AUTOTEST). Jugador: "TerrakeepPrueba" (vida 100/100).
Mundo: "TerrakeepPrueba". inventory[0]: type=0 stack=0 prefix=0 nombre="".
Main.inFancyUI=True, InGameUI.CurrentState=TerrakeepMod.UI.PanelPruebaState
ItemSlot dibujado. Rectangulo en coordenadas de pantalla del juego: x=614 y=334 w=52 h=52
centro=(640,360). Resolucion actual: 1280x720.
```
`inventory[0]` vacío es correcto (el personaje sintético no tiene objetos) - lo que demuestra
esta línea es que el `ItemSlot` nativo de vanilla está leyendo de verdad
`Main.LocalPlayer.inventory[0]` y dibujándose en coordenadas reales de pantalla, dentro de un
panel abierto con `IngameFancyUI.OpenUIState`. **WS0 queda cerrado del todo**: la cadena
completa (mod compilado con `dllReferences` a `TerrasavrNative.Core` net8 + UI nativa de
Terraria + acceso en vivo al jugador real) funciona de principio a fin, verificado con el
cliente gráfico real, no solo con el servidor dedicado.

A partir de aquí, según el plan (`streamed-leaping-balloon.md`), tocan WS1/WS2/WS4/WS7 en
paralelo.

---

## 6-sep-2026 — WS7: deshacer/rehacer + Ajustes e idioma

Workstream 7 del plan, en paralelo con WS1 (Personaje), WS4 (Builds) y WS2 (datos en el repo
hermano). Dos piezas independientes: el modelo de deshacer/rehacer que van a compartir todos los
paneles del mod, y el panel de Ajustes con cambio de idioma en vivo.

### 1. Deshacer/rehacer: snapshots, no closures

`Common/Undo/PilaDeSnapshots.cs` (la pila, genérica y sin dependencias) + `Common/Undo/
Historial.cs` (la fachada, ya con tipos de Terraria) + `Common/Undo/HistorialSystem.cs` (atajos y
ciclo de vida).

**Por qué no vale el modelo de la app de escritorio.** `TerrasavrNative.App/Services/UndoStack.cs`
guarda por entrada dos closures `Undo`/`Redo` encadenadas. Ahí funciona porque el editor es el
único que toca el personaje: entre deshacer y rehacer no puede haber pasado nada más. En una
partida en marcha esa suposición es falsa — el jugador recoge objetos, los NPCs pegan, el
autoguardado escribe. Una closure del tipo "quita 1 a la pila del slot 3" aplicada sobre un
estado que ya cambió corrompe los datos en silencio. Una **foto completa del subconjunto tocado**
no: como mucho pisa un cambio ajeno, pero siempre deja un estado coherente. Es el mismo patrón
que ya usa la propia app para su deshacer local de 6 s al vaciar un contenedor
(`ContainerViewModel.ClearAll` guarda el array de objetos tal cual estaban).

Cada entrada guarda **dos** fotos, la de antes y la de después (esta capturada en el momento de
la acción original), y la función que sabe volcarlas. `Item.Clone()` sirve como copia profunda de
verdad: clona también `ModItem` y los `GlobalItem` (comprobado en el `tModLoader.dll` instalado).
Se vuelve a clonar en cada volcado, para que la foto siga siendo válida al deshacer y rehacer
varias veces seguidas.

**Vive en el MOD, no en `TerrasavrNative.Core`, y es una decisión, no un descuido.** La pila en sí
no depende de nada (sería portable tal cual), pero:
- lo único portable serían esas ~200 líneas, mientras que todo lo que las hace útiles aquí
  (clonar `Item`, saber que un array es `Player.inventory`, vaciar el historial al cambiar de
  mundo) necesita tipos de Terraria y no podría acompañarlas;
- meterlas en Core obligaría a regenerar y re-empaquetar `lib\TerrasavrNative.Core.dll` con
  `scripts\actualizar-core.ps1` en cada retoque del historial;
- y Core lo está tocando otro workstream ahora mismo.

Queda anotado en el propio código por si algún día compensa moverlo.

### 2. Ajustes e idioma en vivo (tecla J)

`Common/Ajustes/` + `UI/Ajustes/PanelAjustesState.cs` + `Localization/README.md`.

- **Localización**: mecanismo oficial de tModLoader, un `.hjson` por idioma en `Localization\` y
  `Language.GetTextValue("Mods.TerrakeepMod.<clave>")`, envuelto en `Idiomas.Texto()`. Todo el
  texto del panel de Ajustes sale de ahí, ni una cadena fija. Cómo migrar los textos de los demás
  paneles está escrito en `Localization/README.md` (entregable de WS7); **no se ha tocado el
  texto de ningún otro workstream**, eso es una pasada de integración posterior.
- **Cambio en vivo**: `LanguageManager.Instance.SetLanguage(cultura)`, la misma llamada pública
  que usa el menú de idioma del propio juego. Recarga los textos de Terraria y los de todos los
  mods y avisa por `ModSystem.OnLocalizationsLoaded`, que es donde el panel se repinta.
- **Efecto que hay que conocer**: en tModLoader **no existe un "idioma solo para mi mod"**. Un
  `LocalizedText` guarda un único valor, el de la cultura activa, así que poner Terrakeep en
  inglés pone el juego entero en inglés. Por eso el valor por defecto es *"el del juego"*.
- **Persistencia**: `ModConfig` con `ConfigScope.ClientSide`. En esta versión `SaveChanges()` es
  público y hace lo correcto dentro de la partida (guarda, recarga y llama a `OnChanged`).
  Verificado: `ModConfigs\TerrakeepMod_AjustesConfig.json` queda con `{"Idioma": 1}` y la partida
  siguiente arranca ya en español.

### El fallo gordo que apareció por el camino: NINGÚN atajo del mod tenía tecla

Al ir a probar Ctrl+Z de verdad, el diagnóstico dejó esto en el log del juego:

```
Teclas asignadas a CADA atajo del mod: AbrirPanel(WS0)=[], AbrirAjustes(WS7)=[],
Deshacer=[], Rehacer=[]. Para comparar, un atajo VANILLA: QuickHeal=[H]
```

**La tecla que se le pasa a `KeybindLoader.RegisterKeybind(mod, nombre, Keys.X)` no se aplica
sola.** Un `ModKeybind` recién registrado nace sin ninguna tecla. La cadena real, leída en el
`tModLoader.dll` instalado (v2026.7.3.0):

1. `KeyConfiguration.SetupKeys()` mete cada atajo de mod en el perfil **con la lista vacía**;
2. `PlayerInput.Reset(...)`, que reparte las teclas por defecto, solo conoce los atajos de
   vanilla — no toca los de mods;
3. `PlayerInputProfile.Load(...)` → `ReadPreferences` solo copia lo que ya estuviera en
   `input profiles.json`, y un atajo nuevo no está;
4. el **único** sitio de todo tModLoader que lee `ModKeybind.DefaultBinding` es la pantalla de
   Controles (`UIManageControls`), al pulsar "Restablecer".

Esto afectaba a **todo el mod**, no solo a WS7: la tecla K de WS0 tampoco funcionaba (aquel panel
se abrió por la autoprueba, nunca por el teclado), ni la J ni la L. Arreglado en
`Common/Ajustes/SembradorDeAtajos.cs`: recorre las claves del perfil que empiezan por
`TerrakeepMod/` — así cubre también los atajos de los demás workstreams sin tocar sus archivos —
y a las que estén vacías les aplica su default llamando a
`PlayerInputProfile.CopyIndividualModKeybindSettingsFrom`, que es público y es exactamente lo que
ejecuta el botón "Restablecer". Se hace **una sola vez por atajo**, anotado en el `ModConfig`: si
el usuario le quita la tecla a mano después, no se le vuelve a poner. Resultado en el juego:

```
Atajos sembrados por primera vez (4): TerrakeepMod/AbrirPanel=[K],
TerrakeepMod/AbrirAjustes=[J], TerrakeepMod/Deshacer=[Z], TerrakeepMod/Rehacer=[Y].
```

**Combinaciones con modificador**: no existen en esta versión. Las dos sobrecargas de
`RegisterKeybind` aceptan una sola tecla y `ModKeybind` resuelve su estado indexando
`PlayerInput.Triggers.JustPressed.KeyStatus[FullName]`, un diccionario de tecla suelta. La forma
real de hacer un Ctrl+Z es la que se ha usado: `ModKeybind` normal para la letra (reasignable por
el usuario como cualquier otro) y el modificador leído de `Main.keyState`.

### Verificado de verdad, en el juego real

Todo con `scripts\verificar-ws7.ps1` y `scripts\verificar-ws7-interactivo.ps1`, sandbox propio
`tModLoader-TerrakeepWS7` (copiado del de WS0, personaje sintético `TerrakeepPrueba`), sobre el
`.tmod` que ya lleva dentro también el código de WS4 — o sea, integrado, no aislado. Logs
completos en `evidencia\ws7-autoprueba-client.log.txt` y `evidencia\ws7-atajos-client.log.txt`.

| Qué | Evidencia real del log |
|---|---|
| Deshacer revierte un cambio concreto | `HISTORIAL/3 tras DESHACER: inventory[5]="Espada corta de cobre" ..., inventory[9]=vacio \| OK: el objeto ha vuelto a su ranura original` |
| Rehacer lo vuelve a aplicar | `HISTORIAL/4 tras REHACER: inventory[5]=vacio, inventory[9]="Espada corta de cobre" ... \| OK` |
| Ctrl+Z por el camino real del juego | `HISTORIAL via Ctrl+Z: Terrakeep ha deshecho: Mover objeto de la ranura 6 a la 10` + `ATAJOS/1 ... OK` |
| Ctrl+Y idem | `HISTORIAL via Ctrl+Y: Terrakeep ha rehecho: ...` + `ATAJOS/2 ... OK` |
| Los cuatro atajos con su tecla | `AbrirPanel(WS0)=[K], AbrirAjustes(WS7)=[J], Deshacer=[Z], Rehacer=[Y]` |
| Panel de Ajustes abierto con la UI nativa | `InGameUI.CurrentState=TerrakeepMod.UI.Ajustes.PanelAjustesState` |
| Idioma en vivo, sin reiniciar | `Ajustes.Titulo="Terrakeep - Ajustes"` → `"Terrakeep - Settings"` → `"Terrakeep - Ajustes"` |
| El mensaje del historial también se traduce | `Ajustes.Deshacer="Deshacer (Ctrl+Z)"` → `"Undo (Ctrl+Z)"` |
| Se recuerda entre partidas | `ModConfigs\TerrakeepMod_AjustesConfig.json` = `{"Idioma": 1}`, y la partida siguiente arrancó con `Idioma configurado=Espanol` |

### Lo que NO se pudo verificar, y por qué (dos intentos, se paró ahí)

**1. Pulsaciones físicas de teclado.** `keybd_event` desde PowerShell **no llega al juego**. Con
la ventana en primer plano según Windows (`SetForegroundWindow` devolviendo true) y
`Main.hasFocus=True` en el log durante los 12 s de la prueba, ni el Ctrl ni la letra aparecen
jamás en `Main.keyState` — comprobado acumulando el estado fotograma a fotograma, no muestreando
(muestrear a 1 Hz podría perderse una pulsación de 150 ms; acumular no). Limitación del arnés
(FNA/SDL no ve esa entrada sintética), no del mod.

Lo que sí se probó, y cubre todo menos el último eslabón físico: rellenar las **dos entradas
reales** de las que depende el atajo — `Main.keyState` para el Ctrl y
`PlayerInput.Triggers.JustPressed` para la tecla, que es literalmente lo que lee
`ModKeybind.JustPressed` — y dejar correr el código de producción tal cual
(`HistorialSystem.ComprobarAtajos`, el mismo método que llama `UpdateUI` cada fotograma).

**2. Captura de pantalla del panel.** Dos intentos, los dos fallidos:
- `CopyFromScreen` (pantalla completa): el juego no estaba en primer plano y lo que se fotografió
  fue **lo que el usuario tenía abierto en ese momento**. Evidencia inservible y además contenido
  privado; se borró en el acto y nunca llegó a git. **No volver a hacer capturas de pantalla
  completa en este proyecto.**
- `PrintWindow` sobre la ventana del juego: devuelve `True` pero la imagen sale **negra**. Es lo
  esperable en una aplicación acelerada por GPU (FNA dibuja por Direct3D, no por GDI). También se
  borraron.

Se paró ahí, siguiendo la regla de no insistir dos veces por la misma causa. La evidencia del
panel es el log, que es además el criterio que ya había fijado WS0.

### Para los demás workstreams

- Envolved vuestras ediciones con `Historial.CambiarObjetos(etiqueta, array, indices, () => ...)`
  (o `Historial.CambiarValor<T>` para lo que no sean objetos) y ya son deshacibles. Los botones
  cuelgan de `Historial.Pila.PuedeDeshacer` / `.EtiquetaDeshacer` / `.Deshacer()`.
- El historial se vacía solo al entrar y salir de mundo: las fotos guardan referencias a los
  arrays reales de la partida.
- Vuestros textos: `Localization/README.md`.
- Vuestros atajos ya funcionan sin hacer nada, gracias al sembrador.

---

## 6-sep-2026 — WS1: panel de Personaje en vivo

Segundo workstream del plan. El panel de prueba de WS0 (una sola ranura) pasa a ser el panel
**real de Personaje**: misma tecla **K**, mismo `IngameFancyUI.OpenUIState`, misma costumbre de
construir un `UIState` nuevo en cada apertura (porque `Main.LocalPlayer.inventory` se reasigna al
cargar otro personaje). Todo lo que hay dentro escribe **directamente sobre `Main.LocalPlayer`**:
ni un archivo, ni una copia intermedia.

### Qué quedó hecho

Seis pestañas, más una cabecera común:

| Pestaña | Qué toca de `Player` |
|---|---|
| Inventario | `inventory[0..49]` + monedas `[50..53]` + munición `[54..57]` |
| Almacenes | `bank`/`bank2`/`bank3`/`bank4` (`.item`, 40 ranuras cada uno) |
| Equipo | `armor` (20), `dye` (10), `miscEquips` (5), `miscDyes` (5) y los 3 `Loadouts` |
| Buffs | `buffType`/`buffTime` vía `AddBuff`/`ClearBuff`/`DelBuff`, con refresco solo |
| Apariencia | `hair`, `hairDye`, `skinVariant` y los 7 colores |
| Desbloqueos | los 13 marcadores permanentes |
| Cabecera | `name`, `statLife/statLifeMax`, `statMana/statManaMax` y el dinero contado |

Cada ranura es un `ItemSlot` **nativo de vanilla** con su contexto correcto (`InventoryItem`,
`InventoryCoin`, `InventoryAmmo`, `BankItem`, `VoidItem`, `EquipArmor`, `EquipAccessory`,
`EquipArmorVanity`, `EquipAccessoryVanity`, `EquipDye`, `EquipPet`, `EquipLight`,
`EquipMinecart`, `EquipMount`, `EquipGrapple`, `EquipMiscDye`), reutilizando el
`SlotObjetoVanilla` que dejó WS0.

### Dos fallos REALES encontrados en el juego, no en teoría

**1. Con un panel de `IngameFancyUI` abierto, el juego no dibuja el objeto que llevas cogido con
el ratón.** La interfaz es una lista de capas que se recorre **hasta que una devuelve false**
(`Main.DrawInterface`), y la capa `"Vanilla: Fancy UI"` (índice 12) devuelve false siempre que
hay un panel abierto. La capa que dibuja el objeto cogido,
`DrawInterface_38_MouseCarriedObject`, va después (índice 38) y **nunca se ejecuta**. En un editor
de inventario eso es fatal: coges un objeto y parece que lo has perdido. Resuelto dibujándolo
nosotros al final de `PanelPersonajeState.Draw`, con el mismo código de esa capa (incluido el
cambio temporal de `Main.inventoryScale` a `Main.cursorScale`). Evidencia: el contador
`FotogramasObjetoEnRaton` del log.

**2. Peor todavía: el juego te VACIABA el objeto del ratón cada tick.**
`IngameFancyUI.OpenUIState` pone `Main.playerInventory = false`, y `Player.dropItemCheck` (que
corre en cada `Player.Update`) tiene esto:

```csharp
if (Main.mouseItem.type > 0 && !Main.playerInventory) {
    ... GetItem de vuelta al inventario, y al suelo lo que no quepa ...
    Main.mouseItem = new Item();
}
```

O sea: **arrastrar un objeto de una ranura a otra era literalmente imposible**, y con el
inventario lleno el objeto se habría caído al suelo. No se dedujo leyendo código: se vio en el
log de la autoprueba, con un objeto puesto en el ratón que aparecía vacío 10 fotogramas después.
Resuelto manteniendo `Main.playerInventory = true` mientras el panel está abierto
(`PanelPersonajeState.MantenerInventarioAbierto`, reafirmado cada fotograma). No dibuja el
inventario de vanilla porque su capa es la 27 y la lista se corta en la 12; los únicos efectos
reales son que se llama a `Player.AdjTiles()` cada tick y que se desactiva el cambio de objeto
con la rueda, las dos deseables con un editor abierto. Además `IngameFancyUI.Close()` ya lo deja
a true al cerrar por su cuenta, así que no hay nada que restaurar.

Como red de seguridad, al cerrar el panel con un objeto todavía cogido se devuelve al inventario
con `Player.GetItem` (comprobado: +30000 de cobre al cerrar con 3 monedas de oro en el ratón).

### Decisiones técnicas que no estaban en el plan

- **API real comprobada contra el `tModLoader.dll` instalado (v2026.7.3.0) con `ilspycmd`**, no
  contra la referencia decompilada vieja (v1.4.4.9). Lo confirmado: `inventory = new Item[59]`
  (50 mochila + 4 monedas + 4 munición + 1 comodín, así que lo de "54 ranuras" del enunciado no
  era exacto), `armor[20]`, `dye[10]`, `miscEquips[5]`, `miscDyes[5]`,
  `maxBuffs => 44 + BuffLoader.extraPlayerBuffCount` (**no es fijo**, se lee del array real),
  `Loadouts[3]`, `HairID.Count = 165` pero el tope bueno es `HairLoader.Count` (incluye mods),
  `PlayerVariantID.Count = 12`.
- **Los conjuntos de equipo van al revés de lo que parece**: el conjunto ACTIVO vive en
  `Player.armor`/`dye`/`hideVisibleAccessory`, y su entrada en `Loadouts[]` está VACÍA (guarda el
  anterior). `EquipmentLoadout.Swap` intercambia **elemento a elemento**, no reasigna los arrays,
  así que las ranuras del panel siguen valiendo después de cambiar de conjunto. Se cambia con
  `Player.TrySwitchingLoadout(i)` (API oficial: mantiene sonido, partículas, red y el aviso a los
  mods) y se edita siempre `Player.armor`.
- **Los 13 desbloqueos se sacaron del orden real de deserialización del `.plr`** en
  `Player.LoadPlayer_*`, que es la definición autoritativa. Uno de ellos, `UsingBiomeTorches`, no
  es un campo sino una propiedad que guarda en `builderAccStatus[11]` y que devuelve false si
  `unlockedBiomeTorches` es false; `enabledSuperCart` depende igual de `unlockedSuperCart`.
- **`Terraria.ModLoader.UI.UIFocusInputTextField` es `internal`**: un mod no puede usarlo. Se
  reimplementó el campo de texto (`CampoTextoTk`) sobre la maquinaria pública que ese mismo campo
  usa por debajo (`Main.GetInputText`, `Main.clrInput`, `Main.instance.HandleIME`,
  `PlayerInput.WritingText`). Detalle importante: `PlayerInput.WritingText` hay que ponerlo a
  true en **cada dibujado** porque el motor lo devuelve a false al final de cada `UpdateInput`;
  mientras está a true, `KeyboardInput()` vacía la lista de teclas y por eso escribir una "k" en
  un campo no cierra el panel.
- **No hay tabla pública de tintes de pelo** (`HairShaderDataSet._shaderDataCount` es
  `protected internal`). La lista se construye recorriendo `ContentSamples.ItemsByType` y
  quedándose con los `Item.hairDye > 0` — que es literalmente de donde el juego copia el valor
  (`Player.cs`: `hairDye = item.hairDye;`). Da nombres de verdad y cubre los tintes de cualquier
  mod cargado. En vanilla salen 13.
- **`UIColoredSliderSimple` es público pero solo dibuja**, no lee el ratón. `DeslizadorTk` hereda
  de él y le añade el arrastre. La posición del ratón se toma de `Main.InGameUI.MousePosition` y
  no de `Main.MouseScreen`: la interfaz del mod se dibuja con `InterfaceScaleType.UI`, así que
  con `MouseScreen` se descuadraría en cuanto alguien tuviera la escala de interfaz distinta de
  100%.
- **El personaje de prueba se puebla desde dentro del juego**, no generando un `.plr` con
  `TerrasavrNative.Core`: lo que WS1 tiene que demostrar es precisamente que se puede escribir en
  vivo. Los objetos se buscan por sus propiedades (`headSlot`, `accessory`, `dye`, `mountType`,
  `Main.vanityPet`...) en `ContentSamples.ItemsByType`, no por ids fijos, así que la prueba no
  depende de que haya ningún mod de contenido cargado.
- **`Common/PanelPruebaSystem.cs` y `PanelPruebaPlayer.cs` conservan su nombre a propósito.** Son
  el punto de entrada compartido del mod y hay otros tres workstreams trabajando en paralelo
  sobre este mismo repositorio; renombrarlos rompería compilaciones ajenas sin aportarle nada al
  usuario. Solo se cambió lo justo para que abran `PanelPersonajeState`. `UI/PanelPruebaState.cs`
  (el panel de una ranura de WS0) sí se borró: ya no lo usa nadie.

### Verificado de verdad en el juego

`scripts\verificar-personaje.ps1` (sandbox propio `tModLoader-TerrakeepWS1`, variable
`TERRAKEEP_AUTOTEST_WS1`) recorre 23 pasos sobre el jugador real y deja el antes y el después de
cada uno en `client.log`. Evidencia completa en `evidencia\ws1-personaje-client.log.txt`.
**Cero excepciones en todo el log.** Lo más significativo:

```
Paso 3 - Inventario=2072355 cobre (2 plat 7 oro 23 plata 55 cobre), esperado 2072355 -> OK.
         Almacenes=1000000 cobre (1 plat), esperado 1000000 -> OK.
Paso 5 - ItemSlot.LeftClick de vanilla sobre inventory[10]. El objeto SIGUE en el raton 10
         fotogramas despues y se dibujo en 55 fotogramas. ANTES ranura=(vacio), raton="Bloque
         de tierra" x42. DESPUES ranura="Bloque de tierra" x42, raton=(vacio).
Paso 6 - Tras pedir el conjunto 1: CurrentLoadoutIndex=1, armor[0]=(vacio). Al volver al 0:
         armor[0]="Gafas de proteccion" -> OK, cada conjunto conserva lo suyo.
Paso 8 - buffTime al aplicarlo=3600, ahora=3534. Ticks de partida transcurridos=66. OK: baja
         solo, por eso la pestaña de buffs TIENE que refrescarse sola.
Paso 11 - 13 desbloqueos: todos False antes, todos True despues, 13 de 13 releidos como activos.
Pestaña "Inventario" dibujada: 64 elementos, 58 ranuras de objeto, la 1ª en x=110 y=189 46x46.
Pestaña "Equipo" dibujada: 68 elementos, 40 ranuras de objeto.
Paso 19 - deslizador de color accionado por su ruta real (LeftMouseDown). Deslizador en
          x=290 y=290 110x20, clic al 25% (x=317). hairColor (200,40,90) -> (64,40,90),
          FillPercent=0,250. OK: el canal rojo ha ido al 25%.
Paso 20 - campo de texto de la cabecera enfocado con su ruta real (LeftClick). Enfocado=True.
Paso 21 - el campo de texto ha capturado el teclado en 54 fotogramas (PlayerInput.WritingText).
Paso 22 - cierre con 3 monedas de oro cogidas: monedas antes=2072355, despues=2102355 (+30000).
```

Los dos controles propios que no son de vanilla se accionan por su ruta de entrada REAL, no
llamando a su manejador: al deslizador se le manda un `LeftMouseDown` con
`Main.InGameUI.MousePosition` colocado al 25% de su ancho (que es exactamente lo que hace
`UserInterface` con un clic), y al campo de texto un `LeftClick`. Lo unico que no se puede
simular sin teclado real es escribir; en su lugar se cuenta cuantos fotogramas ha tenido el
campo capturado el teclado (`PlayerInput.WritingText`), que es la parte que protege al panel
de que escribir una "k" lo cierre.

### Obstáculos del entorno (anotados, no bloquean)

- **Terraria congela la partida en un jugador cuando su ventana pierde el foco**
  (`Main.hasFocus = IsActive` → `Main.gamePaused`). Con la partida congelada los buffs no
  caducan y el paso 8 no podía dar verde, porque comprueba justo eso. Se vio en el log con
  `Ticks de partida transcurridos=0, Main.hasFocus=False, Main.gamePaused=True`. Resuelto en dos
  frentes: el paso espera a ver 60 ticks REALES de partida antes de juzgar (no da por hecho que
  pasan por el mero hecho de dibujar), y el script le devuelve el foco a la ventana del juego con
  `SetForegroundWindow` tras lanzarla.
- **Colisión entre agentes en paralelo**: una tanda se perdió porque otro workstream lanzó su
  propia verificación a la vez y los dos scripts borran y releen el mismo
  `tModLoader-Logs\client.log`, y además cada uno mata al final todos los `dotnet` de la carpeta
  de tModLoader. Se resolvió reintentando cuando el otro terminó. Si se vuelve a dar mucho,
  merecería la pena que cada script use su propio archivo de log.

### Nota: el commit de WS1 arrastró archivos de WS4

El commit `69a2281` (WS1) incluye también archivos de WS4/Builds
(`Common/Builds/RegistroBuilds.cs`, `evidencia/ws4-builds*.log.txt` y cambios en
`Common/Builds/*.cs`, `UI/Builds/PanelBuildsState.cs`, `Localization/*.hjson` y
`scripts/verificar-builds-en-juego.ps1`). **No se ha perdido nada**, solo está mal repartido
entre commits.

Qué pasó, exactamente: WS1 hizo `git add` nombrando SOLO sus archivos, como manda la norma con
varios agentes a la vez, pero **el índice de git es único para todo el repositorio**. Entre ese
`git add` y el `git commit` de WS1, el agente de WS4 preparó los suyos, y el commit se llevó todo
lo que había preparado en ese momento, no solo lo de WS1 (comprobado: justo antes del `git add`,
`git status` daba esos archivos como no preparados).

No se ha reescrito el historial para separarlo: `git reset` sobre un repositorio con otros tres
agentes trabajando a la vez puede pisarles un commit a medias, y el riesgo de eso es mucho peor
que un commit con dos temas dentro. El árbol resultante compila limpio
(`Compilation finished with 0 errors and 0 warnings`).

**Para la próxima**: nombrar los archivos en `git add` no basta en un repositorio compartido. Lo
que sí aísla de verdad es `git commit --only <archivos>` (comitea exactamente esas rutas, ignore
lo que haya preparado en el índice) o darle a cada agente su propio `GIT_INDEX_FILE`.

---

## 6-sep-2026 — WS4: Builds y auto-equipar

Cuarto workstream del plan. Panel propio de **Builds** (tecla **L**), con el catálogo estático
de equipo por etapa y clase que ya usa la app de escritorio, marca de "ya lo tienes" contra el
inventario real, y auto-equipar.

### Lo que se hizo

- `Assets/builds.json` y `Assets/builds_calamity.json` copiados del repo hermano y **parseados
  con el mismo código de `TerrasavrNative.Core`** (`BuildsCatalog.LoadFromStream`), tal como
  preveía el plan. Nada reimplementado.
- `Common/Builds/`: modelo, catálogo, búsqueda en los contenedores del jugador, auto-equipar,
  el `ModSystem` con el atajo y el registro de evidencia.
- `UI/Builds/`: el panel (`PanelBuildsState`) y el slot de catálogo (`SlotCatalogoBuild`).
- `scripts/verificar-builds-en-juego.ps1`: ciclo de prueba propio, con sandbox propio.

### Hallazgos reales (código del tModLoader instalado, v2026.7.3.0)

1. **Los `.json` entran solos en el `.tmod`**, no hay que declararlos en ningún sitio.
   `ModCompile.PackageMod` empaqueta **todos** los archivos de la carpeta salvo los que descarta
   `IgnoreResource`: `buildIgnore`, los que empiezan por ".", `bin\`, `obj\`, el código fuente
   (sin `includeSource`) y `Thumbs.db`. Comprobado con `node tmod-extract.js`: dentro del `.tmod`
   aparecen `Assets/builds.json` y `Assets/builds_calamity.json`, con la ruta normalizada a "/"
   por `TmodFile.Sanitize`.
2. **`ItemID.Search` resuelve los DOS formatos de pid de una sola llamada**, que es justo lo que
   necesita este catálogo. Es el `IdDictionary` de ReLogic: para vanilla, sus claves son los
   nombres de campo de `Terraria.ID.ItemID` (los 101 pid vanilla de los dos archivos existen ahí,
   comprobado uno a uno contra el decompilado); para los mods, `ModItem.Register` hace
   `ItemID.Search.Add(FullName, Type)` con `FullName = "Mod/NombreInterno"`. Más directo que
   `ModContent.Find<ModItem>(mod, interno).Type`, no hay que distinguir los dos casos, y **no
   lanza** si el mod no está instalado. Los ids sintéticos de Calamity de la app de escritorio
   (`CalamityIds.ItemIdBase`) no hacen ninguna falta aquí, como decía el plan.
3. **`GetFileStream` falla una vez cerrado el `.tmod`** (`TmodFile.GetStream` lanza
   `IOException("File not open")`). Por eso los bytes se leen en `ModSystem.Load()` (archivo
   abierto seguro) y se parsean en `PostSetupContent()`, que es cuando ya han registrado su
   contenido todos los mods y los pid de Calamity sí se pueden resolver.
4. **Slots de accesorio reales**: `Player.armor` es 0-2 armadura, 3-9 accesorios, 10-19 vanity
   (su propio XMLdoc lo dice), y los usables son 5 + `Player.GetAmountOfExtraAccessorySlotsToShow()`
   (Corazón de Demonio + modo Maestro). Se calcula en vivo, no se supone un máximo fijo. Para
   validar si un accesorio cabe en un hueco se usa `ItemSlot.AccCheck` (público; devuelve **true
   cuando NO se puede**), que es lo que impide duplicados y dos pares de alas.
5. **`Player.armor` ES el conjunto activo**; los otros viven en `Player.Loadouts[i]` y solo se
   intercambian con `Loadouts[i].Swap(player)`. Escribir en `armor` afecta únicamente al conjunto
   que el jugador lleva puesto.
6. **`-build` acepta `-tmlsavedirectory`**: el `.tmod` sale directamente en el sandbox de la
   prueba en vez de en la carpeta `Mods` compartida. Con cuatro agentes compilando el mismo mod,
   esto elimina que se pisen el `.tmod` unos a otros.

### Decisiones tomadas

- **Auto-equipar solo MUEVE, nunca crea objetos**, igual que la app de escritorio. Lo que el
  jugador no tenga se cuenta como "no lo tienes" y ya. Dar objetos de la nada convertiría una
  herramienta de organización en un generador de trampas y arruinaría la progresión que el propio
  catálogo describe. Por lo mismo **tampoco cambia prefijos**: el prefijo recomendado del catálogo
  se enseña como texto y el objeto se mueve con el que ya tuviera.
- **La única operación es un intercambio** entre dos posiciones de arrays vivos. Si el hueco de
  destino estaba ocupado, lo que había se va al sitio de donde salió el objeto. Es predecible y
  reversible, y no puede perder nada.
- **La fuente "Calamity" no se ofrece si Calamity no está cargado.** No basta con "resolvió algo":
  `builds_calamity.json` apoya su progresión en bastante equipo vanilla, así que sin Calamity
  resuelve igualmente 35 de 154 y se ofrecería una pestaña con casi todo en rojo. Se exige que al
  menos uno de los pid **de mod** haya resuelto.
- **El atajo se registra desde el propio `ModSystem` de WS4** (`KeybindLoader.RegisterKeybind`
  solo necesita el `Mod`, que `ModType.Mod` ya trae asignado antes de `Load()`), no desde la clase
  `Terrakeep`. Así este workstream no toca ni un archivo compartido.

### Verificado de verdad en el juego

Tres ejecuciones reales de `scripts\verificar-builds-en-juego.ps1`, evidencia completa en
`evidencia\ws4-builds.log.txt` y `evidencia\ws4-builds-calamity.log.txt`:

| Qué | Resultado real |
|---|---|
| Catálogo vanilla | `[Vanilla: 3 etapas, 156/156 objetos resueltos]` |
| Catálogo Calamity (con Calamity cargado) | `[Calamity: 3 etapas, 154/154 objetos resueltos]` |
| Catálogo Calamity (sin Calamity) | descartado solo: `35/154 objetos, y 0/119 de los que vienen de un mod` |
| "Ya lo tienes" | `6 de 13` (vanilla) y `5 de 8` (Calamity/Pícaro), con la ubicación exacta de cada uno |
| Auto-equipar | `movidos=5, ya colocados=1, no los tienes=7, sin sitio=0, no existen aqui=0` |
| Idempotencia (segunda pasada) | `movidos=0, ya colocados=6` |
| Filtro de clase con rogue (solo Calamity) | `Fuente="Calamity" ... Clase="Pícaro"`, 8 objetos |
| Panel realmente dibujado | `Marco: x=150 y=60 w=980 h=600` sobre 1280x720, 13 `ItemSlot` con rectángulo propio en pantalla |
| Con el mod ENTERO (WS0+WS1+WS4+WS7) | compila con 0 errores y la autoprueba de WS4 pasa igual |

Los ids resueltos son reales y comprobables: `MoltenHelmet`→231, `NightsEdge`→273,
`CalamityMod/SulphurousHelmet`→5959, `CalamityMod/ScuttlersJewel`→5753.

### Obstáculo real, y cómo se resolvió

**El `client.log` del juego es uno solo para todas las instancias** y tModLoader lo rota al
arrancar (`client.log` → `client1.log`). Con cuatro workstreams construyéndose en paralelo, dos
verificaciones que se solapan se pisan la evidencia: pasó **dos veces seguidas** intentando
cerrar la prueba con Calamity (el `client.log` acabó siendo el de WS1 la primera vez y el de WS7
la segunda, identificados por el `-tmlsavedirectory` de su cabecera). En vez de insistir una
tercera vez, se quitó la causa: `Common/Builds/RegistroBuilds.cs` escribe cada línea **también**
a `terrakeep-ws4-evidencia.log` dentro de la carpeta de guardado de la propia prueba
(`Main.SavePath`, o sea el `-tmlsavedirectory`), que es privada de cada ejecución. Solo lo hace
cuando hay una autoprueba en marcha; jugando normal no deja ningún archivo suelto.

Segundo problema de convivencia: **los otros workstreams borran y renombran sus archivos en
caliente** (`UI\PanelPruebaState.cs` desapareció a mitad de una prueba, sustituido por el panel
de Personaje de WS1), lo que rompía una verificación de WS4 por causas ajenas a WS4. El script
compila por defecto una **copia aislada** con el núcleo del mod (`Terrakeep.cs`) y los archivos
de WS4 y nada más; con `-Completo` compila el proyecto entero, que es como se hizo la última
comprobación.

### Regalo de WS7 que afecta a este panel

El `SembradorDeAtajos` de WS7 recorre **todas** las claves de atajo que empiezan por
`TerrakeepMod/`, así que la tecla **L** de Builds queda asignada de fábrica por ese mismo arreglo
sin tocar nada aquí. Sin él, ningún atajo del mod tenía tecla realmente asignada.

### Añadido después: el filtro por clase, con un clic de verdad

La primera tanda de pruebas verificaba el cambio de clase llamando al método del panel, no
pulsando la píldora. Se cerró ese hueco: `PanelBuildsState.PulsarPildoraClase` dispara el
`OnLeftClick` real de la píldora con `UIElement.LeftClick(new UIMouseEvent(...))`, que es
exactamente el camino que recorre un clic de ratón una vez resuelto sobre qué elemento cae.
Evidencia real:

```
filtro por clase con un clic real en la pildora 4 de 4 ("Invocador"):
clase "Cuerpo a cuerpo" -> "Invocador". Objetos de la clase ahora: 13.
```

De paso, otro efecto de trabajar cuatro agentes a la vez sobre el mismo juego: **una ejecución
del cliente no llegó a arrancar** (ni escribió el archivo de evidencia ni apareció en el
`client.log`, que en ese momento era el de WS1 haciendo su propia prueba). Al reintentarla sin
nadie más lanzando el juego, salió a la primera. No parece un problema del mod: lanzar dos
clientes de tModLoader a la vez es lo que no le sienta bien.
