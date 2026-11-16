param(
    [switch] $SkipMigration
)

$ErrorActionPreference = 'Stop'

$connectionString = [Environment]::GetEnvironmentVariable('CLEAN_CONNECT_POSTGRES_TEST')
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'Set CLEAN_CONNECT_POSTGRES_TEST to a dedicated loopback PostgreSQL test database before running this script.'
}

# Fail closed: never target a remote host, or a database that is not explicitly named for testing.
$hostMatch = [regex]::Match($connectionString, '(?i)(?:^|;)\s*Host\s*=\s*([^;]+)')
$databaseMatch = [regex]::Match($connectionString, '(?i)(?:^|;)\s*Database\s*=\s*([^;]+)')
if (-not $hostMatch.Success -or -not $databaseMatch.Success) {
    throw 'Connection string must explicitly provide Host and Database.'
}

$hostName = $hostMatch.Groups[1].Value.Trim().Trim('"')
$databaseName = $databaseMatch.Groups[1].Value.Trim().Trim('"')
if ($hostName -notin @('localhost', '127.0.0.1', '::1') -or
    $databaseName -notmatch '(?i)test' -or
    $databaseName -match '(?i)prod|live') {
    throw 'Refusing connection: PostgreSQL tests require a loopback host and a database name containing "test" and not "prod" or "live".'
}

$root = Split-Path -Parent $PSScriptRoot
$persistenceProject = Join-Path $root 'Src/Clean-Connect.Persistence/Clean-Connect.Persistence.csproj'
$testsProject = Join-Path $root 'Tests/Clean-Connect.Application.Tests/Clean-Connect.Application.Tests.csproj'
$oldDefaultConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection', 'Process')

try {
    # The design-time factory reads this variable. Keep it process-scoped and identical to the guarded target.
    [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', $connectionString, 'Process')

    if (-not $SkipMigration) {
        dotnet ef database update --project $persistenceProject --startup-project $persistenceProject --context ApplicationDbContext
        if ($LASTEXITCODE -ne 0) { throw "Migration application failed with exit code $LASTEXITCODE." }
    }

    dotnet ef migrations has-pending-model-changes --project $persistenceProject --startup-project $persistenceProject --context ApplicationDbContext
    if ($LASTEXITCODE -ne 0) { throw "EF model drift check failed with exit code $LASTEXITCODE." }

    dotnet test $testsProject --filter 'FullyQualifiedName~PostgreSql' --no-restore
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL integration tests failed with exit code $LASTEXITCODE." }
}
finally {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', $oldDefaultConnection, 'Process')
}
