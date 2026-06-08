import { formatRaxHtml } from "./currency.js";
import { API_BASE_URL, readMetaApiBase } from "./config.js";

const MISSING_PRODUCT = "/exonet/voidcorp/placeholders/missing-product.svg";

let storeCtx = null;
let storeCatalog = [];
let activeProductSlug = null;

export function wireStoreCatalog(ctx) {
  storeCtx = ctx;
  ctx.els.storeBackBtn?.addEventListener("click", () => {
    activeProductSlug = null;
    renderStoreCatalog();
  });
}

export async function loadStoreCatalog() {
  if (!storeCtx) {
    return;
  }

  try {
    const response = await storeCtx.api.getStoreCatalog();
    storeCatalog = response.products ?? [];
    stateStoreCatalog(storeCtx.state, storeCatalog);
  } catch {
    storeCatalog = [];
  }
}

function stateStoreCatalog(state, products) {
  state.storeCatalog = products;
}

export function renderStoreCatalog() {
  if (!storeCtx) {
    return;
  }

  const { els, t, showStatus } = storeCtx;
  const market = storeCtx.state.market;

  els.storeMarketInfo.textContent = market
    ? `Game Day ${market.gameDay} · ${storeCtx.formatMarketSource?.(market.source) ?? market.source} · Factory-new supplies${storeCtx.formatActiveMarketBonuses?.(market) ?? ""}`
    : t("store.loading");

  if (activeProductSlug) {
    renderStoreDetail(activeProductSlug);
    return;
  }

  els.storeGrid?.classList.remove("hidden");
  els.storeDetail?.classList.add("hidden");
  els.storeBackBtn?.classList.add("hidden");

  const grid = els.storeGrid;
  if (!grid) {
    return;
  }

  grid.innerHTML = "";
  for (const product of storeCatalog) {
    const card = document.createElement("article");
    card.className = "store-card";
    const image = resolveVoidCorpProductImageUrl(product.imageUrl);
    const imageFallback = escapeAttr(resolveVoidCorpProductImageUrl(null));
    card.innerHTML = `
      <div class="store-card-image-wrap">
        <img class="store-card-image" src="${escapeAttr(image)}" alt="" loading="lazy"
          onerror="this.onerror=null;this.src='${imageFallback}'">
        <span class="store-badge-new">${t("store.badgeNew")}</span>
      </div>
      <div class="store-card-body">
        <h3>${escapeHtml(product.displayName)}</h3>
        <p class="store-card-tagline">${escapeHtml(product.tagline)}</p>
        <p class="store-card-price">${formatRaxHtml(product.livePrice)} <span class="market-info">${t("store.livePrice")}</span></p>
      </div>`;
    card.addEventListener("click", () => {
      activeProductSlug = product.slug;
      renderStoreCatalog();
    });
    grid.appendChild(card);
  }

  if (!storeCatalog.length) {
    grid.innerHTML = `<p class="market-info">${t("store.empty")}</p>`;
  }

  if (els.storeCompanyNameList) {
    storeCtx.loadStoreCompanyNames?.().catch((error) => {
      if (els.storeCompanyNameStatus) {
        els.storeCompanyNameStatus.textContent = error.message;
      }
    });
  }
}

function renderStoreDetail(slug) {
  const { els, t, api, showStatus, refreshAll } = storeCtx;
  const product = storeCatalog.find((p) => p.slug === slug || p.itemType === slug);
  if (!product) {
    activeProductSlug = null;
    renderStoreCatalog();
    return;
  }

  els.storeGrid?.classList.add("hidden");
  els.storeDetail?.classList.remove("hidden");
  els.storeBackBtn?.classList.remove("hidden");

  const image = resolveVoidCorpProductImageUrl(product.imageUrl);
  const imageFallback = escapeAttr(resolveVoidCorpProductImageUrl(null));
  const stock = sumNewSupplyStock(storeCtx.state.mine, product.itemType);

  els.storeDetail.innerHTML = `
    <div class="store-detail-layout">
      <div class="store-detail-image-wrap">
        <img class="store-detail-image" src="${escapeAttr(image)}" alt=""
          onerror="this.onerror=null;this.src='${imageFallback}'">
        <span class="store-badge-new">${t("store.badgeNew")}</span>
      </div>
      <div class="store-detail-copy">
        <p class="store-detail-category">${escapeHtml(product.category)}</p>
        <h3>${escapeHtml(product.displayName)}</h3>
        <p class="store-detail-tagline">${escapeHtml(product.tagline)}</p>
        <p class="store-detail-summary">${escapeHtml(product.summary)}</p>
        <p class="store-detail-price">${formatRaxHtml(product.livePrice)} <span class="market-info">${t("store.livePrice")}</span></p>
        <p class="store-detail-msrp market-info">${t("store.msrp")}: ${formatRaxHtml(product.basePrice)}</p>
        <p class="store-detail-stock market-info">${t("store.yourNewStock")}: ${stock.toFixed(0)}</p>
        <p class="store-detail-desc">${escapeHtml(product.description)}</p>
        <div class="store-buy-row">
          <label for="store-buy-qty">${t("store.quantity")}</label>
          <input id="store-buy-qty" type="number" min="1" step="1" value="5">
          <button type="button" class="btn primary" id="store-buy-btn">${t("store.buyNew")}</button>
        </div>
      </div>
    </div>`;

  els.storeDetail.querySelector("#store-buy-btn")?.addEventListener("click", async () => {
    const qty = Number(els.storeDetail.querySelector("#store-buy-qty")?.value ?? 5);
    if (!Number.isFinite(qty) || qty <= 0) {
      return;
    }

    showStatus(t("store.buying"));
    try {
      await api.buySupply(product.itemType, qty);
      await refreshAll();
      renderStoreCatalog();
      showStatus("");
    } catch (error) {
      showStatus(error.message, true);
    }
  });
}

function sumNewSupplyStock(mine, itemType) {
  return (mine?.inventory ?? [])
    .filter((i) => i.category === "Supply" && i.itemType === itemType && i.isNew)
    .reduce((sum, i) => sum + Number(i.quantity ?? 0), 0);
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

/** Same URL rules as Exonet VoidCorp listings (API-hosted /exonet/voidcorp/ assets). */
function resolveVoidCorpProductImageUrl(imageUrl) {
  const path = imageUrl || MISSING_PRODUCT;
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  const apiBase = (API_BASE_URL || readMetaApiBase()).replace(/\/$/, "");
  if (apiBase && path.startsWith("/")) {
    return `${apiBase}${path}`;
  }

  return path;
}
