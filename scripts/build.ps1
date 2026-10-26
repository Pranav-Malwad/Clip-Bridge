$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

$localDotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }

& $dotnet test (Join-Path $root 'ClipBridge.slnx') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Windows tests failed.' }

$windowsOutput = Join-Path $dist 'windows-x64'
& $dotnet publish (Join-Path $root 'src\ClipBridge.Windows\ClipBridge.Windows.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:DebugType=None `
    -o $windowsOutput --nologo
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }

if (-not $env:ANDROID_HOME) { throw 'Set ANDROID_HOME to an Android SDK containing API 35.' }
$gradleJar = Join-Path $root 'android\gradle\wrapper\gradle-wrapper.jar'
Push-Location (Join-Path $root 'android')
try {
    & java -classpath $gradleJar org.gradle.wrapper.GradleWrapperMain testDebugUnitTest assembleDebug --no-daemon
    if ($LASTEXITCODE -ne 0) { throw 'Android build failed.' }
} finally {
    Pop-Location
}
Copy-Item (Join-Path $root 'android\app\build\outputs\apk\debug\app-debug.apk') `
    (Join-Path $dist 'ClipBridge-Android-debug.apk') -Force

$installerOutput = Join-Path $dist 'installer'
New-Item -ItemType Directory -Force $installerOutput | Out-Null
Copy-Item (Join-Path $root 'installer\Install-ClipBridge.ps1') $installerOutput -Force
Copy-Item (Join-Path $root 'installer\Uninstall-ClipBridge.ps1') $installerOutput -Force

$checksums = Get-ChildItem -LiteralPath $dist -File -Recurse | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    "$($hash.Hash)  $($_.FullName.Substring($dist.Length + 1))"
}
Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Value $checksums

Write-Output "Build artifacts are in $dist"
