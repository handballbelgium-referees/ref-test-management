output "custom_domain_hostname" {
  description = "Hostname attached to the Worker custom domain."
  value       = cloudflare_workers_custom_domain.api.hostname
}

output "custom_domain_certificate_id" {
  description = "Cloudflare-managed TLS certificate ID for the custom domain."
  value       = cloudflare_workers_custom_domain.api.cert_id
}

output "worker_service_name" {
  description = "Worker service receiving requests for the custom domain."
  value       = cloudflare_workers_custom_domain.api.service
}
