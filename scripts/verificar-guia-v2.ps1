# Verificacion de la GUIA V2 (F3, 02-oct-2026) ejecutando tModLoader DE VERDAD sobre una carpeta de
# guardado aislada (personaje y mundo sinteticos TerrakeepPrueba). Nunca toca los personajes ni los
# mundos reales del usuario: anota sus hashes antes y despues y falla si cambian.
#
#   .\verificar-guia-v2.ps1            -> sin Calamity (guia vanilla, o el aviso honesto si aun no
#                                         esta incrustada en Terrakeep.Core).
#   .\verificar-guia-v2.ps1 -Calamity  -> con CalamityMod: marca del mapa, ficha "cómo conseguirlo",
#                                         Libreria, progreso manual, todas las sub-pestañas y las
#                                         cuatro resoluciones del encargo con UIScale normal y maxima.
#   .\verificar-guia-v2.ps1 -SoloCompilar
#
# Turno de pantalla (REGLAS-LIMPIEZA-FINAL.md): antes de lanzar el juego espera (sondeo cada 60 s) a
# que no exista scratchpad\PANTALLA.lock ni otro juego abierto, lo crea con su nombre y lo borra al
# terminar. Solo mata los procesos que ha lanzado el.

param(
	[switch]$Calamity,
	[switch]$SoloCompilar,
	[int]$SegundosEspera = 1500,
	[string]$Lock = ''
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepGuiaV2'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'
if (-not $Lock) {
	$Lock = Join-Path $env:LOCALAPPDATA 'Temp\claude\C--Users-adrian-Downloads-Keep-Terrasavr-Win-Terrasavr-Calamity-Beta-resources-app\d38ffe35-118f-4719-b326-0ca425888fe7\scratchpad\PANTALLA.lock'
}

# ---- hashes de las partidas REALES (deben quedar identicos) ---------------------------------
function Get-HashesReales {
	$carpetas = @(
		(Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Players'),
		(Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Worlds'),
		(Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\Players'),
		(Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\Worlds')
	)
	$r = @{}
	foreach ($c in $carpetas) {
		if (-not (Test-Path $c)) { continue }
		Get-ChildItem $c -File -Recurse -ErrorAction SilentlyContinue |
			Where-Object { $_.Extension -in '.plr', '.tplr', '.wld', '.twld' } |
			ForEach-Object { $r[$_.FullName] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
	}
	return $r
}

# ---- 0. Sandbox propio, clonado del de WS0 la primera vez ------------------------------------
if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	$origen = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
	if (-not (Test-Path (Join-Path $origen "Worlds\$mundo.wld"))) { throw "No hay sandbox de WS0 del que clonar ($origen)." }
	Write-Host "== Clonando el sandbox de prueba en $sandbox ==" -ForegroundColor Cyan
	New-Item -ItemType Directory -Force -Path "$sandbox\Worlds", "$sandbox\Players", "$sandbox\Mods" | Out-Null
	Copy-Item "$origen\Worlds\*" "$sandbox\Worlds" -Recurse -Force
	Copy-Item "$origen\Players\*" "$sandbox\Players" -Recurse -Force
}

# ---- 1. Compilar el proyecto ENTERO con el compilador real de tModLoader ----------------------
Write-Host '== Compilando el proyecto entero ==' -ForegroundColor Cyan
Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
Push-Location $tmlDir
try {
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

# ---- 1.5 Sin dialogo de "mods actualizados" (ver verificar-guia.ps1) -------------------------
$configSandbox = Join-Path $sandbox 'config.json'
if (Test-Path $configSandbox) {
	$json = Get-Content $configSandbox -Raw -Encoding UTF8 | ConvertFrom-Json
	$json | Add-Member -NotePropertyName 'ShowNewUpdatedModsInfo' -NotePropertyValue $false -Force
	($json | ConvertTo-Json -Depth 10) | Out-File $configSandbox -Encoding utf8
} else {
	'{"ShowNewUpdatedModsInfo": false}' | Out-File $configSandbox -Encoding utf8
}

# ---- 2. Mods habilitados en el sandbox -------------------------------------------------------
if ($Calamity) {
	$origen = Get-ChildItem (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods') -Filter '*CalamityMod.tmod' | Select-Object -First 1
	if (-not $origen) {
		$origen = Get-ChildItem 'C:\Program Files (x86)\Steam\steamapps\workshop\content\1281930' -Recurse -Filter 'CalamityMod.tmod' |
			Sort-Object LastWriteTime -Descending | Select-Object -First 1
	}
	if (-not $origen) { throw 'No se encuentra CalamityMod.tmod.' }
	Copy-Item $origen.FullName (Join-Path $sandbox 'Mods\CalamityMod.tmod') -Force
	'["TerrakeepMod","CalamityMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
	Write-Host "Calamity: $($origen.FullName)" -ForegroundColor DarkGray
} else {
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 3. Turno de pantalla ---------------------------------------------------------------------
function Test-OtroJuego {
	$tml = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like '*tModLoader.dll*' -and $_.CommandLine -notlike '*-server*' }
	$otros = Get-Process -Name 'Terraria', 'dontstarve_steam_x64', 'dontstarve_steam' -ErrorAction SilentlyContinue
	return [bool]($tml -or $otros)
}
$espera = 0
while ((Test-Path $Lock) -or (Test-OtroJuego)) {
	if ($espera -eq 0) { Write-Host "Turno de pantalla ocupado ($(if (Test-Path $Lock) { Get-Content $Lock -Raw } else { 'otro juego abierto' })). Esperando..." -ForegroundColor Yellow }
	Start-Sleep -Seconds 60
	$espera++
	if ($espera -gt 180) { throw 'Tres horas esperando el turno de pantalla: se aborta.' }
}
"F3-guia-terrakeepmod (verificar-guia-v2.ps1$(if ($Calamity) { ' -Calamity' })) $(Get-Date -Format o)" | Out-File $Lock -Encoding utf8
Write-Host "Turno de pantalla tomado ($Lock)." -ForegroundColor Cyan

$hashesAntes = Get-HashesReales
Write-Host "Hashes de partidas reales ANTES: $($hashesAntes.Count) archivos." -ForegroundColor DarkGray

$p = $null
$encontrado = $false
$evidencia = Join-Path $sandbox 'terrakeep-guia-evidencia.log'
try {
	# ---- 4. Lanzar el cliente real -----------------------------------------------------------
	$env:TERRAKEEP_AUTOTEST_GUIAV2 = '1'
	foreach ($v in 'TERRAKEEP_AUTOTEST', 'TERRAKEEP_AUTOTEST_GUIA', 'TERRAKEEP_AUTOTEST_PANEL', 'TERRAKEEP_AUTOTEST_WS1', 'TERRAKEEP_AUTOTEST_WS3',
		'TERRAKEEP_AUTOTEST_WS5', 'TERRAKEEP_AUTOTEST_WS6', 'TERRAKEEP_AUTOTEST_WS7', 'TERRAKEEP_AUTOTEST_BUILDS', 'TERRAKEEP_AUTOTEST_IDIOMAS',
		'TERRAKEEP_AUTOTEST_ESPACIADO') { Set-Item -Path "env:$v" -Value '' }
	Remove-Item $evidencia -Force -ErrorAction SilentlyContinue
	$capturas = Join-Path $sandbox 'terrakeep-capturas'
	Get-ChildItem $capturas -Filter 'guiav2-*.png' -ErrorAction SilentlyContinue | Remove-Item -Force

	Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
	$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
		-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

	Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FocoGuiaV2 {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@ -ErrorAction SilentlyContinue

	Write-Host "Esperando evidencia en $evidencia (hasta $SegundosEspera s)..."
	for ($i = 0; $i -lt $SegundosEspera; $i++) {
		Start-Sleep -Seconds 1
		if ($i % 10 -eq 5) {
			$nuestro = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
				Where-Object { $_.CommandLine -like "*$sandbox*" } | Select-Object -First 1
			if ($nuestro) {
				$h = (Get-Process -Id $nuestro.ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
				if ($h -and $h -ne 0) { [void][FocoGuiaV2]::SetForegroundWindow($h) }
			}
		}
		if ((Test-Path $evidencia) -and (Select-String -Path $evidencia -Pattern 'AUTOPRUEBA GUIA V2 COMPLETA' -Quiet -ErrorAction SilentlyContinue)) {
			$encontrado = $true
			Start-Sleep -Seconds 4
			break
		}
	}
}
finally {
	# Solo lo que ha lanzado este script.
	if ($p) { Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
	Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like "*$sandbox*" } |
		ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
	Start-Sleep -Seconds 3
	Remove-Item $Lock -Force -ErrorAction SilentlyContinue
	Write-Host 'Turno de pantalla liberado.' -ForegroundColor Cyan
}

$hashesDespues = Get-HashesReales
$cambiados = @($hashesAntes.Keys | Where-Object { $hashesDespues[$_] -ne $hashesAntes[$_] })
$nuevos = @($hashesDespues.Keys | Where-Object { -not $hashesAntes.ContainsKey($_) })
Write-Host "Hashes de partidas reales DESPUES: $($hashesDespues.Count) archivos; cambiados: $($cambiados.Count); nuevos: $($nuevos.Count)." -ForegroundColor $(if ($cambiados.Count -eq 0 -and $nuevos.Count -eq 0) { 'Green' } else { 'Red' })

Write-Host ''
Write-Host '== Evidencia real de la Guia v2 ==' -ForegroundColor Cyan
$destino = Join-Path $repo ('evidencia\guia-v2' + $(if ($Calamity) { '-calamity' } else { '' }) + '.log.txt')
if (Test-Path $evidencia) {
	Get-Content $evidencia -Encoding UTF8 | Where-Object { $_ -match 'GUIA V2|Guia v2' } | ForEach-Object { $_ }
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	# Sin rutas personales en el repo publico.
	(Get-Content $evidencia -Encoding UTF8) -replace [regex]::Escape($env:USERPROFILE), '%USERPROFILE%' | Out-File $destino -Encoding utf8
	Write-Host "(copia saneada en $destino)" -ForegroundColor DarkGray
	if (Test-Path $capturas) {
		Write-Host "Capturas reales del juego en: $capturas" -ForegroundColor DarkGray
		Get-ChildItem $capturas -Filter 'guiav2-*.png' | ForEach-Object { Write-Host "  $($_.Name)  ($($_.Length) bytes)" -ForegroundColor DarkGray }
	}
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Get-Content (Join-Path $logDir 'client.log') -Tail 40 -ErrorAction SilentlyContinue
}

if ($cambiados.Count -gt 0 -or $nuevos.Count -gt 0) {
	Write-Host 'Las partidas reales han cambiado: esto NO deberia pasar.' -ForegroundColor Red
	$cambiados + $nuevos | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
	exit 1
}
if (-not $encontrado) {
	Write-Host "NO se encontro 'AUTOPRUEBA GUIA V2 COMPLETA'." -ForegroundColor Red
	exit 1
}
$malos = Select-String -Path $evidencia -Pattern 'NO CUADRA|EXCEPCION|NO CABE' -Encoding UTF8
if ($malos) {
	Write-Host 'Comprobaciones en rojo:' -ForegroundColor Red
	$malos | ForEach-Object { Write-Host "  $($_.Line)" -ForegroundColor Red }
	exit 1
}
Write-Host 'OK: autoprueba completa y ninguna comprobacion en rojo.' -ForegroundColor Green
