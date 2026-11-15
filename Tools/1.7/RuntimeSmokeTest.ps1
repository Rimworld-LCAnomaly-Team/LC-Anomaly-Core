param(
    [string]$RimWorldRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [ValidateSet('English', 'ChineseSimplified')][string]$Language = 'English',
    [ValidateSet('WithoutAnomaly', 'All', 'BaseGame')][string]$DlcProfile = 'WithoutAnomaly',
    [switch]$StartupOnly,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$coreRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$artifactRoot = Join-Path $coreRoot '.runtime-test-1.7'
$testRoot = Join-Path $artifactRoot "$DlcProfile-$Language"
$injectedDll = Join-Path $coreRoot '1.7\Assemblies\RuntimeValidation.dll'
$harnessProject = Join-Path $PSScriptRoot 'RuntimeValidation\RuntimeValidation.csproj'
& dotnet build $harnessProject -c Release --nologo -v:q "/p:RimWorldRoot=$RimWorldRoot"
if ($LASTEXITCODE -ne 0) { throw 'Runtime validation harness failed to build.' }
New-Item -ItemType Directory -Path (Join-Path $testRoot 'Config') -Force | Out-Null
$ids = @(
    'brrainz.harmony', 'ludeon.rimworld', 'ludeon.rimworld.royalty', 'ludeon.rimworld.ideology',
    'ludeon.rimworld.biotech', 'ludeon.rimworld.anomaly', 'ludeon.rimworld.odyssey',
    'erdelf.humanoidalienraces', 'vanillaexpanded.backgrounds',
    'darthcy.lc.anomalycore', 'darthcy.lc.anomalystory', 'darthcy.lc.anomalyordeals',
    'darthcy.lc.laetitia', 'darthcy.lc.meatlantern', 'darthcy.lc.onesin',
    'darthcy.lc.burrowingheaven', 'darthcy.lc.sevensinentity',
    'darthcy.lc.portraitofanotherworld', 'darthcy.lc.har.patch',
    'darthcy.lc.anomalybackgrounds', 'lcanomaly.animationstudio'
)
if ($DlcProfile -eq 'WithoutAnomaly') { $ids = @($ids | Where-Object { $_ -ne 'ludeon.rimworld.anomaly' }) }
if ($DlcProfile -eq 'BaseGame') { $ids = @($ids | Where-Object { $_ -notlike 'ludeon.rimworld.*' }) }
$config = '<ModsConfigData><version>1.7.5005</version><activeMods>' +
    (($ids | ForEach-Object { "<li>$_</li>" }) -join '') + '</activeMods><knownExpansions>' +
    ((@('ludeon.rimworld', 'ludeon.rimworld.royalty', 'ludeon.rimworld.ideology', 'ludeon.rimworld.biotech',
        'ludeon.rimworld.anomaly', 'ludeon.rimworld.odyssey') | ForEach-Object { "<li>$_</li>" }) -join '') +
    '</knownExpansions></ModsConfigData>'
[IO.File]::WriteAllText((Join-Path $testRoot 'Config\ModsConfig.xml'), $config)
$prefsPath = Join-Path $testRoot 'Config\Prefs.xml'
$prefs = [xml]::new()
if (Test-Path -LiteralPath $prefsPath) { $prefs.Load($prefsPath) }
else { $prefs.LoadXml('<PrefsData />') }
$languageNode = $prefs.SelectSingleNode('/PrefsData/langFolderName')
if (-not $languageNode) {
    $languageNode = $prefs.CreateElement('langFolderName')
    $prefs.DocumentElement.AppendChild($languageNode) | Out-Null
}
$languageNode.InnerText = if ($Language -eq 'ChineseSimplified') { 'ChineseSimplified (简体中文)' } else { $Language }
$prefs.Save($prefsPath)
$previousEnabled = $env:LC17_RUNTIME_TEST
$previousMode = $env:LC17_TEST_MODE
$previousProfile = $env:LC17_DLC_PROFILE
$process = $null
try {
    Copy-Item -LiteralPath (Join-Path $artifactRoot 'Harness\RuntimeValidation.dll') -Destination $injectedDll -Force
    $env:LC17_RUNTIME_TEST = '1'
    $env:LC17_DLC_PROFILE = $DlcProfile
    $stages = if ($StartupOnly) { @('startup') } else { @('map', 'load') }
    foreach ($stage in $stages) {
        $env:LC17_TEST_MODE = $stage
        $log = Join-Path $testRoot "$Language-$stage.log"
        $stderr = Join-Path $testRoot "$Language-$stage-stderr.log"
        $process = Start-Process -FilePath (Join-Path $RimWorldRoot 'RimWorld.exe') -WorkingDirectory $RimWorldRoot `
            -ArgumentList ('-savedatafolder="' + $testRoot + '"') -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput $log -RedirectStandardError $stderr
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $marker = if ($stage -eq 'startup') { '[LC17 Validation] Def validation complete.' } elseif ($stage -eq 'map') { '[LC17 Validation] MAP PASS;' } else { '[LC17 Validation] SAVE LOAD PASS;' }
        $finished = $false
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
            $content = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw -ErrorAction SilentlyContinue } else { '' }
            if ($null -eq $content) { $content = '' }
            if ($content.Contains($marker)) { $finished = $true; break }
            if ($content.Contains('[LC17 Validation] Startup failed:')) { break }
            if ($content.Contains('[LC17 Validation] Map test failed:')) { break }
            $process.Refresh()
            if ($process.HasExited) { break }
        }
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit() }
        $process = $null
        if (-not $finished) { throw "Runtime stage '$stage' failed or timed out; inspect $log" }
        # The quick-test scenario can log this base-game initialization warning.
        # VBE's optional VELogo is also absent in the installed third-party package.
        $errors = Get-Content -LiteralPath $log | Where-Object {
            $_ -match '^\d\d:\d\d:\d\d\s+ERROR ' -and
            $_ -notmatch "Accessing TicksAbs but gameStartAbsTick is not set|Could not load Texture2D at 'UI/VELogo'"
        }
        $translationErrors = Select-String -LiteralPath $log -Pattern 'Translation data.*errors|Could not resolve cross-reference|Failed to find.*[Tt]ype'
        # The shipped 1.7 Chinese language data references 28 absent Anomaly fields even with no mods.
        # Allow only that exact independently reproduced diagnostic set; any additional translation error fails.
        if ($Language -eq 'ChineseSimplified' -and $DlcProfile -ne 'All' -and
            @($translationErrors).Count -eq 1 -and $translationErrors.Line -match 'has 28 errors') {
            $expected = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'RuntimeValidation\Vanilla17ChineseWithoutAnomaly.errors.txt')
            $actual = @(Get-Content -LiteralPath (Join-Path $testRoot 'translation-errors.txt') |
                Where-Object { $_.Trim() -ne '' -and $_ -notmatch '^==========' })
            if ($actual.Count -eq 28 -and -not (Compare-Object $expected $actual)) {
                $translationErrors = @()
                Write-Host 'Matched the 28 verified vanilla 1.7 Chinese translation warnings; no series translation errors.'
            }
        }
        if ($errors -or $translationErrors) {
            throw "Runtime validation failed:`n$(($errors + $translationErrors.Line) -join "`n")"
        }
        Write-Host "$Language $stage passed; log: $log"
    }
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit() }
    if (Test-Path -LiteralPath $injectedDll) { Remove-Item -LiteralPath $injectedDll }
    $env:LC17_RUNTIME_TEST = $previousEnabled
    $env:LC17_TEST_MODE = $previousMode
    $env:LC17_DLC_PROFILE = $previousProfile
}
