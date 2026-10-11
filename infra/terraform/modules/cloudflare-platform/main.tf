resource "cloudflare_workers_custom_domain" "api" {
  account_id  = var.account_id
  hostname    = var.api_hostname
  service     = var.worker_service_name
  zone_id     = var.zone_id
  environment = var.worker_environment
}
