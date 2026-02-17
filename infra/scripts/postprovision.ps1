$ErrorActionPreference = "Stop"

Write-Host "Post-provision configuration..." -ForegroundColor Green

# Read environment variables from azd
$azdEnvOutput = azd env get-values
$envVars = @{}
foreach ($line in $azdEnvOutput) {
    if ($line -match '^([^=]+)=(.*)$') {
        $envVars[$matches[1]] = $matches[2] -replace '^"?(.*?)"?$', '$1'
    }
}

Write-Host "Provisioning complete!" -ForegroundColor Green
Write-Host "  - Resource Group: $($envVars['AZURE_RESOURCE_GROUP'])" -ForegroundColor Cyan
Write-Host "  - Container Registry: $($envVars['AZURE_CONTAINER_REGISTRY_ENDPOINT'])" -ForegroundColor Cyan

# Grant API managed identity access to Azure SQL Database
$sqlServer = $envVars['AZURE_SQL_SERVER_NAME']
$sqlDb = $envVars['AZURE_SQL_DATABASE_NAME']
$identityName = $envVars['AZURE_API_IDENTITY_NAME']
$principalId = $envVars['AZURE_API_IDENTITY_PRINCIPAL_ID']

if (-not ($sqlServer -and $sqlDb -and $identityName -and $principalId)) {
    throw "Missing required SQL env vars (AZURE_SQL_SERVER_NAME, AZURE_SQL_DATABASE_NAME, AZURE_API_IDENTITY_NAME, AZURE_API_IDENTITY_PRINCIPAL_ID)."
}

Write-Host "Granting API managed identity '$identityName' access to SQL database '$sqlDb'..." -ForegroundColor Yellow

# Get an access token for Azure SQL using the current az CLI user (the DB admin)
$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>$null
if (-not $token) {
    throw "Could not obtain Azure SQL access token from az CLI."
}

$sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$identityName')
BEGIN
    CREATE USER [$identityName] FROM EXTERNAL PROVIDER WITH OBJECT_ID='$principalId';
END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'db_datareader' AND m.name = '$identityName'
)
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [$identityName];
END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'db_datawriter' AND m.name = '$identityName'
)
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [$identityName];
END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'db_ddladmin' AND m.name = '$identityName'
)
BEGIN
    ALTER ROLE db_ddladmin ADD MEMBER [$identityName];
END;
"@

try {
    # Use .NET SqlClient (always available in PowerShell)
    Add-Type -AssemblyName System.Data
    $conn = New-Object System.Data.SqlClient.SqlConnection
    $conn.ConnectionString = "Server=tcp:$sqlServer.database.windows.net,1433;Initial Catalog=$sqlDb;Encrypt=True;TrustServerCertificate=False;"
    $conn.AccessToken = $token
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.ExecuteNonQuery() | Out-Null
    $verify = $conn.CreateCommand()
    $verify.CommandText = @"
SELECT COUNT(DISTINCT rp.name)
FROM sys.database_role_members rm
JOIN sys.database_principals rp ON rm.role_principal_id = rp.principal_id
JOIN sys.database_principals mp ON rm.member_principal_id = mp.principal_id
WHERE mp.name = '$identityName'
  AND rp.name IN ('db_datareader', 'db_datawriter', 'db_ddladmin');
"@
    $grantedRoleCount = [int]$verify.ExecuteScalar()
    $conn.Close()

    if ($grantedRoleCount -ne 3) {
        throw "Role verification failed for '$identityName'. Expected 3 roles, got $grantedRoleCount."
    }

    Write-Host "  SQL DB user '$identityName' granted and verified (db_datareader, db_datawriter, db_ddladmin)." -ForegroundColor Green
}
catch {
    throw "Automatic SQL identity grant failed: $($_.Exception.Message)"
}
