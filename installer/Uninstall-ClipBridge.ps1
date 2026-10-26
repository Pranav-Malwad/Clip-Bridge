$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\ClipBridge'
$shortcuts = @((Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\ClipBridge.lnk'), (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\ClipBridge.lnk'))
Get-Process -Name ClipBridge -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($installDirectory, [StringComparison]::OrdinalIgnoreCase) } | Stop-Process -Force
foreach ($shortcut in $shortcuts) { Remove-Item -LiteralPath $shortcut -Force -ErrorAction SilentlyContinue }
Remove-NetFirewallRule -DisplayName 'ClipBridge (Private networks)' -ErrorAction SilentlyContinue
Remove-NetFirewallRule -DisplayName 'ClipBridge Discovery (Private networks)' -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $installDirectory) { Remove-Item -LiteralPath $installDirectory -Recurse -Force }
Write-Output 'ClipBridge was uninstalled. Pairing and privacy preferences were retained.'
