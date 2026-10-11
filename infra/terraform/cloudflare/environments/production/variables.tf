variable "account_id" {
  description = "Cloudflare account ID that owns the active production zone and Worker."
  type        = string
}

variable "zone_id" {
  description = "ID of the active Cloudflare production zone containing api_hostname."
  type        = string
}

variable "api_hostname" {
  description = "Existing production hostname to attach to the Wrangler-deployed Worker."
  type        = string
}

variable "worker_service_name" {
  description = "Production Worker service name already deployed with Wrangler."
  type        = string
}

variable "worker_environment" {
  description = "Optional Wrangler environment name; prefer a distinct Worker service name when possible."
  type        = string
  default     = null
  nullable    = true
}
