# Publishes a Release APK for StudyFlow and copies it to artifacts/publish/.
# Signing: copy Directory.Build.props.example → Directory.Build.props and set STUDYFLOW_KEYSTORE_PASS.
# Usage: .\scripts\publish-android.ps1
# Optional: .\scripts\publish-android.ps1 -Configuration Release

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root 'StudyFlow\StudyFlow.csproj'

if (-not (Test-Path $csproj)) {
    throw "Project not found: $csproj"
}

$propsPath = Join-Path $root 'Directory.Build.props'
if ($Configuration -eq 'Release' -and -not (Test-Path $propsPath)) {
    Write-Warning "Directory.Build.props not found. Release APK will not use your signing key. See Directory.Build.props.example and docs/PUBLISH.md."
}

$csprojText = Get-Content -Raw $csproj
if ($csprojText -notmatch '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>') {
    throw 'ApplicationDisplayVersion not found in StudyFlow.csproj'
}
$displayVersion = $Matches[1].Trim()
if ($csprojText -notmatch '<ApplicationVersion>([^<]+)</ApplicationVersion>') {
    throw 'ApplicationVersion not found in StudyFlow.csproj'
}
$buildNumber = $Matches[1].Trim()

Write-Host "Publishing StudyFlow $displayVersion ($buildNumber) as APK ($Configuration)..."

dotnet publish $csproj `
    -f net10.0-android `
    -c $Configuration `
    -p:AndroidPackageFormat=apk `
    -p:AndroidPackageFormats=apk
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishDir = Join-Path $root "StudyFlow\bin\$Configuration\net10.0-android\publish"
$apk = Get-ChildItem -Path $publishDir -Filter '*.apk' -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $apk) {
    # Some SDK versions leave the signed APK next to intermediate outputs.
    $fallbackDir = Join-Path $root "StudyFlow\bin\$Configuration\net10.0-android"
    $apk = Get-ChildItem -Path $fallbackDir -Filter '*.apk' -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\intermediates\\' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

if (-not $apk) {
    throw "APK not found under $publishDir"
}

$outDir = Join-Path $root 'artifacts\publish'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$destName = "StudyFlow-$displayVersion-$buildNumber.apk"
$dest = Join-Path $outDir $destName
Copy-Item -LiteralPath $apk.FullName -Destination $dest -Force

Write-Host "APK: $dest"
Write-Host "Source: $($apk.FullName)"
