const POLL_MS = 10000;

const TEXT_KINDS = new Set([
  "onn_edition_stories",
  "foreverfall_intake",
  "lunar_weather_bulletin",
]);

const KIND_LABELS = {
  onn_edition_stories: "ONN edition stories",
  onn_reporter_avatar: "ONN reporter avatar",
  onn_reporter_background: "ONN reporter background",
  onn_story_image: "ONN story image",
  foreverfall_intake: "Foreverfall intake",
  foreverfall_portrait: "Foreverfall portrait",
  lunar_weather_bulletin: "Lunar Weather bulletin",
  voidcorp_product: "VoidCorp product",
  testing_dummy_avatar: "Testing dummy avatar",
  testing_dummy_background: "Testing dummy background",
  testing_dummy_logo: "Testing dummy logo",
  company_logo: "Company logo",
};

const els = {
  updated: document.getElementById("queue-updated"),
  overall: document.getElementById("queue-overall"),
  enabled: document.getElementById("queue-enabled"),
  currentJob: document.getElementById("queue-current-job"),
  currentKind: document.getElementById("queue-current-kind"),
  waiting: document.getElementById("queue-waiting"),
  completedToday: document.getElementById("queue-completed-today"),
  failedToday: document.getElementById("queue-failed-today"),
  apiError: document.getElementById("queue-api-error"),
  textWaiting: document.getElementById("queue-text-waiting"),
  imageWaiting: document.getElementById("queue-image-waiting"),
  kindRows: document.getElementById("queue-kind-rows"),
  kindEmpty: document.getElementById("queue-kind-empty"),
  linkJson: document.getElementById("link-queue-json"),
};

function setPill(element, label, tone) {
  element.textContent = label;
  element.className = `status-pill ${tone}`;
}

function formatCount(value) {
  if (value == null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat().format(value);
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

function labelForKind(kind) {
  return KIND_LABELS[kind] ?? kind.replaceAll("_", " ");
}

function summarizeQueuedByKind(byKind) {
  let text = 0;
  let image = 0;
  for (const [kind, count] of Object.entries(byKind ?? {})) {
    if (count <= 0) {
      continue;
    }
    if (TEXT_KINDS.has(kind)) {
      text += count;
    } else {
      image += count;
    }
  }
  return { text, image };
}

function renderKindTable(byKind) {
  const entries = Object.entries(byKind ?? {})
    .filter(([, count]) => count > 0)
    .sort((left, right) => right[1] - left[1]);

  if (entries.length === 0) {
    els.kindRows.innerHTML = "";
    els.kindEmpty.hidden = false;
    return;
  }

  els.kindEmpty.hidden = true;
  els.kindRows.innerHTML = entries
    .map(([kind, count]) => {
      const type = TEXT_KINDS.has(kind) ? "Text" : "Image";
      return `<tr>
        <th scope="row">${escapeHtml(labelForKind(kind))}</th>
        <td>${type}</td>
        <td>${formatCount(count)}</td>
      </tr>`;
    })
    .join("");
}

function queueTone(data) {
  if (!data.reachable) {
    return "offline";
  }

  if (!data.enabled) {
    return "degraded";
  }

  if (data.failedToday > 0 && data.completedToday === 0) {
    return "offline";
  }

  if (data.status === "running" || data.queuedCount > 0) {
    return "degraded";
  }

  return "online";
}

function queueLabel(data) {
  if (!data.reachable) {
    return "API unreachable";
  }

  if (!data.enabled) {
    return "Disabled";
  }

  if (data.status === "running") {
    return data.currentJobDescription?.trim() || "Processing";
  }

  if (data.queuedCount > 0) {
    return `${formatCount(data.queuedCount)} waiting`;
  }

  return "Idle";
}

function renderPage(data) {
  els.updated.textContent = `Last updated ${new Date().toLocaleString()} · polls every ${POLL_MS / 1000}s`;
  if (data.apiPublicUrl) {
    els.linkJson.href = `${String(data.apiPublicUrl).replace(/\/$/, "")}/api/status/ai-queue`;
  }

  const byKind = data.queuedByKind ?? {};
  const { text, image } = summarizeQueuedByKind(byKind);

  els.enabled.textContent = data.reachable ? formatYesNo(data.enabled) : "—";
  els.currentJob.textContent = data.currentJobDescription || "—";
  els.currentKind.textContent = data.currentJobKind ? labelForKind(data.currentJobKind) : "—";
  els.waiting.textContent = formatCount(data.queuedCount);
  els.completedToday.textContent = formatCount(data.completedToday);
  els.failedToday.textContent = formatCount(data.failedToday);
  els.apiError.textContent = data.error || "—";
  els.textWaiting.textContent = formatCount(text);
  els.imageWaiting.textContent = formatCount(image);

  renderKindTable(byKind);
  setPill(els.overall, queueLabel(data), queueTone(data));
}

async function refresh() {
  setPill(els.overall, "Checking…", "checking");
  try {
    const response = await fetch("/api/ai-queue");
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
