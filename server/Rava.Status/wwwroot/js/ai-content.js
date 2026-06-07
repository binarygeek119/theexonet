const POLL_MS = 15000;

const els = {
  updated: document.getElementById("ai-content-updated"),
  overall: document.getElementById("ai-overall"),
  apiKey: document.getElementById("ai-api-key"),
  activeFeatures: document.getElementById("ai-active-features"),
  textModel: document.getElementById("ai-text-model"),
  imageModel: document.getElementById("ai-image-model"),
  logoModel: document.getElementById("ai-logo-model"),
  apiError: document.getElementById("ai-api-error"),
  linkJson: document.getElementById("link-ai-json"),
  requestsTotal: document.getElementById("ai-requests-total"),
  requestsToday: document.getElementById("ai-requests-today"),
  lastRequest: document.getElementById("ai-last-request"),
  usageBars: document.getElementById("ai-usage-bars"),
  usageEmpty: document.getElementById("ai-usage-empty"),
  featureGrid: document.getElementById("ai-feature-grid"),
  newsEnabled: document.getElementById("news-enabled"),
  newsEditionDate: document.getElementById("news-edition-date"),
  newsEditionSource: document.getElementById("news-edition-source"),
  newsStoryCount: document.getElementById("news-story-count"),
  newsIllustrated: document.getElementById("news-illustrated"),
  newsMaxImages: document.getElementById("news-max-images"),
  newsArchives: document.getElementById("news-archives"),
  linkGameExonet: document.getElementById("link-game-exonet"),
  reportersTotal: document.getElementById("reporters-total"),
  reportersPool: document.getElementById("reporters-pool"),
  portraitJobStatus: document.getElementById("portrait-job-status"),
  portraitJobDetail: document.getElementById("portrait-job-detail"),
  portraitImagesSaved: document.getElementById("portrait-images-saved"),
  logoEnabled: document.getElementById("logo-enabled"),
  logoQueueDelay: document.getElementById("logo-queue-delay"),
  logoDedicatedKey: document.getElementById("logo-dedicated-key"),
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

function formatYesNo(value) {
  if (value == null) return "—";
  return value ? "Yes" : "No";
}

function formatCategoryLabel(key) {
  return String(key ?? "other").replaceAll("_", " ");
}

function escapeHtml(text) {
  return String(text ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function renderUsageBars(byCategory, total) {
  const entries = Object.entries(byCategory ?? {}).filter(([, count]) => count > 0);
  if (entries.length === 0) {
    els.usageBars.innerHTML = "";
    els.usageEmpty.hidden = false;
    return;
  }

  els.usageEmpty.hidden = true;
  const max = Math.max(total || 0, ...entries.map(([, count]) => count));
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
    els.featureGrid.innerHTML = "<p class=\"section-hint\">No AI features reported.</p>";
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
          <div><dt>Model</dt><dd>${escapeHtml(feature.modelOrEndpoint || "—")}</dd></div>
        </dl>
      </article>`;
    })
    .join("");
}

function renderPage(data) {
  els.updated.textContent = `Last updated ${new Date().toLocaleString()} · polls every ${POLL_MS / 1000}s`;
  els.linkJson.href = `${data.apiPublicUrl.replace(/\/$/, "")}/api/status/openai`;
  els.linkGameExonet.href = data.gameUrl;

  const rava = data.rava;
  if (!rava) {
    setPill(els.overall, "API unreachable", "offline");
    els.apiError.textContent = data.ravaError || "Could not load AI content status from theexonet API";
    return;
  }

  els.apiError.textContent = data.ravaError || "—";
  els.apiKey.textContent = formatYesNo(rava.apiKeyConfigured);
  els.requestsTotal.textContent = formatCount(rava.totalRequests);
  els.requestsToday.textContent = formatCount(rava.requestsToday);
  els.lastRequest.textContent = formatUtc(rava.lastRequestUtc);
  renderUsageBars(rava.requestsByCategory, rava.totalRequests);
  renderFeatures(rava.gameFeatures);

  const activeCount = (rava.gameFeatures ?? []).filter((feature) => feature.enabled).length;
  const totalFeatures = rava.gameFeatures?.length ?? 0;
  els.activeFeatures.textContent = `${activeCount} / ${totalFeatures}`;

  const cfg = rava.configuration;
  if (cfg) {
    els.textModel.textContent = cfg.textModel || "—";
    els.imageModel.textContent = cfg.imageModel || "—";
    els.logoModel.textContent = cfg.companyLogoImageModel || "—";
    els.newsMaxImages.textContent = formatCount(cfg.maxImagesPerDay);
    els.logoEnabled.textContent = formatYesNo(cfg.companyLogoEnabled);
    els.logoDedicatedKey.textContent = formatYesNo(cfg.companyLogoUsesDedicatedKey);
    els.logoQueueDelay.textContent =
      cfg.companyLogoSecondsBetweenGenerations != null
        ? `${cfg.companyLogoSecondsBetweenGenerations}s between queued logos`
        : "—";
  }

  const exonet = rava.exonet;
  if (exonet) {
    els.newsEnabled.textContent = formatYesNo(exonet.offworldNewsEnabled);
    els.newsEditionDate.textContent = exonet.todayEditionDate || "—";
    els.newsEditionSource.textContent = exonet.todayEditionSource || "—";
    els.newsStoryCount.textContent = formatCount(exonet.todayStoryCount);
    els.newsIllustrated.textContent = formatCount(exonet.todayIllustratedStories);
    els.newsArchives.textContent = formatCount(exonet.archivedEditionCount);
    els.reportersTotal.textContent = formatCount(exonet.totalReporters);
    els.reportersPool.textContent =
      `${formatCount(exonet.activeReporterPool)} / ${formatCount(exonet.totalReporters)} (pool ${formatCount(exonet.reporterPoolSize)})`;
    els.portraitJobStatus.textContent = exonet.portraitJobStatus || "—";
    const portraitParts = [
      exonet.portraitJobMessage,
      exonet.portraitJobImageAttempts > 0
        ? `${formatCount(exonet.portraitJobImagesSaved)} / ${formatCount(exonet.portraitJobImageAttempts)} images saved`
        : null,
    ].filter(Boolean);
    els.portraitJobDetail.textContent = portraitParts.length ? portraitParts.join(" · ") : "—";
    els.portraitImagesSaved.textContent =
      exonet.portraitJobImageAttempts > 0
        ? `${formatCount(exonet.portraitJobImagesSaved)} / ${formatCount(exonet.portraitJobImageAttempts)}`
        : "—";
  }

  if (!rava.apiKeyConfigured) {
    setPill(els.overall, "No API key", "offline");
    return;
  }

  if (activeCount === 0) {
    setPill(els.overall, "Configured — inactive", "degraded");
    return;
  }

  setPill(els.overall, "AI content active", "online");
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
