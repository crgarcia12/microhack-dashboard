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
$identityClientId = $envVars['AZURE_API_IDENTITY_CLIENT_ID']

if ($sqlServer -and $sqlDb -and $identityName -and $identityClientId) {
    Write-Host "Granting API managed identity '$identityName' access to SQL database '$sqlDb'..." -ForegroundColor Yellow

    # Get an access token for Azure SQL using the current az CLI user (the DB admin)
    $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>$null

    if ($token) {
        # SQL to create the user from the managed identity and grant read/write/ddl
        $sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$identityName')
BEGIN
    CREATE USER [$identityName] FROM EXTERNAL PROVIDER WITH OBJECT_ID='$($envVars['AZURE_API_IDENTITY_PRINCIPAL_ID'])';
END;
ALTER ROLE db_datareader ADD MEMBER [$identityName];
ALTER ROLE db_datawriter ADD MEMBER [$identityName];
ALTER ROLE db_ddladmin ADD MEMBER [$identityName];
"@
        try {
            Invoke-Sqlcmd -ServerInstance "$sqlServer.database.windows.net" `
                -Database $sqlDb `
                -AccessToken $token `
                -Query $sql `
                -ErrorAction Stop
            Write-Host "  SQL DB user '$identityName' granted db_datareader, db_datawriter, db_ddladmin." -ForegroundColor Green
        }
        catch {
            Write-Host "  Warning: Could not grant SQL access via Invoke-Sqlcmd. Trying sqlcmd..." -ForegroundColor Yellow
            # Fallback: use sqlcmd CLI
            $sql | sqlcmd -S "$sqlServer.database.windows.net" -d $sqlDb -G --authentication-method=ActiveDirectoryDefault 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  SQL DB user '$identityName' granted access via sqlcmd." -ForegroundColor Green
            } else {
                Write-Host "  Warning: Automatic SQL user grant failed. Please run this SQL manually against '$sqlDb':" -ForegroundColor Red
                Write-Host $sql -ForegroundColor White
            }
        }
    } else {
        Write-Host "  Warning: Could not obtain Azure SQL access token. Please grant access manually." -ForegroundColor Red
        Write-Host "  Run this SQL as the DB admin:" -ForegroundColor Yellow
        Write-Host "  CREATE USER [$identityName] FROM EXTERNAL PROVIDER WITH OBJECT_ID='$($envVars['AZURE_API_IDENTITY_PRINCIPAL_ID'])';" -ForegroundColor White
        Write-Host "  ALTER ROLE db_datareader ADD MEMBER [$identityName];" -ForegroundColor White
        Write-Host "  ALTER ROLE db_datawriter ADD MEMBER [$identityName];" -ForegroundColor White
        Write-Host "  ALTER ROLE db_ddladmin ADD MEMBER [$identityName];" -ForegroundColor White
    }
} else {
    Write-Host "  Skipping SQL identity grant (missing env vars)." -ForegroundColor Yellow
}
