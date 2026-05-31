const POLL_MS = 60000;

const els = {
  updated: document.getElementById("values-updated"),
  signupCredits: document.getElementById("signup-credits"),
  birthdayBonus: document.getElementById("birthday-bonus"),
  buybackRate: document.getElementById("buyback-rate"),
  marketDate: document.getElementById("market-date"),
  marketGameDay: document.getElementById("market-game-day"),
  marketSource: document.getElementById("market-source"),
  oreRows: document.getElementById("ore-rows"),
  supplyRows: document.getElementById("supply-rows"),
};

function formatCredits(value) {
  if (value == null) return "—";
  return `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)} cr`;
}

function formatPercent(value) {
  if (value == null) return "—";
  const sign = value > 0 ? "+" : "";
  return `${sign}${value.toFixed(2)}%`;
}

function formatItemName(value) {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function renderOreRows(items) {
  if (!items?.length) {
    els.oreRows.innerHTML = `<tr><td colspan="3">No ore prices available.</td></tr>`;
    return;
  }

  els.oreRows.innerHTML = items.map((item) => `
    <tr>
      <td>${formatItemName(item.itemType)}</td>
      <td>${formatCredits(item.price)}</td>
      <td>${item.note || "—"}</td>
    </tr>
  `).join("");
}

function renderSupplyRows(items) {
  if (!items?.length) {
    els.supplyRows.innerHTML = `<tr><td colspan="4">No supply prices available.</td></tr>`;
    return;
  }

  els.supplyRows.innerHTML = items.map((item) => `
    <tr>
      <td>${formatItemName(item.itemType)}</td>
      <td>${formatCredits(item.price)}</td>
      <td>${formatCredits(item.basePrice)}</td>
      <td>${formatPercent(item.changePct)}</td>
    </tr>
  `).join("");
}

function renderEconomy(data) {
  els.updated.textContent = `Last updated ${new Date().toLocaleString()}`;
  els.signupCredits.textContent = formatCredits(data.signUpCredits);
  els.birthdayBonus.textContent = formatCredits(data.birthdayBonusCredits);
  els.buybackRate.textContent = `${Math.round(data.emergencyBuybackRate * 100)}% of refinery price`;
  els.marketDate.textContent = data.marketDate || "—";
  els.marketGameDay.textContent = data.referenceGameDay ?? "—";
  els.marketSource.textContent = data.marketSource || "—";
  renderOreRows(data.orePrices);
  renderSupplyRows(data.supplyPrices);
}

async function refresh() {
  els.updated.textContent = "Loading item values…";

  try {
    const response = await fetch("/api/economy");
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    renderEconomy(await response.json());
  } catch (error) {
    els.updated.textContent = `Failed to load item values: ${error.message}`;
    els.oreRows.innerHTML = `<tr><td colspan="3">${error.message}</td></tr>`;
    els.supplyRows.innerHTML = `<tr><td colspan="4">${error.message}</td></tr>`;
  }
}

refresh();
window.setInterval(refresh, POLL_MS);
