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
	# OBSOLETO desde la migracion de idiomas: se conserva el parametro para no romper a quien lo
	# escriba, pero ya no hace nada. La copia AISLADA (solo el nucleo y los archivos de este
	# workstream) existia cuando cuatro agentes editaban el repositorio a la vez y se borraban
	# archivos unos a otros en caliente. Hoy el mod es una sola pieza: todas las areas comparten
	# los widgets, la paleta y el sistema de idiomas, asi que una copia parcial YA NO COMPILA
	# (comprobado: "Fallo el -build de tModLoader"). Se compila siempre el proyecto entero.
	[switch]$Completo,
	[int]$SegundosEspera = 240,
	# Objetos que se SIEMBRAN en el inventario del personaje de prueba antes de abrir el panel.
	# Es solo el escenario de la prueba: la funcionalidad real de auto-equipar nunca crea nada.
	# A proposito NO estan todos los de la build, para poder ver tambien los casos "no lo tienes".
	[string]$Sembrar = 'MoltenHelmet,MoltenGreaves,NightsEdge,FeralClaws,ObsidianShield,BandofRegeneration',
	[string]$Clase = 'melee',
	# Fuente del catalogo a seleccionar antes de auto-equipar: 'vanilla' o 'calamity'.
	[string]$Fuente = ''
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
if ($true) {
	$proyecto = $repo
	Write-Host '== Compilando el PROYECTO ENTERO (todos los workstreams) ==' -ForegroundColor Cyan
} else {
	$proyecto = Join-Path $sandbox 'ModSources\TerrakeepMod'
	Write-Host '== Compilando una copia AISLADA (nucleo + WS4) ==' -ForegroundColor Cyan

	# Solo lo IMPRESCINDIBLE: la clase raiz del mod (de la que WS4 usa nada mas que LogTag e
	# Instance) y los archivos propios de WS4. Nada de otros workstreams: durante la
	# construccion en paralelo se ha visto que borran y renombran sus archivos en caliente
	# (UI\PanelPruebaState.cs desaparecio a mitad de una prueba), asi que copiar "los de WS0"
	# por nombre rompia la verificacion por causas ajenas a este workstream.
	if (Test-Path $proyecto) { Remove-Item $proyecto -Recurse -Force -Confirm:$false }
	New-Item -ItemType Directory -Force -Path $proyecto, "$proyecto\Common\Builds",
		"$proyecto\UI\Builds", "$proyecto\Localization", "$proyecto\lib", "$proyecto\Assets" | Out-Null

	Copy-Item (Join-Path (Split-Path -Parent $repo) 'tModLoader.targets') (Split-Path -Parent $proyecto) -Force
	Copy-Item "$repo\build.txt","$repo\description.txt","$repo\TerrakeepMod.csproj","$repo\Terrakeep.cs" $proyecto -Force
	Copy-Item "$repo\Common\Builds\*.cs" "$proyecto\Common\Builds" -Force
	Copy-Item "$repo\UI\Builds\*.cs" "$proyecto\UI\Builds" -Force
	Copy-Item "$repo\Localization\*.hjson" "$proyecto\Localization" -Force
	Copy-Item "$repo\lib\TerrasavrNative.Core.dll" "$proyecto\lib" -Force
	Copy-Item "$repo\Assets\*.json" "$proyecto\Assets" -Force
}

# ---- 2. Compilar con el compilador REAL de tModLoader (sin -eac) --------------------------
Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
Push-Location $tmlDir
try {
	# El -build escribe algun aviso benigno a stderr ("WARN: Image loading failed: unknown image
	# type", de icon_small.png). Con $ErrorActionPreference='Stop' PowerShell 5.1 lo trata como
	# error terminante aunque el proceso acabe con exit code 0; se relaja aqui y se comprueba
	# $LASTEXITCODE de verdad justo debajo. Mismo arreglo que ya lleva compilar.ps1.
	$ErrorActionPreference = 'Continue'
	& $tmlDotnet 'tModLoader.dll' '-server' '-build' $proyecto '-unsafe' 'false' '-tmlsavedirectory' $sandbox
	$ErrorActionPreference = 'Stop'
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
# La evidencia se lee del archivo PROPIO que escribe el mod en la carpeta de guardado de este
# sandbox, no del client.log del juego. Motivo real, medido: tModLoader-Logs\client.log es uno
# solo para todas las instancias y se rota al arrancar, asi que otro workstream lanzando el
# juego a la vez se lleva por delante la evidencia de esta prueba (paso dos veces seguidas:
# el client.log acabo siendo el de WS1 y luego el de WS7). El archivo del sandbox es inmune.
$marca = "WS4-$([DateTime]::Now.ToString('HHmmss'))-$PID"
$env:TERRAKEEP_BUILDS_MARCA = $marca
$evidencia = Join-Path $sandbox 'terrakeep-ws4-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue

# Autoprueba propia de WS4 (variable distinta de la de WS0, para no abrir los dos paneles).
$env:TERRAKEEP_AUTOTEST_BUILDS   = '1'
$env:TERRAKEEP_BUILDS_SEMBRAR    = $Sembrar
$env:TERRAKEEP_BUILDS_AUTOEQUIPAR = $Clase
$env:TERRAKEEP_BUILDS_FUENTE     = $Fuente

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

Write-Host "Esperando evidencia en $evidencia (marca $marca, hasta $SegundosEspera s)..."
$objetivo = 'segunda pasada de auto-equipar'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	if ((Test-Path $evidencia) -and (Select-String -Path $evidencia -Pattern $objetivo -Quiet -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		Start-Sleep -Seconds 3   # deja que termine de escribir la segunda pasada
		break
	}
}

Write-Host ''
Write-Host '== Evidencia real de WS4 ==' -ForegroundColor Cyan
if (Test-Path $evidencia) {
	# -Encoding UTF8 explicito: el mod escribe UTF-8 y Windows PowerShell 5.1 leeria el archivo
	# con la pagina de codigos ANSI, destrozando las tildes y las eñes de los nombres.
	Get-Content $evidencia -Encoding UTF8 | ForEach-Object { $_ }
	$destino = Join-Path $repo ('evidencia\ws4-builds' + $(if ($Calamity) { '-calamity' } else { '' }) + '.log.txt')
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Write-Host 'Ultimas lineas del client.log del juego, por si dice algo:' -ForegroundColor DarkGray
	Get-Content (Join-Path $logDir 'client.log') -Tail 15 -ErrorAction SilentlyContinue
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
