# Revision de IDIOMA y de ESTETICA del panel, en el juego real.
#
# Recorre las once vistas del panel (las seis pestañas, con las seis sub-pestañas de Personaje y
# las tres de Exploracion) DOS veces, en español y en ingles, recoge todo el texto que se esta
# enseñando en cada una y deja una captura real del back buffer por vista e idioma.
#
# Al terminar, el mod lista las cadenas que salen IGUALES en los dos idiomas: ese es el detector
# de literales sin migrar a los .hjson. Las coincidencias legitimas (nombres propios, numeros,
# nombres de objeto del juego) salen tambien y hay que mirarlas una a una.
#
#   .\verificar-idiomas.ps1                 -> solo TerrakeepMod, 1280x720
#   .\verificar-idiomas.ps1 -Calamity       -> con CalamityMod cargado
#   .\verificar-idiomas.ps1 -Ancho 1600 -Alto 900   -> otra resolucion
#   .\verificar-idiomas.ps1 -SoloCompilar

param(
	[switch]$Calamity,
	[switch]$SoloCompilar,
	[int]$Ancho = 0,
	[int]$Alto = 0,
	[int]$SegundosEspera = 420
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepIdiomas'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

# ---- 0. Sandbox propio, clonado del de la verificacion del panel unico ----------------------
if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	$origen = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepPanel'
	if (-not (Test-Path (Join-Path $origen "Worlds\$mundo.wld"))) {
		$origen = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
	}
	if (-not (Test-Path (Join-Path $origen "Worlds\$mundo.wld"))) {
		throw "No hay sandbox del que clonar el mundo de prueba ($origen)."
	}
	Write-Host "== Clonando el sandbox de prueba en $sandbox ==" -ForegroundColor Cyan
	New-Item -ItemType Directory -Force -Path "$sandbox\Worlds", "$sandbox\Players", "$sandbox\Mods" | Out-Null
	Copy-Item "$origen\Worlds\*" "$sandbox\Worlds" -Recurse -Force
	Copy-Item "$origen\Players\*" "$sandbox\Players" -Recurse -Force
}

# ---- 1. Compilar el proyecto entero ---------------------------------------------------------
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

# ---- 2. Mods habilitados --------------------------------------------------------------------
if ($Calamity) {
	$origen = Get-ChildItem (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods') -Filter '*CalamityMod.tmod' |
		Select-Object -First 1
	if (-not $origen) { throw 'No se encuentra CalamityMod.tmod en la carpeta Mods real.' }
	Copy-Item $origen.FullName (Join-Path $sandbox 'Mods\CalamityMod.tmod') -Force
	'["TerrakeepMod","CalamityMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
} else {
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 3. Resolucion de la ventana ------------------------------------------------------------
# El juego guarda la resolucion en el config.json de SU carpeta de guardado, asi que se puede
# pedir una distinta sin tocar la configuracion real del usuario.
if ($Ancho -gt 0 -and $Alto -gt 0) {
	$config = Join-Path $sandbox 'config.json'
	if (Test-Path $config) {
		$json = Get-Content $config -Raw -Encoding UTF8 | ConvertFrom-Json
	} else {
		$json = New-Object PSObject
	}
	foreach ($par in @(@('DisplayWidth', $Ancho), @('DisplayHeight', $Alto))) {
		if ($json.PSObject.Properties.Name -contains $par[0]) {
			$json.$($par[0]) = $par[1]
		} else {
			$json | Add-Member -NotePropertyName $par[0] -NotePropertyValue $par[1]
		}
	}
	if ($json.PSObject.Properties.Name -contains 'Fullscreen') { $json.Fullscreen = $false }
	else { $json | Add-Member -NotePropertyName 'Fullscreen' -NotePropertyValue $false }
	$json | ConvertTo-Json -Depth 20 | Out-File $config -Encoding utf8
	Write-Host "Resolucion pedida para la prueba: ${Ancho}x${Alto}" -ForegroundColor DarkGray
}

# ---- 4. Lanzar el cliente real --------------------------------------------------------------
$env:TERRAKEEP_AUTOTEST_IDIOMAS = '1'
# Las demas autopruebas se apagan: se pelearian por la misma interfaz.
$env:TERRAKEEP_AUTOTEST = ''
$env:TERRAKEEP_AUTOTEST_PANEL = ''
$env:TERRAKEEP_AUTOTEST_WS1 = ''
$env:TERRAKEEP_AUTOTEST_WS3 = ''
$env:TERRAKEEP_AUTOTEST_WS5 = ''
$env:TERRAKEEP_AUTOTEST_WS6 = ''
$env:TERRAKEEP_AUTOTEST_WS7 = ''
$env:TERRAKEEP_AUTOTEST_BUILDS = ''

$evidencia = Join-Path $sandbox 'terrakeep-idiomas-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $sandbox 'terrakeep-capturas\idioma-*.png') -Force -ErrorAction SilentlyContinue

# Con otro cliente de tModLoader abierto, el nuestro se queda colgado cargando mods.
for ($i = 0; $i -lt 60; $i++) {
	$otros = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like '*tModLoader.dll*' -and $_.CommandLine -notlike '*-server*' }
	if (-not $otros) { break }
	Write-Host 'Esperando a que no haya otro cliente de tModLoader abierto...' -ForegroundColor DarkGray
	Start-Sleep -Seconds 2
}

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FocoIdiomas {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@ -ErrorAction SilentlyContinue

Write-Host "Esperando evidencia en $evidencia (hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA IDIOMAS COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1

	if ($i -eq 20 -or $i -eq 45 -or $i -eq 90) {
		$nuestro = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
			Where-Object { $_.CommandLine -like "*$sandbox*" } | Select-Object -First 1
		if ($nuestro) {
			$h = (Get-Process -Id $nuestro.ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
			if ($h -and $h -ne 0) { [void][FocoIdiomas]::SetForegroundWindow($h) }
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
	if ($Calamity) { $sufijo += '-calamity' }
	if ($Ancho -gt 0) { $sufijo += "-${Ancho}x${Alto}" }
	$destino = Join-Path $repo ('evidencia\idiomas' + $sufijo + '.log.txt')
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray

	$capturas = Join-Path $sandbox 'terrakeep-capturas'
	if (Test-Path $capturas) {
		Write-Host "Capturas reales del juego en: $capturas" -ForegroundColor DarkGray
	}
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
