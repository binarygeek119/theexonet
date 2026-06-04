import { setActionStatus } from "./status-feedback.js?v=20260529-action-feedback";

const CHANNEL_LABELS = {
  "staff-to-staff": "Staff → staff",
  "staff-to-player": "Staff → player",
  "player-to-staff": "Player → staff",
  peer: "Player → player",
  "ban-appeal": "Ban appeal",
};

const MAX_WARNINGS = 2;
const PREVIEW_LENGTH = 120;

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

function previewBody(body, maxLength = PREVIEW_LENGTH) {
  const text = String(body ?? "").trim();
  if (text.length <= maxLength) {
    return text;
  }
  return `${text.slice(0, maxLength)}…`;
}

function highlightMatchedTerms(body, matchedTerms) {
  const text = String(body ?? "");
  const terms = String(matchedTerms ?? "")
    .split(",")
    .map((term) => term.trim())
    .filter(Boolean)
    .sort((left, right) => right.length - left.length);

  if (!terms.length) {
    return escapeHtml(text);
  }

  let html = escapeHtml(text);
  for (const term of terms) {
    const escapedTerm = escapeHtml(term).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const pattern = new RegExp(`\\b(${escapedTerm})\\b`, "gi");
    html = html.replace(pattern, '<mark class="admin-flagged-term">$1</mark>');
  }

  return html;
}

function renderMessageAsSent(review) {
  const sentMeta = review.sourceMessageDeleted
    ? `${formatDate(review.sentAt)} · original removed from inbox; showing saved copy`
    : formatDate(review.sentAt);

  return `
    <section class="admin-flagged-as-sent">
      <p class="admin-flagged-section-label">Message as sent</p>
      <header class="staff-message-detail-top">
        <h4>From ${escapeHtml(review.fromLabel)} · To ${escapeHtml(review.toLabel)}</h4>
        <p class="staff-message-detail-meta">Sent ${escapeHtml(sentMeta)}</p>
      </header>
      <div class="admin-flagged-body admin-flagged-body-full">${highlightMatchedTerms(review.body, review.matchedTerms)}</div>
    </section>`;
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
  let reviews = [];
  let selectedId = null;

  function showFeedback(message, isError = false, rowStatusEl = null) {
    setActionStatus(els.actionStatus, message, isError);
    setStatus(els.status, message, isError);
    if (rowStatusEl) {
      setActionStatus(rowStatusEl, message, isError);
    }
  }

  function getReviewElement(flaggedId) {
    return els.detail?.querySelector(`.admin-flagged-review[data-flagged-id="${flaggedId}"]`) ?? null;
  }

  function setDetailActionsBusy(reviewEl, busy) {
    if (!reviewEl) {
      return;
    }

    reviewEl.querySelectorAll(
      ".admin-flagged-dismiss-btn, .admin-flagged-warn-btn, .admin-flagged-ban-btn, .admin-flagged-ban-level",
    ).forEach((control) => {
      if (busy) {
        control.disabled = true;
        return;
      }

      if (control.matches(".admin-flagged-warn-btn")) {
        control.disabled = control.dataset.canWarn !== "1";
        return;
      }

      if (control.matches(".admin-flagged-ban-btn")) {
        control.disabled = control.dataset.canBan !== "1";
        return;
      }

      control.disabled = false;
    });
  }

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

  function findReview(flaggedId) {
    return reviews.find((review) => review.id === flaggedId) ?? null;
  }

  function renderInbox() {
    if (!els.list) {
      return;
    }

    if (!reviews.length) {
      els.list.innerHTML = `<p class="admin-empty-note">No messages pending review.</p>`;
      return;
    }

    els.list.innerHTML = reviews
      .map((review) => {
        const active = review.id === selectedId;
        const preview = previewBody(review.body);
        return `
          <button
            type="button"
            class="staff-message-item ${active ? "active" : ""}"
            data-flagged-id="${review.id}">
            <span class="staff-message-from">${escapeHtml(review.playerUsername)}</span>
            <span class="staff-message-type">${escapeHtml(formatChannel(review.channel))}</span>
            <span class="staff-message-preview">${escapeHtml(preview)}</span>
            <span class="staff-message-date">${formatDate(review.createdAt)}</span>
          </button>`;
      })
      .join("");
  }

  function renderDetail(review) {
    if (!els.detail) {
      return;
    }

    if (!review) {
      els.detail.innerHTML = `<p class="admin-empty-note">Select a flagged message to review the full text.</p>`;
      return;
    }

    const warningCount = Number(review.playerWarningCount ?? 0);
    const canWarn = warningCount < MAX_WARNINGS;
    const canBan = warningCount >= MAX_WARNINGS;
    const warningHistory = (review.playerWarnings ?? [])
      .map(
        (warning) =>
          `<li class="${warning.isActive ? "active" : "resolved"}">${escapeHtml(formatDate(warning.createdAt))} · ${escapeHtml(warning.issuedByUsername)} · ${escapeHtml(warning.reason)} · ${warning.isActive ? `expires ${formatDate(warning.expiresAt)}` : "expired"}</li>`
      )
      .join("");

    els.detail.innerHTML = `
      <article class="admin-flagged-review" data-flagged-id="${review.id}" data-player-id="${review.playerId ?? ""}">
        <header class="admin-flagged-top">
          <div>
            <h3>${escapeHtml(review.playerUsername)}</h3>
            <p class="admin-flagged-meta">
              ${escapeHtml(formatChannel(review.channel))} · flagged ${escapeHtml(formatDate(review.createdAt))}
            </p>
            <p class="admin-flagged-meta">Active warnings: ${warningCount}/${MAX_WARNINGS}</p>
          </div>
          ${
            review.playerId
              ? `<button type="button" class="btn ghost admin-flagged-profile-btn">View profile</button>`
              : ""
          }
        </header>
        <p class="admin-flagged-match">
          <strong>Matched terms:</strong> ${escapeHtml(review.matchedTerms)}
        </p>
        ${renderMessageAsSent(review)}
        ${
          warningHistory
            ? `<ul class="admin-flagged-warnings">${warningHistory}</ul>`
            : `<p class="admin-empty-note">No prior warnings.</p>`
        }
        <div class="admin-flagged-actions">
          <button type="button" class="btn ghost admin-flagged-dismiss-btn">Not hate speech</button>
          <button
            type="button"
            class="btn accent admin-flagged-warn-btn"
            data-can-warn="${canWarn ? "1" : "0"}"
            ${canWarn ? "" : "disabled"}>
            Issue warning (${warningCount}/${MAX_WARNINGS} active)
          </button>
          <label class="admin-flagged-ban-label">
            Ban level
            <select class="admin-select admin-flagged-ban-level">${renderBanLevelOptions()}</select>
          </label>
          <button
            type="button"
            class="btn accent admin-flagged-ban-btn"
            data-can-ban="${canBan ? "1" : "0"}"
            ${canBan ? "" : "disabled"}>
            Issue ban
          </button>
          <span class="admin-flagged-row-status"></span>
        </div>
      </article>`;
  }

  function syncDisplay() {
    renderInbox();
    renderDetail(findReview(selectedId));
  }

  function selectReview(flaggedId) {
    selectedId = flaggedId;
    syncDisplay();
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

  async function loadReviews(options = {}) {
    const { resetFeedback = true } = options;

    if (resetFeedback) {
      showFeedback("Loading…");
    }

    await ensureBanLevels();
    const response = await api.request(`/api/${apiPrefix}/flagged-messages`);
    reviews = response.messages ?? [];

    if (!selectedId || !findReview(selectedId)) {
      selectedId = reviews[0]?.id ?? null;
    }

    syncDisplay();

    if (resetFeedback) {
      setStatus(els.status, `${reviews.length} pending review(s)`);
      setActionStatus(els.actionStatus, "");
    }

    await refreshBadge();
  }

  function formatRemainingSummary(message) {
    const remaining = reviews.length;
    if (remaining === 0) {
      return `${message} Queue is empty.`;
    }
    if (remaining === 1) {
      return `${message} 1 review remaining.`;
    }
    return `${message} ${remaining} reviews remaining.`;
  }

  function getReviewContext(container) {
    const reviewEl = container?.closest(".admin-flagged-review");
    const flaggedId = reviewEl?.dataset.flaggedId;
    const statusEl = reviewEl?.querySelector(".admin-flagged-row-status");
    const playerUsername = reviewEl?.querySelector("h3")?.textContent?.trim() ?? "Player";
    if (!flaggedId || !statusEl) {
      return null;
    }
    return { flaggedId, statusEl, playerUsername, playerId: reviewEl.dataset.playerId };
  }

  async function dismissReview(flaggedId, statusEl, playerUsername, reviewEl) {
    setDetailActionsBusy(reviewEl, true);
    showFeedback("Updating…", false, statusEl);
    try {
      await api.request(`/api/${apiPrefix}/flagged-messages/${flaggedId}/dismiss`, {
        method: "POST",
        body: {},
      });
      const message = `${playerUsername} — flagged message dismissed (not hate speech).`;
      await loadReviews({ resetFeedback: false });
      showFeedback(formatRemainingSummary(message), false);
    } catch (error) {
      showFeedback(error.message, true, statusEl);
      setDetailActionsBusy(reviewEl, false);
    }
  }

  async function warnReview(flaggedId, statusEl, playerUsername, reviewEl) {
    setDetailActionsBusy(reviewEl, true);
    showFeedback("Issuing warning…", false, statusEl);
    try {
      const result = await api.request(`/api/${apiPrefix}/flagged-messages/${flaggedId}/warn`, {
        method: "POST",
        body: {},
      });
      const message =
        result.message ??
        `Warning issued to ${playerUsername} (${result.review?.playerWarningCount ?? "?"}/${MAX_WARNINGS} active).`;
      await loadReviews({ resetFeedback: false });
      showFeedback(formatRemainingSummary(message), false);
    } catch (error) {
      showFeedback(error.message, true, statusEl);
      setDetailActionsBusy(reviewEl, false);
    }
  }

  async function banReview(flaggedId, banLevel, statusEl, playerUsername, reviewEl) {
    setDetailActionsBusy(reviewEl, true);
    showFeedback("Applying ban…", false, statusEl);
    try {
      const result = await api.request(`/api/${apiPrefix}/flagged-messages/${flaggedId}/ban`, {
        method: "POST",
        body: { banLevel, reason: "Hate speech in message" },
      });
      const message =
        result.message ??
        `${playerUsername} banned for ${result.ban?.banLevelLabel ?? banLevel}.`;
      await loadReviews({ resetFeedback: false });
      showFeedback(formatRemainingSummary(message), false);
    } catch (error) {
      showFeedback(error.message, true, statusEl);
      setDetailActionsBusy(reviewEl, false);
    }
  }

  els.refreshBtn.addEventListener("click", () => {
    loadReviews().catch((error) => setStatus(els.status, error.message, true));
  });

  els.list?.addEventListener("click", (event) => {
    const button = event.target.closest(".staff-message-item");
    const flaggedId = button?.dataset.flaggedId;
    if (flaggedId) {
      selectReview(flaggedId);
    }
  });

  els.detail?.addEventListener("click", async (event) => {
    const context = getReviewContext(event.target);
    if (!context) {
      return;
    }

    const { flaggedId, statusEl, playerUsername, playerId } = context;
    const reviewEl = getReviewElement(flaggedId) ?? event.target.closest(".admin-flagged-review");

    if (event.target.closest(".admin-flagged-profile-btn")) {
      if (playerId && onOpenPlayerProfile) {
        await onOpenPlayerProfile(playerId);
      }
      return;
    }

    if (event.target.closest(".admin-flagged-dismiss-btn")) {
      await dismissReview(flaggedId, statusEl, playerUsername, reviewEl);
      return;
    }

    if (event.target.closest(".admin-flagged-warn-btn")) {
      await warnReview(flaggedId, statusEl, playerUsername, reviewEl);
      return;
    }

    if (event.target.closest(".admin-flagged-ban-btn")) {
      const banLevel = reviewEl?.querySelector(".admin-flagged-ban-level")?.value ?? "1day";
      await banReview(flaggedId, banLevel, statusEl, playerUsername, reviewEl);
    }
  });

  return { loadReviews, refreshBadge };
}
