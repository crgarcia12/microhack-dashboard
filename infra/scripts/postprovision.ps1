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

if ($sqlServer -and $sqlDb -and $identityName -and $principalId) {
    Write-Host "Granting API managed identity '$identityName' access to SQL database '$sqlDb'..." -ForegroundColor Yellow

    # Get an access token for Azure SQL using the current az CLI user (the DB admin)
    $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>$null

    if ($token) {
        $sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$identityName')
BEGIN
    CREATE USER [$identityName] FROM EXTERNAL PROVIDER WITH OBJECT_ID='$principalId';
END;
ALTER ROLE db_datareader ADD MEMBER [$identityName];
ALTER ROLE db_datawriter ADD MEMBER [$identityName];
ALTER ROLE db_ddladmin ADD MEMBER [$identityName];
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
            $conn.Close()
            Write-Host "  SQL DB user '$identityName' granted db_datareader, db_datawriter, db_ddladmin." -ForegroundColor Green
        }
        catch {
            Write-Host "  Warning: Automatic SQL user grant failed: $_" -ForegroundColor Red
            Write-Host "  Please run this SQL manually against '$sqlDb':" -ForegroundColor Yellow
            Write-Host $sql -ForegroundColor White
        }
    } else {
        Write-Host "  Warning: Could not obtain Azure SQL access token. Please grant access manually." -ForegroundColor Red
    }
} else {
    Write-Host "  Skipping SQL identity grant (missing env vars)." -ForegroundColor Yellow
}
