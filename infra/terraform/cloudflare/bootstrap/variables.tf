variable "account_id" {
  description = "Cloudflare account ID that will own the R2 Terraform state bucket."
  type        = string

  validation {
    condition     = can(regex("^[0-9a-fA-F]{32}$", var.account_id))
    error_message = "account_id must be a 32-character Cloudflare account ID."
  }
}

variable "state_bucket_name" {
  description = "Globally unique R2 bucket name reserved for Terraform state."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", var.state_bucket_name))
    error_message = "state_bucket_name must contain 3-63 lowercase letters, numbers, or hyphens and start and end with a letter or number."
  }
}

variable "state_bucket_location" {
  description = "Best-effort R2 bucket placement; this does not guarantee data residency."
  type        = string
  default     = "weur"

  validation {
    condition     = contains(["apac", "eeur", "enam", "weur", "wnam", "oc"], var.state_bucket_location)
    error_message = "state_bucket_location must be one of apac, eeur, enam, weur, wnam, or oc."
  }
}
