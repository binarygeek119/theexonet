# wait-for-apt-lock.sh — source before apt-get on fresh Ubuntu VMs.
# unattended-upgrades often holds /var/lib/dpkg/lock-frontend on first boot.
wait_for_apt_lock() {
  local max_wait="${1:-600}"
  local waited=0
  local interval=5

  while fuser /var/lib/dpkg/lock-frontend >/dev/null 2>&1 || \
        fuser /var/lib/apt/lists/lock >/dev/null 2>&1 || \
        fuser /var/lib/dpkg/lock >/dev/null 2>&1; do
    if [ "$waited" -ge "$max_wait" ]; then
      echo "[wait-for-apt-lock] ERROR: apt/dpkg lock still held after ${max_wait}s" >&2
      echo "  Check: ps aux | grep -E 'apt|dpkg|unattended'" >&2
      return 1
    fi
    if [ "$waited" -eq 0 ]; then
      echo "[wait-for-apt-lock] Waiting for apt/dpkg (unattended-upgrades may be running)…"
    fi
    sleep "$interval"
    waited=$((waited + interval))
  done
}
