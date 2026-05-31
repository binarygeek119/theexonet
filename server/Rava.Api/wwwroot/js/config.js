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
const PRODUCTION_API_HOST = "ravaapi.binarygeek119.duckdns.org";
const PRODUCTION_GAME_HOST = "rava.binarygeek119.duckdns.org";

/** Base URL for API requests (no trailing slash). */
export function resolveApiBaseUrl() {
  if (typeof window === "undefined") {
    return "";
  }

  const { hostname, port, protocol } = window.location;
  const isLocalHost =
    hostname === "localhost" ||
    hostname === "127.0.0.1" ||
    hostname === "[::1]";

  if (isLocalHost) {
    return port === "5000" ? "" : LOCAL_API_URL;
  }

  if (hostname === PRODUCTION_GAME_HOST) {
    return `${protocol}//${PRODUCTION_API_HOST}`;
  }

  return "";
}

export const API_BASE_URL = resolveApiBaseUrl();
