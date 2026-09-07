# Verifica el panel de INVESTIGACION (WS5) ejecutando tModLoader DE VERDAD, sobre una carpeta de
# guardado aislada propia de este workstream. Nunca toca los personajes ni los mundos reales del
# usuario.
#
#   .\verificar-investigacion.ps1                 -> las dos fases (recomendado):
#                                                    1) investigar: bateria completa sobre el
#                                                       estado real de investigacion + guardado
#                                                       del personaje con Player.SavePlayer;
#                                                    2) comprobar: se relanza el juego y se lee lo
#                                                       investigado RECIEN CARGADO DEL DISCO, que
#                                                       es lo que demuestra que acabo dentro del
#                                                       .plr de verdad.
#   .\verificar-investigacion.ps1 -SinPersistencia -> solo la fase 1 (mas rapido).
#   .\verificar-investigacion.ps1 -Completo        -> compila el proyecto ENTERO en vez de una
#                                                    copia aislada con lo de WS5.
#   .\verificar-investigacion.ps1 -Calamity        -> con CalamityMod habilitado, para ver las
#                                                    carpetas de mod del arbol y que sus objetos
#                                                    tambien se investigan.
#
# Mismas precauciones que ya aprendio WS4 construyendo varios workstreams en paralelo:
#
#  1. SANDBOX PROPIO (tModLoader-TerrakeepWS5). Compartirlo significa pisarse el .tmod y el
#     enabled.json con los demas agentes.
#
#  2. COMPILA UNA COPIA AISLADA con lo imprescindible (el nucleo del mod, WS7 - del que WS5 usa el
#     historial de deshacer - los cuatro widgets de WS1 que comparte la interfaz, y lo de WS5).
#     El compilador de tModLoader compila TODOS los .cs de la carpeta del mod, asi que con otros
#     workstreams a medias en el mismo repo una compilacion del proyecto entero puede fallar por
#     codigo ajeno (ha pasado: WS6 tenia un CS0103 a medias mientras se escribia esto).
#
#  3. EVIDENCIA EN ARCHIVO PROPIO dentro del sandbox (terrakeep-ws5-evidencia.log), no en el
#     client.log del juego: ese es uno solo para todas las instancias y tModLoader lo rota al
#     arrancar, asi que otro agente lanzando el juego a la vez se lleva por delante la evidencia.
#
#  4. Al terminar solo se mata LO QUE HA LANZADO ESTE SCRIPT.

param(
	# OBSOLETO desde la migracion de idiomas: se conserva el parametro para no romper a quien lo
	# escriba, pero ya no hace nada. La copia AISLADA (solo el nucleo y los archivos de este
	# workstream) existia cuando cuatro agentes editaban el repositorio a la vez y se borraban
	# archivos unos a otros en caliente. Hoy el mod es una sola pieza: todas las areas comparten
	# los widgets, la paleta y el sistema de idiomas, asi que una copia parcial YA NO COMPILA
	# (comprobado: "Fallo el -build de tModLoader"). Se compila siempre el proyecto entero.
	[switch]$Completo,
	[switch]$Calamity,
	[switch]$SinPersistencia,
	[int]$SegundosEspera = 240
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS5'
$origenWs0 = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'
$evidencia = Join-Path $sandbox 'terrakeep-ws5-evidencia.log'
$destino   = Join-Path $repo ('evidencia\ws5-investigacion' + $(if ($Calamity) { '-calamity' } else { '' }) + '.log.txt')

# ---- 0. Sandbox propio, copiando el personaje y el mundo sinteticos de WS0 -----------------
# El personaje TerrakeepPrueba lo genero WS0 desde cero (nunca deriva de ningun archivo real del
# usuario) y el mundo es un 4200x1200 clasico generado tambien para las pruebas.
New-Item -ItemType Directory -Force -Path "$sandbox\Players", "$sandbox\Worlds", "$sandbox\Mods" | Out-Null
if (-not (Test-Path (Join-Path $sandbox "Players\$personaje.plr"))) {
	if (-not (Test-Path (Join-Path $origenWs0 "Players\$personaje.plr"))) {
		throw "No hay personaje de prueba ni en $sandbox ni en $origenWs0."
	}
	Copy-Item (Join-Path $origenWs0 "Players\$personaje.plr") "$sandbox\Players" -Force
	Write-Host "Copiado el personaje de prueba desde el sandbox de WS0." -ForegroundColor DarkGray
}
if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	Copy-Item (Join-Path $origenWs0 "Worlds\$mundo.*") "$sandbox\Worlds" -Force
	Write-Host "Copiado el mundo de prueba desde el sandbox de WS0." -ForegroundColor DarkGray
}

# ---- 1. Proyecto que se va a compilar ------------------------------------------------------
if ($true) {
	$proyecto = $repo
	Write-Host '== Compilando el PROYECTO ENTERO (todos los workstreams) ==' -ForegroundColor Cyan
} else {
	$proyecto = Join-Path $sandbox 'ModSources\TerrakeepMod'
	Write-Host '== Compilando una copia AISLADA (nucleo + WS7 + widgets de WS1 + WS5) ==' -ForegroundColor Cyan

	if (Test-Path $proyecto) { Remove-Item $proyecto -Recurse -Force -Confirm:$false }
	New-Item -ItemType Directory -Force -Path $proyecto,
		"$proyecto\Common\Ajustes", "$proyecto\Common\Undo", "$proyecto\Common\Investigacion",
		"$proyecto\UI\Ajustes", "$proyecto\UI\Investigacion", "$proyecto\UI\Personaje\Widgets",
		"$proyecto\Localization", "$proyecto\lib", "$proyecto\Assets" | Out-Null

	Copy-Item (Join-Path (Split-Path -Parent $repo) 'tModLoader.targets') (Split-Path -Parent $proyecto) -Force
	Copy-Item "$repo\build.txt","$repo\description.txt","$repo\TerrakeepMod.csproj","$repo\Terrakeep.cs" $proyecto -Force
	Copy-Item "$repo\Common\Ajustes\*.cs"       "$proyecto\Common\Ajustes" -Force
	Copy-Item "$repo\Common\Undo\*.cs"          "$proyecto\Common\Undo" -Force
	Copy-Item "$repo\Common\Investigacion\*.cs" "$proyecto\Common\Investigacion" -Force
	Copy-Item "$repo\UI\Ajustes\*.cs"           "$proyecto\UI\Ajustes" -Force
	Copy-Item "$repo\UI\Investigacion\*.cs"     "$proyecto\UI\Investigacion" -Force
	# Solo los cuatro widgets de WS1 que usa esta interfaz; los demas arrastran mas archivos.
	foreach ($w in 'EstiloTk','BotonTk','EtiquetaTk','AlternadorTk') {
		Copy-Item "$repo\UI\Personaje\Widgets\$w.cs" "$proyecto\UI\Personaje\Widgets" -Force
	}
	Copy-Item "$repo\Localization\*.hjson" "$proyecto\Localization" -Force
	Copy-Item "$repo\lib\Terrakeep.Core.dll" "$proyecto\lib" -Force
	Copy-Item "$repo\Assets\*.json" "$proyecto\Assets" -Force
}

# ---- 2. Compilar con el compilador REAL de tModLoader (sin -eac) ---------------------------
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
	$origen = Get-ChildItem (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods') -Filter '*CalamityMod.tmod' |
		Select-Object -First 1
	if (-not $origen) { throw 'No se encuentra CalamityMod.tmod en la carpeta Mods real.' }
	Copy-Item $origen.FullName (Join-Path $sandbox 'Mods\CalamityMod.tmod') -Force
	'["TerrakeepMod","CalamityMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
} else {
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}

# ---- 4. Lanzar el cliente real -------------------------------------------------------------
function Ejecutar-Fase {
	param([string]$Fase, [string]$Objetivo)

	$marca = "WS5-$Fase-$([DateTime]::Now.ToString('HHmmss'))-$PID"
	$env:TERRAKEEP_AUTOTEST_WS5 = '1'
	$env:TERRAKEEP_WS5_FASE     = $Fase
	$env:TERRAKEEP_WS5_MARCA    = $marca
	Remove-Item $evidencia -Force -ErrorAction SilentlyContinue

	# Dos clientes de tModLoader a la vez no se llevan bien (ya lo anoto WS4: una ejecucion no
	# llego ni a arrancar mientras otro agente tenia el juego abierto). Con varios workstreams en
	# paralelo, se espera a que el otro termine en vez de pisarse.
	for ($espera = 0; $espera -lt 300; $espera++) {
		$ajenos = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
			Where-Object { $_.CommandLine -like '*tModLoader.dll*' -and $_.CommandLine -notlike '*-build*' -and $_.CommandLine -notlike "*$sandbox*" }
		if (-not $ajenos) { break }
		if ($espera -eq 0) { Write-Host 'Hay otro cliente de tModLoader abierto (otro agente); esperando...' -ForegroundColor Yellow }
		Start-Sleep -Seconds 1
	}

	Write-Host "== Cliente grafico, fase '$Fase' (marca $marca) ==" -ForegroundColor Cyan
	$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
		-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

	# Terraria congela la partida cuando su ventana pierde el foco (Main.hasFocus -> gamePaused),
	# y con la partida congelada la autoprueba no avanza. Se le devuelve el foco, igual que hace
	# WS1 - pero SOLO a la ventana de ESTA prueba, buscandola por su linea de comandos
	# (-tmlsavedirectory con nuestro sandbox). Filtrar por el titulo de la ventana no vale: con
	# otro agente probando a la vez, su ventana tambien pone "Terraria: ..." y le acabariamos
	# dando el foco a la suya, dejando la nuestra congelada. Paso de verdad la primera vez.
	Start-Sleep -Seconds 12
	try {
		Add-Type -Namespace Win -Name Api -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
'@ -ErrorAction SilentlyContinue
	} catch { }
	$mios = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like "*$sandbox*" } | Select-Object -ExpandProperty ProcessId
	foreach ($id in $mios) {
		$proc = Get-Process -Id $id -ErrorAction SilentlyContinue
		if ($proc -and $proc.MainWindowHandle -ne 0) {
			[Win.Api]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
		}
	}

	$encontrado = $false
	for ($i = 0; $i -lt $SegundosEspera; $i++) {
		Start-Sleep -Seconds 1
		if ((Test-Path $evidencia) -and (Select-String -Path $evidencia -Pattern $Objetivo -Quiet -ErrorAction SilentlyContinue)) {
			$encontrado = $true
			Start-Sleep -Seconds 2
			break
		}
	}

	# Solo lo que ha lanzado ESTE script y lo que corra sobre ESTE sandbox.
	Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
	Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
		Where-Object { $_.CommandLine -like "*$sandbox*" } |
		ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
	Start-Sleep -Seconds 2

	return $encontrado
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
Remove-Item $destino -Force -ErrorAction SilentlyContinue

$okInvestigar = Ejecutar-Fase -Fase 'investigar' -Objetivo 'AUTOPRUEBA WS5 COMPLETA'
if (Test-Path $evidencia) { Get-Content $evidencia -Encoding UTF8 | Out-File $destino -Encoding utf8 -Append }

$okComprobar = $true
if (-not $SinPersistencia) {
	$okComprobar = Ejecutar-Fase -Fase 'comprobar' -Objetivo 'fase de comprobacion terminada'
	if (Test-Path $evidencia) { Get-Content $evidencia -Encoding UTF8 | Out-File $destino -Encoding utf8 -Append }
}

Write-Host ''
Write-Host '== Evidencia real de WS5 ==' -ForegroundColor Cyan
# -Encoding UTF8 explicito: el mod escribe UTF-8 y Windows PowerShell 5.1 leeria el archivo con la
# pagina de codigos ANSI, destrozando las tildes y las eñes de los nombres.
if (Test-Path $destino) {
	Get-Content $destino -Encoding UTF8 | ForEach-Object { $_ }
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray
} else {
	Write-Host '(el mod no llego a escribir ninguna evidencia)' -ForegroundColor Red
	Get-Content (Join-Path $tmlDir 'tModLoader-Logs\client.log') -Tail 20 -ErrorAction SilentlyContinue
}

if ($okInvestigar -and $okComprobar) {
	Write-Host 'OK: las dos fases llegaron a su linea final.' -ForegroundColor Green
} else {
	Write-Host "FALLO: investigar=$okInvestigar, comprobar=$okComprobar" -ForegroundColor Red
	exit 1
}
