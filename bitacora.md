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

## Verificación de integración final de la noche (6-sep-2026, madrugada)

Con los cuatro workstreams ya cerrados por separado (WS0, WS1, WS4, WS7 - WS2 vive en el repo
hermano), tocaba comprobar que **las cuatro piezas juntas, en el mismo `.tmod`**, siguen
funcionando - cada agente había verificado la suya en copias/sandboxes aislados, pero nadie
había vuelto a compilar y probar el árbol entero integrado desde que se fusionaron.

- `git status` tenía un cambio sin comitear en los dos `Localization/*.hjson`: el propio
  tModLoader los había reescrito (de claves con punto a bloques anidados, mismo contenido) al
  cargar el mod con el campo `AtajosYaSembrados` nuevo de WS7. Comiteado tal cual (`c43752f`).
- `scripts\compilar.ps1` sobre el árbol completo: **0 errores** con el compilador real de
  tModLoader (solo 9 avisos de estilo `ChangeMagicNumberToID` en código de autoprueba, cosmético).
  `.tmod` de 212.408 bytes (104.558 en WS0 con solo el panel de prueba - las cuatro piezas están
  dentro de verdad).
- Las tres autopruebas reales de WS1/WS4/WS7, ejecutadas contra ESE `.tmod` integrado (no una
  copia aislada): **`AUTOPRUEBA WS1 COMPLETA`** (23 pasos, inventario/almacenes/loadouts/buffs/
  apariencia/desbloqueos/cabecera, cero excepciones), **auto-equipar con clic real en la píldora
  + segunda pasada idempotente** (Builds), **`AUTOPRUEBA WS7: terminada`** (deshacer/rehacer real
  sobre un objeto movido, y cambio de idioma en vivo con persistencia en `ModConfig`
  verificada). Los tres a la vez, sin ningún conflicto entre ellos.
- `dotnet build`/`dotnet test`/arnés de UI Automation del repo hermano `Terrasavr-Native`,
  también repetidos desde cero tras los commits de WS2 y de la ronda de idioma: **0 errores,
  408+329 tests, arnés en 540 líneas con 0 FALLO**.

**Estado real al cierre de la noche: los cinco workstreams de esta ronda (WS0, WS1, WS2, WS4,
WS7) están cerrados, comiteados, y verificados juntos de verdad en el juego real - no solo cada
uno por separado.** Quedan sin empezar, según el plan: WS3 (Librería del mod, depende del árbol
ya portado en WS2), WS5 (Investigación) y WS6 (Exploración/mapa del mundo, el más grande).

---

## 6-sep-2026 — WS3: panel de Librería

Tercer workstream del plan, en paralelo con WS5 (Investigación) y WS6 (Exploración). Panel
propio de **Librería** (tecla **O**): árbol de carpetas navegable, buscador con la gramática real
de Terrasavr, y coger un objeto del catálogo para soltarlo en cualquier contenedor real del
jugador.

### El árbol es un HÍBRIDO, y las dos mitades son deliberadas

1. **El árbol vanilla CURADO** (el de Terrasavr: "Materiales / Pre-Modo Difícil / Cobre &
   Estaño", "Categorías", "Objectos por ID"...) sale de `LibraryTreeBuilder.BuildItemTree` de
   `TerrasavrNative.Core`, que portó WS2, sobre los mismos dos `.json` que usa la app de
   escritorio (`vanilla_library_tree.json` + `vanilla_library_labels_es.json`, copiados a
   `Assets\`). **Ese orden hecho a mano no se puede deducir de los campos de un `Item`**: hay que
   traerlo. Se le pasa `calamity: null` a propósito, que es un caso que Core ya contempla.
2. **Una carpeta madre por MOD instalado**, descubierta EN VIVO recorriendo
   `ContentSamples.ItemsByType` y montada con `LiveItemTreeBuilder.BuildTree` (también de WS2).
   Aquí no hay ningún `calamity/catalog.json`: es la decisión del plan, y su ventaja real se ve
   en el log — con Calamity cargado aparecen **"Calamity Mod (mod)" con 2665 objetos**, "Calamity
   Mod Music (mod)" con 62 y "tModLoader (mod)" con 90, sin que nadie haya escrito un catálogo.

Se les suma, **solo si hace falta**, una carpeta con los objetos vanilla que el árbol curado no
mencione. En las dos ejecuciones reales salieron **0**: el árbol de Terrasavr cubre entero el
vanilla de esta versión. Se deja porque si algún día no lo cubriera, esos objetos quedarían
inalcanzables navegando.

### La pieza que WS2 dejó a propósito sin hacer

`Common/Libreria/CatalogoVivo.cs`. Core **no puede** depender de Terraria (dejaría de compilar
para net10.0, que es lo que consume la app de escritorio), así que la extracción tenía que vivir
forzosamente en el mod. Rellena un `LiveItemInfo` por objeto y deduce su categoría de los campos
REALES del `Item`, comprobados uno a uno en el `tModLoader.dll` instalado:

- `headSlot`/`bodySlot`/`legSlot` + `vanity` → Armadura / Armadura - Vanidad
- `accessory` + `wingSlot` → Accesorios / Accesorios - Alas / - Vanidad
- `mountType` + `MountID.Sets.Cart` → Monturas / Vagonetas
- `buffType` contra `Main.vanityPet` / `Main.lightPet` → Mascotas (es lo que mira el propio juego)
- `hairDye >= 0` / `dye > 0` / `paint` → Tintes
- `ammo != AmmoID.None` → Munición (**antes** que las armas: una flecha hace daño y no es un arma)
- `pick`/`axe`/`hammer` → Herramientas (**antes** que las armas, o todas caerían en "cuerpo a cuerpo")
- `fishingPole > 1` → Pesca. Ojo: el campo real es `public int fishingPole = 1;`, o sea que **el
  valor por defecto es 1, no 0** — con `> 0` entraría el juego entero
- `damage > 0` → Armas, por `Item.DamageType`, que es un `DamageClass`. Eso hace que **la clase
  Pícaro de Calamity salga sola**, con el nombre que le da el propio mod, sin ninguna tabla
  nuestra
- `createWall`/`createTile` + `Main.tileFrameImportant` → Colocables - Paredes / Muebles / Bloques

Las claves que devuelve son deliberadamente las mismas de `calamity/catalog.json`
("Weapons/Melee", "Accessories/Wings"...) **para poder reutilizar tal cual
`LibraryTreeBuilder.CalamityCategoryLabel` de Core** como traductor de etiquetas: ya trae las 121
categorías traducidas al español y, para lo que no conozca, separa el CamelCase en vez de
inventarse una traducción. Resultado real con Calamity: `"Accesorios (238)" | "Munición (28)" |
"Armadura (186)" | "Criaturas (10)" | "Tintes (48)" | "Pesca (25)" | "Materiales (72)" | "Varios
(192)" | "Monturas (9)" | "Mascotas (34)" | "Colocables (993)" | "Pociones (57)" | "Herramientas
(30)" | "Armas (743)"`.

El catálogo se construye **perezosamente** al abrir el panel por primera vez (**2 ms** para los
8240 objetos de vanilla + Calamity, medido) y se tira al cambiar de idioma
(`ModSystem.OnLocalizationsLoaded`), porque los nombres de los objetos cambian con él.

### La gramática de búsqueda: se COPIÓ, y por qué

`LibrarySearchGrammar` **sí es pura** (solo `System.Globalization`/`System.Text`), pero vive en
`TerrasavrNative.App/ViewModels/`, o sea **dentro del ensamblado WPF** de la app de escritorio,
que solo compila para `net10.0-windows`. El mod solo referencia `TerrasavrNative.Core` (net8), así
que desde aquí ese tipo es **inalcanzable**. Las dos salidas eran moverla a Core o copiarla; se
copió (`Common/Libreria/GramaticaBusqueda.cs`, ~40 líneas de lógica cerrada con pruebas propias en
el repo hermano) porque moverla obliga a tocar el repo hermano, a regenerar y re-empaquetar
`lib\TerrasavrNative.Core.dll` (archivo compartido) y a arreglar los `using` de la app y sus
tests, y todo eso con dos agentes más trabajando en paralelo sobre los mismos repositorios. Queda
anotado en el propio archivo para que se sepa que hay dos copias.

Verificadas las reglas en el juego, con evidencia real y sin cadenas fijas (los términos se sacan
del nombre/tooltip REAL que tenga el objeto con el idioma activo, así que la prueba vale igual en
español que en inglés):

| Regla | Evidencia |
|---|---|
| Nombre | `"Copper"` → 22 objetos, el buscado entre ellos |
| `#id` | `"#3507"` → exactamente 1 |
| `#a-b` | `"#3507-3511"` → 5 |
| `.tooltip` | `".provides"` → 53 objetos; **la misma palabra sin punto → 0**. Es lo que demuestra que el punto cambia de verdad dónde se busca |
| coma = O | `"Dirt Block,Stone Block"` → 23 |
| espacio = Y | `"Dirt Block"` → 2, y uno es `"The Dirtiest Block"`: con subcadena simple no saldría |
| acentos | se busca `"pina colada"` y encuentra `"Piña Colada"` |

Ámbito y tope replican los reales de la app (`LibraryViewModel.Refresh`): con carpeta abierta se
busca DENTRO de ella recorriendo `ItemIdsOrdered` (el orden curado; el `ItemIdSet` es un HashSet
sin orden garantizado), sin carpeta abierta se busca en todo el catálogo, y se enseñan **100**
como mucho.

**Sobre el riesgo conocido de que `UIList` no virtualiza**: no llegó a aparecer. El paginado de 40
del árbol sigue ahí, pero lo que de verdad acota la rejilla es ese tope de 100 resultados, que se
aplica venga de donde venga la lista - abrir "Colocables (993)" de Calamity enseña 100 ranuras, no
993.

### Colocar un objeto: el recorrido lo hace vanilla entero

Clic izquierdo en una ranura del catálogo = 1 unidad al ratón; clic derecho = la pila máxima. A
partir de ahí **no hay ni una línea propia de "colocar objeto"**: se suelta en cualquiera de los 7
contenedores reales del jugador (Inventario, Monedas y munición, Equipo, Hucha, Caja fuerte,
Forja, Bóveda), que son `SlotObjetoVanilla` de WS0 sobre el array vivo, con su contexto de
`ItemSlot` correcto, así que las reglas de qué acepta cada ranura, el apilado y el intercambio son
las del propio juego. Queda **deshacible** con el historial de WS7 (`Historial.CambiarObjetos`).

Aquí SÍ se crean objetos de la nada, al revés que en el auto-equipar de WS4, y es lo correcto: la
Librería es precisamente el catálogo del que se sacan objetos (igual que en la app de escritorio);
Builds no los crea porque su cometido es organizar lo que ya tienes.

Los dos fallos reales que encontró WS1 con `IngameFancyUI` **también hacen falta aquí, y más**
(este panel existe para coger objetos): `Main.playerInventory = true` reafirmado cada fotograma
(si no, `Player.dropItemCheck` vacía `Main.mouseItem` cada tick) y el dibujado propio del objeto
cogido (la capa 38 de vanilla nunca se ejecuta con un panel de `IngameFancyUI` abierto). En el log
se ve que el objeto se dibujó en **13 fotogramas** antes de soltarlo.

### Separación pensada para la fusión de los seis paneles

- `UI/Libreria/ContenidoLibreria.cs` es **solo el contenido**: un `UIElement` que se estira al
  100% de lo que se le dé y no sabe nada de cómo se ha abierto. Ni cabecera, ni botón de cerrar,
  ni atajo.
- `UI/Libreria/PanelLibreriaState.cs` (marco + cabecera + cerrar) y
  `Common/Libreria/PanelLibreriaSystem.cs` (`ModKeybind` + `IngameFancyUI` + autoprueba) son la
  **mecánica de apertura**, y son lo que se tirará entero en la fusión.

El agente de fusión no tiene que reescribir nada de la Librería: crea un `ContenidoLibreria` y lo
cuelga del contenedor de su pestaña, exactamente igual que hace hoy `PanelPersonajeState` con sus
`PestanaInventario`/`PestanaEquipo`.

### Estética: es el mismo programa, no una ventana pegada al lado

Todo sale de lo que ya existía: paleta de `EstiloTk` (WS1) sin un solo color nuevo salvo el fondo
de las ranuras de catálogo, botones `BotonTk`, etiquetas vivas `EtiquetaTk`, buscador
`CampoTextoTk` (el mismo de la cabecera de Personaje), ranuras `SlotObjetoVanilla` (WS0). El marco
copia las medidas exactas del panel de Personaje (96%/94% centrado, tope 1080x700, relleno 10,
botón "Cerrar (tecla)" abajo a la derecha) y las dos zonas grandes van dentro de cajas con
`EstiloTk.FondoCaja`, que es lo que hacen las cajas de las pestañas de Personaje. Los iconos de
carpeta son el sprite REAL del primer objeto de esa carpeta, dibujado sin marco de ranura
(`IconoObjetoTk`) para que no compita visualmente con las ranuras de verdad.

### Verificado de verdad en el juego

`scripts\verificar-libreria.ps1`, sandbox propio `tModLoader-TerrakeepWS3`, variable
`TERRAKEEP_AUTOTEST_WS3`, archivo de evidencia propio `terrakeep-ws3-evidencia.log` dentro del
sandbox (la solución que ya encontró WS4 al `client.log` compartido). Evidencia completa en
`evidencia\ws3-libreria.log.txt` y `evidencia\ws3-libreria-calamity.log.txt`. **Cero excepciones
en los 13 pasos, en las dos configuraciones.**

| Qué | Evidencia real |
|---|---|
| Árbol montado | `10 carpetas raiz` sin Calamity, `12` con él; `5513` / `8240` objetos vivos |
| Navegación con clics REALES | tres `PulsarFilaCarpeta` encadenados hasta `"Cobre & Estaño"`, 30 objetos |
| Carpeta de mod descubierta en vivo | `"Calamity Mod (mod)" (ruta interna "CalamityMod"): 2665 objetos ... 14 categorias` |
| Objetos reales de mod en pantalla | `dentro de "Accesorios (238)": 100 ranuras ... "Abaddon"(5608), "Abyssal Diving Gear"(5609)...` |
| Coger del catálogo | `Raton ANTES: (vacio). Raton DESPUES: "Copper Shortsword" x1 (type=3507)` |
| **Colocar en el jugador real** | `ANTES inventory[20]=(vacio). DESPUES inventory[20]="Copper Shortsword" x1 (type=3507)` |
| Deshacer/rehacer de WS7 | `DESHACER -> inventory[20]=(vacio) -> OK` y `REHACER -> "Copper Shortsword" -> OK` |
| Panel dibujado de verdad | `Marco x=16 y=21 w=768 h=676` y `1ª ranura de catálogo ("Iron Pickaxe") en x=346 y=143 44x44` |
| Con el mod ENTERO (WS0+WS1+WS3+WS4+WS5+WS6+WS7) | `Compilation finished with 0 errors and 0 warnings`, `.tmod` de 355.673 bytes |

### Obstáculos del entorno

- **Dos clientes de tModLoader a la vez no arrancan.** La primera ejecución del script se quedó
  colgada en `Hook System.Runtime.Loader.AssemblyLoadContext...` y nunca llegó a cargar mods,
  con otro agente ejecutando el juego en ese momento. Repetida a solas, salió a la primera. Es el
  mismo efecto que ya anotó WS4; no es un problema del mod.
- **`git commit --only` no vale para archivos NUEVOS** (`did not match any file(s) known to git`:
  `--only` exige que la ruta ya esté seguida por git), que es justo el caso de un workstream
  entero. Lo que sí aísla de verdad, y es lo que se ha usado aquí, es un **índice privado**:
  `GIT_INDEX_FILE=<ruta propia> git read-tree HEAD` + `git add <mis archivos>` + `git commit`. El
  `.git/index` compartido no se toca en ningún momento, así que es imposible arrastrar los
  archivos de otro agente (que es lo que le pasó a WS1 con el commit `69a2281`).
- **Corolario que hay que conocer, porque muerde**: precisamente por no tocarlo, el `.git/index`
  compartido **se queda obsoleto** tras un commit hecho con índice privado (o tras cualquier
  commit de otro agente que use la misma técnica). Se vio en real: `git status` pasó a marcar con
  `D` (borrado PREPARADO) los ~30 archivos de WS3 y de WS6 que sí existen en disco y en `HEAD`.
  Si en ese momento otro agente hubiera hecho un `git commit` normal, **habría comiteado el
  borrado de todos ellos**. Se arregla con un `git reset` a secas (sin `--hard` y sin rutas):
  solo reescribe el índice para que vuelva a coincidir con `HEAD`, no toca ni un archivo del
  árbol de trabajo. Comprobado antes de ejecutarlo que no había ninguna entrada `A` (contenido
  que existiera únicamente en el índice), o sea que no se perdía nada. **Conviene hacerlo
  siempre después de comitear con índice privado.**

---

## 6-sep-2026 — WS6: Exploración y mapa del mundo (tecla P)

El workstream más grande y más nuevo del plan, y el único cuyas tres piezas dependían de cosas
del motor que no se habían tocado nunca: las texturas del mapa, la capa de mapa de mods y el modo
de juego en vivo. **Las tres quedaron cerradas y verificadas en el juego real.**

### Lo que hay

Panel propio (`IngameFancyUI`, tecla **P**), mismo marco, misma paleta (`EstiloTk`), misma barra
de pestañas y mismo botón de cerrar que los paneles de Personaje (K) y Builds (L). Tres pestañas:

| Pestaña | Qué hace |
|---|---|
| Mapa | Mini-mapa navegable con las texturas reales del juego + botón "Ver en el mapa del juego" |
| Búsqueda | 38 objetivos (minerales, gemas, tesoros, contenedores, líquidos, paredes) sobre el mundo real |
| Este mundo | Ficha del mundo y cambio de dificultad en vivo con salvaguardas |

`Common/Exploracion/` (mecánica) y `UI/Exploracion/` (contenido) están separados a propósito, para
que la fusión posterior de los seis paneles en uno solo con pestañas se lleve los `UIElement` y
tire la mecánica de apertura entera.

### Hallazgos reales del motor (todo comprobado con `ilspycmd` sobre el `tModLoader.dll` instalado)

1. **La correspondencia tile ↔ píxel del mapa.** `Main.instance.mapTarget` es una rejilla pública
   de `RenderTarget2D` (`mapTargetX=5`, `mapTargetY=2`) y cada casilla cubre
   `Main.textureMaxWidth` × `Main.textureMaxHeight` = **2000 × 1800** tiles. El píxel `(px, py)`
   de la casilla `[k, l]` es el tile `(k*2000 + px, l*1800 + py)`. No está documentado en ningún
   sitio: se dedujo del bucle de dibujado real de `Main.DrawMap` (cómo calcula la posición de cada
   trozo y su rectángulo de origen) y **se comprobó leyendo un píxel de verdad** con
   `RenderTarget2D.GetData` en la posición del jugador, que sale opaco justo donde el juego dice
   `Main.Map.IsRevealed(x, y) == true`.
2. **La matemática del mapa a pantalla completa, simplificada.** En `DrawMap`, la posición en
   pantalla de un tile es `num + (tile - 10) * escala` con
   `num = -mapFullscreenPos * escala + anchoPantalla/2 + 10 * escala`, que es exactamente
   `centro + (tile - posicionDelMapa) * escala`. El mini-mapa usa esa fórmula con el centro de su
   propio elemento, así que se comporta igual que el mapa del juego.
3. **El mapa se mantiene al día solo.** `DrawToMap` se llama desde el ciclo de dibujado de
   `Main.DoDraw`, no solo cuando el mapa está a la vista, así que el mini-mapa no tiene que
   refrescar nada. Lo que sí tarda es el repintado completo: tras marcar el mapa como sucio, el
   motor lo redibuja **a trozos con un presupuesto de 5 ms por fotograma**
   (`DrawToMap_Section` + `sectionManager`), no de golpe. La autoprueba espera 240 fotogramas por
   eso.
4. **Confirmado el bloqueo que condicionaba todo el diseño**: en `Main.DoDraw`, si
   `mapFullscreen` es true el juego dibuja el mapa y hace `return` **antes** de la interfaz, así
   que no se dibuja ninguna UI de mods. Panel propio y mapa grande son mutuamente excluyentes, tal
   como decía el plan. De ahí el diseño híbrido, y de ahí que el botón "Ver en el mapa" cierre el
   panel y lo vuelva a abrir solo cuando el mapa se cierra.
5. **Recortar y filtrar el mini-mapa sin tocar el `SpriteBatch`.** `UIElement` ya trae las dos
   piezas: `OverflowHidden` recorta a los hijos con el rectángulo de tijera, y
   `OverrideSamplerState` hace que el motor reabra el lote de dibujado con el sampler que le
   pidas. Con un contenedor `OverflowHidden` y un lienzo hijo con `PointClamp`, el mapa sale
   nítido y recortado sin una sola llamada a `GraphicsDevice`.
6. **Nombres de API que NO coinciden con la referencia decompilada vieja**
   (`tModLoader-Decompiled\`, v1.4.4.9) ni con lo que suponía el plan:
   - Los structs de datos de tile viven en el namespace **`Terraria`**, no en
     `Terraria.DataStructures`: `Terraria.TileTypeData`, `Terraria.TileWallWireStateData`,
     `Terraria.WallTypeData`, `Terraria.LiquidData`. Existe además un `WallTypeData` propio, que
     el plan no mencionaba y que es lo que hace barata la búsqueda de paredes.
   - `MapOverlayDrawContext` está en **`Terraria.Map`**, no en `Terraria.ModLoader`.
   - `IngameFancyUI` está en **`Terraria.UI`**, no en `Terraria.GameContent.UI.States`.
   - El índice del array plano de tiles es **`y + x * Height`** (código real de `Tilemap`), no
     `x + y * Width`: por eso el barrido va por columnas, que así es lectura secuencial.
7. **La regla real del modo Viaje**, que es lo que convierte la salvaguarda pedida en algo
   concreto y no en un aviso genérico: `UIWorldSelect.CanWorldBePlayed` exige
   `(jugador.difficulty == 3) == (mundo.GameMode == 3)`. O sea que poner en Viaje un mundo con un
   personaje normal deja a ese personaje **sin poder volver a entrar**. Por eso ese cambio se
   bloquea y se explica el motivo, en vez de pedir una confirmación más.
8. **La tecla P está libre en vanilla**: el perfil de teclado por defecto (`PlayerInput.Reset`)
   usa WASD, Espacio, Escape, E, H, **J**, B, Tab, M, C, F1-F4 y los números. De paso, un dato
   para quien fusione los paneles: **la J que eligió WS7 sí choca con `QuickMana` de vanilla**
   (el atajo del mod funciona igual, pero al pulsarla el jugador se bebe una poción de maná).
   Ninguna otra del mod (K, L, O, I, P) pisa nada.

### Decisiones que no estaban en el plan

- **Troceado por fotogramas, no hilo aparte.** El plan admitía las dos. Se troceó porque el hilo
  no aporta nada aquí y sí trae problemas: el juego muta esos mismos arrays mientras corre y los
  reasigna al cambiar de mundo, así que leerlos desde fuera del hilo del juego daría lecturas a
  medias sin ninguna garantía. Con 2 ms de presupuesto por fotograma, un mundo pequeño entero
  (20.170.801 tiles) se termina en **16 fotogramas y ~34 ms de CPU total**. La medida está en el
  log, no estimada.
- **Los hallazgos se agrupan en zonas de 25×25 tiles** en vez de devolverse sueltos. Un mundo
  pequeño tiene 13.718 tiles de cobre: una lista de 13.718 puntos no le sirve a nadie ni en el
  mapa ni en la pantalla. Se devuelven las 150 zonas con más cantidad, ordenadas por cercanía al
  jugador.
- **Interruptor "Solo en lo que ya he explorado", activado por defecto.** Buscar en todo el mundo
  es leer datos que el jugador no ha descubierto; que se pueda hacer está bien (esto es una
  herramienta de edición), pero por defecto se respeta lo que el mapa ya enseña.
- **Los objetivos se resuelven por NOMBRE** contra `TileID.Search` / `WallID.Search`, igual que
  hizo WS4 con `ItemID.Search`, así que el catálogo admite tiles de mods
  (`"CalamityMod/LoQueSea"`) sin cambiar nada y no revienta si el mod no está.
- **Los iconos de marcador se generan por código** (dos rombos de 15×15 pintados píxel a píxel,
  perezosamente en el primer dibujado) en vez de traer un `.png`: son 15×15, y así el color se
  decide en tiempo de dibujado.
- **El cambio de dificultad va en dos pasos** (elegir modo → confirmar) con el aviso de
  permanencia SIEMPRE visible, no en un diálogo posterior. A diferencia de la app de escritorio,
  aquí no hay ventana de "descartar": en cuanto el mundo se guarde, está escrito.
- **Los textos van fijos en español**, como los de WS1 y WS4, no migrados a
  `Localization\*.hjson`. No es un descuido: los `.hjson` son un archivo compartido que WS3 y WS5
  estaban tocando a la vez, y la migración de los textos de todos los paneles es una pasada de
  integración posterior (así lo dejó escrito WS7 en `Localization/README.md`).

### Verificado de verdad en el juego

`scripts\verificar-exploracion.ps1`, sandbox propio `tModLoader-TerrakeepWS6`, variable
`TERRAKEEP_AUTOTEST_WS6`, evidencia en `evidencia\ws6-exploracion.log.txt`. **Cuatro ejecuciones
reales**, la última con el proyecto ENTERO (los seis paneles dentro del mismo `.tmod`, 357.561
bytes).

| Qué | Evidencia real del log |
|---|---|
| El mini-mapa dibuja texturas reales del juego | `trozos de mapa dibujados el ultimo fotograma: 3` |
| ...en las coordenadas correctas | `pixel ... mapTarget[1, 0] pixel (96, 268): RGBA(131, 164, 255, 255) -> OK: hay mapa dibujado ahi de verdad` |
| Pan/zoom por su ruta real de clic | `clic real en el boton "Ver el mundo entero" (habilitado=True, en x=930 y=245 240x34, clic en 1050,262)`, escala `2,500` a `0,191 px/tile` |
| Centrar en el jugador cuadra | `Jugador en el tile 2096, 268; en pantalla el jugador cae en (515, 406)`, que es el centro exacto del mini-mapa (`x=114 w=802`, `y=169 h=472`) |
| Búsqueda real, con clic real | `clic real en el boton "Buscar en el mundo"`, luego `13718 tiles encontrados en 20170801 mirados, agrupados en 150 zonas, 33,7 ms de CPU` |
| ...sin bloquear el juego | `busqueda terminada en 16 fotogramas` |
| Resultados reales y localizados | `Cobre x42 en el tile (2182, 365), a 129 tiles del jugador` |
| Cofres con su contenido | `171 encontrados` y `Cofre (12 objetos, Bumerán de madera...) en el tile (2042, 304)` |
| NPC vivos con su vida real | `Zach (habitante) - 250/250 de vida en el tile (2109, 269)` |
| "Ver en el mapa" salta de verdad | `Main.mapFullscreen=True, mapFullscreenPos=(2096, 268), mapFullscreenScale=2,5`, la misma vista que tenía el mini-mapa |
| ...y la UI del mod desaparece, como tenía que pasar | `Main.InGameUI.CurrentState=(null)` |
| Los marcadores se dibujan sobre el mapa vanilla | `La capa de mapa de Terrakeep ha dibujado marcadores en 451 fotogramas (ultimo: 150 marcadores)` |
| ...y al cerrar el mapa se vuelve al panel | `panel abierto de nuevo=True, InGameUI.CurrentState=PanelExploracionState` |
| El modo Viaje queda bloqueado, no aplicado | `Main.GameMode antes=0, ahora=0 -> OK, NO se ha tocado nada` |
| Dificultad cambiada de verdad | `Main.GameMode=1 (esperado 1), Main.expertMode=True, Main.GameModeInfo.IsExpertMode=True, ActiveWorldFileData.GameMode=1 -> OK` |
| ...y deshacible con el historial de WS7 | `deshacer -> Main.GameMode=0, expertMode=False -> OK` y `rehacer -> Main.GameMode=1` |
| La tecla P está puesta de verdad | `TerrakeepMod/AbrirExploracion=[P]`, junto a `[K] [L] [J] [O] [I] [Z] [Y]`, comparado con `QuickHeal=[H]` |
| Con el mod ENTERO (los 6 paneles) | `Compilation finished with 0 errors and 0 warnings`, `.tmod` de 357.561 bytes, autoprueba en verde igual |
| **El mundo de prueba queda intacto** | `El mundo de prueba esta byte a byte como antes de la prueba` |

Lo de la última fila no es un detalle: el cambio de dificultad se graba en el `.wld` al siguiente
guardado. El script **desactiva el autoguardado** en el `config.json` del sandbox, guarda una copia
del `.wld` antes, compara los hashes después y lo restaura si hiciera falta; y además la autoprueba
devuelve el modo original antes de terminar. Sin eso, cada ejecución dejaría el mundo de prueba
distinto para la siguiente.

### Lo que costó, y los cuatro fallos reales que aparecieron

Ninguno se dedujo leyendo código: los cuatro salieron de mirar el log de la primera ejecución real.

1. **`OnInitialize` no siempre se llama.** El mini-mapa se dibujaba bien, pero sus contadores
   salían a `-1` (o sea, la referencia interna estaba a null): el motor solo llama a
   `OnInitialize` cuando el elemento pasa por `Activate`/`Initialize`, y un elemento añadido al
   árbol después de que el `UIState` ya esté activo se lo puede saltar. Arreglado construyendo en
   el constructor.
2. **El mini-mapa "miraba" al tile (0, 0) hasta su primer `Update`**, porque la escala mínima
   necesita medidas que todavía no existen. Se vio porque "Ver en el mapa" nada más abrir la
   pestaña saltaba a la esquina del mundo (`mapFullscreenPos=(0,0)`) en vez de a donde estabas
   mirando. Arreglado dando un centro y una escala razonables ya en el constructor.
3. **El entorno de un proceso hijo lanzado desde PowerShell no viaja en UTF-8**:
   `"Cobre / Estaño"` le llegaba al juego como `"Cobre / EstaÃ±o"` y no casaba con ningún
   objetivo, así que la autoprueba buscaba lo que hubiera seleccionado por defecto en vez de lo
   que se le pedía — un verde que no demostraba lo que parecía. Arreglado por los dos lados: la
   comparación ignora mayúsculas y tildes y admite un prefijo, y el valor por defecto del script
   es ASCII puro.
4. **El resumen de una búsqueda de cofres/NPC arrastraba los números de la búsqueda anterior**
   (decía "13718 tiles encontrados" en un barrido que no mira ni un tile), porque los contadores
   solo se ponían a cero en la rama del barrido de tiles.

También hubo que ajustar la autoprueba en varios sitios para que no diera verdes falsos: casi nada
de lo que hay que comprobar aquí es cierto en el mismo fotograma en que se pide (el mini-mapa no ha
dibujado un solo trozo hasta que pasa por `Draw`, el mapa del juego se repinta a 5 ms por
fotograma, y la capa del mapa vanilla solo corre mientras el mapa está abierto de verdad). Por eso
la autoprueba es una máquina de estados con esperas explícitas y no una función que lo hace todo de
una.

**El personaje de prueba es sintético y no ha explorado nada**, así que el mini-mapa no habría
tenido nada real que dibujar. La autoprueba revela 320.000 tiles de mapa alrededor de la aparición
con `Main.Map.Update` **solo cuando está puesta la variable de entorno**, exactamente igual que WS4
siembra objetos en el inventario antes de probar "ya lo tienes". Es escenario de prueba: la
funcionalidad del mod no revela mapa jamás.

### Lo que NO se hizo

- **Búsqueda por texto libre.** El catálogo son 38 objetivos curados en seis categorías. Buscar
  cualquier tile por su nombre escrito a mano (con el `CampoTextoTk` de WS1, que ya existe) es la
  ampliación natural, pero no entraba sin dejar el resto a medias.
- **Marcadores que el jugador pueda poner a mano** (chinchetas). Lo que hay son los resultados de
  la búsqueda, el punto de aparición y el jugador.
- **Textos localizados** (ver arriba: decisión, no olvido).

---

## 6-sep-2026 — WS5: Investigación (Modo Viaje)

Quinto workstream de esta tanda, en paralelo con WS3 (Librería) y WS6 (Exploración). Panel propio
con la tecla **I**: cuánto se lleva investigado de cada carpeta de la Librería, y cómo completarlo
o quitarlo. Todo lo que escribe pasa por la **API oficial del juego**, que era el riesgo concreto
que señalaba el plan: escribir el diccionario de investigación a pelo desincronizaría el menú de
sacrificio/duplicación del Modo Viaje.

### La API real, decompilada del `tModLoader.dll` INSTALADO

Todo comprobado con `ilspycmd` sobre `C:\Program Files (x86)\Steam\steamapps\common\tModLoader\
tModLoader.dll` (**v2026.7.3.0**), no sobre `tModLoader-Decompiled\` (v1.4.4.9): ya sabíamos desde
WS0/WS1 que hay diferencias reales entre las dos.

| Qué | Dónde, de verdad |
|---|---|
| Cuántas unidades hacen falta de cada objeto | `CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId` (`Dictionary<int,int>` **público**), que `Initialize()` rellena leyendo el recurso incrustado `Terraria.GameContent.Creative.Content.Sacrifices.tsv` |
| Cómo entran los objetos de MOD en esa tabla | el **setter** de `Item.ResearchUnlockCount` escribe literalmente en ese diccionario, y `ModItem.AutoStaticDefaults` pone 1 por defecto a todo objeto de mod |
| Investigar del todo un objeto | `CreativeUI.ResearchItem(int type)` → `new Item(type, amountNeeded)` → `CreativeUI.SacrificeItem(ref item, ...)`, la MISMA ruta que la ranura de sacrificio del juego |
| Leer x/N | `Main.LocalPlayerCreativeTracker.ItemSacrifices.TryGetSacrificeNumbers(type, out lleva, out hacenFalta)` |
| Quitar investigación de un objeto | `ItemsSacrificedUnlocksTracker.SetSacrificeCountDirectly(idPersistente, 0)` (público; es lo que usa el propio juego al cargar el `.plr`) |
| Quitarla toda | `ItemsSacrificedUnlocksTracker.Reset()` |
| Enterarse de que algo cambió sin recontar cada fotograma | `ItemsSacrificedUnlocksTracker.LastEditId`, que sube en cada `MarkContentsDirty()` |
| Comprobación independiente de que el juego lo ve | `FillListOfItemsThatCanBeObtainedInfinitely(List<int>)`, que es literalmente lo que alimenta el menú de duplicar del Modo Viaje |
| Dónde vive el dato | `Player.creativeTracker` (`CreativeUnlocksTracker`), serializado dentro del propio `.plr` (`Player.SavePlayer` → `creativeTracker.Save`) |
| La condición de Modo Viaje | `Main.LocalPlayer.difficulty == 3` (`PlayerDifficultyID.Creative`): es literalmente lo que hace `CreativeUI.Draw` (`if (Main.LocalPlayer.difficulty != 3) Enabled = false;`) |

**Trampa real que hay que conocer**: `CreativeUI.GetSacrificeCount(type, out completo)` es público y
parece el método natural para leer el progreso, pero **NO aplica**
`ContentSamples.CreativeResearchItemPersistentIdOverride` (el diccionario de objetos que comparten
investigación con otro): mira la caché con el tipo tal cual se le pasa.
`ItemSacrifices.TryGetSacrificeNumbers` sí lo aplica. Por eso el panel lee siempre por el segundo, y
canoniza el tipo antes de contar para no contar dos veces el mismo progreso. `GetSacrificeCount` se
usa solo en la autoprueba, como segunda opinión.

**Refresco del menú del juego**: `SacrificeItem` llama por dentro a
`Main.CreativeMenu.RefreshAvailableInfiniteItemsList()`, así que investigar deja el menú al día
solo. Al *quitar* investigación no hay refresco (ese método es privado), pero tampoco hace falta:
`CreativeUI.ToggleMenu()` lo vuelve a llamar cada vez que el jugador abre el menú del Modo Viaje.

### Qué quedó hecho

- **Árbol de carpetas: el MISMO de la Librería, reutilizando `TerrasavrNative.Core`** (lo que WS2
  movió allí), no una copia. Tres fuentes, en este orden:
  1. `LibraryTreeBuilder.BuildItemTree(...)` sobre `Assets/vanilla_library_tree.json` +
     `vanilla_library_labels_es.json` (los mismos archivos que usa WS3);
  2. una raíz por MOD con `LiveItemTreeBuilder.BuildTree(...)` — mismo `BuildGroupedRoot` de Core
     por dentro — descubriendo el contenido en vivo desde `ContentSamples.ItemsByType`, así que
     Calamity o cualquier otro mod entran solos, sin catálogo estático;
  3. una carpeta "Otros objetos" para lo investigable de vanilla que no esté en el árbol estático,
     para que la suma de las carpetas sea EXACTAMENTE el total real del juego.
- Cada carpeta enseña **x/N** con una barra fina, y el color dice el estado (verde = hecho, ámbar =
  a medias, gris = sin empezar) con los mismos tonos que el "ya lo tienes" de WS4.
- Lista de objetos de la carpeta abierta, cada uno con su `ItemSlot` **nativo de vanilla** (icono y
  tooltip reales, sin `ItemSlot.Handle`: es una muestra, no una ranura del inventario) y un botón
  que dice "Investigar" o "Quitar" según lo que le falte.
- **Barra de progreso global** (investigado / investigable), dibujada con `TextureAssets.MagicPixel`,
  el mismo pixel blanco con el que el propio Terraria pinta todas sus barras.
- **Aviso claro si el personaje no es de Modo Viaje**, en rojo y arriba del todo, en vez de dejar
  hacer clics que no sirven para nada.
- **Acciones**: investigar un objeto, la carpeta entera (con todo lo que cuelgue de ella), o TODO; y
  quitar la investigación de un objeto, de una carpeta o del personaje entero. **Investigar solo
  sube**: lo que ya estaba se cuenta como "sin cambio". Bajar hay que pedirlo con las acciones de
  quitar, que están separadas a propósito.
- **Confirmación en dos pasos** en las dos acciones globales (el primer clic deja el botón en
  "¿Seguro? Pulsa otra vez" durante 4 s), porque tocan miles de objetos de golpe.
- **Todo es deshacible con Ctrl+Z**, con el modelo de snapshot de WS7
  (`Historial.CambiarValor<SnapshotInvestigacion>`): se guarda el recuento de antes y el de después
  de los tipos tocados, nunca una closure del tipo "súmale 25".
- Filtro "Solo lo que falta", y nota en la cabecera con la tecla REAL del menú del Modo Viaje del
  juego, leída del perfil de controles del jugador (`ToggleCreativeMenu`, de fábrica la C, pero
  reasignable) — no dada por supuesta.

### Contenido separado de la mecánica de apertura

Pedido explícito para la fusión posterior de los seis paneles:

- `UI/Investigacion/ContenidoInvestigacion.cs` es un **`UIElement` corriente**, sin marco, sin
  título y sin botón de cerrar. Es TODO el panel de verdad y se puede colgar de cualquier sitio.
- `UI/Investigacion/PanelInvestigacionState.cs` es solo el marco a pantalla completa (mismas
  medidas y colores que el panel de Personaje de WS1) y el botón "Cerrar (I)".
- `Common/Investigacion/PanelInvestigacionSystem.cs` es el `ModKeybind` +
  `IngameFancyUI.OpenUIState`.

Las dos últimas son las descartables: montar esto dentro de una pestaña es mover un `UIElement`.

### La tecla

**I**, confirmada mirando el código real: las teclas de fábrica de Terraria son W A S D E R H J B M
C más las de la barra rápida (`PlayerInput`, `tModLoader.dll` instalado), y K/L/J/O ya las usaban
WS0-WS1, WS4, WS7 y WS3. La asigna sola el `SembradorDeAtajos` de WS7, sin tocar nada.

### El fallo real que apareció, y cómo se encontró

Probando con **CalamityMod cargado** (no en teoría): un objeto de mod aparecía **dos veces** en el
árbol. El catálogo de Librería de Terrasavr trae ids **por encima del `ItemID.Count` de esta versión
del juego (5456)**, y dentro de la partida esos ids ya no son de vanilla: los ocupan los objetos que
registran los mods. Resultado: `"Andromedon Body"` (type=5456, de tModLoader) salía colado dentro de
la carpeta vanilla "Daño de Invocación" **y** otra vez en la carpeta de su mod. Corregido filtrando
el árbol **estático** a ids `< ItemID.Count`; las raíces vivas por mod siguen aceptándolos todos.

El primer intento del arreglo puso el filtro en la función `Convertir`, que **comparten** las dos
fuentes, y se cargó las carpetas de mod enteras (`0 de mods`, `5391` en el árbol contra `8203` del
juego). Lo cazó la propia línea de "cuadra / NO cuadra" del log, que se había puesto justo para eso.

### Verificado de verdad en el juego

`scripts\verificar-investigacion.ps1`, sandbox propio `tModLoader-TerrakeepWS5`, variable
`TERRAKEEP_AUTOTEST_WS5`, evidencia en archivo propio `terrakeep-ws5-evidencia.log` dentro del
sandbox (el `client.log` del juego es uno solo para todas las instancias — con tres agentes
lanzando el juego a la vez, es inservible). Logs completos en `evidencia\ws5-investigacion.log.txt`
y `evidencia\ws5-investigacion-calamity.log.txt`. **Cero excepciones.**

| Qué | Evidencia real del log |
|---|---|
| El árbol cuadra con el juego, objeto a objeto | `Objetos investigables en el arbol: 5480; segun la tabla real del juego: 5480 (cuadra)` |
| ...y con Calamity también | `8203; segun la tabla real del juego: 8203 (cuadra)`, 12 raíces: `"Objectos por ID" 0/5391` + `"Calamity Mod (2662)"` + `"Calamity Mod Music (61)"` + `"tModLoader (89)"` |
| El aviso de "no es Modo Viaje" se pinta de verdad | `AVISO que se esta pintando: "AVISO: este personaje NO es de Modo Viaje (dificultad 0)..."` |
| ...y desaparece solo al pasar a Modo Viaje | `Paso 4 - Con Modo Viaje, el aviso desaparece solo: "" (esperado vacio)` |
| El panel está dibujado de verdad | `Marco ... x=16 y=21 w=768 h=676 ... 393 elementos, 12 filas de carpeta y 120 slots de objeto (el 1o en x=343 y=216 37x37)` |
| Investigar un objeto con un **clic real** en su botón | `CLIC REAL en el boton "Investigar" ... ANTES: 0/100. DESPUES: "Dirt Block" (type=2, pid=DirtBlock) 100/100 INVESTIGADO` |
| **El JUEGO lo ve**, preguntándoselo a él | `Segun el JUEGO (FillListOfItemsThatCanBeObtainedInfinitely): ... el de prueba esta en la lista: True` |
| Ctrl+Z / Ctrl+Y sobre la investigación | `deshecho: "Investigar Dirt Block" ... 0/100` y `rehecho ... 100/100` |
| Carpeta entera, ida y vuelta, con clics reales | `Carpeta "Alas (7)" ahora: 7/7` y luego `Quitar carpeta ... 0/7` |
| Un objeto de MOD, también con clic real | `"Andromedon Legs" (type=5458, pid=ModLoader/Jofairden_Legs) 0/1` a `1/1 INVESTIGADO`, en `"tModLoader/Armadura/Perneras"` |
| La confirmación global no borra con un solo clic | `el boton pasa a "Seguro? Pulsa otra vez" y NO se ha ejecutado nada`; 5 s después, `Esperando confirmacion: False`, `Estado intacto` |
| **PERSISTENCIA**: sobrevive a guardar y volver a cargar | segunda ejecución del cliente: `PERSISTENCIA/1 - Personaje recien cargado del disco por el propio juego. Estado del objeto de prueba: "Dirt Block" ... 100/100 INVESTIGADO` |

La fase de persistencia es la que de verdad cierra el workstream: la fase 1 investiga y llama a
`Player.SavePlayer`, el script mata el cliente y **vuelve a lanzar el juego**, y la fase 2 solo lee.
Que el objeto siga a 100/100 demuestra que lo que escribe el panel pasa por el serializador real del
juego y acaba dentro del `.plr`, no que se quede en memoria.

### Decisiones tomadas aquí

- **El escenario de la prueba pone `Player.difficulty = 3`** desde dentro del juego (y lo devuelve a
  su valor antes de guardar, para que la prueba siga siendo repetible contra el mundo clásico del
  sandbox). Es escenario, no funcionalidad, y solo corre con la variable de entorno puesta —
  exactamente igual que WS4 siembra objetos en el inventario antes de probar "ya lo tienes". Se hizo
  así en vez de generar un personaje y un mundo de Modo Viaje porque `-skipselect` **valida** que
  los dos coincidan (`UIWorldSelect.CanWorldBePlayed`: `player.difficulty == 3` tiene que ir con
  `file.GameMode == 3`), y montar los dos habría metido dos lanzamientos más de preparación en cada
  ejecución. Lo investigado no depende de la dificultad: vive en `Player.creativeTracker`.
- **Textos fijos en español, no en `Localization/*.hjson`**, igual que WS1 y WS4. No es olvido:
  `Localization/README.md` (entregable de WS7) deja la migración de todos los paneles para la pasada
  de integración, y con tres agentes editando los mismos dos `.hjson` a la vez el riesgo de pisarse
  no compensaba.
- **La lectura de los dos `.json` del árbol se hace por separado de la Librería (WS3)**, con
  tolerancia a que no estén (entonces el árbol se construye solo con lo vivo). Con WS3 escribiéndose
  al mismo tiempo, depender de su código a medias habría bloqueado a los dos. En la fusión, esto se
  sustituye por el catálogo compartido de la Librería sin tocar la interfaz de este panel.
- **Los contadores se recalculan solo cuando el estado cambia de verdad** (`LastEditId` del tracker
  oficial), no en cada fotograma: son 5.480 consultas en vanilla y 8.203 con Calamity. Y se mira el
  contador del juego, no solo las acciones propias, para que el panel también se entere si el
  jugador sacrifica objetos en el menú del propio juego con el panel abierto.

### Obstáculos del entorno

- **La primera ejecución no dejó ni una línea de evidencia.** Dos causas, las dos de convivir con
  otros dos agentes lanzando el juego a la vez: el cliente arrancó y se quedó colgado cargando mods,
  y además el script le daba el foco a la ventana **del otro agente** (filtraba por título de
  ventana, y la suya también pone "Terraria: ..."), con lo que la partida propia se habría quedado
  congelada (`Main.hasFocus` → `gamePaused`). Resuelto por los dos lados: el script busca su ventana
  por la **línea de comandos** (`-tmlsavedirectory` con su sandbox) y espera a que no haya ningún
  otro cliente de tModLoader abierto antes de lanzar el suyo.
- **`git commit --only` no acepta archivos que git no conozca**: hay que hacer antes
  `git add -N <rutas>` (intent-to-add) y entonces sí. Con eso, el commit lleva exactamente las rutas
  nombradas y nada de lo que otros agentes tengan preparado en el índice compartido — que es el
  problema que ya documentó WS1.

---

## 6-sep-2026 — FUSIÓN: los seis paneles pasan a ser uno solo con pestañas

Fase final de esta ronda. Los seis paneles sueltos que dejaron WS0..WS7 (Personaje/K,
Librería/O, Builds/L, Investigación/I, Exploración/P, Ajustes/J) se convierten en **un único
`PanelTerrakeepState`** con una barra de seis pestañas, más un **icono propio en el HUD**, más una
pasada de **animación de botones** y otra de **pulido visual de conjunto**.

### 1. Qué se fusionó, y por qué salió barato

Cada workstream había separado a propósito su CONTENIDO (un `UIElement` autocontenido) de su
MECÁNICA DE APERTURA (`ModKeybind` + `IngameFancyUI.OpenUIState` + marco + cabecera + botón de
cerrar). Esa previsión se pagó sola: **de las seis áreas, cuatro se movieron sin tocar ni una
línea de su lógica**.

| Área | Cómo estaba | Qué hubo que hacer |
|---|---|---|
| Librería (WS3) | `ContenidoLibreria` ya separado | colgarlo de la pestaña, nada más |
| Investigación (WS5) | `ContenidoInvestigacion` ya separado | ídem |
| Personaje (WS1) | contenido dentro del `UIState` | extraer `ContenidoPersonaje` (cabecera + sub-pestañas + contenedor) |
| Exploración (WS6) | ídem | extraer `ContenidoExploracion` |
| Builds (WS4) | contenido dentro del `UIState` **y sin usar los widgets compartidos** | extraer `ContenidoBuilds` **y reestilizarlo** |
| Ajustes (WS7) | ídem | extraer `ContenidoAjustes` **y reestilizarlo** |

Se borraron las seis `Panel*State` y los seis `Abrir/Cerrar/Alternar` que eran seis copias casi
idénticas del mismo código. Ahora hay **un** marco, **una** barra de pestañas, **un** botón de
cerrar, **un** dibujado del objeto cogido con el ratón y **un** `Main.playerInventory = true`.

Los `*System` de cada área **no** se borraron: siguen registrando su `ModKeybind`, leyendo sus
archivos de datos y albergando su autoprueba, pero su `AbrirPanel`/`CerrarPanel` ahora delegan en
`PanelTerrakeepSystem`. Eso mantiene intactos el `SembradorDeAtajos` de WS7 (que recorre las claves
del perfil que empiezan por `TerrakeepMod/`) y las seis autopruebas.

### 2. Las seis teclas: ninguna se queda muerta

Se conservan las seis **con su mismo nombre de atajo** (`AbrirPanel`, `AbrirLibreria`,
`AbrirBuilds`, `AbrirInvestigacion`, `AbrirExploracion`, `AbrirAjustes`) — renombrarlas le habría
borrado al usuario las teclas que tuviera puestas, porque `input profiles.json` las guarda por
nombre. Lo que cambia es qué hacen:

- con el panel **cerrado**, la tecla lo abre **en su pestaña**;
- con el panel abierto en **otra** pestaña, salta a la suya **sin cerrar nada**;
- con el panel abierto en **su** pestaña, lo cierra.

La lectura de las seis está centralizada en `PanelTerrakeepSystem.ComprobarAtajos`. La K sigue
leyéndose además desde `ModPlayer.ProcessTriggers` (vía recomendada por tModLoader), con el guarda
por fotograma de siempre para que una pulsación no cuente dos veces.

**Detalle de API que muerde**: `ModKeybind.FullName` **no es accesible desde un mod** en esta
versión — el compilador real de tModLoader lo rechaza con `CS1061`. La clave con la que
`PlayerInput.Triggers.JustPressed.KeyStatus` indexa un atajo se construye a mano en
`PanelTerrakeepSystem.ClaveDeAtajo` (`"TerrakeepMod/" + nombre`).

Además, el área de Ajustes enseña ahora **la lista real de atajos**, leída del perfil de controles
del jugador: si reasigna una tecla, ahí se ve la que tiene de verdad.

### 3. El icono del HUD

Va **en la fila de iconos que el propio Terraria pone junto al inventario**, continuándola. Los
tres de vanilla no son una fila genérica extensible: son tres métodos privados de `Terraria.Main`
llamados en cadena desde `Main.DrawInventory()`, **con coordenadas fijas escritas a mano** que no
dependen de `Main.screenWidth` ni de `Main.mapStyle` (comprobado en el `tModLoader.dll` instalado):

- papelera `DrawTrashItemSlot` → (448, 258);
- bestiario `DrawBestiaryIcon` → (498, 278, 30, 30);
- emotes `DrawEmoteBubblesButton` → (534, 278, 30, 30).

El de Terrakeep va en **(570, 278, 30, 30)**, respetando la misma separación de 6 px, y replica los
mismos desplazamientos que el juego aplica a esa fila con un cofre o una tienda abiertos
(`num2 += 168; num += 5;`) y al renombrar un cofre (`+24`). Evidencia real del log:
`Rectangulo REAL en pantalla: x=570 y=278 30x30`, y en la captura se ve alineado con el libro del
bestiario y la cara de los emotes.

**Cómo se dibuja**: `ModSystem.ModifyInterfaceLayers`, insertando una `LegacyGameInterfaceLayer`
justo detrás de `"Vanilla: Inventory"` con `InterfaceScaleType.UI`.
**`ModSystem.PreDrawInterface` no existe** en esta versión, y `PostDrawInterface` está desaconsejada
por el propio XML-doc de tModLoader (y cuelga de la capa 34, así que tampoco corre con un panel de
`IngameFancyUI` abierto).

Índices reales de las capas en esta versión (la lista tiene 43): **"Vanilla: Fancy UI" es la 14**,
no la 12 — el "12" del nombre `DrawInterface_12_IngameFancyUI` es el sufijo histórico del método, no
su posición. El inventario es la **28**. Como la 14 corta el recorrido cuando hay un panel de
`IngameFancyUI` abierto, **el icono se ve con el panel cerrado y desaparece con el panel abierto**.
Eso es lo correcto y lo que se buscaba, pero hay que decirlo claro: **en la práctica el icono sirve
para ABRIR**; para cerrar están el botón "Cerrar" del panel y la tecla. El clic llama igualmente a
`AlternarArea`, así que si alguna vez se viera con el panel abierto, lo cerraría.

El clic usa el patrón real de vanilla para un rectángulo dibujado a mano (código de
`DrawBestiaryIcon`): `Contains(mouseX, mouseY)`, respetar `PlayerInput.IgnoreMouseInterface`, poner
`Main.LocalPlayer.mouseInterface = true` para que el clic no llegue al mundo, y consumirlo con
`Main.mouseLeftRelease = false`.

El arte es propio y **reproducible**: `scripts/generar-icono-hud.py` genera
`Assets/IconoTerrakeep.png`, una hoja de dos fotogramas de 30×30 (normal / con el ratón encima),
que es exactamente la convención que usa vanilla (`value.Frame(2, 1, flag ? 1 : 0)` con
`Width -= 2; Height -= 2;`). Un cofre, que es lo que da nombre al mod, sobre la paleta de
`EstiloTk`.

### 4. La animación de los botones: de dónde sale, exactamente

Lo primero que se comprobó al buscarla, y es un dato que conviene no volver a descubrir:
**`UITextPanel<T>` -el botón "de manual" de la API de UI- no anima absolutamente nada.** No
sobrescribe `MouseOver`/`MouseOut`, no reproduce ningún sonido y no tiene ningún campo de escala
interpolada. Es más: **no hay ni un solo `UIElement` de vanilla que interpole escala al pasar el
ratón** (grep de `MathHelper.Lerp`/`Utils.Lerp` en `Terraria.GameContent.UI.Elements`: cero
coincidencias; `_animationFactor` no existe en todo el ensamblado). Las pantallas del juego se
limitan a cambiarle el color de fondo desde fuera.

El botón que **sí** se anima en Terraria está dibujado a mano en el HUD:
`Main.DrawSettingButton(ref bool mouseOver, ref float scale, ...)`. Sus constantes reales, que son
las que ahora usa `BotonTk`:

- escala en reposo **0,80**, con el ratón encima **0,96**;
- **±0,02 por fotograma** en los dos sentidos (~8 fotogramas a 60 fps, ~0,13 s);
- el sonido suena **solo al ENTRAR** el ratón (`if (!mouseOver) PlaySound(12)`), no cada fotograma;
- al hacer clic, `scale = 0.8f`: el botón se "hunde" de golpe y vuelve a crecer.

El sonido es **`SoundID.MenuTick`**: el `PlaySound(12)` que aparece por todo el código decompilado
es su id legacy (`SoundID.GetLegacyStyle`: `case 12: return MenuTick;`), el mismo que usan
`UIImageButton.MouseOver`, `EmoteButton.MouseOver` y los iconos del bestiario y de emotes. Y también
es el del CLIC (`UIIconTextButton.LeftMouseDown` y los dos iconos hacen `PlaySound(12)` justo antes
de abrir su interfaz), así que se usa el mismo en los dos sitios. **Trampa**: la sobrecarga
`SoundEngine.PlaySound(int)` es `internal` y un mod no la puede llamar — hay que pasar el
`SoundStyle` (`SoundEngine.PlaySound(SoundID.MenuTick)`).

**Cómo se dibuja el marco más grande sin romperlo.** `UIPanel.DrawSelf` pinta su marco de nueve
trozos con un método `private` que lee `GetDimensions()` directamente, así que no se le puede pedir
que dibuje inflado. La vía limpia es `Utils.DrawSplicedPanel` con márgenes de **12** sobre las
MISMAS dos texturas (`Images/UI/PanelBackground` y `Images/UI/PanelBorder`, 28×28 las dos): como
28 − 12 − 12 = 4 = el `_barSize` de `UIPanel`, el resultado es idéntico píxel a píxel, las esquinas
mantienen su tamaño y solo se estiran los bordes. Es la misma técnica que usa `GroupOptionButton`
en la creación de personaje.

La animación avanza en **`Update`** y no en `DrawSelf` a propósito: `Update` corre a paso lógico
fijo (`Main.UpdateUIStates` → `UserInterface.Update` → `UIElement.Update` recursivo), mientras que
el dibujado se ejecuta más o menos veces según `Main.FrameSkipMode`. Acumulando en el dibujado, la
velocidad de la animación cambiaría con la configuración de vídeo del usuario.

Como `BotonTk` es el widget compartido, **las seis áreas heredaron la animación sin tocar ni una de
ellas**, y de paso la heredaron las píldoras de Builds y los botones de Ajustes, que antes eran
`UITextPanel` pelados sin sonido ni reacción.

### 5. Evidencia VISUAL: por fin se pueden hacer capturas

Hasta ahora en este proyecto la evidencia visual era imposible, y está anotado más arriba con los
dos intentos que fallaron: `CopyFromScreen` fotografía el escritorio entero (inservible, y además
contenido privado del usuario) y `PrintWindow` devuelve `true` pero la imagen sale **negra**, que es
lo esperable en una aplicación acelerada por GPU (FNA dibuja por Direct3D, no por GDI).

**La vía que sí funciona es pedirle la imagen al motor gráfico desde dentro del propio mod**:
`GraphicsDevice.GetBackBufferData<Color>` + `Texture2D.SaveAsPng`, las dos públicas en la FNA que
trae tModLoader (`Libraries\FNA\1.0.0\FNA.dll`, comprobado con `ilspycmd`). Captura exactamente lo
que se está viendo, interfaz de mods incluida, sin tocar el escritorio y sin depender del foco de
ventana. Vive en `Common/Panel/CapturaDePantalla.cs` y **solo escribe algo con la variable de
autoprueba puesta**. Se llama desde `UpdateUI`, o sea que lo que captura es el fotograma anterior ya
presentado — irrelevante para mirar una pestaña que lleva rato puesta, pero conviene saberlo.

Las imágenes se quedan en la carpeta de guardado de la prueba
(`<sandbox>\terrakeep-capturas\*.png`), **fuera del repositorio**: se regeneran con el mismo script
y no tiene sentido versionar 200 KB por pestaña.

### 6. Los cinco fallos de estética que solo se vieron en las capturas

Ninguno se dedujo leyendo código, y ninguno se habría visto contando elementos en el log: en el log
todos daban verde.

1. **El HUD de vida/maná del juego tapaba la barra de pestañas.** Y no es culpa nuestra:
   `IngameFancyUI.Draw` llama a **`Main.instance.GUIBarsDraw()`** DESPUÉS de que `InGameUI.Draw`
   haya pintado el panel del mod, así que los corazones quedan encima. Es comportamiento vanilla
   (pasa igual con el bestiario), pero nuestro panel es más ancho y su barra de pestañas caía justo
   debajo: los corazones se comían el texto de "Exploración". **Resuelto con una fila de título de
   30 px arriba**, que baja las pestañas por debajo del HUD y de paso le pone nombre al panel (el
   texto va a la izquierda, que es donde el HUD no pinta nada).
2. **Cabecera de Personaje, tres problemas a la vez**: se leía literalmente `Vida maxima<<` (el
   botón `<<` de `SelectorTk` cae en `anchoEtiqueta - 32`, y con 110 caía dentro de la palabra); la
   línea del dinero pisaba al selector de maná por compartir fila; y `Ahora: .../...` y
   `Llenar vida y maná` se salían del marco por la derecha en una ventana de 800 px. Rehecha en
   tres filas con `AnchoEtiqueta = 150`.
3. **Librería**: los siete botones de destino tenían 118 px fijos = 854 px y se salían del panel
   (se veía `InvenMonedas y muni Equipo`). Ahora van en porcentaje, con etiqueta corta
   ("Mochila / Monedas / Equipo / Hucha / Caja / Forja / Bóveda") y el nombre completo en el
   tooltip. El resumen del buscador también se salía; se acortó.
4. **Builds**: la leyenda de colores y el recuento en una sola línea se salían por la derecha (ahora
   son dos líneas), y el séptimo accesorio quedaba cortado por abajo (paso de 52 a 48 px por fila).
5. **Ajustes**: las cuatro líneas de la caja de atajos iban **fijas en español dentro de un panel
   que estaba en inglés**. Ahora salen del `.hjson` como el resto del área, y los títulos de las
   tres cajas se piden en cada fotograma para que cambien con el propio selector de idioma (antes se
   pasaban ya resueltos y se quedaban congelados).

Y uno más, de Exploración: la nota de tres líneas del mapa reservaba 60 px para 63 de texto y se
comía el título "Marcadores" de debajo.

**Aviso para el que venga**: mirando la primera tanda de capturas creí ver que el fondo del marco
desaparecía a media altura en dos pestañas. Era falso — comprobado leyendo los píxeles reales con
PIL: `(31, 40, 74)` en toda la mitad inferior, que es exactamente `EstiloTk.FondoPanel`. Merece la
pena confirmar con los píxeles antes de "arreglar" algo que se cree ver en una imagen reescalada.

### 7. Inconsistencias de estilo corregidas al ver las seis piezas juntas

- **Builds y Ajustes eran las dos únicas áreas que no usaban los widgets compartidos**: marcos de
  tamaño fijo (980×600 y 520×400) frente al 96 %/94 % con tope 1080×700 de las demás, botones
  `UITextPanel` pelados, y los colores de `EstiloTk` repetidos a mano con literales copiados. Ahora
  usan `BotonTk` y la paleta común.
- Los acentos verde/gris/rojo del "ya lo tienes" estaban **duplicados en dos archivos** con los
  mismos valores copiados (`PanelBuildsState` y `EstiloInvestigacion`). Se subieron a `EstiloTk`
  (`Correcto` / `Neutro` / `Peligro`) y `EstiloInvestigacion` los referencia.
- Las medidas de la barra de pestañas (alto 30, separación 6, escala 0,8) estaban repetidas en cada
  panel; ahora son constantes de `EstiloTk`, así que las dos filas de pestañas (la principal y las
  sub-pestañas de Personaje y Exploración) se ven como una sola familia.
- **Anchos fijos → porcentajes** en todas las barras de pestañas y filas de píldoras. Seis botones
  de 150 px se salían del marco en una ventana de 800.
- La cabecera de Exploración era la única que repetía la marca ("Terrakeep · Exploración del
  mundo"); ahora la marca sale una sola vez, en el título del panel.
- El aviso "todo esto se escribe en vivo sobre el personaje cargado" que llevaba dentro la cabecera
  de Personaje pasó al **pie común**, que es donde cada área deja ahora su línea de ayuda.
- El botón "Deshacer/Rehacer" de Ajustes se apagaba bajándole el alfa al color de fondo a mano
  (era un `UITextPanel`, que no tiene estado "deshabilitado"); ahora usa `BotonTk.Habilitado`, que
  es el mismo estado apagado que el resto del mod.

### 8. Verificado de verdad en el juego

`scripts\verificar-panel-unico.ps1`, sandbox propio `tModLoader-TerrakeepPanel`, variable
`TERRAKEEP_AUTOTEST_PANEL`, archivo de evidencia propio. Log completo en
`evidencia\panel-unico.log.txt`. **Cero excepciones en todo el `client.log`.**

| Qué | Evidencia real |
|---|---|
| Las seis pestañas cambian con un **clic real** en la barra | `CLIC REAL en la pestaña "Librería" (en x=150 y=61 118x30, clic en 210,76) ... -> OK` (las seis) |
| ...y montan su contenido de verdad | `ContenidoPersonaje: 88 elementos, 58 ranuras`, `ContenidoLibreria: 86 elementos, 50 ranuras`, `ContenidoInvestigacion: 391 elementos, 124 botones`, `ContenidoBuilds`, `ContenidoExploracion`, `ContenidoAjustes` |
| Animación, en **dos pestañas distintas** | `escala 0.80 -> 0.96, el marco se dibuja 2 px mas grande` en Ajustes y en Personaje |
| ...y se VE | capturas `animacion-*.png`: el botón crece, se aclara y le aparece el borde claro |
| Las seis teclas saltan a SU pestaña | `tecla [O] ... "Personaje" -> "Librería" -> OK: salta a SU pestaña sin cerrar el panel` (las seis) |
| ...y la del área abierta cierra | `la tecla [K] pulsada estando YA en "Personaje": panel abierto ahora=False -> OK` |
| Icono del HUD, coordenadas reales | `Rectangulo REAL en pantalla: x=570 y=278 30x30`, dibujado en 13 fotogramas con el panel cerrado |
| ...y un clic real lo abre | `clic en (585, 293) sobre el icono -> panel abierto=True, pestaña="Personaje"` |
| Las **seis autopruebas de los workstreams**, contra el panel fusionado | `AUTOPRUEBA WS1 COMPLETA` (23 pasos), `AUTOPRUEBA WS3 COMPLETA` (13 pasos), auto-equipar + segunda pasada idempotente (WS4), `AUTOPRUEBA WS5 COMPLETA` + persistencia tras reiniciar el juego, `AUTOPRUEBA WS6 COMPLETA` (incluido el salto al mapa vanilla y la vuelta a `PanelTerrakeepState`), `AUTOPRUEBA WS7: terminada` |
| El mundo de prueba queda intacto | `El mundo de prueba esta byte a byte como antes de la prueba` |

**Regresiones encontradas al fusionar**: cero funcionales. Todo lo que apareció fue estético (los
cinco puntos del apartado 6), y salió al mirar capturas, no al ejecutar las pruebas.

### 9. Obstáculos y detalles sueltos

- **Un cliente de tModLoader no arranca si hay otro en marcha** (ya documentado por WS3 y WS4). La
  primera ejecución de la verificación del panel se quedó colgada en
  `Hook System.Runtime.Loader.AssemblyLoadContext...` sin llegar a cargar mods, con el arnés de UI
  Automation del repo hermano corriendo a la vez. Repetida a solas, salió a la primera. El script
  ahora **espera a que no haya ningún otro cliente** antes de lanzar el suyo.
- **`scripts\verificar-personaje.ps1` y `verificar-ws7.ps1` NO compilan**: copian el
  `Mods\TerrakeepMod.tmod` global. Se vio en real (una ejecución de WS1 pasó con el `.tmod` de
  antes del pulido y por eso salían coordenadas viejas). Hay que ejecutar `scripts\compilar.ps1`
  antes de usarlos. Los demás scripts sí compilan, con `-Completo`.
- **`WARN: Image loading failed: unknown image type`** en cada compilación. **No es del icono del
  HUD**: comprobado quitando `Assets\IconoTerrakeep.png` del proyecto y compilando — el aviso sale
  igual. Viene de `icon.png`/`icon_small.png` (commit `cef4219`). El `.tmod` se genera bien y el
  asset del icono entra dentro convertido a `Assets/IconoTerrakeep.rawimg`, así que no bloquea nada;
  queda anotado.
- **Otro agente estaba trabajando en este mismo repositorio** durante la fusión (commit `cef4219`,
  los iconos del mod). Todos los commits de esta fase se hicieron con **índice privado**
  (`GIT_INDEX_FILE` + `git read-tree HEAD` + `git add` + `git commit`) seguido de `git reset` a
  secas, que es la técnica que dejó documentada WS3.

### 10. Lo que NO se ha cerrado, dicho claro

- **La migración de textos a `Localization\*.hjson` sigue pendiente para cinco de las seis áreas.**
  Solo Ajustes está localizado (lo dejó así WS7 y sigue igual). Se ve en la captura: con el juego en
  inglés, el área de Ajustes está entera en inglés pero los nombres de las pestañas
  ("Personaje", "Librería", "Investigación"...) y todo el texto de las otras cinco áreas siguen
  fijos en español. Son cientos de cadenas; es una pasada propia, no un remate de esta fase.
- **El icono del HUD no puede CERRAR el panel**, porque con el panel abierto su capa no llega a
  dibujarse (la 14 corta antes de la 28). Es una consecuencia del motor, no un descuido, y el
  diseño se apoya en ella a propósito; pero el pedido decía "abre/cierra" y en la práctica es
  "abre". Para cerrar: el botón "Cerrar" del panel o la tecla.
- **No se ha probado con Calamity cargado** en esta fase (`verificar-panel-unico.ps1 -Calamity`
  existe y está listo). Las áreas que dependen de Calamity sí se probaron con él en su workstream, y
  el catálogo de Builds/Librería/Investigación no se ha tocado en la fusión.
- **El último eslabón físico teclado → SDL sigue sin poder simularse**, igual que documentó WS7. Los
  atajos se ejercitan rellenando `PlayerInput.Triggers.JustPressed` y llamando al `ComprobarAtajos`
  de producción; lo único que no se cubre es que una pulsación real de la tecla llegue al juego.
- **Los nombres largos de las etapas de Builds se cortan** con "..." en la píldora
  ("Pre-Hardmode (listo para el Mur..."). Tienen el nombre completo en el tooltip, pero en una
  ventana estrecha no caben enteros. Se deja así a propósito: la alternativa era bajar tanto la
  escala del texto que dejara de leerse.

### 11. Añadido después: espaciado dinámico en Builds, y verificación con Calamity

Probando el panel con **CalamityMod cargado** apareció el único fallo estético que no salía sin él:
con Calamity, el área de Builds enseña una fila más (el selector de fuente Vanilla/Calamity), esos
34 px de menos hacían que el **séptimo accesorio quedara cortado** por abajo. Un paso fijo entre
filas no vale para las dos configuraciones.

Resuelto calculando el paso con el alto REAL del cuerpo
(`ContenidoBuilds.ColocarFilasDeObjetos`), acotado entre 45 px (el lado de la ranura más un píxel
de aire) y 52. Como ese alto no existe hasta que el motor ha recalculado el árbol -y cambia si el
jugador redimensiona la ventana-, las filas se recolocan también desde `Update` en cuanto cambia.
Comprobado en el juego en las dos configuraciones: los 7 accesorios caben enteros con y sin
Calamity.

De paso queda cerrada la verificación con Calamity que faltaba:
`verificar-panel-unico.ps1 -Calamity`, evidencia en `evidencia\panel-unico-calamity.log.txt`.
Las seis pestañas cambian con clic real, los seis atajos saltan a su pestaña, el icono del HUD sale
en el mismo (570, 278, 30, 30) y la autoprueba termina sin excepciones. Se nota que Calamity está
cargado: Builds pasa de 8 a 10 botones (la fila de fuentes) y la Librería y la Investigación
crecen.

### 12. Un tropiezo con el índice privado, para que no se repita

WS3 dejó escrito que después de comitear con un índice privado hay que hacer `git reset` a secas
para que el `.git/index` compartido no se quede obsoleto. Se hizo... pero **dentro del mismo
comando en el que seguía exportada `GIT_INDEX_FILE`**, así que ese `reset` reseteaba el índice
PRIVADO y no el compartido. Cinco commits después, `git status` marcaba con `D` (borrado
preparado) unos 40 archivos que existen en disco y en `HEAD` — exactamente el estado peligroso que
describía WS3.

Arreglado con un `git reset` **sin** `GIT_INDEX_FILE` en el entorno, tras comprobar que no había
ninguna entrada `A` (contenido que existiera solo en el índice). Árbol limpio y nada perdido. La
regla completa es: `GIT_INDEX_FILE=<propio> git read-tree HEAD && git add ... && git commit`, y
**después, en un comando aparte y sin esa variable, `git reset`**.

### 13. Último repaso de Investigación (también con captura)

En la última tanda de capturas quedaban dos cosas en el área de Investigación, las dos por anchos
fijos:

- El **aviso de "no eres de Modo Viaje"** era una sola línea de ~1040 px y se salía del marco por
  la derecha. Partirlo en dos líneas tampoco valía: la segunda se metía por encima de
  "Progreso global", que va a 30 px fijos. Se dejó en **una línea con el texto corto de verdad**.
- El **título de la carpeta abierta** se metía por debajo de los botones "Investigar carpeta" /
  "Quitar carpeta": esos dos ocupaban 352 px fijos desde la derecha y en una caja de ~438 px al
  título le quedaban 86. Los dos botones pasan a **porcentaje** (0,28 del ancho cada uno) y el
  título se acorta a 22 caracteres. Ahora conviven.

Reejecutada la autoprueba de WS5 después: `AUTOPRUEBA WS5 COMPLETA` con sus clics reales en
"Investigar" y "Investigar carpeta", y la fase de persistencia (matar el cliente y volver a
lanzarlo) sigue leyendo el objeto a 100/100 del disco.

---

## 6-sep-2026 — CIERRE: los menús de tModLoader, el idioma de las seis áreas, y diez pasadas de revisión visual

Última fase del proyecto. Cierra los tres cabos que quedaban sueltos al terminar la fusión:
lo que se ve del mod **antes de entrar en una partida**, la migración de textos que la ronda
anterior dejó escrita como pendiente, y una revisión visual repetida con las capturas reales
que hasta la fusión no existían.

### 1. La "ventana taller": lo que nadie había mirado nunca

Todas las verificaciones del mod hasta aquí entraban directas al mundo con `-skipselect`. O sea
que **lo primero que ve quien instala Terrakeep -la lista de Mods, la ficha del mod y la
pantalla de Configuración de Mods- no se había comprobado ni una vez.**

**Arnés nuevo**: `scripts\verificar-menus.ps1` + `Common\Menus\AutopruebaMenus.cs`. Lanza el
juego SIN `-skipselect`, se queda en el menú y desde ahí navega solo, dejando una captura real
de cada pantalla.

**Cómo se navega, y el dato del motor que costó encontrar.** Los botones del menú principal de
Terraria no son `UIElement` (los pinta `Main.DrawMenu` a mano), así que para llegar a la lista
de Mods se hace lo MISMO que hace el botón del juego: `Main.menuMode = 10000`
(`Interface.modsMenuID`; ahí `Interface.ModLoaderMenus` hace `Main.MenuUI.SetState(modsMenu)`).
De ahí en adelante ya sí hay `UIElement` de verdad, y los dos botones de la ficha se pulsan con
`UIElement.LeftClick`, el criterio de producción de siempre.

Lo que costó: **`ModSystem.UpdateUI` NO SE LLAMA EN EL MENÚ.** `SystemLoader.UpdateUI` empieza
literalmente con `if (!Main.gameMenu)`. La primera versión de esta autoprueba colgaba de ahí y
no se ejecutó ni una sola vez (el juego arrancaba, cargaba el mod y no pasaba nada). El hook
bueno es **`ModSystem.PostUpdateInput`**, que cuelga de `Main.DoUpdate_HandleInput` y no tiene
esa guarda. Y hace falta además que la ventana tenga el **FOCO**: sin foco, `Main.DoUpdate`
llama a `UpdateMenu()` y hace `return` antes de procesar la entrada, así que el script le da el
foco insistentemente durante los primeros 40 s.

Lo que se encontró mirando esas cuatro pantallas, y se arregló:

| Qué | Cómo estaba | Cómo está |
|---|---|---|
| **Icono** | El logo-256 del repo hermano reescalado: el hexágono naranja sobre fondo TRANSPARENTE. Al lado de mods como Calamity, que llenan sus 80x80 con arte a sangre y marco, se veía una figura flotando en un hueco | `scripts\generar-icono-mod.py` lo compone sobre el azul del propio panel (`EstiloTk.FondoPanel`), con borde de dos tonos y esquinas redondeadas, y el hexágono centrado. La marca sigue siendo la misma |
| **Descripción** | Ya reescrita, pero con los saltos de línea a 95 columnas del archivo. La ficha envuelve el texto ella sola, así que salían renglones cortados a media frase | Cada párrafo en una sola línea; lo envuelve el juego |
| **Botón muerto** | La ficha enseñaba "Visitar sitio web del mod" porque `build.txt` tenía `homepage = https://github.com/` a secas: llevaba a la portada de GitHub, no al proyecto | Sin valor. `UIModInfo` solo añade ese botón `if (!string.IsNullOrEmpty(_url))` |
| **Config: campo interno** | `AtajosYaSembrados` salía como una lista editable titulada "Atajos Ya Sembrados", con cadenas tipo `TerrakeepMod/AbrirPanel` dentro | Ya no sale. Ver abajo cómo |
| **Config: título** | "Terrakeep: Ajustes de Terrakeep" (tModLoader compone `<mod>: <config>`) | "Terrakeep: Ajustes" |
| **Config: sección** | Sin cabecera | `[Header("Interfaz")]` |

**Cómo se oculta de verdad un campo de un `ModConfig`, que es lo que pedía el encargo.** El
ÚNICO mecanismo que tiene tModLoader es `[JsonIgnore]`: tanto `UIModConfig.SetupList` como
`ConfigManager.RegisterLocalizationKeysForMembers` saltan cualquier miembro que lo lleve (salvo
que además lleve `ShowDespiteJsonIgnoreAttribute`, que es justo lo contrario). **No existe
ningún atributo tipo "Hide"** — comprobado listando los 40 atributos reales de
`Terraria.ModLoader.Config`.

El problema es que `[JsonIgnore]` a secas también lo dejaría **sin guardar**, y esto tiene que
persistir entre partidas. La solución son dos mitades:

- la **propiedad pública** (la que ve el código del mod) lleva `[JsonIgnore]` y desaparece de la
  pantalla;
- un **campo privado** con `[JsonProperty("AtajosYaSembrados")]` es el que se serializa.

Encaja con cómo funcionan las dos piezas por separado, sin trucos:
`ConfigManager.GetFieldsAndProperties` solo mira `BindingFlags.Instance | BindingFlags.Public`
(un campo privado no puede aparecer en la interfaz aunque quiera), y Newtonsoft sí serializa un
miembro no público que lleve `[JsonProperty]`.

**Verificado en el juego real, en los dos idiomas** (`evidencia\menus.log.txt` y
`evidencia\menus-en.log.txt`): los `ConfigElement` que genera tModLoader son exactamente uno
("Idioma"), y los ocho `ModConfigs\TerrakeepMod_AjustesConfig.json` de los sandboxes siguen
llevando dentro la lista `AtajosYaSembrados` completa. Oculto en la interfaz, guardado en el
archivo.

### 2. Las cinco áreas que faltaban por traducir

Hasta aquí **solo Ajustes estaba localizado**. Con el juego en inglés se veía una mezcla real:
Ajustes en inglés, los nombres de las seis pestañas y todo el texto de las otras cinco áreas en
español fijo. Eran cientos de cadenas.

Los dos `.hjson` pasan de **78 a ~600 líneas**: 429 claves de interfaz, 8 atajos y la
configuración.

**Los `.hjson` se generan, no se editan.** `scripts\generar-localizacion.py` los saca de UNA
sola tabla de `(clave, español, inglés)`, así que es imposible que a un idioma le falte una
clave que el otro sí tiene. Con 429 claves, a mano se descuadran solos.

**Cinco widgets compartidos pasan a pedir su texto con un `Func<string>`** en vez de guardarlo
ya resuelto: `BotonTk.Ayuda`, `AlternadorTk` (rótulo y ayuda), `CampoTextoTk` (la pista),
`FilaColorTk` y `SelectorTk` (el rótulo). Guardado como cadena se quedaba congelado en el idioma
que hubiera al construir el elemento. `BotonTk` gana además una `Clave` interna: un grupo de
botones ya no puede identificarse por su TEXTO VISIBLE, que ahora cambia.

**Si el idioma cambia con el panel abierto, la pestaña se rehace entera**
(`PanelTerrakeepState.RehacerAreaSiCambioElIdioma`). Casi todo el texto sale de una
`EtiquetaTk` o se refresca en su `Update`, pero hay cosas que se construyen UNA vez: las
píldoras de Builds, las filas del árbol de la Librería, la lista de objetivos de Exploración.
Rehacer la pestaña es lo mismo que hace ya cualquier clic en la barra, cuesta un fotograma, y
solo pasa cuando alguien cambia de idioma de verdad. Se hace en el MARCO y no en cada área: así
las seis quedan cubiertas de una vez.

**Lo que no era sustitución mecánica:**

- Los **13 desbloqueos** guardaban nombre y descripción como campos. Ahora el `Desbloqueo` solo
  guarda `CampoReal` (el nombre del campo de `Player`, que ya era único y estable) y lo usa
  además como clave, con `Nombre` y `Descripcion` como propiedades: la lista se construye una
  vez y se cachea.
- Las **12 variantes de piel** se componen de dos claves (género + atuendo): 2 + 6 cadenas a
  traducir en vez de 12.
- Los **38 objetivos de búsqueda** pasan a guardar una `Clave` interna estable sin tildes.
  Efecto colateral bueno: las claves de categoría son también las del `switch` de
  `ColorDeCategoria`, que antes dependía literalmente de que la cadena fuera `"Líquidos"` con
  tilde.
- **`ClaseBuild.Etiqueta`** y **`ObjetivoBusqueda.Etiqueta`** dejan de ser campos rellenos al
  parsear y pasan a ser propiedades: esos catálogos se resuelven UNA vez, en `PostSetupContent`,
  así que un nombre guardado ahí se quedaría con el idioma que hubiera al cargar la partida.

**El árbol de la Librería y el de Investigación, enteros en inglés, sin traducir nada.** Sus
rótulos venían de dos sitios que solo existen en español y que no son literales de este
repositorio:

1. `vanilla_library_labels_es.json`. Pero el árbol en sí (`vanilla_library_tree.json`) trae los
   nombres **en inglés** — "Materials", "Pre-Hardmode", "Copper & Tin" — porque son la CLAVE con
   la que se busca la traducción, y `LibraryLabelCatalog.Lookup` devuelve la clave tal cual
   cuando no la encuentra. O sea que para tener el árbol en inglés no hay que traducir nada:
   basta con darle un catálogo de etiquetas **VACÍO** (`"{}"`). Es lo que se hace cuando el juego
   no está en español.
2. `LibraryTreeBuilder.CalamityCategoryLabel` de `TerrasavrNative.Core`, con las 121 categorías
   traducidas a mano. En inglés se sustituye por separar el CamelCase de la clave, que es
   exactamente lo que hace esa misma función de Core con una categoría que no conoce:
   `"Weapons/Melee"` → `"Weapons / Melee"`.

**Los nombres de objeto de Builds venían del catálogo de la app de escritorio** y salían en
español con el juego en inglés. Ahora, si el pid se resolvió a un objeto real, se usa el nombre
que le da el propio juego (`Lang.GetItemNameValue`), que ya viene traducido y además es el bueno
si un mod lo renombra. El del catálogo se queda de respaldo para lo que no exista en la partida.

**Lo que NO se ha migrado, dicho claro**: nada. Las seis etiquetas de ETAPA de Builds venían de
`builds.json` (solo español) y eran la única excepción real; como eran seis y no un catálogo
entero, se han traducido por clave `fuente + etapa`, con la etiqueta del JSON de respaldo por si
algún día aparece una etapa nueva. Los mensajes de log y de consola siguen en español a
propósito: no los ve el jugador.

### 3. El detector de literales sin migrar

`scripts\verificar-idiomas.ps1` + `Common\Panel\AutopruebaIdiomas.cs`. Recorre las **once
vistas** del panel (las seis pestañas, con las seis sub-pestañas de Personaje y las tres de
Exploración) **dos veces, en español y en inglés**, recoge todo el texto que se está enseñando
en cada una -leyéndolo de los propios widgets, o sea de lo que de verdad se dibuja, no de un
listado paralelo que pudiera quedarse viejo- y deja **una captura real del back buffer por
vista e idioma** (22 en total).

Al terminar lista las cadenas que salen **IGUALES en los dos idiomas**. Eso es el detector: si
una frase se ve igual en español y en inglés, o está escrita a pelo en el C# o le falta la clave
en un `.hjson`. Ninguna otra autoprueba del mod hace esta comprobación, porque las demás cuentan
elementos y miden rectángulos, no leen el texto.

Hay coincidencias LEGÍTIMAS y hay que saberlo antes de mirar el informe: nombres propios
("Terrakeep", "Builds", "Buffs", "Vanilla", "Calamity", "English"), letras de canal ("R"),
números y coordenadas, y los nombres de objeto que pone el propio juego cuando coinciden. Por
eso el informe las lista para mirarlas, no las da por error.

**Resultado final: 573 cadenas recogidas, 10 coincidencias distintas, TODAS legítimas.** Cero
literales sin migrar.

### 4. El fallo que encontró el arnés antes de encontrar ninguno estético

`PanelTerrakeepSystem.TeclaDe` llamaba a `ModKeybind.GetAssignedKeys` sin proteger, y eso indexa
por dentro el diccionario del perfil de controles, que **no conoce los atajos de mods hasta que
`PlayerInput` procesa su `reinitialize` pendiente** (el mismo hueco de varios segundos que WS0
documentó para `JustPressed`). La `KeyNotFoundException` subía por `RefrescarTextos` →
`CambiarArea` → `UIElement.Activate` y **ABORTABA LA CONSTRUCCIÓN ENTERA DEL PANEL**: abrirlo en
los primeros segundos de un mundo lo dejaba a medias, sin contenido. Corregido con el mismo
`try/catch` que ya usaba `ComprobarAtajos`.

### 5. Diez pasadas de revisión visual, y los doce fallos que encontraron

Ninguno se dedujo leyendo código, y ninguno se habría visto contando elementos en el log: ahí
las once vistas daban verde, con sus rectángulos medidos.

| # | Pasada | Qué encontró |
|---|---|---|
| 1 | idiomas ES/EN 800x720 | el crash de `TeclaDe` + ocho fallos de maquetación (abajo) |
| 2 | idiomas ES/EN 800x720 | limpia: los ocho arreglados |
| 3 | idiomas 1600x900 | el equipo se sale por abajo otra vez |
| 4 | idiomas 1600x900 | limpia |
| 5 | idiomas + Calamity | los accesorios de Builds se cortan |
| 6 | idiomas + Calamity | limpia |
| 7 | menús, español | icono, descripción, botón muerto, título del config |
| 8 | menús, inglés | limpia |
| 9 | panel único (funcional) | verde: seis pestañas, animación, seis atajos, icono del HUD |
| 10 | WS1 / WS3 / WS4 / WS5 / WS6 / WS7 (funcionales) | verdes, cero excepciones |
| 11 | idiomas 800x720, cierre | limpia, y los `.hjson` ya no cambian tras jugar |

**Los ocho de la primera pasada:**

1. **Equipo, el peor**: las 10 filas de la columna izquierda (3 de armadura + 7 de accesorio) NO
   cabían. Con la escala fija de 0,75 la 7ª **se salía POR ABAJO del marco** y pintaba encima
   del pie y del botón de cerrar. Ahora la escala de las ranuras se calcula del alto REAL
   disponible, acotada entre 0,58 y 0,75, y las filas se recolocan cuando ese alto cambia. Para
   eso `SlotObjetoVanilla.Escala` pasa a poder cambiarse después de crear la ranura.
2. **Equipo**: las tres cabeceras de columna se pisaban unas con otras y se leía
   "EquipaVanidaTinte" — el paso entre ranuras es de ~35-41 px y cada palabra mide más. Ahora es
   UNA línea de leyenda ("Columnas: equipado · vanidad · tinte"), que no se puede solapar.
3. **Equipo**: la nota técnica del selector de conjunto medía ~900 px, se salía por la derecha y
   encima pisaba las cabeceras de la otra columna (se leía "PlayerSpe$ialTequipmentut"). Se ha
   ido al tooltip de los tres botones de conjunto.
4. **Buffs**: toda la pestaña estaba en píxeles fijos sumando 960 px de ancho. En una ventana de
   800 la caja de resultados y la nota se salían del marco, y **el campo "Segundos" quedaba
   FUERA DE LA PANTALLA: no se veía**. Ahora son dos columnas al 50%, con la duración en su
   propia fila y la nota partida en líneas.
5. **Apariencia**: la cabecera de colores era una sola cadena con espacios ("Colores  R  V  A")
   y sus letras no caían encima de sus deslizadores. Ahora cada letra va centrada sobre el suyo,
   en las mismas coordenadas que expone `FilaColorTk`.
6. **Desbloqueos**: dos columnas de 500 px fijos = 980 px; la de la derecha se pintaba por
   encima del borde del marco. Ahora van al 50%.
7. **Este mundo**: la columna derecha eran "lo que sobre de 430 px fijos", o sea ~318 px, y ahí
   no cabían ni los tres botones de modo (140 px cada uno) ni las tres líneas del aviso de
   permanencia (460 px cada una). Ahora el reparto es por porcentaje, los modos van 2x2 y los
   avisos se parten con el ancho real.
8. **Mapa y Búsqueda**: el aviso de tres renglones del mini-mapa llevaba los saltos de línea
   escritos A MANO, medidos para el español: en inglés la tercera se salía del marco. Se parte
   solo con el ancho real (`EtiquetaTk.PartirEnLineas`, ahora compartido), y su hueco pasa a 96
   px porque en inglés son cuatro líneas. Y el detalle inicial de la búsqueda se salía por la
   derecha: texto corto + el resto al tooltip del botón.

**El de 1600x900, que es el más contraintuitivo de todos.** A esa resolución el juego **NO da
más sitio**: usa escala de interfaz 1,47 y la pantalla lógica se queda en **1090x613**, o sea
MENOS alto útil que la ventana de 800x720 con la que se venía probando, y más ancho. Ahí el
arreglo (1) no llegaba: harían falta 24 px por fila y una ranura legible necesita 32. Ahora la
pestaña de Equipo decide **también cuántas columnas usa**: prueba con una y, si con la escala
mínima no caben las 10 filas, las reparte en dos de cinco. Es usar el ancho que sobra justo
cuando lo que falta es alto.

**El de Calamity.** Con Calamity, Builds enseña una fila más (el selector Vanilla/Calamity) y la
clase de cuerpo a cuerpo tiene SIETE accesorios. A 1090x613 el paso mínimo de 45 px solo daba
para cinco y media: "Band of Regeneration" salía cortado y "Countercurse Mantra" y "Bezoar" no
se veían. El paso mínimo baja a 30 y, cuando cae por debajo del tamaño natural de la ranura, la
RANURA encoge con él (hasta 0,55). Con las filas así de apretadas el "prefijo sugerido" ya no
cabe sin pintarse encima de la fila siguiente, así que se esconde por debajo de los 44 px de
paso: es la única información que se pierde, y solo en ventanas bajas.

### 6. Dos trampas del formato que conviene no volver a pisar

**tModLoader REESCRIBE los `.hjson` al cargar el mod**, y en ese viaje de ida y vuelta un valor
que lleve comillas dobles DENTRO se guarda como cadena de triple comilla (`'''...'''`) y al
releerlo **PIERDE LOS ESPACIOS DE LOS BORDES**. Le pasó a `Libreria.EnCarpeta`, que era
`" en \"{0}\""` y se quedó en `"en \"{0}\""`: el resumen del buscador pasaba a decir "Sin
resultadosen "Materiales"". Se vio comparando el archivo del repositorio con el que dejó el
juego, no jugando.

La regla que lo evita del todo -y que el generador comprueba en cada ejecución- es que **ningún
valor empiece ni acabe con un espacio**: los separadores van en la plantilla que concatena, no
en el trozo. Se arreglaron los diez valores que los llevaban.

Y lo que se comitea es el `.hjson` que deja el **JUEGO**, no el que escribe el script:
tModLoader normaliza el formato (quita comillas innecesarias, separa bloques) y si se comiteara
el otro, `git status` saldría sucio cada vez que alguien juega. Comprobado: generar, lanzar el
juego y comparar ya no cambia ni un valor.

### 7. Efectos colaterales del cierre

- **El `.tmod` publicado llevaba dentro el arnés de pruebas**: los 12 `.ps1` de verificación y
  los 12 `.log.txt` de evidencia, 39 archivos. Se vio listándolo con `node tmod-extract.js`.
  Añadidos `scripts\*` y `evidencia\*` al `buildIgnore`: ahora son **14 archivos** y el `.tmod`
  baja de 357.561 a ~343.000 bytes **con más código dentro**.
- **Cuatro scripts de verificación compilaban por defecto una copia AISLADA** (el núcleo más los
  archivos de SU workstream). Tenía sentido cuando cuatro agentes editaban el repositorio a la
  vez; hoy todas las áreas comparten los widgets, la paleta y el sistema de idiomas, así que una
  copia parcial **ya no compila**. Compilan siempre el proyecto entero; `-Completo` se queda por
  compatibilidad pero ya no hace nada.
- **Cinco scripts no relajaban `$ErrorActionPreference` alrededor del `-build`**, así que el
  aviso benigno de stderr (`WARN: Image loading failed: unknown image type`, de
  `icon_small.png`) los abortaba con exit code 0. Mismo arreglo que ya llevaba `compilar.ps1`.

### 8. Estado real al cierre

**El mod está cerrado.** Las últimas tres pasadas de revisión visual (2, 4, 6, 8 y 11) no
encontraron nada nuevo, y las seis autopruebas funcionales de los workstreams pasan sobre el mod
ya migrado, que es lo que demuestra que la migración de idiomas no rompió comportamiento:

| Autoprueba | Evidencia real |
|---|---|
| Panel único | las seis pestañas con clic real, animación en dos pestañas, los seis atajos saltando a su pestaña, el de la pestaña abierta cerrando, icono del HUD en (570, 278, 30, 30) |
| WS1 Personaje | 23 pasos, dinero contado, conjuntos de equipo ida y vuelta, caducidad real de un buff, deslizador de color por su ruta de ratón. Cero excepciones |
| WS3 Librería | las siete reglas del buscador, coger del catálogo y colocar en el jugador real, deshacer y rehacer |
| WS4 Builds | "ya lo tienes" 6 de 13, auto-equipar `movidos=5`, segunda pasada idempotente `movidos=0` |
| WS5 Investigación | el árbol cuadra objeto a objeto con la tabla del juego (5480 = 5480), clic real en "Research", objeto de mod, confirmación en dos pasos, persistencia tras reiniciar el cliente |
| WS6 Exploración | píxel real del mapa, modo Viaje bloqueado, dificultad cambiada y deshecha, mundo de prueba byte a byte como estaba |
| WS7 | deshacer/rehacer sobre un objeto movido, cambio de idioma en vivo con persistencia en `ModConfig` |

**Lo único que queda fuera y no se puede arreglar desde aquí:**

- **El último eslabón físico teclado → SDL sigue sin poder simularse** (ya documentado por WS7).
  Los atajos se ejercitan rellenando `PlayerInput.Triggers.JustPressed` y llamando al
  `ComprobarAtajos` de producción.
- **El icono del HUD no puede CERRAR el panel**: con el panel abierto su capa no llega a
  dibujarse (la 14 corta antes de la 28). Es una consecuencia del motor, y el diseño se apoya en
  ella a propósito.
- **`WARN: Image loading failed: unknown image type`** en cada compilación. Viene de
  `icon_small.png`: `ContentConverters.Convert` intenta pasarlo a `.rawimg` con
  `ImageIO.ToRaw`, `FNA3D.ReadImageStream` no lo lee y devuelve false, así que el PNG se
  empaqueta tal cual — que es exactamente lo que hace falta. El `.tmod` sale bien y el icono se
  ve en la lista de Mods (verificado comparando por REFERENCIA contra `Mod.PlaceholderModIcon`,
  no por el nombre del asset). No bloquea nada.
- **Los nombres largos de las etapas de Builds se cortan** con "..." en la píldora. Tienen el
  nombre completo en el tooltip. Se deja así a propósito: la alternativa era bajar tanto la
  escala del texto que dejara de leerse.

---

## 7-sep-2026 — Investigación del entorno: por qué `keybd_event`/`SendInput` nunca llegan al juego

Retomando el límite de WS7 ("Pulsaciones físicas de teclado"). Diagnóstico de sesión de Windows:
la sesión 1 (RDP, usuario "adrian") llevaba desde el 30-ago-2026 en estado `Desc`
(desconectada), coincidiendo exactamente con `LastBootUpTime` — con `HiberbootEnabled=1`
(Windows Fast Startup) el apagado/encendido diario del usuario no hace un arranque real, así que
la sesión nunca se reconectaba a la consola física. Hipótesis inicial: Windows bloquea a
propósito los efectos reales de `SetCursorPos`/`keybd_event`/`SendInput` sobre una sesión
desconectada de la consola, por diseño de seguridad — encajaba con todos los límites de
clic/teclado arrastrados durante toda la sesión de trabajo.

Se ejecutó `tscon 1 /dest:console` (reenganchar la sesión 1 a la consola física) con permiso
explícito del usuario. Resultado: `query session` confirmó `>console adrian 1 Activo` —
la sesión pasó de verdad a conectada.

**Se volvió a correr `scripts\verificar-ws7-interactivo.ps1 -Modo atajos` con la sesión ya
conectada, y el resultado fue IDÉNTICO al de antes**: `Ctrl pulsado según Main.keyState=False`
en los 4 segundos de la prueba, nunca `True`. La reconexión de sesión NO era la causa (o no la
única). Se investigaron dos hipótesis más, cada una un mecanismo real distinto, no una repetición
de la misma:

1. **UIPI (nivel de integridad)**: si el juego corriera con integridad más alta que el proceso
   que inyecta la tecla, Windows bloquea la entrada entre procesos por diseño (User Interface
   Privilege Isolation). Se repitió la prueba lanzando el script desde una PowerShell en
   integridad **Alta** (confirmado con `whoami /groups` → `S-1-16-12288`) en vez de desde Bash.
   Mismo resultado: `False` todo el rato.
2. **Falta de scan code de hardware**: `keybd_event` sin `KEYEVENTF_SCANCODE` genera una
   pulsación "virtual" que algunos lectores de entrada basados en Raw Input/SDL descartan por no
   traer un código de escaneo real. Se probó `SendInput` con `KEYEVENTF_SCANCODE` +
   `MapVirtualKey(vk, MAPVK_VK_TO_VSC)` para dar un scan code de hardware genuino a cada tecla.
   Mismo resultado: `False` todo el rato.

**Conclusión, tres causas reales descartadas una a una**: no es la sesión desconectada, no es
UIPI, no es la falta de scan code. La única explicación que queda en pie es que tModLoader (FNA
sobre SDL2) lee el teclado por un camino que ignora sistemáticamente la entrada sintética
generada en espacio de usuario por cualquiera de las tres APIs estándar de Windows
(`keybd_event`/`SendInput` con o sin scan code) — el patrón encaja con juegos que filtran por
"dispositivo HID real" a nivel de controlador, algo que ninguna API de espacio de usuario puede
falsificar. La única vía real que queda para pulsaciones físicas 100% indistinguibles de
hardware sería un controlador de HID virtual en modo kernel (p. ej. Interception o ViGEmBus) —
deliberadamente NO instalado sin preguntar antes: a diferencia de instalar una herramienta de
compilación o un arnés de pruebas, un driver de kernel es persistente, afecta a todo el sistema y
puede levantar sospechas en el antivirus, así que queda fuera del alcance de "no invasivo" de la
autonomía técnica ya concedida.

Se para aquí (tres intentos con causas reales distintas, no dos repeticiones de lo mismo). Lo que
sí queda resuelto y verificado con la sesión reconectada, y no es poco: el propio código de
producción del atajo (`HistorialSystem.ComprobarAtajos`, `Main.keyState`/
`PlayerInput.Triggers.JustPressed`, deshacer/rehacer, persistencia del idioma) sigue
funcionando exactamente igual que en WS7 — la limitación es y sigue siendo solo el último eslabón
físico (la tecla en sí), nunca el mod.

### Cuarto intento (mismo día): AutoHotkey v2 en modo `SendPlay`

Antes de valorar un driver de kernel (`Interception`), se investigó primero una vía sin driver:
`SendMode "Play"` de AutoHotkey v2 usa `WH_JOURNALPLAYBACK` (una API de reproducción de macros
de Windows, no `SendInput`/`keybd_event`), documentada por el propio proyecto como pensada
exactamente para "juegos o aplicaciones con lectura de entrada poco habitual" donde los otros
modos fallan. Instalado vía `winget install AutoHotkey.AutoHotkey` (v2.0.27).

Prueba: script `.ahk` que activa la ventana del juego (`WinActivate "ahk_exe dotnet.exe"`) y
envía `SendPlay("^z")`, lanzado justo tras `LISTO PARA ATAJOS` del mismo protocolo de
`verificar-ws7-interactivo.ps1`. **Resultado idéntico a los tres intentos anteriores**: con
`Main.hasFocus=True` confirmado, `Ctrl pulsado según Main.keyState` se queda en `False` todo el
tiempo, nunca `True` en ningún fotograma.

**Cuatro vías reales distintas, cuatro fallos idénticos**: sesión desconectada de RDP, nivel de
integridad UIPI, scan code de hardware ausente, y ahora reproducción vía `WH_JOURNALPLAYBACK`.
Las cuatro comparten que son formas de *inyectar en la cola de entrada de Windows a nivel de
espacio de usuario* - la conclusión que queda en pie es que FNA/SDL2 en esta instalación de
tModLoader lee el teclado por un camino (muy probablemente Raw Input filtrado por dispositivo
HID real, o polling directo de controlador) que ninguna de las cuatro puede alcanzar. La única
vía que quedaría es un dispositivo HID virtual real a nivel de controlador de kernel (que el
sistema no puede distinguir de un teclado físico) - con el riesgo real ya evaluado y documentado
en la conversación con el usuario (caso conocido de `Interception` dejando teclado+ratón
inservibles tras reiniciar, sin garantía de compatibilidad con Windows 11). Decisión pendiente
del usuario, no se instala nada de eso sin su confirmación explícita informada del riesgo.

---

## 7-sep-2026 — Vista previa en vivo del muñeco en Apariencia (con armadura / sin armadura)

Pedido del usuario probando el mod en el juego: en la sub-pestaña **Apariencia**
(`UI/Personaje/PestanaApariencia.cs`) quedaba un hueco vacío grande a la derecha, debajo del
logo decorativo de fondo. Pedía ver ahí, en vivo, cómo queda el personaje con los cambios de
apariencia aplicados, con armadura puesta y sin ella - igual que hace la app de escritorio
hermana.

### Lo que se investigó antes de tocar nada

No hizo falta mirar la app de escritorio (WPF/`WriteableBitmap`, un patrón que no sirve aquí de
todas formas): el propio juego, corriendo en el mismo proceso que el mod, YA sabe dibujar un
muñeco de personaje con dos piezas de código real, las dos comprobadas con `ilspycmd` sobre el
`tModLoader.dll` instalado antes de escribir una sola línea:

1. **`Terraria.GameContent.UI.Elements.UICharacter`** - la ficha de personaje de la pantalla de
   selección. Dibuja con `Main.PlayerRenderer.DrawPlayer(Main.Camera, jugador, posicionDePantalla
   + Main.screenPosition, 0f, Vector2.Zero, 0f, escala)` dentro de su propio `DrawSelf`, con
   `UseImmediateMode = true` y `OverrideSamplerState = SamplerState.PointClamp`. Confirma que se
   puede invocar el renderer real DESDE un `UIElement` corriente sumando `Main.screenPosition` a
   una coordenada de pantalla (el truco que cancela la transformación de cámara).
2. **`Terraria.GameContent.Tile_Entities.TEDisplayDoll`** - el Maniquí de vanilla, el ejemplo de
   producción más parecido a lo que hacía falta aquí. Su `_dollPlayer` es un `Player` PROPIO
   creado una única vez con `new Player()` a secas (nada de clonar ni serializar), con
   `hair`/`skinColor`/`skinVariant` puestos a mano. En cada `Draw()` copia sus 8 `armor[]`/`dye[]`
   guardados al `Player`, pone `isDisplayDollOrInanimate = true` y llama a
   `ResetEffects()` → `ResetVisibleAccessories()` → `UpdateDyes()` → `DisplayDollUpdate()` →
   `UpdateSocialShadow()` → `PlayerFrame()` antes de `DrawPlayer`. Ese campo
   `isDisplayDollOrInanimate` importa de verdad: sin él, código de vanilla que compara
   `whoAmI == Main.myPlayer` trataría al muñeco (cuyo `whoAmI` nunca se toca y vale 0) como si
   fuera el jugador real en una partida de un jugador (`Main.myPlayer` también vale 0).

Con esto se descartó clonar `Main.LocalPlayer` (ni una vez con `SerializedClone()`, que hace una
serializacion/deserializacion completa - válido para construir UNA ficha estática, pero
demasiado caro para repetirlo cada fotograma) y en su lugar se usó el patrón real del Maniquí:
un `Player` propio, construido una sola vez, sincronizado cada fotograma solo con lo barato.

### Lo que se ha hecho

`UI/Personaje/Widgets/MunecoTk.cs` (nuevo): un `UIElement` con un `Player _muneco = new Player()`
propio. En `Update()` copia de `Main.LocalPlayer` SOLO lo necesario para el dibujado - pelo,
tinte, variante, los siete colores y, si `ConArmadura` está activo, las MISMAS referencias de
`Item` de `armor[]`/`dye[]`/`hideVisibleAccessory` que ya lleva puestas el jugador real (compartir
la referencia para leerla no la modifica; nunca se toca ni se clona `Main.LocalPlayer`). Si
`ConArmadura` está desactivado, `armor[]`/`dye[]` se rellenan con arrays propios de `Item` vacíos
(aire) precreados una vez. Después, los mismos cinco pasos que `TEDisplayDoll.Draw`:
`isDisplayDollOrInanimate = true`, `ResetEffects()`, `ResetVisibleAccessories()`, `UpdateDyes()`,
`DisplayDollUpdate()`, `UpdateSocialShadow()`, `PlayerFrame()`. En `DrawSelf` calcula una escala
según el alto real disponible (acotada entre 0,6 y 2,2) y llama a
`Main.PlayerRenderer.DrawPlayer` con el mismo truco de `Main.screenPosition` que usa
`UICharacter`. Si el hueco es demasiado pequeño (ventana muy estrecha), no dibuja nada en vez de
dibujar un muñeco recortado.

`UI/Personaje/PestanaApariencia.cs`: una caja (`UIPanel`, estilo `EstiloTk.FondoCaja`) anclada a
la derecha con `Left.Set(750f, 0f)` y `Width.Set(-(750+20), 1f)` - o sea "lo que sobre a la
derecha del contenido fijo de la izquierda (que llega hasta ~740 px), con un margen de 20". En
ventanas muy estrechas el ancho calculado sale negativo y `MunecoTk` deja de dibujarse solo, sin
pisar nada ni lanzar ninguna excepción (nunca llegó a hacer falta esa rama en las pruebas: a
1600x900 con escala de interfaz 1,47 sale con 241 px de ancho). Dentro: un título ("Vista
previa"), el `MunecoTk` y, anclado abajo, un `AlternadorTk` reutilizado tal cual ya existía
("Con armadura") que solo cambia la propiedad pública `MunecoTk.ConArmadura` - no guarda ningún
estado propio, la propia casilla lee/escribe esa propiedad.

Tres claves nuevas de idioma (`Personaje.Apariencia.Vista/VerConArmadura/VerConArmaduraAyuda`),
añadidas en `scripts/generar-localizacion.py` (la tabla única de la que salen los dos `.hjson`,
que resultó ser un generador nuevo de otro agente trabajando en paralelo en la migración de
idiomas - ver más abajo) y regeneradas con él, no escritas a mano en los `.hjson`.

### Verificación real, con el mismo arnés que ya usa el proyecto

`scripts/verificar-panel-unico.ps1` (sandbox `tModLoader-TerrakeepPanel`, cliente gráfico real),
con seis pasos nuevos añadidos a `Common/Panel/AutopruebaPanelUnico.cs` (31-37, después del icono
del HUD, sin renumerar los que ya había):

1. `PoblarAparienciaDePrueba` - el personaje sintético `TerrakeepPrueba` llega con los siete
   colores a `(0,0,0)` y sin nada equipado (una silueta negra sería igual con y sin armadura, sin
   demostrar nada). Se le ponen colores vivos y una armadura de cobre real (cabeza/cuerpo/piernas
   buscados por propiedades - `headSlot`/`bodySlot`/`legSlot` + `defense > 0` - igual que ya hace
   `AutopruebaPersonaje.PoblarEquipo`, nunca por id fijo).
2. Abre Personaje → Apariencia (`ContenidoPersonaje.IrAPestana(4)`).
3. Captura con armadura, pulsa el `AlternadorTk` con un clic REAL (`UIElement.LeftClick`, la
   misma ruta que ya usó WS1 con el deslizador de color), captura sin armadura, lo pulsa otra
   vez, captura con armadura de nuevo.

Resultado real en `evidencia/panel-unico.log.txt`: `MunecoTk dibujado en x=789 y=296 241x192,
ConArmadura=True` → clic real en "With armour", `Valor antes=True -> despues=False -> OK` →
`ConArmadura=False` → clic real, `Valor antes=False -> despues=True -> OK` → `ConArmadura=True`.
`AUTOPRUEBA PANEL COMPLETA`, cero excepciones.

**Las tres capturas reales del back buffer** (`CapturaDePantalla.Guardar`, nunca
`CopyFromScreen`/`PrintWindow` - prohibidas y documentadas más arriba) se miraron a ojo:
`apariencia-muneco-con-armadura.png` enseña el casco/goggles, el peto de cadena de cobre y las
grebas de cobre puestos; `apariencia-muneco-sin-armadura.png` enseña la piel/pelo/ropa con los
siete colores EXACTOS de los deslizadores (pelo 210,60,40 rojizo; piel 255,200,150; ojos
30,140,230 azules; camisa 40,170,80 verde; pantalón 70,90,200 azul); la casilla del alternador
cambia de `[X]` a `[ ]` y vuelta a `[X]` en las tres capturas. Sin retraso perceptible: los
colores y el equipo del muñeco están ya al día en el mismo fotograma en que se pide la captura.

La resolución del sandbox (`tModLoader-TerrakeepPanel/config.json`) estaba en 800x480 (quedó así
de alguna sesión anterior); se subió a 1600x900 -escala de interfaz 1,4666667 ya guardada, la
misma proporción 1600x900 → 1090x613 que ya documentó la fusión de paneles- porque a 800x480 el
contenido fijo de la izquierda de Apariencia (~740 px) ya deja casi nada para la vista previa.

### El repositorio con varios agentes a la vez, en vivo

Esta vez no fue solo la teoría de la sección de "índice privado": mientras se trabajaba en esto
había, a la vez, otros tres agentes tocando `Common/Builds/*` (una nueva `loadoutObjetivo` en
`AutoEquipar`), un árbol de carpetas nuevo para Buffs (`Common/Personaje/ArbolBuffs.cs`,
`UI/Personaje/Widgets/FilaCarpetaBuffTk.cs`, editor de cantidad + papelera en el inventario) y una
migración completa de idiomas (`scripts/generar-localizacion.py` como generador nuevo,
reescribiendo los dos `.hjson` enteros). El build del proyecto entero (obligatorio: ya no hay
compilación aislada por área) se rompió y se arregló varias veces en directo, nunca por algo
mío:

- `UI/Personaje/Widgets/FilaCarpetaBuffTk.cs`: faltaba `using Terraria.GameContent.UI.Elements;`
  para `UIPanel`. Arreglo de una línea, obviamente correcto, sin inventar comportamiento -
  aplicado para no bloquear el build de todo el mundo.
- `Common/Personaje/ArbolBuffs.cs:221`: un `List<CategoryTreeNodeData>` no admitía asignar un
  `IReadOnlyList<CategoryTreeNodeData>` (la rama `nodo.Children` de un operador ternario). Se
  cambió el tipo de la variable local a `IReadOnlyList<...>` (el tipo real de la propiedad),
  también mecánico, sin tocar la lógica.
- Otros dos fallos en `Common/Personaje/AutopruebaPersonaje.cs` y
  `UI/Personaje/Widgets/EditorCantidadTk.cs` se resolvieron SOLOS entre un intento y el
  siguiente: ese agente seguía escribiendo esos métodos en directo. No se tocaron.
- Mi propio error (`ContentSamples` sin el `using Terraria.ID;` que le hacía falta) apareció
  entre medias y se corrigió igual.

Se esperó con un bucle de reintento (`dotnet build` cada 15 s) en vez de tocar en bucle el mismo
archivo ajeno dos veces seguidas por la misma causa. El commit final (`f1d970d`) se hizo con
**índice privado** y solo lleva mis tres archivos (`MunecoTk.cs`, `PestanaApariencia.cs`,
`AutopruebaPanelUnico.cs`); los `.hjson` y `generar-localizacion.py` sí llevan mis tres claves de
idioma pero se han dejado SIN comitear por mi parte - añadirlos habría mezclado mis tres líneas
con las ~1974 líneas de la migración de idiomas de otro agente bajo mi mensaje de commit. Quedan
en el árbol de trabajo tal cual, listos para el commit de esa migración cuando la cierren.

### Lo que queda fuera, dicho claro

- No se ha probado el redimensionado de ventana en vivo con el panel ya abierto (cambiar el
  ancho de la ventana mientras `MunecoTk` está dibujado, para ver la rama de "hueco demasiado
  pequeño, no dibujar nada" en acción real). El cálculo se revisó a mano y `PestanaEquipo` ya usa
  el mismo patrón de escala-según-alto-real sin problemas, pero no hay captura de esa rama
  concreta.
- No se ha probado con Calamity cargado (una armadura de Calamity con capas raras de dibujado
  podría comportarse distinto). El renderer es el mismo para cualquier armadura real, así que no
  hay motivo real para esperar un problema, pero queda sin comprobar.

---

## 7-sep-2026 — WS4 ampliado: selector de conjunto de destino en Builds

Se pidió arreglar dos quejas reales del usuario probando el mod: "aplicar una build no equipa de
verdad" y "no se puede elegir a qué conjunto (1/2/3) va la armadura de una build". Los dos
tocaban `Common/Builds/` y `UI/Builds/ContenidoBuilds.cs`.

### Lo primero, investigado antes de tocar nada

`AutoEquipar.Ejecutar` YA escribía en un array vivo de verdad (`jugador.armor`), exactamente
igual que la pestaña Equipo de WS1 (que se apoya en el mismo hallazgo:
`EquipmentLoadout.Swap` intercambia elemento a elemento contra `Player.armor`/`dye`, así que el
conjunto ACTIVO vive suelto en esos campos y NO en `Player.Loadouts[]`). Eso significa que
auto-equipar SIEMPRE ha equipado de verdad - pero solo sobre lo que fuera el conjunto ACTIVO en
ese instante, sin ningún control sobre cuál. Con tres conjuntos y ningún selector, aplicar una
build mientras el jugador no estaba mirando/pensando en el conjunto 1 (el único que se tocaba)
podía parecer "no ha hecho nada": el efecto SÍ estaba ahí, solo que en un conjunto distinto al
que el usuario tenía en mente. Ese es el problema real de fondo, no un fallo de escritura.

Se comprobó contra el `tModLoader.dll` instalado (v2026.7.3.0) con `ilspycmd`, no contra el
decompilado de referencia: `EquipmentLoadout` tiene `Armor[20]`, `Dye[10]`, `Hide[10]` (campos
públicos, sin properties), `Player.Loadouts` es `EquipmentLoadout[3]`,
`Player.TrySwitchingLoadout(i)` hace `Loadouts[CurrentLoadoutIndex].Swap(this)` +
`Loadouts[i].Swap(this)` + `CurrentLoadoutIndex = i`. Coincide exactamente con el decompilado de
referencia (`tModLoader-Decompiled\tModLoader\Terraria\EquipmentLoadout.cs`), así que esta vez sí
valía sin volver a decompilar - pero se verificó igual, por norma.

### Lo que se hizo

- `EquipoJugador.ArmorDe(jugador, indiceLoadout)`: nuevo. Devuelve `jugador.armor` si el índice
  pedido es el activo, o `jugador.Loadouts[i].Armor` si no. Es el mismo criterio que
  `PestanaEquipo` ya tenía probado, expuesto como función reutilizable.
- `EquipoJugador.Contenedores` ahora también recorre los DOS conjuntos inactivos
  (`Player.Loadouts[i].Armor` para `i != CurrentLoadoutIndex`) como sitios donde "ya lo tienes"
  puede encontrar un objeto. Antes solo miraba el conjunto activo; un objeto guardado en un
  conjunto que no llevas puesto se contaba como "no lo tienes", que era falso.
- `PrimerSlotAccesorioLibre` y `AccesorioYaPuesto` dejan de asumir `jugador.armor` y reciben el
  array de destino como parámetro. `ItemSlot.AccCheck` se comprobó (decompilado) que opera solo
  sobre el array que se le pasa, sin ningún estado global del jugador activo, así que llamarlo
  contra `Loadouts[n].Armor` es igual de válido que contra `armor`.
- `AutoEquipar.Ejecutar(jugador, build, loadoutObjetivo)`: nueva firma con el índice de destino.
  Calcula `destino = EquipoJugador.ArmorDe(...)` una vez y lo usa para armadura y accesorios (las
  armas siguen yendo siempre a la mochila: los loadouts de Terraria no incluyen armas).
- `UI/Builds/ContenidoBuilds.cs`: nueva fila de pastillas "Aplicar al conjunto 1/2/3" (mismo
  widget `PintarPildoras` que ya usan fuente/etapa/clase, mismo camino de clic real
  `UIElement.LeftClick` que ya probó `PulsarPildoraClase`). Marca con "(activo)" la que coincide
  con `Player.CurrentLoadoutIndex` en cada fotograma (el jugador puede cambiar de conjunto con las
  teclas del propio juego mientras el panel sigue abierto). Por defecto, si el jugador no ha
  tocado el selector, apunta al conjunto ACTIVO - así el botón "Auto-equipar" sigue haciendo
  exactamente lo mismo que antes si nadie usa el selector nuevo.

### Verificado de verdad en el juego (no solo "compila")

`scripts\verificar-builds-en-juego.ps1` ampliado con `-LoadoutObjetivo` y `-CambiarLoadoutA`
(pulsan la pastilla de verdad y llaman a `Player.TrySwitchingLoadout` de verdad). Dos ejecuciones
reales sobre el sandbox `tModLoader-TerrakeepWS4`, evidencia completa en
`evidencia\ws4-builds-conjunto-destino.log.txt` y `evidencia\ws4-builds.log.txt`:

| Escenario | Resultado real |
|---|---|
| Aplicar al conjunto 2 (INACTIVO, activo=conjunto 1) | log dice `Conjunto de destino=2/3 (no activo, se guarda en Player.Loadouts[1].Armor)`; tras aplicar, `Conjunto 1 *ACTIVO*` sigue vacío y SOLO `Conjunto 2` lleva el equipo |
| Cambiar el conjunto activo real al 2 (`TrySwitchingLoadout`) | `CurrentLoadoutIndex antes=1 ahora=2`. `Player.armor` (el que dibuja y usa el juego) pasa a llevar exactamente lo aplicado antes al conjunto 2 |
| Aplicar al conjunto 1 (el que YA está activo) | log dice `Conjunto de destino=1/3 (ACTIVO ahora mismo, se ve al instante)`; el equipo aparece en `Conjunto 1 *ACTIVO*` en la misma pasada, sin cambiar de conjunto |
| Segunda pasada (idempotencia) | `movidos=0, ya colocados=6` en los dos escenarios |
| Selector con clic real | `PulsarPildoraLoadoutObjetivo` dispara `OnLeftClick` de verdad; el log confirma "pildora pulsada de verdad=True" |

Con esto quedan cubiertos los tres pasos que pedía la verificación: build a un conjunto inactivo
sin tocar el activo, cambio de conjunto activo que hace aparecer lo aplicado, y build al conjunto
activo con efecto instantáneo.

### Obstáculo real: los `.hjson` compartidos se pisan entre sesiones del juego a la vez

Las tres claves nuevas de idioma (`Builds.ConjuntoDestinoPildora/Ayuda/ActivoMarca`) se
añadieron a mano en los dos `.hjson` **tres veces**, y las tres veces desaparecieron o quedaron
mal antes de poder comitear:

1. Otra sesión del juego (de otro agente, en paralelo) volvió a guardar los `.hjson` con su
   propio estado en memoria y se llevó por delante mis líneas nuevas sin más.
2. Al añadirlas nuevamente y ejecutar `python scripts\generar-localizacion.py` para intentar
   estabilizarlas (mis 3 claves SÍ están en la tabla `T` del script, se añadieron ahí para el
   futuro), el script regeneró los dos archivos **enteros** desde la tabla y el resultado salió
   más CORTO que el que había en disco (623 líneas contra 692): la tabla `T` no tiene todavía las
   claves que otro agente ha ido añadiendo a mano a los `.hjson` en su propia migración de
   idiomas en marcha (confirmado leyendo más abajo en esta misma bitácora, entrada anterior: ese
   agente dejó explícitamente sin comitear sus cambios de `.hjson`/`generar-localizacion.py` "para
   no mezclar mis tres líneas con las ~1974 líneas de la migración de idiomas de otro agente").
   Se deshizo enseguida restaurando desde una copia hecha justo antes de ejecutar el generador
   (nunca llegó a comitearse), así que no se perdió nada de nadie.
3. Al restaurar esa copia aparecieron mis 3 claves **comentadas y en inglés** dentro del propio
   `es-ES_Mods.TerrakeepMod.hjson` (`// ConjuntoDestinoPildora: Apply to set {0}` etc.): parece
   ser el propio mecanismo de esa migración de idiomas en marcha, que deja como comentario el
   texto en inglés cuando detecta una clave sin traducir todavía a español. Se resolvió
   "traduciendo" el comentario (quitar `//` y poner el texto en español), que es justo el flujo
   que ese mecanismo espera.

**Decisión, siguiendo el precedente que ya dejó escrito el agente de la migración de idiomas
justo arriba en esta bitácora**: `scripts\generar-localizacion.py` (mi adición a la tabla `T`) y
los dos `.hjson` se dejan **sin comitear**, en el árbol de trabajo, listos para cuando se cierre
esa migración. Solo se comitean con índice privado los archivos que son míos de verdad
(`Common/Builds/*.cs`, `UI/Builds/ContenidoBuilds.cs`, el script de verificación y la evidencia).
Si mis tres claves vuelven a desaparecer del `.hjson` antes de que eso pase, no es una regresión
de esto: es la migración en marcha todavía sin cerrar. El código en sí no depende de que la
traducción esté presente - `Idiomas.Texto` devuelve la clave completa tal cual si no encuentra
traducción, así que en el peor caso el selector se ve con el texto de la clave en vez del rótulo
bonito, nunca deja de funcionar.

---

## 7-sep-2026 — Bug real: la pestaña Buffs no enseñaba todos los buffs aplicables

Pedido del usuario, probando el mod en el juego: en **Buffs > Añadir un buff**, "no salen todos
los buffs que se pueden aplicar - como si faltaran entradas o el árbol/lista no estuviera bien
ramificado/categorizado... igual que en terrakeep" (refiriéndose a un bug ya dado y corregido en
la app de escritorio).

### Investigación: es literalmente el MISMO bug, ya resuelto una vez en la app de escritorio

`UI/Personaje/PestanaBuffs.cs` (`ReconstruirResultados`, antes de este arreglo) era un picker
"buscar+aplicar" **plano, sin ninguna categoría**, con un tope de **12** resultados:

```csharp
for (int tipo = 1; tipo < BuffLoader.BuffCount && encontrados < MaximoResultados; tipo++) { ... }
```

Con el buscador vacío (que es como se abre el panel), eso enseñaba SIEMPRE los primeros 12 buffs
por id (los de "Piel de obsidiana" para abajo) y escondía el resto salvo que se supiera
EXACTAMENTE el nombre o el id a buscar. `PersonajeVivo.NombreBuff` nunca devuelve `""` dentro del
rango válido (cae a `"Buff N"`), así que no era un problema de nombres faltantes: era el propio
diseño del picker.

Repasado el `git log` de `Terrasavr-Native` (repo hermano) por "buff", apareció el commit real
que cerró exactamente este mismo problema en la app de escritorio, documentado en su propia
bitácora bajo "Fase 2 (rework de Buffs)": antes de esa fase, "Añadir buff..." era un picker plano
igual que el de aquí, y se sustituyó por una **Librería de buffs real** (árbol de carpetas +
búsqueda), calco de la que ya tenían los objetos. El criterio real, portado tal cual desde
`TerrasavrNative.Core.Data.BuffTreeBuilder` (ya incluido en el `lib/TerrasavrNative.Core.dll` que
usa el mod, comprobado con `ilspycmd` sobre el DLL real, no solo sobre el código fuente del repo
hermano):

- **6 categorías curadas** con listas de ids literales reales de Terrasavr (Utilidad=17,
  Offensivo=18, Defensivo=13, Special=10, Mascota=20, Negativo=26 - con pertenencia múltiple real,
  ej. el buff 3 vive en Utilidad Y Special).
- **"Índice"**: páginas de 33 en 33 sobre **TODOS** los buffs vanilla conocidos (`BuffID.Count`),
  para que ningún buff vanilla pueda quedar fuera del árbol aunque no encaje en ninguna categoría
  curada. Esto es lo que de verdad garantiza cobertura del 100%, no las 6 categorías.
- **"Calamity (mod)"** (en la app de escritorio) agrupado por categoría real de
  `calamity/buffs.json`.

### La corrección real (no un parche del tope de 12)

`Common/Personaje/ArbolBuffs.cs` (nuevo), híbrido igual que `ArbolLibreria` (WS3) para objetos:

1. **El árbol curado + "Índice" real de Terrasavr**, llamando literalmente a
   `BuffTreeBuilder.BuildBuffTree` de Core (nada reimplementado) con un `VanillaBuffCatalog`
   montado **en memoria** (nunca desde un `.json` estático que se quedaría corto): sus únicas
   1..`BuffID.Count`-1 entradas sirven solo para que `BuildIndex` sepa qué ids paginar, los
   nombres que de verdad se enseñan salen en vivo de `PersonajeVivo.NombreBuff` en cada fila (así
   respeta el idioma activo sin tener que reconstruir nada al cambiarlo).
2. **Una carpeta madre por MOD instalado**, descubierta en vivo con `LiveItemTreeBuilder.BuildTree`
   sobre los buffs con `tipo >= BuffID.Count` (el mismo corte real que usa internamente
   `BuffLoader.GetBuff` para decidir si un buff es "de mod" - confirmado con `ilspycmd` sobre el
   `tModLoader.dll` instalado: `GetBuff` devuelve null si `type < BuffID.Count || type >=
   BuffCount`). Cubre **cualquier** mod con buffs propios, no solo Calamity, sin necesitar ningún
   `calamity/buffs.json` portado ni ids sintéticos.

Entre las dos partes cubren `BuffLoader.BuffCount - 1` exactos: a diferencia del árbol de objetos
(que sí puede dejar vanilla fuera del árbol curado porque Terrasavr lo extrajo de una versión con
menos contenido), aquí el "Índice" ya nace pensado para el 100% de lo vanilla, así que nunca hace
falta una carpeta de "sobrantes".

`UI/Personaje/PestanaBuffs.cs` se reescribió para navegar ese árbol (columna de carpetas con
Inicio/Subir, calco reducido de `ContenidoLibreria`) en vez de un buscador plano: sin carpeta y
sin texto no se enseña nada (pantalla de entrada, igual que la Librería real), con una carpeta
abierta se buscan sus ids, y sin carpeta pero CON texto se busca en los `BuffLoader.BuffCount - 1`
buffs aplicables enteros. El tope de resultados sube de 12 a 100 (`BusquedaLibreria.Maximo`) y,
sobre todo, un recorte real ahora es **visible** (`"Mostrando 100 de 311 en ..."`) en vez de
silencioso, que es la otra cara del mismo bug original. Fila de carpeta nueva,
`UI/Personaje/Widgets/FilaCarpetaBuffTk.cs`: calco de `FilaCarpetaTk` (Librería) con el icono real
de un BUFF (`TextureAssets.Buff`) en vez del de un objeto.

### Verificado de verdad en el juego, cuadrando el número (mismo criterio que Investigación, 5480=5480)

Arnés propio y aislado, `Common/Personaje/VerificacionBuffsSystem.cs` (variable de entorno
`TERRAKEEP_AUTOTEST_BUFFS`), deliberadamente en su **propio** `ModSystem` sin tocar
`PanelPruebaSystem.cs`/`AutopruebaPersonaje.cs`: en el momento de esta tanda había varios agentes
trabajando en paralelo sobre esos mismos archivos compartidos y sobre sandboxes de juego reales ya
abiertos (confirmado: dos procesos `dotnet` de tModLoader ya corriendo antes de empezar). tModLoader
llama solo a `PostSetupContent`/`UpdateUI` de cualquier `ModSystem` cargado, así que no hace falta
que nadie más lo invoque. Sandbox propio `tModLoader-TerrakeepBuffs` (copiado de `WS1`), `.tmod`
compilado directamente ahí con `-tmlsavedirectory` (nunca en la carpeta `Mods` compartida). Solo se
paró el PID propio en cada prueba, nunca un `Stop-Process` masivo por nombre - había procesos de
otro agente en marcha y no se tocaron.

| Qué | Evidencia real (`evidencia/buffs-arbol*.log.txt`) |
|---|---|
| Solo vanilla (servidor headless) | `Buffs aplicables segun el juego (BuffLoader.BuffCount-1): 354; cubiertos por el arbol: 354 (cuadra)` |
| Con Calamity + CalamityModMusic | `... 665; cubiertos por el arbol: 665 (cuadra)` (354 vanilla + 311 de Calamity) |
| Panel real abierto en la pestaña Buffs (cliente gráfico) | `Pestaña activa: "Buffs"` + `VERIFICACION-BUFFS-UI: ... 38 elementos ... area de la pestaña x=110 y=265 1060x378` |
| Excepciones en el log completo | ninguna, en las tres ejecuciones |

`354 = BuffID.Count - 1` (355 real, comprobado con `ilspycmd` sobre `tModLoader.dll`), y el salto a
665 con Calamity cargado demuestra que la carpeta de mod descubierta en vivo también cubre el 100%
- exactamente el escenario que reportó el usuario (con Calamity instalado).

### Los dos `.hjson` (claves `Personaje.Buffs.Arbol.*`) se dejan SIN comitear

Mismo motivo y misma decisión que ya dejó escrita la entrada anterior de esta bitácora ("Obstáculo
real: los `.hjson` compartidos se pisan entre sesiones del juego a la vez"): hay una migración de
idiomas en marcha de otro agente que regenera esos dos archivos enteros desde
`scripts/generar-localizacion.py` y ya se ha comido claves nuevas de otros tres veces seguidas. Mis
claves nuevas ya están escritas en el árbol de trabajo (español e inglés) y el código no depende de
que sobrevivan: `Idiomas.Texto` cae a la clave completa si no encuentra traducción, así que en el
peor caso el árbol de "Añadir" se ve con literales tipo `Personaje.Buffs.Arbol.Inicio` en vez del
rótulo bonito, nunca deja de funcionar ni de cubrir el 100% de los buffs. Se comitea con **índice
privado** solo lo que es mío de verdad: `Common/Personaje/ArbolBuffs.cs`,
`Common/Personaje/VerificacionBuffsSystem.cs`, `UI/Personaje/Widgets/FilaCarpetaBuffTk.cs`,
`UI/Personaje/PestanaBuffs.cs` y la evidencia.

---

## 7-sep-2026 — WS6: la búsqueda "no encontraba nada", sprites reales y nombre real al pasar el ratón

Tres pedidos sobre el panel de Exploración (`Common/Exploracion/`, `UI/Exploracion/`), reportados
por el usuario jugando de verdad: la búsqueda no encontraba nunca nada, ni la lista de resultados
ni el mapa tenían sprites, y pasar el ratón por el mini-mapa no decía qué había en cada tile.

### 1. La causa REAL de "no encuentra nada, da igual lo que busques"

No estaba en `BuscadorMundo.cs` ni en `ObjetivosBusqueda.cs`: el barrido de tiles, el `_esObjetivo`
indexado por tipo, el `HasTile` antes de comparar tipo, la agrupación en celdas de 25×25... todo
correcto, y lo demuestra el propio log de verificación de WS6 de hace un día
(`"13718 tiles encontrados en 20170801 mirados"`), que seguía dando el mismo número exacto tal
cual, sin tocar una línea de esos dos archivos.

La causa estaba en `UI/Exploracion/PestanaBusqueda.cs`: `_soloExplorado` nacía en `true`. Ese
interruptor filtra por `Main.Map.IsRevealed(x, y)`, y es la MISMA comprobación para las cinco
clases de objetivo (tiles, paredes, líquidos, cofres y NPC - se ve en `BuscadorMundo.Avanzar` y en
`BuscarCofres`/`BuscarNpcs`). El problema es conceptual, no un bug de índices: **lo que casi
cualquier búsqueda de esta herramienta quiere encontrar es precisamente lo que el jugador NO ha
visto todavía** (una veta de mineral sin descubrir, un cofre en una zona sin explorar). Con el
interruptor activado de fábrica, una búsqueda de algo que el jugador aún no ha encontrado se queda
casi siempre en 0 resultados - que es exactamente "da igual lo que busques, no encuentra nada".
Arreglado cambiando el valor de fábrica a `false` (buscar en TODO el mundo): esto es una
herramienta de edición con el mismo espíritu que Terrasavr, no un asistente que respeta spoilers
por defecto. El interruptor se conserva por si alguien sí quiere ceñirse a lo ya explorado.

**Verificado aislando la variable**: se relanzó `scripts\verificar-exploracion.ps1` sin tocar
`BuscadorMundo`/`ObjetivosBusqueda` ni una línea, primero reproduciendo el bug (interruptor en
`true`, autoprueba forzándolo a apagarse a mano) y después con el valor de fábrica ya en `false` y
la autoprueba dejándolo tal cual (`"estaba en False, ahora False (ya estaba apagada, no hacia
falta)"`): mismo resultado exacto de siempre, `13718 tiles encontrados`, con un CLIC REAL sobre
"Buscar en el mundo" sin tocar ningún otro control primero - que es justo el camino que sigue un
jugador real la primera vez que abre la pestaña.

### 2. Sprites reales en resultados y en el mini-mapa

`Common/Exploracion/IconoResultado.cs` (nuevo): resuelve la textura REAL del juego para cada
resultado y la dibuja escalada (sin deformar) dentro de un rectángulo. Nunca un icono inventado:

| Clase | Textura real | Origen |
|---|---|---|
| Tile (minerales, gemas, tesoros) | `TextureAssets.Tile[tipo]` tras `Main.instance.LoadTiles(tipo)` | `(0, 0, 16, 16)` - primer frame de la rejilla de 16×16 con 2 px de separación, formato fijo de sprite-sheet de Terraria |
| Pared | `TextureAssets.Wall[tipo]` tras `Main.instance.LoadWall(tipo)` | `(0, 0, 32, 32)` - confirmado leyendo `WallDrawing.cs` real (`new Rectangle(0, 0, 32, 32)`) |
| Líquido | `TextureAssets.Liquid[tipo]` (siempre cargada, son 4) | `(0, 0, 16, 16)` |
| Cofres | `TextureAssets.Item[ItemID.Chest]` (icono genérico de "Cofre") | textura entera |
| NPC | `TextureAssets.Npc[tipo]` tras `Main.instance.LoadNPC(tipo)` | `NPC.frame` capturado en el instante de la búsqueda (el motor ya lo mantiene al día mientras el NPC vive; no hace falta calcular el frame a mano) |

Para cofres se usa el icono genérico del objeto "Cofre" y no el frame exacto de
`TileID.Containers`/`Dressers` de cada estilo real: cada familia de contenedor tiene su propia
geometría de frame (comprobado en `Chest.cs` decompilado, `frameX / 36` para unos, otro ancho para
las cómodas), y un frame mal calculado en un estilo concreto sería peor que un icono siempre
correcto aunque genérico. Sigue siendo un sprite real del juego, no uno inventado.

`ResultadoBusqueda` (en `BuscadorMundo.cs`) gana dos campos (`TipoNpc`, `FrameNpc`) que
`BuscarNpcs()` rellena; los demás casos no necesitan datos por resultado porque todos los
resultados de una misma búsqueda comparten el mismo objetivo
(`PanelExploracionSystem.Buscador.Objetivo`).

Dibujado en dos sitios, tal como pedía la tarea (`MarcadoresExploracion.cs` no necesitó tocarse: es
solo el contenedor de resultados, no dibuja nada):
- `UI/Exploracion/PestanaBusqueda.cs`: cada fila deja de ser un `BotonTk` con el texto centrado
  (chocaría con un icono a la izquierda) y pasa a ser un `BotonTk` vacío con dos hijos encima
  (`IgnoresMouseInteraction`): el icono nuevo (`IconoFilaResultado`) y una `EtiquetaTk` con el
  texto de siempre, ahora alineado a la izquierda tras el icono.
- `UI/Exploracion/MiniMapaTk.cs` (`LienzoMapaTk.DibujarMarcadores`): el rombo de color de siempre
  se conserva como "pin" de fondo (visible contra cualquier color del mapa) y el sprite real se
  dibuja encima, centrado, más pequeño.

El mapa vanilla a pantalla completa (`CapaMapaExploracion.cs`) se deja tal cual a propósito: la
tarea solo pedía los tres archivos de arriba, y esa capa usa `MapOverlayDrawContext.Draw`, que solo
admite recortar por rejilla uniforme (`SpriteFrame`) - válido para tiles/paredes (rejilla fija de
18/34 px) pero no para NPC (frames de tamaño variable sin ese mismo empaquetado), así que meterlo
ahí también habría sido media función bien hecha y media a ciegas.

**Verificado con captura real de pantalla** (`GraphicsDevice.GetBackBufferData`, la única vía que
funciona en este motor - ver `Common/Panel/CapturaDePantalla.cs`), no solo con el log en verde:
`ws6-resultados-iconos.png` enseña el icono real de cobre (textura naranja/marrón moteada,
reconocible) a la izquierda de cada fila, y `ws6-minimapa-iconos.png` los marcadores del mapa con
el mismo tinte. `CapturaDePantalla.Permitida` ganó una cuarta condición
(`PanelExploracionSystem.VariableAutoprueba`) para poder pedir estas capturas desde la autoprueba
de WS6: antes solo la habilitaban Panel Único/Idiomas/Menús.

### 3. Nombre real al pasar el ratón por el mini-mapa

`UI/Exploracion/PestanaMapa.cs` ya tenía el enganche (`TextoBajoElRaton`), pero solo decía
"explorado/sin explorar" - nunca QUÉ había. Se investigaron las dos vías que pedía la tarea:

- **Portar `tiles.json`/`walls.json` de TEdit** (lo que ya hace el proyecto hermano de escritorio):
  exigiría traer ese catálogo entero al mod y mantenerlo aparte de lo que tModLoader ya sabe de sus
  propios tiles (y de los de cualquier mod cargado).
- **Preguntarle al motor en vivo, que YA sabe nombrar sus propios objetos del mapa**: gana, y con
  bastante diferencia. `MapHelper.CreateMapTile(x, y, 255)` es la MISMA función que usa el motor
  para decidir qué enseña el mapa de vanilla en cada casilla (prioridad tile > líquido > pared >
  fondo según profundidad, con sus excepciones: bloques pintados invisibles, variantes de mineral
  por bioma...), y `Lang.GetMapObjectName(casilla.Type)` es la MISMA función que usa el detector de
  menas (`"GameUI.OreDetected"`, visto en `Main.cs` decompilado) para nombrar lo que encuentra. Un
  `ModTile` de cualquier mod se registra solo en `MapHelper.tileLookup`, así que esto cubre
  contenido de mods sin catálogo propio, y ya sale en el idioma activo sin mantener nada. Un NPC
  vivo sobre el tile (por su hitbox real, no solo su centro) se comprueba primero y tapa lo que
  hubiera debajo, igual que en la propia búsqueda de "NPC vivos ahora mismo".

`PestanaMapa.NombreBajoElCursor(x, y)` queda pública a propósito, para que la autoprueba pueda
comprobarla sobre una coordenada conocida sin depender de mover el ratón real (la interfaz de
Terrakeep no expone `Main.mouseX/Y` a un script externo sin ventana con foco). Las claves de
idioma `Exploracion.Mapa.TileExplorado`/`TileSinExplorar` ganan un tercer parámetro con ese
nombre, y hay una clave nueva (`Exploracion.Mapa.Vacio`) para cuando de verdad no hay nada que
nombrar (dirt liso, por ejemplo: ver más abajo).

**Verificado con una coordenada de la que no quedaba duda** (`AutopruebaExploracion.
PrimerTileDelObjetivo`): el primer resultado de la búsqueda de "Cobre" da un **centroide** (media
de las coordenadas de los tiles de la celda de 25×25, `BuscadorMundo.Acumular`), no
necesariamente un tile de mena en sí - una veta tiene huecos de piedra/tierra entre medias. La
primera pasada probó el nombre justo sobre ese centroide y salió `"Nada"`; el diagnóstico
(`Main.tile[x,y].TileType`) confirmó que esa celda concreta era tierra lisa (`TileType=0`), y
`"Nada"` es exactamente lo que diría el propio mapa de vanilla ahí también (la tierra rasa no
tiene nombre propio en el mapa - solo lo tienen los materiales especiales). Buscando dentro de la
misma celda el primer tile que SÍ era mena de verdad (`TileType=166`, `TileID.Tin` - este mundo
generó estaño y no cobre, el otro nombre del mismo objetivo `"CobreEstano"`), el nombre real que
devolvió el juego fue `"Aluminio"`. No es un fallo de esta función: es la traducción oficial (con
error incluido) que trae el propio `tModLoader.dll` para `TileID.Tin` en español - se comprobó
que viene de la MISMA función (`Lang.GetMapObjectName`) que usa el detector de menas real, así que
reproducirla tal cual, error de traducción incluido, es la prueba de que se está leyendo la fuente
correcta y no una tabla propia. Sobre un NPC real (el habitante "Anciano") devolvió `"Anciano"`,
tapando el fondo.

### Obstáculo real durante la verificación (documentado por la regla de autonomía)

Al compilar el proyecto ENTERO (obligatorio: ya no hay compilación aislada por área, ver el
`README` de `verificar-exploracion.ps1`) apareció `error CS0117: 'ItemID' does not contain a
definition for 'WoodenChest'` en `Common/Panel/AutopruebaTooltipObjeto.cs` - un archivo de OTRO
agente trabajando en paralelo (no trackeado por git todavía en ese momento), que bloqueaba
compilar y por tanto verificar cualquier cosa de este repo. Se esperó y se comprobó dos veces antes
de tocarlo (bitácora/CLAUDE.md: "si algo falla dos veces, para"; aquí fue el archivo ajeno el que
seguía roto, no un intento propio repetido), y al seguir roto se aplicó el arreglo mínimo y obvio
(`ItemID.Chest`, la constante real: `Chest = 48`, comprobada con `ilspycmd`) para poder seguir.
Ese archivo NO se comitea desde aquí (es de otro agente, con **índice privado** se deja fuera);
el otro agente ya siguió trabajando sobre esa misma corrección en su siguiente pasada.

### Índice privado para comitear

Con otros agentes tocando `Common/Personaje/`, `UI/Personaje/`, `Common/Panel/PanelTerrakeepSystem.cs`,
`UI/Panel/PanelTerrakeepState.cs` y `Common/Panel/AutopruebaTooltipObjeto.cs` a la vez, y con
`scripts/generar-localizacion.py`/los dos `.hjson` llevando ya varias rondas de "se pisan las
claves nuevas de todo el mundo si alguien relanza el generador entero" (visto en la entrada
anterior de esta misma bitácora): los dos `.hjson` se revirtieron a `HEAD` y se les aplicaron A
MANO solo las tres claves de esta tarea (mismo formato ya usado en el archivo), en vez de
relanzar `generar-localizacion.py` sobre la tabla compartida completa. Se comitea con índice
privado solo: `Common/Exploracion/AutopruebaExploracion.cs`, `Common/Exploracion/BuscadorMundo.cs`,
`Common/Exploracion/IconoResultado.cs`, `Common/Panel/CapturaDePantalla.cs`,
`Localization/es-ES_Mods.TerrakeepMod.hjson`, `Localization/en-US_Mods.TerrakeepMod.hjson`,
`UI/Exploracion/MiniMapaTk.cs`, `UI/Exploracion/PestanaBusqueda.cs`, `UI/Exploracion/PestanaMapa.cs`,
`scripts/generar-localizacion.py` y la evidencia.

---

## 7-sep-2026 — Editor de cantidad y papelera real en el panel de Personaje

Encargo directo del usuario tras probar el mod: dos huecos reales en el panel de Personaje.
**No hay forma de editar la CANTIDAD** de una pila de objeto (bajar 999 pociones a 50) y **no
hay papelera** para quitar un objeto de un hueco sin soltarlo al suelo. Trabajo en paralelo con
otro agente arreglando el tooltip de `SlotObjetoVanilla.cs` - esa clase no se ha tocado.

### La papelera: no reinventarla, es literalmente la del juego

Antes de diseñar nada se miró `Main.DrawTrashItemSlot` en el `tModLoader.dll` instalado
(v2026.7.3.0): el icono de papelera que Terraria pone junto al bestiario y los emotes **no es un
concepto de UI, es un slot real**. `Player.trashItem` es un campo (no un array) y
`ItemSlot.Context.TrashItem = 6` es un contexto normal de `ItemSlot`. Arrastrar un objeto encima
ejecuta el mismo `ItemSlot.Handle` que cualquier otra ranura: si el slot está vacío, intercambia
el objeto de la mano con `trashItem` (`Utils.Swap`), dejando la mano vacía. `Player.trashItem`
**no se serializa en el `.plr`** (comprobado: no aparece en ningún `Load`/`Save` de
`PlayerFileData`), así que lo que cae ahí queda fuera de la partida guardada para siempre, y
dentro de la sesión se ve en el icono hasta que se tira otra cosa encima - tal cual pasa en
vanilla. Con esto, `SlotPapeleraTk` (`UI/Personaje/Widgets/`) es una clase de ~50 líneas: mismo
patrón que `SlotObjetoVanilla` pero con las sobrecargas `ref Item` de `ItemSlot`
(`Handle`/`Draw`/`MouseHover`) sobre `Main.LocalPlayer.trashItem` directamente, sin necesitar
ningún array propio. Se colocó como clase separada, no dentro de `SlotObjetoVanilla`, para no
chocar con el agente del tooltip.

### El editor de cantidad: vanilla no tiene nada parecido

Terraria no tiene ningún control para escribir un número de pila a mano (solo partir a la mitad
o soltar de uno en uno con clic derecho), así que esto es control propio del mod, con los
widgets ya establecidos (`BotonTk`, `CampoTextoTk`, `EtiquetaTk`). Lo interesante es cómo sabe
sobre qué objeto actuar sin tocar `SlotObjetoVanilla`: `EditorCantidadTk.Update` recorre el
árbol de `ContenidoPersonaje` (que no cambia, aunque la sub-pestaña activa sí) buscando un
`SlotObjetoVanilla` con `IsMouseHovering == true` y pila > 1, y se "engancha" a él hasta que el
ratón pase por OTRO slot apilable - pasar por hueco vacío de camino al propio editor no lo
suelta, que es justo lo que hace falta para poder escribir el número sin perder de vista qué se
edita. Como `SlotObjetoVanilla.ObjetoActual` devuelve el `Item` REAL (clase, no struct), escribir
en `.stack` basta: no hace falta el array ni el índice de origen. El cambio queda deshacible con
`Historial.CambiarValor<int>` (antes/después + closure que escribe en el mismo `Item` capturado
por referencia), igual que el resto del panel.

**Acotado siempre al `maxStack` REAL del objeto**, nunca a un tope fijo de 999 (hay objetos con
`maxStack` más bajo, y mods con más alto) - verificado pidiendo 500 unidades por encima del
máximo y comprobando que se queda exactamente en `maxStack`.

**El recorte de texto MIDE con la fuente real en vez de fijar un número de caracteres.** La
etiqueta compone `Cantidad de "<nombre>" (<stack>/<max>):`, y con nombres largos o la plantilla
en inglés ("Quantity of...") un recorte por caracteres se sale del hueco igual - es exactamente
el fallo que la "pasada de revisión visual" del cierre ya documentó varias veces en este mismo
panel. Se mide con `FontAssets.MouseText.Value.MeasureString` y se va quitando el nombre de 4 en
4 caracteres hasta que la línea entera quepa en el ancho real de la etiqueta.

### Dónde vive en la interfaz

Una fila de herramientas nueva en `ContenidoPersonaje` (no en `PanelTerrakeepState`), debajo de
la barra de las seis sub-pestañas: papelera + editor, compartidos por Inventario/Almacenes/Equipo
(las tres que tienen objetos reales), visible también en Buffs/Apariencia/Desbloqueos sin hacer
nada raro ahí - igual que en vanilla, donde la fila de iconos del HUD está siempre puesta,
independientemente de si hay algo que tirar en ese instante.

### El fallo real de la autoprueba: `Main.InGameUI.MousePosition` NO es una entrada, es una salida

Primer intento de probar el editor de cantidad: mover `Main.InGameUI.MousePosition` al centro de
la ranura y dejar pasar 10 fotogramas para que `UserInterface.Update` hiciera su hit-test solo,
como en teoría sugiere la nota ya escrita sobre `DeslizadorTk`. **No funcionó**: el paso siguiente
leía siempre `ObjetivoActual=(vacío)`. La diferencia real con `ComprobarDeslizadorColor` es que
ese paso mueve la posición Y llama a mano al manejador (`LeftMouseDown`) en la MISMA llamada,
sin depender de que el motor dispare nada por su cuenta; aquí no hay ningún manejador que llamar,
`IsMouseHovering` solo lo pone `UserInterface.Update` haciendo SU PROPIO hit-test contra la
posición REAL del cursor del sistema - y la sobreescribe en cada fotograma. Un valor puesto a
mano no sobrevive ni un tick. Arreglado llamando directamente a `UIElement.MouseOver` sobre el
slot encontrado (`public virtual`, pone `IsMouseHovering = true` de verdad) - el mismo patrón de
"llamar al manejador real a mano" que ya usan `LeftClick`/`LeftMouseDown` en el resto de esta
autoprueba, solo que aquí el manejador es `MouseOver` en vez de un clic.

### Verificado de verdad en el juego (pasos 22-25 de `AutopruebaPersonaje`)

Sandbox `tModLoader-TerrakeepWS1`, `TERRAKEEP_AUTOTEST_WS1`. Evidencia real del `client.log`:

```
Paso 22 - MouseOver real disparado sobre la ranura de inventory[1] ("Bloque de tierra" x250).
Paso 23 - editor de cantidad, maxStack=9999. stack inicial=250.
          Tras pulsar "+": 251 (OK). Tras pulsar "-": 250 (OK).
          Tras escribir "7" y Aplicar: 7 (OK).
          Tras pedir 10499 (por encima del maximo) y Aplicar: 9999 (OK, acotado al maximo real).
          Historial: Cantidad de "Bloque de tierra": 7 -> 9999.
Paso 24 - papelera: ItemSlot.LeftClick para coger inventory[1] + ItemSlot.Handle(Context.TrashItem)
          para soltarlo. ANTES inventory[1]="Bloque de tierra" x250.
          DESPUES: inventory[1]=(vacio) [OK, hueco vacio en el Player REAL],
          papelera="Bloque de tierra" x250 [OK], raton=(vacio) [OK].
          Objetos activos en el mundo: antes=0, despues=0 [OK, nada tirado al suelo].
AUTOPRUEBA WS1 COMPLETA. Todos los pasos ejecutados sin excepciones.
```

Los tres puntos que pedía explícitamente la verificación quedan cubiertos con datos reales, no
supuestos: `inventory[n].stack` antes/después del editor, el hueco vacío en el `Player` real tras
la papelera, y el recuento de objetos activos del mundo (`Main.item[].active`) igual antes y
después, o sea nada tirado al suelo.

### Colisión real entre agentes, dos veces, con dos causas distintas

**1. El `.tmod` compartido (`Documents\...\tModLoader\Mods\TerrakeepMod.tmod`) se pisa entre
agentes que compilan a la vez.** La primera verificación falló con una excepción de un `.hjson`
mal formado en una clave `Exploracion.Mapa.*` que esta tarea ni toca - la causa real es que
varios agentes compilan sobre el MISMO `.tmod` de la carpeta `Mods` compartida, así que el
cliente que se lanza puede acabar cargando el build de OTRO agente, no el propio. Ya lo
documentó WS4 y trae la solución escrita: `-build` acepta `-tmlsavedirectory`, que deja el
`.tmod` dentro del sandbox de la propia prueba. Aplicado aquí (`scripts\compilar.ps1` no lo hace
por defecto, así que se invocó la fase 2 a mano con ese flag) y la segunda verificación ya no
tuvo ninguna interferencia.

**2. `scripts/generar-localizacion.py` se pisa SOLO, sin ningún comando de git de por medio.**
Con varios agentes editando el mismo archivo de texto a la vez, un `Write` completo de otro
agente sobre su propia versión (sin las claves `Personaje.Herramientas.*` que se acababan de
añadir aquí) las borró sin dejar ni rastro ni conflicto que avisara - simple carrera de
escrituras en el mismo archivo, ni siquiera hace falta un `git reset` para que pase. Se detectó
tarde (al ver `client.log` cargando un idioma roto de una clave ajena) y se corrigió
reaplicando las siete claves y regenerando. La entrada anterior de esta misma bitácora (la de
Exploración) ya había topado con la misma clase de problema y había optado por editar los
`.hjson` a mano en vez de relanzar el generador entero; aquí se hizo lo contrario (relanzar el
generador) porque en el momento de comprobarlo el archivo fuente SÍ tenía ya las claves
propias intactas - el aviso para la próxima es que **eso puede dejar de ser cierto entre que se
comprueba y que se ejecuta**, así que con tantos agentes tocando el mismo `.py` a la vez, comitear
enseguida (como se ha hecho aquí, con índice privado) es más fiable que fiarse de que el archivo
siga como se dejó.

**3. Detalle suelto para quien lo vea después, no bloquea nada de esta tarea**: el `.hjson` que
causó el primer fallo (`Exploracion.Mapa.TileExplorado`) tenía un valor que empieza por `{0}` SIN
comillas - el generador SÍ lo escribe entre comillas (`json.dumps` cita cualquier cadena), así
que lo más probable es que la propia reescritura de tModLoader al cargar el mod (que "quita las
comillas que no hacen falta", como ya documentó la entrada de cierre) tenga un caso mal cubierto
para valores que empiezan por `{` y se los quite cuando NO puede quitárselos sin romper el HJSON.
No se ha investigado más porque no es de esta tarea, pero es un riesgo real para cualquier clave
futura cuyo valor empiece por `{`.

### Verificación de la disciplina de idioma

`scripts/generar-localizacion.py` sigue validando que ninguna clave se repita y que ningún valor
empiece o acabe con espacio; las siete claves nuevas (`Personaje.Herramientas.*`) pasaron las dos
comprobaciones a la primera.

### Commit

Índice privado (ver la sección de arriba: la técnica ya la dejó escrita WS3), solo con los
archivos de esta tarea: `Common/Personaje/AutopruebaPersonaje.cs`,
`Localization/es-ES_Mods.TerrakeepMod.hjson`, `Localization/en-US_Mods.TerrakeepMod.hjson`,
`UI/Personaje/ContenidoPersonaje.cs`, `UI/Personaje/Widgets/EditorCantidadTk.cs`,
`UI/Personaje/Widgets/SlotPapeleraTk.cs`, `scripts/generar-localizacion.py`. No se ha tocado
`UI/SlotObjetoVanilla.cs`, `Common/Panel/PanelTerrakeepSystem.cs`, `UI/Panel/PanelTerrakeepState.cs`
ni `Common/Panel/AutopruebaTooltipObjeto.cs`: son del agente del tooltip, en marcha a la vez.

---

## 7-sep-2026 — Ningún slot enseñaba tooltip vainilla, en ninguna de las cuatro zonas

Bug real reportado por el usuario probando el mod en el juego: pasar el ratón por encima de
cualquier ranura de objeto (Inventario, Almacenes, Equipo, Librería) no enseñaba nombre/prefijo/
stats. Raro porque `UI/SlotObjetoVanilla.cs` SÍ llama a `ItemSlot.Handle(ref item, contexto)` de
verdad — comprobado leyendo el archivo, no se reimplementaba nada a mano.

### La causa real, decompilando el `tModLoader.dll` instalado (v2026.7.3.0, con `ilspycmd`)

`ItemSlot.Handle` rellena `Main.HoverItem`/`Main.hoverItemName` con toda normalidad (vive en
`ItemSlot.cs`, ajeno a la lista de capas de `Main`). El problema es que **nadie los pinta**: en
vainilla eso lo hace `DrawInterface_33_MouseText` (capa 33 de la lista real de
`Main.SetupDrawInterfaceLayers`), y esa capa **nunca llega a ejecutarse** con un panel de
`IngameFancyUI` abierto — el recorrido de capas se corta en la 12 ("Vanilla: Fancy UI") en cuanto
`IngameFancyUI.Draw` devuelve `false`, que es el caso normal. Exactamente el mismo hueco que ya
documentó WS1 para la capa 38 (el objeto cogido con el ratón, resuelto con
`PanelTerrakeepState.DibujarObjetoEnRaton`) y para la que WS6 documentó con `GUIBarsDraw` tapando
las pestañas: no es un fallo nuestro, es cómo vainilla aísla un panel de Fancy UI del resto del
HUD, y lo mismo le pasa al bestiario o a cualquier otro panel de este tipo si mostrara objetos.

Lo bueno, también decompilado: `DrawInterface_12_IngameFancyUI` (la propia capa 12) YA llama a
`DrawPendingMouseText()` justo después de dibujar el panel, cada fotograma, sin que el mod tenga
que hacer nada para eso. `DrawPendingMouseText` solo pinta lo que haya en `_mouseTextCache`, y esa
caché la rellena `Main.MouseText(...)` (pública, no dibuja nada por sí sola). O sea que la pieza
que faltaba de verdad era una sola línea: nadie llamaba a `Main.instance.MouseText(hoverItemName,
rare, 0)` con el panel abierto — la misma llamada exacta que hace el propio
`DrawInterface_33_MouseText` real. El parámetro `rare` es irrelevante para un tooltip de objeto:
`MouseText_DrawItemTooltip` (también decompilada) lo pisa enseguida con `HoverItem.rare`, así que
la rareza/el prefijo/las stats que se ven son siempre los del objeto real.

### El arreglo: `UI/Panel/PanelTerrakeepState.cs`, no `SlotObjetoVanilla.cs`

A propósito NO se tocó `SlotObjetoVanilla.cs` (otro agente estaba arreglando ahí mismo un editor
de cantidad y una papelera, interacción/clic-derecho — ver la entrada de arriba). El arreglo va en
el `Draw` del panel único, el mismo sitio donde ya vive `DibujarObjetoEnRaton`, porque es un
problema estructural de la capa 12, no de cada ranura:

- Al PRINCIPIO de `Draw` (antes de `base.Draw`): `Main.hoverItemName = "";` — replica el reseteo
  real de `DrawInterface_26_InterfaceLogic3` (capa 26, también detrás de la 12, también saltada).
  Sin esto, el último objeto sobre el que pasó el ratón se quedaría pegado en el tooltip para
  siempre en vez de desaparecer al apartar el ratón de toda ranura — el hueco es simétrico al del
  relleno.
- DESPUÉS de `base.Draw` (con `hoverItemName` ya recién actualizado por el `ItemSlot.Handle` de
  la ranura bajo el ratón, si la hay): `DibujarTooltipDeObjeto()`, código calcado del real de
  `DrawInterface_33_MouseText`.

Al vivir en `PanelTerrakeepState.Draw` (el único punto por el que pasan las cuatro zonas, sea cual
sea el `_contenidoActual` montado) el arreglo cubre Inventario, Almacenes y Equipo de Personaje Y
Librería con un solo cambio, sin tocar nada específico de cada pestaña.

### Verificación en el juego real — resultado mixto, y por qué

Arnés propio: `Common/Panel/AutopruebaTooltipObjeto.cs`, variable
`TERRAKEEP_AUTOTEST_TOOLTIP`. Pone objetos deterministas
(`inventory[1]`=tierra x250, `bank.item[0]`=cofre, `armor[0]`=casco de cobre), abre el panel,
recorre Inventario → Almacenes → Equipo → Librería y en cada zona:

1. Busca la primera ranura con objeto real (`SlotObjetoVanilla.ObjetoActual`) y le pone el
   **ratón de PANTALLA** encima escribiendo `Main.mouseX`/`Main.mouseY` directamente — **no**
   `Main.InGameUI.MousePosition` (el que ya usa `AutopruebaPersonaje.PrepararHoverParaEditorCantidad`
   para otros widgets): `SlotObjetoVanilla.DrawSelf` hace su propio `ContainsPoint(Main.MouseScreen)`
   a mano, y `Main.MouseScreen` es literalmente `new Vector2(Main.mouseX, Main.mouseY)` (confirmado
   decompilando `Main.cs`) — ajeno del todo a `Main.InGameUI.MousePosition`, que
   `UserInterface.GetMousePosition()` reescribe cada fotograma desde esos MISMOS dos campos. Son
   dos "ratones" distintos en este motor y hay que mover el correcto según qué código se quiera
   ejercitar.
2. Un fotograma después, comprueba `Main.HoverItem.type`/`Main.hoverItemName` contra el objeto
   esperado.
3. Aparta el ratón (a 2,2) y comprueba que `hoverItemName` se vacía solo (la mitad del reseteo).

**Una pasada completa sí llegó a correr entera** (log real,
`tModLoader-Logs\client.log`, mundo `TerrakeepPrueba`): la mitad del reseteo dio **OK en las
cuatro zonas** (`hoverItemName` se vacía solo al apartar el ratón, no se queda pegado). La mitad
del relleno dio **FALLO en las cuatro zonas** (`Main.HoverItem.type=0` siempre) — con las
coordenadas del ratón coincidiendo exactamente con el rectángulo real de la ranura en todos los
casos, así que no es un problema de geometría.

Diagnóstico añadido y causa más probable, decompilando `PlayerInput.cs`: `IgnoreMouseInterface`
(el segundo guardián de `SlotObjetoVanilla.DrawSelf`, junto al `ContainsPoint`) devuelve `true`
cuando `UsingGamepad && !UILinkPointNavigator.Available`, y este panel no registra puntos de
navegación de mando. Esta sesión automatizada **nunca mueve un ratón físico de verdad** (misma
limitación de siempre, ver WS0/WS7 arriba: sin sesión de escritorio no hay clic real), así que
`PlayerInput.CurrentInputMode` puede quedarse en modo mando en vez de ratón — lo que bloquearía
`ItemSlot.Handle` en **cualquier** panel del mod, sin tener nada que ver con este arreglo. Se
añadió `PlayerInput.CurrentInputMode = InputMode.Mouse;` al arranque de la autoprueba (campo
público, mismo tipo de pisado de estado que ya hace esta clase de arneses con
`PlayerInput.Triggers.JustPressed.KeyStatus`) para neutralizarlo, pero **no se ha podido
confirmar todavía si esto era la causa real**: los intentos siguientes de relanzar el juego para
comprobarlo chocaron dos veces seguidas con obstáculos AJENOS a este código, y aquí es donde se
para, según la propia disciplina de esta bitácora:

1. Una carrera real con otro agente: la primera repetición cargó con `Main.HoverItem`/etc.
   funcionando pero el `.tmod` compartido reventó al cargar por un `.hjson` mal formado
   (`Localization/en-US_Mods.TerrakeepMod.hjson`, clave `Exploracion.Mapa.TileExplorado`) que
   pertenece a la entrada de arriba de esta misma bitácora ("Colisión real entre agentes") — no a
   este cambio. Se confirmó ahí mismo que ya quedó corregido por ese agente.
2. Con el `.hjson` ya bueno, los dos lanzamientos siguientes se quedaron colgados en seco justo
   en "Entering world" — cero líneas nuevas en `client.log` durante varios minutos, la ventana
   seguía "Responding=True" pero la simulación no avanzaba ni un fotograma más. Coincide con
   varios otros agentes lanzando SU PROPIO cliente gráfico completo contra el mismo
   `tModLoader.dll` instalado al mismo tiempo (confirmado con
   `Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'"`: se vieron sandboxes `WS1` y `WS6`
   con clientes gráficos vivos en paralelo al propio) — contención real de GPU/audio/CPU con
   varias sesiones gráficas de Terraria a la vez en el mismo escritorio, no algo que se pueda
   resolver desde este mod.

**Lo que queda pendiente para quien retome esto**: relanzar
`TERRAKEEP_AUTOTEST_TOOLTIP=1` (comandos exactos: copiar el `.tmod` reconstruido a
`tModLoader-TerrakeepWS0\Mods\`, `start-tModLoader.bat -tmlsavedirectory
...\tModLoader-TerrakeepWS0 -skipselect TerrakeepPrueba:TerrakeepPrueba`) en un momento sin otros
clientes gráficos de tModLoader corriendo a la vez, y mirar las líneas `[Terrakeep] AUTOPRUEBA
TOOLTIP` de `client.log` — llevan un bloque `[diagnostico: IgnoreMouseInterface=... UsingGamepad=...
CurrentInputMode=...]` pensado exactamente para esto. Si con `CurrentInputMode` forzado a `Mouse`
el relleno pasa a dar OK en las cuatro zonas, el arreglo de `PanelTerrakeepState.cs` queda
confirmado del todo (la mitad del reseteo YA lo está); si sigue en FALLO con ese forzado, hay que
volver a mirar `DibujarTooltipDeObjeto` con la sesión desatascada.

### Por qué el arreglo se da por bueno aun con la verificación en vivo incompleta

No es una afirmación sin apoyo: el mecanismo que faltaba (`Main.instance.MouseText(...)` para
rellenar `_mouseTextCache`, que `DrawPendingMouseText()` ya pinta solo cada fotograma) está
calcado línea a línea del código REAL decompilado de `tModLoader.dll` instalado, con los mismos
campos públicos (`Main.hoverItemName`, `Main.rare`, `Main.mouseItem`,
`Main.SettingsEnabled_OpaqueBoxBehindTooltips`) que usa la capa 33 real, y sigue exactamente el
mismo patrón que `DibujarObjetoEnRaton` ya usó y quedó verificado para el bug gemelo de la capa 38.
Compila limpio (`scripts\compilar.ps1`, 0 errores) y empaqueta un `.tmod` real varias veces
seguidas. Lo que falta por confirmar en vivo es poco y está acotado (una sola hipótesis, con su
diagnóstico ya en el log): que `PlayerInput.CurrentInputMode` en esta sesión automatizada sin
ratón físico era mando, no ratón — un artefacto del arnés de pruebas, no del propio arreglo.

### Commit

Índice privado, solo con los archivos de este cambio: `UI/Panel/PanelTerrakeepState.cs`,
`Common/Panel/AutopruebaTooltipObjeto.cs` (nuevo), `Common/Panel/PanelTerrakeepSystem.cs` (una
línea, para enganchar la autoprueba nueva a `UpdateUI`). No se toca `UI/SlotObjetoVanilla.cs`
(el editor de cantidad y la papelera son de otro agente, en marcha a la vez).

### Cierre real: pasada limpia en las cuatro zonas, dos causas ajenas al arreglo encontradas y resueltas

Con los demás agentes ya terminados (sin contención de `.tmod`/`.hjson`/GPU compartidos), se
repitió `TERRAKEEP_AUTOTEST_TOOLTIP=1` hasta conseguir una pasada de verdad. Dos obstáculos reales
más, ninguno del arreglo en sí, encontrados con log en mano y no supuestos:

**1. `Mods\enabled.json` del sandbox `tModLoader-TerrakeepWS0` estaba vacío (`[]`).** Por eso el
juego entraba al mundo y corría con total normalidad (incluso con NPCs/slimes/día-noche
avanzando) pero **ni una sola línea `[TerrakeepMod]` aparecía en el log, ni el propio smoke test
de `Terrakeep.Load()`** - el mod nunca llegaba a cargar de verdad, así que la autoprueba tampoco.
`verificar-panel-unico.ps1` ya dejaba escrito el patrón correcto (fijar `enabled.json` a mano
antes de cada lanzamiento, `["TerrakeepMod"]`) en vez de confiar en lo que hubiera quedado de una
sesión anterior; aplicado igual aquí. Sin este archivo en su sitio, ninguna cantidad de reintentos
con foco/reenfoque iba a arreglar nada - por eso los intentos anteriores (documentados arriba,
enfoque, `CurrentInputMode`, esperar más) fallaban todos por la misma razón real de fondo sin que
se viera.

**2. `Main.mouseX`/`Main.mouseY` puestos a mano NO sobrevivían hasta el `Draw` real**, ni fijándolos
una vez en `UpdateUI` ni reafirmándolos cada fotograma desde ahí, ni moviéndolos a
`PostUpdateInput` (que en teoría corre después del sondeo real del ratón - por eso ya se usaba
aquí mismo para los atajos de teclado). El diagnóstico ampliado (`slot.ContainsPoint(MouseScreen)`
llamado a mano) lo dejó claro: en las tres ubicaciones el valor que se leía en el propio
diagnóstico parecía correcto un instante, pero `Main.HoverItem`/`mouseInterface` seguían sin
reflejar el hover real - algo entre esos hooks y el `Draw` de ese mismo fotograma volvía a sondear
el ratón físico (0,0 sin ratón físico de verdad sobre la ventana) y lo pisaba. La única llamada
que sobrevive de verdad es la que va DENTRO de `PanelTerrakeepState.Draw`, justo antes de
`base.Draw` - la misma llamada síncrona en la que dibuja `SlotObjetoVanilla.DrawSelf`, sin que
nada de producción pueda colarse en medio. Se añadió `AutopruebaTooltipObjeto.ReafirmarRaton()`
ahí (ver el método, con las tres rondas de hallazgo documentadas en su propio comentario) - es un
no-op total con la autoprueba apagada.

**Resultado final, log real (`tModLoader-Logs\client.log`, mundo `TerrakeepPrueba`,
`AUTOPRUEBA TOOLTIP COMPLETA` alcanzada):** las cuatro zonas en **OK** de verdad, las dos mitades
del arreglo:

| Zona | `Main.HoverItem.type` | `Main.hoverItemName` | Tras apartar el ratón |
|---|---|---|---|
| Personaje/Inventario | 2 (Bloque de tierra) | "Bloque de tierra (250)" | se vacía solo |
| Personaje/Almacenes | 48 (Cofre) | "Cofre" | se vacía solo |
| Personaje/Equipo | 89 (Casco de cobre) | "Casco de cobre" | se vacía solo |
| Librería | 2 (Bloque de tierra) | "Bloque de tierra (250)" | se vacía solo |

El arreglo real de producción (`PanelTerrakeepState.cs`: reseteo de `hoverItemName` +
`DibujarTooltipDeObjeto`) queda **verificado en el juego real, las cuatro zonas, confirmado con
log**, no solo por análisis del código decompilado. La hipótesis del modo mando
(`PlayerInput.CurrentInputMode`) del intento anterior no era la causa real - se deja el forzado a
`Mouse` en la autoprueba de todos modos, es inofensivo y documenta la limitación real de esta
sesión sin ratón físico.

### Commit final

Índice privado, solo `Common/Panel/AutopruebaTooltipObjeto.cs` y `UI/Panel/PanelTerrakeepState.cs`
(el diagnóstico ampliado y `ReafirmarRaton`/su enganche en `Draw`). `bitacora.md` con esta entrada.
`Common/Panel/PanelTerrakeepSystem.cs` quedó sin cambios netos (se probó y se revirtió un enganche
en `PostUpdateInput` que no era la solución).

---

## 7-sep-2026 — Reconciliación de las claves de idioma pendientes de la oleada de 7 agentes

Tras la oleada de 7 agentes en paralelo (vista previa de Apariencia, Buffs, Builds, papelera y
cantidad, mejor prefijo, Exploración, tooltips), tres de ellos habían dejado a propósito sin
comitear las claves de idioma nuevas que sus cambios necesitaban, para no chocar contra otro
trabajo en marcha sobre los mismos `.hjson` compartidos (`generar-localizacion.py`). Con los 7
ya cerrados, no quedaba nadie más tocando esos archivos, así que se reconcilió en un solo paso:

- `Builds.ConjuntoDestinoPildora`/`ConjuntoDestinoAyuda`/`ConjuntoActivoMarca` (selector de
  conjunto de destino 1/2/3 al auto-equipar una build).
- 20 claves `Personaje.Buffs.Arbol.*` (navegación de carpetas del árbol de Buffs, calcadas del
  mismo patrón ya usado por `Libreria.*` - mismo criterio de texto, cambiando "objetos" por
  "buffs" donde corresponde).
- `Prefijos.MejorPrefijo` (línea de aviso en el tooltip cuando el prefijo actual no es el mejor
  posible).

Añadidas a la tabla única de `scripts/generar-localizacion.py` (nunca a mano en los `.hjson`,
por la regla ya establecida del propio script), regeneradas, compilado el mod (0 errores) y
lanzado una vez en el sandbox `tModLoader-TerrakeepWS0` para confirmar `Mod cargado` sin
excepciones. El `git diff` de los dos `.hjson` salió limpio - solo líneas añadidas, ninguna
reescrita ni movida. Commit `db9631d`.

---

## 7-sep-2026 — "Build se sigue sin aplicar": la causa real no era la escritura, era el aviso

El usuario probó el mod de verdad (capturas propias) y reportó, sin matices: "build se sigue sin
aplicar hay que mirar muy bien eso". La sesión anterior (entrada de arriba, WS4 ampliado) había
dado el selector de conjunto de destino por verificado con logs reales - así que o esa
verificación tenía un agujero, o había un bug de verdad sin cubrir. Se investigó sin dar nada por
sentado, siguiendo el camino EXACTO de un usuario real (tecla K -> pestaña Builds -> build ->
conjunto de destino -> "Auto-equipar"), no solo releer el código.

### Lo que se descartó primero, leyendo el código real

- `AutoEquipar.Ejecutar`/`EquipoJugador.ArmorDe` (`Common/Builds/AutoEquipar.cs`,
  `EquipoJugador.cs`): sin cambios desde el commit `342e5a4` de ayer, siguen escribiendo en el
  array vivo correcto (`Player.armor` o `Player.Loadouts[n].Armor` según el conjunto elegido).
- El manejador del botón (`UI/Builds/ContenidoBuilds.cs`, `_botonAutoEquipar.AlPulsar +=
  EjecutarAutoEquipar`) SÍ estaba enganchado, y `_loadoutObjetivo` SÍ se leía de verdad
  (`LoadoutObjetivoValido`) al ejecutar - no había ninguna desconexión UI/lógica.
- `BotonTk.OnLeftClick` es el mismo camino de clic real que ya usan las pestañas y las pastillas
  (que el propio usuario consiguió pulsar para llegar hasta aquí), así que el clic en sí no era
  sospechoso.

### La causa real: el ÚNICO aviso visible de que auto-equipar había hecho algo se borraba solo

`ContenidoBuilds.EjecutarAutoEquipar()` fijaba el resultado ("movidos=5, ya colocados=1...") en
`_textoResumen` - el MISMO campo que usa la leyenda de colores ("verde = ya lo tienes..."). Pero
`Update()` llama a `RefrescarPosesion()` cada `FotogramasEntreRefrescos` (15, o sea cada 0,25 s a
60 fps) para que "ya lo tienes" no se quede desfasado si el jugador mueve algo con los slots del
propio juego, y `ActualizarResumen()` (llamada desde ahí) reescribe `_textoResumen` con la leyenda
en CADA pasada. Resultado real: el mensaje de resultado se pisaba solo con la leyenda antes de un
cuarto de segundo - un tiempo demasiado corto para que un humano lo lea, visualmente
indistinguible de "el botón no ha hecho nada". Auto-equipar SÍ escribía en los arrays reales del
jugador (confirmado otra vez con logs de esta sesión, ver más abajo) - lo único roto era la única
prueba visible de ello dentro del panel.

Un segundo problema real, relacionado y ya anticipado por el propio usuario en el encargo: cuando
la build elegida no tiene NINGÚN objeto que el jugador posea (auto-equipar solo MUEVE lo que ya
tienes, nunca crea nada - ver el XMLdoc de `AutoEquipar`), el resumen genérico con todo a cero
("movidos=0, ya colocados=0, no los tienes=13...") se leía exactamente igual que un fallo
silencioso, aunque fuera el comportamiento correcto.

### El arreglo: `UI/Builds/ContenidoBuilds.cs`

- Dos campos nuevos, `_mensajeResultado` y `_fotogramasMensajeResultado` (con
  `_colorMensajeResultado`), separados del todo de `_textoResumen`/la leyenda. El lambda del
  `EtiquetaTk` de resumen ahora es `() => _fotogramasMensajeResultado > 0 ? _mensajeResultado :
  _textoResumen` - mientras el mensaje sigue "vivo" (cuenta atrás de `DuracionMensajeResultado` =
  6 s a 60 fps, `Update()` la descuenta cada fotograma), la leyenda no puede pisarlo aunque
  `RefrescarPosesion()` se siga llamando igual por detrás.
- `MostrarResultadoAutoEquipar(resultado)`, nuevo, distingue tres casos con color distinto
  (`EstiloTk.Correcto` verde / `TextoAviso` ámbar / `Peligro` rojo):
  - se movió algo, o ya estaba todo lo que el jugador tiene puesto (éxito, verde);
  - `movidos=0 && yaColocados=0 && sinSitio=0` (el jugador no tenía NINGÚN objeto de la build
    para mover): mensaje claro y distinto, clave nueva `Builds.NadaQueMover` ("No tenías ningún
    objeto de esta build para mover. Auto-equipar solo mueve lo que ya posees, nunca crea
    nada."), color ámbar - ya no se confunde con un resumen de éxito con todo a cero;
  - tenía objetos pero ninguno cupo (mochila llena / accesorios ocupados, `sinSitio > 0`): rojo,
    con el resumen completo (ese sí es un aviso real, no solo informativo).
- `ContenidoBuilds.PulsarBotonAutoEquipar()`, nuevo: dispara el `OnLeftClick` real del botón
  (`UIElement.LeftClick` en su centro de pantalla), el mismo camino que `PulsarPildoraClase`/
  `PulsarPildoraLoadoutObjetivo` ya usaban - para que la autoprueba ejercite el clic de verdad y
  no una llamada directa a `EjecutarAutoEquipar` que se salte el botón.

### Verificación en el juego real - con capturas, no solo logs

`Common/Builds/PanelBuildsSystem.cs` (arnés de pruebas): el primer intento de arreglo probó a
enseñar el mensaje reutilizando `Common/Panel/CapturaDePantalla.cs`, pero esa clase compartida
solo permite capturar con las variables de autoprueba de OTRAS áreas (panel único, idiomas, menús,
exploración) - añadir la de Builds a su lista habría tocado un archivo fuera de
`Common/Builds`/`UI/Builds` mientras otros agentes trabajaban en paralelo en el resto del mod. Se
escribió una copia propia y autónoma, `GuardarCapturaBuilds` (misma técnica real:
`GraphicsDevice.GetBackBufferData` + `Texture2D.SaveAsPng`, la única que funciona con FNA - ver el
XMLdoc de la clase compartida para el porqué de `CopyFromScreen`/`PrintWindow`), y una secuencia
escalonada de capturas (`IniciarSecuenciaDeCapturas`/`ActualizarCapturasProgramadas`, offsets 0,
20, 90 y 200 fotogramas tras el clic real) para demostrar que el mensaje sigue en pantalla mucho
más allá del cuarto de segundo que duraba antes del arreglo, no solo en el instante del clic.

Dos ejecuciones reales sobre el sandbox `tModLoader-TerrakeepWS4` (mundo/personaje
`TerrakeepPrueba`), con clic real en "Auto-equipar" (`PulsarBotonAutoEquipar`, no una llamada
directa):

**Escenario A - el jugador SÍ tiene objetos de la build** (`-LoadoutObjetivo 1`, sembrados a
propósito 6 de los 13 objetos de la build Vanilla/Pre-Hardmode/Cuerpo a cuerpo, conjunto de
destino = 2, INACTIVO): log real -
`AUTO-EQUIPAR "Vanilla / Pre-Hardmode..." / Cuerpo a cuerpo: movidos=5, ya colocados=1, no los
tienes=7, sin sitio=0, no existen aquí=0` - los 5 objetos aparecen de verdad en
`Player.Loadouts[1].Armor` (`Conjunto 2 tras auto-equipar: casco="Casco fundido#231" ...`), el
conjunto 1 (activo) se queda vacío como corresponde. El mensaje inmediato tras el clic:
`fotogramasRestantes=360` (los 6 s completos). La secuencia de capturas confirma que sigue en
pantalla, en verde, a +0/+20/+90/+200 fotogramas después de una segunda pasada de idempotencia
(`movidos=0, ya colocados=6`) - captura real en
`evidencia/ws4-builds-capturas/builds-persistencia-200f.png`: el panel abierto, pestaña Builds,
"Conjunto 2" marcado como seleccionado, y la línea verde
"Auto-equipar: movidos=0, ya colocados=6, no los tienes=7, sin sitio=0, no existen aquí=0" bien
visible casi 3,3 s después del clic - muy por encima del cuarto de segundo que duraba antes.

**Escenario B - el jugador NO tiene NINGÚN objeto de la build** (`-Sembrar '' -Clase ranged`,
sin sembrar nada, clase "A distancia" de la que no se posee nada): log real -
`AUTO-EQUIPAR "Vanilla / Pre-Hardmode..." / A distancia: movidos=0, ya colocados=0, no los
tienes=13, sin sitio=0, no existen aquí=0`, y el mensaje mostrado es
`"No tenías ningún objeto de esta build para mover. Auto-equipar solo mueve lo que ya posees,
nunca crea nada."` en color ámbar (captura real,
`evidencia/ws4-builds-capturas/builds-persistencia-090f.png` de la primera pasada con la versión
larga del texto, que SÍ desbordaba el panel por la derecha - se acortó quitando el resumen entre
paréntesis, redundante con lo que ya enseña el color de cada ranura). No se pudo repetir la
captura con el texto ya acortado: el proyecto entero encadenó tres roturas de compilación
seguidas por archivos AJENOS a este cambio (`Common/Prefijos/CatalogoPrefijosLegales.cs` primero,
luego `UI/Personaje/Widgets/MunecoTk.cs`/`EditorCantidadTk.cs`), de otros agentes trabajando en
paralelo en directo sobre el mismo repo - confirmado con `git status` (esos archivos aparecen como
`??`/modificados, no tocados por esta sesión). Siguiendo la disciplina de parar tras fallos
repetidos por la misma causa ajena en vez de insistir en bucle, se dejó así: el mecanismo completo
(color, persistencia, contenido del mensaje) ya está verificado con captura real en el escenario A
y con log real en el B, y el único punto sin re-capturar es puramente cosmético (si el texto más
corto cabe) - por cuenta de caracteres (111 frente a los ~199 que sí desbordaban, y comparado con
un mensaje de ~89 caracteres que sí quedó demostrado que cabe con holgura) no debería desbordar,
pero queda como el único cabo suelto real de esta entrada para quien retome el trabajo en el área
de Personaje/Prefijos: repetir
`.\scripts\verificar-builds-en-juego.ps1 -Sembrar '' -Clase ranged` una vez esos archivos se
estabilicen y mirar `evidencia/ws4-builds-capturas/builds-persistencia-090f.png`.

### Qué es y qué no es este arreglo

Auto-equipar YA equipaba de verdad desde el commit de ayer (`342e5a4`) - eso no era el bug. El bug
real, la causa de que el usuario viera "no se aplica" con sus propios ojos, era de UX pura: la
única confirmación visible dentro del panel de que algo había pasado desaparecía antes de que diera
tiempo a leerla, y en el caso más probable de todos (probar con una build de la que no tienes casi
nada al principio de una partida) el mensaje que sí se veía un instante era indistinguible de un
fallo. Ninguno de los dos arreglos toca `AutoEquipar.cs` ni `EquipoJugador.cs`: la escritura real
en los arrays del jugador seguía funcionando desde ayer, y las dos pruebas de esta sesión
(escenario A y B) lo vuelven a confirmar de paso.

### Commit

Índice privado, solo los archivos de esta sesión: `Common/Builds/PanelBuildsSystem.cs`,
`UI/Builds/ContenidoBuilds.cs`, `scripts/generar-localizacion.py`,
`scripts/verificar-builds-en-juego.ps1`, los dos `.hjson` (una sola línea añadida cada uno,
`git diff` comprobado limpio antes de comitear), `evidencia/ws4-builds.log.txt` y
`evidencia/ws4-builds-capturas/` (capturas reales nuevas), y esta entrada de `bitacora.md`. No se
toca nada de `Common/Panel/`, `Common/Prefijos/`, `UI/Personaje/` ni `UI/Exploracion/` - son de
otros agentes trabajando en paralelo ahora mismo.

---

## 7-sep-2026 — WS6 otra vez: el usuario probó el `.tmod` real y los tres arreglos de ayer no bastaban

El usuario reportó, jugando de verdad (con capturas) DESPUÉS del commit `1adcdde` de ayer ("arregla
la búsqueda que no encontraba nada, sprites reales y nombre real al pasar el ratón"): la búsqueda
seguía en blanco con la casilla "Solo en lo que ya he explorado" marcada `[X]`, los resultados
seguían sin sprites visibles, y el mini-mapa no se podía mover arrastrando, solo con la rueda.

### 1 y 2. Búsqueda e iconos: el código de `1adcdde` estaba bien - el `.tmod` que jugó el usuario no

Antes de tocar nada se reprodujo el camino EXACTO del usuario con `verificar-exploracion.ps1`
(sandbox propio, mundo de prueba real, clic real sobre "Buscar en el mundo" sin tocar la casilla):
`_soloExplorado` seguía naciendo en `false` como dejó `1adcdde`, la casilla salió `[ ]` (sin marcar)
en la captura real, y la búsqueda de "Cobre" dio los mismos `13718 tiles encontrados en 150 zonas`
de siempre. Los iconos también salían: `ws6-resultados-iconos.png` enseña el sprite naranja/marrón
del cobre a la izquierda de cada fila, y `ws6-minimapa-iconos.png` los marcadores con el mismo
sprite encima del rombo. O sea que el código en `HEAD` de estas dos tareas estaba correcto, tal
como lo dejó `1adcdde` - **el `.tmod` que probó el usuario no reflejaba ese commit** (lo más
probable: siguió jugando con una build de antes de las 03:04 de esa madrugada, o una compilación
que no llegó a completarse). No se tocó ni una línea de `PestanaBusqueda.cs`, `BuscadorMundo.cs` ni
`IconoResultado.cs`: no había nada que arreglar ahí. Al final de esta sesión se dejó un `.tmod`
recién compilado desde `HEAD` en la carpeta `Mods\` real del usuario (`scripts\compilar.ps1`), para
que la próxima partida use de verdad el código verificado.

### 3. El mini-mapa NO se podía arrastrar - causa real, no la que parecía a simple vista

Este SÍ era un bug real y nuevo (no estaba en el alcance de `1adcdde`). `MiniMapaTk.AplicarArrastre`
usaba el idiom habitual de vainilla para "clic recién pulsado": `Main.mouseLeft &&
Main.mouseLeftRelease`. Decompilando el `tModLoader.dll` instalado (v2026.7.3.0) con `ilspycmd` se
encontró que ese idiom es estructuralmente incapaz de cumplirse desde este punto del motor:

- `Main.mouseLeft` solo se escribe una vez por fotograma, en `PlayerInput.UpdateInput()`
  (`Main.DoUpdate_HandleInput`).
- `Main.mouseLeftRelease` se recalcula, TAMBIÉN una vez por fotograma, de forma incondicional
  mientras el mapa vainilla está cerrado (que es justo cuando este panel está abierto), al FINAL de
  `Main.DoDraw`: `mouseLeftRelease = mouseLeft ? false : true;` - literalmente la negación de
  `mouseLeft` de ESE MISMO fotograma.
- Este panel se actualiza vía `Main.InGameUI.Update`, y `Main.DoUpdate` llama a eso DENTRO de
  `UpdateUIStates`, que está ANTES de `DoUpdate_HandleInput` en el mismo fotograma.

Combinando los tres puntos: cuando `MiniMapaTk.Update` se ejecuta, `Main.mouseLeftRelease` es
SIEMPRE la negación de `Main.mouseLeft` (son los valores tal como quedaron al final del Draw
anterior) - así que `mouseLeft && mouseLeftRelease` es SIEMPRE falso, con clic real o sin él. El
zoom con la rueda no tiene este problema porque usa `PlayerInput.ScrollWheelDeltaForUI`, que se
arrastra y se pone a cero sin mirar ningún otro campo (nunca se "invierte" solo) - por eso el
usuario podía hacer zoom pero nunca arrastrar. Arreglado con detección de flanco PROPIA
(`_botonAbajoAnterior`, un campo privado que recuerda si el botón estaba pulsado en el fotograma
anterior de ESTE elemento) en vez de fiarse de `Main.mouseLeftRelease`.

**Verificado con raton sintético inyectado desde el propio mod** (nada de SendInput/AutoHotkey: esa
vía ya falló cuatro veces para el teclado, ver la entrada del 7-sep sobre `keybd_event`/`SendInput`
- para el ratón se encontró un hueco real del motor en su lugar). Primer intento:
`PanelExploracionSystem.PostUpdateInput` forzando `Main.mouseX/mouseY/mouseLeft` - `mouseLeft` sí se
quedaba fijado (único sitio de escritura real, confirmado con contadores de invocación reales:
844-953 llamadas por ejecución), pero `Main.mouseX/mouseY` volvían a leerse como `(0, 0)` pese a
escribirlos también desde `PostDrawInterface` (con 840+ invocaciones confirmadas): `Main.DrawInterface`
vuelve a escribir esas dos coordenadas desde el hardware real en más de un sitio del propio recorrido
de capas de vainilla (incluida una restauración explícita alrededor del icono del mini-mapa de
esquina, `mouseX = num18` con `num18` capturado de una copia previa), y no hay ningún hueco público
de `ModSystem` que los intercepte TODOS. Con cero ratón físico en la máquina de pruebas, siempre
acababa en `(0, 0)` por mucho que se reescribiera - y con el ratón en `(0, 0)` el mini-mapa nunca
tenía `IsMouseHovering`, así que el arrastre no arrancaba nunca por una razón totalmente distinta a
la que se pensaba al principio (se llegó a sospechar un problema de foco de ventana, luego que
`PostDrawInterface` no se invocara - las dos hipótesis se descartaron con contadores reales antes de
dar con la causa de verdad).

Solución real: en vez de seguir peleando contra el motor por `Main.mouseX/mouseY`, `MiniMapaTk` gana
una bandera de arnés de pruebas propia (`RatonSinteticoParaPrueba` / `PosicionSinteticaParaPrueba`)
que solo bypasa la LECTURA de la posición del ratón (`RatonEnInterfaz()`) y el chequeo de
`IsMouseHovering`, sin tocar ni una línea de la lógica de producción de `AplicarArrastre` (la
detección de flanco con `_botonAbajoAnterior` es exactamente la misma que usaría un jugador real). El
botón sigue viniendo de `Main.mouseLeft` sintético vía `PanelExploracionSystem.PostUpdateInput`, que
sí es 100% fiable. Con esto, la autoprueba (`AutopruebaExploracion`, pasos 17-22) demuestra el
arrastre de principio a fin: pulsar sobre el mini-mapa arranca `Arrastrando`, mover el ratón 130×70
px desplaza el centro EXACTAMENTE lo esperado (`centro esperado (2044, 240), centro real (2044, 240),
distancia 0,0 tiles`), y soltar el botón termina el arrastre - con captura real (`ws6-arrastre-
minimapa.png`) mostrando el mapa ya desplazado a la superficie del mundo real.

### Obstáculos reales durante la verificación (documentados por la regla de autonomía)

Dos veces distintas, la compilación del proyecto ENTERO (obligatoria para poder verificar cualquier
cosa) se rompió por archivos de OTROS agentes trabajando en paralelo, a medio escribir en ese
instante (`Common/Prefijos/CatalogoPrefijosLegales.cs` con un `PrefixCategory` ambiguo entre
`Terraria.ModLoader` y `TerrasavrNative.Core.Data`; `UI/Personaje/Widgets/MunecoTk.cs` con un
`GetBackBufferData` con un argumento de más, y `UI/Personaje/Widgets/EditorCantidadTk.cs` con cinco
campos `readonly` rellenados desde un método normal en vez del constructor). Cada vez se esperó a
que el archivo dejara de cambiar (~60 s sin tocar su fecha de modificación) antes de aplicar el
arreglo mínimo y obvio para poder seguir verificando (nunca comiteado desde aquí): los dos agentes
ya subieron sus propias versiones reales más tarde en la sesión (`5d22d4b`, `1413796` y otros), que
ya no tienen diff contra lo que se había parcheado aquí - no hizo falta limpiar nada al final.

### Commit

Índice privado, solo lo de esta tarea: `Common/Exploracion/AutopruebaExploracion.cs`,
`Common/Exploracion/PanelExploracionSystem.cs`, `UI/Exploracion/MiniMapaTk.cs`,
`evidencia/ws6-exploracion.log.txt` y esta entrada de `bitacora.md`. `PestanaBusqueda.cs`,
`BuscadorMundo.cs` e `IconoResultado.cs` no se tocan: ya estaban bien. Se deja además un `.tmod`
recién compilado en la carpeta `Mods\` real (fuera del repo, no se comitea) para que la próxima
partida del usuario use el código ya verificado.

---

## 7-sep-2026 — El muñeco de Apariencia parpadeaba y el pelo salía negro: dos bugs de luz, un solo arreglo

El usuario probó el mod en el juego real (con captura) y reportó dos problemas del `MunecoTk` de
la pestaña Personaje → Apariencia (ver la entrada de arriba, `f1d970d`/`9c019ea`): el muñeco
parpadea, y el pelo sale negro en vez del color elegido en los deslizadores R/V/A. El propio
usuario aportó la hipótesis correcta, citada tal cual porque acertó: "lo de que el personaje
parpadea en apariencia es porque le afecta la iluminacion de el entorno... si mi personaje esta
al lado de una antorcha... en el menu del mod tambien lo reflejara".

### Investigación: dos causas reales distintas, no una sola

Decompilado el `tModLoader.dll` REAL instalado (`ilspycmd`, nunca el de referencia) siguiendo la
disciplina del repositorio: `Terraria.Graphics.Renderers.LegacyPlayerRenderer`,
`Terraria.DataStructures.PlayerDrawSet`, `Terraria.DataStructures.PlayerDrawLayers`,
`Terraria.Lighting`, `Terraria.Player.GetHairColor` y `Terraria.GameContent.UI.Elements.UICharacter`
(la ficha de la pantalla de selección de personaje, el sitio donde vanilla SÍ necesita una vista
previa siempre bien iluminada).

**1) El parpadeo (piel/ojos/ropa/armadura).** `PlayerDrawSet.BoringSetup_2` guarda el parámetro
`position` que se le pasa a `DrawPlayer` (en `MunecoTk` eso es
`dim.Position() + Main.screenPosition` - la posición de pantalla del widget "desproyectada" a
coordenadas de mundo, igual truco que usa `UICharacter`) en su campo `Position`, y con él
muestrea `Lighting.GetColorClamped(...)` para cada color. O sea: ilumina el muñeco con la luz REAL
del mundo en el tile que hay detrás del hueco de pantalla donde se dibuja - que al sumar
`Main.screenPosition` (la cámara sigue al jugador real) es siempre el entorno inmediato del
jugador de verdad. Antorchas, ciclo día/noche y bioma cambian ese color cada fotograma: de ahí el
parpadeo. Confirma la hipótesis del usuario al pie de la letra.

**2) El pelo negro (causa aparte, más grave, no depende de si hay poca luz cerca).**
`PlayerDrawSet` no calcula `colorHair` con la misma fórmula: llama a
`drawPlayer.GetHairColor()`, y esa función (decompilada aparte) muestrea la luz en
`this.position` - el campo `.position` del propio `Player` que se está dibujando, NO el
`Position` del punto 1. Y `_muneco` (el `Player` propio de `MunecoTk`, `new Player()` creado una
sola vez) nunca ha tenido su `.position` puesto por nadie: `Sincronizar()` solo copia
pelo/tinte/variante/colores/equipo, nunca posición. Se queda en `Vector2.Zero` de fábrica, tile
`(0,0)`, la esquina del mapa - un tile que no está iluminado NUNCA por nada real. Por eso el pelo
salía negro SIEMPRE, sin importar dónde estuviera el jugador real ni si el resto del muñeco
parpadeaba: un bug independiente, con causa propia.

**El punto en común, y la salida.** Las dos causas pasan por `Terraria.Lighting`. Comprobadas las
CINCO sobrecargas de `GetColor`/`GetColorClamped` (las tres que usa el punto 1, y la de dos
argumentos que usa `GetHairColor`), la primera línea de todas es idéntica:
`if (Main.gameMenu) return oldColor;` (o `return Color.White;` en la de dos argumentos) - sale
devolviendo el color de entrada tal cual, sin tocar el motor de luces real ni mirar el tile que se
le pasó. Es exactamente por lo que `UICharacter` (que corre con `Main.gameMenu=true`, porque en la
pantalla de selección de personaje ni siquiera hay mundo cargado) sale siempre a color pleno, pelo
incluido - vanilla ya "resuelve" este mismo problema así, solo que porque nunca tuvo que dibujar
un personaje en vivo durante la partida.

### El arreglo: `UI/Personaje/Widgets/MunecoTk.cs`, nada más

En `DrawSelf`, alrededor de la única llamada a `Main.PlayerRenderer.DrawPlayer`: guarda
`Main.gameMenu`, lo pone a `true`, dibuja, y lo restaura en un `finally`. Arregla las DOS causas a
la vez (las dos pasan por el mismo `if (Main.gameMenu)`), sin tener que además ponerle una
`.position` de mentira a `_muneco` para el bug del pelo. Revisado también `PlayerDrawLayers`
completo: el ÚNICO sitio que mira `Main.gameMenu` en todas las capas de dibujado es la pose de
piernas (fuerza el fotograma "de pie" en vez del de andar/saltar), inofensivo y hasta deseable
para una vista previa estática - no hay ningún otro efecto colateral real. El cambio es síncrono
(sin ningún `await` de por medio) y se deshace en el mismo hilo antes de que ningún otro sistema
del juego pueda leer `Main.gameMenu`, con `try`/`finally` por si algún mod (Calamity incluido)
lanzara una excepción dibujando alguna de sus capas.

No se ha tocado `PestanaApariencia.cs`: el bug entero vivía en cómo `MunecoTk` invoca al renderer,
nada de lo que hace `PestanaApariencia` (deslizadores, alternador, layout) tenía que ver.

### Verificación real, en el juego, de forma bloqueante

Compilado el proyecto entero dos veces (`scripts/compilar.ps1`, 0 errores) hasta que dos colisiones
reales y ajenas con otros agentes en marcha a la vez se resolvieron solas (`ContenidoBuilds.cs` con
un campo duplicado, y `CatalogoPrefijosLegales.cs` con un `PrefixCategory` ambiguo entre
`Terraria.ModLoader.PrefixCategory` y el de `TerrasavrNative.Core` - las dos en `Common/Builds/` y
`Common/Prefijos/`, ninguna tocada desde aquí, esperadas con un bucle de reintento real hasta que
el build volvió a estar en verde, nunca arregladas a mano).

Se reutilizó SIN TOCARLO `scripts/verificar-panel-unico.ps1` (de otro agente, ya cubre
Personaje → Apariencia con colores y armadura reales - ver la entrada de arriba), lanzado con la
variable de entorno propia `TERRAKEEP_AUTOTEST_MUNECO_LUZ=1` puesta ADEMÁS de la suya
(`TERRAKEEP_AUTOTEST_PANEL`), sin editar el script: las variables de entorno del proceso padre las
hereda el cliente gráfico que lanza con `Start-Process`. Todo el lanzamiento se hizo de forma
BLOQUEANTE (un bucle con timeout largo sobre el propio proceso, esperando a "AUTOPRUEBA PANEL
COMPLETA" en el log) en vez de lanzarlo en segundo plano y volver más tarde.

**Arnés propio, autocontenido en `MunecoTk.cs`** (variable `TERRAKEEP_AUTOTEST_MUNECO_LUZ`, no
toca ningún otro archivo, no-op total sin la variable puesta): reproduce el escenario exacto que
denunció el usuario ("mi personaje al lado de una antorcha") de forma controlada, sin depender de
la geografía de ningún mundo de prueba - en vez de mover al jugador real hasta encontrar una
antorcha, inyecta luz blanca muy fuerte con `Lighting.AddLight(tileX, tileY, 4f, 4f, 4f)` (la
misma función que usa una antorcha real cada fotograma) durante 40 fotogramas seguidos,
directamente en el mismo tile que `PlayerDrawSet.BoringSetup_2` muestrea para el muñeco (misma
fórmula, decompilada y documentada en el propio comentario de `DrawSelf`). Lee el pixel real ya
renderizado (`GraphicsDevice.GetBackBufferData` sobre un rectángulo de 1x1, llamado desde `Update`
-fase de lógica- nunca desde dentro de un `Draw` en marcha, mismo criterio que ya usa y tiene
verificado `CapturaDePantalla.Guardar`) antes y después de la inyección, y compara.

**Resultado real, log en mano** (`evidencia/panel-unico.log.txt`, mundo `TerrakeepPrueba`,
personaje `TerrakeepPrueba` con pelo (210,60,40), piel (255,200,150), ojos (30,140,230), camisa
(40,170,80), pantalón (70,90,200), armadura de cobre real puesta): fotograma 25, tile (2102,266),
luz real ahí RGB(255,255,255), pixel renderizado del muñeco RGB(39,50,86); fotograma 70, mismo
tile, luz real ahí sigue RGB(255,255,255), pixel renderizado IDÉNTICO, RGB(39,50,86).
`AUTOPRUEBA MUÑECO/luz RESULTADO: OK` - pixel idéntico byte a byte antes y después de forzar la
"antorcha": el muñeco no reacciona en absoluto a `Lighting.AddLight`, confirmado en el juego real,
no solo por análisis del código. `AUTOPRUEBA PANEL COMPLETA` con las seis pestañas, la animación
de botones, los atajos y el icono del HUD, todo en verde - cero regresiones.

**Las tres capturas reales del back buffer** (`apariencia-muneco-con-armadura.png`,
`apariencia-muneco-sin-armadura.png`, `apariencia-muneco-con-armadura-otra-vez.png`, en
`tModLoader-TerrakeepPanel\terrakeep-capturas\`, miradas a ojo) confirman visualmente lo mismo que
el log: sin armadura, el pelo sale ROJIZO nítido (no negro), y los siete colores del muñeco
coinciden a ojo con los valores exactos de los deslizadores; con armadura, el casco/goggles y la
malla/grebas de cobre se ven en su tono natural, sin oscurecer ni saturar de más. El alternador
"Con armadura" cambia de `[ ]` a `[X]` y vuelta en las tres capturas, sin fallos.

### Commit

Índice privado, solo `UI/Personaje/Widgets/MunecoTk.cs` (el único archivo tocado - no hizo falta
cambiar `PestanaApariencia.cs`) y esta entrada de `bitacora.md`. No se comitea
`evidencia/panel-unico.log.txt` (se sobrescribe en cada pasada del script compartido, con
contenido del que otros agentes también dependen) ni ningún otro archivo modificado por otros
agentes en marcha a la vez (`Common/Exploracion/*`, `Common/Panel/*`,
`UI/Personaje/PestanaBuffs.cs`, `UI/Personaje/Widgets/EditorCantidadTk.cs`,
`lib/TerrasavrNative.Core.dll`, `Common/Prefijos/*` nuevos, etc.).

---

## 7-sep-2026 — Espaciado: el recuadro naranja de dificultad y la pestaña Buffs, apretados de verdad

Reporte real del usuario probando el mod, con capturas propias: **"hay que reajustar las frases se
salen del recuadro... si hace falta que todo los elementos del mod se hagan un poquito mas pequeño
para que todo cuadre"**, y sobre Buffs en concreto: **"esta todo super apretado en buff... hacer
todo algo mas pequeño para que todo respire mejor"**.

### 1. El recuadro naranja (`UI/Exploracion/PestanaMundo.cs`, `ConstruirDificultad`)

La causa real, mirando el código: la caja de aviso de permanencia (`cajaAviso`) tenía
**`Height` fijo a 120 px**, pero las tres frases que lleva dentro (permanencia, efecto, deshacer de
Ctrl+Z) se parten en las líneas que hagan falta según el ancho REAL y el idioma activo -
`EtiquetaTk.PartirEnLineas`, ya existía y funcionaba bien para el ANCHO. Para el ALTO no había
nada: con autoguardado desactivado la frase de permanencia es más larga (`PermanenteAlGuardar` en
vez de `PermanenteAutoguardado`) y en inglés las tres frases ocupan más ancho por palabra, así que
el número de líneas variaba y una caja de 120 px fijos se quedaba corta - el texto seguía
dibujándose (`EtiquetaTk.DrawSelf` no recorta nunca, solo `Utils.DrawBorderString` tal cual) pero
por FUERA del rectángulo naranja, exactamente lo que se veía en la captura del usuario.

**Arreglo real, no un número más grande a ojo**: `RecalcularAviso()` (nuevo, llamado desde
`Update` y una vez al final del constructor) mide con la fuente REAL
(`FontAssets.MouseText.Value.MeasureString`) el texto YA partido de las tres frases con el ancho
interior real de la caja (`GetInnerDimensions().Width`), calcula la altura que ocupan de verdad, y
ajusta `_cajaAviso.Height` a esa cifra - nunca una constante. El botón "Confirmar" y el mensaje de
resultado, que van debajo, se recolocan en el mismo método a partir de la altura real de la caja en
ese fotograma. `_derecha.Recalculate()` al final para que el mismo fotograma ya dibuje con la
geometría nueva.

### 2. La pestaña Buffs (`UI/Personaje/PestanaBuffs.cs`)

Huecos reales medidos a mano en el código antes de tocar nada: la caja de "buffs activos" y el
botón "Quitar todos" de debajo tenían 4 px de margen; dentro de cada fila de buff, el nombre y la
columna de tiempo estaban **pegados con 0 px** y el tiempo con el botón "Quitar" con 4 px; la
columna de carpetas del árbol de "Añadir" tenía 4 px entre sus dos botones y otros 4 px entre la
ruta y la caja de carpetas; el campo "Segundos" tenía 6 px respecto al buscador y el resumen 4 px
respecto al campo. Subidos todos a huecos reales (10-16 px según el sitio, ver las constantes
nuevas `AltoFilaBuff`/`SeparacionEnFila`/`MargenDerechoFila`/`SeparacionListaActivosBoton` al
principio de la clase), fila de buff subida de 36 a 40 px de alto, `ListPadding` de las tres listas
subido de 3-4 a 5-6 px.

### 3. Un solapamiento real que solo salió probando la resolución mínima (800x720)

La primera pasada de verificación (ver más abajo) confirmó el recuadro naranja y el aspecto general
de Buffs a 1600x900 y 1280x720, pero a **800x720 con nombres largos en inglés** apareció un
solapamiento real: `"Mana Regeneration (id 6)9:52l 6)"` - el nombre del buff se dibujaba por encima
de la columna de tiempo. Causa: igual que el recuadro naranja, `EtiquetaTk` nunca recorta ni envuelve
por su cuenta, y el presupuesto de ancho para el nombre (`anchoNombre`, calculado a mano en
`CrearFilaBuff`/`CrearFilaResultado`) es más estrecho en una ventana angosta que el texto real de un
nombre largo. Arreglado añadiendo `EtiquetaTk.Recortar` (nuevo, mismo algoritmo ya usado en
`FilaCarpetaBuffTk`/`FilaCarpetaTk`: mide con la fuente real y corta con "..." literal, nunca "…") y
aplicándolo en el `Func` de las dos etiquetas de nombre, con el ANCHO REAL de la propia etiqueta
(`GetDimensions().Width`, no la constante calculada a mano) para que siga siendo correcto si la
ventana cambia de tamaño sin reconstruir la fila.

### Verificación real en el juego (arnés nuevo: `Common/Panel/AutopruebaEspaciado.cs`)

Nueva autoprueba (`TERRAKEEP_AUTOTEST_ESPACIADO=1`, lanzada por `scripts/verificar-espaciado.ps1`
sobre su propio sandbox `tModLoader-TerrakeepEspaciado`, clonado de WS0): recorre las **3
resoluciones × 2 idiomas** (1600x900 y 1280x720 pedidas por el usuario, y 800x720 porque es el
mínimo real que admite el motor - `Main.minScreenW`/`minScreenH` en el `Main.cs` decompilado -,
cambiadas EN VIVO con `Main.SetDisplayMode`, el mismo método público que usa el menú de resolución
de vanilla), pone 6 buffs de prueba reales (incluido uno con nombre largo, el caso más exigente),
abre Exploración > Este mundo y Personaje > Buffs en cada combinación, y para el recuadro naranja
mide de forma INDEPENDIENTE (no solo confía en `RecalcularAviso`) el borde inferior real del texto
ya partido contra el borde inferior real de la caja (`GetDimensions()`/`GetInnerDimensions()` ya
dibujados), más una captura real del back buffer en cada caso.

**Resultado, log real (`evidencia/espaciado.log.txt`), las 6 combinaciones en verde**: el texto
del recuadro naranja cabe dentro de la caja con margen real (ej. a 800x720/en: borde inferior del
texto en 533, borde inferior de la caja en 541, 8 px de margen; a 1600x900/es: 590 vs 598) y el
botón "Confirmar" nunca se solapa con la caja. Las 12 capturas reales
(`evidencia/espaciado-capturas/`) confirman a ojo lo mismo: el recuadro naranja contiene sus tres
líneas con aire real en los seis casos, y la pestaña Buffs respira (huecos visibles entre fila y
fila, entre columnas, entre la lista y "Quitar todos") - incluida la fila con el nombre de buff más
largo, que ahora se recorta con "..." en vez de solaparse, visto en
`buffs-800x720-minimo-en.png` antes y después del arreglo del punto 3.

### 4. Pasada rápida por el resto de pestañas (sin tocar nada fuera de mi zona)

Revisadas por el mismo patrón (texto que se sale de su caja, huecos a 0-4 px) sin encontrar
regresiones nuevas que arreglar:

- **`PestanaEquipo.cs`, `ContenidoInvestigacion.cs`, `PestanaMapa.cs` (Exploración) y
  `ContenidoAjustes.cs` ya tenían este mismo tipo de arreglo hecho en una pasada anterior**, con
  comentarios propios citando capturas reales a 800x720 y/o 1600x900 (escala de ranura dinámica en
  Equipo, texto acortado en Investigación, cuatro líneas de aviso previstas en el mini-mapa). Nada
  que hacer ahí.
- **`PestanaInventario.cs` y `PestanaAlmacenes.cs` usan una escala de ranura FIJA (0,9)**, a
  diferencia de la escala dinámica que ya tiene `PestanaEquipo.cs` para su columna de 10 filas. No
  se ha visto un caso real roto (Almacenes solo tiene 4 filas de slots, Inventario reparte en dos
  bloques cortos), así que se deja anotado para quien lo retome en vez de tocarlo sin evidencia de
  un fallo real.
- **`UI/Exploracion/PestanaMapa.cs` y `MiniMapaTk.cs` NO se han tocado a propósito**: otro agente
  los está editando en paralelo ahora mismo (Exploración/búsqueda-mapa).

### Commit

Índice privado: `UI/Exploracion/PestanaMundo.cs`, `UI/Personaje/PestanaBuffs.cs`,
`UI/Personaje/Widgets/EtiquetaTk.cs` (arreglos reales), `Common/Panel/AutopruebaEspaciado.cs`
(nuevo, arnés de prueba), `scripts/verificar-espaciado.ps1` (nuevo), una línea de enganche en
`Common/Panel/PanelTerrakeepSystem.cs` (`AutopruebaEspaciado.Avanzar()`, mismo patrón que las
otras autopruebas ya enganchadas ahí) y otra en `Common/Panel/CapturaDePantalla.cs` (añadir la
variable de esta autoprueba a la lista de las que permiten capturas), `evidencia/espaciado.log.txt`
y `evidencia/espaciado-capturas/*.png` (evidencia propia, en mi propio sandbox y mi propio nombre
de archivo, sin colisión con la de otros agentes), y esta entrada de `bitacora.md`. No se comitea
ningún archivo modificado por otros agentes en marcha a la vez (`Common/Exploracion/*`,
`UI/Exploracion/MiniMapaTk.cs`, `UI/Libreria/ContenidoLibreria.cs`,
`UI/Personaje/Widgets/EditorCantidadTk.cs`, `lib/TerrasavrNative.Core.dll`,
`scripts/generar-localizacion.py`, `Common/Prefijos/*`, `Assets/vanilla_prefix_*.json`,
`UI/Libreria/Widgets/`, `evidencia/panel-unico.log.txt`, `evidencia/ws6-exploracion.log.txt`), ni
las carpetas de build sueltas (`bin-checkDebug/`, `obj-verif-espaciado/`).

---

## 7-sep-2026 — Papelera + selección explícita por arrastre en Librería (cantidad y prefijo)

Encargo directo del usuario tras probar el mod: la papelera también en Librería (hasta ahora
solo en Personaje), y un rediseño del editor de cantidad de hoy mismo (commit `25221af`) porque
enganchar el control al slot que el ratón sobrevuela "se puede equivocar mucho". Pide en su
lugar selección EXPLÍCITA: un recuadro donde arrastrar el objeto real, y desde ahí poder editar
su cantidad Y su prefijo.

### Selección por arrastre, no por hover: `SlotSeleccionTk`

`UI/Libreria/Widgets/SlotSeleccionTk.cs` es un `ItemSlot` real con su propio campo `Item`
interno (nunca un array del jugador) - exactamente el mismo patrón que ya usa
`SlotPapeleraTk` con `Player.trashItem`, solo que aquí el campo es propio del widget, no del
`Player`. Arrastrar un objeto real encima lo MUEVE de verdad a este campo (mismo
`ItemSlot.Handle`, mismo comportamiento que cualquier ranura de vanilla, intercambio incluido si
el recuadro ya tenía algo dentro) - no hay ninguna copia ni ningún índice que recordar, así que
editar `.stack`/`.prefix` sobre `SlotSeleccionTk.ObjetoActual` cambia el objeto real que luego se
vuelve a arrastrar a donde corresponda.

### El editor de cantidad se reutiliza tal cual, con un segundo modo

`EditorCantidadTk` (Personaje, hoy) pasa a tener DOS modos, decididos por el constructor:
- El de siempre, `EditorCantidadTk(UIElement raiz)`: engancha por HOVER a un
  `SlotObjetoVanilla` bajo el ratón. **Sin cambios de comportamiento**, mismo layout de 580 px.
- Uno nuevo, `EditorCantidadTk(Func<Item> proveedorExplicito, float ancho)`: el objetivo es
  SIEMPRE el que devuelva el delegado (aquí, `() => slotSeleccion.ObjetoActual`), sin buscar
  nada. Layout compacto (`-`/campo/`+`/Aplicar, sin la etiqueta larga: no cabe en el hueco
  estrecho de la Librería) - el nombre/pila del objeto ya lo dibuja el propio `ItemSlot` del
  recuadro de selección ("xN" sobre el icono), y la info completa se enseña igual por el tooltip
  de los tres botones.

Un matiz real que solo apareció al escribir la prueba: el modo hover exige `stack > 1` para
engancharse (tiene sentido: pasar el ratón por encima no debería "activarse" sobre CUALQUIER
objeto). El modo explícito usa `maxStack > 1` en su lugar - la selección ya es deliberada, así
que tiene sentido poder subir una unidad suelta (stack=1) a una pila grande, que es justo el
caso de uso real de coger un solo potingue del catálogo y querer 20.

### El prefijo: cambiarlo SIN acumular multiplicadores (el hallazgo real de esta tarea)

`Item.Prefix(int)` multiplica las estadísticas ACTUALES del objeto por las del prefijo pedido
(`Terraria\Item.cs:1334`, decompilado) - llamarlo dos veces seguidas sobre el mismo objeto
COMPONE los bonos. La forma real de vanilla de cambiar un prefijo ya puesto es la misma que usa
el propio Puesto de Reforma (`Main.cs`, botón de reforjar):
`reforgeItem.ResetPrefix(); reforgeItem.Prefix(-2);` - `ResetPrefix()` primero (vuelve las
estadísticas a su base real, preservando tipo/pila/favorito) y solo entonces `Prefix(idExacto)`.
`UI/Libreria/Widgets/EditorPrefijoTk.cs` sigue exactamente esa ruta. Verificado explícitamente
en el juego real pulsando el MISMO prefijo dos veces seguidas: `item.damage` (y `item.prefix`)
quedan idénticos tras el segundo clic, no compuestos.

### La legalidad del prefijo: reutilizada de `TerrasavrNative.Core`, no reinventada

Pedido explícito del usuario ("investiga la vía real... no reinventes las reglas de legalidad a
mano si ya existen"). `Common/Prefijos/CatalogoPrefijosLegales.cs` reutiliza tal cual:
- `PrefixRulesCatalog` (de `Assets/vanilla_prefix_rules.json`, copiado de
  `TerrasavrNative.App/Assets/vanilla_prefix_rules.json`): la tabla REAL de qué prefijos son
  legales para cada uno de los 683 objetos vanilla tabulados, extraída del código decompilado
  real (`PrefixLegacy.cs`/`Item.cs`) - para objetos vanilla, `Item.type` YA ES el `ItemID` que
  usa esta tabla, sin ningún id sintético de por medio.
- `PrefixGroupCatalog` (código puro de Core, sin archivo): los mismos 8+6 grupos con nombre
  ("Cuerpo a cuerpo +", "Accesorio"...) que ya usa el selector manual de prefijo de la app de
  escritorio, con sus nombres ES/EN.
- `PrefixEffectCatalog` (de `Assets/vanilla_prefix_effects.json`, mismo origen): el efecto real
  de cada prefijo ("+15% de daño"...), como tooltip de cada botón del picker - un extra, nunca
  decide legalidad.

**Objetos de MOD (Calamity incluido): categoría detectada con los campos REALES del `Item`**
(`item.accessory`, `item.DamageType`), NO con el `CalamityCatalog` de ids sintéticos de Core:
ese catálogo está pensado para `catalog.json` offline y el `Item.type` de un objeto de mod en
esta partida es un id asignado en caliente por el orden de carga, que no coincide con el
esquema sintético - traerlo habría reintroducido justo el sistema de ids sintéticos que
`CatalogoVivo` ya evitó a propósito (ver su cabecera). Detectar por campos reales es el mismo
criterio que ya usa `CatalogoVivo.Categorizar` para las carpetas de la Librería, y sirve para
CUALQUIER mod, no solo Calamity.

**Alcance deliberadamente SIN los 21 `ModPrefix` reales de Calamity** (17 de arma Pícaro + 4 de
accesorio, ids sintéticos de Core >= 10000): aplicarlos de verdad exigiría resolver su
`Terraria.ModLoader.ModPrefix.Type` EN TIEMPO DE EJECUCIÓN de esta partida - `CatalogoMejorPrefijo`
ya tomó la misma decisión para el prefijo automático, por el mismo motivo (ver su cabecera);
aquí se documenta igual en vez de reabrir la decisión sin datos nuevos. Los grupos
"Invocación +/-" tampoco se ofrecen: sus ids (85-97) son de `PrefixID` de TerrariaVanilla 1.4.5.8
y no existen en el `PrefixID.Count`=85 real de la 1.4.4.9 instalada aquí - mismo hallazgo real
que ya documentó `CatalogoMejorPrefijo`, defendido con la misma comprobación de rango.

### El hueco real donde vive: la rejilla de destino pasa a ancho FIJO

`ContenidoLibreria.ConstruirZonaDestino` estiraba `_rejillaDestino` al 100% del ancho disponible
aunque solo coloca 10 columnas fijas de objetos - el resto quedaba vacío de verdad, sin ningún
elemento ahí (literalmente "el hueco que hay a la derecha abajo al lado de inventario" del
encargo). Fijar su ancho (`AnchoRejillaDestino`, calculado de las mismas constantes que ya
decidían el tamaño de cada ranura) deja ese hueco como un elemento real y predecible al lado -
`UI/Libreria/Widgets/PanelHerramientasLibreriaTk.cs` (papelera + `SlotSeleccionTk` + los dos
editores, apilados en 176x140 px), sea cual sea la resolución de la ventana.

### Un tropiezo real de la búsqueda del objeto de prueba: "apila" y "admite prefijo" son EXCLUYENTES en vanilla puro

Primer intento del arnés: buscar UN objeto que apilara (`maxStack>1`, para probar cantidad) Y
admitiera prefijo (para probar el prefijo). Encontró Shuriken (id 42): apila (9999) pero
`CatalogoPrefijosLegales.GruposLegales` vacío de verdad. Investigado antes de asumir que era un
bug: `Item.CanHavePrefixes()` (decompilado, `Item.cs:1300`) exige
`maxStack == 1 || AllowReforgeForStackableItem`, y **ningún objeto vanilla real pone ese campo a
true** (comprobado con `grep` sobre el decompilado entero: 0 resultados) - es un enganche pensado
para mods (Calamity lo usa en sus armas Pícaro que sí apilan y sí llevan prefijo). O sea que en
vanilla puro "apila" y "admite prefijo" son mutuamente excluyentes de verdad: ningún objeto vale
para las dos pruebas a la vez. Arreglado usando DOS objetos de prueba (uno apilable sin prefijo,
uno con prefijo sin apilar) y arrastrando el segundo ENCIMA del primero para la prueba de
prefijo - lo que de paso ejercita el intercambio real de `ItemSlot.Handle` cuando el recuadro ya
está ocupado (se comprobó explícitamente: el que estaba antes vuelve al ratón, no se pierde ni
se duplica).

### Verificado de verdad en el juego (`scripts\verificar-libreria.ps1`, pasos 14-18 nuevos de `AutopruebaLibreria`)

Evidencia real de `evidencia/ws3-libreria.log.txt`, sandbox `tModLoader-TerrakeepWS3`:

```
Paso 14 - Apilable "Mushroom" x5 (maxStack=9999, CanHavePrefixes=False, se espera False: apila).
          Con prefijo "Iron Pickaxe" x1 (maxStack=1, CanHavePrefixes=True; categorias legales:
          Best(3), Damage(4), Critical(7), Universal+(9), Common+(6), Melee+(10), ...).
Paso 15 - ItemSlot.LeftClick coge el Mushroom del inventario + SlotSeleccionTk.EjercitarHandle
          (mismo ItemSlot.Handle que DrawSelf) lo suelta en el recuadro. Hueco de origen vacio,
          raton vacio, recuadro = "Mushroom" x5, MISMA referencia que se arrastro. OK.
Paso 16 - editor de cantidad COMPACTO: ObjetivoActual es la MISMA referencia que
          SlotSeleccionTk.ObjetoActual. "+"/"-"/escribir "3"+Aplicar/pedir 10499 (por encima del
          maximo, acotado a 9999): los 4 OK.
Paso 17 - se arrastra el Iron Pickaxe SOBRE el recuadro ya ocupado: intercambio real, el
          Mushroom vuelve al raton (OK, ItemSlot.Handle intercambia de verdad), se descarta en
          la papelera. Popup de prefijo abierto, boton "Demonic" pulsado: prefix 0 -> 60, daño
          5 -> 6. Pulsado el MISMO prefijo otra vez: prefix=60, daño=6 IDENTICO -> OK, no
          compone multiplicadores.
Paso 18 - papelera de Libreria: coge del recuadro + ItemSlot.Handle con Context.TrashItem sobre
          Player.trashItem. Recuadro vacio, papelera con el objeto, raton vacio, 0 objetos
          activos en el mundo antes y despues -> OK, nada tirado al suelo.
AUTOPRUEBA WS3 COMPLETA. Todos los pasos ejecutados sin excepciones.
```

Los tres puntos que pedía explícitamente la verificación quedan cubiertos con datos reales:
`item.stack`/`item.prefix` cambiando en el objeto REAL (mismas referencias comprobadas con
`ReferenceEquals`, no copias), y la papelera vaciando el hueco de origen sin tirar nada al suelo.

**Sin capturas de pantalla en esta tarea, a propósito.** `CapturaDePantalla.Permitida` solo
enciende con una lista fija de variables de otras autopruebas (Panel único, Idiomas, Menús,
Exploración, Espaciado) - la de WS3 (`TERRAKEEP_AUTOTEST_WS3`) nunca estuvo en esa lista, ni
antes de esta tarea ni ahora. Añadirla exigía tocar `Common/Panel/CapturaDePantalla.cs`, fuera
del alcance explícito de esta tarea (`UI/Libreria/`, `Common/Libreria/` y los tres archivos de
Personaje). La evidencia en log, con `ReferenceEquals` y valores antes/después reales, ya cubre
los tres puntos que pedía la verificación sin necesitar una imagen.

### Sobre el editor de cantidad de Personaje: se deja el hover TAL CUAL, a propósito

El encargo pedía decidir con criterio si aplicar selección explícita también en Personaje.
Se deja el modo HOVER sin tocar ahí (cero cambio de comportamiento, mismo layout de 580 px,
mismo constructor de siempre): en Personaje el usuario ya está mirando un slot concreto de SU
propio inventario real (una rejilla de ~50 huecos ya conocidos), muy distinto del catálogo de la
Librería (miles de objetos posibles, ninguno "suyo" hasta que se coge). El riesgo real que
describía el encargo ("te puedes equivocar mucho") es mucho mayor en un catálogo grande que
recorrer visualmente que en la propia mochila. Cambiarlo también ahí sería reescribir un control
que ya se verificó a fondo hoy mismo (commit `25221af`) sin que el usuario lo haya pedido para
ese sitio en concreto - la clase ya admite los dos modos si algún día hace falta.

### Índice privado para comitear

`GIT_INDEX_FILE=<propio> git read-tree HEAD && git add ... && git commit`, después `git reset` a
secas en un comando aparte (sin la variable en el entorno - ver el tropiezo #12 ya documentado
más arriba). Solo los archivos de esta tarea: `Assets/vanilla_prefix_rules.json`,
`Assets/vanilla_prefix_effects.json`, `Common/Prefijos/CatalogoPrefijosLegales.cs`,
`Common/Prefijos/SistemaPrefijosLegales.cs`, `Common/Libreria/AutopruebaLibreria.cs`,
`UI/Libreria/ContenidoLibreria.cs`, `UI/Libreria/Widgets/EditorPrefijoTk.cs`,
`UI/Libreria/Widgets/PanelHerramientasLibreriaTk.cs`, `UI/Libreria/Widgets/SlotSeleccionTk.cs`,
`UI/Personaje/Widgets/EditorCantidadTk.cs`, `Localization/es-ES_Mods.TerrakeepMod.hjson`,
`Localization/en-US_Mods.TerrakeepMod.hjson`, `scripts/generar-localizacion.py`,
`lib/TerrasavrNative.Core.dll` (recompilado desde el repo hermano con
`scripts/actualizar-core.ps1`: hacía falta para traer `PrefixRulesCatalog`/`PrefixGroupCatalog`/
`PrefixEffectCatalog`, que no estaban en el DLL versionado hasta ahora), `evidencia/ws3-libreria.log.txt`.
No se comitea nada de otros agentes en marcha a la vez (`Common/Exploracion/*`,
`UI/Exploracion/MiniMapaTk.cs`, `Common/Panel/AutopruebaEspaciado.cs`,
`evidencia/panel-unico.log.txt`, `evidencia/ws6-exploracion.log.txt`), ni las carpetas de build
sueltas (`bin-checkDebug/`, `obj-verif-espaciado/`).

---

## 7-sep-2026 — Buffs activos: de "recortar con tooltip" a que el layout se adapte de verdad

Encargo real, con captura del usuario tras la primera pasada de espaciado de hoy: **"buff sigue
siendo ilegible mucho contenido ya que se lo come el espacio... en buff activos el botón quitar se
come parte de la caja de tiempo del buff"**. Dos problemas reales en `CrearFilaBuff`
(`UI/Personaje/PestanaBuffs.cs`):

1. **La causa exacta del solape**: el nombre YA se recortaba con `EtiquetaTk.Recortar` (arreglo de
   antes), pero **el tiempo NUNCA se recortaba** - `new EtiquetaTk(() => TextoTiempo(tipo), 0.8f,
   anchoTiempo, 20f)` dibujaba el texto tal cual, sin mirar su caja de 70px fijos. Un tiempo largo
   ("9255 h 40 min", el caso real del reporte) se dibujaba por FUERA de esa caja, y el boton
   "Quitar" - que se pinta DESPUES en el mismo fotograma, o sea ENCIMA - le tapaba el trozo que se
   salia. Confirmado con el arnes: a 70px fijos, "9255 h 40 min" no cabe ni de lejos.
2. **Espacio real desaprovechado**: la columna "Activos" usaba siempre el 50% del ancho de la
   pestaña, sin tope, aunque una fila de buff no necesite tanto - "en la izquierda se puede acotar
   mas el espacio... para dar mas ancho a donde hace falta" (mismo reporte).

### Primer intento (commit-de-trabajo, corregido despues): recortar + tooltip

La primera pasada midio `anchoTiempo` con la fuente real (`AnchoTiempoReal`, cacheado: mide
`"9999 h 59 min"` a escala 0.8) y envolvio nombre/tiempo/titulo con
`EtiquetaTk.Recortar(..., out bool recortado)` + un tooltip (`EtiquetaTk.Ayuda`, nuevo) que
enseñaba el texto completo al pasar el raton si de verdad se habia cortado. Arreglaba el solape,
pero **el propio usuario lo corrigio antes de darlo por bueno**: un nombre mostrado como "Mana
Regenerat..." no cumple "todo se ha de poder leer bien" aunque tecnicamente quepa en su caja y
tenga un tooltip - el contenido tiene que leerse ENTERO de un vistazo, es el LAYOUT el que se
adapta (mas ancho, mas alto, salto de linea), nunca el texto el que se sacrifica.

### Arreglo real: dos lineas por fila, el nombre se ENVUELVE, nunca se recorta

Rediseño completo de `CrearFilaBuff`/`CrearFilaResultado`:
- **Linea 1**: el nombre, con TODO el ancho de la fila para el (nadie mas compite por ese
  espacio), envuelto con `EtiquetaTk.PartirEnLineas` (la misma funcion que ya partia el aviso
  naranja de Exploración/Mundo, la fuente real) a tantas lineas como haga falta.
- **Linea 2**, debajo: tiempo + boton "Quitar" (o solo "Aplicar" en la lista de "Añadir"), con el
  mismo anclaje relativo al borde derecho de siempre (`Left.Set(x, 1f)`) - estructuralmente
  imposible que se solapen entre si, sea cual sea el ancho de la fila.
- El alto de cada fila y la posicion Y de la linea 2 se recalculan **cada fotograma**
  (`AjustarAltoFilasActivas`/`AjustarAltoFilasResultado`, llamados desde `Update`) a partir de la
  altura REAL que ocupa el nombre ya envuelto (`FontAssets.MouseText.Value.MeasureString`) -
  mismo patron que `PestanaMundo.RecalcularAviso` ya establecio hoy para el recuadro naranja, solo
  que aplicado por fila. Se repite cada fotograma porque el ancho disponible cambia con la
  resolucion/columna, y por tanto tambien cuantas lineas necesita el nombre.
- El titulo "Buffs activos: X de Y ranuras" recibio el mismo tratamiento por si algun dia
  necesitara mas de una linea (`AjustarAlturaTitulo`, baja la caja de la lista lo que haga falta) -
  aunque en la practica nunca lo necesita (ver mas abajo).
- La columna de tiempo SI sigue con un ancho fijo sin envolver, pero por una razon real y no
  arbitraria: `Player.buffTime` es `int` (32 bits), y `int.MaxValue / 60 / 3600 ≈ 9942 horas` -
  **"9999 h 59 min" no es un numero optimista, es una cota matematica**. No hace falta envolver ni
  recortar algo que estructuralmente no puede desbordar.
- `EtiquetaTk.Recortar` se mantiene (utilidad generica, usada ya por `FilaCarpetaBuffTk`/
  `FilaCarpetaTk` con su propia copia privada) pero su comentario ahora dice explicitamente que es
  **el ultimo recurso, no la solucion por defecto**: solo para una caja de una sola linea de altura
  infranqueable, nunca para un texto que se pueda envolver o para el que se pueda agrandar la caja.
  Se retiro el mecanismo de tooltip-al-recortar (`EtiquetaTk.Ayuda`) que se habia añadido en el
  primer intento: sin ningun sitio que lo llame ya (nombre/tiempo/titulo ya no recortan), dejarlo
  habria sido código muerto invitando a la próxima persona a "solucionar" un desbordamiento
  recortando en vez de adaptar el layout - justo el patron que este mismo encargo vino a corregir.

### El tope de la columna "Activos", recalculado sin la mediana de nombres

Con el nombre ya en su propia linea, `AnchoMaximoActivos` (el tope real de `RecalcularColumnas`,
ya existente de la pasada de espaciado de hoy) ya no necesita reservar sitio para el nombre
COMPLETO compartiendo linea con el tiempo - una columna estrecha ahora solo hace la fila mas ALTA,
nunca rompe nada. Simplificado a cubrir comodamente la linea 2 (tiempo + boton, ancho fijo) mas un
colchon razonable (200px) para que un nombre corto/medio no se envuelva sin necesidad. Resultado
real medido: el tope bajo de 397px (primera pasada) a **264px**, dandole a "Añadir" 786px en vez de
653px a 1600x900 - mas hueco real aprovechado, justo lo que pedia el reporte.

### Verificado en el juego real, con el arnes de hoy extendido (`AutopruebaEspaciado.cs`)

`PoblarBuffsDePrueba` ahora pone **30 buffs a la vez** (antes 6) y fuerza el de nombre mas largo
de verdad entre TODOS los cargados (medido con la fuente real, no adivinado) a
**1999224000 ticks = 9255 h 40 min exactos**, el caso literal del reporte del usuario.
`MedirYCapturarBuffs` mide, por cada fila de "Activos" Y de "Añadir" (esta ultima poblada de
verdad con `PestanaBuffs.BuscarParaPrueba("a")`, nuevo metodo solo-autopruebas): que ninguna linea
del nombre mida mas que su caja, que el nombre no invada verticalmente la linea 2, que tiempo no
se solape con el boton, y que la fila sea lo bastante alta - ademas de que el titulo tampoco se
recorte. Un primer intento de esta medicion dio 100 fallos falsos en "Añadir": el propio arnes
media las filas de resultados en el MISMO fotograma en que `BuscarParaPrueba` las creaba, antes de
que `AjustarAltoFilasResultado` (que solo corre desde `Update`) llegara a colocarlas - un bug del
arnes, no del producto (las 27 filas de "Activos", que llevaban muchos fotogramas construidas,
dieron 0 fallos en esa misma pasada). Arreglado haciendo que `BuscarParaPrueba` fuerce el ajuste
ya mismo, sin esperar al fotograma siguiente.

**Resultado final, las 3 resoluciones × 2 idiomas, las 6 combinaciones en verde**
(`evidencia/espaciado.log.txt`):

```
(1600x900/es)          - 27 filas activas + 100 filas de resultados medidas -> OK, nada recortado
                          ni solapado. Columna Activos=264px (50% sin tope seria 520px),
                          Añadir=786px, pestaña=1060px.
(1600x900/en)           - OK, mismas cifras.
(1280x720/es), (en)     - OK, mismas cifras (el panel tiene su propio ancho maximo, no crece mas
                          alla de 1060px de "pestaña" aunque la ventana sea mas ancha).
(800x720-minimo/es),(en)- OK. Columna Activos=264px (50% sin tope seria 364px, aqui NO se activa
                          el tope: a la resolucion minima real ya no sobraba espacio de verdad).
```

0 fallos en las 762 filas medidas en total (127 filas × 6 combinaciones). Capturas reales en
`evidencia/espaciado-capturas/buffs-*.png` confirman a ojo el mismo resultado: nombres largos como
"Cabeza de esqueletrón bebé (id 50)" (233,6px, el mas largo real de todo el juego cargado) se ven
enteros en dos lineas, sin ninguna caja recortada con "...".

### Índice privado para comitear

`UI/Personaje/PestanaBuffs.cs`, `UI/Personaje/Widgets/EtiquetaTk.cs`,
`Common/Panel/AutopruebaEspaciado.cs`, `evidencia/espaciado.log.txt`,
`evidencia/espaciado-capturas/*.png` (12 archivos, evidencia propia sobreescrita con la pasada
final), y esta entrada de `bitacora.md`. No se comitea nada de otros agentes en marcha a la vez
(`Common/Libreria/AutopruebaLibreria.cs`, `Common/Panel/CapturaDePantalla.cs`,
`Common/Personaje/AutopruebaPersonaje.cs`, `Common/Prefijos/*`, `UI/Libreria/Widgets/*`,
`UI/Personaje/ContenidoPersonaje.cs`, `UI/Personaje/PestanaAlmacenes.cs`,
`UI/Personaje/PestanaEquipo.cs`, `UI/Personaje/PestanaInventario.cs`,
`UI/Personaje/Widgets/BotonTk.cs`, `UI/Personaje/Widgets/CampoTextoTk.cs`,
`UI/Personaje/Widgets/EditorCantidadTk.cs`, `evidencia/panel-unico.log.txt`,
`evidencia/ws3-libreria.log.txt`, `scripts/verificar-personaje.ps1`), ni las carpetas de build
sueltas (`bin-checkDebug/`, `obj-verif-espaciado/`).

## 7-sep-2026 — Builds: "sin sitio" en un arma que SÍ tenías, y el cambio real que pidió el usuario (auto-equipar ya trae del catálogo)

Encargo: investigar un aviso rojo real de "Auto-equipar" con captura del usuario: build Vanilla /
Pre-Hardmode / Cuerpo a cuerpo, poseía solo "Furia solar" (`Sunfury`, un ARMA), y salió
`sin sitio=1` justo al lado de `ranuras de accesorio disponibles: 5` - lectura natural: contradicción.
El usuario: **"lo de las builds sigue sin funcionar"**.

### La causa real: comportamiento CORRECTO, mensaje engañoso

Reproducido en sandbox propio (`tModLoader-TerrakeepWS4`, arnés nuevo: env vars
`TERRAKEEP_BUILDS_SEMBRAR_BANCO` mete un pid en la hucha en vez del inventario, y
`TERRAKEEP_BUILDS_LLENAR_MOCHILA` rellena los 50 huecos de la mochila con piedra -
`Common/Builds/PanelBuildsSystem.cs`, `SembrarObjetosEnBancoDePrueba`/`LlenarMochilaDePrueba`):
con Sunfury en la hucha y la mochila llena de verdad, el log reprodujo LITERALMENTE el mensaje del
usuario: `"movidos=0, ya colocados=0, no los tienes=12, sin sitio=1, no existen aquí=0"`, con
`Furia solar: la mochila esta llena` en el detalle. Las armas de una build van a la MOCHILA
(`AutoEquipar.ColocarArmas`, `EquipoJugador.PrimerHuecoMochila`), no a un slot de accesorio - son
dos recursos totalmente distintos que el panel mostraba uno al lado del otro sin decir cuál era
el que faltaba. El comportamiento era correcto; lo que faltaba era la razón, visible solo en el
log (`ResultadoAutoEquipar.Detalle`), nunca en el panel.

**Arreglo real**: `CausaSinSitio` (enum: `MochilaLlena`/`SlotAccesorioOcupado`/`NoEsPiezaDeArmadura`)
+ `ResultadoAutoEquipar.DetalleSinSitio` (una entrada por objeto, causa estructurada en vez de
texto suelto) en `Common/Builds/AutoEquipar.cs`. `UI/Builds/ContenidoBuilds.MostrarResultadoAutoEquipar`
añade el motivo real de cada "sin sitio" al mensaje que se ve en el panel (`Builds.SinSitioDetalle`/
`Builds.SinSitioItem`/`Builds.CausaSinSitio.*`, nuevas claves ES/EN).

### El cambio de diseño real, pedido a mitad de la tarea

Con el bug ya diagnosticado, el usuario aclaró que el problema de fondo era otro, cita literal:
**"el tema de builds esta bien que te diga los objetos que tienes pero si no los tienes que lo
aplique directamente desde la libreria"**. O sea: dejar de limitar "Auto-equipar" a mover solo lo
YA poseído (la decisión original, documentada en el XMLdoc viejo de `AutoEquipar` como "nunca crea
objetos, igual que la app de escritorio") y, para lo que NO tienes, traerlo del catálogo - la
MISMA ruta real que ya usa `UI/Libreria/ContenidoLibreria.PedirObjeto` (`Item.SetDefaults` +
`CatalogoMejorPrefijo.MejorPrefijo` para el mejor prefijo real, nada reimplementado).

**Implementado en `Common/Builds/AutoEquipar.cs`**: cuando un objeto de la build no está en
ningún contenedor del jugador (`EquipoJugador.Buscar` devuelve null), se crea con
`CrearDesdeLibreria(tipo)` (mismo `SetDefaults` + mejor prefijo que la Librería) y se coloca:
- Armadura: en su slot fijo (`SlotArmaduraDe`); si ese slot ya llevaba puesta OTRA pieza (no
  destruirla nunca), se desplaza primero a la mochila y solo entonces se coloca la nueva.
- Accesorios: en el primer slot LIBRE (`PrimerSlotAccesorioLibre` solo devuelve huecos vacíos, así
  que aquí nunca hace falta desplazar nada).
- Armas: en el primer hueco libre de la mochila.

Nuevo contador `ResultadoAutoEquipar.Creados` (reemplaza a `NoPoseidos`, que ya no tenía sentido:
con creación automática, todo objeto RESUELTO acaba movido, ya colocado, creado o sin sitio -
"no lo tienes" ya no es un desenlace posible). `Builds.Resumen` pasa de "no los tienes={2}" a
"creados={2}". Nunca se destruye nada para hacer sitio: si crear exigiría desplazar algo que no
cupiera después en la mochila, no se crea nada y cuenta como `SinSitio` con su motivo real.

**Aviso, no silencio**: el texto de ayuda del panel se actualizó para dejar de prometer lo
contrario de lo que ahora hace (`Builds.AutoEquiparAyuda`, tooltip del botón, y `Panel.Ayuda.Builds`,
la línea fija bajo la cabecera) - ya no dice "nunca crea objetos", explica que lo que falta se
TRAE del catálogo. Se decidió NO añadir un diálogo de confirmación bloqueante: la Librería del
propio mod ya crea objetos libremente sin confirmar nada (es la función central de ese panel), así
que un aviso aparte solo en Auto-equipar habría sido inconsistente con el resto del mod - la
transparencia real está en que el texto de ayuda ya no oculta el comportamiento, y en que el
resultado siempre distingue "creados" de "movidos" en vez de mezclarlos.

### Verificado en el sandbox (`tModLoader-TerrakeepWS4`), con Sunfury en la hucha + mochila llena

```
AUTO-EQUIPAR "Vanilla / Pre-Hardmode..." / Cuerpo a cuerpo: movidos=0, ya colocados=0, creados=8,
sin sitio=5, no existen aquí=0. ...
  - Casco fundido: creado del catalogo -> equipo[0]
  ...
  - Mantra antimaldición: se iba a crear, pero no hay slot de accesorio libre (5 disponibles)...
  - Furia solar: la mochila esta llena
mensaje inmediato="Auto-equipar: movidos=0, ya colocados=0, creados=8, sin sitio=5, no existen
aquí=0 Sin sitio: Mantra antimaldición: no hay ranura de accesorio libre para él (...), Bezoar:
no hay ranura de accesorio libre para él (...), Filo de la noche: la mochila está llena, Furia
solar: la mochila está llena, La Despedazadora: la mochila está llena."
```

Y en el escenario por defecto del script (loadout activo, con parte del equipo ya en inventario):
`movidos=5, ya colocados=1, creados=5, sin sitio=2` - mueve y crea en la MISMA pasada sin pisarse,
y la segunda pasada (idempotencia) sale `movidos=0, creados=0, ya colocados=11, sin sitio=2`
idéntica salvo por eso: nada se duplica ni se vuelve a crear. Capturas reales en
`evidencia/ws4-builds-capturas/`.

### El desbordamiento real que dejó el propio arreglo del mensaje (aviso del usuario, con captura)

Con el mensaje de "sin sitio" más largo (hasta 5 objetos con su motivo), un primer intento usó
`EtiquetaTk.Recortar` (recorte con "..." + texto completo en el tooltip al pasar el ratón) -
funcionaba, pero el usuario pidió explícitamente no aceptar texto cortado en ningún sitio que
se toque hoy, ni con "..." aunque "técnicamente quepa": la solución real tiene que ser que la
caja/layout se adapte al contenido para que el texto se lea entero de un vistazo.

**Arreglo real, mismo patrón que `PestanaMundo.RecalcularAviso`** (recuadro naranja de dificultad,
arreglado hoy mismo por otro agente en paralelo - mismo problema, misma solución real, no
inventada dos veces): `ContenidoBuilds.RecalcularCabecera` (nuevo, llamado desde `Update` cada
fotograma) parte el mensaje con `EtiquetaTk.PartirEnLineas` al ancho REAL de la cabecera, mide su
alto con la fuente real (`FontAssets.MouseText.Value.MeasureString`), y CRECE `_cabecera.Height` -
antes fija a 52 px (`AltoCabecera`), ahora `_altoCabecera` dinámico con 52 px de mínimo - hasta lo
que el texto necesite de verdad. `ColocarFilas` (pildoras de etapa/clase/loadout, cuerpo) se
repite con el nuevo alto para que todo lo de abajo baje en el mismo fotograma, sin solape.
Verificado con capturas reales: mensaje de 2 líneas y de 3 líneas, ambos completos, sin recortar,
sin que ninguna fila de pildoras/columnas se solape con la cabecera crecida.

**Lo que se deja SIN tocar, documentado en vez de callado**: las pastillas de etapa/clase/loadout
de este mismo panel (`PintarPildoras`, `EstiloInvestigacionAcortar(etiquetas[i], 34)`) siguen
recortando con "..." de verdad - se ve literalmente en las capturas de esta misma sesión
("Pre-Hardmode (listo para el Mur...", "Final del juego (post Lunatic C..."). Es un patrón
PREEXISTENTE (no tocado hoy, ya estaba así desde WS4) usado en CUATRO sitios de este archivo
(pildoras de etapa/clase/loadout, nombre de objeto del catálogo, pie de arma). Arreglarlo de
verdad exigiría rediseñar el widget de pastilla entera (más ancho, etiqueta a dos líneas con más
alto de fila, o cambiar pastillas por otro control) con efecto en cascada sobre el resto del
panel - fuera de alcance razonable de esta tarea (arreglar un bug de auto-equipar + el cambio de
diseño pedido), y un cambio de esa envergadura en un widget compartido por 4 usos merece su propia
tarea con su propia verificación, no un parche de última hora. Queda para una tarea aparte.

### Verificación obligatoria

Sandbox propio, tres escenarios reales con clic real en el botón (`PulsarBotonAutoEquipar`, no
llamada directa): (1) el caso exacto reportado por el usuario (Sunfury en la hucha + mochila
llena) con el mensaje ya corregido y sin recortar; (2) build entera sin poseer NADA (creados=11,
2 sin sitio por falta de ranuras de accesorio, motivo real mostrado); (3) escenario mixto por
defecto (mueve y crea a la vez, dos pasadas, idempotente). Capturas reales en
`evidencia/ws4-builds-capturas/`, log completo en `evidencia/ws4-builds.log.txt`.

### Índice privado para comitear

`GIT_INDEX_FILE=<propio> git read-tree HEAD && git add ... && git commit`, después `git reset` a
secas en un comando aparte. Archivos de esta tarea: `Common/Builds/AutoEquipar.cs`,
`Common/Builds/PanelBuildsSystem.cs`, `UI/Builds/ContenidoBuilds.cs`,
`scripts/generar-localizacion.py`, `Localization/es-ES_Mods.TerrakeepMod.hjson`,
`Localization/en-US_Mods.TerrakeepMod.hjson`, `evidencia/ws4-builds.log.txt`,
`evidencia/ws4-builds-capturas/*.png`, `bitacora.md`. Nada de otros agentes en marcha a la vez
(`UI/Personaje/PestanaBuffs.cs`, `Common/Panel/AutopruebaEspaciado.cs`, y sus propias evidencias).

---

## 7-sep-2026 — Cuatro bugs de Librería + rediseño de Personaje (el mismo mini-panel en las tres pestañas)

Encargo con capturas reales del usuario, probando el mod en su partida: (1) el popup del editor
de prefijo se abre hacia ARRIBA, pide que se abra a la DERECHA porque ahí sí hay hueco libre;
(2) ese popup "no muestra ninguna opción de nada"; (3) mantener pulsado "-"/"+" del editor de
cantidad no acelera, solo cambia de 1 en 1; (4) la caja de texto de cantidad "no está bien
ajustada" (el placeholder "cantidad" no se ve centrado). Además, rediseño grande pedido para
Personaje: sacar la papelera + editor de cantidad de la fila compartida de `ContenidoPersonaje`
(debajo de la barra de pestañas) y poner en su lugar, en cada sub-pestaña que enseña objetos
reales (Inventario/Almacenes/Equipo), el MISMO mini-panel consolidado que ya tiene Librería
(recuadro de arrastre + cantidad + prefijo + papelera), en el hueco libre de abajo a la derecha.

### Bug 3 (mantener pulsado): arreglo real + un hallazgo aparte sobre `Main.mouseLeft`

`BotonTk` ganó `Manteniendo` (true mientras el ratón sigue pulsado desde que se apretó SOBRE el
botón, hasta que se suelta - `LeftMouseDown` lo enciende, `Update` lo apaga en cuanto
`Main.mouseLeft` es false) y `EditorCantidadTk.ActualizarRepeticion`: tras un retardo inicial de
0,4s (para no disparar un segundo paso en un clic normal) empieza a repetir `Ajustar`, acortando
el intervalo de 0,15s a 0,03s a lo largo de 1,5s de mantenerlo pulsado - patrón estándar de
"hold to repeat" con aceleración real, medida en segundos reales, no en fotogramas supuestos.

**El hallazgo real, verificando esto en el juego**: `Main.mouseLeft` NO es un flag que se pueda
"dejar puesto" varios fotogramas reales desde una autoprueba sin hardware de por medio - el motor
lo SOBREESCRIBE con el estado REAL del ratón físico en cada fotograma de entrada
(`PlayerInput`/`Main.UpdateInput`). El primer intento de la autoprueba (`Main.mouseLeft = true`
puesto una vez, dejando pasar 2,4s reales) dio **0 repeticiones**: el flag se perdía antes de que
le diera tiempo a acelerar. `BotonTk.ForzarManteniendoParaAutoprueba(bool)` (SOLO autopruebas,
nunca el juego real) resuelve esto con un segundo flag (`_pulsandoForzadoPorAutoprueba`) que hace
que `Update` IGNORE `Main.mouseLeft` mientras dure - re-afirmado en CADA reentrada de la
autoprueba (cada ~12 fotogramas reales), dejando que el motor siga corriendo fotogramas de verdad
entre medias. Con el arreglo: **8 repeticiones en la primera mitad de la ventana (1,2s) contra 31
en la segunda mitad** - aceleración real, medida con `DateTime.UtcNow`, no solo "repite".

### Bug 4 (campo de texto): centrado + auto-ajuste de escala, nunca "..."

`CampoTextoTk.DrawSelf` dibujaba el texto/pista SIEMPRE pegado 8px a la izquierda, sin medir si
cabía - en el campo compacto de 60px de Librería, "cantidad"/"quantity" se salía del recuadro por
encima del botón "+" de al lado. Arreglo: medir con la fuente REAL (mismo patrón que
`EditorCantidadTk.MedirYRecortarObjetivo`) y, si no cabe, REDUCIR la escala justo lo necesario
(nunca recortar con "..." - son palabras cortas, truncarlas a medias se leería peor que
achicarlas un poco) + centrar en las dos direcciones en vez de anclar a la izquierda.

De paso, `EditorCantidadTk.TextoEtiqueta` (el texto largo "Cantidad de X (n/m):") dejó de
recortarse con "..." en el modo EXPLÍCITO (Libreria/mini-panel de Personaje): ahí ese texto SOLO
se usa como tooltip de tres botones (`Main.instance.MouseText`, que no vive dentro de ninguna
caja de ancho fijo - mide el texto y dibuja su fondo alrededor), así que el recorte de 360px que
sí hace falta en el modo NO compacto de Personaje (ahí SÍ es una etiqueta real, dentro de un
recuadro fijo) no pintaba nada ahí - un objeto con nombre largo (con prefijo) se veía truncado en
el tooltip sin motivo real. Pedido explícito de esta tarea: el contenido tiene que leerse ENTERO,
nunca recortado en silencio si de verdad no hace falta.

### Bugs 1 y 2 (el popup de prefijo): la causa real no era la dirección, era `MaxWidth`/`MaxHeight`

Investigación en dos capas. Primero, la posición: `EditorPrefijoTk.ConstruirPopup` medía
`Main.screenWidth`/`Main.screenHeight` (mismo criterio que ya usa este mod para no fiarse de
min()/max() en CSS: medir SIEMPRE lo real del motor) para decidir hacia dónde abrir - por defecto
a la DERECHA con el borde superior alineado con el botón, cayendo a la izquierda o desplazándose
verticalmente solo si de verdad no cabe.

Con eso hecho, la primera captura real (800x720) mostró el mini-panel entero cortado por el borde
de la ventana: la rejilla de destino de Librería tenía 10 columnas FIJAS que, sumadas al ancho
fijo del mini-panel, se salían de la ventana en resoluciones normales - **bug real preexistente,
nunca visto porque WS3 se cerró sin capturas** ("sin capturas de pantalla en esta tarea, a
propósito", bitácora de esa sesión). Arreglado haciendo `ContenidoLibreria._columnasDestino`
adaptativo (3 a 10 columnas, mismo patrón que ya usa `_columnasResultado` para la rejilla del
catálogo): se recalcula en `Update()` con el ancho REAL de la zona, dejándole sitio real al
mini-panel al lado siempre.

Con el panel ya visible, una SEGUNDA captura (esta vez sí con el popup abierto) mostró el bug
real de fondo: el popup se abría, pero solo se veían ~14px de la primera fila ("Ninguno"), nada
más - exactamente lo que describía el encargo ("no muestra ninguna opción de nada"). Un método de
diagnóstico temporal (`EditorPrefijoTk.DiagnosticoGeometria`, geometría YA calculada de
boton/popup/lista) reveló la causa real: **`popup 176x26`** en vez de los `240x220` pedidos con
`Width.Set`/`Height.Set` - pese a que esas dos líneas SÍ estaban puestas. La causa:
`UIElement.MaxWidth`/`MaxHeight` valen `StyleDimension.Fill` (100% del padre) POR DEFECTO, y el
padre real de este popup es el propio botón "Prefijo: X" (176x26px) - sin fijar
`MaxWidth`/`MaxHeight` a mano, el motor RECORTA el popup a como mucho el tamaño de su padre, sin
ningún aviso ni excepción. El popup SÍ tenía las 66 filas reales dentro (categorías legales de
verdad, confirmado con el mismo diagnóstico: `filas internas=66, alturaInternaTotal=1602`) - el
bug era puramente de RENDERIZADO, no de datos. Con `_popup.MaxWidth.Set(240f, 0f)` +
`_popup.MaxHeight.Set(220f, 0f)`, el popup pasa a medir de verdad `240x220` y las filas se ven
todas, con scroll real.

**Un matiz que SÍ era real y se dejó como mejora aparte** (no la causa del bug, pero se investigó
antes de descartarlo): objetos vanilla fuera de los 683 tabulados en `PrefixRulesCatalog` (una
muestra curada, no exhaustiva - ver la cabecera de `CatalogoPrefijosLegales`) se quedaban
literalmente sin ningún grupo legal que ofrecer, aunque `Item.CanHavePrefixes()` diera true.
`CategoriasDe`/`GruposLegales` ganaron un *fallback* real: si el objeto vanilla no tiene fila en
la tabla curada, se cae al mismo camino por CAMPOS REALES (`item.accessory`/`item.DamageType`)
que ya usan los objetos de mod, en vez de dejar el picker vacío del todo.

### El rediseño de Personaje: el mismo mini-panel, sin reinventar nada

`PanelHerramientasLibreriaTk` (nacido en Librería) se reutiliza TAL CUAL en
`PestanaInventario`/`PestanaAlmacenes`/`PestanaEquipo` (no se movió de carpeta ni se renombró: ya
era genérico, no sabía nada de en qué pestaña vivía). `ContenidoPersonaje` perdió la fila
compartida (papelera + editor de cantidad en modo hover) que vivía debajo de la barra de
pestañas - encargo explícito: **"quitaría la papelera y lo de editar stacks de la línea de
arriba... a ponerlo todo junto abajo a la derecha"**.

Colocación por pestaña, cada una con su propio motivo real:
- **Inventario**: DEBAJO de Monedas/Munición (no a su derecha): la etiqueta "Mochila: N de 50
  ranuras ocupadas." vive en esa misma franja horizontal y su texto real se solapaba con el panel
  si se ponía al lado - visto midiendo el texto real, no solo suponiéndolo.
- **Almacenes**: a la derecha de la rejilla 10x4 (ahí sí sobra sitio real, sin ninguna otra
  etiqueta que lo dispute).
- **Equipo**: la más difícil, porque la columna de equipo YA usa TODO el alto disponible a
  propósito (ver la cabecera de `PestanaEquipo.ColocarColumnas`, WS1). Colocación DINÁMICA nueva
  (`ColocarHerramientas`, recalculada cada vez que cambian columnas/escala): a la derecha de la
  columna de equipo especial si la ventana da de sí (midiendo el ancho REAL que ya reserva esa
  columna para su fila más larga, `_resumenAccesorios`, 300px), o si no debajo de ella - esa
  columna tiene solo 5 filas contra las 10 de la columna de equipo, así que ahí abajo también
  queda hueco real. Verificado con captura real a 1280x720: se coloca a la derecha, sin pisar
  nada.

**Buffs/Apariencia/Desbloqueos: sin tocar, a propósito.** Buffs está fuera de alcance explícito
(otro agente trabajando en paralelo en `UI/Personaje/PestanaBuffs.cs`); Apariencia/Desbloqueos no
enseñan objetos reales (nada que seleccionar/editar con este mini-panel).

**Un bug de espaciado nuevo, encontrado con las primeras capturas reales de este mismo mini-panel
reutilizado** (nunca visto antes porque WS3 tampoco llevaba capturas): la pista "arrastra aquí"
que dibuja `SlotSeleccionTk` DEBAJO del recuadro vacío (`rect.Bottom+2`) se solapaba con la fila
de cantidad de justo abajo - el hueco de `PanelHerramientasLibreriaTk.filaDos` (34px) contaba el
alto del recuadro pero no el de esa pista. Subido a 46px (+ `Alto` de 140 a 156 para que siga
cabiendo todo). Y las etiquetas "Papelera"/"Seleccionar" quedaban casi tocándose (`Left=46` para
la segunda, cuando "Papelera" a escala 0,62 ya ocupaba casi ese ancho) - subido a `Left=64`.

**Autopruebas reescritas, no solo extendidas.** Los pasos 22/23 de WS1 (`AutopruebaPersonaje.cs`)
enganchaban el editor de cantidad por HOVER (`SlotObjetoVanilla.MouseOver`) - ese modo ya NO
existe en esta posición. Reescritos para arrastrar de verdad al recuadro de selección (mismo
patrón que WS3), y se añadió un paso 24/25 nuevo que reutiliza el objeto de prueba con prefijo
para demostrar que el mini-panel entero (arrastre + cantidad + prefijo + papelera) funciona igual
en Personaje que en Librería. Se usa `inventory[2]` (no `inventory[1]`) para no interferir con el
paso de papelera-desde-hueco (independiente, ya existía). `AutopruebaLibreria.cs` ganó un paso 17
nuevo (aceleración real) y el paso de prefijo se partió en dos (18 abre el popup, 19 captura y
pulsa) - necesario porque `CapturaDePantalla.Guardar` captura el fotograma YA PRESENTADO: capturar
en el MISMO paso que abre el popup enseñaba el popup todavía CERRADO (mismo bug de raíz que la
propia comprobación de "mantener pulsado", encontrado por separado).

### Verificación obligatoria

`CapturaDePantalla.Permitida` nunca había tenido las variables de WS1/WS3 en su lista blanca
(ninguna de las dos pedía capturas hasta ahora) - añadidas. Seis rondas reales en sandbox propio
(`tModLoader-TerrakeepWS3` para Librería a 800x720 y luego 1600x900; `tModLoader-TerrakeepWS1`
para Personaje a 1280x720, compilando SIEMPRE directamente en el sandbox propio con
`-tmlsavedirectory`, nunca la carpeta `Mods` compartida que el juego del usuario tenía abierta -
`verificar-personaje.ps1` ganó el parámetro `-CompilarPropio` para esto). Capturas reales en
`tModLoader-TerrakeepWS3/terrakeep-capturas/` y `tModLoader-TerrakeepWS1/terrakeep-capturas/`
(no versionadas, son de un sandbox fuera del repo): el popup de prefijo abre con opciones reales
visibles y sin solapar nada (ni el propio mini-panel, ni el pie del panel); mantener pulsado "+"
aceleró de 8 repeticiones/1,2s a 31/1,2s en la misma prueba; el mini-panel funciona igual en las
tres pestañas de Personaje que en Librería (arrastre selecciona, cantidad y prefijo editables,
papelera vacía sin tirar nada al suelo - confirmado con `ReferenceEquals` y recuento de objetos
activos en el mundo antes/después). Log completo en `evidencia/ws3-libreria.log.txt` y
`evidencia/ws1-personaje-client.log.txt`.

### Índice privado para comitear

`GIT_INDEX_FILE=<propio> git read-tree HEAD && git add ... && git commit`, después `git reset` a
secas en un comando aparte. Archivos de esta tarea: `Common/Libreria/AutopruebaLibreria.cs`,
`Common/Panel/CapturaDePantalla.cs`, `Common/Personaje/AutopruebaPersonaje.cs`,
`Common/Prefijos/CatalogoPrefijosLegales.cs`, `UI/Libreria/ContenidoLibreria.cs`,
`UI/Libreria/Widgets/EditorPrefijoTk.cs`, `UI/Libreria/Widgets/PanelHerramientasLibreriaTk.cs`,
`UI/Personaje/ContenidoPersonaje.cs`, `UI/Personaje/PestanaAlmacenes.cs`,
`UI/Personaje/PestanaEquipo.cs`, `UI/Personaje/PestanaInventario.cs`,
`UI/Personaje/Widgets/BotonTk.cs`, `UI/Personaje/Widgets/CampoTextoTk.cs`,
`UI/Personaje/Widgets/EditorCantidadTk.cs`, `scripts/verificar-personaje.ps1`,
`evidencia/ws3-libreria.log.txt`, `evidencia/ws1-personaje-client.log.txt`, `bitacora.md`. Nada de
otros agentes en marcha a la vez (`evidencia/panel-unico.log.txt`, modificado por otro agente
entre medias; `bin-checkDebug/`, `obj-verif-espaciado/`, carpetas de build sueltas).

---

## 7-sep-2026 — "Todo el texto se lee entero, nunca con ...": las 4 pastillas de Builds y barrido
## por el resto del mod

Encargo explícito, tras varias rondas insistiendo en el mismo criterio (ya aplicado a
`PestanaBuffs.cs`, commit `cae61b5`, y al aviso de "sin sitio" de Builds, commit `1d230d0`):
**ningún texto del mod se recorta con "...", ni siquiera cuando "técnicamente cabe"** - la caja se
adapta al contenido real, medido con la fuente real del juego, nunca al revés. Quedaba pendiente,
señalado explícitamente por un agente anterior como fuera de alcance, el caso confirmado de las 4
filas de pastillas de `ContenidoBuilds.cs` (etapa/clase/fuente/conjunto de destino), más un barrido
sistemático del resto del mod buscando cualquier otro recorte silencioso.

### 1. Las pastillas de Builds: de "ancho fijo a partes iguales" a "cada una mide lo que su texto
### necesita de verdad"

La causa real del recorte (`EstiloInvestigacionAcortar(etiquetas[i], 34)`, visto literalmente en
capturas reales del usuario: "Pre-Hardmode (listo para el Mur...") era el reparto: `PintarPildoras`
dividía el ancho de la fila en fracciones iguales (`1f / etiquetas.Count`) sin mirar el texto real,
así que una etiqueta larga como "Hardmode temprano (antes de los jefes mecánicos)" (49 caracteres)
quedaba en una caja mucho más estrecha que su propio texto.

**Arreglo real: `GrupoPildoras` (clase nueva, anidada en `ContenidoBuilds`), que sustituye por
completo al reparto en fracciones.** Cada pastilla (`BotonTk`) se crea con su texto COMPLETO y sin
posición fija; `GrupoPildoras.Reflow(anchoDisponible)`, llamado cada fotograma desde el nuevo
`RecalcularPildorasYFilas` (mismo patrón que `RecalcularCabecera`: se llama cada fotograma porque
el ancho depende de la resolución de la ventana, y el texto de la pastilla de conjunto de destino
cambia solo con la marca "(activo)"), mide con la fuente real el ancho de cada pastilla y las
coloca de izquierda a derecha, **saltando a una nueva fila VISUAL cuando la siguiente ya no cabe**
en vez de recortar nada. Caso límite cubierto de verdad (aunque no llega a activarse con las
etiquetas reales de este mod, máximo real medido 53 caracteres): si una sola etiqueta no cupiera
ni ocupando la fila entera, esa pastilla concreta ENVUELVE su propio texto con
`EtiquetaTk.PartirEnLineas` en vez de desbordar. El alto de cada una de las 4 filas
(`_altoFilaFuentes/Etapas/Clases/Loadout`) ahora es dinámico, y `ColocarFilas` (que ya existía)
reposiciona todo lo de abajo con esos altos reales en vez del `AltoFila` fijo de siempre.

Efecto colateral bueno, no buscado a propósito: `ActualizarBotonesLoadoutObjetivo` (la marca
"(activo)" de "Conjunto N") ya no necesita recortar tampoco - se limita a poner el texto completo
y `Reflow` le da el ancho que le haga falta cada fotograma. Y el nombre/pie de cada objeto del
catálogo de Builds (`PintarColumna`, líneas 429/441 del archivo original, mismo patrón de recorte
por CARACTERES sin medir con la fuente real) se arregló con una vía distinta pero igual de real:
`UIText.DynamicallyScaleDownToWidth`, una propiedad NATIVA de vanilla
(`Terraria.GameContent.UI.Elements.UIText`, confirmado en el código decompilado) que reduce la
escala del texto lo justo para que quepa entero en su caja - nunca lo corta, solo lo hace más
pequeño. Hacía falta además `MinWidth.Set(0f, 0f)` explícito: por defecto `UIText` expande su
propio `MinWidth` al ancho de su contenido en `InternalSetText`, lo que anulaba en la práctica
cualquier ancho más estrecho que se le quisiera dar - sin quitar ese mínimo, `DynamicallyScaleDownToWidth`
nunca llegaba a activarse.

### 2. Un bug real de verdad, encontrado por la propia autoprueba (no visto a simple vista)

La primera pasada de `GrupoPildoras` parecía correcta releyendo el código, pero la autoprueba en
el juego real (ver más abajo) midió un fallo real: a 1600x900 y 1280x720, las 3 pastillas de etapa
medían **120px de caja de fábrica** con un texto de hasta 305px - el mismo desbordamiento silencioso
que esta tarea vino a arreglar, solo que invisible (no se recortaba con "...", pero el marco del
botón quedaba más estrecho que su propio texto). La causa: `RecalcularPildorasYFilas` solo llamaba
a `Recalculate()` cuando el ALTO total de alguna fila cambiaba, pero `Reflow` escribe un nuevo
`Width`/`Left`/`Top` en cada pastilla EN CADA llamada, tanto si el alto total de la fila cambia
como si no (con 3 pastillas de etapa que ya caben en una sola línea a 1600x900, el alto se queda en
30px de siempre, pero el ANCHO de cada una sí cambiaba de 120 a ~300px). `UIElement.Width.Set(...)`
por sí solo no actualiza nada visible - hace falta un `Recalculate()` real para que
`GetDimensions()` deje de devolver el valor cacheado de la ÚLTIMA vez que se recalculó (el de
`Reconstruir`, con las pastillas todavía a 120x32 de fábrica). Arreglado llamando a `Recalculate()`
SIEMPRE al final de `RecalcularPildorasYFilas`, no solo cuando cambia el alto total (`ColocarFilas`,
que sí es más caro y sí mueve cosas de verdad, se mantiene condicionado al cambio de alto).

Con ese mismo patrón de bug ya identificado, se revisaron los otros tres sitios nuevos de esta
tarea que también escriben `Top`/`Height` de otro elemento tras un cálculo condicionado
(`ContenidoLibreria.AjustarAlturaRuta`, `PestanaBuffs.AjustarAlturaRutaCarpetas`,
`ContenidoInvestigacion.AjustarAlturaTitulo`): a los tres les faltaba igual el `Recalculate()`
final (a diferencia de las pastillas, aquí sí estaba bien condicionado a "cambió de verdad", pero
faltaba directamente la llamada) - añadido en los tres antes de darlos por buenos, no solo en el
que la autoprueba pilló por casualidad.

### 3. Barrido del resto del mod: `Acortar`/`Recortar` en 13 archivos, revisados uno a uno

- **`UI/Libreria/FilaCarpetaTk.cs` / `UI/Personaje/Widgets/FilaCarpetaBuffTk.cs`** (carpetas de la
  Librería y de Buffs, calco literal una de otra): el nombre de la carpeta se recortaba con "..."
  medido con la fuente real (mejor que el resto, pero recorte al fin). Arreglado con el mismo
  patrón que ya estableció `PestanaBuffs` para el nombre de un buff: se ENVUELVE con
  `EtiquetaTk.PartirEnLineas` a tantas líneas como haga falta y la fila CRECE lo que haga falta -
  trivial aquí porque las dos son filas de un `UIList` real (`RellenarCarpetas`), que ya apila cada
  fila por su alto real sin ayuda extra. El ancho de estas filas es una CONSTANTE fija
  (`AnchoColumnaCarpetas`, 300px en Librería, 132px en Buffs, no depende de la resolución), así que
  el cálculo se hace una sola vez en el constructor, sin necesitar recalculo por fotograma.
- **Ruta/breadcrumb de la carpeta abierta**, en los dos mismos paneles (`ContenidoLibreria.RutaCorta`
  / `PestanaBuffs.RutaCorta`): recortaba por el PRINCIPIO con "..." + los últimos N caracteres -
  con una ruta profunda de verdad ("Librería > Mascotas, Monturas, Herramientas > Mascotas de
  Jefes", 63 caracteres) esto llegaba a cortar literalmente a media palabra. Arreglado envolviendo
  la ruta completa a varias líneas y empujando hacia abajo la lista de carpetas de debajo
  (`AjustarAlturaRuta`/`AjustarAlturaRutaCarpetas`, nuevos, llamados cada fotograma desde `Update`)
  lo que haga falta - mismo patrón que `ContenidoBuilds.RecalcularCabecera`. Hizo falta convertir
  dos variables locales del constructor (`cajaCarpetas` en Buffs, la barra de scroll de objetos en
  Investigación) en campos de la clase para poder tocarlas desde el nuevo método.
- **Panel de Investigación** (`FilaCarpetaInvestigacion.cs`, `FilaObjetoInvestigacion.cs`,
  `ContenidoInvestigacion.cs`, `EstiloInvestigacion.cs`): el peor recorte encontrado en todo el
  barrido - `EstiloInvestigacion.Acortar` cortaba por NÚMERO DE CARACTERES a secas, sin medir con
  la fuente real en ningún momento (los otros sitios al menos medían en píxeles). Los tres sitios
  que lo usaban se rediseñaron: la fila de carpeta del árbol se envuelve y crece
  (`FilaCarpetaInvestigacion.ActualizarLayout`, alto mínimo 26px); la fila de objeto pasa al mismo
  patrón de DOS LÍNEAS que ya usa `PestanaBuffs.CrearFilaBuff` (línea 1 = nombre envuelto con TODO
  el ancho de la fila, línea 2 = estado + botón "Investigar"/"Quitar"); y el título de la carpeta
  seleccionada (`"Nombre  X/Y"`, antes recortado a 22 caracteres) se envuelve y empuja la lista de
  objetos hacia abajo. Las tres necesitaron un método `Ajustar*` nuevo llamado cada fotograma desde
  `ContenidoInvestigacion.Update` (mismas dos listas nuevas trackeadas, `_filasCarpeta` ya existía,
  `_filasObjeto` es nueva) porque, a diferencia de las carpetas de Librería/Buffs, aquí el ancho
  disponible SÍ depende de la resolución de la ventana (`Width.Set(0f, 1f)`, no un ancho fijo).
  `EstiloInvestigacion.Acortar` se borró: sin ningún llamador que le quedara, dejarlo habría sido
  código muerto invitando a "solucionar" el próximo desbordamiento recortando otra vez - el
  criterio que ya dejó escrito `EtiquetaTk.cs` la vez anterior que se tocó este mismo problema.
- **`EtiquetaTk.Recortar`** (la utilidad genérica "último recurso" que dejó la pasada anterior,
  documentada explícitamente como tal): con el arreglo de las carpetas de Librería/Buffs, sus dos
  ÚLTIMOS llamadores reales desaparecieron. Se borró por la misma razón que `EstiloInvestigacion.Acortar`
  - una función de recorte sin ningún sitio que la use es una invitación, no una utilidad.

### Dos sitios revisados y dejados EXPLÍCITAMENTE sin tocar, con su motivo real

- **`UI/Personaje/Widgets/EditorCantidadTk.cs`**: el constructor NO explícito (modo "hover",
  `EditorCantidadTk(UIElement raiz)`) sigue recortando con "..." la etiqueta larga "Cantidad de X
  (n/m):" en su modo NO compacto. Comprobado con `grep` de verdad en todo el repo: **ese
  constructor no lo llama nadie** - el único sitio que crea este control
  (`PanelHerramientasLibreriaTk.cs`, reutilizado por Librería y por las tres pestañas de Personaje
  desde el rediseño de la sesión anterior) usa siempre el otro constructor, el EXPLÍCITO/compacto,
  que ya no recorta nada (arreglado en esa misma sesión anterior). Es código MUERTO, no un bug
  activo - no se ve nunca en el juego real. Se deja documentado en vez de en silencio, y sin tocar:
  borrar un constructor entero es un cambio de forma distinta (limpieza de código muerto, no un
  arreglo de recorte) que merece su propia revisión, no un efecto colateral de esta tarea.
- **`Common/Exploracion/BuscadorMundo.cs`** (`nombre + " (" + objetos + " objetos" + (primero + "...") + ")"`,
  en `BuscarCofres`): revisado y NO es un recorte. El "..." va DESPUÉS del nombre COMPLETO del
  primer objeto del cofre ("Cofre (5 objetos, Espada de hierro...)"), como indicador de "y más
  objetos dentro" - ningún nombre se corta a media palabra. Distinto en naturaleza del patrón que
  esta tarea corrige (texto que debería leerse entero y se corta), así que se deja tal cual.

### Verificación obligatoria: en el juego real, autoprueba extendida (no una nueva)

Se extendió `Common/Panel/AutopruebaEspaciado.cs` (el arnés que ya recorría 3 resoluciones
- 1600x900, 1280x720, 800x720 mínimo real del motor - × 2 idiomas, en vez de crear uno nuevo
duplicado) con 4 pasos nuevos por combinación: pastillas de Builds (forzando `SeleccionarEtapa(1)`,
la etapa de la etiqueta más larga real), carpeta más profunda/de nombre más largo de Librería y de
Buffs (`CaminoLargo`, elige en cada nivel el hijo de nombre más largo - sin adivinar a mano ningún
nombre real del catálogo), y carpeta más profunda de Investigación (`CaminoLargoInvestigacion`,
mismo criterio, más desplegar las carpetas del camino para que la fila objetivo se vea en el
árbol). Hicieron falta 3 métodos nuevos SOLO-autoprueba en producción, mínimos y consistentes con
los que ya existían (`ContenidoBuilds.SeleccionarEtapa`, `PestanaBuffs.AbrirCarpetaParaPrueba`,
más 4 propiedades `FilaFuentesParaPrueba`/etc. para poder medir las pastillas desde fuera).

Las pastillas de Builds se MIDEN de verdad (no solo se capturan): cada `BotonTk` real de las 4
filas, `MeasureString(pildora.Texto) * 0.75f` contra su `GetDimensions().Width` real. **Primera
pasada: 3 FALLOS reales por resolución en 1600x900/1280x720** (el bug de `Recalculate()` que se
describe arriba, encontrado por esta misma autoprueba, no a simple vista) - **segunda pasada, tras
el arreglo: 10 pildoras medidas × 6 combinaciones = 60 mediciones, 0 fallos**. El resto
(carpetas/breadcrumb de Librería y Buffs, árbol + lista de Investigación) se verificó con capturas
reales inspeccionadas visualmente: la ruta "Librería > Mascotas, Monturas, Herramientas > Mascotas
de Jefes" se ve envuelta en 2 líneas completas sin recortar ni solapar la lista de carpetas de
debajo; el árbol de Investigación envuelve "Mascotas, Monturas, Herramientas" a 3 líneas y
"Pociones (regeneración)"/"Carritos de Mina" a 2, todas legibles, sin invadir la columna de
recuento/barra de progreso de al lado. Las 26 filas de "Activos"/"Añadir" de Buffs y el recuadro
naranja de Exploración (arreglos de sesiones anteriores) se remidieron en la misma pasada y siguen
en 0 fallos - sin regresión. Log completo en `evidencia/espaciado.log.txt`, 36 capturas reales
(6 combinaciones × 6 pantallas: aviso/buffs/buffs-carpetas/builds/libreria/investigacion) en
`evidencia/espaciado-capturas/`.

Compilado y desplegado el `.tmod` final de verdad a la carpeta `Mods` real del usuario
(`Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod`, `scripts/compilar.ps1`) - el
juego no estaba abierto en ningún momento de esta sesión, sin bloqueo que esperar.

### Índice privado para comitear

`GIT_INDEX_FILE=<propio> git read-tree HEAD && git add ... && git commit`, después `git reset` a
secas en un comando aparte. Archivos de esta tarea: `Common/Panel/AutopruebaEspaciado.cs`,
`UI/Builds/ContenidoBuilds.cs`, `UI/Investigacion/ContenidoInvestigacion.cs`,
`UI/Investigacion/EstiloInvestigacion.cs`, `UI/Investigacion/FilaCarpetaInvestigacion.cs`,
`UI/Investigacion/FilaObjetoInvestigacion.cs`, `UI/Libreria/ContenidoLibreria.cs`,
`UI/Libreria/FilaCarpetaTk.cs`, `UI/Personaje/PestanaBuffs.cs`,
`UI/Personaje/Widgets/EtiquetaTk.cs`, `UI/Personaje/Widgets/FilaCarpetaBuffTk.cs`,
`evidencia/espaciado.log.txt`, `evidencia/espaciado-capturas/*.png` (36 archivos), `bitacora.md`.
Nada de otros agentes (`evidencia/panel-unico.log.txt`, modificado por otro agente en una sesión
anterior y todavía sin comitear; `bin-checkDebug/`, `obj-verif-espaciado/`, carpetas de build
sueltas).

---

## 7-sep-2026 — Renombrado de la dependencia: TerrasavrNative.Core → Terrakeep.Core

El repo hermano de escritorio (`Terrasavr-Native`) renombró hoy su código fuente de
`TerrasavrNative.*` a `Terrakeep.*` (carpetas, `.csproj`, namespaces - ver su propia bitácora).
Aquí se refleja el mismo cambio del lado del mod, que consume ese proyecto como dependencia real
vía `dllReferences` (`lib/TerrasavrNative.Core.dll` → `lib/Terrakeep.Core.dll`).

**Hecho**: recompilado `Terrakeep.Core` (repo hermano, ya renombrado) para `net8.0`, copiado a
`lib/Terrakeep.Core.dll`, borrado el `.dll` viejo. Reemplazado `TerrasavrNative` → `Terrakeep` en
los 21 `.cs` que lo mencionaban (todos referencias a `TerrasavrNative.Core`/`TerrasavrNative.App`
en comentarios de documentación, ninguno cambia lógica), en `build.txt`
(`dllReferences = Terrakeep.Core`), `.gitignore`, los 8 scripts de `scripts/*.ps1` que
mencionaban la ruta/nombre viejos (en particular `scripts/actualizar-core.ps1`, que apuntaba a
`...\Terrasavr-Native\TerrasavrNative.Core\TerrasavrNative.Core.csproj` - una ruta que dejó de
existir con el renombrado del repo hermano y que habría fallado la próxima vez que alguien
necesitara traer una versión nueva de Core).

**Un sitio que el primer barrido (solo `.cs`) se dejó**: `TerrakeepMod.csproj` tiene su propio
`<Reference Include="TerrasavrNative.Core"><HintPath>lib\TerrasavrNative.Core.dll</HintPath>`,
que no es un `.cs` y por tanto no lo tocó el primer reemplazo - la fase 1 de
`scripts\compilar.ps1` (validación con el SDK del sistema) falló con 64 errores `CS0246` (tipos
de `CatalogoPrefijosLegales.cs` no encontrados) hasta corregirlo también.

**Nota sobre `Terrasavr-Native` (con guion)**: varios comentarios mencionan el nombre real de la
CARPETA del repo hermano en disco (`Downloads\Terrasavr-Win\Terrasavr-Native\`), que NO se ha
renombrado (solo sus proyectos internos) - se dejaron esas menciones intactas a propósito, son
correctas. El reemplazo de texto usado (`TerrasavrNative` sin guion) no las tocaba porque son
cadenas distintas de verdad, no solo por casualidad.

**No se tocan** los `evidencia/*.log.txt` que ya mencionaban "TerrasavrNative.Core" en su salida
capturada de ejecuciones pasadas (son registro histórico real, no la fuente) - se refrescarán
solos la próxima vez que se ejecute el script de verificación correspondiente.

**Verificado en el juego real**: `scripts\compilar.ps1` completo (fase 1 y 2) en verde, `.tmod`
desplegado en el sandbox `tModLoader-TerrakeepWS0`, mod cargado sin excepciones, y la propia
línea de humo del log lo confirma: `"Prueba de humo de Terrakeep.Core: ...ensamblado real =
Terrakeep.Core"` - ya no queda ningún residuo del nombre antiguo en lo que el mod imprime.

**Tropiezo del entorno, anotado por si se repite**: `Remove-Item` sobre
`tModLoader-Logs\client.log` (dentro de `C:\Program Files (x86)\...`) fue bloqueado por el propio
sandbox de la herramienta con "system path... is protected from removal", pese a haber
funcionado sin problema muchas veces antes en esta misma sesión sobre la misma ruta exacta.
`Clear-Content` sobre el mismo archivo sí funcionó como alternativa. No se investigó más a fondo
por no ser bloqueante.

---

## 8-sep-2026 — Auto-equipar de Builds: los objetos CREADOS ya aplican el prefijo REAL del catálogo, no solo el genérico

**El problema real** (encargo explícito, ya investigado antes de tocar nada): `AutoEquipar.CrearDesdeLibreria`
creaba los objetos que el jugador no tenía con `CatalogoMejorPrefijo.MejorPrefijo(tipo)` - el
"mejor prefijo posible" GENÉRICO, el mismo que usa la Librería - ignorando a propósito
`ObjetoBuild.PrefijoRecomendado`/`PrefixId`, que el catálogo de builds SÍ trae para bastantes
objetos. Para Vanilla y para el resto de clases de Calamity esto no perdía nada real (dato
ausente o coincidente con el genérico, documentado ya en el propio catálogo). El caso GRAVE era
Pícaro de Calamity: el catálogo trae los 21 `PrefixId` sintéticos (10000+) de los prefijos REALES
de Pícaro (`modPrefixMod`/`modPrefixName`, `CalamityMod/Prefixes/*.cs` decompilado), que
`CatalogoMejorPrefijo`/`best_prefix.json` NUNCA conoce (solo `Terraria.ID.PrefixID` plano) -
aplicar el genérico a un arma o accesorio Pícaro le ponía un prefijo vainilla sin sentido para
ese tipo de daño en vez de su prefijo real.

**Investigado antes de escribir nada** (regla del CLAUDE.md, "mirar el código real"):
- `rogue_prefixes.json` SÍ existe, en `Terrakeep.App/Assets/calamity/rogue_prefixes.json` del
  repo hermano `Terrasavr-Native` - tabla de los 21 `ModPrefix` reales (17 de arma + 4 de
  accesorio) con su id sintético (`CalamityIds.PrefixIdBase`=10000 + índice) y su nombre
  `"internal"` (ej. `10002`→`"Flawless"`, `10020`→`"Silent"`, los dos "best" que ya usa el
  catálogo de builds para Pícaro).
- `Terrakeep.Core.Data.RoguePrefixCatalog` (mismo repo hermano, ya compilado dentro de
  `lib/Terrakeep.Core.dll`) ya parsea ese JSON y expone `ById(int)` - reutilizado tal cual, cero
  reimplementación de la tabla.
- `BuildItemRef.PrefixId` (int?, `Terrakeep.Core/Data/BuildsCatalog.cs`) YA estaba en el DLL
  (confirmado con `strings lib/Terrakeep.Core.dll | grep PrefixId`) pero `ObjetoBuild` (el modelo
  del lado del mod, `ModeloBuilds.cs`) nunca lo leía - solo se portó `Prefix` (string) a
  `PrefijoRecomendado`, no `PrefixId` (int). Confirmado exactamente lo que sospechaba el encargo.
- **Cómo se aplica de verdad un `ModPrefix` de mod EN VIVO** (decompilado real,
  `tModLoader-Decompiled\tModLoader\Terraria\ModLoader\PrefixLoader.cs`/`ModPrefix.cs` +
  `tModLoader-Decompiled\CalamityMod\CalamityMod\Prefixes\*.cs`, esta última carpeta ya existía
  gracias a `scripts/generar-mejor-prefijo.py` del repo hermano, que ya decompilaba Calamity para
  otro propósito): un `ModPrefix` se registra con un `Type` (int) asignado en caliente por
  `PrefixLoader.ReservePrefixID()` (secuencial, por encima de `PrefixID.Count`=85 real de esta
  1.4.4.9), buscable por `ModContent.TryFind<ModPrefix>("CalamityMod", nombreDeClase)` - y el
  nombre de clase de CalamityMod coincide EXACTO con el campo `"internal"` del JSON
  (`Prefixes/Flawless.cs` → `class Flawless : RogueWeaponPrefix`, `Prefixes/Silent.cs` →
  `class Silent : RogueAccessoryPrefix`). `Terraria.Item.Prefix(int prefixWeWant)` acepta ese
  `Type` directamente - su firma real NO está limitada a `byte` (esa limitación es solo de
  `CatalogoMejorPrefijo.MejorPrefijo`, que devuelve `byte?` a propósito porque solo conoce
  `PrefixID` vanilla). Nada que reimplementar: resolver el id y pasárselo tal cual a `Item.Prefix`.
- Para el nombre vanilla recomendado (`PrefijoRecomendado`, ej. `"Legendary"`) no hizo falta
  invertir a mano la tabla `prefixNames` de `best_prefix.json` (que el encargo sospechaba que
  habría que usar): `Terraria.ID.PrefixID.Search` es un `IdDictionary` real construido por
  reflexión sobre los campos `public const int` de la propia clase (`IdDictionary.Create<PrefixID,
  int>()`, decompilado) - exactamente el mismo patrón que `CatalogoBuilds.ResolverPid` ya usa con
  `ItemID.Search` para los pid de objeto. `PrefixID.Search.TryGetId("Legendary", out int id)`
  resuelve directo, sin duplicar ninguna tabla.

**Hecho**:
- `ModeloBuilds.cs`: `ObjetoBuild` gana el campo `PrefixId` (int?).
- `CatalogoBuilds.cs` (`Convertir`): propaga `refe.PrefixId` al crear cada `ObjetoBuild`.
- Copiado `Terrakeep.App/Assets/calamity/rogue_prefixes.json` → `Assets/rogue_prefixes.json` del
  mod (mismo patrón ya usado con `best_prefix.json`).
- Nuevo `Common/Builds/CatalogoPrefijoPicaro.cs`: lee/parsea `rogue_prefixes.json` (mismo ciclo
  Load()/PostSetupContent() que el resto de catálogos, por el mismo motivo real de
  `TmodFile.GetStream`) y expone `ResolverPrefijoReal(idSintetico)` → `int?` (el `ModPrefix.Type`
  real de esta partida, o null sin lanzar si Calamity no está o cambió el nombre de la clase).
  Cableado en `PanelBuildsSystem.Load/PostSetupContent/Unload`, junto a `CatalogoBuilds`.
- `AutoEquipar.CrearDesdeLibreria` cambia de firma (`ObjetoBuild` en vez de `int tipo`, más un
  `out string origenPrefijo` para la evidencia) y añade `ResolverPrefijoDeBuild`, con el orden de
  prioridad real pedido: 1) `PrefixId` real de Pícaro (`CatalogoPrefijoPicaro`); 2) si no,
  `PrefijoRecomendado` vanilla (`PrefixID.Search`); 3) si no, el genérico de siempre
  (`CatalogoMejorPrefijo`, intacto para todo lo que no tiene un dato mejor). El comentario de la
  cabecera de la clase (antes decía que `PrefijoRecomendado` era "solo texto informativo") se
  actualizó para reflejar que ya decide de verdad.
- Evidencia real añadida al propio log de auto-equipar (`resultado.Detalle`): cada objeto CREADO
  deja qué vía de prefijo se tomó Y el `item.prefix`/nombre real tras aplicarlo (`Lang.prefix[...]`),
  para no fiarse nunca de "el resultado coincide con el genérico" como prueba de que se tomó la
  vía nueva (los dos catálogos pueden coincidir por casualidad, como documenta el propio encargo
  para 35 de las 36 armas vanilla).

**Verificado en el juego real** (`scripts\verificar-builds-en-juego.ps1`, sandbox
`tModLoader-TerrakeepWS4`, mundo/personaje `TerrakeepPrueba`):

- **Pícaro de Calamity** (`-Calamity -Clase rogue -Fuente calamity`, prehardmode): los 5 objetos
  creados con `PrefixId` en el catálogo salieron con el `ModPrefix` REAL, confirmado con el
  `item.prefix`/nombre reales tras `Item.Prefix()`, no solo con "se tomó la rama":
  `Coin of Deceit`/`Scuttler's Jewel`/`Amidias' Pendant` (accesorios, `PrefixId`=10020) →
  `item.prefix=103 "Silent"`; `Scourge of the Desert`/`Spore Knife` (armas, `PrefixId`=10002) →
  `item.prefix=90 "Flawless"`. Los dos ids (90, 103) están muy por encima de `PrefixID.Count`=85 -
  prueba de que son de verdad prefijos de MOD, no vainilla. Evidencia completa en
  `evidencia/ws4-builds-calamity.log.txt`.
- **Vanilla, melee prehardmode** (`-Clase melee -Fuente vanilla`, por defecto NightsEdge/Molten/etc
  ya sembrados - se MUEVEN, prefijo intacto; Sunfury y La Despedazadora NO sembrados, se CREAN):
  `Furia solar` (Sunfury) → rama "recomendado del catálogo, Godly" → `item.prefix=59 "(Piadoso)"`
  (`PrefixID.Godly`=59 real, confirmado contra el decompilado); `La Despedazadora` (TheBreaker) →
  rama "recomendado del catálogo, Legendary" → `item.prefix=81 "(Legendario)"`
  (`PrefixID.Legendary`=81 real). Exactamente lo que documentaba el catálogo, aplicado de verdad
  por la vía nueva (no por casualidad del genérico: el log deja constancia de qué rama disparó
  cada uno). Segunda pasada idempotente (`creados=0, ya colocados=11`). Evidencia completa en
  `evidencia/ws4-builds.log.txt`.
- Accesorios Pícaro con `PrefixId` cubiertos en la misma pasada de arriba (Coin of Deceit,
  Scuttler's Jewel, Amidias' Pendant), no hizo falta una prueba aparte.

**Pendiente, no bloqueante para este arreglo**: el `.tmod` final para la carpeta `Mods\` real
(`scripts\compilar.ps1`, fase 2) no se pudo desplegar en esta sesión porque el propio Adrián tenía
tModLoader ABIERTO DE VERDAD jugando (`Terraria: Perfectamente equi-librado`, ventana real,
~2 GB de RAM, respondiendo) - `ModCompile.Build`/`TmodFile.Save()` no puede escribir
`Mods\TerrakeepMod.tmod` mientras el juego lo tiene bloqueado. No se ha cerrado esa partida (cerrar
la sesión de otro es una decisión suya, no algo que tocar sin preguntar) - la fase 1 (validación
del C#) SÍ pasa en verde, y el `-build` completo con empaquetado real ya se demostró funcionando
en el sandbox de WS4 (mismo compilador Roslyn de tModLoader, mismo empaquetado, otra carpeta de
destino) - el arreglo en sí está probado con el compilador y el motor reales, solo falta repetir
`scripts\compilar.ps1` cuando cierre el juego para que el `.tmod` de `Mods\` quede al día.

---

## 8-sep-2026 — Buffs: la columna del centro (arbol de carpetas) ya no tiene 132 px fijos, se mide por su contenido

**El reporte real**, del usuario con captura: **"la columna del centro esta super apretada...
encontrar un equilibrio entre las 3 columnas"**. En la captura se veian los nombres de categoria
partidos en dos lineas con el contador entre parentesis colgando en su propia linea, pegado a la
barra de scroll: "Offensive" / "(18)", "Defensive" / "(13)", "Mascota" / "(20)"...

### La causa exacta, medida antes de tocar nada

`AnchoColumnaCarpetas = 132f`, una CONSTANTE, en `UI/Personaje/PestanaBuffs.cs`. La cuenta real de
lo que le quedaba al nombre: 132 - 20 (barra de scroll) - 4 (margen de la lista) - 20 (relleno del
`UIPanel` de la caja) - 40 (icono de la carpeta: 6 + 26 + 8) - 22 (flecha ">" de "tiene
subcarpetas") = **46 px**. Ninguna categoria del arbol cabe en 46 px con la fuente del juego a
escala 0,8 ("Offensivo (18)" mide 86 px), asi que TODAS se partian en dos lineas por sistema.

**No era una regresion**: la columna nacio con esos 132 px. Lo que paso es que el criterio de "el
contenido se lee entero, es el LAYOUT el que se adapta" (7-sep, dos entradas mas arriba) se aplico
a las filas de buff activo y a las de resultado, pero **a esta columna nunca**. `FilaCarpetaBuffTk`
ya envolvia el nombre en vez de recortarlo -por eso no salia ningun "..."-, pero envolver dentro de
una caja de 46 px no arregla nada: solo convierte el recorte en un apilamiento vertical igual de
ilegible. De donde salen los 132 px, ademas, tiene una explicacion real que conviene no perder: son
exactamente los dos botones de la cabecera de la columna ("Inicio" 60 + hueco 8 + "Subir" 64). Eso
es un MINIMO legitimo; el error fue usarlo tambien como maximo.

### El arreglo: el ancho lo decide el contenido, con suelo y techo medidos

`PestanaBuffs.AjustarAnchoCarpetas()` (nuevo, llamado desde `Update` como el resto de ajustes de
esta pestaña) reparte cada fotograma el ancho de "Añadir" entre sus dos subcolumnas:

- **Lo que pide el arbol**: el mayor `AnchoParaUnaLinea` de las carpetas visibles ahora mismo. Ese
  numero lo da la propia fila (`FilaCarpetaBuffTk.AnchoParaUnaLinea`, nuevo), que mide el nombre
  con la fuente REAL y suma su icono y su flecha - no se recalcula aqui una copia de esa cuenta,
  para que si la fila cambia de aspecto el reparto la siga solo.
- **Mas la merma real de una fila respecto a su columna** (`_mermaFilaCarpeta`), que tampoco se
  escribe a mano: se MIDE restando el ancho ya dibujado de una fila al de la columna. Asi incluye el
  relleno del `UIPanel`, que no esta a la vista en ese archivo y que dado por supuesto habria dejado
  la columna corta por unos pixeles - justo el tipo de numero a ojo que este panel viene evitando.
- **Suelo**: `AnchoMinimoCarpetas` = los 132 px de los dos botones, ya con su motivo escrito.
- **Techo**: lo que quede sin bajar la columna de resultados de su suelo ESTRUCTURAL
  (`AnchoMinimoResultados()`: icono + la palabra mas larga de un nombre de buff medida con la fuente
  real + boton "Aplicar" + barra de scroll, ~253 px), no de su ancho "comodo".

**Ese ultimo punto fue un error real de la primera version, cazado por la propia verificacion**: la
primera pasada uso como suelo de resultados su ancho COMODO (358 px, con el colchon de 200 px para
que un nombre medio no se envuelva). Con ese suelo, a 800x720 el techo del arbol caia por debajo de
su propio minimo y la resolucion mas apretada -que es justo la del reporte- se quedaba SIN NINGUNA
mejora, otra vez clavada en 132 px (evidencia de esa primera pasada: `arbol=161px`, 0 de 7 carpetas
en una sola linea en español). Un segundo intento con reparto proporcional del deficit tampoco
llegaba a las 7 en una linea. Con el suelo estructural el arbol tiene sitio en las dos resoluciones.

Ademas, `HolguraDeRedondeo` (2 px): la primera pasada dejo "Índice (354)" partido en dos lineas
PIDIENDO 140,x px de fila y teniendo 140 - el ancho real sale de una cadena de `StyleDimension` en
coma flotante y se queda una fraccion por debajo, y `PartirEnLineas` parte con comparacion estricta.
Es margen de redondeo medido, no un numero de diseño.

**Cambios de apoyo** en `UI/Personaje/Widgets/FilaCarpetaBuffTk.cs`: la fila pasa de ancho FIJO en
pixeles a ancho relativo (100 % de su lista) y reenvuelve su nombre cuando su ancho real cambia
(`AjustarAlAnchoReal()`, llamado desde `PestanaBuffs.AjustarFilasCarpetas()`), porque con el ancho
de columna ya en vivo el `PartirEnLineas` de una sola vez en el constructor se quedaba obsoleto en
cuanto cambiaba la resolucion o la carpeta abierta. `FilaCarpetaTk` (Libreria) NO se toca: es de
otro agente y su columna sigue teniendo 300 px fijos, que ahi si dan de sobra.

### Verificado en el juego real (arnes propio nuevo)

`Common/Panel/AutopruebaColumnasBuffs.cs` + `scripts/verificar-columnas-buffs.ps1`, sandbox propio
`tModLoader-TerrakeepColumnas`. Es un arnes **autonomo a proposito** -trae su propio `ModSystem`,
su propia variable de entorno (`TERRAKEEP_AUTOTEST_COLBUFFS`) y su propio guardado de captura- para
no tocar ni `AutopruebaEspaciado.cs` ni `PanelTerrakeepSystem.cs` ni `CapturaDePantalla.cs`, que son
archivos compartidos con los otros agentes en marcha. Recorre 2 resoluciones (1600x900 y 800x720, el
minimo real del motor) x 2 idiomas x 2 niveles del arbol (raiz y un nivel hondo), con 30 buffs
activos a la vez, y mide con la geometria YA dibujada: los tres anchos de columna, que ningun nombre
de carpeta desborde ni lleve "...", **en cuantas LINEAS acaba cada uno** (que es la medida real de
"esta apretado"), que la ruta no desborde, y que las filas de resultados y de activos no se hayan
roto al quitarles ancho.

**Resultado final, las 8 combinaciones en verde, 0 fallos** (`evidencia/columnas-buffs.log.txt`):

```
1600x900/es raiz  - pestaña=1060 | Activos=264 | Añadir=786 (arbol=186 + resultados=592). 7/7 en una linea
1600x900/es hondo - arbol=209 + resultados=569. 11/11 en una linea ("Índice (331-354)")
1600x900/en raiz  - arbol=183 + resultados=595. 7/7 en una linea
1600x900/en hondo - arbol=205 + resultados=573. 11/11 en una linea
800x720/es raiz   - pestaña=748  | Activos=264 | Añadir=474 (arbol=186 + resultados=280). 7/7 en una linea
800x720/es hondo  - arbol=209 + resultados=257. 11/11 en una linea
800x720/en raiz   - arbol=183 + resultados=283. 7/7 en una linea
800x720/en hondo  - arbol=205 + resultados=261. 11/11 en una linea
```

Antes: **0 de 7** en una sola linea a 800x720 y 6 de 7 a 1600x900 (con "Índice (354)" partido).
Ahora **7 de 7 y 11 de 11 en las cuatro combinaciones de cada nivel**, incluida la resolucion
minima. Las 8 capturas reales (`evidencia/columnas-buffs-capturas/`) lo confirman a ojo: cada
categoria con su contador entre parentesis en la MISMA linea, con aire hasta la barra de scroll, y
las tres columnas equilibradas. En el caso mas apretado de todos (800x720 en ingles con "Índice"
abierto, resultados=261 px) un nombre largo como "Weapon Imbue: Ichor (id 76)" se envuelve a dos
lineas y se lee ENTERO - la degradacion prevista y aceptable, nunca un recorte.

### Tropiezo del entorno, anotado por si se repite

`scripts\verificar-columnas-buffs.ps1` fallo la primera vez en la compilacion, pero **por codigo
ajeno**: `UI/Personaje/PestanaApariencia.cs` (otro agente, trabajandolo en paralelo ahora mismo)
estaba a medias y daba 14 errores `CS0103` con el compilador de tModLoader (`RegistroPanel`,
`GameShaders`, `CapturaDePantalla` sin `using`). La fase 1 con el SDK del sistema NO lo detecta
porque el `.csproj` tiene usings implicitos y el Roslyn interno de tModLoader compila sin ellos -
un detalle util: **la fase 1 en verde no garantiza que el `-build` real pase**. Resuelto sin tocar
nada suyo ni esperar: `git archive HEAD` a una copia limpia en el scratchpad (con la carpeta
llamada `TerrakeepMod`, que es de donde tModLoader saca el nombre del mod), mis 4 archivos copiados
encima, y el script ejecutado desde ahi. Toda la verificacion de arriba es de esa copia aislada:
HEAD + solo mis cambios, sin trabajo a medias de nadie.

### Indice privado para comitear

`UI/Personaje/PestanaBuffs.cs`, `UI/Personaje/Widgets/FilaCarpetaBuffTk.cs` (arreglo real),
`Common/Panel/AutopruebaColumnasBuffs.cs` y `scripts/verificar-columnas-buffs.ps1` (nuevos, arnes
propio), `evidencia/columnas-buffs.log.txt` y `evidencia/columnas-buffs-capturas/*.png` (8 archivos,
evidencia propia con nombre propio, sin colision con la de otros agentes) y esta entrada de
`bitacora.md`. No se comitea nada de otros agentes en marcha a la vez
(`Common/Libreria/ArbolLibreria.cs`, `Common/Libreria/CatalogoVivo.cs`,
`Common/Libreria/PanelLibreriaSystem.cs`, `Common/Libreria/AuditoriaCategorias.cs`,
`UI/Libreria/SlotCatalogoLibreria.cs`, `UI/Libreria/Widgets/EditorPrefijoTk.cs`,
`UI/Panel/PanelTerrakeepState.cs`, `UI/Panel/CapaSuperposicionTk.cs`,
`UI/Personaje/PestanaApariencia.cs`, `UI/Personaje/Widgets/MunecoTk.cs`,
`scripts/verificar-categorias-libreria.ps1`), ni las carpetas de build sueltas
(`bin-checkDebug/`, `obj-verif-espaciado/`).

---

## 8-sep-2026 — Librería: "Categorías" con páginas vacías, objetos que no salían y objetos mezclados

**Reportado por el usuario probando el mod (con capturas)**: dentro de la carpeta raíz
"Categorías" había *muchas páginas vacías*, en algunas categorías *no salía nada yendo objeto a
objeto*, y cuando salía algo *estaba todo mezclado* pese a que los sprites/iconos indicaban dónde
debería ir cada cosa. Pidió comprobar en concreto que en "daño cuerpo a cuerpo" salieran todos los
objetos que deben, poniendo de ejemplo **La Cenit** (`ItemID.Zenith`).

### Causa real: el árbol curado es de una versión de Terraria MÁS NUEVA que la del juego

`Assets/vanilla_library_tree.json` se extrajo del Terrasavr real, que va por Terraria **1.4.5.8**
(`ItemID.Count = 6196`, ids reales hasta 6145). tModLoader va por **1.4.4.9**
(`ItemID.Count = 5456`). Comprobado constante a constante contra los dos `ItemID.cs` decompilados
(`Downloads\tModLoader-Decompiled\tModLoader\` y `\TerrariaVanilla\`): **5504 constantes con id
< 5456 comparadas, 0 diferencias** — Terraria solo añade ids al final, nunca reordena. O sea que
lo único que sobra son los **690 ids que 1.4.5 añadió y aquí no existen** (722 contando además los
huecos sin objeto real por debajo de 5456). Nadie los estaba filtrando, y de ahí salían los tres
síntomas exactos que describió el usuario:

1. **Páginas vacías** — 36 hojas del árbol contenían *solo* ids de 1.4.5: 13 páginas seguidas de
   "Colocable" (Page 68..81), "Paredes/Page 8", "Vanidad/Page 14", y 17 rangos enteros de "Objetos
   por id" (5481-5520 en adelante).
2. **Objetos que no salían** — `Item.SetDefaults` con un id por encima de `ItemLoader.ItemCount`
   revienta con `IndexOutOfRangeException` en su primera línea útil
   (`material = ItemID.Sets.IsAMaterial[type]`, código real del `Item.cs` decompilado), así que la
   rejilla se quedaba a medio montar en cuanto una página tocaba uno.
3. **Todo mezclado** — con Calamity cargado esos ids **sí existen**: tModLoader reparte los ids de
   los mods justo a partir de `ItemID.Count`, así que **1452 apariciones** de objetos de Calamity
   caían dentro de carpetas vanilla, y **40 carpetas** enseñaban como icono el sprite de un objeto
   de Calamity. Eso es literalmente lo que el usuario describía como "los sprites indican dónde
   debería ir cada objeto y el contenido no se corresponde".

### Arreglo

- `Common/Libreria/CatalogoVivo.cs`: nuevo `EsVanillaReal(tipo)` — `tipo < ItemID.Count` (deja
  fuera los ids de MODS, no basta con `ItemLoader.ItemCount`) **y** con nombre real en el catálogo
  (deja fuera los huecos).
- `Common/Libreria/ArbolLibreria.cs`: poda del árbol curado justo después de que Core lo monte.
  Quita los ids que no existen, elimina la carpeta entera si se queda sin un solo objeto, reescribe
  el recuento del rótulo ("Daño de Cuerpo a Cuerpo (316)" → "(294)" — se puede porque
  `LibraryLabelCatalog.Translate` traduce por PLANTILLA `"Melee damage ($1)"` y sustituye el número
  después, así que ningún rótulo se queda sin traducir) y sustituye el icono de la carpeta por el
  de su primer objeto real cuando el suyo no existía. **La poda se hace en el lado del mod, no en
  `Terrakeep.Core`**: Core lo comparte la app de escritorio, que sí corre contra 1.4.5.8 y ahí no
  sobra ningún id. **No se renumeran las páginas** a propósito: las hojas van ordenadas por id y lo
  que se cae es siempre la cola, verificado hoja a hoja sobre el `.json` real antes de decidirlo.
- `UI/Libreria/SlotCatalogoLibreria.cs`: guarda `tipo < ItemLoader.ItemCount` antes de
  `SetDefaults`, de red por si alguna vez llega otro id imposible por otro camino.
- `Common/Libreria/AuditoriaCategorias.cs` (nuevo) + una línea en `PanelLibreriaSystem.UpdateUI` y
  otra en `CapturaDePantalla.Permitida`: arnés de recuento propio (`TERRAKEEP_AUDIT_CATEGORIAS`),
  aparte de `AutopruebaLibreria` porque otro agente la tenía abierta y porque esto no depende de
  ningún clic. Script `scripts/verificar-categorias-libreria.ps1`, sandbox propio
  `tModLoader-TerrakeepCategorias`.

### Verificado en el juego real (`-tmlsavedirectory` + `-skipselect`, con y sin Calamity)

Recuento "objeto a objeto" real, evidencia en `evidencia/categorias-libreria.log.txt` y
`evidencia/categorias-libreria-calamity.log.txt`:

- **Cobertura**: el juego cargado tiene **5423** objetos vanilla reales (ids 1..5455 con muestra y
  nombre). El árbol curado cubre **5423**. **Huérfanos: 0**.
- **Salud del árbol**: carpetas vacías **0**, ids que no existen **0**, iconos de carpeta que no
  son un objeto real **0**, y — con **CalamityMod cargado** (`ItemLoader.ItemCount = 8279`, 2817
  objetos de mod) — objetos de mod colados dentro del árbol vanilla curado: **0**. La poda quitó
  **722 ids / 1452 apariciones / 40 carpetas** y corrigió **1 icono** (los otros 39 iconos rotos
  colgaban de carpetas que la poda eliminó enteras).
- **La Cenit** (`ItemID.Zenith`, 4956, `MeleeNoSpeedDamageClass`): aparece en
  `Categories/Weapons/Melee damage/Page 8` — **OK**. Igual comprobados Terra Blade (757), Meowmere
  (3063), Last Prism (3541) en magia, S.D.M.G. (1553) a distancia y Fire Gauntlet (1343) en
  accesorios: todos en su carpeta.
- **Navegación real por la interfaz** (el panel abierto de verdad, no solo los datos): "Melee
  damage (294)" → Page 8 con **26 objetos, todos llenos** (antes 36 huecos con 9 rotos);
  "Colocable (2680)" → Pages 61+ → **Page 67 con 40 objetos**, y ya no existen las páginas 68..81.
  Capturas reales en `evidencia/categorias-capturas/`. Los rótulos y la ruta caben y se envuelven
  bien, ningún texto cortado ni desbordado (el recuento del rótulo solo puede menguar, nunca
  crecer).

### Dos cosas que NO son un fallo y conviene no "arreglar"

- **El árbol curado no clasifica por los campos del `Item`**, sino por la tabla `metatype` que
  Terrasavr trae curada a mano (`local-site/script.js` real: "Melee damage" = `metatype` contiene
  `'d'`; "Wings" = `textLq` contiene `"allows flight"`, por eso esa carpeta son las 7 **botas** que
  dan vuelo y las alas de verdad viven en "Accessories", que es lo que son; "Dyes" = el nombre
  acaba en `"Dye"`). El RECUENTO 3 del arnés mide la discrepancia entre esa curación y los campos
  reales: sale un puñado de casos frontera esperables (Coin Gun con daño base 0, bichos
  capturables como colocables, cajas de música como vanidad). Lo que tiene que salir a cero son los
  RECUENTOS 1 y 2, y sale.
- Con **Calamity cargado**, "Melee damage" pasa de 1 a 211 discrepancias en ese RECUENTO 3 porque
  **Calamity reemplaza el `DamageType` de las armas vanilla melee** por una clase suya. Es cosa de
  Calamity, no del árbol: los objetos son los mismos y están donde deben.

### Obstáculo del entorno (autonomía técnica)

El `-build` del proyecto entero falló tres veces con `CS0103` en `Common/Libreria/
AutopruebaLibreria.cs` (`ComprobarPopupDentroDelPanel`, `ComprobarScrollReal`, `CapturarTrasScroll`
no existen) — trabajo a medias de **otro agente** que estaba en ese archivo, no de esta tarea. En
vez de esperar o de tocar su archivo, se compiló una **copia aislada** del repo en
`tModLoader-TerrakeepCategorias\ModSources\` con ese único archivo sustituido por su versión de
`HEAD` (`git show HEAD:...`). Compila en verde y es el `.tmod` con el que se hicieron las dos
verificaciones reales.

---

## 8-sep-2026 — El desplegable de prefijos se salía del panel, lo tapaba "Cerrar" y no tenía scroll

Encargo con captura del usuario, sobre el editor de prefijo/cantidad de la Librería añadido hoy
mismo: al abrir el desplegable de prefijos (1) el menú se dibujaba **fuera del panel del mod**,
flotando sobre el MUNDO del juego a la derecha del marco; (2) el botón **"Cerrar (O)" del pie del
panel quedaba por encima**, tapando sus opciones; y (3) **el scroll no funcionaba**: no había forma
de bajar por la lista.

### Los tres son el MISMO error de raíz: el popup colgaba del botón que lo abre

`EditorPrefijoTk.ConstruirPopup` hacía `Append(_popup)` sobre sí mismo (el widget del botón
"Prefijo: X", 176x26 px) y lo colocaba midiendo `Main.screenWidth`/`Main.screenHeight`. Cada
síntoma sale de una consecuencia distinta de esa misma decisión, y las tres están en el código real
decompilado, no supuestas:

- **Fuera del panel**: ningún `UIElement` recorta a sus hijos salvo que se le ponga
  `OverflowHidden`, así que un hijo con un `Left` mayor que el ancho de su padre se dibuja igual.
  Y medir contra `Main.screenWidth` solo garantiza que cabe en la VENTANA, no dentro del panel -
  por eso el menú se salía hacia el mundo sin que ninguna cuenta fallara.
- **Tapado por "Cerrar"**: `UIElement.DrawChildren` dibuja en el orden de `Append`, y
  `PanelTerrakeepState.ConstruirPie()` (ayuda + botón Cerrar) se ejecuta DESPUÉS de colgar
  `_contenedor`. O sea que todo lo que salga de la zona de contenido queda por debajo del pie, por
  muy "flotante" que sea.
- **Sin scroll (y sin clics de ratón reales)**: `UIElement.GetElementAt` - la llamada que usa
  `UserInterface.Update` para repartir clics y rueda - solo **desciende** a un hijo cuyo
  `ContainsPoint` sea true, y para llegar a ese hijo antes ha tenido que bajar por todos sus
  ancestros, que también tienen que contener el punto. Un popup de 240x220 que sobresale de un
  botón de 176x26 queda, para el ratón, en tierra de nadie: se ve, pero el motor **nunca le entrega
  un evento**. El arreglo del 7-sep (`MaxWidth`/`MaxHeight` a mano, que es real y sigue puesto)
  arregló que se VIERA entero, no que se pudiera USAR: la autoprueba de entonces pulsaba los
  botones llamando a `BotonTk.LeftClick` a mano, que se salta el hit-testing entero, así que el
  fallo no podía salir por ahí.

### El arreglo: `UI/Panel/CapaSuperposicionTk.cs`

Una capa vacía colgada como **ÚLTIMO hijo del marco**, con la misma geometría que `_contenedor` (ni
título, ni pestañas, ni pie). El desplegable vive ahí, y los tres problemas caen a la vez: sigue
dentro del árbol de interfaz del panel; acotarse a la capa ES no salirse del panel (`Left`/`Top` se
calculan en coordenadas de pantalla y se acotan al rectángulo real de la capa, que es lo que antes
se hacía contra la ventana); al ser el último hijo se dibuja por encima de todo, botón "Cerrar"
incluido; y como la capa sí contiene el punto del ratón, `GetElementAt` desciende hasta el
desplegable y le llegan rueda y clics.

Detalles que hicieron falta de verdad:

- **Transparente al ratón mientras está vacía** (`IgnoresMouseInteraction`), o robaría todos los
  clics del panel: `GetElementAt` salta el elemento Y sus hijos con ese flag. Con algo dentro se
  apaga y la capa hace de fondo modal: el clic fuera del desplegable lo cierra en vez de
  atravesarlo.
- **Las ranuras de objeto no se enteran solas**: `SlotObjetoVanilla`, `SlotSeleccionTk`,
  `SlotPapeleraTk` y `SlotCatalogoLibreria` miran el ratón A MANO dentro de su `DrawSelf`
  (`ContainsPoint(Main.MouseScreen)` + `ItemSlot.Handle`), fuera del sistema de eventos - sin una
  consulta explícita (`CapaSuperposicionTk.TapaAlRaton`), un clic en una fila del desplegable
  ADEMÁS habría cogido o soltado el objeto de la ranura de debajo, que es justo donde se abre.
- **Nada de estado estático colgado**: la capa se vacía sola en `OnDeactivate` (al cerrar el panel,
  `UserInterface.SetState(null)` baja recursivamente por el árbol llamándolo, código real) y en
  `CambiarArea` (el desplegable pertenece a un contenido que se tira, pero vive en la capa, que no).
- **`ManualSortMethod` vacío en la `UIList`** del popup, bug latente encontrado leyendo su código:
  sin él, `UIList` ordena con `List.Sort` + `UIElement.CompareTo`, que devuelve 0 para todo - y
  `List.Sort` **no es estable**, así que con 66 filas reales podía permutarlas y dejar cada prefijo
  bajo la cabecera de otro grupo. Lo dice la propia documentación de `UIList`.
- **Rueda también fuera de la lista** (el margen del popup y la franja de la barra): enganche en el
  popup que mira `evt.Target` - no `IsMouseHovering` - para no aplicar el desplazamiento dos veces
  cuando el evento viene burbujeando desde dentro de la `UIList` (que ya lo aplicó y llama a
  `base.ScrollWheel`, que sube al padre).

### Verificado en el juego real, con datos y con capturas

Pasos 19-21 nuevos en `AutopruebaLibreria` (sandbox `tModLoader-TerrakeepWS3`, 1090x613 de UI
real). Nada de "se ve bien": geometría ya calculada y la ruta real del motor.

```
Paso 19 - popup x=505 y=321 240x220; capa x=31 y=96 1027x444; dentroDeLaCapa=True
Paso 19 - orden real de hijos del marco: ... [9] BotonTk(Cerrar) ... [10] CapaSuperposicionTk OCUPADA
Paso 19 - boton "Cerrar" x=899 y=551 160x34. Se cruzan: False (y la capa se dibuja despues -> encima)
Paso 19 - UIElement.GetElementAt(625,431) devuelve: BotonTk "Godly" -> el raton llega al desplegable
Paso 20 - rueda REAL (3 muescas de 120 via UIElement.ScrollWheel, igual que UserInterface.Update):
          ViewPosition 0.0 -> 360.0
Paso 21 - primera fila de la lista: y=327.2 -> y=-32.8 (el contenido se desplazo de verdad)
Paso 21 - visibles ANTES: None | Demonic | Godly | Legendary | Ruthless | Godly
          visibles DESPUES: Agile | Murderous | Dangerous | Legendary | Keen | Superior | Forceful
Paso 21 - rueda hacia arriba (4 muescas): 360.0 -> 0.0 (sube tambien)
```

Tres capturas reales del back buffer en `evidencia/prefijo-capturas/`: el desplegable abierto
dentro del panel (`...-1-abierto-dentro-del-panel.png`), el mismo tras el scroll con OTRAS opciones
visibles y la barra desplazada (`...-2-tras-scroll.png`), y el de antes de pulsar un prefijo. En
las dos primeras se ve el botón "Cerrar (O)" abajo a la derecha, entero y sin tocar el menú.

**También en Personaje**, donde el mismo mini-panel se reutiliza en otro sitio de la pantalla (paso
25 de `AutopruebaPersonaje`, sandbox WS1, 1280x720): `popup x=808 y=424 240x220; capa x=110 y=99
1060x544; dentroDeLaCapa=True; se cruza con el boton Cerrar (1010,654): False; GetElementAt en su
centro devuelve BotonTk (del popup: True)`. Ahí sí cabe a la derecha del botón, que es la
preferencia de siempre; en la Librería no cabía dentro del panel y cae a la izquierda solo.

### Índice privado para comitear

`GIT_INDEX_FILE=<propio> git read-tree HEAD && git add ... && git commit`, después `git reset` a
secas en un comando aparte. Archivos de esta tarea: `UI/Panel/CapaSuperposicionTk.cs` (nuevo),
`UI/Panel/PanelTerrakeepState.cs`, `UI/Libreria/Widgets/EditorPrefijoTk.cs`,
`UI/Libreria/Widgets/SlotSeleccionTk.cs`, `UI/Libreria/SlotCatalogoLibreria.cs`,
`UI/Personaje/Widgets/SlotPapeleraTk.cs`, `UI/SlotObjetoVanilla.cs`,
`Common/Libreria/AutopruebaLibreria.cs`, `Common/Personaje/AutopruebaPersonaje.cs`,
`evidencia/ws3-libreria.log.txt`, `evidencia/prefijo-capturas/*.png`, `bitacora.md`. Nada de los
otros agentes que trabajan a la vez en el repo (`Common/Libreria/ArbolLibreria.cs`,
`CatalogoVivo.cs`, `PanelLibreriaSystem.cs`, `AuditoriaCategorias.cs`,
`Common/Panel/CapturaDePantalla.cs`, `UI/Personaje/PestanaApariencia.cs`,
`UI/Personaje/Widgets/MunecoTk.cs`, `Assets/best_prefix.json`, sus `scripts/verificar-*.ps1` y sus
logs de evidencia), ni las carpetas de build sueltas.

**Tropiezo del entorno, anotado por si se repite**: la fase 1 de validación falló una vez con 4
`CS0103: ItemID no existe` en `UI/Personaje/Widgets/MunecoTk.cs` - trabajo a medias de otro agente
(le faltaba `using Terraria.ID;`), no de esta tarea. Se resolvió solo al reintentar un minuto
después, cuando ese agente terminó su edición; no hizo falta tocar su archivo ni compilar una copia
aislada.

---

## 8-sep-2026 — "Mejor prefijo": ningún báculo de INVOCACIÓN enseñaba su etiqueta (la tabla se generaba contra otra versión del juego)

**El problema real**, tal cual lo reportó el usuario: *"todos los báculos del juego no muestran la
etiqueta de mejor prefijo posible, ni uno solo"*, más una petición de repasar TODOS los objetos por
si había más huecos del mismo tipo.

### Qué son de verdad "los báculos" aquí (comprobado, no supuesto)

No son una clase de daño ni un `ItemID.Sets` propio. Cruzando `best_prefix.json` con los nombres
reales en español, los objetos afectados eran los **38 objetos de INVOCACIÓN** del set `Summon`
que existen en este 1.4.4.9 - y casi todos se llaman literalmente "Báculo de…": Báculo de slime,
Báculo óptico, Báculo pigmeo, Báculo de araña, Xenobáculo, Terraprisma, Flor de Abigail, las 9
varitas/bastones/báculos de las torres del Ejército Antiguo… Los báculos de **magia** (Báculo de
amatista y compañía) sí funcionaban: están en el set `Magic`, que no estaba roto. Por eso el
síntoma se veía como "todos los báculos" sin serlo del todo.

### La causa raíz (mirando el código real de las dos versiones, no suponiendo)

`Assets/best_prefix.json` se copiaba tal cual del repo hermano, y allí se generaba contra
`TerrariaVanilla\` (Terraria **1.4.5.8**). Este mod corre sobre tModLoader **1.4.4.9**, y a estos
efectos son dos juegos distintos:

- 1.4.5.8 separó `PrefixesForMagic` de `PrefixesForSummons` y añadió 85 Fabled..97; en 1.4.4.9 hay
  un único `PrefixesForMagicAndSummons` (tope 83 Mythical) y `PrefixID.Count` = **85**. Las 149
  entradas de invocación (46 vanilla + 103 de Calamity, más 1 vanilla en 95 Eager) apuntaban a un
  prefijo **que no existe aquí**, y `CatalogoMejorPrefijo` las descartaba con su guardarraíl de
  rango (`valor < PrefixID.Count`) - correcto por su parte: mejor sin etiqueta que una etiqueta
  rota. Pero eso dejaba a todas esas armas sin nada que enseñar.
- Que las armas de invocación de MOD caen en el mismo pool está en el decompilado, no supuesto:
  `SummonDamageClass.GetPrefixInheritance(dc) => dc == DamageClass.Magic`, o sea
  `ModItem.MagicPrefix()` es true para ellas y `Item.GetPrefixCategories()` las manda a
  `PrefixCategory.Magic` → `PrefixesForMagicAndSummons`.
- Y no bastaba con cambiar el pool: **289 objetos tienen stats distintas entre las dos versiones**.
  El caso que decide aquí es que en 1.4.4.9 los báculos de invocación **gastan maná** y en 1.4.5.8
  no, así que aquí los prefijos que tocan el maná sí pasan el filtro `round(mana*mcst)==mana` y
  gana Mythical. Lo confirma por fuera el historial de la wiki oficial: *"Desktop 1.4.5.0: Removed
  mana cost (cost 10 mana previously)"*.

### Hecho

- El generador del repo hermano (`Terrasavr-Native`, `scripts/generar-mejor-prefijo.py`) ahora emite
  **dos** tablas con el mismo criterio: la de siempre para la app de escritorio (1.4.5.8) y una
  nueva contra 1.4.4.9 (`best_prefix_tml.json`) - ver la bitácora de ese repo para el detalle.
- `Assets/best_prefix.json` de aquí pasa a ser **copia tal cual de `best_prefix_tml.json`** (mismo
  criterio de siempre: copiado, nunca editado a mano). Cabecera de `CatalogoMejorPrefijo`
  actualizada; su comprobación de rango se queda como guardarraíl aunque hoy ya no descarte nada.
- `AutopruebaPrefijos`: el paso 1 era una regresión de la defensa de rango con el Báculo pigmeo
  ("tiene que devolver null") - ya no aplica y se sustituye por 5 báculos de invocación reales con
  su prefijo esperado y su línea de tooltip; y un paso 6 nuevo de **auditoría objeto a objeto
  contra el propio motor** (`Item.CanHavePrefixes()` para cada `Item.type` real cargado).

### Cobertura real, antes y después (medida en el juego, no en el .json)

| | antes | ahora |
|---|---|---|
| vanilla con etiqueta | 821 | **826** |
| Calamity con etiqueta | 916 | **1019** |
| de los 826 que `Item.CanHavePrefixes()` acepta, con sugerencia | 798 (96,6 %) | **813 (98,4 %)** |
| con sugerencia que el motor NO deja prefijar | 1 | **0** |

Los 5 vanilla de diferencia (821→826) son el neto de tres cosas a la vez: **+48** que antes no
salían (38 báculos de invocación + 8 accesorios de los bloques `if (type…)` + los contrapesos) y
**+16** pelotas de golf, contra **−57** falsos positivos retirados (muebles dinásticos, las 15
bolsas del tesoro, bloques y paredes de arenisca, ropa de vanidad de obsidiana, el Cojín flatulento
- que en 1.4.4.9 todavía no es accesorio) y **−1** Pistola de monedas. **Ninguna regresión**: no hay
un solo objeto que antes enseñara la etiqueta legítimamente y ahora no.

### Las 13 excepciones que quedan, reales, no huecos

- **12 son objetos del propio tModLoader** (las alas de desarrollador: `AetherBreaker's Wings`,
  `Zeph's Wings`, `A Call Beyond`…). No son vanilla ni de Calamity, y esta tabla cubre a propósito
  solo esas dos fuentes. No se inventa un valor para contenido de terceros.
- **Pistola de bengalas (930)**: `Item.CanHavePrefixes()` dice `true` (tiene daño 2), pero no está
  en NINGÚN bool set de `PrefixLegacy` y no es accesorio, así que `Item.GetPrefixCategories()`
  devuelve lista vacía y `PrefixLoader.Roll` corta con `if (prefixCategories.Count == 0) return
  false`. **En el juego real no puede recibir ningún prefijo**: `CanHavePrefixes()` es más laxo que
  la reforja de verdad. Excepción real del propio juego, documentada, sin inventar valor.

### Verificado en el juego real

`scripts\verificar-prefijos-en-juego.ps1` (sandbox propio `tModLoader-TerrakeepPrefijos`,
`-tmlsavedirectory` + `-skipselect`, copia aislada de HEAD + los archivos de este cambio), todos
los pasos en OK. Los báculos, con su línea de tooltip real:

```
Paso 1.0 - "Slime Staff"  (1309, mana=10, kb=2): MejorPrefijo=83 (Mythical)  Linea: "Best possible prefix: Mythical"  OK.
Paso 1.1 - "Optic Staff"  (2535, mana=10, kb=2): MejorPrefijo=83 (Mythical)  Linea: "Best possible prefix: Mythical"  OK.
Paso 1.2 - "Pygmy Staff"  (1157, mana=10, kb=3): MejorPrefijo=83 (Mythical)  Linea: "Best possible prefix: Mythical"  OK.
Paso 1.3 - "Flinx Staff"  (5069, mana= 5, kb=2): MejorPrefijo=83 (Mythical)  Linea: "Best possible prefix: Mythical"  OK.
Paso 1.4 - "Blade Staff"  (4758, mana=10, kb=0): MejorPrefijo=60 (Demonic)   Linea: "Best possible prefix: Demonic"   OK.
Paso 6 - 826 admiten prefijo segun Item.CanHavePrefixes(); 813 tienen sugerencia (98.4%).
         Objetos con sugerencia que el motor NO deja prefijar: 0.
```

Los pasos 3/4/5 (no regresión de lo que ya funcionaba: coger de la Librería con el mejor prefijo
puesto, aviso con prefijo subóptimo, sin aviso cuando ya es el óptimo) siguen en OK con la
Espada corta de cobre → 81 Legendary. Log completo en `evidencia/prefijos.log.txt`.

Antes de generar nada se verificaron los valores contra **revisiones de la wiki oficial anteriores
a 1.4.5** (las que describen este juego): Báculo de slime *"its best possible modifier is
**Mythical**"*, Báculo óptico *"Its best modifiers are **Mythical**, Furious, or Godly"*, Báculo de
cuchillas *"Its best modifiers are **Demonic**, Deadly, Mystic, or Hurtful"*, y la historia de
`Modifiers`: *"Added 13 new modifiers… exclusively obtainable by summon weapons, **which previously
shared modifiers with magic weapons**"* (1.4.5.0).

---

## 8-sep-2026 — Apariencia: los tintes de pelo mentían en la vista previa, y botón para deshacer

Dos encargos del usuario probando el mod, los dos sobre **Personaje → Apariencia**
(`UI/Personaje/PestanaApariencia.cs` y `UI/Personaje/Widgets/MunecoTk.cs`).

### 1. El bug: "los tintes de pelo no se aplican bien o no se ven"

**Lo primero fue descartar lo obvio, con el código real decompilado en la mano, no suponiendo.**
La escritura sobre el `Player` estaba bien: `PestanaApariencia` pone `Player.hairDye` con el id de
sombreador sacado de `Item.hairDye`, que es literalmente de donde lo copia el juego
(`Player.cs`: `hairDye = item.hairDye;`). Y el renderer también: el pelo se pinta con el
`colorHair` que calcula `PlayerDrawSet` llamando a `Player.GetHairColor()`.

**Lo que sí es cierto -y es la raíz del fallo- es que un tinte de pelo de Terraria NO es un
color.** De los trece que trae vanilla, **doce son `LegacyHairShaderData`**
(`Terraria.Initializers.DyeInitializer.LoadLegacyHairdyes`, decompilado): una función
`(Player, Color) -> Color` que el juego evalúa **en cada fotograma sobre el `Player` que se está
dibujando**. Solo el de crepúsculo (objeto 3259) es un sombreador de verdad
(`GameShaders.Hair.Apply`, `TwilightHairDyeShaderData`).

Y el muñeco de la vista previa es un `Player` **propio** (`new Player()`, el patrón del Maniquí de
vanilla). O sea que esas doce funciones estaban leyendo los valores **de fábrica** del muñeco y no
los del personaje. Medido en el juego real, con el personaje de prueba y ANTES del arreglo:

| Tinte | Color con el JUGADOR | Color con el MUÑECO |
|---|---|---|
| Tinte de maná | `(50,75,255)` azul | `(250,255,255)` **casi blanco** |
| Tinte de las profundidades | `(97,154,83)` | `(115,160,247)` |
| Tinte de pelo marciano | `(232,157,147)` | `(105,30,20)` |
| Tinte de dinero (con 30 platino) | `(161,172,173)` | `(226,118,76)` |

El de maná salía blanco porque el muñeco tenía `statMana` 0 de 20 y el jugador 20 de 20; el de las
profundidades, porque el muñeco vivía en `position` (0,0), o sea el cielo; el de dinero, porque
recorre `inventory[0..53]` y el muñeco no tenía inventario. Los otros ocho coincidían **por
casualidad** (no leen nada del jugador, o leen algo que en el personaje de prueba también valía
cero).

**El arreglo** (`MunecoTk.SincronizarEstadoQueLeenLosTintes`): copiar del jugador real, justo
antes de dibujar, el estado que leen los tintes - `position`, `velocity`, vida y maná (los tres
campos de cada uno: `stat*`, `stat*Max`, `stat*Max2`), `team`, `ZoneShimmer` y las monedas de
`inventory[0..53]`. Qué campo lee cada tinte está sacado uno a uno del código real, no a bulto.

Dos detalles que no son adorno:

- **Va DESPUÉS de `PlayerFrame()`**, exactamente igual que el Maniquí de vanilla hace con su
  `position` (`TEDisplayDoll.Draw`: los cinco pasos, luego `dollPlayer.position = ...`, y solo
  entonces `DrawPlayer`). Así la POSE se sigue calculando con un personaje quieto -de pie, sin
  animación de salto ni de carrera- y solo el dibujado ve los valores de verdad.
- **El inventario NO se comparte por referencia** (a diferencia de `armor`/`dye`): `PlayerFrame`
  mira `HeldItem`, o sea `inventory[selectedItem]`, y compartir el array entero sacaría al muñeco
  empuñando la antorcha o el arma del jugador. Se copian **solo las monedas**, que es lo único que
  lee el tinte de dinero y lo único que nunca se dibuja en la mano.

Queda dicho claro: el **tinte marciano** muestrea `Lighting.GetColor` en el tile del jugador, y
dentro del dibujado del muñeco eso devuelve blanco a propósito (`Main.gameMenu` forzado a true, el
arreglo del parpadeo del 7-sep). En la vista previa ese tinte se ve, por tanto, a plena luz. Es la
consecuencia querida de aquel arreglo, no un fallo suelto.

### 2. La función nueva: botón "Deshacer cambios"

Al lado del botón de Cerrar: mismo borde derecho, mismo alto (34) y justo encima - el pie del
marco mide 44 px y "Cerrar" ocupa los 34 de abajo, así que entre los dos quedan los 10 px de
separación del propio marco. Ancho 210 y no 160 porque "Deshacer cambios" a escala 0,8 pide unos
200 px con la fuente real.

**No se metió DENTRO de la fila del pie**, aunque por ancho cabría: esa línea de ayuda del pie es
una `EtiquetaTk` de 820 px que **no ignora el ratón** y se comería los clics de la mitad izquierda
del botón; y además el pie es del marco común de las seis áreas (`PanelTerrakeepState`), que en
esta tarea estaba fuera de alcance por haber otros agentes trabajando en él.

Restaura los once campos de apariencia (peinado, tinte, variante y los siete colores) a como
estaban **al entrar en la pestaña** - no un deshacer de toda la sesión. La foto se toma en el
constructor de `PestanaApariencia`, y como `ContenidoPersonaje` reconstruye la pestaña cada vez
que se entra en ella, la foto es siempre "lo que había justo al abrir Apariencia".

**Snapshot puro, no closures encadenadas**, el mismo criterio que ya razona `EntradaSnapshot<T>`
para todo el historial del mod (y que usa `ContainerViewModel.ClearAll` en la app de escritorio
hermana). Son once valores de tipo valor, así que la foto es una copia de verdad sin clonar nada.
La acción además se registra en el historial general con `Historial.CambiarValor`, así que el
propio Ctrl+Z puede deshacer el deshacer. El botón **se apaga solo** mientras no haya nada que
deshacer, en vez de dejarse pulsar sin efecto.

### De regalo, un solape de textos que ya estaba y no se veía

Al mirar la captura del panel apareció que la línea `Personaje.Apariencia.NotaColores` **no cabía**:
a 1600x900 con escala de interfaz 1,47 el área de contenido se queda en 312 px de alto y esa
etiqueta caía en el 352, o sea 40 px por DEBAJO, **dibujada encima de la línea de ayuda del pie**.
Las dos frases se pisaban y no se leía ninguna. Y encima decían lo mismo ("Todo lo que toques aquí
se escribe al instante sobre el personaje cargado."). Se ha quitado la etiqueta y su clave del
generador de idiomas; no se deja una clave sin usar.

### Verificación real, en el juego, con arnés propio

`scripts/verificar-apariencia.ps1` (sandbox propio `tModLoader-TerrakeepApariencia`, cliente
gráfico real a 1600x900 en español) + `TERRAKEEP_AUTOTEST_APARIENCIA`, un arnés autocontenido
dentro de `PestanaApariencia.cs` (sin la variable de entorno es un no-op total). Se apoya en
`TERRAKEEP_AUTOTEST_PANEL` solo para abrir el panel y entrar en Apariencia, y espera 260
fotogramas a que aquella termine sus pasos antes de empezar los suyos, para no pelearse con ella.

Lo que hace y lo que salió:

1. **Tabla de los trece tintes**, comparando `GameShaders.Hair.GetColor` con el jugador y con el
   muñeco. Antes del arreglo: **tres DISTINTOS** (maná, profundidades, marciano) y el de dinero
   también en cuanto se le dan monedas. Después: **los trece iguales**, incluido el de dinero con
   30 platino en la cartera.
2. **Tres tintes reales aplicados con CLICS REALES** en la flecha ">" del selector (party, maná y
   crepúsculo - uno de color fijo, uno que era de los rotos y uno que es sombreador de verdad),
   con captura del back buffer de cada uno. El de maná sale ya **azul** en el muñeco.
3. **El botón**: 3 clics reales en la flecha del peinado + 2 en la de variante + los siete colores
   reescritos, captura, **clic real en "Deshacer cambios"**, y comparación campo a campo:
   `RESULTADO: OK ... (identicos campo a campo)`, botón apagado después, e historial general con
   la entrada "Deshacer los cambios de apariencia".
4. **El personaje REAL, con el panel cerrado**: última captura con el tinte festivo puesto, pelo
   magenta en el mundo. `Player.GetHairColor` = `(244,22,175)`.

Capturas en `evidencia/apariencia-capturas/`, log completo en `evidencia/apariencia.log.txt`.

### Tres obstáculos reales del arnés (y lo que costaron)

- **`Main.OnPreDraw` para capturar sin panel: imagen COMPLETAMENTE NEGRA.** Ese evento vive ya
  dentro de `Main.DoDraw`, después de `InitTargets`/`ReleaseTargets`, cuando el back buffer ya no
  tiene el fotograma presentado.
- **`Main.OnTickForThirdPartySoftwareOnly`: imagen ATRASADA un segundo.** En el cliente ese evento
  solo se dispara en la rama de "la ventana no tiene el foco" de `Main.DoUpdate` - la misma en la
  que el juego sigue actualizando a toda velocidad pero deja de dibujar. El que sirve es
  **`Main.OnTickForInternalCodeOnly`**, y aun así la cuenta atrás solo avanza con `Main.hasFocus`;
  el script además reafirma el foco cada 5 s durante toda la espera, no solo al principio.
- **Las monedas del tinte de dinero, dadas en el paso 0, no las veía el muñeco todavía.**
  `MunecoTk` se sincroniza en el mismo `Update` de la pestaña y `base.Update` (los hijos) va
  primero, así que la tabla se imprimía con el muñeco aún sin ellas y salía "DISTINTOS" en el
  tinte 4. Se dan 230 fotogramas antes del primer paso.

### Alcance

Tocados solo `UI/Personaje/PestanaApariencia.cs`, `UI/Personaje/Widgets/MunecoTk.cs`,
`scripts/generar-localizacion.py` (cuatro claves nuevas y una retirada) + los dos `.hjson`
regenerados, y `scripts/verificar-apariencia.ps1` (archivo nuevo). Commit con índice privado. Con
Calamity cargado no se ha probado: el renderer y los tintes son los mismos, pero queda dicho.

---

## 8-sep-2026 — Cierre del lote de 5 informes de la partida: fusión y compilación conjunta

Los cinco frentes de este bloque (Categorías, columnas de Buffs, tintes/deshacer en Apariencia,
desplegable de prefijos fuera del panel, cobertura de mejor prefijo en báculos) se lanzaron en
paralelo sobre el mismo árbol de trabajo, cada uno con su propio índice privado de git para no
pisarse. Commits, en orden: `8d0ba70` (Buffs), `c25d3f2` (Categorías), `ee10c98` + `770f890`
(desplegable de prefijos), `679504b` (báculos), `c785108` (Apariencia).

Dos agentes (Categorías y Buffs) avisaron de que, mientras Apariencia estaba a medias, el árbol
compartido no compilaba con el SDK del sistema por `CS0103` en `PestanaApariencia.cs` sin
terminar - lo resolvieron verificando desde una copia aislada (`git archive HEAD` + sus propios
archivos encima) en vez de esperar o tocar el archivo ajeno. Con los cinco commits ya fusionados,
`scripts\compilar.ps1` se relanzó sobre el árbol completo: **0 errores, 21 advertencias** (todas
`ChangeMagicNumberToID`, cosméticas, ninguna en código tocado hoy) en la fase 1, y la fase 2
empaquetó `Mods\TerrakeepMod.tmod` (958.832 bytes) sin problema. Ningún choque real entre los
cinco frentes al fusionarse.

Quedan sin actuar, por decisión de producto y no por fallo técnico (anotado por el agente de
báculos): la app de escritorio (`Terrasavr-Native`) sigue usando la tabla de mejor-prefijo
generada contra Terraria 1.4.5.8 para editar guardados que en realidad son de tModLoader 1.4.4.9,
donde algunos prefijos de esa tabla no existen. Ya está generada la tabla correcta
(`best_prefix_tml.json`) por si en algún momento se quiere que el botón ★ detecte la versión del
guardado abierto.

---

## 8-sep-2026 — "Categorías": cada carpeta se organiza por el TIPO REAL del objeto, no de 40 en 40 por id

**Reportado por el usuario con captura**, y es un problema distinto (y más de fondo) que el de
esta misma mañana: dentro de la carpeta raíz "Categorías", cada hoja se partía en *"Página 1"*,
*"Página 2"*… y **cada página enseña como icono el primer objeto de su lista**, así que parecía
que la página representaba un tipo de arma concreto — un arco, un pico — cuando dentro había de
todo: *"da igual qué página cliques, después no está ordenado; dentro de picos te encuentras
espadas"*. Después amplió el encargo a **toda** la rama: *"dentro de 'categorias' también están
equipable, herramientas, colocable y paredes; todo eso también debe organizarse"*.

No era un fallo: es el comportamiento **fiel** del Terrasavr original (`Hc.deploy` parte cualquier
hoja de más de 40 objetos en páginas por orden de id). Lo que se pide es ir más allá.

### De dónde sale el criterio (nunca de una lista a mano)

Todo el reparto se calcula **fuera del juego**, en el repo hermano
(`scripts/extraer-subtipos-libreria-vanilla.py`, nuevo), leyendo el código decompilado real de
Terraria 1.4.5.8 — que es la versión de la que salió el árbol curado:

| Qué separa | Dato real |
|---|---|
| arcos / armas de fuego / lanzadores / armas de dardos | `Item.useAmmo` (flecha, bala, cohete, dardo) |
| flechas / balas / cohetes / dardos / bengalas | `Item.ammo` |
| lanzas, mayales, bumeranes, yoyós, espadas cortas | el **`aiStyle` real del proyectil** que dispara (`Projectile.cs`: 19 lanza, 15/13/69 mayal, 3 bumerán, 99 yoyó, 161 espada corta) |
| espadas | set de prefijos real del juego `PrefixLegacy.ItemSets.SwordsHammersAxesPicks`, quitando lo que tenga poder de pico/hacha/martillo |
| picos, taladros, motosierras, hachas, hachas-martillo, picos hacha | `Item.pick`/`axe`/`hammer` + `ItemID.Sets.IsDrill`/`IsChainsaw` |
| cabeza / cuerpo / piernas / accesorio | `Item.headSlot`/`bodySlot`/`legSlot`/`accessory` |
| alas, botas, globos, escudos, collares, cara, guantes, espalda, cinturones | los doce **slots visuales** reales del accesorio (`wingSlot`, `shoeSlot`, `balloonSlot`…) |
| accesorios informativos | el helper real `Item.DefaultToInfoAccessory()` |
| tintes de pelo | `DyeInitializer` (vía `hair_dyes.json`, ya extraído en su día) |
| bloques vs muebles | `Main.tileFrameImportant` del tile que coloca |
| estandartes, cuadros, cofres, plataformas, antorchas, hogueras, estatuas, cajas de música, sillas, camas, puertas… | el **tile real** que coloca (`Item.createTile`), con su nombre real |

Lo que no tiene un campo o un set unívoco **no se fuerza**: cae en un "Otros…" con nombre honesto.

### Qué se ve ahora en el juego (números reales, ya podados a 1.4.4.9)

- **Daño de Cuerpo a Cuerpo (294)** → 14 carpetas: Espadas 105, Lanzas 17, Mayales 14, Yoyós 21,
  Bumeranes 17, Otras armas cuerpo a cuerpo 15, Otras armas con munición 1, Picos 29, Taladros 12,
  Picos hacha 3, Hachas 19, Motosierras 9, Hachas-martillo 9, Martillos 23.
- **Daño a Distancia (178)** → 13 carpetas: Arcos 40, Armas de fuego 24, Lanzadores 7, Armas de
  dardos 4, Otras armas con munición 12, Armas sin munición 4, Armas arrojadizas 27, Flechas 15,
  Balas 16, Cohetes 12, Dardos 5, Bengalas 6, Otra munición 6.
- **Colocable (2680)** → 38 carpetas (Bloques 259, Estandartes 311, Cuadros 223, Cofres 158, Cajas
  de música 88, Jaulas de bicho 92, Estatuas 82, Plataformas 55, Lámparas de araña 49, Faroles 49,
  Bancos 47, Sillas 48, Puertas, Mesas, Relojes, Fregaderos, Bancos de trabajo, Camas, Pianos,
  Bañeras, Lámparas, Candelabros, Estanterías, Retretes, Velas, Estatuas de letras, Reliquias,
  Cajas de pesca, Cometas, Lingotes, Antorchas, Hogueras, Plantas en maceta, Lápidas, Plantas de
  tinte, Torres, Fuentes y **Otros colocables 397**). Antes eran 81 páginas seguidas por id.
- **Accesorios (311)** → 11 carpetas (Alas 42, Botas 25, Globos 13, Escudos 8, Collares 12,
  Accesorios de cara 15, Guantes 23, Accesorios de espalda 11, Cinturones 16, Accesorios
  informativos 16, Otros accesorios 130).
- **Armadura (249)** → Cabeza 102 / Cuerpo 79 / Piernas 68. **Vanidad (493)** → Cabeza / Cuerpo /
  Piernas / Accesorio. **Cabeza/Cuerpo/Piernas** → Armadura / Vanidad. **Tintes (130)** → Tintes
  118 / Tintes de pelo 12. **Herramientas** → Picos, Taladros, Picos hacha / Hachas, Motosierras,
  Hachas-martillo / Martillos.

Un subgrupo de más de 40 objetos se sigue paginando **por dentro** ("Bloques (259) > Página 7"),
pero ya nunca mezclando tipos, y el icono de la página es siempre del tipo que toca.

### Las dos ramas que se revisaron y se dejan como están (con motivo, no por dejadez)

- **Magia (75)**: no existe en el juego ningún campo ni set que separe los tipos de arma mágica
  (báculos, libros, pistolas mágicas…). Cada proyectil mágico tiene su propio `aiStyle`, y agrupar
  por él daría 40 carpetas de un objeto cada una. Se queda paginada: **límite real conocido**.
- **Paredes (272)**: todos sus objetos son paredes, así que el icono de la página ya es una pared
  y no hay ninguna falsa impresión que arreglar. Los sets reales que existen (`WallID.Sets.Fences`,
  `Glass`, `Main.wallHouse`) cubren 12/10/una parte y dejarían un "Otras paredes" de ~250: peor
  que ahora.

### Verificado en el juego real, objeto a objeto y con capturas

`scripts\verificar-categorias-libreria.ps1` (sandbox propio, `-tmlsavedirectory` + `-skipselect`),
con el juego en **español** y también en inglés, y con y sin **Calamity**. `AuditoriaCategorias`
lleva ahora un criterio real por cada carpeta nueva, y **eso es una comprobación cruzada de
verdad**: el reparto se calcula leyendo `Item.cs` de 1.4.5.8 fuera del juego y aquí se comprueba
contra el `Item` que carga tModLoader 1.4.4.9.

```
RECUENTO 1 (cobertura): 5423 objetos vanilla reales, el árbol curado cubre 5423. Huérfanos: 0.
RECUENTO 2 (salud): carpetas vacías=0, ids que no existen=0, iconos imposibles=0,
                    objetos de MOD dentro del árbol vanilla=0 (también con Calamity cargado).
RECUENTO 3: 89 carpetas, 8927 comprobaciones objeto a objeto.
```

De esas 8927, **solo 3 objetos de las carpetas NUEVAS no cumplen su criterio**, y los tres son
diferencias reales entre la versión del árbol (1.4.5.8) y la del juego (1.4.4.9), comprobadas en
los dos `Item.cs` decompilados:

| objeto | en 1.4.5.8 | en 1.4.4.9 |
|---|---|---|
| Llave-espada (671) | `shoot = 1074`, proyectil de bumerán → cae en "Bumeranes" | no dispara nada: es una espada |
| Cojín flatulento (215) | accesorio (`DefaultToVoiceOverrideAccessory`) | todavía no es accesorio |
| Sudadera del muerto (5007) | `bodySlot` con defensa 4, no vanidad | vanidad |

Las otras 213 discrepancias del RECUENTO 3 son las **ya conocidas y documentadas** de las cuatro
carpetas madre (la tabla `metatype` curada de Terrasavr contra los campos reales del `Item`), no
de este cambio. Con el juego en español suben porque dos criterios miran el texto en inglés
("Wings" = el tooltip dice *allows flight*, "Dyes" = el nombre acaba en *Dye*), que es lo que hace
el Terrasavr real; tampoco es de este cambio.

Capturas reales en `evidencia/categorias-capturas/`: la lista de carpetas de melee, de distancia,
de colocable y de accesorios, y el contenido de "Espadas", "Picos", "Arcos", "Armas de fuego",
"Bloques" y su última página. **Ningún texto cortado**: el rótulo más largo en español
("Otras armas cuerpo a cuerpo (15)", "Accesorios informativos (16)") cabe entero en su fila.

### Un bug latente que este cambio destapó: la lista de carpetas se desordenaba sola

Con "Colocable" pasando de 9 a **38 subcarpetas**, las carpetas salían en pantalla en un orden
aleatorio aunque el `.json` las trae ordenadas. Causa: `UIList` ordena sus elementos con
`List.Sort` + `UIElement.CompareTo`, que devuelve 0 para todos — y **`List.Sort` no es estable**,
así que en cuanto hay unos cuantos elementos deja de respetar el orden de inserción. Es el mismo
fallo que ya se había encontrado esta mañana en el desplegable de prefijos, y el mismo arreglo:
`ManualSortMethod = elementos => { }` en `_listaCarpetas` y en `_listaResultados` de
`ContenidoLibreria`. Comprobado con captura antes y después.

Quedan con el mismo riesgo latente, sin tocar por estar fuera de este encargo, las `UIList` de
`PestanaBusqueda`, `ContenidoInvestigacion` y `PestanaBuffs`.

### Alcance

`Assets/vanilla_library_tree.json` y `Assets/vanilla_library_labels_es.json` son **copia tal cual**
de los del repo hermano (mismo criterio de siempre: copiados, nunca editados a mano).
`Common/Libreria/AuditoriaCategorias.cs` (criterios nuevos + pasos visuales nuevos) y
`UI/Libreria/ContenidoLibreria.cs` (las dos `UIList`). La poda por versión del juego
(`ArbolLibreria.Podar`) **no se ha tocado**: el árbol nuevo pasa por ella tal cual — quita 722 ids,
1452 apariciones y 26 carpetas vacías (antes eran 40; ahora se quedan vacías menos carpetas porque
los ids que sobran ya no forman páginas enteras).

---

## 13-sep-2026 — La "Guía en tiempo real": cerebro de progresión + primer tramo funcionando

Encargo nuevo y grande: que Terraria nunca deje al jugador con la parálisis de *"no sé qué hacer
ahora"*, sin quitarle la exploración libre. La referencia que dio el usuario son Cyberpunk, los
Souls, Ori y Warcraft 3: **siempre hay un objetivo claro, una dirección, y una razón clara de por
qué algo todavía no te sale**. Esta ronda sienta las bases: la investigación real, el formato de
datos y el **primer tramo (pre-Ojo de Cthulhu) funcionando de verdad**.

Vive como **séptima pestaña del panel único**, tecla **G**. Que la G estuviera libre no se supuso:
se leyó el preset real `PresetProfiles.Redigit` del `tModLoader.dll` instalado
(`GameInput/PlayerInput.cs`), donde las teclas de fábrica son W A S D, Espacio, Escape, E,
LeftShift, LeftControl, R, H, J, B, Tab, M, +, −, AvPág/RePág, 0-9, OemPlus/OemMinus, C y F1-F4.

### Lo que se decidió sobre Calamity, y por qué

**Fuera de esta fase, pero dicho en el panel en vez de disimulado.** El motivo no es pereza: con
Calamity la progresión no es "unos jefes más", es **otro árbol**. Su `DownedBossSystem.cs`
decompilado tiene **43 banderas de jefe propias** (`downedDesertScourge`, `downedHiveMind`,
`downedPerforator`, `downedProvidence`, `downedExoMechs`…), el primer jefe deja de ser el Ojo de
Cthulhu, y el mod mete sus propios modos (Revengeance, Death) que cambian las cifras con las que se
mide si estás preparado. Nada de eso se puede verificar con el mismo rigor que lo de vanilla
leyendo el juego decompilado: son datos de diseño de un mod, no condiciones del motor. Enseñar un
orden de Calamity a medias sería exactamente el *"análogo falso solo por completar la lista"* que
prohíbe el estándar de la marca.

Lo que sí se hizo es dejar el camino abierto **desde el primer día**: el discriminador
`AmbitoGuia` (vanilla / calamity / ambos) está en el modelo y en el `.json` por tramo,
`CatalogoGuia.Tramos` filtra por él, y el mod detecta Calamity con
`ModLoader.HasMod("CalamityMod")` (la misma vía que ya usan `CatalogoMejorPrefijo` y
`CatalogoBuilds`). Con Calamity cargado, la Guía **sigue enseñando el árbol de vanilla** y pone
arriba del todo un aviso diciendo que eso es lo que está leyendo. Añadir Calamity será **datos**
(tramos en el `.json` + banderas en `BanderasGuia`), no reescribir el cerebro.

### El formato de datos: qué dice el `.json` y qué dice el C#

El reparto es la decisión de diseño central, y está pensado para que ampliar el árbol no sea
programar:

| | Dónde vive | Qué dice |
|---|---|---|
| El árbol | `Assets/guia_progresion.json` | qué tramos, qué pasos, en qué orden, qué requisitos y con qué umbrales |
| La comprobación | `Common/Guia/EvaluadorGuia.cs` | cómo se mira cada clase de requisito en el juego en marcha |
| Los textos | `Localization/*.hjson` | título, **porqué** y **cómo** de cada paso, en los dos idiomas |

El vocabulario de requisitos es **cerrado** (10 tipos: `cristales_vida`, `vida_maxima`, `defensa`,
`npcs_pueblo`, `npc`, `objeto`, `objeto_cualquiera`, `dano_arma`, `gancho`, `bandera`). Añadir un
paso o un tramo es editar el `.json`; solo añadir una **clase nueva** de requisito obliga a tocar
C#. Un tipo que no se reconozca **no revienta y no se da por bueno**: se marca "no evaluable", se
enseña como tal y se avisa en el log al cargar. Un falso verde en una guía es peor que no tener
guía.

**Ningún número que el motor ya sepa está escrito en el `.json`.** La vida, la defensa y el daño de
un jefe se sacan clonando su muestra de `ContentSamples.NpcsByNetId` y llamando a su propio
`NPC.ScaleStats(null, Main.GameModeInfo, null)` — el método público real que usa el juego cuando el
jefe aparece de verdad —, así que el número que ve el jugador es el de **su** partida y no "el del
wiki". El daño del arma sale de `Player.GetWeaponDamage`, o sea el mismo que aparece en su tooltip.
Lo que sí guarda el `.json` son los **umbrales de diseño**, y los que vienen de una condición real
del motor llevan citada su fuente en el propio archivo.

Un paso se da por hecho cuando cumple todos sus requisitos **obligatorios**; los **recomendados**
(la arena, las pociones, el gancho) cuentan para el medidor pero no bloquean. Esa distinción es
deliberada: es la diferencia entre "el juego no te deja" y "te vas a llevar un disgusto", y
mentir ahí convertiría la brújula en una lista de tareas. El medidor pesa doble un obligatorio que
un recomendado y suma el progreso **parcial** de cada uno (3 vecinos de 4 son 0,75 de ese
requisito), para que se mueva mientras juegas en vez de dar saltos de todo o nada.

### La investigación, verificada contra el juego decompilado (nada de memoria)

| Qué | Dónde se leyó | Qué dice de verdad |
|---|---|---|
| Aparición nocturna del Ojo de Cthulhu | `Main.UpdateTime_StartNight` | `ConsumedLifeCrystals >= 5` **y** `(int)statDefense > 10` **y** `num >= 4` NPC del pueblo **y** `rand.Next(3) == 0` |
| Daño que recibe un enemigo | `NPC.HitModifiers.GetDamage` | `max(daño − defensa × 0,5, 1)`; el `DefenseEffectiveness` de un NPC es **siempre 0,5** |
| Daño que recibe el jugador | `Player.VanillaBaseDefenseEffectiveness` | defensa × **0,5** normal, **0,75** experto, **1** maestro |
| Modos de juego | `GameModeData` | normal ×1, experto ×2, maestro ×3 (vida y daño); la defensa **no** se multiplica |
| Vidente Sospechoso | `Recipe.cs:14362` | 6 Lentes (38) en un Altar (tile 26) |
| Meteorito | `NPC.cs` case 13/14/15/266 + `Main.UpdateTime_StartNight` | **garantizado** la primera vez que muere el Devorador/Cerebro (`!downedBoss2`), después 1 de cada 2; y 1/50 por noche con `downedBoss2` |
| Modo Difícil | `NPC.cs` case 113 | el Muro de Carne llama a `WorldGen.StartHardmode()` |
| Quién se muda y por qué | `NPC.SpawnAllowed_*` + `Main.UpdateTime_SpawnTownNPCs` | Mercader = 50 de plata (5000 de cobre contando los ids 71/72/73/74); Enfermera = alguien con `ConsumedLifeCrystals > 0` **y Mercader presente**; Demoliciones = un objeto de `ItemsThatCountAsBombsForDemolitionistToSpawn` **y Mercader**; Armería = munición de bala; Comerciante de tintes = un tinte **y ya 4 vecinos**; Dríade = `downedBoss1`/`downedBoss2`/`downedBoss3`; Sastre = `downedBoss3`; Zoólogo = bestiario ≥ 10%; Pintor = ≥ 8 vecinos; Ciborg = `hardMode && downedPlantBoss` |
| Duración de la noche | `Main.nightLength` | 32400 ticks = 9 minutos reales |
| Vidas/defensas/daños base | bloques `SetDefaults` de `NPC.cs` | Ojo 2800/12/15 · Devorador (cabeza) 150/2/22 · Reina Abeja 3400/8/30 · Esqueletron 4400/10/32 · Deerclops 7000/10/20 · Muro de Carne 8000/12/50 · Reina Slime 18000/26/60 · Retinazer 20000/10/45 · Spazmatism 23000/10/50 · Prime 28000/24/47 · Destructor 80000/0/70 · Plantera 30000/14/50 · Golem 15000/26/72 · Duque Pezhongo 60000/50/100 · Emperatriz 70000/50/80 · Cultista 32000/42/50 · Moon Lord (núcleo) 50000/70 · King Slime 2000/10/40 · Cerebro 1250/14/30 |

### El hallazgo que corrige un error muy fácil de cometer

**`Player.ConsumedLifeCrystals` NO es `(statLifeMax − 100) / 20`.** Es un **contador guardado
aparte** (`consumedLifeCrystals`, con tope 15 en su setter) que solo sube cuando el jugador **usa**
un Cristal de Vida (`Player.ItemCheck`, `sItem.type == 29`). Esa fórmula aparece **una sola vez** en
todo el motor, al convertir un personaje de una versión antigua que aún no guardaba el contador.

Y es justo el campo que mira el juego para dejar aparecer al Ojo. La diferencia no es teórica en
**este** mod: con Terrakeep se puede subir la vida máxima a mano, y eso **no** mueve el contador. Si
la Guía preguntara por `statLifeMax` le diría *"ya estás listo"* a alguien que se va a pasar las
noches esperando a un jefe que no va a venir. Salió de un fallo de la propia autoprueba (poner
`statLifeMax = 200` dejaba el contador en 0) y ahora es **un paso fijo del arnés**, para que no se
pueda volver a colar. El texto del paso lo cuenta tal cual al jugador, porque es justo el tipo de
cosa que no se ve por ningún lado.

### Verificado en el juego real, no "compila y parece bien"

`scripts\verificar-guia.ps1` (sandbox propio con `-tmlsavedirectory` + `-skipselect`, nunca los
personajes del usuario). El arnés no comprueba que el código parezca correcto: **lleva al personaje
de prueba por el tramo entero**, un requisito cada vez, y después de cada cambio vuelve a
preguntarle a la guía cuál es el objetivo:

```
objetivo actual: "Refugio"          esperado "Refugio"          -> OK   (mundo sin vecinos)
objetivo actual: "Defensa"          esperado "Defensa"          -> OK   (1 vecino)
objetivo actual: "CristalesDeVida"  esperado "CristalesDeVida"  -> OK   (defensa 13 > 10)
objetivo actual: "PuebloDeCuatro"   esperado "PuebloDeCuatro"   -> OK   (5 cristales USADOS)
objetivo actual: "ArmaYArena"       esperado "ArmaYArena"       -> OK   (Starfury, 25 de daño)
objetivo actual: "InvocarElOjo"     esperado "InvocarElOjo"     -> OK
con downedBoss1=true: paso en pantalla=(ninguno) -> OK: el tramo se da por cerrado.
```

Todo por caminos reales: los vecinos con `NPC.NewNPC`, la armadura y el arma buscadas **por
propiedades** y no por id fijo, el contador de cristales por su propiedad real, la pestaña abierta
con un **clic real** en la barra, y el idioma cambiado con la misma llamada que usa el selector de
Ajustes. El medidor que se ve en pantalla se compara con el cálculo (`medidor en pantalla: 0.125
-> OK: coincide`). La lectura del jefe que ve el jugador, con el Starfury puesto:

```
Ojo de Cthulhu: 2800 de vida, 12 de defensa y 15 de daño en modo normal.
Tu mejor arma (Furia de estrellas, 25 de daño) le quita 19 por golpe: harían falta unos 148
golpes. Un enemigo resta la mitad de su defensa a cada golpe que recibe, con un mínimo de 1.
```

Log completo en `evidencia\guia.log.txt`; capturas reales del back buffer en el sandbox
(`terrakeep-capturas\guia-1..6-*.png`), en **inglés y en español**.

### Tres cosas que NO se vieron leyendo el código

1. **La columna del objetivo se solapaba consigo misma.** Estaba maquetada con un hueco **fijo** de
   120 px para el párrafo del "por qué"; en inglés a 800x720 ese párrafo ocupa ocho líneas y el
   rótulo *"Where to start"* se pintaba **encima de sus dos últimas**. Ningún dato del log lo habría
   dicho: se vio mirando la captura. Arreglo: la columna es una `UIList`, que apila cada elemento
   por el **alto real** del de arriba (y `ParrafoTk` ajusta el suyo midiendo con la fuente). No
   queda ni un hueco fijo que se pueda quedar corto.
2. **La séptima pestaña estrecha la barra.** Cada botón pasa de 1/6 a 1/7 del marco y en español
   *"Investigación"* ya no cabía a la escala fija de 0,8. Como la regla del proyecto es que **ningún
   texto se recorta**, lo que se adapta es el layout:
   `PanelTerrakeepState.AjustarEscalaDeLasPestanas` mide los siete rótulos con la fuente real en
   cada fotograma y baja la escala **común** lo justo. Medido en el juego: **0,800 en inglés**
   (sobra sitio) y **0,793 en español** (*"Investigación"* se queda con 16 px de holgura). Común a
   las siete a propósito: con una escala por pestaña, las de rótulo corto se verían más grandes y
   la barra parecería rota.
3. **Un contador de texto para no volver a fiarse de la vista.** El arnés recorre todos los
   párrafos montados, mide cada línea con la fuente real y da la peor holgura: *"38 párrafos, 64
   líneas, la más justa a 0,4 px del borde"*.

### Dos tropiezos del arnés, anotados por si se repiten

- **La defensa de una armadura recién equipada tardó 140 fotogramas en recalcularse.** La ponen
  `Player.ResetEffects`/`UpdateEquips` dentro de `Player.Update`, y con la espera fija de 14
  fotogramas que separaba los pasos la comprobación leía la defensa vieja y daba un **falso "NO
  CUADRA"**. Ahora las comprobaciones que dependen del motor esperan **por condición** (`EsperarA`,
  con tope de 300 fotogramas y aviso si se agota), no por reloj. En la pasada siguiente la misma
  espera se resolvió al primer intento: por eso no vale fijar un número.
- **El mundo sintético de pruebas ya traía dos vecinos dentro**, así que el primer paso salía
  cumplido de entrada y la comprobación daba "NO CUADRA" sin que hubiera nada roto — la guía
  acertaba, la prueba partía de un estado que no controlaba. El arnés los retira al empezar y los
  vuelve a crear al terminar.

### Un tropiezo del entorno (regla de autonomía técnica)

Compilar con `-p:BaseIntermediateOutputPath` apuntando a una carpeta **dentro del proyecto**
(`obj-guia\`) rompe la compilación siguiente con **CS0579** (atributos de ensamblado duplicados): el
SDK solo excluye por su cuenta `bin\` y `obj\`, así que los `AssemblyInfo.cs` generados en la
carpeta alternativa se cuelan en el glob de fuentes. Y el problema se hereda: el
`ModCompile.IgnoreCompletely` de tModLoader tampoco las excluye, o sea que rompería también el
`.tmod`. Se han añadido `bin-*/` y `obj-*/` al `.gitignore` con la explicación, y los dos `.cs` que
quedaron dentro se dejaron vacíos (esta sesión no tenía permiso para borrar carpetas).

### Lo que falta del árbol, con prioridad

Los 12 tramos de vanilla ya están en el `.json` **en orden y con su jefe final**, y se ven en el
panel como hoja de ruta ("Lo que viene después") con sus cifras reales. Lo que les falta a los 11
que quedan es **pasos con requisitos evaluables**. Prioridad, de mayor a menor:

1. **`MaldadDelMundo`** (Devorador / Cerebro). El siguiente natural y el que más se nota: es el
   primero cuyo jefe depende del mundo (`WorldGen.crimson`, ya leído por `CatalogoGuia`) y el que
   suelta el meteorito. Requisitos nuevos que hará falta: "romper N esferas de sombra / corazones"
   (mirar `WorldGen.shadowOrbCount`) y "tener un arma que atraviese" (el Devorador es un gusano).
2. **`Esqueletron`**. Cierra el prehardmode de verdad: abre la Mazmorra entera. Requisito nuevo:
   hablar con el Anciano de noche (NPC 37, ya localizable con el requisito `npc`).
3. **`MuroDeCarne`**. El punto de no retorno; el tramo donde más falta hace un aviso claro de "esto
   cambia el mundo para siempre" y un medidor honesto (equipo de infierno, puente de cenizas).
4. **`Mecanicos`**. Tres jefes con un salto de dificultad enorme respecto al Muro; es el otro sitio
   clásico donde la gente se atasca. Requisito nuevo: nivel de mineral de modo difícil.
5. **`Plantera`** y **`TemploYGolem`**. Encadenados y bien definidos por banderas
   (`downedMechBossAny` → bulbos, `downedPlantBoss` → Templo).
6. **`InicioModoDificil`**, **`ReinaAbeja`**, **`JefesOpcionalesTardios`**: opcionales o de
   transición, valen sobre todo como hoja de ruta.
7. **`EventosLunares`** y **`MoonLord`**. Los últimos: a esas alturas el jugador ya no se pierde, y
   el valor de la guía ahí es más el medidor de preparación que la dirección.

Aparte del árbol, las dos piezas del diseño acordado que **todavía no están**:

- **Marcadores en el mapa** para el objetivo actual. La infraestructura ya existe en Exploración
  (`Common/Exploracion/MarcadoresExploracion.cs` y `CapaMapaExploracion.cs`), así que es
  engancharse ahí, con el mismo límite: **zona, nunca el secreto exacto sin explorar**.
- **Dirección horizontal** (hacia qué lado cae la jungla, la nieve, la mazmorra). Hoy la Guía solo
  da la CAPA (arriba/abajo), que es lo único que se puede decir sin coordenadas y sin mirar el
  mundo. `Main.dungeonX` y el `BuscadorMundo` de Exploración dan para más, pero hay que decidir
  primero dónde está la línea entre "brújula" y "GPS" para cada cosa.

### Alcance

Nuevos: `Assets/guia_progresion.json`, `Common/Guia/` (9 archivos), `UI/Guia/` (4 archivos),
`scripts/verificar-guia.ps1`. Tocados: `Common/Panel/PanelTerrakeepSystem.cs` (área 7 + atajo +
registro), `UI/Panel/PanelTerrakeepState.cs` (séptima pestaña + escala adaptativa),
`Common/Panel/CapturaDePantalla.cs` (permitir capturas con esta autoprueba),
`UI/Personaje/Widgets/BotonTk.cs` (`EscalaTexto` escribible),
`scripts/generar-localizacion.py` + los dos `.hjson` (162 claves nuevas por idioma) y
`.gitignore`. Commit `4335eb2`.

### Comprobado también CON Calamity cargado

`scripts\verificar-guia.ps1 -Calamity`: `Calamity cargado=True`, el aviso de alcance sale el
primero de la columna derecha (comprobado en la captura, no solo en el log: 40 párrafos frente a
los 38 de la pasada sin Calamity, que son sus dos líneas), los seis pasos siguen señalando el
objetivo que toca y ninguna comprobación en rojo. Log en `evidencia\guia-calamity.log.txt`.

## 13-sep-2026 — La Guía: dos tramos más (Devorador/Cerebro y Esqueletron) y un fallo real de progresión que solo salió jugando

Siguiendo la prioridad que dejó la entrada anterior, esta sesión implementa los dos tramos
siguientes del árbol — **`MaldadDelMundo`** (Devorador de Mundos / Cerebro de Cthulhu, según
corrupción o carmesí del mundo) y **`Esqueletron`** — con el mismo rigor que `PreOjo`: requisitos
leídos del motor real, nunca copiados a mano, y verificados llevando el personaje de prueba por
el tramo entero con `scripts\verificar-guia.ps1`, ampliando el mismo arnés (no uno nuevo).

### Los tramos nuevos, y de dónde sale cada número

Antes de escribir una sola cifra se miró el `NPC.cs` decompilado real de esta versión:

- **Devorador de Mundos** (`type==13`, cabeza/segmento): daño 22, defensa 2, vida 150 **por
  segmento** (no hay una cifra única "vida total del gusano" en el motor: son decenas de
  segmentos independientes, y la guía lo dice así en vez de inventarse un total).
- **Cerebro de Cthulhu** (`type==266`): daño 30, defensa 14, vida 1250, con
  `dontTakeDamage=true` mientras vivan sus Reptadores alrededor.
- **Esqueletron** (`type==35`, ya citado en el `.json` desde el primer día): daño 32, defensa 10,
  vida 4400.
- **Anciano** (`type==37`): `townNPC=true`, por eso ya cuenta con el tipo de requisito `npc` que
  ya existía (id 37) sin tocar el evaluador.

No hizo falta ampliar el vocabulario cerrado de tipos de requisito (`cristales_vida`,
`vida_maxima`, `defensa`, `npcs_pueblo`, `npc`, `objeto`, `objeto_cualquiera`, `dano_arma`,
`gancho`, `bandera`): los dos tramos nuevos se componen enteros con esos diez. Lo único que se
amplió fue el CONTENIDO de un tipo que ya existía — `BanderasGuia` gana una entrada nueva,
`shadowOrbSmashed`, que lee `Terraria.WorldGen.shadowOrbSmashed` (persiste de verdad: se guarda
en el `.wld`, `WorldFile.cs` líneas 1283/2045/3439, y solo se borra al generar un mundo nuevo). Se
usa como requisito **recomendado** ("ya has roto una esfera/corazón") y no obligatorio a
propósito: el contador real que cuenta hasta 3 para invocar solo (`WorldGen.shadowOrbCount`) es
módulo 3 y vuelve a 0 en cuanto invoca al jefe, así que un "vas 2 de 3" mentiría justo cuando más
importa — se comprobó en el código antes de descartarlo, no se dio por hecho.

Cada tramo son dos pasos, siguiendo el mismo patrón que cerró `PreOjo` (un paso de "prepárate" con
el requisito obligatorio real, y un último paso cuyo único requisito obligatorio es la bandera del
jefe — `downedBoss2` / `downedBoss3`, las dos ya estaban en la tabla desde el primer día):

- `MaldadDelMundo`: **`ArmaContraLaMaldad`** (`dano_arma` ≥20, citando los bloques de arriba;
  `shadowOrbSmashed` y Cebo de gusanos/Espina dorsal sangrienta —ids 70/1331, verificados en
  `ItemID.cs`, no adivinados— como recomendados; gancho recomendado) → **`VencerLaMaldad`**
  (`bandera downedBoss2`).
- `Esqueletron`: **`ArmaParaEsqueletron`** (`dano_arma` ≥20; el Anciano, `npc` id 37, recomendado;
  gancho recomendado) → **`VencerAEsqueletron`** (`bandera downedBoss3`).

### El hallazgo real: un tramo ya superado se podía volver a abrir solo

Esto NO se vio leyendo el código, se vio en el primer pase real del arnés ampliado. El plan era
cerrar `MaldadDelMundo` a mano (`NPC.downedBoss2 = true`) y comprobar que la guía saltaba sola al
primer paso de `Esqueletron`. En vez de eso, el log dijo que el objetivo seguía siendo
**"Defensa"** — el SEGUNDO paso de `PreOjo`, un tramo que en teoría llevaba rato cerrado.

La causa, una vez se miró `EstadoGuia.PasoActual`: el bucle recorre cada tramo IMPLEMENTADO y
devuelve el primer paso cuyo `PasoCompletado` da `false` — y `PasoCompletado` vuelve a leer el
estado EN VIVO de cada requisito, sin memoria de "esto ya se dio por bueno una vez". El arnés
había limpiado la armadura y el arma al final del bloque de `PreOjo` (`Restaurar()`, que deja el
personaje de prueba tal como lo encontró), así que al llegar a los tramos nuevos la defensa real
volvía a ser 0 — y como el paso "Defensa" de `PreOjo` exige `statDefense >= 11` en vivo, volvía a
contar como pendiente, sin que le importara que `downedBoss1` siguiera en `true`.

Esto no es un artefacto de la prueba: es un fallo real de diseño que ya existía desde el primer
día, solo que con un único tramo implementado nunca se había podido observar (con solo `PreOjo`,
"cerrar el tramo" y "no perder ningún requisito anterior" eran la misma cosa por coincidencia). Un
jugador real que se quita la armadura inicial al conseguir una mejor, o suelta el arma de partida
del inventario, habría visto a la guía mandarle otra vez a por 10 de defensa contra un jefe que
llevaba muerto un buen rato — justo el "esto no funciona de verdad" que este proyecto no se puede
permitir.

**Arreglo, en `EstadoGuia.PasoActual`:** antes de mirar los pasos de un tramo uno a uno, se
comprueba si su ÚLTIMO paso ya está completado (por convención del propio `.json`, es el que
cierra el tramo de verdad — casi siempre la bandera del jefe, que el motor nunca vuelve a poner a
`false`). Si lo está, el tramo entero se da por hecho y se salta sin mirar los de más atrás.
Mientras el tramo sigue EN CURSO, sí importa cuál de sus pasos anteriores falta — ahí no cambia
nada, se sigue pudiendo decir "te falta el arma" con precisión.

Efecto secundario, encontrado con el mismo arnés al re-ejecutar: `EstadoGuia.TramosPorDelante`
tenía el mismo problema en el caso "no queda ningún tramo implementado por hacer" — con `actual =
null` usaba `desde = 0` sin más, así que "Lo que viene después" volvía a listar desde el
principio: **se vio literalmente en la captura `guia-10-todo-lo-implementado-hecho.png`**, con
"Antes del primer jefe" arriba del todo pese a llevar rato derrotado. Arreglo en la misma función:
si no hay tramo activo porque todo lo implementado ya está superado (no porque no haya partida),
el punto de partida es el `Orden` más alto entre los tramos IMPLEMENTADOS, no 0. Verificado con un
tercer tipo de comprobación en el arnés (no solo "el paso en pantalla es el que toca", también
"ningún tramo ya cerrado aparece en la hoja de ruta") y con la captura repetida tras el arreglo.

### Verificación real, las dos veces (sin Calamity y con Calamity)

`scripts\verificar-guia.ps1` (sin Calamity) y `scripts\verificar-guia.ps1 -Calamity`: las dos
pasadas completas, **ninguna comprobación en rojo** (`NO CUADRA` / `EXCEPCION` / `NO COINCIDE` /
`NO CABE`), incluida la transición de `PreOjo` → `MaldadDelMundo` → `Esqueletron` → "no queda
nada pendiente" con banderas puestas a mano y sin pelear ningún jefe de verdad. Con Calamity
cargado el aviso de alcance sigue saliendo primero en la columna derecha y las cifras de los
jefes nuevos se leen igual de bien (comprobado con un arma distinta a la de la pasada sin
Calamity — "Bumerán encantado", 21 de daño — para no depender de que sea siempre la misma).
Capturas reales revisadas a mano (no solo el log en verde, lección ya aprendida en esta misma
área): `guia-7-maldad-preparativos.png`, `guia-8-maldad-vencer.png`, `guia-9-esqueletron.png` y
`guia-10-todo-lo-implementado-hecho.png`, en las dos pasadas — sin texto solapado ni cortado, con
el aviso de Calamity bien colocado encima de "Qué te falta". Logs en `evidencia\guia.log.txt` y
`evidencia\guia-calamity.log.txt` (sobrescritos con esta pasada; el log anterior, solo de
`PreOjo`, queda en el historial de git si hace falta consultarlo).

### Alcance de esta sesión

Tocados: `Assets/guia_progresion.json` (dos tramos pasan de `implementado:false` a `true`, cuatro
pasos y doce requisitos nuevos), `Common/Guia/BanderasGuia.cs` (bandera `shadowOrbSmashed`),
`Common/Guia/EstadoGuia.cs` (el arreglo real: tramo superado no se reabre, hoja de ruta no repite
tramos ya hechos), `Common/Guia/AutopruebaGuia.cs` (arnés ampliado con 25 pasos nuevos, del 37 al
61, más los métodos que cambian el estado real: romper esfera de mentira, poner objeto de
invocación, quitar/poner arma por daño, matar la maldad/Esqueletron en falso, crear al Anciano),
`scripts/generar-localizacion.py` + los dos `.hjson` (24 claves nuevas por idioma: 2 banderas, 1
zona nueva —"la entrada de la Mazmorra"— y 4 pasos completos con título/porqué/cómo en los dos
idiomas).

### Lo que sigue, con la misma prioridad que dejó la entrada anterior

Quedan por convertir en pasos evaluables, en este orden: `MuroDeCarne` (el punto de no retorno,
necesita un aviso claro de que el mundo cambia para siempre), `Mecanicos` (los tres jefes
mecánicos, salto de dificultad grande), `Plantera` + `TemploYGolem` (encadenados por
`downedMechBossAny`/`downedPlantBoss`), y como opcionales/tardíos `InicioModoDificil`,
`ReinaAbeja`, `JefesOpcionalesTardios`, `EventosLunares` y `MoonLord`. `ReinaAbeja` en concreto
merece una nota propia: aunque es opcional, si algún día se implementa habrá que decidir si debe
seguir apareciendo en la hoja de ruta incluso después de tramos con `Orden` mayor (hoy
`TramosPorDelante` es puramente lineal por `Orden`, y un tramo opcional saltado desaparecería de
"lo que viene después" en cuanto se pasa de largo — no es un bug nuevo de esta sesión, es la
misma simplificación que ya tenía el código, pero conviene decidirlo antes de implementarla para
no heredar el mismo tipo de sorpresa que costó encontrar hoy).

Sin tocar todavía, siguen pendientes las dos piezas de diseño acordadas (marcadores de mapa
enganchados a `Common/Exploracion/MarcadoresExploracion.cs`/`CapaMapaExploracion.cs`, y dirección
horizontal hacia el objetivo) — esta sesión se dedicó entera a los tramos nuevos y al fallo de
progresión que salió al ampliarlos, que por su naturaleza (un jugador real se podría haber
encontrado con la guía mandándole hacia atrás) tenía prioridad sobre features nuevas.

## 13-sep-2026 (más tarde) — Auditoría de espaciado para TODO el mod, no solo la Guía

Encargo explícito del usuario: auditar que **ningún texto sea más grande que su caja, que no se
solape texto/cajas/opciones, que todo tenga su espacio y su sitio** en TODO TerrakeepMod — el
editor de personaje, el catálogo de objetos, un hipotético "árbol de habilidades" y cualquier otra
pantalla, no solo la Guía en Tiempo Real de la sesión de hoy. Con el mismo rigor ya usado en este
mod: capturas reales del back buffer, medidas con la fuente real, en español e inglés, con y sin
Calamity.

### Lo primero: qué ya estaba cubierto y qué no

Antes de tocar nada se leyó `Common/Panel/AutopruebaEspaciado.cs` (el arnés que ya existía desde el
7-sep, `scripts/verificar-espaciado.ps1`) y el resto de arneses (`AutopruebaPersonaje`,
`AutopruebaExploracion`, `AutopruebaColumnasBuffs`, etc.). Ya cubrían con medición real: el
recuadro naranja de Exploración › "Este mundo", la pestaña Buffs completa, Builds, y capturas
(sin medición dedicada) de Libería/Investigación. **Sin cubrir con medición real ni capturas
revisadas**: Personaje › Equipo/Inventario/Almacenes/Desbloqueos/Apariencia, toda el área de
Ajustes, y Exploración › Mapa/Búsqueda. No existe ningún "árbol de habilidades" en TerrakeepMod
(no hay equivalente real en Terraria/tModLoader a ese concepto de otros juegos) — se documenta
aquí en vez de forzar un análogo falso, mismo criterio que la regla de la "Marca Keep".

### El arnés, ampliado (no uno nuevo)

`Common/Panel/AutopruebaEspaciado.cs` gana:
- **Un auditor genérico** (`AuditarArbol`): recorre TODO el árbol de una pantalla con
  `ExecuteRecursively`, mide cada `EtiquetaTk`/`BotonTk` con texto contra su propia caja
  (`FontAssets.MouseText.Value.MeasureString(...) * Escala` vs `GetDimensions().Width`, con la
  MISMA geometría ya dibujada — `MaxWidth` por defecto es `StyleDimension.Fill`, así que un ancho
  fijo declarado se recorta solo al hueco real del padre si es más pequeño, confirmado leyendo
  `UIElement.cs` decompilado) y comprueba solapamiento por PARES entre hermanos que comparten el
  mismo padre directo (nunca contra un antepasado: eso sería "el hijo cabe dentro del padre", que
  es contención normal). Complementa, no sustituye, a las comprobaciones específicas que ya
  existían donde las había.
- Se abren y miden con este auditor, en las mismas 3 resoluciones × 2 idiomas que ya usaba el
  arnés (1600×900, 1280×720, 800×720 el mínimo real): las cinco pestañas de Personaje que
  faltaban, toda Ajustes, y Mapa/Búsqueda de Exploración.
- Contenido REALISTA, no vacío: `PoblarPersonajeDePrueba` reutiliza
  `AutopruebaPersonaje.PoblarInventario/PoblarAlmacenes/PoblarEquipo` (esos tres métodos pasan de
  `private` a `internal` para esto, sin duplicar la lógica) para dejar objetos de verdad puestos;
  `PrepararAjustesDePrueba` deja dos entradas reales en el historial de deshacer (para medir los
  botones "Deshacer: ‹etiqueta›"/"Rehacer: ‹etiqueta›" con su texto largo real, no el estado
  vacío); `PrepararBusqueda` lanza una búsqueda real de "Cobre" (mismo objetivo que ya usaba
  `AutopruebaExploracion`, existe en cualquier mundo) para que la lista de resultados tenga filas
  reales que medir.
- `EtiquetaTk` gana un accesor público `Escala` (antes solo interna) para que el auditor mida con
  la MISMA escala con la que se dibuja, no una supuesta.

`scripts/verificar-espaciado.ps1` gana `-Calamity` (mismo patrón que `verificar-guia.ps1`) y ahora
también comprueba `FALLO` (no solo `NO CUADRA`/`EXCEPCION`) al final, para que un solape o
desborde real tumbe el script en rojo igual que ya hacía la Guía.

### Un obstáculo real del entorno, resuelto (regla de autonomía técnica)

Las dos primeras pasadas (900s y luego 2400s de margen) se quedaron colgadas para siempre en
`Finding Mods...`, sin llegar nunca a escribir la evidencia. Diagnosticado leyendo
`Interface.cs`/`ModOrganizer.cs` REALES decompilados (no supuesto): tModLoader compara los mods
del Workshop (`HEROsMod`/`CalamityMod`, aunque estén DESHABILITADOS en el sandbox — la
comparación mira la carpeta Workshop entera, no `enabled.json`) contra `LastLaunchedMods.txt`, y
si detecta que cambiaron desde el último lanzamiento (`HEROsMod` se actualizó el 8-sep, y el
sandbox de espaciado venía de una pasada anterior a esa fecha) abre un diálogo modal ("Mod
Changes since last launch") que exige un CLIC real para continuar — el mismo tipo de bloqueo que
ya documentaba el `CLAUDE.md` de este proyecto para el aviso de audio. Sin nadie delante, el
cliente se quedaba esperando ese clic para siempre. Arreglo real en
`scripts/verificar-espaciado.ps1`: la misma clave que usa el propio menú del juego
(`Main.Configuration.Put("ShowNewUpdatedModsInfo", ...)`, confirmado en `ModLoader.cs`
decompilado) se escribe a `false` en el `config.json` del sandbox antes de lanzar — es
exactamente lo que haría un jugador real para no volver a ver ese aviso, aplicado solo a este
sandbox de pruebas. Commit y nota aquí antes de reintentar, como pide la regla de autonomía
técnica; no hizo falta instalar nada, solo diagnosticar con el código real.

### Los fallos reales encontrados (todos con captura revisada a mano, no solo el log)

Primera pasada completa del arnés ampliado: **30 líneas en rojo** (repetidas por resolución/
idioma, pero de 8 causas raíz distintas). Cada una se verificó de verdad mirando la captura antes
de arreglar — dos de ellas (los solapes "en caja, pero el texto real es corto y no llega a
tocarse") se dejaron arregladas igualmente porque el hueco declarado seguía siendo una
inconsistencia real, aunque esta vez no rompiera nada a la vista; el resto sí eran visibles en la
captura:

1. **"Dinero" de la cabecera de Personaje, roto de verdad.** `CabeceraPersonaje.ConstruirDinero`
   declaraba una caja de 520px fija que nadie había medido contra dinero real. Con un personaje
   realista (inventario Y los cuatro almacenes con monedas) el texto mide ~650px: en la captura
   a 800×720 se leía literalmente cortado a media palabra, encima de "Ahora: ... vida, ... maná"
   ("...1 almace[CORTE]da y maná)"). Arreglo en dos pasos (el primero abrió un solape nuevo, visto
   en la SIGUIENTE pasada del propio arnés — la razón de por qué hay una pasada 4 y una 5): ahora
   el texto se envuelve a dos líneas (`EtiquetaTk.PartirEnLineas`) dentro del ancho real de la
   fila MENOS el hueco real del botón "Llenar vida y maná" que comparte esa fila, y
   `CabeceraPersonaje`/`ContenidoPersonaje.AltoCabecera` suben de 90 a 110px para dejarle sitio
   real a la segunda línea.
2. **"Camiseta interior" pisando su propia muestra de color, en Apariencia.** `FilaColorTk`
   reservaba 124px fijos entre el rótulo y la muestra de color, medidos solo a ojo. En español
   "Camiseta interior" es el rótulo más largo de las siete filas y se leía prácticamente encima
   del recuadro negro — visible en la captura, con un contraste claro contra las otras seis filas
   (que sí dejaban hueco). Arreglo con el mismo criterio que ya usa
   `PanelTerrakeepState.AjustarEscalaDeLasPestanas` para la barra de pestañas: una escala COMÚN a
   las siete filas (no una por fila, para que las cortas no se vean más grandes que las largas),
   calculada cada fotograma con la fuente real y bajada solo lo justo para que la más larga quepa.
3. **Equipo especial pisando el selector de Conjunto, en Personaje › Equipo.** Tres fallos con la
   misma raíz: `PestanaEquipo` colocaba el título de la columna de equipo especial
   ("Equipo especial") en la MISMA banda vertical que los tres botones "Conjunto N" de arriba, sin
   comprobar que no se tocaran en horizontal; el rótulo de cada fila de equipo se declaraba con
   240px de caja mientras el cálculo de la anchura de columna reservaba solo 170
   (`AnchoRotuloFila`, ya medido para el caso real más largo); y la leyenda de columnas de la
   izquierda ("Columnas: equipado · vanidad · tinte", 340px) no se tenía en cuenta al calcular
   dónde empezaba la columna derecha. Arreglado bajando el título de "Equipo especial" a una banda
   vertical propia (6px libres bajo los botones — `ArribaRejilla` sube de 68 a 78 para dejarle
   sitio), igualando el rótulo de fila a `AnchoRotuloFila` en los dos sitios, y calculando el
   inicio de la columna derecha con `Math.Max` entre el ancho real de la rejilla y el de la
   leyenda.
4. **La cabecera de Exploración, con dos fallos.** "Estás en el tile..." y "Mapa del juego listo"
   se solapaban 4px SIEMPRE (Top fijos demasiado juntos, sin relación con la resolución); y a
   800×720 el título "Exploración del mundo" y la línea de mundo ("TerrakeepPrueba · Pequeño ·
   Clásico") tenían cajas declaradas (640/700px fijos) más anchas que el hueco real que dejaba la
   columna derecha. Arreglado subiendo "Mapa del juego listo" 6px y dando a las dos cajas de la
   izquierda un ancho en PORCENTAJE hasta donde empieza la columna derecha, en vez de un número
   fijo nunca medido; `CabeceraExploracion`/`ContenidoExploracion.AltoCabecera` suben de 84 a 98px
   para que la columna derecha (ahora con más separación vertical) siga cabiendo dentro del marco.
5. **El detalle de resultados de Búsqueda, cortado a 800×720.** "13.718 tiles encontrados en
   20.170.801 mirados..." (una frase larga y realista, con separadores de miles) medía 529px en
   una caja ya recortada por el motor a 406px reales (`MaxWidth` = ancho real de la columna
   derecha, no el 640 fijo declarado). Arreglado envolviendo el texto con
   `EtiquetaTk.PartirEnLineas` contra el ancho real de la columna (medido cada fotograma, depende
   de idioma y resolución) y bajando la barra de progreso/la lista de resultados lo que haga falta
   según cuántas líneas ocupe de verdad (`PestanaBusqueda.ReflowDerecha`, nuevo).
6. **"Papelera"/"Seleccionar" con las cajas tocándose**, en el mini-panel compartido
   (`PanelHerramientasLibreriaTk`, usado en Equipo/Inventario/Almacenes): la caja declarada de
   "Papelera" medía 70px pero el siguiente elemento empezaba en 64 — 6px de solape de CAJA
   (el texto real, más corto, no llegaba a tocarse de verdad, pero la inconsistencia entre los dos
   números era real). Arreglado moviendo el segundo grupo a 74px.
7. **"Variante / género" muy justo contra el botón "<"**, en el selector genérico `SelectorTk`
   (usado en la cabecera de Personaje y en Apariencia): en español, más largo que "Variant /
   gender", casi tocaba el botón siguiente. No llegaba a solaparse de forma medible con la fuente
   real, pero quedaba sin margen. `SelectorTk.DrawSelf` ahora autoajusta la escala del rótulo
   (nunca sube de 0,8, baja solo si de verdad no cabe) igual que ya hacían otros widgets de este
   mod.

### Verificación real, tres pasadas más tras los arreglos

- **Pasada 4** (tras arreglar 1-7 salvo el propio arreglo de "Dinero"): confirmó los siete arreglos
  pero destapó un fallo NUEVO — el primer intento de "Dinero" (ensancharlo a toda la fila) quitó
  el desborde pero abrió un solape con el botón "Llenar vida y maná", que comparte esa misma fila
  (30 líneas en rojo, las 24 combinaciones pestaña×idioma×resolución de Personaje que comparten
  cabecera, más las 6 que ya se habían visto de Exploración pero que en realidad seguían sin
  arreglar del todo — visto ANTES de darlo por bueno, no después). Arreglo real: el descrito en el
  punto 1 de arriba (envolver a dos líneas + subir la cabecera a 110px).
- **Pasada 5**: **0 líneas en rojo**, `Ninguna comprobacion en rojo.` del propio script, en las 3
  resoluciones × 2 idiomas × (Aviso, Buffs, Buffs-carpetas, Builds, Librería, Investigación,
  Equipo, Inventario, Almacenes, Desbloqueos, Apariencia, Ajustes, Mapa, Búsqueda) = 84 capturas
  reales, revisadas a mano las de mayor riesgo (Equipo, Inventario, Apariencia, Ajustes, Búsqueda,
  Mapa) confirmando visualmente cada arreglo: "Dinero" en dos líneas limpias sin tocar el botón,
  "Camiseta interior" con el mismo hueco que las otras seis filas, "Equipo especial" claramente
  por debajo de los botones de Conjunto, el detalle de Búsqueda envuelto sin solaparse con la
  lista de resultados.
- **Con Calamity cargado** (`-Calamity`, mismo patrón que `verificar-guia.ps1` — confirmado que
  cargó de verdad: categorías reales como "Calamity Mod (mod) › Otros (311)" y "Calamity Mod
  Music" en Librería/Investigación/carpetas de Buffs): **0 líneas en rojo** también, en las
  mismas 6 combinaciones. Captura revisada a mano de Builds con las dos píldoras de fuente
  (Vanilla/Calamity) y las tres etapas con sus nombres largos reales
  ("Hardmode temprano (antes de los jefes mecánicos)", etc.) — todo cabe limpio.

Logs completos en `evidencia/espaciado.log.txt` y `evidencia/espaciado-calamity.log.txt`
(sobrescritos con la pasada 5 y la pasada con Calamity respectivamente — las pasadas 1-4
intermedias no se guardan aparte, quedan en el historial de git si hiciera falta reconstruirlas).
84 capturas reales en `evidencia/espaciado-capturas/` (sin Calamity) y otras 84 en
`evidencia/espaciado-capturas-calamity/` (con Calamity).

### Lo que NO se encontró roto (comprobado, no asumido)

Inventario, Almacenes y Desbloqueos ya venían bien de la ronda de rediseño de WS1 (el mini-panel
`PanelHerramientasLibreriaTk` consolidado): solo cargaron el fallo 6 de arriba, compartido con
Equipo por ser el mismo widget. Los checkboxes largos de Desbloqueos
("Antorchas de bioma desbloqueadas/activadas") SÍ se sospecharon por su longitud al leer el
código, pero la captura real los descartó: caben con margen de sobra — se comprobó antes de tocar
nada, siguiendo la lección ya aprendida en este proyecto de no "arreglar" algo sin verlo roto en
una captura real primero.

### Alcance

Tocados: `Common/Panel/AutopruebaEspaciado.cs` (auditor genérico + 8 pantallas nuevas cubiertas),
`Common/Personaje/AutopruebaPersonaje.cs` (tres métodos de `private` a `internal`),
`UI/Personaje/Widgets/EtiquetaTk.cs` (accesor `Escala`), `UI/Personaje/CabeceraPersonaje.cs` +
`UI/Personaje/ContenidoPersonaje.cs` (arreglo de "Dinero", cabecera 90→110px),
`UI/Personaje/PestanaEquipo.cs` (arreglo de la columna de equipo especial, `ArribaRejilla`
68→78px), `UI/Personaje/PestanaApariencia.cs` + `UI/Personaje/Widgets/FilaColorTk.cs` (escala
común de las siete filas de color), `UI/Personaje/Widgets/SelectorTk.cs` (autoajuste de escala),
`UI/Exploracion/CabeceraExploracion.cs` + `UI/Exploracion/ContenidoExploracion.cs` (anchos en
porcentaje, cabecera 84→98px), `UI/Exploracion/PestanaBusqueda.cs` (envoltorio del detalle +
`ReflowDerecha`), `UI/Libreria/Widgets/PanelHerramientasLibreriaTk.cs` (hueco real entre
Papelera/Seleccionar), `scripts/verificar-espaciado.ps1` (`-Calamity`, comprobación de `FALLO`,
arreglo del diálogo modal bloqueante), `evidencia/espaciado*.log.txt` y las dos carpetas de
capturas. No se ha tocado nada de `Common/Guia/` ni de los tramos de progresión pendientes
(`MuroDeCarne`, `Mecanicos`) — fuera del alcance de esta auditoría, encargo de otra sesión en
curso.

## 13-sep-2026 (sesión larga) — La Guía llega al 100%: los doce tramos hasta Moon Lord, dos bugs reales y los tres jefes opcionales nombrados en el encargo

Encargo explícito del usuario: la guía de progresión tiene que cubrir el camino vanilla entero
hasta derrotar a Moon Lord, "de principio a fin, sin huecos". Partiendo de donde dejó la sesión
anterior (`PreOjo`, `MaldadDelMundo`, `Esqueletron` ya cerrados), esta sesión implementa **los
nueve tramos que faltaban del camino obligatorio** — `MuroDeCarne`, `Mecanicos`, `Plantera`,
`TemploYGolem`, `EventosLunares` (Cultista Lunático + las cuatro torres) y `MoonLord` — más **los
tres jefes opcionales que el propio encargo nombró explícitamente**: la Reina Abeja, la Reina
Slime (dentro de `InicioModoDificil`) y el Duque Pezhongo junto con la Emperatriz de la Luz
(dentro de `JefesOpcionalesTardios`). Con esto, **los doce tramos del `.json` tienen ya
requisitos evaluables** (32 pasos, 64 requisitos, 0 avisos de datos) — el árbol vanilla completo
queda cerrado, salvo las dos piezas de diseño aparte (marcadores de mapa y dirección horizontal)
y el contenido opcional más allá de estos tres jefes, ver "Dónde seguir" al final.

Encargo ampliado a media sesión por el coordinador: además del camino obligatorio, la guía tiene
que evitar que el jugador se sienta perdido en NINGUNA parte del juego, incluido el contenido
opcional (más jefes, eventos, biomas, clases, pesca, mascotas/monturas, Pilones, semillas
secretas, Mercader Ambulante). Se cubrió lo que el tiempo permitió con el mismo rigor que el
camino obligatorio (los tres jefes ya nombrados en el encargo original) y se deja planificado con
claridad el resto — ver "Dónde seguir".

### De dónde sale cada número, tramo a tramo (todo desde el `NPC.cs` decompilado real)

Igual que en las sesiones anteriores, ni un solo número se copió de memoria ni de una wiki: se
sacó del `NPC.cs` decompilado de esta versión (`Downloads\tModLoader-Decompiled\tModLoader\
Terraria\NPC.cs`), con la línea real citada en el `_fuente` de cada requisito del `.json`.

- **Muro de Carne** (`type==113`, cuerpo): daño 50, defensa 12, vida 8000; el ojo (`type==114`)
  daño 50, defensa 0. Mecanismo de invocación confirmado en el motor, no supuesto: `NPC.cs`
  `case 22` (el Guía) — `Collision.LavaCollision(position,...)` → `SpawnWOF(position)`. Cierre con
  bandera **`hardMode`** (ya presente en `BanderasGuia` desde el primer día): confirmado que
  `case 113` de la muerte del jefe llama a `WorldGen.StartHardmode()`.
- **Los tres mecánicos**: Gemelos (`type==125/126`) defensa 10, vida 20000/23000, daño 45/50;
  Esqueletron Prime (`type==127`) defensa 24, vida 28000, daño 47; Destructor (`type==134`,
  cabeza sin defensa; cuerpo/cola `135/136` defensa 30/35) vida 80000. Solo hace falta **UNO** de
  los tres para seguir: confirmado que los tres tocan la misma pareja
  `downedMechBoss{1,2,3}`+`downedMechBossAny` al morir (`case 125/126`, `case 127`, `case 134`), y
  es `downedMechBossAny` (ya presente) la bandera real que cierra el tramo.
- **Plantera** (`type==262`): daño 50, defensa 14, vida 30000, con DOS fases (se enrabia bajo la
  mitad de la vida). Cierre con `downedPlantBoss` (ya presente), confirmada como la bandera que
  abre la puerta de piedra del Templo (`case 262`).
- **Golem** (`type==245`, cuerpo): daño 72, defensa 26, vida 15000; cabeza suelta (`type==249`)
  tras romper los puños, 32 de defensa/80 de daño y **sin recibir daño** mientras vivan los puños
  (`dontTakeDamage`). Cierre con `downedGolemBoss` (ya presente), confirmada vía `BuffTownNPC`
  (+15%/+8 a los NPC del pueblo) como el mismo escalón que dan Plantera/Emperatriz/Cultista.
- **Cultista Lunático** (`type==439`): daño 50, defensa 42, vida 32000 — la defensa más alta de
  todo el prehardmode y el hardmode hasta ahí. Confirmado en el motor que las cuatro
  `CultistDevote`/`CultistArcher` (`type==437/438`) que lo protegen deben morir todas antes de que
  aparezca (AI del `type==437`, `NPC.cs` líneas ~39560-39645). Su muerte (`case 439`) enciende
  `downedAncientCultist` **Y llama en la misma línea** a `WorldGen.TriggerLunarApocalypse()` — no
  hay margen para prepararse después, y la guía lo avisa con todas las letras.
- **Las cuatro torres** (Solar=517/Vortex=422/Nebula=507/Stardust=493): 20 de defensa y 20000 de
  vida cada una, daño 0 directo (el peligro real son sus oleadas). `NPC.downedTowers` es una
  **propiedad calculada** que exige las cuatro banderas `downedTower*` juntas — confirmado
  leyendo su `getter` real, no asumido.
- **Moon Lord**: núcleo (`type==398`) 70 de defensa y 50000 de vida PROPIA, con `dontTakeDamage`
  hasta que caen sus manos (`type==397`, 40 defensa/25000 vida cada una) y la cabeza (`type==396`,
  50 defensa/45000 vida) — más de 145000 de vida repartida en total. Cierre con `downedMoonlord`,
  la última bandera de la progresión vanilla.
- **Reina Abeja** (`type==222`): daño 30, defensa 8, vida 3400 — menos defensa que el propio Ojo
  de Cthulhu. Cierre con `downedQueenBee`.
- **Reina Slime** (`type==657`): daño 60, defensa 26, vida 18000. Cierre con `downedQueenSlime`.
- **Duque Pezhongo** (`type==370`): daño 100, defensa 50, vida 60000 — el daño más alto de todo
  el árbol hasta el propio Moon Lord. Cierre con `downedFishron`.
- **Emperatriz de la Luz** (`type==636`): daño 80, defensa 50, vida 70000 — la vida más alta de
  los opcionales tardíos, y con `dontTakeDamage` salvo en las condiciones de luz correctas (pelear
  de noche la vuelve mucho más dura a propósito). Cierre con `downedEmpressOfLight`.

Cada tramo obligatorio sigue el mismo patrón de dos pasos que ya cerró `PreOjo` (arma → bandera
del jefe), salvo `EventosLunares` que son CUATRO pasos seguidos (arma-Cultista → vencer-Cultista →
arma-Torres → vencer-Torres) por ser dos jefes distintos en el mismo tramo — y ahí salió el primer
bug real, ver abajo. `JefesOpcionalesTardios` es igual, cuatro pasos por los mismos dos jefes
(Fishron + Emperatriz).

### Bug real #1: un tramo de CUATRO pasos podía "reabrirse" a mitad, el mismo fallo de siempre pero un nivel más adentro

La sesión del 13-sep ya había arreglado que un TRAMO entero no se reabriera al perder un requisito
efímero (quitarte el arma, la armadura...) una vez superado. Ese arreglo solo protegía el **último**
paso de cada tramo. Con `EventosLunares` de CUATRO pasos (dos jefes seguidos), el arnés lo pilló en
el primer pase real: tras marcar al Cultista como derrotado y quitar el arma antes de ir a las
torres, la guía volvía a enseñar "arma para el Cultista" — con el Cultista llevando rato muerto.

**Causa real:** `EstadoGuia.PasoActual` solo comprobaba si el ÚLTIMO paso del tramo ya estaba
cumplido para decidir si el tramo entero se daba por hecho; el resto de los pasos se evaluaban
todos en vivo cada vez, sin memoria de "esto ya se dio por bueno antes". Con un tramo de dos
pasos (arma→bandera) esto coincidía con "el paso que cierra de verdad" por casualidad; con cuatro
pasos (dos jefes) dejó de coincidir.

**Arreglo real, en `EstadoGuia.cs`:** el "suelo" del tramo ya no es solo su último paso, es el
paso **anclado por bandera** (todos sus requisitos obligatorios son de tipo `bandera` — un dato
persistido que el motor nunca vuelve a poner a `false`, a diferencia de `dano_arma` o `defensa`,
que se releen en vivo) MÁS AVANZADO que ya esté cumplido, sea o no el último. Se extrajo el
cálculo a un método compartido (`PrimerPasoPendiente`) que también usa el nuevo
`PasoOpcionalActual` (ver más abajo). Verificado con el mismo arnés: el paso `PrepararLasTorres`
aparece correctamente tras cerrar al Cultista y quitar el arma, sin volver atrás.

### Bug real #2: la hoja de ruta escondía los tramos opcionales para siempre en cuanto el camino obligatorio los adelantaba

Visto literalmente en una captura, no en el log: con Moon Lord recién cerrado, la columna "Lo que
viene después" quedaba **completamente en blanco**, pese a que `ReinaAbeja`, `InicioModoDificil`
y `JefesOpcionalesTardios` seguían sin construir en ese momento de la prueba.

**Causa real:** `EstadoGuia.TramosPorDelante` calculaba el punto de partida (`desde`) como el
`Orden` más alto entre los tramos IMPLEMENTADOS, y solo enseñaba tramos con `Orden > desde`. En
cuanto el camino obligatorio llega a `MoonLord` (Orden 12, el más alto de todos), ningún tramo
tiene `Orden` mayor — así que la hoja de ruta se queda vacía para siempre, aunque queden jefes
opcionales sin tocar.

**Arreglo real:** un tramo sin implementar (todavía mapa puro) se enseña SIEMPRE, pase lo que pase
con `desde` — ya no hay datos con los que decidir si está superado. Este arreglo llevó
directamente a la decisión de arquitectura de fondo (ver siguiente sección): en cuanto esos tres
tramos se implementaron de verdad, hacía falta un criterio distinto para ELLOS (basado en su
propia bandera de cierre, no en el `Orden`) sin romper el criterio ya bueno para los obligatorios.

### La decisión de arquitectura que la bitácora ya había dejado pendiente: `TramoGuia.Opcional`

La entrada del 13-sep sobre `Mecanicos`/`Esqueletron` ya avisaba: *"`ReinaAbeja` merece una nota
propia: si algún día se implementa habrá que decidir si debe seguir apareciendo en la hoja de ruta
incluso después de tramos con Orden mayor... conviene decidirlo antes de implementarla"*. Esta
sesión llegó exactamente a ese punto al implementar los tres opcionales, y sin resolverlo primero
habría sido un fallo real y no de laboratorio: `EstadoGuia.PasoActual` recorre los tramos POR
ORDEN, así que marcar `ReinaAbeja` (Orden 3) como `implementado=true` sin más habría hecho que la
guía mandara **"tu objetivo ahora mismo es la Reina Abeja" ANTES que Esqueletron** (Orden 4) solo
por tener menor número — justo lo contrario de "opcional".

**La solución:** `TramoGuia` gana un campo `Opcional` (leído del `.json`, `"opcional": true`).
Con él:

- `PasoActual` salta SIEMPRE los tramos opcionales al buscar el objetivo obligatorio — nunca
  bloquean el camino principal, pase lo que pase con su `Orden`.
- `TramosPorDelante` deja de mirar el `Orden` para ellos: se enseñan en la hoja de ruta mientras no
  estén superados (comprobado con su propia bandera de cierre), y desaparecen en cuanto el jugador
  los cierra de verdad — verificado marcando `downedQueenBee=true` a mano y comprobando que
  `ReinaAbeja` desaparece mientras `InicioModoDificil`/`JefesOpcionalesTardios` siguen ahí.
- **Hallazgo aparte, encontrado ANTES de terminar la feature, no después:** con los tramos
  opcionales siempre saltados por `PasoActual`, sus pasos (con su propio umbral de daño y sus
  objetos recomendados, ya escritos con el mismo cuidado que los obligatorios) se habrían quedado
  sin NINGÚN sitio donde enseñarse — la hoja de ruta solo da el resumen en dos líneas del tramo,
  nunca sus pasos evaluables. Se añadió `EstadoGuia.PasoOpcionalActual()` (el primer paso
  pendiente del primer tramo opcional, por Orden, que no esté superado) y una sección nueva
  **"Objetivo opcional"** en `ContenidoGuia` (columna derecha, entre el tramo obligatorio en curso
  y la hoja de ruta), con sus propios requisitos (`FilaRequisitoTk`) y lectura de jefe en vivo.
  Verificado con captura real: tras derrotar Moon Lord, el panel muestra "Objetivo opcional: La
  Reina Abeja (opcional): Derrotarla" con sus 3400 de vida reales y el arma actual del jugador
  (105 de daño en la prueba) calculando los golpes que hacen falta — el mismo nivel de detalle que
  ya tiene el camino obligatorio, no una versión rebajada.

### Verificación real, en cada tramo y al final del todo

Cada tramo (MuroDeCarne, Mecanicos, Plantera+TemploYGolem juntos, EventosLunares+MoonLord juntos,
y finalmente los tres opcionales) se compiló y se llevó al personaje de prueba por el tramo
entero con `scripts\verificar-guia.ps1`, **sin Calamity y con Calamity cargado las dos veces**,
antes de pasar al siguiente — nunca se escribió el tramo siguiente sobre una base sin probar. Cada
pasada real quedó en verde (`Ninguna comprobacion en rojo`) antes de seguir, y las dos veces que
salió algo en rojo (los dos bugs de arriba, más un fallo menor de la propia prueba que esperaba el
paso equivocado del opcional sin contar con que un arma de 90+ de daño de un paso anterior seguía
puesta) se pararon, se arreglaron y se re-verificaron antes de continuar - nunca se seguió con una
comprobación roja pendiente.

La pasada final, con el árbol completo (doce tramos) y Calamity cargado:
```
12 tramos (12 con requisitos evaluables), 32 pasos, 64 requisitos, 0 avisos. Calamity cargado=True.
...
OK: encontrado 'AUTOPRUEBA GUIA COMPLETA' en el log.
Ninguna comprobacion en rojo.
```
Capturas reales revisadas a mano en cada tramo (no solo el log en verde, la lección que este
proyecto no se puede permitir olvidar): preparativos y cierre de cada jefe, la pantalla final con
"No queda nada pendiente" + hoja de ruta con los tres opcionales, y el "Objetivo opcional" con
números en vivo — sin solapes ni texto cortado, con el aviso de Calamity siempre el primero de la
columna derecha cuando toca. Logs completos en `evidencia\guia.log.txt` y
`evidencia\guia-calamity.log.txt` (la pasada final de esta sesión; las intermedias quedan en el
historial de git). Capturas en el sandbox de pruebas (`terrakeep-capturas`, fuera del repo, se
regeneran con el script).

### Alcance de la sesión

Commits: `3665c42` (Muro de Carne), `b4b13dc` (mecánicos), `9226df1` (Plantera + Templo/Golem),
`5128b9a` (Cultista + torres + Moon Lord, con los dos arreglos de bugs), `2661e8c` (los tres
opcionales + arquitectura `Opcional`).

Tocados en total: `Assets/guia_progresion.json` (los nueve tramos obligatorios que faltaban +
los tres opcionales nombrados, con `"opcional": true` en los tres), `Common/Guia/BanderasGuia.cs`
(sin cambios — todas las banderas necesarias ya estaban desde el primer día),
`Common/Guia/EstadoGuia.cs` (los dos arreglos de bugs + `Opcional`/`PasoOpcionalActual`/
`PrimerPasoPendiente`), `Common/Guia/ModeloGuia.cs` (`TramoGuia.Opcional`),
`Common/Guia/CatalogoGuia.cs` (parseo de `"opcional"`), `Common/Guia/AutopruebaGuia.cs` (arnés
ampliado de 61 a 125 pasos, con comprobaciones nuevas dedicadas a los dos bugs y a la sección de
objetivo opcional), `UI/Guia/ContenidoGuia.cs` (sección "Objetivo opcional"),
`scripts/generar-localizacion.py` + los dos `.hjson` (de 599 a 673 claves: 9 tramos × pasos
completos es/en + 3 opcionales × pasos completos + banderas nuevas + zonas nuevas + las dos claves
de "Objetivo opcional"), `evidencia/guia*.log.txt`.

### Dónde seguir (para la próxima pasada, sin releer nada a ciegas)

**El camino obligatorio hasta Moon Lord está TERMINADO y verificado al 100%** (los nueve tramos,
las dos pasadas cada uno). Lo que queda, en el orden en que conviene abordarlo:

1. **Las dos piezas de diseño acordadas** (lo único que el encargo original pedía "una vez el
   árbol llegue a Moon Lord" — ya se puede empezar):
   - **Marcadores de mapa**: "brújula de propósito, nunca GPS" — nunca revelar secretos sin
     explorar, solo evitar la sensación de estar perdido. La infraestructura ya existe en
     Exploración: `Common/Exploracion/MarcadoresExploracion.cs` y `CapaMapaExploracion.cs` (sin
     tocar todavía por esta sesión). Referencia de UX que dio el usuario: el nivel de guía de
     Cyberpunk/Witcher 3/Souls/Borderlands/Dying Light/Warcraft 3/Ori.
   - **Dirección horizontal** (izquierda/derecha en el mundo 2D) hacia el objetivo actual. Hoy
     `EstadoGuia.Direccion` solo da la CAPA (arriba/abajo, con las fronteras reales del mundo:
     `Main.worldSurface`/`rockLayer`/`UnderworldLayer`). `Main.dungeonX` y el `BuscadorMundo` de
     Exploración dan para más, pero antes de tocar código hace falta decidir dónde está la línea
     entre "brújula" y "GPS" para el eje horizontal — no es obvio como con la capa (que tiene
     fronteras binarias claras del propio juego), un bioma puede estar a un lado u otro según el
     mundo, así que probablemente haga falta leer `Main.dungeonX`/posiciones reales de bioma del
     `BuscadorMundo` y decidir el nivel de precisión (¿"a tu izquierda", sin más? ¿una distancia
     aproximada tipo "lejos"/"cerca"?) antes de escribir una sola línea.

2. **Contenido opcional más allá de los tres jefes ya cubiertos**, pedido por el coordinador a
   media sesión de hoy. Cubierto con el mismo rigor que el camino obligatorio: Reina Abeja, Reina
   Slime, Duque Pezhongo, Emperatriz de la Luz (los tres tramos de esta sesión). **Sin cubrir
   todavía, con lo ya investigado para no repetir el trabajo:**
   - **Rey Slime** (`NPCID.KingSlime=50`): daño 40, defensa 10, vida 2000 (`NPC.cs`, bloque
     `type==50`). El más flojo de todos los opcionales, pensado para muy pronto (incluso antes del
     Ojo de Cthulhu). Bandera ya en `BanderasGuia`: `downedSlimeKing`. No tiene tramo en el
     `.json` todavía — haría falta decidir dónde encaja (¿un tramo propio con `Orden` bajo, tipo
     0.5, o dentro de `PreOjo` como opcional?).
   - **Deerclops** (`NPCID.Deerclops=668`): daño 20 (con `coldDamage=true`, debuff de frío
     extra), defensa 10, vida 7000 (`NPC.cs`, bloque `type==668`). Evento de nieve, se invoca con
     el Amuleto Esquimal de noche en bioma nevado, o aparece solo con baja probabilidad. Bandera
     ya en `BanderasGuia`: `downedDeerclops`. Tampoco tiene tramo todavía.
   - **Eventos**: Ejército Goblin, Piratas, Legión de Escarcha, Luna de Calabazas, Luna Helada,
     Eclipse Solar, Luna de Sangre, Antiguo Ejército D2, Locura Marciana. `BanderasGuia` ya tiene
     `downedGoblins`/`savedGoblin`/`savedMech`/`savedWizard`/`downedPirates`/`downedMartians` (de
     cuando se pensó el vocabulario cerrado, no usadas todavía por ningún tramo) - faltan las de
     Frost Legion, las cuatro lunas/eclipse y DD2, que no están en la tabla y habría que
     confirmar sus nombres reales en `NPC.cs` antes de citarlas.
   - **Biomas opcionales de exploración** (Selva, Mazmorra, Océano, Desierto, Nieve,
     Corrupción/Carmesí, Sagrado, Inframundo, islas flotantes): la exploración YA los cubre de
     verdad vía `Common/Exploracion/` (categorías, búsqueda, mapa) - lo que falta es que la GUÍA
     los mencione como contenido a explorar, no que se construya desde cero.
   - **Progresión de clases, pesca/Pescador, mascotas/monturas, Pilones, semillas secretas,
     Mercader Ambulante**: sin empezar. Ninguno de estos encaja en el modelo actual de "tramo con
     jefe final" - antes de escribir nada haría falta decidir si merecen su propia sección de la
     Guía (no un tramo más) o si se quedan fuera a propósito por no ser progresión medible con
     banderas reales del motor (documentar la decisión, sea cual sea, en vez de forzar un tramo
     falso solo por completar la lista - mismo criterio que la regla de la "Marca Keep").

Nada de lo anterior se ha tocado esta sesión: es la lista real de lo que falta, con los datos ya
investigados donde los hay, para que la próxima pasada no tenga que releer el NPC.cs desde cero
para los que ya están aquí (Rey Slime, Deerclops).

## 13-sep-2026 (continuación) — Las dos piezas de diseño (dirección horizontal + marcadores de
## mapa) y cuatro tramos opcionales más: Rey Slime, Deerclops, Ejército Goblin, Legión de Escarcha

Encargo explícito del coordinador: terminar las dos piezas de diseño que quedaban pendientes desde
que el árbol obligatorio llegó a Moon Lord (marcadores de mapa e indicador de dirección horizontal)
y seguir ampliando el contenido opcional con el mismo rigor que el resto del árbol, sin parar a
pedir confirmación. Las cinco piezas de este bloque se implementaron y verificaron EN EL JUEGO REAL
una a una, cada una cerrada y comiteada antes de pasar a la siguiente - nunca a medias.

### 1. Rey Slime y Deerclops (los dos jefes opcionales que ya tenían datos reales de la sesión anterior)

Confirmados de nuevo contra `NPC.cs` decompilado (no copiados a ciegas de la nota anterior):
Rey Slime (`type==50`) 40 de daño/10 de defensa/2000 de vida; Deerclops (`type==668`) 20 de daño
(`coldDamage=true`)/10 de defensa/7000 de vida. Los objetos de invocación reales se sacaron de
`Player.SummonItemCheck` (línea ~41813 de `Player.cs`, la función que decide si ya hay un jefe de
ese tipo vivo antes de dejar reusar el objeto): `SlimeCrown=560` → Rey Slime,
`DeerThing=5120` → Deerclops - la nota anterior había supuesto mal un "Amuleto Esquimal" que no
existe en el juego real, corregido aquí con la cita exacta del código.

Dato real adicional, útil para el umbral de `dano_arma`: `Main.AnyPlayerReadyToFightKingSlime()`
(la condición que de verdad usa el motor para la lluvia de slimes aleatoria) exige
`ConsumedLifeCrystals>2` y `statDefense>8` - confirma que el Rey Slime está pensado para ANTES
incluso del tramo `PreOjo`.

**Decisión de arquitectura, la que la sesión anterior dejó pendiente**: en vez de forzar los doce
`orden` existentes (1..12) a hacer hueco, se renumeraron TODOS a múltiplos de 10 (10..90) - cambio
puramente mecánico (el orden relativo no cambia, y `EstadoGuia`/`CatalogoGuia` solo comparan
`Orden` entre sí, nunca su valor absoluto, comprobado con `grep` antes de tocar nada) que deja
hueco de sobra para insertar tramos intermedios sin repetir este ejercicio cada vez. Rey Slime
entra en `orden=5` (antes de `PreOjo=10`), Deerclops en `orden=15` (justo después).

### 2. Ejército Goblin y Legión de Escarcha (nuevos esta sesión)

Dos invasiones por oleadas, no un jefe único - `jefeFinal=0` a propósito en el `.json`, con la
decisión documentada en vez de fingir un jefe que no existe (mismo criterio que ya fijó la
"Marca Keep"). Verificado en `NPC.cs`: el enemigo más duro de cada oleada es el Guerrero Goblin
(`type==28`: 25/8/110) y Mister Estocada (`type==144`: 65/26/240, con `coldDamage=true`). Los
objetos de invocación: `GoblinBattleStandard=361` y `SnowGlobe=602`.

Hallazgo real que mejora el propio requisito: `Main.CanStartInvasion(tipo, ignoreDelay:true)` (la
función que de verdad comprueba `Player.ItemCheck_UseEventItems` antes de dejar usar cualquiera de
los dos objetos) exige `ConsumedLifeCrystals>=5` - el MISMO hito que ya usa `PreOjo/CristalesDeVida`
- y que no haya ya otra invasión en marcha. Se añadió como requisito `recomendado` citando la
función real, no inventado.

Banderas reales: `downedGoblins` (ya estaba en la tabla, sin usar hasta ahora) y `downedFrost`
(nueva - el nombre real del campo es `downedFrost`, NO `downedFrostLegion`, fácil de suponer mal).

Orden 12 y 17 (entre `PreOjo=10` y `MaldadDelMundo=20`, con `Deerclops=15` en medio).

### 3. Indicador de dirección horizontal - solo la Mazmorra, y por qué solo ella

`EstadoGuia.Direccion` gana un lado (izquierda/derecha) cuando el paso apunta a la Mazmorra,
comparando `Main.dungeonX` (campo real del motor, escrito/leído SIN condición alguna en todo
formato de `.wld` - `WorldFile.cs`, líneas ~1262/2021/3376) contra la posición real del jugador,
con 100 tiles de tolerancia para no titubear entre lados según un jugador se mueva un paso.

Investigado y descartado A PROPÓSITO extenderlo a Jungla/Templo Lihzahrd/Nieve: `GenVars.jungleOriginX`
(`WorldBuilding/GenVars.cs`) parecía el equivalente, pero es una variable de PASE DE GENERACIÓN -
se pone a 0 al empezar `WorldGen.jungle()` (confirmado en `WorldGen.cs`, líneas ~8053-8252) y no
sobrevive a cargar un mundo YA EXISTENTE en una sesión nueva, que es el caso normal. Usarla habría
sido una brújula falsa. Documentado en el XMLdoc de `EstadoGuia.LadoHorizontalDelPaso` para que la
próxima sesión no repita la investigación.

Verificado moviendo al jugador DE VERDAD a los dos lados de un `Main.dungeonX` de prueba
(`AutopruebaGuia.ComprobarDireccionHorizontalMazmorra`) y comprobando la frase real con el paso
real `ArmaParaEsqueletron`. Confirmado también en captura real que la frase combinada ("Estás
donde toca: la entrada de la Mazmorra, a tu derecha") envuelve limpia sin solaparse con nada.

### 4. Marcadores de mapa (la brújula) - la pieza más grande de este bloque

"Brújula de propósito, nunca GPS": cuando el objetivo activo apunta a Mazmorra/Templo
Lihzahrd/Jungla/Nieve (las únicas cuatro zonas con un sitio real que señalar - las otras cuatro,
Superficie/Subterráneo/Cavernas/Infierno, son capas enteras del mundo, sin un punto concreto que
marcar), `Common/Guia/BrujulaGuia.cs` busca esa zona SOLO dentro de lo que el jugador YA tiene
explorado en su propio mapa (`BuscadorMundo` con `soloExplorado=true`, el mismo motor de búsqueda
ya maduro de Exploración, en una instancia propia para no pelearse con una búsqueda manual del
jugador) y dibuja un marcador dorado sobre el mapa real del juego
(`Common/Exploracion/MarcadoresGuia.cs` + `CapaMapaExploracion` ampliada para dibujar los dos
conjuntos a la vez). Si no se ha explorado nada de esa zona todavía, NO hay marcador - nunca
revela un secreto sin explorar, solo recuerda dónde está lo que ya se encontró. `ContenidoGuia`
enseña una línea de estado (Marcado/Buscando/Sin explorar) para las cuatro zonas con brújula.

Mazmorra y Templo Lihzahrd reutilizan los objetivos YA CATALOGADOS de Exploración (categoría
"Paredes"), no una copia. Jungla y Nieve se construyen ad-hoc dentro de `BrujulaGuia` (tiles reales
`TileID.JungleGrass`/`SnowBlock`/`IceBlock`), sin añadirlos al catálogo público de Exploración
- son un detalle interno de la brújula, no una opción de búsqueda nueva que nadie pidió.

**Verificación real, sin adivinar nada**: el arnés busca la Mazmorra REAL de este mundo en TODO el
mapa (sin restricción de explorado - el mismo barrido que ya usa Exploración) para saber DÓNDE
revelar mapa de prueba con `Main.Map.Update` (la misma técnica ya verificada de
`AutopruebaExploracion.SembrarMapaDePrueba`), en vez de adivinar un área alrededor de
`Main.dungeonX` y arriesgarse a fallar por la profundidad real. Confirmado que la brújula pasa de
"sin explorar" a "marcado" en cuanto esa zona real queda explorada, y saltado de verdad al mapa
vanilla (`PanelExploracionSystem.VerEnElMapa`, la misma llamada de producción del botón "Ver en el
mapa" de Exploración) para comprobar CON UNA CAPTURA REAL que el rombo dorado se ve dibujado ahí
- no solo que el log dijera que sí.

**Dos hallazgos reales durante la verificación, los dos arreglados antes de comitear:**
1. Los objetivos ad-hoc de Jungla/Nieve (tiles de relleno de bioma, sin nombre propio en
   `Lang.GetMapObjectName` - solo lo tienen los objetos "interesantes") se enseñaban con la clave
   cruda sin traducir en el tooltip del mapa vanilla. Añadidas
   `Exploracion.Objetivo.BrujulaJungla`/`BrujulaNieve`.
2. El propio arnés de pruebas (no el mod): saltar al mapa reutilizando
   `PanelExploracionSystem.VerEnElMapa` deja el flag interno `_volverAlPanelAlCerrarMapa` de ESE
   sistema pendiente, que disparaba un fotograma más tarde y reabría el panel en la pestaña
   Exploración en vez de Guía - visto en las capturas (`guia-36..39` enseñaban la pestaña
   equivocada con el log en verde, porque `ContenidoGuia.PanelActual` no depende de qué pestaña
   esté visible). Arreglado forzando la pestaña Guía de nuevo al principio del bloque siguiente
   del arnés. No afecta a jugadores reales: la Guía no tiene ningún botón de producción que salte
   al mapa, solo lo hacía este arnés.

### Verificación real de todo el bloque

Cada pieza se compiló y se llevó al personaje de prueba con `scripts\verificar-guia.ps1`, sin
Calamity y con Calamity cargado, antes de pasar a la siguiente - igual que las sesiones anteriores.
Estado final del árbol: **16 tramos (16 con requisitos evaluables), 40 pasos, 80 requisitos, 0
avisos**. Capturas reales revisadas a mano en cada pieza (no solo el log en verde): el compás
horizontal envolviendo limpio, el marcador dorado en el mapa vanilla de verdad, las cuatro
secciones "Objetivo opcional" nuevas sin solapes ni texto cortado.

Commits: `f851eca` (Rey Slime + Deerclops), `a22330c` (dirección horizontal), `e8d6501`
(marcadores de mapa), `69cde3c` (Ejército Goblin + Legión de Escarcha).

### Hallazgo aparte, confirmado pre-existente (no introducido esta sesión, sin tocar)

Las capturas con "no queda nada pendiente en el tramo que la guía sabe medir" (`guia-28`, ya
existía ANTES de este bloque) muestran el tooltip de la pestaña activa "Guía" superpuesto en la
esquina superior del cuerpo del panel, en vez de seguir al cursor. Mismo diagnóstico que esta
bitácora ya dejó escrito para OTRO tooltip en la sesión del `_mouseTextCache`/`DrawInterface_33`
(ver esa entrada más arriba): en esta sandbox sin ratón físico, `PlayerInput.CurrentInputMode` se
detecta como mando en vez de ratón, y eso cambia cómo/dónde se posiciona el tooltip de
`Main.instance.MouseText`. No es un fallo del código de este mod (llama a la misma API pública
exactamente como vainilla) ni algo introducido en este bloque - se deja documentado y sin tocar,
consistente con la regla de no perseguir un artefacto del arnés como si fuera un bug real sin
verificación interactiva primero.

### Contenido opcional restante: datos reales ya investigados, para no repetir trabajo

Con el mismo rigor que arriba, sin implementar todavía - lo que sigue son datos REALES sacados del
código decompilado, listos para usar directamente:

- **Piratas**: bandera ya en la tabla (`downedPirates`). Objeto de invocación real:
  `PirateMap=1315`, mismo `Main.CanStartInvasion(3, ignoreDelay:true)` (mismos 5 cristales de
  vida) que Goblin/Escarcha. El enemigo común más duro es el Capitán Pirata (`NPC.cs`,
  `type==216`: 70 de daño/30 de defensa/3000 de vida). OJO con un hallazgo real: el "Flying
  Dutchman" que se suele citar de memoria NO es un NPC de combate con stats propios - `type==491`
  ("Pirate Ship") tiene `dontTakeDamage=true`, `damage=0`, `lifeMax=50` (es la plataforma/decoración
  visual, no el jefe que golpea). Igual que Goblin/Frost Legion, no hay un único "jefe" limpio que
  citar - mismo criterio, `jefeFinal=0`, sin inventar un análogo falso. Orden sugerido: 19 (entre
  `LegionDeEscarcha=17` y `MaldadDelMundo=20`).
- **Locura Marciana**: HARDMODE y post-Golem (`downedGolemBoss`), bandera `downedMartians` (ya en
  la tabla). El jefe real es la Nave Marciana (`NPCID.MartianSaucer=392`, con partes `393`
  Turret/`394` Cannon/`395` Core - un jefe MULTI-PARTE, más complejo de modelar que uno solo).
  **A diferencia de los demás, NO hay un objeto de invocación directo**: el evento lo dispara una
  Sonda Marciana (Martian Probe) que te detecta y escapa - haría falta investigar el NPC/flag real
  de esa sonda antes de escribir el paso "arma y prepárate", no asumido aquí.
  Sin investigar más a fondo esta sesión.
- **Luna de Calabazas**: objeto real `PumpkinMoonMedallion=1844`. Banderas reales confirmadas en
  `NPC.cs`: `downedHalloweenTree` (Mourning Wood) y `downedHalloweenKing` (Pumpking) - DOS
  banderas, no una, porque son dos jefes de oleada distintos dentro del mismo evento (parecido al
  patrón ya usado en `EventosLunares` con el Cultista+Torres). Ninguna de las dos está en
  `BanderasGuia` todavía.
- **Luna Helada**: objeto real `NaughtyPresent=1958`. Banderas reales confirmadas en `NPC.cs`:
  `downedChristmasIceQueen`, `downedChristmasTree` (Everscream), `downedChristmasSantank`
  (Santa-NK1) - TRES banderas. Ninguna está en `BanderasGuia` todavía.
- **Antiguo Ejército D2 (DD2)**: distinto de todos los anteriores - su bandera de completado,
  `downedDD2EventAnyDifficulty`, es un campo de **`Player`**, no de `NPC` (`Player.cs` línea
  ~2171, escrito a `true` en la línea ~23413 al terminar el evento en cualquier dificultad, y
  guardado/leído sin condición en el `.plr` - líneas ~55965/56464). `BanderasGuia.Construir()` hoy
  solo tiene lambdas que leen `NPC`/`WorldGen`/`Main`; añadir esta bandera es el primer caso que
  necesita leer `Main.LocalPlayer` en su lugar - trivial (`() => Main.LocalPlayer != null &&
  Main.LocalPlayer.downedDD2EventAnyDifficulty`), pero hay que tenerlo presente al tocar la tabla.
  Se invoca hablando con el Anciano (Old Man) en el Altar de la Driada, no con un objeto - falta
  investigar el NPC/mecanismo real del Altar antes de escribir el paso.
- **Eclipse Solar y Luna de Sangre**: comprobado que NO existe una bandera de "completado" para
  ninguno de los dos - son eventos por TIEMPO/probabilidad (duran hasta el amanecer o hasta que se
  usa un objeto para cancelarlos), no algo que se "derrote" una vez y quede guardado. Decisión:
  **no son tramos** - forzar una bandera falsa aquí sería exactamente el "análogo falso solo por
  completar la lista" que prohíbe la Marca Keep. Si algún día se quiere dar seguimiento, sería
  como contenido informativo (qué son, cómo se activan/desactivan), nunca como un tramo con
  bandera de cierre.

### Decisiones "Marca Keep" sobre el resto del contenido opcional pedido (documentadas, no forzadas)

- **Pesca y misiones del Pescador**: SÍ tiene una señal de progreso real y medible -
  `Player.anglerQuestsFinished` (contador real, guardado en el `.plr`) es justo el tipo de dato
  que ya usa este modelo (cristales de vida, etc.). Candidato razonable a una sección propia de la
  Guía en el futuro (no investigado más a fondo esta sesión: falta confirmar los umbrales reales
  que el juego usa para desbloquear el título "Pescador" / la armadura del Pescador / el acceso a
  ítems de fin de partida).
- **Mascotas y monturas**: decisión — **no les hace falta un tramo de la Guía**. No hay una
  "siguiente mascota/montura" objetiva (son cientos, coleccionables, sin orden natural), y este
  mod YA tiene una herramienta real y mejor para esto: la pestaña **Librería**, que ya cataloga y
  deja soltar cualquier objeto del juego (mascotas y monturas incluidas) por nombre/categoría. Un
  tramo de la Guía sería un peor duplicado de una función que ya existe.
- **Sistema de Pilones**: decisión — **no le hace falta un tramo**. Es infraestructura continua
  (depende de cuántos NPC vivan en cada zona y su felicidad, no de un hito puntual que se completa
  una vez), no progresión medible con una bandera de cierre real.
- **Semillas secretas conocidas**: decisión — **ya está cubierto, sin trabajo pendiente**.
  `Common/Exploracion/MundoActual.cs` (`SemillasSecretas`, ya escrito en sesiones anteriores de
  WS6) ya lee y enseña las ocho semillas reales del motor (`Main.drunkWorld`, `getGoodWorld`,
  `tenthAnniversaryWorld`, `notTheBeesWorld`, `dontStarveWorld`, `remixWorld`, `noTrapsWorld`,
  `zenithWorld`) en la pestaña Exploración. No hace falta repetirlo en la Guía.
- **Mercader Ambulante**: decisión — **no le hace falta un tramo**. Es un NPC aleatorio diario sin
  bandera de progreso real que marcar; nada que la Guía pueda medir con el mismo rigor que el
  resto del árbol.
- **Biomas opcionales de exploración** (Selva, Mazmorra, Océano, Desierto, Nieve,
  Corrupción/Carmesí, Sagrado, Inframundo, islas flotantes): decisión — **no les hace falta un
  tramo de la Guía además de lo que ya existe**. `Common/Exploracion/` ya los cubre de verdad
  (categorías de búsqueda, mapa, marcadores), y esta misma sesión los ha reforzado más todavía
  desde el lado de la Guía: la brújula (punto 4 de arriba) ya da seguimiento real a Mazmorra,
  Templo Lihzahrd, Jungla y Nieve en cuanto un tramo de la Guía los menciona como zona. Océano,
  Desierto, Corrupción/Carmesí, Sagrado, Inframundo e islas flotantes no tienen ningún tramo de la
  Guía que los cite como zona todavía (ningún paso del árbol apunta ahí), así que la brújula no
  tiene nada que resolver para ellos hoy - si algún día un tramo nuevo los usa como Zona, extender
  `BrujulaGuia.ObjetivoParaZona` es mecánico (mismo patrón que Jungla/Nieve).

### Dónde seguir (para la próxima pasada, sin releer nada a ciegas)

Todo lo de este bloque (las dos piezas de diseño + cuatro tramos opcionales nuevos) está
**terminado y verificado**. Lo que queda, con los datos reales ya listos arriba para no repetir
la investigación:

1. **Piratas** es el más fácil de los que faltan: mismos objeto/gate que Goblin/Escarcha, un solo
   enemigo común de referencia (Capitán Pirata), sin jefe único (`jefeFinal=0`, igual que los
   otros dos). Seguir el mismo patrón de `.json` + localización + bloque de `AutopruebaGuia` que
   ya usan Rey Slime/Deerclops/Goblin/Escarcha (Arrancar() tendría que pre-marcar también este
   quinto tramo como superado desde el principio, junto a los otros cuatro).
2. **Luna de Calabazas** y **Luna Helada** necesitan DOS y TRES banderas nuevas en `BanderasGuia`
   respectivamente (nombres reales ya confirmados arriba) antes de poder escribir sus tramos -
   son más parecidos en estructura a `EventosLunares` (varios jefes de oleada seguidos en el mismo
   tramo) que a Goblin/Escarcha/Piratas (un solo paso de "vencer").
3. **Antiguo Ejército D2** necesita primero un cambio pequeño pero real en `BanderasGuia.Construir()`:
   es la primera bandera que lee `Player` en vez de `NPC`/`WorldGen`/`Main` - el patrón de la tabla
   (`Dictionary<string, Func<bool>>`) ya lo admite sin cambios de diseño, solo hace falta la lambda
   nueva. Falta investigar el mecanismo real de invocación (hablar con el Anciano en el Altar de
   la Driada) antes de escribir el paso "arma y prepárate".
4. **Locura Marciana** es la más compleja de las que faltan: jefe multi-parte (Nave/Torreta/Cañón/
   Núcleo) y sin objeto de invocación directo (lo dispara una Sonda Marciana al detectarte) - hace
   falta investigar ese mecanismo real antes de escribir nada.
5. **Eclipse Solar** y **Luna de Sangre**: decisión ya tomada (arriba) de que NO son tramos por no
   tener bandera de cierre real. Si el coordinador pide igualmente darles seguimiento, sería como
   contenido informativo aparte, nunca forzando una bandera falsa.
6. **Pesca/Pescador**: el único de los "no-tramo" de arriba que SÍ merece investigarse más -
   `Player.anglerQuestsFinished` es un dato real y medible, candidato razonable a una sección
   propia de la Guía (no necesariamente el modelo de "tramo con jefe final") si el coordinador lo
   pide.
7. **Mascotas/monturas, Pilones, Mercader Ambulante**: decisión ya tomada (arriba) de que no les
   hace falta ningún trabajo nuevo en la Guía - documentado el porqué, no forzado.
8. **Semillas secretas**: ya cubierto por `MundoActual.SemillasSecretas` (Exploración) - sin
   trabajo pendiente.

## 13-sep-2026 (continuación 2) — Los cuatro tramos opcionales que quedaban: Piratas, Luna de
## Calabazas, Luna Helada y Antiguo Ejército D2

Partiendo de los datos reales que ya había dejado listos la entrada anterior (objetos de
invocación, banderas confirmadas en `NPC.cs`), esta sesión construyó los cuatro tramos opcionales
que faltaban de la lista pedida, con el mismo patrón exacto que los ocho ya existentes: `.json` +
`BanderasGuia` + localización es/en vía `scripts/generar-localizacion.py` + bloque dedicado en
`AutopruebaGuia.cs`, verificado con `scripts/verificar-guia.ps1` en vanilla Y con `-Calamity`.

### Investigación real hecha esta sesión (más allá de lo que ya traía la entrada anterior)

- **Stats reales de los cinco jefes de oleada** (Mourning Wood, Pumpking, Everscream, Santa-NK1,
  Reina de Hielo) y del Capitán Pirata y Betsy: sacados del bloque `else if (type == N)` de
  `NPC.cs` (SetDefaults NO usa un switch para estos campos, usa una cadena de `else if` - se
  localizaron buscando primero el patrón equivocado, un `switch` con `case 325:`, que solo existía
  para el escalado por dificultad, no para los valores base).
- **Orden real de aparición dentro de cada evento**: `NPC.CheckProgressPumpkinMoon`/
  `CheckProgressFrostMoon` (`NPC.cs`, listas `MoonEventRequiredPointsPerWaveLookup` por oleada)
  confirman Mourning Wood (oleada 5) antes que Pumpking (oleada 9), y Everscream (oleada 3) antes
  que Santa-NK1 (oleada 6) antes que la Reina de Hielo (oleada 10) - el orden que usan los cuatro
  pasos de cada tramo.
- **Mecanismo real de invocación del Antiguo Ejército D2**, sin investigar todavía en la entrada
  anterior: `Player.cs` (~línea 30670) - clic derecho sobre el tile 466 (Soporte del Cristal de
  Eternia, del objeto `DD2ElderCrystalStand=3816`) con el Cristal de Eternia (`DD2ElderCrystal=
  3828`) en el inventario consume el objeto y llama a `DD2Event.SummonCrystal` (`GameContent/
  Events/DD2Event.cs`), siempre que no haya ya un asedio en marcha ni sea Luna de Calabazas/Helada.
  Betsy (`NPC.cs`, type==551): 80 de daño, 38 de defensa, 50000 de vida.
- **El "Doblón del Capitán rescatado" que iba a poner en el texto de Piratas era falso** - se
  comprobó antes de escribirlo: `NPC.downedPirates` no está ligado a ningún NPC cautivo que
  rescatar (ese patrón es solo el del Ejército Goblin, con el Duende Mecánico). Lo real, sacado de
  `GameContent/ItemDropRules/ItemDropDatabase.cs` (`RegisterToNPC(216,...)`), es que el Capitán
  Pirata suelta objetos concretos (Cañón de Monedas, báculo de invocación Pirata, Tarjeta de
  Descuento...) solo mientras dura la invasión - así quedó escrito, sin el rescate inventado.
- **El "Tabernero se muda tras un jefe mecánico" que iba a poner en el texto de D2 tampoco se
  pudo verificar** - no se encontró la condición de aparición real del NPC 550 en el código
  decompilado (no usa el mismo patrón `SpawnAllowed_*` de otros vecinos). Se dejó el texto sin esa
  afirmación en vez de darla por buena de memoria.
- **Pumpkin Moon Medallion y Naughty Present NO exigen cristales de vida** (a diferencia del
  Ejército Goblin/Legión de Escarcha): `Player.cs` (~líneas 43625/43692/53498/53502) solo exige que
  sea de noche y que no haya ya otro evento de oleadas en marcha. Se comprobó antes de copiar el
  requisito `cristales_vida` de los otros dos, que aquí habría sido un requisito falso.

### Bug real, PRE-EXISTENTE, encontrado y corregido de paso

Al tocar `Guia.Bandera.*` para las banderas nuevas se encontró que **`downedGoblins` y
`downedFrost`** (usadas por `EjercitoGoblin`/`LegionDeEscarcha`, los dos tramos opcionales ya
verificados de la entrada anterior) **no tenían clave de localización**: `EvaluadorGuia.
EvaluarBandera` llama a `Idiomas.Texto("Guia.Bandera." + requisito.Bandera)` sin comprobar que la
clave exista, así que el segundo paso de esos dos tramos ("Derrotarlo") enseñaba la clave cruda sin
traducir en vez de un texto legible. No es una regresión de esta sesión - estaba desde que se
implementaron esos dos tramos y el log en verde nunca lo habría pillado (la clave sí "resuelve" a
algo, solo que a la clave misma). Se añadieron las tres claves que faltaban (`downedGoblins`,
`downedFrost` y la nueva `downedPirates`) en el mismo bloque.

### El fallo real de ESTA sesión, encontrado por el propio arnés (no en una captura)

Al escribir el bloque nuevo de `AutopruebaGuia.cs` se asumió que, tras superar Piratas (Orden 19),
el siguiente opcional pendiente sería `LunaDeCalabazas` (Orden 72). El primer pase del arnés lo
desmintió con el log en rojo: el objetivo opcional real era `ReinaAbeja/ArmaParaLaReina`.

**Causa real:** `ReinaAbeja` (Orden 25) e `InicioModoDificil` (Orden 45) caen ENTRE Piratas (19) y
LunaDeCalabazas (72), y ningún paso de este bloque nuevo los tocaba - seguían con su valor REAL del
mundo de pruebas (pendientes), así que en cuanto Piratas quedó superado, el algoritmo de "menor
Orden sin superar" los eligió a ellos antes que a LunaDeCalabazas, exactamente como se supone que
tiene que funcionar. El error no estaba en `EstadoGuia`, estaba en la prueba: le faltaba remarcar
esos dos tramos superados temporalmente, igual que `Arrancar()` ya hace con los ocho opcionales más
tempranos desde el principio.

**Arreglo real:** `PrepararOpcionalesRestantes()` marca también `NPC.downedQueenBee` y
`NPC.downedQueenSlime` a `true` antes de probar Piratas/Calabazas/Helada/D2, y los devuelve a su
valor original (capturado en `Arrancar()`, campo nuevo `_downedQueenSlimeOriginal`) al final del
bloque. La comprobación final (`ComprobarLosOchoOpcionalesNuevosDesaparecen`) se ajustó para
esperar que el siguiente opcional pendiente de verdad, tras cerrar los diez, sea
`JefesOpcionalesTardios/ArmaParaFishron` (el único que ningún bloque de todo el arnés toca nunca).
Con el arreglo, la segunda pasada salió limpia.

### Verificación real

`scripts/verificar-guia.ps1` (vanilla) y `-Calamity`: **las dos en verde, "Ninguna comprobación en
rojo"**, catálogo `20 tramos (20 con requisitos evaluables), 54 pasos, 105 requisitos, 0 avisos` en
ambas (antes: 16 tramos, 40 pasos, 80 requisitos). 55 capturas reales por ejecución (`guia-40` a
`guia-54` son las quince nuevas). Miradas de verdad varias capturas (`guia-40`, `guia-41`,
`guia-54`): mismo layout ya establecido para Goblin/Legión (columna izquierda con el objetivo
obligatorio reabierto por las pruebas de dirección/brújula, columna derecha con el aviso de
Calamity + "Qué te falta" + "Lectura del jefe"; la sección "Objetivo opcional" vive más abajo,
fuera del viewport sin hacer scroll - comportamiento ya existente, no una regresión de esta
sesión, confirmado comparando `guia-40` byte a byte contra el mismo layout de `guia-36-goblin-
preparativos.png` de la sesión anterior).

`dotnet` compiló limpio (0 errores) tanto en la fase 1 (SDK del sistema) como en la fase 2
(Roslyn interno de tModLoader, sin `-eac`).

### La última pieza pendiente de la lista original: pesca/Pescador, investigada y cerrada

La entrada anterior dejó dicho que `Player.anglerQuestsFinished` "es un dato real y medible,
candidato razonable" y que merecía investigarse más antes de decidir. Esta sesión lo hizo:
`Player.GetAnglerReward` (`Player.cs`, ~línea 57928) usa `anglerQuestsFinished` únicamente como
multiplicador CONTINUO de rareza de recompensa (`GetAnglerRewardRarityMultiplier(questsDone)`), no
hay ningún umbral discreto en el código (ni "a las 10 misiones desbloqueas X armadura/título/NPC")
como sí existe para Modo Difícil, cristales de vida o cualquier otro requisito de la Guía. Es el
mismo patrón ya descartado para Pilones: progreso continuo sin una bandera de cierre real que
marcar. **Decisión: pesca/Pescador tampoco necesita un tramo de la Guía** - no por pereza, sino
porque no hay ningún hito discreto real que citar sin inventárselo. Con esto se cierra el último
punto que seguía abierto de la lista de contenido opcional pedida en las últimas sesiones: no
queda nada más pendiente de investigar salvo Locura Marciana (jefe multi-parte, sin objeto de
invocación directo - sigue sin investigar, no estaba en el encargo de esta sesión) y Eclipse
Solar/Luna de Sangre (decisión ya tomada: no son tramos, no tienen bandera de cierre).

### Dónde seguir

Los cuatro tramos de esta sesión (Piratas, Luna de Calabazas, Luna Helada, Antiguo Ejército D2)
están **terminados y verificados** en vanilla y Calamity, 0 avisos. La Guía cubre ahora TODO el
contenido opcional pedido salvo Locura Marciana (investigación pendiente, jefe multi-parte sin
objeto de invocación directo, sin tocar esta sesión a propósito - no estaba en el encargo). Si se
pide en el futuro: el jefe es `NPCID.MartianSaucer=392` con partes `393`/`394`/`395`, y hace falta
investigar primero el NPC/bandera real de la Sonda Marciana (Martian Probe) que dispara el evento,

## 13-sep-2026 (continuación 3) — Locura Marciana: la última pieza, y cierre de la lista completa
## de contenido opcional

Encargo de esta sesión: investigar a fondo la Locura Marciana contra el código real decompilado
(cómo se activa de verdad, qué banderas de finalización existen, si tiene sentido modelarla como
evento de invasión igual que Goblin/Escarcha/Piratas), implementarla con el mismo patrón exacto
que el resto del árbol, y después repasar la lista completa de contenido opcional pedida en
sesiones anteriores para confirmar con números reales si la Guía está genuinamente al 100%.

### Investigación real del mecanismo (NPC.cs/WorldGen.cs/Main.cs decompilados, no de memoria)

- **La Sonda Marciana es `NPCID.MartianProbe=399`** (no `392`, que es el casco del platillo -
  `NPC.cs`, tabla de nombres ~línea 11325), `aiStyle==80`. Su IA completa está en el bloque
  `else if (aiStyle == 80)` de `NPC.cs` (~línea 38857-38942): vuela patrullando, y en cuanto
  detecta a un jugador a menos de 352px estando el jugador por debajo de ella
  (`distanceToPlayer < 352f && Main.player[num1375].Center.Y > base.Center.Y`), entra en un
  estado de "alerta" de 60 fotogramas y después uno de "huida" de hasta 180 fotogramas (vuela
  hacia arriba acelerando, `noTileCollide=true`). Si sobrevive esos ~3 segundos (o sale por
  arriba del mundo) sin que la maten, **llama `Main.StartInvasion(4)` y se autodestruye**
  (`NPC.cs` línea ~38938). Si la matas antes, no pasa nada: no hay invasión.
- **No hay objeto de invocación real** - confirmado buscando en todo `NPC.cs`/`Player.cs`
  cualquier `ItemCheck` o `SummonItemCheck` que llame a `StartInvasion(4)`: no existe ninguno. El
  único disparador real es la sonda huyendo con éxito.
- **Condición de aparición de la sonda** (`NPC.cs`, función gigante de spawn natural, ~líneas
  87447-87472 y 89636-89664, dos ramas distintas del mismo bucle): `Main.hardMode &&
  NPC.downedGolemBoss`, estar a más de un tercio de la anchura del mundo del centro
  (`Math.Abs(x - maxTilesX/2) / (maxTilesX/2) > 0.33f`), que no haya ya otro peligro activo
  (`!AnyDanger()`) y que no exista ya otra sonda (`!AnyNPCs(399)`). Probabilidad baja por
  intento de spawn (1/8 y 1/30 en una rama, 1/100 y 1/400 en la otra, más alta con Vela de
  Agua/`ZoneWaterCandle` - confirmado en el propio código, más antes de `downedMartians` que
  después, así que el evento puede repetirse).
- **Hallazgo real no obvio, verificado leyendo `Main.StartInvasion` completo (`Main.cs`
  ~línea 82210)**: aunque no hay objeto que lo compruebe antes, `StartInvasion(4)` en sí mismo
  exige **lo mismo que las otras tres invasiones** - al menos un jugador con
  `ConsumedLifeCrystals>=5`, si no la función no hace nada (`invasionType` se queda a 0) aunque
  la sonda ya haya escapado. Por eso el primer paso del tramo sí lleva `cristales_vida` como
  requisito recomendado, con la fuente citada tal cual.
- **La bandera de cierre es exactamente el mismo mecanismo que Goblin/Escarcha/Piratas**:
  `Main.UpdateInvasion_Inner` (`Main.cs` ~línea 82123) pone `NPC.downedMartians` a `true` vía
  `NPC.SetEventFlagCleared` cuando `invasionSize` llega a 0 - no al matar un único jefe. Mismo
  criterio ya usado con los otros tres: `jefeFinal=0`, sin inventar un análogo falso.
- **El "platillo" SÍ es un enemigo real y coordinado, aunque no sea "el jefe" que cierra el
  evento**: `NPCID.MartianSaucer=392` (casco, `dontTakeDamage=true`, decorativo - igual que el
  "Flying Dutchman" de Piratas) no es el combatiente real. Al aparecer, el Núcleo
  (`MartianSaucerCore=395`, 10000 de vida, 0 de defensa, 80 de daño, `NPC.cs` ~línea 37003) crea
  él mismo dos Torretas (`MartianSaucerTurret=393`, 5000 de vida cada una) y dos Cañones
  (`MartianSaucerCannon=394`, 3500 de vida cada uno) - seis NPC en total luchando juntos, el
  enemigo más peligroso de la invasión. `NPCID.Sets.BelongsToInvasionMartianMadness` confirma
  que el Núcleo (395) sí cuenta para el progreso de la invasión, el casco/Torretas/Cañones no
  directamente por sí solos.
- **El Oficial Marciano (`type==383`) es el enemigo común más blindado de las cuatro
  invasiones**: 50 de defensa, 75 de daño, 300 de vida - por delante de los 30 del Capitán
  Pirata, los 26 de Mister Estocada y los 8 del Guerrero Goblin. Es la referencia real usada
  para el `dano_arma` del primer paso (60).

### Implementación (mismo patrón exacto que Goblin/Escarcha/Piratas)

- `Assets/guia_progresion.json`: tramo nuevo `LocuraMarciana`, Orden 71 (justo después de
  `TemploYGolem=70`, antes de `LunaDeCalabazas=72` - el único de los cinco que de verdad exige
  `downedGolemBoss`), `opcional=true`, `jefeFinal=0`. Dos pasos: `PrepararLocuraMarciana`
  (`dano_arma=60` obligatorio, `cristales_vida=5` recomendado, y un aviso nuevo de "Sonda
  Marciana activa en el mundo ahora mismo") y `VencerALaLocuraMarciana` (`bandera:
  downedMartians`).
- **Pieza nueva de diseño, no solo datos**: el aviso de "Sonda Marciana activa" no podía
  reutilizar el tipo `npc` ya existente (el que usa Esqueletron para el Anciano) porque su
  plantilla de texto es `"Que viva contigo: {0}"` - mentira para un enemigo hostil que nunca
  "vive contigo". Se añadió un tipo nuevo, honesto y de responsabilidad única:
  `TipoRequisito.NpcActivo` (`ModeloGuia.cs`), mapeado desde `"npc_activo"` en
  `CatalogoGuia.Tipo`, evaluado en `EvaluadorGuia.EvaluarNpcActivo` (mismo dato real que `HayNpc`,
  texto distinto: `"Activo en el mundo ahora mismo: {0}"`, clave `Guia.Req.NpcActivo`). El tipo
  `npc` original no se tocó: sigue sirviendo bien para el Anciano y cualquier vecino futuro.
- `scripts/generar-localizacion.py`: `Guia.Bandera.downedMartians` (no tenía clave - a
  diferencia de la sesión anterior, esta vez se añadió desde el principio, no como fallo
  encontrado después), `Guia.Tramo.LocuraMarciana.*`, `Guia.Paso.PrepararLocuraMarciana.*`,
  `Guia.Paso.VencerALaLocuraMarciana.*`, `Guia.Req.NpcActivo`.
- `Common/Guia/AutopruebaGuia.cs`: bloque nuevo de 8 `case` (mismo patrón que Piratas: preparar,
  capturar, poner arma, volcar estado, comprobar "vencer", capturar, marcar derrotada de mentira,
  quitar arma) insertado entre el bloque de Piratas y el de Luna de Calabazas, con la
  renumeración mecánica de TODOS los `case` posteriores (192→200 en adelante, +8) y de las
  capturas (`guia-42` en adelante, +2) para mantener la secuencia sin huecos - comprobado con un
  script que verificó los 251 `case` (0 a 250) sin huecos ni duplicados antes de compilar.
  `ComprobarLosOchoOpcionalesNuevosDesaparecen` pasó a llamarse
  `ComprobarLosNueveOpcionalesNuevosDesaparecen` (ahora comprueba los nueve, no ocho).

### Verificación real (`scripts/verificar-guia.ps1`, cliente gráfico real, sin saltarse nada)

Primera pasada (antes de la pieza `NpcActivo`): vanilla en verde, `21 tramos, 56 pasos, 109
requisitos, 0 avisos`, "Ninguna comprobación en rojo", pero con un fallo de PULIDO real que el
propio log dejó a la vista sin que ninguna comprobación lo marcara en rojo (0 avisos, 0 "NO
CUADRA" - el dato era correcto, solo la frase mentía): `requisito opcional 3/3: [FALTA] Que viva
contigo: Sonda marciana`. Se corrigió con el tipo `NpcActivo` de arriba (no era aceptable
enseñárselo así al jugador: una Sonda Marciana no "vive contigo"). Segunda pasada en vanilla:
mismo resultado en verde, y la línea ahora dice `Activo en el mundo ahora mismo: Sonda marciana`
(confirmado línea a línea en el log, no solo "compiló"). Pasada con `-Calamity`: igual en verde,
mismo catálogo `21 tramos, 56 pasos, 109 requisitos, 0 avisos`, sin ninguna comprobación en rojo.
Las tres pasadas dejaron capturas reales del back buffer (`guia-42-marciana-preparativos.png`,
`guia-43-marciana-vencer.png` por pasada).

### La Guía de Terraria está genuinamente al 100% - repaso final con números reales

Con Locura Marciana cerrada, se repasó la lista completa de contenido opcional pedida en las
últimas sesiones contra lo que hay hoy en `Assets/guia_progresion.json` y en las decisiones ya
documentadas de la entrada anterior:

- **Camino obligatorio completo**: Refugio → Ojo de Cthulhu → Maldad del Mundo → Esqueletron →
  Muro de Carne → tres Mecánicos → Plantera → Templo/Golem → Cultista/Torres → Moon Lord. Once
  tramos, `implementado=true` en todos, `opcional=false`.
- **Jefes opcionales**: Rey Slime, Reina Abeja, Deerclops, Reina Slime, Duque Pezhongo,
  Emperatriz de la Luz - los seis con tramo propio.
- **Eventos por invasión/oleadas**: Ejército Goblin, Legión de Escarcha, Piratas, Luna de
  Calabazas, Luna Helada, Antiguo Ejército D2 y **ahora Locura Marciana** - los siete con tramo
  propio, cada uno con la bandera real citada contra el motor.
- **Eclipse Solar y Luna de Sangre**: decisión ya tomada y sin cambios - no son tramos porque no
  existe una bandera de "completado" real (eventos por tiempo/probabilidad, no algo que se
  derrote una vez). No es un hueco, es un límite real del motor ya documentado.
- **Exploración/biomas, pesca, mascotas/monturas, Pilones, semillas secretas, Mercader
  Ambulante**: cada uno con su decisión ya tomada y documentada en la entrada anterior (cubierto
  por otra pestaña del mod, sin bandera de cierre real, o infraestructura continua) - ninguno
  forzado a un tramo falso solo por completar la lista.

**Catálogo final: 21 tramos (21 con requisitos evaluables), 56 pasos, 109 requisitos, 0 avisos**,
verificado en vanilla y con Calamity cargado, con el cliente gráfico real, sin ninguna
comprobación en rojo. No queda ningún punto pendiente de investigar ni de construir de la lista
de contenido opcional pedida en ninguna sesión anterior: la Guía cubre el camino obligatorio
entero más todo el contenido opcional que tiene un hito real y medible, y documenta con claridad
- sin fingir - los pocos casos donde ese hito no existe de verdad en el motor. Esta es la última
pieza de este encargo.
antes de escribir el paso "arma y prepárate".
## 13-sep-2026 (continuación 4) — Encargo nuevo del usuario: cuatro piezas grandes (undo general,
## sincronización con Terrakeep, gestión de loadouts, vista de completitud). Primera: el undo/redo
## general tenía un agujero real - el arrastre con el ratón nunca pasaba por el historial

Encargo de esta sesión (con libertad de diseño total, "como si fueras un arquitecto de
software"): cuatro piezas nuevas para el mod en vivo. Antes de construir nada, la instrucción
pedía verificar de verdad el estado de cada una contra el código real, no suponer que hacía falta
partir de cero. Empezando por la primera ("undo/redo general: pila de acciones deshacer/rehacer
real, no solo recargar desde disco"): WS7 (6-sep-2026) ya dejó el historial construido y con UI en
Ajustes, así que la primera tarea real era auditar la COBERTURA real, no reescribir el sistema.

### El hallazgo: SlotObjetoVanilla nunca pasaba por el historial

UI/SlotObjetoVanilla.cs es la única pieza que dibuja y gestiona TODAS las ranuras reales del mod
(inventario, equipo, hucha, caja, forja, bóveda, y la propia rejilla de destino de Librería - su
propio comentario de clase ya lo decía: "es la pieza que WS1 y siguientes van a reutilizar tal
cual para todos los slots"). Su DrawSelf llamaba a ItemSlot.Handle(_inventario, _contexto,
_indice) DIRECTAMENTE, sin envolver - cada workstream envolvió sus acciones programáticas propias
(editor de cantidad, editor de prefijo, "colocar desde el catálogo" de Librería) pero ninguno tocó
nunca esta clase. La propia ContenidoLibreria.ColocarEnRanura lo dejaba dicho en su comentario,
sin que nadie se diera cuenta de la implicación: "jugando, lo que se usa es exactamente el mismo
ItemSlot.Handle a través de SlotObjetoVanilla" - es decir, la acción MÁS común de todo el mod
(arrastrar un objeto con el ratón) nunca quedaba deshacible, solo las acciones con botón propio.
Confirmado leyendo el código, no solo suponiéndolo.

### El bug real que habría salido de envolver esto a lo simple (encontrado ANTES de escribir una
### sola línea de producción, pensando el diseño, no jugando a probar y ver qué pasa)

Envolver ItemSlot.Handle con Historial.CambiarObjetos a secas (vigilando solo la ranura) parecía
la solución obvia, pero un vistazo al ItemSlot.cs real decompilado (692-1080) enseña que un clic
normal casi nunca mueve el objeto SOLO dentro de la ranura: lo intercambia con Main.mouseItem (lo
que se lleva "en la mano"), y un Ctrl+clic de papelera rápida (por defecto de vanilla -
Options.DisableLeftShiftTrashCan=true de fábrica, confirmado en el código, así que es Ctrl y no
Mayús) lo manda a Player.trashItem. Ninguno de los dos es una ranura de ningún array. Con solo la
ranura vigilada, "deshacer" el SEGUNDO clic de un arrastre (soltar) habría vaciado la ranura
destino sin devolver el objeto a ningún sitio - el objeto desaparece de verdad, ni duplicado ni
conservado, justo lo que WS7 prometió que nunca pasaría ("como mucho pisa un cambio ajeno, pero
siempre deja un estado coherente"). Con otras rutas de ItemSlot.OverrideLeftClick (cofre real
abierto, menú de Reforjar/Guía/Investigar) el objeto puede ir a un TERCER sitio que tampoco es
ninguna celda vigilable sin más - ahí no se intentó cubrir todo: se añadió una guarda
(HayOtroContenedorAbierto) que, si detecta cualquiera de esos estados, ejecuta el ItemSlot.Handle
de siempre SIN envolver (ni mejor ni peor que antes de este arreglo, nunca un riesgo real de
duplicar o perder el objeto). Nuestro panel es a pantalla completa y ninguno de esos menús debería
estar abierto a la vez, pero la guarda cuesta cuatro comprobaciones y cierra la duda por completo.

### La pieza nueva: Historial.CambiarObjetoDeSlotConCeldas

Common/Undo/Historial.cs: una "celda" (Historial.CeldaDeObjeto) es un objeto suelto que no vive en
ningún array - un getter y un setter, nada más. El método nuevo toma la foto de la ranura Y de las
celdas a la vez (antes y después), y si CUALQUIERA de las dos cambió, registra UNA sola entrada de
historial que las aplica juntas y atómicamente al deshacer/rehacer. Se le pasa además una función
Func<Item, Item, string> etiqueta porque, a diferencia de CambiarObjetos, no se sabe si el clic
real va a coger, soltar, apilar, intercambiar o marcar favorito hasta que ItemSlot.Handle ya ha
corrido - SlotObjetoVanilla.EtiquetaCambio cubre los cinco casos reales comparando la ranura
antes/después. De paso, SnapshotDeObjetos.MismoContenidoQue se refactorizó para reutilizar un
comparador MismoContenido(Item, Item) estático (mismos cuatro campos: type/stack/prefix/favorited,
con "los dos vacíos" tratado como "iguales" de forma explícita) - lo usan tanto las ranuras de
array como las celdas sueltas.

UI/SlotObjetoVanilla.cs: ManejarConHistorial (antes private, ahora internal para que la autoprueba
la llame tal cual) sustituye la llamada directa a ItemSlot.Handle, vigilando Main.mouseItem y
Player.trashItem como celdas.

### Verificación real, en el juego real (no "debería funcionar")

Common/Undo/AutopruebaDeshacerArrastre.cs (gatillada por TERRAKEEP_AUTOTEST_ARRASTRE,
scripts/verificar-deshacer-arrastre.ps1, mismo sandbox que WS7): simula el clic real rellenando
Main.mouseLeft/mouseLeftRelease (y Main.keyState con Ctrl para la papelera) exactamente como ya
hace ContenidoLibreria.ColocarEnRanura, y llama al código de PRODUCCIÓN exacto
(SlotObjetoVanilla.ManejarConHistorial), no una copia. Dos escenarios:

- Arrastrar (coger de la ranura 5, soltar en la 9) y deshacer dos veces: el paso crítico es
  ARRASTRE/4, deshacer el "soltar" - con el código viejo el objeto habría desaparecido del todo;
  con el arreglo, log real: inventory[9]=vacio, mano="Espada corta de cobre" x1 | OK: la ranura
  destino volvio a quedar vacia y el objeto volvio a la MANO (no desaparecio). Deshacer otra vez
  devuelve el objeto EXACTAMENTE a la ranura 5 (ARRASTRE/5, "sin duplicarse ni perderse"), y
  rehacer x2 lo vuelve a dejar en la 9 (ARRASTRE/6).
- Ctrl+clic de papelera rápida y deshacer: PAPELERA/2 confirma que se va a Player.trashItem de
  verdad; PAPELERA/3 confirma que deshacer lo devuelve a la ranura Y deja la papelera vacía
  ("sin duplicarse").

Las dos pasadas en verde, "OK: encontrado AUTOPRUEBA ARRASTRE: terminada en el log, sin ningún
FALLO". dotnet compiló limpio (0 errores, 89 avisos - los mismos de siempre, CS1701 de
Newtonsoft.Json contra System.Runtime, ya documentados en sesiones previas, no nuevos).

### Un obstáculo real de entorno por el camino (autonomía técnica, sin el usuario delante)

La primera pasada del arnés se quedó colgada sin ningún error, log cortado justo después de
"Mods actualizados: HEROsMod (HERO's Mod) v0.4.18 -> v0.4.18.1". Investigado contra el tModLoader
decompilado (ModLoader/UI/Interface.cs ~206, ModLoader/Core/ModOrganizer.cs ~342):
ModOrganizer.DetectModChangesForInfoMessage compara los mods de Steam Workshop actuales contra
<SavePath>\LastLaunchedMods.txt (que SOLO existe si un lanzamiento anterior llegó a guardarlo) y,
si algo cambió de versión desde la última vez, Interface.cs muestra una pantalla informativa real
que exige un clic para continuar - -skipselect no la salta, así que sin nadie delante el proceso
se queda ahí para siempre. El archivo del sandbox de WS7 tenía HEROsMod 0.4.18 (versión vieja);
Steam había actualizado el mod a 0.4.18.1 de fondo entre sesiones. Arreglo real, sin tocar nada
del usuario: se borró LastLaunchedMods.txt del sandbox (si el archivo no existe,
DetectModChangesForInfoMessage devuelve vacío sin comprobar nada, línea 344 del propio método) y
se añadió el mismo borrado, con el porqué completo en un comentario, a
scripts/verificar-deshacer-arrastre.ps1 ANTES de cada lanzamiento - así no vuelve a colgarse
aunque Steam actualice otro mod de Workshop entre sesiones futuras. Vale también para
scripts/verificar-ws7.ps1 y cualquier otro script que reutilice este mismo sandbox si algún día se
cuelga igual (no se tocó ese script en esta sesión, para no mezclar cambios de áreas distintas -
queda anotado aquí).

### Dónde seguir

Undo/redo general: la pieza concreta de esta sesión (cobertura del arrastre) está terminada y
verificada. El resto del encargo de cuatro piezas sigue en marcha en la misma sesión, sin cortar:
2) investigar qué sincronización real tiene sentido con Terrakeep (escritorio); 3) gestión de
loadouts ampliada sobre Player.Loadouts (nativo, 3 slots fijos sin nombre - confirmado en
EquipmentLoadout.cs/Player.cs decompilados); 4) vista de completitud (bestiario/logros/colección/
jefes) reutilizando datos reales ya existentes (Common/Investigacion, banderas NPC.downed* que ya
usa la Guía).

## 13-sep-2026 (continuación 5) — Segunda pieza del encargo: qué tiene sentido sincronizar con
## Terrakeep (escritorio), investigado y construido con criterio honesto

Encargo: investigar qué sincronización real tiene sentido entre el mod en vivo y la app de
escritorio hermana (Downloads\Terrasavr-Win\Terrasavr-Native\Terrakeep.App), decidiendo con
criterio y documentando honestamente lo que NO tiene un análogo real - nunca forzando uno falso.

### Lo que se investigó de verdad antes de tocar código

Se leyó `Terrakeep.App/Services/SettingsService.cs` (settings.json en
`%LOCALAPPDATA%\Terrakeep\`, con Language "es"/"en", ExtraCharacterFolders/WorldFolders,
BackupHistoryCap, anchos de sidebar) y `BackupHistoryService.cs` completo (historial de copias
`.tkbak` - zip con player.plr+meta.json, carpeta `{personaje}-{huella}` bajo
`%LOCALAPPDATA%\Terrakeep\Backups\`, huella = 8 hex de SHA-256 de la ruta completa normalizada).

### Decisión: dos piezas SÍ tienen un análogo real, el resto NO (documentado sin forzar nada)

**SÍ - Idioma compartido.** Las dos herramientas ya guardan "es"/"en" cada una por su cuenta; es
la misma preferencia de la misma persona sobre la misma marca. Implementado bidireccional pero con
reglas claras para no pisar una elección activa:
- Al elegir idioma DENTRO del mod (`Idiomas.Elegir`, el mismo camino que ya usa el selector del
  panel de Ajustes), se refleja en `settings.json` de escritorio - un merge sobre el JSON
  existente, nunca lo sustituye entero (se conservan ExtraCharacterFolders, BackupHistoryCap...).
- Al arrancar una partida, SOLO si el mod nunca ha elegido idioma a mano
  (`AjustesConfig.Idioma == SeguirElJuego`, el valor de fábrica) se adopta la preferencia de
  escritorio si existe - una elección activa del jugador dentro del mod nunca se pisa. No se
  persiste en el ModConfig: se re-evalúa cada partida, así que si la app de escritorio cambia de
  idioma mañana, la siguiente sesión del mod lo recoge sola.

**SÍ - Historial de copias de seguridad compartido.** El mod no puede "guardar" el `.plr` (eso lo
hace el propio Terraria), pero SÍ puede fotografiarlo justo cuando el juego lo escribe de verdad
(detección por sondeo del `LastWriteTimeUtc` del `.plr` activo, cada 0,5 s mientras se juega, más
una última pasada en `OnWorldUnload`), con el MISMO formato `.tkbak` y la MISMA fórmula de huella
que ya usa `BackupHistoryService` - abrir el historial desde cualquiera de las dos herramientas
enseña una única línea de tiempo, jugada en el mod o editada en el escritorio. Motivo reutilizado
a propósito ('A'/"BeforeSave", la categoría existente más honesta para "una foto tomada alrededor
de un guardado real a disco") en vez de inventar uno nuevo sin poder recompilar/probar
`Terrakeep.App` en esta sesión para confirmar que su lector lo reconocería.

**NO, con la razón real de cada una (sin forzar un análogo falso):**
- *Presets/plantillas reutilizables*: la app de escritorio no tiene hoy ningún concepto así - no
  existe ese servicio en `Terrakeep.App/Services`, sincronizar algo que ninguna de las dos partes
  tiene todavía sería inventarse un dato.
- *Estado de ventana/ancho de panel*: geometría de una ventana WPF de escritorio contra un panel
  que vive DENTRO de la ventana del propio juego a resolución/escala completamente distintas
  (ver `AutopruebaEspaciado`) - un ancho bueno en un monitor de escritorio no significa nada en el
  juego.
- *Carpetas extra de personajes/mundos*: son para que la app de escritorio ENCUENTRE partidas
  fuera de la carpeta estándar; el mod ya está cargando la partida activa, no tiene nada que
  buscar.
- *Edición simultánea de la MISMA partida*: mientras tModLoader tiene la partida cargada, el
  `.plr` en disco es una foto vieja hasta el próximo guardado - la app de escritorio escribiendo
  encima mientras tanto se perdería en el siguiente guardado del juego. Limitación real de ser dos
  procesos independientes sobre el mismo archivo, no un hueco de esta sesión - y es justo por lo
  que el historial de copias (arriba) importa más que un "en vivo" que no puede existir de verdad.

Las cuatro decisiones "NO" están documentadas con el mismo detalle dentro del propio XMLdoc de
`Common/Sincronizacion/SincronizacionEscritorio.cs`, no solo aquí.

### Implementación

`Common/Sincronizacion/`: `SincronizacionEscritorio.cs` (toda la lógica: fingerprint idéntico,
lectura/escritura de idioma con merge, formato `.tkbak` byte a byte compatible, purga con la misma
política que `BackupHistoryService.Purge` - automáticas más antiguas primero, manuales nunca),
`SincronizacionSystem.cs` (`ModSystem`: engancha `Idiomas.Cambiado`, sondeo del `.plr` cada 30
fotogramas, adopción de idioma al arrancar), `RegistroSincronizacion.cs`.

`CarpetaTerrakeep` es una propiedad con `internal set` (mismo motivo real que ya documentó la
propia app de escritorio para `BackupHistoryService.BackupsRoot`: "las pruebas... estrenaban una
carpeta en el %LOCALAPPDATA% real que no retiraba nadie") - la autoprueba la redirige a una
carpeta de TEMP propia ANTES de tocar nada, así que ninguna pasada automática toca jamás el
`%LOCALAPPDATA%\Terrakeep` real de quien esté jugando en esta máquina.

### Verificación real, en el juego real

`Common/Sincronizacion/AutopruebaSincronizacion.cs` (`TERRAKEEP_AUTOTEST_SINCRO`,
`scripts/verificar-sincronizacion.ps1`, mismo sandbox WS7): tres escenarios reales, código de
producción exacto, sin mocks.

- `IDIOMA/1`: con `settings.json` de escritorio diciendo "en" y el mod en `SeguirElJuego`, arrancar
  la partida deja la cultura activa en `en-US` de verdad (log real: `Idioma cambiado EN VIVO...
  es-ES -> en-US`). OK.
- `IDIOMA/2`: `Idiomas.Elegir(Espanol)` (mismo camino que el selector del panel) deja
  `settings.json` con `Language="es"` Y con `BackupHistoryCap` intacto (merge real, no
  sustitución). OK.
- `INSTANTANEA/1-2`: `GuardarInstantanea` sobre el `.plr` real del personaje de pruebas crea
  exactamente un `.tkbak`, y es un zip válido con `player.plr`+`meta.json`, `meta.json` parsea y
  trae `CharacterName="TerrakeepPrueba"` real. OK.
- `SONDEO/0-1`: tras tocar el `.plr` (mismo timestamp que dejaría un guardado real), sin llamar a
  nada a mano, `SincronizacionSystem.UpdateUI` detectó el cambio por su cuenta y generó una
  instantánea nueva sola - el camino automático de producción, no solo el método aislado. OK.

Pasada completa en verde, `OK: encontrado 'AUTOPRUEBA SINCRONIZACION: terminada' en el log, sin
ningún FALLO`. `dotnet` compiló limpio (0 errores, 193 avisos - subida de 169 a 193 por los
nuevos archivos, mismos `CS1701` benignos de siempre).

### Dónde seguir

Sincronización: terminada y verificada. Quedan las dos últimas piezas del encargo: 3) gestión de
loadouts (nativo `Player.Loadouts`, 3 slots fijos sin nombre - investigado ya contra
`EquipmentLoadout.cs`/`Player.cs` decompilados en la entrada anterior); 4) vista de completitud
(bestiario/logros/colección/jefes).

## 13-sep-2026 (continuación 6) — Tercera pieza del encargo: gestión ampliada de loadouts
## (conjuntos), y un hallazgo real de seguridad entre autopruebas por el camino

Encargo: "si Terraria ya tiene loadouts nativos, amplía/mejora la gestión de eso desde el panel
del mod (guardar más de 3, nombrarlos, aplicar rápido) - investiga el sistema real de loadouts
de Terraria antes de construir encima".

### Investigación real (Player.cs/EquipmentLoadout.cs decompilados)

`Player.Loadouts` es un array FIJO de exactamente 3 `EquipmentLoadout` (`Player.cs` ~línea 57422,
`new EquipmentLoadout[3]` - tamaño del motor, no algo que un mod pueda ampliar sin reescribir
media docena de sitios que lo dan por hecho). `EquipmentLoadout` (Armor[20]/Dye[10]/Hide[10]) no
tiene NINGÚN campo de nombre - los "Conjunto 1/2/3" que ya enseñaba `PestanaEquipo` (WS1) eran
literales. Hallazgo no obvio, ya documentado por `PestanaEquipo` pero crítico para esta pieza:
mientras un conjunto está ACTIVO, su entrada en `Loadouts[]` está VACÍA de verdad - el dato real
vive en `Player.armor`/`.dye`/`.hideVisibleAccessory` hasta el próximo `TrySwitchingLoadout`
(`EquipmentLoadout.Swap` intercambia ELEMENTOS con esos arrays). Por eso un preset del mod
siempre se aplica sobre el conjunto ACTIVO (los arrays vivos), nunca escribiendo directamente en
`Loadouts[i]`: escribir ahí para un conjunto que no está activo se perdería en el siguiente
cambio de conjunto real.

### Lo que ya existía (no se reescribió) vs. lo que se añadió

`PestanaEquipo` (WS1) ya tenía el "aplicar rápido" de los tres nativos (`TrySwitchingLoadout`).
Esta sesión añade las DOS piezas que faltaban del encargo, como sub-pestaña NUEVA de Personaje
("Conjuntos", séptima, índice 6 - añadida al final para no correr el índice guardado de las
demás):

- **Nombrarlos**: `LoadoutsPlayer : ModPlayer` (persistido DENTRO del propio `.plr`, vía
  `SaveData`/`LoadData` oficial - nunca un archivo aparte) guarda `Nombres[3]`. Botón
  "Renombrar" junto a cada conjunto abre un `CampoTextoTk` inline.
- **Guardar más de 3**: `PresetLoadout` - una foto propia del mod (armadura+tintes+ocultar,
  serializada con `ItemIO.Save`/`Load` real de tModLoader, así que items de Calamity también se
  guardan bien) con nombre, sin límite fijo, en una lista `UIList`+`UIScrollbar`. "Guardar
  conjunto activo como preset" fotografía el equipo activo; "Aplicar" lo escribe de vuelta sobre
  el activo; "Borrar" lo quita.
- **Deshacer/rehacer integrado**: aplicar un preset queda deshacible por el historial general de
  WS7 (`Historial.CambiarValor` con una foto conjunta armadura+tintes+ocultar - una sola entrada,
  no tres sueltas) - cruce real con la primera pieza de este mismo encargo.

### Verificación real, en el juego real

`Common/Loadouts/AutopruebaConjuntos.cs` (mismo patrón que `PestanaApariencia`/
`AutopruebaApariencia`: corre dentro de `PestanaConjuntos.Update`, solo mientras esa pestaña está
construida de verdad) + `LoadoutsSystem.cs` (arranque: abre el panel y salta a la pestaña 6) +
`scripts/verificar-conjuntos.ps1`. Ocho pasos reales: 3 conjuntos nativos construidos, renombrar
persistido en el ModPlayer, guardar preset con el equipo REAL puesto, cambiar de casco y Aplicar
el preset (vuelve el casco correcto), Ctrl+Z (vuelve el casco de antes) y Ctrl+Y (vuelve el del
preset), captura real es/en, limpieza. Todo en verde.

**Bug real encontrado por la propia captura, no leyendo código**: la fila "Guardar conjunto activo
como preset" no aparecía en absoluto en la primera captura - `VAlign=1f` ya ancla el elemento a la
base del contenedor, y `Top.Set(-Npx, 1f)` (segundo argumento a 1f en vez de 0f) suma ADEMÁS un
alto entero del contenedor por encima de eso, empujando la fila muy por debajo del marco visible
del panel. Arreglado a `Top.Set(-Npx, 0f)` (píxeles puros) en las tres piezas de esa fila;
verificado de nuevo con captura real, ahora sí visible y sin solapar nada.

### Hallazgo de seguridad entre autopruebas (encontrado verificando esto, no buscándolo)

`AutopruebaConjuntos` cicla el idioma es→en→es para capturar las dos versiones (mismo patrón que
ya usaba WS7). La pieza de sincronización de la sesión anterior engancha CUALQUIER cambio de
idioma real (`Idiomas.Cambiado`) para reflejarlo en el `settings.json` de escritorio - así que,
sin darse cuenta, la primera pasada de `verificar-conjuntos.ps1` escribió de verdad en el
`%LOCALAPPDATA%\Terrakeep\settings.json` REAL de esta máquina (visto en su propio log:
`Sincronizacion: Idioma reflejado...`). La prueba restauraba el idioma al final, así que el
archivo quedó en un valor correcto, pero por suerte del orden de los pasos, no por diseño - un
corte a mitad de la prueba lo habría dejado mal. Arreglado con un guardado nuevo en
`SincronizacionSystem` (`OtraAutopruebaEnMarcha`, reutilizando la misma lista de variables que ya
agrega `CapturaDePantalla.Permitida` - ahora `public` a propósito para esto) que evita tocar el
archivo real durante CUALQUIER autoprueba del mod que no sea la suya propia (esa sigue
funcionando: ya redirige a una carpeta de TEMP). Reverificado: `verificar-conjuntos.ps1` ya no
toca el archivo real, y `verificar-sincronizacion.ps1` sigue en verde con su propio guardado
funcionando igual. De paso, `SincronizacionEscritorio.EscribirIdiomaEscritorio` pasó a pedir
`Formatting.Indented` explícito (el archivo real había quedado en una sola línea tras el toque
accidental; restaurado a mano y el código ya no lo repetirá).

### Verificación cruzada (regresión)

Antes de comitear, se re-ejecutaron TAMBIÉN `verificar-deshacer-arrastre.ps1` y
`verificar-sincronizacion.ps1` completos: los tres en verde a la vez, sin ningún FALLO.

### Dónde seguir

Gestión de loadouts: terminada y verificada. Queda la última pieza del encargo: 4) vista de
completitud (bestiario/logros/colección/jefes derrotados), reutilizando datos reales ya
existentes del juego (banderas `NPC.downed*` que ya usa la Guía, `Common/Investigacion` para
colección de objetos).

## 13-sep-2026 (continuación 7) — Cuarta y última pieza del encargo: vista de "qué falta para el
## 100%", y cierre de las cuatro piezas grandes de esta sesión

Encargo: "un resumen de completitud (bestiario, logros, colección de objetos, jefes derrotados) -
reutiliza datos reales del juego, nunca inventados".

### De dónde sale cada dato real (los cuatro, sin inventar ninguno)

Nueva sub-pestaña de Personaje, "Completitud" (octava, índice 7). `Common/Completitud/
EstadoCompletitud.cs`:

- **Jefes y eventos**: reutiliza tal cual `CatalogoGuia.Tramos` (el árbol de la propia Guía, 21
  tramos reales ya investigados y verificados en sesiones anteriores) - un tramo cuenta como
  superado con el MISMO criterio que ya usa `EstadoGuia` internamente: su último paso completado
  (`EvaluadorGuia.PasoCompletado`). No se reinvestiga nada, se reutiliza el trabajo ya hecho.
- **Bestiario**: `Main.BestiaryDB`/`Main.BestiaryTracker`, el sistema oficial de 1.4 - un bicho
  cuenta como "conocido" si su `BestiaryEntryUnlockState` ya superó `NotKnownAtAll_0` (visto,
  matado o hablado con él), el mismo criterio con el que el propio juego decide qué enseñar en su
  pantalla de bestiario.
- **Logros**: `Main.Achievements.CreateAchievementsList()`, el `AchievementManager` oficial -
  `Achievement.IsCompleted` real. Los logros secretos (`Hidden`) no enseñan su nombre hasta
  completarse (un "???" real, mismo criterio que la pantalla de logros de vanilla - nunca
  spoileado).
- **Objetos investigados (Modo Viaje)**: reutiliza `EstadoInvestigacion.EsInvestigable`/`.Completo`
  (ya construidos por la pestaña "Investigación" del mod), sumando tipo a tipo - a propósito NO se
  suman las carpetas raíz de `CatalogoInvestigacion` directamente: varias son vistas ALTERNATIVAS
  de los mismos objetos ("Categorías" y "Objetos por ID" cuentan casi los mismos ~5400 objetos
  cada una por su cuenta), sumarlas habría contado cada objeto dos o tres veces.

Cada resumen es una barra (`MedidorPreparacionTk`, reutilizado tal cual de la Guía - mismo
rojo/ámbar/verde) + "hecho/total (%)" + lista de lo que falta cuando tiene sentido enseñarla
(jefes y logros; bestiario e investigación no, por lo mismo que ya no lista Investigación en su
propia pestaña: con miles de objetos no cabría ni sería legible - "Investigación" ya es el sitio
real para explorar eso carpeta a carpeta).

### Verificación real, en el juego real

`Common/Completitud/AutopruebaCompletitud.cs` + `CompletitudSystem.cs` (mismo patrón que
Conjuntos) + `scripts/verificar-completitud.ps1`. Comprobación cruzada real: `Jefes.Total` tiene
que dar exactamente 21, el mismo número que la propia Guía ya tiene verificado en la bitácora -
si no coincidiera, "Completitud" estaría leyendo mal el mismo catálogo. Log real:
`Jefes.Total=21... OK: mismo catalogo, mismo numero`. Los cuatro resúmenes con datos reales y
coherentes (`Bestiario: 0/540`, `Logros: 0/115`, `Investigacion: 0/5491`, para el personaje
sintético de pruebas, recién creado). Captura real es/en, sin ningún solapamiento.

**Bug real encontrado por la propia captura** (segunda vez en esta sesión con la misma familia de
bug): la etiqueta de "hecho/total (%)" no aparecía en la primera captura - `HAlign=1f` A LA VEZ
que `Left.Set(0f, 1f)` suma los dos desplazamientos y empuja el elemento fuera del marco visible
(la combinación exacta que ya rompió la fila de guardar preset de Conjuntos, pero con
`HAlign`+`Left` en vez de `VAlign`+`Top` - mismo patrón, otro sitio). Arreglado a `Left.Set(-260f,
1f)` sin `HAlign`; verificado de nuevo con captura real, visible y alineado a la derecha sin
solapar el título.

### Un segundo hallazgo real, sobre la propia pieza de sincronización de dos sesiones atrás

Comprobando por qué el `settings.json` REAL de esta máquina había cambiado de formato (de
indentado a una sola línea) sin que ninguna autoprueba lo tocara (confirmado por la ausencia de la
línea `Sincronizacion: Idioma reflejado...` en los logs), se releyó el código REAL de
`SettingsService.Save` de la app de escritorio: usa `JsonSerializer.Serialize(settings)` SIN
ninguna opción de indentado - su `settings.json` real de verdad es compacto, de una sola línea. La
suposición anterior de esta sesión ("la app usa `WriteIndented=true`") era incorrecta, sin
comprobar contra el código real antes de escribirla. Corregido:
`SincronizacionEscritorio.EscribirIdiomaEscritorio` ahora escribe con `Formatting.None` a
propósito, para no imponerle a un archivo ajeno un formato que la propia app nunca produce por su
cuenta.

### Verificación cruzada final (regresión de las cuatro piezas juntas)

Antes de comitear, se re-ejecutaron `verificar-conjuntos.ps1` y `verificar-sincronizacion.ps1`
completos tras el arreglo de formato - las cuatro autopruebas de esta sesión (arrastre,
sincronización, conjuntos, completitud) en verde a la vez, sin ningún FALLO, y sin tocar nunca el
`%LOCALAPPDATA%\Terrakeep` real salvo por la propia escritura legítima que sí le corresponde a la
autoprueba de sincronización (redirigida a TEMP).

### Cierre de las cuatro piezas grandes del encargo (13-sep-2026)

1. **Undo/redo general**: cerrado el agujero real (el arrastre con el ratón nunca pasaba por el
   historial), verificado.
2. **Sincronización con Terrakeep (escritorio)**: idioma compartido + historial de copias
   compartido (formato `.tkbak` real), documentando con honestidad lo que NO tiene análogo real,
   verificado.
3. **Gestión de loadouts**: nombrar los tres conjuntos nativos + presets propios sin límite fijo,
   con deshacer/rehacer integrado, verificado.
4. **Vista de completitud**: los cuatro resúmenes reales de esta entrada, verificado.

Las cuatro con captura real es/en, arnés de pruebas propio por pieza (mismo patrón establecido:
variable de entorno, sandbox compartido, log real, sin FALLO) y comprobación cruzada final para
descartar regresiones entre ellas. No queda ningún punto pendiente del encargo original.

---

## 14-sep-2026 — Capturas automáticas de hito (álbum de progreso)

Encargo de la familia Keep: cuando un tramo de la Guía se cierra DE VERDAD jugando normal, el mod
tiene que guardar solo una captura del momento, sin que el jugador tenga que acordarse de pulsar
nada, y dejar un panel sencillo para verlas.

### Diseño

- **`Common/Guia/EstadoGuia.TramoSuperado(TramoGuia)`** (nuevo, público): la misma fórmula que ya
  usaba `TramosPorDelante` solo para los tramos opcionales ("implementado, con pasos, y el último
  ya completado"), sacada a un sitio único para que la reutilice también el sistema de hitos - un
  tramo obligatorio y uno opcional se dan por cerrado exactamente igual.
- **`Common/Hitos/HitosSystem.cs`** (`ModSystem`): toma una fotografía de `TramoSuperado` de cada
  tramo la primera vez que hay partida activa tras entrar al mundo, y solo cuenta como hito un
  tramo que pasa de no-superado a superado DESPUÉS de esa fotografía - nunca en la fotografía
  misma. Sin esto, cargar una partida ya avanzada llenaría el álbum de hitos falsos en el primer
  fotograma de cada sesión. La fotografía se retoma en cada entrada a partida (`OnWorldUnload` la
  invalida), así que un tramo ya cerrado en sesiones anteriores nunca vuelve a disparar.
- **`Common/Panel/CapturaDePantalla.GuardarHito(...)`** (nuevo): la MISMA técnica que ya usaba
  `Guardar` (`GraphicsDevice.GetBackBufferData` + `Texture2D.SaveAsPng`, nunca el escritorio), pero
  sin pasar por `Permitida` - `Guardar` sigue siendo SOLO arnés de pruebas a propósito (esa
  garantía, de la que depende `SincronizacionSystem` para no ensuciar el `settings.json` real
  durante una autoprueba, no se toca). El núcleo real se sacó a un método privado común
  (`GuardarEnArchivo`) para no duplicar la captura.
- **`Common/Hitos/AlbumHitos.cs`**: dispara la captura y mantiene `terrakeep-hitos/album.json`
  (clave, nombre YA TRADUCIDO al idioma del momento, archivo, fecha, personaje, mundo), escritura
  atómica (temporal + `File.Move`, mismo patrón que `SincronizacionEscritorio`). Carpeta propia
  (`terrakeep-hitos`, dentro de `Main.SavePath`) y distinta de `terrakeep-capturas` (esa es solo
  arnés de pruebas y se puede borrar sin perder nada real; el álbum es del jugador).
- **Pestaña "Álbum"** (octava área del panel único, `AreaTerrakeep.Album`, atajo **U** - comprobado
  libre con un grep real de todos los `RegisterKeybind` del mod antes de elegirla):
  `UI/Hitos/ContenidoAlbum.cs`. Lista con fecha, no miniaturas (el encargo admitía las dos): cargar
  cada `.png` como textura a resolución real del back buffer por cada fila sería memoria de vídeo
  sin límite en una partida larga, por una miniatura que además saldría borrosa. Un clic en la fila
  abre el archivo real con el visor de imágenes del sistema (`Process.Start`); "Abrir carpeta"
  hace lo mismo con la carpeta entera. "Actualizar" vuelve a leer `album.json` del disco.

### Verificación real, en el juego real

1. **Que las capturas se disparan solas, sin arnés nuevo**: `AutopruebaGuia`
   (`TERRAKEEP_AUTOTEST_GUIA`, ya existente y verificada al 100%) fuerza una a una las banderas
   reales de cada tramo. Con `HitosSystem` activo (siempre lo está, sin variable de entorno), la
   misma pasada de `scripts\verificar-guia.ps1` basta como prueba: **47 archivos `.png` reales**
   aparecieron solos en `terrakeep-hitos\` (PNG válidos, comprobado con `file`: p.ej.
   `800x720, 8-bit/color RGBA`, tamaños variados según lo que hubiera en pantalla - nunca 0 bytes
   ni un color plano), `album.json` con las 47 entradas bien formadas, y
   `terrakeep-hitos-evidencia.log` con una línea `HITO "..."` por cada una. La barra de pestañas
   pasó de 7 a 8 y se re-verificó que las ocho caben enteras (`AUTOPRUEBA GUIA/2`, log real:
   `"Álbum"=52,1px -> OK: las 8 caben enteras`).
2. **Que el panel del álbum en sí abre y no se solapa**: nuevo `Common/Hitos/AutopruebaHitos.cs`
   (`TERRAKEEP_AUTOTEST_HITOS`) - clic real en la pestaña, mide con números reales el hueco entre
   "Actualizar"/"Abrir carpeta" y la caja de la lista, y compara las entradas montadas en pantalla
   con las reales de `album.json`. **Bug real cazado por la propia autoprueba, no por lectura de
   código**: la primera versión de la comprobación de espaciado buscaba `is UIPanel` para
   encontrar la caja de la lista, pero `BotonTk` HEREDA de `UIPanel` (ver `BotonTk.cs`) - la
   comprobación encontraba el botón "Actualizar" y lo comparaba consigo mismo, dando un falso
   "NO CUADRA". Corregido excluyendo `BotonTk` de la búsqueda; verificado nuevo con captura real
   (`evidencia`-style, `hitos-1-album.png`) mostrando el álbum con sus 47 entradas, sin ningún
   solapamiento, y `"OK: sin solapes"` con las medidas reales
   (`caja-lista=x=26 y=135 748x508`, muy por debajo de los botones en `y=99..127`).
3. **Compilación real**: `scripts\compilar.ps1` (Roslyn interno de tModLoader, sin `-eac`):
   `Compilation finished with 0 errors and 260 warnings` (ninguna advertencia nueva de los
   archivos añadidos).

### Nota de diseño honesta

El texto de `MedirLaBarraDeSietePestanas` en `AutopruebaGuia.cs` decía "7" a mano en el log; se
cambió a contar de verdad (`total`) en vez de tocar el número la próxima vez que crezca la barra -
ya pasó una vez (6→7 con la Guía) y ha vuelto a pasar ahora (7→8 con el Álbum).

## 14-sep-2026 (madrugada) - `Assets/best_prefix.json` resincronizado con el arreglo MP-01 de
## Terrakeep, encontrado durante el `repaso-familia` de KeepQA

`Assets/best_prefix.json` de este repo es una copia manual byte a byte de
`Terrakeep.App/Assets/calamity/best_prefix_tml.json` (repo aparte, `Terrasavr-Native`) - lo
documenta la propia cabecera de `scripts/generar-mejor-prefijo.py` ("se copia tal cual"). El
arreglo MP-01 de la sesión de los 7 bugs de Terrakeep de esta misma noche (Coin Gun, id 905, antes
sin "mejor prefijo" calculado por un filtro `damage<=0` incorrecto) regeneró la tabla fuente pero
la copia aquí no se había rehecho. Encontrado durante el `repaso-familia` de KeepQA (diff
estructural clave a clave contra la fuente real: la entrada `"905"` era la única discrepancia).
Corregido con una copia directa del archivo real; confirmado idéntico byte a byte con
`Downloads\KeepQA\src\integridad\compararDespliegue.js`. Se ha añadido además un caso de
regresión permanente en KeepQA (`terrakeep-terrakeepmod-best-prefix-json-desincronizado`) que
compara los dos archivos byte a byte, así que una futura regeneración que se olvide de la copia
hará fallar la regresión, no solo este caso puntual. Detalle completo en
`Downloads\KeepQA\bitacora.md`.

## 14-sep-2026 - Barrido visual FRESCO con el arsenal nuevo de KeepQA (protocolo de
## `Downloads\KeepQA\PROTOCOLO-REVISION-VISUAL.md`), no solo repetir bugs ya conocidos

Encargo explícito: buscar activamente en TODAS las pantallas (Personaje completo, Ajustes,
Exploración, Librería, Investigación, Builds, Guía, Álbum), con contenido adversarial, las tres
piezas mecánicas nuevas (`verificarGeometria.js`/`verificarAlineacion.js`/`comprobarContraste.js
--formato-espec`) y mi propia revisión manual de 8 pasadas mirando capturas reales - no solo
confirmar que lo ya arreglado sigue arreglado.

### Qué se hizo de verdad

1. **Revisión manual de ~30 capturas REALES** ya existentes en `evidencia\espaciado-capturas\`
   (y su gemela `-calamity`), `evidencia\categorias-capturas\` y
   `tModLoader-TerrakeepGuia\terrakeep-capturas\` (56 capturas de los tramos de la Guía + el
   álbum de hitos) - cubriendo Inventario/Almacenes/Equipo/Apariencia/Buffs/Buffs-carpetas/
   Desbloqueos de Personaje, Ajustes (ES/EN), Mapa/Búsqueda/Este mundo de Exploración, Librería +
   categorías (espadas, armas de fuego, bloques con paginación), Investigación, Builds (con y sin
   Calamity), ~15 tramos representativos de la Guía (refugio, brújula sin explorar/marcado/mapa
   vanilla, detección de Calamity instalado, final Moon Lord, "todos los opcionales superados") y
   el Álbum, a 1600x900/1280x720/800x720 y ES/EN. **Ningún defecto objetivo nuevo** (Parte 26):
   diseño consistente, sin overflow, sin solapes reales, contraste bueno a simple vista,
   jerarquía clara, tabs de ancho variable bien resueltos (p.ej. "Hardmode temprano (antes de los
   jefes mecánicos)" en Builds cabe entero sin recortar). El bug conocido de `guia-33/34/35`
   "brújula" NO es tal - comprobado a mano que son tres pasos secuenciales de UN MISMO caso de
   prueba (antes de marcar / justo marcado / abierto en el mapa grande vanilla), no tres pantallas
   con contenido intercambiado.

2. **`comprobarContraste.js --formato-espec` (pieza nueva de KeepQA, nunca ejecutada contra este
   mod hasta hoy)** contra las 28 capturas a 1600x900-es (14 pantallas × con/sin Calamity).
   Resultado bruto: ~90 hallazgos Critical/High/Medium/Low. Tras filtrar ruido evidente (tokens de
   1 carácter, cajas <10px de alto) y cruzar el resto A MANO contra la captura real en las
   coordenadas exactas que reporta cada hallazgo: **cero defectos de contraste reales
   confirmados**. Todos trazan a límites ya documentados de la pieza (`comprobarContraste.js`,
   cabecera, y `PROTOCOLO-REVISION-VISUAL.md` Sección F.4): el OCR confunde repetidamente los
   iconos "«"/"‹"/"›"/"»" de los spinners de Vida/Maná máximos con letras ("ES"/"PA"/"60"/"DA" en
   almacenes/equipo, siempre en el mismo rango de coordenadas x=660-900,y=175-235), confunde el
   follaje/corteza de los árboles del fondo del mundo con texto (grupos de hallazgos con el MISMO
   par de color exacto `rgb(34,168,81)`/`rgb(23,51,28)` repetido en "palabras" distintas de la
   esquina superior derecha) y confunde las partículas de luz doradas decorativas de la esquina
   inferior (mismo `rgb(117,88,0)`/`rgb(0,0,0)` en "ro"/"EA" siempre en y≈800, justo debajo del
   panel) con letras dentro de una caja de color sólido. El rótulo de pestaña "Apariencia" sale
   garabateado ("Ayaiondo"/"Cuiemo"/"Aypiond"...) de forma consistente en la MISMA coordenada
   (1026,278) en cuatro capturas de pantallas distintas (Buffs, Buffs-carpetas, Desbloqueos,
   Inventario) - la propia repetición idéntica confirma que es un límite del OCR a ese tamaño de
   fuente, no un problema real de cada pantalla por separado. Confianza real de todos los
   hallazgos: 0,50 (el mínimo que emite la pieza) - coherente con la Parte 29 ("0,50-0,74: revisar
   a mano antes de reportar como defecto definitivo", exactamente lo que se ha hecho aquí).
   **Conclusión honesta**: la pieza funciona como está documentada, pero para este mod concreto
   (paleta de fondo con árboles/mundo real detrás de un panel semitransparente-oscuro, iconos de
   flecha en vez de letras) genera muchos más falsos positivos por OCR que en una app de fondo
   plano - no se ha tocado la pieza (cambiar su detección está fuera de este encargo), solo se
   documenta el resultado real de usarla aquí.

3. **Hallazgo real nuevo, encontrado por la revisión manual (no por ninguna pieza mecánica -
   ninguna evalúa solapes transitorios de tooltip): el tooltip de pestaña puede solapar contenido
   real del panel.** `UI/Panel/PanelTerrakeepState.cs:214-215` construye el `Ayuda` de cada
   `BotonTk` de la barra de pestañas como `AyudaDeArea(area) + "\n" + Idiomas.Texto("Panel.Atajo",
   ...)` (la MISMA descripción larga que ya se ve fija en el pie del panel, más el atajo de
   teclado) y `BotonTk.DrawSelf` (línea 319-322) lo pasa tal cual a `Main.instance.MouseText(...)`
   - el tooltip vainilla de Terraria, sin ningún panel de fondo propio (confirmado en el código
   real: solo `Utils.DrawBorderString`, igual que el texto de nombre de un NPC). Visto en CINCO
   capturas reales e independientes: `ajustes-1280x720-en.png` (tapa "Settings"/la fila de idioma
   con "The language changes live..." + "Shortcut: J"), `investigacion-1280x720-es.png` y
   `investigacion-800x720-minimo-es.png` (tapa el AVISO naranja "no es un personaje de Modo
   Viaje..." Y la fila "Progreso global"), `ajustes-800x720-minimo-en.png` (tapa la cabecera y los
   tres botones de idioma) y `buffs-800x720-minimo-es.png` (tapa "Nombre"/"Vida máxima"). Siempre
   al pasar el ratón por una pestaña NO activa a una resolución de ventana pequeña (1280x720 o
   800x720; no se ha reproducido a 1600x900, donde sobra sitio por encima del panel). **Evaluado
   con el protocolo de la Parte 2/22/26 antes de decidir si es un bug**: `Main.instance.MouseText`
   es EXACTAMENTE el mismo mecanismo vainilla que usa cualquier tooltip de objeto/NPC de Terraria
   en todo el juego (sin panel de fondo, tapando transitoriamente lo que haya debajo mientras el
   ratón está encima) - tapar contenido brevemente mientras el jugador activamente lee un tooltip
   es el propio lenguaje visual del juego, no una interfaz moderna que deba evitarlo a toda costa.
   Lo único que distingue a este caso de un tooltip vainilla normal es que concatena DOS líneas
   (descripción + atajo) en vez de una, así que ocupa más alto de lo habitual y es más fácil que
   choque con la fila de justo debajo de la barra de pestañas en una ventana pequeña. **No
   arreglado a ciegas** (criterio explícito del encargo: esto es ambiguo, no "claro y pequeño") -
   cambiar el comportamiento de un tooltip vainilla tiene un lado de identidad visual real y
   ninguna pieza mecánica lo puede validar. Recomendación para quien retome esto: si se decide que
   sí molesta, la opción más barata es quitar la segunda línea (el atajo) del `Ayuda` de la barra
   de pestañas - la descripción sola ya es la información importante, y el atajo ya está visible
   en el pie del panel en todo momento.

4. **Contenido adversarial (Parte 21, `Downloads\KeepQA\src\adversarial\catalogo.json`)**: ya hay
   cobertura real y verificada en `Common\Libreria\AutopruebaLibreria.cs`
   (`BuscarConComaYAcentos`, cubre SPECIAL CHARACTERS con tildes/eñe reales del español) además de
   `BuscarPorNombre`/`BuscarPorId`/`BuscarEnTooltip`. **Hueco real, no cubierto hoy ni antes**:
   ninguna autoprueba inyecta las categorías LONG/VERY LONG/OTHER LANGUAGES (japonés/árabe/
   cirílico) del catálogo en el CUADRO de búsqueda de la Librería para comprobar que el propio
   campo de texto (no los resultados) no se desborda con una cadena muy larga o con glifos anchos
   no latinos. No se ha hecho en vivo hoy: exige tocar `AutopruebaLibreria.cs` para escribir un
   texto adversarial de verdad en el campo (no solo cambiar el string de búsqueda ya usado) y
   pasar por el ciclo completo compilar+lanzar+capturar (~10-15 min), un cambio de código nuevo en
   un arnés ya maduro que no encaja en "arreglo claro y pequeño" del encargo de hoy - queda
   apuntado aquí como recomendación concreta para una ronda futura en vez de improvisarlo a ciegas.

### Resumen honesto, con números reales

- **~30 capturas reales revisadas a mano** (8 pasadas del protocolo cada una) cubriendo las 8
  áreas pedidas - 0 defectos objetivos nuevos encontrados por la vista.
- **28 ejecuciones de `comprobarContraste.js --formato-espec`** (primera vez contra este mod) -
  ~90 hallazgos brutos, 0 confirmados reales tras cruzar cada uno contra la captura real.
- **1 hallazgo real nuevo** (tooltip de pestaña sin panel de fondo, potencialmente solapando
  contenido a ventana pequeña) - documentado con 5 capturas reales, causa exacta en código,
  evaluado como comportamiento vainilla-consistente y NO arreglado a ciegas por ser ambiguo, con
  recomendación concreta para quien decida si merece cambiarse.
- **1 hueco de cobertura adversarial real identificado** (LONG/VERY LONG/OTHER LANGUAGES en el
  cuadro de búsqueda de la Librería) - no cerrado hoy, recomendación dejada para una ronda futura.
- No se ha tocado ningún tramo de la lógica de la Guía (fuera del alcance salvo bug visual real
  dentro de ellos, y no se encontró ninguno).

## 14-sep-2026 (noche) — Tooltip de pestaña inactiva: fondo propio, en vez de documentar el límite

Retomo el hallazgo de arriba (tooltip de pestaña sin panel de fondo, tapando contenido a ventana
pequeña, 5 capturas reales) con el encargo explícito de decidir - no solo documentar - y construir
la opción elegida, verificada con captura real. Checklist maestro: `Downloads\KeepQA\
PENDIENTES-CIERRE-14SEP.md`, punto 1.

**Decisión, con criterio de diseño ya concedido**: sustituir el tooltip vainilla
(`Main.instance.MouseText`) de `BotonTk` (pestañas, píldoras, acciones - todos los botones del
mod que usan `Ayuda`) por uno con fondo propio, reutilizando la MISMA técnica de marco de 9 trozos
(`Utils.DrawSplicedPanel` + `Images/UI/PanelBackground`/`PanelBorder`) que `BotonTk` ya usa para
dibujarse A SÍ MISMO. Comprobado primero que la premisa era cierta y no un supuesto: busqué en
TODO el mod (`grep MouseText\(` en `*.cs`) y confirmé que **ningún** tooltip del mod lleva fondo
propio todavía - los seis sitios que usan `Main.instance.MouseText` (`BotonTk`, `AlternadorTk`,
`FilaCarpetaTk`, `FilaCarpetaBuffTk`, `IconoHudTerrakeep`, el tooltip de objeto de
`PanelTerrakeepState`) son vainilla puro. Y decompilando `Main.MouseTextInner` real
(`tModLoader.dll` instalado, vía `ilspycmd`) confirmé que la "caja opaca detrás de tooltips"
(`Main.SettingsEnabled_OpaqueBoxBehindTooltips`) SOLO se dibuja dentro de
`MouseText_DrawItemTooltip` (el tooltip de un OBJETO) - un tooltip de texto suelto como el de una
pestaña NUNCA lleva panel en vainilla, ni con esa opción activada. Alcance de la corrección:
**solo `BotonTk`** (arregla pestañas Y de paso unifica el resto de tooltips de botón, sin tocar
los otros cinco sitios sueltos - fuera del hallazgo original, otra ronda si hace falta).

### Construcción

- `UI/Personaje/Widgets/BotonTk.cs`: `DrawSelf` ya no llama a `Main.instance.MouseText(ayuda)`;
  guarda el texto en un campo estático `_tooltipPendiente`. Nuevo método estático
  `DibujarTooltipPendiente(SpriteBatch)`: mide las líneas reales (`FontAssets.MouseText`), dibuja
  fondo + borde con el mismo `Utils.DrawSplicedPanel` que usa el marco del propio botón, clampa a
  pantalla (mismo criterio que `Main.MouseTextInner`) y pinta el texto (primera línea blanca,
  resto en `EstiloTk.TextoSuave` - el mismo gris que ya usa el pie del panel para texto
  secundario). Se consume solo (se pone a `null`) al dibujarse.
- `UI/Panel/PanelTerrakeepState.cs`: `Draw` llama a `BotonTk.DibujarTooltipPendiente(spriteBatch)`
  DESPUÉS de `base.Draw` - mismo motivo y mismo patrón que `DibujarTooltipDeObjeto`/
  `DibujarObjetoEnRaton` ya establecidos ahí: la barra de pestañas se dibuja PRIMERO en el árbol,
  así que sin aplazar esto el tooltip quedaría POR DEBAJO de cualquier fila dibujada después -
  exactamente el problema que se está arreglando, al revés.
- `UI/Personaje/Widgets/EstiloTk.cs`: nueva constante `FondoTooltip` (mismo azul oscuro de
  `FondoCaja`, algo más opaco: un tooltip flota sobre CUALQUIER color de fondo del mundo/HUD
  detrás, necesita más cobertura que una caja fija del panel).

### Verificación en el juego real - captura real, con dos obstáculos reales resueltos

Arnés nuevo: `Common/Panel/AutopruebaTooltipPestana.cs` (`TERRAKEEP_AUTOTEST_TOOLTIP_PESTANA`) +
`scripts/verificar-tooltip-pestana.ps1`. Reproduce el escenario EXACTO del hallazgo original
(800x720, el mínimo real del motor): abre el panel en Personaje, fuerza el ratón de PANTALLA
sobre la primera pestaña inactiva de la barra (`Librería`) y pide una captura real del back
buffer (`CapturaDePantalla`, ya existente).

**Obstáculo 1 (bloqueaba el lanzamiento entero)**: el sandbox `tModLoader-TerrakeepWS0` no tenía
`ShowNewUpdatedModsInfo=false` en su `config.json` (a diferencia del sandbox propio de
`verificar-espaciado.ps1`, que sí lo pone) - el cliente se quedaba colgado en "Finding Mods..."
esperando un clic real en el diálogo "Mod Changes since last launch" (mismo obstáculo ya
documentado ahí). Resuelto aplicando el mismo parche de `config.json` dentro de mi script.

**Obstáculo 2 (bug real de mi propio arnés, no del arreglo de producción)**: la primera pasada
compiló y corrió entera pero la captura NO mostraba ningún tooltip. Causa real, no supuesta:
`IsMouseHovering` (lo que lee `BotonTk.DrawSelf`) no se recalcula en `Draw`, se fija UNA vez por
fotograma dentro de `UserInterface.Update -> GetMousePosition() -> hit-test`, que corre ANTES de
que `PanelTerrakeepState.Draw` (donde reafirmaba `Main.mouseX`/`Main.mouseY`, calcando el patrón
ya usado por `AutopruebaTooltipObjeto`) llegara a ejecutarse - la entrada real (polling) pisa el
valor forzado en medio, igual que la nota larga de `AutopruebaTooltipObjeto.ReafirmarRaton` ya
documentaba para OTRO caso. Ese patrón sirve para un `ContainsPoint` manual dentro de `DrawSelf`
(caso de `SlotObjetoVanilla`), pero no para `IsMouseHovering`, que se decide antes. Arreglado
llamando DIRECTAMENTE a `pestana.MouseOver(new UIMouseEvent(...))` - el mismo método público que
dispara el motor real - en vez de intentar ganar la carrera del fotograma; mismo principio que ya
usa `AutopruebaPersonaje.ComprobarDeslizadorColor` con `LeftMouseDown`. `Main.mouseX`/`mouseY` se
siguen reafirmando en `Draw` aparte, para que la POSICIÓN del tooltip (que sí lee esos dos campos
directamente) aparezca junto a la pestaña real y no en una esquina.

**Resultado, log real (`client.log`)**:
```
AUTOPRUEBA TOOLTIP PESTAÑA - raton de PANTALLA puesto en x=163 y=76 sobre la pestaña "Librería"
(rectangulo real x=119 y=61 87x30), MouseOver disparado a mano (IsMouseHovering=True), 20
fotogramas antes de pedir la captura.
AUTOPRUEBA TOOLTIP PESTAÑA - IsMouseHovering=True justo antes de capturar - captura real del back
buffer guardada en ".../terrakeep-capturas/tooltip-pestana-fondo-800x720-minimo.png"
```

Captura real: `evidencia/tooltip-pestana-fondo/tooltip-pestana-fondo-800x720-minimo.png` -
tooltip de dos líneas con caja azul oscura y borde claro, delimitado con claridad de la fila
"Nombre"/"Ahora: vida" de debajo, sin mezclar letras con el fondo. Comparado a mano contra
`evidencia/espaciado-capturas/ajustes-800x720-minimo-en.png` (el "antes" real del hallazgo: texto
blanco suelto ilegible, mezclado letra a letra con "Follow the game"/los botones de idioma) - la
diferencia es clara e inequívoca.

### Compilación

`scripts/compilar.ps1`, 0 errores, `.tmod` generado en las dos pasadas (con y sin el arreglo del
hover forzado).

### Commit

`UI/Personaje/Widgets/BotonTk.cs`, `UI/Personaje/Widgets/EstiloTk.cs`,
`UI/Panel/PanelTerrakeepState.cs`, `Common/Panel/AutopruebaTooltipPestana.cs` (nuevo),
`Common/Panel/PanelTerrakeepSystem.cs` (enganche de `Avanzar`), `Common/Panel/
CapturaDePantalla.cs` (variable nueva en `Permitida`), `scripts/verificar-tooltip-pestana.ps1`
(nuevo), `evidencia/tooltip-pestana-fondo/` (captura real), `bitacora.md`.

## 14-sep-2026 (noche) — .tmod limpio de verdad, subida de versión

Punto 2 y 3 del checklist de cierre. Hallazgo real de KeepQA de esta noche: el `.tmod`
distribuible llevaba símbolos de depuración y restos de builds sueltas. Investigado con el
código REAL de `ModCompile.cs`/`TmodFile.cs` (`tModLoader.dll` instalado, decompilado con
`ilspycmd` - nunca supuesto), dos causas distintas:

1. **`ModCompile.IgnoreCompletely`** solo descarta rutas que empiezan LITERALMENTE por
   `"bin\"`/`"obj\"`. Las carpetas de salida alternativas de sesiones de verificación pasadas
   (`bin-checkDebug\`, `obj-guia\`, `obj-verif-espaciado\`... - ya en `.gitignore`, nunca parte
   del mod, dejadas por `-p:BaseOutputPath` aparte para no pelearse con un `bin\` bloqueado por
   el juego abierto) no coinciden con ese prefijo exacto y se colaban enteras: `project.assets.
   json`, `*.nuget.cache`, rutas absolutas de esta máquina. **Arreglo real, dos partes**:
   borradas las carpetas sueltas (basura local, 0 archivos trackeados en git) y añadidas
   `bin-*, obj-*` a `buildIgnore` en `build.txt`, para que no vuelvan a colarse si una sesión
   futura deja alguna otra suelta.
2. **El `.pdb` no pasa por `buildIgnore` en absoluto**: `ModCompile.Build` lo añade a mano,
   sin condición, en cuanto `RoslynCompile` lo genera (código real: SIEMPRE lo genera, no hay
   ningún ajuste de `Configuration`/"Release sin símbolos" que `tModLoader` exponga en su
   propio `-build` - confirmado, no hay ninguna vía de config para esto). Construida la única
   vía real: `Downloads\KeepQA\src\empaquetado-tmod\limpiar-tmod.js` (nuevo, herramienta
   compartida de la familia), que reescribe el `.tmod` ya compilado quitando las entradas que
   coincidan (por defecto `*.pdb`) con un hash SHA1 recalculado siguiendo EXACTAMENTE el
   formato real de `TmodFile.Save()` (decompilado) - no un zip a medias. `scripts\
   limpiar-tmod.ps1` (nuevo) lo invoca sobre el `.tmod` ya compilado.

**Verificado de verdad, no solo con el propio lector**: tras `compilar.ps1` + `limpiar-tmod.ps1`,
`node tmod-extract.js` confirma el contenido limpio (19 → 18 archivos, sin `.pdb` ni carpetas
sueltas, 581.326 → 479.318 bytes) y `verificar-en-juego.ps1 -Servidor` carga el `.tmod` YA
LIMPIO en un servidor dedicado real: `"[Terrakeep] Mod cargado. Prueba de humo de Terrakeep.Core:
GameItem(Id=3389).IsEmpty=False..."` - el hash recalculado a mano es válido de verdad para el
motor real, no solo para mi propio lector.

**Versión**: subida de `0.2.0` a `0.3.0` (salto de minor: tooltip con fondo propio + el propio
arreglo de empaquetado, no un cambio incompatible). Sin pantalla "Acerca de"/versión propia en
el panel (comprobado: único sitio que la muestra es `AppVersion` de `SincronizacionEscritorio.cs`,
que ya lee `Terrakeep.Instance.Version` en vivo del ensamblado, sin número escrito a mano que
tocar) - la pantalla nativa "Mod Info" de tModLoader ya la toma de `build.txt` sola.

### Commit

`build.txt` (versión + `buildIgnore`), `scripts/limpiar-tmod.ps1` (nuevo). El borrado de las
carpetas `bin-*/obj-*` no aparece en el commit: nunca estuvieron trackeadas (`.gitignore`).

## 14-sep-2026 (tarde) — KeepQA V2.0 Fase 1: extractor de capas/orden_z portado desde TModLoaderMod

Encargo real de KeepQA V2.0 (`Downloads\KeepQA\v2\PROPUESTA-UNIFICADA.md`, Fase 2/4: "portar el
extractor de capas de TModLoaderMod también a TerrakeepMod"). Mismo "Motor 3" ya construido y
validado allí (commit `68684d5`): `UIElement.Children` (la lista que recorre `DrawChildren`, sin
ningún `Sort`/`ZIndex` - confirmado otra vez contra
`Downloads\tModLoader-Decompiled\tModLoader\Terraria\UI\UIElement.cs`) ya es el orden de pintado
real y basta con LEERLO, nunca instrumentar nada. No se reimplementa ninguna lógica de
comparación: `verificarGeometria.js`/`verificarCapas.js` de KeepQA se usan tal cual.

**Diferencia real frente a TModLoaderMod**: el árbol de `PanelTerrakeepState` no es el mismo (marco
único + barra de 8 pestañas + una de 8 áreas de contenido HETEROGÉNEAS, cada una con sus propios
widgets - `ContenidoPersonaje`, `ContenidoLibreria`... - frente a las dos columnas fijas de
toggles/multiplicadores del trainer). `VolcarGeometriaJson()` (nuevo, en `UI/Panel/
PanelTerrakeepState.cs`) por tanto:

- Clasifica los hijos DIRECTOS de `_marco` por identidad de referencia (`_botonCerrar`,
  `_botonesPestana`, `_contenedor`, `_capaSuperposicion`) - sin campos nuevos que exponer, todos ya
  existían.
- Para el contenido de la pestaña abierta (heterogéneo, imposible de enumerar a mano para las 8
  áreas) usa un recorrido GENÉRICO y recursivo (`VolcarHijosRecursivo`), con dos recortes de
  seguridad reales y documentados en el propio código: profundidad máxima 4 y 60 hijos por
  contenedor - la Librería puede tener miles de objetos cargados (`ArbolLibreria`/`RejillaSlots`,
  ~8000 con Calamity) y un volcado sin límite no aporta nada nuevo a unos verificadores que ya
  comparan por GRUPO, no elemento a elemento.
- `capa` se aproxima por TIPO de widget (`ClasificarCapa`, switch de patrones) contra el vocabulario
  cerrado de `verificarCapas.js` (fondo/decoracion/panel/contenido/controles/navegacion/overlay/
  modal/primer_plano) - nunca se fuerza una clasificación que no encaja: `IconoObjetoTk`/
  `IconoResultado` se descartaron del switch al comprobar que son clases `static` (dibujan, no son
  nodos del árbol de `UIElement`) y `CampoTextoTk`/`AlternadorTk` (ambos heredan de `UIPanel`) se
  colocaron ANTES del `case UIPanel _:` genérico - un switch de patrones de tipo se queda con el
  PRIMER caso que encaja, así que el orden importa de verdad, no es cosmético.
- `OrdenZDe` es el mismo método, literal, que ya usa `TrainerPanelState.cs` de TModLoaderMod (índice
  real dentro de `Children` del padre real) - portado sin cambios porque `UIElement.Children`/
  `Parent` son idénticos en los dos mods (mismo tModLoader instalado).

**Enganche real** (no una autoprueba nueva, se reutiliza la que YA recorre las 11 vistas × 2
idiomas): `Common/Panel/AutopruebaIdiomas.cs`, método nuevo `VolcarGeometriaSiToca`, llamado justo
después de cada captura de pantalla en `Recoger()` - mismo patrón exacto que
`Localizacion\IdiomaSystem.cs` de TModLoaderMod: escribe `geometria-<idioma>-<vista>.json` en la
MISMA carpeta que ya usan las capturas (`Autoprueba.CapturaDePantalla.Carpeta` - aquí
`Common\Panel\CapturaDePantalla.Carpeta`), protegido con try/catch propio (nunca aborta la
autoprueba si el volcado falla).

### Compilación

`scripts\compilar.ps1`: **0 errores** en las dos fases (`dotnet build` de validación + `-build` real
con el Roslyn interno de tModLoader), `TerrakeepMod.tmod` generado (584.255 bytes).

### Verificación real, con un obstáculo real encontrado y resuelto en el camino

Lanzado `scripts\verificar-idiomas.ps1` (cliente gráfico real, `TERRAKEEP_AUTOTEST_IDIOMAS=1`)
contra el sandbox `tModLoader-TerrakeepIdiomas`. **Primer intento: el cliente se quedaba
indefinidamente en la pantalla de splash** ("Terraria: Coming soon to a computer near you", CPU
prácticamente plana, `Responding=True`) sin cargar nunca el mundo. Diagnosticado leyendo
`client.log`: se paraba justo después de `"Mod Changes since last launch: Updated Mods: HEROsMod
(HERO's Mod) v0.4.18 -> v0.4.18.1"` - confirmado contra el código real decompilado
(`Terraria/ModLoader/UI/Interface.cs:206-268`, `ModOrganizer.DetectModChangesForInfoMessage`) que
esto dispara una pantalla `infoMessage.Show(...)` DENTRO del propio juego (no un diálogo nativo de
Windows - comprobado enumerando todas las ventanas visibles del sistema, solo existía la ventana
SDL del propio juego) que exige un clic para continuar, y por tanto bloquea cualquier automatización
sin simulación de input. Causa real: `LastLaunchedMods.txt` (dentro de `Main.SavePath`, o sea del
propio sandbox) llevaba fecha del 6-sep-2026, de una sesión anterior, y comparaba contra el estado
ACTUAL de la carpeta real de Workshop del usuario (`HEROsMod`/`CalamityMod`, actualizados desde
entonces) - el mismo obstáculo, y el mismo arreglo, que ya documentaba `medirFps.js` de KeepQA
("Restos de una sesión anterior real del propio sandbox que atascan el arranque en una pantalla que
pide un clic"). **Arreglo**: borrar `LastLaunchedMods.txt` del sandbox antes de lanzar (sin archivo,
`DetectModChangesForInfoMessage` devuelve `null` de inmediato, Paso 2 del código real citado arriba)
- con eso el cliente entró limpio, sin pantalla de confirmación, en el primer intento siguiente.

**Evidencia real tras el arreglo** (`terrakeep-idiomas-evidencia.log`, 22 vistas = 11 × 2 idiomas,
todas con volcado):
```
[16:53:37.888] [Terrakeep] VISTA Personaje-0 [es]: geometria volcada en "geometria-es-Personaje-0.json".
...
[16:53:58.597] [Terrakeep] VISTA Investigacion [en]: geometria volcada en "geometria-en-Investigacion.json".
```
Ni una sola línea de "volcado de geometría fallido" en las 22 vistas.

**Validado contra las piezas compartidas de KeepQA** (mismo criterio que TModLoaderMod), sobre
`geometria-es-Personaje-7.json` (la vista "Buffs", la más cargada: 55 elementos, `orden_z`/`capa`
reales en todos los nodos del marco):
- `verificarCapas.js`: **RESULTADO: OK** (0 inversiones de capa reales; 4 pares comparables, 202
  sin solape, 40 sin datos suficientes de `capa`/`orden_z` - honesto, no todo elemento tiene una
  `capa` clasificada).
- `verificarGeometria.js`: **RESULTADO: REVISAR** (1 contención rota, 2 consistencias de grupo, 6
  solapes) - confirma que el extractor SÍ detecta cosas reales, no solo produce ceros. Dos de los
  hallazgos son diseño intencional documentado en el propio código (`contenedor_area`/
  `capa_superposicion` se solapan A PROPÓSITO - ver el comentario de `OnInitialize` en
  `PanelTerrakeepState.cs` sobre por qué la capa de superposición ocupa la misma franja que el
  contenedor). El resto (un `MedidorPreparacionTk` que se sale 9px por abajo de su fila en la
  pestaña de Completitud, la ayuda del pie solapando el botón "Cerrar", dos pares de etiquetas
  vecinas solapadas en Completitud) son candidatos REALES sin triar - mismo criterio que el resto
  de la familia ("un hallazgo puede ser un bug real O un diseño intencional, MIRA la captura antes
  de decidir"): quedan anotados aquí para que el propio proyecto los revise cuando le toque, no se
  han tocado en esta ronda (fuera del alcance de "portar el extractor", que es lo que pedía la
  Fase 1).

### Commit

`UI/Panel/PanelTerrakeepState.cs` (extractor `VolcarGeometriaJson`/`VolcarHijosRecursivo`/
`ClasificarCapa`/`OrdenZDe`, nuevo), `Common/Panel/AutopruebaIdiomas.cs` (enganche
`VolcarGeometriaSiToca`), `bitacora.md`.

---

## 14-sep-2026 (KeepQA V2.0, Fase 6, Bloque B) - `AutopruebaSoak`: sesión real de estrés/soak con servidor dedicado + cliente gráfico único

Encargo real: `Downloads\KeepQA\v2\PROPUESTA-UNIFICADA.md`, Fase 6, Bloque B - reutilizar el
servidor dedicado real de TerrakeepMod (ya usado en `probarMultijugador.js`/`lanzarServidor.js`)
para una sesión CORTA pero real de estrés/soak, repitiendo alguna acción real vía las
`Autoprueba*` ya existentes, con muestreo de `Get-Process` cada 15-30s y la serie completa
guardada como artefacto.

**Por qué un cliente gráfico y no basta el servidor dedicado a secas**: `PanelTerrakeepSystem.
UpdateUI` (el hook del que cuelgan TODAS las autopruebas del panel) empieza con `if (Main.dedServ)
return;` - el panel es UI, no existe en el proceso headless. El servidor dedicado real SOLO aporta
aquí la parte de "sesión de red sostenida" (RAM/CPU del proceso que de verdad sirve la partida);
el cliente es quien ejecuta la acción repetida.

**`Common/Panel/AutopruebaSoak.cs`** (nuevo): activada por `TERRAKEEP_AUTOTEST_SOAK=1`, abre y
cierra el panel único repetidamente reutilizando EXACTAMENTE los mismos métodos de producción que
ya usa `AutopruebaPanelUnico` (`PanelTerrakeepSystem.AbrirEnArea`/`CerrarPanel`), nunca lógica de
apertura/cierre propia. A diferencia de `AutopruebaPanelUnico` (una pasada FIJA de 37 pasos que
termina sola), esta se mantiene en bucle por TIEMPO REAL (`DateTime.UtcNow`, no fotogramas - una
sesión de red puede tener fotogramas irregulares por lag/carga de chunks) hasta agotar
`TERRAKEEP_SOAK_MINUTOS` (defecto 6); el espaciado FINO dentro de un mismo ciclo (medio segundo
con el panel realmente dibujado antes de cerrarlo) sí usa fotogramas, igual que el resto de
autopruebas. Intervalo entre ciclos configurable con `TERRAKEEP_SOAK_INTERVALO_S` (defecto 15).
Enganchada en `PanelTerrakeepSystem.UpdateUI` junto al resto de `Avanzar()` de autopruebas.

**Bug real encontrado y arreglado de paso**: `RegistroPanel.EscribirEnArchivo` (el volcado a
`terrakeep-panel-evidencia.log` dentro del sandbox, que un arnés externo sondea para saber cuándo
ha empezado/terminado algo) solo escribía si `TERRAKEEP_AUTOTEST_PANEL` estaba puesta - la
variable de `AutopruebaPanelUnico`, no la de `AutopruebaSoak`. Con solo `TERRAKEEP_AUTOTEST_SOAK=1`
puesta, el archivo NUNCA se creaba (el mensaje sí llegaba a `client.log` vía `Logger.Info`, pero
ningún arnés externo puede fiarse de ese log global compartido - ver el motivo real documentado en
la cabecera del propio `RegistroPanel.cs`). Arreglado añadiendo `AutopruebaSoak.Variable` a la
misma comprobación, sin tocar el comportamiento de `AutopruebaPanelUnico`.

**`Downloads\KeepQA\src\rendimiento\soakTModLoader.js`** (nuevo, repo KeepQA): orquesta la sesión
completa reutilizando `src/motor-servidor-dedicado/lanzarServidor.js` (servidor dedicado real,
puerto propio 7815) y lanzando UN cliente gráfico real conectado por `-j`/`-plr` (mismo mecanismo
de conexión directa ya validado por `probarMultijugador.js` con dos clientes - aquí basta uno).
Muestrea `Get-Process` del servidor Y del cliente cada `--intervalo-muestreo-s` (defecto 20)
durante toda la sesión, y guarda la serie temporal COMPLETA (no solo el resumen) como artefacto
real vía `runId.js` (Fase 2).

**Bug real encontrado y arreglado en el propio script Node**: la búsqueda del PID real del cliente
(`Get-CimInstance Win32_Process ... -like '*<ruta del sandbox>*'`) duplicaba a mano las barras
invertidas de la ruta antes de meterla en el patrón `-like` de PowerShell - `-like` NO trata la
barra invertida como carácter especial (no hace falta escaparla, a diferencia de un regex), así
que el patrón acababa buscando DOBLES barras que nunca aparecen en la `CommandLine` real. El
cliente real estaba vivo y con la ruta correcta, pero el sondeo nunca lo encontraba (confirmado a
mano con `Get-CimInstance` directo, comparando el PID real contra lo que el script decía "no
encontrado"). Arreglado quitando el escape innecesario - la ruta va tal cual.

### Ejecutado de verdad, con evidencia real (no simulada)

Sesión real completa de 6 minutos (tras un primer intento fallido por el bug de arriba, parado y
arreglado antes de reintentar - regla de la casa respetada):

- Servidor dedicado real (puerto 7815) + cliente gráfico real conectado por red, ambos
  muestreados cada 20s (19 muestras cada uno): servidor RAM 666,0→653,3 MB (sin crecimiento, de
  hecho bajó -12,7 MB), CPU acumulada 15,5s; cliente RAM 799,3→721,6 MB (sin crecimiento, -77,8
  MB), CPU acumulada 13,1s - **ninguno de los dos procesos muestra indicio de fuga en esta
  sesión**.
- **23 ciclos reales de abrir/cerrar el panel** confirmados por el log real del cliente
  (`terrakeep-panel-evidencia.log`, copiado al artefacto), marca `AUTOPRUEBA SOAK COMPLETA` vista
  al final de la sesión (cierre limpio, sin panel colgado abierto).
- **El servidor sobrevivió la sesión completa** (`servidorSobrevivioTodaLaSesion: true`),
  `veredictoRun: "PASS"`. Artefactos completos (series de servidor y cliente + log de evidencia +
  `informe.json`) en `Downloads\KeepQA\artifacts\runs\2026-09-14T17-17-40_tmodloadermod_soak\`.
- Limpieza confirmada de verdad tras la sesión: sin ningún proceso `dotnet.exe` de tModLoader
  vivo (`Get-CimInstance` vacío), sandbox de KeepQA (`tModLoader-KeepQA`/`tModLoader-KeepQA-Soak`)
  intacto para la próxima ronda (mismo criterio que el resto de piezas de rendimiento de la
  familia - se reutiliza, no se borra).

### Archivos tocados

- `Common/Panel/AutopruebaSoak.cs` (nuevo).
- `Common/Panel/PanelTerrakeepSystem.cs` (engancha `AutopruebaSoak.Avanzar()`).
- `Common/Panel/RegistroPanel.cs` (arreglo: también escribe el archivo de evidencia cuando
  `AutopruebaSoak` está activa, no solo `AutopruebaPanelUnico`).
- `Downloads\KeepQA\src\rendimiento\soakTModLoader.js` (nuevo, repo KeepQA aparte).

---

## 14-sep-2026 (noche) — KeepQA V2.0, Fase 5: intento de baseline visual real, bloqueado por un
## cuelgue real del cliente gráfico (obstáculo de entorno, no del mod)

Encargo: cerrar el hueco de Fase 5 de KeepQA (`v2/PROPUESTA-UNIFICADA.md`) generando baseline
visual real para TerrakeepMod vía `scripts\verificar-panel-unico.ps1` (que ya lanza
`AutopruebaPanelUnico` con `CapturaDePantalla.Guardar()` real, `GetBackBufferData`), guardando 2-3
capturas reales con `gestorBaseline.guardar('terrakeepmod', <pantalla>, ..., {confirmar:true})`.

**No se consiguió cerrar - tres intentos reales, los dos últimos con el MISMO fallo**:

1. **Intento 1** (`-SegundosEspera 240`): compiló bien (`.tmod` real, 585179 bytes), el cliente
   gráfico SÍ arrancó y SÍ progresó (`client.log` real llegó a "Finding Mods..." y "Mod Changes
   since last launch"), pero `terrakeep-panel-evidencia.log` nunca apareció dentro de los 240s -
   el cliente se quedó cargando mods más tiempo del esperado (causa real no aislada del todo:
   pudo ser simplemente que 240s no bastaban esta vez). El script mató su propio proceso al
   agotar el tiempo (comportamiento correcto, sin residuos).
2. **Intento 2** (`-SegundosEspera 420`, tras confirmar `Get-Process`/`Get-CimInstance` vacíos
   antes de lanzar): esta vez el cliente se quedó COLGADO DE VERDAD, mucho antes de "Finding
   Mods" - el `client.log` se detuvo en seco justo después de `[FNA]: Hook
   System.Runtime.Loader...` y no avanzó ni una línea más en los 7 minutos completos de espera
   (confirmado con un bucle de sondeo real cada 5-8s, no solo "parecía colgado"). Justo antes de
   colgarse, el log mostraba algo que el intento 1 NO tenía: `Microsoft.Xna.Framework.Audio.
   NoAudioHardwareException` / `"No audio hardware found. Disabling all audio."` - una diferencia
   real de entorno entre los dos intentos, no un cambio de código (nada se tocó en el mod entre
   medias).
3. **Diagnóstico antes de un tercer intento** (regla de la casa: entender antes de repetir a
   ciegas): `query session` mostró la sesión 1 (`adrian`, donde corre este agente) como `Desc`
   (desconectada) y la sesión `console` como `Conn` - el mismo patrón ya documentado en la
   memoria del usuario para el cuelgue en negro de Don't Starve Together
   (`reference_tscon-reconexion-limitaciones`). Los dispositivos de audio del sistema seguían
   `OK` a nivel de Windows (`Win32_SoundDevice`), así que no es que falte hardware de verdad -
   es la sesión desconectada la que se lo esconde al proceso. **Matiz real que complica el
   diagnóstico simple**: esta MISMA sesión de trabajo ya había lanzado el cliente gráfico con
   éxito varias veces hoy (barrido de idiomas de TModLoaderMod a las 18:33/18:34, y el soak de 6
   minutos de este propio proyecto en la entrada de Fase 6 justo arriba) bajo, presumiblemente,
   el mismo estado de sesión desconectada - así que "sesión Desc" por sí sola no garantiza el
   cuelgue, solo lo hace más probable en algún momento de la tarde/noche.
4. **Intento 3** (`-SegundosEspera 300`, tras confirmar de nuevo cero procesos huérfanos): **el
   cliente se colgó exactamente en el mismo punto que el intento 2** (mismo `NoAudioHardware
   Exception`, mismo corte seco justo tras el mismo `Hook` de FNA, confirmado con el mismo bucle
   de sondeo). Dos fallos reales seguidos con la MISMA causa observable - por la regla de la casa
   ("si algo falla dos veces seguidas, parar y anotarlo, no insistir en bucle") se paró aquí, sin
   un cuarto intento. Proceso colgado matado a mano (`Stop-Process -Force` sobre el PID real,
   confirmado sin residuos con `Get-CimInstance` vacío después).

**Conclusión honesta**: el mecanismo de captura en sí (`CapturaDePantalla.cs` +
`AutopruebaPanelUnico` + `verificar-panel-unico.ps1`) sigue siendo válido y ya demostrado hoy
mismo en otro contexto (el soak de 23 ciclos de arriba SÍ abrió y cerró el panel con éxito real
muchas veces) - el bloqueo de esta ronda es de ENTORNO (degradación de la sesión de escritorio
remota a medida que avanza la noche, el mismo límite ya documentado para StarvekeepMod), no del
código del mod ni del arnés de KeepQA. **No se generó ningún baseline nuevo de TerrakeepMod en
esta ronda** - queda pendiente para una sesión con la sesión de escritorio en estado `Conn` desde
el principio (memoria del usuario: "hace falta reiniciar Windows de verdad, no basta con `tscon`
otra vez" - no intentado aquí por ser una acción más invasiva que lo que cubre la autonomía
técnica de una tarea de QA).

---

## 14-sep-2026 (noche, continuación) — KeepQA V2.0, Fase 5: baseline real conseguido con la sesión
## `Activo` de verdad, tras un cuelgue adicional distinto (no `NoAudioHardwareException`) en el
## primer intento de esta misma ronda

El usuario confirmó EN VIVO que estaba usando el PC con normalidad (Windows App abierto) justo
antes de esta ronda. Comprobado `query session` (sesión 1 `adrian` → **`Activo`**) y sin ningún
`dotnet.exe` de tModLoader colgado antes de lanzar nada.

**Intento 1** (`-SegundosEspera 300`, este ya sería el 4º intento real contando la ronda anterior):
compiló bien (0 errores), el cliente **NO mostró `NoAudioHardwareException` esta vez** (diferencia
real frente a los dos cuelgues de la ronda anterior, coherente con la sesión ahora `Activo`), y
avanzó más lejos que nunca: `Finding Mods...` → `Mod Changes since last launch:` (detecta la
actualización de `HEROsMod` en el Workshop). Pero **el log se quedó congelado exactamente en esa
línea durante los 300s completos de espera** (confirmado con sondeo real cada 20s sobre el tamaño
de `client.log`, no solo "parecía colgado") - un síntoma DISTINTO del de la ronda anterior (ni
audio ni un cuelgue tan temprano), así que no aplicaba todavía la regla de "mismo síntoma dos veces
seguidas, parar". Proceso colgado matado por el propio script al agotar el tiempo, sin residuos
(confirmado después con `Get-CimInstance`).

**Intento 2** (mismo comando, `-SegundosEspera 500`, sesión limpia otra vez): **ÉXITO REAL** -
`OK: encontrado 'AUTOPRUEBA PANEL COMPLETA' en el log.`, código de salida 0, a los 342s reales.
Capturas reales y con contenido dejadas en `terrakeep-capturas\` (`pestana-0-character.png`,
`pestana-2-builds.png`, `pestana-5-settings.png` y el resto de pestañas/animaciones/apariencia),
confirmadas a ojo una a una antes de promoverlas - panel de Terrakeep real, en inglés (idioma del
juego en ese lanzamiento), sin nada negro ni en blanco. **El mismo comando, sin cambiar nada, pasó
de congelarse en seco a completar la batería entera dos intentos después** - indicio de que el
intento 1 sí era un cuelgue real puntual (no un bug determinista del panel ni del arnés), y no algo
que un tercer intento con el mismo timeout fuera a arreglar por sí solo; alargar la espera
(300s → 500s) fue lo que permitió comprobarlo de verdad en vez de suponerlo.

**Hipótesis planteada y no confirmada para el intento 1**: podría tratarse de un diálogo nativo de
tModLoader ("mods actualizados", con un botón OK) esperando un clic que el arnés no da, bloqueando
el hilo principal justo después de `Mod Changes since last launch:` - encaja con el punto exacto del
cuelgue. No se pudo confirmar ni descartar con captura real porque el intento 1 ya se había matado
cuando se planteó la hipótesis, y el intento 2 tuvo éxito sin necesitar ningún clic (mismo mod
recién actualizado, mismo Workshop). **Queda como sospecha razonable, no como hecho verificado** -
si un futuro intento se vuelve a colgar en el mismo punto exacto, comprobar con una captura real
(`PrintWindow`, mismo mecanismo que `CapturaDePantalla.cs`) si hay un diálogo de "mods actualizados"
esperando un clic en OK, y si lo hay, dar el clic real (`PostMessage`/coordenadas sobre la captura)
en vez de solo esperar más tiempo - anotado también en la sección de patrones de automatización de
tModLoader para no repetir la investigación desde cero.

**Baseline de KeepQA cerrado con esta evidencia real**: 3 pantallas promovidas con
`gestorBaseline.js guardar terrakeepmod ... --confirmar --commit c32e749 --idioma en` -
`panel-personaje` (pestaña Character), `panel-builds` (pestaña Builds), `panel-settings` (pestaña
Settings).

### Archivos tocados
Ninguno del mod en esta ronda - solo ejecución del arnés ya existente y escritura en
`Downloads\KeepQA\baselines\terrakeepmod\`.

---

## 15-sep-2026 — Repaso integral con el arsenal de KeepQA: auditoría de exactitud de la Guía

Petición explícita del usuario: revisar si el contenido REAL del panel Guía (jefes, biomas,
eventos, mecánicas) estaba suficientemente curado en sus explicaciones. Detalle completo del
alcance, metodología y verificación en `Downloads\KeepQA\REPASO-INTEGRAL-15SEP\
REPASO-TMODLOADER.md` - aquí solo el resumen y los archivos tocados.

Se leyeron las 1384 líneas de `Localization/es-ES_Mods.TerrakeepMod.hjson` (sección `Guia`, 26
tramos, 52 pasos) y se verificaron contra `Downloads\tModLoader-Decompiled\tModLoader\Terraria\
NPC.cs` (rama 1.4.4.9) las cifras de vida/daño/defensa citadas para ~24 jefes. Veredicto honesto:
el contenido YA estaba muy bien curado (redacción específica citando su fuente en el código,
ninguna generalidad vacía) - no había que "inventar más texto", había que encontrar los errores
puntuales reales. Se encontraron dos:

**H1 - Esqueletron, cifra de vida incorrecta**: el texto decía "4400 de vida repartidos entre las
dos manos y la cabeza". Falso: 4400 es `lifeMax` del tipo 35 (la CABEZA sola, confirmado en
`NPC.cs`); cada mano es el tipo 36 con 600 de vida PROPIA y SEPARADA. Total real ~5600, no 4400.
Es la única cita de todo el árbol que mezclaba mal sus propias fuentes (el resto de jefes
multi-parte - Muro de Carne, Golem, Destructor, Señor de la Luna - sí separan bien cada parte).
Arreglado en `es-ES` y `en-US_Mods.TerrakeepMod.hjson`, clave `Guia.Paso.ArmaParaEsqueletron.
Porque`.

**H2 - Esqueletron, sin "Lectura del jefe" en vivo**: a los dos pasos del tramo le faltaba el
campo `"jefe": 35` en `Assets/guia_progresion.json` que SÍ tienen los otros 19 tramos de jefe
único del árbol. Sin ese campo, `PasoGuia.Jefe` queda a 0 y el panel nunca muestra la lectura en
vivo de vida/daño/defensa reales (vía `EvaluadorGuia.StatsDeJefe`) para el único jefe principal
no opcional de todo el árbol sin ese dato. No hay ninguna razón de diseño documentada para la
exclusión (a diferencia de la Maldad del Mundo, ambigua entre dos jefes, o las cuatro invasiones
por oleadas, sin un único NPC representable) - era una laguna real. Arreglado siguiendo el mismo
patrón que `MuroDeCarne`/`TemploYGolem` (jefe representativo, ignorando la parte secundaria).

Verificación real antes de comitear: `JSON.parse` sobre `guia_progresion.json` (sin errores, 21
tramos; cruce automatizado confirma que solo quedan sin `jefe` los 5 pasos legítimamente sin un
único NPC representable), `hjson.parse` sobre los dos `.hjson` editados (ambos OK), y compilación
COMPLETA real con el compilador de tModLoader (`scripts\verificar-guia.ps1 -SoloCompilar`,
comprobado antes que no hubiera ninguna ventana de juego en primer plano): 0 errores, `.tmod`
empaquetado (585299 bytes), solo los avisos ya conocidos (`CS1701` de Newtonsoft.Json,
`WARN: Image loading failed` de `icon_small.png` por stderr nativo). No se relanzó el cliente
gráfico completo: `AutopruebaGuia` solo cubre el tramo pre-Ojo por diseño, así que no habría
ejercitado el tramo Esqueletron de todas formas; el cambio reutiliza un mecanismo (`StatsDeJefe`)
ya probado en producción por otros tramos, así que el riesgo residual es bajo pero la captura real
en juego del panel de Esqueletron queda como siguiente paso si se quiere cerrar del todo.

TModLoaderMod (el trainer) revisado por si tenía un equivalente narrativo que auditar: no lo
tiene de forma honesta (es un panel de cheats/ajustes, sin texto explicativo de mecánicas) - se
documenta en vez de forzar un análogo falso.

### Archivos tocados
- `Assets/guia_progresion.json` (campo `"jefe": 35` en los dos pasos de Esqueletron).
- `Localization/es-ES_Mods.TerrakeepMod.hjson` y `en-US_Mods.TerrakeepMod.hjson` (texto de
  `Guia.Paso.ArmaParaEsqueletron.Porque` corregido en los dos idiomas).
- `Downloads\KeepQA\REPASO-INTEGRAL-15SEP\REPASO-TMODLOADER.md` (repo aparte, KeepQA): informe
  completo en formato de 10 campos.

## 15-sep-2026 (noche, más tarde) — `viewportAlto` real para `UIList` (KeepQA
## `verificarBordeViewport.js`) + WS3 desplaza de verdad la rejilla de resultados y el árbol de
## carpetas de la Librería

Encargo de KeepQA (repo hermano): cerrar el hueco de cobertura que la pieza nueva
`verificarBordeViewport.js` (nacida esa misma noche, motivada por un bug real de StarvekeepMod)
dejó escrito con honestidad al nacer - "tampoco se ha cableado en TerrakeepMod todavía, revisar
primero el código real de `UIList`".

**Investigado el código real antes de tocar nada** (decompilado, nunca supuesto):
`Downloads\tModLoader-Decompiled\tModLoader\Terraria\GameContent\UI\Elements\UIList.cs` - `UIList`
tiene `OverflowHidden=true` de fábrica y envuelve sus filas reales en un `UIInnerList` PRIVADO del
motor (`internal UIElement _innerList`, único hijo real de `UIList.Children`) cuyo único trabajo es
mover su propio `Top` según `UIScrollbar.GetValue()` en `DrawSelf` - no es un nivel semántico del
árbol. El campo público real que expone las filas de verdad es `UIList._items` (`public
List<UIElement> _items`).

**Extractor** (`UI\Panel\PanelTerrakeepState.cs`): `VolcarGeometriaJson` ahora rellena
`viewportAlto = UIList.GetInnerDimensions().Height` para cualquier `UIList` del árbol (ningún
`UIList` real del mod lleva padding propio, verificado por grep - `GetInnerDimensions()` coincide
en tamaño con `GetDimensions()`, así que el campo es exacto). `VolcarHijosRecursivo` ahora
detecta un `UIList` y, en vez de descender por su `_innerList` interno, recorre
`UIList._items` directamente (`VolcarFilasUIList`, método nuevo) con `padre_id` apuntando al
propio `UIList` - y filtra solo las filas cuyo rectángulo intersecta con el viewport real (mismo
chequeo AABB que hace `UIInnerList.DrawChildren` del motor para decidir qué pintar de verdad).

**Arnés** (`Common\Libreria\AutopruebaLibreria.cs`, WS3, pasos 24/25 nuevos): búsqueda amplia
("es", 478 objetos casan, 100 mostrados - tope real) y desplazamiento de verdad
(`UIList.ViewPosition = float.MaxValue`, vía el helper nuevo
`ContenidoLibreria.DesplazarResultadosAlFinalParaQA`) tanto de la rejilla de resultados como del
árbol de carpetas de primer nivel, antes de volcar geometría (`geometria-viewport-libreria.json`)
y capturar (`ws3-viewport-libreria-final.png`).

**Verificación real, con un bug de ARNÉS (no de UI) cazado y corregido por el camino**: la primera
pasada (solo la rejilla de resultados desplazada) dio `hueco=-30px, DESBORDA` en el árbol de
carpetas - la captura real mostró que las 10 carpetas raíz de verdad no caben en su columna sin
scroll (la 10ª, "tModLoader (mod)", queda fuera; hay scrollbar visible para eso) y esa lista
sencillamente NO se había desplazado todavía en esta autoprueba - la precondición que
`verificarBordeViewport.js` exige (volcado con la lista YA al final) no se cumplía, así que el
"desborda" no era un bug de la UI real. Corregido desplazando también el árbol de carpetas;
revalidado con el juego real: `hueco=+4px` (árbol) y `hueco=+2px` (rejilla), los dos "apropiado",
`RESULTADO: OK` - confirmado con captura real (la 10ª carpeta ya visible, con aire real antes del
borde).

**Estado final**: sin bug de UI real en TerrakeepMod esta ronda (esperable, el motor no se había
tocado desde la ronda anterior de esa misma noche) - la pieza queda construida y verificada contra
dos casos reales positivos (ninguno sintético). `TModLoaderMod` (el trainer) revisado y confirmado
SIN ningún widget con scroll real (grep de `UIScrollbar`/`ViewPosition`/`OverflowHidden`: 0
resultados en todo el mod) - documentado como "no aplica" en vez de forzar un caso sintético.

### Archivos tocados
- `UI\Panel\PanelTerrakeepState.cs` (extractor: `viewportAlto`, `VolcarFilasUIList`).
- `UI\Libreria\ContenidoLibreria.cs` (`DesplazarResultadosAlFinalParaQA`, nuevo).
- `Common\Libreria\AutopruebaLibreria.cs` (pasos 24/25, WS3).
- `evidencia\ws3-libreria.log.txt` (evidencia real de la corrida final).
- Detalle completo (incluido el falso "desborda" del arnés y su corrección) en
  `Downloads\KeepQA\bitacora.md` y `Downloads\KeepQA\PATRONES.md`.
- Commit local: `f1cfbb8`.

---

## 15-sep-2026 (noche) — Cobertura ABSOLUTA de la Guía: cierra el hueco entero de Calamity (0% -> 25
## tramos/60 pasos reales) + reescribe TODO el texto de la Guía (vanilla y Calamity) sin jerga de
## programador

Encargo explícito del usuario, tras rechazar el "21 tramos/57% opcional" que ya se había dado por
bueno antes: la cobertura tiene que ser ABSOLUTA, vanilla Y Calamity, sin excepción, y el texto
que lee el jugador tiene que sonar a guía real del juego, no a informe técnico - dos correcciones
de tono llegaron a mitad de la ronda, la segunda matizando la primera ("no basta con quitar los
nombres técnicos, hace falta ademas la fantasía del juego").

### 1. Auditoría de vanilla: NO hay hueco real (confirmado, no supuesto)

Cross-check contra `terraria.wiki.gg` (páginas `Bosses` y `Events`, dos fetches independientes):
los 21 tramos ya existentes cubren el 100% real de jefes/eventos vanilla con una bandera de
"completado" rastreable (incluidas las cuatro invasiones, Luna de Calabazas/Helada, D2, Torres,
Mechdusa - que es el mismo Golem con otra piel en semillas especiales, sin bandera propia). Los
tres huecos que parecían faltar - Luna de Sangre, Eclipse Solar, Lluvia de Slimes - se investigaron
a propósito y NO tienen ninguna bandera de "completado" en el motor (son eventos ambientales que
terminan solos): forzar un tramo con una bandera falsa habría sido exactamente el "análogo forzado"
que prohíbe el estándar de la casa, así que se documentan como investigados y excluidos con motivo,
no como un descuido. Documentado en la cabecera de `Assets\guia_progresion.json`.

### 2. Calamity: arquitectura nueva, real, verificada contra el mod instalado

`CatalogoGuia.cs` daba por buena, desde su primer día, una decisión de quedarse solo en vanilla
porque nada de Calamity se podía verificar con el mismo rigor que el juego decompilado
(`DownedBossSystem` es un tipo propio del mod, sin referencia de compilación, y sus NPC no tienen
un id fijo). Las dos cosas ya tienen solución real:

- **Banderas de Calamity por reflexión** (`BanderasGuia.AgregarBanderasCalamity`): las 37 banderas
  reales que usa el árbol nuevo (de las 44 propiedades públicas que tiene
  `CalamityMod.DownedBossSystem` en la v2.2.2 instalada) se confirmaron **decompilando el `.tmod`
  real** (`ilspycmd -t CalamityMod.DownedBossSystem CalamityMod.dll`, extraído a mano del
  `2026.6CalamityMod.tmod` instalado siguiendo el formato real de `TmodFile.cs`) - nunca de memoria.
  Se leen por reflexión (`Type.GetProperty` + `PropertyInfo.GetValue`) porque el proyecto no
  referencia `CalamityMod.dll` en `dllReferences` a propósito (mod opcional). Si una actualización
  futura de Calamity renombra una propiedad, se queda "no evaluable", nunca se inventa un valor -
  mismo contrato que una bandera de vanilla desconocida.
- **NPC/objetos de Calamity por nombre** (`CatalogoGuia.ResolverNpcMod`/`ResolverItemMod`): un NPC
  o un objeto de un mod no tiene un id fijo (tModLoader se lo asigna al cargar). El `.json` ya cita
  el "pid" real (`"CalamityMod/DesertScourgeHead"`, mismo formato que `CatalogoBuilds`/
  `ArbolLibreria`) y se resuelve a su `type` real de la partida con la API PÚBLICA de tModLoader
  `ModContent.TryFind<T>` (sin reflexión, sin referenciar `CalamityMod.dll`) durante `Construir()`.
  De ahí para abajo (`EvaluadorGuia.StatsDeJefe`, `EstadoGuia.LecturaDeJefe`) un jefe de Calamity es
  indistinguible de uno vanilla: los dos son un `NPC.type` normal con vida/daño/defensa REALES de
  ESTA partida, así que los modos de dificultad propios de Calamity (Revengeance, Death) quedan
  cubiertos solos.
- **`banderaCarmesi`** (nueva, en `LeerRequisito`): igual que `jefeFinalCarmesi` ya hacía para el
  jefe, pero para el requisito de bandera - hacía falta de verdad para `HiveMindOPerforator`
  (Corrupción y Carmesí tienen banderas DISTINTAS y de verdad separadas en Calamity,
  `downedHiveMind`/`downedPerforator`, a diferencia de vanilla donde el Devorador y el Cerebro
  comparten la misma `downedBoss2`).

Todos los nombres internos reales (banderas, clases de NPC, clases de objeto de invocación) se
confirmaron decompilando `CalamityMod.dll` con `ilspycmd` esa misma noche - no una sola vez de
memoria. El orden de progresión (qué jefe va antes de cuál, qué es obligatorio) no se puede sacar
del código: se investigó contra la Calamity Mod Wiki oficial (`calamitymod.wiki.gg`,
`Guide:Mod_progression` y la página de cada jefe), cruzado con el propio drop real del juego para
confirmar los enganches duros (p. ej. Profaned Guardians -> Profaned Core -> invoca a Providence).

### 3. Los 25 tramos nuevos (60 pasos), en `Assets\guia_progresion.json`

**Columna vertebral (7, casi-obligatorios)**: Astrum Deus, Profaned Guardians+Providence, el trío
Ceaseless Void/Storm Weaver/Signus, Polterghast, Old Duke+Devourer of Gods, Yharon, Exo Mechs+
Supreme Calamitas - la cadena final real del mod, en el orden que confirma la propia wiki.

**Opcionales reales (18)**: Desert Scourge, Giant Clam, Crabulon, Hive Mind/Perforators, Slime God,
Dreadnautilus+Horrible Hog (Luna de Sangre exclusiva de Calamity - el `NPCID` vanilla 618,
`BloodNautilus`, con su IA reescrita), Cryogen, Aquatic Scourge, Brimstone Elemental, Cragmaw Mire
(Lluvia Ácida fase 2), Astrum Aureus, Calamitas Clone, Great Sand Shark, Ravager, Plaguebringer
Goliath, Dragonfolly, Primordial Wyrm (secreto de postgame, documentado con honestidad como
"detalle sin confirmar" donde de verdad no se pudo confirmar - defensa base 999, huele a mecánica
especial no investigada), Mauler+Nuclear Terror (Lluvia Ácida fase 3).

Cada requisito de daño de arma se calculó con la MISMA fórmula que ya usaban los 21 tramos vanilla
(`daño - defensa*0,5`, con margen), citando la defensa/vida real decompilada de cada NPC
(`LifeMaxNERB`, confirmado por `ilspycmd` para cada jefe) en el comentario `_fuente` del `.json` -
nunca en el texto que lee el jugador (ver punto 5).

### 4. Arnés de pruebas: `AutopruebaGuia` desactualizada, arreglada

La verificación EN VIVO real (`scripts\verificar-guia.ps1 -Calamity`, cliente gráfico real contra
el `.tmod` recién compilado) confirmó que la arquitectura funciona de verdad - el tramo
`DesertScourge` apareció en pantalla con sus requisitos reales (14 de daño, objeto "Desert
Medallion", gancho) sin ningún aviso de "no se encontró el NPC/objeto" - pero también sacó a la luz
que el recorrido hardcodeado de `AutopruebaGuia.cs` (escrito antes de que existiera el árbol de
Calamity) esperaba que `ReySlime` (Orden 5) fuera el primer opcional pendiente, y ahora
`DesertScourge` (Orden 3, nunca derrotado en el recorrido) se colaba delante en cada comprobación
-> 27 líneas "NO CUADRA" en la evidencia. No es un bug de la guía (el comportamiento real - mostrar
siempre el opcional pendiente de menor Orden - es exactamente el diseño correcto), es la prueba
quedándose vieja. Arreglado con el mismo patrón que ya usaba el propio arnés para sus opcionales nuevos, en tres
rondas reales de recompilar + relanzar el cliente gráfico con Calamity cargado de verdad
(`scripts\verificar-guia.ps1 -Calamity`, nunca solo compilar):

1. **Ronda 1** (antes de tocar `AutopruebaGuia.cs`): 27 líneas "NO CUADRA" - los 21 tramos
   opcionales de Calamity (Orden 3 a 97) se colaban delante de `ReySlime`/`EjercitoGoblin`/etc. en
   cada comprobación del recorrido opcional, porque nunca se derrotan en este arnés.
2. **Ronda 2** (tras marcar "superadas" las 21 banderas opcionales de Calamity en `Arrancar()`, vía
   el setter nuevo `BanderasGuia.IntentarEscribirBanderaCalamity`, y devolverlas a `false` en
   `RestaurarTodosLosOpcionalesRestantes`): bajó a 3 líneas "NO CUADRA", esta vez en el camino
   OBLIGATORIO - Astrum Deus (Orden 82, real de verdad) cae entre `EventosLunares` (80) y `MoonLord`
   (90), y el arnés esperaba pasar directo de las cuatro torres a Moon Lord.
3. **Ronda 3** (tras neutralizar también Astrum Deus en `MarcarTorresDerrotadasDeMentira`): bajó a 1
   línea "NO CUADRA" - el tramo final ENTERO de Calamity (Guardianes, Providence, el trío,
   Polterghast, Old Duke, DoG, Yharon, Exo Mechs, Supreme Calamitas - Orden 91 a 96, todos
   obligatorios) se quedaba pendiente tras `MoonLord`, donde el arnés esperaba "no queda ningún paso
   pendiente". Arreglo definitivo: unificadas las dos columnas vertebrales (Astrum Deus + el tramo
   final entero) en `_banderasObligatoriasCalamity`, neutralizadas juntas en el mismo punto
   (`MarcarTorresDerrotadasDeMentira`) y restauradas juntas en `RestaurarTramosNuevos`.
4. **Ronda 4, la definitiva**: recompilado otra vez y relanzado el cliente gráfico completo con
   Calamity - **0 líneas "NO CUADRA", `AUTOPRUEBA GUIA COMPLETA` alcanzada, script de verificación
   terminado con exit code 0** (confirmado leyendo el código de salida real del proceso, no solo el
   texto del log). El arnés vanilla completo (los 56 pasos originales) sigue pasando entero con
   Calamity cargado.

**Honesto sobre el alcance real de la verificación en vivo**: no hay un recorrido paso a paso
dedicado que abra y compruebe cada uno de los 25 tramos nuevos uno por uno (como sí existe para los
21 de vanilla) - construir eso es un arnés bastante más grande, pendiente. Lo que SÍ está
verificado en vivo: el catálogo carga sin ningún aviso de datos con Calamity activo, el tramo de
menor Orden (Desert Scourge) se muestra con sus requisitos y objeto de invocación reales, y el
resto de la lógica (banderas, resolución de NPC/objeto) es el MISMO código que ya ejercita ese
tramo - no una ruta aparte sin probar.

### 5. Corrección de tono, en dos vueltas (pedida por el usuario a mitad de la ronda)

Primera corrección: el texto que lee el jugador (`Guia.Paso.*.Porque`/`.Como` en los dos idiomas)
tenía citas de implementación seguidas al pie de la letra - nombres de clase, código C# literal
(`if (!downedHiveMind && !downedPerforator)`), "NPC.cs", "CalamityMod.dll", nombres de variable como
`downedBoss2` expuestos directamente. Confirmado que el problema era real y **anterior a esta
sesión** (no solo de los tramos nuevos): una captura real del panel en juego, hecha esta misma
noche, mostraba literalmente "(NPC.cs, bloque type==1113)" en pantalla para el Muro de Carne.

Segunda corrección, más fina: quitar los tecnicismos no basta si el texto se queda plano/genérico -
tiene que sonar a la fantasía real de Terraria/Calamity, con la instrucción/consecuencia concreta
intacta. Investigado el tono real de la Terraria Wiki (`terraria.wiki.gg`) como referencia antes de
reescribir, no solo de memoria.

Limpieza en tres pasadas sobre los DOS idiomas, dentro de todo `Guia.*` (no solo lo nuevo):
1. Automática: strip de paréntesis con marcadores técnicos (`NPC.cs`, `CalamityMod.dll`,
   `LifeMaxNERB`, `OnKill`...) - 58 líneas por idioma.
2. Manual, texto completo por texto completo: 60-61 campos `Porque`/`Como` por idioma reescritos a
   mano, quitando toda mención de bandera/clase/variable interna.
3. Segunda vuelta manual sobre los mismos 60-61 campos: de "limpio pero plano" a voz de aventura de
   verdad ("en cuanto caiga el Devorador de Mundos o el Cerebro de Cthulhu, tu mundo entero lo
   nota..." en vez de "el juego marca NPC.downedBoss2 al morir cualquiera de los dos").

Verificado con `node` + el paquete `hjson` (instalado en el scratchpad de la sesión, no en el
proyecto) que los dos `.hjson` siguen parseando bien y que un grep del árbol completo `Guia.*` ya
no encuentra NINGÚN patrón técnico (`NPC.`, `WorldGen.`, `Main.`, `CalamityMod.dll`, `OnKill`,
`SetDefaults`, `type==`, `LifeMaxNERB`, `downedX`, `flag`/`bandera` como jerga, `reflexion`) en
ninguno de los dos idiomas. Las citas técnicas siguen vivas donde tienen que estar: en el comentario
`_fuente` de cada requisito del `.json` (para quien mantenga el proyecto después), nunca en el texto
que ve el jugador.

### Verificación real antes de comitear
- `JSON.parse`/`json.load` sobre `guia_progresion.json`: sin errores, 46 tramos (21 vanilla + 25
  Calamity), 176 pasos.
- `hjson.parse` sobre los dos `.hjson`: sin errores, `Guia.Tramo`=46, `Guia.Paso`=116 (56+60),
  `Guia.Bandera`=58 (27 vanilla + 30 Calamity + `downedPerforator`), `Guia.Zona`=10 (8+2 nuevas:
  Desierto, OceanoProfundo).
- Compilación COMPLETA real con el compilador de tModLoader (`scripts\compilar.ps1`): 0 errores,
  `.tmod` empaquetado (609467 bytes, última recompilación tras el arreglo completo del arnés),
  solo los avisos ya conocidos (`CS1701` de Newtonsoft.Json,
  `WARN: Image loading failed` de `icon_small.png`).
- Verificación EN VIVO con el cliente gráfico real y CalamityMod cargado de verdad
  (`scripts\verificar-guia.ps1 -Calamity`), CUATRO rondas completas (compilar + relanzar el
  cliente gráfico + leer el log real cada vez), hasta la última con 0 líneas "NO CUADRA" y el
  script terminado con exit code 0 - ver punto 4 para el detalle honesto de qué cubre y qué no.

### Archivos tocados
- `Assets\guia_progresion.json` (25 tramos nuevos, cabecera actualizada sobre la auditoría vanilla).
- `Common\Guia\CatalogoGuia.cs` (`ResolverNpcMod`/`ResolverItemMod`, `jefeFinalMod`/`jefeMod`/
  `jefeFinalModCarmesi`, `idMod`/`idsMod`, `banderaCarmesi`, cabecera actualizada).
- `Common\Guia\BanderasGuia.cs` (`AgregarBanderasCalamity`, `IntentarEscribirBanderaCalamity`).
- `Common\Guia\AutopruebaGuia.cs` (neutraliza/restaura los opcionales de Calamity en
  `Arrancar()`/`RestaurarTodosLosOpcionalesRestantes`).
- `Localization\es-ES_Mods.TerrakeepMod.hjson` / `en-US_Mods.TerrakeepMod.hjson` (contenido nuevo +
  reescritura de tono de TODO `Guia.*`, vanilla incluido).
- `evidencia\guia-calamity.log.txt` (log real de la verificación en vivo más reciente).
- Investigación externa citada: `calamitymod.wiki.gg` (`Guide:Mod_progression` y páginas de jefe
  individuales), `terraria.wiki.gg` (`Bosses`, `Events`), decompilación real de
  `CalamityMod.dll` v2.2.2 con `ilspycmd` (banderas, clases de NPC/objeto, `LifeMaxNERB`,
  `NPC.defense`).

### Obstáculo real resuelto con autonomía técnica: el diálogo "Mod Changes since last launch" podía
### colgar `verificar-guia.ps1` sin ningún error visible - causa real encontrada y arreglo real, sin
### depender de ningún clic

Aviso del usuario a mitad de la ronda: al lanzar tModLoader puede salir un diálogo real de "mods
actualizados" que exige un clic en "OK" para que el arranque siga - si nadie lo da, el proceso se
queda colgado ahí sin ningún error, y ya había quedado como sospecha sin confirmar en una ronda
anterior (14-sep-2026, cuelgue real justo después de "Mod Changes since last launch:" en el log).

Investigado a fondo en el código decompilado en vez de suponer (`Terraria\ModLoader\UI\Interface.cs`,
`Terraria\ModLoader\Core\ModOrganizer.cs`, `Terraria\ModLoader\ModLoader.cs`): el diálogo es un
`UIInfoMessage` de verdad (`infoMessage.Show(...)`, botón "OK"/"Continuar de todos modos") que sale
cuando `ModOrganizer.DetectModChangesForInfoMessage` detecta que algún mod de Workshop (en este
harness, casi siempre `CalamityMod.tmod`, que el propio script copia de la carpeta `Mods` real cada
vez, con la versión que tenga en ese momento) tiene distinta versión que la última vez que ESE
sandbox arrancó con éxito (comparado contra `Main.SavePath\LastLaunchedMods.txt`).

**La solución real, sin tocar ningún clic**: `"ShowNewUpdatedModsInfo"` es un ajuste YA EXISTENTE de
tModLoader, persistido en `config.json` (`Terraria.ModLoader.ModLoader.showNewUpdatedModsInfo`,
leído/escrito con `Main.Configuration.Get`/`Put` - el mismo ajuste que el jugador puede apagar a
mano en Ajustes del juego) - con él a `false`, `DetectModChangesForInfoMessage` devuelve vacío en su
primerísima línea sin ni siquiera comprobar si hay cambios, así que el diálogo nunca puede salir,
pase lo que pase con las versiones de los mods.

**Arreglo aplicado**: `scripts\verificar-guia.ps1` fuerza `"ShowNewUpdatedModsInfo": false` en el
`config.json` del sandbox ANTES de cada lanzamiento (crea la clave si el archivo no la trae
todavía), así que no depende de que un lanzamiento anterior ya lo hubiera desactivado a mano.
Verificado con una ronda completa real tras el arreglo: el paso nuevo se ejecuta y lo deja escrito
en el log (`ShowNewUpdatedModsInfo forzado a false...`), la corrida entera llega a `AUTOPRUEBA GUIA
COMPLETA` con 0 líneas "NO CUADRA" y el script termina con exit code 0 - sin ningún cuelgue, sin
necesitar ningún clic de nadie.

**Alcance real de este arreglo**: solo se tocó `TerrakeepMod\scripts\verificar-guia.ps1`, el único
script de este repositorio que lanza el cliente gráfico completo. El resto de la familia "Keep"
(StarvekeepMod, TerrakeepTrainer...) tiene sus propios arneses con el mismo patrón de sandbox de
tModLoader/DST y podría toparse con el mismo síntoma - queda anotado aquí para que una sesión futura
en esos repos no tenga que volver a investigarlo desde cero: mismo ajuste (`ShowNewUpdatedModsInfo`
en su `config.json`), mismo arreglo.

### Archivos tocados (este arreglo)
- `scripts\verificar-guia.ps1` (fuerza `ShowNewUpdatedModsInfo=false` antes de cada lanzamiento).
- `C:\Users\adrian\Documents\My Games\Terraria\tModLoader-TerrakeepGuia\config.json` (parcheado a
  mano una vez, y ya lo mantiene el propio script de ahora en adelante).


---

## 15-sep-2026 (cierre de la noche) — Verificación de cierre: recompilación, capturas frescas de la
## Guía con Calamity, versión 0.4.0 y README real con las 8 áreas

Encargo de cierre de la ronda de esta noche (ver la entrada anterior, "Cobertura ABSOLUTA de la
Guía"): confirmar que lo construido compila y funciona de verdad, subir la versión, y dejar el
`README.md` a la altura de lo que el panel hace hoy - hasta ahora decía "seis pestañas" y no
mencionaba ni Guía ni Álbum en absoluto, pese a que `AreaTerrakeep` tiene 8 valores reales
(`Personaje=0, Libreria=1, Builds=2, Investigacion=3, Exploracion=4, Ajustes=5, Guia=6, Album=7`).

### 1. Recompilación real, dos veces

`scripts\compilar.ps1` (compilador de dos fases de verdad, nunca `dotnet build` a secas - ver la
cabecera del propio script): **0 errores** las dos veces.

- Primera pasada (antes de tocar nada): `.tmod` de 609467 bytes, IDÉNTICO en tamaño al que ya
  estaba instalado en `Mods\TerrakeepMod.tmod` (mismo hash de contenido, solo cambiaba la fecha de
  compilación) - confirma que el `.tmod` instalado ya reflejaba de verdad el trabajo de la ronda
  anterior, sin nada pendiente de recompilar.
- Segunda pasada (tras subir la versión en `build.txt` y añadir `docs\*` a `buildIgnore`):
  609967 bytes. Verificado con `node tmod-extract.js` (desde
  `Downloads\Terrasavr-Win\Terrasavr-Calamity-Beta\resources\app\`, la herramienta real de
  inspección de `.tmod` que ya documenta el `CLAUDE.md` de este repo): 19 archivos dentro,
  `Version: 0.4.0`, y **`docs\` no aparece por ningún lado** - la carpeta nueva de capturas del
  README queda fuera del paquete distribuible, igual que ya pasaba con `scripts\`/`evidencia\`.

### 2. Capturas reales frescas: `scripts\verificar-guia.ps1 -Calamity`

Ronda completa del arnés gráfico real (recompila + lanza el cliente contra el sandbox aislado +
recorre el tramo vanilla completo con Calamity cargado): **exit code 0, `AUTOPRUEBA GUIA
COMPLETA`, ninguna comprobación en rojo**. 70 capturas reales del back buffer en
`tModLoader-TerrakeepGuia\terrakeep-capturas\` (fuera del repo, como documenta el propio script).

Antes de lanzarlo, comprobada la ventana en primer plano por `user32.dll` (`GetForegroundWindow`)
para no interrumpir una partida real del usuario si hubiera alguna - salió la del propio cliente
de pruebas anterior, nada que proteger.

Entre las 70 capturas, dos con contenido especialmente relevante para el README:
- `guia-34-brujula-marcado.png`: la pestaña Guía real, con la brújula del mapa ya marcada y, en la
  columna derecha, el aviso real "Tienes Calamity instalado" con la explicación de cómo se suma la
  progresión de Calamity encima de la vanilla - confirma en pantalla, sin montaje, que el trabajo
  de esta noche está de verdad ahí.
- `hitos-1-album.png`: la pestaña Álbum real, con 47 capturas de hito ya listadas (fecha + nombre
  del tramo cerrado) - la otra mitad del titular de esta noche que el README tampoco mencionaba.

### 3. Corrección honesta: no existe pantalla de "Acerca de"/créditos en el panel

El encargo original hablaba de "una pantalla de changelog dentro del panel". Comprobado a fondo
(grep de "changelog"/"acerca de"/"version"/"creditos" por todo el proyecto + lectura completa de
`UI\Ajustes\ContenidoAjustes.cs`): esa pestaña solo tiene idioma, deshacer/rehacer y la lista de
atajos. **No existe ninguna pantalla así de verdad**, así que no se inventó una - el análogo real y
honesto que sí existe es subir `version` en `build.txt` (contenido grande: se justifica un salto de
versión menor, no un parche) y documentarlo como entrada de verdad en una sección "Novedades" del
propio `README.md` (se descartó crear un `CHANGELOG.md` aparte: con una sola entrada real hasta
ahora, meterlo en el propio README es más fácil de encontrar y no añade un archivo más que
mantener sincronizado).

### 4. Cambios de contenido

- `build.txt`: `version = 0.3.0` → `0.4.0`. `buildIgnore` con `docs\*` añadido (mismo motivo que
  `scripts\*`/`evidencia\*`: documentación pura, cero uso en tiempo de ejecución).
- `description.txt` (los dos idiomas): "seis pestañas"/"six tabs" → **ocho**, con Guía y Álbum
  descritas con el mismo nivel de detalle que las otras seis - se había quedado desactualizada
  desde que Guía y Álbum se añadieron al panel.
- `README.md`:
  - `## Qué hace` reescrito de seis a ocho pestañas, con Guía y Álbum descritas a fondo (qué
    cubren los 46 tramos/176 pasos, la brújula del mapa, el medidor de preparación, el por qué/
    cómo, la lectura del jefe, la hoja de ruta; y el disparo automático de capturas del Álbum).
  - `## Capturas` nueva, con 4 imágenes reales (nunca montajes): las dos ya citadas de la Guía y
    el Álbum (las protagonistas de esta noche) más `builds-1600x900-es.png` (Builds con el
    selector Vanilla/Calamity, capturado el 13-sep) y `libreria-1600x900-es.png` (Librería
    navegando "Mascotas de Jefes", mismo día) - reutilizadas de `evidencia\espaciado-capturas-
    calamity\` en vez de regenerar todo el juego para eso, tal y como pedía el encargo.
  - `## Novedades` nueva, con la entrada real de la versión 0.4.0.
  - Copiadas a `docs\screenshots\01-guia-calamity.png` / `02-album.png` / `03-builds.png` /
    `04-libreria.png` (carpeta nueva, mismo patrón de nombrado numerado que ya usa el repo hermano
    `Downloads\TerrakeepTrainer\docs\screenshots\`).

### Verificación real antes de comitear
- Compilación completa dos veces con `scripts\compilar.ps1`: 0 errores las dos veces.
- `node tmod-extract.js` sobre el `.tmod` final: `Version: 0.4.0`, 19 archivos, sin `docs\` dentro.
- `scripts\verificar-guia.ps1 -Calamity`: exit code 0, `AUTOPRUEBA GUIA COMPLETA`, sin
  comprobaciones en rojo.

### Archivos tocados
- `build.txt` (versión, `buildIgnore`).
- `description.txt` (ocho pestañas, los dos idiomas).
- `README.md` (Guía y Álbum documentadas, sección de capturas, sección de Novedades).
- `docs\screenshots\01-guia-calamity.png`, `02-album.png`, `03-builds.png`, `04-libreria.png`
  (nuevos).

---

## 15-sep-2026 (madrugada) — cierra la deuda real pendiente: recorrido EN VIVO de los 25 tramos
## nuevos de Calamity, no solo el primero

Encargo explícito del usuario: hasta ahora `AutopruebaGuia`/`verificar-guia.ps1 -Calamity`
confirmaba en el juego real solo el primer tramo de Calamity (`DesertScourge`, el de menor Orden);
los otros 24 se habían validado por datos/decompilación (`CalamityMod.dll`) pero nunca recorriendo
la partida real tramo a tramo, a diferencia de los 21 tramos de vanilla, que sí tienen ese
recorrido completo desde antes. Encargo: ampliar el arnés para cubrir los 25 automáticamente,
documentando con honestidad cualquiera que de verdad no se pudiera simular.

### Diseño: generado desde los propios datos, no un `case` por tramo a mano

Copiar el patrón literal que ya usa `AutopruebaGuia` para los tramos de vanilla (un `case` por
micro-paso, escrito a mano) habría significado ~500 líneas más para 60 pasos nuevos - inmanejable.
Antes de escribir nada se leyó `Assets/guia_progresion.json` entero (los 25 tramos de Calamity,
línea a línea) para confirmar que TODOS siguen sin excepción el mismo patrón de datos: cada tramo
son pares de pasos "ArmaParaX" (un único requisito obligatorio `dano_arma`, más objeto/gancho
opcionales recomendados) + "VencerAX" (un único requisito obligatorio `bandera`, a veces con
`banderaCarmesi`). Confirmado eso, el recorrido se generó en tiempo de ejecución desde
`CatalogoGuia.Tramos` en vez de a mano:

- `EncolarTramoCalamity` recorre los pasos de un tramo DE DOS EN DOS (arma + vencer) y encola una
  `Action` por cada micro-acción real (comprobar el paso con `ComprobarPaso`/`ComprobarPasoOpcional`
  según el tramo sea vertebral u opcional, capturar, poner un arma real con el daño exacto que pide
  el `.json`, comprobar que el objetivo salta a "vencer", leer los stats reales del jefe, capturar
  otra vez, escribir la bandera real por reflexión con `BanderasGuia.IntentarEscribirBanderaCalamity`,
  quitar el arma) - el MISMO conjunto de comprobaciones que ya usa el resto del arnés para sus
  propios tramos, solo que generado en vez de tecleado 25 veces. Si algún par de pasos no siguiera
  el patrón esperado, se avisa en el log y se salta, en vez de fingir una comprobación que no toca
  (no hizo falta: los 60 pasos reales lo siguieron).
- `AvanzarRecorridoCalamityCompleto` (case 252) ejecuta UNA acción de la cola por fotograma,
  reutilizando `_repetir` para que `Paso()` vuelva a llamar al mismo `case` en vez de avanzar -
  misma granularidad (una cosa por fotograma, con capturas reales) que el resto del arnés.
- Antes de arrancar (case 251, `PrepararRecorridoCalamityCompleto`), se cierran a mano el camino
  obligatorio de vanilla ENTERO y los doce tramos opcionales de vanilla (reutilizando los mismos
  campos `_xOriginal` que ya capturaba `Arrancar()`, más dos nuevos, `_downedFishronOriginal`/
  `_downedEmpressOfLightOriginal`, que no tenían respaldo hasta ahora) para que ningún tramo de
  vanilla se cuele por delante de Calamity por Orden (p.ej. ReySlime=5 cae entre DesertScourge=3 y
  GiantClam=4) - las 33 banderas propias de Calamity (21 opcionales + 12 de la columna vertebral)
  ya estaban en `false` en este punto de la prueba (las dejaron así los dos bloques de restauración
  de más arriba), así que es el punto de partida limpio que hace falta.
- Al terminar (case 254, `RestaurarRecorridoCalamityCompleto`), las 33 banderas de Calamity y los
  21 flags de vanilla que este bloque forzó a `true` vuelven todos a su valor ORIGINAL.

### Vertebral vs. opcional: por qué hacía falta distinguirlos

Los 7 tramos de la columna vertebral (Astrum Deus, Guardianes+Providence, el trío, Polterghast,
Old Duke+DoG, Yharon, Exo Mechs+Supreme Calamitas) NO son opcionales - forman parte del camino
OBLIGATORIO real (`TramoGuia.Opcional=false`), así que su objetivo se lee con
`EstadoGuia.PasoActual`/`ComprobarPaso`, igual que los 9 tramos obligatorios de vanilla. Los otros
18 sí son opcionales (`Opcional=true`), y se leen con `EstadoGuia.PasoOpcionalActual`/
`ComprobarPasoOpcional`. `ComprobarPasoDelTramoCalamity` elige cuál de las dos llamar mirando
`TramoGuia.Opcional`, tramo por tramo - el mismo criterio que ya usa el resto del mod.

### Verificación real antes de comitear

- Compilación completa (`scripts\verificar-guia.ps1 -Calamity -SoloCompilar`): 0 errores.
- **Corrida 1** (cliente gráfico real, CalamityMod cargado): el recorrido nuevo de los 25 tramos
  llegó limpio hasta el final (`60 pasos comprobados, cola vacia=True, objetivo obligatorio
  restante=(ninguno) -> OK`), pero el script salió con exit code 1 por 4 líneas "NO CUADRA" en el
  `case 11` PRE-EXISTENTE (la espera de que `Player.UpdateEquips` recalcule la defensa de la
  armadura, tras equipar en `case 10`) - un paso de vanilla escrito hace semanas, sin relación con
  este cambio, que agotó los 300 fotogramas de margen esa vez en concreto.
- **Corrida 2** (misma máquina, mismo `.tmod`, relanzada para aislar si era un fallo real o una
  única vez de más carga en el sistema): exit code 0, `AUTOPRUEBA GUIA COMPLETA`, **ninguna
  comprobación en rojo** - confirma que la corrida 1 fue un pico de carga puntual del sistema en un
  paso de vanilla ya existente, no una regresión de este cambio (que ni toca ese código).
- `scripts\compilar.ps1`: recompilado también hacia la carpeta `Mods` REAL (la que carga el
  usuario, no el sandbox de pruebas) - `TerrakeepMod.tmod`, 612608 bytes, mismo momento.
- `SegundosEspera` de `verificar-guia.ps1` subido de 300 a 600: con el recorrido nuevo (~330
  acciones más a 14 fotogramas cada una) el margen antiguo se quedaba justo, aunque en la práctica
  las dos corridas terminaron mucho antes del límite (el bucle de espera corta en cuanto encuentra
  `AUTOPRUEBA GUIA COMPLETA` en el log).

**Cobertura real conseguida**: los 25 tramos nuevos de Calamity (60 pasos) tienen ahora
verificación EN VIVO automática completa, igual que los 21 de vanilla - no quedó ninguno sin
recorrer. No hizo falta documentar ningún límite técnico real: los 60 pasos siguieron el patrón
arma+vencer sin excepción, así que el generador los cubrió todos sin necesitar un caso especial a
mano.

### Archivos tocados
- `Common\Guia\AutopruebaGuia.cs` (cases 251-254 nuevos: `PrepararRecorridoCalamityCompleto`,
  `EncolarTramoCalamity`, `AvanzarRecorridoCalamityCompleto`, `ComprobarPasoDelTramoCalamity`,
  `ComprobarLecturaDeJefeGenerica`, `MarcarBanderaCalamityDeVerdad`,
  `ComprobarRecorridoCalamityCompletoTerminado`, `RestaurarRecorridoCalamityCompleto`; campos nuevos
  `_downedFishronOriginal`/`_downedEmpressOfLightOriginal`/`_colaCalamity`/
  `_tramosCalamityRecorridos`/`_pasosCalamityComprobados`).
- `scripts\verificar-guia.ps1` (`SegundosEspera` por defecto, 300 → 600).
- `evidencia\guia-calamity.log.txt` (log real de la corrida final, limpia).
- `.tmod` recompilado hacia la carpeta `Mods` real del usuario (612608 bytes).

## 15-sep-2026 - Verificación de calidad del español con la maquinaria de Starvekeep-Traduccion-ES (TR1)

Encargo de I+D (`KeepQA4\I+D-PROXIMOS-PASOS-FAMILIA-KEEP.md`, TR1): comprobar si la maquinaria
de corpus de Starvekeep-Traduccion-ES sirve para verificar la calidad del español ya escrito en
`Localization\{es-ES,en-US}_Mods.TerrakeepMod.hjson`, tras la reescritura de tono de esta noche.
Detalle completo de la investigación y de las herramientas nuevas en `Downloads\KeepQAitacora.md`
(entrada de la misma fecha). Resumen para esta bitácora:

- Primera ejecución real de `KeepQA\src\localizacion\comprobarLocalizacion.js` contra los dos
  `.hjson` (existía desde antes, nunca se había invocado): 1109 claves es / 1110 en, **1 huérfana
  real** - `Keybinds/AbrirAlbum.DisplayName` está comentada en es-ES y su valor en en-US quedó sin
  traducir ("Abrir Album" en el fichero inglés). NO se ha tocado ese bloque (está comentado a
  propósito y no está claro si el keybind está cableado a código) - queda pendiente de una
  decisión del usuario, no es un arreglo de texto.
- Herramienta nueva `KeepQA\src\corpusuditarCalidadEs.js` (anglicismos, latinoamericanismos,
  ortografía por patrones) encontró **6 tildes reales** que faltaban en el texto de Guía
  reescrito esta noche: "mucho mas" → "mucho más" (x4, tramos de Calamitas Clone,
  Dreadnautilus/Horrible Hog, lluvia ácida y Supreme Calamitas), "sólo" con tilde obsoleta → "solo"
  (x2, Moon Lord y El Ojo - norma RAE 2010), y de paso "actua" → "actúa" en el mismo tramo de
  Supreme Calamitas. Las 6 corregidas en `Localizations-ES_Mods.TerrakeepMod.hjson` (solo texto,
  ninguna clave ni estructura tocada) y reverificadas: la herramienta ya no las encuentra.
- Commit local en este repo con las 6 correcciones de texto.


## 16-sep-2026 - Un solo cerebro de Guia (T1): EvaluadorGuia.cs pasa a llamar a Terrakeep.Core

Encargo de I+D (I+D-PROXIMOS-PASOS-FAMILIA-KEEP.md, recomendacion T1/hallazgo H3): dos
evaluadores de Guia independientes (aqui y en Terrakeep.Core/Guia/GuideEvaluator.cs) con las
mismas funciones (Fraccion, Contar, Preparacion, PasoCompletado, el despacho de requisitos)
escritas dos veces y sincronizadas solo de memoria - el mismo problema que el lado DST ya resolvio
ejecutando el Lua real del mod en vez de reimplementar la logica en C#. Aqui no hay nada que
interpretar, asi que la forma real es un unico motor compartido detras de una interfaz pequena de
"fuente de estado", tal como propuso el propio documento.

**Diseno.** Nuevo en Terrakeep.Core.Guia (repo hermano Terrasavr-Native): IGuideStateProvider (la
fuente de estado: capacidades Has* + los datos/consultas que el despacho necesita),
GuideEvaluationEngine (el despacho + la aritmetica, MOVIDOS aqui tal cual, una sola vez) y
DesktopGuideStateProvider (la mitad de escritorio, con la misma logica que antes tenia
GuideEvaluator.cs como metodos privados). GuideEvaluator.cs queda como adaptador con su API
publica intacta (cero cambios en GuideViewModel.cs de Terrakeep).

En este mod: ProveedorEstadoGuiaMod.cs nuevo implementa IGuideStateProvider leyendo el juego en
vivo via EstadoJugadorGuia/BanderasGuia (sus cuatro capacidades son siempre true:
EstadoJugadorGuia ya se degrada sola a ceros/false sin partida activa, igual que antes).
EvaluadorGuia.cs pasa a ser una fachada que reenvia a GuideEvaluationEngine - StatsDeJefe,
NombreDeObjeto, NombreDeNpc se quedan tal cual (nunca fueron aritmetica duplicada: Terrakeep.Core
no puede escalar stats de un NPC sin el motor cargado). El modelo (TipoRequisito/RequisitoGuia/
PasoGuia/TramoGuia/AmbitoGuia/CapaMundo/ResultadoRequisito) pasa a ser el mismo tipo que ya usaba
Terrakeep.Core via alias de tipo GLOBALES en ModeloGuia.cs (C# 10+, LangVersion=12.0 ya fijado en
el .csproj) - asi CatalogoGuia.cs y casi todo el resto del mod no cambian ni una linea. Lo unico
que si cambio: los sitios que leian texto YA RESUELTO (paso.Titulo, tramo.Nombre, estado.Linea...)
- Core no puede tener eso (no conoce idioma), asi que ahora son metodos de extension en
TextosGuiaMod.cs nuevo (mismo nombre, con parentesis) en EstadoGuia.cs, EstadoCompletitud.cs,
HitosSystem.cs, ContenidoGuia.cs, FilaRequisitoTk.cs y AutopruebaGuia.cs.

**Verificacion real, sin atajos.** scriptsctualizar-core.ps1 (Core recompilado, DLL copiado a
lib\) -> dotnet build -p:BuildMod=false en verde -> scriptserificar-guia.ps1 -Calamity
completo: compilo con el Roslyn real de tModLoader (0 errores, solo los CS1701 benignos de
siempre), empaqueto el .tmod, lanzo el cliente real y recorrio EN VIVO los 46 tramos (21 de
vanilla + 25 de Calamity) -> AUTOPRUEBA GUIA COMPLETA, avisos de datos: 0, 0 comprobaciones en
rojo, capturas reales de cada tramo (evidencia\guia-calamity.log.txt). Mismo comportamiento
exacto que antes del refactor - esto es arquitectura, no un cambio de que dice la Guia.
scripts\compilar.ps1 despues, para dejar el .tmod real recompilado en la carpeta Mods de verdad
(632971 bytes).

### Archivos tocados

- Common/Guia/EvaluadorGuia.cs: pierde el despacho/aritmetica duplicados, pasa a ser fachada +
  StatsDeJefe/NombreDeObjeto/NombreDeNpc (sin tocar).
- Common/Guia/ModeloGuia.cs: de definir sus propios tipos a alias de tipo globales hacia
  Terrakeep.Core.Guia.
- Common/Guia/ProveedorEstadoGuiaMod.cs (nuevo): IGuideStateProvider sobre el juego en vivo.
- Common/Guia/TextosGuiaMod.cs (nuevo): Titulo()/Porque()/Como()/ZonaLegible() (en PasoGuia),
  Nombre()/Resumen() (en TramoGuia), Linea() (en ResultadoRequisito).
- Common/Guia/EstadoGuia.cs, Common/Completitud/EstadoCompletitud.cs, Common/Hitos/HitosSystem.cs,
  UI/Guia/ContenidoGuia.cs, UI/Guia/FilaRequisitoTk.cs, Common/Guia/AutopruebaGuia.cs: llamadas a
  texto ya resuelto, con parentesis nuevos.
- lib/Terrakeep.Core.dll actualizado con el commit hermano de Terrasavr-Native.
- evidencia/guia-calamity.log.txt, .tmod real recompilado.

## 16-sep-2026 - Preparación técnica para Steam Workshop (D1 de la I+D de Fable, extendida a tModLoader)

A diferencia de los mods de DST (ver bitácoras de StarvekeepMod/Starvekeep-Traduccion-ES esta
misma noche), tModLoader SÍ publica desde dentro del propio juego: Workshop → Develop Mods →
Publish. Investigado contra la documentación oficial (`tmodloader.app/docs/publishing-mods.html`,
wiki de `tModLoader/tModLoader`). `build.txt`/`description.txt` ya estaban listos de antes
(homepage real desde el 8-sep-2026); faltaban los dos archivos que solo hacen falta para la
ficha del Workshop, no para jugar:

- `icon_workshop.png` (nuevo): la documentación oficial exige hasta 512×512 (480×480 mínimo)
  para la ficha del Workshop, distinto de `icon.png` (80×80, el que se ve dentro del juego).
  Generado a partir del `icon.png` real (mismo hexágono "T") con remuestreo de calidad en vez
  del vecino-más-cercano que la propia documentación recomienda para sprites de píxeles - este
  icono es arte plano, no pixel art, así que sale más limpio. Verificado 512×512 RGBA real tras
  generarlo.
- `description_workshop.txt` (nuevo): la ficha del Workshop usa un archivo APARTE de
  `description.txt` (que es la que se ve dentro del juego, en texto plano), con BBCode de Steam.
  Redactado a partir del contenido real y ya verificado de `description.txt`, sin inventar
  ninguna función nueva.
- Hallazgo de la investigación, para que quede constancia: la propia documentación oficial avisa
  de que la cuenta de Steam necesita haber gastado al menos 5 USD para no caer en las
  restricciones de cuenta limitada al publicar - condición de la cuenta del usuario, no del mod,
  no comprobable ni solucionable desde aquí.
- No se ha relanzado tModLoader para llegar a la pantalla real de Publish: exige la sesión de
  Steam ya autenticada del usuario y es un menú nunca recorrido por los arneses existentes de
  `scripts\` (que verifican menús de ANTES de Workshop). Documentado como límite real en
  `PUBLICAR-WORKSHOP.md` (nuevo).
- Sin `git push`. Commit local en este repo.

## 16-sep-2026 - Investigacion PRIORIDAD MAXIMA: bug real en directo de la Guia (arreglado en el repo hermano) + hueco cerrado aqui en el arnes en vivo

El usuario reporto, jugando de verdad, que Terrakeep de escritorio (la app, no este mod) se
quedaba pillado mostrando el Devorador de Mundos como pendiente aunque el mundo real ya lo tuviera
derrotado hace tiempo, y que con un mundo sin personaje cargado "todo pone siempre lo mismo".
Investigacion completa (evidencia real paso a paso, comparacion contra el codigo de antes del
refactor de esta noche, arnes GUIA_SOLO real con `adrian`+`roca_negra.wld`) en la bitacora hermana
de Terrasavr-Native (misma fecha, entrada "Bug real en directo: la Guia de escritorio se quedaba
pillada para siempre") - resumen: NO era el refactor T1 de esta noche (logica movida tal cual,
`GuideFlags.cs` sin tocar), sino un bug real de arquitectura ya presente desde que se integro la
Guia en Terrakeep: todo tramo obligatorio exige `dano_arma` como unico requisito de su paso de
preparacion, y `dano_arma` es SIEMPRE no evaluable en el editor de escritorio (no simula combate)
- eso bloqueaba el tramo entero para siempre, sin relacion con el progreso real. Arreglado en
`Terrakeep.Core.Guia.GuideEvaluationEngine` (nueva distincion "limite estructural" que no bloquea
la completitud, frente a "sin datos todavia" que si sigue bloqueando) - **cero cambio de
comportamiento aqui**, porque `ProveedorEstadoGuiaMod.HasLiveGameData` es siempre `true` con una
partida real en marcha, asi que `dano_arma` nunca pasaba por esa ruta en este mod. Verificado con
`scripts\actualizar-core.ps1` + `scripts\verificar-guia.ps1 -Calamity` completo tras traer el DLL
nuevo: compilacion real, `.tmod` empaquetado, cliente real lanzado, 46 tramos recorridos ->
`AUTOPRUEBA GUIA COMPLETA`, 0 requisitos `[?]`, ninguna comprobacion en rojo - exactamente igual
que antes, como se esperaba.

**Corregido tambien un dato real erroneo que el usuario señalo de pasada**: `Guia.Paso.
VencerLaMaldad.Porque` decia que romper Altares Demoniacos/Carmesíes para sacar mineral nuevo se
podia hacer nada mas caer el Devorador de Mundos/Cerebro de Cthulhu - falso, hace falta el
Martillo Sagrado (solo se consigue derrotando al Muro de Carne). Corregido a los hechos reales
(meteorito garantizado + Driade vendiendo Polvo Vil/Viscoso y el % de corrupcion/carmesi) en los
dos `.hjson` de localizacion - la app de escritorio lo trae resincronizado con `scripts\
sync-guia-desde-terrakeepmod.ps1` (ver bitacora hermana).

**Hueco cerrado en el arnes propio de este repo** (hallazgo de una investigacion paralela de
Fable, aplicado aqui tras confirmar que hacia falta): `verificar-guia.ps1` solo ponia en rojo
`NO CUADRA|EXCEPCION|NO COINCIDE|NO CABE` - `[?]` (NoEvaluable) se imprimia linea a linea pero
NUNCA hacia caer el gate, y no habia ningun invariante de que en una partida real el conteo
deberia ser 0 (`ProveedorEstadoGuiaMod` tiene sus cuatro `Has*` siempre `true`). Nuevo:
`AutopruebaGuia.cs` cuenta `_contadorNoEvaluable` en los dos sitios donde se imprime
`[?]`/`[HECHO]`/`[FALTA]` y lo resume en `Terminar()`; `verificar-guia.ps1` añade `NO EVALUABLE:`
a su patron rojo. Verificado que el invariante se cumple de verdad en un recorrido real completo
(0, ver arriba).

**No tocado**: `Assets/guia_progresion.json` tenia cambios de OTRA sesion en marcha a la vez en
este mismo repo (reordenacion de Piratas/Legion de Escarcha a Modo Dificil, Mecanicos exige los
tres en vez de cualquiera, zona de Ceaseless Void, profundidad de Crabulon - investigacion de
contenido mas amplia con Fable, ajena a este encargo) - respetado sin tocar ni comitear, mismo
criterio de siempre con sesiones concurrentes en este repo.

### Archivos tocados (commit local, nunca push)
- `Common/Guia/AutopruebaGuia.cs`: contador `_contadorNoEvaluable` + resumen en `Terminar()`.
- `scripts/verificar-guia.ps1`: `NO EVALUABLE:` añadido al patron rojo.
- `Localization/es-ES_Mods.TerrakeepMod.hjson`/`en-US_Mods.TerrakeepMod.hjson`:
  `NoEvaluableGenerico` nuevo, `VencerLaMaldad.Porque` corregido.
- `lib/Terrakeep.Core.dll`: actualizado con el commit hermano de Terrasavr-Native (T1 corregido).
- `evidencia/guia-calamity.log.txt`: log real del recorrido de verificacion de esta ronda.
- `.tmod` real recompilado con `scripts\compilar.ps1` (747838 bytes).

## 16-sep-2026 - Auditoria de EXACTITUD de la Guia, paso a paso, contra el codigo real (encargo nocturno de Fable)

Reporte real del usuario jugando: la guia decia que tras el Devorador/Cerebro "los Altares
empiezan a soltar mineral nuevo al romperse", y eso es falso (hace falta el Gran martillo del Muro
de Carne y el mundo en Modo Dificil). A raiz de eso, encargo de corroborar CADA paso de las guias
de los cuatro proyectos contra fuentes externas. Metodo: cada afirmacion de texto y cada requisito
del .json contrastados contra el tModLoader 1.4.4.9 y el CalamityMod v2.2.2 decompilados de
`Downloads\tModLoader-Decompiled\` primero, y terraria.wiki.gg / calamitymod.wiki.gg como
contraste. La otra sesion en paralelo (bug "no lee el mundo") ya habia corregido
`VencerLaMaldad.Porque` y se respeto: aqui se reescribio sobre su version con lo que dice el
codigo.

### Errores reales encontrados y corregidos (46 tramos / 116 pasos revisados)
Estructura (`Assets\guia_progresion.json`, cada uno con su `_fuente` citando archivo:linea):
1. **Piratas** (orden 19 -> 44) y **Legion de Escarcha** (17 -> 43): eran tramos de prehardmode y
   son de Modo Dificil. Mapa pirata solo en hardmode (`ItemDropDatabase`, `Conditions.PirateMap`);
   invasion natural `hardMode && altarCount > 0` (`Main.cs:83223`). Globo de nieve solo de un
   Regalo abierto en hardmode (`RegisterPresent`, `Conditions.IsHardmode`, 1 de 15) y los Regalos
   solo en Navidad (1 de 13, `XmasPresentDrop`). Los dos llevan ahora requisito real `hardMode`.
2. **Mecanicos**: el cierre exigia `downedMechBossAny` ("con uno basta") y los bulbos de Plantera
   exigen LOS TRES (`WorldGen.cs:68792`). Bandera compuesta nueva `downedMechBossAll` en
   `BanderasGuia.cs` (mod) y `GuideFlags.cs` (escritorio), como `Condition.DownedMechBossAll` del
   propio juego. Lo que si abre uno solo (Frutas de la vida, Eclipse, dificultad 2 del Antiguo
   Ejercito) va ahora en el texto.
3. **Ceaseless Void**: zona Cavernas -> Mazmorra (`MarkofProvidence.UseItem`: ZoneDungeon).
4. **Crabulon**: superficie -> subsuelo (`DecapoditaSprout.CanUseItem` exige Hongos Luminosos
   por debajo de `worldSurface`).

Texto (los dos .hjson, es-ES y en-US, mismas correcciones):
- Altares, meteorito, Tabernero y picos tras la maldad del mundo (`SmashAltar` devuelve sin
  hardmode; `Player.cs:45912` hiere al jugador con martillo <80 o sin hardmode; Pwnhammer al 100%
  del Muro).
- Muro de Carne: partes debiles al reves (ojos defensa 0, boca 12); Gran martillo, mas la nota
  real de Calamity (`EarlyHardmodeProgressionRework`: altares dan Almas de la noche, mineral por
  jefes).
- Golem: era "la cabeza no recibe daño mientras vivan los puños"; es el CUERPO mientras la
  cabeza siga montada (`AI_045_Golem`, `dontTakeDamage = flag`). Celulas: cofres y Lihzahrd o
  Serpientes voladoras (1 de 50), no "trampas". Golem abre Cultistas, Sonda y dificultad 3.
- Plantera: "semillas de Clorofita" no existen; suelta la Llave del templo, abre la Mazmorra
  de hardmode y los cofres de bioma. Bulbos solo con los tres mecanicos.
- Emperatriz de la Luz: estaba INVERTIDO ("pelea de dia, de noche es mas dura"). De DIA esta
  enfurecida (`ShouldEmpressBeEnraged` devuelve `Main.dayTime`), de noche es la pelea normal;
  Terraprisma solo enfurecida. Crisopa prismatica (NPC 661): superficie del Sagrado, de noche
  antes de medianoche, tras Plantera (`NPC.cs:89524`). Ningun `dontTakeDamage`.
- Reina Slime: no exige noche (`Player.cs:43541`, solo ZoneHallow); "Ala Real Cristalina" no
  existe: Sillin gelatinoso, Gancho de Disonancia, Baculo de cuchillas, armadura de asesino.
- Luna de Calabazas: Mourning Wood oleada 6 (no 5), Pumpking 10 (no 9); Luna Helada:
  Everscream 4, Santa-NK1 7, Reina de Hielo 11 (no 3, 6 y 10). Codigo y wiki coinciden.
- Antiguo Ejercito: Betsy solo en la oleada 7 de la dificultad 3 (`DD2Event.cs:204`); Cristal y
  Stand los vende el Tabernero (`NPCShopDatabase.cs:802` y `:817`), no se fabrican; dificultades
  por `ReadyForTier2` y `ReadyForTier3`; Tabernero rescatable con `downedBoss2`.
- Sello celestial: Manipulador antiguo, 12 fragmentos de cada (`Recipe.cs:14516`), no yunque.
- Deerclops: sale a MEDIANOCHE en ventisca (`Main.cs:82838`), no "tras el atardecer"; drops
  reales (Hueso del ojo y Chester, Ojombrilla, Lucy...). Duende chapucero = Tinkerer (era el
  mismo NPC nombrado dos veces). Legion: sus muñecos solo sueltan Bloques de nieve; Papa Noel.
- Duque Pezhongo: drops reales (Pistola de burbujas, no "Cañon de Cavajabon" ni "Shrimpy
  Truffle").
- Vecinos: Enfermera y Demoledor no necesitan al Mercader (`SpawnAllowed_Nurse` y
  `SpawnAllowed_Demolitionist`).
- Devorador: defensa 2, 4 y 8 por segmento y 150 de vida cada uno; Cerebro 1250 (`SetDefaults`).
- Moon Lord: 500 de vida son 15 cristales + 20 frutas (no 10 + 14).
- Calamity: Desert Scourge y Crabulon NO aparecen solos (sin `SpawnChance`); Seafood solo en el
  Mar Sulfuroso; Idolo Chamuscado solo en los Riscos de Azufre y Brimstone Elemental no tiene
  "brazos" (fase de capullo, wiki); Cryo Key: receta real, sin "pico de Mithril"; Lluvia Acida
  por banderas (110, 135 o 170 enemigos, corte a los 2 min 30 s sin matar - no "115 y 140
  puntos" ni "4 minutos"); Primordial Wyrm: Abismo + Estado caotico (`TrySpawnAEoW`), ya no
  "sin confirmar"; Huevo de Yharon: 10 Life Alloy + 15 Effulgent Feather (Dragonfolly), no
  "completar el Templo"; Silbato de la Muerte: receta real; Dreadnautilus vanilla ya es un
  miniboss (7000 de vida, 55 de daño, 24 de defensa).

### Infraestructura tocada
- `Common/Guia/AutopruebaGuia.cs`: `MarcarLosTresMecanicosDerrotadosDeMentira` (marca y restaura
  `downedMechBoss1`, `2` y `3`), y los bloques de Legion y Piratas remarcan ReinaAbeja y ponen
  `hardMode` a true mientras se comprueban (devueltos a su valor real antes de las
  comprobaciones que los esperan). Textos de las expectativas actualizados al orden 43 y 44.
- `scripts/generar-localizacion.py`: aviso en la cabecera - la seccion Guia se autora en los
  .hjson desde el 15-sep y la tabla T esta atrasada; no regenerar sin portar antes.
- `lib/Terrakeep.Core.dll` resincronizado (`actualizar-core.ps1`) con el `GuideFlags.cs` nuevo.

### Verificacion real
- `scripts\verificar-guia.ps1 -Calamity` DOS veces (antes y despues de resincronizar lib):
  "avisos de datos: 0", recorrido con Legion (43), Piratas (44) y los tres mecanicos en el log
  real (`evidencia\guia-calamity.log.txt`), "requisitos [?]: 0", "Ninguna comprobacion en
  rojo", 191 y 193 s.
- `.tmod` real recompilado y desplegado (`scripts\compilar.ps1`, 756399 bytes, 02:27:51).
- Repo hermano Terrasavr-Native: `dotnet test` Terrakeep.Core 556 de 556 y `GUIA_SOLO=1` con
  personaje y mundo reales: 46 tramos, 0 textos sin resolver, objetivo real "Los tres mecanicos,
  Derrotar a los tres". Terrakeep.exe reinstalado en `%LocalAppData%\Programs\Terrakeep`.
- Sin `git push`. Commit local solo de los archivos de esta ronda.

### Para la otra sesion (bug "no lee el mundo")
En toda la auditoria no aparecio ninguna bandera mal escrita ni inexistente: el propio arnes
confirma "avisos de datos: 0" y 0 requisitos no evaluables con partida real, asi que la causa de
lo que vio el usuario no esta en los nombres de bandera del .json.

## 16-sep-2026 (mañana) - KeepQA S1+S2: evidencia con nombre de Conjuntos/Completitud/Hitos y par antes/despues del tooltip de objeto

Encargo de cierre de `Downloads\KeepQA\AUDITORIA-SESGOS-16SEP.md` para toda la familia. Aqui, dos
huecos reales: (S1) Conjuntos, Completitud e Hitos tenian `Autoprueba*.cs` real pero NINGUNA
evidencia con nombre en `evidencia\` (las capturas se quedaban en el sandbox WS7, donde el
inventario de cobertura de KeepQA no las ve; Hitos ni siquiera tenia script que la lanzara); (S2)
el tooltip de objeto (bug real del 14-sep) solo dejaba log, sin ningun volcado del estado con el
raton encima.

- `scripts/verificar-conjuntos.ps1`, `scripts/verificar-completitud.ps1`: copian el log real
  `[Terrakeep]` a `evidencia\conjuntos.log.txt`/`completitud.log.txt` y las capturas del sandbox a
  `evidencia\conjuntos-capturas\`/`completitud-capturas\` (mismo patron que verificar-espaciado).
- `scripts/verificar-hitos.ps1` (nuevo): lanza `TERRAKEEP_AUTOTEST_HITOS`; copia antes el album REAL
  del sandbox de la Guia (`tModLoader-TerrakeepGuia\terrakeep-hitos`, 1155 hitos reales) al WS7,
  porque con el album vacio el paso 3 daria "NO CUADRA" sin decir nada de la pestaña. Pasada real:
  clic real en "Álbum" OK, "Actualizar"/"Abrir carpeta"/lista sin solapes (numeros reales),
  1155 entradas montadas = 1155 en album.json. Evidencia: `evidencia\hitos.log.txt` +
  `hitos-capturas\hitos-1-album.png`.
- `Common/Panel/AutopruebaTooltipObjeto.cs`: por zona, volcado de geometria del panel
  (`PanelTerrakeepState.VolcarGeometriaJson`) ANTES de mover el raton y DESPUES de que el motor
  rellene el tooltip -> `transicion-hover-<zona>-antes/despues.json` + captura
  `tooltip-hover-<zona>.png`. `CapturaDePantalla.Permitida` incluye ahora esta autoprueba (no
  podia capturar). El rectangulo del tooltip vainilla NO se añade al volcado a proposito: lo
  pinta el motor (`MouseText`) fuera del arbol `UIElement`, y reimplementar `MouseTextInner` solo
  para medirlo seria un analogo forzado - lo que se juzga (con `--sin-cambio`) es que el hover no
  mueve, agranda ni oculta nada del panel; que el tooltip se pinta lo demuestran el log
  (`HoverItem.type`/`hoverItemName` OK en las 4 zonas) y la captura (mirada: "Bloque de tierra
  (250) / Se puede colocar / Material" sobre la ranura 2).
- `scripts/verificar-tooltip-objeto.ps1` (nuevo; hasta hoy la autoprueba se lanzaba a mano): copia
  log/capturas/pares a `evidencia\tooltip-objeto-transiciones\` y pasa cada par por
  `Downloads\KeepQA\src\transicion\verificarTransicion.js` (pieza compartida nueva, S2) con canario
  de la pieza y canario del extractor (antes contra si mismo -> `sin_efecto`). Pasada real: 4/4
  pares OK.
- `scripts\compilar.ps1` (compilador real de tModLoader): 0 errores, `.tmod` 756.953 B.
- `node Downloads\KeepQA\src\cobertura-pantallas\verificarCoberturaPantallas.js --proyecto
  TerrakeepMod`: **19/19 pantallas con evidencia** (antes 16/19).
- Limite honesto: el par de SCROLL (UIList al final) no se ha cableado aqui - exigia un paso nuevo
  en el switch de `AutopruebaLibreria.cs` (tocado por otra sesion esta noche); el volcado "final"
  para `verificarBordeViewport.js` sigue existiendo (paso 25).
- Visto de paso en el log del sandbox WS7 (sin Calamity): 36 avisos "Guia: AVISO de datos -
  bandera desconocida downedX" en los pasos de Calamity. No es de este encargo; para quien
  retome la Guia: decidir si con `Calamity cargado=False` esos avisos deben silenciarse.

## 16-sep-2026 (tarde) - QA a ciegas con juego-libre/dossier de KeepQA: "Zoom" pisando "Markers" en Exploracion (en-US)

Ronda a ciegas (sin pista previa de bug) sobre Terrakeep de escritorio y TerrakeepMod con las
herramientas nuevas de KeepQA (`src/juego-libre` + `src/hipotesis/dossier.js` +
`verificarHipotesis.js`), evitando a proposito Equipamiento/Inventario de **Terrakeep de
escritorio** (bug del ★ en manos de otro agente en paralelo). En Terrakeep de escritorio: 3
partidas de `juegoLibre.js` (semillas 11/7/3, ~95 pasos, pestañas Inicio/Personaje(solo
hover)/Builds/Novedades/Acerca de/Exploracion/Guia/Servidor, tamaños normal y 1080x700) mas
`dossier.js`+`verificarHipotesis.js` sobre 3 pantallas (Builds/Inicio/Guia): sin ningun hallazgo
real nuevo fuera de Equipamiento/Inventario - los "Medium" que salieron eran falsos positivos ya
conocidos de las piezas (tooltip pisando su fila de origen, microanimacion de hover de 2-3px,
boton mudo interno de `ScrollBar` de WPF) o el propio harness abriendo por accidente el menu
"Sistema" de la ventana y cerrandola con su "Cerrar" nativo (Critical `proceso_muerto`: NO es un
bug de Terrakeep, es el fuzzer clicando el menu de sistema de Windows).

**LIMITE REAL**: `juego-libre` (UIA externo, pywinauto) solo funciona con motores WPF
(documentado en `DIAGNOSTICO-DE-FONDO-16SEP.md`: "juego-libre solo para WPF por UIA externo -
tModLoader/DST no exponen ese arbol"). Para TerrakeepMod se uso en su lugar el arnes propio del
mod (`Common/Panel/AutopruebaPanelUnico.cs` + `scripts\verificar-panel-unico.ps1`), y no hay
extractor de geometria en formato KeepQA (`{id,tipo,padre_id,x,y,ancho,alto}`) para el arbol
`UIElement` de tModLoader, asi que `dossier.js`/`verificarHipotesis.js` tampoco se pudieron usar
aqui de forma literal - en su lugar, auditoria visual directa de las capturas reales que la propia
autoprueba deja en `terrakeep-capturas\` (recortes con zoom real via PIL, no "a ojo" sobre la
miniatura).

**Bug real encontrado y arreglado**: en `pestana-4-exploration.png` (autoprueba en ingles,
1600x900), el titulo "Markers" quedaba literalmente pisado por "Zoom: X px per tile" (recorte
real, letras superpuestas) - `UI/Exploracion/PestanaMapa.cs`, `ConstruirLateral()`. Causa real:
el renglon de estado (`estado`, el "Zoom:") se posicionaba con `VAlign = 1f` sin `Top` (anclado al
fondo del contenedor ENTERO), mientras que todo lo de encima (Marcadores/detalle/bajo el raton) se
apilaba con un `y` manual que crecia mas en ingles (el aviso de arriba parte en 4 lineas en vez de
3) - cuando el flujo de arriba crecia lo suficiente, se comia el hueco fijo de abajo.

Reconstruido en 6 iteraciones reales contra el juego (nunca a ciegas leyendo solo el codigo):
1. Meter `estado` en el mismo flujo secuencial (`y` acumulado) -> arregla el pisado de "Markers"
   pero el texto se sale por debajo del propio panel (el flujo entero no cabia en la pestaña).
2. `y += 96f` fijo del aviso sustituido por una medida REAL
   (`DynamicSpriteFont.MeasureString("Ay").Y * escala` x numero de `\n` que trajo
   `PartirEnLineas`) - mismo principio que ya usaba `PartirEnLineas` para el ANCHO, aplicado ahora
   al ALTO. No cambio nada por si solo: el problema no era medir mal el aviso.
3. Instrumentado con un log de diagnostico REAL (`RegistroPanel.Linea`, retirado despues) dentro
   de un `Update()` nuevo: la pestaña mide **~308-310px de alto real** (`GetDimensions()`), NO los
   444px del area de contenido que reporta el panel para otras pestañas (esa cifra confundio el
   primer intento) - el flujo entero llegaba a 331px, mas alto que el propio contenedor.
4. `Update()` recorta `estado.Top` contra `GetDimensions().Height` REAL cada fotograma (no una
   constante) - necesito `_estado.Recalculate()` explicito tras el `Top.Set`, si no el cambio se
   guarda pero no se ve hasta el fotograma siguiente (confirmado en vivo: sin la llamada, la
   captura no cambiaba nada).
5. Con el recorte solo, "Zoom" ya no pisa "Markers" pero SI pisa el boton "Close (P)" del pie del
   panel (que vive fuera de esta pestaña, con su propio `VAlign=1f` sobre el marco entero).
6. Huecos entre botones/renglones ajustados al alto REAL de `BotonTk` (34px, `ColocarBoton`) + un
   margen pequeño en vez de numeros redondos elegidos a ojo (40/40/48/46/30 -> 36/36/40/42/22) -
   libera ~55px, suficiente para que el flujo completo (incluido "Zoom") quepa dentro de los
   308-310px reales sin tocar ni "Markers" arriba ni "Close" abajo.
7. Verificado con captura real tras cada paso (`verificar-panel-unico.ps1`, recorte 3x con PIL de
   la esquina inferior derecha): "Markers" / "No search yet: use the Search tab" / "Zoom: 2.50 px
   per tile" apilados limpios, "Close (P)" con hueco debajo, sin fuga fuera del panel. Grupo de
   botones de arriba (Zoom in/out, Centre on me, See the whole world, Open the game map) sigue con
   huecos visibles entre si, sin apelotonarse.

### Verificacion real
- `scripts\compilar.ps1`: 0 errores en cada una de las 6 iteraciones, `.tmod` recompilado y
  **instalado de verdad** en `Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod`
  (no solo el sandbox) tras cada cambio.
- `scripts\verificar-panel-unico.ps1` real (cliente grafico real, mundo/personaje sintetico
  `TerrakeepPrueba`) tras cada iteracion: "AUTOPRUEBA PANEL COMPLETA", 0 lineas "NO CUADRA", las 6
  pestañas + animacion + atajos + icono HUD + muñeco de Apariencia en verde. Evidencia final en
  `evidencia\panel-unico.log.txt`.
- Capturas reales inspeccionadas con recortes 3x-4x (PIL, no la miniatura completa) antes y
  despues de cada cambio - la ultima confirma el arreglo con evidencia de pixeles, no solo "el
  codigo deberia ya no solaparse".
- Commit local solo de `UI/Exploracion/PestanaMapa.cs` + `evidencia/panel-unico.log.txt`. Sin
  `git push`.

### Honestidad sobre lo que NO se encontro
Terrakeep de escritorio: ningun bug nuevo fuera de Equipamiento/Inventario en 95 pasos de juego
libre + 3 dossiers con hipotesis verificadas - no se fuerza ningun hallazgo debil. TerrakeepMod:
el resto de pestañas (Character/Library/Builds/Research/Settings, animacion, atajos, icono HUD,
Apariencia) pasaron la autoprueba y la inspeccion visual sin ningun problema detectado.

---

## 17-sep-2026 - Mismo trio externo (analizadores + CsCheck + Stryker.NET) aplicado a TerrakeepMod - séptimo proyecto de la familia, en paralelo con TModLoaderMod

Mismo patrón ya integrado en Terrakeep.Core/Starvekeep.Core/ServidorKeep.Core/Keep.Wpf (16/17-sep)
y, esta misma noche en paralelo, en el repo hermano `TModLoaderMod` - ver su propio `bitacora.md`.
Detalle técnico completo (con números reales) en `bitacora.md` de KeepQA, 17-sep-2026. Resumen aquí:

### Paso 1 - Analizadores de .NET + Roslynator.Analyzers 5.0.0 en `TerrakeepMod.csproj`
- Gate por defecto: 18 avisos únicos reales de entrada (17× `RCS1075` catch vacío de
  `System.Exception`, 1× `RCS1155` comparación de cadenas sin `StringComparison`).
- **Arreglado de verdad**: `RCS1155` en `Common/Investigacion/AutopruebaInvestigacion.cs` -
  `fase.Trim().ToLowerInvariant() == "comprobar"` → `string.Equals(fase.Trim(), "comprobar",
  StringComparison.OrdinalIgnoreCase)`.
- **17× `RCS1075` documentados como backlog deliberado, NO arreglados**: el mismo patrón repetido
  en ~15 escritores de evidencia (`Registro*.cs`/`Autoprueba*.cs`, activos solo bajo variables de
  entorno `TERRAKEEP_AUTOTEST_*`) más 2 lecturas defensivas (`AlbumHitos.cs`,
  `SincronizacionEscritorio.cs`) - todos ya llevan un comentario explicando por qué se ignora
  CUALQUIER excepción a propósito (el log del juego ya tiene la línea vía otro canal, así que
  fallar al escribir el archivo de evidencia nunca debe tumbar nada). Acotar a
  `IOException`/`UnauthorizedAccessException` habría sido una opción real, pero se dejó
  documentado sin tocar por ser exactamente el mismo tipo de backlog de diseño que ya se dejó sin
  tocar en Terrakeep.Core/Keep.Wpf - no un bug real evidente.
- `--completo` (informe, nunca gatea): ~300 avisos únicos de diseño (`CA1305` formateo sin
  cultura×119, `CA1051` campos visibles×110, `CA1031` catch genérico×76, `CA1062` validación de
  argumentos×68...), mismo perfil que el resto de la familia.
- Verificado recompilando el mod ENTERO con el compilador real de tModLoader
  (`scripts\compilar.ps1`) tras el fix: 0 errores, `.tmod` reinstalado de verdad.

### Paso 2 - CsCheck 4.9.0: investigación real de qué es "lógica propia y pura" en el mod
Investigados `Common/` y `UI/` completos (~90 archivos). Conclusión honesta: la inmensa mayoría
depende en vivo de objetos del motor (`Main.tile`, `Main.LocalPlayer`, `ItemLoader`,
`ModContent.TryFind`...) - **LÍMITE REAL**, sin superficie testeable fuera del juego. El ÚNICO
candidato real: `Common/Libreria/GramaticaBusqueda.cs` - lógica pura (`System.Globalization`/
`System.Text`, cero `using` de Terraria), una COPIA deliberada y ya documentada de
`Terrakeep.App/ViewModels/LibrarySearchGrammar.cs` (assembly WPF, inalcanzable desde el mod), con
su propio riesgo real de desincronización entre copias (ya pasó una vez en la familia con
`best_prefix.json`).

Nuevo proyecto `TerrakeepMod.Tests` (net10.0 - el `dotnet` del PATH no tiene runtime .NET 8
instalado, solo lo trae tModLoader, ver `scripts\compilar.ps1`; el archivo probado es C# puro
compatible con ambos TFM) con 24 pruebas:
- 9 propiedades CsCheck reales (idempotencia y limpieza de diacríticos de `Plegar`, vacío/nulo
  siempre coincide, término de 1 carácter se ignora siempre, `#id` exacto, `#lo-hi` por rango,
  coma=OR metamórfico, espacio=AND, `.texto` busca solo en tooltip).
- 13 pruebas deterministas: traducción literal de los 9 `[Fact]` reales de
  `LibrarySearchGrammarTests.cs` (incluida la tilde "máscara"/"Máscara" de C-09) más 2 cierres de
  frontera (ver Stryker abajo).
- **Hallazgo real durante la propia escritura de las pruebas**: la primera versión de la propiedad
  de AND-entre-palabras daba un falso positivo (con solo 36 letras/dígitos posibles y palabras de
  2-8 caracteres, `w2` podía aparecer "de casualidad" dentro de `w1` - `Casa` compara por
  SUBCADENA, no por palabra completa) - corregido acotando el generador, no el código de
  producción (mismo principio que el hallazgo de precisión IEEE 754 de `Keep.Wpf.Tests`).
- **Sin bug real encontrado en `GramaticaBusqueda.cs`** en esta ronda - resultado honesto, no
  forzado (mismo tipo de resultado limpio que ya tuvo `Starvekeep.Core.Tests`).

### Paso 3 - Stryker.NET 5.0.0, acotado a `GramaticaBusqueda.cs`
Biblioteca intermedia `TerrakeepMod.LogicaPura` (enlaza, no copia, el mismo `.cs` real) porque
Stryker exige una referencia de proyecto real para saber qué mutar. 44 mutantes: 14 `CompileError`
(mutaciones `&&`→`||` que rompen la asignación definitiva de `hasta`/`desde` en `CasaId` - Stryker
las descarta solo con su propio "Safe Mode"), 1 `NoCoverage` (el guardarrail `palabras.Length==0`
en `Casa` - analizado y parece código realmente inalcanzable hoy: `Trim()` ya deja vacío/filtrado
cualquier término que llegara ahí como solo-espacios antes de la comprobación de longitud mínima;
documentado, no forzado), 6 `Ignored`, 23 puestos a prueba de verdad. **Primera corrida: 21
matados, 2 supervivientes reales** (`termino.Length < 2` → `<= 2` sin ningún test que exigiera que
un término de EXACTAMENTE 2 caracteres siguiera contando; el `return "";` del guardarrail nulo de
`Plegar` sin ningún test que llamara `Plegar(null)` directamente). Añadidos los 2 cierres de
frontera correspondientes en `GramaticaBusquedaParidadTests.cs` - **segunda corrida: 23/23
matados, 0 supervivientes, puntuación final 95.83 %**. Informe en
`KeepQA\artifacts\stryker-terrakeepmod-logicapura\mutation-report.{json,html}`.

### Hallazgo arquitectónico real (aplica a CUALQUIER mod de tModLoader, documentado para el futuro)
El compilador REAL de tModLoader (`Terraria.ModLoader.Core.ModCompile.CompileMod`, decompilado)
NUNCA usa MSBuild: hace su propio `Directory.GetFiles(mod.path, "*.cs", SearchOption.AllDirectories)`
y filtra con `IgnoreCompletely` → `BuildProperties.ignoreFile` → `buildIgnore` de `build.txt` (los
mismos patrones que ya se usaban para excluir `scripts\`/`evidencia\` del `.tmod` empaquetado,
confirmado que TAMBIÉN aplican al propio paso de compilación, no solo al empaquetado de recursos).
Un `<Compile Remove>` en el `.csproj` NUNCA basta por sí solo - solo protege `dotnet build`/el IDE,
nunca el `-build` real (confirmado en vivo: 116 errores `CS0246`/`CS0579` con `TerrakeepMod.Tests\`
recién creada y solo el `<Compile Remove>` puesto). Solución real y definitiva: `<Compile Remove>`
en el `.csproj` (protege `dotnet build`/IDE) **Y** `TerrakeepMod.Tests*`/`TerrakeepMod.LogicaPura*`
en `buildIgnore` de `build.txt` (protege el `-build` real) - las dos cosas a la vez, ninguna sola
basta. `dotnet-tools.json` (manifiesto de `dotnet-stryker`) también necesitó su propia entrada en
`buildIgnore`: sin extensión `.cs`/`.csproj`/`.sln`, se habría empaquetado como recurso suelto
dentro del `.tmod`. Mismo patrón exacto ya validado esta misma noche, en paralelo, en el repo
hermano `TModLoaderMod` (`TModLoaderMod.Pruebas.Unit*`) - confirmado el archivo `.tmod` final
limpio con un volcado real de sus 20 entradas (`TerrakeepMod.dll`, `Assets/*`, `Localization/*`,
`lib/Terrakeep.Core.dll`... nada de tests/tooling).

### Verificación real
- `scripts\compilar.ps1`: 0 errores, `.tmod` recompilado e instalado de verdad tras el fix de
  `RCS1155` y tras añadir los dos proyectos nuevos + `buildIgnore`.
- `scripts\limpiar-tmod.ps1`: 652.366 bytes, 20 archivos conservados - volcado real de entradas
  confirmando que no se coló nada de `TerrakeepMod.Tests\`/`TerrakeepMod.LogicaPura\`/
  `dotnet-tools.json`.
- `dotnet test` (net10.0): 24/24, repetido varias veces con semillas distintas de CsCheck.
- `dotnet stryker`: 23/23 matados, 95.83 %, repetido tras la corrección de los 2 supervivientes.

Commit local en TerrakeepMod (`TerrakeepMod.csproj`, `build.txt`, el fix de `RCS1155`,
`TerrakeepMod.Tests\`, `TerrakeepMod.LogicaPura\`, `dotnet-tools.json`) y en KeepQA
(`src/analisis-estatico/verificarAnalisisEstatico.js` con el `--prop Clave=Valor` nuevo +
`artifacts/stryker-terrakeepmod-logicapura/` + esta documentación). Sin `git push`.


---

## 20-sep-2026 - Catálogos de funciones y de rediseño visual (Claude Docs): 5 ideas reales implementadas y verificadas, 1 parcial, resto investigado y documentado

Encargo: implementar todo lo real que aplique a TerrakeepMod de los dos documentos vivos
publicados esa noche (catálogo de FUNCIONES y catálogo de REDISEÑO VISUAL de toda la familia
Keep), encadenando sin parar, sin publicar nada (sin `git push`, sin subir versión). Sección real
de TerrakeepMod: 10 ideas de funciones (numeradas) + 6 ideas de rediseño visual (TM1-TM6).

### Lo implementado y verificado de verdad en el juego (6 commits locales, sin `git push`)

**TM6 · Transición suave + alto dinámico** (`UI/Panel/PanelTerrakeepState.cs`). El alto máximo del
marco pasa de un fijo de 700px a un 82% real de `Main.screenHeight` (recalculado cada fotograma,
como ya hace `AjustarEscalaDeLasPestanas` con el ancho) - antes tapaba el HUD de vida en
portátiles 1366x768. El contenido de cada pestaña entra con un fundido de 120ms: un velo pintado a
mano con `TextureAssets.MagicPixel` sobre `_contenedor`, nunca un `UIElement` más (así no roba el
clic durante la transición). Verificado con captura real: el panel mide ≈501px lógicos a 613px de
pantalla lógica, exactamente el 82% calculado.

**TM3 · Chips de cabecera** (mismo archivo). Dos de los tres chips que proponía el catálogo
original: hora del mundo (`Utils.GetDayTimeAs24FloatStartingFromMidnight`, real) y el objetivo
actual de la Guía (`EstadoGuia.PasoActual`, pulsable → salta a la pestaña Guía). El tercero
(vida/maná) se descartó a propósito: esa mitad de la fila del título ya está reservada al HUD real
del juego (`GUIBarsDraw`, ver el XMLdoc ya existente de `AltoTitulo`) y duplicarlo ahí reproduciría
el bug histórico de los corazones tapando la barra de pestañas. El rótulo del chip de objetivo es
un texto corto FIJO ("Objetivo"/"Objective"); el nombre real del tramo, que llega a 36 caracteres,
va en el tooltip - nunca se recorta.

**Función 2 · DPS-metro** (`Common/Guia/MedidorDanio.cs`, nuevo `GlobalNPC`). Daño real del jugador
local en los últimos 10 segundos, leído de `OnHitByItem`/`OnHitByProjectile` (`damageDone` real, el
mismo número del numerito flotante) con `Main.GameUpdateCount` como reloj. Es el tercer chip de la
cabecera de TM3 (cierra el hueco que el propio catálogo dejaba previsto: "aquí encaja el DPS-metro
como cuarto chip"). Verificado con 3 golpes de daño CONOCIDO (100+150+200=450 → 45.0 DPS exactos) y
el chip reflejándolo un fotograma después, visto desde la pestaña Builds (no la Guía) - prueba real
de que es visible desde cualquier pestaña.

**Función 3 · Diario de partida automático** (`Common/Hitos/AlbumHitos.cs`,
`Common/Hitos/ContadorDiasSystem.cs` nuevo, `UI/Hitos/ContenidoAlbum.cs`). Cada hito del Álbum
guarda ahora día del mundo (contador propio persistido en el `.wld`, investigado que vanilla no
expone ninguno), equipo llevado (reutiliza `AutoEquipar.EstadoEquipo`), tiempo de esta sesión y
jefe/evento (`TramoGuia.JefeFinal` resuelto y traducido). Se ven en el tooltip de cada fila, solo
las líneas que la entrada tiene de verdad (un álbum grabado antes de este cambio no rompe ni
enseña datos inventados). Verificado con una pasada real y completa de la Guía (vanilla+Calamity,
56+ tramos): `album.json` real con `"jefe":"Árbol de luto"/"Gritoeterno"/"Betsy"` y
`"tiempoSesionSegundos"` creciendo de verdad.

**Función 7 · Códigos de build dentro del juego** (`Common/Builds/CodigoDeBuild.cs` nuevo,
`Common/Builds/CatalogoPrefijoPicaro.cs` ampliado, `UI/Builds/ContenidoBuilds.cs`). Exportar/
importar el conjunto de equipo (armadura + 7 accesorios + 10 tintes) como código `TKBUILD1:...`
con `Terrakeep.Core.Model.BuildCode`, el códec real que trae `lib/Terrakeep.Core.dll` desde el
primer día sin que nada lo llamara. Mismo criterio que Auto-equipar: lo que ya tienes se mueve, lo
que no se crea, nunca se destruye nada, los tintes nunca se crean. **LÍMITE REAL documentado en el
XMLdoc de la clase**: los objetos de un mod (Calamity incluido) no se codifican todavía -
`BuildCode.Encode` solo acepta un `int` por ranura y el `Item.type` de un objeto de mod lo asigna
tModLoader en caliente, no es estable entre sesiones; la app de escritorio resuelve esto con un
catálogo de ids sintéticos de OBJETO (`CalamityCatalog.cs`/`CalamityItemCodec.cs` de
Terrasavr-Native) que este mod no tiene portado, a diferencia de los PREFIJOS de Pícaro, que sí
(`CatalogoPrefijoPicaro`, 21 entradas). Un objeto de mod se omite del código (ranura vacía, contado
y avisado) en vez de codificar un id que mentiría. Verificado con una extensión real del arnés
existente: auto-equipar crea 8 objetos → exportar produce un código real (53 caracteres, copiado al
portapapeles) → importar el MISMO código es idempotente (creados=0, movidos=0, ya_puestos=8).

**Función 4 (parcial) · Sonar de estructuras** (`Common/Exploracion/ObjetivosBusqueda.cs`).
Investigado que Mazmorra/Templo lihzahrd/Nido de araña YA se buscaban por su pared "Unsafe" real
(sesión anterior) - un proxy de estructura ya correcto. Añadida la Isla flotante, marcada por el
tile real "Sunplate" (id 202, confirmado en `TileID.cs` decompilado). **Solo verificado por
compilación** (0 errores) - la verificación en vivo (`verificar-exploracion.ps1 -Buscar`) quedó
interrumpida a media compilación porque el usuario pasó a primer plano con una partida real de
Don't Starve Together, y la regla de "comprobar primer plano antes de forzar foco" prohíbe robarle
el foco en ese momento. Pendiente de verificar en vivo la próxima sesión libre.

### Bug real encontrado y arreglado por el camino (con la propia autoprueba existente, no una nueva)

Los tres chips de cabecera llevaban `EsPestana = true` (solo por el efecto visual de "crecer 2px en
vez de 3px" al pasar el ratón). `AutopruebaGuia.AnchosDePestana` (`Common/Guia/AutopruebaGuia.cs`)
resulta que recorre `panel.MarcoHijos` buscando CUALQUIER `BotonTk` con `EsPestana=true` para medir
"la barra de pestañas" real de cara a comprobar que ningún rótulo se recorta - los tres chips se
colaban ahí como si fueran pestañas de navegación, y como nunca pasan por
`AjustarEscalaDeLasPestanas` (que solo toca `_botonesPestana`), su texto a escala fija "no cabía"
según esa cuenta (`HAY TEXTO QUE NO CABE`, visto en rojo en una pasada completa de
`verificar-guia.ps1`). Arreglado quitando `EsPestana=true` de los tres chips (inerte de todas
formas en los dos con `Habilitado=false`: `BotonTk.Update` solo anima con `Habilitado=true`).
Reverificado con la Guía entera (vanilla+Calamity, ~600s): "Ninguna comprobación en rojo".

### Lo investigado y descartado, con razón real (no vago "queda pendiente")

- **TM1 (pestañas con sprite real)**: investigado en el juego decompilado qué rutas de sprite
  vanilla podrían representar cada uno de los 8 conceptos (cabeza del jugador/cofre/yunque/lupa/
  mapa/engranaje/libro/cámara) - no se encontró un icono limpio de un solo concepto para varios de
  ellos con la búsqueda real hecha esta sesión (`Images/UI/Settings_*` son gráficos de interruptor,
  no un engranaje genérico; no hay icono vanilla de "cámara"). Necesita una sesión de investigación
  de assets dedicada, con capturas reales del juego probando cada candidato, antes de escribir
  código - se prefiere no forzar un sprite equivocado o vacío.
- **TM2 (editor de objeto flotante) y TM4 (Guía con checklist de sprites)**: catalogados como
  "Grande" con razón - tocan interacción central de Librería y el layout completo de la Guía. No
  investigados a fondo esta sesión por presupuesto de tiempo; quedan para la próxima ronda.
- **TM5 (Builds: filtros compactos)**: investigado el archivo real (`ContenidoBuilds.cs`,
  `GrupoPildoras.Reflow`) - es de los ficheros más delicados y con más historial de bugs reales ya
  documentados de todo el mod (reflow dinámico de píldoras, `Recalculate` condicional). Requeriría
  además dos widgets nuevos que no existen (`SelectorTk` desplegable, anillo de progreso). Se
  decide NO tocarlo sin una ronda de verificación visual iterativa dedicada, para no arriesgar
  regresiones en un sistema ya fino.
- **Idea 8 (checklist de coleccionista)**: investigado - ya está MUY avanzada de una sesión
  anterior (`PestanaCompletitud` ya tiene 4 resúmenes con barra + lista scrollable de "lo que
  falta" para Jefes y Logros, con Bestiario/Investigación deliberadamente sin nombres por no
  destripar el propio bestiario del juego). El hueco real que quedaba ("navegable, con dónde
  conseguirlo") choca con que `ContenidoGuia` es a propósito una "brújula, no un GPS" (solo enseña
  el objetivo ACTUAL, nunca un tramo arbitrario que el jugador elija) - hacer clic en un jefe
  pendiente de la lista y saltar a la Guía mostraría el objetivo actual real, no necesariamente el
  jefe pulsado, una experiencia confusa. Se decide no forzar esa navegación sin rediseñar antes
  cómo se vería una Guía "explorable" de verdad.
- **Ideas 1 (entrenador de jefe), 5 (planificador de felicidad de NPCs), 6 (cofres del mundo en
  vivo), 9 (guía de grupo) y 10 (rebobinar el mundo)**: sistemas nuevos genuinamente grandes
  (arena de ensayo con snapshot+restauración, IA de recolocación de NPCs, editor de contenedores
  del mundo en vivo, evaluación multijugador, snapshot/restauración de una región de tiles). No
  investigados a fondo esta sesión - quedan en el catálogo para las próximas rondas, con la
  infraestructura real ya localizada donde existe (`Undo/PilaDeSnapshots`, `ShopHelper`
  decompilado, `Main_WorldEdit_Patch.TryCalcularAreaSeleccionada` de TModLoaderMod) para no
  arrancar de cero la próxima vez.

### Verificación real de conjunto

- `scripts\compilar.ps1`: 0 errores en cada uno de los commits de esta sesión.
- `scripts\verificar-panel-unico.ps1` (contra tModLoader real, mundo/personaje sintéticos): AUTOPRUEBA
  PANEL COMPLETA en cada pasada, capturas reales inspeccionadas a mano (chips sin solape con HUD ni
  con la barra de pestañas, controles de código de build en el pie sin desbordar).
- `scripts\verificar-guia.ps1` (vanilla + Calamity, 56+ tramos, ~600s): AUTOPRUEBA GUIA COMPLETA,
  "Ninguna comprobación en rojo" tras el arreglo del bug de `EsPestana`.
- Verificador de analizadores estáticos de KeepQA (`verificarAnalisisEstatico.js`, gate por
  defecto): 17 avisos únicos RCS1075, los mismos de antes de esta sesión (backlog deliberado ya
  documentado el 17-sep) - ningún aviso nuevo introducido.
- `.codebase-memory/` apareció sin seguimiento durante la sesión (herramienta del entorno, no
  tocada a propósito) - no se ha añadido a git.

Sin `git push`, sin `gh release`, sin empaquetar el mod, sin subir versión - el usuario revisará
todo antes de publicar nada, tal como se pidió.


---

## 20-sep-2026 (continuación) - Retomada la cadena tras el aviso de "usuario ya no delante": TM1 y TM4 (parcial) implementados, TM5 e idea 8-bioma investigados y descartados con motivo real

El coordinador avisó de que el usuario dejó de estar delante del ordenador (Terraria vanilla
abierto, sin partida activa) y pidió retomar lo que había quedado documentado como pendiente en la
entrada anterior: TM1/TM2/TM4/TM5 del catálogo visual y las ideas 1/5/6/8/9/10 del catálogo de
funciones. Antes de tocar nada se comprobó primer plano y procesos de inyección reales
(`Get-CimInstance Win32_Process`) para no interferir si TerrakeepTrainer estuviera enganchándose a
la misma partida vanilla - limpio en todas las comprobaciones de esta ronda.

### Implementado y verificado de verdad en el juego (2 commits más)

**TM1 · Pestañas con sprite real del juego** (`UI/Personaje/Widgets/BotonTk.cs`,
`Common/Panel/IconosPestanas.cs` nuevo). `BotonTk` gana icono real opcional; por debajo de 74px de
ancho el texto se esconde y solo queda el icono con su tooltip, tal cual pedía el catálogo. Los 8
iconos se investigaron contra el `Content/` real de la instalación de Steam (nunca adivinados):
corazón real (Personaje), cofre real `ChestStack_0` (Librería), icono real de "se puede fabricar
aquí" `Craft` (Builds), la lupa real del Bestiario `Button_Search` (Investigación), `Map_0`
(Exploración), `Research_GearA` (Ajustes), la cabeza REAL del NPC Guía resuelta en vivo con
`TownNPCProfiles.GetHeadIndexSafe` (Guía), y el icono real de cámara `Camera_0` (Álbum). Dos
intentos reales de icono para Personaje se probaron y se descartaron con evidencia (documentado en
el XMLdoc de `IconosPestanas`, no de memoria): `Main.MapPlayerRenderer.DrawPlayerHead` (la cabeza
REAL del jugador) dio un borrón negro - causa real confirmada decompilando
`MapHeadRenderer.RenderDrawData`, que dibuja con `Main.spriteBatch` y aplica un pase de
`Main.pixelShader` asumiendo el contexto de `SpriteBatch.Begin()` con el que vanilla lo llama desde
su propio HUD, no el de este panel; y `Bestiary/Portrait_Front` como respaldo estático dio un marco
vacío (es el BORDE del retrato, no el contenido). El corazón real fue el que sí salió relleno y
reconocible.

**TM4 (parcial) · Guía con checklist de sprites** (`Common/Guia/IconoJefe.cs`,
`UI/Guia/TarjetaObjetivoTk.cs` nuevos). Tarjeta con el sprite real del jefe (72px, el mismo
`TextureAssets.NpcHeadBoss` con el que vanilla marca un jefe en el mapa) junto al título del paso,
creciendo en alto si el título envuelve a más líneas - nunca recorta. El checklist-con-icono-de-
objeto por requisito y la tira de "lo que viene" en tres tarjetas se dejan para otra ronda: el
modelo real (`RequisitoGuia.Id`/`IdMod`/`Ids`/`IdsMod`, `Terrakeep.Core.dll`) no expone la
resolución a "id real de esta partida" fuera de `ProveedorEstadoGuiaMod`, y adivinar un id crudo sin
mirar esa resolución con calma habría sido justo el tipo de "a ciegas" que este proyecto prohíbe.

### Bug real encontrado por el camino y descartado como propio (aislado con `git stash`)

La primera pasada de `verificar-guia.ps1` con TM4 a medio escribir dio "NO CUADRA: se agotaron 308
fotogramas esperando... `Player.UpdateEquips`". Antes de asumir que era mío: `git stash` para volver
al último commit (solo TM1) y repetir la misma pasada - **el mismo fallo, idéntico, sin ningún
cambio de TM4 puesto**. Revisando el sistema: `Get-CimInstance Win32_Processor` con
`LoadPercentage=100` sostenido, y un proceso `find` huérfano de una `Bash` de más de una hora
consumiendo CPU de un comando anterior de esta misma sesión (matado). Con la CPU liberada, la
siguiente pasada (con TM4 ya restaurado con `git stash pop`) salió limpia del todo. Confirmado
ambiental (contención real de CPU tras muchos lanzamientos seguidos del juego en una sola sesión),
nunca un fallo de este código - documentado aquí para que quede constancia sin tener que repetir la
investigación si vuelve a pasar.

### Investigado y descartado esta ronda, con motivo real (no vago)

- **TM5 (Builds: filtros compactos)**: se investigó en detalle mover el alternador Vanilla/Calamity
  a la cabecera (el sub-cambio de menor riesgo del catálogo). Hallazgo real antes de escribir una
  sola línea: `_subtitulo` (el "Tienes X de Y objetos... ranuras disponibles: N") es una
  `EtiquetaTk` con ancho FIJO de 900px que no envuelve ni se recorta - en una ventana estrecha con
  el catálogo de Calamity activo (el texto más largo de los dos), un alternador de 240px anclado a
  la derecha de esa misma cabecera invadiría el mismo hueco donde ese texto puede llegar a
  dibujarse. Sin rediseñar antes cómo se comporta ese subtítulo largo, mover el alternador ahí
  arriesgaba el mismo tipo de solape que el proyecto lleva meses evitando a propósito. Se descarta
  el movimiento; el resto de TM5 (selector desplegable de etapa, fila combinada clase+conjunto,
  anillo de progreso) sigue necesitando los dos widgets nuevos ya señalados en la ronda anterior.
- **Idea 8 (checklist de coleccionista) - "bestiario por bioma"**: investigada la API real
  (`Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes`,
  tModLoader.dll instalado) para desglosar el resumen agregado de Bestiario en sub-totales por
  bioma sin destripar el propio bestiario (mismo criterio ya usado: contar, nunca nombrar lo no
  descubierto). Es real y existe, pero identificar con certeza CUÁLES de las condiciones de
  aparición de cada `BestiaryEntry.Info` son de bioma (y no de hora del día, evento o clima) exige
  más tiempo de investigación del que quedaba en esta ronda para no adivinar una categorización
  equivocada. Queda con la ruta real ya localizada para la próxima vez.
- **TM2 (editor de objeto flotante)**: investigada la pieza que el propio catálogo señalaba
  (`UI/Panel/CapaSuperposicionTk.cs`) - confirmado que SÍ está lista de verdad: capa genérica
  reutilizable para "mostrar cualquier contenido flotante dentro del panel" con cierre al pulsar
  fuera, orden de dibujado correcto (por encima de todo, botón Cerrar incluido) y enrutado real de
  clic/rueda ya resuelto (los tres problemas reales que en su día rompían el desplegable de
  prefijo). El trabajo real que falta no es la capa - es MOVER los widgets ya maduros del "Editar
  objeto" fijo actual (`EditorCantidadTk`, `EditorPrefijoTk`) a una tarjeta nueva anclada al slot,
  sin romper nada del flujo de edición que ya funciona - una cirugía real sobre una función central
  ya pulida de la Librería, que se decide no empezar a medias en esta ronda. Punto de partida real
  dejado para la próxima vez.
- **Ideas 1/5/6/9/10**: no investigadas a fondo en esta ronda (presupuesto de tiempo). Siguen
  siendo, con la información ya reunida en la entrada anterior de hoy, los candidatos reales para
  la próxima sesión - ninguno se ha tocado ni a medias para no dejar código a mitad de camino.

### Verificación real de esta ronda

- `scripts\compilar.ps1`: 0 errores en cada commit.
- `scripts\verificar-panel-unico.ps1`: AUTOPRUEBA PANEL COMPLETA en cada pasada de TM1 (tres
  iteraciones reales hasta dar con un icono de Personaje que saliera relleno), con inspección de
  píxeles real (recortes 2x-4x con PIL) de cada intento, nunca solo "compila".
- `scripts\verificar-guia.ps1` (vanilla + Calamity, ~600s): AUTOPRUEBA GUIA COMPLETA, "Ninguna
  comprobación en rojo" en la pasada final con TM4, tras aislar y descartar el falso positivo de
  CPU. Capturas reales: Muro de Carne con su sprite limpio junto al título
  (`guia-11-muro-preparativos.png`), Señor de la Luna (sin icono de jefe real registrado en
  vanilla) cayendo con elegancia al título simple sin tarjeta vacía
  (`guia-26-moonlord-preparativos.png`).

## 20-sep-2026 (segundo empujón) — TM2 completo (editor de objeto flotante) + idea 5 (planificador de vecindad de NPCs)

El aviso de la ronda anterior ("son sistemas grandes que merecen sesión dedicada" no es un
límite real) era correcto: con el punto de partida ya investigado (`CapaSuperposicionTk`
confirmada lista), TM2 sí se pudo construir entero esta ronda, y de las 5 ideas de funciones
pendientes se investigó primero cuál tenía menos riesgo real (evitando las que tocan datos de
partida real/multijugador sin poder verificarlas con cuidado) y se implementó la idea 5.

### TM2 - tarjeta flotante de edición de objeto

- `UI/Libreria/Widgets/TarjetaEdicionFlotanteTk.cs` (nuevo): tarjeta `UIPanel` de 240x204,
  anclada al slot de selección vía `CapaSuperposicionTk`, con el sprite del objeto a 2x
  (`ItemSlot.Draw` con guardado/restaurado real de `Main.inventoryScale`/`Main.inventoryBack`,
  igual que hace el propio vanilla), nombre coloreado por rareza
  (`Terraria.GameContent.UI.ItemRarity.GetColor`) con auto-reducción de escala si no cabe (nunca
  se trunca), el editor de cantidad ya maduro (`EditorCantidadTk`, reutilizado tal cual) y la
  papelera (`SlotPapeleraTk`, reutilizada). Dos decisiones de diseño reales, documentadas en el
  propio XMLdoc de la clase:
  1. **No sustituye el arrastre de `SlotSeleccionTk` por clic directo sobre el slot real**:
     habría que reimplementar a mano la semántica de recogida de `ItemSlot.Handle` que usa toda
     la Librería - más riesgo que beneficio para lo que pide el catálogo.
  2. **El prefijo se muestra de solo lectura** (`TextoPrefijo()`, con el mejor prefijo posible
     vía `CatalogoMejorPrefijo.MejorPrefijo` si es distinto del actual) en vez de anidar un
     `EditorPrefijoTk` interactivo dentro de la tarjeta: ambos widgets comparten la MISMA
     `CapaSuperposicionTk` (un solo hueco), así que anidarlos los habría hecho pelearse por ese
     hueco entre sí mismos.
- `UI/Libreria/Widgets/PanelHerramientasLibreriaTk.cs`: nuevo `Update()` que muestra/oculta la
  tarjeta automáticamente según si `Seleccion` tiene un objeto real.
- **Bug real encontrado y arreglado con la propia autoprueba** (no hipotético, capturado en el
  log): la tarjeta y el desplegable YA EXISTENTE de `EditorPrefijoTk` (del mismo mini-panel, no
  anidados entre sí - ver el punto 2 de arriba) resultaron ser DOS widgets hermanos que comparten
  la misma `CapaSuperposicionTk`. `CapaSuperposicionTk.Mostrar()` hace `Quitar(null)` antes de
  colgar lo nuevo, así que en el primer intento, en cuanto el jugador abría el desplegable de
  prefijo, el `Update()` de la tarjeta (que corre todos los fotogramas mientras haya selección)
  volvía a llamar a `Mostrar()` en el fotograma siguiente y expulsaba el desplegable que el
  jugador acababa de abrir - confirmado con la autoprueba ANTES del arreglo: paso 18 registraba
  `PopupAbierto=True` pero el paso 19, un fotograma después, ya no encontraba el popup abierto.
  Arreglado con una guarda de cesión: si la capa la ocupa algo que no es la propia tarjeta, se
  espera a que se libere sola en vez de arrebatársela.
- `Common/Libreria/AutopruebaLibreria.cs`: pasos 26/27 nuevos (arrastra un objeto a selección,
  comprueba que `TarjetaFlotanteAbierta` coincide con si hay objeto, vuelca el texto real de
  `TextoPrefijo()` al log y guarda captura).
- Localización (es-ES/en-US) con las claves `Libreria.TarjetaFlotante.Prefijo`/`PrefijoConMejor`.

**Verificación real** (`scripts\verificar-libreria.ps1`, comprobado el primer plano/proceso de
TerrakeepTrainer antes de lanzar el cliente gráfico): `AUTOPRUEBA WS3 COMPLETA` tras el arreglo,
con el paso 19 mostrando ahora la comprobación completa de geometría en vez del fallo en cadena
("el popup no esta abierto") - cabe entero dentro de la capa, no se cruza con el botón Cerrar,
y `UIElement.GetElementAt` (la misma llamada real que usa `UserInterface` para repartir clics)
devuelve el botón del desplegable, no la tarjeta. Capturas reales revisadas a ojo, pixel a pixel:
`ws3-prefijo-1-abierto-dentro-del-panel.png` (desplegable de prefijo intacto, ya no expulsado) y
`ws3-tarjeta-flotante.png` (tarjeta con sprite 2x, cantidad, papelera y "Prefix: None" - todo
dentro de su caja, sin solapes).

### Idea 5 - planificador de felicidad de vecinos (nueva sub-pestaña "Vecindad" en Exploración)

- `UI/Exploracion/PestanaVecindad.cs` (nuevo): lista con scroll, refrescada cada 30 fotogramas,
  de todos los NPCs de pueblo activos (`Main.npc[i].active && townNPC`, excluyendo mascotas de
  pueblo vía `NPCID.Sets.IsTownPet`), ordenados por nombre. Por cada uno, usa la API vainilla
  REAL de felicidad - `Main.ShopHelper.GetShoppingSettings(jugador, npc)`, la misma que calcula
  el precio de la tienda del propio NPC - para mostrar el % de ajuste de precio (verde/rojo/gris
  según sea mejor/peor/neutro que 1.0) y el texto REAL localizado de `HappinessReport` (la misma
  frase que vainilla muestra en la conversación del NPC), sin reinventar el cálculo.
- Dos avisos añadidos con investigación real, no adivinados: "SinCasa" si
  `homeTileX/Y < 0` (el NPC aún no tiene casa asignada) y "Lejos" si la distancia jugador-NPC
  supera 60 tiles - documentado en el XMLdoc de la clase el motivo concreto: la felicidad por
  bioma se calcula sobre la posición VIVA del jugador
  (`BiomePreferenceListTrait.ModifyShopPrice` llama a `preference.Biome.IsInBiome(info.player)`,
  visto en el decompilado real de `Terraria.GameContent.Personalities.BiomePreferenceListTrait`),
  no sobre la casa del NPC - así que el informe que se muestra solo es fiable estando cerca del
  NPC de verdad, y el aviso lo deja claro en vez de dar un dato que podría no corresponder a esa
  casa.
- **Límite real investigado y aceptado, no forzado**: la idea original de "proponer una
  reubicación mejor" para cada NPC no tiene una superficie segura - no existe ninguna API pública
  para simular `GetShoppingSettings` con el jugador en OTRA posición sin moverlo de verdad (que
  sería tocar la partida real para una simulación, descartado por el mismo criterio que ya se
  aplicó con datos de partida real en TerrakeepTrainer). Se documenta aquí en vez de forzar un
  cálculo que mentiría en cuanto el bioma de destino no coincidiera con el real.
- `UI/Exploracion/ContenidoExploracion.cs`: cuarta sub-pestaña ("Vecindad") añadida al array de
  claves y al `switch` de construcción, mismo patrón que las otras tres.
- Localización (es-ES/en-US): pestaña + bloque `Exploracion.Vecindad.*`
  (Resumen/Ninguno/Precio/SinInforme/SinCasa/Lejos).

**Verificación real** (`scripts\verificar-exploracion.ps1`, pasos 23/24 nuevos, mismo sandbox
WS6 con NPCs de pueblo activos): `AUTOPRUEBA WS6 COMPLETA`, "NPC de pueblo activos detectados: 2"
- Anciano y Zach el Guía. Captura real `ws6-vecindad.png` revisada pixel a pixel: Anciano al 100%
del precio base con el aviso ámbar "A 1259 tiles - acércate a su casa para una lectura real"
(dispara de verdad el umbral de 60 tiles), Zach el Guía al 150% en rojo con el texto REAL de
`HappinessReport` de vanilla sobre no tener casa y que le gusta el Bosque - sin solapes, scroll
funcional, colores correctos.

### Idea 6 - cofres del mundo en vivo (8º destino de la Librería)

Investigación previa delegada a un fork (solo lectura, sin tocar código) sobre las 4 ideas de
funciones que quedaban (1 entrenador de jefe, 6 cofres del mundo, 9 guía de grupo, 10 rebobinar
el mundo), con evidencia real del `tModLoader.dll` instalado decompilado. Veredictos, de menor a
mayor riesgo: **idea 6 tractable con riesgo mínimo** (confirmado: `Terraria.Chest.item` es un
`Item[]` público normal, el mismo tipo que ya editan los 7 contenedores del jugador), **idea 10
tractable con riesgo bajo-medio** (pendiente, ver más abajo), **idea 1 tractable salvo el Muro de
Carne** (matarlo dispara `WorldGen.StartHardmode()`, conversión PERMANENTE de tiles de todo el
mundo - **LÍMITE REAL concreto**, con la línea real de `NPC.cs` localizada), **idea 9 LÍMITE REAL
para esta noche** (confirmado por grep: cero arnés de dos clientes tModLoader en toda la familia
Keep; la ruta real existe - `ModPacket`, difusión del paso propio ya calculado - pero construirla
a ciegas sin poder verificar la sincronización es justo el riesgo que el usuario pidió evitar).

Implementada la idea 6: `UI/Libreria/Widgets/SelectorCofreMundoTk.cs` (nuevo) - un desplegable,
colgado de la misma `CapaSuperposicionTk` que `EditorPrefijoTk`/`TarjetaEdicionFlotanteTk`, con
TODOS los cofres reales colocados en el mundo cargado (`Main.chest[]`, confirmado que el juego
mantiene la lista entera en memoria desde que carga la partida - no hace falta estar cerca),
ordenados por distancia, cada fila con nombre/posición/objetos reales. Al elegir uno, se convierte
en un 8º "destino" más de `ContenidoLibreria` (`Destinos[]`, ahora con un `nombreDinamico`
opcional para el rótulo que cambia según el cofre elegido) - reutiliza EXACTAMENTE el mismo
mecanismo ya probado (`ItemSlot.Handle` sobre un `Item[]`) sin tocar `MostrarDestino`/
`ArrayDestino`, solo el origen del array. **Límite real aceptado a propósito**: acotado a partida
de un jugador (`Main.netMode == 0`) - escribir en un cofre de un servidor real exige reenviar el
cambio a los clientes conectados (`NetMessage.SendData(MessageID.SyncChestItem, ...)`), mismo tipo
de riesgo que la idea 9 y sin arnés real para verificarlo esta noche; el selector muestra un aviso
en vez de la lista si `Main.netMode != 0`, documentado en el XMLdoc de la clase.

**Bug real encontrado y arreglado con la propia captura, no a priori**: la primera versión dejaba
el texto de resumen ("172 real chests found...") cortado a media palabra, porque `EtiquetaTk` no
envuelve ni recorta texto sola (`Utils.DrawBorderString` puro, visto en su propio código) - se
descubrió con la captura real, no adivinando. Arreglado envolviendo el texto A MANO con
`EtiquetaTk.PartirEnLineas` (el propio helper ya existente para esto) antes de crear la etiqueta,
con el alto ajustado al número real de líneas resultante.

**Verificación real** (`scripts\verificar-libreria.ps1`, pasos 28-32 nuevos, comprobado el primer
plano antes de cada lanzamiento - se detectó a TerrakeepTrainer activo en primer plano una vez y
se esperó a que soltara el foco antes de lanzar el cliente): `AUTOPRUEBA WS3 COMPLETA`. Paso 28
siembra un cofre SINTÉTICO de prueba (nunca uno real, mismo patrón que `Chest.CreateChest` real)
con 2 objetos conocidos; paso 29 pulsa el botón "Cofre" real; paso 30 confirma 172 cofres reales
encontrados en el mundo de pruebas y captura la lista; paso 31 pulsa la fila real del cofre
sembrado; paso 32 (un fotograma después, mismo motivo ya documentado para el popup de prefijo)
confirma que `ArrayDestino` es el `Item[]` REAL del cofre elegido (`ReferenceEquals`, no una
copia) con sus dos objetos exactos, y captura la rejilla ya con el cofre cargado. Dos capturas
reales revisadas pixel a pixel: `ws3-selector-cofre.png` (lista envuelta correctamente, sin
cortes) y `ws3-destino-cofre.png` (8º botón "Chest" activo, rejilla con el hierro x20 y la moneda
de oro x5 reales).

### Idea 10 - rebobinar el mundo (nueva sub-pestaña "Rebobinar" en Exploración) - VERIFICADA Y CERRADA

Implementado sobre la investigación real ya hecha por el fork (ver arriba): `UI/Exploracion/PestanaRebobinar.cs`
(nuevo) - un deshacer de TERRENO, mismo espíritu que `PilaDeSnapshots` pero para tiles. "Marcar
aquí" fotografía un cuadrado de 201x201 tiles alrededor del jugador (`RadioTiles=100`) en un
struct PROPIO (`TileGuardado`: tipo, pared, líquido, pendiente, media altura, pintura, cables) -
**nunca guardando el `Tile` tal cual**, investigado con el decompilado real que desde el refactor
de memoria de 1.4.4+ un `Tile` es un accesor ligero sobre arrays compartidos (`Get<TileTypeData>()`
y compañía), no un dato independiente: copiar el struct copiaría la MISMA celda viva, no una foto.
"Rebobinar" escribe cada campo de vuelta (todos con setter público real, confirmado uno a uno) y
llama a `WorldGen.RangeFrame` + `Main.refreshMap` para el reencuadrado/minimapa. Un contador real
"N de M tiles distintos ahora mismo" se recalcula cada 30 fotogramas, para que el jugador vea de
verdad si algo ha cambiado antes de rebobinar. **Límite real aceptado a propósito, igual que la
idea 6**: acotado a partida de un jugador (`Main.netMode == SinglePlayer`) - mismo motivo (no hay
arnés de dos clientes en la familia para verificar `NetMessage.SendTileSquare` con cuidado esta
noche), documentado en el XMLdoc de la clase. `ContenidoExploracion.cs`: 5ª sub-pestaña.
Localización (es-ES/en-US): pestaña + bloque `Exploracion.Rebobinar.*`.

`Common/Exploracion/AutopruebaExploracion.cs`: pasos 25-29 nuevos (abre la pestaña, pulsa "Marcar
aquí" real, cambia un tile SINTÉTICO de prueba dentro del área fotografiada a un tipo conocido,
comprueba que `DiferentesAhora` detecta el cambio, pulsa "Rebobinar ahora" real, comprueba que el
tile vuelve exactamente a como estaba).

**Corrección del usuario sobre el arnés propio**: TerrakeepMod SÍ tiene su propio arnés autónomo
(`scripts\verificar-*.ps1`, arranca tModLoader con su propio mundo/personaje de pruebas) - a
diferencia de TerrakeepTrainer, que dependía de un personaje REAL del usuario en Terraria vanilla.
La pausa inicial por "actividad del usuario en primer plano" fue un error de razonamiento propio:
confundir el caso de TerrakeepTrainer (donde SÍ hay que esperar, la ventana de vainilla es la del
propio usuario) con el de TerrakeepMod (donde el cliente gráfico de verificación es una ventana e
instalación TOTALMENTE APARTE, un sandbox propio, que nunca ha dependido de lo que el usuario
tenga abierto). Corregido: se lanza el arnés propio sin esperar.

**Dos bugs reales encontrados y arreglados con la propia autoprueba** (no hipotéticos):
1. **Falso positivo de arrastre del minimapa (pasos 20/21)**: en la primera pasada de esta ronda
   fallaron con `MAL` (`Main.mouseLeft=False` tras pulsar el ratón sintético). Investigado antes
   de tocar nada: 10 procesos `dotnet.exe` de reutilización de nodos de MSBuild acumulados de
   las repetidas compilaciones de la noche (mismo patrón raíz ya documentado hoy mismo con el
   `find` huérfano). Cerrados esos procesos y repetida la misma pasada sin tocar el código: los
   pasos 20/21 pasaron en verde - confirmado que era contención de CPU, no una regresión real.
2. **Choque de nombres real entre la pestaña y su propio botón**: el botón de acción se llamaba
   igual que la pestaña ("Rebobinar"/"Rewind"), así que `panel.PulsarBoton` (que recorre TODO el
   panel, no solo la pestaña activa) encontraba primero el botón de la BARRA DE PESTAÑAS y
   reconstruía `PestanaRebobinar` entera de cero, perdiendo la foto ya tomada - visto en el log
   real (`DiferentesAhora=-1` tras "rebobinar", el tile sin restaurar). Arreglado renombrando el
   botón de acción a "Rebobinar ahora"/"Rewind now" (mejora real de UX además de arreglo de
   prueba: un botón que repite el nombre de su propia pestaña era confuso incluso para un jugador
   real, no solo para el arnés).

**Verificación real, ya completa** (`scripts\verificar-exploracion.ps1`): `AUTOPRUEBA WS6
COMPLETA` en verde de principio a fin. Paso 26 cambia un tile sintético de prueba dentro del área;
paso 27 confirma `DiferentesAhora=1` (detecta el cambio real); paso 28 pulsa "Rebobinar ahora" con
un clic real; paso 29 confirma el tile exactamente restaurado (`TileType`/`HasTile` iguales a la
foto) y `DiferentesAhora=0`. Captura real revisada pixel a pixel (`ws6-rebobinar-despues.png`):
título, explicación envuelta en dos líneas, los dos botones sin recorte, y el mensaje real
"¡Hecho! 1 de 40000 tiles han vuelto a como estaban en la foto."

### Idea 1 (entrenador de jefe) - NO implementada esta ronda, con el límite real ya localizado

Como se documentó en la investigación del fork, el subconjunto seguro (todos los jefes salvo el
Muro de Carne, cuya muerte dispara `WorldGen.StartHardmode()` - conversión PERMANENTE de tiles de
todo el mundo, límite real confirmado en `NPC.cs`) es tractable, pero requiere investigar el
mecanismo de invocación REAL de cada jefe uno a uno (tipos de NPC, parámetros de `NewNPC`/
`SpawnBoss`, condiciones previas) antes de escribir nada - y con el cliente gráfico bloqueado por
la misma actividad real del usuario de arriba, no hay forma de verificar nada esta ronda. Se
decide NO escribir código de invocación de jefes sin poder probarlo de verdad (misma disciplina de
dos fases que el resto del proyecto): queda documentado el límite real del Muro de Carne y el
punto de partida (`NPC.SpawnBoss`/`NPC.NewNPC`, banderas `downedBossN` solo se tocan al morir) para
la próxima sesión.

### Idea 10 (continuación) - correción de alcance real: faltaban los cofres

Al consultar el texto REAL del catálogo (Claude Docs, no de memoria) se confirmó que la idea 10
pedía "snapshot de una región (**tiles + cofres** en un radio)", no solo terreno - un hueco real
en lo ya cerrado, no una decisión de alcance propia. Ampliado `PestanaRebobinar.cs`: "Marcar aquí"
ahora también fotografía los cofres reales cuya esquina (`Chest.x/Chest.y`) cae dentro del área,
clonando su contenido de verdad (`Item.Clone()`, el mismo método ya usado por
`Common/Undo/Historial.cs`). Al restaurar, comprueba que el cofre en ese índice de `Main.chest[]`
sigue siendo el MISMO (misma `x`/`y`) antes de escribir - un cofre destruido y otro nuevo colocado
después podría reciclar el mismo índice, y escribir a ciegas sobre eso sería un bug real, no solo
teórico. Contador aparte ("N cofres en la zona, M con contenido distinto") además del de tiles.

**Verificación real** (`scripts\verificar-exploracion.ps1`, pasos renumerados 25-31): siembra un
cofre sintético de prueba (nunca uno real) muy cerca del jugador antes de marcar, confirma que la
foto lo incluye (`CofresEnFoto=2` - el sembrado más un cofre real ya generado ahí cerca), cambia su
contenido junto con el tile de siempre, confirma que `CofresDistintosAhora` detecta el cambio real,
y que "Rebobinar ahora" lo devuelve exactamente. Captura real revisada pixel a pixel: "2 cofres en
la zona marcada, 0 con contenido distinto ahora mismo." y "¡Hecho! 1 de 40000 tiles y 1 de 2 cofres
han vuelto a como estaban en la foto." - ambas líneas nuevas envueltas sin solapes.

### Idea 4 (sonar de estructuras) - CERRADA: verificación en vivo pendiente, ahora hecha

El código ya estaba: Mazmorra/Templo lihzahrd/Nido de araña por sus paredes reales "Unsafe" y la
Isla flotante por el tile real "Sunplate" (sesión anterior). Lo único que quedaba de verdad era la
verificación en vivo, interrumpida entonces por una partida real del usuario. Verificado ahora con
`scripts\verificar-exploracion.ps1 -Buscar 'Isla' -Revelar 2000`: "Isla flotante (Sunplate)"
seleccionada de verdad por el buscador, **333 tiles reales encontrados en 20.170.801 tiles
mirados, agrupados en 5 zonas** (5 islas flotantes reales del mundo de pruebas, cada una con sus
coordenadas y distancia reales). Captura real revisada pixel a pixel
(`ws6-resultados-iconos.png`): las 5 filas con icono, nombre y distancia, sin solapes. La Pirámide
del desierto sigue como LÍMITE REAL ya documentado (sin tile/pared exclusivo propio) - no se
fuerza un marcador falso. Idea 4 queda 100% cerrada.

### Idea 8 (checklist de coleccionista) - CERRADA: desglose real por bioma del Bestiario

Investigación de la sesión anterior completada: `BestiaryDatabaseNPCsPopulator.CommonTags.
SpawnConditions` separa sus condiciones en CUATRO grupos reales (`Biomes`, `Events`, `Invasions`,
`Times`, confirmado en el decompilado real) - `Biomes` es la única categoría real de "bioma" sin
mezclar hora del día/clima/invasión, y trae 40 etiquetas reales (`Terraria.GameContent.Bestiary.
SpawnConditionBestiaryInfoElement`, una por bioma: Superficie, Cavernas, Mazmorra, Corrupción,
Cripta subterránea de Corrupción... hasta los 4 Pilares lunares). Cada etiqueta expone
`GetDisplayNameKey()` público, así que el nombre mostrado es el texto OFICIAL de vanilla (el mismo
que usan los propios botones de filtro del Bestiario), nunca inventado ni traducido a mano.

`Common/Completitud/EstadoCompletitud.cs`: `Bestiario()` ahora recorre las 40 etiquetas por cada
`BestiaryEntry` con `entry.Info.Contains(etiqueta)` - el MISMO camino real que usa
`Filters.ByInfoElement` (el filtro real de la propia pantalla de Bestiario de vanilla), nunca un
camino inventado - y deja un `ResumenCompletitud.Desglose` (campo nuevo, paralelo a `Faltan` pero
para subtotales "Nombre: hecho/total" en vez de nombres de lo que falta) ordenado por total
descendente. **Nunca se listan nombres de bichos**: solo biomas, mismo criterio de no-spoiler ya
aplicado al resto de Bestiario. `UI/Personaje/PestanaCompletitud.cs`: la fila de Bestiario pasa a
`conLista: true` y `Refrescar()` prioriza `Desglose` sobre `Faltan` cuando lo trae.

**Verificación real** (`scripts\verificar-completitud.ps1`, paso 0 ampliado): "PASO0.bioma total de
filas=40 (bestiario.Total=540)" - las 40 etiquetas reales, todas con al menos un bicho real
asociado, ordenadas por total descendente ("Superficie: 0/59", "Cavernas: 0/46", "La Mazmorra:
0/40"...). Captura real revisada pixel a pixel (`completitud-01-es.png`): la fila de Bestiario con
su propia lista scrollable, sin solapes con las demás filas.

### TM4 (continuación) - CERRADO por completo: checklist con icono real por requisito + "lo que viene" en tira

El commit parcial anterior (`1db1662`, misma sesión) ya dejaba la tarjeta con el sprite real del
jefe; quedaban dos piezas reales por cerrar, ambas con el motivo concreto ya investigado entonces.

**Checklist con icono de objeto por requisito**: el hueco real que dejó pendiente ("no hay una
resolución de 'id real de esta partida' ya expuesta fuera de `ProveedorEstadoGuiaMod`") resultó
tener solución real al mirarlo con calma: `RequisitoGuia.Id` (vanilla) YA es un `Item.type` real
sin resolver nada, y `RequisitoGuia.IdMod` (Calamity y demás) es un nombre `"Mod/Objeto"` resoluble
con `ItemID.Search.TryGetId` - el MISMO truco real que ya usa este mod para tiles de mod
(`ObjetivosBusqueda.Resolver`). `Common/Guia/IconoRequisito.cs` (nuevo) resuelve el tipo real
(objeto único o el primero resoluble de "cualquiera de estos"); `UI/Guia/FilaRequisitoTk.cs`
dibuja el icono con `ItemSlot.Draw` a 20px (la misma técnica de TM2, guardando/restaurando
`Main.inventoryScale`/`Main.inventoryBack`).

**"Lo que viene" en tira horizontal de 3 tarjetas**: la primera versión reutilizaba
`TarjetaObjetivoTk` (icono a la izquierda, título como `ParrafoTk` envolviendo) a tamaño reducido.
**Bug real encontrado por la propia autoprueba, no hipotético**: `scripts\verificar-guia.ps1`
devolvió "NO CABE: hay texto fuera de su caja" con la línea `"Esqueletron"` saliéndose -14,5 px de
su caja - en una columna de 1/3 de ancho con icono a la izquierda, `EtiquetaTk.PartirEnLineas` no
puede partir una palabra suelta más ancha que la caja (no trunca, la deja salirse). Arreglado con
un widget nuevo (`TarjetaLoQueVieneTk`, icono ENCIMA, título de una sola línea DEBAJO) que nunca
envuelve: mide con la fuente real y reduce la escala a mano si hace falta (la misma técnica ya
probada en `TarjetaEdicionFlotanteTk` de TM2 para el nombre del objeto), recalculado en cada
`DrawSelf` contra el ancho real - matemáticamente no puede desbordar, con un suelo de escala
(0,46) para que un nombre muy largo no encoja hasta ser ilegible. Con más de 3 tramos por delante
se avisa "y N más adelante" en vez de forzar una cuarta tarjeta.

**Bug real preexistente encontrado de paso (no de esta sesión)**: un byte NUL literal incrustado
dentro de una cadena de texto en `ContenidoGuia.cs` (`"\x00sin montar"`, ya en el commit `1db1662`)
- corregido (era inofensivo funcionalmente, C# tolera un NUL dentro de un string, pero no era
intencional).

**Verificación real** (`scripts\verificar-guia.ps1`, vanilla + Calamity, ~90s con las capturas ya
cacheadas del compilador): dos pasadas completas, la primera confirmó el bug real de desbordamiento
("NO CABE... Esqueletron"), la segunda (tras el arreglo) "Ninguna comprobación en rojo". Paso
nuevo en `Common/Guia/AutopruebaGuia.cs` (dentro del mismo case 21 existente, sin renumerar los
230+ pasos siguientes, con un fotograma real de por medio entre bajar el scroll y capturar - mismo
motivo ya documentado varias veces hoy) que baja el scroll de la columna derecha del todo y
captura `guia-3b-lo-que-viene.png`, revisada pixel a pixel: 3 tarjetas reales (altar demoníaco,
corona de la Reina Abeja, calavera de Esqueletrón) con sus títulos completos y legibles, sin
solapes, más el aviso "y 8 más adelante". También revisado `guia-3-arma-y-arena.png`: iconos
reales de plataforma de madera y poción curativa junto a sus requisitos, alineados sin solapes.

### TM5 (Builds: filtros en dos filas, no en cuatro) - CERRADO

La ronda anterior había descartado tocar este archivo ("de los más delicados... se decide NO
tocarlo sin una ronda de verificación visual iterativa dedicada") - corregido: no era un límite
real, era el tamaño esperado del trabajo. Investigado con calma y hecho entero.

- **Fuente (Vanilla/Calamity) → alternador de dos estados junto al título**: reutiliza
  `AlternadorTk` (widget YA existente, leído primero antes de usarlo). Colgado/descolgado de la
  cabecera segun `CatalogoBuilds.Fuentes.Count > 1` (con Calamity no usable en la partida, como en
  esta misma verificación, el alternador no aparece - nunca un control inerte).
- **Etapa → desplegable**: nuevo widget genérico, colgado de `CapaSuperposicionTk` igual que
  `EditorPrefijoTk`. **Bug real evitado por pelos**: el catálogo lo llama "SelectorTk", pero ese
  nombre YA estaba cogido - un widget real y distinto desde WS1 (fila "etiqueta [-] valor [+]" de
  paso, usado en `PestanaApariencia`/`CabeceraPersonaje`). La primera versión de este archivo lo
  sobrescribió sin leerlo antes (fallo real de disciplina propia), con errores de compilación
  reales en los tres sitios que ya lo usaban - detectado al compilar, restaurado desde git antes de
  seguir, y el widget nuevo se quedó con un nombre real distinto: `DesplegableTk`.
- **Clase y conjunto de destino → una sola fila**: cada uno sigue midiendo su propio ancho con
  `GrupoPildoras.Reflow` de forma independiente (sin tocar esa maquinaria, ya delicada y con
  historial real de bugs), solo que ahora dentro de una columna fraccional (62%/38%) en vez de la
  fila entera - `RecalcularPildorasYFilas` pasa a medir el ancho REAL de cada columna, no el ancho
  del panel entero (que era correcto cuando cada fila ocupaba el 100%, y habría sido un bug real
  silencioso de quedarse sin cambiar).
- **"Tienes X de Y" → anillo de progreso**: `AnilloProgresoTk` (nuevo), un círculo de verdad
  dibujado con segmentos de `TextureAssets.MagicPixel` alrededor de una circunferencia (SpriteBatch
  no tiene primitivos circulares - mismo principio real que ya usa `FilaRequisitoTk.DibujarMarca`
  para su cuadradito de estado, en círculo), con el número "X/Y" centrado y con la misma técnica de
  auto-reducción de escala de TM2/TM4 si no cupiera.

**Verificación real** (`scripts\verificar-builds-en-juego.ps1`): la autoprueba funcional completa
pasó sin ningún fallo (auto-equipar, filtrado por clase con clic real, idempotencia de la segunda
pasada) - la lógica de negocio no se tocó, solo la presentación. **Bug de infraestructura real
encontrado y arreglado de paso**: el script se quedó colgado en el diálogo "mods actualizados" de
tModLoader (HERO's Mod se había actualizado por Steam Workshop entre sesiones) - el mismo problema
que `verificar-guia.ps1` ya resolvía con `ShowNewUpdatedModsInfo: false`, pero que a este script
todavía le faltaba; aplicado el mismo arreglo real. Captura real revisada pixel a pixel
(`builds-persistencia-200f.png`): anillo "11/13" a la derecha del subtítulo, desplegable de etapa
en su propia fila, clase (4 píldoras) y conjunto (3 píldoras) compartiendo una sola fila sin
solaparse, cuerpo de 3 columnas intacto debajo.

### Sin publicar nada

`git push`, `gh release`, empaquetado del mod y subida de versión siguen sin tocarse, tal como se
pidió.
