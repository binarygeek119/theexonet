/** Poll GET /api/status and update a status element. */
export function initApiStatusMonitor(api, { elementId = "api-status", intervalMs = 30000 } = {}) {
  const el = document.getElementById(elementId);
  if (!el) {
    return () => {};
  }

  function formatDatabaseStatus(status) {
    if (status?.databaseStatus === "online" || status?.databaseConnected === true) {
      return "Database online";
    }
    if (status?.databaseStatus === "offline" || status?.databaseConnected === false) {
      return "Database offline";
    }
    return "Database unknown";
  }

  async function refresh() {
    el.textContent = "Checking API…";
    el.className = "api-status checking";

    try {
      const status = await api.getStatus();
      const databaseLabel = formatDatabaseStatus(status);
      const apiLabel = status.status === "online" ? "API online" : "API running";

      if (status.databaseConnected === false || status.databaseStatus === "offline") {
        el.textContent = `${apiLabel} · ${databaseLabel}`;
        el.className = "api-status degraded";
        return;
      }

      el.textContent = `${apiLabel} · ${databaseLabel}`;
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
