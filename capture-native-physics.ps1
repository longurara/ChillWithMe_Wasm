param(
    [Parameter(Mandatory=$true)][string]$PCManaged,
    [string]$Editor='C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [switch]$AllMotions
)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskProject=Join-Path $taskRoot 'UnityProject'
if(!(Test-Path -LiteralPath (Join-Path $PCManaged 'MagicaClothV2.dll'))){throw 'PCManaged must contain the original MagicaClothV2.dll.'}
if(!(Test-Path -LiteralPath (Join-Path $taskProject 'Assets/Prototype/Room.unity'))){throw 'Prepare/build the recovered project first.'}
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Close Unity before capturing the native reference.'}
$taskPreviousManaged=$env:CHILL_PC_MANAGED
$taskPreviousMotions=$env:CHILL_NATIVE_ALL_MOTIONS
$taskStarted=[DateTime]::UtcNow
try {
    $env:CHILL_PC_MANAGED=(Resolve-Path -LiteralPath $PCManaged).Path
    $env:CHILL_NATIVE_ALL_MOTIONS=if($AllMotions){'1'}else{''}
    Push-Location $taskRoot
    try {
        python -X utf8 src/graphics/prepare_native_reference.py
        if($LASTEXITCODE -ne 0){throw 'Native parameter extraction failed.'}
        Copy-Item -LiteralPath src/graphics/Runtime,src/graphics/Editor -Destination (Join-Path $taskProject 'Assets/Prototype') -Recurse -Force
        $taskBuild=Start-Process -FilePath $Editor -ArgumentList '-batchmode','-quit','-buildTarget','StandaloneWindows64','-projectPath',('"'+$taskProject+'"'),'-executeMethod','ChillNativePhysicsReference.BuildPlayer','-logFile',('"'+(Join-Path $taskRoot 'logs/unity-native-reference.log')+'"') -WindowStyle Hidden -PassThru
        $taskBuild.WaitForExit()
        if($taskBuild.ExitCode -ne 0){throw 'Native reference player build failed; see logs/unity-native-reference.log.'}
        $taskPlayer=Start-Process -FilePath (Join-Path $taskRoot '.tools/native-reference-player/Reference.exe') -ArgumentList '-batchmode','-screen-width','320','-screen-height','180','-logFile',('"'+(Join-Path $taskRoot 'logs/native-reference.log')+'"') -WorkingDirectory $taskProject -WindowStyle Hidden -PassThru
        $taskPlayer.WaitForExit()
        if($taskPlayer.ExitCode -ne 0){throw 'Native capture failed; see logs/native-reference.log.'}
        $taskResult=Get-Item -LiteralPath (Join-Path $taskProject 'Assets/Prototype/native-bone-bakes.json')
        if($taskResult.LastWriteTimeUtc -lt $taskStarted){throw 'Capture did not produce a fresh reference.'}
        Write-Host 'Native reference captured. Run build.ps1 to include it in WebGL.'
    } finally {Pop-Location}
} finally {$env:CHILL_PC_MANAGED=$taskPreviousManaged;$env:CHILL_NATIVE_ALL_MOTIONS=$taskPreviousMotions}
