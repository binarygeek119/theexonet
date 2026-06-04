import { RavaApi } from "./api.js?v=20260529-testing-actions";
import { API_BASE_URL, readMetaApiBase } from "./config.js";
import { initApiStatusMonitor } from "./api-status.js";
import { initStaffMessaging } from "./staff-messages.js";
import { initStaffPlayerMessaging } from "./staff-player-messages.js";
import { initAdminMessagesHub } from "./admin-messages-hub.js";
import { initFlaggedMessages } from "./flagged-messages.js?v=20260529-flagged-action-feedback";
import { renderSocialLinksHtml } from "./profile-social.js";
import {
  formatRaxHtml,
  formatRaxPlain,
  setRaxHtml,
  RAX_NAME,
  formatRewardAmount,
} from "./currency.js";
import { initI18n, applyTranslations, wireLocaleSelectors } from "./i18n.js?v=20260529-locale-fix";
import {
  clearRemovedDummyFriendships,
  getDummyPlayerProfile,
  getDummyPlayerSummaries,
  isDummyPlayerId,
  mergePlayersForDisplay,
  saveTestingModeEnabled,
  setCachedTestingModeEnabled,
} from "./admin-testing-mode.js?v=20260529-testing-mode-server";
import { setActionStatus } from "./status-feedback.js?v=20260529-action-feedback";
import {
  getResolvedBanReason,
  populateBanReasonPresets,
  resetBanReasonForm,
  wireBanReasonForm,
} from "./ban-reason-ui.js?v=20260529-ban-reasons";

const api = new RavaApi(API_BASE_URL);

const els = {
  loginScreen: document.getElementById("admin-login-screen"),
  deniedScreen: document.getElementById("admin-denied-screen"),
  portalScreen: document.getElementById("admin-portal-screen"),
  username: document.getElementById("admin-username"),
  password: document.getElementById("admin-password"),
  loginStatus: document.getElementById("admin-login-status"),
  loginBtn: document.getElementById("admin-login-btn"),
  deniedUser: document.getElementById("admin-denied-user"),
  deniedLogoutBtn: document.getElementById("admin-denied-logout-btn"),
  signedIn: document.getElementById("admin-signed-in"),
  logoutBtn: document.getElementById("admin-logout-btn"),
  navButtons: document.querySelectorAll("[data-admin-page]"),
  pageDashboard: document.getElementById("admin-page-dashboard"),
  pageTesting: document.getElementById("admin-page-testing"),
  pagePlayers: document.getElementById("admin-page-players"),
  pageBans: document.getElementById("admin-page-bans"),
  pageAppeals: document.getElementById("admin-page-appeals"),
  pageMessages: document.getElementById("admin-page-messages"),
  pageMessageLog: document.getElementById("admin-page-message-log"),
  pageFlagged: document.getElementById("admin-page-flagged"),
  pageCredits: document.getElementById("admin-page-credits"),
  pageEvents: document.getElementById("admin-page-events"),
  pageOffworldNews: document.getElementById("admin-page-offworld-news"),
  eventsStatus: document.getElementById("admin-events-status"),
  eventsList: document.getElementById("admin-events-list"),
  eventsNewBtn: document.getElementById("admin-events-new-btn"),
  eventEditor: document.getElementById("admin-event-editor"),
  eventEditorTitle: document.getElementById("admin-event-editor-title"),
  eventForm: document.getElementById("admin-event-form"),
  eventTitle: document.getElementById("admin-event-title"),
  eventMessage: document.getElementById("admin-event-message"),
  eventActive: document.getElementById("admin-event-active"),
  eventStarts: document.getElementById("admin-event-starts"),
  eventEnds: document.getElementById("admin-event-ends"),
  eventChallengeType: document.getElementById("admin-event-challenge-type"),
  eventChallengeTarget: document.getElementById("admin-event-challenge-target"),
  eventChallengeDetail: document.getElementById("admin-event-challenge-detail"),
  eventSaleBonus: document.getElementById("admin-event-sale-bonus"),
  eventTradeBonus: document.getElementById("admin-event-trade-bonus"),
  eventRewards: document.getElementById("admin-event-rewards"),
  eventAddRewardBtn: document.getElementById("admin-event-add-reward-btn"),
  eventDeleteBtn: document.getElementById("admin-event-delete-btn"),
  eventCancelBtn: document.getElementById("admin-event-cancel-btn"),
  eventFormStatus: document.getElementById("admin-event-form-status"),
  stats: document.getElementById("admin-stats"),
  offworldNewsSummary: document.getElementById("admin-offworld-news-summary"),
  offworldNewsStatus: document.getElementById("admin-offworld-news-status"),
  offworldNewsRegenEditionBtn: document.getElementById("admin-offworld-news-regen-edition-btn"),
  offworldNewsRegenImagesBtn: document.getElementById("admin-offworld-news-regen-images-btn"),
  offworldNewsRegenReporterPortraitsBtn: document.getElementById(
    "admin-offworld-news-regen-reporter-portraits-btn",
  ),
  onnReportersPath: document.getElementById("admin-onn-reporters-path"),
  onnPoolForm: document.getElementById("admin-onn-pool-form"),
  onnPoolSize: document.getElementById("admin-onn-pool-size"),
  onnPoolSaveBtn: document.getElementById("admin-onn-pool-save-btn"),
  onnPoolStatus: document.getElementById("admin-onn-pool-status"),
  onnReportersStatus: document.getElementById("admin-onn-reporters-status"),
  onnReportersList: document.getElementById("admin-onn-reporters-list"),
  testingModeToggle: document.getElementById("admin-testing-mode-toggle"),
  testingModeHint: document.getElementById("admin-testing-mode-hint"),
  testingStatus: document.getElementById("admin-testing-status"),
  testingDummyList: document.getElementById("admin-testing-dummy-list"),
  testingActions: document.getElementById("admin-testing-actions"),
  testingDummySelect: document.getElementById("admin-testing-dummy-select"),
  testingActionsStatus: document.getElementById("admin-testing-actions-status"),
  testingStaffMessageBtn: document.getElementById("admin-testing-staff-message-btn"),
  testingPeerMessageBtn: document.getElementById("admin-testing-peer-message-btn"),
  testingFlaggedMessageBtn: document.getElementById("admin-testing-flagged-message-btn"),
  testingBanAppealBtn: document.getElementById("admin-testing-ban-appeal-btn"),
  profileTestingBanner: document.getElementById("admin-profile-testing-banner"),
  playerSearch: document.getElementById("admin-player-search"),
  playerSearchBtn: document.getElementById("admin-player-search-btn"),
  playersStatus: document.getElementById("admin-players-status"),
  playersBody: document.getElementById("admin-players-body"),
  creditsSearch: document.getElementById("admin-credits-search"),
  creditsSearchBtn: document.getElementById("admin-credits-search-btn"),
  creditsStatus: document.getElementById("admin-credits-status"),
  creditsBody: document.getElementById("admin-credits-body"),
  gameCreditsPath: document.getElementById("admin-game-credits-path"),
  gameCreditsForm: document.getElementById("admin-game-credits-form"),
  gameCreditsSignUp: document.getElementById("admin-game-credits-signup"),
  gameCreditsBirthday: document.getElementById("admin-game-credits-birthday"),
  gameCreditsReclaim: document.getElementById("admin-game-credits-reclaim"),
  gameCreditsSaveBtn: document.getElementById("admin-game-credits-save-btn"),
  gameCreditsResetBtn: document.getElementById("admin-game-credits-reset-btn"),
  gameCreditsStatus: document.getElementById("admin-game-credits-status"),
  profileModal: document.getElementById("admin-profile-modal"),
  profileCloseBtn: document.getElementById("admin-profile-close-btn"),
  profileAvatar: document.getElementById("admin-profile-avatar"),
  profileAvatarImg: document.getElementById("admin-profile-avatar-img"),
  profileAvatarInitials: document.getElementById("admin-profile-avatar-initials"),
  profileUsername: document.getElementById("admin-profile-username"),
  profileMood: document.getElementById("admin-profile-mood"),
  profileNumber: document.getElementById("admin-profile-number"),
  profileEmail: document.getElementById("admin-profile-email"),
  profileMemberSince: document.getElementById("admin-profile-member-since"),
  profileAbout: document.getElementById("admin-profile-about"),
  profileInterests: document.getElementById("admin-profile-interests"),
  profileMusic: document.getElementById("admin-profile-music"),
  profileSocial: document.getElementById("admin-profile-social"),
  profileEmailStat: document.getElementById("admin-profile-email-stat"),
  profileBirthday: document.getElementById("admin-profile-birthday"),
  profileTheme: document.getElementById("admin-profile-theme"),
  profileCredits: document.getElementById("admin-profile-credits"),
  profileGameDay: document.getElementById("admin-profile-game-day"),
  profileMine: document.getElementById("admin-profile-mine"),
  profileWorkers: document.getElementById("admin-profile-workers"),
  profileZones: document.getElementById("admin-profile-zones"),
  profileMineCount: document.getElementById("admin-profile-mine-count"),
  profileActiveFlag: document.getElementById("admin-profile-active-flag"),
  profileActiveFlagComment: document.getElementById("admin-profile-active-flag-comment"),
  profileActiveFlagMeta: document.getElementById("admin-profile-active-flag-meta"),
  profileFlagCommentInput: document.getElementById("admin-profile-flag-comment-input"),
  profileFlagBtn: document.getElementById("admin-profile-flag-btn"),
  profileFlagStatus: document.getElementById("admin-profile-flag-status"),
  profileFlagHistory: document.getElementById("admin-profile-flag-history"),
  profileFlagBox: document.querySelector("#admin-profile-modal .admin-profile-flag-box"),
  profileActiveBan: document.getElementById("admin-profile-active-ban"),
  profileActiveBanSummary: document.getElementById("admin-profile-active-ban-summary"),
  profileActiveBanMeta: document.getElementById("admin-profile-active-ban-meta"),
  profileBanLevel: document.getElementById("admin-profile-ban-level"),
  profileBanReasonPreset: document.getElementById("admin-profile-ban-reason-preset"),
  profileBanReasonLabel: document.getElementById("admin-profile-ban-reason-label"),
  profileBanReason: document.getElementById("admin-profile-ban-reason"),
  profileBanBtn: document.getElementById("admin-profile-ban-btn"),
  profileUnbanBtn: document.getElementById("admin-profile-unban-btn"),
  profileBanStatus: document.getElementById("admin-profile-ban-status"),
  profileBanHistory: document.getElementById("admin-profile-ban-history"),
  profileBanBox: document.querySelector("#admin-profile-modal .admin-profile-ban-box"),
  profileMessageInput: document.getElementById("admin-profile-message-input"),
  profileMessageBtn: document.getElementById("admin-profile-message-btn"),
  profileMessageStatus: document.getElementById("admin-profile-message-status"),
  profileMessageHistory: document.getElementById("admin-profile-message-history"),
  appealsStatus: document.getElementById("admin-appeals-status"),
  appealsList: document.getElementById("admin-appeals-list"),
  appealsRefreshBtn: document.getElementById("admin-appeals-refresh-btn"),
  bansSearch: document.getElementById("admin-bans-search"),
  bansActiveOnly: document.getElementById("admin-bans-active-only"),
  bansSearchBtn: document.getElementById("admin-bans-search-btn"),
  bansRefreshBtn: document.getElementById("admin-bans-refresh-btn"),
  bansStatus: document.getElementById("admin-bans-status"),
  bansBody: document.getElementById("admin-bans-body"),
  messagesStatus: document.getElementById("admin-messages-status"),
  messagesRecipient: document.getElementById("admin-messages-recipient"),
  messagesBody: document.getElementById("admin-messages-body"),
  messagesSendBtn: document.getElementById("admin-messages-send-btn"),
  messagesRefreshBtn: document.getElementById("admin-messages-refresh-btn"),
  messagesUnifiedInbox: document.getElementById("admin-messages-unified-inbox"),
  messagesUnifiedDetail: document.getElementById("admin-messages-unified-detail"),
  messagesFilterButtons: document.querySelectorAll("[data-message-filter]"),
  messagesNavBadge: document.getElementById("admin-messages-nav-badge"),
  messageLogSearch: document.getElementById("admin-message-log-search"),
  messageLogChannel: document.getElementById("admin-message-log-channel"),
  messageLogSearchBtn: document.getElementById("admin-message-log-search-btn"),
  messageLogRefreshBtn: document.getElementById("admin-message-log-refresh-btn"),
  messageLogStatus: document.getElementById("admin-message-log-status"),
  messageLogBody: document.getElementById("admin-message-log-body"),
  flaggedStatus: document.getElementById("admin-flagged-status"),
  flaggedRefreshBtn: document.getElementById("admin-flagged-refresh-btn"),
  flaggedList: document.getElementById("admin-flagged-list"),
  flaggedNavBadge: document.getElementById("admin-flagged-nav-badge"),
  profileWarningSummary: document.getElementById("admin-profile-warning-summary"),
  profileWarningReason: document.getElementById("admin-profile-warning-reason"),
  profileWarningBtn: document.getElementById("admin-profile-warning-btn"),
  profileWarningStatus: document.getElementById("admin-profile-warning-status"),
  profileWarningHistory: document.getElementById("admin-profile-warning-history"),
};

let messagesHub;

const staffMessaging = initStaffMessaging({
  api,
  els: {
    messagesStatus: document.getElementById("admin-messages-status"),
    messagesRecipient: document.getElementById("admin-messages-recipient"),
    messagesBody: document.getElementById("admin-messages-body"),
    messagesSendBtn: document.getElementById("admin-messages-send-btn"),
    messagesRefreshBtn: document.getElementById("admin-messages-refresh-btn"),
    messagesNavBadge: document.getElementById("admin-messages-nav-badge"),
  },
  setStatus,
  skipInboxRender: true,
  onInboxUpdated: () => messagesHub?.syncDisplay(),
  onRefresh: () => messagesHub?.refresh(),
});

messagesHub = initAdminMessagesHub({
  api,
  els: {
    status: document.getElementById("admin-messages-status"),
    inbox: document.getElementById("admin-messages-unified-inbox"),
    detail: document.getElementById("admin-messages-unified-detail"),
    filterButtons: document.querySelectorAll("[data-message-filter]"),
  },
  setStatus,
  staffMessaging,
});

const flaggedMessaging = initFlaggedMessages({
  api,
  apiPrefix: "admin",
  els: {
    status: document.getElementById("admin-flagged-status"),
    actionStatus: document.getElementById("admin-flagged-action-status"),
    refreshBtn: document.getElementById("admin-flagged-refresh-btn"),
    list: document.getElementById("admin-flagged-inbox"),
    detail: document.getElementById("admin-flagged-detail"),
    navBadge: document.getElementById("admin-flagged-nav-badge"),
  },
  setStatus,
  getBanLevels: () => ensureBanLevelsLoaded().then(() => state.banLevels),
  onOpenPlayerProfile: (playerId) => openPlayerProfile(playerId),
});

const staffPlayerMessaging = initStaffPlayerMessaging({
  api,
  els: {
    profileMessageInput: document.getElementById("admin-profile-message-input"),
    profileMessageBtn: document.getElementById("admin-profile-message-btn"),
    profileMessageStatus: document.getElementById("admin-profile-message-status"),
    profileMessageHistory: document.getElementById("admin-profile-message-history"),
  },
  getPlayerId: () => state.profilePlayerId,
  setStatus: (el, message, isError) => {
    el.textContent = message ?? "";
    el.classList.toggle("error", Boolean(isError && message));
    el.classList.toggle("success", Boolean(!isError && message));
  },
});

const state = {
  page: "dashboard",
  dashboard: null,
  players: [],
  profilePlayerId: null,
  banLevels: [],
  banReasonPresets: [],
  gameCreditsConfig: null,
  events: [],
  editingEventId: null,
  testingMode: false,
};

function setStatus(el, message, isError = false) {
  setActionStatus(el, message, isError);
}

function formatCredits(value) {
  return formatRaxHtml(value);
}

function formatDate(value) {
  if (!value) {
    return "—";
  }
  return new Date(value).toLocaleString();
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function showScreen(screen) {
  els.loginScreen.hidden = screen !== "login";
  els.deniedScreen.hidden = screen !== "denied";
  els.portalScreen.hidden = screen !== "portal";
  document.body.classList.toggle("is-authenticated", screen === "portal");
}

function prefillLoginUsername() {
  if (!els.username.value && api.username) {
    els.username.value = api.username;
  }
}

const adminPages = () => document.querySelectorAll("#admin-portal-screen .admin-page");

function showPage(page) {
  state.page = page;
  const activeId = `admin-page-${page}`;

  adminPages().forEach((section) => {
    const isActive = section.id === activeId;
    section.classList.toggle("admin-page-active", isActive);
    section.hidden = !isActive;
  });

  els.navButtons.forEach((button) => {
    button.classList.toggle("active", button.dataset.adminPage === page);
  });
}

function renderStats(dashboard) {
  const items = [
    ["Players", dashboard.playerCount],
    ["Mines", dashboard.mineCount],
    ["Friendships", dashboard.friendshipCount],
    ["Game day", dashboard.currentGameDay],
    [`Total ${RAX_NAME}`, formatCredits(dashboard.totalCredits)],
    [`Sign-up ${RAX_NAME}`, formatCredits(dashboard.signUpCredits)],
    ["Birthday bonus", formatCredits(dashboard.birthdayBonus)],
  ];

  els.stats.innerHTML = items
    .map(
      ([label, value]) => `
        <div class="admin-stat">
          <p class="admin-stat-label">${label}</p>
          <p class="admin-stat-value">${value}</p>
        </div>`
    )
    .join("");
}

function formatBirthday(value) {
  if (!value) {
    return "—";
  }

  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

function profileInitials(username) {
  const parts = (username ?? "").trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return "??";
  }
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
}

function setProfileText(el, value, emptyText) {
  const text = (value ?? "").trim();
  el.textContent = text || emptyText;
  el.classList.toggle("empty", !text);
}

function isTestingDummyProfile(profile) {
  return Boolean(profile?.isTestingDummy) || isDummyPlayerId(profile?.id);
}

function renderTestingModeUi() {
  if (els.testingModeToggle) {
    els.testingModeToggle.checked = state.testingMode;
  }
  if (els.testingModeHint) {
    els.testingModeHint.hidden = !state.testingMode;
  }
}

function setTestingMode(enabled) {
  saveTestingModeEnabled(api, enabled)
    .then((response) => {
      state.testingMode = Boolean(response?.enabled);
      clearRemovedDummyFriendships();
      renderTestingModeUi();
      if (els.testingStatus) {
        setStatus(
          els.testingStatus,
          state.testingMode
            ? "Testing mode on — dummy players appear on Players and Rax, and are auto-friended in-game."
            : "Testing mode off.",
        );
      }
      if (state.page === "players") {
        loadPlayers().catch((error) => setStatus(els.playersStatus, error.message, true));
      } else if (state.page === "credits") {
        loadCreditsPage().catch((error) => setStatus(els.creditsStatus, error.message, true));
      } else if (state.page === "testing") {
        loadTestingPage();
      }
    })
    .catch((error) => {
      if (els.testingStatus) {
        setStatus(els.testingStatus, error.message, true);
      }
      renderTestingModeUi();
    });
}

function applyPlayersForDisplay(realPlayers, search) {
  return mergePlayersForDisplay(state.testingMode, search, realPlayers);
}

function renderAdminProfile(profile) {
  const isDummy = isTestingDummyProfile(profile);
  if (els.profileTestingBanner) {
    els.profileTestingBanner.hidden = !isDummy;
  }

  const imageUrl = profile.profileImageUrl;
  if (imageUrl) {
    els.profileAvatarImg.src = imageUrl;
    els.profileAvatarImg.hidden = false;
    els.profileAvatar.classList.add("has-photo");
  } else {
    els.profileAvatarImg.removeAttribute("src");
    els.profileAvatarImg.hidden = true;
    els.profileAvatar.classList.remove("has-photo");
  }

  els.profileAvatarInitials.textContent = profileInitials(profile.username);
  els.profileUsername.textContent = profile.username;
  els.profileMood.textContent = profile.mood || "No mood set.";
  els.profileNumber.textContent = profile.profileNumber
    ? `Profile #: ${profile.profileNumber}`
    : "Profile #: —";
  els.profileEmail.textContent = profile.email;
  els.profileMemberSince.textContent = `Joined ${formatDate(profile.memberSince)}`;
  setProfileText(els.profileAbout, profile.aboutMe, "No bio yet.");
  setProfileText(els.profileInterests, profile.interests, "Nothing listed yet.");
  setProfileText(els.profileMusic, profile.music, "Silence in the void.");
  if (els.profileSocial) {
    els.profileSocial.innerHTML = renderSocialLinksHtml(profile);
  }
  els.profileEmailStat.textContent = profile.email || "—";
  els.profileBirthday.textContent = formatBirthday(profile.birthday);
  els.profileTheme.textContent = profile.theme || "classic";
  setRaxHtml(els.profileCredits, profile.credits);
  els.profileGameDay.textContent = String(profile.currentGameDay ?? "—");
  els.profileMine.textContent = profile.mineName || "—";
  els.profileWorkers.textContent = String(profile.workerCount ?? 0);
  els.profileZones.textContent = String(profile.zoneCount ?? 0);
  els.profileMineCount.textContent = String(profile.mineCount ?? 0);
  const isProtectedAdmin = Boolean(profile.isProtectedAdmin);
  const hideModeration = isProtectedAdmin || isDummy;
  if (els.profileFlagBox) {
    els.profileFlagBox.hidden = hideModeration;
  }
  if (els.profileBanBox) {
    els.profileBanBox.hidden = hideModeration;
  }
  if (document.querySelector("#admin-profile-modal .admin-profile-warning-box")) {
    document.querySelector("#admin-profile-modal .admin-profile-warning-box").hidden = hideModeration;
  }
  const messageBox = document.querySelector("#admin-profile-modal .admin-profile-message-box");
  if (messageBox) {
    messageBox.hidden = hideModeration;
  }
  if (!hideModeration) {
    renderProfileFlags(profile);
    renderProfileWarnings(profile);
    renderProfileBans(profile);
  }
}

function formatBanSummary(ban) {
  if (!ban) {
    return "";
  }
  if (ban.isPermanent) {
    return "Life ban";
  }
  if (ban.expiresAt) {
    return `${ban.banLevelLabel} · until ${formatDate(ban.expiresAt)}`;
  }
  return ban.banLevelLabel;
}

function formatPlayerBanStatus(player) {
  const ban = player.activeBan;
  if (!ban?.isActive) {
    return "—";
  }
  if (ban.isPermanent) {
    return '<span class="admin-ban-badge permanent">Life ban</span>';
  }
  return `<span class="admin-ban-badge">Until ${formatDate(ban.expiresAt)}</span>`;
}

function formatBanRecordStatus(ban) {
  if (!ban) {
    return "—";
  }
  if (ban.isActive) {
    if (ban.isPermanent) {
      return '<span class="admin-ban-badge permanent">Active · life ban</span>';
    }
    return `<span class="admin-ban-badge">Active · until ${formatDate(ban.expiresAt)}</span>`;
  }
  if (ban.liftedAt) {
    return `<span class="admin-ban-badge lifted">Lifted ${formatDate(ban.liftedAt)}</span>`;
  }
  if (ban.expiresAt) {
    return `<span class="admin-ban-badge expired">Expired ${formatDate(ban.expiresAt)}</span>`;
  }
  return '<span class="admin-ban-badge expired">Inactive</span>';
}

function formatBanReason(reason) {
  const text = String(reason ?? "").trim();
  return text ? escapeHtml(text) : '<span class="admin-page-desc">No reason provided</span>';
}

async function ensureBanLevelsLoaded() {
  if (state.banLevels.length) {
    return;
  }

  state.banLevels = await api.adminBanLevels();
  els.profileBanLevel.innerHTML = state.banLevels
    .map(
      (level) =>
        `<option value="${escapeHtml(level.code)}">${escapeHtml(level.label)}</option>`
    )
    .join("");
}

async function ensureBanReasonPresetsLoaded() {
  if (state.banReasonPresets.length) {
    return;
  }

  const response = await api.adminBanReasonPresets();
  state.banReasonPresets = response.presets ?? [];
  populateBanReasonPresets(els.profileBanReasonPreset, state.banReasonPresets);
  wireBanReasonForm(
    els.profileBanReasonPreset,
    els.profileBanReason,
    els.profileBanReasonLabel,
  );
}

async function ensureBanFormLoaded() {
  await ensureBanLevelsLoaded();
  await ensureBanReasonPresetsLoaded();
}

function renderProfileBans(profile) {
  const activeBan = profile.activeBan;
  if (activeBan?.isActive) {
    els.profileActiveBan.hidden = false;
    els.profileActiveBanSummary.textContent = formatBanSummary(activeBan);
    const reason = activeBan.reason ? `Reason: ${activeBan.reason}` : "No reason provided.";
    els.profileActiveBanMeta.textContent =
      `${reason} · By ${activeBan.bannedByUsername} on ${formatDate(activeBan.createdAt)}`;
    els.profileUnbanBtn.disabled = false;
  } else {
    els.profileActiveBan.hidden = true;
    els.profileActiveBanSummary.textContent = "";
    els.profileActiveBanMeta.textContent = "";
    els.profileUnbanBtn.disabled = true;
  }

  const history = profile.banHistory ?? [];
  if (!history.length) {
    els.profileBanHistory.innerHTML = "";
    return;
  }

  els.profileBanHistory.innerHTML = `
    <p class="admin-profile-flag-label">Ban history</p>
    <ul class="admin-profile-flag-list">
      ${history
        .map((ban) => {
          const status = ban.isActive
            ? "active"
            : ban.liftedAt
              ? "resolved"
              : "expired";
          const statusText = ban.isActive
            ? "active"
            : ban.liftedAt
              ? `lifted ${formatDate(ban.liftedAt)}`
              : ban.expiresAt
                ? `expired ${formatDate(ban.expiresAt)}`
                : "ended";
          return `
            <li class="${status}">
              <p class="admin-profile-flag-comment">${escapeHtml(formatBanSummary(ban))}</p>
              <p class="admin-profile-flag-meta">
                ${escapeHtml(ban.bannedByUsername)} · ${formatDate(ban.createdAt)} · ${statusText}
                ${ban.reason ? ` · ${escapeHtml(ban.reason)}` : ""}
              </p>
            </li>`;
        })
        .join("")}
    </ul>`;
}

function setProfileBanStatus(message, isError = false) {
  els.profileBanStatus.textContent = message ?? "";
  els.profileBanStatus.classList.toggle("error", Boolean(isError && message));
  els.profileBanStatus.classList.toggle("success", Boolean(!isError && message));
}

async function submitProfileBan() {
  if (!state.profilePlayerId || isDummyPlayerId(state.profilePlayerId)) {
    return;
  }

  const banLevel = els.profileBanLevel.value;
  if (!banLevel) {
    setProfileBanStatus("Select a ban level.", true);
    return;
  }

  const reason = getResolvedBanReason(els.profileBanReasonPreset, els.profileBanReason);
  if (!reason) {
    setProfileBanStatus("Select or enter a ban reason for the player.", true);
    return;
  }

  els.profileBanBtn.disabled = true;
  setProfileBanStatus("Applying ban...");
  try {
    const result = await api.adminBanPlayer(
      state.profilePlayerId,
      banLevel,
      reason
    );
    resetBanReasonForm(
      els.profileBanReasonPreset,
      els.profileBanReason,
      els.profileBanReasonLabel,
    );
    setProfileBanStatus(result.message, false);
    const profile = await api.adminPlayerProfile(state.profilePlayerId);
    renderAdminProfile(profile);
    await loadPlayers();
    if (state.page === "bans") {
      await loadBansPage();
    }
  } catch (error) {
    setProfileBanStatus(error.message, true);
  } finally {
    els.profileBanBtn.disabled = false;
  }
}

async function liftProfileBan() {
  if (!state.profilePlayerId || isDummyPlayerId(state.profilePlayerId)) {
    return;
  }

  els.profileUnbanBtn.disabled = true;
  setProfileBanStatus("Lifting ban...");
  try {
    const result = await api.adminUnbanPlayer(state.profilePlayerId);
    setProfileBanStatus(result.message, false);
    const profile = await api.adminPlayerProfile(state.profilePlayerId);
    renderAdminProfile(profile);
    await loadPlayers();
    if (state.page === "bans") {
      await loadBansPage();
    }
  } catch (error) {
    setProfileBanStatus(error.message, true);
  } finally {
    els.profileUnbanBtn.disabled = false;
  }
}

function renderProfileFlags(profile) {
  const activeFlag = profile.activeFlag;
  if (activeFlag) {
    els.profileActiveFlag.hidden = false;
    els.profileActiveFlagComment.textContent = activeFlag.comment;
    els.profileActiveFlagMeta.textContent =
      `Flagged by ${activeFlag.flaggedByUsername} on ${formatDate(activeFlag.createdAt)}`;
  } else {
    els.profileActiveFlag.hidden = true;
    els.profileActiveFlagComment.textContent = "";
    els.profileActiveFlagMeta.textContent = "";
  }

  const history = profile.flagHistory ?? [];
  if (!history.length) {
    els.profileFlagHistory.innerHTML = "";
    return;
  }

  els.profileFlagHistory.innerHTML = `
    <p class="admin-profile-flag-label">Flag history</p>
    <ul class="admin-profile-flag-list">
      ${history
        .map(
          (flag) => `
            <li class="${flag.resolvedAt ? "resolved" : "active"}">
              <p class="admin-profile-flag-comment">${escapeHtml(flag.comment)}</p>
              <p class="admin-profile-flag-meta">
                ${escapeHtml(flag.flaggedByUsername)} · ${formatDate(flag.createdAt)}
                ${flag.resolvedAt ? ` · resolved ${formatDate(flag.resolvedAt)}` : " · active"}
              </p>
            </li>`
        )
        .join("")}
    </ul>`;
}

function renderProfileWarnings(profile) {
  const warningCount = Number(profile.warningCount ?? 0);
  els.profileWarningSummary.textContent = `Active warnings: ${warningCount}/2`;
  const canWarn = warningCount < 2;
  if (els.profileWarningBtn) {
    els.profileWarningBtn.disabled = !canWarn;
  }
  const history = profile.warningHistory ?? [];
  if (!history.length) {
    els.profileWarningHistory.innerHTML = `<p class="admin-empty-note">No warnings on record.</p>`;
    return;
  }

  els.profileWarningHistory.innerHTML = `
    <ul class="admin-profile-flag-list">
      ${history
        .map(
          (warning) => `
            <li class="${warning.isActive ? "active" : "resolved"}">
              <p class="admin-profile-flag-comment">${escapeHtml(warning.reason)}</p>
              <p class="admin-profile-flag-meta">
                ${escapeHtml(warning.issuedByUsername)} · ${formatDate(warning.createdAt)}
                · ${warning.isActive ? `expires ${formatDate(warning.expiresAt)}` : `expired ${formatDate(warning.expiresAt)}`}
              </p>
            </li>`
        )
        .join("")}
    </ul>`;
}

function setProfileWarningStatus(message, isError = false) {
  els.profileWarningStatus.textContent = message ?? "";
  els.profileWarningStatus.classList.toggle("error", Boolean(isError && message));
  els.profileWarningStatus.classList.toggle("success", Boolean(!isError && message));
}

async function submitProfileWarning() {
  if (!state.profilePlayerId || isDummyPlayerId(state.profilePlayerId)) {
    return;
  }

  const reason = els.profileWarningReason.value.trim();
  if (!reason) {
    setProfileWarningStatus("Enter a reason for the warning.", true);
    return;
  }

  els.profileWarningBtn.disabled = true;
  setProfileWarningStatus("Issuing warning...");
  try {
    const result = await api.adminWarnPlayer(state.profilePlayerId, reason);
    els.profileWarningReason.value = "";
    setProfileWarningStatus(result.message, false);
    const profile = await api.adminPlayerProfile(state.profilePlayerId);
    renderAdminProfile(profile);
  } catch (error) {
    setProfileWarningStatus(error.message, true);
  } finally {
    els.profileWarningBtn.disabled = false;
  }
}

function setProfileFlagStatus(message, isError = false) {
  els.profileFlagStatus.textContent = message ?? "";
  els.profileFlagStatus.classList.toggle("error", Boolean(isError && message));
  els.profileFlagStatus.classList.toggle("success", Boolean(!isError && message));
}

async function submitProfileFlag() {
  if (!state.profilePlayerId || isDummyPlayerId(state.profilePlayerId)) {
    return;
  }

  const comment = els.profileFlagCommentInput.value.trim();
  if (!comment) {
    setProfileFlagStatus("Enter a comment explaining why this profile was flagged.", true);
    return;
  }

  els.profileFlagBtn.disabled = true;
  setProfileFlagStatus("Flagging profile...");
  try {
    const result = await api.adminFlagProfile(state.profilePlayerId, comment);
    els.profileFlagCommentInput.value = "";
    setProfileFlagStatus(result.message, false);
    const profile = await api.adminPlayerProfile(state.profilePlayerId);
    renderAdminProfile(profile);
  } catch (error) {
    setProfileFlagStatus(error.message, true);
  } finally {
    els.profileFlagBtn.disabled = false;
  }
}

function openAdminProfileModal() {
  els.profileModal.hidden = false;
  document.body.appendChild(els.profileModal);
}

function closeAdminProfileModal() {
  els.profileModal.hidden = true;
}

function profileOpenStatusElement() {
  if (state.page === "credits") {
    return els.creditsStatus;
  }
  if (state.page === "testing") {
    return els.testingStatus;
  }
  return els.playersStatus;
}

async function openPlayerProfile(playerId) {
  const statusEl = profileOpenStatusElement();
  try {
    if (isDummyPlayerId(playerId)) {
      const profile = getDummyPlayerProfile(playerId);
      if (!profile) {
        setStatus(statusEl, "Testing profile not found.", true);
        return;
      }

      state.profilePlayerId = playerId;
      els.profileFlagCommentInput.value = "";
      resetBanReasonForm(
        els.profileBanReasonPreset,
        els.profileBanReason,
        els.profileBanReasonLabel,
      );
      staffPlayerMessaging.clearForm();
      setProfileFlagStatus("");
      setProfileWarningStatus("");
      if (els.profileWarningReason) {
        els.profileWarningReason.value = "";
      }
      setProfileBanStatus("");
      renderAdminProfile(profile);
      if (els.profileMessageHistory) {
        els.profileMessageHistory.innerHTML =
          `<p class="admin-empty-note">No messages — testing dummy profile.</p>`;
      }
      openAdminProfileModal();
      return;
    }

    await ensureBanFormLoaded();
    state.profilePlayerId = playerId;
    els.profileFlagCommentInput.value = "";
    resetBanReasonForm(
      els.profileBanReasonPreset,
      els.profileBanReason,
      els.profileBanReasonLabel,
    );
    staffPlayerMessaging.clearForm();
    setProfileFlagStatus("");
    setProfileWarningStatus("");
    if (els.profileWarningReason) {
      els.profileWarningReason.value = "";
    }
    setProfileBanStatus("");
    const profile = await api.adminPlayerProfile(playerId);
    renderAdminProfile(profile);
    await staffPlayerMessaging.loadHistory();
    openAdminProfileModal();
  } catch (error) {
    setStatus(statusEl, error.message, true);
  }
}

function testingBadgeHtml(player) {
  return player.isTestingDummy
    ? ` <span class="admin-testing-badge">TEST</span>`
    : "";
}

function renderPlayersTable(players) {
  if (!players.length) {
    els.playersBody.innerHTML = `<tr><td colspan="7">No players found.</td></tr>`;
    return;
  }

  els.playersBody.innerHTML = players
    .map(
      (player) => `
        <tr data-player-id="${player.id}"${player.isTestingDummy ? ' data-testing-dummy="1"' : ""}>
          <td>${escapeHtml(player.username)}${testingBadgeHtml(player)}</td>
          <td>${escapeHtml(player.email)}</td>
          <td class="admin-credits-current">${formatCredits(player.credits)}</td>
          <td>${player.mineCount}</td>
          <td>${formatDate(player.createdAt)}</td>
          <td>${formatPlayerBanStatus(player)}</td>
          <td>
            <button type="button" class="btn ghost admin-view-profile-btn">View profile</button>
          </td>
        </tr>`
    )
    .join("");
}

function renderCreditsTable(players) {
  if (!players.length) {
    els.creditsBody.innerHTML = `<tr><td colspan="5">No players found.</td></tr>`;
    return;
  }

  els.creditsBody.innerHTML = players
    .map((player) => {
      const isDummy = Boolean(player.isTestingDummy);
      return `
        <tr data-player-id="${player.id}"${isDummy ? ' data-testing-dummy="1"' : ""}>
          <td>${escapeHtml(player.username)}${testingBadgeHtml(player)}</td>
          <td>${escapeHtml(player.email)}</td>
          <td class="admin-credits-current">${formatCredits(player.credits)}</td>
          <td>
            ${
              isDummy
                ? `<span class="admin-page-desc">Dummy (read-only)</span>`
                : `<input
              class="admin-credits-input"
              type="number"
              min="0"
              step="0.01"
              value="${Number(player.credits)}"
              aria-label="New ${RAX_NAME} balance for ${escapeHtml(player.username)}">`
            }
          </td>
          <td>
            <div class="admin-row-actions">
              ${
                isDummy
                  ? `<button type="button" class="btn ghost admin-view-profile-btn">View profile</button>`
                  : `<button type="button" class="btn success admin-save-credits-btn">Save</button>`
              }
            </div>
          </td>
        </tr>`;
    })
    .join("");
}

async function loadDashboard() {
  state.dashboard = await api.adminDashboard();
  renderStats(state.dashboard);
}

function renderTestingDummySelect() {
  if (!els.testingDummySelect) {
    return;
  }

  if (!state.testingMode) {
    els.testingDummySelect.innerHTML = "";
    return;
  }

  const players = getDummyPlayerSummaries();
  els.testingDummySelect.innerHTML = players
    .map((player, index) => {
      const profileNumber = String(100_000 + index * 7919).slice(0, 6);
      return `<option value="${index}">${escapeHtml(player.username)} (#${profileNumber})</option>`;
    })
    .join("");
}

function getSelectedTestingDummyIndex() {
  const value = Number.parseInt(els.testingDummySelect?.value ?? "", 10);
  return Number.isFinite(value) ? value : 0;
}

async function runTestingAction(label, request) {
  if (!state.testingMode) {
    setStatus(els.testingActionsStatus, "Turn on testing mode first.", true);
    return;
  }

  const dummyIndex = getSelectedTestingDummyIndex();
  const buttons = [
    els.testingStaffMessageBtn,
    els.testingPeerMessageBtn,
    els.testingFlaggedMessageBtn,
    els.testingBanAppealBtn,
  ];

  buttons.forEach((button) => {
    if (button) {
      button.disabled = true;
    }
  });

  setStatus(els.testingActionsStatus, `${label}…`);

  try {
    const response = await request(dummyIndex);
    setStatus(els.testingActionsStatus, response?.message ?? `${label} completed.`);
  } catch (error) {
    setStatus(els.testingActionsStatus, error.message, true);
  } finally {
    buttons.forEach((button) => {
      if (button) {
        button.disabled = false;
      }
    });
  }
}

function renderTestingDummyList() {
  if (!els.testingDummyList) {
    return;
  }

  if (!state.testingMode) {
    els.testingDummyList.innerHTML =
      `<p class="admin-empty-note">Enable testing mode to preview synthetic player profiles.</p>`;
    return;
  }

  const players = getDummyPlayerSummaries();
  els.testingDummyList.innerHTML = `
    <table class="admin-table">
      <thead>
        <tr>
          <th>Username</th>
          <th>Email</th>
          <th>Rax</th>
          <th>Mines</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        ${players
          .map(
            (player) => `
          <tr data-player-id="${player.id}" data-testing-dummy="1">
            <td>${escapeHtml(player.username)}${testingBadgeHtml(player)}</td>
            <td>${escapeHtml(player.email)}</td>
            <td class="admin-credits-current">${formatCredits(player.credits)}</td>
            <td>${player.mineCount}</td>
            <td>
              <button type="button" class="btn ghost admin-view-profile-btn">View profile</button>
            </td>
          </tr>`,
          )
          .join("")}
      </tbody>
    </table>`;
}

function loadTestingPage() {
  renderTestingModeUi();
  renderTestingDummySelect();
  renderTestingDummyList();
  if (els.testingActions) {
    els.testingActions.hidden = !state.testingMode;
  }
  if (els.testingActionsStatus && !state.testingMode) {
    setStatus(els.testingActionsStatus, "");
  }
  if (els.testingStatus) {
    setStatus(
      els.testingStatus,
      state.testingMode
        ? "Testing mode is enabled."
        : "Testing mode is disabled.",
    );
  }
}

async function loadOffworldNewsPage() {
  await Promise.all([loadOffworldNewsSummary(), loadOnnReporters()]);
}

function countOffworldNewsAiImages(stories) {
  return (stories ?? []).filter((story) => String(story.imageUrl ?? "").includes("/images/")).length;
}

function renderOffworldNewsSummary(edition) {
  if (!edition) {
    els.offworldNewsSummary.textContent = "No edition loaded for today.";
    return;
  }

  const storyCount = edition.stories?.length ?? 0;
  const imageCount = countOffworldNewsAiImages(edition.stories);
  const source = edition.source ?? "unknown";
  const headline = edition.stories?.[0]?.headline;
  const lead = headline ? ` Lead: “${headline}”.` : "";
  els.offworldNewsSummary.textContent =
    `Today (${edition.editionDate}): ${storyCount} stories, ${imageCount} AI images, source ${source}.${lead}`;
}

async function loadOffworldNewsSummary() {
  try {
    const edition = await api.getOffworldNews();
    renderOffworldNewsSummary(edition);
  } catch {
    els.offworldNewsSummary.textContent = "Could not load today's Offworld News edition.";
  }
}

async function regenerateOffworldNewsEdition() {
  if (!window.confirm("Regenerate today's Offworld News stories and images? This may take several minutes.")) {
    return;
  }

  setStatus(els.offworldNewsStatus, "Regenerating stories and images…");
  setOffworldNewsButtonsDisabled(true);
  try {
    const result = await api.adminRegenerateOffworldNewsEdition();
    await loadOffworldNewsSummary();
    setStatus(
      els.offworldNewsStatus,
      [
        result.message,
        `${result.storyCount} stories, ${result.illustratedStoryCount} AI images.`,
        result.imageGenerationError,
      ]
        .filter(Boolean)
        .join(" "),
      Boolean(result.imageGenerationError),
    );
  } catch (error) {
    setStatus(els.offworldNewsStatus, error.message, true);
  } finally {
    setOffworldNewsButtonsDisabled(false);
  }
}

function setOffworldNewsButtonsDisabled(disabled) {
  els.offworldNewsRegenEditionBtn.disabled = disabled;
  els.offworldNewsRegenImagesBtn.disabled = disabled;
  els.offworldNewsRegenReporterPortraitsBtn.disabled = disabled;
}

function pickJson(value, camelKey) {
  if (value == null) {
    return undefined;
  }

  if (value[camelKey] !== undefined && value[camelKey] !== null) {
    return value[camelKey];
  }

  const pascalKey = camelKey.charAt(0).toUpperCase() + camelKey.slice(1);
  return value[pascalKey];
}

function readOnnSlugFromForm(form) {
  const fromInput = form.querySelector("[data-onn-slug]")?.value?.trim() ?? "";
  if (fromInput) {
    return fromInput;
  }

  return String(form.dataset.slug ?? "").trim();
}

function resolveAdminAssetUrl(url) {
  if (!url) {
    return "";
  }

  if (/^https?:\/\//i.test(url)) {
    return url;
  }

  const apiBase = API_BASE_URL || readMetaApiBase();
  if (apiBase && url.startsWith("/")) {
    return `${apiBase.replace(/\/$/, "")}${url}`;
  }

  return url;
}

function onnReporterPortraitHtml(reporter, displayName, { compact = false } = {}) {
  const avatarUrl = resolveAdminAssetUrl(pickJson(reporter, "avatarUrl"));
  const backgroundUrl = resolveAdminAssetUrl(pickJson(reporter, "backgroundUrl"));
  const alt = escapeHtml(displayName || pickJson(reporter, "slug") || "Reporter");
  const avatarMarkup = avatarUrl
    ? `<img class="admin-onn-reporter-avatar${compact ? " admin-onn-reporter-avatar-compact" : ""}" src="${escapeHtml(avatarUrl)}" alt="${alt}" loading="lazy">`
    : `<div class="admin-onn-reporter-avatar admin-onn-reporter-avatar-placeholder${compact ? " admin-onn-reporter-avatar-compact" : ""}" aria-hidden="true"></div>`;
  const bannerStyle = backgroundUrl ? ` style="background-image:url('${escapeHtml(backgroundUrl)}')"` : "";

  if (compact) {
    return `<div class="admin-onn-reporter-banner admin-onn-reporter-banner-compact"${bannerStyle}>${avatarMarkup}</div>`;
  }

  return `
    <div class="admin-onn-reporter-visuals">
      <div class="admin-onn-reporter-banner"${bannerStyle}>
        ${avatarMarkup}
      </div>
      <p class="admin-onn-reporter-visuals-caption">Portrait &amp; banner on Exonet</p>
    </div>`;
}

function onnReporterFieldValue(reporter, key, { list = false } = {}) {
  const raw = pickJson(reporter, key);
  if (list && Array.isArray(raw)) {
    return raw.join("; ");
  }

  if (raw == null) {
    return "";
  }

  return String(raw);
}

function onnReporterField(id, label, value, { textarea = false, hint = "", slugInput = false } = {}) {
  const displayValue = value == null ? "" : Array.isArray(value) ? value.join("; ") : String(value);
  const attrs = slugInput ? ' data-onn-slug type="text"' : ` id="${id}" type="text"`;
  const control = textarea
    ? `<textarea id="${id}" rows="3">${escapeHtml(displayValue)}</textarea>`
    : `<input${attrs} value="${escapeHtml(displayValue)}">`;
  const hintHtml = hint ? `<span class="admin-page-desc">${escapeHtml(hint)}</span>` : "";
  return `<label>${escapeHtml(label)}${control}${hintHtml}</label>`;
}

function onnReporterGenderField(id, gender) {
  const value = String(gender ?? "female").toLowerCase();
  return `<label>Portrait gender (AI)
    <select id="${id}">
      <option value="female"${value === "female" ? " selected" : ""}>Female</option>
      <option value="male"${value === "male" ? " selected" : ""}>Male</option>
    </select>
    <span class="admin-page-desc">Portrait AI uses gender, species, race, and the appearance fields below. Banner AI uses notable locations and career stories.</span>
  </label>`;
}

function onnReporterCareerFields(formId, reporter = {}) {
  return `
    <details class="admin-onn-field-group" open>
      <summary>Career &amp; embeds</summary>
      ${onnReporterField(`${formId}-notable-locations`, "Notable reporting locations", onnReporterFieldValue(reporter, "notableLocations", { list: true }), { textarea: true, hint: "Semicolon-separated places that matter to this reporter (drives banner AI)." })}
      ${onnReporterField(`${formId}-notable-stories`, "Big career stories", onnReporterFieldValue(reporter, "notableStories", { list: true }), { textarea: true, hint: "Semicolon-separated headline-style scoops (shown on profile and banner memorabilia)." })}
    </details>`;
}

function onnReporterAppearanceFields(formId, reporter = {}) {
  const species = String(onnReporterFieldValue(reporter, "species") || "human").trim() || "human";
  const race = onnReporterFieldValue(reporter, "race") || onnReporterFieldValue(reporter, "complexion");
  return `
    <details class="admin-onn-field-group" open>
      <summary>Portrait appearance (AI)</summary>
      ${onnReporterField(`${formId}-species`, "Species", species, { hint: "Human, or an alien type (Europan, Callistan, Martian, etc.). Aliens use the same ONN blue/cyan portrait style." })}
      ${onnReporterField(`${formId}-race`, "Race / skin", race, { hint: "Ethnicity, skin tone, or alien dermal color/texture." })}
      ${onnReporterField(`${formId}-hair`, "Hair / crest", onnReporterFieldValue(reporter, "hair"), { hint: "Human hair or alien crest, ridges, filaments." })}
      ${onnReporterField(`${formId}-eyes`, "Eyes", onnReporterFieldValue(reporter, "eyes"))}
      ${onnReporterField(`${formId}-build`, "Build", onnReporterFieldValue(reporter, "build"), { hint: "e.g. tall and lean, stocky, muscular." })}
      ${onnReporterField(`${formId}-facial-hair`, "Facial hair", onnReporterFieldValue(reporter, "facialHair"), { hint: "None, stubble, beard, etc." })}
      ${onnReporterField(`${formId}-makeup`, "Makeup / markings", onnReporterFieldValue(reporter, "makeup"), { hint: "None or describe style / bioluminescent accents." })}
      ${onnReporterField(`${formId}-distinctive`, "Distinctive features", onnReporterFieldValue(reporter, "distinctiveFeatures"), { textarea: true, hint: "Scars, antennae, jewelry, accessories, alien anatomy cues." })}
    </details>`;
}

function onnReporterAddFormHtml() {
  const formId = "admin-onn-add-form";
  return `<details class="admin-onn-reporter admin-onn-add-reporter" open>
    <summary class="admin-onn-reporter-summary">
      <span class="admin-onn-reporter-summary-text"><strong>Add reporter</strong></span>
    </summary>
    <form id="${formId}" class="admin-onn-reporter-form" data-slug="">
      <div class="admin-onn-reporter-fields">
        ${onnReporterField(`${formId}-slug`, "Slug (URL)", "", { slugInput: true, hint: "Lowercase letters, numbers, and hyphens only." })}
        ${onnReporterField(`${formId}-name`, "Display name", "")}
        ${onnReporterGenderField(`${formId}-gender`, "female")}
        ${onnReporterField(`${formId}-title`, "Title", "")}
        ${onnReporterField(`${formId}-beat`, "Beat", "")}
        ${onnReporterField(`${formId}-bureau`, "Bureau", "")}
        ${onnReporterField(`${formId}-personality`, "Personality", "", { textarea: true })}
        ${onnReporterField(`${formId}-voice`, "Writing voice", "", { textarea: true })}
        ${onnReporterField(`${formId}-directory-bio`, "Directory bio", "", { textarea: true })}
        ${onnReporterField(`${formId}-onn-bio`, "ONN bio", "", { textarea: true })}
        ${onnReporterField(`${formId}-kicker`, "Story kicker", "", { textarea: true })}
        ${onnReporterField(`${formId}-specialties`, "Specialties", "", { hint: "Separate with semicolons (;)." })}
        ${onnReporterCareerFields(formId)}
        ${onnReporterAppearanceFields(formId)}
        <div class="button-row admin-onn-reporter-actions">
          <button type="submit" class="btn primary">Add reporter</button>
          <button type="button" class="btn ghost admin-onn-add-generate-btn" data-onn-assets="both">Add &amp; generate AI portraits</button>
        </div>
        <p class="status-text admin-onn-reporter-form-status"></p>
      </div>
    </form>
  </details>`;
}

function renderOnnReporters(page) {
  const reporters = pickJson(page, "reporters") ?? [];
  const settings = pickJson(page, "settings") ?? {};
  const reportersFilePath = pickJson(page, "reportersFilePath") ?? "";
  els.onnReportersPath.textContent = `Roster: ${reportersFilePath}. Story pool: ${pickJson(settings, "activePoolCount") ?? 0} of ${pickJson(settings, "totalReporters") ?? reporters.length} reporters.`;
  els.onnPoolSize.value = pickJson(settings, "reporterPoolSize") ?? 0;

  els.onnReportersList.innerHTML =
    onnReporterAddFormHtml() +
    reporters
    .map((reporter) => {
      const slug = String(pickJson(reporter, "slug") ?? "").trim();
      const displayName = pickJson(reporter, "displayName") ?? "";
      const beat = pickJson(reporter, "beat") ?? "";
      const inStoryPool = Boolean(pickJson(reporter, "inStoryPool"));
      const poolTag = inStoryPool ? " · in story pool" : "";
      const specialties = (pickJson(reporter, "specialties") ?? []).join("; ");
      const formId = `admin-onn-form-${slug || "reporter"}`;
      return `<details class="admin-onn-reporter">
        <summary class="admin-onn-reporter-summary">
          ${onnReporterPortraitHtml(reporter, displayName, { compact: true })}
          <span class="admin-onn-reporter-summary-text">
            <strong>${escapeHtml(displayName)}</strong>
            <span class="admin-onn-reporter-meta">${escapeHtml(slug)} · ${escapeHtml(beat)}${poolTag}</span>
          </span>
        </summary>
        <form id="${formId}" class="admin-onn-reporter-form" data-slug="${escapeHtml(slug)}">
          <div class="admin-onn-reporter-layout">
            ${onnReporterPortraitHtml(reporter, displayName)}
            <div class="admin-onn-reporter-main">
            <div class="admin-onn-reporter-fields">
          ${onnReporterField(`${formId}-slug`, "Slug (URL)", slug, { slugInput: true, hint: "Change only when renaming; updates portrait folder and friend links." })}
          ${onnReporterField(`${formId}-name`, "Display name", displayName)}
          ${onnReporterGenderField(`${formId}-gender`, pickJson(reporter, "gender"))}
          ${onnReporterField(`${formId}-title`, "Title", pickJson(reporter, "title"))}
          ${onnReporterField(`${formId}-beat`, "Beat", beat)}
          ${onnReporterField(`${formId}-bureau`, "Bureau", pickJson(reporter, "bureau"))}
          ${onnReporterField(`${formId}-personality`, "Personality", pickJson(reporter, "personality"), { textarea: true })}
          ${onnReporterField(`${formId}-voice`, "Writing voice", pickJson(reporter, "writingVoice"), { textarea: true })}
          ${onnReporterField(`${formId}-directory-bio`, "Directory bio", pickJson(reporter, "directoryBio"), { textarea: true })}
          ${onnReporterField(`${formId}-onn-bio`, "ONN bio", pickJson(reporter, "onnBio"), { textarea: true })}
          ${onnReporterField(`${formId}-kicker`, "Story kicker", pickJson(reporter, "storyKicker"), { textarea: true })}
          ${onnReporterField(`${formId}-specialties`, "Specialties", specialties, { hint: "Separate with semicolons (;)." })}
          ${onnReporterCareerFields(formId, reporter)}
          ${onnReporterAppearanceFields(formId, reporter)}
            </div>
            <div class="admin-onn-reporter-fields-footer">
              <div class="button-row admin-onn-reporter-actions">
                <button type="submit" class="btn primary">Save reporter</button>
                <button type="button" class="btn ghost admin-onn-regen-portrait-btn" data-onn-assets="avatar">Regenerate portrait</button>
                <button type="button" class="btn ghost admin-onn-regen-portrait-btn" data-onn-assets="background">Regenerate banner</button>
                <button type="button" class="btn ghost admin-onn-regen-portrait-btn" data-onn-assets="both">Regenerate both</button>
              </div>
              <p class="status-text admin-onn-reporter-form-status"></p>
            </div>
            </div>
          </div>
        </form>
      </details>`;
    })
    .join("");

  const addForm = document.getElementById("admin-onn-add-form");
  addForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    addOnnReporter(addForm, { generatePortraits: false }).catch((error) =>
      setStatus(addForm.querySelector(".admin-onn-reporter-form-status"), error.message, true),
    );
  });
  addForm?.querySelector(".admin-onn-add-generate-btn")?.addEventListener("click", () => {
    addOnnReporter(addForm, { generatePortraits: true }).catch((error) =>
      setStatus(addForm.querySelector(".admin-onn-reporter-form-status"), error.message, true),
    );
  });

  els.onnReportersList.querySelectorAll(".admin-onn-reporter-form").forEach((form) => {
    if (form.id === "admin-onn-add-form") {
      return;
    }
    form.addEventListener("submit", (event) => {
      event.preventDefault();
      saveOnnReporter(form).catch((error) =>
        setStatus(form.querySelector(".admin-onn-reporter-form-status"), error.message, true),
      );
    });
  });

  els.onnReportersList.querySelectorAll(".admin-onn-regen-portrait-btn").forEach((button) => {
    button.addEventListener("click", () => {
      const form = button.closest(".admin-onn-reporter-form");
      const assets = button.dataset.onnAssets || "both";
      regenerateOnnReporterPortraits(form, assets).catch((error) =>
        setStatus(form.querySelector(".admin-onn-reporter-form-status"), error.message, true),
      );
    });
  });
}

function onnPortraitAssetConfirmLabel(assets) {
  switch (assets) {
    case "avatar":
      return "portrait (profile image)";
    case "background":
      return "banner (background image)";
    default:
      return "portrait and banner";
  }
}

function readOnnReporterForm(form) {
  const prefix = form.id;
  const newSlug = readOnnSlugFromForm(form);
  const originalSlug = String(form.dataset.slug ?? "").trim();
  return {
    newSlug: newSlug !== originalSlug ? newSlug : null,
    displayName: document.getElementById(`${prefix}-name`)?.value.trim() ?? "",
    gender: document.getElementById(`${prefix}-gender`)?.value.trim() ?? "female",
    title: document.getElementById(`${prefix}-title`)?.value.trim() ?? "",
    beat: document.getElementById(`${prefix}-beat`)?.value.trim() ?? "",
    bureau: document.getElementById(`${prefix}-bureau`)?.value.trim() ?? "",
    personality: document.getElementById(`${prefix}-personality`)?.value.trim() ?? "",
    writingVoice: document.getElementById(`${prefix}-voice`)?.value.trim() ?? "",
    directoryBio: document.getElementById(`${prefix}-directory-bio`)?.value.trim() ?? "",
    onnBio: document.getElementById(`${prefix}-onn-bio`)?.value.trim() ?? "",
    storyKicker: document.getElementById(`${prefix}-kicker`)?.value.trim() ?? "",
    specialties: document.getElementById(`${prefix}-specialties`)?.value.trim() ?? "",
    notableLocations: document.getElementById(`${prefix}-notable-locations`)?.value.trim() ?? "",
    notableStories: document.getElementById(`${prefix}-notable-stories`)?.value.trim() ?? "",
    hair: document.getElementById(`${prefix}-hair`)?.value.trim() ?? "",
    eyes: document.getElementById(`${prefix}-eyes`)?.value.trim() ?? "",
    race: document.getElementById(`${prefix}-race`)?.value.trim() ?? "",
    build: document.getElementById(`${prefix}-build`)?.value.trim() ?? "",
    facialHair: document.getElementById(`${prefix}-facial-hair`)?.value.trim() ?? "",
    makeup: document.getElementById(`${prefix}-makeup`)?.value.trim() ?? "",
    distinctiveFeatures: document.getElementById(`${prefix}-distinctive`)?.value.trim() ?? "",
    species: document.getElementById(`${prefix}-species`)?.value.trim() ?? "human",
  };
}

function readOnnReporterCreateBody(form) {
  const body = readOnnReporterForm(form);
  return {
    slug: readOnnSlugFromForm(form),
    displayName: body.displayName,
    gender: body.gender,
    title: body.title,
    beat: body.beat,
    bureau: body.bureau,
    personality: body.personality,
    writingVoice: body.writingVoice,
    directoryBio: body.directoryBio,
    onnBio: body.onnBio,
    storyKicker: body.storyKicker,
    specialties: body.specialties,
    notableLocations: body.notableLocations,
    notableStories: body.notableStories,
    hair: body.hair,
    eyes: body.eyes,
    race: body.race,
    build: body.build,
    facialHair: body.facialHair,
    makeup: body.makeup,
    distinctiveFeatures: body.distinctiveFeatures,
    species: body.species,
  };
}

async function persistOnnReporterProfileForAi(form) {
  const routeSlug = String(form.dataset.slug ?? "").trim();
  if (!routeSlug) {
    throw new Error("Reporter slug is missing. Save the reporter first.");
  }

  await api.adminUpdateOffworldNewsReporter(routeSlug, readOnnReporterForm(form));
  const savedSlug = readOnnSlugFromForm(form);
  if (savedSlug !== routeSlug) {
    form.dataset.slug = savedSlug;
  }
}

async function addOnnReporter(form, { generatePortraits = false } = {}) {
  const statusEl = form.querySelector(".admin-onn-reporter-form-status");
  const body = readOnnReporterCreateBody(form);
  if (!body.slug) {
    setStatus(statusEl, "Slug is required.", true);
    return;
  }
  if (!body.displayName) {
    setStatus(statusEl, "Display name is required.", true);
    return;
  }

  if (generatePortraits) {
    const assetLabel = onnPortraitAssetConfirmLabel("both");
    if (!window.confirm(`Add ${body.displayName} and generate AI ${assetLabel}? This uses OffworldNews.ApiKey.`)) {
      return;
    }
  }

  setStatus(statusEl, generatePortraits ? "Adding reporter…" : "Saving…");
  form.querySelectorAll("button").forEach((button) => {
    button.disabled = true;
  });
  try {
    await api.adminCreateOffworldNewsReporter(body);
    if (generatePortraits) {
      setStatus(statusEl, "Generating AI portraits from profile…");
      await api.adminRegenerateOneOffworldNewsReporterPortraits(body.slug, "both");
      const job = await waitForReporterPortraitJob((message) => setStatus(statusEl, message));
      const result = formatReporterPortraitJobStatus(job);
      await loadOnnReporters();
      setStatus(statusEl, `Reporter added. ${result.text}`, result.isError);
      return;
    }

    await loadOnnReporters();
    setStatus(statusEl, "Reporter added. Generate portraits when ready.");
  } catch (error) {
    setStatus(statusEl, error.message, true);
  } finally {
    form.querySelectorAll("button").forEach((button) => {
      button.disabled = false;
    });
  }
}

async function loadOnnReporters() {
  setStatus(els.onnReportersStatus, "Loading reporters…");
  try {
    const page = await api.adminGetOffworldNewsReporters();
    state.onnReporters = page;
    renderOnnReporters(page);
    const count = (pickJson(page, "reporters") ?? []).length;
    setStatus(els.onnReportersStatus, `${count} reporters loaded.`);
  } catch (error) {
    els.onnReportersList.innerHTML = "";
    setStatus(els.onnReportersStatus, error.message, true);
  }
}

async function saveOnnReporter(form) {
  const statusEl = form.querySelector(".admin-onn-reporter-form-status");
  const routeSlug = String(form.dataset.slug ?? "").trim();
  if (!routeSlug) {
    setStatus(statusEl, "Reporter slug is missing. Reload the page and try again.", true);
    return;
  }

  const body = readOnnReporterForm(form);
  setStatus(statusEl, "Saving…");
  form.querySelectorAll("button").forEach((button) => {
    button.disabled = true;
  });
  try {
    await api.adminUpdateOffworldNewsReporter(routeSlug, body);
    await loadOnnReporters();
    setStatus(statusEl, "Reporter saved.");
  } catch (error) {
    setStatus(statusEl, error.message, true);
  } finally {
    form.querySelectorAll("button").forEach((button) => {
      button.disabled = false;
    });
  }
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function waitForReporterPortraitJob(onProgress) {
  for (;;) {
    const job = await api.adminGetOffworldNewsReporterPortraitJob();
    const status = String(pickJson(job, "status") ?? "").toLowerCase();
    if (status === "running") {
      const saved = pickJson(job, "imagesSaved") ?? 0;
      const attempts = pickJson(job, "imageAttempts") ?? 0;
      const message = pickJson(job, "message") ?? "Regenerating reporter portraits…";
      onProgress?.(`${message} (${saved}/${attempts} images)`);
      await sleep(3000);
      continue;
    }

    return job;
  }
}

function formatReporterPortraitJobStatus(job) {
  const status = String(pickJson(job, "status") ?? "").toLowerCase();
  const message = pickJson(job, "message") ?? "";
  const error = pickJson(job, "imageGenerationError");
  const saved = pickJson(job, "imagesSaved") ?? 0;
  const attempts = pickJson(job, "imageAttempts") ?? 0;

  if (status === "failed") {
    return { text: message || "Portrait regeneration failed.", isError: true };
  }

  return {
    text: error ? `${message} ${error}` : `${message} (${saved}/${attempts} images).`,
    isError: Boolean(error),
  };
}

async function regenerateOnnReporterPortraits(form, assets = "both") {
  const statusEl = form.querySelector(".admin-onn-reporter-form-status");
  const slug = readOnnSlugFromForm(form);
  const name = form.querySelector(`#${form.id}-name`)?.value.trim() || slug;
  if (!slug) {
    setStatus(statusEl, "Reporter slug is missing. Reload the page and try again.", true);
    return;
  }

  const assetLabel = onnPortraitAssetConfirmLabel(assets);
  if (!window.confirm(`Regenerate AI ${assetLabel} for ${name}? This uses OffworldNews.ApiKey.`)) {
    return;
  }

  setStatus(statusEl, "Saving profile for AI…");
  form.querySelectorAll("button").forEach((button) => {
    button.disabled = true;
  });
  try {
    await persistOnnReporterProfileForAi(form);
    const slug = readOnnSlugFromForm(form);
    setStatus(statusEl, "Starting regeneration…");
    await api.adminRegenerateOneOffworldNewsReporterPortraits(slug, assets);
    const job = await waitForReporterPortraitJob((message) => setStatus(statusEl, message));
    const result = formatReporterPortraitJobStatus(job);
    await loadOnnReporters();
    setStatus(statusEl, result.text, result.isError);
  } catch (error) {
    setStatus(statusEl, error.message, true);
  } finally {
    form.querySelectorAll("button").forEach((button) => {
      button.disabled = false;
    });
  }
}

async function saveOnnPoolSize(event) {
  event.preventDefault();
  const size = Number.parseInt(els.onnPoolSize.value, 10);
  if (!Number.isFinite(size) || size < 0) {
    setStatus(els.onnPoolStatus, "Enter a non-negative pool size.", true);
    return;
  }

  setStatus(els.onnPoolStatus, "Saving…");
  els.onnPoolSaveBtn.disabled = true;
  try {
    const settings = await api.adminUpdateOffworldNewsSettings(size);
    await loadOnnReporters();
    const poolSize = pickJson(settings, "reporterPoolSize") ?? 0;
    const activeCount = pickJson(settings, "activePoolCount") ?? 0;
    setStatus(
      els.onnPoolStatus,
      `Story pool set to ${poolSize === 0 ? "all reporters" : poolSize} (${activeCount} active).`,
    );
  } catch (error) {
    setStatus(els.onnPoolStatus, error.message, true);
  } finally {
    els.onnPoolSaveBtn.disabled = false;
  }
}

async function regenerateOffworldNewsReporterPortraits() {
  if (
    !window.confirm(
      "Regenerate AI portrait and banner JPEGs for all 15 ONN reporters? This uses OffworldNews.ApiKey and may take several minutes.",
    )
  ) {
    return;
  }

  setStatus(els.offworldNewsStatus, "Starting reporter portrait regeneration…");
  setOffworldNewsButtonsDisabled(true);
  try {
    await api.adminRegenerateAllOffworldNewsReporterPortraits();
    const job = await waitForReporterPortraitJob((message) => setStatus(els.offworldNewsStatus, message));
    const result = formatReporterPortraitJobStatus(job);
    await loadOnnReporters();
    setStatus(els.offworldNewsStatus, result.text, result.isError);
  } catch (error) {
    setStatus(els.offworldNewsStatus, error.message, true);
  } finally {
    setOffworldNewsButtonsDisabled(false);
  }
}

async function regenerateOffworldNewsImages() {
  if (
    !window.confirm(
      "Regenerate only today's existing AI story images? Headlines, article text, archive editions, and placeholder images are left unchanged.",
    )
  ) {
    return;
  }

  setStatus(els.offworldNewsStatus, "Regenerating today's AI images…");
  setOffworldNewsButtonsDisabled(true);
  try {
    const result = await api.adminRegenerateOffworldNewsImages();
    await loadOffworldNewsSummary();
    setStatus(
      els.offworldNewsStatus,
      result.imageGenerationError
        ? result.imageGenerationError
        : `${result.message} ${result.illustratedStoryCount} AI images.`,
      Boolean(result.imageGenerationError),
    );
  } catch (error) {
    setStatus(els.offworldNewsStatus, error.message, true);
  } finally {
    setOffworldNewsButtonsDisabled(false);
  }
}

async function loadPlayers() {
  setStatus(els.playersStatus, "Loading…");
  const search = els.playerSearch.value.trim();
  const response = await api.adminPlayers(search);
  state.players = applyPlayersForDisplay(response.players ?? [], search);
  renderPlayersTable(state.players);
  const dummyCount = state.players.filter((player) => player.isTestingDummy).length;
  const suffix =
    state.testingMode && dummyCount && !search
      ? ` (${dummyCount} testing dummy${dummyCount === 1 ? "" : "ies"})`
      : "";
  setStatus(els.playersStatus, `${state.players.length} player(s) shown${suffix}`);
}

async function loadCreditsPage() {
  setStatus(els.gameCreditsStatus, "Loading…");
  setStatus(els.creditsStatus, "Loading…");
  const [configResponse, playersResponse] = await Promise.all([
    api.adminGameCreditsConfig(),
    api.adminPlayers(els.creditsSearch.value.trim()),
  ]);

  state.gameCreditsConfig = configResponse.credits ?? null;
  renderGameCreditsConfig(configResponse);
  const search = els.creditsSearch.value.trim();
  state.players = applyPlayersForDisplay(playersResponse.players ?? [], search);
  renderCreditsTable(state.players);
  setStatus(els.gameCreditsStatus, "Loaded");
  const dummyCount = state.players.filter((player) => player.isTestingDummy).length;
  const suffix =
    state.testingMode && dummyCount && !search
      ? ` (${dummyCount} testing dummy${dummyCount === 1 ? "" : "ies"})`
      : "";
  setStatus(els.creditsStatus, `${state.players.length} player(s) shown${suffix}`);
}

function toDateTimeLocalValue(iso) {
  if (!iso) {
    return "";
  }
  return new Date(iso).toISOString().slice(0, 16);
}

function toUtcIsoFromLocalInput(value) {
  if (!value) {
    return null;
  }
  return new Date(`${value}:00.000Z`).toISOString();
}

function formatEventReward(reward) {
  return formatRewardAmount(reward.itemType ?? "", reward.amount ?? 0);
}

function addEventRewardRow(itemType = "", amount = "") {
  const row = document.createElement("div");
  row.className = "admin-event-reward-row";
  row.innerHTML = `
    <input type="text" class="admin-event-reward-item" placeholder="Item type (Credits = ${RAX_NAME})" value="${escapeHtml(itemType)}" required>
    <input type="number" class="admin-event-reward-amount" min="0.01" step="0.1" placeholder="Amount" value="${escapeHtml(amount)}" required>
    <button type="button" class="btn ghost admin-event-reward-remove">Remove</button>
  `;
  els.eventRewards.appendChild(row);
}

function clearEventRewardRows() {
  els.eventRewards.innerHTML = "";
}

function renderEventsList(events) {
  if (!events.length) {
    els.eventsList.innerHTML = `<p class="admin-page-desc">No events yet. Create a challenge event for players to win rewards.</p>`;
    return;
  }

  els.eventsList.innerHTML = events
    .map((event) => {
      const status = event.isActive ? "Active" : "Inactive";
      const windowText = [
        event.startsAt ? `from ${formatDate(event.startsAt)}` : null,
        event.endsAt ? `until ${formatDate(event.endsAt)}` : null,
      ].filter(Boolean).join(" · ");
      const rewards = (event.rewards ?? [])
        .map((reward) => `<span class="admin-event-reward-chip">${escapeHtml(formatEventReward(reward))}</span>`)
        .join("");
      const challenge = escapeHtml(event.challengeDescription ?? event.challengeType ?? "Challenge");
      const marketBonus = event.marketBonusDescription
        ? `<p class="admin-event-card-meta"><strong>Market bonus:</strong> ${escapeHtml(event.marketBonusDescription)}</p>`
        : "";
      return `
        <article class="admin-event-card">
          <div class="admin-event-card-head">
            <div>
              <h3>${escapeHtml(event.title)}</h3>
              <p class="admin-event-card-meta">${escapeHtml(status)}${windowText ? ` · ${escapeHtml(windowText)}` : ""} · ${Number(event.claimCount ?? 0)} winner(s)</p>
            </div>
            <button type="button" class="btn ghost admin-event-edit-btn" data-event-id="${event.id}">Edit</button>
          </div>
          <p>${escapeHtml(event.message)}</p>
          <p class="admin-event-card-meta"><strong>Challenge:</strong> ${challenge}</p>
          ${marketBonus}
          <div class="admin-event-card-rewards">${rewards || "<span class=\"admin-event-card-meta\">No challenge rewards</span>"}</div>
        </article>`;
    })
    .join("");
}

function showEventEditor(event = null) {
  state.editingEventId = event?.id ?? null;
  els.eventEditor.hidden = false;
  els.eventEditorTitle.textContent = event ? "Edit event" : "New event";
  els.eventDeleteBtn.hidden = !event;
  els.eventTitle.value = event?.title ?? "";
  els.eventMessage.value = event?.message ?? "";
  els.eventActive.checked = event?.isActive ?? true;
  els.eventStarts.value = toDateTimeLocalValue(event?.startsAt);
  els.eventEnds.value = toDateTimeLocalValue(event?.endsAt);
  els.eventChallengeType.value = event?.challengeType ?? "AdvanceDay";
  els.eventChallengeTarget.value = String(event?.challengeTarget ?? 1);
  els.eventChallengeDetail.value = event?.challengeDetail ?? "";
  els.eventSaleBonus.value = String(event?.saleBonusPercent ?? 0);
  els.eventTradeBonus.value = String(event?.tradeBonusPercent ?? 0);
  clearEventRewardRows();
  if (event?.rewards?.length) {
    for (const reward of event.rewards) {
      addEventRewardRow(reward.itemType, reward.amount);
    }
  } else {
    addEventRewardRow("Credits", "100");
  }
  setStatus(els.eventFormStatus, "");
}

function hideEventEditor() {
  state.editingEventId = null;
  els.eventEditor.hidden = true;
  setStatus(els.eventFormStatus, "");
}

function collectEventPayload() {
  const rewards = [...els.eventRewards.querySelectorAll(".admin-event-reward-row")]
    .map((row) => ({
      itemType: row.querySelector(".admin-event-reward-item")?.value.trim() ?? "",
      amount: Number(row.querySelector(".admin-event-reward-amount")?.value),
    }))
    .filter((reward) => reward.itemType);

  return {
    title: els.eventTitle.value.trim(),
    message: els.eventMessage.value.trim(),
    isActive: els.eventActive.checked,
    startsAt: toUtcIsoFromLocalInput(els.eventStarts.value),
    endsAt: toUtcIsoFromLocalInput(els.eventEnds.value),
    challengeType: els.eventChallengeType.value,
    challengeTarget: Number(els.eventChallengeTarget.value),
    challengeDetail: els.eventChallengeDetail.value.trim(),
    saleBonusPercent: Number(els.eventSaleBonus.value),
    tradeBonusPercent: Number(els.eventTradeBonus.value),
    rewards,
  };
}

async function loadEventsPage() {
  setStatus(els.eventsStatus, "Loading…");
  hideEventEditor();
  const response = await api.adminSpecialEvents();
  state.events = response.events ?? [];
  renderEventsList(state.events);
  setStatus(els.eventsStatus, `${state.events.length} event(s)`);
}

async function saveEventForm(event) {
  event.preventDefault();
  const payload = collectEventPayload();
  if (!payload.rewards.length) {
    setStatus(els.eventFormStatus, "Add at least one reward.", true);
    return;
  }

  setStatus(els.eventFormStatus, "Saving…");
  try {
    const saved = state.editingEventId
      ? await api.adminUpdateSpecialEvent(state.editingEventId, payload)
      : await api.adminCreateSpecialEvent(payload);
    hideEventEditor();
    await loadEventsPage();
    setStatus(els.eventsStatus, `Saved "${saved.title}".`);
  } catch (error) {
    setStatus(els.eventFormStatus, error.message, true);
  }
}

async function deleteCurrentEvent() {
  if (!state.editingEventId) {
    return;
  }
  if (!window.confirm("Delete this event? Players who already claimed it keep their rewards.")) {
    return;
  }

  setStatus(els.eventFormStatus, "Deleting…");
  try {
    await api.adminDeleteSpecialEvent(state.editingEventId);
    hideEventEditor();
    await loadEventsPage();
    setStatus(els.eventsStatus, "Event deleted.");
  } catch (error) {
    setStatus(els.eventFormStatus, error.message, true);
  }
}

function renderGameCreditsConfig(response) {
  const credits = response?.credits ?? state.gameCreditsConfig;
  if (!credits) {
    return;
  }

  els.gameCreditsSignUp.value = String(credits.signUp ?? 0);
  els.gameCreditsBirthday.value = String(credits.birthdayBonus ?? 0);
  els.gameCreditsReclaim.value = String(credits.companyNameReclaimFee ?? 0);
  els.gameCreditsPath.textContent = response?.filePath
    ? `File: ${response.filePath}`
    : "";
}

function setGameCreditsStatus(message, isError = false) {
  setStatus(els.gameCreditsStatus, message, isError);
}

async function saveGameCreditsConfig() {
  const signUp = Number(els.gameCreditsSignUp.value);
  const birthdayBonus = Number(els.gameCreditsBirthday.value);
  const companyNameReclaimFee = Number(els.gameCreditsReclaim.value);

  if (!Number.isFinite(signUp) || signUp < 0) {
    setGameCreditsStatus(`Enter a valid sign-up ${RAX_NAME.toLowerCase()} amount.`, true);
    return;
  }

  if (!Number.isFinite(birthdayBonus) || birthdayBonus < 0) {
    setGameCreditsStatus("Enter a valid birthday bonus amount.", true);
    return;
  }

  if (!Number.isFinite(companyNameReclaimFee) || companyNameReclaimFee < 0) {
    setGameCreditsStatus("Enter a valid company name reclaim fee.", true);
    return;
  }

  els.gameCreditsSaveBtn.disabled = true;
  setGameCreditsStatus("Saving…");
  try {
    const result = await api.adminSaveGameCreditsConfig(signUp, birthdayBonus, companyNameReclaimFee);
    state.gameCreditsConfig = result.credits ?? null;
    renderGameCreditsConfig({ credits: result.credits, filePath: els.gameCreditsPath.textContent.replace(/^File: /, "") });
    setGameCreditsStatus(result.message ?? "Saved.", false);
    if (state.dashboard) {
      await loadDashboard();
    }
  } catch (error) {
    setGameCreditsStatus(error.message, true);
  } finally {
    els.gameCreditsSaveBtn.disabled = false;
  }
}

function resetGameCreditsForm() {
  renderGameCreditsConfig({ credits: state.gameCreditsConfig, filePath: null });
  setGameCreditsStatus("");
}

function formatAppealBanSummary(ban) {
  if (!ban) {
    return "No active ban";
  }
  if (ban.isPermanent) {
    return "Life ban";
  }
  return `${ban.banLevelLabel} until ${formatDate(ban.expiresAt)}`;
}

function renderAppeals(appeals) {
  if (!appeals.length) {
    els.appealsList.innerHTML = `<p class="admin-empty-note">No pending ban appeals.</p>`;
    return;
  }

  els.appealsList.innerHTML = appeals
    .map(
      (appeal) => `
        <article class="admin-appeal-card" data-appeal-id="${appeal.id}" data-player-id="${appeal.playerId}">
          <header class="admin-appeal-top">
            <div>
              <h3>${escapeHtml(appeal.username)}</h3>
              <p class="admin-appeal-meta">${escapeHtml(appeal.email)} · ${formatDate(appeal.createdAt)}</p>
            </div>
            <button type="button" class="btn ghost admin-view-profile-btn">View profile</button>
          </header>
          <p class="admin-appeal-ban">${escapeHtml(formatAppealBanSummary(appeal.activeBan))}</p>
          <p class="admin-appeal-message">${escapeHtml(appeal.message)}</p>
          <div class="admin-appeal-actions">
            <button type="button" class="btn ghost admin-dismiss-appeal-btn">Mark reviewed</button>
            <span class="admin-appeal-row-status"></span>
          </div>
        </article>`
    )
    .join("");
}

async function loadAppealsPage() {
  setStatus(els.appealsStatus, "Loading…");
  const response = await api.adminBanAppeals();
  const appeals = response.appeals ?? [];
  renderAppeals(appeals);
  setStatus(els.appealsStatus, `${appeals.length} pending appeal(s)`);
}

function renderBansTable(bans) {
  if (!bans.length) {
    els.bansBody.innerHTML = `<tr><td colspan="9">No bans found.</td></tr>`;
    return;
  }

  els.bansBody.innerHTML = bans
    .map((entry) => {
      const ban = entry.ban ?? {};
      const expiresLabel = ban.isPermanent
        ? "Permanent"
        : ban.expiresAt
          ? formatDate(ban.expiresAt)
          : "—";
      return `
        <tr data-player-id="${entry.playerId}">
          <td>${escapeHtml(entry.username)}</td>
          <td>${escapeHtml(entry.email)}</td>
          <td>${escapeHtml(ban.banLevelLabel ?? ban.banLevel ?? "—")}</td>
          <td class="admin-ban-reason">${formatBanReason(ban.reason)}</td>
          <td>${escapeHtml(ban.bannedByUsername ?? "—")}</td>
          <td>${ban.createdAt ? formatDate(ban.createdAt) : "—"}</td>
          <td>${expiresLabel}</td>
          <td>${formatBanRecordStatus(ban)}</td>
          <td>
            <button type="button" class="btn ghost admin-view-profile-btn">View profile</button>
          </td>
        </tr>`;
    })
    .join("");
}

async function loadBansPage() {
  setStatus(els.bansStatus, "Loading…");
  const search = els.bansSearch.value.trim();
  const activeOnly = els.bansActiveOnly.checked;
  const response = await api.adminBans(search, activeOnly);
  const bans = response.bans ?? [];
  renderBansTable(bans);
  const scope = activeOnly ? "active ban(s)" : "ban record(s)";
  setStatus(els.bansStatus, `${bans.length} ${scope}`);
}

async function dismissAppeal(appealId, statusEl, username) {
  setStatus(statusEl, "Updating…");
  try {
    await api.adminDismissBanAppeal(appealId);
    await loadAppealsPage();
    const message = `Appeal from ${username} marked reviewed.`;
    setStatus(els.appealsStatus, message, false);
    setStatus(statusEl, message, false);
  } catch (error) {
    setStatus(statusEl, error.message, true);
    setStatus(els.appealsStatus, error.message, true);
  }
}

async function loadMessagesPage() {
  await messagesHub.refresh();
}

const MESSAGE_LOG_CHANNEL_LABELS = {
  "staff-to-staff": "Staff → staff",
  "staff-to-player": "Staff → player",
  "player-to-staff": "Player → staff",
  peer: "Player → player",
  "ban-appeal": "Ban appeal",
};

function formatMessageLogChannel(channel) {
  return MESSAGE_LOG_CHANNEL_LABELS[channel] ?? channel;
}

function truncateMessageBody(body, maxLength = 120) {
  const text = String(body ?? "");
  if (text.length <= maxLength) {
    return text;
  }
  return `${text.slice(0, maxLength)}…`;
}

function renderMessageLog(entries) {
  if (!entries.length) {
    els.messageLogBody.innerHTML = `<tr><td colspan="6" class="admin-empty-note">No messages found.</td></tr>`;
    return;
  }

  els.messageLogBody.innerHTML = entries
    .map(
      (entry) => `
        <tr>
          <td>${escapeHtml(formatDate(entry.createdAt))}</td>
          <td>${escapeHtml(formatMessageLogChannel(entry.channel))}</td>
          <td>${escapeHtml(entry.fromLabel)}</td>
          <td>${escapeHtml(entry.toLabel)}</td>
          <td title="${escapeHtml(entry.body)}">${escapeHtml(truncateMessageBody(entry.body))}</td>
          <td>${entry.isRead ? "Yes" : "No"}</td>
        </tr>`
    )
    .join("");
}

async function loadMessageLogPage() {
  setStatus(els.messageLogStatus, "Loading…");
  const response = await api.adminMessageLog(
    els.messageLogSearch.value.trim(),
    els.messageLogChannel.value,
    100
  );
  const entries = response.entries ?? [];
  renderMessageLog(entries);
  setStatus(els.messageLogStatus, `${entries.length} message(s)`);
}

async function loadFlaggedPage() {
  await flaggedMessaging.loadReviews();
}

async function loadPortal() {
  renderTestingModeUi();
  await ensureBanLevelsLoaded();
  await loadDashboard();
  await staffMessaging.refreshUnreadBadge();
  await flaggedMessaging.refreshBadge();
  if (state.page === "players") {
    await loadPlayers();
  } else if (state.page === "bans") {
    await loadBansPage();
  } else if (state.page === "appeals") {
    await loadAppealsPage();
  } else if (state.page === "messages") {
    await loadMessagesPage();
  } else if (state.page === "message-log") {
    await loadMessageLogPage();
  } else if (state.page === "flagged") {
    await loadFlaggedPage();
  } else if (state.page === "credits") {
    await loadCreditsPage();
  } else if (state.page === "events") {
    await loadEventsPage();
  } else if (state.page === "offworld-news") {
    await loadOffworldNewsPage();
  } else if (state.page === "testing") {
    loadTestingPage();
  }
}

async function loadCurrentPage() {
  if (state.page === "dashboard") {
    await loadDashboard();
    return;
  }
  if (state.page === "testing") {
    loadTestingPage();
    return;
  }
  if (state.page === "offworld-news") {
    await loadOffworldNewsPage();
    return;
  }
  if (state.page === "players") {
    await loadPlayers();
    return;
  }
  if (state.page === "bans") {
    await loadBansPage();
    return;
  }
  if (state.page === "appeals") {
    await loadAppealsPage();
    return;
  }
  if (state.page === "messages") {
    await loadMessagesPage();
    return;
  }
  if (state.page === "message-log") {
    await loadMessageLogPage();
    return;
  }
  if (state.page === "flagged") {
    await loadFlaggedPage();
    return;
  }
  if (state.page === "events") {
    await loadEventsPage();
    return;
  }
  await loadCreditsPage();
}

async function saveCreditsForRow(row, statusEl) {
  if (row?.dataset.testingDummy === "1") {
    return;
  }

  const playerId = row?.dataset.playerId;
  const input = row?.querySelector(".admin-credits-input");
  const button = row?.querySelector(".admin-save-credits-btn");
  if (!playerId || !input || !button) {
    return;
  }

  const credits = Number(input.value);
  if (!Number.isFinite(credits) || credits < 0) {
    setStatus(statusEl, `Enter a valid non-negative ${RAX_NAME.toLowerCase()} amount.`, true);
    return;
  }

  button.disabled = true;
  setStatus(statusEl, `Saving ${RAX_NAME}…`);
  try {
    const updated = await api.adminSetCredits(playerId, credits);
    input.value = Number(updated.credits);
    const currentCell = row.querySelector(".admin-credits-current");
    if (currentCell) {
      currentCell.innerHTML = formatCredits(updated.credits);
    }
    setStatus(statusEl, `Updated ${RAX_NAME} for ${updated.username}.`);
    state.dashboard = await api.adminDashboard();
    renderStats(state.dashboard);
  } catch (error) {
    setStatus(statusEl, error.message, true);
  } finally {
    button.disabled = false;
  }
}

async function checkAdminAccess() {
  const access = await api.adminAccess();
  if (!access.isAdmin) {
    const name = access.username || api.username || "unknown";
    els.deniedUser.textContent =
      `Signed in as "${name}". Add this exact username to Admin:Usernames in appsettings.json, then restart the API.`;
    showScreen("denied");
    return false;
  }

  state.testingMode = Boolean(access.testingModeEnabled);
  setCachedTestingModeEnabled(state.testingMode);
  renderTestingModeUi();
  els.signedIn.textContent = `Signed in as ${access.username}`;
  showScreen("portal");
  return true;
}

function isAuthError(error) {
  const message = (error?.message ?? "").toLowerCase();
  return error?.code === "banned"
    || error?.code === "warning_required"
    || message.includes("session expired")
    || message.includes("invalid username or password")
    || message.includes("unauthorized");
}

async function tryRestoreSession() {
  api.reloadFromStorage();

  if (!api.token) {
    prefillLoginUsername();
    showScreen("login");
    return;
  }

  try {
    await api.restoreMineIdIfNeeded();
    const allowed = await checkAdminAccess();
    if (allowed) {
      try {
        await loadPortal();
      } catch (error) {
        setStatus(els.playersStatus, error.message, true);
        setStatus(els.creditsStatus, error.message, true);
      }
    }
  } catch (error) {
    if (isAuthError(error)) {
      api.clearAuth();
    }
    prefillLoginUsername();
    showScreen("login");
    setStatus(els.loginStatus, error.message, true);
  }
}

async function login() {
  const username = els.username.value.trim();
  const password = els.password.value;
  if (!username || !password) {
    setStatus(els.loginStatus, "Enter username and password.", true);
    return;
  }

  setStatus(els.loginStatus, "Signing in…");
  try {
    const response = await api.login(username, password);
    if (!response?.token) {
      throw new Error("Login succeeded but no token was returned.");
    }

    api.saveAuth(response);
    const allowed = await checkAdminAccess();
    if (allowed) {
      await loadPortal();
    }
    setStatus(els.loginStatus, "");
  } catch (error) {
    setStatus(els.loginStatus, error.message, true);
  }
}

function portalLogout() {
  els.password.value = "";
  prefillLoginUsername();
  showScreen("login");
  setStatus(els.loginStatus, "");
}

function signOutCompletely() {
  api.clearAuth();
  portalLogout();
}

els.navButtons.forEach((button) => {
  button.addEventListener("click", () => {
    showPage(button.dataset.adminPage);
    loadCurrentPage().catch((error) => {
      if (state.page === "credits") {
        setStatus(els.creditsStatus, error.message, true);
      } else if (state.page === "players") {
        setStatus(els.playersStatus, error.message, true);
      } else if (state.page === "bans") {
        setStatus(els.bansStatus, error.message, true);
      } else if (state.page === "appeals") {
        setStatus(els.appealsStatus, error.message, true);
      } else if (state.page === "messages") {
        setStatus(els.messagesStatus, error.message, true);
      } else if (state.page === "message-log") {
        setStatus(els.messageLogStatus, error.message, true);
      } else if (state.page === "flagged") {
        setStatus(els.flaggedStatus, error.message, true);
      } else if (state.page === "events") {
        setStatus(els.eventsStatus, error.message, true);
      } else if (state.page === "offworld-news") {
        setStatus(els.offworldNewsStatus, error.message, true);
      } else if (state.page === "testing") {
        setStatus(els.testingStatus, error.message, true);
      }
    });
  });
});

els.eventsNewBtn.addEventListener("click", () => showEventEditor());
els.eventCancelBtn.addEventListener("click", hideEventEditor);
els.eventAddRewardBtn.addEventListener("click", () => addEventRewardRow());
els.eventForm.addEventListener("submit", saveEventForm);
els.eventDeleteBtn.addEventListener("click", () => {
  deleteCurrentEvent().catch((error) => setStatus(els.eventFormStatus, error.message, true));
});
els.eventsList.addEventListener("click", (event) => {
  const button = event.target.closest(".admin-event-edit-btn");
  if (!button) {
    return;
  }
  const eventId = button.dataset.eventId;
  const selected = state.events.find((item) => item.id === eventId);
  if (selected) {
    showEventEditor(selected);
  }
});
els.eventRewards.addEventListener("click", (event) => {
  const button = event.target.closest(".admin-event-reward-remove");
  if (!button) {
    return;
  }
  const row = button.closest(".admin-event-reward-row");
  if (row) {
    row.remove();
  }
});

els.loginBtn.addEventListener("click", login);
els.username.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    login();
  }
});
els.password.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    login();
  }
});
els.logoutBtn.addEventListener("click", portalLogout);
els.deniedLogoutBtn.addEventListener("click", signOutCompletely);
els.playerSearchBtn.addEventListener("click", () => {
  loadPlayers().catch((error) => setStatus(els.playersStatus, error.message, true));
});
els.playerSearch.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    loadPlayers().catch((error) => setStatus(els.playersStatus, error.message, true));
  }
});
els.creditsSearchBtn.addEventListener("click", () => {
  loadCreditsPage().catch((error) => setStatus(els.creditsStatus, error.message, true));
});
els.creditsSearch.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    loadCreditsPage().catch((error) => setStatus(els.creditsStatus, error.message, true));
  }
});

els.gameCreditsForm.addEventListener("submit", (event) => {
  event.preventDefault();
  saveGameCreditsConfig().catch((error) => setGameCreditsStatus(error.message, true));
});

els.gameCreditsResetBtn.addEventListener("click", () => {
  resetGameCreditsForm();
});

els.creditsBody.addEventListener("click", async (event) => {
  const profileButton = event.target.closest(".admin-view-profile-btn");
  if (profileButton) {
    const playerId = profileButton.closest("tr")?.dataset.playerId;
    if (playerId) {
      await openPlayerProfile(playerId);
    }
    return;
  }

  const button = event.target.closest(".admin-save-credits-btn");
  if (!button) {
    return;
  }

  await saveCreditsForRow(button.closest("tr"), els.creditsStatus);
});

els.creditsBody.addEventListener("keydown", async (event) => {
  if (event.key !== "Enter" || !event.target.matches(".admin-credits-input")) {
    return;
  }

  event.preventDefault();
  await saveCreditsForRow(event.target.closest("tr"), els.creditsStatus);
});

els.appealsRefreshBtn.addEventListener("click", () => {
  loadAppealsPage().catch((error) => setStatus(els.appealsStatus, error.message, true));
});

els.bansSearchBtn.addEventListener("click", () => {
  loadBansPage().catch((error) => setStatus(els.bansStatus, error.message, true));
});

els.bansRefreshBtn.addEventListener("click", () => {
  loadBansPage().catch((error) => setStatus(els.bansStatus, error.message, true));
});

els.bansActiveOnly.addEventListener("change", () => {
  loadBansPage().catch((error) => setStatus(els.bansStatus, error.message, true));
});

els.bansSearch.addEventListener("keydown", (event) => {
  if (event.key !== "Enter") {
    return;
  }
  event.preventDefault();
  loadBansPage().catch((error) => setStatus(els.bansStatus, error.message, true));
});

els.bansBody.addEventListener("click", async (event) => {
  const profileButton = event.target.closest(".admin-view-profile-btn");
  if (!profileButton) {
    return;
  }
  const playerId = profileButton.closest("tr")?.dataset.playerId;
  if (playerId) {
    await openPlayerProfile(playerId);
  }
});

els.offworldNewsRegenEditionBtn.addEventListener("click", () => {
  regenerateOffworldNewsEdition().catch((error) => setStatus(els.offworldNewsStatus, error.message, true));
});

els.offworldNewsRegenImagesBtn.addEventListener("click", () => {
  regenerateOffworldNewsImages().catch((error) => setStatus(els.offworldNewsStatus, error.message, true));
});

els.offworldNewsRegenReporterPortraitsBtn.addEventListener("click", () => {
  regenerateOffworldNewsReporterPortraits().catch((error) =>
    setStatus(els.offworldNewsStatus, error.message, true),
  );
});

els.onnPoolForm?.addEventListener("submit", (event) => {
  saveOnnPoolSize(event).catch((error) => setStatus(els.onnPoolStatus, error.message, true));
});

els.messageLogSearchBtn.addEventListener("click", () => {
  loadMessageLogPage().catch((error) => setStatus(els.messageLogStatus, error.message, true));
});

els.messageLogRefreshBtn.addEventListener("click", () => {
  loadMessageLogPage().catch((error) => setStatus(els.messageLogStatus, error.message, true));
});

els.messageLogSearch.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    loadMessageLogPage().catch((error) => setStatus(els.messageLogStatus, error.message, true));
  }
});

els.appealsList.addEventListener("click", async (event) => {
  const dismissButton = event.target.closest(".admin-dismiss-appeal-btn");
  if (dismissButton) {
    const card = dismissButton.closest(".admin-appeal-card");
    const appealId = card?.dataset.appealId;
    const statusEl = card?.querySelector(".admin-appeal-row-status");
    const username = card?.querySelector("h3")?.textContent?.trim() ?? "player";
    if (appealId) {
      await dismissAppeal(appealId, statusEl, username);
    }
    return;
  }

  const profileButton = event.target.closest(".admin-view-profile-btn");
  if (profileButton) {
    const playerId = profileButton.closest(".admin-appeal-card")?.dataset.playerId;
    if (playerId) {
      await openPlayerProfile(playerId);
    }
  }
});

els.playersBody.addEventListener("click", async (event) => {
  const button = event.target.closest(".admin-view-profile-btn");
  if (!button) {
    return;
  }

  const playerId = button.closest("tr")?.dataset.playerId;
  if (playerId) {
    await openPlayerProfile(playerId);
  }
});

els.profileCloseBtn.addEventListener("click", closeAdminProfileModal);
els.profileFlagBtn.addEventListener("click", () => {
  submitProfileFlag().catch((error) => setProfileFlagStatus(error.message, true));
});
els.profileWarningBtn.addEventListener("click", () => {
  submitProfileWarning().catch((error) => setProfileWarningStatus(error.message, true));
});
els.profileBanBtn.addEventListener("click", () => {
  submitProfileBan().catch((error) => setProfileBanStatus(error.message, true));
});
els.profileUnbanBtn.addEventListener("click", () => {
  liftProfileBan().catch((error) => setProfileBanStatus(error.message, true));
});
els.profileModal.addEventListener("click", (event) => {
  if (event.target === els.profileModal) {
    closeAdminProfileModal();
  }
});
document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && !els.profileModal.hidden) {
    closeAdminProfileModal();
  }
});

if (els.testingModeToggle) {
  els.testingModeToggle.addEventListener("change", () => {
    setTestingMode(els.testingModeToggle.checked);
  });
}

els.testingStaffMessageBtn?.addEventListener("click", () => {
  runTestingAction("Sending staff message", (dummyIndex) => api.adminTestingStaffMessage(dummyIndex));
});

els.testingPeerMessageBtn?.addEventListener("click", () => {
  runTestingAction("Sending peer message", (dummyIndex) => api.adminTestingPeerMessage(dummyIndex));
});

els.testingFlaggedMessageBtn?.addEventListener("click", () => {
  runTestingAction("Sending flagged message", (dummyIndex) => api.adminTestingFlaggedMessage(dummyIndex));
});

els.testingBanAppealBtn?.addEventListener("click", () => {
  runTestingAction("Submitting ban appeal", (dummyIndex) => api.adminTestingBanAppeal(dummyIndex));
});

els.testingDummyList?.addEventListener("click", async (event) => {
  const button = event.target.closest(".admin-view-profile-btn");
  if (!button) {
    return;
  }

  const playerId = button.closest("tr")?.dataset.playerId;
  if (playerId) {
    await openPlayerProfile(playerId);
  }
});

async function startAdminPortal() {
  await initI18n({ namespaces: ["admin"] });
  applyTranslations(document);
  wireLocaleSelectors();
  document.addEventListener("rava:localechange", () => applyTranslations(document));
  renderTestingModeUi();
  showPage("dashboard");
  initApiStatusMonitor(api);
  tryRestoreSession();
}

startAdminPortal().catch((error) => console.error("[admin] startup failed", error));
