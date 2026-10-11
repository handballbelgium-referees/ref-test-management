data "azurerm_client_config" "current" {}

data "azurerm_container_registry" "shared" {
  name                = var.acr_name
  resource_group_name = var.acr_resource_group_name
}

locals {
  base_name  = "${var.name_prefix}-${var.unique_suffix}"
  split_mode = var.deployment_mode == "split"
  database   = "RefTestManagement-${var.environment}"
  redis_acl  = "+@read +@write +@pubsub +@scripting +@transaction +@connection +cluster|slots +cluster|nodes ~*"
  tags       = merge(var.tags, { Application = "RefTestManagement", Environment = var.environment, ManagedBy = "Terraform" })
  acr_and_secret_principal_ids = merge(
    {
      api       = azurerm_user_assigned_identity.api.principal_id
      migration = azurerm_user_assigned_identity.migration.principal_id
    },
    local.split_mode ? { worker = azurerm_user_assigned_identity.worker.principal_id } : {}
  )
  data_protection_principal_ids = merge(
    { api = azurerm_user_assigned_identity.api.principal_id },
    local.split_mode ? { worker = azurerm_user_assigned_identity.worker.principal_id } : {}
  )
  # Create secret-scoped data-plane grants only after the out-of-band secrets are seeded.
  key_vault_secret_grants = var.create_applications ? merge(
    {
      for secret_name in [
        "sql-api-connection",
        "auth0-client-secret",
        "auth0-management-client-secret",
        "brevo-api-key"
        ] : "api-${secret_name}" => {
        identity     = "api"
        principal_id = azurerm_user_assigned_identity.api.principal_id
        secret_name  = secret_name
      }
    },
    local.split_mode ? merge(
      {
        "api-privacy-challenge-hmac" = {
          identity     = "api"
          principal_id = azurerm_user_assigned_identity.api.principal_id
          secret_name  = "privacy-challenge-hmac"
        }
      },
      {
        for secret_name in [
          "sql-worker-connection",
          "auth0-management-client-secret",
          "brevo-api-key",
          "privacy-challenge-hmac"
          ] : "worker-${secret_name}" => {
          identity     = "worker"
          principal_id = azurerm_user_assigned_identity.worker.principal_id
          secret_name  = secret_name
        }
      }
    ) : {},
    {
      "migration-sql-migration-connection" = {
        identity     = "migration"
        principal_id = azurerm_user_assigned_identity.migration.principal_id
        secret_name  = "sql-migration-connection"
      }
    }
  ) : {}
  redis_endpoint = local.split_mode ? format(
    "rediss://%s:%d",
    azurerm_managed_redis.shared[0].hostname,
    azurerm_managed_redis.shared[0].default_database[0].port
  ) : ""
  api_sql_secret_id          = "${azurerm_key_vault.main.vault_uri}secrets/sql-api-connection"
  worker_sql_secret_id       = "${azurerm_key_vault.main.vault_uri}secrets/sql-worker-connection"
  migration_sql_secret_id    = "${azurerm_key_vault.main.vault_uri}secrets/sql-migration-connection"
  auth0_client_secret_id     = "${azurerm_key_vault.main.vault_uri}secrets/auth0-client-secret"
  auth0_management_secret_id = "${azurerm_key_vault.main.vault_uri}secrets/auth0-management-client-secret"
  brevo_secret_id            = "${azurerm_key_vault.main.vault_uri}secrets/brevo-api-key"
  privacy_hmac_secret_id     = "${azurerm_key_vault.main.vault_uri}secrets/privacy-challenge-hmac"
}

resource "azurerm_resource_group" "environment" {
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}

# Export only the workspace ID/customer ID; never retrieve workspace shared keys into state.
resource "azapi_resource" "log_analytics" {
  type      = "Microsoft.OperationalInsights/workspaces@2025-02-01"
  name      = "${local.base_name}-logs"
  parent_id = azurerm_resource_group.environment.id
  location  = azurerm_resource_group.environment.location
  tags      = local.tags

  body = {
    properties = {
      features = {
        disableLocalAuth                            = true
        enableLogAccessUsingOnlyResourcePermissions = true
      }
      publicNetworkAccessForIngestion = "Enabled"
      publicNetworkAccessForQuery     = "Enabled"
      retentionInDays                 = 30
      sku = {
        name = "PerGB2018"
      }
      workspaceCapping = {
        dailyQuotaGb = var.log_analytics_daily_quota_gb
      }
    }
  }

  response_export_values = ["properties.customerId"]
}

resource "azurerm_container_app_environment" "environment" {
  name                  = "${local.base_name}-cae"
  resource_group_name   = azurerm_resource_group.environment.name
  location              = azurerm_resource_group.environment.location
  logs_destination      = "azure-monitor"
  public_network_access = "Enabled"
  tags                  = local.tags
}

resource "azurerm_monitor_diagnostic_setting" "container_app_environment" {
  name                           = "${local.base_name}-cae-logs"
  target_resource_id             = azurerm_container_app_environment.environment.id
  log_analytics_workspace_id     = azapi_resource.log_analytics.id
  log_analytics_destination_type = "Dedicated"

  enabled_log {
    category = "ContainerAppConsoleLogs"
  }

  enabled_log {
    category = "ContainerAppSystemLogs"
  }
}

resource "azurerm_user_assigned_identity" "api" {
  name                = "${local.base_name}-api-mi"
  resource_group_name = azurerm_resource_group.environment.name
  location            = azurerm_resource_group.environment.location
  tags                = local.tags
}

resource "azurerm_user_assigned_identity" "worker" {
  name                = "${local.base_name}-worker-mi"
  resource_group_name = azurerm_resource_group.environment.name
  location            = azurerm_resource_group.environment.location
  tags                = local.tags
}

resource "azurerm_user_assigned_identity" "migration" {
  name                = "${local.base_name}-migration-mi"
  resource_group_name = azurerm_resource_group.environment.name
  location            = azurerm_resource_group.environment.location
  tags                = local.tags
}

resource "azurerm_mssql_server" "environment" {
  name                          = var.sql_server_name
  resource_group_name           = azurerm_resource_group.environment.name
  location                      = azurerm_resource_group.environment.location
  version                       = "12.0"
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true
  tags                          = local.tags

  azuread_administrator {
    login_username              = var.sql_entra_admin_login_username
    object_id                   = var.sql_entra_admin_object_id
    tenant_id                   = data.azurerm_client_config.current.tenant_id
    azuread_authentication_only = true
  }
}

resource "azurerm_mssql_database" "environment" {
  name        = local.database
  server_id   = azurerm_mssql_server.environment.id
  sku_name    = "Basic"
  max_size_gb = 2
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  tags        = local.tags

  # Application data is not recreated or deleted by routine Terraform changes.
  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_mssql_firewall_rule" "aca_egress" {
  for_each = var.sql_firewall_ip_addresses

  name             = "aca-egress-${replace(each.value, ".", "-")}"
  server_id        = azurerm_mssql_server.environment.id
  start_ip_address = each.value
  end_ip_address   = each.value
}

resource "azurerm_key_vault" "main" {
  name                          = var.key_vault_name
  resource_group_name           = azurerm_resource_group.environment.name
  location                      = azurerm_resource_group.environment.location
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  soft_delete_retention_days    = 90
  purge_protection_enabled      = true
  public_network_access_enabled = true
  tags                          = local.tags

  # Apply an IP allowlist only after ACA and Terraform-runner egress addresses are known.
  network_acls {
    bypass         = "AzureServices"
    default_action = var.key_vault_firewall_enabled ? "Deny" : "Allow"
    ip_rules       = var.key_vault_firewall_enabled ? var.key_vault_allowed_ip_addresses : []
  }
}

resource "azurerm_key_vault_key" "data_protection" {
  name         = "data-protection"
  key_vault_id = azurerm_key_vault.main.id
  key_type     = "RSA"
  key_size     = 2048
  key_opts     = ["wrapKey", "unwrapKey"]
  tags         = local.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  for_each = local.acr_and_secret_principal_ids

  scope                            = data.azurerm_container_registry.shared.id
  role_definition_name             = "AcrPull"
  principal_id                     = each.value
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
  description                      = "Allow the ${each.key} identity to pull this environment's OCI images."
}

resource "azurerm_role_assignment" "key_vault_secrets" {
  for_each = local.key_vault_secret_grants

  scope                            = "${azurerm_key_vault.main.id}/secrets/${each.value.secret_name}"
  role_definition_name             = "Key Vault Secrets User"
  principal_id                     = each.value.principal_id
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
  description                      = "Allow the ${each.value.identity} identity to read the ${each.value.secret_name} secret."
}

resource "azurerm_role_assignment" "key_vault_crypto" {
  for_each = local.data_protection_principal_ids

  scope                            = azurerm_key_vault.main.id
  role_definition_name             = "Key Vault Crypto User"
  principal_id                     = each.value
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
  description                      = "Allow the ${each.key} identity to wrap and unwrap shared Data Protection keys."
}

resource "azurerm_managed_redis" "shared" {
  count = local.split_mode ? 1 : 0

  name                      = "${local.base_name}-redis"
  resource_group_name       = azurerm_resource_group.environment.name
  location                  = azurerm_resource_group.environment.location
  sku_name                  = "Balanced_B0"
  high_availability_enabled = false
  public_network_access     = "Enabled"
  tags                      = local.tags

  default_database {
    access_keys_authentication_enabled = false
    client_protocol                    = "Encrypted"
    clustering_policy                  = "OSSCluster"
    eviction_policy                    = "VolatileLRU"
  }
}

resource "azapi_resource" "redis_api_access" {
  count = local.split_mode ? 1 : 0

  type      = "Microsoft.Cache/redisEnterprise/databases/accessPolicyAssignments@2026-05-01-preview"
  name      = "api"
  parent_id = "${azurerm_managed_redis.shared[0].id}/databases/default"
  body = {
    properties = {
      accessPolicyName = "default"
      accessString     = local.redis_acl
      user = {
        objectId = azurerm_user_assigned_identity.api.principal_id
      }
    }
  }
  schema_validation_enabled = false
  response_export_values    = ["properties.provisioningState"]
}

resource "azapi_resource" "redis_worker_access" {
  count = local.split_mode ? 1 : 0

  type      = "Microsoft.Cache/redisEnterprise/databases/accessPolicyAssignments@2026-05-01-preview"
  name      = "worker"
  parent_id = "${azurerm_managed_redis.shared[0].id}/databases/default"
  body = {
    properties = {
      accessPolicyName = "default"
      accessString     = local.redis_acl
      user = {
        objectId = azurerm_user_assigned_identity.worker.principal_id
      }
    }
  }
  schema_validation_enabled = false
  response_export_values    = ["properties.provisioningState"]
}

resource "azurerm_container_app" "api" {
  count = var.create_applications ? 1 : 0

  name                         = "${local.base_name}-api"
  resource_group_name          = azurerm_resource_group.environment.name
  container_app_environment_id = azurerm_container_app_environment.environment.id
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
  }

  registry {
    server   = data.azurerm_container_registry.shared.login_server
    identity = azurerm_user_assigned_identity.api.id
  }

  secret {
    name                = "sql-connection"
    identity            = azurerm_user_assigned_identity.api.id
    key_vault_secret_id = local.api_sql_secret_id
  }

  secret {
    name                = "auth0-client-secret"
    identity            = azurerm_user_assigned_identity.api.id
    key_vault_secret_id = local.auth0_client_secret_id
  }

  secret {
    name                = "auth0-management-client-secret"
    identity            = azurerm_user_assigned_identity.api.id
    key_vault_secret_id = local.auth0_management_secret_id
  }

  secret {
    name                = "brevo-api-key"
    identity            = azurerm_user_assigned_identity.api.id
    key_vault_secret_id = local.brevo_secret_id
  }

  dynamic "secret" {
    for_each = local.split_mode ? [local.privacy_hmac_secret_id] : []

    content {
      name                = "privacy-challenge-hmac"
      identity            = azurerm_user_assigned_identity.api.id
      key_vault_secret_id = secret.value
    }
  }

  template {
    min_replicas = 1
    max_replicas = var.api_max_replicas

    container {
      name   = "api"
      image  = var.api_image_digest
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }
      env {
        name  = "DatabaseProvider"
        value = "SqlServer"
      }
      env {
        name  = "Hosting__Mode"
        value = local.split_mode ? "Api" : "All"
      }
      env {
        name  = "ForwardedHeadersConfiguration__TrustedClientIpHeaders__0"
        value = "X-Forwarded-For"
      }
      env {
        name  = "ForwardedHeadersConfiguration__ForwardLimit"
        value = "1"
      }
      dynamic "env" {
        for_each = var.api_trusted_proxy_networks

        content {
          name  = "ForwardedHeadersConfiguration__KnownNetworks__${env.key}"
          value = env.value
        }
      }
      env {
        name  = "Hosting__Provider"
        value = "Azure"
      }
      env {
        name  = "State__Mode"
        value = local.split_mode ? "Shared" : "Local"
      }
      env {
        name        = "ConnectionStrings__RefTestManagement"
        secret_name = "sql-connection"
      }
      env {
        name  = "Auth0__Domain"
        value = var.auth0_domain
      }
      env {
        name  = "Auth0__ClientId"
        value = var.auth0_client_id
      }
      env {
        name  = "Auth0__Audience"
        value = var.auth0_audience
      }
      env {
        name  = "Auth0__ManagementClientId"
        value = var.auth0_management_client_id
      }
      env {
        name        = "Auth0__ClientSecret"
        secret_name = "auth0-client-secret"
      }
      env {
        name        = "Auth0__ManagementClientSecret"
        secret_name = "auth0-management-client-secret"
      }
      env {
        name  = "EmailConfiguration__BaseUrl"
        value = var.email_base_url
      }
      env {
        name  = "EmailConfiguration__FromEmail"
        value = var.email_from_email
      }
      env {
        name  = "EmailConfiguration__FromName"
        value = var.email_from_name
      }
      env {
        name        = "EmailConfiguration__BrevoApiKey"
        secret_name = "brevo-api-key"
      }
      env {
        name  = "RulesQuestions__Url"
        value = var.rules_questions_url
      }
      env {
        name  = "PrivacyChallengeConfiguration__RateLimitBackend"
        value = local.split_mode ? "Redis" : "Local"
      }
      dynamic "env" {
        for_each = local.split_mode ? [1] : []

        content {
          name        = "PrivacyChallengeConfiguration__HmacSecret"
          secret_name = "privacy-challenge-hmac"
        }
      }
      env {
        name  = "RedisConfiguration__Endpoint"
        value = local.redis_endpoint
      }
      env {
        name  = "RedisConfiguration__AuthenticationMode"
        value = local.split_mode ? "Token" : "Password"
      }
      env {
        name  = "Azure__KeyVault__DataProtectionKeyIdentifier"
        value = azurerm_key_vault_key.data_protection.versionless_id
      }
      env {
        name  = "Azure__KeyVault__ManagedIdentityClientId"
        value = azurerm_user_assigned_identity.api.client_id
      }
      env {
        name  = "Azure__Redis__ManagedIdentityClientId"
        value = local.split_mode ? azurerm_user_assigned_identity.api.client_id : ""
      }
    }
  }

  ingress {
    external_enabled           = true
    allow_insecure_connections = false
    target_port                = 8080
    transport                  = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.key_vault_secrets,
    azurerm_role_assignment.key_vault_crypto,
    azapi_resource.redis_api_access,
    azapi_resource.redis_worker_access
  ]
}

resource "azurerm_container_app" "worker" {
  count = var.create_applications && local.split_mode ? 1 : 0

  name                         = "${local.base_name}-worker"
  resource_group_name          = azurerm_resource_group.environment.name
  container_app_environment_id = azurerm_container_app_environment.environment.id
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.worker.id]
  }

  registry {
    server   = data.azurerm_container_registry.shared.login_server
    identity = azurerm_user_assigned_identity.worker.id
  }

  secret {
    name                = "sql-connection"
    identity            = azurerm_user_assigned_identity.worker.id
    key_vault_secret_id = local.worker_sql_secret_id
  }

  secret {
    name                = "auth0-management-client-secret"
    identity            = azurerm_user_assigned_identity.worker.id
    key_vault_secret_id = local.auth0_management_secret_id
  }

  secret {
    name                = "brevo-api-key"
    identity            = azurerm_user_assigned_identity.worker.id
    key_vault_secret_id = local.brevo_secret_id
  }

  secret {
    name                = "privacy-challenge-hmac"
    identity            = azurerm_user_assigned_identity.worker.id
    key_vault_secret_id = local.privacy_hmac_secret_id
  }

  template {
    min_replicas = 1
    max_replicas = 1

    container {
      name   = "worker"
      image  = var.worker_image_digest
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "DatabaseProvider"
        value = "SqlServer"
      }
      env {
        name  = "Hosting__Mode"
        value = "Worker"
      }
      env {
        name  = "Hosting__Provider"
        value = "Azure"
      }
      env {
        name  = "State__Mode"
        value = "Shared"
      }
      env {
        name        = "ConnectionStrings__RefTestManagement"
        secret_name = "sql-connection"
      }
      env {
        name  = "Auth0__Domain"
        value = var.auth0_domain
      }
      env {
        name  = "Auth0__Audience"
        value = var.auth0_audience
      }
      env {
        name  = "Auth0__ManagementClientId"
        value = var.auth0_management_client_id
      }
      env {
        name        = "Auth0__ManagementClientSecret"
        secret_name = "auth0-management-client-secret"
      }
      env {
        name  = "EmailConfiguration__BaseUrl"
        value = var.email_base_url
      }
      env {
        name  = "EmailConfiguration__FromEmail"
        value = var.email_from_email
      }
      env {
        name  = "EmailConfiguration__FromName"
        value = var.email_from_name
      }
      env {
        name        = "EmailConfiguration__BrevoApiKey"
        secret_name = "brevo-api-key"
      }
      env {
        name  = "PrivacyChallengeConfiguration__RateLimitBackend"
        value = "Redis"
      }
      env {
        name        = "PrivacyChallengeConfiguration__HmacSecret"
        secret_name = "privacy-challenge-hmac"
      }
      env {
        name  = "RedisConfiguration__Endpoint"
        value = local.redis_endpoint
      }
      env {
        name  = "RedisConfiguration__AuthenticationMode"
        value = "Token"
      }
      env {
        name  = "Azure__KeyVault__DataProtectionKeyIdentifier"
        value = azurerm_key_vault_key.data_protection.versionless_id
      }
      env {
        name  = "Azure__KeyVault__ManagedIdentityClientId"
        value = azurerm_user_assigned_identity.worker.client_id
      }
      env {
        name  = "Azure__Redis__ManagedIdentityClientId"
        value = azurerm_user_assigned_identity.worker.client_id
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.key_vault_secrets,
    azurerm_role_assignment.key_vault_crypto,
    azapi_resource.redis_api_access,
    azapi_resource.redis_worker_access
  ]
}

resource "azurerm_container_app_job" "migration" {
  count = var.create_applications ? 1 : 0

  name                         = "${local.base_name}-migration"
  location                     = azurerm_resource_group.environment.location
  resource_group_name          = azurerm_resource_group.environment.name
  container_app_environment_id = azurerm_container_app_environment.environment.id
  replica_timeout_in_seconds   = 3600
  replica_retry_limit          = 0
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.migration.id]
  }

  registry {
    server   = data.azurerm_container_registry.shared.login_server
    identity = azurerm_user_assigned_identity.migration.id
  }

  secret {
    name                = "sql-connection"
    identity            = azurerm_user_assigned_identity.migration.id
    key_vault_secret_id = local.migration_sql_secret_id
  }

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  template {
    container {
      name   = "migration"
      image  = var.worker_image_digest
      cpu    = 0.25
      memory = "0.5Gi"
      args   = ["--migrate"]

      env {
        name  = "DatabaseProvider"
        value = "SqlServer"
      }
      env {
        name  = "Hosting__Provider"
        value = "Azure"
      }
      env {
        name        = "ConnectionStrings__RefTestManagement"
        secret_name = "sql-connection"
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.key_vault_secrets
  ]
}
