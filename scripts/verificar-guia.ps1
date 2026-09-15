# Verificacion de la GUIA EN TIEMPO REAL (primer tramo, pre-Ojo de Cthulhu) ejecutando
# tModLoader DE VERDAD sobre una carpeta de guardado aislada. Nunca toca los personajes ni los
# mundos reales del usuario.
#
#   .\verificar-guia.ps1               -> recorre el tramo entero cambiando el estado real del
#                                         personaje paso a paso y comprueba que la guia señala
#                                         siempre el objetivo que toca. Deja capturas reales.
#   .\verificar-guia.ps1 -Calamity     -> lo mismo con CalamityMod habilitado, para comprobar que
#                                         el aviso de alcance sale y que nada revienta.
#   .\verificar-guia.ps1 -SoloCompilar -> se queda en la compilacion.
#
# Compila SIEMPRE el proyecto entero: la Guia es una septima pestaña del panel unico y lo que hay
# que demostrar es que convive con las otras seis en el mismo .tmod.

param(
	[switch]$Calamity,
	[switch]$SoloCompilar,
	[int]$SegundosEspera = 300
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepGuia'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

# ---- 0. Sandbox propio, clonado del de WS0 la primera vez ----------------------------------
# El personaje de prueba es el SINTETICO que genero WS0 (TerrakeepPrueba), nunca una copia de
# ningun archivo real del usuario.
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

# ---- 1. Compilar el proyecto ENTERO con el compilador real de tModLoader (sin -eac) ---------
Write-Host '== Compilando el proyecto entero ==' -ForegroundColor Cyan
Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
Push-Location $tmlDir
try {
	# El -build escribe un aviso benigno a stderr ("WARN: Image loading failed: unknown image
	# type", de icon_small.png). Con $ErrorActionPreference='Stop' PowerShell 5.1 lo trata como
	# error terminante aunque el proceso acabe con exit code 0; se relaja aqui y se comprueba
	# $LASTEXITCODE de verdad justo debajo.
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

# ---- 1.5 Desactivar el dialogo real de "Mod Changes since last launch" -------------------
# Hallazgo real (15-sep-2026, ver bitacora.md): ese dialogo (Terraria.ModLoader.UI.Interface,
# ModOrganizer.DetectModChangesForInfoMessage) es un UIInfoMessage de verdad que exige un clic en
# "OK"/"Continuar de todos modos" para que el arranque siga - si nadie lo da, el proceso se queda
# colgado ahi sin ningun error visible (mismo sintoma que ya documento una ronda anterior como
# sospecha sin confirmar). Sale cuando el mod Workshop instalado (typicamente CalamityMod, que este
# script SIEMPRE copia de la carpeta Mods real, no de un espejo fijo) tiene distinta version que la
# ultima vez que TAL sandbox arranco con exito (comparado contra Main.SavePath\LastLaunchedMods.txt).
# La solucion real, sin tocar ningun clic: "ShowNewUpdatedModsInfo" es un ajuste YA EXISTENTE y
# persistido en config.json (Terraria.ModLoader.ModLoader.showNewUpdatedModsInfo, leido/escrito con
# Main.Configuration.Get/Put) - con el a "false" el motor ni siquiera comprueba si hay cambios
# (ModOrganizer.DetectModChangesForInfoMessage vuelve vacio en la primera linea). Se fuerza aqui,
# ANTES de cada lanzamiento, por si el config.json del sandbox no existe todavia o lo trae a "true".
$configSandbox = Join-Path $sandbox 'config.json'
if (Test-Path $configSandbox) {
	$json = Get-Content $configSandbox -Raw -Encoding UTF8 | ConvertFrom-Json
	$json | Add-Member -NotePropertyName 'ShowNewUpdatedModsInfo' -NotePropertyValue $false -Force
	($json | ConvertTo-Json -Depth 10) | Out-File $configSandbox -Encoding utf8
} else {
	'{"ShowNewUpdatedModsInfo": false}' | Out-File $configSandbox -Encoding utf8
}
Write-Host "== ShowNewUpdatedModsInfo forzado a false en $configSandbox (evita el dialogo de mods actualizados) ==" -ForegroundColor DarkGray

# ---- 2. Mods habilitados en el sandbox ----------------------------------------------------
if ($Calamity) {
	$origen = Get-ChildItem (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods') -Filter '*CalamityMod.tmod' |
		Select-Object -First 1
	if (-not $origen) { throw 'No se encuentra CalamityMod.tmod en la carpeta Mods real.' }
	Copy-Item $origen.FullName (Join-Path $sandbox 'Mods\CalamityMod.tmod') -Force
	'["TerrakeepMod","CalamityMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
} else {
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 3. Lanzar el cliente real ------------------------------------------------------------
$marca = "GUIA-$([DateTime]::Now.ToString('HHmmss'))-$PID"
$env:TERRAKEEP_GUIA_MARCA = $marca
$env:TERRAKEEP_AUTOTEST_GUIA = '1'
# Las autopruebas de los demas workstreams se apagan explicitamente: si alguna quedara puesta en
# la sesion, se pelearia con esta por la misma interfaz.
$env:TERRAKEEP_AUTOTEST = ''
$env:TERRAKEEP_AUTOTEST_PANEL = ''
$env:TERRAKEEP_AUTOTEST_WS1 = ''
$env:TERRAKEEP_AUTOTEST_WS3 = ''
$env:TERRAKEEP_AUTOTEST_WS5 = ''
$env:TERRAKEEP_AUTOTEST_WS6 = ''
$env:TERRAKEEP_AUTOTEST_WS7 = ''
$env:TERRAKEEP_AUTOTEST_BUILDS = ''
$env:TERRAKEEP_AUTOTEST_IDIOMAS = ''
$env:TERRAKEEP_AUTOTEST_ESPACIADO = ''

$evidencia = Join-Path $sandbox 'terrakeep-guia-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue

# Con otro cliente de tModLoader abierto, el nuestro se queda colgado cargando mods (medido por
# WS3 y WS4). Se espera a que no haya ninguno antes de lanzar.
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

# La partida se congela si la ventana pierde el foco (Main.hasFocus -> gamePaused), y con ella la
# autoprueba. Se le devuelve el foco a NUESTRA ventana, identificada por su linea de comandos
# (filtrar por titulo cogeria la de otro agente: todas ponen "Terraria: ...").
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FocoGuia {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@ -ErrorAction SilentlyContinue

Write-Host "Esperando evidencia en $evidencia (marca $marca, hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA GUIA COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1

	if ($i % 10 -eq 5) {
		$nuestro = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
			Where-Object { $_.CommandLine -like "*$sandbox*" } | Select-Object -First 1
		if ($nuestro) {
			$h = (Get-Process -Id $nuestro.ProcessId -ErrorAction SilentlyContinue).MainWindowHandle
			if ($h -and $h -ne 0) { [void][FocoGuia]::SetForegroundWindow($h) }
		}
	}

	if ((Test-Path $evidencia) -and (Select-String -Path $evidencia -Pattern $objetivo -Quiet -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		Start-Sleep -Seconds 2
		break
	}
}

Write-Host ''
Write-Host '== Evidencia real de la Guia ==' -ForegroundColor Cyan
if (Test-Path $evidencia) {
	# -Encoding UTF8 explicito: el mod escribe UTF-8 y Windows PowerShell 5.1 leeria el archivo
	# con la pagina de codigos ANSI, destrozando las tildes y las eñes.
	Get-Content $evidencia -Encoding UTF8 | ForEach-Object { $_ }
	$destino = Join-Path $repo ('evidencia\guia' + $(if ($Calamity) { '-calamity' } else { '' }) + '.log.txt')
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray

	# Las capturas reales del back buffer que ha dejado el propio mod. Se quedan FUERA del repo
	# (son imagenes grandes y se regeneran con este mismo script).
	$capturas = Join-Path $sandbox 'terrakeep-capturas'
	if (Test-Path $capturas) {
		Write-Host "Capturas reales del juego en: $capturas" -ForegroundColor DarkGray
		Get-ChildItem $capturas -Filter *.png | ForEach-Object { Write-Host "  $($_.Name)  ($($_.Length) bytes)" -ForegroundColor DarkGray }
	}
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Write-Host 'Ultimas lineas del client.log del juego, por si dice algo:' -ForegroundColor DarkGray
	Get-Content (Join-Path $logDir 'client.log') -Tail 40 -ErrorAction SilentlyContinue
}

# Solo se mata LO QUE HA LANZADO ESTE SCRIPT.
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
	Where-Object { $_.CommandLine -like "*$sandbox*" } |
	ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
	$malos = Select-String -Path $evidencia -Pattern 'NO CUADRA|EXCEPCION|NO COINCIDE|NO CABE' -Encoding UTF8
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
