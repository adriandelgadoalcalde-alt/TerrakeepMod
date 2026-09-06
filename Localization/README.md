# Textos de Terrakeep: cómo se traducen

Nació como entregable de **WS7** para explicarle a los demás workstreams cómo migrar sus textos.
**Esa migración YA ESTÁ HECHA** (ronda de cierre del 6-sep-2026): las seis áreas del panel salen
enteras de los `.hjson`, en español y en inglés, y no queda ni un literal de interfaz en el C#.
Lo que sigue explica el mecanismo, que es el que hay que seguir usando para cualquier texto
nuevo.

## Antes de tocar nada: los `.hjson` NO se editan a mano

Los genera **`scripts/generar-localizacion.py`** a partir de UNA sola tabla de
`(clave, español, inglés)`. Con ~430 claves, mantener dos archivos a mano se descuadra solo: la
tabla única hace imposible que a un idioma le falte una clave que el otro sí tiene, y el script
comprueba además que no haya claves repetidas.

Dos reglas que el propio script verifica y que vienen de fallos reales:

- **Ningún valor puede empezar ni acabar con un espacio.** tModLoader reescribe estos archivos al
  cargar el mod, y un valor que lleve comillas dobles dentro se guarda como cadena de triple
  comilla y al releerlo PIERDE los espacios de los bordes. Los separadores van en la plantilla
  que concatena, no en el trozo.
- **Lo que se comitea es el archivo que deja el JUEGO**, no el que escribe el script: tModLoader
  normaliza el formato al cargar, y si se comiteara el otro `git status` saldría sucio cada vez
  que alguien juega. El ciclo es: ejecutar el script, lanzar el mod una vez, comitear.

## Cómo se comprueba que no queda nada sin traducir

`scriptserificar-idiomas.ps1`. Recorre las once vistas del panel dos veces, en español y en
inglés, recoge todo el texto que se está enseñando (leído de los propios widgets) y lista las
cadenas que salen IGUALES en los dos idiomas. Si una frase se ve igual en los dos, o está escrita
a pelo en el C# o le falta la clave. Deja además una captura real por vista e idioma.

## El mecanismo, en corto

- Un archivo **por idioma** en esta carpeta, con el nombre `<cultura>_Mods.TerrakeepMod.hjson`.
  Ahora mismo: `es-ES_Mods.TerrakeepMod.hjson` y `en-US_Mods.TerrakeepMod.hjson`.
- El nombre del archivo determina el **prefijo** de todas sus claves (`Mods.TerrakeepMod.`) y a
  qué idioma pertenecen. Lo resuelve `LocalizationLoader.TryGetCultureAndPrefixFromPath`.
- Dentro, una clave por línea. Se admiten puntos en el propio nombre de la clave (es lo que hace
  este mod), porque tModLoader construye la clave final concatenando los nombres de propiedad del
  JSON, y una propiedad puede llamarse `Ajustes.Titulo` tal cual.
- Se leen con `Language.GetTextValue("Mods.TerrakeepMod.<clave>")`. En este mod, envuelto en
  `Idiomas.Texto("<clave>")` (`Common/Ajustes/Idiomas.cs`), que añade el prefijo y admite
  sustituciones `{0}`.
- Si una clave no existe, tModLoader **devuelve la clave completa** en vez de fallar: se ve en
  pantalla que falta, en vez de quedar un hueco silencioso.

## Cómo migrar un panel existente

1. Por cada texto fijo del panel, añadir la misma clave a **los dos** `.hjson`, agrupada bajo un
   prefijo propio del workstream para que no choquen:

   ```
   # es-ES_Mods.TerrakeepMod.hjson
   Personaje.Titulo: Terrakeep - Personaje
   Personaje.Vida: "Vida: {0}/{1}"
   ```
   ```
   # en-US_Mods.TerrakeepMod.hjson
   Personaje.Titulo: Terrakeep - Character
   Personaje.Vida: "Health: {0}/{1}"
   ```

   Los valores que contengan `{` van **entre comillas**: sin ellas, hjson intentaría leerlos como
   un objeto.

2. Sustituir el literal por la llamada:

   ```csharp
   _titulo.SetText(Idiomas.Texto("Personaje.Titulo"));
   _vida.SetText(Idiomas.Texto("Personaje.Vida", jugador.statLife, jugador.statLifeMax));
   ```

3. Si el panel puede estar abierto cuando cambie el idioma, engancharse al aviso para volver a
   pedir los textos (patrón completo en `UI/Ajustes/PanelAjustesState.cs`):

   ```csharp
   public override void OnActivate()   { base.OnActivate(); Idiomas.Cambiado += RefrescarTextos; RefrescarTextos(); }
   public override void OnDeactivate() { Idiomas.Cambiado -= RefrescarTextos; base.OnDeactivate(); }
   ```

4. El nombre visible de un `ModKeybind` **no** se pone en código: sale de la clave
   `Keybinds.<NombreDelAtajo>.DisplayName`, que el propio `ModKeybind` registra solo.

## Cambio de idioma en vivo

Lo hace el panel de Ajustes (tecla **J**) con `LanguageManager.Instance.SetLanguage(cultura)`, la
misma llamada pública que usa el menú de idioma del propio juego. Recarga de golpe los textos de
Terraria y los de todos los mods, y avisa por `ModSystem.OnLocalizationsLoaded`.

**Efecto que hay que conocer**: en tModLoader **no existe un "idioma solo para mi mod"**. Un
`LocalizedText` guarda un único valor, el de la cultura activa, así que poner Terrakeep en inglés
pone todo el juego en inglés. Por eso el valor por defecto del ajuste es *"el del juego"*: si el
usuario no elige nada, Terrakeep no le toca el idioma a nadie. La elección se recuerda entre
partidas en el `ModConfig` (`Common/Ajustes/AjustesConfig.cs`).

## Cosas que ya están comprobadas (no volver a descubrirlas)

- **`Language.GetTextValue` funciona en cualquier momento del juego**, incluso justo después de un
  cambio de idioma en caliente: verificado en el juego real, la misma clave devuelve
  `"Terrakeep - Ajustes"` y `"Terrakeep - Settings"` sin reiniciar nada.
- **El `-build` de tModLoader NO reescribe estos `.hjson`.** Se comprobó comparando los hashes
  antes y después de empaquetar: quedan exactamente como se dejaron.
- **Las tildes y la ñ se ven bien**: la fuente de Terraria las trae, el juego ya se distribuye en
  español. Los `.hjson` van en UTF-8.
- El idioma por defecto de tModLoader (`GameCulture.DefaultCulture`) es **en-US**: las claves que
  solo existan en `es-ES` funcionan igual, pero conviene tenerlas en los dos archivos para que
  quien juegue en inglés no vea aparecer texto en español a medias.
