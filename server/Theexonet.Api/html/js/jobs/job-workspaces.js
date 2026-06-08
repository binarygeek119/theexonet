/**
 * Job workspace registry — add a future job by:
 * 1. Catalog entry + WorkspaceModule in PlayerJobCatalog.cs
 * 2. HTML section id="job-workspace-{module}" in index.html
 * 3. JS module + registration below
 */
import {
  initAsteroidMiner,
  render as renderAsteroidMiner,
  refresh as refreshAsteroidMiner,
} from "./asteroid-miner.js?v=20260608-job-workspaces";

/** @typedef {object} JobWorkspaceContext */
/** @typedef {object} JobWorkspaceDefinition */

export const UNAVAILABLE_PANEL_ID = "job-workspace-unavailable";

export const jobWorkspaces = {
  asteroid_miner: {
    module: "asteroid-miner",
    panelId: "job-workspace-asteroid-miner",
    render: renderAsteroidMiner,
    refresh: refreshAsteroidMiner,
  },
};

/**
 * @param {JobWorkspaceContext} context
 */
export function initJobWorkspaces(context) {
  initAsteroidMiner(context);
}

/**
 * @param {string | null | undefined} jobSlug
 * @returns {JobWorkspaceDefinition | null}
 */
export function resolveJobWorkspace(jobSlug) {
  if (!jobSlug) {
    return null;
  }

  return jobWorkspaces[jobSlug] ?? null;
}
