resource "azurerm_resource_group" "bootstrap" {
  name     = var.bootstrap_resource_group_name
  location = var.location
  tags     = var.tags
}

# Export only the resource ID so generated storage keys never enter Terraform state.
resource "azapi_resource" "state_storage" {
  type      = "Microsoft.Storage/storageAccounts@2025-01-01"
  name      = var.state_storage_account_name
  parent_id = azurerm_resource_group.bootstrap.id
  location  = azurerm_resource_group.bootstrap.location
  tags      = var.tags

  body = {
    kind = "StorageV2"
    properties = {
      accessTier                   = "Hot"
      allowBlobPublicAccess        = false
      allowSharedKeyAccess         = false
      defaultToOAuthAuthentication = true
      minimumTlsVersion            = "TLS1_2"
      publicNetworkAccess          = "Enabled"
      supportsHttpsTrafficOnly     = true
    }
    sku = {
      name = "Standard_LRS"
    }
  }

  response_export_values = ["id"]

  lifecycle {
    prevent_destroy = true
  }
}

resource "azapi_resource" "state_blob_service" {
  type      = "Microsoft.Storage/storageAccounts/blobServices@2025-01-01"
  name      = "default"
  parent_id = azapi_resource.state_storage.id

  body = {
    properties = {
      containerDeleteRetentionPolicy = {
        enabled = true
        days    = 14
      }
      deleteRetentionPolicy = {
        enabled = true
        days    = 14
      }
      isVersioningEnabled = true
    }
  }

  response_export_values = ["id"]
}

resource "azurerm_storage_container" "state" {
  name                  = var.state_container_name
  storage_account_id    = azapi_resource.state_storage.id
  container_access_type = "private"

  depends_on = [azapi_resource.state_blob_service]

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_role_assignment" "state_bootstrap_runner" {
  scope                = azapi_resource.state_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = data.azurerm_client_config.current.object_id
  description          = "Allow the bootstrap operator to manage Terraform state blobs."
}

resource "azurerm_role_assignment" "state_oidc_runner" {
  count = var.terraform_runner_object_id == null ? 0 : 1

  scope                            = azapi_resource.state_storage.id
  role_definition_name             = "Storage Blob Data Contributor"
  principal_id                     = var.terraform_runner_object_id
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
  description                      = "Allow the configured GitHub OIDC principal to manage Terraform state blobs."
}

resource "azurerm_container_registry" "shared" {
  name                          = var.acr_name
  resource_group_name           = azurerm_resource_group.bootstrap.name
  location                      = azurerm_resource_group.bootstrap.location
  sku                           = "Basic"
  admin_enabled                 = false
  public_network_access_enabled = true
  tags                          = var.tags

  # Basic does not support anonymous pull; admin credentials remain disabled.
  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_role_assignment" "acr_oidc_publisher" {
  count = var.acr_publisher_object_id == null ? 0 : 1

  scope                            = azurerm_container_registry.shared.id
  role_definition_name             = "AcrPush"
  principal_id                     = var.acr_publisher_object_id
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
  description                      = "Allow the configured GitHub OIDC principal to push validated OCI images."
}

resource "azurerm_consumption_budget_subscription" "combined_eur50" {
  count = var.subscription_currency == "EUR" ? 1 : 0

  name            = "ref-test-testing-production-eur50"
  subscription_id = var.subscription_id
  amount          = 50
  time_grain      = "Monthly"

  time_period {
    start_date = formatdate("YYYY-MM-01'T'00:00:00'Z'", timestamp())
  }

  filter {
    dimension {
      name = "ResourceGroupName"
      values = [
        var.bootstrap_resource_group_name,
        var.testing_resource_group_name,
        var.production_resource_group_name
      ]
    }
  }

  notification {
    enabled        = true
    threshold      = 80
    threshold_type = "Actual"
    operator       = "GreaterThanOrEqualTo"
    contact_roles  = ["Owner"]
  }

  notification {
    enabled        = true
    threshold      = 100
    threshold_type = "Actual"
    operator       = "GreaterThanOrEqualTo"
    contact_roles  = ["Owner"]
  }

  notification {
    enabled        = true
    threshold      = 100
    threshold_type = "Forecasted"
    operator       = "GreaterThanOrEqualTo"
    contact_roles  = ["Owner"]
  }
}
