import { t } from "../i18n.js";

/** @type {import("./job-workspaces.js").JobWorkspaceContext | null} */
let ctx = null;

export function initAsteroidMiner(context) {
  ctx = context;
}

export function render(state) {
  if (!ctx) {
    return;
  }

  renderMineGrid(state);
  renderZonePanel(state);
}

export function refresh(state) {
  render(state);
}

function renderMineGrid(state) {
  const { els, oreMeta } = ctx;
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
      renderMineGrid(state);
      renderZonePanel(state);
    });
    els.mineGrid.appendChild(cell);
  }
}

function renderZonePanel(state) {
  const { els, oreMeta } = ctx;
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
    const salaryLabel = `${Number(worker.salary).toFixed(0)} Rax`;
    button.textContent = assignedHere
      ? `${worker.name} (${salaryLabel}, assigned)`
      : busyElsewhere
        ? `${worker.name} (${salaryLabel}, busy)`
        : `${worker.name} (${salaryLabel})`;
    button.addEventListener("click", () => toggleWorker(worker, zone.id, state));
    els.workerList.appendChild(button);
  }
}

async function toggleWorker(worker, zoneId, state) {
  const { api, showStatus, refreshAll, showEventCompletions } = ctx;
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
