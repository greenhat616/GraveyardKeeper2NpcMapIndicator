# Builds a Release DLL and packs the Nexus archive:
#   dist/GK2.MapMarkers-<version>.zip
#     BepInEx/plugins/GK2.MapMarkers.dll
# Extracting the archive into the game folder installs the mod (same layout as GK2 Mod Framework).
param(
    [string]$GameDir = ""
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

[xml]$project = Get-Content "GK2.MapMarkers.csproj"
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Version not found in GK2.MapMarkers.csproj" }

$buildArgs = @("build", "GK2.MapMarkers.csproj", "-c", "Release", "-p:DeployToPlugins=false", "--nologo")
if ($GameDir) { $buildArgs += "-p:GameDir=$GameDir" }
dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$staging = Join-Path $PSScriptRoot "dist\staging"
$archive = Join-Path $PSScriptRoot "dist\GK2.MapMarkers-$version.zip"
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $staging "BepInEx\plugins") | Out-Null
Copy-Item "bin\Release\netstandard2.1\GK2.MapMarkers.dll" (Join-Path $staging "BepInEx\plugins")

Remove-Item $archive -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $archive
Remove-Item $staging -Recurse -Force

Write-Host "Packed $archive"
