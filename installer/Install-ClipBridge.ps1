param([switch]$NoStartup, [string]$SourceDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'windows-x64'))
$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\ClipBridge'
$executable = Join-Path $installDirectory 'ClipBridge.exe'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\ClipBridge.lnk'
$startup = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\ClipBridge.lnk'
if (-not (Test-Path -LiteralPath (Join-Path $SourceDirectory 'ClipBridge.exe'))) { throw "ClipBridge.exe was not found in $SourceDirectory." }
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
$stagedExecutable = Join-Path $installDirectory 'ClipBridge.exe.new'
Copy-Item -LiteralPath (Join-Path $SourceDirectory 'ClipBridge.exe') -Destination $stagedExecutable -Force
$installedProcesses = @(Get-Process -Name ClipBridge -ErrorAction SilentlyContinue | Where-Object {
    try { $_.Path -and ([IO.Path]::GetFullPath($_.Path) -eq [IO.Path]::GetFullPath($executable)) } catch { $false }
})
if ($installedProcesses.Count -gt 0) {
    $installedProcesses | Stop-Process
    $installedProcesses | Wait-Process -Timeout 10
}
Move-Item -LiteralPath $stagedExecutable -Destination $executable -Force
$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in @($startMenu, $startup)) {
    if ($NoStartup -and $shortcutPath -eq $startup) { Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue; continue }
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.Description = 'Private local clipboard bridge for Windows and Android'
    $shortcut.Save()
}
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Remove-NetFirewallRule -DisplayName 'ClipBridge (Private networks)' -ErrorAction SilentlyContinue
    Remove-NetFirewallRule -DisplayName 'ClipBridge Discovery (Private networks)' -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName 'ClipBridge (Private networks)' -Direction Inbound -Action Allow -Program $executable -Protocol TCP -LocalPort 24891 -Profile Private | Out-Null
    New-NetFirewallRule -DisplayName 'ClipBridge Discovery (Private networks)' -Direction Inbound -Action Allow -Program $executable -Protocol UDP -LocalPort 24892 -Profile Private | Out-Null
} else { Write-Warning 'Run as administrator to create Private-network firewall rules; Windows may prompt once at startup.' }
Start-Process -FilePath $executable
Write-Output "ClipBridge installed at $installDirectory"
