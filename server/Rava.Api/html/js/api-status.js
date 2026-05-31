/** Poll GET /api/status and update a status element. */
export function initApiStatusMonitor(api, { elementId = "api-status", intervalMs = 30000 } = {}) {
  const el = document.getElementById(elementId);
  if (!el) {
    return () => {};
  }

  async function refresh() {
    el.textContent = "Checking API…";
    el.className = "api-status checking";

    try {
      const status = await api.getStatus();
      if (status.databaseConnected === false) {
        el.textContent = "API running · database unavailable";
        el.className = "api-status degraded";
        return;
      }

      el.textContent = "API online";
      el.className = "api-status online";
    } catch {
      const target = api.baseUrl || window.location.origin;
      el.textContent = `API offline · ${target}`;
      el.className = "api-status error";
    }
  }

  refresh();
  const timer = window.setInterval(refresh, intervalMs);
  return () => window.clearInterval(timer);
}
