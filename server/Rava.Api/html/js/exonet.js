import { renderSocialLinksHtml } from "./profile-social.js?v=20260529-login";

const BOOKMARKS = [
  { slug: "home", title: "Exonet Portal", subtitle: "Start here" },
  { slug: "trade", title: "Trade Market", subtitle: "Public market data" },
  { slug: "store", title: "Supply Store", subtitle: "Catalog and listings" },
  { slug: "shipping", title: "Shipping Authority", subtitle: "Refinery and cargo" },
  { slug: "company", title: "Company Exchange", subtitle: "Listed company names" },
  { slug: "friends", title: "Social Directory", subtitle: "Your friends online" },
  { slug: "profile", title: "Miner Profiles", subtitle: "Search and rankings" },
  { slug: "docs", title: "RAVA Archives", subtitle: "Official game docs" },
  { slug: "sites/offworld-news", title: "Offworld News", subtitle: "Daily frontier headlines" },
  { slug: "sites/void-corp", title: "VoidCorp", subtitle: "Coming soon", placeholder: true },
  { slug: "sites/lunar-weather", title: "Lunar Weather", subtitle: "Coming soon", placeholder: true },
];

const PLACEHOLDER_SITES = {
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
      } else if (currentSlug === "sites/offworld-news" || currentSlug.startsWith("sites/offworld-news/")) {
        await renderOffworldNews(parseOffworldNewsSlug(currentSlug));
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
        <p class="exonet-muted">Ship ore cargo from your mine to NPC refineries. Emergency buy back pays ${Math.round(Number(economy.emergencyBuybackRate ?? 0.5) * 100)}% of refinery value.</p>
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
      const leaderboard = await api.getPublicProfileLeaderboard("companyValue", 25);
      const comingSoonLabels = {
        gameDay: "Game day",
        workers: "Workers",
        zones: "Mine zones",
        oreStockpile: "Ore stockpile",
        runwayDays: "Cash runway",
      };

      content.innerHTML = `
        ${pageHeader("Miner Profiles", "profile")}
        <div class="exonet-panel">
          <h3>Find a miner</h3>
          <form id="exonet-profile-search" class="exonet-search-row">
            <select id="exonet-profile-mode" aria-label="Search by">
              <option value="auto">Auto detect</option>
              <option value="username">Username</option>
              <option value="profileNumber">Profile number</option>
              <option value="companyName">Company name</option>
            </select>
            <input id="exonet-profile-query" type="search" placeholder="Username, profile number, or company name…" required>
            <button type="submit" class="btn primary">Search</button>
          </form>
          <div id="exonet-profile-results"></div>
        </div>
        <div class="exonet-panel">
          <h3>Top operators by company value</h3>
          <p class="exonet-muted">Company value is Rax on hand plus ore and supply stock at base prices.</p>
          ${renderProfileLeaderboardTable(leaderboard.entries ?? [])}
        </div>
        <div class="exonet-panel">
          <h3>More leaderboards</h3>
          <p class="exonet-muted">Additional public rankings will appear here in future updates.</p>
          <ul class="exonet-coming-soon-list">
            ${(leaderboard.comingSoonSorts ?? [])
              .map((sort) => `<li>${escapeHtml(comingSoonLabels[sort] ?? sort)}</li>`)
              .join("")}
          </ul>
        </div>`;

      content.querySelector("#exonet-profile-search")?.addEventListener("submit", async (event) => {
        event.preventDefault();
        const query = content.querySelector("#exonet-profile-query")?.value?.trim();
        const mode = content.querySelector("#exonet-profile-mode")?.value ?? "auto";
        const resultsHost = content.querySelector("#exonet-profile-results");
        if (!query || !resultsHost) {
          return;
        }

        resultsHost.innerHTML = `<p class="exonet-muted">Searching relay…</p>`;
        try {
          const response = await api.searchPublicProfiles(query, mode, 20);
          const results = response.results ?? [];
          if (results.length === 1) {
            navigate(`profile/${results[0].username}`);
            return;
          }
          if (results.length === 0) {
            resultsHost.innerHTML = `<p class="exonet-muted">No public profiles matched that search.</p>`;
            return;
          }
          resultsHost.innerHTML = `
            <p class="exonet-muted">${results.length} profiles matched.</p>
            ${renderProfileResultsList(results)}`;
          bindProfileResultLinks(resultsHost);
        } catch (error) {
          resultsHost.innerHTML = `<p class="exonet-muted">${escapeHtml(error.message)}</p>`;
        }
      });

      bindProfileResultLinks(content);
      return;
    }

    const profile = await api.getPublicProfileByUsername(username);
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
            <p class="exonet-muted">${escapeHtml(profile.profileNumber ?? "—")} · ${escapeHtml(profile.companyName ?? "Unknown mine")}</p>
            <p>${escapeHtml(profile.mood || "Ready to mine.")}</p>
          </div>
        </div>
        <p><strong>About</strong><br>${escapeHtml(profile.aboutMe || "No bio published.")}</p>
        <p><strong>Interests</strong><br>${escapeHtml(profile.interests || "Nothing listed.")}</p>
        <p><strong>Music</strong><br>${escapeHtml(profile.music || "Silence in the void.")}</p>
        <div>${renderSocialLinksHtml(profile)}</div>
        <p class="exonet-muted">Day ${profile.currentGameDay ?? "—"} · Workers ${profile.workerCount ?? 0} · Zones ${profile.zoneCount ?? 0} · Company value ${formatRaxPlain(profile.companyValue ?? 0)}</p>
      </div>`;
  }

  function renderProfileLeaderboardTable(entries) {
    if (!entries.length) {
      return `<p class="exonet-muted">No public rankings available yet.</p>`;
    }

    const rows = entries
      .map(
        (entry) => `
        <tr>
          <td>#${entry.rank ?? "—"}</td>
          <td><button type="button" class="exonet-link-btn" data-profile="${escapeHtml(entry.username)}">${escapeHtml(entry.username)}</button></td>
          <td>${escapeHtml(entry.companyName ?? "—")}</td>
          <td>${escapeHtml(entry.profileNumber ?? "—")}</td>
          <td>${formatRaxHtml(entry.companyValue ?? 0)}</td>
        </tr>`,
      )
      .join("");

    return `
      <table class="exonet-table">
        <thead>
          <tr>
            <th>Rank</th>
            <th>Operator</th>
            <th>Company</th>
            <th>Profile #</th>
            <th>Company value</th>
          </tr>
        </thead>
        <tbody>${rows}</tbody>
      </table>`;
  }

  function renderProfileResultsList(results) {
    return `
      <div class="exonet-grid exonet-profile-results">
        ${results
          .map(
            (entry) => `
          <button type="button" class="exonet-tile" data-profile="${escapeHtml(entry.username)}">
            <strong>${escapeHtml(entry.username)}</strong>
            <span>${escapeHtml(entry.companyName ?? "Unknown mine")}</span>
            <span>${escapeHtml(entry.profileNumber ?? "—")} · ${formatRaxPlain(entry.companyValue ?? 0)}</span>
          </button>`,
          )
          .join("")}
      </div>`;
  }

  function bindProfileResultLinks(host) {
    host.querySelectorAll("[data-profile]").forEach((button) => {
      button.addEventListener("click", () => navigate(`profile/${button.dataset.profile}`));
    });
  }

  function formatNewsTimestamp(value) {
    if (!value) {
      return "—";
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return "—";
    }
    return date.toLocaleString(undefined, {
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    });
  }

  function formatNewsEditionDate(value) {
    if (!value) {
      return "Today";
    }
    const date = typeof value === "string" ? new Date(`${value}T12:00:00Z`) : value;
    if (Number.isNaN(date.getTime())) {
      return String(value);
    }
    return date.toLocaleDateString(undefined, {
      weekday: "short",
      year: "numeric",
      month: "short",
      day: "numeric",
      timeZone: "UTC",
    });
  }

  function parseOffworldNewsSlug(slug) {
    const parts = slug.split("/").slice(2);
    if (parts.length === 0) {
      return { view: "today" };
    }
    if (parts[0] === "archives") {
      if (parts.length === 1) {
        return { view: "archives" };
      }
      if (parts.length === 2) {
        return { view: "edition", date: parts[1] };
      }
      return { view: "story", date: parts[1], storyId: parts.slice(2).join("/") };
    }
    return { view: "story", storyId: parts.join("/") };
  }

  function offworldNewsStoryPath(storyId, archiveDate = "") {
    if (archiveDate) {
      return `sites/offworld-news/archives/${archiveDate}/${storyId}`;
    }
    return `sites/offworld-news/${storyId}`;
  }

  function renderOffworldNewsMasthead(editionLabel, sourceLabel, storyCount, { showArchivesButton = true } = {}) {
    return `
      <div class="exonet-news-masthead">
        <div class="exonet-news-brand">OFFWORLD NEWS NETWORK</div>
        <div class="exonet-news-tagline">Independent coverage of the RAVA frontier</div>
        ${editionLabel ? `<div class="exonet-news-edition">${escapeHtml(editionLabel)}${storyCount != null ? ` · ${storyCount} stories` : ""}${sourceLabel ? ` · ${escapeHtml(sourceLabel)}` : ""}</div>` : ""}
        ${showArchivesButton ? `<div class="exonet-news-toolbar"><button type="button" class="btn ghost exonet-news-archives-btn" data-news-archives>ONN Archives →</button></div>` : ""}
      </div>`;
  }

  function bindOffworldNewsArchivesButton(root) {
    root.querySelector("[data-news-archives]")?.addEventListener("click", () => navigate("sites/offworld-news/archives"));
  }

  function newsTeaser(body, maxLength = 220) {
    const plain = String(body ?? "").replace(/\s+/g, " ").trim();
    if (plain.length <= maxLength) {
      return plain;
    }
    return `${plain.slice(0, maxLength).trim()}…`;
  }

  function renderNewsImage(imageUrl, className = "exonet-news-thumb", aspect = "") {
    if (!imageUrl) {
      return "";
    }

    const aspectClass = aspect ? ` aspect-${aspect}` : "";

    return `
      <div class="exonet-news-image-wrap${aspectClass}">
        <img class="${className}" src="${escapeHtml(imageUrl)}" alt="">
      </div>`;
  }

  function renderNewsCompany(companyName) {
    if (!companyName) {
      return "";
    }

    return `<span class="exonet-news-company">${escapeHtml(companyName)}</span>`;
  }

  function renderNewsParagraphs(body) {
    return String(body ?? "")
      .split(/\n\s*\n/)
      .map((paragraph) => paragraph.trim())
      .filter(Boolean)
      .map((paragraph) => `<p>${escapeHtml(paragraph)}</p>`)
      .join("");
  }

  function renderOffworldNewsStoryCard(story, featured = false, archiveDate = "") {
    const imageClass = featured ? "exonet-news-thumb featured" : "exonet-news-thumb";
    const imageHtml = renderNewsImage(story.imageUrl, imageClass, story.imageAspect);
    const teaser = newsTeaser(story.body);
    const dateAttr = archiveDate ? ` data-news-date="${escapeHtml(archiveDate)}"` : "";

    return `
      <article class="exonet-news-card${featured ? " featured" : ""}" data-news-story="${escapeHtml(story.id)}"${dateAttr} tabindex="0" role="link">
        ${imageHtml}
        <div class="exonet-news-card-body">
          <div class="exonet-news-meta">
            <span class="exonet-news-category">${escapeHtml(story.category ?? "News")}</span>
            ${renderNewsCompany(story.companyName)}
            <span>${escapeHtml(story.location ?? "Belt Relay")}</span>
            <span>${formatNewsTimestamp(story.publishedAt)}</span>
          </div>
          <h3 class="exonet-news-headline">${escapeHtml(story.headline)}</h3>
          <p class="exonet-news-dek">${escapeHtml(story.dek ?? "")}</p>
          ${teaser ? `<p class="exonet-news-teaser">${escapeHtml(teaser)}</p>` : ""}
          <p class="exonet-news-byline">By ${escapeHtml(story.author ?? "ONN Wire Desk")}</p>
          <span class="exonet-news-read-more">Read full story →</span>
        </div>
      </article>`;
  }

  function bindOffworldNewsStoryLinks(root) {
    root.querySelectorAll("[data-news-story]").forEach((element) => {
      const storyId = element.dataset.newsStory;
      const archiveDate = element.dataset.newsDate || "";
      const openStory = () => navigate(offworldNewsStoryPath(storyId, archiveDate));
      element.addEventListener("click", openStory);
      element.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          openStory();
        }
      });
    });
  }

  function renderOffworldNewsEditionPage(edition, { archiveDate = "", pageSlug, backLabel = "", backSlug = "" } = {}) {
    const stories = edition.stories ?? [];
    const editionLabel = formatNewsEditionDate(edition.editionDate ?? archiveDate);
    const sourceLabel =
      edition.source === "openai" ? "AI newsroom edition" : "Relay bulletin edition";
    const featured = stories[0];
    const rest = stories.slice(1);

    content.innerHTML = `
      ${pageHeader("Offworld News Network", pageSlug)}
      <div class="exonet-news-site">
        ${renderOffworldNewsMasthead(editionLabel, sourceLabel, stories.length, { showArchivesButton: !archiveDate })}
        ${backSlug ? `<button type="button" class="exonet-news-back btn ghost" data-news-back>← ${escapeHtml(backLabel)}</button>` : ""}
        ${featured ? `<section class="exonet-news-featured">${renderOffworldNewsStoryCard(featured, true, archiveDate)}</section>` : ""}
        <section class="exonet-news-list">
          ${rest.map((story) => renderOffworldNewsStoryCard(story, false, archiveDate)).join("")}
        </section>
        <p class="exonet-news-footer">${archiveDate ? "Archived ONN relay edition" : "New edition daily at UTC midnight · Click any story for the full article"}</p>
      </div>`;

    content.querySelector("[data-news-back]")?.addEventListener("click", () => navigate(backSlug));
    bindOffworldNewsArchivesButton(content);
    bindOffworldNewsStoryLinks(content);
  }

  async function renderOffworldNewsArchives() {
    const archives = await api.getOffworldNewsArchives();
    const editions = archives.editions ?? [];

    content.innerHTML = `
      ${pageHeader("ONN Archives", "sites/offworld-news/archives")}
      <div class="exonet-news-site">
        ${renderOffworldNewsMasthead("Past relay editions", "", null, { showArchivesButton: false })}
        <button type="button" class="exonet-news-back btn ghost" data-news-back>← Today's edition</button>
        <section class="exonet-news-archives">
          ${editions.length === 0
            ? `<div class="exonet-panel"><p class="exonet-muted">No archived editions yet. Editions are saved daily at UTC midnight.</p></div>`
            : editions
                .map(
                  (entry) => `
            <button type="button" class="exonet-news-archive-row" data-archive-date="${escapeHtml(entry.editionDate)}">
              <span class="exonet-news-archive-date">${escapeHtml(formatNewsEditionDate(entry.editionDate))}</span>
              <span class="exonet-news-archive-meta">${entry.storyCount ?? 0} stories</span>
              <span class="exonet-news-archive-headline">${escapeHtml(entry.headline ?? "Edition available")}</span>
              <span class="exonet-news-read-more">Read edition →</span>
            </button>`,
                )
                .join("")}
        </section>
      </div>`;

    content.querySelector("[data-news-back]")?.addEventListener("click", () => navigate("sites/offworld-news"));
    content.querySelectorAll("[data-archive-date]").forEach((button) => {
      button.addEventListener("click", () => navigate(`sites/offworld-news/archives/${button.dataset.archiveDate}`));
    });
  }

  async function renderOffworldNews(route) {
    if (route.view === "archives") {
      await renderOffworldNewsArchives();
      return;
    }

    const archiveDate = route.view === "edition" || route.view === "story" ? route.date : "";
    const edition = await api.getOffworldNews(archiveDate || undefined);
    const stories = edition.stories ?? [];

    if (route.view === "story" && route.storyId) {
      const story = stories.find((entry) => entry.id === route.storyId);
      const storySlug = offworldNewsStoryPath(route.storyId, archiveDate);
      const backSlug = archiveDate ? `sites/offworld-news/archives/${archiveDate}` : "sites/offworld-news";
      const backLabel = archiveDate ? "Archived edition" : "Today's edition";

      if (!story) {
        content.innerHTML = `
          ${pageHeader("Offworld News Network", storySlug)}
          <div class="exonet-panel"><p class="exonet-muted">Story not found in this edition.</p></div>`;
        return;
      }

      content.innerHTML = `
        ${pageHeader("Offworld News Network", storySlug)}
        <div class="exonet-news-site">
          ${renderOffworldNewsMasthead(formatNewsEditionDate(edition.editionDate ?? archiveDate), "", null, { showArchivesButton: !archiveDate })}
          <button type="button" class="exonet-news-back btn ghost" data-news-back>← ${escapeHtml(backLabel)}</button>
          <article class="exonet-news-article">
            ${renderNewsImage(story.imageUrl, "exonet-news-hero", story.imageAspect)}
            <div class="exonet-news-meta">
              <span class="exonet-news-category">${escapeHtml(story.category ?? "News")}</span>
              ${renderNewsCompany(story.companyName)}
              <span>${escapeHtml(story.location ?? "Belt Relay")}</span>
              <span>${formatNewsTimestamp(story.publishedAt)}</span>
            </div>
            <h2 class="exonet-news-article-title">${escapeHtml(story.headline)}</h2>
            <p class="exonet-news-dek">${escapeHtml(story.dek ?? "")}</p>
            <p class="exonet-news-byline">By ${escapeHtml(story.author ?? "ONN Wire Desk")}</p>
            <div class="exonet-news-article-body">
              ${renderNewsParagraphs(story.body)}
            </div>
          </article>
        </div>`;

      content.querySelector("[data-news-back]")?.addEventListener("click", () => navigate(backSlug));
      bindOffworldNewsArchivesButton(content);
      return;
    }

    if (route.view === "edition" && archiveDate) {
      renderOffworldNewsEditionPage(edition, {
        archiveDate,
        pageSlug: `sites/offworld-news/archives/${archiveDate}`,
        backLabel: "ONN Archives",
        backSlug: "sites/offworld-news/archives",
      });
      return;
    }

    renderOffworldNewsEditionPage(edition, {
      pageSlug: "sites/offworld-news",
    });
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
