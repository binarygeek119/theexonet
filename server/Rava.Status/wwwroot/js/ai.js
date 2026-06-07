const POLL_MS = 15000;

const AI_FEATURES = [
  { key: "story_generation", label: "AI news stories" },
  { key: "image_generation", label: "Story illustrations" },
  { key: "reporter_avatar", label: "Reporter profile pics" },
  { key: "reporter_background", label: "Reporter profile backgrounds" },
  { key: "reporter_portrait", label: "Reporter portraits (legacy)" },
  { key: "company_logo", label: "Company logos" },
  { key: "other", label: "Other" },
];

const els = {
  updated: document.getElementById("ai-updated"),
  overall: document.getElementById("ai-overall"),
  apiKey: document.getElementById("ai-api-key"),
  totalCalls: document.getElementById("ai-total-calls"),
  successful: document.getElementById("ai-successful"),
  failed: document.getElementById("ai-failed"),
  todayTotal: document.getElementById("ai-today-total"),
  todaySuccess: document.getElementById("ai-today-success"),
  todayFailed: document.getElementById("ai-today-failed"),
  lastRequest: document.getElementById("ai-last-request"),
  apiError: document.getElementById("ai-api-error"),
  linkJson: document.getElementById("link-ai-json"),
  creditsGranted: document.getElementById("ai-credits-granted"),
  creditsRemaining: document.getElementById("ai-credits-remaining"),
  creditsUsed: document.getElementById("ai-credits-used"),
  creditsNote: document.getElementById("ai-credits-note"),
  statsRows: document.getElementById("ai-stats-rows"),
  statsFoot: document.getElementById("ai-stats-foot"),
};

function setPill(element, label, tone) {
  element.textContent = label;
  element.className = `status-pill ${tone}`;
}

function formatUtc(value) {
  if (!value) return "—";
  return new Date(value).toISOString().replace("T", " ").replace(".000Z", " UTC");
}

function formatCount(value) {
  if (value == null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat().format(value);
}

function formatUsd(value) {
  if (value == null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 2,
  }).format(value);
}

function formatYesNo(value) {
  if (value == null) return "—";
  return value ? "Yes" : "No";
}

function escapeHtml(text) {
  return String(text ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function readCount(map, key) {
  if (!map || typeof map !== "object") return 0;
  return Number(map[key] ?? 0);
}

function renderStatsTable(rava) {
  const totals = rava.requestsByCategory ?? {};
  const successes = rava.successfulRequestsByCategory ?? {};
  const failures = rava.failedRequestsByCategory ?? {};

  const rows = AI_FEATURES.map(({ key, label }) => {
    const total = readCount(totals, key);
    const success = readCount(successes, key);
    const failed = readCount(failures, key);
    if (total === 0 && success === 0 && failed === 0) {
      return "";
    }

    return `<tr>
      <th scope="row">${escapeHtml(label)}</th>
      <td>${formatCount(total)}</td>
      <td class="ai-stat-success">${formatCount(success)}</td>
      <td class="ai-stat-failed">${formatCount(failed)}</td>
    </tr>`;
  }).filter(Boolean);

  if (rows.length === 0) {
    els.statsRows.innerHTML = `<tr><td colspan="4">No AI calls recorded yet.</td></tr>`;
    els.statsFoot.innerHTML = "";
    return;
  }

  els.statsRows.innerHTML = rows.join("");
  els.statsFoot.innerHTML = `<tr>
    <th scope="row">All features</th>
    <td>${formatCount(rava.totalRequests)}</td>
    <td class="ai-stat-success">${formatCount(rava.successfulRequests)}</td>
    <td class="ai-stat-failed">${formatCount(rava.failedRequests)}</td>
  </tr>`;
}

function renderPage(data) {
  els.updated.textContent = `Last updated ${new Date().toLocaleString()} · polls every ${POLL_MS / 1000}s`;
  els.linkJson.href = `${data.apiPublicUrl.replace(/\/$/, "")}/api/status/openai`;

  const rava = data.rava;
  if (!rava) {
    setPill(els.overall, "API unreachable", "offline");
    els.apiError.textContent = data.ravaError || "Could not load AI status from theexonet API";
    els.statsRows.innerHTML = `<tr><td colspan="4">${escapeHtml(data.ravaError || "Unavailable")}</td></tr>`;
    return;
  }

  els.apiError.textContent = data.ravaError || "—";
  els.apiKey.textContent = formatYesNo(rava.apiKeyConfigured);
  els.totalCalls.textContent = formatCount(rava.totalRequests);
  els.successful.textContent = formatCount(rava.successfulRequests);
  els.failed.textContent = formatCount(rava.failedRequests);
  els.todayTotal.textContent = formatCount(rava.requestsToday);
  els.todaySuccess.textContent = formatCount(rava.successfulRequestsToday);
  els.todayFailed.textContent = formatCount(rava.failedRequestsToday);
  els.lastRequest.textContent = formatUtc(rava.lastRequestUtc);
  els.creditsGranted.textContent = formatUsd(rava.creditsGrantedUsd);
  els.creditsRemaining.textContent = formatUsd(rava.creditsRemainingUsd);
  els.creditsUsed.textContent = formatUsd(rava.creditsUsedUsd);
  els.creditsNote.textContent = rava.creditsNote || "—";

  renderStatsTable(rava);

  if (!rava.apiKeyConfigured) {
    setPill(els.overall, "No API key", "offline");
    return;
  }

  const failRate = rava.totalRequests > 0 ? rava.failedRequests / rava.totalRequests : 0;
  if (failRate > 0.2 && rava.failedRequests > 0) {
    setPill(els.overall, "Elevated failures", "degraded");
    return;
  }

  setPill(els.overall, "AI active", "online");
}

async function refresh() {
  setPill(els.overall, "Checking…", "checking");
  try {
    const response = await fetch("/api/openai");
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    renderPage(await response.json());
  } catch (error) {
    setPill(els.overall, "Monitor error", "error");
    els.updated.textContent = `Failed to load: ${error.message}`;
    els.apiError.textContent = error.message;
  }
}

refresh();
window.setInterval(refresh, POLL_MS);
