[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$rootPrefix = $root.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

function Get-RepositoryRelativePath {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped the repository: $fullPath"
    }
    return $fullPath.Substring($rootPrefix.Length)
}

$allowedTopLevelDirectories = @(
    '.github', 'KartRider.Connector', 'KartRider.Data', 'KartriderLibrary',
    'Legacy', 'Properties', 'scripts', 'tests'
)
$allowedRootFiles = @(
    '.editorconfig', '.gitattributes', '.gitignore', 'CompileTime.cs',
    'CONTRIBUTING.md', 'Directory.Build.props', 'FEATURES.md', 'global.json',
    'KartRider.P5136.Server.csproj', 'LEGAL.md', 'LICENSE.md', 'NOTICE.md',
    'README.md', 'SECURITY.md', 'THIRD_PARTY_NOTICES.md'
)
$excludedSegments = @(
    '.git', 'bin', 'obj', 'artifacts', 'publish', 'dist', 'TestResults',
    '.vs', '.idea', '.vscode'
)
$forbiddenSegmentPatterns = @(
    '^(bin|obj|artifacts|publish|dist|TestResults)$',
    '^(analysis|captures|decompiled|ida|ghidra|x64dbg|clients)(?:[-_].*)?$',
    '^(Launcher_HF_5136_upstream|kartrider\.origrepo|KartRider_5136)$',
    '^stripper', '^HF_(?:.+)$', '^reverse', '^tools$'
)
$forbiddenNames = @(
    'KartRider.exe', 'KartRiderU.exe', 'KartRider.pin', 'KartRider.xml',
    'launcher.xml', 'NGClient.aes', 'Settings.json', 'profiles.json',
    'observers.json', 'connector-instances.json', 'packet-trace.log',
    '.gitmodules'
)
$forbiddenExtensions = @(
    '.exe', '.dll', '.pdb', '.rho', '.rho5', '.bml', '.pk', '.aes',
    '.ksv', '.1s', '.sg', '.dds', '.tga', '.pcap', '.pcapng', '.dmp',
    '.idb', '.id0', '.id1', '.i64', '.nam', '.til', '.asm', '.lst',
    '.map', '.mdmp', '.etl', '.rar', '.7z', '.zip', '.iso', '.vhd',
    '.vhdx', '.avhdx', '.ova', '.ovf', '.vdi', '.vmdk', '.qcow2',
    '.db', '.sqlite', '.sqlite3', '.log', '.bak', '.tmp', '.bin', '.dat',
    '.pfx', '.p12', '.pem', '.key', '.snk', '.evtx', '.png', '.jpg',
    '.jpeg', '.gif', '.webp', '.ico', '.wav', '.mp3', '.ogg', '.mp4',
    '.htm', '.html', '.mht', '.mhtml', '.pdf', '.doc', '.docx', '.chm',
    '.rtf', '.odt', '.apk', '.msi', '.msix', '.tar', '.gz', '.tgz',
    '.bz2', '.xz', '.wasm', '.reg'
)
$sensitivePatterns = [ordered]@{
    'absolute Windows path' = '(?<![A-Za-z0-9])[A-Za-z]:\\[^\s"'']+'
    'absolute home path'    = '(?<![A-Za-z0-9])/(?:home|Users)/[^/\s"'']+'
    'GitHub token'          = '(?:ghp_|github_pat_)[A-Za-z0-9_]+'
    'OpenAI token'          = 'sk-[A-Za-z0-9_-]{20,}'
    'AWS access key'        = '(?<![A-Z0-9])(?:AKIA|ASIA)[A-Z0-9]{16}(?![A-Z0-9])'
    'GitLab token'          = 'glpat-[A-Za-z0-9_-]{20,}'
    'Slack token'           = 'xox[baprs]-[A-Za-z0-9-]{10,}'
    'private key'           = '-----BEGIN [A-Z ]*PRIVATE KEY-----'
}

$violations = [System.Collections.Generic.List[string]]::new()
$gitDirectory = Join-Path $root '.git'
if (Test-Path -LiteralPath $gitDirectory) {
    $relativeFiles = @(& git -C $root ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }

    $specialEntries = @(& git -C $root ls-files --stage | Where-Object {
        $_ -match '^(120000|160000)\s'
    })
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files --stage failed.' }
    foreach ($entry in $specialEntries) {
        $violations.Add("tracked symlink or submodule is not allowed: $entry")
    }

    $files = @($relativeFiles | Where-Object { $_ } | ForEach-Object {
        $candidate = Join-Path $root $_
        if (Test-Path -LiteralPath $candidate) {
            Get-Item -LiteralPath $candidate -Force
        }
    })
}
else {
    $files = @(Get-ChildItem -LiteralPath $root -Recurse -Force | Where-Object {
        $relative = Get-RepositoryRelativePath $_.FullName
        $segments = $relative -split '[\\/]'
        $isReparsePoint = ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
        (-not $_.PSIsContainer -or $isReparsePoint) -and
            -not ($segments | Where-Object { $_ -in $excludedSegments })
    })
}

foreach ($file in $files) {
    $relative = Get-RepositoryRelativePath $file.FullName
    $segments = $relative -split '[\\/]'
    $topLevel = $segments[0]

    if ($segments.Count -eq 1) {
        if ($relative -notin $allowedRootFiles) {
            $violations.Add("unexpected repository-root file: $relative")
        }
    }
    elseif ($topLevel -notin $allowedTopLevelDirectories) {
        $violations.Add("unexpected top-level directory '$topLevel': $relative")
    }

    if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        $violations.Add("filesystem reparse point is not allowed: $relative")
        continue
    }
    if ($file.PSIsContainer) {
        $violations.Add("tracked directory or submodule is not allowed: $relative")
        continue
    }

    foreach ($segment in $segments) {
        foreach ($pattern in $forbiddenSegmentPatterns) {
            if ($segment -match $pattern) {
                $violations.Add("forbidden path segment '$segment': $relative")
                break
            }
        }
    }

    if ($file.Name -in $forbiddenNames) {
        $violations.Add("proprietary or private filename: $relative")
    }
    if ($file.Extension.ToLowerInvariant() -in $forbiddenExtensions) {
        $violations.Add("forbidden binary/data extension '$($file.Extension)': $relative")
    }
    if ($file.Length -gt 2MB) {
        $violations.Add("unexpected file larger than 2 MiB: $relative")
    }

    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    if ($bytes -contains 0) {
        $violations.Add("binary NUL byte detected: $relative")
        continue
    }
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    foreach ($entry in $sensitivePatterns.GetEnumerator()) {
        if ($text -match $entry.Value) {
            $violations.Add("$($entry.Key): $relative")
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("Source-boundary check failed:`n - " +
        (($violations | Sort-Object -Unique) -join "`n - "))
}

Write-Host "Source-boundary check passed: $($files.Count) files inspected."
