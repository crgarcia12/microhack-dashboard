#!/bin/bash
set -e

echo -e "\033[0;32mPost-provision configuration...\033[0m"

# Read environment variables from azd
eval "$(azd env get-values)"

echo -e "\033[0;32mProvisioning complete!\033[0m"
echo -e "\033[0;36m  - Resource Group: ${AZURE_RESOURCE_GROUP:-not set}\033[0m"
echo -e "\033[0;36m  - Container Registry: ${AZURE_CONTAINER_REGISTRY_ENDPOINT:-not set}\033[0m"
