# Compila TerrakeepMod y genera el .tmod real en la carpeta Mods de tModLoader.
#
# Por que hace falta este script y no vale un "dotnet build" a secas:
#   tMLMod.targets termina con un target BuildMod que ejecuta literalmente
#       dotnet tModLoader.dll -server -build <proyecto> ...
#   invocando "dotnet" DEL PATH. En esta maquina el dotnet del PATH es el SDK 10.0.400 y NO
#   hay ningun runtime .NET 8 instalado a nivel de sistema (comprobado con `dotnet
#   --list-runtimes`: 6.0.36, 9.0.14, 10.0.3, 10.0.11). tModLoader.runtimeconfig.json exige
#   Microsoft.NETCore.App 8.0.0 exacto, asi que ese paso falla siempre con
#   "You must install or update .NET to run this application" (MSB3073, codigo -2147450730).
#   tModLoader trae su propio runtime .NET 8 en dotnet\dotnet.exe (es el que usa su propio
#   lanzador, ver LaunchUtils\ScriptCaller.sh), y ese si sirve.
#
# El script hace las dos fases por separado y de forma explicita:
#   1. dotnet build normal -> valida el C# con el SDK del sistema y deja bin\Debug\net8.0\
#      TerrakeepMod.dll. Se desactiva BuildMod para que no intente el paso que sabemos que
#      falla.
#   2. El dotnet de tModLoader ejecuta el -build de verdad, que compila con el Roslyn interno
#      de tModLoader (el mismo que usa el boton "Compilar" del menu Fuentes de mods del juego)
#      y empaqueta el .tmod, metiendo dentro lib\Terrakeep.Core.dll por el
#      "dllReferences" de build.txt.
#
# Nota: NO se pasa -eac. Con -eac, tModLoader reutiliza el DLL ya compilado por MSBuild
# ("Loading pre-compiled TerrakeepMod.dll") en vez de compilarlo el; sin -eac compila de
# verdad con sus propias opciones (LanguageVersion.Preview, sin usings implicitos, nullable
# desactivado - ver ModCompile.RoslynCompile en el codigo decompilado). Nos interesa lo
# segundo: es lo que de verdad demuestra que el codigo compila en las condiciones reales del
# juego.

$ErrorActionPreference = 'Stop'

$proyecto = Split-Path -Parent $PSScriptRoot
$tmlDir   = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader'
$tmlDotnet = Join-Path $tmlDir 'dotnet\dotnet.exe'

if (-not (Test-Path $tmlDotnet)) {
	throw "No se encuentra el dotnet propio de tModLoader en $tmlDotnet"
}

Write-Host "== Fase 1: validar el C# con el SDK del sistema ==" -ForegroundColor Cyan
& dotnet build (Join-Path $proyecto 'TerrakeepMod.csproj') -p:BuildMod=false -p:TargetFramework=net8.0 -p:LangVersion=12.0 -v m
if ($LASTEXITCODE -ne 0) { throw "Fallo el dotnet build (fase 1)" }

Write-Host "== Fase 2: compilar y empaquetar el .tmod con tModLoader ==" -ForegroundColor Cyan
Push-Location $tmlDir
try {
	# El "-build" real de tModLoader escribe alguna linea benigna de aviso a stderr (p.ej.
	# "WARN: Image loading failed: unknown image type" al procesar icon.png/icon_small.png -
	# no impide que el .tmod se genere bien, comprobado). Con $ErrorActionPreference='Stop' (fijado
	# arriba del todo del script) PowerShell 5.1 puede tratar esa salida nativa como un error
	# terminante aunque el proceso acabe con exit code 0 - se relaja aqui, alrededor de esta
	# unica llamada, y se sigue comprobando $LASTEXITCODE de verdad justo debajo.
	$ErrorActionPreference = 'Continue'
	& $tmlDotnet 'tModLoader.dll' '-server' '-build' $proyecto '-unsafe' 'false'
	$ErrorActionPreference = 'Stop'
	if ($LASTEXITCODE -ne 0) { throw "Fallo el -build de tModLoader (fase 2)" }
}
finally {
	Pop-Location
}

$tmod = Join-Path $env:USERPROFILE 'Documents\My Games\Terraria\tModLoader\Mods\TerrakeepMod.tmod'
if (Test-Path $tmod) {
	$info = Get-Item $tmod
	Write-Host "OK: $($info.FullName) ($($info.Length) bytes, $($info.LastWriteTime))" -ForegroundColor Green
} else {
	throw "El -build termino sin error pero no aparecio $tmod"
}
