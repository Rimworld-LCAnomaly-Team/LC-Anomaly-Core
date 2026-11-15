param(
    [string]$RimWorldRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$HarmonyAssembliesDir,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$modsRoot = Join-Path $RimWorldRoot 'Mods'
if (-not (Test-Path -LiteralPath (Join-Path $RimWorldRoot 'UnityStubs.dll'))) {
    throw 'The selected game directory is not a RimWorld 1.7 installation.'
}
if (-not $HarmonyAssembliesDir) {
    $HarmonyAssembliesDir = [IO.Path]::GetFullPath((Join-Path $RimWorldRoot '..\..\workshop\content\294100\2009463077\1.7\Assemblies'))
}
if (-not (Test-Path -LiteralPath (Join-Path $HarmonyAssembliesDir '0Harmony.dll'))) {
    throw 'Harmony 1.7 was not found. Pass -HarmonyAssembliesDir explicitly.'
}

# Build dependencies first. A submod must never distribute another mod's DLL.
$names = @(
    'LC Anomaly Core', 'LC-Anomaly-Story', 'LC-Anomaly-Ordeals',
    'LC Anomaly Laetitia', 'LC Anomaly MeatLantern', 'LC Anomaly OneSin',
    'LC Anomaly BurrowingHeaven', 'LC SevenSin Entity',
    'LC Anomaly Portrait of Another World', 'LC-Anomaly-AnimationStudio',
    'LC Anomaly Backgrounds', 'LC Anomaly HAR Patch'
)
$xmlCount = 0
$buildCount = 0
foreach ($name in $names) {
    $root = Join-Path $modsRoot $name
    [xml]$about = Get-Content -Raw -LiteralPath (Join-Path $root 'About\About.xml')
    [xml]$folders = Get-Content -Raw -LiteralPath (Join-Path $root 'LoadFolders.xml')
    if ('1.7' -notin $about.ModMetaData.supportedVersions.li -or -not $folders.loadFolders.'v1.7') {
        throw "$name is missing its 1.7 metadata or load folders."
    }
    $scanRoots = @((Join-Path $root '1.7'))
    foreach ($entry in $folders.loadFolders.'v1.7'.li) {
        $relative = if ($entry -is [string]) { $entry } else { $entry.InnerText }
        $folder = Join-Path $root $relative
        if (-not (Test-Path -LiteralPath $folder)) { throw "Missing load folder: $folder" }
        $scanRoots += $folder
    }
    foreach ($scanRoot in ($scanRoots | Select-Object -Unique)) {
        foreach ($file in Get-ChildItem -LiteralPath $scanRoot -File -Recurse -Filter '*.xml') {
            $document = [xml]::new()
            $document.XmlResolver = $null
            $document.Load($file.FullName)
            $xmlCount++
        }
    }
    $sourceRoot = Join-Path $root 'Source\1.7'
    if (-not (Test-Path -LiteralPath $sourceRoot)) { continue }
    $project = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.csproj' | Select-Object -First 1
    if (-not $project) { throw "Missing 1.7 project: $name" }
    & dotnet build $project.FullName -c $Configuration --nologo -v:minimal /p:BuildProjectReferences=false "/p:RimWorldRoot=$RimWorldRoot" "/p:HarmonyAssembliesDir=$HarmonyAssembliesDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $name" }
    [xml]$projectData = Get-Content -Raw -LiteralPath $project.FullName
    $assembly = @($projectData.Project.PropertyGroup.AssemblyName | Where-Object { $_ })[0]
    $dlls = @(Get-ChildItem -LiteralPath (Join-Path $root '1.7\Assemblies') -Filter '*.dll')
    if ($dlls.Count -ne 1 -or $dlls[0].BaseName -ne $assembly) {
        throw "Unexpected DLLs in $name/1.7/Assemblies. Each mod must contain only its own assembly."
    }
    $buildCount++
}
Write-Host "1.7 family validation passed: $($names.Count) mods, $buildCount assemblies, $xmlCount XML files."
