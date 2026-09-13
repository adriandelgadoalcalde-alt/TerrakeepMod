# Verifica en el juego REAL que ningun texto es mas grande que su caja y que nada se solapa, en
# TODO el mod (no solo el recuadro naranja de aviso de dificultad de Exploracion > "Este mundo" y
# la pestaña Buffs de Personaje, las dos primeras areas que tuvo esta autoprueba): tambien
# Inventario/Almacenes/Equipo/Desbloqueos/Apariencia de Personaje, Ajustes y Mapa/Busqueda de
# Exploracion. En los dos idiomas del mod y a tres resoluciones de ventana (1600x900, 1280x720 y
# 800x720, el minimo real que admite el motor). Ver bitacora.md, entrada del 13-sep-2026.
#
#   .\verificar-espaciado.ps1
#   .\verificar-espaciado.ps1 -SegundosEspera 900
#   .\verificar-espaciado.ps1 -Calamity     -> lo mismo con CalamityMod habilitado (mismo patron
#                                              que verificar-guia.ps1 -Calamity).
#
# Mismo patron que verificar-exploracion.ps1/verificar-panel-unico.ps1: sandbox PROPIO (clonado
# del de WS0 la primera vez) para no pisar el .tmod/enabled.json de otro agente lanzando el juego
# a la vez, y evidencia leida del archivo PROPIO que escribe el mod dentro de ese sandbox.

param(
	[switch]$Calamity,
	[int]$SegundosEspera = 900
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepEspaciado'
$origenSandbox = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

# ---- 1. Sandbox propio, clonado del de WS0 la primera vez --------------------------------
if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	if (-not (Test-Path (Join-Path $origenSandbox "Worlds\$mundo.wld"))) {
		throw "No hay sandbox de WS0 del que clonar el mundo de prueba ($origenSandbox)."
	}
	Write-Host "== Creando el sandbox de espaciado a partir del de WS0 ==" -ForegroundColor Cyan
	New-Item -ItemType Directory -Force -Path $sandbox, "$sandbox\Worlds", "$sandbox\Players", "$sandbox\Mods" | Out-Null
	Copy-Item "$origenSandbox\Worlds\*" "$sandbox\Worlds" -Force
	Copy-Item "$origenSandbox\Players\*" "$sandbox\Players" -Force
	if (Test-Path "$origenSandbox\config.json") { Copy-Item "$origenSandbox\config.json" $sandbox -Force }
}

# Autoguardado FUERA: la prueba cambia el modo de juego del mundo (permanente al guardar) y
# ademas conmuta Main.autoSave para ver las dos variantes del aviso de permanencia.
#
# ShowNewUpdatedModsInfo A FALSE: obstaculo real encontrado y resuelto en esta sesion (regla de
# autonomia tecnica, ver bitacora.md). Sin esto, tModLoader compara los mods del Workshop
# (HEROsMod/CalamityMod, aunque esten DESHABILITADOS en este sandbox: la comparacion mira la
# carpeta Workshop entera, no enabled.json) contra "LastLaunchedMods.txt" y, si detecta que
# cambiaron desde el ultimo lanzamiento, abre un dialogo modal ("Mod Changes since last launch",
# Interface.cs real decompilado, ModOrganizer.DetectModChangesForInfoMessage) que exige un CLIC
# real para continuar - exactamente el mismo tipo de bloqueo ya documentado en el CLAUDE.md de
# este proyecto para el aviso de audio. Sin nadie delante para hacer ese clic, el cliente se
# quedaba colgado para siempre en "Finding Mods..." (confirmado: dos pasadas seguidas, 900s y
# 2400s, sin que 'terrakeep-espaciado-evidencia.log' llegara a existir - la condicion de "parar
# tras dos fallos seguidos" de las reglas globales). La propia opcion de menu del juego
# ("Ajustes de mods > Mostrar aviso de mods actualizados") escribe esta MISMA clave
# (Main.Configuration.Put("ShowNewUpdatedModsInfo", ...), ModLoader.cs real decompilado): apagarla
# aqui es exactamente lo que haria un jugador real para no ver mas ese aviso, aplicado solo a este
# sandbox de pruebas.
$config = Join-Path $sandbox 'config.json'
if (Test-Path $config) {
	$json = Get-Content $config -Raw -Encoding UTF8 | ConvertFrom-Json
	$json.AutoSave = $false
	if ($json.PSObject.Properties.Name -contains 'ShowNewUpdatedModsInfo') {
		$json.ShowNewUpdatedModsInfo = $false
	} else {
		$json | Add-Member -NotePropertyName 'ShowNewUpdatedModsInfo' -NotePropertyValue $false -Force
	}
	$json | ConvertTo-Json -Depth 10 | Out-File $config -Encoding utf8
}

$copiaMundo = Join-Path $sandbox "Worlds\$mundo.wld.antes-de-la-prueba"
if (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld")) {
	Copy-Item (Join-Path $sandbox "Worlds\$mundo.wld") $copiaMundo -Force
}

# ---- 2. Compilar el proyecto ENTERO con el compilador real de tModLoader -----------------
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

# ---- 2b. Mods habilitados en el sandbox (mismo patron que verificar-guia.ps1 -Calamity) --
if ($Calamity) {
	$origenCalamity = Get-ChildItem (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods') -Filter '*CalamityMod.tmod' |
		Select-Object -First 1
	if (-not $origenCalamity) { throw 'No se encuentra CalamityMod.tmod en la carpeta Mods real.' }
	Copy-Item $origenCalamity.FullName (Join-Path $sandbox 'Mods\CalamityMod.tmod') -Force
	'["TerrakeepMod","CalamityMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
} else {
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 3. Lanzar el cliente real, sin otro cliente grafico a la vez ------------------------
for ($i = 0; $i -lt 60; $i++) {
	$otros = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like '*tModLoader.dll*' -and $_.CommandLine -notlike '*-server*' }
	if (-not $otros) { break }
	Write-Host 'Esperando a que no haya otro cliente de tModLoader abierto...' -ForegroundColor DarkGray
	Start-Sleep -Seconds 2
}

$env:TERRAKEEP_AUTOTEST_ESPACIADO = '1'
# El resto de autopruebas se apagan explicitamente para que no se peleen por la misma interfaz.
$env:TERRAKEEP_AUTOTEST = ''
$env:TERRAKEEP_AUTOTEST_PANEL = ''
$env:TERRAKEEP_AUTOTEST_WS1 = ''
$env:TERRAKEEP_AUTOTEST_WS3 = ''
$env:TERRAKEEP_AUTOTEST_WS5 = ''
$env:TERRAKEEP_AUTOTEST_WS6 = ''
$env:TERRAKEEP_AUTOTEST_WS7 = ''
$env:TERRAKEEP_AUTOTEST_BUILDS = ''
$env:TERRAKEEP_AUTOTEST_TOOLTIP = ''

$evidencia = Join-Path $sandbox 'terrakeep-espaciado-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FocoEspaciado {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@ -ErrorAction SilentlyContinue

Write-Host "Esperando evidencia en $evidencia (hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA ESPACIADO COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1

	if ($i -eq 15 -or $i -eq 40 -or $i -eq 80) {
		$nuestro = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
			Where-Object { $_.CommandLine -like "*$sandbox*" } | Select-Object -First 1
		if ($nuestro) {
			$h = (Get-Process -Id $nuestro.ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
			if ($h -and $h -ne 0) { [void][FocoEspaciado]::SetForegroundWindow($h) }
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
	$sufijo = if ($Calamity) { '-calamity' } else { '' }
	$destino = Join-Path $repo ('evidencia\espaciado' + $sufijo + '.log.txt')
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray

	$capturas = Join-Path $sandbox 'terrakeep-capturas'
	if (Test-Path $capturas) {
		$destinoCapturas = Join-Path $repo ('evidencia\espaciado-capturas' + $sufijo)
		New-Item -ItemType Directory -Force -Path $destinoCapturas | Out-Null
		Copy-Item (Join-Path $capturas '*.png') $destinoCapturas -Force
		Write-Host "Capturas reales copiadas a $destinoCapturas :" -ForegroundColor DarkGray
		Get-ChildItem $destinoCapturas -Filter *.png | ForEach-Object { Write-Host "  $($_.Name)  ($($_.Length) bytes)" -ForegroundColor DarkGray }
	}
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Write-Host 'Ultimas lineas del client.log del juego, por si dice algo:' -ForegroundColor DarkGray
	Get-Content (Join-Path $logDir 'client.log') -Tail 30 -ErrorAction SilentlyContinue
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
	Where-Object { $_.CommandLine -like "*$sandbox*" } |
	ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Start-Sleep -Seconds 2
$mundoActual = Join-Path $sandbox "Worlds\$mundo.wld"
if (Test-Path $copiaMundo) {
	$antes = (Get-FileHash $copiaMundo).Hash
	$ahora = if (Test-Path $mundoActual) { (Get-FileHash $mundoActual).Hash } else { '' }
	if ($antes -ne $ahora) {
		Copy-Item $copiaMundo $mundoActual -Force
		Write-Host 'El mundo de prueba habia cambiado en disco: restaurado desde la copia previa.' -ForegroundColor Yellow
	}
}

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
	$malos = Select-String -Path $evidencia -Pattern 'FALLO|NO CUADRA|EXCEPCION|NO COINCIDE|NO CABE' -Encoding UTF8
	if ($malos) {
		Write-Host "PERO hay comprobaciones en rojo:" -ForegroundColor Red
		$malos | ForEach-Object { Write-Host "  $($_.Line)" -ForegroundColor Red }
		exit 1
	}
	Write-Host 'Ninguna comprobacion en rojo.' -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo'. Revisar $evidencia." -ForegroundColor Red
	exit 1
}
