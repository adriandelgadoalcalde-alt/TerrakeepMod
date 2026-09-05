# Segunda mitad de la verificacion de WS7, la que NO se puede hacer con el mod llamandose a si
# mismo. Dos modos:
#
#   .\verificar-ws7-interactivo.ps1 -Modo atajos
#       El mod prepara una accion deshacible y despues dispara los atajos rellenando las DOS
#       entradas reales de las que dependen (Main.keyState para el Ctrl y
#       PlayerInput.Triggers.JustPressed para la tecla), y llamando al codigo de produccion tal
#       cual. Este script ademas intenta enviar Ctrl+Z y Ctrl+Y como pulsaciones fisicas.
#
#       AVISO MEDIDO EN WS7: esas pulsaciones fisicas (keybd_event) NO llegan al juego. Con la
#       ventana en primer plano segun Windows y Main.hasFocus=True en el log, ni el Ctrl ni la
#       letra aparecen jamas en Main.keyState (comprobado acumulando el estado fotograma a
#       fotograma durante 12 s, no muestreando). Es una limitacion del arnes, no del mod: lo que
#       de verdad prueba el atajo es la parte de arriba. Se deja el envio porque no estorba y por
#       si algun dia funciona.
#
#   .\verificar-ws7-interactivo.ps1 -Modo captura
#       El mod abre el panel de Ajustes y cambia de idioma cada ~4 s. Este script fotografia LA
#       VENTANA DEL JUEGO (PrintWindow) en los tres estados (espanol -> ingles -> espanol).
#
#       NO FUNCIONA, y se deja documentado para que nadie lo vuelva a intentar: PrintWindow
#       devuelve True pero la imagen sale NEGRA. Es lo esperable en una aplicacion acelerada por
#       GPU (FNA dibuja por Direct3D, no por GDI, asi que no hay nada que "imprimir" del HDC de
#       la ventana). Antes se probo con una captura de pantalla completa (CopyFromScreen) y fue
#       peor: como el juego no estaba en primer plano, lo que se fotografio fue lo que el usuario
#       tenia abierto en ese momento - evidencia inservible y contenido privado en el repo, que se
#       borro en el acto.
#
#       La evidencia real del panel es el LOG, como ya establecio WS0: la clave propia del mod
#       resuelta en cada idioma y Main.InGameUI.CurrentState = PanelAjustesState.
#
# Requiere sesion de Windows desbloqueada y con audio (ver bitacora.md, WS0). Usa el mismo
# sandbox aislado que verificar-ws7.ps1: nunca toca personajes ni mundos reales.

param(
	[ValidateSet('atajos', 'captura')]
	[string]$Modo = 'atajos',
	[int]$SegundosEspera = 150
)

$ErrorActionPreference = 'Stop'

$tmlDir    = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$logDir    = Join-Path $tmlDir 'tModLoader-Logs'
$log       = Join-Path $logDir 'client.log'
$proyecto  = Split-Path -Parent $PSScriptRoot
$evidencia = Join-Path $proyecto 'evidencia'
$sandbox   = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader-TerrakeepWS7'
$mundo     = 'TerrakeepPrueba'
$personaje = 'TerrakeepPrueba'

if (-not (Test-Path (Join-Path $sandbox "Worlds\$mundo.wld"))) {
	throw "Falta el sandbox de WS7 en $sandbox. Ejecuta antes scripts\verificar-ws7.ps1."
}

New-Item -ItemType Directory -Force -Path $evidencia | Out-Null
Copy-Item (Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod') `
	(Join-Path $sandbox 'Mods\TerrakeepMod.tmod') -Force

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Entrada {
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
	[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
	[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
	[DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
	[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
	[DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, ref RECT r);
	public struct RECT { public int Left, Top, Right, Bottom; }
	public const byte VK_CONTROL = 0x11;
	public const byte VK_MENU = 0x12;
	public const uint KEYUP = 0x0002;

	// Windows solo deja robar el primer plano a un proceso que "ha interactuado" hace poco. El
	// truco documentado y de toda la vida es simular una pulsacion de ALT justo antes: sin esto,
	// SetForegroundWindow devuelve true y no hace nada, la ventana del juego se queda sin foco y
	// Terraria congela Main.keyState (Main.cs: hasFocus = IsActive), asi que ninguna tecla
	// enviada despues llega a contar como pulsacion.
	public static bool TraerAlFrente(IntPtr hWnd) {
		keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
		keybd_event(VK_MENU, 0, KEYUP, UIntPtr.Zero);
		ShowWindow(hWnd, 9);   // SW_RESTORE
		SetForegroundWindow(hWnd);
		System.Threading.Thread.Sleep(500);
		return GetForegroundWindow() == hWnd;
	}
	public static void Combinacion(byte tecla) {
		keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
		System.Threading.Thread.Sleep(60);
		keybd_event(tecla, 0, 0, UIntPtr.Zero);
		System.Threading.Thread.Sleep(90);
		keybd_event(tecla, 0, KEYUP, UIntPtr.Zero);
		System.Threading.Thread.Sleep(60);
		keybd_event(VK_CONTROL, 0, KEYUP, UIntPtr.Zero);
	}
}
'@

function Esperar-Linea([string]$patron, [int]$segundos) {
	for ($i = 0; $i -lt $segundos; $i++) {
		Start-Sleep -Seconds 1
		if ((Test-Path $log) -and (Select-String -Path $log -Pattern $patron -Quiet -SimpleMatch -ErrorAction SilentlyContinue)) {
			return $true
		}
	}
	return $false
}

function Ventana-DelJuego {
	$p = Get-Process -Name dotnet -ErrorAction SilentlyContinue |
		Where-Object { $_.MainWindowHandle -ne 0 -and $_.Path -like "$tmlDir*" } | Select-Object -First 1
	if ($p) { return $p.MainWindowHandle }
	return [IntPtr]::Zero
}

# IMPORTANTE: se fotografia SOLO la ventana del juego, con PrintWindow, nunca la pantalla entera.
# Aprendido a las malas en WS7: una captura de pantalla completa (CopyFromScreen) se llevo por
# delante lo que el usuario tenia abierto en ese momento, porque el juego no estaba en primer
# plano. Ademas de ser una foto inutil como evidencia, es contenido privado que no pinta nada en
# el repo. PrintWindow pide el contenido a la ventana concreta, este donde este.
function Capturar([string]$nombre, $hwnd) {
	if ($hwnd -eq [IntPtr]::Zero) { Write-Host '  (sin ventana del juego, no se captura)' -ForegroundColor Yellow; return }
	$r = New-Object Entrada+RECT
	[void][Entrada]::GetClientRect($hwnd, [ref]$r)
	$ancho = $r.Right - $r.Left; $alto = $r.Bottom - $r.Top
	if ($ancho -le 0 -or $alto -le 0) { Write-Host '  (ventana sin area de cliente)' -ForegroundColor Yellow; return }
	$bmp = New-Object System.Drawing.Bitmap $ancho, $alto
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$hdc = $g.GetHdc()
	$ok = [Entrada]::PrintWindow($hwnd, $hdc, 1)   # PW_CLIENTONLY
	$g.ReleaseHdc($hdc)
	$ruta = Join-Path $evidencia $nombre
	$bmp.Save($ruta, [System.Drawing.Imaging.ImageFormat]::Png)
	$g.Dispose(); $bmp.Dispose()
	Write-Host "  captura (PrintWindow ok=$ok) -> $ruta" -ForegroundColor DarkGray
}

Remove-Item $log -Force -ErrorAction SilentlyContinue
$env:TERRAKEEP_AUTOTEST_WS7 = $Modo
Write-Host "== Cliente grafico, modo '$Modo' ==" -ForegroundColor Cyan
$p = Start-Process -FilePath (Join-Path $tmlDir 'start-tModLoader.bat') -WorkingDirectory $tmlDir -PassThru `
	-ArgumentList @('-tmlsavedirectory', "`"$sandbox`"", '-skipselect', "${personaje}:${mundo}")

try {
	if ($Modo -eq 'atajos') {
		if (-not (Esperar-Linea 'LISTO PARA ATAJOS' $SegundosEspera)) { throw "El mod nunca llego a 'LISTO PARA ATAJOS'." }
		Write-Host 'Escenario preparado. Enviando Ctrl+Z real...' -ForegroundColor Yellow

		$hwnd = Ventana-DelJuego
		if ($hwnd -eq [IntPtr]::Zero) { throw 'No se encontro la ventana del juego.' }
		$conFoco = [Entrada]::TraerAlFrente($hwnd)
		Write-Host "  ventana del juego en primer plano: $conFoco" -ForegroundColor DarkGray
		if (-not $conFoco) { Write-Host '  AVISO: sin foco, Terraria congela el teclado y el atajo no llegara.' -ForegroundColor Red }
		Start-Sleep -Seconds 2

		[Entrada]::Combinacion(0x5A)   # Z
		Start-Sleep -Seconds 3
		Write-Host 'Enviando Ctrl+Y real...' -ForegroundColor Yellow
		[Entrada]::Combinacion(0x59)   # Y
		Esperar-Linea 'AUTOPRUEBA WS7: terminada' 30 | Out-Null
	}
	else {
		if (-not (Esperar-Linea 'LISTO PARA CAPTURA 1' $SegundosEspera)) { throw "El mod nunca llego a 'LISTO PARA CAPTURA 1'." }
		$hwnd = Ventana-DelJuego
		Start-Sleep -Milliseconds 800
		Capturar 'ws7-panel-ajustes-es.png' $hwnd

		if (Esperar-Linea 'LISTO PARA CAPTURA 2' 30) { Start-Sleep -Milliseconds 800; Capturar 'ws7-panel-ajustes-en.png' $hwnd }
		if (Esperar-Linea 'LISTO PARA CAPTURA 3' 30) { Start-Sleep -Milliseconds 800; Capturar 'ws7-panel-ajustes-es-vuelta.png' $hwnd }
		Esperar-Linea 'AUTOPRUEBA WS7: terminada' 30 | Out-Null
	}
}
finally {
	Write-Host ''
	Write-Host '== Lineas [Terrakeep] del log real ==' -ForegroundColor Cyan
	if (Test-Path $log) { Select-String -Path $log -Pattern '\[Terrakeep\]' | ForEach-Object { $_.Line } }

	Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
	Get-Process dotnet -ErrorAction SilentlyContinue |
		Where-Object { $_.Path -like "$tmlDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue
}
