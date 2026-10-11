output "resource_group_name" {
  description = "Environment resource group."
  value       = azurerm_resource_group.environment.name
}

output "sql_server_fqdn" {
  description = "Azure SQL logical server endpoint."
  value       = azurerm_mssql_server.environment.fully_qualified_domain_name
}

output "sql_database_name" {
  description = "Fresh database dedicated to this environment."
  value       = azurerm_mssql_database.environment.name
}

output "key_vault_name" {
  description = "Environment Key Vault name."
  value       = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  description = "Environment Key Vault URI."
  value       = azurerm_key_vault.main.vault_uri
}

output "data_protection_key_identifier" {
  description = "Versionless Key Vault key identifier used to wrap shared Data Protection keys."
  value       = azurerm_key_vault_key.data_protection.versionless_id
}

output "api_identity_name" {
  description = "API user-assigned managed identity name."
  value       = azurerm_user_assigned_identity.api.name
}

output "api_identity_client_id" {
  description = "API user-assigned managed identity client ID."
  value       = azurerm_user_assigned_identity.api.client_id
}

output "api_identity_object_id" {
  description = "API user-assigned managed identity service-principal object ID for the SQL contained user."
  value       = azurerm_user_assigned_identity.api.principal_id
}

output "worker_identity_name" {
  description = "Worker user-assigned managed identity name."
  value       = azurerm_user_assigned_identity.worker.name
}

output "worker_identity_client_id" {
  description = "Worker user-assigned managed identity client ID."
  value       = azurerm_user_assigned_identity.worker.client_id
}

output "worker_identity_object_id" {
  description = "Worker user-assigned managed identity service-principal object ID for the SQL contained user."
  value       = azurerm_user_assigned_identity.worker.principal_id
}

output "migration_identity_name" {
  description = "One-shot migration job user-assigned managed identity name."
  value       = azurerm_user_assigned_identity.migration.name
}

output "migration_identity_client_id" {
  description = "Migration job user-assigned managed identity client ID."
  value       = azurerm_user_assigned_identity.migration.client_id
}

output "migration_identity_object_id" {
  description = "Migration job user-assigned managed identity service-principal object ID for the SQL contained user."
  value       = azurerm_user_assigned_identity.migration.principal_id
}

output "api_fqdn" {
  description = "Public API hostname, or null until applications are enabled."
  value       = var.create_applications ? azurerm_container_app.api[0].ingress[0].fqdn : null
}

output "container_app_egress_ip_addresses" {
  description = "Current public egress IPs for the ACA app/job resources; use these as exact SQL firewall entries."
  value = var.create_applications ? toset(concat(
    tolist(azurerm_container_app.api[0].outbound_ip_addresses),
    local.split_mode ? tolist(azurerm_container_app.worker[0].outbound_ip_addresses) : [],
    tolist(azurerm_container_app_job.migration[0].outbound_ip_addresses)
  )) : toset([])
}

output "redis_endpoint" {
  description = "TLS-only, credential-free Managed Redis URI in split mode; null in single mode."
  value       = local.split_mode ? local.redis_endpoint : null
}
