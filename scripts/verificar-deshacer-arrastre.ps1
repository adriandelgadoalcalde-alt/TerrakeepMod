# Verifica en el juego REAL el arreglo de esta sesion: SlotObjetoVanilla (arrastrar/soltar en
# CUALQUIER ranura del mod) ahora pasa por el historial de deshacer/rehacer de WS7, vigilando
# ademas Main.mouseItem y Player.trashItem para que deshacer nunca pierda ni duplique el objeto.
# Mismo patron que scripts\verificar-ws7.ps1 (del que sale este): reutiliza su MISMO sandbox
# (Documents\My Games\Terraria\tModLoader-TerrakeepWS7) porque es el mismo tipo de prueba
# (inventario del personaje sintetico, sin nada que persista entre pasadas que pueda chocar).
#
#   .\verificar-deshacer-arrastre.ps1              -> cliente grafico.
#   .\verificar-deshacer-arrastre.ps1 -Servidor    -> servidor dedicado (solo confirma que el mod
#                                                      carga; sin cliente no hay Main.LocalPlayer).

param(
	[switch]$Servidor,
	[int]$SegundosEspera = 150
)

$ErrorActionPreference = 'Stop'

$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS7'
$sandboxWs0 = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS0'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

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

# Hallazgo real de esta sesion: si algun mod de Steam Workshop (HEROsMod/CalamityMod...) se
# actualizo desde el ultimo lanzamiento de ESTE sandbox, tModLoader.ModLoader.Core.ModOrganizer.
# DetectModChangesForInfoMessage (decompilado, linea ~342) compara la version actual contra
# "LastLaunchedMods.txt" y, si no coincide, PanelTerrakeepSystem nunca llega a arrancar: el juego
# se queda parado en una pantalla informativa ("Mod Changes since last launch") que pide un clic
# real para continuar - -skipselect no la salta, y sin nadie delante se queda ahi para siempre
# (visto en el juego real: el log se corta justo despues de "Mods actualizados: ..."). Si el
# archivo NO EXISTE, la funcion devuelve vacio sin comprobar nada (misma linea 344) y la pantalla
# no aparece nunca - por eso se borra aqui en cada pasada, sea la primera vez o no.
Remove-Item (Join-Path $sandbox 'LastLaunchedMods.txt') -Force -ErrorAction SilentlyContinue

if ($Servidor) {
	$log = Join-Path $logDir 'server.log'
	Remove-Item $log -Force -ErrorAction SilentlyContinue
	Write-Host '== Servidor dedicado (headless) ==' -ForegroundColor Cyan
	$objetivo = 'Mod cargado'
	$p = Start-Process -FilePath $tmlDotnet -WorkingDirectory $tmlDir -PassThru -WindowStyle Hidden `
		-ArgumentList @('tModLoader.dll', '-server', '-tmlsavedirectory', "`"$sandbox`"",
			'-world', "`"$sandbox\Worlds\$mundo.wld`"", '-players', '1', '-port', '7807', '-nosteam')
}
else {
	$log = Join-Path $logDir 'client.log'
	Remove-Item $log -Force -ErrorAction SilentlyContinue
	Write-Host '== Cliente grafico ==' -ForegroundColor Cyan
	$objetivo = 'AUTOPRUEBA ARRASTRE: terminada'
	$env:TERRAKEEP_AUTOTEST_ARRASTRE = '1'
	$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
		-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")
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

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host ''
if ($encontrado) {
	$falla = Select-String -Path $log -Pattern 'FALLO' -SimpleMatch -ErrorAction SilentlyContinue
	if ($falla) {
		Write-Host "NO: se encontro 'FALLO' en el log. Lineas:" -ForegroundColor Red
		$falla | ForEach-Object { $_.Line }
		exit 1
	}
	Write-Host "OK: encontrado '$objetivo' en el log, sin ningun FALLO." -ForegroundColor Green
} else {
	Write-Host "NO se encontro '$objetivo' en el log. Mirar $log completo." -ForegroundColor Red
	exit 1
}
