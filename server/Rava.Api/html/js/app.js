import { RavaApi } from "./api.js";
import { GRID_SIZE, ORE_TYPES, SUPPLY_TYPES, API_BASE_URL, readMetaApiBase } from "./config.js";
import {
  formatRaxHtml,
  formatRaxPlain,
  formatRewardAmount,
  setRaxHtml,
  RAX_NAME,
} from "./currency.js";
import { initPlayerMessaging } from "./player-messages.js";
import { renderSocialLinksHtml, hasSocialLinks } from "./profile-social.js";
import { initExonet } from "./exonet.js?v=20260602-onn-profile-portraits";
import { initI18n, applyTranslations, wireLocaleSelectors, wireLocaleSelector, getLocale, setLocale, t } from "./i18n.js";

const api = new RavaApi(API_BASE_URL);

const PROFILE_AVATAR_PRESETS = [
  { id: "female", labelKey: "avatar.preset.female" },
  { id: "male", labelKey: "avatar.preset.male" },
  { id: "neutral", labelKey: "avatar.preset.neutral" },
];

const PROFILE_GENDER_LABEL_KEYS = {
  male: "profile.edit.gender.male",
  female: "profile.edit.gender.female",
  "trans-female": "profile.edit.gender.transFemale",
  "trans-male": "profile.edit.gender.transMale",
  "non-binary": "profile.edit.gender.nonBinary",
  "prefer-not-to-say": "profile.edit.gender.preferNot",
};

function genderLabel(gender) {
  const key = PROFILE_GENDER_LABEL_KEYS[gender];
  return key ? t(key) : gender;
}

function profileGenderRequiresPronouns(gender) {
  return gender === "non-binary" || gender === "prefer-not-to-say";
}

function syncRegisterGenderUi() {
  const gender = els.registerGenderInput?.value ?? "";
  const needsPronouns = profileGenderRequiresPronouns(gender);
  setHidden(els.registerPronounsGroup, !needsPronouns);
  if (els.registerPronounsInput && !needsPronouns) {
    els.registerPronounsInput.value = "";
  }
}

function resetRegisterProfileFields() {
  if (els.registerGenderInput) {
    els.registerGenderInput.value = "";
  }
  if (els.registerPronounsInput) {
    els.registerPronounsInput.value = "";
  }
  syncRegisterGenderUi();
}

async function applyProfileLocaleFromServer(profile) {
  if (!profile?.isOwner || !profile.profileLocale) {
    return;
  }

  if (getLocale() !== profile.profileLocale) {
    await setLocale(profile.profileLocale);
    applyTranslations(document);
  }
}

function capitalizePronoun(word) {
  if (!word) {
    return "";
  }
  return word.charAt(0).toUpperCase() + word.slice(1);
}

function formatRaxLabelLine(label, value) {
  return `${label}: ${formatRaxHtml(value)}`;
}

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
    peerCompose: document.getElementById("player-peer-compose"),
    staffCompose: document.getElementById("player-staff-compose"),
    staffToggleBtn: document.getElementById("player-staff-toggle-btn"),
    peerToggleBtn: document.getElementById("player-peer-toggle-btn"),
  },
  setStatus: (_el, message, isError) => setMessagesStatus(message, isError),
});

const exonet = initExonet({
  api,
  getState: () => state,
  formatRaxHtml,
  formatRaxPlain,
  formatMarketSource,
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
  tradeAuctions: [],
  tradeMarketValue: 0,
  auctionFeePercent: 5,
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
  registerProfileGroup: document.getElementById("register-profile-group"),
  registerLocaleSelect: document.getElementById("locale-select-register"),
  registerGenderInput: document.getElementById("register-gender-input"),
  registerPronounsGroup: document.getElementById("register-pronouns-group"),
  registerPronounsInput: document.getElementById("register-pronouns-input"),
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
  exonetBtn: document.getElementById("exonet-btn"),
  exonetModal: document.getElementById("exonet-modal"),
  financeModal: document.getElementById("finance-modal"),
  supplyModal: document.getElementById("supply-modal"),
  storeModal: document.getElementById("store-modal"),
  storeMarketInfo: document.getElementById("store-market-info"),
  storeSupplyList: document.getElementById("store-supply-list"),
  storeCompanyNameList: document.getElementById("store-company-name-list"),
  storeCompanyNameStatus: document.getElementById("store-company-name-status"),
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
  tradeMarketValue: document.getElementById("trade-market-value"),
  auctionCreateForm: document.getElementById("auction-create-form"),
  auctionItem: document.getElementById("auction-item"),
  auctionQuantity: document.getElementById("auction-quantity"),
  auctionStartPrice: document.getElementById("auction-start-price"),
  auctionDuration: document.getElementById("auction-duration"),
  auctionCreateBtn: document.getElementById("auction-create-btn"),
  auctionStatus: document.getElementById("auction-status"),
  auctionList: document.getElementById("auction-list"),
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
  profileFriendsList: document.getElementById("profile-friends-list"),
  profileMineName: document.getElementById("profile-mine-name"),
  profileGameDay: document.getElementById("profile-game-day"),
  profileCredits: document.getElementById("profile-credits"),
  profileWorkers: document.getElementById("profile-workers"),
  profileZones: document.getElementById("profile-zones"),
  profileSidebarNumber: document.getElementById("profile-sidebar-number"),
  profileSidebarMemberSince: document.getElementById("profile-sidebar-member-since"),
  profileCustomizeBtn: document.getElementById("profile-customize-btn"),
  profileEditModal: document.getElementById("profile-edit-modal"),
  profileEditCard: document.getElementById("profile-edit-card"),
  profileEditBackBtn: document.getElementById("profile-edit-back-btn"),
  profileEditFlagNotice: document.getElementById("profile-edit-flag-notice"),
  profileEditFlagComment: document.getElementById("profile-edit-flag-comment"),
  profileEditAvatar: document.getElementById("profile-edit-avatar"),
  profileEditAvatarImg: document.getElementById("profile-edit-avatar-img"),
  profileEditAvatarInitials: document.getElementById("profile-edit-avatar-initials"),
  profileEditNumber: document.getElementById("profile-edit-number"),
  profileAddFriendNumber: document.getElementById("profile-add-friend-number"),
  profileAddFriendSubmitBtn: document.getElementById("profile-add-friend-submit-btn"),
  profileAddFriendStatus: document.getElementById("profile-add-friend-status"),
  profilePhotoUpload: document.getElementById("profile-photo-upload"),
  profilePhotoInput: document.getElementById("profile-photo-input"),
  profilePhotoChooseBtn: document.getElementById("profile-photo-choose-btn"),
  profilePhotoBtn: document.getElementById("profile-photo-btn"),
  profilePhotoStatus: document.getElementById("profile-photo-status"),
  profileAvatarPresetSection: document.getElementById("profile-avatar-preset-section"),
  profileAvatarPresetGrid: document.getElementById("profile-avatar-preset-grid"),
  profileAvatarPresetStatus: document.getElementById("profile-avatar-preset-status"),
  profileBanner: document.getElementById("profile-banner"),
  profileBackgroundPreview: document.getElementById("profile-background-preview"),
  profileBackgroundInput: document.getElementById("profile-background-input"),
  profileBackgroundChooseBtn: document.getElementById("profile-background-choose-btn"),
  profileBackgroundUploadBtn: document.getElementById("profile-background-upload-btn"),
  profileBackgroundRemoveBtn: document.getElementById("profile-background-remove-btn"),
  profileBackgroundStatus: document.getElementById("profile-background-status"),
  profileLocaleInput: document.getElementById("profile-locale-input"),
  profileGenderInput: document.getElementById("profile-gender-input"),
  profilePreferredPronounsSection: document.getElementById("profile-preferred-pronouns-section"),
  profilePreferredPronounsInput: document.getElementById("profile-preferred-pronouns-input"),
  profileGenderLabel: document.getElementById("profile-gender-label"),
  profileGenderDisplay: document.getElementById("profile-gender-display"),
  profilePronounsDisplay: document.getElementById("profile-pronouns-display"),
  profilePronounsHint: document.getElementById("profile-pronouns-hint"),
  profileMoodInput: document.getElementById("profile-mood-input"),
  profileCompanyNameInput: document.getElementById("profile-company-name-input"),
  profileCompanyColumn: document.getElementById("profile-company-column"),
  profileCompanyLogoSlot: document.getElementById("profile-company-logo-slot"),
  profileCompanyLogo: document.getElementById("profile-company-logo"),
  profileCompanyLogoPlaceholder: document.getElementById("profile-company-logo-placeholder"),
  profileCompanyLogoPreview: document.getElementById("profile-company-logo-preview"),
  profileCompanyLogoInput: document.getElementById("profile-company-logo-input"),
  profileCompanyLogoChooseBtn: document.getElementById("profile-company-logo-choose-btn"),
  profileCompanyLogoUploadBtn: document.getElementById("profile-company-logo-upload-btn"),
  profileCompanyLogoGenerateBtn: document.getElementById("profile-company-logo-generate-btn"),
  profileCompanyLogoStatus: document.getElementById("profile-company-logo-status"),
  profileCompanySaveBtn: document.getElementById("profile-company-save-btn"),
  profileCompanyRegenerateBtn: document.getElementById("profile-company-regenerate-btn"),
  profileCompanyStatus: document.getElementById("profile-company-status"),
  profileCompanyListPrice: document.getElementById("profile-company-list-price"),
  profileCompanyListBtn: document.getElementById("profile-company-list-btn"),
  profileCompanyCancelListBtn: document.getElementById("profile-company-cancel-list-btn"),
  profileCompanyListStatus: document.getElementById("profile-company-list-status"),
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
  profileCompletionModal: document.getElementById("profile-completion-modal"),
  profileCompletionLocaleBlock: document.getElementById("profile-completion-locale-block"),
  profileCompletionLocaleSelect: document.getElementById("profile-completion-locale-select"),
  profileCompletionGenderBlock: document.getElementById("profile-completion-gender-block"),
  profileCompletionGenderInput: document.getElementById("profile-completion-gender-input"),
  profileCompletionPronounsBlock: document.getElementById("profile-completion-pronouns-block"),
  profileCompletionPronounsInput: document.getElementById("profile-completion-pronouns-input"),
  profileCompletionSaveBtn: document.getElementById("profile-completion-save-btn"),
  profileCompletionStatus: document.getElementById("profile-completion-status"),
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
  setHidden(els.registerProfileGroup, !isRegister);
  if (isRegister) {
    syncRegisterGenderUi();
    if (els.registerLocaleSelect) {
      els.registerLocaleSelect.value = getLocale() || "en";
    }
  }
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
    els.toggleMode.textContent = t("auth.switchToLogin");
    ensureBirthdayDropdownsReady();
    showLoginStatus(t("auth.hint.register"), "info");
  } else if (isForgot) {
    showLoginStatus(t("auth.hint.forgot"), "info");
  } else if (isReset) {
    showLoginStatus(t("auth.hint.reset"), "info");
  } else if (isBanAppeal) {
    els.banAppealNotice.textContent = state.banMessage;
    els.banAppealMessage.value = "";
    showLoginStatus(t("auth.hint.banAppeal"), "info");
  } else {
    els.toggleMode.textContent = t("auth.switchToRegister");
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

let cachedGameVersion = "";

function setGameVersionLabel(label) {
  const versionEl = document.getElementById("game-version");
  if (!versionEl) {
    return;
  }

  const text = label?.trim();
  if (!text) {
    versionEl.hidden = true;
    versionEl.textContent = "";
    cachedGameVersion = "";
    return;
  }

  cachedGameVersion = text;
  versionEl.textContent = text;
  versionEl.hidden = els.loginScreen?.hidden ?? false;
}

async function initGameVersionTag() {
  try {
    const status = await api.getStatus();
    setGameVersionLabel(status?.gameVersion);
  } catch {
    setGameVersionLabel("");
  }
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
  if (screen === "login" && cachedGameVersion) {
    setGameVersionLabel(cachedGameVersion);
  } else if (screen !== "login") {
    const versionEl = document.getElementById("game-version");
    if (versionEl) {
      versionEl.hidden = true;
    }
  }
  if (screen !== "game") {
    closeModals();
  }
  if (screen === "game") {
    startUtcTimers();
  } else {
    stopUtcTimers();
  }
}

function setHidden(el, hidden) {
  if (el) {
    el.hidden = hidden;
  }
}

function hideProfileScreens() {
  setHidden(els.profileModal, true);
  setHidden(els.profileEditModal, true);
}

function isProfileCompletionBlocking() {
  return Boolean(els.profileCompletionModal && !els.profileCompletionModal.hidden);
}

function closeModals() {
  if (isProfileCompletionBlocking()) {
    return;
  }

  els.financeModal.hidden = true;
  els.supplyModal.hidden = true;
  els.dayModal.hidden = true;
  hideProfileScreens();
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  els.eventModal.hidden = true;
  exonet.close();
}

function openModal(modal) {
  if (!modal || isProfileCompletionBlocking()) {
    return;
  }

  exonet.close();
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
  if (!el) {
    return;
  }
  const text = (value ?? "").trim();
  el.textContent = text || emptyText;
  el.classList.toggle("empty", !text);
}

function applyProfileTheme() {
  const themeClass = "profile-card theme-classic";
  if (els.profileCard) {
    els.profileCard.className = themeClass;
  }
  if (els.profileEditCard) {
    els.profileEditCard.className = themeClass;
  }
}

function resolveProfileAssetUrl(url) {
  if (!url) {
    return "";
  }

  if (/^https?:\/\//i.test(url)) {
    return url;
  }

  const apiBase = (API_BASE_URL || readMetaApiBase()).replace(/\/$/, "");
  if (url.startsWith("/") && apiBase) {
    return `${apiBase}${url}`;
  }

  return url;
}

function applyProfileBannerBackground(bannerEl, url) {
  if (!bannerEl) {
    return;
  }

  const resolved = resolveProfileAssetUrl(url);
  if (resolved) {
    bannerEl.style.backgroundImage = `url("${resolved}")`;
    bannerEl.classList.add("has-custom-background");
  } else {
    bannerEl.style.removeProperty("background-image");
    bannerEl.classList.remove("has-custom-background");
  }
}

function renderProfileBackgroundPreview(profile) {
  if (!els.profileBackgroundPreview) {
    return;
  }

  applyProfileBannerBackground(els.profileBackgroundPreview, profile?.profileBackgroundUrl);
  setHidden(els.profileBackgroundRemoveBtn, !profile?.profileBackgroundUrl);
}

function clearProfileAvatarPhoto(imgEl, initialsEl, avatarEl) {
  if (!imgEl || !initialsEl || !avatarEl) {
    return;
  }

  imgEl.removeAttribute("src");
  imgEl.hidden = true;
  imgEl.onerror = null;
  avatarEl.classList.remove("has-photo");
}

function renderAvatarOnElements(profile, imgEl, initialsEl, avatarEl) {
  if (!imgEl || !initialsEl || !avatarEl) {
    return;
  }

  initialsEl.textContent = profileInitials(profile.username);

  const imageUrl = resolveProfileAssetUrl(profile.profileImageUrl);
  if (!imageUrl) {
    clearProfileAvatarPhoto(imgEl, initialsEl, avatarEl);
    return;
  }

  imgEl.onerror = () => {
    clearProfileAvatarPhoto(imgEl, initialsEl, avatarEl);
  };
  imgEl.src = imageUrl;
  imgEl.hidden = false;
  avatarEl.classList.add("has-photo");
}

function renderProfileAvatar(profile) {
  renderAvatarOnElements(
    profile,
    els.profileAvatarImg,
    els.profileAvatarInitials,
    els.profileAvatar,
  );
}

function renderProfileEditAvatar(profile) {
  renderAvatarOnElements(
    profile,
    els.profileEditAvatarImg,
    els.profileEditAvatarInitials,
    els.profileEditAvatar,
  );
}

function companyLogoInitials(companyName) {
  return profileInitials(companyName);
}

function renderCompanyLogoOnElement(profile, imgEl, { slotEl = null, placeholderEl = null } = {}) {
  if (!imgEl) {
    return;
  }

  if (placeholderEl) {
    placeholderEl.textContent = "";
    placeholderEl.hidden = true;
  }

  if (profile?.isReporter) {
    imgEl.removeAttribute("src");
    imgEl.hidden = true;
    imgEl.onerror = null;
    slotEl?.classList.remove("has-logo");
    return;
  }

  const companyName = profile?.mineName?.trim() || "Company";
  const imageUrl = resolveProfileAssetUrl(profile?.companyLogoUrl);
  if (!imageUrl) {
    imgEl.removeAttribute("src");
    imgEl.hidden = true;
    imgEl.onerror = null;
    slotEl?.classList.remove("has-logo");
    return;
  }

  imgEl.alt = companyName;
  imgEl.onerror = () => {
    imgEl.removeAttribute("src");
    imgEl.hidden = true;
    imgEl.onerror = null;
    slotEl?.classList.remove("has-logo");
  };
  imgEl.src = imageUrl;
  imgEl.hidden = false;
  slotEl?.classList.add("has-logo");
}

function renderProfileCompanyLogo(profile) {
  setHidden(els.profileCompanyColumn, Boolean(profile?.isReporter));
  renderCompanyLogoOnElement(profile, els.profileCompanyLogo, {
    slotEl: els.profileCompanyLogoSlot,
    placeholderEl: els.profileCompanyLogoPlaceholder,
  });
  renderCompanyLogoOnElement(profile, els.profileCompanyLogoPreview);
  updateCompanyLogoGenerationUi(profile);
}

function updateCompanyLogoGenerationUi(profile) {
  if (!els.profileCompanyLogoGenerateBtn) {
    return;
  }

  const aiEnabled = Boolean(profile?.companyLogoAiEnabled);
  const status = (profile?.companyLogoGenerationStatus ?? "none").toLowerCase();
  const isOwner = Boolean(profile?.isOwner);
  const isReporter = Boolean(profile?.isReporter);
  const busy = status === "queued" || status === "processing";

  setHidden(els.profileCompanyLogoGenerateBtn, !aiEnabled || !isOwner || isReporter);
  if (els.profileCompanyLogoGenerateBtn) {
    els.profileCompanyLogoGenerateBtn.disabled = busy;
    els.profileCompanyLogoGenerateBtn.textContent = busy
      ? (status === "processing" ? t("profile.logoGenerating") : t("profile.logoQueued"))
      : t("profile.edit.generateLogo");
  }

  if (els.profileCompanyLogoStatus && profile?.companyLogoGenerationMessage && busy) {
    els.profileCompanyLogoStatus.textContent = profile.companyLogoGenerationMessage;
    els.profileCompanyLogoStatus.classList.remove("error", "success");
  }
}

let companyLogoGenerationPollTimer = null;

function stopCompanyLogoGenerationPoll() {
  if (companyLogoGenerationPollTimer) {
    clearInterval(companyLogoGenerationPollTimer);
    companyLogoGenerationPollTimer = null;
  }
}

function syncCompanyLogoGenerationPoll(profile) {
  const status = (profile?.companyLogoGenerationStatus ?? "none").toLowerCase();
  const shouldPoll = Boolean(profile?.isOwner) && (status === "queued" || status === "processing");

  if (!shouldPoll) {
    stopCompanyLogoGenerationPoll();
    return;
  }

  if (companyLogoGenerationPollTimer) {
    return;
  }

  companyLogoGenerationPollTimer = setInterval(() => {
    pollCompanyLogoGeneration().catch(() => {
      stopCompanyLogoGenerationPoll();
    });
  }, 4000);
}

async function pollCompanyLogoGeneration() {
  const generation = await api.getCompanyLogoGeneration();
  const status = (generation?.status ?? "none").toLowerCase();
  if (!state.profile) {
    stopCompanyLogoGenerationPoll();
    return;
  }

  state.profile = {
    ...state.profile,
    companyLogoGenerationStatus: status,
    companyLogoGenerationMessage: generation?.message ?? "",
  };

  if (status === "queued" || status === "processing") {
    updateCompanyLogoGenerationUi(state.profile);
    return;
  }

  stopCompanyLogoGenerationPoll();
  const profile = await api.getMyProfile();
  renderProfile(profile);
  if (!els.profileEditModal?.hidden) {
    populateProfileEditForm(profile);
  }

  if (els.profileCompanyLogoStatus) {
    if (status === "failed") {
      els.profileCompanyLogoStatus.textContent = generation?.message || t("profile.logoFailed");
      els.profileCompanyLogoStatus.classList.add("error");
    } else if (profile.companyLogoUrl) {
      els.profileCompanyLogoStatus.textContent = t("profile.logoReady");
      els.profileCompanyLogoStatus.classList.add("success");
    }
  }
}

async function enqueueCompanyLogoGeneration() {
  if (!els.profileCompanyLogoGenerateBtn) {
    return;
  }

  els.profileCompanyLogoGenerateBtn.disabled = true;
  if (els.profileCompanyLogoStatus) {
    els.profileCompanyLogoStatus.textContent = t("profile.logoJoinQueue");
    els.profileCompanyLogoStatus.classList.remove("error", "success");
  }

  try {
    const response = await api.enqueueCompanyLogoGeneration();
    const generation = response?.generation ?? response;
    state.profile = {
      ...state.profile,
      companyLogoGenerationStatus: generation?.status ?? "queued",
      companyLogoGenerationMessage: response?.message ?? generation?.message ?? "Queued for AI logo generation.",
    };
    updateCompanyLogoGenerationUi(state.profile);
    syncCompanyLogoGenerationPoll(state.profile);
    if (els.profileCompanyLogoStatus) {
      els.profileCompanyLogoStatus.textContent = state.profile.companyLogoGenerationMessage;
    }
  } catch (error) {
    if (els.profileCompanyLogoStatus) {
      els.profileCompanyLogoStatus.textContent = error.message;
      els.profileCompanyLogoStatus.classList.add("error");
    }
    updateCompanyLogoGenerationUi(state.profile);
  }
}

function profileUpdatePayloadFromState(overrides = {}) {
  const profile = state.profile ?? {};
  const gender =
    overrides.profileGender ??
    els.profileGenderInput?.value ??
    profile.profileGender ??
    "";
  const preferredPronouns = profileGenderRequiresPronouns(gender)
    ? (overrides.profilePreferredPronouns ??
      els.profilePreferredPronounsInput?.value ??
      profile.profilePreferredPronouns ??
      "")
    : "";

  return {
    mood: profile.mood ?? "",
    aboutMe: profile.aboutMe ?? "",
    music: profile.music ?? "",
    interests: profile.interests ?? "",
    discord: profile.discord ?? "",
    bluesky: profile.bluesky ?? "",
    twitter: profile.twitter ?? "",
    youtube: profile.youtube ?? "",
    facebook: profile.facebook ?? "",
    profileAvatarPreset: profile.profileAvatarPreset ?? "neutral",
    profileGender: gender,
    profilePreferredPronouns: preferredPronouns,
    profileLocale:
      overrides.profileLocale ??
      els.profileLocaleInput?.value ??
      profile.profileLocale ??
      getLocale(),
    ...overrides,
  };
}

function profileEditFormPayload() {
  return profileUpdatePayloadFromState({
    mood: els.profileMoodInput?.value.trim() ?? "",
    aboutMe: els.profileAboutInput?.value ?? "",
    music: els.profileMusicInput?.value.trim() ?? "",
    interests: els.profileInterestsInput?.value ?? "",
    discord: els.profileDiscordInput?.value.trim() ?? "",
    bluesky: els.profileBlueskyInput?.value.trim() ?? "",
    twitter: els.profileTwitterInput?.value.trim() ?? "",
    youtube: els.profileYoutubeInput?.value.trim() ?? "",
    facebook: els.profileFacebookInput?.value.trim() ?? "",
  });
}

function profileCompletionNeedsField(profile, fieldId) {
  return (profile?.missingProfileFields ?? []).some((field) => field.fieldId === fieldId);
}

function syncProfileCompletionGenderUi() {
  const gender = els.profileCompletionGenderInput?.value ?? "";
  const needsGender = profileCompletionNeedsField(state.profile, "gender");
  const needsPronounsField = profileCompletionNeedsField(state.profile, "preferredPronouns");
  const showPronouns =
    profileGenderRequiresPronouns(gender) && (needsPronounsField || needsGender);
  setHidden(els.profileCompletionPronounsBlock, !showPronouns);
  if (!needsPronouns && els.profileCompletionPronounsInput) {
    els.profileCompletionPronounsInput.value = "";
  }
}

function renderProfileCompletionModal(profile) {
  if (!els.profileCompletionModal || !profile?.profileCompletionRequired) {
    setHidden(els.profileCompletionModal, true);
    return;
  }

  const needsLocale = profileCompletionNeedsField(profile, "locale");
  const needsGender = profileCompletionNeedsField(profile, "gender");
  const needsPronouns = profileCompletionNeedsField(profile, "preferredPronouns");

  setHidden(els.profileCompletionLocaleBlock, !needsLocale);
  setHidden(els.profileCompletionGenderBlock, !needsGender);
  setHidden(els.profileCompletionPronounsBlock, !needsPronouns);

  if (needsLocale && els.profileCompletionLocaleSelect) {
    wireLocaleSelector(els.profileCompletionLocaleSelect);
    els.profileCompletionLocaleSelect.value = profile.profileLocale || getLocale();
  }

  if (els.profileCompletionGenderInput) {
    els.profileCompletionGenderInput.value = needsGender ? (profile.profileGender ?? "") : "";
  }
  if (els.profileCompletionPronounsInput) {
    els.profileCompletionPronounsInput.value = profile.profilePreferredPronouns ?? "";
  }

  syncProfileCompletionGenderUi();
  applyTranslations(els.profileCompletionModal);

  if (els.profileCompletionStatus) {
    els.profileCompletionStatus.textContent = "";
    els.profileCompletionStatus.classList.remove("error", "success");
  }

  els.profileCompletionModal.hidden = false;
  document.body.appendChild(els.profileCompletionModal);
  if (needsLocale) {
    els.profileCompletionLocaleSelect?.focus();
  } else {
    els.profileCompletionGenderInput?.focus();
  }
}

async function maybeShowProfileCompletion(profile) {
  if (!profile?.isOwner || profile.isReporter) {
    setHidden(els.profileCompletionModal, true);
    return;
  }

  if (profile.profileCompletionRequired) {
    state.profile = profile;
    renderProfileCompletionModal(profile);
    return;
  }

  setHidden(els.profileCompletionModal, true);
  await applyProfileLocaleFromServer(profile);
}

async function saveProfileCompletion() {
  if (!state.profile) {
    return;
  }

  const needsLocale = profileCompletionNeedsField(state.profile, "locale");
  const needsGender = profileCompletionNeedsField(state.profile, "gender");
  const profileLocale = needsLocale
    ? (els.profileCompletionLocaleSelect?.value ?? "")
    : null;

  if (needsLocale && !profileLocale) {
    els.profileCompletionStatus.textContent = t("profile.completion.localeRequired");
    els.profileCompletionStatus.classList.add("error");
    els.profileCompletionLocaleSelect?.focus();
    return;
  }

  const gender = needsGender
    ? (els.profileCompletionGenderInput?.value ?? "")
    : (state.profile.profileGender ?? "");
  if (needsGender && !gender) {
    els.profileCompletionStatus.textContent = t("profile.completion.genderRequired");
    els.profileCompletionStatus.classList.add("error");
    els.profileCompletionGenderInput?.focus();
    return;
  }

  const preferredPronouns = profileGenderRequiresPronouns(gender)
    ? needsGender || profileCompletionNeedsField(state.profile, "preferredPronouns")
      ? (els.profileCompletionPronounsInput?.value ?? "")
      : (state.profile.profilePreferredPronouns ?? "")
    : "";

  if (profileGenderRequiresPronouns(gender) && !preferredPronouns) {
    els.profileCompletionStatus.textContent = t("profile.completion.pronounsRequired");
    els.profileCompletionStatus.classList.add("error");
    els.profileCompletionPronounsInput?.focus();
    return;
  }

  els.profileCompletionStatus.textContent = t("profile.completion.saving");
  els.profileCompletionStatus.classList.remove("error", "success");
  if (els.profileCompletionSaveBtn) {
    els.profileCompletionSaveBtn.disabled = true;
  }

  try {
    const overrides = {
      profileGender: gender,
      profilePreferredPronouns: preferredPronouns,
    };
    if (needsLocale && profileLocale) {
      overrides.profileLocale = profileLocale;
    }

    const profile = await api.updateProfile(profileUpdatePayloadFromState(overrides));
    state.profile = profile;
    if (needsLocale && profileLocale) {
      await setLocale(profileLocale);
      applyTranslations(document);
    }
    renderProfileGenderPronouns(profile);
    await maybeShowProfileCompletion(profile);
    els.profileCompletionStatus.textContent = t("profile.completion.saved");
    els.profileCompletionStatus.classList.add("success");
  } catch (error) {
    els.profileCompletionStatus.textContent = error.message;
    els.profileCompletionStatus.classList.add("error");
  } finally {
    if (els.profileCompletionSaveBtn) {
      els.profileCompletionSaveBtn.disabled = false;
    }
  }
}

function syncProfileGenderPronounsUi() {
  const gender = els.profileGenderInput?.value ?? "";
  const needsPronouns = profileGenderRequiresPronouns(gender);
  setHidden(els.profilePreferredPronounsSection, !needsPronouns);
  if (els.profilePreferredPronounsInput && !needsPronouns) {
    els.profilePreferredPronounsInput.value = "";
  }
}

function renderProfileGenderPronouns(profile) {
  if (profile?.isReporter) {
    setHidden(els.profileGenderLabel, true);
    setHidden(els.profileGenderDisplay, true);
    if (els.profilePronounsDisplay) {
      els.profilePronounsDisplay.textContent = "—";
    }
    setHidden(els.profilePronounsHint, true);
    return;
  }

  const label = profile?.pronounLabel || "they/them";
  if (els.profilePronounsDisplay) {
    els.profilePronounsDisplay.textContent = label;
  }

  const showGender = Boolean(profile?.isOwner && profile?.profileGender);
  setHidden(els.profileGenderLabel, !showGender);
  setHidden(els.profileGenderDisplay, !showGender);
  if (showGender && els.profileGenderDisplay) {
    els.profileGenderDisplay.textContent = genderLabel(profile.profileGender);
  }

  if (els.profilePronounsHint) {
    const obj = profile?.pronounObject || "them";
    const pos = profile?.pronounPossessive || "their";
    if (profile?.isOwner) {
      els.profilePronounsHint.textContent = t("profile.pronounsHintOwner", {
        object: obj,
        possessive: pos,
        possessiveCap: capitalizePronoun(pos),
      });
      setHidden(els.profilePronounsHint, false);
    } else if (!profile?.isOwner && profile?.profileGender) {
      els.profilePronounsHint.textContent = t("profile.pronounsHintViewer", {
        name: profile.username,
        object: obj,
        label,
      });
      setHidden(els.profilePronounsHint, false);
    } else {
      setHidden(els.profilePronounsHint, true);
    }
  }
}

function renderProfileAvatarPresets(profile) {
  if (!els.profileAvatarPresetGrid || !els.profileAvatarPresetSection) {
    return;
  }

  const hasCustom = Boolean(profile?.hasCustomProfilePhoto);
  const selected = profile?.profileAvatarPreset || "neutral";
  setHidden(els.profileAvatarPresetSection, Boolean(profile?.isReporter));

  els.profileAvatarPresetGrid.innerHTML = PROFILE_AVATAR_PRESETS.map((preset) => {
    const assetUrl = resolveProfileAssetUrl(`/images/profile-defaults/${preset.id}.svg`);
    const isSelected = preset.id === selected;
    return `
      <button
        type="button"
        class="profile-avatar-preset-option${isSelected ? " is-selected" : ""}"
        data-avatar-preset="${preset.id}"
        aria-pressed="${isSelected ? "true" : "false"}"
        ${hasCustom ? "disabled" : ""}
      >
        <img class="profile-avatar-preset-thumb" src="${assetUrl}" alt="">
        <span class="profile-avatar-preset-label">${t(preset.labelKey)}</span>
      </button>`;
  }).join("");

  els.profileAvatarPresetGrid.querySelectorAll("[data-avatar-preset]").forEach((button) => {
    button.addEventListener("click", () => {
      selectProfileAvatarPreset(button.dataset.avatarPreset).catch((error) => {
        if (els.profileAvatarPresetStatus) {
          els.profileAvatarPresetStatus.textContent = error.message;
          els.profileAvatarPresetStatus.classList.add("error");
        }
      });
    });
  });

  if (els.profileAvatarPresetStatus && !hasCustom) {
    els.profileAvatarPresetStatus.textContent = "";
    els.profileAvatarPresetStatus.classList.remove("error", "success");
  } else if (els.profileAvatarPresetStatus && hasCustom) {
    els.profileAvatarPresetStatus.textContent = t("profile.avatarCustomShown");
    els.profileAvatarPresetStatus.classList.remove("error", "success");
  }
}

async function selectProfileAvatarPreset(preset) {
  if (!state.profile?.isOwner || state.profile?.hasCustomProfilePhoto) {
    return;
  }

  if (els.profileAvatarPresetStatus) {
    els.profileAvatarPresetStatus.textContent = t("profile.avatarSaving");
    els.profileAvatarPresetStatus.classList.remove("error", "success");
  }

  const profile = await api.updateProfile({
    ...profileEditFormPayload(),
    profileAvatarPreset: preset,
  });
  state.profile = profile;
  renderProfile(profile);
  renderProfileEditAvatar(profile);
  renderProfileAvatarPresets(profile);
  if (els.profileAvatarPresetStatus) {
    els.profileAvatarPresetStatus.textContent = t("profile.avatarUpdated");
    els.profileAvatarPresetStatus.classList.add("success");
  }
}

function populateProfileEditForm(profile) {
  if (els.profileEditNumber) {
    els.profileEditNumber.textContent = profile.profileNumber || "---";
  }
  if (els.profileCompanyNameInput) {
    els.profileCompanyNameInput.value = profile.mineName ?? "";
  }
  if (els.profileLocaleInput) {
    wireLocaleSelector(els.profileLocaleInput);
    els.profileLocaleInput.value = profile.profileLocale || getLocale();
  }
  if (els.profileGenderInput) {
    els.profileGenderInput.value = profile.profileGender ?? "";
  }
  if (els.profilePreferredPronounsInput) {
    els.profilePreferredPronounsInput.value = profile.profilePreferredPronouns ?? "";
  }
  syncProfileGenderPronounsUi();
  if (els.profileMoodInput) {
    els.profileMoodInput.value = profile.mood ?? "";
  }
  if (els.profileAboutInput) {
    els.profileAboutInput.value = profile.aboutMe ?? "";
  }
  if (els.profileInterestsInput) {
    els.profileInterestsInput.value = profile.interests ?? "";
  }
  if (els.profileMusicInput) {
    els.profileMusicInput.value = profile.music ?? "";
  }
  if (els.profileDiscordInput) {
    els.profileDiscordInput.value = profile.discord ?? "";
  }
  if (els.profileBlueskyInput) {
    els.profileBlueskyInput.value = profile.bluesky ?? "";
  }
  if (els.profileTwitterInput) {
    els.profileTwitterInput.value = profile.twitter ?? "";
  }
  if (els.profileYoutubeInput) {
    els.profileYoutubeInput.value = profile.youtube ?? "";
  }
  if (els.profileFacebookInput) {
    els.profileFacebookInput.value = profile.facebook ?? "";
  }
  if (els.profileSaveStatus) {
    els.profileSaveStatus.textContent = "";
    els.profileSaveStatus.classList.remove("error", "success");
  }
  if (els.profileAddFriendStatus) {
    els.profileAddFriendStatus.textContent = "";
    els.profileAddFriendStatus.classList.remove("error", "success");
  }
  if (els.profilePhotoStatus) {
    els.profilePhotoStatus.textContent = "";
    els.profilePhotoStatus.classList.remove("error", "success");
  }
  renderProfileAvatarPresets(profile);
  if (els.profilePhotoBtn) {
    els.profilePhotoBtn.disabled = true;
  }
  if (els.profilePhotoChooseBtn) {
    els.profilePhotoChooseBtn.textContent = t("profile.edit.choosePhoto");
  }
  if (els.profilePhotoInput) {
    els.profilePhotoInput.value = "";
  }
  if (els.profileBackgroundStatus) {
    els.profileBackgroundStatus.textContent = "";
    els.profileBackgroundStatus.classList.remove("error", "success");
  }
  if (els.profileBackgroundUploadBtn) {
    els.profileBackgroundUploadBtn.disabled = true;
  }
  if (els.profileBackgroundChooseBtn) {
    els.profileBackgroundChooseBtn.textContent = t("profile.edit.chooseImage");
  }
  if (els.profileBackgroundInput) {
    els.profileBackgroundInput.value = "";
  }
  renderProfileCompanyLogo(profile);
  if (els.profileCompanyLogoStatus) {
    els.profileCompanyLogoStatus.textContent = "";
    els.profileCompanyLogoStatus.classList.remove("error", "success");
  }
  if (els.profileCompanyLogoUploadBtn) {
    els.profileCompanyLogoUploadBtn.disabled = true;
  }
  if (els.profileCompanyLogoChooseBtn) {
    els.profileCompanyLogoChooseBtn.textContent = t("profile.edit.choosePng");
  }
  if (els.profileCompanyLogoInput) {
    els.profileCompanyLogoInput.value = "";
  }
  renderProfileBackgroundPreview(profile);
  renderCompanyNameListingControls(profile);
}

function renderCompanyNameListingControls(profile) {
  if (!els.profileCompanyNameInput) {
    return;
  }

  const listed = Boolean(profile.companyNameListed);
  setHidden(els.profileCompanyListBtn, listed);
  setHidden(els.profileCompanyCancelListBtn, !listed);
  els.profileCompanyListPrice.disabled = listed;
  els.profileCompanyRegenerateBtn.disabled = listed;
  els.profileCompanySaveBtn.disabled = listed;
  els.profileCompanyNameInput.readOnly = listed;

  if (listed) {
    els.profileCompanyListPrice.value = String(profile.companyNameListingPrice ?? "");
    els.profileCompanyListStatus.textContent = t("profile.companyListListed", {
      price: formatRaxPlain(profile.companyNameListingPrice ?? 0),
    });
    els.profileCompanyListStatus.classList.add("success");
  } else {
    els.profileCompanyListStatus.textContent = "";
    els.profileCompanyListStatus.classList.remove("error", "success");
  }

  els.profileCompanyStatus.textContent = "";
  els.profileCompanyStatus.classList.remove("error", "success");
}

function applyCompanyNameUpdate(result) {
  if (!state.profile || !result) {
    return;
  }

  state.profile = {
    ...state.profile,
    mineName: result.companyName,
    mineId: result.mineId,
    companyNameListed: result.companyNameListed,
    companyNameListingId: result.companyNameListingId,
    companyNameListingPrice: result.companyNameListingPrice,
  };

  els.profileMineName.textContent = result.companyName;
  populateProfileEditForm(state.profile);
  renderProfile(state.profile);

  if (state.mine) {
    state.mine = { ...state.mine, name: result.companyName };
    renderHud();
  }
}

async function saveCompanyName() {
  const companyName = els.profileCompanyNameInput.value.trim();
  if (!companyName) {
    els.profileCompanyStatus.textContent = t("profile.companyNameRequired");
    els.profileCompanyStatus.classList.add("error");
    return;
  }

  els.profileCompanyStatus.textContent = t("profile.companyNameSaving");
  els.profileCompanyStatus.classList.remove("error", "success");
  try {
    const result = await api.updateCompanyName(companyName);
    applyCompanyNameUpdate(result);
    els.profileCompanyStatus.textContent = result.message;
    els.profileCompanyStatus.classList.add("success");
  } catch (error) {
    els.profileCompanyStatus.textContent = error.message;
    els.profileCompanyStatus.classList.add("error");
  }
}

async function regenerateCompanyName() {
  els.profileCompanyStatus.textContent = t("profile.companyNameGenerating");
  els.profileCompanyStatus.classList.remove("error", "success");
  try {
    const result = await api.regenerateCompanyName();
    applyCompanyNameUpdate(result);
    els.profileCompanyStatus.textContent = result.message;
    els.profileCompanyStatus.classList.add("success");
  } catch (error) {
    els.profileCompanyStatus.textContent = error.message;
    els.profileCompanyStatus.classList.add("error");
  }
}

async function listCompanyNameForSale() {
  const price = Number(els.profileCompanyListPrice.value);
  if (!Number.isFinite(price) || price < 1) {
    els.profileCompanyListStatus.textContent = t("profile.companyListNeedPrice", {
      rax: RAX_NAME.toLowerCase(),
    });
    els.profileCompanyListStatus.classList.add("error");
    return;
  }

  els.profileCompanyListStatus.textContent = t("profile.companyListListing");
  els.profileCompanyListStatus.classList.remove("error", "success");
  try {
    const result = await api.listCompanyName(price);
    applyCompanyNameUpdate(result);
    els.profileCompanyListStatus.textContent = result.message;
    els.profileCompanyListStatus.classList.add("success");
  } catch (error) {
    els.profileCompanyListStatus.textContent = error.message;
    els.profileCompanyListStatus.classList.add("error");
  }
}

async function cancelCompanyNameListing() {
  const listingId = state.profile?.companyNameListingId;
  if (!listingId) {
    return;
  }

  els.profileCompanyListStatus.textContent = t("profile.companyListCancelling");
  els.profileCompanyListStatus.classList.remove("error", "success");
  try {
    const result = await api.cancelCompanyNameListing(listingId);
    applyCompanyNameUpdate(result);
    els.profileCompanyListStatus.textContent = result.message;
    els.profileCompanyListStatus.classList.add("success");
  } catch (error) {
    els.profileCompanyListStatus.textContent = error.message;
    els.profileCompanyListStatus.classList.add("error");
  }
}

async function purchaseCompanyNameListing(listingId) {
  els.storeCompanyNameStatus.textContent = t("market.storePurchasing");
  try {
    const result = await api.purchaseCompanyName(listingId);
    els.storeCompanyNameStatus.textContent = result.message;
    await refreshAll();
    await loadStoreCompanyNames();
    if (!els.profileModal?.hidden || !els.profileEditModal?.hidden) {
      await refreshProfileIfOpen();
    }
  } catch (error) {
    els.storeCompanyNameStatus.textContent = error.message;
  }
}

async function loadStoreCompanyNames() {
  if (!els.storeCompanyNameList) {
    return;
  }

  els.storeCompanyNameList.innerHTML = "";
  try {
    const response = await api.getCompanyNameListings();
    const listings = response.listings ?? [];
    if (listings.length === 0) {
      els.storeCompanyNameList.innerHTML = `<p class='market-info'>${t("market.storeEmpty")}</p>`;
      return;
    }

    for (const listing of listings) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "shop-btn";
      button.innerHTML = `<strong>${listing.companyName}</strong><span>Seller: ${listing.sellerUsername} · ${formatRaxHtml(listing.price)}</span>`;
      button.addEventListener("click", () => {
        purchaseCompanyNameListing(listing.id).catch((error) => {
          els.storeCompanyNameStatus.textContent = error.message;
        });
      });
      els.storeCompanyNameList.appendChild(button);
    }
  } catch (error) {
    els.storeCompanyNameList.innerHTML = `<p class='market-info'>${t("market.storeLoadFailed")}</p>`;
    els.storeCompanyNameStatus.textContent = error.message;
  }
}

function formatPublicStatus(status) {
  const text = (status ?? "").trim();
  return text || t("profile.noPublicStatus");
}

function renderProfileFriends(profile) {
  if (!els.profileFriendsList) {
    return;
  }

  const friends = profile.friends ?? [];
  els.profileFriendsList.innerHTML = "";
  els.profileFriendsList.classList.toggle("empty", friends.length === 0);

  if (friends.length === 0) {
    els.profileFriendsList.textContent = profile.isOwner
      ? t("profile.friendsEmptyOwner")
      : t("profile.friendsEmptyGuest");
    return;
  }

  for (const friend of friends) {
    const item = document.createElement("article");
    item.className = "profile-friend-item";

    const head = document.createElement("div");
    head.className = "profile-friend-head";

    const name = document.createElement("button");
    name.type = "button";
    name.className = "profile-friend-name";
    name.textContent = friend.isReporter
      ? `${friend.username}${t("profile.reporterSuffix")}`
      : friend.username;
    name.addEventListener("click", () => openProfile(profileOpenKey(friend)));

    const number = document.createElement("span");
    number.className = "profile-friend-number";
    number.textContent = friend.profileNumber || "---";

    head.append(name, number);

    const status = document.createElement("p");
    status.className = "profile-friend-status";
    status.textContent = friend.isReporter
      ? t("profile.friendOnnCorrespondent")
      : formatPublicStatus(friend.publicStatus);

    const mood = document.createElement("p");
    mood.className = "profile-friend-mood";
    mood.textContent = friend.mood || (friend.isReporter ? t("profile.moodReporter") : t("profile.moodDefault"));

    item.append(head, status, mood);
    els.profileFriendsList.appendChild(item);
  }
}

function profileOpenKey(profile) {
  if (profile?.isReporter && profile.reporterSlug) {
    return profile.reporterSlug;
  }

  return profile?.username ?? "";
}

function renderProfile(profile) {
  state.profile = profile;
  applyProfileTheme();
  renderProfileAvatar(profile);
  renderProfileCompanyLogo(profile);
  applyProfileBannerBackground(els.profileBanner, profile.profileBackgroundUrl);
  const displayName = profile.isReporter ? profile.username : profile.username;
  els.profileUsername.textContent = profile.isReporter
    ? `${displayName}${t("profile.reporterSuffix")}`
    : displayName;
  els.profileMoodDisplay.textContent =
    profile.mood || (profile.isReporter ? t("profile.moodReporter") : t("profile.moodDefault"));
  els.profileNumber.textContent = profile.profileNumber || "---";
  els.profileSidebarNumber.textContent = profile.profileNumber || "---";
  const signedUp = formatProfileDate(profile.memberSince);
  els.profileMemberSince.textContent = t("profile.memberSince", { date: signedUp });
  els.profileSidebarMemberSince.textContent = signedUp;
  setProfileText(els.profileAboutView, profile.aboutMe, t("profile.aboutEmpty"));
  setProfileText(els.profileInterestsView, profile.interests, t("profile.interestsEmpty"));
  setProfileText(els.profileMusicView, profile.music, t("profile.musicEmpty"));
  if (profile.isReporter) {
    const onnPath = profile.onnProfilePath || `sites/offworld-news/reporters/${profile.reporterSlug}`;
    els.profileSocialView.innerHTML = `<p class="profile-reporter-links"><button type="button" class="btn ghost profile-reporter-link" data-open-onn-bureau>${t("profile.openOnn")}</button></p>`;
    els.profileSocialView.classList.remove("empty");
    els.profileSocialView.querySelector("[data-open-onn-bureau]")?.addEventListener("click", () => {
      exonet.open(onnPath);
    });
  } else {
    els.profileSocialView.innerHTML = renderSocialLinksHtml(profile);
    els.profileSocialView.classList.toggle("empty", !hasSocialLinks(profile));
  }
  if (profile.isReporter) {
    els.profileMineName.textContent = profile.mineName ?? t("profile.reporterNetwork");
    els.profileGameDay.textContent = "—";
    setRaxHtml(els.profileCredits, 0);
    els.profileWorkers.textContent = "—";
    els.profileZones.textContent = "—";
  } else {
    els.profileMineName.textContent = profile.mineName ?? "---";
    els.profileGameDay.textContent = String(profile.currentGameDay ?? "---");
    setRaxHtml(els.profileCredits, profile.credits ?? 0);
    els.profileWorkers.textContent = String(profile.workerCount ?? 0);
    els.profileZones.textContent = String(profile.zoneCount ?? 0);
  }

  const isOwner = Boolean(profile.isOwner);
  setHidden(els.profileCustomizeBtn, !isOwner || profile.isReporter);
  if (isOwner) {
    populateProfileEditForm(profile);
    renderProfileEditAvatar(profile);
  }

  renderProfileGenderPronouns(profile);
  renderProfileFlagNotice(profile);
  renderProfileFriendPanel(profile);
  renderProfileFriends(profile);
  syncCompanyLogoGenerationPoll(profile);
}

function renderProfileFlagNotice(profile) {
  const activeFlag = profile.activeFlag;
  const showFlag = Boolean(profile.isOwner && activeFlag);
  const comment = showFlag ? activeFlag.comment : "";

  setHidden(els.profileFlagNotice, !showFlag);
  if (els.profileFlagComment) {
    els.profileFlagComment.textContent = comment;
  }
  setHidden(els.profileEditFlagNotice, !showFlag);
  if (els.profileEditFlagComment) {
    els.profileEditFlagComment.textContent = comment;
  }
}

async function uploadProfilePhoto() {
  const file = els.profilePhotoInput.files?.[0];
  if (!file) {
    els.profilePhotoStatus.textContent = t("profile.photoChooseFirst");
    els.profilePhotoStatus.classList.add("error");
    return;
  }

  els.profilePhotoStatus.textContent = t("profile.photoUploading");
  els.profilePhotoStatus.classList.remove("error", "success");

  try {
    const profile = await api.uploadProfileAvatar(file);
    state.profile = profile;
    els.profilePhotoInput.value = "";
    renderProfile(profile);
    if (!els.profileEditModal?.hidden) {
      renderProfileEditAvatar(profile);
      renderProfileAvatarPresets(profile);
    }
    els.profilePhotoStatus.textContent = t("profile.photoUpdated");
    els.profilePhotoStatus.classList.add("success");
  } catch (error) {
    els.profilePhotoStatus.textContent = error.message;
    els.profilePhotoStatus.classList.add("error");
  }
}

async function uploadCompanyLogo() {
  const file = els.profileCompanyLogoInput.files?.[0];
  if (!file) {
    els.profileCompanyLogoStatus.textContent = t("profile.logoChoosePng");
    els.profileCompanyLogoStatus.classList.add("error");
    return;
  }

  if (file.type !== "image/png") {
    els.profileCompanyLogoStatus.textContent = t("profile.logoPngOnly");
    els.profileCompanyLogoStatus.classList.add("error");
    return;
  }

  els.profileCompanyLogoStatus.textContent = t("profile.logoUploading");
  els.profileCompanyLogoStatus.classList.remove("error", "success");

  try {
    const profile = await api.uploadCompanyLogo(file);
    els.profileCompanyLogoInput.value = "";
    renderProfile(profile);
    if (!els.profileEditModal?.hidden) {
      populateProfileEditForm(profile);
    }
    els.profileCompanyLogoStatus.textContent = t("profile.logoUpdated");
    els.profileCompanyLogoStatus.classList.add("success");
  } catch (error) {
    els.profileCompanyLogoStatus.textContent = error.message;
    els.profileCompanyLogoStatus.classList.add("error");
  }
}

async function uploadProfileBackground() {
  const file = els.profileBackgroundInput.files?.[0];
  if (!file) {
    els.profileBackgroundStatus.textContent = t("profile.bannerChooseFirst");
    els.profileBackgroundStatus.classList.add("error");
    return;
  }

  els.profileBackgroundStatus.textContent = t("profile.bannerUploading");
  els.profileBackgroundStatus.classList.remove("error", "success");

  try {
    const profile = await api.uploadProfileBackground(file);
    els.profileBackgroundInput.value = "";
    renderProfile(profile);
    if (!els.profileEditModal?.hidden) {
      populateProfileEditForm(profile);
    }
    els.profileBackgroundStatus.textContent = t("profile.bannerUpdated");
    els.profileBackgroundStatus.classList.add("success");
  } catch (error) {
    els.profileBackgroundStatus.textContent = error.message;
    els.profileBackgroundStatus.classList.add("error");
  }
}

async function removeProfileBackground() {
  els.profileBackgroundStatus.textContent = t("profile.bannerRemoving");
  els.profileBackgroundStatus.classList.remove("error", "success");

  try {
    const profile = await api.removeProfileBackground();
    renderProfile(profile);
    if (!els.profileEditModal?.hidden) {
      populateProfileEditForm(profile);
    }
    els.profileBackgroundStatus.textContent = t("profile.bannerRemoved");
    els.profileBackgroundStatus.classList.add("success");
  } catch (error) {
    els.profileBackgroundStatus.textContent = error.message;
    els.profileBackgroundStatus.classList.add("error");
  }
}

function renderProfileFriendPanel(profile) {
  if (!els.profileFriendPanel) {
    return;
  }

  const isOwner = Boolean(profile.isOwner);
  setHidden(els.profileFriendPanel, isOwner);
  els.profileFriendActionStatus.textContent = "";
  els.profileFriendActionStatus.classList.remove("error", "success");

  if (isOwner) {
    return;
  }

  const status = profile.friendshipStatus ?? "none";
  setHidden(els.profileAddFriendBtn, status !== "none");
  setHidden(els.profileAcceptFriendBtn, status !== "pending_incoming" || profile.isReporter);
  setHidden(els.profileMessageFriendBtn, status !== "accepted" || profile.isReporter);
  setHidden(els.profileRemoveFriendBtn, !["pending_outgoing", "pending_incoming", "accepted"].includes(status));

  switch (status) {
    case "accepted": {
      const obj = profile.pronounObject || "them";
      els.profileFriendStatus.textContent = profile.isReporter
        ? t("profile.friendReporter", { name: profile.username })
        : t("profile.friendAccepted", { name: profile.username, object: obj });
      els.profileRemoveFriendBtn.textContent = t("profile.removeFriend");
      break;
    }
    case "pending_outgoing":
      els.profileFriendStatus.textContent = t("profile.requestSent");
      els.profileRemoveFriendBtn.textContent = t("profile.cancelRequest");
      break;
    case "pending_incoming":
      els.profileFriendStatus.textContent = t("profile.requestIncoming", { name: profile.username });
      els.profileRemoveFriendBtn.textContent = t("profile.decline");
      break;
    default:
      els.profileFriendStatus.textContent = t("profile.notFriends");
      els.profileRemoveFriendBtn.textContent = t("profile.remove");
      break;
  }
}

async function openMessagesModal(toPlayerId = null) {
  els.financeModal.hidden = true;
  els.supplyModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  hideProfileScreens();
  els.dayModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = false;
  openModal(els.messagesModal);
  playerMessaging.openToPlayer(toPlayerId);
  setMessagesStatus(t("messages.loading"));
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
  name.addEventListener("click", () => openProfile(profileOpenKey(friend)));
  if (friend.isReporter) {
    name.textContent = `${friend.username} · ONN`;
  }

  const number = document.createElement("span");
  number.className = "friend-item-number";
  number.textContent = friend.profileNumber;

  head.append(name, number);

  const mood = document.createElement("div");
  mood.className = "friend-item-mood";
  mood.textContent = friend.mood || (friend.isReporter ? "ONN correspondent" : "Ready to mine.");

  item.append(head, mood);

  if (showAccept || showRemove || showMessage) {
    const actions = document.createElement("div");
    actions.className = "friend-item-actions";

    if (showMessage && !friend.isReporter) {
      const messageBtn = document.createElement("button");
      messageBtn.type = "button";
      messageBtn.className = "btn primary";
      messageBtn.textContent = t("friend.message");
      messageBtn.addEventListener("click", () => {
        openMessagesModal(friend.playerId).catch((error) => setFriendsStatus(error.message, true));
      });
      actions.appendChild(messageBtn);
    }

    if (showAccept) {
      const acceptBtn = document.createElement("button");
      acceptBtn.type = "button";
      acceptBtn.className = "btn success";
      acceptBtn.textContent = t("friend.accept");
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
        createFriendItem(friend, {
          showRemove: true,
          showMessage: true,
          removeLabel: t("profile.removeFriend"),
        }),
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
  hideProfileScreens();
  els.dayModal.hidden = true;
  els.messagesModal.hidden = true;
  els.friendsModal.hidden = false;
  openModal(els.friendsModal);
  setFriendsStatus(t("friends.loading"));
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
    setFriendsStatus(t("friends.invalidNumber"), true);
    return;
  }

  setFriendsStatus(t("friends.sending"));
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
    els.profileAddFriendStatus.textContent = t("friends.invalidNumber");
    els.profileAddFriendStatus.classList.add("error");
    return;
  }

  els.profileAddFriendStatus.textContent = t("friends.sending");
  els.profileAddFriendStatus.classList.remove("error", "success");
  try {
    const result = await api.addFriend(value);
    els.profileAddFriendNumber.value = "";
    els.profileAddFriendStatus.textContent = result.message;
    els.profileAddFriendStatus.classList.add("success");
    if (!els.friendsModal.hidden) {
      await loadFriends();
    }
    await refreshProfileIfOpen();
  } catch (error) {
    els.profileAddFriendStatus.textContent = error.message;
    els.profileAddFriendStatus.classList.add("error");
  }
}

async function refreshProfileIfOpen() {
  const profileClosed = !els.profileModal || els.profileModal.hidden;
  const editClosed = !els.profileEditModal || els.profileEditModal.hidden;
  if ((profileClosed && editClosed) || !state.profile) {
    return;
  }

  try {
    const profile = state.profile.isOwner
      ? await api.getProfile()
      : await api.getProfileByUsername(state.profile.username);
    renderProfile(profile);
  } catch (error) {
    showStatus(error.message, true);
  }
}

async function acceptFriendRequest(friendshipId) {
  const result = await api.acceptFriend(friendshipId);
  setFriendsStatus(result.message, false);
  await loadFriends();
  await refreshProfileIfOpen();
}

async function removeFriendRequest(friendshipId) {
  const result = await api.removeFriend(friendshipId);
  setFriendsStatus(result.message, false);
  await loadFriends();
  await refreshProfileIfOpen();
}

async function profileAddFriend() {
  const profile = state.profile;
  if (!profile?.profileNumber) {
    return;
  }

  els.profileFriendActionStatus.textContent = t("friend.sendingRequest");
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

  els.profileFriendActionStatus.textContent = t("friend.accepting");
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

  els.profileFriendActionStatus.textContent = t("friend.updating");
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

function openProfileEdit() {
  const profile = state.profile;
  if (!profile?.isOwner || !els.profileEditModal) {
    return;
  }

  populateProfileEditForm(profile);
  syncProfileGenderPronounsUi();
  renderProfileEditAvatar(profile);
  renderProfileFlagNotice(profile);
  applyProfileTheme();

  setHidden(els.profileModal, true);
  setHidden(els.profileEditModal, false);
  openModal(els.profileEditModal);
}

async function closeProfileEdit(refreshProfile = true) {
  setHidden(els.profileEditModal, true);
  setHidden(els.profileModal, false);
  openModal(els.profileModal);

  if (!refreshProfile || !state.profile?.isOwner) {
    return;
  }

  try {
    const profile = await api.getProfile();
    renderProfile(profile);
  } catch (error) {
    showStatus(error.message, true);
  }
}

async function openProfile(username) {
  els.financeModal.hidden = true;
  els.supplyModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.dayModal.hidden = true;
  setHidden(els.profileEditModal, true);
  setHidden(els.profileModal, false);
  openModal(els.profileModal);

  try {
    const profile = username
      ? await api.getProfileByUsername(username)
      : await api.getProfile();
    renderProfile(profile);
  } catch (error) {
    hideProfileScreens();
    showStatus(error.message, true);
  }
}

async function saveProfile() {
  els.profileSaveStatus.textContent = t("profile.saving");
  els.profileSaveStatus.classList.remove("error", "success");

  const payload = profileEditFormPayload();
  if (
    profileGenderRequiresPronouns(payload.profileGender) &&
    !payload.profilePreferredPronouns
  ) {
    els.profileSaveStatus.textContent = t("profile.savePronounsRequired");
    els.profileSaveStatus.classList.add("error");
    syncProfileGenderPronounsUi();
    els.profilePreferredPronounsInput?.focus();
    return;
  }

  const nextLocale = els.profileLocaleInput?.value?.trim() ?? payload.profileLocale;
  if (!nextLocale) {
    els.profileSaveStatus.textContent = t("profile.completion.localeRequired");
    els.profileSaveStatus.classList.add("error");
    els.profileLocaleInput?.focus();
    return;
  }

  try {
    const profile = await api.updateProfile({
      ...payload,
      mood: els.profileMoodInput.value.trim(),
      aboutMe: els.profileAboutInput.value,
      music: els.profileMusicInput.value.trim(),
      interests: els.profileInterestsInput.value,
      discord: els.profileDiscordInput.value.trim(),
      bluesky: els.profileBlueskyInput.value.trim(),
      twitter: els.profileTwitterInput.value.trim(),
      youtube: els.profileYoutubeInput.value.trim(),
      facebook: els.profileFacebookInput.value.trim(),
      profileAvatarPreset: state.profile?.profileAvatarPreset ?? "neutral",
      profileLocale: nextLocale,
    });
    state.profile = profile;
    if (getLocale() !== nextLocale) {
      await setLocale(nextLocale);
      applyTranslations(document);
    }
    renderProfile(profile);
    renderProfileEditAvatar(profile);
    els.profileSaveStatus.textContent = t("profile.saved");
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

  try {
    const profile = await api.getProfile();
    state.profile = profile;
    await maybeShowProfileCompletion(profile);
  } catch {
    // Profile fetch is optional during refresh; completion modal runs on next successful load.
  }
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
      throw new Error(t("auth.sessionIncomplete"));
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
    els.utcClock.textContent = t("game.utcEmpty");
    return;
  }

  const utcDate = mine.utcDate ?? new Date().toISOString().slice(0, 10);
  const countdown = formatUtcCountdown(state.nextDayAtUtc ?? mine.nextDayAtUtc);
  els.utcClock.textContent = t("game.utcNextDay", { date: utcDate, countdown });
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

  els.playerName.textContent = api.username ?? t("game.commander");
  setRaxHtml(els.credits, mine.credits ?? 0);
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
    els.zoneInfo.textContent = t("game.zoneSelectHint");
    return;
  }

  const meta = oreMeta(zone.oreType);
  els.zoneInfo.innerHTML = [
    `<strong>Zone (${zone.x}, ${zone.y})</strong>`,
    `Ore: ${meta.displayName}`,
    `Richness: ${Number(zone.richness).toFixed(2)}`,
    `Depleted: ${Number(zone.depletedPct).toFixed(0)}%`,
    zone.isSalvageZone ? t("game.salvageZone") : "",
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
  showStatus(t("game.updatingWorker"));
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
    formatRaxLabelLine("Balance", finances.credits),
    formatRaxLabelLine(t("finance.payroll"), finances.dailyPayroll),
    formatRaxLabelLine(t("finance.supplyCost"), finances.dailySupplyCost),
    formatRaxLabelLine(t("finance.estIncome"), finances.estimatedDailyIncome),
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
    line.append(`Day ${tx.gameDay}: `);
    const amountWrap = document.createElement("span");
    amountWrap.innerHTML = `${sign}${formatRaxHtml(tx.amount)}`;
    line.append(amountWrap, ` — ${tx.description ?? ""}`);
    els.financeTransactions.appendChild(line);
  }
}

function formatMarketSource(source) {
  switch (source) {
    case "yahoo-us":
      return t("market.earthStocks");
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
    parts.push(`+${Number(bonuses.saleBonusPercent)}% sale ${RAX_NAME.toLowerCase()}`);
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
    : t("market.loading");

  els.storeSupplyList.innerHTML = "";
  for (const [type, meta] of Object.entries(tradeSupplyTypes)) {
    const priceEntry = market?.prices?.find((p) => p.supplyType === type);
    const price = priceEntry ? Number(priceEntry.price) : meta.basePrice;
    const stock = inventoryQty(state.mine, type);
    const button = document.createElement("button");
    button.type = "button";
    button.className = "shop-btn";
    button.style.borderColor = meta.color;
    button.innerHTML = `<strong>Buy ${meta.displayName}</strong><span>${formatRaxHtml(price)} · Stock: ${stock.toFixed(0)}</span>`;
    button.addEventListener("click", () => buySupply(type));
    els.storeSupplyList.appendChild(button);
  }

  if (els.storeCompanyNameStatus) {
    els.storeCompanyNameStatus.textContent = "";
  }
  if (els.storeCompanyNameList) {
    loadStoreCompanyNames().catch((error) => {
      if (els.storeCompanyNameStatus) {
        els.storeCompanyNameStatus.textContent = error.message;
      }
    });
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
    button.innerHTML = `<strong>Ship ${meta.displayName}</strong><span>${formatRaxHtml(salePrice)}/u · Qty: ${stock.toFixed(1)}</span>`;
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
    : t("market.loading");

  els.supplyList.innerHTML = "";
  for (const [type, meta] of Object.entries(tradeSupplyTypes)) {
    const priceEntry = market?.prices?.find((p) => p.supplyType === type);
    const price = priceEntry ? Number(priceEntry.price) : meta.basePrice;
    const stock = inventoryQty(mine, type);
    const button = document.createElement("button");
    button.type = "button";
    button.className = "shop-btn";
    button.style.borderColor = meta.color;
    button.innerHTML = `<strong>${meta.displayName}</strong><span>${formatRaxHtml(price)} · Stock: ${stock.toFixed(0)}</span>`;
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
    button.innerHTML = `<strong>Sell ${meta.displayName}</strong><span>${formatRaxHtml(salePrice)}/u · Qty: ${stock.toFixed(1)}</span>`;
    button.addEventListener("click", () => sellOre(type, stock));
    els.oreList.appendChild(button);
  }

  refreshTradeAuctions().catch((error) => setAuctionStatus(error.message, true));
}

function setAuctionStatus(message, isError = false) {
  if (!els.auctionStatus) {
    return;
  }

  els.auctionStatus.textContent = message ?? "";
  els.auctionStatus.classList.toggle("error", Boolean(isError && message));
  els.auctionStatus.classList.toggle("success", Boolean(!isError && message));
}

function formatAuctionTime(seconds) {
  if (seconds == null || Number.isNaN(Number(seconds))) {
    return t("auction.waitingFirstBid");
  }

  const total = Math.max(0, Number(seconds));
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const secs = total % 60;
  if (hours > 0) {
    return `${hours}h ${minutes}m ${secs}s`;
  }
  if (minutes > 0) {
    return `${minutes}m ${secs}s`;
  }
  return `${secs}s`;
}

function renderTradeMarketValue() {
  if (!els.tradeMarketValue) {
    return;
  }

  els.tradeMarketValue.innerHTML =
    `Trade Market value: ${formatRaxHtml(state.tradeMarketValue)} · Auction fee: ${state.auctionFeePercent}% of sale`;
}

function populateAuctionItemSelect() {
  if (!els.auctionItem) {
    return;
  }

  const mine = state.mine;
  const options = ['<option value="">Select item…</option>'];
  for (const item of mine?.inventory ?? []) {
    const quantity = Number(item.quantity ?? 0);
    if (quantity <= 0) {
      continue;
    }

    const category = item.category;
    const itemType = item.itemType;
    const meta = category === "Ore" ? tradeOreTypes[itemType] : tradeSupplyTypes[itemType];
    if (!meta) {
      continue;
    }

    options.push(
      `<option value="${category}|${itemType}">${meta.displayName} (${quantity.toFixed(1)} in stock)</option>`,
    );
  }

  els.auctionItem.innerHTML = options.join("");
}

function renderAuctionList() {
  if (!els.auctionList) {
    return;
  }

  const auctions = state.tradeAuctions ?? [];
  if (!auctions.length) {
    els.auctionList.innerHTML = `<p class='market-info'>${t("auction.noLive")}</p>`;
    return;
  }

  els.auctionList.innerHTML = "";
  for (const auction of auctions) {
    const card = document.createElement("article");
    card.className = "auction-card";

    const currentBid =
      auction.currentBid != null ? formatRaxHtml(auction.currentBid) : t("auction.noBids");
    const timerText = auction.status === "active"
      ? `Ends in ${formatAuctionTime(auction.secondsRemaining)}`
      : `Runs ${auction.durationMinutes} min after first bid`;

    card.innerHTML = `
      <div class="auction-card-head">
        <strong>${auction.displayName} × ${Number(auction.quantity).toFixed(1)}</strong>
        <span class="market-info">${auction.status}</span>
      </div>
      <p class="auction-card-meta">
        Seller: ${auction.sellerUsername} · Start ${formatRaxHtml(auction.startPrice)} · Current ${currentBid}
        ${auction.highBidderUsername ? ` · High bidder: ${auction.highBidderUsername}` : ""}
        · ${timerText}
      </p>`;

    if (auction.isMine && auction.status === "open") {
      const cancelBtn = document.createElement("button");
      cancelBtn.type = "button";
      cancelBtn.className = "btn ghost";
      cancelBtn.textContent = t("auction.cancelListing");
      cancelBtn.addEventListener("click", () => {
        cancelAuctionListing(auction.id).catch((error) => setAuctionStatus(error.message, true));
      });
      card.appendChild(cancelBtn);
    } else if (!auction.isMine) {
      const bidRow = document.createElement("div");
      bidRow.className = "auction-bid-row";
      const bidInput = document.createElement("input");
      bidInput.type = "number";
      bidInput.min = String(auction.minimumNextBid);
      bidInput.step = "1";
      bidInput.value = String(Math.ceil(Number(auction.minimumNextBid)));
      bidInput.setAttribute("aria-label", `Bid on ${auction.displayName}`);
      const bidBtn = document.createElement("button");
      bidBtn.type = "button";
      bidBtn.className = "btn primary";
      bidBtn.textContent = t("auction.placeBid");
      bidBtn.addEventListener("click", () => {
        placeAuctionBid(auction.id, bidInput.value).catch((error) => setAuctionStatus(error.message, true));
      });
      bidRow.appendChild(bidInput);
      bidRow.appendChild(bidBtn);
      card.appendChild(bidRow);
    }

    els.auctionList.appendChild(card);
  }
}

async function refreshTradeAuctions() {
  const data = await api.getTradeAuctions();
  state.tradeAuctions = data.auctions ?? [];
  state.tradeMarketValue = Number(data.tradeMarketValue ?? 0);
  state.auctionFeePercent = Number(data.auctionFeePercent ?? 5);
  renderTradeMarketValue();
  populateAuctionItemSelect();
  renderAuctionList();
}

async function createAuctionFromForm(event) {
  event.preventDefault();
  const selection = els.auctionItem.value;
  if (!selection) {
    setAuctionStatus(t("auction.selectItem"), true);
    return;
  }

  const [category, itemType] = selection.split("|");
  const quantity = Number(els.auctionQuantity.value);
  const startPrice = Number(els.auctionStartPrice.value);
  const durationMinutes = Number(els.auctionDuration.value);

  els.auctionCreateBtn.disabled = true;
  setAuctionStatus(t("auction.listing"));
  try {
    const result = await api.createTradeAuction(category, itemType, quantity, startPrice, durationMinutes);
    setAuctionStatus(result.message ?? "Auction listed.", false);
    els.auctionCreateForm.reset();
    await refreshAll();
    await refreshTradeAuctions();
  } catch (error) {
    setAuctionStatus(error.message, true);
  } finally {
    els.auctionCreateBtn.disabled = false;
  }
}

async function placeAuctionBid(auctionId, bidAmount) {
  const amount = Number(bidAmount);
  if (!Number.isFinite(amount) || amount <= 0) {
    setAuctionStatus(t("auction.bidInvalid"), true);
    return;
  }

  setAuctionStatus(t("auction.bidding"));
  try {
    const result = await api.placeTradeAuctionBid(auctionId, amount);
    setAuctionStatus(result.message ?? "Bid placed.", false);
    if (result.newCredits != null && state.mine) {
      state.mine.credits = result.newCredits;
      setRaxHtml(els.credits, result.newCredits);
    }
    await refreshTradeAuctions();
  } catch (error) {
    setAuctionStatus(error.message, true);
  }
}

async function cancelAuctionListing(auctionId) {
  setAuctionStatus(t("auction.cancelling"));
  try {
    const result = await api.cancelTradeAuction(auctionId);
    setAuctionStatus(result.message ?? "Auction cancelled.", false);
    await refreshAll();
    await refreshTradeAuctions();
  } catch (error) {
    setAuctionStatus(error.message, true);
  }
}

function showDayReport(result) {
  els.dayReportTitle.textContent = t("game.dayReportTitle", { day: result.newGameDay });
  const lines = [...(result.messages ?? [])];
  if (result.oreExtracted?.length) {
    const extracted = result.oreExtracted
      .map((o) => `${Number(o.quantity).toFixed(1)} ${o.oreType}`)
      .join(", ");
    lines.push("", `Extracted: ${extracted}`);
  }
  els.dayReportBody.textContent = lines.join("\n") || t("game.dayComplete");
  openModal(els.dayModal);
}

function formatEventRewardLabel(reward) {
  const amount = Number(reward.amount ?? 0);
  const itemType = reward.itemType ?? "";
  if ((reward.category ?? itemType).toLowerCase() === "credits") {
    return formatRaxPlain(amount);
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
    return formatRaxPlain(amount);
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
  els.eventModalTitle.textContent = item.title || t("event.title");
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
  els.eventModalRewardsHeading.textContent = isWin ? t("event.youWon") : t("event.possibleRewards");
  const rewards = item.rewards ?? [];
  els.eventModalRewards.innerHTML = rewards.length
    ? rewards.map((reward) => `<li>${isWin ? formatEventRewardLabel(reward) : formatAnnouncementReward(reward)}</li>`).join("")
    : `<li>${isWin ? t("event.rewardsWon") : t("event.rewardsChallenge")}</li>`;
  els.eventModalClose.textContent = isWin ? t("event.claimPrize") : t("event.gotIt");
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
    const message = t("auth.usernamePasswordRequired");
    if (isRegister) {
      notifyRegisterResult(message, "error");
    } else {
      showLoginStatus(message, "error");
    }
    return;
  }

  if (isRegister && !email) {
    notifyRegisterResult(t("auth.emailRequiredSignup"), "error");
    return;
  }

  if (isRegister && !isValidEmail(email)) {
    notifyRegisterResult(t("auth.emailInvalid"), "error");
    return;
  }

  if (isRegister && !birthday) {
    notifyRegisterResult(t("auth.birthdayRequiredSignup"), "error");
    return;
  }

  if (isRegister && password.length < 8) {
    notifyRegisterResult(t("auth.passwordMinLength"), "error");
    return;
  }

  const profileGender = els.registerGenderInput?.value?.trim() ?? "";
  const profilePreferredPronouns = els.registerPronounsInput?.value?.trim() ?? "";
  const profileLocale = els.registerLocaleSelect?.value?.trim() ?? getLocale();
  if (isRegister && !profileLocale) {
    notifyRegisterResult(t("auth.languageRequiredSignup"), "error");
    els.registerLocaleSelect?.focus();
    return;
  }

  if (isRegister && !profileGender) {
    notifyRegisterResult(t("auth.genderRequiredSignup"), "error");
    els.registerGenderInput?.focus();
    return;
  }

  if (isRegister && profileGenderRequiresPronouns(profileGender) && !profilePreferredPronouns) {
    notifyRegisterResult(t("auth.pronounsRequiredSignup"), "error");
    els.registerPronounsInput?.focus();
    return;
  }

  showLoginStatus(isRegister ? t("auth.creatingAccount") : t("auth.connecting"), "info");
  try {
    if (isRegister) {
      await api.register(username, email, password, birthday, profileGender, profilePreferredPronouns, profileLocale);
      await setLocale(profileLocale);
      applyTranslations(document);
      els.password.value = "";
      els.email.value = "";
      resetBirthdayDropdowns();
      resetRegisterProfileFields();
      const message = t("auth.accountCreated");
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
    showLoginStatus(t("auth.usernamePasswordRequired"), "error");
    return;
  }

  if (!message) {
    showLoginStatus(t("auth.appealMessageRequired"), "error");
    return;
  }

  showLoginStatus(t("auth.sendingAppeal"), "info");
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
    showLoginStatus(t("auth.emailRequiredReset"), "error");
    return;
  }

  if (!isValidEmail(email)) {
    showLoginStatus(t("auth.emailInvalid"), "error");
    return;
  }

  showLoginStatus(t("auth.sendingReset"));
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
    showLoginStatus(t("auth.resetRequired"), "error");
    return;
  }

  if (newPassword.length < 8) {
    showLoginStatus(t("auth.passwordMinLength"), "error");
    return;
  }

  if (newPassword !== confirmPassword) {
    showLoginStatus(t("auth.passwordsMismatch"), "error");
    return;
  }

  showLoginStatus(t("auth.updatingPassword"));
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
els.registerGenderInput?.addEventListener("change", syncRegisterGenderUi);
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
  hideProfileScreens();
});
els.profileCustomizeBtn?.addEventListener("click", () => {
  openProfileEdit();
});
els.profileEditBackBtn?.addEventListener("click", () => {
  closeProfileEdit().catch((error) => showStatus(error.message, true));
});
els.profileSaveBtn?.addEventListener("click", () => {
  saveProfile().catch((error) => {
    els.profileSaveStatus.textContent = error.message;
    els.profileSaveStatus.classList.add("error");
  });
});
els.profileGenderInput?.addEventListener("change", syncProfileGenderPronounsUi);
els.profileCompletionGenderInput?.addEventListener("change", syncProfileCompletionGenderUi);
els.profileCompletionSaveBtn?.addEventListener("click", () => {
  saveProfileCompletion().catch((error) => {
    if (els.profileCompletionStatus) {
      els.profileCompletionStatus.textContent = error.message;
      els.profileCompletionStatus.classList.add("error");
    }
  });
});
els.profileCompanySaveBtn?.addEventListener("click", () => {
  saveCompanyName().catch((error) => {
    els.profileCompanyStatus.textContent = error.message;
    els.profileCompanyStatus.classList.add("error");
  });
});
els.profileCompanyRegenerateBtn?.addEventListener("click", () => {
  regenerateCompanyName().catch((error) => {
    els.profileCompanyStatus.textContent = error.message;
    els.profileCompanyStatus.classList.add("error");
  });
});
els.profileCompanyListBtn?.addEventListener("click", () => {
  listCompanyNameForSale().catch((error) => {
    els.profileCompanyListStatus.textContent = error.message;
    els.profileCompanyListStatus.classList.add("error");
  });
});
els.profileCompanyCancelListBtn?.addEventListener("click", () => {
  cancelCompanyNameListing().catch((error) => {
    els.profileCompanyListStatus.textContent = error.message;
    els.profileCompanyListStatus.classList.add("error");
  });
});
els.profilePhotoChooseBtn.addEventListener("click", () => {
  els.profilePhotoInput.click();
});
els.profilePhotoInput.addEventListener("change", () => {
  const file = els.profilePhotoInput.files?.[0];
  if (!file) {
    els.profilePhotoBtn.disabled = true;
    els.profilePhotoChooseBtn.textContent = t("profile.edit.choosePhoto");
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
els.profileCompanyLogoChooseBtn?.addEventListener("click", () => {
  els.profileCompanyLogoInput.click();
});
els.profileCompanyLogoInput?.addEventListener("change", () => {
  const file = els.profileCompanyLogoInput.files?.[0];
  if (!file) {
    els.profileCompanyLogoUploadBtn.disabled = true;
    els.profileCompanyLogoChooseBtn.textContent = t("profile.edit.choosePng");
    renderCompanyLogoOnElement(state.profile, els.profileCompanyLogoPreview);
    return;
  }

  if (file.type !== "image/png") {
    els.profileCompanyLogoUploadBtn.disabled = true;
    els.profileCompanyLogoStatus.textContent = t("profile.logoPngOnly");
    els.profileCompanyLogoStatus.classList.add("error");
    return;
  }

  els.profileCompanyLogoUploadBtn.disabled = false;
  els.profileCompanyLogoChooseBtn.textContent = file.name.length > 14
    ? `${file.name.slice(0, 11)}…`
    : file.name;

  const previewUrl = URL.createObjectURL(file);
  if (els.profileCompanyLogoPreview) {
    els.profileCompanyLogoPreview.src = previewUrl;
    els.profileCompanyLogoPreview.hidden = false;
  }
});
els.profileCompanyLogoUploadBtn?.addEventListener("click", () => {
  uploadCompanyLogo().catch((error) => {
    els.profileCompanyLogoStatus.textContent = error.message;
    els.profileCompanyLogoStatus.classList.add("error");
  });
});
els.profileCompanyLogoGenerateBtn?.addEventListener("click", () => {
  enqueueCompanyLogoGeneration().catch((error) => {
    if (els.profileCompanyLogoStatus) {
      els.profileCompanyLogoStatus.textContent = error.message;
      els.profileCompanyLogoStatus.classList.add("error");
    }
  });
});
els.profileBackgroundChooseBtn?.addEventListener("click", () => {
  els.profileBackgroundInput.click();
});
els.profileBackgroundInput?.addEventListener("change", () => {
  const file = els.profileBackgroundInput.files?.[0];
  if (!file) {
    els.profileBackgroundUploadBtn.disabled = true;
    els.profileBackgroundChooseBtn.textContent = t("profile.edit.chooseImage");
    applyProfileBannerBackground(els.profileBackgroundPreview, state.profile?.profileBackgroundUrl);
    return;
  }

  els.profileBackgroundUploadBtn.disabled = false;
  els.profileBackgroundChooseBtn.textContent = file.name.length > 14
    ? `${file.name.slice(0, 11)}…`
    : file.name;

  const previewUrl = URL.createObjectURL(file);
  els.profileBackgroundPreview.style.backgroundImage = `url("${previewUrl}")`;
  els.profileBackgroundPreview.classList.add("has-custom-background");
});
els.profileBackgroundUploadBtn?.addEventListener("click", () => {
  uploadProfileBackground().catch((error) => {
    els.profileBackgroundStatus.textContent = error.message;
    els.profileBackgroundStatus.classList.add("error");
  });
});
els.profileBackgroundRemoveBtn?.addEventListener("click", () => {
  removeProfileBackground().catch((error) => {
    els.profileBackgroundStatus.textContent = error.message;
    els.profileBackgroundStatus.classList.add("error");
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
  hideProfileScreens();
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  openModal(els.financeModal);
  renderFinancePanel();
});
els.tradeMarketBtn.addEventListener("click", () => {
  els.financeModal.hidden = true;
  hideProfileScreens();
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  openModal(els.supplyModal);
  renderSupplyPanel();
});
els.auctionCreateForm?.addEventListener("submit", (event) => {
  createAuctionFromForm(event).catch((error) => setAuctionStatus(error.message, true));
});
els.storeBtn.addEventListener("click", () => {
  els.financeModal.hidden = true;
  hideProfileScreens();
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.supplyModal.hidden = true;
  els.shippingModal.hidden = true;
  openModal(els.storeModal);
  renderStorePanel();
});
els.shippingBtn.addEventListener("click", () => {
  els.financeModal.hidden = true;
  hideProfileScreens();
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.supplyModal.hidden = true;
  els.storeModal.hidden = true;
  openModal(els.shippingModal);
  renderShippingPanel();
});
els.exonetBtn.addEventListener("click", () => {
  els.financeModal.hidden = true;
  els.supplyModal.hidden = true;
  els.dayModal.hidden = true;
  hideProfileScreens();
  els.friendsModal.hidden = true;
  els.messagesModal.hidden = true;
  els.shippingModal.hidden = true;
  els.storeModal.hidden = true;
  els.eventModal.hidden = true;
  document.body.appendChild(els.exonetModal);
  exonet.open("home");
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

async function startApp() {
  await initI18n({ namespaces: ["game"] });
  applyTranslations(document);
  wireLocaleSelectors();

  document.addEventListener("rava:localechange", () => {
    applyTranslations(document);
    setAuthMode(state.authMode);
    if (state.profile) {
      renderProfile(state.profile);
      if (!els.profileEditModal?.hidden) {
        populateProfileEditForm(state.profile);
        renderProfileAvatarPresets(state.profile);
      }
    }
    if (state.mine && !els.supplyModal.hidden) {
      renderSupplyPanel();
    }
    if (state.mine && !els.storeModal.hidden) {
      renderStorePanel();
    }
  });

  setAuthMode("login");
  initBirthdayDropdowns();
  initGameVersionTag();
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
}

startApp().catch((error) => {
  console.error("[rava] startup failed", error);
});
