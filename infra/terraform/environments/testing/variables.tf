variable "subscription_id" {
  description = "Subscription that contains this environment."
  type        = string
}

variable "resource_group_name" {
  description = "New resource group dedicated to testing."
  type        = string
}

variable "location" {
  description = "Azure region for the environment."
  type        = string
  default     = "Belgium Central"
}

variable "name_prefix" {
  description = "Short CAF-style prefix for testing resource names."
  type        = string
  default     = "rtm-test"
}

variable "unique_suffix" {
  description = "Four to eight lowercase alphanumeric characters for globally unique names."
  type        = string
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
}

variable "key_vault_name" {
  description = "Globally unique Key Vault name."
  type        = string
}

variable "sql_entra_admin_login_username" {
  description = "Display name of the Entra user or group configured as the SQL administrator."
  type        = string
}

variable "sql_entra_admin_object_id" {
  description = "Object ID of the Entra user or group configured as the SQL administrator."
  type        = string
}

variable "api_image_digest" {
  description = "Already-pushed API image reference pinned by registry digest."
  type        = string
}

variable "worker_image_digest" {
  description = "Already-pushed Worker image reference pinned by registry digest."
  type        = string
}

variable "deployment_mode" {
  description = "single (default) or split; split remains gated by allow_split_mode."
  type        = string
  default     = "single"
}

variable "allow_split_mode" {
  description = "Explicit cost gate for split mode; keep false while the combined EUR 50 target is in force."
  type        = bool
  default     = false
}

variable "create_applications" {
  description = "Create Container Apps and the manual migration job after secrets and images are ready."
  type        = bool
  default     = false
}

variable "api_max_replicas" {
  description = "Must remain one until shared/global GraphQL rate limiting is available."
  type        = number
  default     = 1
}

variable "api_trusted_proxy_networks" {
  description = "Verified ACA ingress peer CIDRs trusted for X-Forwarded-For; required when create_applications is true."
  type        = list(string)
  default     = []
}

variable "sql_firewall_ip_addresses" {
  description = "Exact ACA egress and operator IPv4 addresses allowed by the SQL firewall."
  type        = set(string)
  default     = []
}

variable "key_vault_firewall_enabled" {
  description = "Enable a Key Vault IP allowlist after ACA and Terraform-runner egress addresses are known."
  type        = bool
  default     = false
}

variable "key_vault_allowed_ip_addresses" {
  description = "IPv4 addresses allowed by the Key Vault firewall when key_vault_firewall_enabled is true."
  type        = set(string)
  default     = []
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
  description = "Public testing application URL used in email links."
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
  description = "Daily ingestion cap for the testing Log Analytics workspace."
  type        = number
  default     = 1
}

variable "tags" {
  description = "Additional tags applied to testing resources."
  type        = map(string)
  default     = {}
}
