# Verificacion EN EL JUEGO REAL de la pestaña Personaje -> Apariencia: el tinte de pelo y el
# boton de deshacer los cambios de apariencia. Sandbox propio (tModLoader-TerrakeepApariencia):
# nunca toca los personajes ni los mundos reales del usuario.
#
#   .\verificar-apariencia.ps1               -> compila, lanza el cliente real y recoge evidencia.
#   .\verificar-apariencia.ps1 -SoloCompilar -> se queda en la compilacion.
#
# Se apoya en la autoprueba del panel unico (TERRAKEEP_AUTOTEST_PANEL) SOLO para abrir el panel,
# poblar el personaje de prueba con colores vivos y entrar en Apariencia; la parte de tintes y de
# deshacer la lleva TERRAKEEP_AUTOTEST_APARIENCIA, que espera a que aquella termine sus pasos
# antes de empezar los suyos.

param(
	[switch]$SoloCompilar,
	[int]$SegundosEspera = 300
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepApariencia'
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

# ---- 1. Compilar el proyecto ENTERO con el compilador real de tModLoader --------------------
Write-Host '== Compilando el proyecto entero ==' -ForegroundColor Cyan
Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
Push-Location $tmlDir
try {
	# El -build escribe un aviso benigno a stderr (icon_small.png). Con $ErrorActionPreference
	# 'Stop', PowerShell 5.1 lo trata como error terminante aunque el exit code sea 0.
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

'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8

# ---- 2. Lanzar el cliente real -------------------------------------------------------------
$marca = "APARIENCIA-$([DateTime]::Now.ToString('HHmmss'))-$PID"
$env:TERRAKEEP_PANEL_MARCA = $marca
$env:TERRAKEEP_AUTOTEST_PANEL = '1'
$env:TERRAKEEP_AUTOTEST_APARIENCIA = '1'
# Las autopruebas de los demas workstreams se apagan: se pelearian por la misma interfaz.
$env:TERRAKEEP_AUTOTEST = ''
$env:TERRAKEEP_AUTOTEST_WS1 = ''
$env:TERRAKEEP_AUTOTEST_WS3 = ''
$env:TERRAKEEP_AUTOTEST_WS5 = ''
$env:TERRAKEEP_AUTOTEST_WS6 = ''
$env:TERRAKEEP_AUTOTEST_WS7 = ''
$env:TERRAKEEP_AUTOTEST_BUILDS = ''
$env:TERRAKEEP_AUTOTEST_MUNECO_LUZ = ''

$evidencia = Join-Path $sandbox 'terrakeep-panel-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $sandbox 'terrakeep-capturas\*.png') -Force -ErrorAction SilentlyContinue

# Con otro cliente de tModLoader abierto, el nuestro se queda colgado cargando mods.
for ($i = 0; $i -lt 120; $i++) {
	$otros = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like '*tModLoader.dll*' -and $_.CommandLine -notlike '*-server*' }
	if (-not $otros) { break }
	Write-Host 'Esperando a que no haya otro cliente de tModLoader abierto...' -ForegroundColor DarkGray
	Start-Sleep -Seconds 2
}

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

# La partida se congela si la ventana pierde el foco (Main.hasFocus -> gamePaused).
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FocoApariencia {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@ -ErrorAction SilentlyContinue

Write-Host "Esperando evidencia en $evidencia (marca $marca, hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA APARIENCIA COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1

	# El foco se reafirma cada 5 s durante TODA la espera, no solo al principio: el paso final
	# (cerrar el panel y fotografiar al personaje real) necesita que la ventana este dibujando, y
	# sin foco Terraria sigue actualizando pero deja de dibujar - la captura salia atrasada.
	if ($i % 5 -eq 2) {
		$nuestro = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
			Where-Object { $_.CommandLine -like "*$sandbox*" } | Select-Object -First 1
		if ($nuestro) {
			$h = (Get-Process -Id $nuestro.ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
			if ($h -and $h -ne 0) { [void][FocoApariencia]::SetForegroundWindow($h) }
		}
	}

	if ((Test-Path $evidencia) -and (Select-String -Path $evidencia -Pattern $objetivo -Quiet -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		Start-Sleep -Seconds 2
		break
	}
}

Write-Host ''
Write-Host '== Evidencia real ==' -ForegroundColor Cyan
if (Test-Path $evidencia) {
	Get-Content $evidencia -Encoding UTF8 | Where-Object { $_ -like '*APARIENCIA*' -or $_ -like '*muñeco*' }
	$destino = Join-Path $repo 'evidencia\apariencia.log.txt'
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia completa guardada en $destino)" -ForegroundColor DarkGray

	$capturas = Join-Path $sandbox 'terrakeep-capturas'
	if (Test-Path $capturas) {
		Write-Host "Capturas reales del juego en: $capturas" -ForegroundColor DarkGray
		Get-ChildItem $capturas -Filter *.png | ForEach-Object { Write-Host "  $($_.Name)  ($($_.Length) bytes)" -ForegroundColor DarkGray }
	}
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Get-Content (Join-Path $logDir 'client.log') -Tail 40 -ErrorAction SilentlyContinue
}

# Solo se mata LO QUE HA LANZADO ESTE SCRIPT.
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
