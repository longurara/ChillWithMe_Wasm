param([string]$ChromeExecutable='C:\Program Files\Google\Chrome\Application\chrome.exe')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskRegistryPath='Software\Microsoft\DirectX\UserGpuPreferences'
if(!(Test-Path -LiteralPath $ChromeExecutable)){throw 'Chrome executable not found.'}
$taskKey=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($taskRegistryPath)
$taskPresent=$taskKey.GetValueNames() -contains $ChromeExecutable
$taskPrevious=$taskKey.GetValue($ChromeExecutable)
if($taskPresent){$taskKind=$taskKey.GetValueKind($ChromeExecutable)}
$taskBackup=Join-Path $taskRoot 'output/playwright/gpu-preference-backup.json'
if(Test-Path -LiteralPath $taskBackup){$taskKey.Dispose();throw 'A previous GPU preference backup needs restoration before another run.'}
@{path=$ChromeExecutable;existed=$taskPresent;value=$taskPrevious;kind=if($taskPresent){[int]$taskKind}else{1}} | ConvertTo-Json | Set-Content -LiteralPath $taskBackup
Push-Location $taskRoot
try{
    # Applies to the newly launched test process. Existing browser windows remain open.
    $taskKey.SetValue($ChromeExecutable,'GpuPreference=2;',[Microsoft.Win32.RegistryValueKind]::String)
    & 'C:/Program Files/nodejs/npx.cmd' --yes --package '@playwright/cli' playwright-cli -s=chill-gpu24 open about:blank --browser chrome
    if($LASTEXITCODE -ne 0){throw 'GPU qualification browser could not start.'}
    $taskCode=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'parity-browser-performance.js') | Select-Object -Skip 1) -join ' '
    & 'C:/Program Files/nodejs/npx.cmd' --yes --package '@playwright/cli' playwright-cli -s=chill-gpu24 run-code $taskCode | Set-Content -LiteralPath (Join-Path $taskRoot 'output/playwright/nvidia-performance.txt')
    if($LASTEXITCODE -ne 0){throw 'Performance capture failed.'}
    $taskLines=Get-Content -LiteralPath (Join-Path $taskRoot 'output/playwright/nvidia-performance.txt')
    $taskIndex=[Array]::IndexOf($taskLines,'### Result')
    if($taskIndex -lt 0){throw 'No performance result returned.'}
    $taskResult=$taskLines[$taskIndex+1] | ConvertFrom-Json
    if($taskResult.gpu -notmatch 'RTX 4070'){throw 'RTX 4070 was not confirmed; do not label this result as RTX 4070 performance.'}
    if(!$taskResult.passed){throw 'RTX 4070 was selected, but the frame-cap or frame-time qualification failed; inspect the result.'}
    $taskResult | ConvertTo-Json -Depth 6
}finally{
    try{& 'C:/Program Files/nodejs/npx.cmd' --yes --package '@playwright/cli' playwright-cli -s=chill-gpu24 close}
    finally{
        if($taskPresent){$taskKey.SetValue($ChromeExecutable,$taskPrevious,$taskKind)}else{$taskKey.DeleteValue($ChromeExecutable,$false)}
        $taskKey.Dispose();Remove-Item -LiteralPath $taskBackup;Pop-Location
    }
}
