# Service Bus–triggered Azure Function

A .NET 10 isolated-worker Azure Function that processes messages from an Azure Service Bus queue, with Bicep infrastructure and a GitHub Actions pipeline that deploys both.

Nothing uses connection strings or keys. The function reaches Storage, Service Bus and Application Insights through a user-assigned managed identity, and the pipeline logs in to Azure with GitHub OIDC (no client secrets).

## What gets deployed

| Resource | Notes |
|---|---|
| Function App (Flex Consumption, Linux) | .NET 10 isolated; scales on queue length |
| Service Bus namespace + `orders` queue | SAS/local auth disabled; max delivery 10, then dead-letter |
| Storage account | Host storage + deployment package container; shared-key access disabled |
| User-assigned managed identity | Granted Blob/Queue/Table data roles, Service Bus Data Receiver, Monitoring Metrics Publisher; configure it as a Dataverse application user with contact read permission |
| Application Insights + Log Analytics | Local auth disabled; Entra ID ingestion |

## Repo layout

```
.github/
  workflows/
    ci-cd.yml          build/test, Bicep lint, PR what-if, deploy dev -> prod
    deploy.yml         reusable: Bicep deployment + function code deployment
  dependabot.yml
infra/
  main.bicep           entry point (resource group scope)
  main.dev.bicepparam
  main.prod.bicepparam
  modules/             monitoring, storage, servicebus, functionapp
scripts/
  bootstrap-azure.sh   one-off: resource group, OIDC app registration, RBAC, GitHub variables
src/MessageProcessor/  the function app
tests/MessageProcessor.Tests/
```

## Message handling

`ProcessOrderFunction` turns off auto-complete and settles each message itself:

| Outcome | Action |
|---|---|
| Handler succeeds | `Complete` |
| Body isn't valid JSON / empty | `DeadLetter` (`InvalidJson` / `EmptyBody`) |
| Handler throws `InvalidOrderException` | `DeadLetter` (`ValidationFailed`): retrying won't help |
| Any other exception | Bubbles up, so the message is abandoned and retried until `maxDeliveryCount`, then dead-lettered by Service Bus |

The business logic sits in `OrderHandler`, which has no Functions or Service Bus dependencies, so it is unit-tested directly. Dataverse access uses the Dataverse SDK `ServiceClient` and SDK query/entity types. Unit tests use FakeXrmEasy with the Reciprocal Public License 1.5; review its terms before redistributing or reusing the test setup.

## Getting started

### 1. One-off Azure + GitHub setup (per environment)

Push this repo to GitHub, then, logged in with `az` (and optionally `gh`):

```bash
./scripts/bootstrap-azure.sh dev  <owner>/<repo> uksouth
./scripts/bootstrap-azure.sh prod <owner>/<repo> uksouth
```

For each environment this creates:
- resource group `rg-msgproc-<env>`
- an Entra app registration with a federated credential for `repo:<owner>/<repo>:environment:<env>`
- **Contributor** + **Role Based Access Control Administrator** on that resource group (the Bicep assigns roles to the function's identity, so the pipeline must be able to write role assignments)
- GitHub environment variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`

Then add **required reviewers** on the `prod` environment (Settings → Environments → prod) so prod deployments wait for approval.

Set the `DATAVERSE_URL` variable in each GitHub environment (`dev` and `prod`) to its Dataverse HTTPS URL, for example `https://<organization>.crm.dynamics.com`. The deployment configures this URL on the Function App. In each Dataverse environment, add the Function App's user-assigned managed identity as an application user and grant it a security role with read access to contacts and the `fullname`, `mobilephone`, and `emailaddress1` columns.

Optional: set `DEVELOPER_PRINCIPAL_ID` (your Entra object ID) on the `dev` environment to get send/receive rights on the dev queue for local debugging.

### 2. Pipeline behaviour

| Trigger | Jobs |
|---|---|
| Pull request to `main` | Build + test, Bicep lint, **what-if against dev** (posted to the job summary) |
| Push to `main` | Build + test, Bicep lint → deploy **dev** → (approval) → deploy **prod** |

Each deployment runs `az deployment group create` with `infra/main.<env>.bicepparam`, reads the function app name from the deployment outputs, then deploys the package that was built once in the `build` job with `Azure/functions-action`.

### 3. Run locally

```bash
cp src/MessageProcessor/local.settings.example.json src/MessageProcessor/local.settings.json
# set ServiceBusConnection__fullyQualifiedNamespace to the dev namespace
az login                      # DefaultAzureCredential picks this up
azurite &                     # local storage emulator
cd src/MessageProcessor && func start
```

You need the `Azure Service Bus Data Receiver` role on the queue (see `DEVELOPER_PRINCIPAL_ID` above). To send a test message, use **Service Bus Explorer** on the queue in the Azure portal (it needs the Data Sender role, which `DEVELOPER_PRINCIPAL_ID` also grants) with a body like:

```json
{ "orderId": "ord-1", "customerId": "<contact-guid>", "amount": 42.5, "createdAt": "2026-10-15T10:00:00Z" }
```

For local Dataverse access, set `Dataverse__Url` in `local.settings.json` and ensure the identity selected by `DefaultAzureCredential` is also configured as a Dataverse application user with the contact permissions above. `customerId` must contain the contact's Dataverse GUID.

## Customising

- **Topic + subscription instead of a queue:** add the topic/subscription to `servicebus.bicep` (Standard SKU or higher), scope the receiver role to the subscription, and change the trigger to `[ServiceBusTrigger("%TopicName%", "%SubscriptionName%", Connection = "ServiceBusConnection")]`.
- **Sessions:** set `requiresSession: true` on the queue and `IsSessionsEnabled = true` on the trigger.
- **Batch processing:** use `ServiceBusReceivedMessage[]` with `IsBatched = true`.
- **Networking:** Flex Consumption supports VNet integration. Add a subnet delegated to `Microsoft.App/environments` and set `virtualNetworkSubnetId` on the site, plus private endpoints for Storage and Service Bus (Service Bus Premium is needed for private endpoints).
- **More environments:** add `infra/main.<env>.bicepparam`, run the bootstrap script for it, and add another `deploy-<env>` job.
