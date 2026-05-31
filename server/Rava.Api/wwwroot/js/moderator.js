import { RavaApi } from "./api.js";
import { API_BASE_URL } from "./config.js";
import { initStaffMessaging } from "./staff-messages.js";
import { initStaffPlayerMessaging } from "./staff-player-messages.js";
import { initStaffPlayerInbox } from "./staff-player-inbox.js";
import { initFlaggedMessages } from "./flagged-messages.js";
import { renderSocialLinksHtml } from "./profile-social.js";

const api = new RavaApi(API_BASE_URL);

const els = {
  loginScreen: document.getElementById("mod-login-screen"),
  deniedScreen: document.getElementById("mod-denied-screen"),
  portalScreen: document.getElementById("mod-portal-screen"),
  username: document.getElementById("mod-username"),
  password: document.getElementById("mod-password"),
  loginStatus: document.getElementById("mod-login-status"),
  loginBtn: document.getElementById("mod-login-btn"),
  deniedUser: document.getElementById("mod-denied-user"),
  deniedLogoutBtn: document.getElementById("mod-denied-logout-btn"),
  signedIn: document.getElementById("mod-signed-in"),
  adminLink: document.getElementById("mod-admin-link"),
  logoutBtn: document.getElementById("mod-logout-btn"),
  stats: document.getElementById("mod-stats"),
  playerSearch: document.getElementById("mod-player-search"),
  playerSearchBtn: document.getElementById("mod-player-search-btn"),
  playersStatus: document.getElementById("mod-players-status"),
  playersBody: document.getElementById("mod-players-body"),
  profileModal: document.getElementById("mod-profile-modal"),
  profileCloseBtn: document.getElementById("mod-profile-close-btn"),
  profileAvatar: document.getElementById("mod-profile-avatar"),
  profileAvatarImg: document.getElementById("mod-profile-avatar-img"),
  profileAvatarInitials: document.getElementById("mod-profile-avatar-initials"),
  profileUsername: document.getElementById("mod-profile-username"),
  profileMood: document.getElementById("mod-profile-mood"),
  profileNumber: document.getElementById("mod-profile-number"),
  profileEmail: document.getElementById("mod-profile-email"),
  profileMemberSince: document.getElementById("mod-profile-member-since"),
  profileAbout: document.getElementById("mod-profile-about"),
  profileInterests: document.getElementById("mod-profile-interests"),
  profileMusic: document.getElementById("mod-profile-music"),
  profileSocial: document.getElementById("mod-profile-social"),
  profileEmailStat: document.getElementById("mod-profile-email-stat"),
  profileBirthday: document.getElementById("mod-profile-birthday"),
  profileTheme: document.getElementById("mod-profile-theme"),
  profileCredits: document.getElementById("mod-profile-credits"),
  profileGameDay: document.getElementById("mod-profile-game-day"),
  profileMine: document.getElementById("mod-profile-mine"),
  profileWorkers: document.getElementById("mod-profile-workers"),
  profileZones: document.getElementById("mod-profile-zones"),
  profileMineCount: document.getElementById("mod-profile-mine-count"),
  profileActiveFlag: document.getElementById("mod-profile-active-flag"),
  profileActiveFlagComment: document.getElementById("mod-profile-active-flag-comment"),
  profileActiveFlagMeta: document.getElementById("mod-profile-active-flag-meta"),
  profileFlagCommentInput: document.getElementById("mod-profile-flag-comment-input"),
  profileFlagBtn: document.getElementById("mod-profile-flag-btn"),
  profileFlagStatus: document.getElementById("mod-profile-flag-status"),
  profileFlagHistory: document.getElementById("mod-profile-flag-history"),
  profileFlagBox: document.querySelector("#mod-profile-modal .admin-profile-flag-box"),
  profileWarningSummary: document.getElementById("mod-profile-warning-summary"),
  profileWarningReason: document.getElementById("mod-profile-warning-reason"),
  profileWarningBtn: document.getElementById("mod-profile-warning-btn"),
  profileWarningStatus: document.getElementById("mod-profile-warning-status"),
  profileWarningHistory: document.getElementById("mod-profile-warning-history"),
  profileActiveBan: document.getElementById("mod-profile-active-ban"),
  profileActiveBanSummary: document.getElementById("mod-profile-active-ban-summary"),
  profileActiveBanMeta: document.getElementById("mod-profile-active-ban-meta"),
  profileBanLevel: document.getElementById("mod-profile-ban-level"),
  profileBanReason: document.getElementById("mod-profile-ban-reason"),
  profileBanBtn: document.getElementById("mod-profile-ban-btn"),
  profileUnbanBtn: document.getElementById("mod-profile-unban-btn"),
  profileBanStatus: document.getElementById("mod-profile-ban-status"),
  profileBanHistory: document.getElementById("mod-profile-ban-history"),
  profileBanBox: document.querySelector("#mod-profile-modal .admin-profile-ban-box"),
  profileMessageInput: document.getElementById("mod-profile-message-input"),
  profileMessageBtn: document.getElementById("mod-profile-message-btn"),
  profileMessageStatus: document.getElementById("mod-profile-message-status"),
  profileMessageHistory: document.getElementById("mod-profile-message-history"),
};

const state = {
  profilePlayerId: null,
  banLevels: [],
};

const staffMessaging = initStaffMessaging({
  api,
  els: {
    messagesStatus: document.getElementById("mod-messages-status"),
    messagesRecipient: document.getElementById("mod-messages-recipient"),
    messagesBody: document.getElementById("mod-messages-body"),
    messagesSendBtn: document.getElementById("mod-messages-send-btn"),
    messagesRefreshBtn: document.getElementById("mod-messages-refresh-btn"),
    messagesInbox: document.getElementById("mod-messages-inbox"),
    messagesDetail: document.getElementById("mod-messages-detail"),
    messagesNavBadge: null,
  },
  setStatus,
});

const staffPlayerInbox = initStaffPlayerInbox({
  api,
  els: {
    status: document.getElementById("mod-player-inbox-status"),
    refreshBtn: document.getElementById("mod-player-inbox-refresh-btn"),
    inbox: document.getElementById("mod-player-inbox"),
    detail: document.getElementById("mod-player-inbox-detail"),
  },
  setStatus,
  onUnreadChange: () => staffMessaging.refreshUnreadBadge(),
});

const flaggedMessaging = initFlaggedMessages({
  api,
  apiPrefix: "moderator",
  els: {
    status: document.getElementById("mod-flagged-status"),
    refreshBtn: document.getElementById("mod-flagged-refresh-btn"),
    list: document.getElementById("mod-flagged-list"),
    navBadge: null,
  },
  setStatus,
  getBanLevels: () => ensureBanLevelsLoaded().then(() => state.banLevels),
  onOpenPlayerProfile: (playerId) => openPlayerProfile(playerId),
});

const staffPlayerMessaging = initStaffPlayerMessaging({
  api,
  els: {
    profileMessageInput: document.getElementById("mod-profile-message-input"),
    profileMessageBtn: document.getElementById("mod-profile-message-btn"),
    profileMessageStatus: document.getElementById("mod-profile-message-status"),
    profileMessageHistory: document.getElementById("mod-profile-message-history"),
  },
  getPlayerId: () => state.profilePlayerId,
  setStatus: (el, message, isError) => {
    el.textContent = message ?? "";
    el.classList.toggle("error", Boolean(isError && message));
    el.classList.toggle("success", Boolean(!isError && message));
  },
});

function setStatus(el, message, isError = false) {
  el.textContent = message ?? "";
  el.style.color = isError ? "var(--danger)" : "";
}

function formatCredits(value) {
  return Number(value ?? 0).toLocaleString(undefined, {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  });
}

function formatDate(value) {
  if (!value) {
    return "—";
  }
  return new Date(value).toLocaleString();
}

function showScreen(screen) {
  els.loginScreen.hidden = screen !== "login";
  els.deniedScreen.hidden = screen !== "denied";
  els.portalScreen.hidden = screen !== "portal";
}

function prefillLoginUsername() {
  if (!els.username.value && api.username) {
    els.username.value = api.username;
  }
}

function renderStats(dashboard) {
  const items = [
    ["Players", dashboard.playerCount],
    ["Mines", dashboard.mineCount],
    ["Friendships", dashboard.friendshipCount],
    ["Game day", dashboard.currentGameDay],
    ["Total credits", formatCredits(dashboard.totalCredits)],
    ["Sign-up credits", formatCredits(dashboard.signUpCredits)],
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

function renderPlayerProfile(profile) {
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
  els.profileCredits.textContent = formatCredits(profile.credits);
  els.profileGameDay.textContent = String(profile.currentGameDay ?? "—");
  els.profileMine.textContent = profile.mineName || "—";
  els.profileWorkers.textContent = String(profile.workerCount ?? 0);
  els.profileZones.textContent = String(profile.zoneCount ?? 0);
  els.profileMineCount.textContent = String(profile.mineCount ?? 0);
  const isProtectedAdmin = Boolean(profile.isProtectedAdmin);
  const isModerator = Boolean(profile.isModerator);
  const hideModeration = isProtectedAdmin || isModerator;
  if (els.profileFlagBox) {
    els.profileFlagBox.hidden = hideModeration;
  }
  if (els.profileBanBox) {
    els.profileBanBox.hidden = hideModeration;
  }
  if (document.querySelector("#mod-profile-modal .admin-profile-warning-box")) {
    document.querySelector("#mod-profile-modal .admin-profile-warning-box").hidden = hideModeration;
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

async function ensureBanLevelsLoaded() {
  if (state.banLevels.length) {
    return;
  }

  state.banLevels = await api.moderatorBanLevels();
  els.profileBanLevel.innerHTML = state.banLevels
    .map(
      (level) =>
        `<option value="${escapeHtml(level.code)}">${escapeHtml(level.label)}</option>`
    )
    .join("");
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
  if (!state.profilePlayerId) {
    return;
  }

  const banLevel = els.profileBanLevel.value;
  if (!banLevel) {
    setProfileBanStatus("Select a ban level.", true);
    return;
  }

  els.profileBanBtn.disabled = true;
  setProfileBanStatus("Applying ban...");
  try {
    const result = await api.moderatorBanPlayer(
      state.profilePlayerId,
      banLevel,
      els.profileBanReason.value.trim()
    );
    els.profileBanReason.value = "";
    setProfileBanStatus(result.message, false);
    const profile = await api.moderatorPlayerProfile(state.profilePlayerId);
    renderPlayerProfile(profile);
    await loadPortal();
  } catch (error) {
    setProfileBanStatus(error.message, true);
  } finally {
    els.profileBanBtn.disabled = false;
  }
}

async function liftProfileBan() {
  if (!state.profilePlayerId) {
    return;
  }

  els.profileUnbanBtn.disabled = true;
  setProfileBanStatus("Lifting ban...");
  try {
    const result = await api.moderatorUnbanPlayer(state.profilePlayerId);
    setProfileBanStatus(result.message, false);
    const profile = await api.moderatorPlayerProfile(state.profilePlayerId);
    renderPlayerProfile(profile);
    await loadPortal();
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
  if (!state.profilePlayerId) {
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
    const result = await api.moderatorWarnPlayer(state.profilePlayerId, reason);
    els.profileWarningReason.value = "";
    setProfileWarningStatus(result.message, false);
    const profile = await api.moderatorPlayerProfile(state.profilePlayerId);
    renderPlayerProfile(profile);
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
  if (!state.profilePlayerId) {
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
    const result = await api.moderatorFlagProfile(state.profilePlayerId, comment);
    els.profileFlagCommentInput.value = "";
    setProfileFlagStatus(result.message, false);
    const profile = await api.moderatorPlayerProfile(state.profilePlayerId);
    renderPlayerProfile(profile);
  } catch (error) {
    setProfileFlagStatus(error.message, true);
  } finally {
    els.profileFlagBtn.disabled = false;
  }
}

function openProfileModal() {
  els.profileModal.hidden = false;
  document.body.appendChild(els.profileModal);
}

function closeProfileModal() {
  els.profileModal.hidden = true;
}

async function openPlayerProfile(playerId) {
  try {
    await ensureBanLevelsLoaded();
    state.profilePlayerId = playerId;
    els.profileFlagCommentInput.value = "";
    els.profileBanReason.value = "";
    staffPlayerMessaging.clearForm();
    setProfileFlagStatus("");
    setProfileWarningStatus("");
    setProfileBanStatus("");
    if (els.profileWarningReason) {
      els.profileWarningReason.value = "";
    }
    const profile = await api.moderatorPlayerProfile(playerId);
    renderPlayerProfile(profile);
    await staffPlayerMessaging.loadHistory();
    openProfileModal();
  } catch (error) {
    setStatus(els.playersStatus, error.message, true);
  }
}

function renderPlayers(players) {
  if (!players.length) {
    els.playersBody.innerHTML = `<tr><td colspan="7">No players found.</td></tr>`;
    return;
  }

  els.playersBody.innerHTML = players
    .map(
      (player) => `
        <tr data-player-id="${player.id}">
          <td>${escapeHtml(player.username)}</td>
          <td>${escapeHtml(player.email)}</td>
          <td>${formatCredits(player.credits)}</td>
          <td>${player.mineCount}</td>
          <td>${formatDate(player.createdAt)}</td>
          <td>${formatPlayerBanStatus(player)}</td>
          <td>
            <button type="button" class="btn accent mod-primary-btn mod-view-profile-btn">View profile</button>
          </td>
        </tr>`
    )
    .join("");
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

async function loadPortal() {
  await ensureBanLevelsLoaded();
  setStatus(els.playersStatus, "Loading…");
  const [dashboard, players] = await Promise.all([
    api.moderatorDashboard(),
    api.moderatorPlayers(els.playerSearch.value.trim()),
    staffMessaging.loadMessages(),
    staffPlayerInbox.loadMessages(),
    flaggedMessaging.loadReviews(),
  ]);
  renderStats(dashboard);
  renderPlayers(players.players ?? []);
  setStatus(els.playersStatus, `${players.players?.length ?? 0} player(s) shown`);
}

async function checkModeratorAccess() {
  const access = await api.moderatorAccess();
  if (!access.isModerator) {
    const name = access.username || api.username || "unknown";
    els.deniedUser.textContent =
      `Signed in as "${name}". Add this exact username to Moderator:Usernames in appsettings.Development.json, then restart the API. Admins can also use the admin portal.`;
    showScreen("denied");
    return null;
  }

  els.signedIn.textContent = access.isAdmin
    ? `Signed in as ${access.username} (admin + moderator)`
    : `Signed in as ${access.username}`;
  els.adminLink.hidden = !access.isAdmin;
  showScreen("portal");
  return access;
}

function isAuthError(error) {
  const message = (error?.message ?? "").toLowerCase();
  return message.includes("session expired")
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
    const access = await checkModeratorAccess();
    if (access) {
      try {
        await loadPortal();
      } catch (error) {
        setStatus(els.playersStatus, error.message, true);
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
    const access = await checkModeratorAccess();
    if (access) {
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
  loadPortal().catch((error) => setStatus(els.playersStatus, error.message, true));
});
els.playerSearch.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    loadPortal().catch((error) => setStatus(els.playersStatus, error.message, true));
  }
});

els.playersBody.addEventListener("click", async (event) => {
  const button = event.target.closest(".mod-view-profile-btn");
  if (!button) {
    return;
  }

  const playerId = button.closest("tr")?.dataset.playerId;
  if (playerId) {
    await openPlayerProfile(playerId);
  }
});

els.profileCloseBtn.addEventListener("click", closeProfileModal);
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
    closeProfileModal();
  }
});
document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && !els.profileModal.hidden) {
    closeProfileModal();
  }
});

tryRestoreSession();
