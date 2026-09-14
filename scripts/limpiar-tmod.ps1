# Quita el .pdb del .tmod YA COMPILADO (scripts\compilar.ps1 primero) en la carpeta Mods real
# de tModLoader. build.txt (buildIgnore) no puede hacerlo: ModCompile.Build (tModLoader.dll
# real, decompilado) añade el .pdb sin condicion en cuanto compila, sin pasar por buildIgnore -
# ver el comentario real en build.txt. Usa la herramienta compartida de la familia
# (Downloads\KeepQA\src\empaquetado-tmod\limpiar-tmod.js), que reescribe el .tmod con un hash
# SHA1 valido siguiendo el formato real de TmodFile.Save() - no un zip a medias.
#
#   .\scripts\compilar.ps1
#   .\scripts\limpiar-tmod.ps1

$ErrorActionPreference = 'Stop'

$node = 'C:\Users\adrian\Downloads\dev-tools\node-v24.20.0-win-x64\node.exe'
$herramienta = 'C:\Users\adrian\Downloads\KeepQA\src\empaquetado-tmod\limpiar-tmod.js'
$tmod = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod'

if (-not (Test-Path $node)) { throw "No se encuentra node.exe en $node (ver herramientas.json)." }
if (-not (Test-Path $herramienta)) { throw "No se encuentra limpiar-tmod.js en $herramienta." }
if (-not (Test-Path $tmod)) { throw "No se encuentra $tmod - compila primero con scripts\compilar.ps1." }

& $node $herramienta $tmod
if ($LASTEXITCODE -ne 0) { throw 'Fallo limpiar-tmod.js' }
