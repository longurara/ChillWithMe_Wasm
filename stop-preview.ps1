$ErrorActionPreference = 'Stop'
$taskPidFile = Join-Path $PSScriptRoot 'logs\preview-server.pid'
if (!(Test-Path -LiteralPath $taskPidFile)) { Write-Host 'No tracked background preview server.'; exit }
$taskServerId = [int](Get-Content -LiteralPath $taskPidFile -Raw)
$taskServerProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $taskServerId"
$taskScript = Join-Path $PSScriptRoot 'serve.py'
if ($taskServerProcess -and $taskServerProcess.Name -eq 'python.exe' -and $taskServerProcess.CommandLine.Contains($taskScript)) {
    Stop-Process -Id $taskServerId
    Write-Host 'Standalone preview stopped.'
} elseif ($taskServerProcess) { throw 'PID belongs to another process; left it running.' }
Remove-Item -LiteralPath $taskPidFile
