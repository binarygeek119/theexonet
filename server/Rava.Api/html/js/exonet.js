import { renderSocialLinksHtml } from "./profile-social.js";

const BOOKMARKS = [
  { slug: "home", title: "Exonet Portal", subtitle: "Start here" },
  { slug: "trade", title: "Trade Market", subtitle: "Public market data" },
  { slug: "store", title: "Supply Store", subtitle: "Catalog and listings" },
  { slug: "shipping", title: "Shipping Authority", subtitle: "Refinery and cargo" },
  { slug: "company", title: "Company Exchange", subtitle: "Listed company names" },
  { slug: "friends", title: "Social Directory", subtitle: "Your friends online" },
  { slug: "profile", title: "Miner Profiles", subtitle: "Lookup by username" },
  { slug: "docs", title: "RAVA Archives", subtitle: "Official game docs" },
  { slug: "sites/offworld-news", title: "Offworld News", subtitle: "Coming soon", placeholder: true },
  { slug: "sites/void-corp", title: "VoidCorp", subtitle: "Coming soon", placeholder: true },
  { slug: "sites/lunar-weather", title: "Lunar Weather", subtitle: "Coming soon", placeholder: true },
];

const PLACEHOLDER_SITES = {
  "sites/offworld-news": {
    title: "Offworld News Network",
    tagline: "Signal pending from the inner system relay.",
  },
  "sites/void-corp": {
    title: "VoidCorp Holdings",
    tagline: "Corporate portal under reconstruction in orbit.",
  },
  "sites/lunar-weather": {
    title: "Lunar Weather Service",
    tagline: "Forecast arrays offline for scheduled maintenance.",
  },
};

const DOC_SLUGS = [
  { slug: "index", title: "Overview" },
  { slug: "getting-started", title: "Getting started" },
  { slug: "mining", title: "Mining" },
  { slug: "economy", title: "Economy" },
];

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function profileInitials(username) {
  const parts = String(username ?? "")
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  if (!parts.length) {
    return "??";
  }
  return parts
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

function formatItemName(value) {
  return String(value ?? "").replace(/([a-z])([A-Z])/g, "$1 $2");
}

function renderMarkdown(md) {
  const lines = String(md ?? "").split(/\r?\n/);
  let html = "";
  let inList = false;
  let inTable = false;
  let tableHeadDone = false;

  const closeList = () => {
    if (inList) {
      html += "</ul>";
      inList = false;
    }
  };

  const closeTable = () => {
    if (inTable) {
      html += "</tbody></table>";
      inTable = false;
      tableHeadDone = false;
    }
  };

  for (const rawLine of lines) {
    const line = rawLine.trimEnd();
    if (!line.trim()) {
      closeList();
      closeTable();
      continue;
    }

    if (line.startsWith("|")) {
      closeList();
      const cells = line
        .split("|")
        .slice(1, -1)
        .map((cell) => cell.trim());
      if (cells.every((cell) => /^:?-+:?$/.test(cell))) {
        continue;
      }
      if (!inTable) {
        html += "<table><tbody>";
        inTable = true;
        tableHeadDone = false;
      }
      const tag = !tableHeadDone ? "th" : "td";
      html += "<tr>";
      for (const cell of cells) {
        html += `<${tag}>${inlineMarkdown(cell)}</${tag}>`;
      }
      html += "</tr>";
      if (!tableHeadDone) {
        tableHeadDone = true;
      }
      continue;
    }

    closeTable();

    if (line.startsWith("### ")) {
      closeList();
      html += `<h3>${inlineMarkdown(line.slice(4))}</h3>`;
      continue;
    }
    if (line.startsWith("## ")) {
      closeList();
      html += `<h2>${inlineMarkdown(line.slice(3))}</h2>`;
      continue;
    }
    if (line.startsWith("# ")) {
      closeList();
      html += `<h1>${inlineMarkdown(line.slice(2))}</h1>`;
      continue;
    }
    if (line.startsWith("- ")) {
      if (!inList) {
        html += "<ul>";
        inList = true;
      }
      html += `<li>${inlineMarkdown(line.slice(2))}</li>`;
      continue;
    }

    closeList();
    html += `<p>${inlineMarkdown(line)}</p>`;
  }

  closeList();
  closeTable();
  return html;
}

function inlineMarkdown(text) {
  let out = escapeHtml(text);
  out = out.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
  out = out.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_match, label, href) => {
    const safeHref = escapeHtml(href);
    return `<a href="${safeHref}" target="_blank" rel="noopener noreferrer">${escapeHtml(label)}</a>`;
  });
  return out;
}

export function initExonet({ api, getState, formatRaxHtml, formatRaxPlain, formatMarketSource }) {
  const modal = document.getElementById("exonet-modal");
  const content = document.getElementById("exonet-content");
  const urlInput = document.getElementById("exonet-url-input");
  const urlForm = document.getElementById("exonet-url-form");
  const backBtn = document.getElementById("exonet-back");
  const forwardBtn = document.getElementById("exonet-forward");
  const homeBtn = document.getElementById("exonet-home");
  const closeBtn = document.getElementById("exonet-close");
  const bookmarkList = document.getElementById("exonet-bookmarks");
  const statusBar = document.getElementById("exonet-status-bar");

  if (!modal || !content) {
    return { open: () => {}, close: () => {} };
  }

  let currentSlug = "home";
  const history = ["home"];
  let historyIndex = 0;

  function setStatus(message) {
    if (statusBar) {
      statusBar.textContent = message;
    }
  }

  function slugToDisplay(slug) {
    return slug || "home";
  }

  function updateChrome() {
    urlInput.value = slugToDisplay(currentSlug);
    backBtn.disabled = historyIndex <= 0;
    forwardBtn.disabled = historyIndex >= history.length - 1;
    bookmarkList?.querySelectorAll(".exonet-bookmark").forEach((button) => {
      button.classList.toggle("active", button.dataset.slug === currentSlug.split("/")[0]
        || button.dataset.slug === currentSlug);
    });
  }

  function navigate(slug, { pushHistory = true } = {}) {
    currentSlug = slug.replace(/^exo:\/\//, "").replace(/^\/+/, "") || "home";
    if (pushHistory) {
      history.splice(historyIndex + 1);
      history.push(currentSlug);
      historyIndex = history.length - 1;
    }
    updateChrome();
    renderCurrentPage();
  }

  function goBack() {
    if (historyIndex <= 0) {
      return;
    }
    historyIndex -= 1;
    currentSlug = history[historyIndex];
    updateChrome();
    renderCurrentPage();
  }

  function goForward() {
    if (historyIndex >= history.length - 1) {
      return;
    }
    historyIndex += 1;
    currentSlug = history[historyIndex];
    updateChrome();
    renderCurrentPage();
  }

  function pageHeader(title, slug) {
    return `
      <h1 class="exonet-page-title">${escapeHtml(title)}</h1>
      <p class="exonet-page-url">exo://${escapeHtml(slug)}</p>`;
  }

  async function renderCurrentPage() {
    setStatus(`Fetching exo://${currentSlug} …`);
    content.innerHTML = `<p class="exonet-muted">Establishing interplanetary link…</p>`;

    try {
      if (currentSlug === "home") {
        await renderHome();
      } else if (currentSlug === "trade") {
        await renderTrade();
      } else if (currentSlug === "store") {
        await renderStore();
      } else if (currentSlug === "shipping") {
        await renderShipping();
      } else if (currentSlug === "company") {
        await renderCompany();
      } else if (currentSlug === "friends") {
        await renderFriends();
      } else if (currentSlug === "profile" || currentSlug.startsWith("profile/")) {
        await renderProfile(currentSlug.split("/").slice(1).join("/"));
      } else if (currentSlug === "docs" || currentSlug.startsWith("docs/")) {
        await renderDocs(currentSlug.split("/").slice(1)[0] || "index");
      } else if (PLACEHOLDER_SITES[currentSlug]) {
        renderPlaceholder(currentSlug);
      } else {
        renderNotFound();
      }
      setStatus(`Connected · exo://${currentSlug} · interplanetary relay stable`);
    } catch (error) {
      content.innerHTML = `
        ${pageHeader("Transmission Error", currentSlug)}
        <div class="exonet-panel"><p class="exonet-muted">${escapeHtml(error.message)}</p></div>`;
      setStatus(`Link fault · ${error.message}`);
    }
  }

  async function renderHome() {
    content.innerHTML = `
      ${pageHeader("Exonet Portal", "home")}
      <p class="exonet-muted">Interplanetary internet for public RAVA network services.</p>
      <div class="exonet-grid">
        ${BOOKMARKS.filter((item) => !item.placeholder)
          .map(
            (item) => `
          <button type="button" class="exonet-tile" data-nav="${escapeHtml(item.slug)}">
            <strong>${escapeHtml(item.title)}</strong>
            <span>${escapeHtml(item.subtitle)}</span>
          </button>`,
          )
          .join("")}
      </div>`;

    content.querySelectorAll("[data-nav]").forEach((button) => {
      button.addEventListener("click", () => navigate(button.dataset.nav));
    });
  }

  async function renderTrade() {
    const [economy, marketInfo, auctions] = await Promise.all([
      api.getPublicEconomy(),
      api.getTradeMarketInfo(),
      api.getTradeAuctions(),
    ]);

    const supplyRows = (economy.supplyPrices ?? [])
      .map(
        (item) => `
        <tr>
          <td>${escapeHtml(formatItemName(item.itemType))}</td>
          <td>${formatRaxHtml(item.price)}</td>
          <td>${formatRaxHtml(item.basePrice)}</td>
          <td>${item.changePct != null ? `${Number(item.changePct).toFixed(2)}%` : "—"}</td>
        </tr>`,
      )
      .join("");

    const auctionRows = (auctions.auctions ?? []).slice(0, 12).map((auction) => `
      <tr>
        <td>${escapeHtml(auction.displayName)} × ${Number(auction.quantity).toFixed(1)}</td>
        <td>${escapeHtml(auction.sellerUsername)}</td>
        <td>${formatRaxHtml(auction.startPrice)}</td>
        <td>${auction.currentBid != null ? formatRaxHtml(auction.currentBid) : "—"}</td>
        <td>${escapeHtml(auction.status)}</td>
      </tr>`).join("");

    content.innerHTML = `
      ${pageHeader("Trade Market Registry", "trade")}
      <div class="exonet-panel">
        <h3>Trade Market value</h3>
        <p>${formatRaxHtml(marketInfo.tradeMarketValue)} · Auction fee ${marketInfo.auctionFeePercent}% of completed sales</p>
      </div>
      <div class="exonet-panel">
        <h3>Earth-linked supply prices</h3>
        <p class="exonet-muted">Game day ${economy.referenceGameDay} · ${escapeHtml(formatMarketSource(economy.marketSource))} · ${escapeHtml(economy.marketDate)} UTC</p>
        <table class="exonet-table">
          <thead><tr><th>Supply</th><th>Today</th><th>Base</th><th>Change</th></tr></thead>
          <tbody>${supplyRows || `<tr><td colspan="4">No supply prices available.</td></tr>`}</tbody>
        </table>
      </div>
      <div class="exonet-panel">
        <h3>Live player auctions</h3>
        <table class="exonet-table">
          <thead><tr><th>Lot</th><th>Seller</th><th>Start</th><th>Current bid</th><th>Status</th></tr></thead>
          <tbody>${auctionRows || `<tr><td colspan="5">No active auctions.</td></tr>`}</tbody>
        </table>
      </div>`;
  }

  async function renderStore() {
    const [economy, listings] = await Promise.all([
      api.getPublicEconomy(),
      api.getCompanyNameListings(),
    ]);

    const supplyRows = (economy.supplyPrices ?? [])
      .map(
        (item) => `
        <tr>
          <td>${escapeHtml(formatItemName(item.itemType))}</td>
          <td>${formatRaxHtml(item.price)}</td>
        </tr>`,
      )
      .join("");

    const listingRows = (listings.listings ?? [])
      .map(
        (listing) => `
        <tr>
          <td>${escapeHtml(listing.companyName)}</td>
          <td>${escapeHtml(listing.sellerUsername)}</td>
          <td>${formatRaxHtml(listing.price)}</td>
        </tr>`,
      )
      .join("");

    content.innerHTML = `
      ${pageHeader("RAVA Supply Store", "store")}
      <div class="exonet-panel">
        <h3>Public supply catalog</h3>
        <p class="exonet-muted">Buy supplies in-game from the Store panel. Prices track the Earth market feed.</p>
        <table class="exonet-table">
          <thead><tr><th>Supply</th><th>Store price</th></tr></thead>
          <tbody>${supplyRows || `<tr><td colspan="2">No listings available.</td></tr>`}</tbody>
        </table>
      </div>
      <div class="exonet-panel">
        <h3>Company name marketplace</h3>
        <table class="exonet-table">
          <thead><tr><th>Company</th><th>Seller</th><th>Ask</th></tr></thead>
          <tbody>${listingRows || `<tr><td colspan="3">No company names listed.</td></tr>`}</tbody>
        </table>
      </div>`;
  }

  async function renderShipping() {
    const economy = await api.getPublicEconomy();
    const oreRows = (economy.orePrices ?? [])
      .map(
        (item) => `
        <tr>
          <td>${escapeHtml(formatItemName(item.itemType))}</td>
          <td>${formatRaxHtml(item.price)}</td>
          <td>${escapeHtml(item.note ?? "—")}</td>
        </tr>`,
      )
      .join("");

    content.innerHTML = `
      ${pageHeader("Orbital Shipping Authority", "shipping")}
      <div class="exonet-panel">
        <h3>NPC refinery schedule</h3>
        <p class="exonet-muted">Ship ore cargo from your mine to NPC refineries. Emergency buyback pays ${Math.round(Number(economy.emergencyBuybackRate ?? 0.5) * 100)}% of refinery value.</p>
        <table class="exonet-table">
          <thead><tr><th>Ore</th><th>Refinery price</th><th>Notes</th></tr></thead>
          <tbody>${oreRows || `<tr><td colspan="3">No ore prices available.</td></tr>`}</tbody>
        </table>
      </div>
      <div class="exonet-panel">
        <h3>Cargo advisory</h3>
        <p class="exonet-muted">Use the in-game Shipping panel to load cargo from your inventory. Extracted ore must be in your cargo hold before dispatch.</p>
      </div>`;
  }

  async function renderCompany() {
    const listings = await api.getCompanyNameListings();
    const rows = (listings.listings ?? [])
      .map(
        (listing) => `
        <tr>
          <td><strong>${escapeHtml(listing.companyName)}</strong></td>
          <td>${escapeHtml(listing.sellerUsername)}</td>
          <td>${formatRaxHtml(listing.price)}</td>
        </tr>`,
      )
      .join("");

    content.innerHTML = `
      ${pageHeader("Company Name Exchange", "company")}
      <div class="exonet-panel">
        <h3>Public company listings</h3>
        <p class="exonet-muted">Unique mine company names listed for sale by other commanders.</p>
        <table class="exonet-table">
          <thead><tr><th>Company</th><th>Commander</th><th>Listing price</th></tr></thead>
          <tbody>${rows || `<tr><td colspan="3">No public company listings.</td></tr>`}</tbody>
        </table>
      </div>`;
  }

  async function renderFriends() {
    const friendsResponse = await api.getFriends();
    const friends = friendsResponse.friends ?? [];

    if (!friends.length) {
      content.innerHTML = `
        ${pageHeader("Social Directory", "friends")}
        <div class="exonet-panel"><p class="exonet-muted">No accepted friends yet. Add friends in-game to browse their public profiles here.</p></div>`;
      return;
    }

    content.innerHTML = `
      ${pageHeader("Social Directory", "friends")}
      <div class="exonet-grid">
        ${friends
          .map(
            (friend) => `
          <button type="button" class="exonet-tile" data-profile="${escapeHtml(friend.username)}">
            <strong>${escapeHtml(friend.username)}</strong>
            <span>${escapeHtml(friend.profileNumber ?? "Profile")}</span>
          </button>`,
          )
          .join("")}
      </div>`;

    content.querySelectorAll("[data-profile]").forEach((button) => {
      button.addEventListener("click", () => navigate(`profile/${button.dataset.profile}`));
    });
  }

  async function renderProfile(username) {
    if (!username) {
      content.innerHTML = `
        ${pageHeader("Miner Profiles", "profile")}
        <form id="exonet-profile-search" class="exonet-search-row">
          <input id="exonet-profile-query" type="search" placeholder="Search username…" required>
          <button type="submit" class="btn primary">Open profile</button>
        </form>
        <p class="exonet-muted">Public profile data relayed from the RAVA network.</p>`;

      content.querySelector("#exonet-profile-search")?.addEventListener("submit", (event) => {
        event.preventDefault();
        const query = content.querySelector("#exonet-profile-query")?.value?.trim();
        if (query) {
          navigate(`profile/${query}`);
        }
      });
      return;
    }

    const profile = await api.getProfileByUsername(username);
    const avatarHtml = profile.profileImageUrl
      ? `<img class="exonet-profile-avatar" src="${escapeHtml(profile.profileImageUrl)}" alt="">`
      : `<div class="exonet-profile-initials">${escapeHtml(profileInitials(profile.username))}</div>`;

    content.innerHTML = `
      ${pageHeader(`${profile.username}`, `profile/${profile.username}`)}
      <div class="exonet-panel exonet-profile-card">
        <div class="exonet-profile-head">
          ${avatarHtml}
          <div>
            <h3>${escapeHtml(profile.username)}</h3>
            <p class="exonet-muted">${escapeHtml(profile.profileNumber ?? "—")} · ${escapeHtml(profile.mineName ?? "Unknown mine")}</p>
            <p>${escapeHtml(profile.mood || "Ready to mine.")}</p>
          </div>
        </div>
        <p><strong>About</strong><br>${escapeHtml(profile.aboutMe || "No bio published.")}</p>
        <p><strong>Interests</strong><br>${escapeHtml(profile.interests || "Nothing listed.")}</p>
        <p><strong>Music</strong><br>${escapeHtml(profile.music || "Silence in the void.")}</p>
        <div>${renderSocialLinksHtml(profile)}</div>
        <p class="exonet-muted">Day ${profile.currentGameDay ?? "—"} · Workers ${profile.workerCount ?? 0} · Zones ${profile.zoneCount ?? 0} · Balance ${formatRaxPlain(profile.credits ?? 0)}</p>
      </div>`;
  }

  async function renderDocs(docSlug) {
    const normalized = docSlug.replace(/\.md$/i, "").replace(/^\/+/, "") || "index";
    const docMeta = DOC_SLUGS.find((doc) => doc.slug === normalized) ?? { slug: normalized, title: normalized };

    let markdown = "";
    try {
      const response = await fetch(`/exonet/docs/${encodeURIComponent(normalized)}.md`);
      if (!response.ok) {
        throw new Error(`Document not found (${response.status})`);
      }
      markdown = await response.text();
    } catch (error) {
      content.innerHTML = `
        ${pageHeader("RAVA Archives", `docs/${normalized}`)}
        <div class="exonet-panel"><p class="exonet-muted">${escapeHtml(error.message)}</p></div>`;
      return;
    }

    content.innerHTML = `
      ${pageHeader(docMeta.title, `docs/${normalized}`)}
      <div class="exonet-panel">
        <div class="exonet-grid" style="margin-bottom:16px">
          ${DOC_SLUGS.map(
            (doc) => `
            <button type="button" class="exonet-tile" data-doc="${escapeHtml(doc.slug)}">
              <strong>${escapeHtml(doc.title)}</strong>
              <span>exo://docs/${escapeHtml(doc.slug)}</span>
            </button>`,
          ).join("")}
        </div>
        <div class="exonet-doc-body">${renderMarkdown(markdown)}</div>
      </div>`;

    content.querySelectorAll("[data-doc]").forEach((button) => {
      button.addEventListener("click", () => navigate(`docs/${button.dataset.doc}`));
    });
  }

  function renderPlaceholder(slug) {
    const site = PLACEHOLDER_SITES[slug];
    content.innerHTML = `
      ${pageHeader(site.title, slug)}
      <div class="exonet-placeholder">
        <strong>${escapeHtml(site.title)}</strong>
        <p>${escapeHtml(site.tagline)}</p>
        <p>This Exonet destination will be expanded in a future update.</p>
      </div>`;
  }

  function renderNotFound() {
    content.innerHTML = `
      ${pageHeader("Unknown Destination", currentSlug)}
      <div class="exonet-panel"><p class="exonet-muted">No Exonet route matched this address.</p></div>`;
  }

  function renderBookmarks() {
    if (!bookmarkList) {
      return;
    }

    bookmarkList.innerHTML = BOOKMARKS.map(
      (item) => `
      <button type="button" class="exonet-bookmark" data-slug="${escapeHtml(item.slug)}">
        ${escapeHtml(item.title)}
        <small>${escapeHtml(item.subtitle)}</small>
      </button>`,
    ).join("");

    bookmarkList.querySelectorAll(".exonet-bookmark").forEach((button) => {
      button.addEventListener("click", () => navigate(button.dataset.slug));
    });
  }

  function open(initialSlug = "home") {
    modal.hidden = false;
    history.length = 0;
    historyIndex = -1;
    navigate(initialSlug);
  }

  function close() {
    modal.hidden = true;
  }

  urlForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    navigate(urlInput.value.trim() || "home");
  });
  backBtn?.addEventListener("click", goBack);
  forwardBtn?.addEventListener("click", goForward);
  homeBtn?.addEventListener("click", () => navigate("home"));
  closeBtn?.addEventListener("click", close);
  modal.addEventListener("click", (event) => {
    if (event.target === modal) {
      close();
    }
  });

  renderBookmarks();
  updateChrome();

  return { open, close, navigate };
}
