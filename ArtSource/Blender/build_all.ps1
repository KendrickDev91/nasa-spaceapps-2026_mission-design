# Regenera todos los modelos de MISSION TRADE-OFF con Blender en modo headless.
# Uso:  powershell -ExecutionPolicy Bypass -File ArtSource/Blender/build_all.ps1 [-CharacterBlend <PERSONAJE.blend>]
param(
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
    # Opcional: .blend del personaje (avatar Avaturn con esqueleto Mixamo) -> Art/Characters/Player_Ken.fbx
    [string]$CharacterBlend = ''
)
$ErrorActionPreference = 'Stop'
$scripts = Join-Path $PSScriptRoot 'scripts'
$failed = @()
foreach ($name in 'build_rockets', 'build_launchpad', 'build_spacecraft', 'build_control_room', 'build_moon', 'build_campus', 'build_plans') {
    Write-Host "== $name"
    $out = & $Blender --background --factory-startup --python (Join-Path $scripts "$name.py") 2>&1
    $out | Select-String '\[MT\]' | ForEach-Object { Write-Host "   $($_.Line)" }
    if ($LASTEXITCODE -ne 0 -or ($out | Select-String '\[MT\] ERROR')) { $failed += $name; $out | Select-Object -Last 15 | ForEach-Object { Write-Host "   ! $_" } }
}
if ($CharacterBlend -and (Test-Path $CharacterBlend)) {
    Write-Host '== export_character'
    $out = & $Blender --background --factory-startup --python (Join-Path $scripts 'export_character.py') -- $CharacterBlend 2>&1
    $out | Select-String '\[MT\]' | ForEach-Object { Write-Host "   $($_.Line)" }
    if ($LASTEXITCODE -ne 0) { $failed += 'export_character' }
}
if ($failed.Count) { Write-Host "FALLARON: $($failed -join ', ')"; exit 1 } else { Write-Host 'OK: todos los modelos generados' }
