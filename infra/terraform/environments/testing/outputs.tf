output "api_fqdn" {
  description = "Public testing API hostname, or null until applications are enabled."
  value       = module.aca_platform.api_fqdn
}

output "container_app_egress_ip_addresses" {
  description = "Current public egress IPs to add as exact server-database firewall rules."
  value       = module.aca_platform.container_app_egress_ip_addresses
}

output "database_provider" {
  description = "Selected database provider for testing."
  value       = module.aca_platform.database_provider
}

output "database_server_fqdn" {
  description = "Managed database server endpoint, or null for SQLite."
  value       = module.aca_platform.database_server_fqdn
}

output "database_name" {
  description = "Logical database name for testing."
  value       = module.aca_platform.database_name
}

output "sqlite_storage_account_name" {
  description = "Dedicated Azure Files storage account name when SQLite is selected."
  value       = module.aca_platform.sqlite_storage_account_name
}

output "sqlite_file_share_name" {
  description = "Persistent SQLite file share name when SQLite is selected."
  value       = module.aca_platform.sqlite_file_share_name
}

output "sql_server_fqdn" {
  description = "Azure SQL logical server endpoint."
  value       = module.aca_platform.sql_server_fqdn
}

output "sql_database_name" {
  description = "Testing database name."
  value       = module.aca_platform.sql_database_name
}

output "key_vault_name" {
  description = "Testing Key Vault name."
  value       = module.aca_platform.key_vault_name
}

output "api_identity_name" {
  description = "API managed identity name for the contained-user bootstrap."
  value       = module.aca_platform.api_identity_name
}

output "api_identity_client_id" {
  description = "API managed identity client ID for Key Vault and Azure SQL access."
  value       = module.aca_platform.api_identity_client_id
}

output "api_identity_object_id" {
  description = "API managed identity object ID for the contained-user bootstrap."
  value       = module.aca_platform.api_identity_object_id
}

output "worker_identity_name" {
  description = "Worker managed identity name for the contained-user bootstrap."
  value       = module.aca_platform.worker_identity_name
}

output "worker_identity_client_id" {
  description = "Worker managed identity client ID for Key Vault, Redis, and Azure SQL access."
  value       = module.aca_platform.worker_identity_client_id
}

output "worker_identity_object_id" {
  description = "Worker managed identity object ID for the contained-user bootstrap."
  value       = module.aca_platform.worker_identity_object_id
}

output "migration_identity_name" {
  description = "Migration managed identity name for the contained-user bootstrap."
  value       = module.aca_platform.migration_identity_name
}

output "migration_identity_client_id" {
  description = "Migration managed identity client ID for Key Vault and Azure SQL access."
  value       = module.aca_platform.migration_identity_client_id
}

output "migration_identity_object_id" {
  description = "Migration managed identity object ID for the contained-user bootstrap."
  value       = module.aca_platform.migration_identity_object_id
}

output "data_protection_key_identifier" {
  description = "Versionless Key Vault key identifier used by the API and Worker."
  value       = module.aca_platform.data_protection_key_identifier
}

output "redis_endpoint" {
  description = "TLS-only Redis endpoint in split mode, or null in single mode."
  value       = module.aca_platform.redis_endpoint
}
