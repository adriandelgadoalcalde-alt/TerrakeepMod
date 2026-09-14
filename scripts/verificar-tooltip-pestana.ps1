# Verifica con una captura REAL del juego que el tooltip de una pestaña inactiva de la barra
# superior, arreglado el 14-sep-2026 (ver bitacora.md), ya no tapa el contenido de detras sin
# fondo propio. Mismo patron que verificar-en-juego.ps1 (sandbox WS0 ya existente, client.log
# como evidencia) mas la copia de la captura real a evidencia\, igual que verificar-espaciado.ps1.
#
#   .\verificar-tooltip-pestana.ps1

param(
	[int]$SegundosEspera = 180
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	throw "Falta el mundo de prueba en $sandbox (ver verificar-en-juego.ps1 para generarlo)."
}

Copy-Item (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod') `
	(Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force
'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8

# Mismo obstaculo real ya documentado en verificar-espaciado.ps1: sin esto, tModLoader compara
# los mods del Workshop contra LastLaunchedMods.txt y abre un dialogo modal ("Mod Changes since
# last launch") que exige un clic real - sin nadie delante, el cliente se queda colgado en
# "Finding Mods..." para siempre. Se apaga solo en ESTE sandbox de pruebas.
$config = Join-Path $sandbox 'config.json'
if (Test-Path $config) {
	$json = Get-Content $config -Raw -Encoding UTF8 | ConvertFrom-Json
	if ($json.PSObject.Properties.Name -contains 'ShowNewUpdatedModsInfo') {
		$json.ShowNewUpdatedModsInfo = $false
	} else {
		$json | Add-Member -NotePropertyName 'ShowNewUpdatedModsInfo' -NotePropertyValue $false -Force
	}
	$json | ConvertTo-Json -Depth 10 | Out-File $config -Encoding utf8
}

# Ningun otro cliente grafico de tModLoader a la vez (contencion de GPU real, ver bitacora.md).
for ($i = 0; $i -lt 60; $i++) {
	$otros = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like '*tModLoader.dll*' -and $_.CommandLine -notlike '*-server*' }
	if (-not $otros) { break }
	Write-Host 'Esperando a que no haya otro cliente de tModLoader abierto...' -ForegroundColor DarkGray
	Start-Sleep -Seconds 2
}

$log = Join-Path $logDir 'client.log'
Remove-Item $log -Force -ErrorAction SilentlyContinue

$env:TERRAKEEP_AUTOTEST_TOOLTIP_PESTANA = '1'
$env:TERRAKEEP_AUTOTEST = ''
$env:TERRAKEEP_AUTOTEST_TOOLTIP = ''
$env:TERRAKEEP_AUTOTEST_ESPACIADO = ''

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

Write-Host "Esperando evidencia en $log (hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA TOOLTIP PESTA.A COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	if ((Test-Path $log) -and (Select-String -Path $log -Pattern $objetivo -Quiet -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		Start-Sleep -Seconds 2
		break
	}
}

Write-Host ''
Write-Host '== Lineas [Terrakeep] AUTOPRUEBA TOOLTIP PESTANA del log real ==' -ForegroundColor Cyan
if (Test-Path $log) {
	Select-String -Path $log -Pattern 'TOOLTIP PESTA.A' | ForEach-Object { $_.Line }
} else {
	Write-Host "(no se llego a crear $log)" -ForegroundColor Red
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

$capturas = Join-Path $sandbox 'terrakeep-capturas'
$png = Join-Path $capturas 'tooltip-pestana-fondo-800x720-minimo.png'
if (Test-Path $png) {
	$destino = Join-Path $repo 'evidencia\tooltip-pestana-fondo'
	New-Item -ItemType Directory -Force -Path $destino | Out-Null
	Copy-Item $png $destino -Force
	Write-Host "Captura real copiada a $destino\tooltip-pestana-fondo-800x720-minimo.png" -ForegroundColor Green
} else {
	Write-Host "NO se genero la captura ($png)." -ForegroundColor Red
}

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo'. Revisar $log completo." -ForegroundColor Red
	exit 1
}
