variable "subscription_id" {
  description = "Subscription where the bootstrap resources are created."
  type        = string

  validation {
    condition     = can(regex("^[0-9a-fA-F-]{36}$", var.subscription_id))
    error_message = "subscription_id must be a subscription GUID."
  }
}

variable "location" {
  description = "Azure region for shared bootstrap resources. Prefer Belgium Central."
  type        = string
  default     = "Belgium Central"
}

variable "bootstrap_resource_group_name" {
  description = "New resource group for the state storage account, ACR, and budget."
  type        = string
}

variable "state_storage_account_name" {
  description = "Globally unique, lowercase alphanumeric name for the Terraform state account."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{3,24}$", var.state_storage_account_name))
    error_message = "state_storage_account_name must be 3-24 lowercase letters or digits."
  }
}

variable "state_container_name" {
  description = "Private blob container shared by the separate testing and production state keys."
  type        = string
  default     = "terraform-state"
}

variable "acr_name" {
  description = "Globally unique Azure Container Registry name."
  type        = string

  validation {
    condition     = can(regex("^[a-zA-Z0-9]{5,50}$", var.acr_name))
    error_message = "acr_name must contain 5-50 alphanumeric characters."
  }
}

variable "terraform_runner_object_id" {
  description = "Optional Entra service-principal object ID used by GitHub OIDC to access remote state."
  type        = string
  default     = null
  nullable    = true

  validation {
    condition = var.terraform_runner_object_id == null || can(regex(
      "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
      var.terraform_runner_object_id
    ))
    error_message = "terraform_runner_object_id must be null or an Entra object ID GUID."
  }
}

variable "acr_publisher_object_id" {
  description = "Optional Entra service-principal object ID used by GitHub OIDC to push images."
  type        = string
  default     = null
  nullable    = true

  validation {
    condition = var.acr_publisher_object_id == null || can(regex(
      "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
      var.acr_publisher_object_id
    ))
    error_message = "acr_publisher_object_id must be null or an Entra object ID GUID."
  }
}

variable "testing_resource_group_name" {
  description = "Testing environment resource group; included in the combined budget filter."
  type        = string
  default     = "rtm-testing"
}

variable "production_resource_group_name" {
  description = "Production environment resource group; included in the combined budget filter."
  type        = string
  default     = "rtm-production"
}

variable "subscription_currency" {
  description = "Verified billing currency for this subscription. The EUR 50 budget is created only for EUR subscriptions."
  type        = string
  default     = "EUR"

  validation {
    condition     = can(regex("^[A-Z]{3}$", var.subscription_currency))
    error_message = "subscription_currency must be a three-letter uppercase currency code."
  }
}

variable "tags" {
  description = "Tags applied to shared bootstrap resources."
  type        = map(string)
  default     = {}
}
