# Verifica en el juego REAL el tooltip vainilla de las ranuras de objeto (Common\Panel\
# AutopruebaTooltipObjeto.cs): Main.HoverItem/hoverItemName se rellenan al poner el raton sobre una
# ranura con objeto en las cuatro zonas (Inventario, Almacenes, Equipo, Libreria) y se vacian al
# apartarlo. Mismo sandbox que WS7/conjuntos/completitud/hitos.
#
# 16-sep-2026 (KeepQA S2, AUDITORIA-SESGOS-16SEP.md): hasta hoy esta autoprueba se lanzaba A MANO
# (ningun script ponia TERRAKEEP_AUTOTEST_TOOLTIP=1; varios la apagaban) y solo dejaba log. Ahora:
#   1. La autoprueba escribe por zona un PAR de volcados de geometria del panel, antes y despues del
#      hover (transicion-hover-<zona>-antes/despues.json) y una captura con el tooltip pintado.
#   2. Este script copia log, capturas y pares a evidencia\ y pasa cada par por la pieza compartida
#      Downloads\KeepQA\src\transicion\verificarTransicion.js con --transicion hover --sin-cambio:
#      pasar el raton por una ranura NO debe mover, agrandar ni ocultar nada del panel. (El
#      rectangulo del tooltip lo pinta el motor fuera del arbol UIElement - ver el comentario de
#      VolcarParDeTransicionHover en la autoprueba - asi que lo que se juzga es el panel.)
#   3. Canario del EXTRACTOR: el "antes" de la primera zona contra si mismo declarado con cambio
#      esperado tiene que dar sin_efecto (codigo 1) - demuestra que la pieza distingue de verdad.
#
#   .\verificar-tooltip-objeto.ps1              -> cliente grafico.

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
$node      = 'C:\Users\adrian\Downloads\dev-tools\node-v24.20.0-win-x64\node.exe'
$verificadorTransicion = 'C:\Users\adrian\Downloads\KeepQA\src\transicion\verificarTransicion.js'

if (-not (Test-Path $node)) { throw "No existe el node portable indexado en herramientas.json: $node" }
if (-not (Test-Path $verificadorTransicion)) { throw "No existe la pieza compartida de KeepQA: $verificadorTransicion" }

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
'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8

Remove-Item (Join-Path $sandbox 'LastLaunchedMods.txt') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $sandbox 'terrakeep-capturas\transicion-hover-*.json') -Force -ErrorAction SilentlyContinue

$log = Join-Path $logDir 'client.log'
Remove-Item $log -Force -ErrorAction SilentlyContinue
Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$objetivo = 'AUTOPRUEBA TOOLTIP COMPLETA'
$env:TERRAKEEP_AUTOTEST_TOOLTIP = '1'
$env:TERRAKEEP_AUTOTEST_CONJUNTOS = ''
$env:TERRAKEEP_AUTOTEST_COMPLETITUD = ''
$env:TERRAKEEP_AUTOTEST_HITOS = ''
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
Write-Host '== Lineas [Terrakeep] AUTOPRUEBA TOOLTIP del log real ==' -ForegroundColor Cyan
if (Test-Path $log) {
	Select-String -Path $log -Pattern 'AUTOPRUEBA TOOLTIP' | ForEach-Object { $_.Line }
} else {
	Write-Host "(no se llego a crear $log)" -ForegroundColor Red
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

# Evidencia CON NOMBRE en el repo + verificacion de los pares con la pieza compartida.
$repo = Split-Path -Parent $PSScriptRoot
$fallosTransicion = 0
if (Test-Path $log) {
	$lineas = Select-String -Path $log -Pattern '\[Terrakeep\]' | ForEach-Object { $_.Line }
	$lineas | Out-File (Join-Path $repo 'evidencia\tooltip-objeto.log.txt') -Encoding utf8
	Write-Host "Log [Terrakeep] copiado a evidencia\tooltip-objeto.log.txt ($($lineas.Count) lineas)." -ForegroundColor DarkGray
}
$capturas = Join-Path $sandbox 'terrakeep-capturas'
if (Test-Path $capturas) {
	$destino = Join-Path $repo 'evidencia\tooltip-objeto-transiciones'
	New-Item -ItemType Directory -Force -Path $destino | Out-Null
	Copy-Item (Join-Path $capturas 'tooltip-hover-*.png') $destino -Force -ErrorAction SilentlyContinue
	Copy-Item (Join-Path $capturas 'transicion-hover-*.json') $destino -Force -ErrorAction SilentlyContinue

	Write-Host ''
	Write-Host '== Pares antes/despues del hover (verificarTransicion.js --sin-cambio) ==' -ForegroundColor Cyan
	& $node $verificadorTransicion --canario 2>&1 | Out-Null
	if ($LASTEXITCODE -ne 0) { Write-Host '  CANARIO de la pieza: FALLO - ningun resultado de abajo es de fiar.' -ForegroundColor Red; $fallosTransicion++ }
	else { Write-Host '  CANARIO de la pieza: OK' -ForegroundColor Green }

	$pares = Get-ChildItem $destino -Filter 'transicion-hover-*-antes.json' -ErrorAction SilentlyContinue
	if (-not $pares) { Write-Host '  SIN PARES: la autoprueba no escribio ningun transicion-hover-*-antes.json' -ForegroundColor Red; $fallosTransicion++ }
	$primero = $true
	foreach ($antes in $pares) {
		$despues = $antes.FullName -replace '-antes\.json$', '-despues.json'
		$nombre = $antes.BaseName -replace '-antes$', ''
		if (-not (Test-Path $despues)) { Write-Host ("  {0,-42} SIN 'despues'" -f $nombre) -ForegroundColor Red; $fallosTransicion++; continue }
		if ($primero) {
			# Canario del EXTRACTOR: el mismo volcado contra si mismo, declarado CON cambio esperado,
			# tiene que dar sin_efecto (codigo 1). Si diera 0, la pieza estaria ciega.
			& $node $verificadorTransicion --antes $antes.FullName --despues $antes.FullName --transicion hover 2>&1 | Out-Null
			if ($LASTEXITCODE -eq 1) { Write-Host '  CANARIO del extractor (antes contra si mismo -> sin_efecto): OK' -ForegroundColor Green }
			else { Write-Host "  CANARIO del extractor: FALLO (codigo $LASTEXITCODE, se esperaba 1)" -ForegroundColor Red; $fallosTransicion++ }
			$primero = $false
		}
		$informe = & $node $verificadorTransicion --antes $antes.FullName --despues $despues --transicion hover --sin-cambio --formato-espec 2>&1 | Out-String
		$codigo = $LASTEXITCODE
		$informe | Out-File (Join-Path $destino "$nombre.informe.txt") -Encoding utf8
		if ($codigo -eq 0) { Write-Host ("  {0,-42} OK (el hover no movio nada del panel)" -f $nombre) -ForegroundColor Green }
		else { Write-Host ("  {0,-42} REVISAR (ver {1}.informe.txt)" -f $nombre, $nombre) -ForegroundColor Yellow; $fallosTransicion++ }
	}
}

Write-Host ''
if ($encontrado) {
	$falla = Select-String -Path $log -Pattern 'FALLO|EXCEPCION' -ErrorAction SilentlyContinue |
		Where-Object { $_.Line -match 'AUTOPRUEBA TOOLTIP' }
	if ($falla) {
		Write-Host "NO: se encontro 'FALLO/EXCEPCION' en las lineas de la autoprueba. Lineas:" -ForegroundColor Red
		$falla | ForEach-Object { $_.Line }
		exit 1
	}
	if ($fallosTransicion -gt 0) {
		Write-Host "NO: $fallosTransicion par(es)/canario(s) de transicion con algo que revisar (ver arriba)." -ForegroundColor Red
		exit 1
	}
	Write-Host "OK: encontrado '$objetivo' en el log, sin ningun FALLO, y los pares de hover en verde." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
