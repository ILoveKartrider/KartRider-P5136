[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$serverProject = Join-Path $root 'KartRider.P5136.Server.csproj'
$connectorProject = Join-Path $root 'KartRider.Connector\KartRider.Connector.csproj'
$codecSmokeProject = Join-Path $root 'tests\KartRider.P5136.CodecSmoke\KartRider.P5136.CodecSmoke.csproj'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

& (Join-Path $PSScriptRoot 'Test-SourceBoundary.ps1')

Push-Location $root
try {
    Invoke-DotNet @('restore', $serverProject)
    Invoke-DotNet @('restore', $connectorProject)
    Invoke-DotNet @('restore', $codecSmokeProject)
    Invoke-DotNet @('build', $serverProject, '-c', $Configuration, '--no-restore')
    Invoke-DotNet @('build', $connectorProject, '-c', $Configuration, '--no-restore')
    Invoke-DotNet @('run', '--project', $codecSmokeProject, '-c', $Configuration, '--no-restore')
}
finally {
    Pop-Location
}
