# Verifica el panel de EXPLORACION (WS6) ejecutando tModLoader DE VERDAD, sobre una carpeta de
# guardado aislada propia de este workstream. Nunca toca los personajes ni los mundos reales
# del usuario.
#
#   .\verificar-exploracion.ps1                 -> autoprueba completa (mini-mapa, busqueda,
#                                                  "Ver en el mapa" y dificultad).
#   .\verificar-exploracion.ps1 -Buscar 'Oro / Platino'
#   .\verificar-exploracion.ps1 -Completo       -> compila el proyecto ENTERO, no solo WS6.
#
# Decisiones a proposito, todas por convivir con los demas agentes trabajando a la vez:
#
#  1. SANDBOX PROPIO (tModLoader-TerrakeepWS6). Compartirlo significa pisarse el .tmod y el
#     enabled.json con WS3/WS5, que estan lanzando el juego ahora mismo.
#
#  2. La evidencia se lee del archivo PROPIO que escribe el mod dentro de ese sandbox
#     (terrakeep-ws6-evidencia.log), no del client.log del juego: el client.log es uno solo para
#     todas las instancias y tModLoader lo rota al arrancar, asi que con varios clientes a la vez
#     la evidencia del log compartido es una carrera perdida (ver bitacora, WS4).
#
#  3. AUTOGUARDADO DESACTIVADO en el config.json del sandbox, y ademas se guarda una copia del
#     .wld antes de la prueba y se restaura despues. La prueba cambia el modo de juego del mundo,
#     que es un cambio PERMANENTE en el siguiente guardado: sin esto, cada ejecucion dejaria el
#     mundo de prueba distinto para la siguiente.
#
#  4. Por defecto compila una copia AISLADA con el nucleo del mod y WS6 (mas los widgets de WS1 y
#     el historial de WS7, que WS6 reutiliza), sin el codigo de WS3/WS5, que estan a medias.

param(
	[switch]$Completo,
	[int]$SegundosEspera = 300,
	# Objetivo de busqueda que prueba la autoprueba. Basta con el principio del nombre y sin
	# tildes: el entorno de un proceso hijo lanzado desde PowerShell no viaja en UTF-8 (medido:
	# "Cobre / Estaño" le llega al juego como "Cobre / EstaÃ±o" y no casa con nada), asi que el
	# valor por defecto es ASCII puro a proposito.
	[string]$Buscar = 'Cobre',
	# Radio en tiles de mapa que se revela alrededor de la aparicion ANTES de la prueba. Es
	# escenario de prueba: el personaje sintetico no ha explorado nada y sin esto el mini-mapa no
	# tendria nada real que dibujar.
	[int]$Revelar = 400
)

$ErrorActionPreference = 'Stop'

$repo      = Split-Path -Parent $PSScriptRoot
$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS6'
$origenSandbox = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

# ---- 1. Sandbox propio, copiado del de WS0 la primera vez ---------------------------------
if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	Write-Host "== Creando el sandbox de WS6 a partir del de WS0 ==" -ForegroundColor Cyan
	New-Item -ItemType Directory -Force -Path $sandbox, "$sandbox\Worlds", "$sandbox\Players", "$sandbox\Mods" | Out-Null
	Copy-Item "$origenSandbox\Worlds\*" "$sandbox\Worlds" -Force
	Copy-Item "$origenSandbox\Players\*" "$sandbox\Players" -Force
	Copy-Item "$origenSandbox\config.json" $sandbox -Force
}

# Autoguardado FUERA: la prueba toca el modo de juego del mundo y eso se graba al guardar.
$config = Join-Path $sandbox 'config.json'
$json = Get-Content $config -Raw -Encoding UTF8 | ConvertFrom-Json
$json.AutoSave = $false
$json.MapEnabled = $true
$json | ConvertTo-Json -Depth 10 | Out-File $config -Encoding utf8
Write-Host "config.json del sandbox: AutoSave=$($json.AutoSave), MapEnabled=$($json.MapEnabled)" -ForegroundColor DarkGray

# Copia de seguridad del mundo, por si algo se guardara igualmente.
$copiaMundo = Join-Path $sandbox "Worlds\$mundo.wld.antes-de-la-prueba"
Copy-Item (Join-Path $sandbox "Worlds\$mundo.wld") $copiaMundo -Force

# ---- 2. Proyecto que se va a compilar -----------------------------------------------------
if ($Completo) {
	$proyecto = $repo
	Write-Host '== Compilando el PROYECTO ENTERO (todos los workstreams) ==' -ForegroundColor Cyan
} else {
	$proyecto = Join-Path $sandbox 'ModSources\TerrakeepMod'
	Write-Host '== Compilando una copia SIN los workstreams a medias (WS3 Librería / WS5 Investigación) ==' -ForegroundColor Cyan

	# Se copia el repo entero y se quitan solo las carpetas de los dos workstreams que otros dos
	# agentes estan escribiendo AHORA MISMO. Es mas robusto que ir eligiendo archivo por archivo:
	# WS6 reutiliza los widgets de WS1 y el historial de WS7, y esa lista de dependencias cambiaria
	# cada vez que se toque algo. Con -Completo se compila el arbol tal cual, sin quitar nada.
	if (Test-Path $proyecto) { Remove-Item $proyecto -Recurse -Force -Confirm:$false }
	New-Item -ItemType Directory -Force -Path $proyecto | Out-Null

	Copy-Item (Join-Path (Split-Path -Parent $repo) 'tModLoader.targets') (Split-Path -Parent $proyecto) -Force
	Get-ChildItem $repo -Force |
		Where-Object { $_.Name -notin @('.git', 'bin', 'obj', 'evidencia', 'scripts') } |
		ForEach-Object { Copy-Item $_.FullName $proyecto -Recurse -Force }

	foreach ($ajena in 'Common\Libreria', 'Common\Investigacion', 'UI\Libreria', 'UI\Investigacion') {
		Remove-Item (Join-Path $proyecto $ajena) -Recurse -Force -ErrorAction SilentlyContinue
	}
}

# ---- 3. Compilar con el compilador REAL de tModLoader (sin -eac) --------------------------
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

'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8

# ---- 4. Lanzar el cliente real ------------------------------------------------------------
$marca = "WS6-$([DateTime]::Now.ToString('HHmmss'))-$PID"
$env:TERRAKEEP_WS6_MARCA   = $marca
$env:TERRAKEEP_AUTOTEST_WS6 = '1'
$env:TERRAKEEP_WS6_BUSCAR  = $Buscar
$env:TERRAKEEP_WS6_REVELAR = "$Revelar"

$evidencia = Join-Path $sandbox 'terrakeep-ws6-evidencia.log'
Remove-Item $evidencia -Force -ErrorAction SilentlyContinue

Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

# El juego CONGELA la partida cuando su ventana pierde el foco (Main.hasFocus = IsActive ->
# Main.gamePaused), y con la partida congelada la autoprueba no avanza ni un fotograma. Se le
# devuelve el foco, igual que hace el script de WS1.
Start-Sleep -Seconds 12
$sig = @'
using System;
using System.Runtime.InteropServices;
public class Foco {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@
if (-not ('Foco' -as [type])) { Add-Type -TypeDefinition $sig }
Get-Process -Name dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.MainWindowTitle -like '*Terraria*' -or $_.MainWindowTitle -like '*tModLoader*' } |
	ForEach-Object { [Foco]::SetForegroundWindow($_.MainWindowHandle) | Out-Null }

Write-Host "Esperando evidencia en $evidencia (marca $marca, hasta $SegundosEspera s)..."
$objetivo = 'AUTOPRUEBA WS6 COMPLETA'
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	if ((Test-Path $evidencia) -and (Select-String -Path $evidencia -Pattern $objetivo -Quiet -ErrorAction SilentlyContinue)) {
		$encontrado = $true
		Start-Sleep -Seconds 2
		break
	}
}

Write-Host ''
Write-Host '== Evidencia real de WS6 ==' -ForegroundColor Cyan
if (Test-Path $evidencia) {
	# -Encoding UTF8 explicito: el mod escribe UTF-8 y Windows PowerShell 5.1 leeria el archivo con
	# la pagina de codigos ANSI, destrozando las tildes y las eñes.
	Get-Content $evidencia -Encoding UTF8 | ForEach-Object { $_ }
	$destino = Join-Path $repo 'evidencia\ws6-exploracion.log.txt'
	New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
	Copy-Item $evidencia $destino -Force
	Write-Host "(copia guardada en $destino)" -ForegroundColor DarkGray
} else {
	Write-Host "(el mod no llego a escribir $evidencia)" -ForegroundColor Red
	Write-Host 'Ultimas lineas del client.log del juego, por si dice algo:' -ForegroundColor DarkGray
	Get-Content (Join-Path $logDir 'client.log') -Tail 20 -ErrorAction SilentlyContinue
}

# Solo se mata LO QUE HA LANZADO ESTE SCRIPT: con varios workstreams probando a la vez, un
# "Stop-Process -Name dotnet" a secas tumbaria tambien la instancia de otro agente.
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
	Where-Object { $_.CommandLine -like "*$sandbox*" } |
	ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

# ---- 5. Dejar el mundo de prueba como estaba ----------------------------------------------
Start-Sleep -Seconds 2
$mundoActual = Join-Path $sandbox "Worlds\$mundo.wld"
if (Test-Path $copiaMundo) {
	$antes = (Get-FileHash $copiaMundo).Hash
	$ahora = if (Test-Path $mundoActual) { (Get-FileHash $mundoActual).Hash } else { '' }
	if ($antes -ne $ahora) {
		Copy-Item $copiaMundo $mundoActual -Force
		Write-Host 'El mundo de prueba habia cambiado en disco: restaurado desde la copia previa.' -ForegroundColor Yellow
	} else {
		Write-Host 'El mundo de prueba esta byte a byte como antes de la prueba (autoguardado desactivado).' -ForegroundColor Green
	}
}

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log." -ForegroundColor Red
	exit 1
}
