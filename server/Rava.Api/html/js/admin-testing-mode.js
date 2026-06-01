export const TESTING_MODE_STORAGE_KEY = "rava.admin.testingMode";
export const DUMMY_PLAYER_ID_PREFIX = "aaaaaaaa-aaaa-4aaa-8aaa-";

const DUMMY_COUNT = 12;

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

export function loadTestingModeEnabled() {
  try {
    return localStorage.getItem(TESTING_MODE_STORAGE_KEY) === "1";
  } catch {
    return false;
  }
}

export function saveTestingModeEnabled(enabled) {
  try {
    localStorage.setItem(TESTING_MODE_STORAGE_KEY, enabled ? "1" : "0");
  } catch {
    /* ignore */
  }
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

  return {
    id: summary.id,
    username: summary.username,
    email: summary.email,
    profileNumber: profileNum,
    profileImageUrl: "",
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
  };
}

export function mergePlayersForDisplay(testingMode, search, realPlayers) {
  const trimmed = (search ?? "").trim();
  if (!testingMode || trimmed) {
    return realPlayers ?? [];
  }

  return [...getDummyPlayerSummaries(), ...(realPlayers ?? [])];
}
