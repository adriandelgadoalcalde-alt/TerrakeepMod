# Publicar TerrakeepMod en el Steam Workshop

Preparación técnica hecha la noche del 15/16-sep-2026 (recomendación D1 de
`KeepQA\v4\I+D-PROXIMOS-PASOS-FAMILIA-KEEP-FABLE.md`, extendida a tModLoader). A diferencia de
los mods de DST, tModLoader **sí** tiene un flujo de publicación integrado en el propio juego -
investigado esta noche contra la documentación oficial (`tmodloader.app/docs/publishing-mods.html`,
wiki de GitHub `tModLoader/tModLoader`).

## 1. Qué mecanismo usa tModLoader de verdad

1. Compilar el mod normalmente (`.tmod` generado a partir de este `ModSources\TerrakeepMod\`).
2. Dentro del juego: **Workshop → Develop Mods**. Tiene que aparecer un botón **Publish** junto
   a TerrakeepMod (si no sale, recompilar y recargar mods).
3. Al pulsar **Publish**, tModLoader abre un formulario con opciones adicionales (nombre,
   tags del Workshop) rellenadas a partir de `build.txt`/`description.txt`.
4. Pulsar **Publish** otra vez en ese formulario para confirmar. Requiere estar logueado con
   Steam (el propio tModLoader ya corre bajo la cuenta de Steam del usuario) y, según la propia
   documentación oficial, la cuenta necesita **al menos 5 USD gastados en Steam** para no caer
   en las restricciones de cuenta limitada (esto no se puede comprobar ni solucionar desde
   aquí - es una condición de la cuenta del usuario, no del mod).

Este es el paso que no puedo dar yo: abrir tModLoader, entrar a Workshop → Develop Mods y pulsar
Publish es una acción dentro de la sesión de Steam del usuario.

## 2. Qué se ha preparado de verdad esta noche

`build.txt` y `description.txt` YA estaban listos de antes (ver commits del 8-sep-2026:
`homepage` real apuntando al repo público, `version`, `side = NoSync`, `buildIgnore` cuidado).
Lo que faltaba, confirmado contra la documentación oficial de publicación:

| Archivo | Qué es | Cómo se hizo |
|---|---|---|
| `icon_workshop.png` | Icono de la ficha del Workshop (hasta 512×512; 480×480 es el mínimo real exigido) - **distinto** de `icon.png` (80×80, el que se ve dentro del juego) | Generado a partir del `icon.png` real del mod (mismo hexágono azul marino/dorado con "T"), reescalado a 512×512 con remuestreo de calidad (Lanczos - el arte es plano/vectorial, no pixel art, así que reescalar así da un resultado más limpio que el vecino-más-cercano que recomienda la documentación oficial para sprites de píxeles) |
| `description_workshop.txt` | Descripción de la ficha del Workshop, en BBCode de Steam - **archivo distinto** de `description.txt` (que es la que se ve dentro del juego, en texto plano). No existía | Redactado a partir del contenido real de `description.txt` y del README, con encabezados y lista en BBCode |

`build.txt` no necesitó ningún cambio: ya tenía todo lo que la documentación oficial pide
(`homepage`, `version`). No se ha tocado `buildIgnore`: ambos archivos nuevos quedan sin excluir
a propósito, igual que `icon.png`/`description.txt`, que tampoco lo están.

## 3. Tags del Workshop

Se eligen dentro del propio formulario "Publish" del juego, no en ningún archivo del repo. Con
lo que hace TerrakeepMod (editor de personaje/mundo en vivo, guía, librería, trainer de builds),
las categorías reales de tModLoader que mejor encajan son **"Tools"** (o equivalente de utilidad/
QoL, según qué lista de tags tenga la build instalada en ese momento - tModLoader las cambia de
cuando en cuando) - no hay un tag "trainer" oficial en tModLoader, así que no forzar uno que no
exista.

## 4. Cómo se ha verificado

- `icon_workshop.png`: confirmado 512×512 RGBA con Pillow tras generarlo (no solo "debería
  medir eso").
- El contenido de `description_workshop.txt` está sacado literalmente de `description.txt`
  (ya verificado en el juego en rondas anteriores del proyecto - ver `bitacora.md`), solo
  reformateado a BBCode: no se inventa ninguna función nueva.
- No se ha relanzado tModLoader esta noche para llegar hasta la pantalla real de Workshop →
  Develop Mods (ver límite abajo), pero los dos archivos nuevos son datos estáticos que
  tModLoader lee en el momento de publicar, sin ningún efecto sobre la carga del mod en juego -
  no hay riesgo de que rompan nada que ya estuviera verificado.

## 5. Límite real

No he generado capturas de la pantalla real "Workshop → Develop Mods → Publish" dentro de
tModLoader: llegar hasta ahí exige que el juego esté corriendo bajo la sesión de Steam ya
autenticada del usuario y navegar un menú que nunca se había recorrido en este proyecto (los
arneses existentes en `scripts\` verifican menús de ANTES de Workshop, no ese). No es prudente
automatizar clics a ciegas ahí dentro de su cuenta real de Steam sin que él lo vea primero.
`icon_workshop.png` y `description_workshop.txt` están verificados por su cuenta (dimensiones,
contenido) y listos para que tModLoader los recoja solo en cuanto se pulse Publish.
