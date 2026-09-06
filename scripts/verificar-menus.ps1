# Verificacion de lo que se ve de Terrakeep ANTES de entrar en ninguna partida: los menus del
# propio tModLoader.
#
# A diferencia de todas las demas verificaciones del mod, esta lanza el juego SIN -skipselect:
# se queda en el menu principal, y desde ahi el mod navega solo a la lista de Mods, a la ficha
# de "Mas informacion" (donde se lee description.txt) y a la pantalla de Configuracion de Mods
# que tModLoader genera a partir del ModConfig, dejando una captura real de cada una.
#
#   .\verificar-menus.ps1                       -> 800x720 (la ventana de prueba de siempre)
#   .\verificar-menus.ps1 -Ancho 1600 -Alto 900 -> otra resolucion
#   .\verificar-menus.ps1 -Ingles               -> con el juego en ingles
#   .\verificar-menus.ps1 -SoloCompilar

param(
	[switch]$SoloCompilar,
	[switch]$Ingles,
	[int]$Ancho = 0,
	[int]$Alto = 0,
	[int]$SegundosEspera = 300
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepMenus'

New-Item -ItemType Directory -Force -Path "$sandbox\Mods" | Out-Null

# ---- 1. Compilar ---------------------------------------------------------------------------
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

'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8

# ---- 2. Configuracion del sandbox ----------------------------------------------------------
$config = Join-Path $sandbox 'config.json'
if (Test-Path $config) {
	$json = Get-Content $config -Raw -Encoding UTF8 | ConvertFrom-Json
} else {
	$json = New-Object PSObject
}
function Fijar($obj, $nombre, $valor) {
	if ($obj.PSObject.Properties.Name -contains $nombre) { $obj.$nombre = $valor }
	else { $obj | Add-Member -NotePropertyName $nombre -NotePropertyValue $valor }
}
if ($Ancho -gt 0 -and $Alto -gt 0) {
	Fijar $json 'DisplayWidth' $Ancho
	Fijar $json 'DisplayHeight' $Alto
}
Fijar $json 'Fullscreen' $false
# El idioma del juego con el que arranca la prueba. El mod lo puede cambiar en vivo despues,
# pero la lista de Mods y la pantalla de config son interfaz de tModLoader y hay que verlas en
# los dos idiomas para saber que estan bien.
Fijar $json 'Language' $(if ($Ingles) { 'en-US' } else { 'es-ES' })
$json | ConvertTo-Json -Depth 20 | Out-File $config -Encoding utf8

# ---- 3. Lanzar el cliente SIN -skipselect --------------------------------------------------
$env:TERRAKEEP_AUTOTEST_MENUS = '1'
$env:TERRAKEEP_AUTOTEST = ''
$env:TERRAKEEP_AUTOTEST_PANEL = ''
$env:TERRAKEEP_AUTOTEST_IDIOMAS = ''
$env:TERRAKEEP_AUTOTEST_WS1 = ''
$env:TERRAKEEP_AUTOTEST_WS3 = ''
$env:TERRAKEEP_AUTOTEST_WS5 = ''
$env:TERRAKEEP_AUTOTEST_WS6 = ''
$env:TERRAKEEP_AUTOTEST_WS7 = ''
$env:TERRAKEEP_AUTOTEST_BUILDS = ''

$evidencia = Join-Path $sandbox 'terrakeep-menus-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $sandbox 'terrakeep-capturas\menu-*.png') -Force -ErrorAction SilentlyContinue

for ($i = 0; $i -lt 60; $i++) {
	$otros = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like '*tModLoader.dll*' -and $_.CommandLine -notlike '*-server*' }
	if (-not $otros) { break }
	Write-Host 'Esperando a que no haya otro cliente de tModLoader abierto...' -ForegroundColor DarkGray
	Start-Sleep -Seconds 2
}

Write-Host '== Cliente grafico (se queda en el menu, sin -skipselect) ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"")

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FocoMenus {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@ -ErrorAction SilentlyContinue

Write-Host "Esperando evidencia en $evidencia (hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA MENUS COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	# El foco se le devuelve MUCHAS veces al principio: sin foco, Main.DoUpdate hace return antes
	# de procesar la entrada y ModSystem.PostUpdateInput -de donde cuelga esta autoprueba- no se
	# llega a llamar (comprobado en el codigo real del tModLoader instalado).
	if ($i -lt 40 -or $i -eq 60 -or $i -eq 90) {
		$nuestro = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
			Where-Object { $_.CommandLine -like "*$sandbox*" } | Select-Object -First 1
		if ($nuestro) {
			$h = (Get-Process -Id $nuestro.ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
			if ($h -and $h -ne 0) { [void][FocoMenus]::SetForegroundWindow($h) }
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
	Get-Content $evidencia -Encoding UTF8 | ForEach-Object { $_ }
	$sufijo = ''
	if ($Ingles) { $sufijo += '-en' }
	if ($Ancho -gt 0) { $sufijo += "-${Ancho}x${Alto}" }
	$destino = Join-Path $repo ('evidencia\menus' + $sufijo + '.log.txt')
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray
	Write-Host "Capturas: $(Join-Path $sandbox 'terrakeep-capturas')" -ForegroundColor DarkGray
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Get-Content (Join-Path $logDir 'client.log') -Tail 30 -ErrorAction SilentlyContinue
}

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
