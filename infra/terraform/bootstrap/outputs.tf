output "bootstrap_resource_group_name" {
  description = "Resource group containing the shared bootstrap resources."
  value       = azurerm_resource_group.bootstrap.name
}

output "state_storage_account_name" {
  description = "Storage account used by the testing and production remote-state backends."
  value       = var.state_storage_account_name
}

output "state_container_name" {
  description = "Private container used by the separate testing and production state keys."
  value       = azurerm_storage_container.state.name
}

output "acr_name" {
  description = "Shared Basic Azure Container Registry name."
  value       = azurerm_container_registry.shared.name
}

output "acr_resource_group_name" {
  description = "Resource group containing the shared Azure Container Registry."
  value       = azurerm_resource_group.bootstrap.name
}

output "acr_login_server" {
  description = "Registry endpoint for image publishing and Container Apps pulls."
  value       = azurerm_container_registry.shared.login_server
}
