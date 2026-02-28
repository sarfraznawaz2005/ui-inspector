# Build script for UI Inspector
param(
    [ValidateSet("framework-dependent", "self-contained")]
    [string]$Mode = "self-contained"
)

$ErrorActionPreference = "Stop"

Write-Host "Building UI Inspector ($Mode)..." -ForegroundColor Cyan

if ($Mode -eq "self-contained") {
    dotnet publish -c Release -r win-x64 `
        /p:PublishSingleFile=true `
        /p:SelfContained=true `
        /p:PublishingBinary=true
} else {
    dotnet publish -c Release -r win-x64 `
        /p:PublishSingleFile=true `
        /p:SelfContained=false `
        /p:PublishingBinary=true
}

if ($LASTEXITCODE -eq 0) {
    $outputDir = "bin/Release/net8.0-windows/win-x64/publish"
    $exe = Get-ChildItem "$outputDir/UIInspector.exe" -ErrorAction SilentlyContinue
    if ($exe) {
        $sizeMB = [math]::Round($exe.Length / 1MB, 1)
        Write-Host "Build successful! Output: $($exe.FullName) ($sizeMB MB)" -ForegroundColor Green
    }
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
