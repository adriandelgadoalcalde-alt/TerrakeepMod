# Verifica el panel de LIBRERIA (WS3) ejecutando tModLoader DE VERDAD, sobre una carpeta de
# guardado aislada propia de este workstream. Nunca toca los personajes ni los mundos reales
# del usuario.
#
#   .\verificar-libreria.ps1              -> arbol vanilla curado + descubrimiento en vivo,
#                                            navegacion real por carpetas, las cuatro reglas de
#                                            la gramatica de busqueda, coger un objeto del
#                                            catalogo y colocarlo en el inventario real.
#   .\verificar-libreria.ps1 -Calamity    -> lo mismo, pero con CalamityMod habilitado, para ver
#                                            la carpeta madre "CalamityMod (N)" descubierta en
#                                            vivo (sin ningun catalog.json estatico).
#   .\verificar-libreria.ps1 -SoloCompilar -> se queda en la compilacion, sin lanzar el juego.
#
# Sigue el MISMO patron que scripts\verificar-builds-en-juego.ps1 (WS4), por los mismos motivos
# reales ya medidos y anotados en bitacora.md:
#
#  1. SANDBOX PROPIO (tModLoader-TerrakeepWS3). Compartir sandbox con otro agente significa
#     pisarse el .tmod y el enabled.json.
#
#  2. ARCHIVO DE EVIDENCIA PROPIO dentro de ese sandbox (terrakeep-ws3-evidencia.log). El
#     tModLoader-Logs\client.log es uno solo para todas las instancias del juego y se rota al
#     arrancar, asi que con varios agentes probando a la vez la evidencia de una prueba se la
#     lleva por delante la de otra.
#
#  3. COMPILA UNA COPIA AISLADA con el nucleo del mod y solo lo que WS3 necesita (parametro
#     -Completo para compilar el proyecto entero). El compilador de tModLoader compila TODOS
#     los .cs de la carpeta, asi que con otros workstreams a medias en el mismo repo una
#     compilacion completa puede fallar por codigo ajeno a WS3.
#
#  4. El -build lleva -tmlsavedirectory, asi que el .tmod sale directamente en el sandbox de
#     WS3 en vez de en la carpeta Mods compartida.

param(
	[switch]$Calamity,
	[switch]$Completo,
	[switch]$SoloCompilar,
	[int]$SegundosEspera = 240
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS3'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

# ---- 0. Sandbox propio, clonado del de WS0 la primera vez ----------------------------------
# El personaje de prueba es el SINTETICO que genero WS0 (TerrakeepPrueba), nunca una copia de
# ningun archivo real del usuario: ese camino ya reventó una vez (ver bitacora.md, cierre de
# WS0) y ademas no hay motivo para tocar sus partidas.
if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	$origen = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
	if (-not (Test-Path (Join-Path $origen "Worlds\$mundo.wld"))) {
		throw "No hay sandbox de WS0 del que clonar el mundo de prueba ($origen)."
	}
	Write-Host "== Clonando el sandbox de prueba en $sandbox ==" -ForegroundColor Cyan
	New-Item -ItemType Directory -Force -Path "$sandbox\Worlds", "$sandbox\Players", "$sandbox\Mods" | Out-Null
	Copy-Item "$origen\Worlds\*" "$sandbox\Worlds" -Recurse -Force
	Copy-Item "$origen\Players\*" "$sandbox\Players" -Recurse -Force
}

# ---- 1. Preparar el proyecto que se va a compilar ------------------------------------------
if ($Completo) {
	$proyecto = $repo
	Write-Host '== Compilando el PROYECTO ENTERO (todos los workstreams) ==' -ForegroundColor Cyan
} else {
	$proyecto = Join-Path $sandbox 'ModSources\TerrakeepMod'
	Write-Host '== Compilando una copia AISLADA (nucleo + WS3 + lo que WS3 reutiliza) ==' -ForegroundColor Cyan

	if (Test-Path $proyecto) { Remove-Item $proyecto -Recurse -Force -Confirm:$false }
	New-Item -ItemType Directory -Force -Path $proyecto, "$proyecto\Common\Libreria",
		"$proyecto\Common\Undo", "$proyecto\UI\Libreria", "$proyecto\UI\Personaje\Widgets",
		"$proyecto\Localization", "$proyecto\lib", "$proyecto\Assets" | Out-Null

	Copy-Item (Join-Path (Split-Path -Parent $repo) 'tModLoader.targets') (Split-Path -Parent $proyecto) -Force
	Copy-Item "$repo\build.txt","$repo\description.txt","$repo\TerrakeepMod.csproj","$repo\Terrakeep.cs" $proyecto -Force
	Copy-Item "$repo\Common\Libreria\*.cs" "$proyecto\Common\Libreria" -Force
	# Historial de WS7: la Libreria deja deshacible lo que coloca. Solo la pila y su fachada; el
	# HistorialSystem (atajos Ctrl+Z) arrastra el panel de Ajustes y aqui no hace falta.
	Copy-Item "$repo\Common\Undo\Historial.cs","$repo\Common\Undo\PilaDeSnapshots.cs" "$proyecto\Common\Undo" -Force
	Copy-Item "$repo\UI\Libreria\*.cs" "$proyecto\UI\Libreria" -Force
	Copy-Item "$repo\UI\SlotObjetoVanilla.cs" "$proyecto\UI" -Force
	# Los cuatro widgets de WS1 que reutiliza la Libreria para verse igual que el resto del mod.
	Copy-Item "$repo\UI\Personaje\Widgets\EstiloTk.cs","$repo\UI\Personaje\Widgets\BotonTk.cs",
		"$repo\UI\Personaje\Widgets\EtiquetaTk.cs","$repo\UI\Personaje\Widgets\CampoTextoTk.cs" `
		"$proyecto\UI\Personaje\Widgets" -Force
	Copy-Item "$repo\Localization\*.hjson" "$proyecto\Localization" -Force
	Copy-Item "$repo\lib\TerrasavrNative.Core.dll" "$proyecto\lib" -Force
	Copy-Item "$repo\Assets\vanilla_library_tree.json","$repo\Assets\vanilla_library_labels_es.json" "$proyecto\Assets" -Force
}

# ---- 2. Compilar con el compilador REAL de tModLoader (sin -eac) ---------------------------
Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
Push-Location $tmlDir
try {
	& $tmlDotnet 'tModLoader.dll' '-server' '-build' $proyecto '-unsafe' 'false' '-tmlsavedirectory' $sandbox
	if ($LASTEXITCODE -ne 0) { throw 'Fallo el -build de tModLoader' }
}
finally { Pop-Location }

$tmod = Join-Path $sandbox 'Mods\TerrakeepMod.tmod'
if (-not (Test-Path $tmod)) { throw "El -build termino sin error pero no aparecio $tmod" }
Write-Host "OK: $tmod ($((Get-Item $tmod).Length) bytes)" -ForegroundColor Green

if ($SoloCompilar) { return }

# ---- 3. Mods habilitados en el sandbox ----------------------------------------------------
if ($Calamity) {
	$origen = Get-ChildItem (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods') -Filter '*CalamityMod.tmod' |
		Select-Object -First 1
	if (-not $origen) { throw 'No se encuentra CalamityMod.tmod en la carpeta Mods real.' }
	Copy-Item $origen.FullName (Join-Path $sandbox 'Mods\CalamityMod.tmod') -Force
	'["TerrakeepMod","CalamityMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
} else {
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 4. Lanzar el cliente real ------------------------------------------------------------
$marca = "WS3-$([DateTime]::Now.ToString('HHmmss'))-$PID"
$env:TERRAKEEP_WS3_MARCA = $marca
$env:TERRAKEEP_AUTOTEST_WS3 = '1'
# Las autopruebas de los demas workstreams se apagan explicitamente: si alguna quedara puesta en
# la sesion, abriria su propio panel encima del de la Libreria.
$env:TERRAKEEP_AUTOTEST = ''
$env:TERRAKEEP_AUTOTEST_WS1 = ''
$env:TERRAKEEP_AUTOTEST_BUILDS = ''
$env:TERRAKEEP_AUTOTEST_WS7 = ''

$evidencia = Join-Path $sandbox 'terrakeep-ws3-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

Write-Host "Esperando evidencia en $evidencia (marca $marca, hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA WS3 COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	if ((Test-Path $evidencia) -and (Select-String -Path $evidencia -Pattern $objetivo -Quiet -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		Start-Sleep -Seconds 2
		break
	}
}

Write-Host ''
Write-Host '== Evidencia real de WS3 ==' -ForegroundColor Cyan
if (Test-Path $evidencia) {
	# -Encoding UTF8 explicito: el mod escribe UTF-8 y Windows PowerShell 5.1 leeria el archivo
	# con la pagina de codigos ANSI, destrozando las tildes y las eñes de los nombres.
	Get-Content $evidencia -Encoding UTF8 | ForEach-Object { $_ }
	$destino = Join-Path $repo ('evidencia\ws3-libreria' + $(if ($Calamity) { '-calamity' } else { '' }) + '.log.txt')
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Write-Host 'Ultimas lineas del client.log del juego, por si dice algo:' -ForegroundColor DarkGray
	Get-Content (Join-Path $logDir 'client.log') -Tail 25 -ErrorAction SilentlyContinue
}

# Solo se mata LO QUE HA LANZADO ESTE SCRIPT: con varios workstreams probando a la vez, un
# "Stop-Process -Name dotnet" a secas tumbaria tambien la instancia de otro agente.
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
	Where-Object { $_.CommandLine -like "*$sandbox*" } |
	ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo'. Revisar $evidencia." -ForegroundColor Red
	exit 1
}
