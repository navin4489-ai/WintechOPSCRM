param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../private-backups'))
$ErrorActionPreference = 'Stop'
if (-not (Get-Command pg_dump -ErrorAction SilentlyContinue)) { throw 'Install PostgreSQL client tools (pg_dump) first.' }
if (-not $env:DATABASE_URL) { throw 'Set DATABASE_URL in this process only. Do not commit the value.' }
$uri = [Uri]$env:DATABASE_URL
if ($uri.AbsolutePath.Trim('/') -ne 'oro_pos') { throw 'Safety guard: this script only backs up the isolated oro_pos database, never ayushield/HRMS.' }
$credentials = $uri.UserInfo.Split(':', 2)
$names = 'PGHOST','PGPORT','PGDATABASE','PGUSER','PGPASSWORD','PGSSLMODE'
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:PGHOST = $uri.Host
    $env:PGPORT = if ($uri.Port -gt 0) { "$($uri.Port)" } else { '5432' }
    $env:PGDATABASE = 'oro_pos'
    $env:PGUSER = [Uri]::UnescapeDataString($credentials[0])
    $env:PGPASSWORD = [Uri]::UnescapeDataString($credentials[1])
    $env:PGSSLMODE = 'require'
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetFullPath($OutputDirectory)) | Out-Null
    $file = Join-Path $OutputDirectory "oro_pos-$(Get-Date -Format 'yyyyMMdd-HHmmss').dump"
    & pg_dump --format=custom --no-owner --no-acl --file=$file
    if ($LASTEXITCODE -ne 0) { throw 'Backup failed; do not treat the output file as a verified backup.' }
    Get-FileHash $file -Algorithm SHA256
    Write-Output 'Dump created. Encrypt/off-site-copy it and test pg_restore into a separate staging database before relying on it.'
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
