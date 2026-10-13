param([int]$Port = 8093)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
Write-Host "Open http://127.0.0.1:$Port/ in Chrome or Edge. Ctrl+C stops the preview."
python (Join-Path $PSScriptRoot 'serve.py') --port $Port
