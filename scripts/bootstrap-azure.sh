#!/usr/bin/env bash
# One-off setup per environment: resource group, Entra app registration with a GitHub OIDC
# federated credential (no client secrets), RBAC on the resource group, and GitHub environment variables.
#
# Usage:
#   ./scripts/bootstrap-azure.sh <env> <github-owner/repo> [location]
#   ./scripts/bootstrap-azure.sh dev  my-org/servicebus-function uksouth
#   ./scripts/bootstrap-azure.sh prod my-org/servicebus-function uksouth
#
# Requires: az (logged in as someone who can create app registrations and assign roles on the subscription),
#           gh (logged in, with admin on the repo) — optional; if missing the variables are printed instead.
set -euo pipefail

ENV_NAME="${1:?environment name (dev|prod) required}"
REPO="${2:?GitHub repo (owner/name) required}"
LOCATION="${3:-uksouth}"

WORKLOAD="msgproc"
RG="rg-${WORKLOAD}-${ENV_NAME}"
APP_NAME="gh-${REPO//\//-}-${ENV_NAME}"

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

echo "==> Subscription $SUBSCRIPTION_ID, resource group $RG ($LOCATION)"
az group create --name "$RG" --location "$LOCATION" --tags workload="$WORKLOAD" environment="$ENV_NAME" -o none
RG_ID=$(az group show --name "$RG" --query id -o tsv)

echo "==> App registration $APP_NAME"
APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)
if [[ -z "$APP_ID" ]]; then
  APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
fi
SP_ID=$(az ad sp list --filter "appId eq '$APP_ID'" --query "[0].id" -o tsv)
if [[ -z "$SP_ID" ]]; then
  SP_ID=$(az ad sp create --id "$APP_ID" --query id -o tsv)
fi

echo "==> Federated credential for GitHub environment '$ENV_NAME'"
CRED_NAME="github-env-${ENV_NAME}"
if ! az ad app federated-credential show --id "$APP_ID" --federated-credential-id "$CRED_NAME" -o none 2>/dev/null; then
  az ad app federated-credential create --id "$APP_ID" --parameters "{
    \"name\": \"${CRED_NAME}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:${REPO}:environment:${ENV_NAME}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }" -o none
fi

echo "==> Role assignments on $RG"0
# echo "==> SP ID is $SP_ID"
# set -x
# Contributor: create/update resources and deploy code.
MSYS_NO_PATHCONV=1 az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "Contributor" --scope "$RG_ID" -o none
# The Bicep creates role assignments for the function's managed identity, so the pipeline needs
# to be able to write roleAssignments. RBAC Administrator is narrower than Owner/User Access Administrator.
# For tighter control, add an ABAC condition limiting which roles it may assign.
MSYS_NO_PATHCONV=1 az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" --scope "$RG_ID" -o none

echo
if command -v gh >/dev/null 2>&1; then
  echo "==> Setting GitHub environment variables on $REPO / $ENV_NAME"
  gh api --method PUT "repos/${REPO}/environments/${ENV_NAME}" >/dev/null
  gh variable set AZURE_CLIENT_ID       --repo "$REPO" --env "$ENV_NAME" --body "$APP_ID"
  gh variable set AZURE_TENANT_ID       --repo "$REPO" --env "$ENV_NAME" --body "$TENANT_ID"
  gh variable set AZURE_SUBSCRIPTION_ID --repo "$REPO" --env "$ENV_NAME" --body "$SUBSCRIPTION_ID"
  gh variable set AZURE_RESOURCE_GROUP  --repo "$REPO" --env "$ENV_NAME" --body "$RG"
else
  echo "gh CLI not found. Add these as variables on the '$ENV_NAME' environment in GitHub:"
  echo "  AZURE_CLIENT_ID=$APP_ID"
  echo "  AZURE_TENANT_ID=$TENANT_ID"
  echo "  AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID"
  echo "  AZURE_RESOURCE_GROUP=$RG"
fi

echo
echo "Done. For prod, add required reviewers under Settings > Environments > prod."
