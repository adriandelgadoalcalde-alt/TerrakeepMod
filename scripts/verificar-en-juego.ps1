# Verifica TerrakeepMod ejecutando tModLoader DE VERDAD sobre una carpeta de guardado aislada,
# y comprueba la evidencia en el log real del juego. Nunca toca los personajes ni los mundos
# reales del usuario.
#
#   .\verificar-en-juego.ps1 -Servidor    ->  servidor dedicado, sin ventana y sin audio.
#                                             Comprueba que el .tmod carga y que
#                                             lib\TerrasavrNative.Core.dll (net8) se resuelve y
#                                             EJECUTA dentro del runtime .NET 8 de tModLoader.
#                                             Funciona siempre, tambien sin sesion de escritorio.
#
#   .\verificar-en-juego.ps1              ->  cliente grafico completo. Ademas de lo anterior,
#                                             entra al mundo de prueba con -skipselect y la
#                                             autoprueba del mod (TERRAKEEP_AUTOTEST) abre el
#                                             panel sola y deja en el log el objeto real del
#                                             inventario y el rectangulo en pantalla del
#                                             ItemSlot.
#                                             REQUIERE UNA SESION DE WINDOWS DESBLOQUEADA Y CON
#                                             AUDIO: si el sistema no tiene ninguna salida de
#                                             audio, SoundEngine.Initialize muestra un dialogo
#                                             modal que solo se cierra con un clic de raton y el
#                                             juego no llega ni a cargar los mods (ver
#                                             bitacora.md, entrada de WS0).

param(
	[switch]$Servidor,
	[int]$SegundosEspera = 120
)

$ErrorActionPreference = 'Stop'

$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo     = 'TerrakeepPrueba'
$personaje = 'prueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	throw "Falta el mundo de prueba en $sandbox. Se genera con:`n" +
		"  & '$tmlDotnet' tModLoader.dll -server -tmlsavedirectory '$sandbox' -autocreate 1 " +
		"-worldname $mundo -world '$sandbox\Worlds\$mundo.wld' -difficulty 0 -players 1 -port 7799 -nosteam"
}

# El .tmod recien compilado tiene que estar tambien dentro del sandbox: -tmlsavedirectory
# cambia la carpeta Mods entera.
Copy-Item (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod') `
	(Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force

if ($Servidor) {
	$log = Join-Path $logDir 'server.log'
	Remove-Item $log -Force -ErrorAction SilentlyContinue
	Write-Host '== Servidor dedicado (headless) ==' -ForegroundColor Cyan
	$p = Start-Process -FilePath $tmlDotnet -WorkingDirectory $tmlDir -PassThru -WindowStyle Hidden `
		-ArgumentList @('tModLoader.dll', '-server', '-tmlsavedirectory', "`"$sandbox`"",
			'-world', "`"$sandbox\Worlds\$mundo.wld`"", '-players', '1', '-port', '7801', '-nosteam')
}
else {
	$log = Join-Path $logDir 'client.log'
	Remove-Item $log -Force -ErrorAction SilentlyContinue
	Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
	# La autoprueba del mod se activa con esta variable: espera 180 fotogramas dentro del mundo
	# y abre el panel sola, sin depender de que nadie pulse ninguna tecla.
	$env:TERRAKEEP_AUTOTEST = '1'
	$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
		-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")
}

Write-Host "Esperando evidencia en $log (hasta $SegundosEspera s)..."
$objetivo = if ($Servidor) { 'Mod cargado' } else { 'PANEL ABIERTO' }
$encontrado = $false
for ($i = 0; $i -lt $SegundosEspera; $i++) {
	Start-Sleep -Seconds 1
	if ((Test-Path $log) -and (Select-String -Path $log -Pattern $objetivo -Quiet -ErrorAction SilentlyContinue)) {
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

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

if ($encontrado) {
	Write-Host "OK: encontrado '$objetivo' en el log." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
