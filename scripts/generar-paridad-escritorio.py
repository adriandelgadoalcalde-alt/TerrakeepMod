# -*- coding: utf-8 -*-
"""Uso: python scripts/generar-paridad-escritorio.py

Genera docs/paridad-escritorio-3.3.0.md a partir de UNA tabla de grupos y comprueba que
TODOS los commits de Terrakeep escritorio del rango auditado quedan asignados a un grupo
(exactamente uno). Si falta o sobra un commit, se para con error: nada de "revisados a ojo"."""
import io, os, re, subprocess, sys, collections

# Repo hermano (solo lectura). Se puede cambiar con la variable TERRAKEEP_ESCRITORIO.
REPO_ESCRITORIO = os.environ.get('TERRAKEEP_ESCRITORIO', os.path.join(os.path.expanduser('~'), 'Downloads', 'Keep', 'Terrasavr-Win', 'Terrasavr-Native'))
SALIDA = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'paridad-escritorio-3.3.0.md')
DESDE = '2026-09-20 00:00'
# Fin del rango auditado (HEAD de escritorio al cerrar la auditoria). Para una ronda nueva se
# cambia aqui y se asignan a un grupo los commits nuevos (el script se para si falta alguno).
HASTA = 'b18aa8c59a3627028d4e43259de15dab50a8c528'

# (id, decision, funcionalidad, que es / que hace el mod, commits)
G = [
 # ------------------------------------------------------------------ (b) PORTADO
 ('B1', 'b', 'Editor de objeto: botones rápidos +10 / +100 / MAX contra el maxStack real',
  'Portado en v0.7.0: fila "+10 / +100 / Máx" en la tarjeta flotante "Editar objeto" de la Librería, contra `Item.maxStack` del objeto vivo (vanilla o de cualquier mod, sin catálogo que mantener). Mismo camino de escritura e historial Ctrl+Z que "-"/"+"; con la pila llena se apagan. `Common/Personaje/CantidadRapida.cs` (+ tests), `EditorCantidadTk`, `TarjetaEdicionFlotanteTk`; en el juego, `AutopruebaLibreria` paso 27b.',
  '09195ce0'),
 ('B2', 'b', 'Mundo: banderas de invasión Goblins / Legión de Escarcha / Piratas editables',
  'Portado en v0.7.0: fila "Invasiones" en Exploración > Este mundo que escribe en vivo `NPC.downedGoblins`/`downedFrost`/`downedPirates` (los mismos campos que el motor graba en el bloque de banderas del .wld y que ya lee la Guía). Resaltado = vencida, Ctrl+Z, aviso de permanencia de la ficha, solo partida de un jugador. `Common/Exploracion/InvasionesMundo.cs`, `PestanaMundo`; en el juego, `AutopruebaExploracion` 36-38.',
  'cc1d4ddc'),
 ('B3', 'b', 'Mundo: añadir / quitar NPCs de pueblo',
  'Portado en v0.7.0: Exploración > Vecindad gana "Traer vecino..." (lista oficial `VanillaTownNpcRoster`, la MISMA de escritorio vía `lib/Terrakeep.Core.dll`; llega al punto de aparición y sin casa, igual que en escritorio) y "Echar" por fila (lo retira sin matarlo). Las dos con Ctrl+Z; solo un jugador. `VecindadEditable.cs`, `VecinosQueFaltan.cs` (+ tests), `PestanaVecindad`; en el juego, `AutopruebaExploracion` 39-44.',
  'ec916e8b'),

 # ------------------------------------------------------------------ (a) YA EXISTE
 ('A1', 'a', 'Terminología oficial de Terraria en ES en las carpetas de la Librería (tildes, L-01) y armaduras de Calamity separadas',
  'Ya existe: `Assets/vanilla_library_labels_es.json` del mod es byte a byte idéntico al de escritorio 3.3.0 (comprobado con `cmp`), y la separación de nombres de armaduras de Calamity vive en `LibraryTreeBuilder` de Terrakeep.Core, que el mod usa desde `lib/Terrakeep.Core.dll` regenerado el 29-sep desde la HEAD 3.3.0. Verificado en vivo el 29-sep (bitácora).',
  '968d6e67 a07e4f0c'),
 ('A2', 'a', 'Guía: banderas de progreso tardías y de invasión, cristales de vida y defensa total reales',
  'Ya existe: `BanderasGuia` lee en vivo `NPC.downedGoblins/downedFrost/downedPirates/downedFishron/downedMartians/downedAncientCultist/downedTowers/downedEmpressOfLight/downedQueenSlime/downedDeerclops`...; los cristales usan `Player.ConsumedLifeCrystals` real (no derivado de la vida máxima) y la defensa `Player.statDefense` (ya incluye armadura, accesorios y buffs). Escritorio necesitó leerlos del .wld/.plr y calcularlos; en el juego son el dato del motor.',
  '660a2ec1 b88866d7 ea405518 9250e6bd'),
 ('A3', 'a', 'Guía: sprites reales de objetos, vecinos y jefes (vanilla, Devorador/Cerebro y Calamity)',
  'Ya existe: `IconoJefe`/`IconoRequisito` dibujan con las texturas del propio juego (incluidos los jefes de Calamity cargado). El sprite del Devorador/Cerebro se propagó el mismo 28-sep (commit `6ff88f5` del mod).',
  '1770822e 69998998 15f821ff 8f4515fc'),
 ('A4', 'a', 'Guía de Calamity leída sin el juego (.twld hermano) y daño de arma real',
  'Ya existe: el mod lee `CalamityMod.DownedBossSystem` en vivo por reflexión (`BanderasGuia.AgregarBanderasCalamity`) y evalúa con el mismo `GuideEvaluationEngine` de Terrakeep.Core (DañoArma base+prefijo incluido).',
  'af46351f 7efbec5e'),
 ('A5', 'a', 'Capa de la Guía sobre el mapa: marcador de Mazmorra y bandas de profundidad',
  'Ya existe el equivalente honesto dentro del juego: la Guía da la dirección real a la Mazmorra (`EstadoGuia.LadoHorizontalDelPaso`, `Main.dungeonX`) y la capa de profundidad actual del jugador (`EstadoJugadorGuia`, `Main.worldSurface/rockLayer`); el mapa del propio juego ya pinta las capas. La brújula del mod es "brújula, no GPS" a propósito.',
  '7b93b97d'),
 ('A6', 'a', 'Vista previa del personaje fiel al juego (canales de accesorio, alas, barba, tintes, Hide, FaceHead, FloatingTube, accesorios de Calamity, loadouts)',
  'Ya existe por construcción: `MunecoTk` dibuja con `Main.PlayerRenderer`, el mismo renderizador que la selección de personaje y el Maniquí del juego; todo lo que escritorio tuvo que reconstruir capa a capa lo pinta el propio motor.',
  '5025fdbc 371c6550 47240093 02eefb04 2ef72eb1 abc0faeb 067f173c e3a51b68 647ac665 7131dc85 ee637b52 0c12b676 56db2d11 b60903ef 422b5a16 e5848664 9dc4608e 99793a56 9dbaa741 2c9612aa 67b13a63 6831a6e6 4486ad3c b9d10556'),
 ('A7', 'a', 'Cofres del mundo: editar contenido y prefijos, guardar, columnas y ranuras vacías',
  'Ya existe: "Cofres del mundo" de la Librería (`SelectorCofreMundoTk`) edita `Chest.item[]` del mundo cargado con `ItemSlot` de vanilla; el prefijo se cambia con el mismo editor de prefijo de la Librería y el guardado es el nativo del juego (aviso en Este mundo). Los fallos de escritorio (prefijo sin efecto, botón Guardar deshabilitado, columnas/ranuras del inspector WPF) no tienen equivalente.',
  '213ecf10 dfa3ad3c 973c0c8e 83b466fc bdae3f6a 490d90ac 9f38abaa'),
 ('A8', 'a', 'Informe extendido del mundo cargado',
  'Ya existe: la ficha de Exploración > Este mundo (nombre, semilla, tamaño, modo, progreso, mal del mundo, semillas secretas, aparición, exploración, autoguardado y, desde v0.7.0, invasiones). El comparador de dos mundos es c (C4).',
  'c4bb98db'),
 ('A9', 'a', 'Bestiario del mundo con sprites reales',
  'Ya existe dentro del juego: el Bestiario nativo (con sprites, animaciones y filtros) y su resumen de completitud en Personaje > Completitud. Escritorio lo reconstruye porque fuera del juego no hay Bestiario.',
  '775e3b68'),
 ('A10', 'a', 'Personaje: navegación Equipamiento / Inventario / Almacenes',
  'Ya existe: sub-pestañas propias de Personaje (Inventario, Almacenes, Equipo...). Lo que cambió en escritorio (tablero de scroll continuo, páginas NAV123, selector 1/2/3 y sus desalineaciones/clamps de ScrollViewer) es la forma WPF de navegar, sin equivalente en la UI de Terraria.',
  '7e2ae0b3 db0e15eb 306a7d41 28654a0d dfc7ebf5 309e402e 547d09d3 a434580e'),
 ('A11', 'a', 'Iconos de los NPC en el mapa de Exploración',
  'Ya existe: el mini-mapa del mod pinta las cabezas de los NPC de pueblo desde el arreglo del 25-sep (bitácora del mod, canario NPC-EN-MINIMAPA).',
  '40c24460'),
 ('A12', 'a', 'Apariencia: los nombres de las tarjetas de color siguen al idioma real',
  'Ya existe: `FilaColorTk` recibe el rótulo como `Func<string>` y lo resuelve al dibujar, así que cambia con el idioma en vivo (patrón del mod desde WS7).',
  'aade5ac7'),
 ('A13', 'a', 'Nombres de buffs corregidos ("Ofensivo", "Especial") y contraste de los prefijos de Calamity (L-04)',
  'Ya existe: el árbol de buffs del mod es `BuffTreeBuilder` de Terrakeep.Core (lib regenerado desde 3.3.0), así que hereda el nombre visible corregido. El mod no tiene un color propio de bajo contraste para prefijos de Calamity (usa el del resto de filas), así que el defecto L-04 no existe aquí.',
  '223366bf'),

 # ------------------------------------------------------------------ (c) NO APLICA
 ('C1', 'c', 'Responsive global de ventanas (fases A-G: columnas adaptativas, un solo scroll owner, SizeClass, WARN-01/02, clasificación de regiones)',
  'No aplica: el mod no tiene una ventana WPF redimensionable; el tamaño lo fijan la resolución y `Main.UIScale` de Terraria. El equivalente real ya está hecho y verificado: auditoría UIScale 48/48 celdas + reflujo vertical (`ReflowVertical`) del 29-sep, y `verificar-espaciado.ps1` a 3 resoluciones x 2 idiomas.',
  '59b5fd2d 8f3094dc 6d37f4e9 4e20a72a c7e2ab66 0900908b ae8d23f9 9c3df863 9112948b 378ef89a 2723059a 1433d61e 662e4641 b06bedbd cd894809 03513c1c feb490b9 1e312d66 f83548fa 2ad99296 5f366010 5c0b3156 31bef0d7 a36947ca 8f09e68e f21e1c7a 3aef4db4 b18aa8c5'),
 ('C2', 'c', 'Arrastre: fantasma sin recuadro, recortes, cursor de mano que agarra y sprite en la palma',
  'No aplica: son artefactos del `DragDrop` OLE de WPF. El mod usa `ItemSlot.Draw/Handle` de vanilla: el objeto cogido lo pinta el propio motor pegado al cursor, sin recuadro (verificado en vivo el 29-sep).',
  '70b3def2 902b3ae7 02f82f5f bb8c76cd 020fde86 a72c0f7d'),
 ('C3', 'c', 'Botón "Guardar mundo" que junta las ediciones pendientes',
  'No aplica: escritorio edita un .wld sin juego y necesita guardar a mano; el mod edita el mundo vivo y lo persiste el autoguardado/guardado nativo. La ficha avisa SIEMPRE antes de tocar nada ("se escribe en el mundo en el siguiente guardado", fila Autoguardado).',
  '580d398b'),
 ('C4', 'c', 'Comparadores (personaje como pestaña, dos mundos, tarjeta PNG/HTML) y búsqueda "¿dónde está?" en todos los personajes y mundos',
  'No aplica: gestionan la COLECCIÓN de partidas guardadas en disco; dentro del juego solo existe el personaje y el mundo cargados. Dentro de la partida, el mod ya busca en el mundo cargado (Búsqueda) y en sus cofres (Cofres del mundo).',
  'e0a9688c 111a3b3d 11f2453b'),
 ('C5', 'c', 'Laboratorio de personajes: generar un .plr nuevo listo para un build',
  'No aplica: no se crea un personaje con la partida en marcha. El equivalente dentro del juego ya existe: Builds con auto-equipar y códigos de build.',
  '85bb1daa d70ebea0'),
 ('C6', 'c', '"Partida en vivo": el historial se refresca solo y narra los cambios entre copias',
  'No aplica: el mod ES la partida en vivo; cada cambio queda en el historial de Terrakeep con Ctrl+Z/Ctrl+Y y el Álbum registra los hitos.',
  'b56fdb41 e9499539'),
 ('C7', 'c', 'Diagnóstico y "arreglo en un clic" de prefijos ilegales, ranuras fantasma, duraciones de buff desbordadas y .tplr huérfano',
  'No aplica: son defectos de ARCHIVOS importados. Al cargar la partida, tModLoader ya sanea los prefijos (`Item.Prefix` rechaza lo que no pasa `CanApplyPrefix`, Item.cs decompilado) y el mod nunca deja crear combinaciones ilegales (`CatalogoPrefijosLegales`); ranuras y .tplr son formato de archivo.',
  '6275dfff ad69a40f 041968f5'),
 ('C8', 'c', 'Vista previa exportable: imagen, animación de andar y GIF; exportar el mapa a PNG con marcadores',
  'No aplica: sirven para ver/compartir el personaje o el mundo SIN el juego. Dentro del juego el personaje ya se ve animado de verdad, el Álbum guarda capturas reales y el Modo Cámara del juego exporta el mundo a imagen.',
  'fe77df9e fbf18af2 a912adbb'),
 ('C9', 'c', 'Pantalla de Inicio: tarjetas animadas al pasar el ratón, mascotas, globos, "Te toca"/"Tu último mundo", maná/vida/tiempo jugado',
  'No aplica: el mod no tiene pantalla de Inicio ni selector de partidas (eso lo hace el menú del propio juego, que ya enseña el personaje real).',
  '6bf60deb c0e2cab0 3d723f6c 63e9cf05 9c437aec e4bf368c bbf27de1 7058c42b 4101eb33 491a297e 78378806 6dfb3c8d 1ec1c0d4 3f184038 7ef2f32a b6503855'),
 ('C10', 'c', 'Rediseño visual del armazón WPF: rail con iconos, cabecera de una fila, avisos Toast, tipografía y escala de espaciado, badges/píldoras',
  'No aplica: es el armazón de ventanas de escritorio. El mod tuvo su propio catálogo visual (TM1-TM6: pestañas con icono, chips de cabecera, alto dinámico) y su autoprueba de espaciado.',
  'ca2ac1ba 1928a0c1 6ddf8f53 29c152c2 bff5048a c0df5d74'),
 ('C11', 'c', 'Exploración de escritorio a pantalla completa y rediseño de su barra lateral (modos Browse/Inspector/WorldTools, subvistas, resultados, MinHeight, bucle de layout)',
  'No aplica: layout WPF. El mod no puede ocupar la pantalla del juego (convive con el HUD y el mapa grande) y ya separa Mapa / Búsqueda / Este mundo / Vecindad / Rebobinar en sub-pestañas.',
  '0211e071 b7835fb4 99c9cbbd 7e6914f1 774eb9d9 eec5c03f adf13835 d31f00f0 2b206594 44557095 7d801dfe'),
 ('C12', 'c', 'Botón "Personaje" de la cabecera con desplegable (▾/▲, doble toggle)',
  'No aplica: el mod navega con pestañas exclusivas, no con un desplegable que se abre y se cierra con el mismo botón.',
  '119eedb4 2341b678 dc68bf66'),
 ('C13', 'c', 'Fallos propios de controles WPF (título partido letra a letra, niebla azul, overlay tapado, slot medio tapado que desplaza la página)',
  'No aplica: defectos de `TextBlock`/`ScrollViewer`/composición de WPF. El mod tiene sus propias autopruebas de desborde/solape (`AutopruebaEspaciado`).',
  '1f8ba6e1 3c3c4022 43be4958'),
 ('C14', 'c', 'Cambiar de idioma ya no marca el personaje como modificado',
  'No aplica: en el mod no hay estado "modificado sin guardar"; todo se edita en vivo.',
  '8d8da638'),
 ('C15', 'c', 'Tarjetas del historial de versiones a la misma altura',
  'No aplica: el mod no tiene pestaña de Novedades; su historial va en el README y en la ficha del mod.',
  '314f3f26'),
 ('C16', 'c', 'Abrir un mundo pasado por ServidorKeep (--abrir-mundo)',
  'No aplica: argumento de línea de órdenes del ejecutable de escritorio.',
  'd1741a56'),
 ('C18', 'c', 'Guía: "motivo" de lo no evaluable en el árbol, tratamiento de límite estructural y aviso global "sin mundo cargado"',
  'No aplica: son los casos en que escritorio NO puede leer un dato (sin mundo abierto, bandera que el formato .wld no expone). Dentro del juego siempre hay partida y todas las banderas se leen en vivo, así que esos motivos no llegan a darse (`ProveedorEstadoGuiaMod`); el caso no evaluable real (sin arma) ya se enseña en `FilaRequisitoTk`.',
  '527b09c6'),
 ('C17', 'c', 'Interno sin efecto visible: extracción de MainWindow a UserControls y ADR, escritor de .wld, WorldRenderer a Core, puente KeepQA, grabador UIA, detector AR-LAY, arnés y su seguridad, CLAUDE.md, gitignore, privacidad, README/capturas y versión 3.3.0',
  'No aplica: no es funcionalidad de usuario (código interno, pruebas, documentación o publicación de escritorio).',
  '71024199 5c574919 f1930a9d 4bf4f23d c7c9ca40 b74378bc 86e33c7d 774a1a6d 53030d21 852a43b9 07250ca2 17197c09 4790dc4a e228b628 e4721659 23dcc574 05918323 404a1a3e 783c66d3 aab78489 2866269d c9f7a784 85d595eb 202117a0 eb42e87c dcd5b152 fcb53495 63f9aafc bb085c1b e4a29473 47d7c5d1 313c35e1 eb4561a8 9fcda254 8ba736e0 d83fd642 634d99b7 adf6a5ac a055fc6c 6b1c3725 97538a95 d50c1eb7 56f1e236 987dad74 a2299900'),
]

RE_DOC = re.compile(r'^(bit[aá]cora|documenta|confirma en la bit|completa la bit)', re.I)


def main():
    salida = subprocess.run(['git', '-C', REPO_ESCRITORIO, 'log', HASTA, '--reverse', '--since=' + DESDE,
                             '--format=%h|%ad|%s', '--date=format:%d-%m %H:%M'],
                            capture_output=True, text=True, encoding='utf-8', check=True).stdout
    commits = [l.split('|', 2) for l in salida.strip().split('\n') if l]
    por_hash = {c[0][:8]: c for c in commits}
    asignado = {}
    for gid, _, _, _, lista in G:
        for h in lista.split():
            if h not in por_hash:
                sys.exit('ERROR: el commit %s del grupo %s no esta en el rango' % (h, gid))
            if h in asignado:
                sys.exit('ERROR: el commit %s esta en %s y en %s' % (h, asignado[h], gid))
            asignado[h] = gid
    doc = []
    sin = []
    for h, fecha, asunto in commits:
        h8 = h[:8]
        if h8 in asignado:
            continue
        if RE_DOC.match(asunto):
            doc.append((h8, fecha, asunto))
        else:
            sin.append((h8, asunto))
    if sin:
        sys.exit('ERROR: commits sin asignar: ' + '; '.join('%s %s' % s for s in sin))

    cuenta = collections.Counter(g[1] for g in G)
    head = subprocess.run(['git', '-C', REPO_ESCRITORIO, 'log', '-1', HASTA, '--format=%h %ad', '--date=format:%d-%m-%Y %H:%M'],
                          capture_output=True, text=True, encoding='utf-8', check=True).stdout.strip()
    L = []
    L.append('# Paridad de TerrakeepMod con Terrakeep escritorio 3.3.0')
    L.append('')
    L.append('Auditoría cruzada del requirement `0446b3c9` ("cada novedad de escritorio revisada una a una"), '
             'hecha el 29-sep-2026 para la v0.7.0 del mod. Terrakeep escritorio solo se ha LEÍDO.')
    L.append('')
    L.append('- **Rango**: todos los commits de `Terrasavr-Native` desde el %s (punto de la última ronda de paridad '
             'que consta en la bitácora del mod: "Catálogos de funciones y de rediseño visual", 20-sep-2026) hasta el '
             'commit `%s` (HEAD de la 3.3.0 al auditar): **%d commits**.' % (DESDE.split()[0], head, len(commits)))
    L.append('- **Cobertura comprobada por script**, no a ojo: cada commit está en exactamente un grupo de la tabla '
             'o en el apéndice de commits solo de bitácora/documentación (%d). Si faltara o sobrara uno, el '
             'generador (`scripts/generar-paridad-escritorio.py`) se para con error.' % len(doc))
    L.append('- **Decisiones**: (a) ya existe en el mod · (b) aplica y faltaba: portado · (c) no aplica dentro del juego, con motivo.')
    L.append('')
    L.append('| | Grupos | Commits |')
    L.append('|---|---|---|')
    for d, nombre in (('a', 'Ya existe'), ('b', 'Portado en v0.7.0'), ('c', 'No aplica (motivo real)')):
        n = sum(len(g[4].split()) for g in G if g[1] == d)
        L.append('| (%s) %s | %d | %d |' % (d, nombre, cuenta[d], n))
    L.append('| Solo bitácora / documentación | - | %d |' % len(doc))
    L.append('| **Total** | **%d** | **%d** |' % (len(G), len(commits)))
    L.append('')
    L.append('## Tabla por funcionalidad')
    L.append('')
    L.append('| Id | Decisión | Funcionalidad de escritorio | En TerrakeepMod | Commits de escritorio |')
    L.append('|---|---|---|---|---|')
    for gid, d, titulo, texto, lista in G:
        L.append('| %s | **(%s)** | %s | %s | %s |' % (gid, d, titulo, texto, ' '.join('`%s`' % h for h in lista.split())))
    L.append('')
    L.append('## Qué se ha portado en v0.7.0')
    L.append('')
    L.append('1. **Botones rápidos de cantidad** (B1): tarjeta "Editar objeto" de la Librería, fila "+10 / +100 / Máx".')
    L.append('2. **Invasiones vencidas editables** (B2): Exploración > Este mundo, fila "Invasiones".')
    L.append('3. **Traer / echar vecinos** (B3): Exploración > Vecindad, desplegable "Traer vecino..." y botón "Echar" por fila.')
    L.append('')
    L.append('Además, fuera de la auditoría pero del mismo requirement: "Zoom" (Exploración > Mapa) ya nunca baja de la '
             'escala mínima legible del mod (0,68), comprobado por `AutopruebaEspaciado`.')
    L.append('')
    L.append('Lógica pura con tests en `TerrakeepMod.Tests/ParidadEscritorio330Tests.cs`; comprobación en el juego real '
             'con `scripts/verificar-libreria.ps1`, `scripts/verificar-exploracion.ps1` y `scripts/verificar-espaciado.ps1` '
             '(evidencia en la bitácora, entrada de la v0.7.0).')
    L.append('')
    L.append('## Apéndice: commits solo de bitácora / documentación (%d)' % len(doc))
    L.append('')
    L.append('Cambian únicamente `bitacora.md` u otra documentación de escritorio; no traen ninguna función nueva.')
    L.append('')
    L.append(' '.join('`%s`' % h for h, _, _ in doc))
    L.append('')
    io.open(SALIDA, 'w', encoding='utf-8', newline='\n').write('\n'.join(L))
    print('OK: %d commits, %d grupos (a=%d b=%d c=%d), %d solo doc' % (len(commits), len(G), cuenta['a'], cuenta['b'], cuenta['c'], len(doc)))


main()
