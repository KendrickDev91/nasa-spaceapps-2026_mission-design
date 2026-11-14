# Regenera todos los modelos de MISSION TRADE-OFF con Blender en modo headless.
# Uso:  powershell -ExecutionPolicy Bypass -File ArtSource/Blender/build_all.ps1
param(
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
)
$ErrorActionPreference = 'Stop'
$scripts = Join-Path $PSScriptRoot 'scripts'
$failed = @()
foreach ($name in 'build_rockets', 'build_launchpad', 'build_spacecraft', 'build_control_room', 'build_moon') {
    Write-Host "== $name"
    $out = & $Blender --background --factory-startup --python (Join-Path $scripts "$name.py") 2>&1
    $out | Select-String '\[MT\]' | ForEach-Object { Write-Host "   $($_.Line)" }
    if ($LASTEXITCODE -ne 0 -or ($out | Select-String '\[MT\] ERROR')) { $failed += $name; $out | Select-Object -Last 15 | ForEach-Object { Write-Host "   ! $_" } }
}
if ($failed.Count) { Write-Host "FALLARON: $($failed -join ', ')"; exit 1 } else { Write-Host 'OK: todos los modelos generados' }
