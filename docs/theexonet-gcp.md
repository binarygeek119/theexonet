# theexonet.com on Google Cloud (Always Free, Midwest)

Host **theexonet** on GCP’s **Always Free** tier in **us-central1** (Iowa / Midwest): Ubuntu, root SSH key, Apache2, Docker, and Docker Compose.

## Free tier requirements

| Setting | Value |
|---------|--------|
| Region | **us-central1** (Midwest) — also allowed: us-west1, us-east1 |
| Machine | **e2-micro** |
| Disk | **30 GB** **Standard** (`pd-standard`) |
| Billing | Account required; stay within limits for **$0** compute |

Avoid other regions, larger VMs, Balanced/SSD disks, and load balancers to prevent charges.

## 1. Install gcloud (your PC or use [Cloud Shell](https://shell.cloud.google.com))

- Windows: [Google Cloud SDK installer](https://cloud.google.com/sdk/docs/install)
- Then: `gcloud auth login` and `gcloud config set project YOUR_PROJECT_ID`

## 2. SSH key for root

```bash
ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519 -C "root@theexonet"
```

Public key: `~/.ssh/id_ed25519.pub`  
Private key (for login): `~/.ssh/id_ed25519`

## 3. Create the VM

### Cloud Shell (no key file upload)

Replace `YOUR_GCP_PROJECT_ID`, then paste and run `scripts/theexonet/cloud-shell-bootstrap.sh`, or:

```bash
export GCP_PROJECT_ID=your-gcp-project-id
export THEEXONET_SSH_PUBLIC_KEY='ssh-ed25519 AAAA... root@theexonet'
git clone --depth 1 https://github.com/binarygeek119/theexonet.git
cd theexonet
bash scripts/theexonet/create-gcp-vm.sh
```

Paste the public key from Windows:

```powershell
Get-Content $env:USERPROFILE\.ssh\id_ed25519.pub
```

### Local (key file on disk)

```bash
export GCP_PROJECT_ID=your-gcp-project-id
export THEEXONET_SSH_PUBKEY_FILE=~/.ssh/id_ed25519.pub
bash scripts/theexonet/create-gcp-vm.sh
```

This creates **theexonet** in **us-central1-a** with:

- Ubuntu 22.04 LTS
- Metadata SSH key as **root**
- Startup script: Apache2 + Docker + `docker compose` plugin
- Firewall: TCP 22, 80, 443

If the VM already exists, the script only refreshes the root SSH key.

## 4. Connect

```bash
ssh -i ~/.ssh/id_ed25519 root@EXTERNAL_IP
```

## 5. DNS for theexonet.com

At your registrar (or Cloud DNS):

| Type | Name | Value |
|------|------|--------|
| A | `@` | VM external IP |
| A | `www` | same IP (optional) |

Check: `dig theexonet.com +short`

## 6. HTTPS (after DNS works)

On the VM:

```bash
apt install -y certbot python3-certbot-apache
certbot --apache -d theexonet.com -d www.theexonet.com
```

## 7. Verify services

```bash
systemctl status apache2 docker
docker compose version
curl -sI http://127.0.0.1/
```

## Manual re-run of software install

If you created the VM before adding the startup script:

```bash
sudo bash /path/to/scripts/theexonet/startup-install.sh
```

Or copy `scripts/theexonet/startup-install.sh` to the server and run as root.

## Windows without gcloud

1. Open [Google Cloud Console](https://console.cloud.google.com) → Compute Engine → Create instance  
2. Name: **theexonet**, Region: **us-central1**, Zone: **us-central1-a**  
3. Machine: **e2-micro**, Image: **Ubuntu 22.04 LTS**  
4. Boot disk: **Standard**, **30 GB**  
5. Networking: **Standard** tier; allow HTTP/HTTPS  
6. Security → SSH keys → Add item:

   ```text
   root:ssh-ed25519 AAAA...contents-of-id_ed25519.pub...
   ```

7. Management → Automation → Startup script: paste contents of `scripts/theexonet/startup-install.sh`

## 8. Full host setup (recommended)

After the VM is up, run the all-in-one installer (Apache vhosts, Docker Postgres, game user, FTPS, systemd):

```bash
git clone --depth 1 https://github.com/binarygeek119/theexonet.git
cd theexonet
sudo bash scripts/theexonet/install-host-server.sh
```

See **[theexonet-host-server.md](theexonet-host-server.md)** for DNS, HTTPS, and deploy steps.

## 9. FTP only (optional, if you skipped the full installer)

On the VM as **root** (after SSH key login):

```bash
sudo bash scripts/theexonet/install-ftp-server.sh
```

Creates user **`gameftp`**, upload dir **`/var/www/staging`**, **FTPS** (TLS) via vsftpd.

Open firewall **only for your IP** (passive ports required):

```bash
gcloud compute firewall-rules create theexonet-ftp \
  --direction=INGRESS --action=ALLOW \
  --rules=tcp:21,tcp:40000-40050 \
  --source-ranges=YOUR.HOME.IP/32 \
  --target-tags=theexonet-web
```

**FileZilla:** Protocol = **FTP over explicit TLS**, host = VM IP, user `gameftp`.

**Safer (no FTP):** use **SFTP** with your existing SSH key:

```bash
sftp -i ~/.ssh/id_ed25519 root@EXTERNAL_IP
```

## Notes

- **1 GB RAM** on e2-micro is tight for heavy stacks; fine for Apache + small Docker apps.  
- Prefer **root + SSH keys** only; password login is disabled by the startup script.  
- Set a **billing budget alert** (e.g. $1) in GCP Console.
