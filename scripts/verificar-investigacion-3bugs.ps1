# Lanza TERRAKEEP_INVESTIGACION_3BUGS=1 (Common/Panel/DiagnosticoInvestigador3Bugs.cs, arnes NUEVO
# de solo investigacion, no toca ningun archivo de produccion) sobre el sandbox de WS7, que ya
# tiene un personaje/mundo de pruebas con NPCs de pueblo reales (mismo sandbox que usa
# verificar-conjuntos.ps1). Recoge el log real y las capturas del diagnostico.
#
#   .\verificar-investigacion-3bugs.ps1

param(
	[int]$SegundosEspera = 150
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS7'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	throw "No existe el sandbox $sandbox (lo crea verificar-conjuntos.ps1/verificar-en-juego.ps1)."
}

New-Item -ItemType Directory -Force -Path (Join-Path $sandbox 'Mods') | Out-Null
Copy-Item (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod') `
	(Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force
Remove-Item (Join-Path $sandbox 'LastLaunchedMods.txt') -Force -ErrorAction SilentlyContinue

$log = Join-Path $logDir 'client.log'
Remove-Item $log -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $sandbox 'terrakeep-investigacion-3bugs') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$objetivo = 'INVESTIGACION 3 BUGS: terminada'
$env:TERRAKEEP_INVESTIGACION_3BUGS = '1'
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

# El juego congela la partida sin foco (Main.hasFocus -> Main.gamePaused), y con la partida
# congelada el diagnostico no avanza ni un fotograma - mismo hallazgo ya documentado por WS1/WS6.
Start-Sleep -Seconds 12
$sig = @'
using System;
using System.Runtime.InteropServices;
public class FocoInvestigacion3Bugs {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@
if (-not ('FocoInvestigacion3Bugs' -as [type])) { Add-Type -TypeDefinition $sig }
Get-Process -Name dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.MainWindowTitle -like '*Terraria*' -or $_.MainWindowTitle -like '*tModLoader*' } |
	ForEach-Object { [FocoInvestigacion3Bugs]::SetForegroundWindow($_.MainWindowHandle) | Out-Null }

Write-Host "Esperando '$objetivo' en $log (hasta $SegundosEspera s)..."
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	if ((Test-Path $log) -and (Select-String -Path $log -Pattern $objetivo -Quiet -SimpleMatch -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		Start-Sleep -Seconds 2
		break
	}
}

Write-Host ''
Write-Host '== Lineas INVESTIGACION-3BUGS del log real ==' -ForegroundColor Cyan
if (Test-Path $log) {
	Select-String -Path $log -Pattern 'INVESTIGACION-3BUGS' | ForEach-Object { $_.Line }
} else {
	Write-Host "(no se llego a crear $log)" -ForegroundColor Red
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

$destino = Join-Path $repo 'evidencia\investigacion-3bugs.log.txt'
if (Test-Path $log) {
	Select-String -Path $log -Pattern 'INVESTIGACION-3BUGS' | ForEach-Object { $_.Line } |
		Out-File $destino -Encoding utf8
	Write-Host "Log copiado a $destino" -ForegroundColor DarkGray
}
$capturas = Join-Path $sandbox 'terrakeep-investigacion-3bugs'
if (Test-Path $capturas) {
	$destinoCapturas = Join-Path $repo 'evidencia\investigacion-3bugs-capturas'
	New-Item -ItemType Directory -Force -Path $destinoCapturas | Out-Null
	Copy-Item (Join-Path $capturas '*.png') $destinoCapturas -Force -ErrorAction SilentlyContinue
	Write-Host "Capturas copiadas a $destinoCapturas" -ForegroundColor DarkGray
}

Write-Host ''
if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
