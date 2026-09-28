# Lanza TERRAKEEP_DIAG_ANILLO_DESBORDE=1 (Common/Panel/DiagnosticoAnilloBuildsDesbordado.cs, arnes
# NUEVO de solo verificacion, no toca ningun archivo de produccion aparte del arreglo real) sobre
# el sandbox de WS7, que ya tiene un personaje/mundo de pruebas (mismo sandbox que usa
# verificar-investigacion-3bugs.ps1). Recoge el log real del diagnostico.
#
#   .\verificar-anillo-builds-desbordado.ps1

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

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$objetivo = 'ANILLO-BUILDS-DESBORDE: terminado'
$env:TERRAKEEP_DIAG_ANILLO_DESBORDE = '1'
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

# El juego congela la partida sin foco (Main.hasFocus -> Main.gamePaused), y con la partida
# congelada el diagnostico no avanza ni un fotograma - mismo hallazgo ya documentado por WS1/WS6.
Start-Sleep -Seconds 12
$sig = @'
using System;
using System.Runtime.InteropServices;
public class FocoAnilloBuildsDesborde {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@
if (-not ('FocoAnilloBuildsDesborde' -as [type])) { Add-Type -TypeDefinition $sig }
Get-Process -Name dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.MainWindowTitle -like '*Terraria*' -or $_.MainWindowTitle -like '*tModLoader*' } |
	ForEach-Object { [FocoAnilloBuildsDesborde]::SetForegroundWindow($_.MainWindowHandle) | Out-Null }

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
Write-Host '== Lineas ANILLO-BUILDS-DESBORDE del log real ==' -ForegroundColor Cyan
if (Test-Path $log) {
	Select-String -Path $log -Pattern 'ANILLO-BUILDS-DESBORDE' | ForEach-Object { $_.Line }
} else {
	Write-Host "(no se llego a crear $log)" -ForegroundColor Red
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

$destino = Join-Path $repo 'evidencia\anillo-builds-desbordado.log.txt'
if (Test-Path $log) {
	New-Item -ItemType Directory -Force -Path (Join-Path $repo 'evidencia') | Out-Null
	Select-String -Path $log -Pattern 'ANILLO-BUILDS-DESBORDE' | ForEach-Object { $_.Line } |
		Out-File $destino -Encoding utf8
	Write-Host "Log copiado a $destino" -ForegroundColor DarkGray
}

Write-Host ''
if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
