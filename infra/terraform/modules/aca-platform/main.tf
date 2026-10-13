data "azurerm_client_config" "current" {}

data "azurerm_container_registry" "shared" {
  name                = var.acr_name
  resource_group_name = var.acr_resource_group_name
}

locals {
  base_name                           = "${var.name_prefix}-${var.unique_suffix}"
  split_mode                          = var.deployment_mode == "split"
  sqlite_mode                         = var.database_provider == "SQLite"
  sql_server_mode                     = var.database_provider == "SqlServer"
  postgresql_mode                     = var.database_provider == "PostgreSQL"
  mysql_mode                          = var.database_provider == "MySQL"
  database                            = "RefTestManagement-${var.environment}"
  managed_database                    = "reftestmanagement_${var.environment}"
  app_database_connection_secret_name = local.sql_server_mode ? "sql-connection" : "database-connection"
  postgresql_server_name              = "${local.base_name}-postgresql"
  mysql_server_name                   = "${local.base_name}-mysql"
  sqlite_storage_account_name         = "${replace(local.base_name, "-", "")}data"
  sqlite_connection_string            = "Data Source=/mnt/ref-test-data/RefTestManagement.db"
  redis_acl                           = "+@read +@write +@pubsub +@scripting +@transaction +@connection +cluster|slots +cluster|nodes ~*"
  tags                                = merge(var.tags, { Application = "RefTestManagement", Environment = var.environment, ManagedBy = "Terraform" })
  database_connection_secret_names = {
    api       = local.sql_server_mode ? "sql-api-connection" : "database-api-connection"
    worker    = local.sql_server_mode ? "sql-worker-connection" : "database-worker-connection"
    migration = local.sql_server_mode ? "sql-migration-connection" : "database-migration-connection"
  }
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
      for secret_name in concat(
        local.sqlite_mode ? [] : [local.database_connection_secret_names.api],
        [
          "auth0-client-secret",
          "auth0-management-client-secret",
          "brevo-api-key"
        ]
        ) : "api-${secret_name}" => {
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
        for secret_name in concat(
          [local.database_connection_secret_names.worker],
          [
            "auth0-management-client-secret",
            "brevo-api-key",
            "privacy-challenge-hmac"
          ]
          ) : "worker-${secret_name}" => {
          identity     = "worker"
          principal_id = azurerm_user_assigned_identity.worker.principal_id
          secret_name  = secret_name
        }
      }
    ) : {},
    local.sqlite_mode ? {} : {
      "migration-${local.database_connection_secret_names.migration}" = {
        identity     = "migration"
        principal_id = azurerm_user_assigned_identity.migration.principal_id
        secret_name  = local.database_connection_secret_names.migration
      }
    }
  ) : {}
  redis_endpoint = local.split_mode ? format(
    "rediss://%s:%d",
    azurerm_managed_redis.shared[0].hostname,
    azurerm_managed_redis.shared[0].default_database[0].port
  ) : ""
  api_database_secret_id       = "${azurerm_key_vault.main.vault_uri}secrets/${local.database_connection_secret_names.api}"
  worker_database_secret_id    = "${azurerm_key_vault.main.vault_uri}secrets/${local.database_connection_secret_names.worker}"
  migration_database_secret_id = "${azurerm_key_vault.main.vault_uri}secrets/${local.database_connection_secret_names.migration}"
  auth0_client_secret_id       = "${azurerm_key_vault.main.vault_uri}secrets/auth0-client-secret"
  auth0_management_secret_id   = "${azurerm_key_vault.main.vault_uri}secrets/auth0-management-client-secret"
  brevo_secret_id              = "${azurerm_key_vault.main.vault_uri}secrets/brevo-api-key"
  privacy_hmac_secret_id       = "${azurerm_key_vault.main.vault_uri}secrets/privacy-challenge-hmac"
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

# Preserve existing SQL Server state addresses when making the resource conditional.
moved {
  from = azurerm_mssql_server.environment
  to   = azurerm_mssql_server.environment[0]
}

moved {
  from = azurerm_mssql_database.environment
  to   = azurerm_mssql_database.environment[0]
}

resource "azurerm_mssql_server" "environment" {
  count                         = local.sql_server_mode ? 1 : 0
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
  count       = local.sql_server_mode ? 1 : 0
  name        = local.database
  server_id   = azurerm_mssql_server.environment[0].id
  sku_name    = "Basic"
  max_size_gb = 2
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  tags        = local.tags

  # Application data is not recreated or deleted by routine Terraform changes.
  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_postgresql_flexible_server" "environment" {
  count = local.postgresql_mode ? 1 : 0

  name                              = local.postgresql_server_name
  resource_group_name               = azurerm_resource_group.environment.name
  location                          = azurerm_resource_group.environment.location
  version                           = "16"
  administrator_login               = var.database_admin_login
  administrator_password_wo         = var.database_admin_password_wo
  administrator_password_wo_version = var.database_admin_password_wo_version
  sku_name                          = "B_Standard_B1ms"
  storage_mb                        = 32768
  storage_tier                      = "P4"
  auto_grow_enabled                 = false
  backup_retention_days             = 7
  geo_redundant_backup_enabled      = false
  public_network_access_enabled     = true
  authentication {
    active_directory_auth_enabled = false
    password_auth_enabled         = true
  }
  tags = local.tags

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_postgresql_flexible_server_database" "environment" {
  count = local.postgresql_mode ? 1 : 0

  name      = local.managed_database
  server_id = azurerm_postgresql_flexible_server.environment[0].id
  charset   = "UTF8"
  collation = "en_US.utf8"

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_mysql_flexible_server" "environment" {
  count = local.mysql_mode ? 1 : 0

  name                              = local.mysql_server_name
  resource_group_name               = azurerm_resource_group.environment.name
  location                          = azurerm_resource_group.environment.location
  version                           = "8.4"
  administrator_login               = var.database_admin_login
  administrator_password_wo         = var.database_admin_password_wo
  administrator_password_wo_version = var.database_admin_password_wo_version
  sku_name                          = "B_Standard_B1ms"
  backup_retention_days             = 7
  geo_redundant_backup_enabled      = false
  public_network_access             = "Enabled"
  storage {
    size_gb           = 20
    auto_grow_enabled = false
  }
  tags = local.tags

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_mysql_flexible_database" "environment" {
  count = local.mysql_mode ? 1 : 0

  name                = local.managed_database
  resource_group_name = azurerm_resource_group.environment.name
  server_name         = azurerm_mysql_flexible_server.environment[0].name
  charset             = "utf8mb4"
  collation           = "utf8mb4_unicode_ci"

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_mssql_firewall_rule" "aca_egress" {
  for_each = local.sql_server_mode ? var.sql_firewall_ip_addresses : toset([])

  name             = "aca-egress-${replace(each.value, ".", "-")}"
  server_id        = azurerm_mssql_server.environment[0].id
  start_ip_address = each.value
  end_ip_address   = each.value
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "aca_egress" {
  for_each = local.postgresql_mode ? var.sql_firewall_ip_addresses : toset([])

  name             = "aca-egress-${replace(each.value, ".", "-")}"
  server_id        = azurerm_postgresql_flexible_server.environment[0].id
  start_ip_address = each.value
  end_ip_address   = each.value
}

resource "azurerm_mysql_flexible_server_firewall_rule" "aca_egress" {
  for_each = local.mysql_mode ? var.sql_firewall_ip_addresses : toset([])

  name                = "aca-egress-${replace(each.value, ".", "-")}"
  resource_group_name = azurerm_resource_group.environment.name
  server_name         = azurerm_mysql_flexible_server.environment[0].name
  start_ip_address    = each.value
  end_ip_address      = each.value
}

resource "azurerm_storage_account" "sqlite" {
  count = local.sqlite_mode ? 1 : 0

  name                            = local.sqlite_storage_account_name
  resource_group_name             = azurerm_resource_group.environment.name
  location                        = azurerm_resource_group.environment.location
  account_tier                    = "Standard"
  account_replication_type        = "ZRS"
  account_kind                    = "StorageV2"
  https_traffic_only_enabled      = true
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = true
  tags                            = local.tags

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_storage_share" "sqlite" {
  count = local.sqlite_mode ? 1 : 0

  name               = "sqlite-data"
  storage_account_id = azurerm_storage_account.sqlite[0].id
  quota              = 5

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_container_app_environment_storage" "sqlite" {
  count = local.sqlite_mode ? 1 : 0

  name                         = "sqlite-data"
  container_app_environment_id = azurerm_container_app_environment.environment.id
  account_name                 = azurerm_storage_account.sqlite[0].name
  access_key                   = azurerm_storage_account.sqlite[0].primary_access_key
  share_name                   = azurerm_storage_share.sqlite[0].name
  access_mode                  = "ReadWrite"
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

  dynamic "secret" {
    for_each = local.sqlite_mode ? [] : [1]

    content {
      name                = local.app_database_connection_secret_name
      identity            = azurerm_user_assigned_identity.api.id
      key_vault_secret_id = local.api_database_secret_id
    }
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

      dynamic "volume_mounts" {
        for_each = local.sqlite_mode ? [1] : []

        content {
          name = "sqlite-data"
          path = "/mnt/ref-test-data"
        }
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }
      env {
        name  = "DatabaseProvider"
        value = var.database_provider
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
      dynamic "env" {
        for_each = local.sqlite_mode ? [1] : []

        content {
          name  = "ConnectionStrings__RefTestManagement"
          value = local.sqlite_connection_string
        }
      }
      dynamic "env" {
        for_each = local.sqlite_mode ? [] : [1]

        content {
          name        = "ConnectionStrings__RefTestManagement"
          secret_name = local.app_database_connection_secret_name
        }
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

    dynamic "volume" {
      for_each = local.sqlite_mode ? [1] : []

      content {
        name         = "sqlite-data"
        storage_name = azurerm_container_app_environment_storage.sqlite[0].name
        storage_type = "AzureFile"
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
    name                = local.app_database_connection_secret_name
    identity            = azurerm_user_assigned_identity.worker.id
    key_vault_secret_id = local.worker_database_secret_id
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
        value = var.database_provider
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
        secret_name = local.app_database_connection_secret_name
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

  dynamic "secret" {
    for_each = local.sqlite_mode ? [] : [1]

    content {
      name                = local.app_database_connection_secret_name
      identity            = azurerm_user_assigned_identity.migration.id
      key_vault_secret_id = local.migration_database_secret_id
    }
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

      dynamic "volume_mounts" {
        for_each = local.sqlite_mode ? [1] : []

        content {
          name = "sqlite-data"
          path = "/mnt/ref-test-data"
        }
      }

      env {
        name  = "DatabaseProvider"
        value = var.database_provider
      }
      env {
        name  = "Hosting__Provider"
        value = "Azure"
      }
      dynamic "env" {
        for_each = local.sqlite_mode ? [1] : []

        content {
          name  = "ConnectionStrings__RefTestManagement"
          value = local.sqlite_connection_string
        }
      }
      dynamic "env" {
        for_each = local.sqlite_mode ? [] : [1]

        content {
          name        = "ConnectionStrings__RefTestManagement"
          secret_name = local.app_database_connection_secret_name
        }
      }
    }

    dynamic "volume" {
      for_each = local.sqlite_mode ? [1] : []

      content {
        name         = "sqlite-data"
        storage_name = azurerm_container_app_environment_storage.sqlite[0].name
        storage_type = "AzureFile"
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
