# Generate SSH key for theexonet root login and print next steps.
# gcloud is not required for this step.
param(
    [string]$KeyPath = "$env:USERPROFILE\.ssh\id_ed25519"
)

$pubPath = "$KeyPath.pub"
$keyDir = Split-Path -Parent $KeyPath
if (-not (Test-Path $keyDir)) {
    New-Item -ItemType Directory -Path $keyDir -Force | Out-Null
}

if (-not (Test-Path $pubPath)) {
    Write-Host "Generating Ed25519 key at $KeyPath ..."
    ssh-keygen -t ed25519 -f $KeyPath -C "root@theexonet" -N '""'
} else {
    Write-Host "Public key already exists: $pubPath"
}

Write-Host ""
Write-Host "=== Public key (for GCP metadata: root:... ) ===" -ForegroundColor Cyan
Get-Content $pubPath
Write-Host ""
Write-Host "=== Next steps ===" -ForegroundColor Green
Write-Host "1. Install Google Cloud SDK: https://cloud.google.com/sdk/docs/install"
Write-Host "2. In WSL or Cloud Shell, from repo root:"
Write-Host '   export GCP_PROJECT_ID=your-project-id'
$unixPub = $pubPath -replace '\\', '/'
Write-Host "   export THEEXONET_SSH_PUBKEY_FILE=$unixPub"
Write-Host '   bash scripts/theexonet/create-gcp-vm.sh'
Write-Host ""
Write-Host '3. Or use Google Cloud Console - see docs/theexonet-gcp.md'
Write-Host ''
Write-Host 'SSH after VM is up:'
Write-Host ('  ssh -i ' + $KeyPath + ' root@EXTERNAL_IP')
