variable "environment" {
  description = "Deployment environment."
  type        = string

  validation {
    condition     = contains(["testing", "production"], var.environment)
    error_message = "environment must be testing or production."
  }
}

variable "resource_group_name" {
  description = "New resource group dedicated to this environment."
  type        = string
}

variable "location" {
  description = "Azure region for the environment resources."
  type        = string
  default     = "Belgium Central"
}

variable "name_prefix" {
  description = "Short CAF-style application/environment prefix for Azure resource names."
  type        = string

  validation {
    condition = (
      length(var.name_prefix) >= 2 && length(var.name_prefix) <= 10 &&
      can(regex("^[a-z0-9-]+$", var.name_prefix)) &&
      !startswith(var.name_prefix, "-") &&
      !endswith(var.name_prefix, "-")
    )
    error_message = "name_prefix must be 2-10 lowercase letters, digits, or hyphens and cannot start or end with a hyphen."
  }
}

variable "unique_suffix" {
  description = "Four to eight lowercase alphanumeric characters that make globally named resources unique."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{4,8}$", var.unique_suffix))
    error_message = "unique_suffix must be 4-8 lowercase letters or digits."
  }
}

variable "acr_name" {
  description = "Name of the shared ACR created by the bootstrap root."
  type        = string
}

variable "acr_resource_group_name" {
  description = "Resource group containing the shared ACR."
  type        = string
}

variable "sql_server_name" {
  description = "Globally unique Azure SQL logical server name."
  type        = string

  validation {
    condition = (
      length(var.sql_server_name) >= 3 && length(var.sql_server_name) <= 63 &&
      can(regex("^[a-z0-9-]+$", var.sql_server_name)) &&
      !startswith(var.sql_server_name, "-") &&
      !endswith(var.sql_server_name, "-")
    )
    error_message = "sql_server_name must be 3-63 lowercase letters, digits, or hyphens and cannot start or end with a hyphen."
  }
}

variable "key_vault_name" {
  description = "Globally unique Key Vault name."
  type        = string

  validation {
    condition = (
      length(var.key_vault_name) >= 3 && length(var.key_vault_name) <= 24 &&
      can(regex("^[a-zA-Z0-9-]+$", var.key_vault_name))
    )
    error_message = "key_vault_name must be 3-24 letters, digits, or hyphens."
  }
}

variable "sql_entra_admin_login_username" {
  description = "Display name of the Entra user or group configured as the Azure SQL administrator."
  type        = string
}

variable "sql_entra_admin_object_id" {
  description = "Object ID of the Entra user or group configured as the Azure SQL administrator."
  type        = string

  validation {
    condition = can(regex(
      "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
      var.sql_entra_admin_object_id
    ))
    error_message = "sql_entra_admin_object_id must be an Entra object ID GUID."
  }
}

variable "api_image_digest" {
  description = "Already-pushed API image reference pinned by registry digest."
  type        = string

  validation {
    condition     = can(regex("^.+@sha256:[0-9a-f]{64}$", var.api_image_digest))
    error_message = "api_image_digest must end with @sha256: followed by 64 lowercase hexadecimal characters."
  }
}

variable "worker_image_digest" {
  description = "Already-pushed Worker image reference pinned by registry digest; also used by the migration job."
  type        = string

  validation {
    condition     = can(regex("^.+@sha256:[0-9a-f]{64}$", var.worker_image_digest))
    error_message = "worker_image_digest must end with @sha256: followed by 64 lowercase hexadecimal characters."
  }
}

variable "deployment_mode" {
  description = "single runs API Hosting:Mode=All without Redis; split runs API and Worker with Managed Redis."
  type        = string
  default     = "single"

  validation {
    condition = (
      contains(["single", "split"], var.deployment_mode) &&
      (var.deployment_mode != "split" || var.allow_split_mode)
    )
    error_message = "deployment_mode must be single or split; split also requires allow_split_mode=true after a revised budget or cost-validated design is approved."
  }
}

variable "allow_split_mode" {
  description = "Explicit cost gate for split mode. Keep false while the combined EUR 50 target is in force."
  type        = bool
  default     = false
}

variable "create_applications" {
  description = "Create the API, optional Worker, and manual migration job after Key Vault secrets and image digests are ready."
  type        = bool
  default     = false
}

variable "api_trusted_proxy_networks" {
  description = "Verified ACA ingress peer CIDRs trusted for X-Forwarded-For. Required when create_applications is true."
  type        = list(string)
  default     = []

  validation {
    condition = alltrue([
      for network in var.api_trusted_proxy_networks :
      can(cidrhost(network, 0)) &&
      try(tonumber(split("/", network)[1]) > 0, false)
    ])
    error_message = "api_trusted_proxy_networks must contain valid CIDRs with a non-zero prefix length; catch-all networks are not permitted."
  }

  validation {
    condition     = !var.create_applications || length(var.api_trusted_proxy_networks) > 0
    error_message = "create_applications requires verified api_trusted_proxy_networks so the API can safely process ACA forwarded headers."
  }
}

variable "api_max_replicas" {
  description = "Maximum API replicas. Must stay at one until shared/global GraphQL rate limiting is implemented."
  type        = number
  default     = 1

  validation {
    condition     = var.api_max_replicas == 1
    error_message = "api_max_replicas must remain 1 until a shared/global GraphQL rate-limit strategy is available."
  }
}

variable "sql_firewall_ip_addresses" {
  description = "Exact public IPv4 addresses allowed by the SQL firewall. Include current ACA egress and the operator during SQL user bootstrap."
  type        = set(string)
  default     = []

  validation {
    condition = alltrue([
      for address in var.sql_firewall_ip_addresses :
      can(cidrhost("${address}/32", 0)) && address != "0.0.0.0"
    ])
    error_message = "sql_firewall_ip_addresses must contain exact IPv4 addresses; 0.0.0.0 (Allow Azure services) is not permitted."
  }
}

variable "key_vault_firewall_enabled" {
  description = "Enable a Key Vault IP allowlist after ACA and Terraform-runner egress addresses are known."
  type        = bool
  default     = false

  validation {
    condition     = !var.key_vault_firewall_enabled || length(var.key_vault_allowed_ip_addresses) > 0
    error_message = "key_vault_firewall_enabled requires at least one key_vault_allowed_ip_addresses entry."
  }
}

variable "key_vault_allowed_ip_addresses" {
  description = "IPv4 addresses allowed by the Key Vault firewall when key_vault_firewall_enabled is true."
  type        = set(string)
  default     = []

  validation {
    condition = alltrue([
      for address in var.key_vault_allowed_ip_addresses :
      can(cidrhost("${address}/32", 0)) && address != "0.0.0.0"
    ])
    error_message = "key_vault_allowed_ip_addresses must contain exact IPv4 addresses; 0.0.0.0 is not permitted."
  }
}

variable "auth0_domain" {
  description = "Auth0 tenant domain without the https scheme."
  type        = string
}

variable "auth0_client_id" {
  description = "Auth0 application client ID."
  type        = string
}

variable "auth0_audience" {
  description = "Auth0 API audience."
  type        = string
}

variable "auth0_management_client_id" {
  description = "Auth0 Management API M2M client ID."
  type        = string
}

variable "email_base_url" {
  description = "Public application URL used in email links."
  type        = string
}

variable "email_from_email" {
  description = "Verified sender address used for outgoing email."
  type        = string
}

variable "email_from_name" {
  description = "Sender display name used for outgoing email."
  type        = string
}

variable "rules_questions_url" {
  description = "IHF rules/questions API URL."
  type        = string
}

variable "log_analytics_daily_quota_gb" {
  description = "Daily ingestion cap for this environment's Log Analytics workspace."
  type        = number
  default     = 1

  validation {
    condition = (
      var.log_analytics_daily_quota_gb > 0 &&
      floor(var.log_analytics_daily_quota_gb) == var.log_analytics_daily_quota_gb
    )
    error_message = "log_analytics_daily_quota_gb must be a positive whole number."
  }
}

variable "tags" {
  description = "Additional tags applied to resources in this environment."
  type        = map(string)
  default     = {}
}
