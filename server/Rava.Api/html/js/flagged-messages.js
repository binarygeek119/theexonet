const CHANNEL_LABELS = {
  "staff-to-staff": "Staff → staff",
  "staff-to-player": "Staff → player",
  "player-to-staff": "Player → staff",
  peer: "Player → player",
  "ban-appeal": "Ban appeal",
};

const MAX_WARNINGS = 2;

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function formatDate(value) {
  if (!value) {
    return "—";
  }
  return new Date(value).toLocaleString();
}

function formatChannel(channel) {
  return CHANNEL_LABELS[channel] ?? channel;
}

export function initFlaggedMessages({
  api,
  apiPrefix,
  els,
  setStatus,
  getBanLevels,
  onOpenPlayerProfile,
}) {
  let banLevels = [];

  async function ensureBanLevels() {
    if (banLevels.length) {
      return banLevels;
    }
    banLevels = (await getBanLevels()) ?? [];
    return banLevels;
  }

  function renderBanLevelOptions(selectedCode = "1day") {
    return banLevels
      .map(
        (level) =>
          `<option value="${escapeHtml(level.code)}" ${level.code === selectedCode ? "selected" : ""}>${escapeHtml(level.label)}</option>`
      )
      .join("");
  }

  function renderReview(review) {
    const warningCount = Number(review.playerWarningCount ?? 0);
    const canWarn = warningCount < MAX_WARNINGS;
    const canBan = warningCount >= MAX_WARNINGS;
    const warningHistory = (review.playerWarnings ?? [])
      .map(
        (warning) =>
          `<li class="${warning.isActive ? "active" : "resolved"}">${escapeHtml(formatDate(warning.createdAt))} · ${escapeHtml(warning.issuedByUsername)} · ${escapeHtml(warning.reason)} · ${warning.isActive ? `expires ${formatDate(warning.expiresAt)}` : "expired"}</li>`
      )
      .join("");

    return `
      <article class="admin-flagged-card" data-flagged-id="${review.id}" data-player-id="${review.playerId ?? ""}">
        <header class="admin-flagged-top">
          <div>
            <h3>${escapeHtml(review.playerUsername)}</h3>
            <p class="admin-flagged-meta">
              ${escapeHtml(formatChannel(review.channel))} · ${escapeHtml(formatDate(review.createdAt))}
            </p>
            <p class="admin-flagged-meta">Active warnings: ${warningCount}/${MAX_WARNINGS}</p>
          </div>
          ${
            review.playerId
              ? `<button type="button" class="btn ghost admin-flagged-profile-btn">View profile</button>`
              : ""
          }
        </header>
        <p class="admin-flagged-route">
          <strong>From:</strong> ${escapeHtml(review.fromLabel)}
          · <strong>To:</strong> ${escapeHtml(review.toLabel)}
        </p>
        <p class="admin-flagged-match">
          <strong>Matched terms:</strong> ${escapeHtml(review.matchedTerms)}
        </p>
        <div class="admin-flagged-body">${escapeHtml(review.body)}</div>
        ${
          warningHistory
            ? `<ul class="admin-flagged-warnings">${warningHistory}</ul>`
            : `<p class="admin-empty-note">No prior warnings.</p>`
        }
        <div class="admin-flagged-actions">
          <button type="button" class="btn ghost admin-flagged-dismiss-btn">Not hate speech</button>
          <button type="button" class="btn accent admin-flagged-warn-btn" ${canWarn ? "" : "disabled"}>
            Issue warning (${warningCount}/${MAX_WARNINGS} active)
          </button>
          <label class="admin-flagged-ban-label">
            Ban level
            <select class="admin-select admin-flagged-ban-level">${renderBanLevelOptions()}</select>
          </label>
          <button type="button" class="btn accent admin-flagged-ban-btn" ${canBan ? "" : "disabled"}>
            Issue ban
          </button>
          <span class="admin-flagged-row-status"></span>
        </div>
      </article>`;
  }

  function renderList(reviews) {
    if (!reviews.length) {
      els.list.innerHTML = `<p class="admin-empty-note">No messages pending review.</p>`;
      return;
    }

    els.list.innerHTML = reviews.map(renderReview).join("");
  }

  async function refreshBadge() {
    if (!els.navBadge) {
      return;
    }

    try {
      const result = await api.request(`/api/${apiPrefix}/flagged-messages/pending-count`);
      const count = Number(result.count ?? 0);
      els.navBadge.hidden = count <= 0;
      els.navBadge.textContent = String(count);
    } catch {
      els.navBadge.hidden = true;
    }
  }

  async function loadReviews() {
    setStatus(els.status, "Loading…");
    await ensureBanLevels();
    const response = await api.request(`/api/${apiPrefix}/flagged-messages`);
    const reviews = response.messages ?? [];
    renderList(reviews);
    setStatus(els.status, `${reviews.length} pending review(s)`);
    await refreshBadge();
  }

  async function dismissReview(flaggedId, statusEl) {
    setStatus(statusEl, "Updating…");
    try {
      await api.request(`/api/${apiPrefix}/flagged-messages/${flaggedId}/dismiss`, {
        method: "POST",
        body: {},
      });
      await loadReviews();
    } catch (error) {
      setStatus(statusEl, error.message, true);
    }
  }

  async function warnReview(flaggedId, statusEl) {
    setStatus(statusEl, "Issuing warning…");
    try {
      const result = await api.request(`/api/${apiPrefix}/flagged-messages/${flaggedId}/warn`, {
        method: "POST",
        body: {},
      });
      setStatus(statusEl, result.message ?? "Warning issued.", false);
      await loadReviews();
    } catch (error) {
      setStatus(statusEl, error.message, true);
    }
  }

  async function banReview(flaggedId, banLevel, statusEl) {
    setStatus(statusEl, "Applying ban…");
    try {
      const result = await api.request(`/api/${apiPrefix}/flagged-messages/${flaggedId}/ban`, {
        method: "POST",
        body: { banLevel, reason: "Hate speech in message" },
      });
      setStatus(statusEl, result.message ?? "Ban applied.", false);
      await loadReviews();
    } catch (error) {
      setStatus(statusEl, error.message, true);
    }
  }

  els.refreshBtn.addEventListener("click", () => {
    loadReviews().catch((error) => setStatus(els.status, error.message, true));
  });

  els.list.addEventListener("click", async (event) => {
    const card = event.target.closest(".admin-flagged-card");
    if (!card) {
      return;
    }

    const flaggedId = card.dataset.flaggedId;
    const statusEl = card.querySelector(".admin-flagged-row-status");
    if (!flaggedId || !statusEl) {
      return;
    }

    if (event.target.closest(".admin-flagged-profile-btn")) {
      const playerId = card.dataset.playerId;
      if (playerId && onOpenPlayerProfile) {
        await onOpenPlayerProfile(playerId);
      }
      return;
    }

    if (event.target.closest(".admin-flagged-dismiss-btn")) {
      await dismissReview(flaggedId, statusEl);
      return;
    }

    if (event.target.closest(".admin-flagged-warn-btn")) {
      await warnReview(flaggedId, statusEl);
      return;
    }

    if (event.target.closest(".admin-flagged-ban-btn")) {
      const banLevel = card.querySelector(".admin-flagged-ban-level")?.value ?? "1day";
      await banReview(flaggedId, banLevel, statusEl);
    }
  });

  return { loadReviews, refreshBadge };
}
