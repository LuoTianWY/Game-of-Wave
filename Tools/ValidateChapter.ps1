param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe",
    [switch]$Graphics,
    [ValidateSet("Chapter", "Analysis", "Melody")]
    [string]$Suite = "Chapter"
)
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
if (!(Test-Path (Join-Path $project "ProjectSettings/ProjectVersion.txt"))) { throw "Not a Unity project." }
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Set -UnityPath to your Unity 2021.3.45f2c1 Editor." }
$profile = [guid]::NewGuid().ToString("N")
$validation = Join-Path $env:TEMP ("GameOfWave-ChapterValidation-" + $profile)
New-Item -ItemType Directory -Path $validation | Out-Null
foreach ($directory in @("Assets", "ProjectSettings", "Packages", "Library")) {
    $source = Join-Path $project $directory
    if (!(Test-Path $source)) { continue }
    & robocopy $source (Join-Path $validation $directory) /E /R:0 /W:0 /MT:12 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy failed for $directory." }
}
$testDirectory = Join-Path $validation "Assets/Editor"
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$runner = switch ($Suite) { "Analysis" { "AnalysisSmoke" } "Melody" { "MelodySmoke" } default { "ChapterSmoke" } }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot ($runner + ".cs")) -Destination (Join-Path $testDirectory ($runner + ".cs"))
Write-Output "Validation copy and logs: $validation"
$phases = if ($Suite -eq "Analysis") { @("analysis") } else { @("full", "restart") }
foreach ($phase in $phases) {
    $log = Join-Path $validation ("chapter-" + $phase + ".log")
    $editorArguments = @("-batchmode", "-disable-assembly-updater", "-projectPath", ('"' + $validation + '"'),
        "-executeMethod", ($runner + ".Run"), "-chapterTestProfile", $profile, "-logFile", ('"' + $log + '"'))
    if (!$Graphics) { $editorArguments += "-nographics" }
    if ($phase -eq "restart") { $editorArguments += "-chapterRestart" }
    $process = Start-Process -FilePath $UnityPath -WorkingDirectory $validation -ArgumentList $editorArguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(300000)) {
        Stop-Process -InputObject $process
        throw "Unity timed out. Inspect $log"
    }
    Select-String -LiteralPath $log -Pattern "CHAPTER .*PASS|ANALYSIS PASS|MELODY .*PASS|Exception:|error CS" | ForEach-Object Line | Write-Output
    if ($process.ExitCode -ne 0) { throw "Unity exited $($process.ExitCode). Inspect $log" }
}
Write-Output "Passed. Test saves use only isolated company $profile. Validation copy retained at $validation"
