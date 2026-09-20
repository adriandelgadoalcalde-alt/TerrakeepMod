# Verificacion EN EL JUEGO REAL de la idea 9 del catalogo de funciones ("guia de grupo
# multijugador"): un SERVIDOR DEDICADO real (headless, start-tModLoaderServer.bat) mas DOS
# CLIENTES graficos reales conectados por red de verdad (127.0.0.1) - nunca una simulacion de
# paquetes ni un solo cliente hablando consigo mismo.
#
#   .\verificar-grupo.ps1               -> lanza los tres procesos, espera al observador y
#                                         comprueba su evidencia real.
#   .\verificar-grupo.ps1 -SoloCompilar -> se queda en la compilacion.
#
# Por que hacen falta DOS clientes de verdad y no uno: lo que se esta demostrando es que la
# mochila/armadura de OTRO jugador conectado llega sincronizada por la RED real a este cliente
# (Main.TrySyncingMyPlayer + PlayerItemSlotID.CanRelay, ver Common\Guia\GuiaGrupo.cs) - eso no
# existe con un solo proceso, por definicion.

param(
	[switch]$SoloCompilar,
	[int]$SegundosEsperaObservador = 180
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'
$puerto    = 27977

$sandboxServidor   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepGrupoServidor'
$sandboxObservador = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepGrupoObservador'
$sandboxCompanero  = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepGrupoCompanero'

# ---- 0. Mundo y personaje de origen (el sintetico de WS0), clonados si hace falta -----------
$origenWS0 = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
if (-not (Test-Path (Join-Path $origenWS0 "Worlds\$mundo.wld"))) {
	throw "No hay sandbox de WS0 del que clonar mundo/personaje ($origenWS0)."
}

New-Item -ItemType Directory -Force -Path "$sandboxServidor\Worlds", "$sandboxServidor\Mods" | Out-Null
New-Item -ItemType Directory -Force -Path "$sandboxObservador\Players", "$sandboxObservador\Mods" | Out-Null
New-Item -ItemType Directory -Force -Path "$sandboxCompanero\Players", "$sandboxCompanero\Mods" | Out-Null

# El servidor necesita el MUNDO (no personaje: un servidor dedicado no tiene jugador propio).
Copy-Item (Join-Path $origenWS0 "Worlds\$mundo.wld") (Join-Path $sandboxServidor 'Worlds') -Force
if (Test-Path (Join-Path $origenWS0 "Worlds\$mundo.twld")) {
	Copy-Item (Join-Path $origenWS0 "Worlds\$mundo.twld") (Join-Path $sandboxServidor 'Worlds') -Force
}

# Cada CLIENTE necesita su propio personaje (dos procesos no pueden compartir el mismo archivo
# .plr abierto a la vez) - los dos son una copia del mismo sintetico de WS0, con nombre interno
# identico ("TerrakeepPrueba"): vanilla real NO rechaza nombres duplicados entre jugadores
# conectados (comprobado en el decompilado, MessageBuffer.cs no filtra por nombre), asi que no
# hace falta renombrar nada para que la conexion funcione.
foreach ($sandboxCliente in @($sandboxObservador, $sandboxCompanero)) {
	Copy-Item (Join-Path $origenWS0 "Players\$personaje.plr") (Join-Path $sandboxCliente 'Players') -Force
	if (Test-Path (Join-Path $origenWS0 "Players\$personaje.tplr")) {
		Copy-Item (Join-Path $origenWS0 "Players\$personaje.tplr") (Join-Path $sandboxCliente 'Players') -Force
	}
}

# ---- 1. Compilar el proyecto ENTERO -----------------------------------------------------------
Write-Host '== Compilando el proyecto entero ==' -ForegroundColor Cyan
Push-Location $tmlDir
try {
	$ErrorActionPreference = 'Continue'
	& $tmlDotnet 'tModLoader.dll' '-server' '-build' $repo '-unsafe' 'false' '-tmlsavedirectory' $sandboxObservador
	$ErrorActionPreference = 'Stop'
	if ($LASTEXITCODE -ne 0) { throw 'Fallo el -build de tModLoader' }
}
finally { Pop-Location }

$tmodBuild = Join-Path $sandboxObservador 'Mods\TerrakeepMod.tmod'
if (-not (Test-Path $tmodBuild)) { throw "El -build termino sin error pero no aparecio $tmodBuild" }
Write-Host "OK: $tmodBuild ($((Get-Item $tmodBuild).Length) bytes)" -ForegroundColor Green

if ($SoloCompilar) { return }

# ---- 2. El mismo .tmod + enabled.json en LOS TRES sandboxes ----------------------------------
Copy-Item $tmodBuild (Join-Path $sandboxServidor 'Mods\TerrakeepMod.tmod') -Force
Copy-Item $tmodBuild (Join-Path $sandboxCompanero 'Mods\TerrakeepMod.tmod') -Force
foreach ($sandbox in @($sandboxServidor, $sandboxObservador, $sandboxCompanero)) {
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 2.5 Mismo arreglo real que verificar-guia.ps1: el dialogo de "mods actualizados" ---------
foreach ($sandboxCliente in @($sandboxObservador, $sandboxCompanero)) {
	$configSandbox = Join-Path $sandboxCliente 'config.json'
	if (Test-Path $configSandbox) {
		$json = Get-Content $configSandbox -Raw -Encoding UTF8 | ConvertFrom-Json
		$json | Add-Member -NotePropertyName 'ShowNewUpdatedModsInfo' -NotePropertyValue $false -Force
		($json | ConvertTo-Json -Depth 10) | Out-File $configSandbox -Encoding utf8
	} else {
		'{"ShowNewUpdatedModsInfo": false}' | Out-File $configSandbox -Encoding utf8
	}
	Remove-Item (Join-Path $sandboxCliente 'LastLaunchedMods.txt') -Force -ErrorAction SilentlyContinue
}

# ---- 3. Servidor dedicado headless real -------------------------------------------------------
$evidenciaServidor = Join-Path $sandboxServidor 'server.log'
Remove-Item $evidenciaServidor -Force -ErrorAction SilentlyContinue
Write-Host '== Servidor dedicado (headless, sin interfaz grafica) ==' -ForegroundColor Cyan
# Se invoca dotnet.exe DIRECTAMENTE con tModLoader.dll en vez de pasar por start-tModLoaderServer.bat
# (que en Windows llama a busybox-sh.bat -> start-tModLoaderServer.sh -> ScriptCaller.sh, una cadena
# de procesos anidados): el propio ScriptCaller.sh real (LaunchUtils\ScriptCaller.sh) termina
# ejecutando exactamente "<dotnet> tModLoader.dll -server <args>" de todos modos (visto en su codigo
# fuente), y el mismo patron de invocar dotnet.exe directo YA lo usa este mismo script mas arriba
# para el -build. Con la cadena de .bat/.sh intermedia, Start-Process -RedirectStandardOutput no
# llegaba a capturar nada del proceso real (server.log se quedaba a 0 bytes) - invocando el binario
# real directamente se evita el problema entero, no solo se disimula.
$argsServidor = @(
	'tModLoader.dll', '-server', '-nosteam',
	'-world', (Join-Path $sandboxServidor "Worlds\$mundo.wld"),
	'-port', $puerto, '-maxplayers', '8', '-tmlsavedirectory', "`"$sandboxServidor`""
)
$procServidor = Start-Process -FilePath $tmlDotnet -WorkingDirectory $tmlDir -PassThru -WindowStyle Minimized `
	-ArgumentList $argsServidor -RedirectStandardOutput $evidenciaServidor `
	-RedirectStandardError (Join-Path $sandboxServidor 'server.err.log')

# Espera a que el puerto este de verdad escuchando antes de lanzar ningun cliente.
$servidorListo = $false
for ($i = 0; $i -lt 60; $i++) {
	Start-Sleep -Seconds 1
	if (Test-NetConnection -ComputerName '127.0.0.1' -Port $puerto -InformationLevel Quiet -WarningAction SilentlyContinue) {
		$servidorListo = $true
		break
	}
}
if (-not $servidorListo) {
	Write-Host 'El servidor no llego a escuchar en el puerto. Ultimas lineas de su salida:' -ForegroundColor Red
	Get-Content $evidenciaServidor -Tail 40 -ErrorAction SilentlyContinue
	Stop-Process -Id $procServidor.Id -Force -ErrorAction SilentlyContinue
	exit 1
}
Write-Host "OK: servidor escuchando en 127.0.0.1:$puerto." -ForegroundColor Green

# ---- 4. Los dos clientes graficos reales, con su propio rol de autoprueba --------------------
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FocoGrupo {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@ -ErrorAction SilentlyContinue

function LimpiarAutopruebas {
	$env:TERRAKEEP_AUTOTEST = ''
	$env:TERRAKEEP_AUTOTEST_PANEL = ''
	$env:TERRAKEEP_AUTOTEST_WS1 = ''
	$env:TERRAKEEP_AUTOTEST_WS3 = ''
	$env:TERRAKEEP_AUTOTEST_WS5 = ''
	$env:TERRAKEEP_AUTOTEST_WS6 = ''
	$env:TERRAKEEP_AUTOTEST_WS7 = ''
	$env:TERRAKEEP_AUTOTEST_BUILDS = ''
	$env:TERRAKEEP_AUTOTEST_IDIOMAS = ''
	$env:TERRAKEEP_AUTOTEST_ESPACIADO = ''
	$env:TERRAKEEP_AUTOTEST_GUIA = ''
	$env:TERRAKEEP_AUTOTEST_GRUPO_OBSERVADOR = ''
	$env:TERRAKEEP_AUTOTEST_GRUPO_COMPANERO = ''
}

$evObservador = Join-Path $sandboxObservador 'terrakeep-grupo-evidencia.log'
$evCompanero  = Join-Path $sandboxCompanero 'terrakeep-grupo-evidencia.log'
Remove-Item $evObservador -Force -ErrorAction SilentlyContinue
Remove-Item $evCompanero -Force -ErrorAction SilentlyContinue

Write-Host '== Cliente COMPAÑERO (se conecta y se pone encima un estado real conocido) ==' -ForegroundColor Cyan
LimpiarAutopruebas
$env:TERRAKEEP_AUTOTEST_GRUPO_COMPANERO = '1'
$procCompanero = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandboxCompanero`"", '-j', '127.0.0.1', '-port', $puerto, '-player', $personaje)

# El companero se RENOMBRA en vivo nada mas conectar (bug real encontrado con este mismo arnes: el
# servidor rechaza una segunda conexion con el mismo nombre de jugador activo - ver la cabecera de
# AutopruebaGrupo.PasoCompanero) - hay que esperar a que ese renombrado real haya pasado antes de
# lanzar al observador con el mismo nombre "TerrakeepPrueba" que el companero tenia al principio.
Write-Host 'Esperando a que el companero se renombre de verdad (libera el nombre "TerrakeepPrueba")...'
for ($i = 0; $i -lt 240; $i++) {
	Start-Sleep -Seconds 1
	if ((Test-Path $evCompanero) -and (Select-String -Path $evCompanero -Pattern 'renombrado' -Quiet -ErrorAction SilentlyContinue)) {
		break
	}
}

Write-Host '== Cliente OBSERVADOR (lee al companero por red real y comprueba) ==' -ForegroundColor Cyan
LimpiarAutopruebas
$env:TERRAKEEP_AUTOTEST_GRUPO_OBSERVADOR = '1'
$procObservador = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandboxObservador`"", '-j', '127.0.0.1', '-port', $puerto, '-player', $personaje)
LimpiarAutopruebas

Write-Host "Esperando '$evObservador' -> AUTOPRUEBA GRUPO COMPLETA (hasta $SegundosEsperaObservador s)..."
$objetivo = 'AUTOPRUEBA GRUPO COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEsperaObservador; $i++) {
	Start-Sleep -Seconds 1

	if ($i % 10 -eq 5) {
		$nuestro = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
			Where-Object { $_.CommandLine -like "*$sandboxObservador*" } | Select-Object -First 1
		if ($nuestro) {
			$h = (Get-Process -Id $nuestro.ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
			if ($h -and $h -ne 0) { [void][FocoGrupo]::SetForegroundWindow($h) }
		}
	}

	if ((Test-Path $evObservador) -and (Select-String -Path $evObservador -Pattern $objetivo -Quiet -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		Start-Sleep -Seconds 2
		break
	}
}

Write-Host ''
Write-Host '== Evidencia real del OBSERVADOR ==' -ForegroundColor Cyan
if (Test-Path $evObservador) {
	Get-Content $evObservador -Encoding UTF8 | ForEach-Object { $_ }
	$destino = Join-Path $repo 'evidencia\grupo-observador.log.txt'
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evObservador $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray

	$capturas = Join-Path $sandboxObservador 'terrakeep-capturas'
	if (Test-Path $capturas) {
		Write-Host "Capturas reales del juego en: $capturas" -ForegroundColor DarkGray
		Get-ChildItem $capturas -Filter *grupo* | ForEach-Object { Write-Host "  $($_.Name)  ($($_.Length) bytes)" -ForegroundColor DarkGray }
	}
} else {
	Write-Host "(el mod no llego a escribir $evObservador)" -ForegroundColor Red
}

Write-Host ''
Write-Host '== Evidencia real del COMPAÑERO ==' -ForegroundColor Cyan
if (Test-Path $evCompanero) {
	Get-Content $evCompanero -Encoding UTF8 | ForEach-Object { $_ }
	Copy-Item $evCompanero (Join-Path $repo 'evidencia\grupo-companero.log.txt') -Force
}

# Solo se mata LO QUE HA LANZADO ESTE SCRIPT.
foreach ($p in @($procServidor, $procObservador, $procCompanero)) {
	Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}
foreach ($sandbox in @($sandboxServidor, $sandboxObservador, $sandboxCompanero)) {
	Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like "*$sandbox*" } |
		ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log del observador." -ForegroundColor Green
	$malos = Select-String -Path $evObservador -Pattern 'NO CUADRA|EXCEPCION' -Encoding UTF8
	if ($malos) {
		Write-Host "PERO hay comprobaciones en rojo:" -ForegroundColor Red
		$malos | ForEach-Object { Write-Host "  $($_.Line)" -ForegroundColor Red }
		exit 1
	}
	Write-Host 'Ninguna comprobacion en rojo.' -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo'. Revisar $evObservador." -ForegroundColor Red
	exit 1
}
