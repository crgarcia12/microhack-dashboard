#!/bin/bash
set -e

echo -e "\033[0;32mPost-provision configuration...\033[0m"

# Read environment variables from azd
eval "$(azd env get-values)"

echo -e "\033[0;32mProvisioning complete!\033[0m"
echo -e "\033[0;36m  - Resource Group: ${AZURE_RESOURCE_GROUP:-not set}\033[0m"
echo -e "\033[0;36m  - Container Registry: ${AZURE_CONTAINER_REGISTRY_ENDPOINT:-not set}\033[0m"

# Grant API managed identity access to Azure SQL Database
if [ -n "$AZURE_SQL_SERVER_NAME" ] && [ -n "$AZURE_SQL_DATABASE_NAME" ] && [ -n "$AZURE_API_IDENTITY_NAME" ] && [ -n "$AZURE_API_IDENTITY_PRINCIPAL_ID" ]; then
    echo -e "\033[0;33mGranting API managed identity '$AZURE_API_IDENTITY_NAME' access to SQL database '$AZURE_SQL_DATABASE_NAME'...\033[0m"

    TOKEN=$(az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>/dev/null || true)

    if [ -n "$TOKEN" ]; then
        SQL="IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$AZURE_API_IDENTITY_NAME')
BEGIN
    CREATE USER [$AZURE_API_IDENTITY_NAME] FROM EXTERNAL PROVIDER WITH OBJECT_ID='$AZURE_API_IDENTITY_PRINCIPAL_ID';
END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'db_datareader' AND m.name = '$AZURE_API_IDENTITY_NAME'
)
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [$AZURE_API_IDENTITY_NAME];
END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'db_datawriter' AND m.name = '$AZURE_API_IDENTITY_NAME'
)
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [$AZURE_API_IDENTITY_NAME];
END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'db_ddladmin' AND m.name = '$AZURE_API_IDENTITY_NAME'
)
BEGIN
    ALTER ROLE db_ddladmin ADD MEMBER [$AZURE_API_IDENTITY_NAME];
END;"

        echo "$SQL" | sqlcmd -S "$AZURE_SQL_SERVER_NAME.database.windows.net" -d "$AZURE_SQL_DATABASE_NAME" -G --authentication-method=ActiveDirectoryDefault -b 2>&1
        echo -e "\033[0;32m  SQL DB user '$AZURE_API_IDENTITY_NAME' granted and verified roles.\033[0m"
    else
        echo -e "\033[0;31mCould not obtain Azure SQL access token.\033[0m"
        exit 1
    fi
else
    echo -e "\033[0;31mMissing required SQL env vars (AZURE_SQL_SERVER_NAME, AZURE_SQL_DATABASE_NAME, AZURE_API_IDENTITY_NAME, AZURE_API_IDENTITY_PRINCIPAL_ID).\033[0m"
    exit 1
fi
