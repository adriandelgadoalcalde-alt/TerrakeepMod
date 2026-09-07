# Terrakeep (mod de tModLoader)

Del creador de [Terrakeep de escritorio](https://github.com/adriandelgadoalcalde-alt/Terrakeep)
(el editor offline de personajes y mundos de Terraria) - ahora la misma idea, pero **en vivo,
dentro de la partida**: un editor completo de personaje y mundo que corre como mod real de
tModLoader, sobre lo que ya tienes cargado. No abre ni toca ningún `.plr`/`.wld`: escribe
directamente sobre el personaje y el mundo de la sesión, y todo lo que hace se puede deshacer
con Ctrl+Z.

## Cómo se abre

Un único panel, tecla **K** (reasignable en Ajustes > Controles del propio juego) o el icono de
Terrakeep junto al bestiario y los emotes, con el inventario abierto.

## Qué hace

Seis pestañas, todas con la interfaz nativa de Terraria (sin ninguna ventana externa):

- **Personaje** - inventario, hucha/caja fuerte/forja/bóveda, los tres conjuntos de equipo (con
  editor de cantidad y papelera reales), buffs (con árbol de carpetas navegable), apariencia
  (peinado, variante, tintes, los siete colores y una **vista previa en vivo** del personaje,
  con o sin armadura puesta) y los 13 desbloqueos permanentes.
- **Librería** - catálogo navegable de TODOS los objetos de la partida, incluidos los de
  cualquier mod instalado (no solo Calamity), con buscador (`coma` = o, `espacio` = y, `#id`,
  `.texto` busca en el tooltip). Coge un objeto y suéltalo en cualquier contenedor real, o
  arrástralo al recuadro de edición para cambiarle la cantidad o el prefijo, o para tirarlo a la
  papelera.
- **Builds** - equipo recomendado por etapa y clase, con "ya lo tienes" y auto-equipar a
  cualquiera de los tres conjuntos. Coloca primero lo que ya tienes; lo que te falte lo trae
  directamente del catálogo de la Librería (con su mejor prefijo real), sin tocar nunca nada
  que ya tuvieras puesto en otro sitio.
- **Investigación** - cuánto llevas investigado de cada carpeta en Modo Viaje, y cómo
  completarlo o quitarlo - pasa por la API oficial del juego, así que el menú de duplicar de
  Modo Viaje refleja exactamente lo mismo.
- **Exploración** - mini-mapa navegable con las texturas reales del juego, búsqueda de
  minerales, gemas, tesoros, cofres, NPCs, líquidos y paredes por todo el mundo, salto al mapa
  vanilla a pantalla completa con marcadores propios, y cambio de dificultad en vivo con
  avisos claros de sus riesgos reales.
- **Ajustes** - idioma (Español/English) en vivo sin reiniciar, historial de deshacer/rehacer,
  y la lista real de atajos de teclado.

Es una herramienta puramente local: no añade contenido al juego, no hace falta sincronizarla en
multijugador y no cambia nada de la partida por su cuenta.

## Instalación

Este repo es el **código fuente** del mod (`ModSources`, no un `.tmod` compilado). Para
compilarlo tú mismo:

1. Copia esta carpeta a `Documents\My Games\Terraria\tModLoader\ModSources\TerrakeepMod\`.
2. Desde dentro del propio tModLoader: menú **Herramientas para desarrolladores de mods →
   Compilar y recargar** (o `scripts\compilar.ps1`, que documenta por qué compilar en dos fases
   hace falta en Windows).
3. Actívalo desde el menú **Mods** del juego.

Requiere tModLoader (probado en 2026.7.3.0). Compatible con o sin Calamity Mod instalado - la
Librería y Builds detectan solos qué mods hay cargados.

## Arquitectura

Reutiliza [`Terrakeep.Core`](https://github.com/adriandelgadoalcalde-alt/Terrakeep) (los
catálogos de datos del proyecto hermano de escritorio, ahora con doble destino `net10.0`/`net8.0`
para poder cargar dentro del runtime de tModLoader) como dependencia real vía `dllReferences` -
pero a diferencia de la app de escritorio, **nunca parsea ningún archivo**: todo se lee y se
escribe en vivo sobre los objetos reales del juego (`Main.LocalPlayer`, `Main.tile`,
`Main.chest`...).

Toda la interfaz está construida con los bloques nativos de Terraria (`IngameFancyUI`,
`UIPanel`, `UIList`, `ItemSlot` reutilizado tal cual de vanilla) - sin ninguna ventana ni
tecnología externa al juego.

## Autoría y créditos

Terrakeep está diseñado y desarrollado por **IncrediBad**.

Terraria, tModLoader y Calamity Mod son propiedad de sus respectivos autores (Re-Logic, el
equipo de tModLoader y el equipo de CalamityMod). Terrakeep no está afiliado con ninguno de
ellos.

## Licencia

Ver [`LICENSE.md`](LICENSE.md).
