resource "cloudflare_r2_bucket" "terraform_state" {
  account_id = var.account_id
  name       = var.state_bucket_name
  location   = var.state_bucket_location

  lifecycle {
    prevent_destroy = true
  }
}
