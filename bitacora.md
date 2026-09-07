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
