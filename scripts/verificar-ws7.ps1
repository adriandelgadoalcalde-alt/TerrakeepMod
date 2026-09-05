# Verifica WS7 (deshacer/rehacer + Ajustes/idioma) ejecutando tModLoader DE VERDAD y leyendo la
# evidencia del log real del juego. Mismo criterio que scripts\verificar-en-juego.ps1 (WS0), del
# que sale este, con dos diferencias a proposito:
#
#   1. Sandbox PROPIO: Documents\My Games\Terraria\tModLoader-TerrakeepWS7. Hay varios
#      workstreams trabajando en paralelo sobre este mismo repo y compartir la carpeta de
#      guardado (y por tanto ModConfigs\, que es justo lo que WS7 tiene que comprobar que
#      persiste) daria resultados cruzados.
#   2. Variable de autoprueba propia (TERRAKEEP_AUTOTEST_WS7) en vez de la de WS0: las dos
#      autopruebas abren un panel a pantalla completa y se pelearian por la interfaz.
#
# El sandbox se crea solo la primera vez a partir del de WS0 (personaje sintetico TerrakeepPrueba
# generado con TerrasavrNative.Core + mundo pequeño de pruebas). NUNCA se tocan los personajes ni
# los mundos reales del usuario.
#
#   .\verificar-ws7.ps1              -> cliente grafico. Requiere sesion de Windows desbloqueada
#                                       y con salida de audio (ver bitacora.md, WS0).
#   .\verificar-ws7.ps1 -Servidor    -> servidor dedicado. Comprueba que el mod carga, pero NO la
#                                       autoprueba: sin cliente no hay Main.LocalPlayer ni
#                                       interfaz, que es justo lo que WS7 tiene que demostrar.

param(
	[switch]$Servidor,
	[int]$SegundosEspera = 150
)

$ErrorActionPreference = 'Stop'

$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS7'
$sandboxWs0 = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	if (-not (Test-Path (Join-Path $sandboxWs0 "Worlds\$mundo.wld"))) {
		throw "No hay sandbox de pruebas. Falta $sandboxWs0 (lo crea scripts\verificar-en-juego.ps1 de WS0)."
	}
	Write-Host "Creando el sandbox de WS7 a partir del de WS0..." -ForegroundColor Yellow
	New-Item -ItemType Directory -Force -Path $sandbox | Out-Null
	Copy-Item (Join-Path $sandboxWs0 '*') $sandbox -Recurse -Force
}

New-Item -ItemType Directory -Force -Path (Join-Path $sandbox 'Mods') | Out-Null
Copy-Item (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod') `
	(Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force

if ($Servidor) {
	$log = Join-Path $logDir 'server.log'
	Remove-Item $log -Force -ErrorAction SilentlyContinue
	Write-Host '== Servidor dedicado (headless) ==' -ForegroundColor Cyan
	$objetivo = 'Mod cargado'
	$p = Start-Process -FilePath $tmlDotnet -WorkingDirectory $tmlDir -PassThru -WindowStyle Hidden `
		-ArgumentList @('tModLoader.dll', '-server', '-tmlsavedirectory', "`"$sandbox`"",
			'-world', "`"$sandbox\Worlds\$mundo.wld`"", '-players', '1', '-port', '7807', '-nosteam')
}
else {
	$log = Join-Path $logDir 'client.log'
	Remove-Item $log -Force -ErrorAction SilentlyContinue
	Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
	$objetivo = 'AUTOPRUEBA WS7: terminada'
	$env:TERRAKEEP_AUTOTEST_WS7 = '1'
	$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
		-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")
}

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
Write-Host '== ModConfig persistido (lo que se recordara en la siguiente partida) ==' -ForegroundColor Cyan
$config = Join-Path $sandbox 'ModConfigs\TerrakeepMod_AjustesConfig.json'
if (Test-Path $config) { Get-Content $config } else { Write-Host "(todavia no existe $config)" -ForegroundColor Yellow }

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
