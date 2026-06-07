export const ORE_TYPES = {
  Ferroxite: { displayName: "Ferroxite", color: "#996644", basePrice: 120 },
  Voidium: { displayName: "Voidium", color: "#6633b3", basePrice: 280 },
  Stellarite: { displayName: "Stellarite", color: "#e6cc4d", basePrice: 450 },
  SalvageScrap: { displayName: "Salvage Scrap", color: "#80808c", basePrice: 40, isEmergencySource: true },
};

export const SUPPLY_TYPES = {
  DrillBits: { displayName: "Drill Bits", color: "#b38033", basePrice: 85, symbol: "XLI" },
  FuelCells: { displayName: "Fuel Cells", color: "#3399e6", basePrice: 110, symbol: "XLE" },
  LifeSupport: { displayName: "Life Support", color: "#4dcc80", basePrice: 95, symbol: "XLV" },
  CommModules: { displayName: "Comm Modules", color: "#80b3ff", basePrice: 130, symbol: "XLK" },
};

export const GRID_SIZE = 8;

const LOCAL_API_URL = "http://localhost:5000";
/** Production apex domain — API is api.{apex}, game is {apex}. */
export const PRODUCTION_APEX_DOMAIN = "theexonet.com";

function normalizeHost(hostname) {
  return hostname.toLowerCase().replace(/^www\./, "");
}

export function productionApiHost(apex = PRODUCTION_APEX_DOMAIN) {
  return `api.${apex}`;
}

export function productionGameHost(apex = PRODUCTION_APEX_DOMAIN) {
  return apex;
}

export function productionAdminHost(apex = PRODUCTION_APEX_DOMAIN) {
  return `admin.${apex}`;
}

export function productionModeratorHost(apex = PRODUCTION_APEX_DOMAIN) {
  return `moderator.${apex}`;
}

function resolveApexDomain(hostname) {
  const host = normalizeHost(hostname);
  if (host === PRODUCTION_APEX_DOMAIN || host.endsWith(`.${PRODUCTION_APEX_DOMAIN}`)) {
    return PRODUCTION_APEX_DOMAIN;
  }
  return null;
}

export function readMetaApiBase() {
  const value = document.querySelector('meta[name="theexonet-api-base"]')?.getAttribute("content")?.trim();
  if (!value) {
    return "";
  }
  // Ignore legacy duckdns meta after migrating to theexonet.com.
  if (value.includes("duckdns.org")) {
    return "";
  }
  return value.replace(/\/$/, "");
}

/** Base URL for API requests (no trailing slash). */
export function resolveApiBaseUrl() {
  if (typeof window === "undefined") {
    return "";
  }

  const metaBase = readMetaApiBase();
  if (metaBase) {
    return metaBase;
  }

  const { hostname, port } = window.location;
  const host = normalizeHost(hostname);
  const apex = resolveApexDomain(host);
  const apiHost = apex ? productionApiHost(apex) : productionApiHost();

  const isLocalHost =
    host === "localhost" ||
    host === "127.0.0.1" ||
    host === "[::1]";

  if (isLocalHost) {
    return port === "5000" ? "" : LOCAL_API_URL;
  }

  if (host === normalizeHost(apiHost)) {
    return "";
  }

  return `https://${apiHost}`;
}

export const API_BASE_URL = resolveApiBaseUrl();
