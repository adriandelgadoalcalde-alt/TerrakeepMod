# Recompila Terrakeep.Core (repo hermano) para net8.0 y copia el DLL a lib\.
#
# lib\Terrakeep.Core.dll SI se versiona en este repo a proposito, para que TerrakeepMod
# se pueda compilar por si solo sin tener al lado el repo hermano. Este script es lo que hay
# que ejecutar cuando Core cambie.
#
# El destino net8.0 existe justamente por esto: tModLoader corre sobre .NET 8 y un DLL
# compilado para net10 no carga en su runtime.

$ErrorActionPreference = 'Stop'

$proyecto = Split-Path -Parent $PSScriptRoot
$core = 'C:\Users\adrian\Downloads\Terrasavr-Win\Terrasavr-Native\Terrakeep.Core\Terrakeep.Core.csproj'

if (-not (Test-Path $core)) {
	throw "No se encuentra el repo hermano en $core"
}

Write-Host "== Compilando Terrakeep.Core para net8.0 ==" -ForegroundColor Cyan
& dotnet build $core -f net8.0 -c Release -v m
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion de Terrakeep.Core' }

$origen = Join-Path (Split-Path -Parent $core) 'bin\Release\net8.0\Terrakeep.Core.dll'
$destino = Join-Path $proyecto 'lib\Terrakeep.Core.dll'
Copy-Item $origen $destino -Force
Write-Host "OK: copiado a $destino ($((Get-Item $destino).Length) bytes)" -ForegroundColor Green
Write-Host "Recuerda recompilar el mod despues: scripts\compilar.ps1" -ForegroundColor Yellow
