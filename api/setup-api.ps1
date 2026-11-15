[CmdletBinding()]
param(
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
$apiRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $apiRoot 'Ecommerce.Api\Ecommerce.Api.csproj'

Write-Host 'Checking .NET SDK...'
$null = Get-Command dotnet -ErrorAction Stop

$localDbCommand = Get-Command sqllocaldb -ErrorAction SilentlyContinue
if ($null -eq $localDbCommand) {
    throw 'SQL Server LocalDB is required. Install SQL Server Express LocalDB, then run this script again.'
}

$instances = & sqllocaldb info 2>$null
if ($instances -notcontains 'MSSQLLocalDB') {
    Write-Host 'Creating MSSQLLocalDB instance...'
    & sqllocaldb create MSSQLLocalDB | Out-Null
}

Write-Host 'Starting MSSQLLocalDB...'
& sqllocaldb start MSSQLLocalDB | Out-Null

if (-not $SkipRestore) {
    Write-Host 'Restoring API packages...'
    & dotnet restore $projectPath
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
}

Write-Host 'Building API...'
& dotnet build $projectPath --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

Write-Host 'Creating schema and seed data...'
& dotnet run --project $projectPath --no-build -- --initialize-only
if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed.' }

Write-Host ''
Write-Host 'Setup complete.' -ForegroundColor Green
Write-Host 'Run the API: dotnet run --project .\api\Ecommerce.Api\Ecommerce.Api.csproj'
Write-Host 'API URL:     http://localhost:5080'
Write-Host 'Health:      http://localhost:5080/api/health'
