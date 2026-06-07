const POLL_MS = 15000;

const els = {
  updated: document.getElementById("openai-updated"),
  platformOverall: document.getElementById("platform-overall"),
  platformDescription: document.getElementById("platform-description"),
  platformIndicator: document.getElementById("platform-indicator"),
  platformResponseMs: document.getElementById("platform-response-ms"),
  platformChecked: document.getElementById("platform-checked"),
  platformDegraded: document.getElementById("platform-degraded"),
  platformError: document.getElementById("platform-error"),
  linkPlatformStatus: document.getElementById("link-platform-status"),
  linkOpenAiBilling: document.getElementById("link-openai-billing"),
  ravaApiKey: document.getElementById("rava-api-key"),
  ravaCreditsRemaining: document.getElementById("rava-credits-remaining"),
  ravaCreditsGranted: document.getElementById("rava-credits-granted"),
  ravaCreditsUsed: document.getElementById("rava-credits-used"),
  ravaCreditsNote: document.getElementById("rava-credits-note"),
  ravaRequestsTotal: document.getElementById("rava-requests-total"),
  ravaRequestsToday: document.getElementById("rava-requests-today"),
  ravaLastRequest: document.getElementById("rava-last-request"),
  ravaApiError: document.getElementById("rava-api-error"),
  linkRavaOpenAiJson: document.getElementById("link-rava-openai-json"),
  cfgBaseUrl: document.getElementById("cfg-base-url"),
  cfgApiKeyHint: document.getElementById("cfg-api-key-hint"),
  cfgTextModel: document.getElementById("cfg-text-model"),
  cfgImageModel: document.getElementById("cfg-image-model"),
  cfgStoriesPerDay: document.getElementById("cfg-stories-per-day"),
  cfgMaxImages: document.getElementById("cfg-max-images"),
  cfgOffworldEnabled: document.getElementById("cfg-offworld-enabled"),
  cfgLogoEnabled: document.getElementById("cfg-logo-enabled"),
  cfgLogoModel: document.getElementById("cfg-logo-model"),
  cfgLogoDelay: document.getElementById("cfg-logo-delay"),
  usageBars: document.getElementById("usage-bars"),
  usageBarsEmpty: document.getElementById("usage-bars-empty"),
  featureGrid: document.getElementById("feature-grid"),
  exonetEnabled: document.getElementById("exonet-enabled"),
  exonetEditionDate: document.getElementById("exonet-edition-date"),
  exonetEditionSource: document.getElementById("exonet-edition-source"),
  exonetStoryCount: document.getElementById("exonet-story-count"),
  exonetIllustrated: document.getElementById("exonet-illustrated"),
  exonetArchives: document.getElementById("exonet-archives"),
  exonetReportersTotal: document.getElementById("exonet-reporters-total"),
  exonetReportersPool: document.getElementById("exonet-reporters-pool"),
  exonetPortraitJob: document.getElementById("exonet-portrait-job"),
  exonetPortraitDetail: document.getElementById("exonet-portrait-detail"),
  linkGameExonet: document.getElementById("link-game-exonet"),
  platformComponentsRows: document.getElementById("platform-components-rows"),
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
  if (value == null) return "—";
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

function openAiIndicatorTone(indicator) {
  const value = String(indicator ?? "").toLowerCase();
  if (value === "none") return "online";
  if (value === "maintenance" || value === "minor") return "degraded";
  if (value === "major" || value === "critical") return "offline";
  return "checking";
}

function componentStatusClass(status) {
  const value = String(status ?? "").toLowerCase();
  if (value === "operational") return "online";
  if (value.includes("degraded") || value.includes("partial")) return "degraded";
  return "offline";
}

function formatCategoryLabel(key) {
  return String(key ?? "other").replaceAll("_", " ");
}

function renderPlatform(platform, checkedUtc) {
  const statusPageUrl = platform?.statusPageUrl || "https://status.openai.com/";
  els.linkPlatformStatus.href = statusPageUrl;
  els.platformResponseMs.textContent =
    platform?.responseMs != null ? `${platform.responseMs} ms` : "—";
  els.platformChecked.textContent = formatUtc(checkedUtc);
  els.platformError.textContent = platform?.error || "—";
  els.platformIndicator.textContent = platform?.indicator || "—";
  els.platformDescription.textContent = platform?.description || "—";

  const degraded = platform?.degradedComponents ?? [];
  els.platformDegraded.textContent =
    degraded.length === 0
      ? "None"
      : degraded.map((c) => `${c.name} (${c.status})`).join("; ");

  if (!platform?.reachable) {
    setPill(els.platformOverall, "Unreachable", "offline");
    els.platformComponentsRows.innerHTML =
      `<tr><td colspan="2">${platform?.error || "Could not reach status.openai.com"}</td></tr>`;
    return;
  }

  const indicator = String(platform.indicator ?? "").toLowerCase();
  const label =
    platform.description?.trim() || (indicator === "none" ? "Operational" : indicator || "Unknown");
  setPill(els.platformOverall, label, openAiIndicatorTone(indicator));

  const components = platform.allComponents ?? [];
  if (components.length === 0) {
    els.platformComponentsRows.innerHTML = "<tr><td colspan=\"2\">No component list returned.</td></tr>";
    return;
  }

  els.platformComponentsRows.innerHTML = components
    .map((component) => {
      const tone = componentStatusClass(component.status);
      return `<tr>
        <td>${escapeHtml(component.name)}</td>
        <td><span class="component-status ${tone}">${escapeHtml(component.status)}</span></td>
      </tr>`;
    })
    .join("");
}

function renderUsageBars(byCategory, total) {
  const entries = Object.entries(byCategory ?? {}).filter(([, count]) => count > 0);
  if (entries.length === 0) {
    els.usageBars.innerHTML = "";
    els.usageBarsEmpty.hidden = false;
    return;
  }

  els.usageBarsEmpty.hidden = true;
  const max = Math.max(total || 0, ...entries.map(([, c]) => c));
  els.usageBars.innerHTML = entries
    .sort((a, b) => b[1] - a[1])
    .map(([key, count]) => {
      const pct = max > 0 ? Math.round((count / max) * 100) : 0;
      return `<div class="usage-bar-row">
        <div class="usage-bar-label">${escapeHtml(formatCategoryLabel(key))}</div>
        <div class="usage-bar-track"><div class="usage-bar-fill" style="width:${pct}%"></div></div>
        <div class="usage-bar-value">${formatCount(count)}</div>
      </div>`;
    })
    .join("");
}

function renderFeatures(features) {
  const list = features ?? [];
  if (list.length === 0) {
    els.featureGrid.innerHTML = "<p class=\"section-hint\">No features reported.</p>";
    return;
  }

  els.featureGrid.innerHTML = list
    .map((feature) => {
      const tone = feature.enabled ? "online" : "offline";
      const statusLabel = feature.enabled ? "Active" : "Inactive";
      return `<article class="feature-card">
        <div class="feature-card-head">
          <h3>${escapeHtml(feature.title)}</h3>
          <span class="status-pill ${tone}">${statusLabel}</span>
        </div>
        <p>${escapeHtml(feature.description)}</p>
        <dl>
          <div><dt>Request type</dt><dd>${escapeHtml(formatCategoryLabel(feature.requestCategory))}</dd></div>
          <div><dt>Model / endpoint</dt><dd>${escapeHtml(feature.modelOrEndpoint || "—")}</dd></div>
        </dl>
      </article>`;
    })
    .join("");
}

function renderRava(rava, ravaError, apiPublicUrl, gameUrl) {
  els.linkRavaOpenAiJson.href = `${apiPublicUrl.replace(/\/$/, "")}/api/status/openai`;
  els.linkGameExonet.href = gameUrl;

  if (!rava) {
    els.ravaApiError.textContent = ravaError || "Could not load theexonet OpenAI status from API";
    return;
  }

  els.ravaApiError.textContent = ravaError || "—";
  els.ravaApiKey.textContent = formatYesNo(rava.apiKeyConfigured);
  els.ravaCreditsRemaining.textContent = formatUsd(rava.creditsRemainingUsd);
  els.ravaCreditsGranted.textContent = formatUsd(rava.creditsGrantedUsd);
  els.ravaCreditsUsed.textContent = formatUsd(rava.creditsUsedUsd);
  els.ravaCreditsNote.textContent = rava.creditsNote || "—";
  els.ravaRequestsTotal.textContent = formatCount(rava.totalRequests);
  els.ravaRequestsToday.textContent = formatCount(rava.requestsToday);
  els.ravaLastRequest.textContent = formatUtc(rava.lastRequestUtc);

  renderUsageBars(rava.requestsByCategory, rava.totalRequests);
  renderFeatures(rava.gameFeatures);

  const cfg = rava.configuration;
  if (cfg) {
    els.cfgBaseUrl.textContent = cfg.baseUrl || "—";
    els.cfgApiKeyHint.textContent = cfg.apiKeyHint || (cfg.apiKeyConfigured ? "configured" : "not set");
    els.cfgTextModel.textContent = cfg.textModel || "—";
    els.cfgImageModel.textContent = cfg.imageModel || "—";
    els.cfgStoriesPerDay.textContent = formatCount(cfg.storiesPerDay);
    els.cfgMaxImages.textContent = formatCount(cfg.maxImagesPerDay);
    els.cfgOffworldEnabled.textContent = formatYesNo(cfg.offworldNewsEnabled);
    els.cfgLogoEnabled.textContent = formatYesNo(cfg.companyLogoEnabled);
    els.cfgLogoModel.textContent = cfg.companyLogoImageModel || "—";
    els.cfgLogoDelay.textContent =
      cfg.companyLogoSecondsBetweenGenerations != null
        ? `${cfg.companyLogoSecondsBetweenGenerations}s between queued logos`
        : "—";
  }

  const exonet = rava.exonet;
  if (exonet) {
    els.exonetEnabled.textContent = formatYesNo(exonet.offworldNewsEnabled);
    els.exonetEditionDate.textContent = exonet.todayEditionDate || "—";
    els.exonetEditionSource.textContent = exonet.todayEditionSource || "—";
    els.exonetStoryCount.textContent = formatCount(exonet.todayStoryCount);
    els.exonetIllustrated.textContent = formatCount(exonet.todayIllustratedStories);
    els.exonetArchives.textContent = formatCount(exonet.archivedEditionCount);
    els.exonetReportersTotal.textContent = formatCount(exonet.totalReporters);
    els.exonetReportersPool.textContent = `${formatCount(exonet.activeReporterPool)} / ${formatCount(exonet.totalReporters)} (pool size ${formatCount(exonet.reporterPoolSize)})`;
    els.exonetPortraitJob.textContent = exonet.portraitJobStatus || "—";
    const portraitParts = [
      exonet.portraitJobMessage,
      exonet.portraitJobImageAttempts > 0
        ? `${formatCount(exonet.portraitJobImagesSaved)} / ${formatCount(exonet.portraitJobImageAttempts)} images saved`
        : null,
    ].filter(Boolean);
    els.exonetPortraitDetail.textContent = portraitParts.length ? portraitParts.join(" · ") : "—";
  }
}

function escapeHtml(text) {
  return String(text ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function renderPage(data) {
  els.updated.textContent = `Last updated ${new Date().toLocaleString()} · polls every ${POLL_MS / 1000}s`;
  renderPlatform(data.platform, data.utc);
  renderRava(data.rava, data.ravaError, data.apiPublicUrl, data.gameUrl);
}

async function refresh() {
  setPill(els.platformOverall, "Checking…", "checking");
  try {
    const response = await fetch("/api/openai");
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    renderPage(await response.json());
  } catch (error) {
    setPill(els.platformOverall, "Monitor error", "error");
    els.updated.textContent = `Failed to load: ${error.message}`;
    els.ravaApiError.textContent = error.message;
  }
}

refresh();
window.setInterval(refresh, POLL_MS);
