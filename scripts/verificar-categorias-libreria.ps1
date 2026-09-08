# Recuento REAL, objeto a objeto, del arbol de carpetas de la Libreria (bug del 8-sep-2026:
# "Categorias" con paginas vacias, objetos que no salen y objetos mezclados). Lanza tModLoader de
# verdad sobre un sandbox propio y recoge lo que escribe AuditoriaCategorias.
#
#   .\verificar-categorias-libreria.ps1            -> solo el juego base.
#   .\verificar-categorias-libreria.ps1 -Calamity  -> con CalamityMod cargado, que es donde el bug
#                                                     se veia peor (los ids sobrantes del arbol
#                                                     curado caian encima de objetos del mod).
#   .\verificar-categorias-libreria.ps1 -SoloCompilar
#
# Mismo patron y mismos motivos que scripts\verificar-libreria.ps1 (sandbox propio para no pisar
# el .tmod ni el enabled.json de otro agente, archivo de evidencia propio porque el client.log es
# unico para todas las instancias del juego y se rota al arrancar).

param(
	[switch]$Calamity,
	[switch]$SoloCompilar,
	[int]$SegundosEspera = 240
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepCategorias'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

# ---- 0. Sandbox propio, clonado del de WS0 la primera vez ----------------------------------
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
New-Item -ItemType Directory -Force -Path "$sandbox\Mods" | Out-Null

# ---- 1. Compilar con el compilador REAL de tModLoader --------------------------------------
Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
Push-Location $tmlDir
try {
	# El -build escribe un aviso benigno a stderr (icon_small.png) que PowerShell 5.1 trataria como
	# error terminante con $ErrorActionPreference='Stop'; se comprueba $LASTEXITCODE de verdad.
	$ErrorActionPreference = 'Continue'
	& $tmlDotnet 'tModLoader.dll' '-server' '-build' $repo '-unsafe' 'false' '-tmlsavedirectory' $sandbox
	$ErrorActionPreference = 'Stop'
	if ($LASTEXITCODE -ne 0) { throw 'Fallo el -build de tModLoader' }
}
finally { Pop-Location }

$tmod = Join-Path $sandbox 'Mods\TerrakeepMod.tmod'
if (-not (Test-Path $tmod)) { throw "El -build termino sin error pero no aparecio $tmod" }
Write-Host "OK: $tmod ($((Get-Item $tmod).Length) bytes)" -ForegroundColor Green

if ($SoloCompilar) { return }

# ---- 2. Mods habilitados en el sandbox ----------------------------------------------------
if ($Calamity) {
	$origen = Get-ChildItem (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods') -Filter '*CalamityMod.tmod' |
		Select-Object -First 1
	if (-not $origen) { throw 'No se encuentra CalamityMod.tmod en la carpeta Mods real.' }
	Copy-Item $origen.FullName (Join-Path $sandbox 'Mods\CalamityMod.tmod') -Force
	'["TerrakeepMod","CalamityMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
} else {
	Remove-Item (Join-Path $sandbox 'Mods\CalamityMod.tmod') -Force -ErrorAction SilentlyContinue
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 3. Lanzar el cliente real ------------------------------------------------------------
$env:TERRAKEEP_AUDIT_CATEGORIAS = '1'
# Las autopruebas de los demas workstreams (y la de WS3, que recorre la interfaz entera) se apagan:
# aqui solo interesa el recuento del arbol.
$env:TERRAKEEP_AUTOTEST = ''
$env:TERRAKEEP_AUTOTEST_WS1 = ''
$env:TERRAKEEP_AUTOTEST_WS3 = ''
$env:TERRAKEEP_AUTOTEST_BUILDS = ''
$env:TERRAKEEP_AUTOTEST_WS7 = ''

$evidencia = Join-Path $sandbox 'terrakeep-categorias-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

$objetivo = 'AUDITORIA CATEGORIAS COMPLETA'
Write-Host "Esperando '$objetivo' en $evidencia (hasta $SegundosEspera s)..."
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
Write-Host '== Evidencia real ==' -ForegroundColor Cyan
if (Test-Path $evidencia) {
	Get-Content $evidencia -Encoding UTF8 | ForEach-Object { $_ }
	$destino = Join-Path $repo ('evidencia\categorias-libreria' + $(if ($Calamity) { '-calamity' } else { '' }) + '.log.txt')
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Get-Content (Join-Path $tmlDir 'tModLoader-Logs\client.log') -Tail 30 -ErrorAction SilentlyContinue
}

# Solo se mata LO QUE HA LANZADO ESTE SCRIPT: con varios agentes probando a la vez, matar todos los
# dotnet tumbaria la instancia de otro.
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
	Where-Object { $_.CommandLine -like "*$sandbox*" } |
	ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo'." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo'. Revisar $evidencia." -ForegroundColor Red
	exit 1
}
