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

Write-Output "Build artifacts are in $dist"
