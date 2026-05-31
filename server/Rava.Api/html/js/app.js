import { RavaApi } from "./api.js";
import { GRID_SIZE, ORE_TYPES, SUPPLY_TYPES, API_BASE_URL } from "./config.js";
import { initApiStatusMonitor } from "./api-status.js";
import { initPlayerMessaging } from "./player-messages.js";
import { renderSocialLinksHtml, hasSocialLinks } from "./profile-social.js";

const api = new RavaApi(API_BASE_URL);

let tradeOreTypes = { ...ORE_TYPES };
let tradeSupplyTypes = { ...SUPPLY_TYPES };

function applyTradeItems(items) {
  const nextOreTypes = {};
  const nextSupplyTypes = {};

  for (const item of items ?? []) {
    const meta = {
      displayName: item.displayName || item.itemType,
      color: item.color || "#888",
      basePrice: Number(item.basePrice ?? 0),
    };

    if (item.category === "Ore") {
      if (item.isEmergencySource) {
        meta.isEmergencySource = true;
      }
      nextOreTypes[item.itemType] = meta;
      continue;
    }

    if (item.category === "Supply") {
      if (item.uiSymbol) {
        meta.symbol = item.uiSymbol;
      }
      nextSupplyTypes[item.itemType] = meta;
    }
  }

  if (Object.keys(nextOreTypes).length > 0) {
    tradeOreTypes = nextOreTypes;
  }
  if (Object.keys(nextSupplyTypes).length > 0) {
    tradeSupplyTypes = nextSupplyTypes;
  }
}

async function loadTradeItems() {
  try {
    const response = await api.getTradeItems();
    applyTradeItems(response?.items);
  } catch {
    // Keep bundled defaults when the API is offline.
  }
}

function setMessagesStatus(message, isError = false) {
  const el = document.getElementById("messages-status");
  if (!el) {
    return;
  }
  el.textContent = message ?? "";
  el.classList.toggle("error", Boolean(isError && message));
  el.classList.toggle("success", Boolean(!isError && message));
}

const playerMessaging = initPlayerMessaging({
  api,
  els: {
    messagesStatus: document.getElementById("messages-status"),
    messagesInbox: document.getElementById("player-messages-inbox"),
    messagesDetail: document.getElementById("player-messages-detail"),
    messagesNavBadge: document.getElementById("messages-nav-badge"),
    messageRecipient: document.getElementById("player-message-recipient"),
    messageBody: document.getElementById("player-message-body"),
    messageSendBtn: document.getElementById("player-message-send-btn"),
    staffRecipient: document.getElementById("player-staff-recipient"),
    staffBody: document.getElementById("player-staff-body"),
    staffSendBtn: document.getElementById("player-staff-send-btn"),
  },
  setStatus: (_el, message, isError) => setMessagesStatus(message, isError),
});

const state = {
  mine: null,
  finances: null,
  market: null,
  profile: null,
  friends: null,
  selectedZoneId: null,
  authMode: "login",
  banMessage: "",
  nextDayAtUtc: null,
};

let utcClockTimer;
let utcRefreshTimer;
const eventModalQueue = [];

const els = {
  loginScreen: document.getElementById("login-screen"),
  gameScreen: document.getElementById("game-screen"),
  usernameGroup: document.getElementById("username-group"),
  username: document.getElementById("username"),
  email: document.getElementById("email"),
  emailGroup: document.getElementById("email-group"),
  birthdayGroup: document.getElementById("birthday-group"),
  birthdayMonth: document.getElementById("birthday-month"),
  birthdayDay: document.getElementById("birthday-day"),
  birthdayYear: document.getElementById("birthday-year"),
  birthdayMonthOptions: document.getElementById("birthday-month-options"),
  birthdayDayOptions: document.getElementById("birthday-day-options"),
  birthdayYearOptions: document.getElementById("birthday-year-options"),
  forgotGroup: document.getElementById("forgot-group"),
  forgotEmail: document.getElementById("forgot-email"),
  resetGroup: document.getElementById("reset-group"),
  resetToken: document.getElementById("reset-token"),
  newPassword: document.getElementById("new-password"),
  confirmPassword: document.getElementById("confirm-password"),
  passwordGroup: document.getElementById("password-group"),
  banAppealGroup: document.getElementById("ban-appeal-group"),
  banAppealNotice: document.getElementById("ban-appeal-notice"),
  banAppealMessage: document.getElementById("ban-appeal-message"),
  banAppealBtn: document.getElementById("ban-appeal-btn"),
  password: document.getElementById("password"),
  loginStatus: document.getElementById("login-status"),
  authToast: document.getElementById("auth-toast"),
  toggleMode: document.getElementById("toggle-mode"),
  loginBtn: document.getElementById("login-btn"),
  registerBtn: document.getElementById("register-btn"),
  forgotBtn: document.getElementById("forgot-btn"),
  sendResetBtn: document.getElementById("send-reset-btn"),
  resetPasswordBtn: document.getElementById("reset-password-btn"),
  backLoginBtn: document.getElementById("back-login-btn"),
  logoutBtn: document.getElementById("logout-btn"),
  playerName: document.getElementById("player-name"),
  credits: document.getElementById("credits"),
  day: document.getElementById("day"),
  utcClock: document.getElementById("utc-clock"),
  statusBar: document.getElementById("status-bar"),
  mineGrid: document.getElementById("mine-grid"),
  zoneInfo: document.getElementById("zone-info"),
  workerList: document.getElementById("worker-list"),
  profileBtn: document.getElementById("profile-btn"),
  financeBtn: document.getElementById("finance-btn"),
  friendsBtn: document.getElementById("friends-btn"),
  messagesBtn: document.getElementById("messages-btn"),
  messagesNavBadge: document.getElementById("messages-nav-badge"),
  messagesModal: document.getElementById("messages-modal"),
  messagesStatus: document.getElementById("messages-status"),
  messagesInbox: document.getElementById("player-messages-inbox"),
  messagesDetail: document.getElementById("player-messages-detail"),
  tradeMarketBtn: document.getElementById("trade-market-btn"),
  storeBtn: document.getElementById("store-btn"),
  shippingBtn: document.getElementById("shipping-btn"),
  financeModal: document.getElementById("finance-modal"),
  supplyModal: document.getElementById("supply-modal"),
  storeModal: document.getElementById("store-modal"),
  storeMarketInfo: document.getElementById("store-market-info"),
  storeSupplyList: document.getElementById("store-supply-list"),
  shippingModal: document.getElementById("shipping-modal"),
  shippingSummary: document.getElementById("shipping-summary"),
  shippingCargoList: document.getElementById("shipping-cargo-list"),
  dayModal: document.getElementById("day-modal"),
  financeSummary: document.getElementById("finance-summary"),
  financeTransactions: document.getElementById("finance-transactions"),
  emergencyBtn: document.getElementById("emergency-btn"),
  marketInfo: document.getElementById("market-info"),
  supplyList: document.getElementById("supply-list"),
  oreList: document.getElementById("ore-list"),
  dayReportTitle: document.getElementById("day-report-title"),
  dayReportBody: document.getElementById("day-report-body"),
  closeDayReport: document.getElementById("close-day-report"),
  eventModal: document.getElementById("event-modal"),
  eventModalTitle: document.getElementById("event-modal-title"),
  eventModalMessage: document.getElementById("event-modal-message"),
  eventModalChallenge: document.getElementById("event-modal-challenge"),
  eventModalRewardsHeading: document.getElementById("event-modal-rewards-heading"),
  eventModalRewards: document.getElementById("event-modal-rewards"),
  eventModalClose: document.getElementById("event-modal-close"),
  profileModal: document.getElementById("profile-modal"),
  profileCard: document.getElementById("profile-card"),
  profileCloseBtn: document.getElementById("profile-close-btn"),
  profileFlagNotice: document.getElementById("profile-flag-notice"),
  profileFlagComment: document.getElementById("profile-flag-comment"),
  profileAvatar: document.getElementById("profile-avatar"),
  profileAvatarColumn: document.getElementById("profile-avatar-column"),
  profileAvatarImg: document.getElementById("profile-avatar-img"),
  profileAvatarInitials: document.getElementById("profile-avatar-initials"),
  profileUsername: document.getElementById("profile-username"),
  profileMoodDisplay: document.getElementById("profile-mood-display"),
  profileNumber: document.getElementById("profile-number"),
  profileMemberSince: document.getElementById("profile-member-since"),
  profileAboutView: document.getElementById("profile-about-view"),
  profileInterestsView: document.getElementById("profile-interests-view"),
  profileMusicView: document.getElementById("profile-music-view"),
  profileSocialView: document.getElementById("profile-social-view"),
  profileMineName: document.getElementById("profile-mine-name"),
  profileGameDay: document.getElementById("profile-game-day"),
  profileCredits: document.getElementById("profile-credits"),
  profileWorkers: document.getElementById("profile-workers"),
  profileZones: document.getElementById("profile-zones"),
  profileSidebarNumber: document.getElementById("profile-sidebar-number"),
  profileSidebarMemberSince: document.getElementById("profile-sidebar-member-since"),
  profileEditNumber: document.getElementById("profile-edit-number"),
  profileAddFriendNumber: document.getElementById("profile-add-friend-number"),
  profileAddFriendSubmitBtn: document.getElementById("profile-add-friend-submit-btn"),
  profileAddFriendStatus: document.getElementById("profile-add-friend-status"),
  profileEditPanel: document.getElementById("profile-edit-panel"),
  profilePhotoUpload: document.getElementById("profile-photo-upload"),
  profilePhotoInput: document.getElementById("profile-photo-input"),
  profilePhotoChooseBtn: document.getElementById("profile-photo-choose-btn"),
  profilePhotoBtn: document.getElementById("profile-photo-btn"),
  profilePhotoStatus: document.getElementById("profile-photo-status"),
  profileMoodInput: document.getElementById("profile-mood-input"),
  profileAboutInput: document.getElementById("profile-about-input"),
  profileInterestsInput: document.getElementById("profile-interests-input"),
  profileMusicInput: document.getElementById("profile-music-input"),
  profileDiscordInput: document.getElementById("profile-discord-input"),
  profileBlueskyInput: document.getElementById("profile-bluesky-input"),
  profileTwitterInput: document.getElementById("profile-twitter-input"),
  profileYoutubeInput: document.getElementById("profile-youtube-input"),
  profileFacebookInput: document.getElementById("profile-facebook-input"),
  profileSaveBtn: document.getElementById("profile-save-btn"),
  profileSaveStatus: document.getElementById("profile-save-status"),
  profileFriendPanel: document.getElementById("profile-friend-panel"),
  profileFriendStatus: document.getElementById("profile-friend-status"),
  profileAddFriendBtn: document.getElementById("profile-add-friend-btn"),
  profileAcceptFriendBtn: document.getElementById("profile-accept-friend-btn"),
  profileMessageFriendBtn: document.getElementById("profile-message-friend-btn"),
  profileRemoveFriendBtn: document.getElementById("profile-remove-friend-btn"),
  profileFriendActionStatus: document.getElementById("profile-friend-action-status"),
  friendsModal: document.getElementById("friends-modal"),
  addFriendNumber: document.getElementById("add-friend-number"),
  addFriendBtn: document.getElementById("add-friend-btn"),
  addFriendStatus: document.getElementById("add-friend-status"),
  friendsList: document.getElementById("friends-list"),
  incomingFriendsList: document.getElementById("incoming-friends-list"),
  outgoingFriendsList: document.getElementById("outgoing-friends-list"),
};

function showStatus(message, isError = false) {
  els.statusBar.textContent = message ?? "";
  els.statusBar.classList.toggle("error", Boolean(isError && message));
}

let authToastTimer;

function normalizeStatusVariant(variant) {
  if (variant === true || variant === "error") {
    return "error";
  }
  if (variant === "success") {
    return "success";
  }
  if (variant === "info") {
    return "info";
  }
  return "";
}

function showLoginStatus(message, variant = "") {
  els.loginStatus.textContent = message ?? "";
  els.loginStatus.classList.remove("error", "success", "info");
  const kind = normalizeStatusVariant(variant);
  if (message && kind) {
    els.loginStatus.classList.add(kind);
  }
}

function hideAuthToast() {
  if (authToastTimer) {
    clearTimeout(authToastTimer);
    authToastTimer = undefined;
  }
  els.authToast.hidden = true;
  els.authToast.classList.remove("visible", "success", "error");
  els.authToast.textContent = "";
}

function showAuthToast(message, variant = "success") {
  hideAuthToast();
  const kind = normalizeStatusVariant(variant) || "success";
  els.authToast.textContent = message;
  els.authToast.hidden = false;
  els.authToast.classList.add(kind, "visible");
  authToastTimer = setTimeout(hideAuthToast, 6000);
}

function setAuthMode(mode) {
  state.authMode = mode;
  const isLogin = mode === "login";
  const isRegister = mode === "register";
  const isForgot = mode === "forgot";
  const isReset = mode === "reset";
  const isBanAppeal = mode === "ban-appeal";

  els.usernameGroup.hidden = isForgot || isReset;

  els.emailGroup.hidden = !isRegister;
  els.birthdayGroup.hidden = !isRegister;
  els.forgotGroup.hidden = !isForgot;
  els.resetGroup.hidden = !isReset;
  els.passwordGroup.hidden = isForgot || isReset || isBanAppeal;
  els.banAppealGroup.hidden = !isBanAppeal;

  els.loginBtn.hidden = !isLogin;
  els.registerBtn.hidden = !isRegister;
  els.toggleMode.hidden = isForgot || isReset || isBanAppeal;
  els.forgotBtn.hidden = !isLogin;
  els.sendResetBtn.hidden = !isForgot;
  els.resetPasswordBtn.hidden = !isReset;
  els.banAppealBtn.hidden = !isBanAppeal;
  els.backLoginBtn.hidden = !isForgot && !isReset && !isBanAppeal;

  if (isRegister) {
    els.toggleMode.textContent = "Switch to Login";
    ensureBirthdayDropdownsReady();
    showLoginStatus("Email and birthday are required to create an account.", "info");
  } else if (isForgot) {
    showLoginStatus("Enter the email on your account. We'll send a reset link.", "info");
  } else if (isReset) {
    showLoginStatus("Choose a new password (at least 8 characters).", "info");
  } else if (isBanAppeal) {
    els.banAppealNotice.textContent = state.banMessage;
    els.banAppealMessage.value = "";
    showLoginStatus("Send a message to the admin team requesting ban removal.", "info");
  } else {
    els.toggleMode.textContent = "Switch to Register";
    showLoginStatus("");
  }
}

function isValidEmail(value) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

const BIRTHDAY_MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

function padBirthdayPart(value) {
  return String(value).padStart(2, "0");
}

function fillDatalist(datalist, values) {
  datalist.innerHTML = "";
  for (const value of values) {
    const option = document.createElement("option");
    option.value = String(value);
    datalist.appendChild(option);
  }
}

function parseBirthdayMonth(value) {
  const trimmed = (value ?? "").trim();
  if (!trimmed) {
    return null;
  }

  const asNumber = Number(trimmed);
  if (Number.isInteger(asNumber) && asNumber >= 1 && asNumber <= 12) {
    return asNumber;
  }

  const index = BIRTHDAY_MONTHS.findIndex(
    (name) => name.toLowerCase() === trimmed.toLowerCase(),
  );
  return index >= 0 ? index + 1 : null;
}

function parseBirthdayDay(value) {
  const trimmed = (value ?? "").trim();
  if (!trimmed) {
    return null;
  }

  const day = Number(trimmed);
  return Number.isInteger(day) && day >= 1 && day <= 31 ? day : null;
}

function parseBirthdayYear(value) {
  const trimmed = (value ?? "").trim();
  if (!trimmed) {
    return null;
  }

  const year = Number(trimmed);
  return Number.isInteger(year) && year >= 1900 && year <= 9999 ? year : null;
}

function daysInBirthdayMonth(month, year) {
  if (!month || !year) {
    return 31;
  }

  return new Date(Number(year), Number(month), 0).getDate();
}

function populateBirthdayDayOptions() {
  const month = parseBirthdayMonth(els.birthdayMonth.value);
  const year = parseBirthdayYear(els.birthdayYear.value);
  const maxDays = daysInBirthdayMonth(month, year);
  const days = [];
  for (let day = 1; day <= maxDays; day += 1) {
    days.push(day);
  }

  fillDatalist(els.birthdayDayOptions, days);

  const currentDay = parseBirthdayDay(els.birthdayDay.value);
  if (currentDay && currentDay > maxDays) {
    els.birthdayDay.value = "";
  }
}

function populateBirthdayMonthOptions() {
  const values = [];
  for (let month = 1; month <= 12; month += 1) {
    values.push(BIRTHDAY_MONTHS[month - 1]);
    values.push(String(month));
  }

  fillDatalist(els.birthdayMonthOptions, values);
}

function populateBirthdayYearOptions() {
  if (els.birthdayYearOptions.children.length > 0) {
    return;
  }

  const currentYear = new Date().getUTCFullYear();
  const years = [];
  for (let year = currentYear - 13; year >= currentYear - 120; year -= 1) {
    years.push(year);
  }

  fillDatalist(els.birthdayYearOptions, years);
}

function initBirthdayDropdowns() {
  populateBirthdayMonthOptions();
  populateBirthdayYearOptions();
  populateBirthdayDayOptions();

  if (els.birthdayMonth.dataset.initialized === "true") {
    return;
  }

  els.birthdayMonth.addEventListener("input", populateBirthdayDayOptions);
  els.birthdayMonth.addEventListener("change", populateBirthdayDayOptions);
  els.birthdayYear.addEventListener("input", populateBirthdayDayOptions);
  els.birthdayYear.addEventListener("change", populateBirthdayDayOptions);
  els.birthdayMonth.dataset.initialized = "true";
}

function getBirthdayValue() {
  const month = parseBirthdayMonth(els.birthdayMonth.value);
  const day = parseBirthdayDay(els.birthdayDay.value);
  const year = parseBirthdayYear(els.birthdayYear.value);
  if (!month || !day || !year) {
    return "";
  }

  if (day > daysInBirthdayMonth(month, year)) {
    return "";
  }

  return `${year}-${padBirthdayPart(month)}-${padBirthdayPart(day)}`;
}

function resetBirthdayDropdowns() {
  els.birthdayMonth.value = "";
  els.birthdayDay.value = "";
  els.birthdayYear.value = "";
  populateBirthdayDayOptions();
}

function ensureBirthdayDropdownsReady() {
  initBirthdayDropdowns();
}

function showScreen(screen) {
  els.loginScreen.hidden = screen !== "login";
  els.gameScreen.hidden = screen !== "game";
  document.body.classList.toggle("is-authenticated", screen === "game");
  if (screen !== "game") {
    closeModals();
  }
  if (screen === "game") {
    startUtcTimers();
  } else {
    stopUtcTimers();
  }
}

function closeModals() {
  els.financeModal.hidden = true;
  els.supplyModal.hidden = true;
  els.dayModal.hidden = true;
  els.profileModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  els.eventModal.hidden = true;
}

function openModal(modal) {
  if (!modal) {
    return;
  }

  modal.hidden = false;
  document.body.appendChild(modal);
}

function profileInitials(username) {
  const parts = (username ?? "").trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return "??";
  }
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  return `${parts[0][0] ?? ""}${parts[1][0] ?? ""}`.toUpperCase();
}

function formatProfileDate(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "---";
  }
  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

function setProfileText(el, value, emptyText) {
  const text = (value ?? "").trim();
  el.textContent = text || emptyText;
  el.classList.toggle("empty", !text);
}

function applyProfileTheme() {
  els.profileCard.className = "profile-card theme-classic";
}

function renderProfileAvatar(profile) {
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
}

function renderProfile(profile) {
  state.profile = profile;
  applyProfileTheme();
  renderProfileAvatar(profile);
  els.profileUsername.textContent = profile.username;
  els.profileMoodDisplay.textContent = profile.mood || "Ready to mine.";
  els.profileNumber.textContent = profile.profileNumber || "---";
  els.profileSidebarNumber.textContent = profile.profileNumber || "---";
  const signedUp = formatProfileDate(profile.memberSince);
  els.profileMemberSince.textContent = `Member since ${signedUp}`;
  els.profileSidebarMemberSince.textContent = signedUp;
  setProfileText(els.profileAboutView, profile.aboutMe, "No bio yet.");
  setProfileText(els.profileInterestsView, profile.interests, "Nothing listed yet.");
  setProfileText(els.profileMusicView, profile.music, "Silence in the void.");
  els.profileSocialView.innerHTML = renderSocialLinksHtml(profile);
  els.profileSocialView.classList.toggle("empty", !hasSocialLinks(profile));
  els.profileMineName.textContent = profile.mineName ?? "---";
  els.profileGameDay.textContent = String(profile.currentGameDay ?? "---");
  els.profileCredits.textContent = Number(profile.credits ?? 0).toLocaleString();
  els.profileWorkers.textContent = String(profile.workerCount ?? 0);
  els.profileZones.textContent = String(profile.zoneCount ?? 0);

  const isOwner = Boolean(profile.isOwner);
  els.profileEditPanel.hidden = !isOwner;
  els.profilePhotoUpload.hidden = !isOwner;
  if (isOwner) {
    els.profileEditNumber.textContent = profile.profileNumber || "---";
    els.profileMoodInput.value = profile.mood ?? "";
    els.profileAboutInput.value = profile.aboutMe ?? "";
    els.profileInterestsInput.value = profile.interests ?? "";
    els.profileMusicInput.value = profile.music ?? "";
    els.profileDiscordInput.value = profile.discord ?? "";
    els.profileBlueskyInput.value = profile.bluesky ?? "";
    els.profileTwitterInput.value = profile.twitter ?? "";
    els.profileYoutubeInput.value = profile.youtube ?? "";
    els.profileFacebookInput.value = profile.facebook ?? "";
  }

  els.profileSaveStatus.textContent = "";
  els.profileSaveStatus.classList.remove("error", "success");
  els.profileAddFriendStatus.textContent = "";
  els.profileAddFriendStatus.classList.remove("error", "success");
  els.profilePhotoStatus.textContent = "";
  els.profilePhotoStatus.classList.remove("error", "success");
  els.profilePhotoBtn.disabled = true;
  els.profilePhotoChooseBtn.textContent = "Choose Photo";
  renderProfileFlagNotice(profile);
  renderProfileFriendPanel(profile);
}

function renderProfileFlagNotice(profile) {
  const activeFlag = profile.activeFlag;
  if (profile.isOwner && activeFlag) {
    els.profileFlagNotice.hidden = false;
    els.profileFlagComment.textContent = activeFlag.comment;
  } else {
    els.profileFlagNotice.hidden = true;
    els.profileFlagComment.textContent = "";
  }
}

async function uploadProfilePhoto() {
  const file = els.profilePhotoInput.files?.[0];
  if (!file) {
    els.profilePhotoStatus.textContent = "Choose a JPEG, PNG, WebP, or GIF image first.";
    els.profilePhotoStatus.classList.add("error");
    return;
  }

  els.profilePhotoStatus.textContent = "Uploading photo...";
  els.profilePhotoStatus.classList.remove("error", "success");

  try {
    const profile = await api.uploadProfileAvatar(file);
    els.profilePhotoInput.value = "";
    renderProfile(profile);
    els.profilePhotoStatus.textContent = "Profile photo updated.";
    els.profilePhotoStatus.classList.add("success");
  } catch (error) {
    els.profilePhotoStatus.textContent = error.message;
    els.profilePhotoStatus.classList.add("error");
  }
}

function renderProfileFriendPanel(profile) {
  const isOwner = Boolean(profile.isOwner);
  els.profileFriendPanel.hidden = isOwner;
  els.profileFriendActionStatus.textContent = "";
  els.profileFriendActionStatus.classList.remove("error", "success");

  if (isOwner) {
    return;
  }

  const status = profile.friendshipStatus ?? "none";
  els.profileAddFriendBtn.hidden = status !== "none";
  els.profileAcceptFriendBtn.hidden = status !== "pending_incoming";
  els.profileMessageFriendBtn.hidden = status !== "accepted";
  els.profileRemoveFriendBtn.hidden = !["pending_outgoing", "pending_incoming", "accepted"].includes(status);

  switch (status) {
    case "accepted":
      els.profileFriendStatus.textContent = `${profile.username} is your friend.`;
      els.profileRemoveFriendBtn.textContent = "Remove Friend";
      break;
    case "pending_outgoing":
      els.profileFriendStatus.textContent = "Friend request sent.";
      els.profileRemoveFriendBtn.textContent = "Cancel Request";
      break;
    case "pending_incoming":
      els.profileFriendStatus.textContent = `${profile.username} wants to be friends.`;
      els.profileRemoveFriendBtn.textContent = "Decline";
      break;
    default:
      els.profileFriendStatus.textContent = "Not friends yet.";
      els.profileRemoveFriendBtn.textContent = "Remove";
      break;
  }
}

async function openMessagesModal(toPlayerId = null) {
  els.financeModal.hidden = true;
  els.supplyModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  els.profileModal.hidden = true;
  els.dayModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = false;
  openModal(els.messagesModal);
  playerMessaging.openToPlayer(toPlayerId);
  setMessagesStatus("Loading...");
  try {
    await playerMessaging.loadMessages();
  } catch (error) {
    setMessagesStatus(error.message, true);
  }
}

function setFriendsStatus(message, isError = false) {
  els.addFriendStatus.textContent = message ?? "";
  els.addFriendStatus.classList.toggle("error", Boolean(isError && message));
  els.addFriendStatus.classList.toggle("success", Boolean(!isError && message));
}

function createFriendItem(
  friend,
  { showAccept = false, showRemove = false, showMessage = false, removeLabel = "Remove" } = {},
) {
  const item = document.createElement("div");
  item.className = "friend-item";

  const head = document.createElement("div");
  head.className = "friend-item-head";

  const name = document.createElement("button");
  name.type = "button";
  name.className = "friend-item-name btn ghost";
  name.textContent = friend.username;
  name.addEventListener("click", () => openProfile(friend.username));

  const number = document.createElement("span");
  number.className = "friend-item-number";
  number.textContent = friend.profileNumber;

  head.append(name, number);

  const mood = document.createElement("div");
  mood.className = "friend-item-mood";
  mood.textContent = friend.mood || "Ready to mine.";

  item.append(head, mood);

  if (showAccept || showRemove || showMessage) {
    const actions = document.createElement("div");
    actions.className = "friend-item-actions";

    if (showMessage) {
      const messageBtn = document.createElement("button");
      messageBtn.type = "button";
      messageBtn.className = "btn primary";
      messageBtn.textContent = "Message";
      messageBtn.addEventListener("click", () => {
        openMessagesModal(friend.playerId).catch((error) => setFriendsStatus(error.message, true));
      });
      actions.appendChild(messageBtn);
    }

    if (showAccept) {
      const acceptBtn = document.createElement("button");
      acceptBtn.type = "button";
      acceptBtn.className = "btn success";
      acceptBtn.textContent = "Accept";
      acceptBtn.addEventListener("click", () => {
        acceptFriendRequest(friend.friendshipId).catch((error) => setFriendsStatus(error.message, true));
      });
      actions.appendChild(acceptBtn);
    }

    if (showRemove) {
      const removeBtn = document.createElement("button");
      removeBtn.type = "button";
      removeBtn.className = "btn ghost";
      removeBtn.textContent = removeLabel;
      removeBtn.addEventListener("click", () => {
        removeFriendRequest(friend.friendshipId).catch((error) => setFriendsStatus(error.message, true));
      });
      actions.appendChild(removeBtn);
    }

    item.appendChild(actions);
  }

  return item;
}

function renderFriendsPanel() {
  const friends = state.friends;
  els.friendsList.innerHTML = "";
  els.incomingFriendsList.innerHTML = "";
  els.outgoingFriendsList.innerHTML = "";

  if (!friends) {
    return;
  }

  if ((friends.friends ?? []).length === 0) {
    els.friendsList.innerHTML = "<p class='friends-status'>No friends yet. Add someone by profile number.</p>";
  } else {
    for (const friend of friends.friends) {
      els.friendsList.appendChild(
        createFriendItem(friend, { showRemove: true, showMessage: true, removeLabel: "Remove Friend" }),
      );
    }
  }

  if ((friends.incomingRequests ?? []).length === 0) {
    els.incomingFriendsList.innerHTML = "<p class='friends-status'>No incoming requests.</p>";
  } else {
    for (const friend of friends.incomingRequests) {
      els.incomingFriendsList.appendChild(
        createFriendItem(friend, { showAccept: true, showRemove: true, removeLabel: "Decline" }),
      );
    }
  }

  if ((friends.outgoingRequests ?? []).length === 0) {
    els.outgoingFriendsList.innerHTML = "<p class='friends-status'>No pending requests.</p>";
  } else {
    for (const friend of friends.outgoingRequests) {
      els.outgoingFriendsList.appendChild(
        createFriendItem(friend, { showRemove: true, removeLabel: "Cancel" }),
      );
    }
  }
}

async function loadFriends() {
  state.friends = await api.getFriends();
  renderFriendsPanel();
}

async function openFriendsModal() {
  els.financeModal.hidden = true;
  els.supplyModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  els.profileModal.hidden = true;
  els.dayModal.hidden = true;
  els.messagesModal.hidden = true;
  els.friendsModal.hidden = false;
  openModal(els.friendsModal);
  setFriendsStatus("Loading friends...");
  try {
    await loadFriends();
    setFriendsStatus("");
  } catch (error) {
    setFriendsStatus(error.message, true);
  }
}

async function submitAddFriend(profileNumber) {
  const value = profileNumber.trim();
  if (!value) {
    setFriendsStatus("Enter a profile number like !K7R-8842-9F3A.", true);
    return;
  }

  setFriendsStatus("Sending request...");
  try {
    const result = await api.addFriend(value);
    els.addFriendNumber.value = "";
    setFriendsStatus(result.message, false);
    await loadFriends();
    return result;
  } catch (error) {
    setFriendsStatus(error.message, true);
    throw error;
  }
}

async function submitProfileAddFriend() {
  const value = els.profileAddFriendNumber.value.trim();
  if (!value) {
    els.profileAddFriendStatus.textContent = "Enter a profile number like !K7R-8842-9F3A.";
    els.profileAddFriendStatus.classList.add("error");
    return;
  }

  els.profileAddFriendStatus.textContent = "Sending request...";
  els.profileAddFriendStatus.classList.remove("error", "success");
  try {
    const result = await api.addFriend(value);
    els.profileAddFriendNumber.value = "";
    els.profileAddFriendStatus.textContent = result.message;
    els.profileAddFriendStatus.classList.add("success");
    if (!els.friendsModal.hidden) {
      await loadFriends();
    }
  } catch (error) {
    els.profileAddFriendStatus.textContent = error.message;
    els.profileAddFriendStatus.classList.add("error");
  }
}

async function acceptFriendRequest(friendshipId) {
  const result = await api.acceptFriend(friendshipId);
  setFriendsStatus(result.message, false);
  await loadFriends();
  if (state.profile && !state.profile.isOwner) {
    const profile = await api.getProfileByUsername(state.profile.username);
    renderProfile(profile);
  }
}

async function removeFriendRequest(friendshipId) {
  const result = await api.removeFriend(friendshipId);
  setFriendsStatus(result.message, false);
  await loadFriends();
  if (state.profile && !state.profile.isOwner && state.profile.friendshipId === friendshipId) {
    const profile = await api.getProfileByUsername(state.profile.username);
    renderProfile(profile);
  }
}

async function profileAddFriend() {
  const profile = state.profile;
  if (!profile?.profileNumber) {
    return;
  }

  els.profileFriendActionStatus.textContent = "Sending request...";
  els.profileFriendActionStatus.classList.remove("error", "success");
  try {
    const result = await api.addFriend(profile.profileNumber);
    els.profileFriendActionStatus.textContent = result.message;
    els.profileFriendActionStatus.classList.add("success");
    const refreshed = await api.getProfileByUsername(profile.username);
    renderProfile(refreshed);
    if (!els.friendsModal.hidden) {
      await loadFriends();
    }
  } catch (error) {
    els.profileFriendActionStatus.textContent = error.message;
    els.profileFriendActionStatus.classList.add("error");
  }
}

async function profileAcceptFriend() {
  const friendshipId = state.profile?.friendshipId;
  if (!friendshipId) {
    return;
  }

  els.profileFriendActionStatus.textContent = "Accepting...";
  els.profileFriendActionStatus.classList.remove("error", "success");
  try {
    const result = await api.acceptFriend(friendshipId);
    els.profileFriendActionStatus.textContent = result.message;
    els.profileFriendActionStatus.classList.add("success");
    const refreshed = await api.getProfileByUsername(state.profile.username);
    renderProfile(refreshed);
    if (!els.friendsModal.hidden) {
      await loadFriends();
    }
  } catch (error) {
    els.profileFriendActionStatus.textContent = error.message;
    els.profileFriendActionStatus.classList.add("error");
  }
}

async function profileRemoveFriend() {
  const friendshipId = state.profile?.friendshipId;
  if (!friendshipId) {
    return;
  }

  els.profileFriendActionStatus.textContent = "Updating...";
  els.profileFriendActionStatus.classList.remove("error", "success");
  try {
    const result = await api.removeFriend(friendshipId);
    els.profileFriendActionStatus.textContent = result.message;
    els.profileFriendActionStatus.classList.add("success");
    const refreshed = await api.getProfileByUsername(state.profile.username);
    renderProfile(refreshed);
    if (!els.friendsModal.hidden) {
      await loadFriends();
    }
  } catch (error) {
    els.profileFriendActionStatus.textContent = error.message;
    els.profileFriendActionStatus.classList.add("error");
  }
}

async function openProfile(username) {
  els.profileSaveStatus.textContent = "";
  els.financeModal.hidden = true;
  els.supplyModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.dayModal.hidden = true;
  els.profileModal.hidden = false;
  openModal(els.profileModal);

  try {
    const profile = username
      ? await api.getProfileByUsername(username)
      : await api.getProfile();
    renderProfile(profile);
  } catch (error) {
    els.profileModal.hidden = true;
    showStatus(error.message, true);
  }
}

async function saveProfile() {
  els.profileSaveStatus.textContent = "Saving...";
  els.profileSaveStatus.classList.remove("error", "success");

  try {
    const profile = await api.updateProfile({
      mood: els.profileMoodInput.value.trim(),
      aboutMe: els.profileAboutInput.value,
      music: els.profileMusicInput.value.trim(),
      interests: els.profileInterestsInput.value,
      discord: els.profileDiscordInput.value.trim(),
      bluesky: els.profileBlueskyInput.value.trim(),
      twitter: els.profileTwitterInput.value.trim(),
      youtube: els.profileYoutubeInput.value.trim(),
      facebook: els.profileFacebookInput.value.trim(),
    });
    renderProfile(profile);
    els.profileSaveStatus.textContent = "Profile saved!";
    els.profileSaveStatus.classList.add("success");
  } catch (error) {
    els.profileSaveStatus.textContent = error.message;
    els.profileSaveStatus.classList.add("error");
  }
}

function oreMeta(type) {
  return tradeOreTypes[type] ?? { displayName: type, color: "#888", basePrice: 0 };
}

function supplyMeta(type) {
  return tradeSupplyTypes[type] ?? { displayName: type, color: "#888", basePrice: 0 };
}

function inventoryQty(mine, itemType) {
  const item = mine?.inventory?.find((i) => i.itemType === itemType);
  return item ? Number(item.quantity) : 0;
}

function formatRunway(days) {
  return Number(days) >= 999 ? "∞" : Number(days).toFixed(1);
}

async function refreshAll() {
  const mine = await api.getMine();
  const [finances, market] = await Promise.all([api.getFinances(), api.getMarket()]);
  state.mine = mine;
  state.finances = finances;
  state.market = market;
  state.nextDayAtUtc = mine?.nextDayAtUtc ?? null;
  renderHud();
  renderMineGrid();
  renderZonePanel();
  if (mine?.latestDayReport) {
    showDayReport(mine.latestDayReport);
  }
  if (mine?.birthdayMessage) {
    showAuthToast(mine.birthdayMessage, "success");
  }
  showEventCompletions(mine?.eventCompletions);
  if (!els.financeModal.hidden) {
    renderFinancePanel();
  }
  if (!els.supplyModal.hidden) {
    renderSupplyPanel();
  }
  if (!els.shippingModal.hidden) {
    renderShippingPanel();
  }
  if (!els.storeModal.hidden) {
    renderStorePanel();
  }
  await playerMessaging.refreshUnreadBadge();
}

function isAuthError(error) {
  const message = (error?.message ?? "").toLowerCase();
  return message.includes("session expired")
    || message.includes("invalid username or password")
    || message.includes("unauthorized")
    || message.includes("banned");
}

async function tryAutoLogin() {
  api.reloadFromStorage();

  if (!api.token) {
    showScreen("login");
    return;
  }

  try {
    const session = await api.getSession();
    api.applySession(session);
    if (!api.mineId) {
      throw new Error("Session incomplete. Sign in again.");
    }

    showScreen("game");
    await refreshAll();
    showEventAnnouncements(session.eventAnnouncements);
  } catch (error) {
    if (error.code === "banned" || (error.status === 403 && error.message?.toLowerCase().includes("banned"))) {
      api.clearAuth();
      state.banMessage = error.message;
      setAuthMode("ban-appeal");
      showScreen("login");
      showLoginStatus(error.message, "error");
      return;
    }

    if (isAuthError(error)) {
      api.clearAuth();
    }
    showScreen("login");
    showLoginStatus(error.message, "error");
  }
}

function formatUtcCountdown(nextDayAtUtc) {
  if (!nextDayAtUtc) {
    return "---";
  }

  const remainingMs = new Date(nextDayAtUtc).getTime() - Date.now();
  if (remainingMs <= 0) {
    return "00:00:00";
  }

  const totalSeconds = Math.floor(remainingMs / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

function updateUtcClockDisplay() {
  const mine = state.mine;
  if (!mine) {
    els.utcClock.textContent = "UTC ---";
    return;
  }

  const utcDate = mine.utcDate ?? new Date().toISOString().slice(0, 10);
  const countdown = formatUtcCountdown(state.nextDayAtUtc ?? mine.nextDayAtUtc);
  els.utcClock.textContent = `UTC ${utcDate} · next day in ${countdown}`;
}

function startUtcTimers() {
  stopUtcTimers();
  updateUtcClockDisplay();
  utcClockTimer = setInterval(updateUtcClockDisplay, 1000);
  utcRefreshTimer = setInterval(() => {
    if (!els.gameScreen.hidden) {
      refreshAll().catch((error) => showStatus(error.message, true));
    }
  }, 60000);
}

function stopUtcTimers() {
  if (utcClockTimer) {
    clearInterval(utcClockTimer);
    utcClockTimer = undefined;
  }
  if (utcRefreshTimer) {
    clearInterval(utcRefreshTimer);
    utcRefreshTimer = undefined;
  }
}

function renderHud() {
  const mine = state.mine;
  if (!mine) {
    return;
  }

  els.playerName.textContent = api.username ?? "Commander";
  els.credits.textContent = `Credits: ${Number(mine.credits).toFixed(0)}`;
  els.day.textContent = `Day ${mine.currentGameDay}`;
  updateUtcClockDisplay();
}

function renderMineGrid() {
  const mine = state.mine;
  els.mineGrid.innerHTML = "";
  if (!mine?.zones) {
    return;
  }

  const sorted = [...mine.zones].sort((a, b) => a.y - b.y || a.x - b.x);
  for (const zone of sorted) {
    const meta = oreMeta(zone.oreType);
    const cell = document.createElement("button");
    cell.type = "button";
    cell.className = "zone-cell";
    cell.title = `(${zone.x}, ${zone.y}) ${meta.displayName}`;
    cell.style.backgroundColor = zoneColor(zone, meta.color);
    cell.dataset.zoneId = zone.id;
    if (zone.id === state.selectedZoneId) {
      cell.classList.add("selected");
    }
    cell.addEventListener("click", () => {
      state.selectedZoneId = zone.id;
      renderMineGrid();
      renderZonePanel();
    });
    els.mineGrid.appendChild(cell);
  }
}

function zoneColor(zone, baseColor) {
  if (zone.isSalvageZone) {
    return "#99a6b3";
  }
  if (Number(zone.depletedPct) >= 100) {
    return shadeColor(baseColor, 0.35);
  }
  return baseColor;
}

function shadeColor(hex, factor) {
  const rgb = hex.match(/^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i);
  if (!rgb) {
    return hex;
  }
  const r = Math.round(parseInt(rgb[1], 16) * factor);
  const g = Math.round(parseInt(rgb[2], 16) * factor);
  const b = Math.round(parseInt(rgb[3], 16) * factor);
  return `rgb(${r}, ${g}, ${b})`;
}

function renderZonePanel() {
  const mine = state.mine;
  const zone = mine?.zones?.find((z) => z.id === state.selectedZoneId);
  els.workerList.innerHTML = "";

  if (!zone) {
    els.zoneInfo.textContent = "Select a zone on the grid.";
    return;
  }

  const meta = oreMeta(zone.oreType);
  els.zoneInfo.innerHTML = [
    `<strong>Zone (${zone.x}, ${zone.y})</strong>`,
    `Ore: ${meta.displayName}`,
    `Richness: ${Number(zone.richness).toFixed(2)}`,
    `Depleted: ${Number(zone.depletedPct).toFixed(0)}%`,
    zone.isSalvageZone ? "Emergency salvage zone — always mineable." : "",
  ]
    .filter(Boolean)
    .join("<br>");

  for (const worker of mine.workers ?? []) {
    const assignedHere = worker.assignedZoneId === zone.id;
    const busyElsewhere = worker.assignedZoneId && worker.assignedZoneId !== zone.id;
    const button = document.createElement("button");
    button.type = "button";
    button.className = "worker-btn";
    if (assignedHere) {
      button.classList.add("assigned");
    }
    if (busyElsewhere) {
      button.classList.add("busy");
    }
    button.textContent = assignedHere
      ? `${worker.name} (assigned)`
      : busyElsewhere
        ? `${worker.name} (busy)`
        : worker.name;
    button.addEventListener("click", () => toggleWorker(worker, zone.id));
    els.workerList.appendChild(button);
  }
}

async function toggleWorker(worker, zoneId) {
  showStatus("Updating worker assignment...");
  try {
    let response;
    if (worker.assignedZoneId === zoneId) {
      response = await api.unassignWorker(worker.id);
    } else {
      response = await api.assignWorker(worker.id, zoneId);
    }
    await refreshAll();
    showEventCompletions(response?.eventCompletions);
    showStatus("");
  } catch (error) {
    showStatus(error.message, true);
  }
}

function renderFinancePanel() {
  const finances = state.finances;
  if (!finances) {
    return;
  }

  els.financeSummary.innerHTML = [
    `Credits: ${Number(finances.credits).toFixed(0)}`,
    `Daily Payroll: ${Number(finances.dailyPayroll).toFixed(0)}`,
    `Daily Supply Cost: ${Number(finances.dailySupplyCost).toFixed(0)}`,
    `Est. Daily Income: ${Number(finances.estimatedDailyIncome).toFixed(0)}`,
    `Runway: ${formatRunway(finances.runwayDays)} days`,
    finances.isSoftlocked ? "<strong class='danger'>SOFTLOCKED — Use emergency buyback!</strong>" : "",
  ]
    .filter(Boolean)
    .join("<br>");

  els.emergencyBtn.hidden = !finances.canEmergencyBuyback;

  els.financeTransactions.innerHTML = "";
  for (const tx of (finances.recentTransactions ?? []).slice(0, 8)) {
    const line = document.createElement("div");
    const sign = Number(tx.amount) >= 0 ? "+" : "";
    line.textContent = `Day ${tx.gameDay}: ${sign}${Number(tx.amount).toFixed(0)} — ${tx.description}`;
    els.financeTransactions.appendChild(line);
  }
}

function formatMarketSource(source) {
  switch (source) {
    case "yahoo-us":
      return "US stocks (CAT, XOM, JNJ, QCOM)";
    case "mock-fallback":
      return "fallback mock prices";
    default:
      return source ?? "market";
  }
}

function formatActiveMarketBonuses(market) {
  const bonuses = market?.eventBonuses;
  if (!bonuses) {
    return "";
  }

  const parts = [];
  if (Number(bonuses.saleBonusPercent) > 0) {
    parts.push(`+${Number(bonuses.saleBonusPercent)}% sale credits`);
  }
  if (Number(bonuses.tradeBonusPercent) > 0) {
    parts.push(`+${Number(bonuses.tradeBonusPercent)}% trade rebate`);
  }

  return parts.length ? ` · Event: ${parts.join(", ")}` : "";
}

function effectiveOreSalePrice(basePrice, market) {
  const bonus = Number(market?.eventBonuses?.saleBonusPercent ?? 0);
  if (bonus <= 0) {
    return basePrice;
  }
  return basePrice * (1 + bonus / 100);
}

function renderStorePanel() {
  const market = state.market;
  els.storeMarketInfo.textContent = market
    ? `Game Day ${market.gameDay} · ${formatMarketSource(market.source)} · refreshes UTC midnight${formatActiveMarketBonuses(market)}`
    : "Market prices loading...";

  els.storeSupplyList.innerHTML = "";
  for (const [type, meta] of Object.entries(tradeSupplyTypes)) {
    const priceEntry = market?.prices?.find((p) => p.supplyType === type);
    const price = priceEntry ? Number(priceEntry.price) : meta.basePrice;
    const stock = inventoryQty(state.mine, type);
    const button = document.createElement("button");
    button.type = "button";
    button.className = "shop-btn";
    button.style.borderColor = meta.color;
    button.innerHTML = `<strong>Buy ${meta.displayName}</strong><span>${price.toFixed(0)} cr · Stock: ${stock.toFixed(0)}</span>`;
    button.addEventListener("click", () => buySupply(type));
    els.storeSupplyList.appendChild(button);
  }
}

function renderShippingPanel() {
  const mine = state.mine;
  const oreItems = (mine?.inventory ?? []).filter((i) => i.category === "Ore");
  const totalCargo = oreItems.reduce((sum, item) => sum + Number(item.quantity ?? 0), 0);

  els.shippingSummary.textContent = totalCargo > 0
    ? `Cargo ready: ${totalCargo.toFixed(1)} units · Ships up to 10 units per run${formatActiveMarketBonuses(state.market)}`
    : `No ore in cargo hold. Mine zones to fill the hold.${formatActiveMarketBonuses(state.market)}`;

  els.shippingCargoList.innerHTML = "";
  let hasCargo = false;

  for (const [type, meta] of Object.entries(tradeOreTypes)) {
    const stock = inventoryQty(mine, type);
    if (stock <= 0) {
      continue;
    }

    hasCargo = true;
    const salePrice = effectiveOreSalePrice(meta.basePrice, state.market);
    const button = document.createElement("button");
    button.type = "button";
    button.className = "shop-btn";
    button.style.borderColor = meta.color;
    button.innerHTML = `<strong>Ship ${meta.displayName}</strong><span>${salePrice.toFixed(0)} cr/u · Qty: ${stock.toFixed(1)}</span>`;
    button.addEventListener("click", () => shipCargo(type, stock));
    els.shippingCargoList.appendChild(button);
  }

  if (!hasCargo) {
    els.shippingCargoList.innerHTML = "<p class='market-info'>No ore ready to ship.</p>";
  }
}

function renderSupplyPanel() {
  const market = state.market;
  const mine = state.mine;
  els.marketInfo.textContent = market
    ? `Game Day ${market.gameDay} · ${formatMarketSource(market.source)} · refreshes UTC midnight${formatActiveMarketBonuses(market)}`
    : "Market prices loading...";

  els.supplyList.innerHTML = "";
  for (const [type, meta] of Object.entries(tradeSupplyTypes)) {
    const priceEntry = market?.prices?.find((p) => p.supplyType === type);
    const price = priceEntry ? Number(priceEntry.price) : meta.basePrice;
    const stock = inventoryQty(mine, type);
    const button = document.createElement("button");
    button.type = "button";
    button.className = "shop-btn";
    button.style.borderColor = meta.color;
    button.innerHTML = `<strong>${meta.displayName}</strong><span>${price.toFixed(0)} cr · Stock: ${stock.toFixed(0)}</span>`;
    button.addEventListener("click", () => buySupply(type));
    els.supplyList.appendChild(button);
  }

  els.oreList.innerHTML = "";
  for (const [type, meta] of Object.entries(tradeOreTypes)) {
    const stock = inventoryQty(mine, type);
    if (stock <= 0) {
      continue;
    }
    const salePrice = effectiveOreSalePrice(meta.basePrice, market);
    const button = document.createElement("button");
    button.type = "button";
    button.className = "shop-btn";
    button.style.borderColor = meta.color;
    button.innerHTML = `<strong>Sell ${meta.displayName}</strong><span>${salePrice.toFixed(0)} cr/u · Qty: ${stock.toFixed(1)}</span>`;
    button.addEventListener("click", () => sellOre(type, stock));
    els.oreList.appendChild(button);
  }
}

function showDayReport(result) {
  els.dayReportTitle.textContent = `Day ${result.newGameDay} Report`;
  const lines = [...(result.messages ?? [])];
  if (result.oreExtracted?.length) {
    const extracted = result.oreExtracted
      .map((o) => `${Number(o.quantity).toFixed(1)} ${o.oreType}`)
      .join(", ");
    lines.push("", `Extracted: ${extracted}`);
  }
  els.dayReportBody.textContent = lines.join("\n") || "Day complete.";
  openModal(els.dayModal);
}

function formatEventRewardLabel(reward) {
  const amount = Number(reward.amount ?? 0);
  const itemType = reward.itemType ?? "";
  if ((reward.category ?? itemType).toLowerCase() === "credits") {
    return `${amount.toLocaleString()} credits`;
  }
  const meta = reward.category === "Supply"
    ? supplyMeta(itemType)
    : oreMeta(itemType);
  const name = meta.displayName ?? itemType;
  const formattedAmount = Number.isInteger(amount) ? amount.toLocaleString() : amount.toFixed(1);
  return `${formattedAmount} ${name}`;
}

function formatAnnouncementReward(reward) {
  const amount = Number(reward.amount ?? 0);
  const itemType = reward.itemType ?? "";
  if (itemType.toLowerCase() === "credits") {
    return `${amount.toLocaleString()} credits`;
  }
  const meta = tradeOreTypes[itemType] ?? tradeSupplyTypes[itemType];
  const name = meta?.displayName ?? itemType;
  const formattedAmount = Number.isInteger(amount) ? amount.toLocaleString() : amount.toFixed(1);
  return `${formattedAmount} ${name}`;
}

function queueEventModalItem(item) {
  eventModalQueue.push(item);
  showNextEventModal();
}

function showNextEventModal() {
  if (eventModalQueue.length === 0) {
    return;
  }

  const item = eventModalQueue[0];
  const isWin = item.type === "completion";
  els.eventModalTitle.textContent = item.title || "Special Event";
  els.eventModalMessage.textContent = item.message || "";
  els.eventModalChallenge.hidden = isWin;
  if (!isWin) {
    const parts = [];
    if (item.challengeDescription) {
      parts.push(`How to win: ${item.challengeDescription}`);
    }
    if (item.marketBonusDescription) {
      parts.push(`While active: ${item.marketBonusDescription}`);
    }
    els.eventModalChallenge.textContent = parts.join(" · ");
    els.eventModalChallenge.hidden = parts.length === 0;
  }
  els.eventModalRewardsHeading.textContent = isWin ? "You won" : "Possible rewards";
  const rewards = item.rewards ?? [];
  els.eventModalRewards.innerHTML = rewards.length
    ? rewards.map((reward) => `<li>${isWin ? formatEventRewardLabel(reward) : formatAnnouncementReward(reward)}</li>`).join("")
    : `<li>${isWin ? "Rewards added to your account." : "Complete the challenge to earn rewards."}</li>`;
  els.eventModalClose.textContent = isWin ? "Claim prize" : "Got it";
  openModal(els.eventModal);
}

function showEventAnnouncements(announcements) {
  if (!announcements?.length) {
    return;
  }

  for (const announcement of announcements) {
    queueEventModalItem({
      type: "announcement",
      title: announcement.title,
      message: announcement.message,
      challengeDescription: announcement.challengeDescription,
      marketBonusDescription: announcement.marketBonusDescription,
      rewards: announcement.rewards ?? [],
    });
  }
}

function showEventCompletions(completions) {
  if (!completions?.length) {
    return;
  }

  for (const completion of completions) {
    queueEventModalItem({
      type: "completion",
      title: completion.title,
      message: completion.message,
      rewards: completion.rewards ?? [],
    });
  }
}

function dismissEventModal() {
  els.eventModal.hidden = true;
  eventModalQueue.shift();
  showNextEventModal();
}

function notifyRegisterResult(message, variant) {
  showLoginStatus(message, variant);
  showAuthToast(message, variant);
}

async function authenticate(register) {
  const isRegister = register || state.authMode === "register";
  const username = els.username.value.trim();
  const password = els.password.value;
  const email = els.email.value.trim();
  const birthday = getBirthdayValue();

  if (!username || !password) {
    const message = "Username and password are required.";
    if (isRegister) {
      notifyRegisterResult(message, "error");
    } else {
      showLoginStatus(message, "error");
    }
    return;
  }

  if (isRegister && !email) {
    notifyRegisterResult("Email is required to sign up.", "error");
    return;
  }

  if (isRegister && !isValidEmail(email)) {
    notifyRegisterResult("Enter a valid email address.", "error");
    return;
  }

  if (isRegister && !birthday) {
    notifyRegisterResult("Birthday is required to sign up.", "error");
    return;
  }

  if (isRegister && password.length < 8) {
    notifyRegisterResult("Password must be at least 8 characters.", "error");
    return;
  }

  showLoginStatus(isRegister ? "Creating account..." : "Connecting...", "info");
  try {
    if (isRegister) {
      await api.register(username, email, password, birthday);
      els.password.value = "";
      els.email.value = "";
      resetBirthdayDropdowns();
      const message = "Account created successfully. Log in with your username and password.";
      setAuthMode("login");
      notifyRegisterResult(message, "success");
      return;
    }

    const response = await api.login(username, password);
    api.saveAuth(response);
    hideAuthToast();
    showLoginStatus("");
    showScreen("game");
    await refreshAll();
    showEventAnnouncements(response.eventAnnouncements);
  } catch (error) {
    if (!isRegister && (error.code === "banned" || (error.status === 403 && error.message?.toLowerCase().includes("banned")))) {
      state.banMessage = error.message;
      setAuthMode("ban-appeal");
      showLoginStatus(error.message, "error");
      return;
    }

    if (isRegister) {
      notifyRegisterResult(error.message, "error");
    } else {
      showLoginStatus(error.message, "error");
    }
  }
}

async function submitBanAppeal() {
  const username = els.username.value.trim();
  const password = els.password.value;
  const message = els.banAppealMessage.value.trim();

  if (!username || !password) {
    showLoginStatus("Username and password are required.", "error");
    return;
  }

  if (!message) {
    showLoginStatus("Enter a message explaining why your ban should be removed.", "error");
    return;
  }

  showLoginStatus("Sending appeal...", "info");
  try {
    const result = await api.submitBanAppeal(username, password, message);
    els.banAppealMessage.value = "";
    els.password.value = "";
    setAuthMode("login");
    showLoginStatus(result.message, "success");
  } catch (error) {
    showLoginStatus(error.message, "error");
  }
}

async function sendPasswordReset() {
  const email = els.forgotEmail.value.trim();
  if (!email) {
    showLoginStatus("Email is required.", "error");
    return;
  }

  if (!isValidEmail(email)) {
    showLoginStatus("Enter a valid email address.", "error");
    return;
  }

  showLoginStatus("Sending reset link...");
  try {
    const result = await api.forgotPassword(email);
    showLoginStatus(result.message);
  } catch (error) {
    showLoginStatus(error.message, "error");
  }
}

async function submitPasswordReset() {
  const token = els.resetToken.value.trim();
  const newPassword = els.newPassword.value;
  const confirmPassword = els.confirmPassword.value;

  if (!token || !newPassword) {
    showLoginStatus("Reset token and new password are required.", "error");
    return;
  }

  if (newPassword.length < 8) {
    showLoginStatus("Password must be at least 8 characters.", "error");
    return;
  }

  if (newPassword !== confirmPassword) {
    showLoginStatus("Passwords do not match.", "error");
    return;
  }

  showLoginStatus("Updating password...");
  try {
    const result = await api.resetPassword(token, newPassword);
    showLoginStatus(result.message);
    setAuthMode("login");
    history.replaceState({}, "", "/");
  } catch (error) {
    showLoginStatus(error.message, "error");
  }
}

async function buySupply(supplyType) {
  showStatus("Buying supplies...");
  try {
    const response = await api.buySupply(supplyType, 5);
    await refreshAll();
    showEventCompletions(response?.eventCompletions);
    showStatus("");
  } catch (error) {
    showStatus(error.message, true);
  }
}

async function shipCargo(oreType, stock) {
  showStatus("Shipping cargo...");
  try {
    const response = await api.sellOre(oreType, Math.min(stock, 10));
    await refreshAll();
    showEventCompletions(response?.eventCompletions);
    showStatus("");
  } catch (error) {
    showStatus(error.message, true);
  }
}

async function sellOre(oreType, stock) {
  showStatus("Selling ore...");
  try {
    const response = await api.sellOre(oreType, Math.min(stock, 10));
    await refreshAll();
    showEventCompletions(response?.eventCompletions);
    showStatus("");
  } catch (error) {
    showStatus(error.message, true);
  }
}

async function emergencySell() {
  const mine = state.mine;
  const emergencyType = Object.entries(tradeOreTypes).find(([, meta]) => meta.isEmergencySource)?.[0];
  const salvage = emergencyType
    ? mine?.inventory?.find(
        (i) => i.category === "Ore" && i.itemType === emergencyType && Number(i.quantity) > 0,
      )
    : null;
  const ore =
    salvage ??
    mine?.inventory?.find((i) => i.category === "Ore" && Number(i.quantity) > 0);
  if (!ore) {
    showStatus("No ore available for emergency buyback.", true);
    return;
  }

  showStatus("Processing emergency buyback...");
  try {
    const response = await api.sellOre(ore.itemType, Math.min(Number(ore.quantity), 5), true);
    await refreshAll();
    renderFinancePanel();
    showEventCompletions(response?.eventCompletions);
    showStatus("");
  } catch (error) {
    showStatus(error.message, true);
  }
}


function logout() {
  api.clearAuth();
  state.mine = null;
  state.finances = null;
  state.market = null;
  state.profile = null;
  state.friends = null;
  state.selectedZoneId = null;
  state.nextDayAtUtc = null;
  stopUtcTimers();
  closeModals();
  showScreen("login");
  hideAuthToast();
  showLoginStatus("");
}

function submitAuthForm() {
  switch (state.authMode) {
    case "register":
      authenticate(true);
      break;
    case "forgot":
      sendPasswordReset();
      break;
    case "reset":
      submitPasswordReset();
      break;
    case "ban-appeal":
      submitBanAppeal();
      break;
    default:
      authenticate(false);
  }
}

function handleAuthEnter(event) {
  if (event.key !== "Enter") {
    return;
  }

  if (els.loginScreen.hidden) {
    return;
  }

  event.preventDefault();
  submitAuthForm();
}

for (const input of [
  els.username,
  els.password,
  els.email,
  els.forgotEmail,
  els.resetToken,
  els.newPassword,
  els.confirmPassword,
  els.banAppealMessage,
]) {
  input.addEventListener("keydown", handleAuthEnter);
}

els.toggleMode.addEventListener("click", () => {
  setAuthMode(state.authMode === "register" ? "login" : "register");
});
els.loginBtn.addEventListener("click", () => authenticate(false));
els.registerBtn.addEventListener("click", () => authenticate(true));
els.forgotBtn.addEventListener("click", () => setAuthMode("forgot"));
els.sendResetBtn.addEventListener("click", sendPasswordReset);
els.resetPasswordBtn.addEventListener("click", submitPasswordReset);
els.backLoginBtn.addEventListener("click", () => {
  history.replaceState({}, "", "/");
  state.banMessage = "";
  setAuthMode("login");
});
els.banAppealBtn.addEventListener("click", submitBanAppeal);
els.logoutBtn.addEventListener("click", logout);
els.playerName.addEventListener("click", () => openProfile());
els.profileBtn.addEventListener("click", () => openProfile());
els.profileCloseBtn.addEventListener("click", () => {
  els.profileModal.hidden = true;
});
els.profileSaveBtn.addEventListener("click", () => {
  saveProfile().catch((error) => {
    els.profileSaveStatus.textContent = error.message;
    els.profileSaveStatus.classList.add("error");
  });
});
els.profilePhotoChooseBtn.addEventListener("click", () => {
  els.profilePhotoInput.click();
});
els.profilePhotoInput.addEventListener("change", () => {
  const file = els.profilePhotoInput.files?.[0];
  if (!file) {
    els.profilePhotoBtn.disabled = true;
    els.profilePhotoChooseBtn.textContent = "Choose Photo";
    return;
  }

  els.profilePhotoBtn.disabled = false;
  els.profilePhotoChooseBtn.textContent = file.name.length > 14
    ? `${file.name.slice(0, 11)}…`
    : file.name;
});
els.profilePhotoBtn.addEventListener("click", () => {
  uploadProfilePhoto().catch((error) => {
    els.profilePhotoStatus.textContent = error.message;
    els.profilePhotoStatus.classList.add("error");
  });
});
els.profileAddFriendBtn.addEventListener("click", () => {
  profileAddFriend().catch((error) => {
    els.profileFriendActionStatus.textContent = error.message;
    els.profileFriendActionStatus.classList.add("error");
  });
});
els.profileAcceptFriendBtn.addEventListener("click", () => {
  profileAcceptFriend().catch((error) => {
    els.profileFriendActionStatus.textContent = error.message;
    els.profileFriendActionStatus.classList.add("error");
  });
});
els.profileRemoveFriendBtn.addEventListener("click", () => {
  profileRemoveFriend().catch((error) => {
    els.profileFriendActionStatus.textContent = error.message;
    els.profileFriendActionStatus.classList.add("error");
  });
});
els.profileMessageFriendBtn.addEventListener("click", () => {
  const playerId = state.profile?.playerId;
  if (!playerId) {
    return;
  }

  openMessagesModal(playerId).catch((error) => setMessagesStatus(error.message, true));
});
els.friendsBtn.addEventListener("click", () => {
  openFriendsModal().catch((error) => setFriendsStatus(error.message, true));
});
els.messagesBtn.addEventListener("click", () => {
  openMessagesModal().catch((error) => setMessagesStatus(error.message, true));
});
els.addFriendBtn.addEventListener("click", () => {
  submitAddFriend(els.addFriendNumber.value).catch((error) => setFriendsStatus(error.message, true));
});
els.profileAddFriendSubmitBtn.addEventListener("click", () => {
  submitProfileAddFriend().catch((error) => {
    els.profileAddFriendStatus.textContent = error.message;
    els.profileAddFriendStatus.classList.add("error");
  });
});
els.profileAddFriendNumber.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    event.preventDefault();
    submitProfileAddFriend().catch((error) => {
      els.profileAddFriendStatus.textContent = error.message;
      els.profileAddFriendStatus.classList.add("error");
    });
  }
});
els.addFriendNumber.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    event.preventDefault();
    submitAddFriend(els.addFriendNumber.value).catch((error) => setFriendsStatus(error.message, true));
  }
});
els.financeBtn.addEventListener("click", () => {
  els.supplyModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  els.profileModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  openModal(els.financeModal);
  renderFinancePanel();
});
els.tradeMarketBtn.addEventListener("click", () => {
  els.financeModal.hidden = true;
  els.profileModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  openModal(els.supplyModal);
  renderSupplyPanel();
});
els.storeBtn.addEventListener("click", () => {
  els.financeModal.hidden = true;
  els.profileModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.supplyModal.hidden = true;
  els.shippingModal.hidden = true;
  openModal(els.storeModal);
  renderStorePanel();
});
els.shippingBtn.addEventListener("click", () => {
  els.financeModal.hidden = true;
  els.profileModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.supplyModal.hidden = true;
  els.storeModal.hidden = true;
  openModal(els.shippingModal);
  renderShippingPanel();
});
els.emergencyBtn.addEventListener("click", emergencySell);
els.closeDayReport.addEventListener("click", () => {
  els.dayModal.hidden = true;
});
els.eventModalClose.addEventListener("click", dismissEventModal);

document.querySelectorAll("[data-close-modal]").forEach((button) => {
  button.addEventListener("click", () => {
    button.closest(".modal")?.setAttribute("hidden", "");
  });
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    closeModals();
  }
});

setAuthMode("login");
initBirthdayDropdowns();
initApiStatusMonitor(api);
showScreen("login");
els.mineGrid.style.setProperty("--grid-size", GRID_SIZE);

const resetTokenFromUrl = new URLSearchParams(window.location.search).get("reset");
loadTradeItems().finally(() => {
  if (resetTokenFromUrl) {
    els.resetToken.value = resetTokenFromUrl;
    setAuthMode("reset");
  } else {
    tryAutoLogin();
  }
});
