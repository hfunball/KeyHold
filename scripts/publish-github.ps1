param(
    [string]$Version = '1.11',
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$DotNetPath,
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'src\RunHold\RunHold.csproj'
$normalizedVersion = $Version.Trim().TrimStart('v')
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))

$parsedVersion = $null
if (-not [System.Version]::TryParse($normalizedVersion, [ref]$parsedVersion)) {
    throw "Version '$Version' is not a valid numeric version. Use a value like 1.11."
}

$buildVersion = [Math]::Max(0, $parsedVersion.Build)
$assemblyVersion = '{0}.{1}.{2}.0' -f $parsedVersion.Major, $parsedVersion.Minor, $buildVersion

if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    $localDotNet = Join-Path $repoRoot '.tools\dotnet\dotnet.exe'
    $DotNetPath = if (Test-Path -LiteralPath $localDotNet) { $localDotNet } else { 'dotnet' }
}

$publishRoot = Join-Path $repoRoot 'artifacts\publish'
$outputPath = Join-Path $publishRoot "RunHold-$normalizedVersion-$Runtime"
$releaseRoot = Join-Path $repoRoot 'artifacts\release'
$zipPath = Join-Path $releaseRoot "RunHold-$normalizedVersion-$Runtime-portable.zip"
$hashPath = "$zipPath.sha256.txt"

$outputFullPath = [System.IO.Path]::GetFullPath($outputPath)
if (-not $outputFullPath.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean output path outside artifacts: $outputFullPath"
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

$publishArgs = @(
    'publish',
    $projectPath,
    '--configuration',
    $Configuration,
    '--runtime',
    $Runtime,
    '--self-contained',
    'true',
    '--output',
    $outputPath,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    "-p:Version=$normalizedVersion",
    "-p:AssemblyVersion=$assemblyVersion",
    "-p:FileVersion=$assemblyVersion",
    "-p:InformationalVersion=$normalizedVersion"
)

if (-not $Restore) {
    $publishArgs += '--no-restore'
}

& $DotNetPath @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$releaseDocs = @(
    'README.md',
    'LICENSE',
    'PRIVACY.md',
    'SECURITY.md'
)

foreach ($releaseDoc in $releaseDocs) {
    $releaseDocPath = Join-Path $repoRoot $releaseDoc
    if (Test-Path -LiteralPath $releaseDocPath) {
        Copy-Item -LiteralPath $releaseDocPath -Destination (Join-Path $outputPath $releaseDoc) -Force
    }
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $hashPath) {
    Remove-Item -LiteralPath $hashPath -Force
}

Compress-Archive -Path (Join-Path $outputPath '*') -DestinationPath $zipPath -Force

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
"$($hash.Hash)  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $hashPath -Encoding ascii

Write-Host "Published $zipPath"
Write-Host "SHA256 $($hash.Hash)"
