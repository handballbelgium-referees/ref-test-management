output "state_bucket_name" {
  description = "R2 bucket name used by the Cloudflare environment backends."
  value       = cloudflare_r2_bucket.terraform_state.name
}

output "state_backend_endpoint" {
  description = "S3-compatible R2 endpoint for Terraform state."
  value       = "https://${var.account_id}.r2.cloudflarestorage.com"
}
