import { formatRaxHtml } from "./currency.js";
import { formatConditionBar } from "./trade-marketplace.js?v=20260609-store-trade";

let shippingCtx = null;

export function wireShippingPanel(ctx) {
  shippingCtx = ctx;
  ctx.els.shippingScheduleForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    scheduleShipment().catch((error) => setShippingStatus(error.message, true));
  });
  for (const el of [ctx.els.shippingShipClass, ctx.els.shippingRouteTier, ctx.els.shippingArrivalDay]) {
    el?.addEventListener("change", () => updateRoutePreview(shippingCtx?.state.shipping));
    el?.addEventListener("input", () => updateRoutePreview(shippingCtx?.state.shipping));
  }
}

export async function loadShippingDashboard() {
  if (!shippingCtx?.api.mineId) {
    return null;
  }

  const dashboard = await shippingCtx.api.getShippingDashboard();
  shippingCtx.state.shipping = dashboard;
  return dashboard;
}

export function renderShippingPanel() {
  if (!shippingCtx) {
    return;
  }

  const { els, state, t } = shippingCtx;
  const dash = state.shipping;
  if (!dash) {
    els.shippingSummary.textContent = t("shipping.loading");
    return;
  }

  const pct = dash.stockpileCapacity > 0
    ? Math.min(100, (dash.stockpileTotal / dash.stockpileCapacity) * 100)
    : 0;

  els.shippingSummary.innerHTML = [
    `${t("shipping.stockpile")}: ${dash.stockpileTotal.toFixed(1)} / ${dash.stockpileCapacity}`,
    `<div class="stockpile-gauge"><div class="stockpile-gauge-fill" style="width:${pct}%"></div></div>`,
    dash.isStockpileFull ? `<strong class="danger">${t("shipping.stockpileFull")}</strong>` : "",
  ].join("");

  renderStockpileList(dash);
  renderScheduleForm(dash);
  renderShipmentList(dash);
}

function renderStockpileList(dash) {
  const list = shippingCtx.els.shippingStockpileList;
  if (!list) {
    return;
  }

  list.innerHTML = "";
  const items = dash.stockpile ?? [];
  if (!items.length) {
    list.innerHTML = `<p class="market-info">${shippingCtx.t("shipping.stockpileEmpty")}</p>`;
    return;
  }

  for (const item of items) {
    const row = document.createElement("div");
    row.className = "shipping-stockpile-row";
    row.innerHTML = `<strong>${item.oreType}</strong> · ${Number(item.quantity).toFixed(1)} · ${Number(item.condition).toFixed(0)}%${formatConditionBar(item.condition)}`;
    list.appendChild(row);
  }
}

function renderScheduleForm(dash) {
  const { els, t } = shippingCtx;
  const oreSelect = els.shippingOreType;
  const shipSelect = els.shippingShipClass;
  const routeSelect = els.shippingRouteTier;
  const arrivalInput = els.shippingArrivalDay;

  if (oreSelect) {
    const types = [...new Set((dash.stockpile ?? []).map((i) => i.oreType))];
    oreSelect.innerHTML = types.length
      ? types.map((type) => `<option value="${type}">${type}</option>`).join("")
      : `<option value="">${t("shipping.noOre")}</option>`;
  }

  if (shipSelect) {
    shipSelect.innerHTML = ["Scout", "Hauler", "Freighter", "Bulk"]
      .map((s) => `<option value="${s}">${s}</option>`)
      .join("");
  }

  if (routeSelect) {
    routeSelect.innerHTML = ["Express", "Standard", "Economy"]
      .map((r) => `<option value="${r}">${r}</option>`)
      .join("");
  }

  updateRoutePreview(dash);
}

function updateRoutePreview(dash) {
  const preview = shippingCtx.els.shippingRoutePreview;
  if (!preview) {
    return;
  }

  const ship = shippingCtx.els.shippingShipClass?.value ?? "Hauler";
  const route = shippingCtx.els.shippingRouteTier?.value ?? "Standard";
  const arrival = Number(shippingCtx.els.shippingArrivalDay?.value ?? 0);
  const match = (dash.routes ?? []).find(
    (r) => r.shipClass === ship && r.routeTier === route,
  );

  if (!match) {
    preview.textContent = "";
    return;
  }

  const departure = arrival > 0 ? arrival - match.transitDays : match.transitDays;
  preview.innerHTML = [
    match.routeDescription,
    `<br>${shippingCtx.t("shipping.departsDay")} ${departure} · ${shippingCtx.t("shipping.arrivesDay")} ${arrival || "?"}`,
    `<br>${shippingCtx.t("shipping.capacity")}: ${match.capacity} · ${shippingCtx.t("shipping.estCostFull")}: ${formatRaxHtml(match.estimatedCost)}`,
    `<br>${shippingCtx.t("shipping.fastLeg")}: ${(match.fastLegPercent * 100).toFixed(0)}% · ${shippingCtx.t("shipping.slowLeg")}: ${(match.slowLegPercent * 100).toFixed(0)}%`,
  ].join("");
}

function renderShipmentList(dash) {
  const list = shippingCtx.els.shippingVoyageList;
  if (!list) {
    return;
  }

  list.innerHTML = "";
  const shipments = (dash.shipments ?? []).filter((s) => s.status !== "arrived" && s.status !== "cancelled");
  if (!shipments.length) {
    list.innerHTML = `<p class="market-info">${shippingCtx.t("shipping.noVoyages")}</p>`;
    return;
  }

  for (const s of shipments) {
    const card = document.createElement("article");
    card.className = "shipping-voyage-card";
    card.innerHTML = `
      <strong>${s.shipClass} · ${s.oreType} → day ${s.scheduledArrivalDay}</strong>
      <p class="market-info">${s.routeTier} · ${s.status} · departs day ${s.departureDay}</p>
      <p>${s.lastEventDescription ?? ""}</p>
      ${s.cargoQuantity > 0 ? `<p>Loaded ${s.cargoQuantity} (${s.fillPercent}% fill) · cost ${formatRaxHtml(s.shippingCostPaid)}</p>` : ""}
      ${s.daysRemaining > 0 && s.status !== "scheduled" ? `<p>${shippingCtx.t("shipping.daysRemaining")}: ${s.daysRemaining}</p>` : ""}`;

    if (s.status === "scheduled") {
      const cancelBtn = document.createElement("button");
      cancelBtn.type = "button";
      cancelBtn.className = "btn ghost";
      cancelBtn.textContent = shippingCtx.t("shipping.cancel");
      cancelBtn.addEventListener("click", () => {
        cancelShipment(s.id).catch((error) => setShippingStatus(error.message, true));
      });
      card.appendChild(cancelBtn);
    }

    list.appendChild(card);
  }
}

async function scheduleShipment() {
  const { api, els, refreshAll, t } = shippingCtx;
  const body = {
    shipClass: els.shippingShipClass?.value,
    routeTier: els.shippingRouteTier?.value,
    oreType: els.shippingOreType?.value,
    scheduledArrivalDay: Number(els.shippingArrivalDay?.value),
  };

  setShippingStatus(t("shipping.scheduling"));
  await api.scheduleShipment(body);
  await refreshAll();
  setShippingStatus(t("shipping.scheduled"));
}

async function cancelShipment(shipmentId) {
  const { api, refreshAll, t } = shippingCtx;
  await api.cancelShipment(shipmentId);
  await refreshAll();
  setShippingStatus(t("shipping.cancelled"));
}

function setShippingStatus(message, isError = false) {
  const el = shippingCtx?.els.shippingStatus;
  if (!el) {
    return;
  }

  el.textContent = message ?? "";
  el.classList.toggle("error", Boolean(isError && message));
}
