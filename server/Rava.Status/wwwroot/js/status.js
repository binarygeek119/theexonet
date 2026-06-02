import { initI18n, applyTranslations, wireLocaleSelectors, t } from "./i18n.js";

const POLL_MS = 10000;

const els = {
  lastUpdated: document.getElementById("last-updated"),
  gameVersion: document.getElementById("game-version"),
  apiOverall: document.getElementById("api-overall"),
  apiService: document.getElementById("api-service"),
  apiGameVersion: document.getElementById("api-game-version"),
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
  docsOverall: document.getElementById("docs-overall"),
  docsResponseMs: document.getElementById("docs-response-ms"),
  docsChecked: document.getElementById("docs-checked"),
  docsEndpoint: document.getElementById("docs-endpoint"),
  docsPublicUrl: document.getElementById("docs-public-url"),
  docsError: document.getElementById("docs-error"),
  adminOverall: document.getElementById("admin-overall"),
  adminResponseMs: document.getElementById("admin-response-ms"),
  adminChecked: document.getElementById("admin-checked"),
  adminEndpoint: document.getElementById("admin-endpoint"),
  adminPublicUrl: document.getElementById("admin-public-url"),
  adminError: document.getElementById("admin-error"),
  moderatorOverall: document.getElementById("moderator-overall"),
  moderatorResponseMs: document.getElementById("moderator-response-ms"),
  moderatorChecked: document.getElementById("moderator-checked"),
  moderatorEndpoint: document.getElementById("moderator-endpoint"),
  moderatorPublicUrl: document.getElementById("moderator-public-url"),
  moderatorError: document.getElementById("moderator-error"),
  openaiOverall: document.getElementById("openai-overall"),
  openaiDescription: document.getElementById("openai-description"),
  openaiIndicator: document.getElementById("openai-indicator"),
  openaiResponseMs: document.getElementById("openai-response-ms"),
  openaiChecked: document.getElementById("openai-checked"),
  openaiStatusPage: document.getElementById("openai-status-page"),
  openaiComponents: document.getElementById("openai-components"),
  openaiApiKey: document.getElementById("openai-api-key"),
  openaiCreditsRemaining: document.getElementById("openai-credits-remaining"),
  openaiCreditsGranted: document.getElementById("openai-credits-granted"),
  openaiCreditsNote: document.getElementById("openai-credits-note"),
  openaiRequestsTotal: document.getElementById("openai-requests-total"),
  openaiRequestsToday: document.getElementById("openai-requests-today"),
  openaiLastRequest: document.getElementById("openai-last-request"),
  openaiRequestsByType: document.getElementById("openai-requests-by-type"),
  openaiUsageError: document.getElementById("openai-usage-error"),
  openaiError: document.getElementById("openai-error"),
  monitorUptime: document.getElementById("monitor-uptime"),
  monitorFirstRun: document.getElementById("monitor-first-run"),
  monitorUtc: document.getElementById("monitor-utc"),
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

function setGameVersion(label) {
  if (!els.gameVersion) {
    return;
  }

  const text = label?.trim();
  if (!text) {
    els.gameVersion.hidden = true;
    return;
  }

  els.gameVersion.textContent = text;
  els.gameVersion.hidden = false;
}

function openAiIndicatorTone(indicator) {
  const value = String(indicator ?? "").toLowerCase();
  if (value === "none") {
    return "online";
  }

  if (value === "maintenance" || value === "minor") {
    return "degraded";
  }

  if (value === "major" || value === "critical") {
    return "offline";
  }

  return "checking";
}

function formatUsd(value) {
  if (value == null || Number.isNaN(value)) {
    return "—";
  }

  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 2,
  }).format(value);
}

function formatOpenAiRequestsByCategory(byCategory) {
  if (!byCategory || typeof byCategory !== "object") {
    return "—";
  }

  const entries = Object.entries(byCategory).filter(([, count]) => count > 0);
  if (entries.length === 0) {
    return "None yet";
  }

  return entries
    .sort((a, b) => b[1] - a[1])
    .map(([key, count]) => `${key.replaceAll("_", " ")}: ${formatCount(count)}`)
    .join("; ");
}

function renderOpenAiUsage(usage) {
  if (!usage?.reachable) {
    els.openaiApiKey.textContent = "—";
    els.openaiCreditsRemaining.textContent = "—";
    els.openaiCreditsGranted.textContent = "—";
    els.openaiCreditsNote.textContent = "—";
    els.openaiRequestsTotal.textContent = "—";
    els.openaiRequestsToday.textContent = "—";
    els.openaiLastRequest.textContent = "—";
    els.openaiRequestsByType.textContent = "—";
    els.openaiUsageError.textContent = usage?.error || "Could not load usage from API";
    return;
  }

  els.openaiUsageError.textContent = "—";
  els.openaiApiKey.textContent = usage.apiKeyConfigured ? "Yes" : "No";
  els.openaiCreditsRemaining.textContent = formatUsd(usage.creditsRemainingUsd);
  els.openaiCreditsGranted.textContent = formatUsd(usage.creditsGrantedUsd);
  els.openaiCreditsNote.textContent = usage.creditsNote || "—";
  els.openaiRequestsTotal.textContent = formatCount(usage.totalRequests);
  els.openaiRequestsToday.textContent = formatCount(usage.requestsToday);
  els.openaiLastRequest.textContent = formatUtc(usage.lastRequestUtc);
  els.openaiRequestsByType.textContent = formatOpenAiRequestsByCategory(usage.requestsByCategory);
}

function formatOpenAiComponents(components) {
  if (!Array.isArray(components) || components.length === 0) {
    return "None";
  }

  return components
    .map((component) => {
      const name = component?.name ?? "Unknown";
      const status = component?.status ?? "unknown";
      return `${name} (${status})`;
    })
    .join("; ");
}

function renderOpenAiCard(openAi, checkedUtc) {
  const statusPageUrl = openAi?.statusPageUrl || "https://status.openai.com/";
  els.openaiStatusPage.textContent = statusPageUrl;

  els.openaiResponseMs.textContent = openAi?.responseMs != null ? `${openAi.responseMs} ms` : "—";
  els.openaiChecked.textContent = formatUtc(checkedUtc);
  els.openaiError.textContent = openAi?.error || "—";
  els.openaiIndicator.textContent = openAi?.indicator || "—";
  els.openaiDescription.textContent = openAi?.description || "—";
  els.openaiComponents.textContent = formatOpenAiComponents(openAi?.degradedComponents);

  if (!openAi?.reachable) {
    setPill(els.openaiOverall, "Unreachable", "offline");
    return;
  }

  const indicator = String(openAi.indicator ?? "").toLowerCase();
  const label = openAi.description?.trim() || (indicator === "none" ? "Operational" : indicator || "Unknown");
  setPill(els.openaiOverall, label, openAiIndicatorTone(indicator));
}

function renderPortalCard(portal, {
  overall,
  responseMs,
  checked,
  endpoint,
  publicUrl,
  error,
}, checkedUtc) {
  endpoint.textContent = portal?.internalUrl || "—";
  publicUrl.textContent = portal?.publicUrl || "—";
  responseMs.textContent = portal?.responseMs != null ? `${portal.responseMs} ms` : "—";
  checked.textContent = formatUtc(checkedUtc);
  error.textContent = portal?.error || "—";

  if (portal?.reachable) {
    setPill(overall, "Online", "online");
  } else {
    setPill(overall, "Offline", "offline");
  }
}

function renderDashboard(data) {
  els.lastUpdated.textContent = t("footer.lastUpdated", {
    time: new Date().toLocaleString(),
  });
  els.apiEndpoint.textContent = data.apiBaseUrl;
  els.apiResponseMs.textContent = data.apiResponseMs != null ? `${data.apiResponseMs} ms` : "—";
  els.apiChecked.textContent = formatUtc(data.utc);
  els.apiError.textContent = data.apiError || "—";

  renderPortalCard(data.docsPortal, {
    overall: els.docsOverall,
    responseMs: els.docsResponseMs,
    checked: els.docsChecked,
    endpoint: els.docsEndpoint,
    publicUrl: els.docsPublicUrl,
    error: els.docsError,
  }, data.utc);

  renderPortalCard(data.adminPortal, {
    overall: els.adminOverall,
    responseMs: els.adminResponseMs,
    checked: els.adminChecked,
    endpoint: els.adminEndpoint,
    publicUrl: els.adminPublicUrl,
    error: els.adminError,
  }, data.utc);

  renderPortalCard(data.moderatorPortal, {
    overall: els.moderatorOverall,
    responseMs: els.moderatorResponseMs,
    checked: els.moderatorChecked,
    endpoint: els.moderatorEndpoint,
    publicUrl: els.moderatorPublicUrl,
    error: els.moderatorError,
  }, data.utc);

  renderOpenAiCard(data.openAi, data.utc);
  renderOpenAiUsage(data.openAiUsage);

  els.monitorUptime.textContent = formatDuration(data.monitorUptimeSeconds);
  els.monitorFirstRun.textContent = formatUtc(data.monitorFirstRunUtc);
  els.monitorUtc.textContent = formatUtc(data.utc);

  const apiStatus = data.apiStatus;
  if (!data.apiReachable || !apiStatus) {
    setPill(els.apiOverall, "Offline", "offline");
    els.apiService.textContent = "Unreachable";
    setGameVersion(null);
    els.apiGameVersion.textContent = "—";
    els.apiPlayers.textContent = "—";
    els.apiUptime.textContent = "—";
    els.apiFirstRun.textContent = "—";
    setPill(els.dbOverall, "Unknown", "offline");
    els.dbConnection.textContent = "Unknown";
    els.dbReported.textContent = "—";
    return;
  }

  els.apiService.textContent = apiStatus.service || "Rava.Api";
  setGameVersion(apiStatus.gameVersion);
  els.apiGameVersion.textContent = apiStatus.gameVersion || "—";
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
  setPill(els.docsOverall, "Checking…", "checking");
  setPill(els.adminOverall, "Checking…", "checking");
  setPill(els.moderatorOverall, "Checking…", "checking");
  setPill(els.openaiOverall, "Checking…", "checking");

  try {
    const response = await fetch("/api/dashboard");
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    renderDashboard(await response.json());
  } catch (error) {
    setPill(els.apiOverall, "Monitor error", "error");
    setPill(els.dbOverall, "Unknown", "offline");
    setPill(els.docsOverall, "Unknown", "offline");
    setPill(els.adminOverall, "Unknown", "offline");
    setPill(els.moderatorOverall, "Unknown", "offline");
    setPill(els.openaiOverall, "Unknown", "offline");
    renderOpenAiUsage(null);
    setGameVersion(null);
    els.apiError.textContent = error.message;
    els.lastUpdated.textContent = "Failed to load dashboard data";
  }
}

async function startStatusDashboard() {
  await initI18n({ namespaces: ["status"] });
  applyTranslations(document);
  wireLocaleSelectors();
  document.addEventListener("rava:localechange", () => applyTranslations(document));
  refresh();
  window.setInterval(refresh, POLL_MS);
}

startStatusDashboard().catch((error) => console.error("[status] startup failed", error));
