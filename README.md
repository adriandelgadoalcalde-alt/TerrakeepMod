# Terrakeep (mod de tModLoader)

Del creador de [Terrakeep de escritorio](https://github.com/adriandelgadoalcalde-alt/Terrakeep)
(el editor offline de personajes y mundos de Terraria) - ahora la misma idea, pero **en vivo,
dentro de la partida**: un editor completo de personaje y mundo que corre como mod real de
tModLoader, sobre lo que ya tienes cargado. No abre ni toca ningún `.plr`/`.wld`: escribe
directamente sobre el personaje y el mundo de la sesión, y todo lo que hace se puede deshacer
con Ctrl+Z.

> Preparación técnica para publicarlo en el Steam Workshop de tModLoader: ver
> [`PUBLICAR-WORKSHOP.md`](PUBLICAR-WORKSHOP.md).

## Cómo se abre

Un único panel, tecla **K** (reasignable en Ajustes > Controles del propio juego) o el icono de
Terrakeep junto al bestiario y los emotes, con el inventario abierto.

## Capturas

<p align="center">
  <img src="docs/screenshots/01-guia-calamity.png" width="49%" alt="Pestaña Guía con Calamity instalado: cabecera con reloj, objetivo y chip de DPS, tarjeta con el sprite real de Yharon, lo que te falta y la lectura del jefe" />
  <img src="docs/screenshots/02-album.png" width="49%" alt="Pestaña Álbum con las capturas automáticas de cada hito cerrado, bajo la barra de pestañas con icono de sprite" />
</p>
<p align="center">
  <img src="docs/screenshots/03-builds.png" width="49%" alt="Pestaña Builds con dos filas de filtros, el anillo de progreso 0/13 junto al alternador Vanilla, las tres columnas de armadura, armas y accesorios y los códigos de build" />
  <img src="docs/screenshots/04-libreria.png" width="49%" alt="Pestaña Librería navegando la carpeta Mascotas de Jefes, con los ocho destinos (incluido Cofre) abajo" />
</p>

## Qué hace

Ocho pestañas, todas con la interfaz nativa de Terraria (sin ninguna ventana externa):

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
- **Guía** - brújula de progresión en tiempo real, la pestaña más nueva del panel. Cubre **el
  juego entero**: 46 tramos y 176 pasos, los 21 tramos vanilla (jefes y eventos, del Ojo de
  Cthulhu a la Luna de Escarcha) y, si tienes Calamity instalado, 25 tramos más propios del mod
  (desde el Desert Scourge hasta Supreme Calamitas) que se suman encima de la progresión vanilla
  sin sustituirla. Para el objetivo de ahora mismo te dice **qué es, hacia dónde cae** (con una
  brújula dorada real sobre el mapa del juego cuando hay un sitio concreto que señalar - mazmorra,
  templo, jungla, nieve -, y una dirección en vertical cuando no lo hay), un **medidor de
  preparación** con los requisitos que de verdad te faltan (arma con el daño mínimo real,
  accesorios recomendados, vida/maná mínimos...), el **por qué** (qué mecánica del juego hay
  detrás, contado como lo contaría la propia wiki, nunca con jerga de programador) y el **cómo**.
  A la derecha, la **lectura del jefe** (su vida, defensa y daño reales de tu partida y tu
  dificultad) y la **hoja de ruta** de lo que viene después. Todo se lee en vivo de tu personaje y
  tu mundo, fotograma a fotograma - equípate algo o mata a un jefe con el panel abierto y se nota
  al instante.
- **Álbum** - el diario visual de tu progreso: en cuanto cierras de verdad un tramo de la Guía
  (obligatorio u opcional) jugando, se dispara sola una captura real de pantalla que queda listada
  aquí con su nombre y su fecha, sin que tengas que acordarte de pulsar nada. Pulsa una entrada
  para abrir la captura a tamaño real con el visor de imágenes de tu sistema.

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

## Novedades

### 0.6.0 (29-sep-2026)

- **Rediseño visual del panel** (TM1-TM6): pestañas con el sprite real del objeto/jefe en vez de
  solo texto, chips de cabecera con un tercer indicador de DPS en tiempo real, transición suave al
  cambiar de pestaña, alto dinámico del panel y checklist con icono real por requisito.
- **Diez funciones nuevas**: entrenador de jefe en la Guía (vida/defensa/daño de tu partida real,
  con el Muro de Carne excluido por un límite real del juego), sonar de estructuras (incluida la
  isla flotante de Calamity), diario automático de la partida (día/equipo/tiempo por cada hito),
  pestaña **Vecindad** en Exploración (felicidad real de los NPC de pueblo), los cofres del mundo
  como octavo destino navegable de la Librería, **rebobinado de terreno** (deshacer cambios de
  tiles y cofres con una foto real del estado anterior), marcadores de casas de NPC en el mapa,
  Laboratorio de Draedon (Calamity), aviso de "El grupo está listo" en partidas multijugador y
  códigos de build compartibles (`TKBUILD1:...`).
- **Builds** rediseñada: de cuatro filas de filtros a dos (alternador + desplegable + anillo de
  progreso), con el reparto de equipo por clase corregido para que ya no se solape.
- **Arreglos reales**: el anillo de progreso de Builds se salía de su marco; el texto de "Vecindad"
  se solapaba consigo mismo a UIScale alto; el desplegable de etapa de Builds recortaba texto;
  Rebobinar perdía la foto de referencia al cerrar el panel; el chip de DPS de la cabecera se salía
  de su pastilla con "Sin golpes recientes"; el editor de objeto flotante de Librería/Personaje
  tenía dos bugs reales (uno de ellos quedaba "atrapado" sin poder cerrarse); renombrar un conjunto
  desplazaba el texto al parpadear el cursor; la pestaña Vecindad reconstruía su lista y parpadeaba
  sin que hubiera ningún cambio real; el mini-mapa interno de Exploración ya dibuja los NPC de
  pueblo reales.
- **Auditoría de UIScale/resolución** completa (48 combinaciones de resolución × escala): 3 defectos
  reales de solape/desbordamiento, todos acotados a 1366×768 al 150%, cerrados con un reflujo
  vertical general que comprime posición y escala de texto a la vez (nunca solo una de las dos).
- **Paridad de terminología** con Terrakeep de escritorio (español e inglés) y sincronización de las
  etiquetas de la Librería.
- Preparación técnica para el Steam Workshop de tModLoader (sin publicar todavía: falta el paso
  manual de "Publish" desde dentro del juego) - ver [`PUBLICAR-WORKSHOP.md`](PUBLICAR-WORKSHOP.md).
- Versión del mod: `0.5.0` → `0.6.0`.

### 0.5.0 (16-sep-2026, madrugada)

- **Arreglado un bug real**: la Guía podía quedarse marcando un paso muy temprano como pendiente
  para siempre, aunque el jefe llevara mucho tiempo derrotado en el mundo real - un requisito que
  ningún editor de archivos estático puede comprobar de verdad (el daño real del arma) se trataba
  como bloqueante en vez de "sin datos todavía". El cerebro de evaluación de la Guía queda
  consolidado en un solo sitio compartido con Terrakeep de escritorio.
- **Auditoría de precisión completa** de los 46 tramos (116 pasos) contra el código real
  decompilado del juego y Calamity, no de memoria: entre otros, romper altares exige el Martillo
  Sagrado del Muro de Carne (no basta con matar al Devorador/Cerebro); Piratas y Legión de
  Escarcha son de Modo Difícil, no prehardmode; los TRES mecánicos hacen falta juntos para
  Plantera, no uno cualquiera; la Emperatriz de la Luz se enfurece de día, no de noche; el Golem
  tiene invulnerable el cuerpo mientras la cabeza siga montada, no al revés; oleadas reales de las
  Lunas y objetos de recompensa inventados sustituidos por los reales.
- Versión del mod: `0.4.0` → `0.5.0`.

### 0.4.0 (15-sep-2026)

- **Cobertura absoluta de la Guía**: 25 tramos nuevos de Calamity (60 pasos), sobre los 21 tramos
  vanilla que ya existían - la progresión del panel cubre ahora el juego entero, con o sin
  Calamity instalado. Arquitectura nueva y verificada contra el `.tmod` real de Calamity (banderas
  de jefe por reflexión, NPC/objetos de invocación resueltos por su nombre real en tiempo de
  carga), nunca supuesta.
- **Reescritura completa de tono** de todo el texto que lee el jugador en la Guía (español e
  inglés, vanilla incluido): fuera nombres de clase, código C# literal y variables internas que se
  habían colado en pantalla; dentro, la misma voz de aventura que usa la propia Terraria Wiki.
- Verificado en vivo con el cliente gráfico real y Calamity cargado
  (`scripts\verificar-guia.ps1 -Calamity`): compila sin errores, `AUTOPRUEBA GUIA COMPLETA`, 0
  comprobaciones en rojo.
- Versión del mod: `0.3.0` → `0.4.0`.

## Autoría y créditos

Terrakeep está diseñado y desarrollado por **IncrediBad**.

Terraria, tModLoader y Calamity Mod son propiedad de sus respectivos autores (Re-Logic, el
equipo de tModLoader y el equipo de CalamityMod). Terrakeep no está afiliado con ninguno de
ellos.

## Licencia

Ver [`LICENSE.md`](LICENSE.md).
