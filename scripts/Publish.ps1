[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
$rootPrefix = $root.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

if (-not $artifactRoot.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Artifact path escaped the repository: $artifactRoot"
}

function Assert-NoReparsePoint {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped the repository: $fullPath"
    }
    if (-not (Test-Path -LiteralPath $fullPath)) { return }

    foreach ($entry in @(Get-Item -LiteralPath $fullPath -Force) +
        @(Get-ChildItem -LiteralPath $fullPath -Recurse -Force)) {
        if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to delete an artifact tree containing a reparse point: $($entry.FullName)"
        }
    }
}

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-DotNetNoticeFiles {
    $candidateRoots = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    if (-not [string]::IsNullOrWhiteSpace($dotnetCommand.Source)) {
        [void]$candidateRoots.Add((Split-Path -Parent $dotnetCommand.Source))
    }
    foreach ($environmentRoot in @($env:DOTNET_ROOT, $env:DOTNET_ROOT_X64)) {
        if (-not [string]::IsNullOrWhiteSpace($environmentRoot)) {
            [void]$candidateRoots.Add([System.IO.Path]::GetFullPath($environmentRoot))
        }
    }
    foreach ($line in @(& dotnet --list-sdks)) {
        if ($line -match '\[(?<sdkDirectory>.+)\]\s*$') {
            [void]$candidateRoots.Add((Split-Path -Parent $Matches.sdkDirectory))
        }
    }

    foreach ($candidateRoot in $candidateRoots) {
        $licensePath = Join-Path $candidateRoot 'LICENSE.txt'
        $noticePath = Join-Path $candidateRoot 'ThirdPartyNotices.txt'
        if ((Test-Path -LiteralPath $licensePath -PathType Leaf) -and
            (Test-Path -LiteralPath $noticePath -PathType Leaf)) {
            return [pscustomobject]@{ License = $licensePath; Notices = $noticePath }
        }
    }
    throw 'The active .NET SDK license and third-party notice files were not found.'
}

function Assert-FrameworkDependentPackage {
    param(
        [Parameter(Mandatory)][string]$Output,
        [Parameter(Mandatory)][string]$ExpectedExecutable
    )

    $executablePath = Join-Path $Output $ExpectedExecutable
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        throw "Expected published executable was not found: $executablePath"
    }
    if ((Get-Item -LiteralPath $executablePath).Length -gt 10MB) {
        throw "Published executable exceeds the 10 MiB limit: $executablePath"
    }

    $runtimePayloadNames = @(
        'coreclr.dll', 'clrjit.dll', 'hostfxr.dll', 'hostpolicy.dll',
        'System.Private.CoreLib.dll', 'createdump.exe', 'mscordaccore.dll',
        'mscordbi.dll'
    )
    $runtimePayloads = @(Get-ChildItem -LiteralPath $Output -Recurse -File |
        Where-Object { $_.Name -in $runtimePayloadNames })
    if ($runtimePayloads.Count -gt 0) {
        throw "Framework-dependent package contains .NET runtime payloads: $($runtimePayloads.FullName -join ', ')"
    }

    $unexpectedExecutables = @(Get-ChildItem -LiteralPath $Output -Recurse -File -Filter *.exe |
        Where-Object { $_.Name -ne $ExpectedExecutable })
    if ($unexpectedExecutables.Count -gt 0) {
        throw "Unexpected executable in publish output: $($unexpectedExecutables.FullName -join ', ')"
    }
}

& (Join-Path $PSScriptRoot 'Test-SourceBoundary.ps1')
& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release
$dotNetNotices = Get-DotNetNoticeFiles

Assert-NoReparsePoint -Path $artifactRoot
if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}

$publishRoot = Join-Path $artifactRoot 'publish'
$serverOutput = Join-Path $publishRoot 'server'
$connectorOutput = Join-Path $publishRoot 'connector'
$releaseOutput = Join-Path $artifactRoot 'release'
New-Item -ItemType Directory -Path $serverOutput, $connectorOutput, $releaseOutput -Force | Out-Null

Push-Location $root
try {
    Invoke-DotNet @(
        'publish', '.\KartRider.P5136.Server.csproj', '-c', 'Release',
        '-r', $Runtime, '--self-contained', 'false',
        '-p:PublishSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false',
        '-o', $serverOutput
    )
    Invoke-DotNet @(
        'publish', '.\KartRider.Connector\KartRider.Connector.csproj',
        '-c', 'Release', '-r', $Runtime, '--self-contained', 'false',
        '-p:PublishSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false',
        '-o', $connectorOutput
    )
}
finally {
    Pop-Location
}

Assert-FrameworkDependentPackage -Output $serverOutput -ExpectedExecutable 'KartRider.P5136.Server.exe'
Assert-FrameworkDependentPackage -Output $connectorOutput -ExpectedExecutable 'KartRider.P5136.Connector.exe'

foreach ($output in @($serverOutput, $connectorOutput)) {
    foreach ($document in @('README.md', 'LICENSE.md', 'NOTICE.md', 'THIRD_PARTY_NOTICES.md', 'LEGAL.md')) {
        Copy-Item -LiteralPath (Join-Path $root $document) -Destination $output
    }
    Copy-Item -LiteralPath $dotNetNotices.License -Destination (Join-Path $output 'DOTNET-LICENSE.txt')
    Copy-Item -LiteralPath $dotNetNotices.Notices -Destination (Join-Path $output 'DOTNET-THIRD-PARTY-NOTICES.txt')
}

$serverAsset = Join-Path $releaseOutput 'KartRider-P5136-Server-win-x64.exe'
$connectorAsset = Join-Path $releaseOutput 'KartRider-P5136-Connector-win-x64.exe'
Copy-Item -LiteralPath (Join-Path $serverOutput 'KartRider.P5136.Server.exe') -Destination $serverAsset
Copy-Item -LiteralPath (Join-Path $connectorOutput 'KartRider.P5136.Connector.exe') -Destination $connectorAsset

$serverZip = Join-Path $releaseOutput 'KartRider-P5136-Server-win-x64.zip'
$connectorZip = Join-Path $releaseOutput 'KartRider-P5136-Connector-win-x64.zip'
Compress-Archive -Path (Join-Path $serverOutput '*') -DestinationPath $serverZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $connectorOutput '*') -DestinationPath $connectorZip -CompressionLevel Optimal

$releaseFiles = @(Get-ChildItem -LiteralPath $releaseOutput -File | Sort-Object Name)
foreach ($file in $releaseFiles) {
    if ($file.Length -gt 20MB) {
        throw "Release asset exceeds the 20 MiB limit: $($file.FullName)"
    }
}

$checksumLines = @($releaseFiles | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
})
$checksumPath = Join-Path $releaseOutput 'SHA256SUMS.txt'
Set-Content -LiteralPath $checksumPath -Value $checksumLines -Encoding utf8

Write-Host "Published server:    $serverOutput"
Write-Host "Published connector: $connectorOutput"
Write-Host "Release assets:      $releaseOutput"
Write-Host 'Packaging mode:      framework-dependent (.NET 8 runtime not included)'
