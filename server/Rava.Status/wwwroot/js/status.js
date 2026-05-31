const POLL_MS = 10000;

const els = {
  lastUpdated: document.getElementById("last-updated"),
  apiOverall: document.getElementById("api-overall"),
  apiService: document.getElementById("api-service"),
  apiPlayers: document.getElementById("api-players"),
  apiUptime: document.getElementById("api-uptime"),
  apiFirstRun: document.getElementById("api-first-run"),
  apiResponseMs: document.getElementById("api-response-ms"),
  apiChecked: document.getElementById("api-checked"),
  apiEndpoint: document.getElementById("api-endpoint"),
  apiError: document.getElementById("api-error"),
  dbOverall: document.getElementById("db-overall"),
  dbConnection: document.getElementById("db-connection"),
  dbReported: document.getElementById("db-reported"),
  monitorUptime: document.getElementById("monitor-uptime"),
  monitorFirstRun: document.getElementById("monitor-first-run"),
  monitorUtc: document.getElementById("monitor-utc"),
  linkStatus: document.getElementById("link-status"),
  linkGame: document.getElementById("link-game"),
  linkApi: document.getElementById("link-api"),
  linkValues: document.getElementById("link-values"),
  linkApiStatus: document.getElementById("link-api-status"),
};

function setPill(element, label, tone) {
  element.textContent = label;
  element.className = `status-pill ${tone}`;
}

function formatDuration(totalSeconds) {
  if (totalSeconds == null || Number.isNaN(totalSeconds)) {
    return "—";
  }

  const seconds = Math.floor(totalSeconds);
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;

  const parts = [];
  if (days) parts.push(`${days}d`);
  if (hours || days) parts.push(`${hours}h`);
  parts.push(`${minutes}m ${secs}s`);
  return parts.join(" ");
}

function formatUtc(value) {
  if (!value) return "—";
  return new Date(value).toISOString().replace("T", " ").replace(".000Z", " UTC");
}

function formatCount(value) {
  if (value == null) return "—";
  return new Intl.NumberFormat().format(value);
}

function renderDashboard(data) {
  els.lastUpdated.textContent = `Last updated ${new Date().toLocaleString()}`;
  els.apiEndpoint.textContent = data.apiBaseUrl;
  els.apiResponseMs.textContent = data.apiResponseMs != null ? `${data.apiResponseMs} ms` : "—";
  els.apiChecked.textContent = formatUtc(data.utc);
  els.apiError.textContent = data.apiError || "—";

  els.linkStatus.href = data.statusPublicUrl;
  els.linkGame.href = data.gameUrl;
  els.linkApi.href = data.apiPublicUrl;
  els.linkApiStatus.href = `${data.apiBaseUrl}/api/status`;

  els.monitorUptime.textContent = formatDuration(data.monitorUptimeSeconds);
  els.monitorFirstRun.textContent = formatUtc(data.monitorFirstRunUtc);
  els.monitorUtc.textContent = formatUtc(data.utc);

  const apiStatus = data.apiStatus;
  if (!data.apiReachable || !apiStatus) {
    setPill(els.apiOverall, "Offline", "offline");
    els.apiService.textContent = "Unreachable";
    els.apiPlayers.textContent = "—";
    els.apiUptime.textContent = "—";
    els.apiFirstRun.textContent = "—";
    setPill(els.dbOverall, "Unknown", "offline");
    els.dbConnection.textContent = "Unknown";
    els.dbReported.textContent = "—";
    return;
  }

  els.apiService.textContent = apiStatus.service || "Rava.Api";
  els.apiPlayers.textContent = formatCount(apiStatus.playerCount);
  els.apiUptime.textContent = formatDuration(apiStatus.serverUptimeSeconds);
  els.apiFirstRun.textContent = formatUtc(apiStatus.serverFirstRunUtc);

  if (apiStatus.status === "online" && apiStatus.databaseConnected) {
    setPill(els.apiOverall, "Online", "online");
  } else if (apiStatus.status === "degraded" || !apiStatus.databaseConnected) {
    setPill(els.apiOverall, "Degraded", "degraded");
  } else {
    setPill(els.apiOverall, apiStatus.status || "Unknown", "checking");
  }

  if (apiStatus.databaseStatus === "online" || apiStatus.databaseConnected) {
    setPill(els.dbOverall, "Online", "online");
    els.dbConnection.textContent = "Connected";
  } else {
    setPill(els.dbOverall, "Offline", "offline");
    els.dbConnection.textContent = "Not connected";
  }

  els.dbReported.textContent = formatUtc(apiStatus.utc);
}

async function refresh() {
  setPill(els.apiOverall, "Checking…", "checking");
  setPill(els.dbOverall, "Checking…", "checking");

  try {
    const response = await fetch("/api/dashboard");
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    renderDashboard(await response.json());
  } catch (error) {
    setPill(els.apiOverall, "Monitor error", "error");
    setPill(els.dbOverall, "Unknown", "offline");
    els.apiError.textContent = error.message;
    els.lastUpdated.textContent = "Failed to load dashboard data";
  }
}

refresh();
window.setInterval(refresh, POLL_MS);
