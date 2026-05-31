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
/** HTTPS API host (reverse proxy → port 5000). */
const PRODUCTION_API_HOST = "ravaapi.binarygeek119.duckdns.org";
/** HTTPS admin portal host (reverse proxy → port 7000). */
const PRODUCTION_ADMIN_HOST = "ravaadmin.binarygeek119.duckdns.org";
/** HTTPS moderator portal host (reverse proxy → port 7050). */
const PRODUCTION_MODERATOR_HOST = "ravamoderator.binarygeek119.duckdns.org";
/** HTTPS game host (static html → port 80). */
const PRODUCTION_GAME_HOST = "rava.binarygeek119.duckdns.org";

function normalizeHost(hostname) {
  return hostname.toLowerCase().replace(/^www\./, "");
}

function readMetaApiBase() {
  const value = document.querySelector('meta[name="rava-api-base"]')?.getAttribute("content")?.trim();
  return value ? value.replace(/\/$/, "") : "";
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

  const { hostname, port, protocol } = window.location;
  const host = normalizeHost(hostname);
  const apiHost = normalizeHost(PRODUCTION_API_HOST);

  const isLocalHost =
    host === "localhost" ||
    host === "127.0.0.1" ||
    host === "[::1]";

  if (isLocalHost) {
    return port === "5000" ? "" : LOCAL_API_URL;
  }

  if (host === apiHost) {
    return "";
  }

  // Game site and any other public host: call the API subdomain.
  return `${protocol}//${PRODUCTION_API_HOST}`;
}

export const API_BASE_URL = resolveApiBaseUrl();
