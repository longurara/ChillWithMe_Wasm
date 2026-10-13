$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
$taskProject = Join-Path $taskRoot 'UnityProject'
$taskSources = Join-Path $taskRoot 'src\graphics'
if (!(Test-Path -LiteralPath $taskEditor)) { throw 'Install Unity 2022.3.62f2 + WebGL Build Support first.' }
if (!(Test-Path -LiteralPath "$taskProject\Assets")) { throw 'UnityProject is missing.' }
Copy-Item -LiteralPath "$taskSources\Runtime", "$taskSources\Editor" -Destination "$taskProject\Assets\Prototype" -Recurse -Force
Copy-Item -LiteralPath "$taskSources\link.xml" -Destination "$taskProject\Assets\Prototype\link.xml" -Force
Copy-Item -LiteralPath "$taskSources\Plugins" -Destination "$taskProject\Assets\Prototype" -Recurse -Force
python "$taskSources\generate_shaders.py"
if ($LASTEXITCODE -ne 0) { throw 'Shader generation failed.' }
python "$taskSources\prepare_toon_web.py"
if ($LASTEXITCODE -ne 0) { throw 'lilToon WebGL preparation failed.' }
python "$taskSources\prepare_lighting.py"
if ($LASTEXITCODE -ne 0) { throw 'Lighting extraction failed.' }
python "$taskSources\prepare_controls.py"
if ($LASTEXITCODE -ne 0) { throw 'Desk binding extraction failed.' }
python "$taskSources\prepare_motion_scene.py"
if ($LASTEXITCODE -ne 0) { throw 'Motion binding extraction failed.' }
Copy-Item -LiteralPath "$taskSources\Runtime\ChillAnimationActions.Events.generated.cs" -Destination "$taskProject\Assets\Prototype\Runtime\ChillAnimationActions.Events.generated.cs" -Force
python "$taskSources\prepare_story.py"
if ($LASTEXITCODE -ne 0) { throw 'Story conversion failed.' }
python "$taskSources\prepare_story_scene.py"
if ($LASTEXITCODE -ne 0) { throw 'Story scene extraction failed.' }
python "$taskSources\prepare_native_ui.py"
if ($LASTEXITCODE -ne 0) { throw 'Native UI binding extraction failed.' }
python "$taskSources\prepare_features.py"
if ($LASTEXITCODE -ne 0) { throw 'Native feature policy extraction failed.' }
New-Item -ItemType Directory -Path "$taskRoot\logs" -Force | Out-Null
$taskLog = Join-Path $taskRoot 'logs\unity-standalone-build.log'
$taskUnity = Start-Process -FilePath $taskEditor -ArgumentList @('-batchmode','-quit','-buildTarget','WebGL','-projectPath',$taskProject,'-executeMethod','Hlc.ChillPrototype.ChillStandalone.Build','-logFile',$taskLog) -WindowStyle Hidden -PassThru
Write-Host "Unity PID $($taskUnity.Id). Build log: $taskLog"
$taskUnity.WaitForExit()
if ($taskUnity.ExitCode -ne 0) { throw "Unity build failed; see $taskLog" }
if (Select-String -LiteralPath $taskLog -Pattern 'Shader error|error CS\d+' -Quiet) { throw "Compilation errors; see $taskLog" }
python (Join-Path $taskRoot 'finish_preview.py')
if ($LASTEXITCODE -ne 0) { throw 'Preview page preparation failed.' }
Write-Host 'Standalone build ready. Run start-preview.ps1.'
