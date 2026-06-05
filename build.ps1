# Build script for Kirrun application
# Builds release version with icon and outputs to artifacts/release folder

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectDir "src\KillRun.App\KillRun.App.csproj"
$outputDir = Join-Path $projectDir "artifacts\release"
$iconPath = Join-Path $projectDir ".\data\emew.ico"

# Ensure output directory exists
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

Write-Host "Building Kirrun application..." -ForegroundColor Cyan

# Build the project in Release configuration
dotnet publish $projectFile `
    -c Release `
    -o $outputDir `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:AssemblyName=Kirrun `
    -p:ApplicationIcon=$iconPath `
    -p:GenerateAssemblyInfo=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:RuntimeIdentifier=win-x64

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "Output location: $outputDir" -ForegroundColor Yellow
    
    # List the generated files
    Get-ChildItem -Path $outputDir -Filter "*.exe" | ForEach-Object {
        Write-Host "  - $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)" -ForegroundColor Gray
    }
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
