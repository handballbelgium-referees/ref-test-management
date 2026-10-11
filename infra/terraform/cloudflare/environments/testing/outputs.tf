output "custom_domain_hostname" {
  description = "Testing hostname attached to the Worker."
  value       = module.cloudflare_platform.custom_domain_hostname
}

output "worker_service_name" {
  description = "Testing Worker service receiving the custom domain."
  value       = module.cloudflare_platform.worker_service_name
}

output "custom_domain_certificate_id" {
  description = "Cloudflare-managed TLS certificate ID for the testing hostname."
  value       = module.cloudflare_platform.custom_domain_certificate_id
}
