import { formatRaxHtml } from "./currency.js";

const MISSING_PRODUCT = "/exonet/voidcorp/placeholders/missing-product.svg";

let tradeCtx = null;

export function wireTradeMarketplace(ctx) {
  tradeCtx = ctx;
  ctx.els.listingCreateForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    createListing().catch((error) => setListingStatus(error.message, true));
  });
}

export async function refreshTradeListings() {
  if (!tradeCtx) {
    return;
  }

  const response = await tradeCtx.api.getTradeListings();
  tradeCtx.state.tradeListings = response.listings ?? [];
}

export function renderTradeMarketplace() {
  if (!tradeCtx) {
    return;
  }

  renderListingCards();
  populateListingSellSelect();
}

function renderListingCards() {
  const { els, t, state } = tradeCtx;
  const list = els.tradeListingList;
  if (!list) {
    return;
  }

  const listings = state.tradeListings ?? [];
  list.innerHTML = "";

  if (!listings.length) {
    list.innerHTML = `<p class="market-info">${t("trade.listings.empty")}</p>`;
    return;
  }

  for (const listing of listings) {
    const card = document.createElement("article");
    card.className = "trade-listing-card";
    const seller = listing.sellerUsername ?? t("trade.listings.npcSeller");
    const total = Number(listing.unitPrice) * Number(listing.quantity);
    const condition = Number(listing.condition ?? 100);
    const image = listing.imageUrl || MISSING_PRODUCT;

    card.innerHTML = `
      <div class="trade-listing-thumb">
        <img src="${escapeAttr(image)}" alt="" loading="lazy"
          onerror="this.onerror=null;this.src='${MISSING_PRODUCT}'">
      </div>
      <div class="trade-listing-body">
        <h4>${escapeHtml(listing.displayName)} × ${Number(listing.quantity).toFixed(1)}</h4>
        <p class="trade-listing-meta">${t("trade.listings.used")} · ${seller}</p>
        <div class="condition-bar" aria-label="Condition ${condition}%">
          <div class="condition-bar-fill" style="width:${Math.max(0, Math.min(100, condition))}%"></div>
        </div>
        <p class="trade-listing-price">${formatRaxHtml(listing.unitPrice)}/u · ${t("trade.listings.total")} ${formatRaxHtml(total)}</p>
      </div>`;

    if (listing.isMine) {
      const cancelBtn = document.createElement("button");
      cancelBtn.type = "button";
      cancelBtn.className = "btn ghost";
      cancelBtn.textContent = t("trade.listings.cancel");
      cancelBtn.addEventListener("click", () => {
        cancelListing(listing.id).catch((error) => setListingStatus(error.message, true));
      });
      card.querySelector(".trade-listing-body")?.appendChild(cancelBtn);
    } else {
      const buyBtn = document.createElement("button");
      buyBtn.type = "button";
      buyBtn.className = "btn primary";
      buyBtn.textContent = t("trade.listings.buyNow");
      buyBtn.addEventListener("click", () => {
        purchaseListing(listing.id).catch((error) => setListingStatus(error.message, true));
      });
      card.querySelector(".trade-listing-body")?.appendChild(buyBtn);
    }

    list.appendChild(card);
  }
}

function populateListingSellSelect() {
  const { els, state, tradeOreTypes, tradeSupplyTypes } = tradeCtx;
  const select = els.listingSellItem;
  if (!select) {
    return;
  }

  const options = [`<option value="">${tradeCtx.t("trade.listings.selectItem")}</option>`];
  for (const item of state.mine?.inventory ?? []) {
    const qty = Number(item.quantity ?? 0);
    const condition = Number(item.condition ?? 100);
    if (qty <= 0 || condition <= 0) {
      continue;
    }

    const meta = item.category === "Ore"
      ? tradeOreTypes[item.itemType]
      : tradeSupplyTypes[item.itemType];
    if (!meta) {
      continue;
    }

    const label = `${meta.displayName} (${qty.toFixed(1)} · ${condition.toFixed(0)}%${item.isNew ? " new" : ""})`;
    options.push(
      `<option value="${item.category}|${item.itemType}|${item.isNew ? "1" : "0"}">${label}</option>`,
    );
  }

  select.innerHTML = options.join("");
}

async function createListing() {
  const { els, api, t, refreshAll } = tradeCtx;
  const raw = els.listingSellItem?.value ?? "";
  const [category, itemType, isNewFlag] = raw.split("|");
  const quantity = Number(els.listingSellQty?.value ?? 0);
  const unitPrice = Number(els.listingSellPrice?.value ?? 0);

  if (!category || !itemType || quantity <= 0 || unitPrice <= 0) {
    setListingStatus(t("trade.listings.invalid"), true);
    return;
  }

  setListingStatus(t("trade.listings.posting"));
  await api.createTradeListing({
    category,
    itemType,
    quantity,
    unitPrice,
    isNew: isNewFlag === "1",
  });
  await refreshAll();
  setListingStatus(t("trade.listings.posted"));
}

async function purchaseListing(listingId) {
  const { api, refreshAll, t } = tradeCtx;
  setListingStatus(t("trade.listings.buying"));
  await api.purchaseTradeListing(listingId);
  await refreshAll();
  setListingStatus(t("trade.listings.purchased"));
}

async function cancelListing(listingId) {
  const { api, refreshAll, t } = tradeCtx;
  await api.cancelTradeListing(listingId);
  await refreshAll();
  setListingStatus(t("trade.listings.cancelled"));
}

function setListingStatus(message, isError = false) {
  const el = tradeCtx?.els.listingStatus;
  if (!el) {
    return;
  }

  el.textContent = message ?? "";
  el.classList.toggle("error", Boolean(isError && message));
  el.classList.toggle("success", Boolean(!isError && message));
}

export function formatConditionBar(condition) {
  const pct = Math.max(0, Math.min(100, Number(condition ?? 100)));
  return `<div class="condition-bar" aria-hidden="true"><div class="condition-bar-fill" style="width:${pct}%"></div></div>`;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function escapeAttr(value) {
  return escapeHtml(value).replaceAll("'", "&#39;");
}
