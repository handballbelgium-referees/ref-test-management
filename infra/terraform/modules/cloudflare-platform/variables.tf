variable "account_id" {
  description = "Cloudflare account ID that owns the active zone and Worker."
  type        = string

  validation {
    condition     = can(regex("^[0-9a-fA-F]{32}$", var.account_id))
    error_message = "account_id must be a 32-character Cloudflare account ID."
  }
}

variable "zone_id" {
  description = "ID of the active Cloudflare zone containing api_hostname."
  type        = string

  validation {
    condition     = can(regex("^[0-9a-fA-F]{32}$", var.zone_id))
    error_message = "zone_id must be a 32-character Cloudflare zone ID."
  }
}

variable "api_hostname" {
  description = "Existing hostname in zone_id to attach to the deployed Worker."
  type        = string

  validation {
    condition = length(split(".", var.api_hostname)) > 1 && alltrue([
      for label in split(".", lower(var.api_hostname)) :
      can(regex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$", label))
    ])
    error_message = "api_hostname must be a DNS hostname without a scheme, port, path, wildcard, or trailing dot."
  }
}

variable "worker_service_name" {
  description = "Name of the Worker service already deployed with Wrangler."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9][a-z0-9-]{0,62}$", var.worker_service_name))
    error_message = "worker_service_name must contain 1-63 lowercase letters, numbers, or hyphens and start with a letter or number."
  }
}

variable "worker_environment" {
  description = "Optional Wrangler environment name; prefer a distinct Worker service name when possible."
  type        = string
  default     = null
  nullable    = true

  validation {
    condition = var.worker_environment == null || can(regex(
      "^[A-Za-z0-9_-]{1,64}$",
      var.worker_environment
    ))
    error_message = "worker_environment must be null or contain 1-64 letters, numbers, underscores, or hyphens."
  }
}
