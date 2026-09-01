# Общие функции headless-стенда. Подключается из run-trial.ps1, не запускать напрямую.
# Пути не зашиты в E: / F: — корень проекта выводится из расположения Tools.

Set-StrictMode -Version Latest

function Get-TrialProjectRoot {
    param([string]$ProjectPath)

    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        return [System.IO.Path]::GetFullPath($ProjectPath)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

function Get-TrialProjectUnityVersion {
    param([string]$ProjectPath)

    $versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile)) {
        return $null
    }

    foreach ($line in Get-Content -LiteralPath $versionFile) {
        if ($line -match '^\s*m_EditorVersion:\s*(\S+)') {
            return $Matches[1]
        }
    }

    return $null
}

function Get-TrialRelativeUnixPath {
    param(
        [string]$FullPath,
        [string]$Root
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $full = [System.IO.Path]::GetFullPath($FullPath)
    if ($full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
    }

    return $full.Replace('\', '/')
}

function Test-TrialFingerprintExcluded {
    param([string]$RelativeUnixPath)

    $parts = $RelativeUnixPath -split '/'
    $skip = @(
        'Library', 'Temp', 'Obj', 'obj', 'Build', 'Builds', 'Logs',
        'UserSettings', 'MemoryCaptures', 'Recordings'
    )
    foreach ($part in $parts) {
        if ($skip -contains $part) { return $true }
    }

    if ($RelativeUnixPath -eq 'Trials/Runs' -or $RelativeUnixPath.StartsWith('Trials/Runs/')) {
        return $true
    }

    return $false
}

function Get-TrialFingerprintFiles {
    param([string]$ProjectPath)

    $acc = @()
    foreach ($rootName in @('Assets', 'Packages', 'ProjectSettings')) {
        $root = Join-Path $ProjectPath $rootName
        if (-not (Test-Path -LiteralPath $root)) { continue }

        Get-ChildItem -LiteralPath $root -Recurse -File -Force | ForEach-Object {
            $rel = Get-TrialRelativeUnixPath -FullPath $_.FullName -Root $ProjectPath
            if (Test-TrialFingerprintExcluded -RelativeUnixPath $rel) { return }
            $acc += [pscustomobject]@{ File = $_; Rel = $rel }
        }
    }

    return @($acc | Sort-Object -Property Rel)
}

function Get-TrialSourceFingerprint {
    param([string]$ProjectPath)

    $items = @(Get-TrialFingerprintFiles -ProjectPath $ProjectPath)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        foreach ($item in $items) {
            $bytes = [System.IO.File]::ReadAllBytes($item.File.FullName)
            $header = [System.Text.Encoding]::UTF8.GetBytes(
                $item.Rel + "`n" + $bytes.Length.ToString([System.Globalization.CultureInfo]::InvariantCulture) + "`n"
            )
            [void]$sha.TransformBlock($header, 0, $header.Length, $null, 0)
            if ($bytes.Length -gt 0) {
                [void]$sha.TransformBlock($bytes, 0, $bytes.Length, $null, 0)
            }
        }

        [void]$sha.TransformFinalBlock([byte[]]::new(0), 0, 0)
        $hash = [System.BitConverter]::ToString($sha.Hash).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }

    $relFiles = @($items | ForEach-Object { $_.Rel })
    return [pscustomobject]@{
        Fingerprint = $hash
        Algorithm   = 'SHA-256'
        FileCount   = $items.Count
        Files       = $relFiles
    }
}

function Find-GitExecutable {
    $cmd = Get-Command git -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        return $cmd.Source
    }

    foreach ($candidate in @(
            'C:\Program Files\Git\cmd\git.exe',
            'C:\Program Files\Git\bin\git.exe',
            'C:\Program Files (x86)\Git\cmd\git.exe',
            (Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe'),
            'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
        )) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    return $null
}

function Get-TrialGitInfo {
    param([string]$ProjectPath)

    $info = [pscustomobject]@{
        RepositoryPresent = $false
        GitAvailable      = $false
        Commit            = ''
        Dirty             = $false
    }

    if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath '.git'))) {
        return $info
    }

    $info.RepositoryPresent = $true
    $git = Find-GitExecutable
    if (-not $git) {
        $info.Dirty = $true
        return $info
    }

    $info.GitAvailable = $true

    Push-Location -LiteralPath $ProjectPath
    try {
        $oldEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $commit = & $git rev-parse HEAD 2>$null
        $commitCode = $LASTEXITCODE
        $ErrorActionPreference = $oldEap

        if ($commitCode -ne 0 -or [string]::IsNullOrWhiteSpace([string]$commit)) {
            $info.Dirty = $true
            return $info
        }

        $info.Commit = ([string]$commit).Trim()

        $ErrorActionPreference = 'Continue'
        $status = & $git status --porcelain
        $statusCode = $LASTEXITCODE
        $ErrorActionPreference = $oldEap

        if ($statusCode -ne 0 -or $status) {
            $info.Dirty = $true
        }
    }
    finally {
        Pop-Location
    }

    return $info
}

function Get-UnityEditorProcesses {
    Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and ($_.Path -like '*\Editor\Unity.exe')
    }
}

function Assert-UnityEditorClosedForRebuild {
    $editor = @(Get-UnityEditorProcesses)
    if ($editor.Count -gt 0) {
        $pids = ($editor | ForEach-Object { $_.Id }) -join ', '
        throw "Unity Editor запущен (PID $pids). Закройте редактор перед пересборкой. Запуск уже собранного плеера редактор не блокирует."
    }
}

function Find-UnityEditor {
    param(
        [string]$ProjectPath,
        [string]$UnityPath
    )

    $candidates = New-Object System.Collections.Generic.List[string]
    foreach ($item in @($UnityPath, $env:UNITY_EDITOR, $env:UNITY_PATH)) {
        if (-not [string]::IsNullOrWhiteSpace($item)) {
            $candidates.Add($item)
        }
    }

    $version = Get-TrialProjectUnityVersion -ProjectPath $ProjectPath
    $hubRoots = New-Object System.Collections.Generic.List[string]
    foreach ($root in @(
            (Join-Path $env:ProgramFiles 'Unity\Hub\Editor'),
            (Join-Path ${env:ProgramFiles(x86)} 'Unity\Hub\Editor'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Unity\Hub\Editor')
        )) {
        if ($root) { $hubRoots.Add($root) }
    }

    $secondaryFile = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path -LiteralPath $secondaryFile) {
        $raw = Get-Content -LiteralPath $secondaryFile -Raw
        try {
            $secondary = $raw | ConvertFrom-Json
        }
        catch {
            $secondary = $raw.Trim().Trim('"')
        }
        if (-not [string]::IsNullOrWhiteSpace($secondary)) {
            $hubRoots.Add([string]$secondary)
        }
    }

    foreach ($editorsFile in @(
            (Join-Path $env:APPDATA 'UnityHub\editors.json'),
            (Join-Path $env:APPDATA 'UnityHub\editors-v1.json'),
            (Join-Path $env:APPDATA 'UnityHub\editors-v2.json')
        )) {
        if (-not (Test-Path -LiteralPath $editorsFile)) { continue }
        try {
            $editors = Get-Content -LiteralPath $editorsFile -Raw | ConvertFrom-Json
            foreach ($entry in @($editors.PSObject.Properties)) {
                $loc = $entry.Value.location
                if ($loc) { $candidates.Add($loc) }
            }
        }
        catch {
        }
    }

    if ($version) {
        foreach ($root in $hubRoots) {
            $candidates.Add((Join-Path $root "$version\Editor\Unity.exe"))
            $candidates.Add((Join-Path $root 'Editor\Unity.exe'))
        }
    }

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (Test-Path -LiteralPath $candidate) {
            return (Get-Item -LiteralPath $candidate).FullName
        }
    }

    if ($version) {
        foreach ($root in $hubRoots) {
            if (-not (Test-Path -LiteralPath $root)) { continue }
            $hit = Get-ChildItem -LiteralPath $root -Filter 'Unity.exe' -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -like "*\$version\Editor\Unity.exe" } |
                Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }

    $hint = $version
    if (-not $hint) { $hint = 'из ProjectSettings/ProjectVersion.txt' }
    throw "Не найден Unity Editor для версии '$hint'. Задайте -UnityPath или переменную UNITY_EDITOR."
}

function Resolve-TrialPaths {
    param(
        [string]$ProjectPath,
        [string]$UnityPath,
        [string]$BuildFolder,
        [string]$TrialFolder,
        [switch]$NeedUnity
    )

    $root = Get-TrialProjectRoot -ProjectPath $ProjectPath

    $build = $BuildFolder
    if ([string]::IsNullOrWhiteSpace($build)) { $build = $env:TRIAL_BUILD_FOLDER }
    if ([string]::IsNullOrWhiteSpace($build)) { $build = Join-Path $root 'Build\Headless' }
    $build = [System.IO.Path]::GetFullPath($build)

    $trials = $TrialFolder
    if ([string]::IsNullOrWhiteSpace($trials)) { $trials = $env:TRIAL_FOLDER }
    if ([string]::IsNullOrWhiteSpace($trials)) { $trials = Join-Path $root 'Trials\Runs' }
    $trials = [System.IO.Path]::GetFullPath($trials)

    $unity = $null
    if ($NeedUnity) {
        $unity = Find-UnityEditor -ProjectPath $root -UnityPath $UnityPath
    }

    return [pscustomobject]@{
        ProjectPath     = $root
        UnityPath       = $unity
        UnityVersion    = Get-TrialProjectUnityVersion -ProjectPath $root
        BuildFolder     = $build
        TrialFolder     = $trials
        ExePath         = Join-Path $build 'BalanceTrial.exe'
        ManifestPath    = Join-Path $build 'build-manifest.json'
        BuildInfoPath   = Join-Path $build 'build-info.json'
        FilesPath       = Join-Path $build 'build-manifest.files.txt'
    }
}

function Read-TrialJsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Write-TrialBuildManifest {
    param(
        [Parameter(Mandatory = $true)]$Paths,
        $Fingerprint
    )

    $git = Get-TrialGitInfo -ProjectPath $Paths.ProjectPath
    $buildInfo = Read-TrialJsonFile -Path $Paths.BuildInfoPath

    $unityVersion = $Paths.UnityVersion
    if ($buildInfo -and $buildInfo.unityVersion) {
        $unityVersion = [string]$buildInfo.unityVersion
    }

    $buildGuid = ''
    if ($buildInfo -and $buildInfo.buildGuid) {
        $buildGuid = [string]$buildInfo.buildGuid
    }

    $buildTimeUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    if ($buildInfo -and $buildInfo.buildTimeUtc) {
        $buildTimeUtc = [string]$buildInfo.buildTimeUtc
    }

    $payload = [ordered]@{
        fingerprint        = $Fingerprint.Fingerprint
        algorithm          = $Fingerprint.Algorithm
        fileCount          = $Fingerprint.FileCount
        filesPath          = $Paths.FilesPath
        unityVersion       = $unityVersion
        unityPath          = $Paths.UnityPath
        buildTimeUtc       = $buildTimeUtc
        repositoryPresent  = [bool]$git.RepositoryPresent
        gitAvailable       = [bool]$git.GitAvailable
        gitCommit          = $git.Commit
        gitDirty           = [bool]$git.Dirty
        buildGuid          = $buildGuid
        outputPath         = $Paths.ExePath
    }

    $json = $payload | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($Paths.ManifestPath, $json)

    $list = @($Fingerprint.Files)
    [System.IO.File]::WriteAllLines($Paths.FilesPath, $list)
}

function Test-TrialBuildFreshness {
    param(
        [Parameter(Mandatory = $true)]$Paths,
        [string]$ManifestPath
    )

    $manifestFile = $ManifestPath
    if ([string]::IsNullOrWhiteSpace($manifestFile)) {
        $manifestFile = $Paths.ManifestPath
    }

    if (-not (Test-Path -LiteralPath $Paths.ExePath)) {
        throw "Плеер не собран: $($Paths.ExePath). Запустите с -Rebuild."
    }

    if (-not (Test-Path -LiteralPath $manifestFile)) {
        throw "Нет build-manifest.json рядом с плеером ($manifestFile). Запустите с -Rebuild."
    }

    $manifest = Read-TrialJsonFile -Path $manifestFile
    if (-not $manifest -or [string]::IsNullOrWhiteSpace([string]$manifest.fingerprint)) {
        throw "build-manifest.json не содержит fingerprint: $manifestFile. Запустите с -Rebuild."
    }

    $current = Get-TrialSourceFingerprint -ProjectPath $Paths.ProjectPath
    $built = [string]$manifest.fingerprint
    if ($current.Fingerprint -ne $built) {
        throw ("STALE_BUILD: исходники $($current.Fingerprint), билд $built. Нужен -Rebuild. Текущий hash исходников не подставляется в прогон как hash старого билда.")
    }

    return [pscustomobject]@{
        Fresh       = $true
        Fingerprint = $built
        Manifest    = $manifest
        Current     = $current
    }
}

function Test-ExtraHasSwitch {
    param(
        [string[]]$Extra,
        [string]$Name
    )

    if (-not $Extra) { return $false }
    return $Extra -contains $Name
}
