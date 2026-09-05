# Verifica el panel de BUILDS (WS4) ejecutando tModLoader DE VERDAD, sobre una carpeta de
# guardado aislada propia de este workstream. Nunca toca los personajes ni los mundos reales
# del usuario.
#
#   .\verificar-builds-en-juego.ps1              -> catalogo + "ya lo tienes" + auto-equipar
#                                                   (dos pasadas, la segunda comprueba que es
#                                                   idempotente) sobre la build vanilla
#                                                   Pre-Hardmode / Cuerpo a cuerpo.
#   .\verificar-builds-en-juego.ps1 -Calamity    -> lo mismo, pero con CalamityMod habilitado,
#                                                   para comprobar la resolucion de los pid
#                                                   "CalamityMod/NombreInterno".
#
# Diferencias a proposito con scripts\verificar-en-juego.ps1 (WS0):
#
#  1. SANDBOX PROPIO (tModLoader-TerrakeepWS4). Mientras WS1/WS4/WS7 se construyen en paralelo
#     sobre el mismo repo, compartir sandbox significa pisarse el .tmod y el enabled.json.
#
#  2. COMPILA UNA COPIA AISLADA del proyecto, con los archivos de WS0 + WS4 y nada mas
#     (parametro -Completo para compilar el proyecto entero). El compilador de tModLoader
#     compila TODOS los .cs de la carpeta del mod, asi que con otros workstreams a medias en el
#     mismo repo una compilacion del proyecto entero puede fallar por codigo ajeno a WS4.
#
#  3. El -build lleva -tmlsavedirectory, asi que el .tmod sale DIRECTAMENTE en el sandbox de
#     WS4 en vez de en la carpeta Mods compartida (comprobado que funciona: ModCompile guarda
#     en ModLoader.ModPath, que cuelga de Main.SavePath). Sin esto, dos agentes compilando a la
#     vez se sobrescriben el .tmod el uno al otro.

param(
	[switch]$Calamity,
	[switch]$Completo,
	[int]$SegundosEspera = 240,
	# Objetos que se SIEMBRAN en el inventario del personaje de prueba antes de abrir el panel.
	# Es solo el escenario de la prueba: la funcionalidad real de auto-equipar nunca crea nada.
	# A proposito NO estan todos los de la build, para poder ver tambien los casos "no lo tienes".
	[string]$Sembrar = 'MoltenHelmet,MoltenGreaves,NightsEdge,FeralClaws,ObsidianShield,BandofRegeneration',
	[string]$Clase = 'melee'
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS4'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	throw "Falta el mundo de prueba en $sandbox\Worlds. Copiarlo del sandbox de WS0 " +
		"(tModLoader-TerrakeepWS0) o generarlo con -autocreate."
}

# ---- 1. Preparar el proyecto que se va a compilar ----------------------------------------
if ($Completo) {
	$proyecto = $repo
	Write-Host '== Compilando el PROYECTO ENTERO (todos los workstreams) ==' -ForegroundColor Cyan
} else {
	$proyecto = Join-Path $sandbox 'ModSources\TerrakeepMod'
	Write-Host '== Compilando una copia AISLADA con WS0 + WS4 ==' -ForegroundColor Cyan

	if (Test-Path $proyecto) { Remove-Item $proyecto -Recurse -Force -Confirm:$false }
	New-Item -ItemType Directory -Force -Path $proyecto, "$proyecto\Common\Builds",
		"$proyecto\UI\Builds", "$proyecto\Localization", "$proyecto\lib", "$proyecto\Assets" | Out-Null

	Copy-Item (Join-Path (Split-Path -Parent $repo) 'tModLoader.targets') (Split-Path -Parent $proyecto) -Force
	Copy-Item "$repo\build.txt","$repo\description.txt","$repo\TerrakeepMod.csproj","$repo\Terrakeep.cs" $proyecto -Force
	Copy-Item "$repo\Common\PanelPruebaPlayer.cs","$repo\Common\PanelPruebaSystem.cs" "$proyecto\Common" -Force
	Copy-Item "$repo\Common\Builds\*.cs" "$proyecto\Common\Builds" -Force
	Copy-Item "$repo\UI\PanelPruebaState.cs","$repo\UI\SlotObjetoVanilla.cs" "$proyecto\UI" -Force
	Copy-Item "$repo\UI\Builds\*.cs" "$proyecto\UI\Builds" -Force
	Copy-Item "$repo\Localization\*.hjson" "$proyecto\Localization" -Force
	Copy-Item "$repo\lib\TerrasavrNative.Core.dll" "$proyecto\lib" -Force
	Copy-Item "$repo\Assets\*.json" "$proyecto\Assets" -Force
}

# ---- 2. Compilar con el compilador REAL de tModLoader (sin -eac) --------------------------
Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
Push-Location $tmlDir
try {
	& $tmlDotnet 'tModLoader.dll' '-server' '-build' $proyecto '-unsafe' 'false' '-tmlsavedirectory' $sandbox
	if ($LASTEXITCODE -ne 0) { throw 'Fallo el -build de tModLoader' }
}
finally { Pop-Location }

$tmod = Join-Path $sandbox 'Mods\TerrakeepMod.tmod'
if (-not (Test-Path $tmod)) { throw "El -build termino sin error pero no aparecio $tmod" }
Write-Host "OK: $tmod ($((Get-Item $tmod).Length) bytes)" -ForegroundColor Green

# ---- 3. Mods habilitados en el sandbox ----------------------------------------------------
if ($Calamity) {
	foreach ($m in 'CalamityMod') {
		$origen = Get-ChildItem (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods') -Filter "*$m.tmod" |
			Select-Object -First 1
		if (-not $origen) { throw "No se encuentra $m.tmod en la carpeta Mods real." }
		Copy-Item $origen.FullName (Join-Path $sandbox "Mods\$m.tmod") -Force
	}
	'["TerrakeepMod","CalamityMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
} else {
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 4. Lanzar el cliente real ------------------------------------------------------------
# El log del juego es COMPARTIDO entre todas las instancias, y tModLoader rota client.log ->
# client1.log al arrancar. Con varios workstreams lanzando el juego a la vez, la evidencia
# puede acabar en cualquiera de ellos, asi que se buscan todos los client*.log y se elige el
# que contenga la marca de ESTA ejecucion.
$marca = "WS4-$([DateTime]::Now.ToString('HHmmss'))-$PID"
$env:TERRAKEEP_BUILDS_MARCA = $marca

function Buscar-LogConMarca {
	param([string]$patron)
	Get-ChildItem $logDir -Filter 'client*.log' -ErrorAction SilentlyContinue |
		Sort-Object LastWriteTime -Descending |
		Where-Object { Select-String -Path $_.FullName -Pattern ([regex]::Escape($marca)) -Quiet -ErrorAction SilentlyContinue } |
		Where-Object { Select-String -Path $_.FullName -Pattern $patron -Quiet -ErrorAction SilentlyContinue } |
		Select-Object -First 1
}

# Autoprueba propia de WS4 (variable distinta de la de WS0, para no abrir los dos paneles).
$env:TERRAKEEP_AUTOTEST_BUILDS   = '1'
$env:TERRAKEEP_BUILDS_SEMBRAR    = $Sembrar
$env:TERRAKEEP_BUILDS_AUTOEQUIPAR = $Clase

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

Write-Host "Esperando evidencia en $logDir\client*.log (marca $marca, hasta $SegundosEspera s)..."
$objetivo = 'segunda pasada de auto-equipar'
$log = $null
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	$log = Buscar-LogConMarca $objetivo
	if ($log) {
		Start-Sleep -Seconds 2   # deja que termine de escribir la segunda pasada
		break
	}
}
$encontrado = [bool]$log
if (-not $log) { $log = Buscar-LogConMarca '\[Terrakeep\]' }

Write-Host ''
Write-Host '== Lineas [Terrakeep] del log real ==' -ForegroundColor Cyan
if ($log) {
	Write-Host "(log: $($log.FullName))" -ForegroundColor DarkGray
	Select-String -Path $log.FullName -Pattern '\[Terrakeep\]' | ForEach-Object { $_.Line }
} else {
	Write-Host "(ningun client*.log con la marca $marca)" -ForegroundColor Red
}

# Solo se mata LO QUE HA LANZADO ESTE SCRIPT: con varios workstreams probando a la vez, un
# "Stop-Process -Name dotnet" a secas tumbaria tambien la instancia de otro agente.
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
	Where-Object { $_.CommandLine -like "*$sandbox*" } |
	ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
