# Textos de Terrakeep: cómo se traducen

Entregable de **WS7**. Explica el mecanismo real de localización de tModLoader que usa este mod y
**cómo tienen que migrar sus textos los demás workstreams** en la pasada de integración.

Mientras tanto, nadie está bloqueado: un panel con sus textos escritos a pelo en español funciona
perfectamente. Migrarlos es una pasada mecánica que se puede hacer en cualquier momento.

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
