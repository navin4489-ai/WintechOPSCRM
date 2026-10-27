# Rebuilds the Angular production bundle that the Docker image ships in wwwroot.
# Run this from the solution root after changing anything under web/oro-pos-web/src:
#   pwsh ./scripts/build-web.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$web = Join-Path $root 'web/oro-pos-web'

Push-Location $web
try {
    node node_modules/@angular/cli/bin/ng.js build --configuration production
    if ($LASTEXITCODE -ne 0) { throw "ng build failed with exit code $LASTEXITCODE" }

    $out = Join-Path $web 'dist/oro-pos-web/browser'
    if (-not (Test-Path (Join-Path $out 'index.html'))) { throw "Expected bundle not found at $out" }
    Write-Host "Built Angular bundle -> $out" -ForegroundColor Green
}
finally {
    Pop-Location
}
