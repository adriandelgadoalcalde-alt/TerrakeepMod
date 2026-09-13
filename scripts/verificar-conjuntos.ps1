# Verifica en el juego REAL la pestaña nueva "Conjuntos" (gestion ampliada de loadouts): nombres
# personalizados de los tres conjuntos nativos + presets propios del mod, aplicar con deshacer/
# rehacer de verdad. Mismo sandbox que WS7/arrastre/sincronizacion.
#
#   .\verificar-conjuntos.ps1              -> cliente grafico.

param(
	[int]$SegundosEspera = 150
)

$ErrorActionPreference = 'Stop'

$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS7'
$sandboxWs0 = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	if (-not (Test-Path (Join-Path $sandboxWs0 "Worlds\$mundo.wld"))) {
		throw "No hay sandbox de pruebas. Falta $sandboxWs0 (lo crea scripts\verificar-en-juego.ps1 de WS0)."
	}
	Write-Host "Creando el sandbox a partir del de WS0..." -ForegroundColor Yellow
	New-Item -ItemType Directory -Force -Path $sandbox | Out-Null
	Copy-Item (Join-Path $sandboxWs0 '*') $sandbox -Recurse -Force
}

New-Item -ItemType Directory -Force -Path (Join-Path $sandbox 'Mods') | Out-Null
Copy-Item (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod') `
	(Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force

Remove-Item (Join-Path $sandbox 'LastLaunchedMods.txt') -Force -ErrorAction SilentlyContinue

$log = Join-Path $logDir 'client.log'
Remove-Item $log -Force -ErrorAction SilentlyContinue
Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$objetivo = 'AUTOPRUEBA CONJUNTOS: terminada'
$env:TERRAKEEP_AUTOTEST_CONJUNTOS = '1'
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

Write-Host "Esperando '$objetivo' en $log (hasta $SegundosEspera s)..."
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	if ((Test-Path $log) -and (Select-String -Path $log -Pattern $objetivo -Quiet -SimpleMatch -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		break
	}
}

Write-Host ''
Write-Host '== Lineas [Terrakeep] del log real ==' -ForegroundColor Cyan
if (Test-Path $log) {
	Select-String -Path $log -Pattern '\[Terrakeep\]' | ForEach-Object { $_.Line }
} else {
	Write-Host "(no se llego a crear $log)" -ForegroundColor Red
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host ''
if ($encontrado) {
	$falla = Select-String -Path $log -Pattern 'FALLO' -SimpleMatch -ErrorAction SilentlyContinue
	if ($falla) {
		Write-Host "NO: se encontro 'FALLO' en el log. Lineas:" -ForegroundColor Red
		$falla | ForEach-Object { $_.Line }
		exit 1
	}
	Write-Host "OK: encontrado '$objetivo' en el log, sin ningun FALLO." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
