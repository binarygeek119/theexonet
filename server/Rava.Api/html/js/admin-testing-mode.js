export const TESTING_MODE_STORAGE_KEY = "rava.admin.testingMode";
export const REMOVED_DUMMY_FRIENDS_KEY = "rava.admin.testingMode.removedFriends";
export const DUMMY_PLAYER_ID_PREFIX = "aaaaaaaa-aaaa-4aaa-8aaa-";
export const DUMMY_FRIENDSHIP_ID_PREFIX = "bbbbbbbb-bbbb-4bbb-8bbb-";

const DUMMY_COUNT = 12;

let cachedTestingModeEnabled = false;

const USERNAMES = [
  "vein_runner",
  "ore_hauler_7",
  "void_shift",
  "ridge_claim",
  "nova_digger",
  "basalt_jax",
  "titan_bore",
  "lunar_haul",
  "cobalt_sable",
  "fault_line",
  "pulse_ore",
  "zenith_claim",
];

const MOODS = [
  "Drilling through the quiet shift.",
  "Ore prices up, morale questionable.",
  "Living on freeze-dried coffee.",
  "Just hit a rich ferroxite pocket.",
  "Union meeting at 1800 ship time.",
  "Cataloging asteroid samples.",
];

const ABOUT_SNIPPETS = [
  "Third-gen belt miner. I run tight crews and tighter ledgers.",
  "Ex-corporate geologist gone independent. Ask me about vein mapping.",
  "Shipping surplus ore when the market hiccups. DMs open for convoy runs.",
  "Building a small syndicate one claim at a time.",
];

const INTERESTS = [
  "Asteroid geology, vintage exosuits, belt hockey",
  "Market arbitrage, drone mods, synthwave",
  "Crew management, hazard pay debates, noodle bars",
  "Deep-core surveys, poker, ONN gossip",
];

const MUSIC = [
  "Dust Choir — Beltline Echoes",
  "Static Haul — Night Shift 9",
  "Ferroxite Sons — Core Sample",
  "Orbital Lull — Slow Burn",
];

const MINE_PREFIXES = ["Orion", "Stellar", "Nova", "Apex", "Deep", "Void", "Iron", "Quartz"];
const MINE_CORES = ["Vein", "Dig", "Drill", "Ore", "Claim", "Shaft", "Forge", "Bore"];
const MINE_SUFFIXES = ["Co.", "Corp", "Works", "Syndicate", "Excavation", "Holdings"];

const THEMES = ["classic", "ember", "frost", "neon", "slate"];

const TESTING_DUMMY_ASSETS_ROOT = "/exonet/testing-dummy-friends";

/**
 * @param {number} index
 * @param {"avatar.jpg"|"background.jpg"|"logo.png"} filename
 */
export function dummyAssetUrl(index, filename) {
  return `${TESTING_DUMMY_ASSETS_ROOT}/${String(index).padStart(2, "0")}/${filename}`;
}

/**
 * @param {object} profile
 * @param {number} index
 */
function applyDummyProfileAssets(profile, index) {
  return {
    ...profile,
    profileImageUrl: dummyAssetUrl(index, "avatar.jpg"),
    profileBackgroundUrl: dummyAssetUrl(index, "background.jpg"),
    companyLogoUrl: dummyAssetUrl(index, "logo.png"),
    hasCustomProfilePhoto: true,
  };
}

/**
 * @param {string} seed
 */
function hashSeed(seed) {
  let hash = 0;
  for (let i = 0; i < seed.length; i += 1) {
    hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
  }
  return hash;
}

/**
 * @param {string} seed
 * @param {number} maxExclusive
 */
function pick(seed, maxExclusive) {
  return hashSeed(seed) % maxExclusive;
}

/**
 * @param {number} index
 */
export function dummyPlayerId(index) {
  return `${DUMMY_PLAYER_ID_PREFIX}${String(index).padStart(12, "0")}`;
}

export function isDummyPlayerId(playerId) {
  return String(playerId ?? "").startsWith(DUMMY_PLAYER_ID_PREFIX);
}

/**
 * @param {number} index
 */
export function dummyFriendshipId(index) {
  return `${DUMMY_FRIENDSHIP_ID_PREFIX}${String(index).padStart(12, "0")}`;
}

export function isDummyFriendshipId(friendshipId) {
  return String(friendshipId ?? "").startsWith(DUMMY_FRIENDSHIP_ID_PREFIX);
}

export function shouldApplyTestingFriends(testingMode, isStaffAdmin) {
  return Boolean(testingMode && isStaffAdmin);
}

export function loadRemovedDummyFriendships() {
  try {
    const raw = sessionStorage.getItem(REMOVED_DUMMY_FRIENDS_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return new Set(Array.isArray(parsed) ? parsed : []);
  } catch {
    return new Set();
  }
}

/**
 * @param {string} friendshipId
 */
export function saveRemovedDummyFriendship(friendshipId) {
  const removed = loadRemovedDummyFriendships();
  removed.add(String(friendshipId));
  try {
    sessionStorage.setItem(REMOVED_DUMMY_FRIENDS_KEY, JSON.stringify([...removed]));
  } catch {
    /* ignore */
  }
}

export function clearRemovedDummyFriendships() {
  try {
    sessionStorage.removeItem(REMOVED_DUMMY_FRIENDS_KEY);
  } catch {
    /* ignore */
  }
}

function dummyProfileNumber(index) {
  return String(100_000 + index * 7919).slice(0, 6);
}

/**
 * @param {string} username
 */
export function getDummyIndexByUsername(username) {
  const normalized = String(username ?? "").trim().toLowerCase();
  const index = USERNAMES.findIndex((entry) => entry.toLowerCase() === normalized);
  return index >= 0 ? index : -1;
}

/**
 * @param {string} profileNumber
 */
export function getDummyIndexByProfileNumber(profileNumber) {
  const normalized = String(profileNumber ?? "").trim();
  for (let index = 0; index < DUMMY_COUNT; index += 1) {
    if (dummyProfileNumber(index) === normalized) {
      return index;
    }
  }
  return -1;
}

/**
 * @param {number} index
 */
function buildDummyProfileFriend(index) {
  const summary = buildDummyPlayerSummary(index);
  const seed = `dummy-profile-${index}`;
  return {
    playerId: summary.id,
    username: summary.username,
    profileNumber: dummyProfileNumber(index),
    mood: MOODS[pick(`${seed}-mood`, MOODS.length)],
    publicStatus: "",
    isReporter: false,
    reporterSlug: "",
    isTestingDummy: true,
  };
}

/**
 * @param {object} adminProfile
 */
function buildAdminProfileFriend(adminProfile) {
  return {
    playerId: adminProfile.playerId,
    username: adminProfile.username,
    profileNumber: adminProfile.profileNumber ?? "",
    mood: adminProfile.mood ?? "",
    publicStatus: "",
    isReporter: false,
    reporterSlug: "",
  };
}

/**
 * @param {number} index
 */
function buildDummyFriendSummary(index) {
  const summary = buildDummyPlayerSummary(index);
  const seed = `dummy-profile-${index}`;
  const daysFriends = 1 + pick(`${seed}-friends`, 90);

  return {
    friendshipId: dummyFriendshipId(index),
    playerId: summary.id,
    username: summary.username,
    profileNumber: dummyProfileNumber(index),
    mood: MOODS[pick(`${seed}-mood`, MOODS.length)],
    status: "accepted",
    since: new Date(Date.now() - daysFriends * 86_400_000).toISOString(),
    isReporter: false,
    reporterSlug: "",
    isTestingDummy: true,
  };
}

export function getDummyFriendSummaries() {
  const removed = loadRemovedDummyFriendships();
  return Array.from({ length: DUMMY_COUNT }, (_, index) => buildDummyFriendSummary(index)).filter(
    (friend) => !removed.has(friend.friendshipId),
  );
}

/**
 * @param {object} adminProfile
 * @param {number} index
 */
export function buildDummyGameProfile(index, adminProfile) {
  const summary = buildDummyPlayerSummary(index);
  const seed = `dummy-profile-${index}`;
  const workerCount = 2 + pick(`${seed}-workers`, 48);
  const zoneCount = 1 + pick(`${seed}-zones`, 12);
  const gameDay = 1 + pick(`${seed}-day`, 120);
  const profileNum = dummyProfileNumber(index);

  const month = 1 + pick(`${seed}-bmonth`, 12);
  const day = 1 + pick(`${seed}-bday`, 28);
  const birthYear = 1988 + pick(`${seed}-byear`, 20);

  const friends = adminProfile ? [buildAdminProfileFriend(adminProfile)] : [];
  const friendshipId = dummyFriendshipId(index);
  const removed = loadRemovedDummyFriendships();
  const isFriend = !removed.has(friendshipId);

  return applyDummyProfileAssets({
    playerId: summary.id,
    username: summary.username,
    profileNumber: profileNum,
    profileImageUrl: "",
    profileBackgroundUrl: "",
    companyLogoUrl: "",
    mood: MOODS[pick(`${seed}-mood`, MOODS.length)],
    aboutMe: ABOUT_SNIPPETS[pick(`${seed}-about`, ABOUT_SNIPPETS.length)],
    music: MUSIC[pick(`${seed}-music`, MUSIC.length)],
    interests: INTERESTS[pick(`${seed}-interests`, INTERESTS.length)],
    discord: index % 3 === 0 ? summary.username : "",
    bluesky: index % 4 === 0 ? `${summary.username}.bsky` : "",
    twitter: "",
    youtube: index % 5 === 0 ? `@${summary.username}` : "",
    facebook: "",
    memberSince: summary.createdAt,
    currentGameDay: gameDay,
    credits: summary.credits,
    mineName: miningCompanyName(seed),
    workerCount,
    zoneCount,
    isOwner: false,
    friendshipStatus: isFriend ? "accepted" : "none",
    friendshipId: isFriend ? friendshipId : "",
    friends: isFriend ? friends : [],
    isReporter: false,
    reporterSlug: "",
    onnProfilePath: "",
    profileAvatarPreset: "neutral",
    hasCustomProfilePhoto: false,
    profileGender: "",
    profilePreferredPronouns: "",
    profileLocale: "",
    pronounSubject: "they",
    pronounObject: "them",
    pronounPossessive: "their",
    pronounLabel: "they/them",
    requiresPreferredPronouns: false,
    profileCompletionRequired: false,
    missingProfileFields: [],
    reportedLocationsNote: "",
    isTestingDummy: true,
  }, index);
}

/**
 * @param {string} usernameOrPlayerId
 * @param {object|null} adminProfile
 * @param {boolean} testingMode
 * @param {boolean} isStaffAdmin
 */
export function resolveDummyGameProfile(usernameOrPlayerId, adminProfile, testingMode, isStaffAdmin) {
  if (!shouldApplyTestingFriends(testingMode, isStaffAdmin)) {
    return null;
  }

  let index = getDummyIndexByUsername(usernameOrPlayerId);
  if (index < 0 && isDummyPlayerId(usernameOrPlayerId)) {
    index = Number.parseInt(String(usernameOrPlayerId).slice(-12), 10);
  }

  if (!Number.isFinite(index) || index < 0 || index >= DUMMY_COUNT) {
    return null;
  }

  return buildDummyGameProfile(index, adminProfile);
}

/**
 * @param {object} profile
 * @param {boolean} testingMode
 * @param {boolean} isStaffAdmin
 */
export function augmentOwnerProfileForTesting(profile, testingMode, isStaffAdmin) {
  if (!shouldApplyTestingFriends(testingMode, isStaffAdmin) || !profile?.isOwner) {
    return profile;
  }

  const removed = loadRemovedDummyFriendships();
  const dummyFriends = Array.from({ length: DUMMY_COUNT }, (_, index) => buildDummyProfileFriend(index)).filter(
    (_, index) => !removed.has(dummyFriendshipId(index)),
  );
  const existing = profile.friends ?? [];
  const seen = new Set(existing.map((friend) => String(friend.playerId)));
  const merged = [...existing];

  for (const dummy of dummyFriends) {
    if (!seen.has(String(dummy.playerId))) {
      merged.push(dummy);
    }
  }

  merged.sort((left, right) =>
    left.username.localeCompare(right.username, undefined, { sensitivity: "base" }),
  );

  return { ...profile, friends: merged };
}

/**
 * @param {object|null|undefined} friendsResponse
 * @param {boolean} testingMode
 * @param {boolean} isStaffAdmin
 */
export function mergeFriendsListForTesting(friendsResponse, testingMode, isStaffAdmin) {
  if (!shouldApplyTestingFriends(testingMode, isStaffAdmin)) {
    return friendsResponse ?? { friends: [], incomingRequests: [], outgoingRequests: [] };
  }

  const realFriends = friendsResponse?.friends ?? [];
  const seen = new Set(realFriends.map((friend) => String(friend.playerId)));
  const merged = [...realFriends];

  for (const dummy of getDummyFriendSummaries()) {
    if (!seen.has(String(dummy.playerId))) {
      merged.push(dummy);
    }
  }

  merged.sort((left, right) =>
    left.username.localeCompare(right.username, undefined, { sensitivity: "base" }),
  );

  return {
    friends: merged,
    incomingRequests: friendsResponse?.incomingRequests ?? [],
    outgoingRequests: friendsResponse?.outgoingRequests ?? [],
  };
}

export function loadTestingModeEnabled() {
  return cachedTestingModeEnabled;
}

export function setCachedTestingModeEnabled(enabled) {
  cachedTestingModeEnabled = Boolean(enabled);
  try {
    localStorage.removeItem(TESTING_MODE_STORAGE_KEY);
  } catch {
    /* ignore legacy localStorage */
  }
}

/**
 * @param {import("./api.js").RavaApi} api
 * @param {boolean} enabled
 */
export async function saveTestingModeEnabled(api, enabled) {
  const response = await api.setAdminTestingMode(enabled);
  setCachedTestingModeEnabled(Boolean(response?.enabled));
  return response;
}

function miningCompanyName(seed) {
  const prefix = MINE_PREFIXES[pick(`${seed}-p`, MINE_PREFIXES.length)];
  const core = MINE_CORES[pick(`${seed}-c`, MINE_CORES.length)];
  const suffix = MINE_SUFFIXES[pick(`${seed}-s`, MINE_SUFFIXES.length)];
  const number = 100 + (pick(`${seed}-n`, 8900));
  return `${prefix} ${core} ${suffix} ${number}`;
}

function buildDummyPlayerSummary(index) {
  const id = dummyPlayerId(index);
  const username = USERNAMES[index % USERNAMES.length];
  const seed = `dummy-${index}`;
  const credits = 500 + pick(`${seed}-rax`, 95000);
  const mineCount = 1 + pick(`${seed}-mines`, 4);
  const daysAgo = 3 + pick(`${seed}-joined`, 400);

  return {
    id,
    username,
    email: `${username}@test.rava.local`,
    credits,
    createdAt: new Date(Date.now() - daysAgo * 86_400_000).toISOString(),
    mineCount,
    activeBan: null,
    isTestingDummy: true,
  };
}

export function getDummyPlayerSummaries() {
  return Array.from({ length: DUMMY_COUNT }, (_, index) => buildDummyPlayerSummary(index));
}

/**
 * @param {string} playerId
 */
export function getDummyPlayerProfile(playerId) {
  if (!isDummyPlayerId(playerId)) {
    return null;
  }

  const index = Number.parseInt(playerId.slice(-12), 10);
  if (!Number.isFinite(index) || index < 0 || index >= DUMMY_COUNT) {
    return null;
  }

  const summary = buildDummyPlayerSummary(index);
  const seed = `dummy-profile-${index}`;
  const workerCount = 2 + pick(`${seed}-workers`, 48);
  const zoneCount = 1 + pick(`${seed}-zones`, 12);
  const gameDay = 1 + pick(`${seed}-day`, 120);
  const profileNum = String(100_000 + index * 7919).slice(0, 6);

  const month = 1 + pick(`${seed}-bmonth`, 12);
  const day = 1 + pick(`${seed}-bday`, 28);
  const birthYear = 1988 + pick(`${seed}-byear`, 20);

  return applyDummyProfileAssets({
    id: summary.id,
    username: summary.username,
    email: summary.email,
    profileNumber: profileNum,
    profileImageUrl: "",
    profileBackgroundUrl: "",
    companyLogoUrl: "",
    mood: MOODS[pick(`${seed}-mood`, MOODS.length)],
    aboutMe: ABOUT_SNIPPETS[pick(`${seed}-about`, ABOUT_SNIPPETS.length)],
    music: MUSIC[pick(`${seed}-music`, MUSIC.length)],
    interests: INTERESTS[pick(`${seed}-interests`, INTERESTS.length)],
    discord: index % 3 === 0 ? `${summary.username}` : "",
    bluesky: index % 4 === 0 ? `${summary.username}.bsky` : "",
    twitter: "",
    youtube: index % 5 === 0 ? `@${summary.username}` : "",
    facebook: "",
    theme: THEMES[pick(`${seed}-theme`, THEMES.length)],
    memberSince: summary.createdAt,
    birthday: `${birthYear}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`,
    lastBirthdayBonusYear: birthYear < 2010 ? gameDay - 1 : null,
    currentGameDay: gameDay,
    credits: summary.credits,
    mineName: miningCompanyName(seed),
    workerCount,
    zoneCount,
    mineCount: summary.mineCount,
    activeFlag: null,
    flagHistory: [],
    activeBan: null,
    banHistory: [],
    isProtectedAdmin: false,
    isModerator: false,
    warningCount: 0,
    warningHistory: [],
    isTestingDummy: true,
  }, index);
}

export function mergePlayersForDisplay(testingMode, search, realPlayers) {
  const trimmed = (search ?? "").trim();
  if (!testingMode || trimmed) {
    return realPlayers ?? [];
  }

  return [...getDummyPlayerSummaries(), ...(realPlayers ?? [])];
}
