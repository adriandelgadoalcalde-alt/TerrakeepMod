# Verifica en el juego REAL los dos problemas de prefijos que pidio el usuario:
#   1. Indicador de calidad de prefijo (Librería y/o Inventario).
#   2. Auto-aplicar el mejor prefijo al coger un objeto del catalogo de la Libreria.
#
# Sandbox y variable de entorno PROPIOS (tModLoader-TerrakeepPrefijos /
# TERRAKEEP_AUTOTEST_PREFIJOS), para no pisar la autoprueba de WS3 (Libreria) ni la de ningun
# otro workstream que pueda estar corriendo en paralelo sobre este mismo repositorio - ver
# AutopruebaPrefijos.cs.
#
# COMPILA UNA COPIA AISLADA de HEAD + los archivos propios de este cambio (Assets/best_prefix.json,
# Common/Prefijos/*.cs, UI/Libreria/ContenidoLibreria.cs, Localization/*.hjson con la clave
# "Prefijos.MejorPrefijo" ya añadida), NO el repositorio de trabajo tal cual. Motivo real, medido
# en esta misma sesion: hay al menos otro agente editando el repo en paralelo (Common/Personaje,
# UI/Personaje, UI/Builds...) y su copia de trabajo no compila en este momento (CS0103
# "LongitudMaximaObjetivo" no existe, CS0234 "ContentSamples" no existe) - errores ajenos a este
# cambio. Una copia de HEAD (que sí compila, es lo ultimo comiteado) mas SOLO los archivos de
# este cambio evita depender de que el resto del repo este en un estado compilable ahora mismo.

param(
	[int]$SegundosEspera = 180
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepPrefijos'
$build     = Join-Path $env:TEMP 'terrakeep-prefijos-build'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	throw "Falta el mundo de prueba en $sandbox\Worlds. Copiarlo de tModLoader-TerrakeepWS3."
}
if (-not (Test-Path (Join-Path $build 'TerrakeepMod\TerrakeepMod.csproj'))) {
	throw "No existe la copia aislada en $build. Ver la cabecera de este script: hay que " +
		"exportar HEAD con 'git archive' y superponer Assets/best_prefix.json, " +
		"Common/Prefijos/*.cs, UI/Libreria/ContenidoLibreria.cs y los dos .hjson."
}

# ---- 1. Compilar con el compilador REAL de tModLoader (sin -eac), en el sandbox propio -------
Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
Push-Location $tmlDir
try {
	# Aviso benigno a stderr esperado (ver compilar.ps1 / verificar-builds-en-juego.ps1): se
	# relaja ErrorActionPreference alrededor de esta unica llamada nativa.
	$ErrorActionPreference = 'Continue'
	& $tmlDotnet 'tModLoader.dll' '-server' '-build' (Join-Path $build 'TerrakeepMod') '-unsafe' 'false' '-tmlsavedirectory' $sandbox
	$ErrorActionPreference = 'Stop'
	if ($LASTEXITCODE -ne 0) { throw 'Fallo el -build de tModLoader' }
}
finally { Pop-Location }

$tmod = Join-Path $sandbox 'Mods\TerrakeepMod.tmod'
if (-not (Test-Path $tmod)) { throw "El -build termino sin error pero no aparecio $tmod" }
Write-Host "OK: $tmod ($((Get-Item $tmod).Length) bytes)" -ForegroundColor Green

'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8

# ---- 2. Lanzar el cliente real -----------------------------------------------------------
$evidencia = Join-Path $sandbox 'terrakeep-prefijos-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue

$env:TERRAKEEP_AUTOTEST_PREFIJOS = '1'

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

Write-Host "Esperando evidencia en $evidencia (hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA PREFIJOS COMPLETA'
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
	$destino = Join-Path $repo 'evidencia\prefijos.log.txt'
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Write-Host 'Ultimas lineas del client.log del juego, por si dice algo:' -ForegroundColor DarkGray
	Get-Content (Join-Path $logDir 'client.log') -Tail 20 -ErrorAction SilentlyContinue
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
	Where-Object { $_.CommandLine -like "*$sandbox*" } |
	ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log." -ForegroundColor Red
	exit 1
}
