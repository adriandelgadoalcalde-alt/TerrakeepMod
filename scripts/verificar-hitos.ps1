# Verifica en el juego REAL la pestaña "Álbum" de hitos (Common\Hitos\AutopruebaHitos.cs): abre por
# clic real, mide con numeros que el encabezado (Actualizar / Abrir carpeta) y la lista no se
# solapan, y comprueba que la lista refleja lo que hay de verdad en album.json. Mismo sandbox que
# WS7/conjuntos/completitud.
#
# 16-sep-2026 (KeepQA S1, AUDITORIA-SESGOS-16SEP.md): esta autoprueba existia desde que nacio la
# pestaña, pero NINGUN script la lanzaba ni dejaba evidencia con nombre en evidencia\ - "Hitos" era
# una de las 3 pestañas de TerrakeepMod sin evidencia nombrada. Este script cierra las dos cosas.
#
# El album real vive en <guardado>\terrakeep-hitos\album.json. El sandbox WS7 no ha jugado nunca la
# Guia, asi que su album esta vacio y el paso 3 de la autoprueba daria "NO CUADRA" (enDisco=0) sin
# que eso diga nada de la pestaña. Se copia el album REAL del sandbox de la Guia (tModLoader-
# TerrakeepGuia, que si ha disparado hitos de verdad con TERRAKEEP_AUTOTEST_GUIA) al sandbox WS7
# antes de lanzar - datos reales, no inventados.
#
#   .\verificar-hitos.ps1              -> cliente grafico.

param(
	[int]$SegundosEspera = 150
)

$ErrorActionPreference = 'Stop'

$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS7'
$sandboxWs0 = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$sandboxGuia = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepGuia'
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

$albumGuia = Join-Path $sandboxGuia 'terrakeep-hitos'
if (Test-Path (Join-Path $albumGuia 'album.json')) {
	New-Item -ItemType Directory -Force -Path (Join-Path $sandbox 'terrakeep-hitos') | Out-Null
	Copy-Item (Join-Path $albumGuia '*') (Join-Path $sandbox 'terrakeep-hitos') -Recurse -Force
	Write-Host "Album real de la Guia copiado al sandbox ($((Get-ChildItem $albumGuia -Filter *.png).Count) capturas de hito)." -ForegroundColor DarkGray
} else {
	Write-Host "AVISO: no hay album real en $albumGuia - la autoprueba dira NO CUADRA en el paso 3 (album vacio), no es un bug de la pestaña." -ForegroundColor Yellow
}

New-Item -ItemType Directory -Force -Path (Join-Path $sandbox 'Mods') | Out-Null
Copy-Item (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod') `
	(Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force
'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8

Remove-Item (Join-Path $sandbox 'LastLaunchedMods.txt') -Force -ErrorAction SilentlyContinue

$log = Join-Path $logDir 'client.log'
Remove-Item $log -Force -ErrorAction SilentlyContinue
Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$objetivo = 'AUTOPRUEBA ALBUM COMPLETA'
$env:TERRAKEEP_AUTOTEST_HITOS = '1'
$env:TERRAKEEP_AUTOTEST_CONJUNTOS = ''
$env:TERRAKEEP_AUTOTEST_COMPLETITUD = ''
$env:TERRAKEEP_AUTOTEST_TOOLTIP = ''
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

# Evidencia CON NOMBRE en el repo (mismo patron que verificar-conjuntos.ps1).
$repo = Split-Path -Parent $PSScriptRoot
if (Test-Path $log) {
	$lineas = Select-String -Path $log -Pattern '\[Terrakeep\]' | ForEach-Object { $_.Line }
	$lineas | Out-File (Join-Path $repo 'evidencia\hitos.log.txt') -Encoding utf8
	Write-Host "Log [Terrakeep] copiado a evidencia\hitos.log.txt ($($lineas.Count) lineas)." -ForegroundColor DarkGray
}
$capturas = Join-Path $sandbox 'terrakeep-capturas'
if (Test-Path $capturas) {
	$destinoCapturas = Join-Path $repo 'evidencia\hitos-capturas'
	New-Item -ItemType Directory -Force -Path $destinoCapturas | Out-Null
	Copy-Item (Join-Path $capturas 'hitos-*.png') $destinoCapturas -Force -ErrorAction SilentlyContinue
	Write-Host "Capturas hitos-*.png copiadas a evidencia\hitos-capturas\." -ForegroundColor DarkGray
}

Write-Host ''
if ($encontrado) {
	$falla = Select-String -Path $log -Pattern 'FALLO|NO CUADRA|EXCEPCION' -ErrorAction SilentlyContinue
	if ($falla) {
		Write-Host "NO: se encontro 'FALLO/NO CUADRA/EXCEPCION' en el log. Lineas:" -ForegroundColor Red
		$falla | ForEach-Object { $_.Line }
		exit 1
	}
	Write-Host "OK: encontrado '$objetivo' en el log, sin ningun FALLO." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
