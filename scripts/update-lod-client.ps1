# Update the Kiota-generated Lod.Client library from the existing Swagger definition.
# Run this script from the lod-mcp-dotnet folder:
#   .\scripts\update-lod-client.ps1

$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$swaggerPath = Join-Path $repoRoot "docs\swagger.json"
$outputDir = Join-Path $repoRoot "src\lod.client\generated"
$projectPath = Join-Path $repoRoot "src\lod.client\lod.client.csproj"
$lockFilePath = Join-Path $outputDir "kiota-lock.json"

Push-Location $repoRoot
try {
    if (-not (Get-Command kiota -ErrorAction SilentlyContinue)) {
        throw "Kiota CLI is not installed. Install it with 'dotnet tool install -g microsoft.openapi.kiota' or ensure it is available in PATH."
    }

    if (-not (Test-Path $swaggerPath)) {
        throw "Swagger file not found at $swaggerPath"
    }

    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

    if (Test-Path $lockFilePath) {
        Write-Host "Updating Lod.Client generated code from the existing Kiota lock file in $outputDir"
        kiota update -o $outputDir --clean-output
    }
    else {
        Write-Host "Generating Lod.Client from $swaggerPath into $outputDir"
        kiota generate -d $swaggerPath -l CSharp -c LodClient -n Lod.Client -o $outputDir --clean-output
    }

    Write-Host "Restoring the lod.client project"
    dotnet restore $projectPath
}
finally {
    Pop-Location
}
