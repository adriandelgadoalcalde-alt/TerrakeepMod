# TerrakeepMod

Mod real de tModLoader que porta las funcionalidades de **Terrakeep** (la app de escritorio
WPF, repo hermano) a dentro del juego, editando **en vivo** el personaje y el mundo ya cargados
en la partida, con la interfaz nativa de Terraria.

## Idioma
SIEMPRE en español de España, nunca inglés - respuestas, comentarios de código, mensajes de
commit, bitácora, todo. Incluido justo después de reanudar la sesión o de que el contexto se
compacte/resuma solo. Ver la regla global en `C:\Users\adrian\.claude\CLAUDE.md`.

## Documentos que mandan
- **El plan completo del proyecto**: `C:\Users\adrian\.claude\plans\streamed-leaping-balloon.md`
  — arquitectura, decisiones ya tomadas por el usuario, y los workstreams WS0..WS7. Leerlo
  entero antes de tocar nada.
- **Repo hermano** (la app de escritorio de la que esto es un puerto):
  `C:\Users\adrian\Downloads\Terrasavr-Win\Terrasavr-Native\` — su `CLAUDE.md` y su
  `bitacora.md` (691 KB de historial real) explican el porqué de casi todo lo que hay portado.
- `bitacora.md` (esta carpeta) — historial de este repo.

## Dónde está todo
- Índice de herramientas (rutas de ejecutables): `C:\Users\adrian\Downloads\herramientas.json`.
  **Nunca invocar un ejecutable por su nombre a secas**: ruta absoluta + directorio de trabajo.
- tModLoader instalado: `C:\Program Files (x86)\Steam\steamapps\common\tModLoader\`
  (lanzador `start-tModLoader.bat`, título de ventana "tModLoader"/"Terraria: ...").
- Código decompilado de referencia: `C:\Users\adrian\Downloads\tModLoader-Decompiled\`,
  carpeta `tModLoader\` (v1.4.4.9, la MISMA que está instalada — comprobado con
  `(Get-Item tModLoader.dll).VersionInfo`). **Antes de suponer cómo se comporta tModLoader o
  Calamity, mirar ahí el código real.**
- Este proyecto vive en `ModSources\` porque es donde tModLoader busca el código fuente de los
  mods en desarrollo; el `.tmod` compilado sale a `..\..\Mods\`.

## Cómo se compila
`scripts\compilar.ps1` — hace las dos fases y deja el `.tmod` en `Mods\`.

**No vale un `dotnet build` a secas.** `tMLMod.targets` termina llamando a `dotnet
tModLoader.dll -server -build ...` usando el `dotnet` **del PATH**, y en esta máquina el del
PATH es el SDK 10.0.400 sin ningún runtime .NET 8 instalado a nivel de sistema (`dotnet
--list-runtimes`: 6.0.36, 9.0.14, 10.0.3, 10.0.11). Como `tModLoader.runtimeconfig.json` exige
`Microsoft.NETCore.App 8.0.0`, ese paso falla siempre con MSB3073 / código -2147450730. El
runtime bueno es el que trae el propio tModLoader en `dotnet\dotnet.exe`, y es el que usa el
script.

El script compila **sin `-eac`** a propósito: con `-eac`, tModLoader reutiliza el DLL que ya
compiló MSBuild ("Loading pre-compiled TerrakeepMod.dll") en vez de compilarlo él.

## Verdades del entorno (no volver a descubrirlas)
- **tModLoader compila el mod con su propio Roslyn, no con MSBuild**
  (`ModCompile.RoslynCompile`, código real decompilado): `LanguageVersion.Preview`, **sin
  usings implícitos** y con **nullable desactivado**. Por eso el `.csproj` no activa
  `ImplicitUsings` ni `Nullable`: si se activaran, el proyecto compilaría en el IDE y fallaría
  al generar el `.tmod` de verdad. **Todos los `using` van explícitos en cada archivo.**
- `dllReferences = TerrasavrNative.Core` en `build.txt` hace que tModLoader busque el DLL
  **literalmente** en `lib\TerrasavrNative.Core.dll` (`ModCompile.DllRefPath`) y lo empaquete
  dentro del `.tmod` como `lib/TerrasavrNative.Core.dll`. Ese DLL es la build **net8.0** de
  `TerrasavrNative.Core` del repo hermano; se regenera con `scripts\actualizar-core.ps1`.
- El nombre real del mod lo determina **el nombre de la carpeta**
  (`ModCompile.ReadBuildInfo` hace `Path.GetFileName` del directorio), no la clase `Mod` ni el
  `AssemblyName`. Por eso la clase raíz se llama `Terrakeep` y no `TerrakeepMod`: así se evita
  la ambigüedad tipo/namespace que arrastra la plantilla oficial.
- `build.txt` ignora las líneas sin `=`, así que admite comentarios con `#` sin problema.
- Todo lo que empiece por `.` (incluido `.git\`) y las carpetas `bin\`/`obj\` los excluye
  tModLoader del empaquetado por su cuenta (`ModCompile.IgnoreCompletely`).
- **`-skipselect Personaje:Mundo`** entra directo al mundo saltándose todos los menús
  (`LaunchInitializer.LoadSharedParameters`) — es la forma de probar el mod sin navegar a mano.
  Si no encuentra el nombre, coge el PRIMERO de la lista, así que hay que usarlo siempre junto
  con `-tmlsavedirectory` sobre una carpeta de guardado aislada.
- **`-tmlsavedirectory <carpeta>`** cambia la carpeta de guardado entera (Players, Worlds,
  Mods, config.json). Es la forma limpia de probar sin tocar los personajes/mundos reales del
  usuario. La carpeta de pruebas de WS0 es
  `Documents\My Games\Terraria\tModLoader-TerrakeepWS0`.
- **El cliente no arranca si el sistema no tiene NINGUNA salida de audio**: `SoundEngine
  .Initialize()` llama a `Utils.ShowFancyErrorMessage(..., 10002)` y el juego se queda en un
  diálogo modal a pantalla completa que hay que cerrar **con un clic de ratón** (no tiene
  atajo de teclado) antes de que llegue siquiera a cargar los mods. Ver bitácora, entrada de
  WS0: eso, con la sesión de Windows bloqueada, deja la verificación en cliente inviable.
- Los logs reales van a `C:\Program Files (x86)\Steam\steamapps\common\tModLoader\
  tModLoader-Logs\` (`client.log` / `server.log`), **no** a la carpeta de guardado. Son la
  fuente de evidencia principal de este proyecto: el mod escribe ahí con `Mod.Logger` y todas
  sus líneas llevan el prefijo `[Terrakeep]` para poder filtrarlas con un grep limpio.
- Para inspeccionar un `.tmod` ya compilado (propio o ajeno) sin descompilar nada:
  `node tmod-extract.js <ruta.tmod>` desde
  `C:\Users\adrian\Downloads\Terrasavr-Win\Terrasavr-Calamity-Beta\resources\app\`.

## Reglas
- Commit **antes** de cualquier cambio grande y **también después de cada cambio verificado**,
  sin esperar a que se pida (misma disciplina que el repo hermano).
- Si algo falla **dos veces seguidas por la misma causa**: parar, escribirlo en `bitacora.md`
  y no insistir en bucle.
- Nunca probar con los personajes/mundos reales del usuario (`adrian`, `Eldelgas`,
  `Terrariano`, `adriandres`, `Afueras_de_Larvas_de_gusano`) sin que lo pida explícitamente.
  Usar siempre el sandbox `-tmlsavedirectory`.
- Ningún workstream se da por cerrado con "compila": hay que probarlo en el juego real.
