module "cloudflare_platform" {
  source = "../../../modules/cloudflare-platform"

  account_id          = var.account_id
  zone_id             = var.zone_id
  api_hostname        = var.api_hostname
  worker_service_name = var.worker_service_name
  worker_environment  = var.worker_environment
}
