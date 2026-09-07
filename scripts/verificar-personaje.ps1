# Verifica WS1 (panel de Personaje: inventario, almacenes, equipo por conjunto, buffs,
# apariencia, desbloqueos y cabecera) ejecutando tModLoader DE VERDAD y leyendo la evidencia del
# log real del juego. Sale de scripts\verificar-en-juego.ps1 (WS0), con dos diferencias a
# proposito, por las mismas razones que verificar-ws7.ps1:
#
#   1. Sandbox PROPIO: Documents\My Games\Terraria\tModLoader-TerrakeepWS1. Hay varios
#      workstreams trabajando a la vez sobre este repo y compartir la carpeta de guardado daria
#      resultados cruzados (ademas, esta autoprueba MODIFICA el personaje: lo llena de objetos,
#      le cambia el pelo y le activa los 13 desbloqueos).
#   2. Variable de autoprueba propia (TERRAKEEP_AUTOTEST_WS1) ademas de la de WS0
#      (TERRAKEEP_AUTOTEST, que es la que abre el panel): la de WS1 solo dispara la bateria de
#      comprobaciones una vez el panel ya esta abierto.
#
# El sandbox se crea solo la primera vez a partir del de WS0 (personaje sintetico
# TerrakeepPrueba generado con Terrakeep.Core + mundo pequeño de pruebas). NUNCA se tocan
# los personajes ni los mundos reales del usuario.
#
#   .\verificar-personaje.ps1            -> cliente grafico. Requiere sesion de Windows
#                                           desbloqueada y con salida de audio (ver bitacora.md,
#                                           entrada de WS0).
#   .\verificar-personaje.ps1 -Servidor  -> servidor dedicado. Solo comprueba que el mod carga:
#                                           sin cliente no hay Main.LocalPlayer ni interfaz, que
#                                           es justo lo que WS1 tiene que demostrar.

param(
	[switch]$Servidor,
	# Compila la copia de trabajo ENTERA directamente en el sandbox de WS1 (mismo patron que
	# scripts\verificar-libreria.ps1), en vez de copiar el .tmod ya compilado de la carpeta Mods
	# compartida. Pedido real: esa carpeta compartida es la que tiene abierta el juego del
	# usuario cuando esta jugando, y bloquearla con un -build a mitad de partida no es buena idea
	# (ver el AVISO del encargo) - compilar aparte, en el sandbox propio, no toca ese archivo para
	# nada.
	[switch]$CompilarPropio,
	[int]$SegundosEspera = 180
)

$ErrorActionPreference = 'Stop'

$repo       = Split-Path -Parent $PSScriptRoot
$tmlDir     = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet  = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir     = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox    = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS1'
$sandboxWs0 = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo      = 'TerrakeepPrueba'
$personaje  = 'TerrakeepPrueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	if (-not (Test-Path (Join-Path $sandboxWs0 "Worlds\$mundo.wld"))) {
		throw "No hay sandbox de pruebas. Falta $sandboxWs0 (lo crea scripts\verificar-en-juego.ps1 de WS0)."
	}
	Write-Host "Creando el sandbox de WS1 a partir del de WS0..." -ForegroundColor Yellow
	New-Item -ItemType Directory -Force -Path $sandbox | Out-Null
	Copy-Item (Join-Path $sandboxWs0 '*') $sandbox -Recurse -Force
}

New-Item -ItemType Directory -Force -Path (Join-Path $sandbox 'Mods') | Out-Null

if ($CompilarPropio) {
	Write-Host '== Compilando el proyecto ENTERO directamente en el sandbox de WS1 ==' -ForegroundColor Cyan
	Remove-Item (Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force -ErrorAction SilentlyContinue
	Push-Location $tmlDir
	try {
		# Aviso benigno a stderr esperado (ver compilar.ps1): se relaja ErrorActionPreference
		# alrededor de esta unica llamada nativa y se comprueba $LASTEXITCODE de verdad debajo.
		$ErrorActionPreference = 'Continue'
		& $tmlDotnet 'tModLoader.dll' '-server' '-build' $repo '-unsafe' 'false' '-tmlsavedirectory' $sandbox
		$ErrorActionPreference = 'Stop'
		if ($LASTEXITCODE -ne 0) { throw 'Fallo el -build de tModLoader' }
	}
	finally { Pop-Location }

	$tmod = Join-Path $sandbox 'Mods\TerrakeepMod.tmod'
	if (-not (Test-Path $tmod)) { throw "El -build termino sin error pero no aparecio $tmod" }
	Write-Host "OK: $tmod ($((Get-Item $tmod).Length) bytes)" -ForegroundColor Green
	'["TerrakeepMod"]' | Out-File (Join-Path $sandbox 'Mods\enabled.json') -Encoding utf8
}
else {
	Copy-Item (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod') `
		(Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force
}

if ($Servidor) {
	$log = Join-Path $logDir 'server.log'
	Remove-Item $log -Force -ErrorAction SilentlyContinue
	Write-Host '== Servidor dedicado (headless) ==' -ForegroundColor Cyan
	$objetivo = 'Mod cargado'
	$p = Start-Process -FilePath $tmlDotnet -WorkingDirectory $tmlDir -PassThru -WindowStyle Hidden `
		-ArgumentList @('tModLoader.dll', '-server', '-tmlsavedirectory', "`"$sandbox`"",
			'-world', "`"$sandbox\Worlds\$mundo.wld`"", '-players', '1', '-port', '7811', '-nosteam')
}
else {
	$log = Join-Path $logDir 'client.log'
	Remove-Item $log -Force -ErrorAction SilentlyContinue
	Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
	$objetivo = 'AUTOPRUEBA WS1 COMPLETA'
	$env:TERRAKEEP_AUTOTEST = '1'
	$env:TERRAKEEP_AUTOTEST_WS1 = '1'
	$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
		-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

	# Terraria CONGELA la partida en un jugador cuando su ventana pierde el foco
	# (Main.hasFocus = IsActive -> Main.gamePaused). Si se queda en segundo plano, los buffs no
	# caducan y el paso 8 de la autoprueba (que comprueba justo eso) no puede dar verde. Como el
	# arnes se lanza desde una consola que puede robarle el foco, se le devuelve a mano.
	Add-Type -Name Ventanas -Namespace Tk -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
'@ -ErrorAction SilentlyContinue
	for ($intento = 0; $intento -lt 40; $intento++) {
		Start-Sleep -Milliseconds 500
		$ventana = Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue |
			Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -like '*Terraria*' } |
			Select-Object -First 1
		if ($ventana) {
			[Tk.Ventanas]::ShowWindow($ventana.MainWindowHandle, 9) | Out-Null
			[Tk.Ventanas]::SetForegroundWindow($ventana.MainWindowHandle) | Out-Null
			Write-Host "Foco devuelto a la ventana del juego: '$($ventana.MainWindowTitle)'"
			break
		}
	}
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

Write-Host ''
Write-Host '== Excepciones del log (deberia estar vacio) ==' -ForegroundColor Cyan
if (Test-Path $log) {
	$fallos = Select-String -Path $log -Pattern 'Exception|Silent Catch|EXCEPCION' -ErrorAction SilentlyContinue
	if ($fallos) { $fallos | ForEach-Object { $_.Line } } else { Write-Host '(ninguna)' -ForegroundColor Green }
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
