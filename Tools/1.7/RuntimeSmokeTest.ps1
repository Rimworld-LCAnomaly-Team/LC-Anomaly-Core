param(
    [string]$RimWorldRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [ValidateSet('English', 'ChineseSimplified')][string]$Language = 'English',
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$coreRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testRoot = Join-Path $coreRoot '.runtime-test-1.7'
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
$config = '<ModsConfigData><version>1.7.5005</version><activeMods>' +
    (($ids | ForEach-Object { "<li>$_</li>" }) -join '') + '</activeMods></ModsConfigData>'
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
$process = $null
try {
    Copy-Item -LiteralPath (Join-Path $testRoot 'Harness\RuntimeValidation.dll') -Destination $injectedDll -Force
    $env:LC17_RUNTIME_TEST = '1'
    foreach ($stage in @('map', 'load')) {
        $env:LC17_TEST_MODE = $stage
        $log = Join-Path $testRoot "$Language-$stage.log"
        $stderr = Join-Path $testRoot "$Language-$stage-stderr.log"
        $process = Start-Process -FilePath (Join-Path $RimWorldRoot 'RimWorld.exe') -WorkingDirectory $RimWorldRoot `
            -ArgumentList ('-savedatafolder="' + $testRoot + '"') -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput $log -RedirectStandardError $stderr
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $marker = if ($stage -eq 'map') { '[LC17 Validation] MAP PASS;' } else { '[LC17 Validation] SAVE LOAD PASS;' }
        $finished = $false
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
            $content = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw -ErrorAction SilentlyContinue } else { '' }
            if ($null -eq $content) { $content = '' }
            if ($content.Contains($marker)) { $finished = $true; break }
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
        $translationErrors = Select-String -LiteralPath $log -Pattern 'Translation data.*errors'
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
}
