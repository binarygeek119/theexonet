// Exonet / Offworld News is English-only and excluded from Weblate (AI-generated articles).
import { renderSocialLinksHtml } from "./profile-social.js?v=20260529-login";
import { API_BASE_URL, readMetaApiBase } from "./config.js";
import { mergeFriendsListForTesting } from "./admin-testing-mode.js?v=20260529-testing-mode-server";

const BOOKMARKS = [
  { slug: "home", title: "Exonet Portal", subtitle: "Start here" },
  { slug: "trade", title: "Trade Market", subtitle: "Public market data" },
  { slug: "store", title: "Supply Store", subtitle: "Catalog and listings" },
  { slug: "shipping", title: "Shipping Authority", subtitle: "Refinery and cargo" },
  { slug: "company", title: "Company Exchange", subtitle: "Listed company names" },
  { slug: "friends", title: "Social Directory", subtitle: "Your friends online" },
  { slug: "profile", title: "Miner Profiles", subtitle: "Browse, search, and rankings" },
  { slug: "docs", title: "theexonet Archives", subtitle: "Official game docs" },
  { slug: "sites/offworld-news", title: "Offworld News", subtitle: "Daily frontier headlines" },
  { slug: "sites/void-corp", title: "VoidCorp", subtitle: "Mining equipment catalog" },
  { slug: "sites/lunar-weather", title: "Lunar Weather", subtitle: "Relay network space forecasts" },
  { slug: "sites/foreverfall-penitentiary", title: "Foreverfall Penitentiary", subtitle: "Galactic lifetime sentence registry" },
];

const PLACEHOLDER_SITES = {};

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
      } else if (currentSlug === "reporters" || currentSlug.startsWith("reporters/")) {
        const reporterTail =
          currentSlug === "reporters" ? "" : currentSlug.slice("reporters/".length);
        navigate(
          reporterTail ? `sites/offworld-news/reporters/${reporterTail}` : "sites/offworld-news/reporters",
          { pushHistory: false },
        );
        return;
      } else if (currentSlug === "docs" || currentSlug.startsWith("docs/")) {
        await renderDocs(currentSlug.split("/").slice(1)[0] || "index");
      } else if (currentSlug === "sites/offworld-news" || currentSlug.startsWith("sites/offworld-news/")) {
        await renderOffworldNews(parseOffworldNewsSlug(currentSlug));
      } else if (currentSlug === "sites/lunar-weather" || currentSlug.startsWith("sites/lunar-weather/")) {
        await renderLunarWeather(currentSlug);
      } else if (
        currentSlug === "sites/foreverfall-penitentiary" ||
        currentSlug.startsWith("sites/foreverfall-penitentiary/")
      ) {
        await renderForeverfallPenitentiary(currentSlug);
      } else if (currentSlug === "sites/void-corp" || currentSlug.startsWith("sites/void-corp/")) {
        await renderVoidCorp(currentSlug);
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
      <p class="exonet-muted">Interplanetary internet for public theexonet network services.</p>
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
      ${pageHeader("theexonet Supply Store", "store")}
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
    const friends = mergeFriendsListForTesting(
      friendsResponse,
      Boolean(getState()?.testingModeEnabled),
      Boolean(getState()?.isStaffAdmin),
    ).friends ?? [];

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
          <h3>Browse all miners</h3>
          <p class="exonet-muted">Scroll public profile summaries — A–Z, who is online, newest and oldest members, and birthdays today (only players who share their birthday). Ages are never shown in lists.</p>
          <div class="exonet-profile-browse-tabs">
            ${PROFILE_BROWSE_SORTS.map(
              (entry) =>
                `<button type="button" class="btn ghost exonet-profile-browse-tab" data-profile-browse-sort="${escapeHtml(entry.id)}">${escapeHtml(entry.label)}</button>`,
            ).join("")}
          </div>
          <p id="exonet-profile-browse-status" class="exonet-muted">Loading public profiles…</p>
          <div id="exonet-profile-browse-results"></div>
          <div class="button-row">
            <button type="button" class="btn ghost" data-profile-browse-more hidden>Load more</button>
          </div>
        </div>
        <div class="exonet-panel">
          <h3>Find an ONN reporter</h3>
          <p class="exonet-muted">Search the public reporter directory by name, handle, beat, or bureau.</p>
          <button type="button" class="btn ghost" data-nav-reporters>Browse ONN reporters →</button>
        </div>
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

      content.querySelector("[data-nav-reporters]")?.addEventListener("click", () => navigate("sites/offworld-news/reporters"));
      bindProfileBrowseControls();
      await refreshProfileBrowsePanel();
      bindProfileResultLinks(content);
      return;
    }

    const profile = await api.getPublicProfileByUsername(username);
    const avatarHtml = renderProfileAvatarMarkup(profile, pickField(profile, "username"));

    if (pickField(profile, "isReporter")) {
      const profileSlug = pickField(profile, "reporterSlug") || username;
      const onnPath = pickField(profile, "onnProfilePath") || offworldNewsReporterPath(profileSlug);
      content.innerHTML = `
        ${pageHeader(`${profile.username} · ONN`, `profile/${profileSlug}`)}
        <div class="exonet-panel exonet-profile-card exonet-profile-card-reporter">
          <div class="exonet-profile-head">
            ${avatarHtml}
            <div>
              <h3>${escapeHtml(profile.username)}</h3>
              <p class="exonet-muted">${escapeHtml(profile.profileNumber ?? "—")} · ${escapeHtml(profile.companyName ?? "Offworld News Network")}</p>
              <p>${escapeHtml(profile.mood || "On assignment.")}</p>
            </div>
          </div>
          <p><strong>About</strong><br>${escapeHtml(profile.aboutMe || "No bio published.")}</p>
          ${pickField(profile, "reportedLocationsNote") ? `<p><strong>Noteworthy locations</strong><br>${escapeHtml(pickField(profile, "reportedLocationsNote"))}</p>` : ""}
          <p><strong>Beats &amp; specialties</strong><br>${escapeHtml(profile.interests || "Nothing listed.")}</p>
          <p><strong>Desk</strong><br>${escapeHtml(profile.music || "ONN relay desk")}</p>
          <div class="exonet-reporter-directory-actions">
            <button type="button" class="btn primary" data-open-onn-profile>Open ONN bureau profile →</button>
            <button type="button" class="btn ghost" data-nav-reporter-directory>ONN reporter directory →</button>
          </div>
          <p class="exonet-muted">ONN correspondents use miner profile numbers so you can add them as friends in-game. They do not receive miner direct messages.</p>
        </div>`;
      content.querySelector("[data-open-onn-profile]")?.addEventListener("click", () => navigate(onnPath));
      content.querySelector("[data-nav-reporter-directory]")?.addEventListener("click", () => {
        navigate(offworldNewsReporterPath(profileSlug));
      });
      return;
    }

    const publicInfo = formatProfilePublicInfo(profile);
    content.innerHTML = `
      ${pageHeader(`${pickField(profile, "username")}`, `profile/${pickField(profile, "username")}`)}
      <div class="exonet-panel exonet-profile-card">
        <div class="exonet-profile-head">
          ${avatarHtml}
          <div>
            <h3>${escapeHtml(profile.username)}</h3>
            <p class="exonet-muted">${escapeHtml(profile.profileNumber ?? "—")}</p>
            ${renderExonetCompanyLine(profile, "Unknown mine")}
            <p>${escapeHtml(profile.mood || "Ready to mine.")}</p>
          </div>
        </div>
        <p><strong>About</strong><br>${escapeHtml(profile.aboutMe || "No bio published.")}</p>
        <p><strong>Interests</strong><br>${escapeHtml(profile.interests || "Nothing listed.")}</p>
        <p><strong>Music</strong><br>${escapeHtml(profile.music || "Silence in the void.")}</p>
        ${renderProfileJobsMarkup(profile)}
        ${publicInfo ? `<p class="exonet-muted">${escapeHtml(publicInfo)}</p>` : ""}
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

  function renderExonetStaffBadgeMarkup(profileOrEntry) {
    if (pickField(profileOrEntry, "isStaffAdmin")) {
      return '<span class="exonet-profile-staff-badge exonet-profile-staff-badge-admin">Admin</span>';
    }
    if (pickField(profileOrEntry, "isStaffModerator")) {
      return '<span class="exonet-profile-staff-badge exonet-profile-staff-badge-moderator">Moderator</span>';
    }
    return "";
  }

  function formatExonetJobEntry(entry) {
    if (!entry) {
      return "";
    }

    const title = pickField(entry, "jobTitle") || pickField(entry, "JobTitle") || "";
    if (!title) {
      return "";
    }

    const started = pickField(entry, "startedAtUtc") || pickField(entry, "StartedAtUtc");
    const ended = pickField(entry, "endedAtUtc") || pickField(entry, "EndedAtUtc");
    const startedLabel = started ? new Date(started).toLocaleDateString() : "";
    const endedLabel = ended ? new Date(ended).toLocaleDateString() : "";
    if (startedLabel && endedLabel) {
      return `${title} (${startedLabel} – ${endedLabel})`;
    }

    if (startedLabel) {
      return `${title} (since ${startedLabel})`;
    }

    return title;
  }

  function renderProfileJobsMarkup(profile) {
    const currentJob = pickField(profile, "currentJob") || pickField(profile, "CurrentJob");
    const jobHistory = pickField(profile, "jobHistory") || pickField(profile, "JobHistory") || [];
    const currentTitle = currentJob ? formatExonetJobEntry(currentJob) : "";
    const previousJobs = jobHistory.filter((entry) => !pickField(entry, "isCurrent") && !pickField(entry, "IsCurrent"));
    const previousHtml = previousJobs.length
      ? `<ul>${previousJobs.map((entry) => `<li>${escapeHtml(formatExonetJobEntry(entry))}</li>`).join("")}</ul>`
      : "No previous postings.";

    return `
        <p><strong>Current job</strong><br>${escapeHtml(currentTitle || "No posting on file.")}</p>
        <p><strong>Previous jobs</strong><br>${previousJobs.length ? previousHtml : escapeHtml("No previous postings.")}</p>`;
  }

  function renderProfileAvatarMarkup(profileOrEntry, label) {
    const url = resolveExonetAssetUrl(pickField(profileOrEntry, "profileImageUrl"));
    const name = label ?? pickField(profileOrEntry, "username") ?? "?";
    let avatarHtml;
    if (!url) {
      avatarHtml = `<div class="exonet-profile-initials exonet-profile-initials-compact">${escapeHtml(profileInitials(name))}</div>`;
    } else {
      const initials = escapeHtml(profileInitials(name));
      avatarHtml = `<img class="exonet-profile-avatar exonet-profile-avatar-compact" src="${escapeHtml(url)}" alt="" loading="lazy" onerror="this.remove(); this.parentElement?.querySelector('.exonet-profile-initials-fallback')?.classList.remove('is-hidden');"><div class="exonet-profile-initials exonet-profile-initials-compact exonet-profile-initials-fallback is-hidden">${initials}</div>`;
    }

    const staffBadge = renderExonetStaffBadgeMarkup(profileOrEntry);
    if (!staffBadge) {
      return avatarHtml;
    }

    return `<div class="exonet-profile-avatar-stack">${avatarHtml}${staffBadge}</div>`;
  }

  function renderProfileResultsList(results) {
    return `
      <div class="exonet-grid exonet-profile-results">
        ${results
          .map(
            (entry) => {
              const publicBirthday = formatProfileBrowsePublicInfo(entry);
              const metaParts = [
                escapeHtml(entry.profileNumber ?? "—"),
                entry.isReporter ? "" : formatRaxPlain(entry.companyValue ?? 0),
                publicBirthday,
              ].filter(Boolean);
              return `
          <button type="button" class="exonet-tile exonet-profile-result-tile" data-profile="${escapeHtml(entry.username)}">
            ${entry.isReporter || entry.profileImageUrl ? renderProfileAvatarMarkup(entry, entry.username) : ""}
            <div class="exonet-profile-result-copy">
              <strong>${escapeHtml(entry.username)}${entry.isReporter ? " · ONN" : ""}${renderProfileBrowseBadges(entry)}</strong>
              <span>${escapeHtml(entry.companyName ?? (entry.isReporter ? "Offworld News Network" : "Unknown mine"))}</span>
              <span>${metaParts.join(" · ")}</span>
              <span class="exonet-profile-result-mood">${escapeHtml(entry.mood || (entry.isReporter ? "On assignment." : "Ready to mine."))}</span>
            </div>
          </button>`;
            },
          )
          .join("")}
      </div>`;
  }

  function bindProfileResultLinks(host) {
    host.querySelectorAll("[data-profile]").forEach((button) => {
      button.addEventListener("click", () => navigate(`profile/${button.dataset.profile}`));
    });
  }

  const PROFILE_BROWSE_SORTS = [
    { id: "username", label: "A–Z" },
    { id: "online", label: "Online now" },
    { id: "newest", label: "Newest" },
    { id: "oldest", label: "Oldest" },
    { id: "birthdaysToday", label: "Birthdays today" },
  ];

  const PROFILE_BROWSE_PAGE_SIZE = 50;
  let profileBrowseSort = "username";
  let profileBrowseOffset = 0;
  let profileBrowseTotal = 0;
  let profileBrowseEntries = [];

  function formatProfileMemberSince(value) {
    if (!value) {
      return "—";
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return "—";
    }

    return date.toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  }

  function renderProfileBrowseBadges(entry) {
    const badges = [];
    if (pickField(entry, "isOnline")) {
      badges.push('<span class="exonet-profile-badge exonet-profile-badge-online">Online</span>');
    }
    if (pickField(entry, "birthdayToday")) {
      badges.push('<span class="exonet-profile-badge exonet-profile-badge-birthday">Birthday</span>');
    }

    return badges.length ? `<span class="exonet-profile-badges">${badges.join("")}</span>` : "";
  }

  function formatProfileBrowsePublicInfo(entry) {
    const birthday = pickField(entry, "publicBirthday");
    return birthday ? `Birthday ${escapeHtml(birthday)}` : "";
  }

  function formatProfilePublicInfo(profile) {
    const parts = [];
    const birthday = pickField(profile, "publicBirthday");
    const age = pickField(profile, "publicAge");
    if (birthday) {
      parts.push(`Birthday ${birthday}`);
    }
    if (age != null && Number.isFinite(Number(age))) {
      parts.push(`Age ${age}`);
    }
    return parts.join(" · ");
  }

  function renderProfileBrowseMeta(entry, sort) {
    const parts = [
      escapeHtml(pickField(entry, "profileNumber") || "—"),
      formatRaxPlain(pickField(entry, "companyValue") ?? 0),
    ];
    if (sort === "newest" || sort === "oldest") {
      parts.push(`Joined ${formatProfileMemberSince(pickField(entry, "memberSince"))}`);
    }
    const publicBirthday = formatProfileBrowsePublicInfo(entry);
    if (publicBirthday) {
      parts.push(publicBirthday);
    }

    return parts.join(" · ");
  }

  function renderProfileBrowseList(entries, sort) {
    if (!entries.length) {
      return `<p class="exonet-muted">No public profiles in this list yet.</p>`;
    }

    return `
      <div class="exonet-grid exonet-profile-results">
        ${entries
          .map(
            (entry) => `
          <button type="button" class="exonet-tile exonet-profile-result-tile" data-profile="${escapeHtml(pickField(entry, "username"))}">
            ${renderProfileAvatarMarkup(entry, pickField(entry, "username"))}
            <div class="exonet-profile-result-copy">
              <strong>${escapeHtml(pickField(entry, "username"))}${renderProfileBrowseBadges(entry)}</strong>
              <span>${escapeHtml(pickField(entry, "companyName") || "Unknown mine")}</span>
              <span>${renderProfileBrowseMeta(entry, sort)}</span>
              <span class="exonet-profile-result-mood">${escapeHtml(pickField(entry, "mood") || "Ready to mine.")}</span>
            </div>
          </button>`,
          )
          .join("")}
      </div>`;
  }

  async function refreshProfileBrowsePanel({ append = false } = {}) {
    const statusEl = content.querySelector("#exonet-profile-browse-status");
    const resultsHost = content.querySelector("#exonet-profile-browse-results");
    const loadMoreBtn = content.querySelector("[data-profile-browse-more]");
    if (!resultsHost) {
      return;
    }

    if (!append) {
      profileBrowseOffset = 0;
      profileBrowseEntries = [];
    }

    if (statusEl) {
      statusEl.textContent = "Loading public profiles…";
    }

    try {
      const response = await api.browsePublicProfiles(
        profileBrowseSort,
        PROFILE_BROWSE_PAGE_SIZE,
        profileBrowseOffset,
      );
      profileBrowseTotal = Number(response.totalCount ?? 0);
      const pageEntries = response.entries ?? [];
      profileBrowseEntries = append ? [...profileBrowseEntries, ...pageEntries] : pageEntries;

      content.querySelectorAll("[data-profile-browse-sort]").forEach((button) => {
        button.classList.toggle("active", button.dataset.profileBrowseSort === profileBrowseSort);
      });

      if (statusEl) {
        const sortLabel =
          PROFILE_BROWSE_SORTS.find((entry) => entry.id === profileBrowseSort)?.label ?? "Profiles";
        statusEl.textContent =
          profileBrowseTotal === 0
            ? `No miners matched ${sortLabel.toLowerCase()}.`
            : `Showing ${profileBrowseEntries.length} of ${profileBrowseTotal} public profiles · ${sortLabel}`;
      }

      resultsHost.innerHTML = renderProfileBrowseList(profileBrowseEntries, profileBrowseSort);
      bindProfileResultLinks(resultsHost);

      if (loadMoreBtn) {
        const hasMore = profileBrowseEntries.length < profileBrowseTotal;
        loadMoreBtn.hidden = !hasMore;
      }
    } catch (error) {
      if (statusEl) {
        statusEl.textContent = error.message;
      }
      if (!append) {
        resultsHost.innerHTML = `<p class="exonet-muted">${escapeHtml(error.message)}</p>`;
      }
    }
  }

  function bindProfileBrowseControls() {
    content.querySelectorAll("[data-profile-browse-sort]").forEach((button) => {
      button.addEventListener("click", async () => {
        profileBrowseSort = button.dataset.profileBrowseSort || "username";
        await refreshProfileBrowsePanel();
      });
    });

    content.querySelector("[data-profile-browse-more]")?.addEventListener("click", async () => {
      profileBrowseOffset += PROFILE_BROWSE_PAGE_SIZE;
      await refreshProfileBrowsePanel({ append: true });
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

  function renderOffworldNewsReporterListSection(title, items) {
    const list = (items ?? [])
      .map((item) => String(item ?? "").trim())
      .filter((item) => item.length > 0);
    if (list.length === 0) {
      return "";
    }

    return `
      <div class="exonet-news-reporter-list-block">
        <h4 class="exonet-news-reporter-list-title">${escapeHtml(title)}</h4>
        <ul class="exonet-news-reporter-list">
          ${list.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}
        </ul>
      </div>`;
  }

  /** Player-facing masthead never shows internal edition source (e.g. openai / template). */
  function offworldNewsPlayerSourceLabel() {
    return "";
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
    if (parts[0] === "reporters") {
      if (parts.length === 1) {
        return { view: "reporters" };
      }
      return {
        view: "reporter",
        reporterSlug: decodeURIComponent(parts.slice(1).join("/")),
      };
    }
    return { view: "story", storyId: parts.join("/") };
  }

  function pickField(item, key) {
    if (item == null) {
      return undefined;
    }

    if (item[key] !== undefined && item[key] !== null) {
      return item[key];
    }

    const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
    return item[pascalKey];
  }

  function renderExonetCompanyLogo(profile) {
    const url = pickField(profile, "companyLogoUrl");
    if (!url) {
      return "";
    }

    return `<img class="exonet-company-logo" src="${escapeHtml(resolveExonetAssetUrl(url))}" alt="">`;
  }

  function renderExonetCompanyLine(profile, fallbackName) {
    const name = escapeHtml(profile.companyName ?? fallbackName);
    const logo = renderExonetCompanyLogo(profile);
    if (!logo) {
      return `<p class="exonet-muted">${name}</p>`;
    }

    return `<p class="exonet-company-line exonet-muted">${logo}<span>${name}</span></p>`;
  }

  function offworldNewsReporterSlug(authorName) {
    return String(authorName ?? "")
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "");
  }

  function normalizeReporterSlug(value) {
    return String(value ?? "")
      .trim()
      .toLowerCase()
      .replace(/_/g, "-")
      .replace(/\./g, "-")
      .replace(/[^a-z0-9-]+/g, "-")
      .replace(/-+/g, "-")
      .replace(/^-+|-+$/g, "");
  }

  function offworldNewsReporterPath(reporterSlug) {
    const slug = normalizeReporterSlug(reporterSlug) || String(reporterSlug ?? "").trim();
    return `sites/offworld-news/reporters/${encodeURIComponent(slug)}`;
  }

  function resolveReporterSlug(authorName, authorSlug) {
    const slugCandidate = normalizeReporterSlug(authorSlug);
    if (slugCandidate) {
      return slugCandidate;
    }

    const name = String(authorName ?? "").trim();
    if (name) {
      return offworldNewsReporterSlug(name);
    }

    return "mira-solano";
  }

  async function fetchOffworldNewsReporterDetail(reporterSlug, authorName) {
    const normalized = decodeURIComponent(String(reporterSlug ?? "")).trim();
    const candidates = [];
    const add = (value) => {
      const entry = String(value ?? "").trim();
      if (entry && !candidates.includes(entry)) {
        candidates.push(entry);
      }
    };

    add(normalized);
    add(normalizeReporterSlug(normalized));
    add(offworldNewsReporterSlug(normalized));

    const name = String(authorName ?? "").trim();
    if (name) {
      add(name);
      add(offworldNewsReporterSlug(name));
    }

    let lastError = null;
    for (const candidate of candidates) {
      try {
        return await api.getOffworldNewsReporter(candidate);
      } catch (error) {
        lastError = error;
        if (error?.status !== 404) {
          throw error;
        }
      }
    }

    throw lastError ?? new Error("Reporter not found.");
  }

  function renderNewsByline(authorName, authorSlug) {
    const name = String(authorName ?? "").trim() || "Mira Solano";
    const slug = resolveReporterSlug(name, authorSlug);
    return `<p class="exonet-news-byline">By <button type="button" class="exonet-link-btn exonet-news-reporter-link" data-news-reporter="${escapeHtml(slug)}" data-news-author="${escapeHtml(name)}" aria-label="Open ${escapeHtml(name)} ONN bureau profile">${escapeHtml(name)}</button></p>`;
  }

  function bindOffworldNewsReporterLinks(root) {
    root.querySelectorAll("[data-news-reporter]").forEach((button) => {
      button.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        const author = String(button.dataset.newsAuthor ?? "").trim();
        if (author) {
          sessionStorage.setItem("onnReporterAuthor", author);
        } else {
          sessionStorage.removeItem("onnReporterAuthor");
        }
        navigate(offworldNewsReporterPath(button.dataset.newsReporter));
      });
    });
  }

  function offworldNewsStoryPath(storyId, archiveDate = "") {
    if (archiveDate) {
      return `sites/offworld-news/archives/${archiveDate}/${storyId}`;
    }
    return `sites/offworld-news/${storyId}`;
  }

  function renderOffworldNewsMasthead(
    editionLabel,
    sourceLabel,
    storyCount,
    { showArchivesButton = true, showReportersButton = true } = {},
  ) {
    const toolbarButtons = [];
    if (showReportersButton) {
      toolbarButtons.push(
        `<button type="button" class="btn ghost exonet-news-reporters-btn" data-news-reporters>Reporter Directory</button>`,
      );
    }
    if (showArchivesButton) {
      toolbarButtons.push(
        `<button type="button" class="btn ghost exonet-news-archives-btn" data-news-archives>ONN Archives →</button>`,
      );
    }

    return `
      <div class="exonet-news-masthead">
        <div class="exonet-news-brand">OFFWORLD NEWS NETWORK</div>
        <div class="exonet-news-tagline">Independent coverage of the the frontier</div>
        ${editionLabel ? `<div class="exonet-news-edition">${escapeHtml(editionLabel)}${storyCount != null ? ` · ${storyCount} stories` : ""}${sourceLabel ? ` · ${escapeHtml(sourceLabel)}` : ""}</div>` : ""}
        ${toolbarButtons.length ? `<div class="exonet-news-toolbar">${toolbarButtons.join("")}</div>` : ""}
      </div>`;
  }

  function bindOffworldNewsArchivesButton(root) {
    root.querySelector("[data-news-archives]")?.addEventListener("click", () => navigate("sites/offworld-news/archives"));
  }

  function bindOffworldNewsReportersButton(root) {
    root.querySelector("[data-news-reporters]")?.addEventListener("click", () => navigate("sites/offworld-news/reporters"));
  }

  function bindOffworldNewsToolbar(root) {
    bindOffworldNewsArchivesButton(root);
    bindOffworldNewsReportersButton(root);
  }

  function newsTeaser(body, maxLength = 220) {
    const plain = String(body ?? "").replace(/\s+/g, " ").trim();
    if (plain.length <= maxLength) {
      return plain;
    }
    return `${plain.slice(0, maxLength).trim()}…`;
  }

  function resolveExonetAssetUrl(url) {
    if (!url) {
      return "";
    }

    if (/^https?:\/\//i.test(url)) {
      return url;
    }

    const apiBase = API_BASE_URL || readMetaApiBase();
    if (apiBase && url.startsWith("/")) {
      return `${apiBase.replace(/\/$/, "")}${url}`;
    }

    return url;
  }

  function resolveOffworldNewsImageUrl(imageUrl) {
    return resolveExonetAssetUrl(imageUrl);
  }

  function renderOnnReporterCard(reporter) {
    const slug = String(pickField(reporter, "slug") ?? "").trim();
    if (!slug) {
      return "";
    }

    const avatarUrl = resolveExonetAssetUrl(pickField(reporter, "avatarUrl"));
    const backgroundUrl = resolveExonetAssetUrl(pickField(reporter, "backgroundUrl"));
    const displayName = pickField(reporter, "displayName") ?? "ONN correspondent";
    const title = pickField(reporter, "title") ?? "";
    const beat = pickField(reporter, "beat") ?? "";
    const bureau = pickField(reporter, "bureau") ?? "";
    const teaser =
      pickField(reporter, "onnBio") ??
      pickField(reporter, "directoryBio") ??
      pickField(reporter, "directoryTeaser") ??
      "";

    return `
      <button type="button" class="exonet-reporter-directory-card" data-news-reporter="${escapeHtml(slug)}">
        <div class="exonet-reporter-directory-banner" style="background-image:url('${escapeHtml(backgroundUrl)}')"></div>
        <div class="exonet-reporter-directory-body">
          <img class="exonet-reporter-directory-avatar" src="${escapeHtml(avatarUrl)}" alt="${escapeHtml(displayName)}">
          <strong>${escapeHtml(displayName)}</strong>
          <span class="exonet-muted">${escapeHtml(title)}</span>
          <span class="exonet-muted">${escapeHtml(beat)} · ${escapeHtml(bureau)}</span>
          <span class="exonet-news-reporter-teaser">${escapeHtml(teaser)}</span>
          <span class="exonet-news-read-more">View profile →</span>
        </div>
      </button>`;
  }

  const OFFWORLD_NEWS_LOST_IMAGE = "/exonet/offworld-news/placeholders/lost-transmission.svg";

  function renderNewsImage(imageUrl, className = "exonet-news-thumb", aspect = "") {
    if (!imageUrl) {
      return "";
    }

    const resolvedUrl = resolveOffworldNewsImageUrl(imageUrl);
    const lostUrl = resolveOffworldNewsImageUrl(OFFWORLD_NEWS_LOST_IMAGE);
    const aspectClass = aspect ? ` aspect-${aspect}` : "";
    const isLost = imageUrl === OFFWORLD_NEWS_LOST_IMAGE;
    const lostAlt = "Image has been lost in transmission";

    return `
      <div class="exonet-news-image-wrap${aspectClass}${isLost ? " is-lost-transmission" : ""}">
        <img
          class="${className}"
          src="${escapeHtml(resolvedUrl)}"
          alt="${isLost ? escapeHtml(lostAlt) : ""}"
          ${
            isLost
              ? ""
              : `onerror="this.onerror=null;this.src='${lostUrl.replace(/'/g, "\\'")}';this.alt='${lostAlt.replace(/'/g, "\\'")}';this.closest('.exonet-news-image-wrap')?.classList.add('is-lost-transmission');"`
          }
        >
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
          ${renderNewsByline(pickField(story, "author"), pickField(story, "authorSlug"))}
          <span class="exonet-news-read-more">Read full story →</span>
        </div>
      </article>`;
  }

  function bindOffworldNewsStoryLinks(root) {
    root.querySelectorAll("[data-news-story]").forEach((element) => {
      const storyId = element.dataset.newsStory;
      const archiveDate = element.dataset.newsDate || "";
      const openStory = () => navigate(offworldNewsStoryPath(storyId, archiveDate));
      element.addEventListener("click", (event) => {
        if (event.target.closest("[data-news-reporter]")) {
          return;
        }
        openStory();
      });
      element.addEventListener("keydown", (event) => {
        if (event.target.closest("[data-news-reporter]")) {
          return;
        }
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          openStory();
        }
      });
    });
    bindOffworldNewsReporterLinks(root);
  }

  function renderOffworldNewsEditionPage(edition, { archiveDate = "", pageSlug, backLabel = "", backSlug = "" } = {}) {
    const stories = edition.stories ?? [];
    const editionLabel = formatNewsEditionDate(edition.editionDate ?? archiveDate);
    const sourceLabel = offworldNewsPlayerSourceLabel();
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
        <p class="exonet-news-footer">${archiveDate ? "Archived ONN relay edition" : "New edition daily at UTC midnight · Click any story for the full article"} · <button type="button" class="exonet-link-btn" data-news-reporters>Reporter Directory</button></p>
      </div>`;

    content.querySelector("[data-news-back]")?.addEventListener("click", () => navigate(backSlug));
    bindOffworldNewsToolbar(content);
    bindOffworldNewsStoryLinks(content);
    bindOffworldNewsReporterLinks(content);
  }

  async function renderOffworldNewsReporters() {
    const roster = await api.getOffworldNewsReporters();
    const reporters = pickField(roster, "reporters") ?? [];

    content.innerHTML = `
      ${pageHeader("Reporter Directory", "sites/offworld-news/reporters")}
      <div class="exonet-news-site">
        ${renderOffworldNewsMasthead("Reporter Directory", "", reporters.length, { showArchivesButton: true, showReportersButton: false })}
        <button type="button" class="exonet-news-back btn ghost" data-news-back>← Offworld News Network</button>
        <div class="exonet-panel exonet-onn-reporters-intro">
          <p class="exonet-muted">Correspondents filing for the Offworld News Network across the belt. Select a profile to read their bureau bio, miner profile, and recent stories.</p>
          <form id="exonet-onn-reporter-search" class="exonet-search-row">
            <input id="exonet-onn-reporter-query" type="search" placeholder="Search by name, beat, or bureau…" required>
            <button type="submit" class="btn primary">Search</button>
          </form>
          <div id="exonet-onn-reporter-results"></div>
        </div>
        <div class="exonet-reporter-directory-grid exonet-onn-reporter-grid">
          ${reporters.map((reporter) => renderOnnReporterCard(reporter)).join("")}
        </div>
      </div>`;

    content.querySelector("[data-news-back]")?.addEventListener("click", () => navigate("sites/offworld-news"));
    bindOffworldNewsArchivesButton(content);
    bindOffworldNewsReporterLinks(content);

    const resultsHost = content.querySelector("#exonet-onn-reporter-results");
    content.querySelector("#exonet-onn-reporter-search")?.addEventListener("submit", async (event) => {
      event.preventDefault();
      const query = content.querySelector("#exonet-onn-reporter-query")?.value?.trim();
      if (!query || !resultsHost) {
        return;
      }

      resultsHost.innerHTML = `<p class="exonet-muted">Searching roster…</p>`;
      try {
        const response = await api.getOffworldNewsReporters(query);
        const results = pickField(response, "reporters") ?? [];
        if (results.length === 1) {
          const slug = pickField(results[0], "slug");
          if (slug) {
            navigate(offworldNewsReporterPath(slug));
          }
          return;
        }
        if (results.length === 0) {
          resultsHost.innerHTML = `<p class="exonet-muted">No correspondents matched.</p>`;
          return;
        }
        resultsHost.innerHTML = `<p class="exonet-muted">${results.length} correspondents matched. Select a card below or refine your search.</p>`;
      } catch (error) {
        resultsHost.innerHTML = `<p class="exonet-muted">${escapeHtml(error.message)}</p>`;
      }
    });
  }

  async function renderOffworldNewsArchives() {
    const archives = await api.getOffworldNewsArchives();
    const editions = archives.editions ?? [];

    content.innerHTML = `
      ${pageHeader("ONN Archives", "sites/offworld-news/archives")}
      <div class="exonet-news-site">
        ${renderOffworldNewsMasthead("Past relay editions", "", null, { showArchivesButton: false, showReportersButton: true })}
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
    bindOffworldNewsToolbar(content);
    content.querySelectorAll("[data-archive-date]").forEach((button) => {
      button.addEventListener("click", () => navigate(`sites/offworld-news/archives/${button.dataset.archiveDate}`));
    });
  }

  async function renderOffworldNewsReporter(reporterSlug) {
    const pageSlug = offworldNewsReporterPath(reporterSlug);
    const authorHint = String(sessionStorage.getItem("onnReporterAuthor") ?? "").trim();
    sessionStorage.removeItem("onnReporterAuthor");

    try {
      const detail = await fetchOffworldNewsReporterDetail(reporterSlug, authorHint);
      const reporter = pickField(detail, "reporter");
      if (!reporter) {
        throw new Error("Reporter payload was incomplete.");
      }

      const resolvedSlug = String(pickField(reporter, "slug") ?? reporterSlug).trim();
      const stories = pickField(detail, "recentStories") ?? [];
      const specialties = (pickField(reporter, "specialties") ?? [])
        .map((item) => `<span class="exonet-news-reporter-tag">${escapeHtml(item)}</span>`)
        .join("");

      const displayName = pickField(reporter, "displayName") ?? "ONN correspondent";
      const personality = pickField(reporter, "personality") ?? "";
      const title = pickField(reporter, "title") ?? "";
      const beat = pickField(reporter, "beat") ?? "";
      const bureau = pickField(reporter, "bureau") ?? "";
      const bio = pickField(reporter, "onnBio") ?? pickField(reporter, "directoryBio") ?? "";
      const notableLocations = pickField(reporter, "notableLocations") ?? [];
      const notableStories = pickField(reporter, "notableStories") ?? [];
      const locationsNote = pickField(reporter, "reportedLocationsNote") ?? "";
      const locationsHtml = renderOffworldNewsReporterListSection("Noteworthy locations", notableLocations)
        || (locationsNote
          ? `<p class="exonet-news-reporter-locations"><strong>Noteworthy locations</strong><br>${escapeHtml(locationsNote)}</p>`
          : "");
      const careerHtml = renderOffworldNewsReporterListSection("Career highlights", notableStories);

      content.innerHTML = `
        ${pageHeader(displayName, pageSlug)}
        <div class="exonet-news-site">
          ${renderOffworldNewsMasthead("Reporter Directory", "", null, { showArchivesButton: true, showReportersButton: false })}
          <button type="button" class="exonet-news-back btn ghost" data-news-reporters-list>← Reporter Directory</button>
          <div class="exonet-reporter-directory-actions exonet-onn-reporter-actions">
            <button type="button" class="btn primary" data-open-miner-profile>View miner profile →</button>
          </div>
          <section class="exonet-news-reporter-profile">
            <div class="exonet-news-reporter-banner" style="background-image:url('${escapeHtml(resolveExonetAssetUrl(pickField(reporter, "backgroundUrl")))}')"></div>
            <div class="exonet-news-reporter-main">
              <img class="exonet-news-reporter-avatar" src="${escapeHtml(resolveExonetAssetUrl(pickField(reporter, "avatarUrl")))}" alt="${escapeHtml(displayName)}">
              <div class="exonet-news-reporter-copy">
                <h2 class="exonet-news-reporter-name">${escapeHtml(displayName)}</h2>
                <p class="exonet-news-reporter-personality">${escapeHtml(personality)}</p>
                <p class="exonet-news-reporter-title">${escapeHtml(title)}</p>
                <p class="exonet-news-reporter-meta">${escapeHtml(beat)} desk · ${escapeHtml(bureau)}</p>
                <p class="exonet-news-reporter-bio">${escapeHtml(bio)}</p>
                ${locationsHtml}
                ${careerHtml}
                ${specialties ? `<div class="exonet-news-reporter-tags">${specialties}</div>` : ""}
              </div>
            </div>
          </section>
          <section class="exonet-news-reporter-stories">
            <h3 class="exonet-news-reporter-stories-title">Recent ONN stories</h3>
            ${stories.length === 0
              ? `<p class="exonet-muted">No archived stories filed under this byline yet.</p>`
              : stories
                  .map((story) => {
                    const storyId = pickField(story, "storyId");
                    const editionDate = pickField(story, "editionDate");
                    const storyPath = pickField(story, "isArchive")
                      ? offworldNewsStoryPath(storyId, editionDate)
                      : offworldNewsStoryPath(storyId);
                    return `
              <button type="button" class="exonet-news-archive-row" data-news-story-link="${escapeHtml(storyPath)}">
                <span class="exonet-news-archive-date">${escapeHtml(formatNewsEditionDate(editionDate))}</span>
                <span class="exonet-news-archive-meta">${escapeHtml(pickField(story, "category") ?? "News")}</span>
                <span class="exonet-news-archive-headline">${escapeHtml(pickField(story, "headline") ?? "")}</span>
                <span class="exonet-news-read-more">Read story →</span>
              </button>`;
                  })
                  .join("")}
          </section>
        </div>`;

      content.querySelector("[data-news-reporters-list]")?.addEventListener("click", () => navigate("sites/offworld-news/reporters"));
      content.querySelector("[data-open-miner-profile]")?.addEventListener("click", () => {
        navigate(`profile/${resolvedSlug}`);
      });
      bindOffworldNewsArchivesButton(content);
      content.querySelectorAll("[data-news-story-link]").forEach((button) => {
        button.addEventListener("click", () => navigate(button.dataset.newsStoryLink));
      });
    } catch (error) {
      const message =
        error?.status === 404 || /not found/i.test(String(error?.message ?? ""))
          ? "Reporter not found on the ONN roster."
          : String(error?.message ?? "Could not load this reporter profile.");
      content.innerHTML = `
        ${pageHeader("Offworld News Network", pageSlug)}
        <div class="exonet-panel"><p class="exonet-muted">${escapeHtml(message)}</p></div>
        <button type="button" class="btn ghost" data-news-reporters-list>← Reporter Directory</button>`;
      content.querySelector("[data-news-reporters-list]")?.addEventListener("click", () => navigate("sites/offworld-news/reporters"));
    }
  }

  async function renderOffworldNews(route) {
    if (route.view === "archives") {
      await renderOffworldNewsArchives();
      return;
    }

    if (route.view === "reporters") {
      await renderOffworldNewsReporters();
      return;
    }

    if (route.view === "reporter" && route.reporterSlug) {
      await renderOffworldNewsReporter(route.reporterSlug);
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
            ${renderNewsByline(pickField(story, "author"), pickField(story, "authorSlug"))}
            <div class="exonet-news-article-body">
              ${renderNewsParagraphs(story.body)}
            </div>
          </article>
        </div>`;

      content.querySelector("[data-news-back]")?.addEventListener("click", () => navigate(backSlug));
      bindOffworldNewsToolbar(content);
      bindOffworldNewsReporterLinks(content);
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
        ${pageHeader("theexonet Archives", `docs/${normalized}`)}
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

  const LWS_ALERT_LABELS = {
    nominal: "Nominal",
    caution: "Caution",
    advisory: "Advisory",
    warning: "Warning",
    severe: "Severe",
  };

  function lwsAlertClass(level) {
    const key = String(level ?? "caution").toLowerCase();
    return `lws-alert lws-alert-${key}`;
  }

  function formatLwsObserved(iso) {
    if (!iso) {
      return "—";
    }
    try {
      return new Date(iso).toUTCString();
    } catch {
      return iso;
    }
  }

  async function renderLunarWeather(slug) {
    const segments = slug.split("/");
    const dateParam =
      segments.length >= 4 && segments[2] === "archives" ? segments[3] : segments[2] || "";

    setStatus("Fetching Lunar Weather relay bulletin…");
    const bulletin = await api.getLunarWeather(dateParam || undefined);
    const editionLabel = bulletin.bulletinDate ?? "today";
    const sourceLabel =
      bulletin.source === "openai" ? "AI relay synthesis" : "Template fallback bulletin";
    const reporting = bulletin.operationalCount ?? bulletin.readings?.length ?? 0;
    const offline = bulletin.outageCount ?? bulletin.outages?.length ?? 0;
    const pool = bulletin.relayPoolSize ?? 100;

    content.innerHTML = `
      ${pageHeader("Lunar Weather Service", slug)}
      <div class="exonet-lws-site">
        <header class="exonet-lws-masthead">
          <div class="exonet-lws-brand">LUNAR WEATHER SERVICE</div>
          <p class="exonet-lws-tagline">Hard-vacuum forecasts from ${pool} belt and deep-space relays</p>
          <p class="exonet-lws-edition">Edition ${escapeHtml(editionLabel)} · ${escapeHtml(sourceLabel)}</p>
          <div class="exonet-lws-stats">
            <span class="exonet-lws-stat reporting"><strong>${reporting}</strong> reporting</span>
            <span class="exonet-lws-stat offline"><strong>${offline}</strong> offline</span>
          </div>
        </header>
        <div class="exonet-lws-toolbar">
          <button type="button" class="btn ghost active" data-lws-tab="reporting">Reporting relays</button>
          <button type="button" class="btn ghost" data-lws-tab="offline">Offline relays</button>
        </div>
        <section class="exonet-lws-panel" data-lws-panel="reporting">
          <div class="exonet-lws-grid">
            ${(bulletin.readings ?? [])
              .map(
                (reading) => `
              <article class="exonet-lws-card">
                <div class="exonet-lws-card-head">
                  <h3>${escapeHtml(reading.relayName)}</h3>
                  <span class="${lwsAlertClass(reading.alertLevel)}">${escapeHtml(LWS_ALERT_LABELS[reading.alertLevel] ?? reading.alertLevel)}</span>
                </div>
                <p class="exonet-lws-meta">${escapeHtml(reading.region)} · ${escapeHtml(reading.sector)} · ${escapeHtml(reading.relayId)}</p>
                <p class="exonet-lws-summary">${escapeHtml(reading.summary)}</p>
                <ul class="exonet-lws-conditions">
                  ${(reading.conditions ?? []).map((c) => `<li>${escapeHtml(c)}</li>`).join("")}
                </ul>
                <dl class="exonet-lws-metrics">
                  ${reading.particleFlux ? `<div><dt>Particle flux</dt><dd>${escapeHtml(reading.particleFlux)}</dd></div>` : ""}
                  ${reading.radiationIndex ? `<div><dt>Radiation</dt><dd>${escapeHtml(reading.radiationIndex)}</dd></div>` : ""}
                  ${reading.visibility ? `<div><dt>Optics</dt><dd>${escapeHtml(reading.visibility)}</dd></div>` : ""}
                  ${reading.pressureNote ? `<div><dt>Vacuum / exosphere</dt><dd>${escapeHtml(reading.pressureNote)}</dd></div>` : ""}
                </dl>
                <p class="exonet-lws-observed">Observed ${escapeHtml(formatLwsObserved(reading.observedAt))}</p>
              </article>`,
              )
              .join("")}
          </div>
        </section>
        <section class="exonet-lws-panel" data-lws-panel="offline" hidden>
          <table class="exonet-table exonet-lws-outage-table">
            <thead>
              <tr>
                <th>Relay</th>
                <th>Region</th>
                <th>Issue</th>
                <th>Detail</th>
              </tr>
            </thead>
            <tbody>
              ${(bulletin.outages ?? [])
                .map(
                  (outage) => `
                <tr>
                  <td><strong>${escapeHtml(outage.relayName)}</strong><br><span class="exonet-muted">${escapeHtml(outage.relayId)}</span></td>
                  <td>${escapeHtml(outage.region)}</td>
                  <td>${escapeHtml(outage.issue)}</td>
                  <td>${escapeHtml(outage.detail ?? "—")}</td>
                </tr>`,
                )
                .join("")}
            </tbody>
          </table>
        </section>
      </div>`;

    content.querySelectorAll("[data-lws-tab]").forEach((button) => {
      button.addEventListener("click", () => {
        const tab = button.dataset.lwsTab;
        content.querySelectorAll("[data-lws-tab]").forEach((node) => {
          node.classList.toggle("active", node.dataset.lwsTab === tab);
        });
        content.querySelectorAll("[data-lws-panel]").forEach((panel) => {
          const show = panel.dataset.lwsPanel === tab;
          panel.hidden = !show;
        });
      });
    });

    setStatus(`LWS · ${reporting}/${pool} relays reporting · ${offline} offline`);
  }

  const FOREVERFALL_MISSING_PORTRAIT =
    "/exonet/foreverfall-penitentiary/placeholders/missing-portrait.svg";

  const VOIDCORP_MISSING_PRODUCT = "/exonet/voidcorp/placeholders/missing-product.svg";

  function voidCorpImageCaption(product) {
    if (product.imageUrl && product.source === "openai") {
      return "AI product imagery";
    }

    if (!product.imageUrl) {
      return "AI product imagery pending";
    }

    return null;
  }

  function voidCorpImageCaptionHtml(product) {
    const caption = voidCorpImageCaption(product);
    return caption
      ? `<p class="exonet-vc-image-caption">${escapeHtml(caption)}</p>`
      : "";
  }

  function resolveVoidCorpProductImageUrl(imageUrl) {
    if (!imageUrl) {
      return VOIDCORP_MISSING_PRODUCT;
    }

    return resolveExonetAssetUrl(imageUrl);
  }

  function voidCorpProductCard(product) {
    const image = resolveVoidCorpProductImageUrl(product.imageUrl);
    const fallback = escapeHtml(VOIDCORP_MISSING_PRODUCT);
    return `
      <article class="exonet-vc-card" data-vc-product="${escapeHtml(product.slug)}">
        <div class="exonet-vc-image-wrap">
          <img class="exonet-vc-image" src="${escapeHtml(image)}" alt="" loading="lazy"
            onerror="this.onerror=null;this.src='${fallback}'">
          ${voidCorpImageCaptionHtml(product)}
        </div>
        <div class="exonet-vc-card-body">
          <h3>${escapeHtml(product.displayName)}</h3>
          <p class="exonet-vc-tagline">${escapeHtml(product.tagline)}</p>
          <p class="exonet-vc-price">${formatRaxHtml(product.basePrice)} <span class="exonet-muted">MSRP</span></p>
        </div>
      </article>`;
  }

  function voidCorpProductDetail(product) {
    const image = resolveVoidCorpProductImageUrl(product.imageUrl);
    const fallback = escapeHtml(VOIDCORP_MISSING_PRODUCT);
    return `
      <article class="exonet-vc-detail">
        <div class="exonet-vc-detail-head">
          <div class="exonet-vc-image-wrap exonet-vc-image-wrap--large">
            <img class="exonet-vc-image" src="${escapeHtml(image)}" alt="" loading="lazy"
              onerror="this.onerror=null;this.src='${fallback}'">
            ${voidCorpImageCaptionHtml(product)}
          </div>
          <div>
            <p class="exonet-vc-category">${escapeHtml(product.category)}</p>
            <h2>${escapeHtml(product.displayName)}</h2>
            <p class="exonet-vc-tagline">${escapeHtml(product.tagline)}</p>
            <p class="exonet-vc-summary">${escapeHtml(product.summary)}</p>
          </div>
        </div>
        <div class="exonet-vc-copy">
          <p>${escapeHtml(product.description)}</p>
        </div>
        <dl class="exonet-vc-specs">
          <div><dt>Reference MSRP</dt><dd>${formatRaxHtml(product.basePrice)}</dd></div>
          <div><dt>Market symbol</dt><dd>${escapeHtml(product.uiSymbol ?? "—")}</dd></div>
          <div><dt>Field effect</dt><dd>${escapeHtml(product.summary)}</dd></div>
          <div><dt>Catalog ID</dt><dd>${escapeHtml(product.slug)}</dd></div>
        </dl>
      </article>`;
  }

  async function renderVoidCorp(slug) {
    const segments = slug.split("/");
    const tail = segments.slice(2);

    if (tail[0] === "product" && tail[1]) {
      setStatus("Fetching VoidCorp product…");
      const product = await api.getVoidCorpProduct(tail[1]);
      content.innerHTML = `
        ${pageHeader("VoidCorp", slug)}
        <div class="exonet-vc-site">
          <div class="exonet-vc-toolbar">
            <button type="button" class="btn ghost" data-vc-back>← Product catalog</button>
          </div>
          ${voidCorpProductDetail(product)}
        </div>`;
      content.querySelector("[data-vc-back]")?.addEventListener("click", () =>
        navigate("sites/void-corp"),
      );
      setStatus(`VoidCorp · ${product.displayName}`);
      return;
    }

    setStatus("Fetching VoidCorp catalog…");
    const catalog = await api.getVoidCorpCatalog();
    const products = catalog.products ?? [];

    content.innerHTML = `
      ${pageHeader("VoidCorp", slug)}
      <div class="exonet-vc-site">
        <header class="exonet-vc-masthead">
          <div class="exonet-vc-brand">VOIDCORP</div>
          <p class="exonet-vc-tagline-main">Frontier mining systems · Industrial supply division</p>
          <p class="exonet-vc-edition">${products.length} products in catalog · Updated ${escapeHtml(new Date(catalog.updatedAtUtc).toISOString().slice(0, 10))}</p>
        </header>
        <div class="exonet-vc-grid">
          ${products.length ? products.map(voidCorpProductCard).join("") : `<p class="exonet-muted">No products listed.</p>`}
        </div>
      </div>`;

    content.querySelectorAll("[data-vc-product]").forEach((card) => {
      card.addEventListener("click", () =>
        navigate(`sites/void-corp/product/${encodeURIComponent(card.dataset.vcProduct)}`),
      );
    });

    setStatus(`VoidCorp · ${products.length} products`);
  }

  function resolveForeverfallPortraitUrl(imageUrl) {
    if (!imageUrl) {
      return FOREVERFALL_MISSING_PORTRAIT;
    }

    return resolveExonetAssetUrl(imageUrl);
  }

  function foreverfallInmateCard(inmate) {
    const portrait = resolveForeverfallPortraitUrl(inmate.imageUrl);
    const fallback = escapeHtml(FOREVERFALL_MISSING_PORTRAIT);
    return `
      <article class="exonet-ffp-card" data-ffp-inmate="${escapeHtml(inmate.id)}">
        <div class="exonet-ffp-portrait-wrap">
          <img class="exonet-ffp-portrait" src="${escapeHtml(portrait)}" alt="" loading="lazy"
            onerror="this.onerror=null;this.src='${fallback}'">
        </div>
        <div class="exonet-ffp-card-body">
          <h3>${escapeHtml(inmate.displayName)}</h3>
          <p class="exonet-ffp-meta">${escapeHtml(inmate.species)} · ${escapeHtml(inmate.gender)} wing</p>
          <p class="exonet-ffp-crime"><strong>Charge:</strong> ${escapeHtml(inmate.crime)}</p>
          <p class="exonet-ffp-reason">${escapeHtml(inmate.intakeReason)}</p>
          <p class="exonet-ffp-sentence">${escapeHtml(inmate.sentence)}</p>
        </div>
      </article>`;
  }

  function foreverfallInmateDetail(inmate) {
    const portrait = resolveForeverfallPortraitUrl(inmate.imageUrl);
    const fallback = escapeHtml(FOREVERFALL_MISSING_PORTRAIT);
    return `
      <article class="exonet-ffp-detail">
        <div class="exonet-ffp-detail-head">
          <div class="exonet-ffp-portrait-wrap exonet-ffp-portrait-wrap--large">
            <img class="exonet-ffp-portrait" src="${escapeHtml(portrait)}" alt="" loading="lazy"
              onerror="this.onerror=null;this.src='${fallback}'">
          </div>
          <div>
            <h2>${escapeHtml(inmate.displayName)}</h2>
            <p class="exonet-ffp-meta">${escapeHtml(inmate.species)} · ${escapeHtml(inmate.gender)} wing · Intake ${escapeHtml(inmate.intakeDate)}</p>
            <p class="exonet-ffp-id">Registry ${escapeHtml(inmate.id)}</p>
          </div>
        </div>
        <dl class="exonet-ffp-dossier">
          <div><dt>Charge</dt><dd>${escapeHtml(inmate.crime)}</dd></div>
          <div><dt>Sentence</dt><dd>${escapeHtml(inmate.sentence)}</dd></div>
          <div><dt>Intake reason</dt><dd>${escapeHtml(inmate.intakeReason)}</dd></div>
          <div><dt>Dossier</dt><dd>${escapeHtml(inmate.bio)}</dd></div>
        </dl>
      </article>`;
  }

  async function renderForeverfallPenitentiary(slug) {
    const segments = slug.split("/");
    const tail = segments.slice(2);

    if (tail[0] === "inmate" && tail[1]) {
      setStatus("Fetching inmate dossier…");
      const inmate = await api.getForeverfallInmate(tail[1]);
      content.innerHTML = `
        ${pageHeader("Foreverfall Penitentiary", slug)}
        <div class="exonet-ffp-site">
          <div class="exonet-ffp-toolbar">
            <button type="button" class="btn ghost" data-ffp-back>← Intake registry</button>
          </div>
          ${foreverfallInmateDetail(inmate)}
        </div>`;
      content.querySelector("[data-ffp-back]")?.addEventListener("click", () =>
        navigate("sites/foreverfall-penitentiary"),
      );
      setStatus(`Foreverfall · dossier ${inmate.id}`);
      return;
    }

    if (tail[0] === "search") {
      setStatus("Foreverfall inmate search…");
      content.innerHTML = `
        ${pageHeader("Foreverfall Penitentiary", slug)}
        <div class="exonet-ffp-site">
          <form class="exonet-ffp-search" data-ffp-search-form>
            <label>
              Search lifetime-sentence registry
              <input type="search" name="q" placeholder="Name, species, crime…" autocomplete="off">
            </label>
            <button type="submit" class="btn primary">Search</button>
          </form>
          <div class="exonet-ffp-toolbar">
            <button type="button" class="btn ghost" data-ffp-back>← Intake registry</button>
          </div>
          <div data-ffp-search-results class="exonet-ffp-search-results"></div>
        </div>`;
      const form = content.querySelector("[data-ffp-search-form]");
      const results = content.querySelector("[data-ffp-search-results]");
      content.querySelector("[data-ffp-back]")?.addEventListener("click", () =>
        navigate("sites/foreverfall-penitentiary"),
      );
      form?.addEventListener("submit", async (event) => {
        event.preventDefault();
        const q = new FormData(form).get("q")?.toString().trim() ?? "";
        if (!q) {
          results.innerHTML = `<p class="exonet-muted">Enter a search term.</p>`;
          return;
        }
        setStatus(`Searching Foreverfall registry for “${q}”…`);
        const data = await api.searchForeverfallInmates(q);
        const inmates = data.inmates ?? [];
        results.innerHTML =
          inmates.length === 0
            ? `<p class="exonet-muted">No matching inmates in the last two weeks of intake records.</p>`
            : `<p class="exonet-ffp-search-count">${inmates.length} match(es)</p>
               <div class="exonet-ffp-grid">${inmates.map(foreverfallInmateCard).join("")}</div>`;
        wireForeverfallInmateCards();
        setStatus(`Foreverfall search · ${inmates.length} result(s)`);
      });
      setStatus("Foreverfall · inmate search");
      return;
    }

    if (tail[0] === "archives") {
      const archiveDate = tail[1] ?? "";
      if (archiveDate) {
        setStatus(`Loading Foreverfall intake ${archiveDate}…`);
        const roster = await api.getForeverfallRoster(archiveDate);
        content.innerHTML = `
          ${pageHeader("Foreverfall Penitentiary", slug)}
          <div class="exonet-ffp-site">
            <header class="exonet-ffp-masthead">
              <div class="exonet-ffp-brand">FOREVERFALL PENITENTIARY</div>
              <p class="exonet-ffp-tagline">Archived intake · ${escapeHtml(archiveDate)}</p>
            </header>
            <div class="exonet-ffp-toolbar">
              <button type="button" class="btn ghost" data-ffp-archives>← Archives</button>
              <button type="button" class="btn ghost" data-ffp-home>Today’s intake</button>
            </div>
            ${renderForeverfallWings(roster)}
          </div>`;
        content.querySelector("[data-ffp-archives]")?.addEventListener("click", () =>
          navigate("sites/foreverfall-penitentiary/archives"),
        );
        content.querySelector("[data-ffp-home]")?.addEventListener("click", () =>
          navigate("sites/foreverfall-penitentiary"),
        );
        wireForeverfallInmateCards();
        setStatus(`Foreverfall archive · ${archiveDate} · ${roster.intakeCount ?? 0} inmates`);
        return;
      }

      setStatus("Loading Foreverfall archives…");
      const archives = await api.getForeverfallArchives();
      const entries = archives.rosters ?? [];
      content.innerHTML = `
        ${pageHeader("Foreverfall Penitentiary", slug)}
        <div class="exonet-ffp-site">
          <header class="exonet-ffp-masthead">
            <div class="exonet-ffp-brand">INTAKE ARCHIVES</div>
            <p class="exonet-ffp-tagline">Rolling registry history (approx. two weeks retained)</p>
          </header>
          <div class="exonet-ffp-toolbar">
            <button type="button" class="btn ghost" data-ffp-home>← Today’s intake</button>
          </div>
          <table class="exonet-table exonet-ffp-archive-table">
            <thead><tr><th>Date</th><th>Intake</th><th>Male</th><th>Female</th><th>Intake officer</th></tr></thead>
            <tbody>
              ${entries
                .map(
                  (entry) => `
                <tr class="exonet-ffp-archive-row" data-ffp-date="${escapeHtml(entry.intakeDate)}">
                  <td>${escapeHtml(entry.intakeDate)}</td>
                  <td>${entry.intakeCount ?? 0}</td>
                  <td>${entry.maleCount ?? 0}</td>
                  <td>${entry.femaleCount ?? 0}</td>
                  <td>${escapeHtml(entry.intakeOfficer ?? "—")}</td>
                </tr>`,
                )
                .join("")}
            </tbody>
          </table>
        </div>`;
      content.querySelector("[data-ffp-home]")?.addEventListener("click", () =>
        navigate("sites/foreverfall-penitentiary"),
      );
      content.querySelectorAll("[data-ffp-date]").forEach((row) => {
        row.addEventListener("click", () =>
          navigate(`sites/foreverfall-penitentiary/archives/${row.dataset.ffpDate}`),
        );
      });
      setStatus(`Foreverfall archives · ${entries.length} day(s)`);
      return;
    }

    setStatus("Fetching Foreverfall Penitentiary intake…");
    const roster = await api.getForeverfallRoster();
    const editionLabel = roster.intakeDate ?? "today";
    const sourceLabel =
      roster.source === "openai" ? "AI dossier synthesis" : roster.source === "template" ? "Template fallback intake" : "Awaiting midnight intake";
    const intake = roster.intakeCount ?? 0;

    content.innerHTML = `
      ${pageHeader("Foreverfall Penitentiary", slug)}
      <div class="exonet-ffp-site">
        <header class="exonet-ffp-masthead">
          <div class="exonet-ffp-brand">FOREVERFALL PENITENTIARY</div>
          <p class="exonet-ffp-tagline">Maximum-security black-hole prison · galactic lifetime sentences</p>
          <p class="exonet-ffp-edition">New intake — ${escapeHtml(editionLabel)} · ${escapeHtml(sourceLabel)}</p>
          <div class="exonet-ffp-stats">
            <span class="exonet-ffp-stat"><strong>${intake}</strong> new inmates</span>
            <span class="exonet-ffp-stat"><strong>${roster.maleCount ?? 0}</strong> male wing</span>
            <span class="exonet-ffp-stat"><strong>${roster.femaleCount ?? 0}</strong> female wing</span>
          </div>
        </header>
        <div class="exonet-ffp-toolbar">
          <button type="button" class="btn ghost" data-ffp-search>Search registry</button>
          <button type="button" class="btn ghost" data-ffp-archives>Intake archives</button>
        </div>
        ${renderForeverfallWings(roster)}
      </div>`;

    content.querySelector("[data-ffp-search]")?.addEventListener("click", () =>
      navigate("sites/foreverfall-penitentiary/search"),
    );
    content.querySelector("[data-ffp-archives]")?.addEventListener("click", () =>
      navigate("sites/foreverfall-penitentiary/archives"),
    );
    wireForeverfallInmateCards();
    setStatus(`Foreverfall · ${intake} new inmates · ${roster.maleCount ?? 0}M / ${roster.femaleCount ?? 0}F`);
  }

  function renderForeverfallWings(roster) {
    const males = roster.maleWing ?? [];
    const females = roster.femaleWing ?? [];
    return `
      <section class="exonet-ffp-wing">
        <h2 class="exonet-ffp-wing-title">Male Wing — New Inmates</h2>
        <div class="exonet-ffp-grid">
          ${males.length ? males.map(foreverfallInmateCard).join("") : `<p class="exonet-muted">No male-wing intake on file.</p>`}
        </div>
      </section>
      <section class="exonet-ffp-wing">
        <h2 class="exonet-ffp-wing-title">Female Wing — New Inmates</h2>
        <div class="exonet-ffp-grid">
          ${females.length ? females.map(foreverfallInmateCard).join("") : `<p class="exonet-muted">No female-wing intake on file.</p>`}
        </div>
      </section>`;
  }

  function wireForeverfallInmateCards() {
    content.querySelectorAll("[data-ffp-inmate]").forEach((card) => {
      card.addEventListener("click", () =>
        navigate(`sites/foreverfall-penitentiary/inmate/${encodeURIComponent(card.dataset.ffpInmate)}`),
      );
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

  function isExonetOpen() {
    return !modal.hidden;
  }

  function handleMouseHistoryButton(event) {
    if (!isExonetOpen()) {
      return;
    }
    if (event.button === 3) {
      event.preventDefault();
      goBack();
      return;
    }
    if (event.button === 4) {
      event.preventDefault();
      goForward();
    }
  }

  function blockBrowserHistoryMouseButtons(event) {
    if (!isExonetOpen()) {
      return;
    }
    if (event.button === 3 || event.button === 4) {
      event.preventDefault();
    }
  }

  // Mouse side buttons (back = 3, forward = 4) while Exonet is open.
  document.addEventListener("mousedown", blockBrowserHistoryMouseButtons, true);
  document.addEventListener("mouseup", handleMouseHistoryButton);
  document.addEventListener("auxclick", handleMouseHistoryButton);

  renderBookmarks();
  updateChrome();

  function reloadCurrent() {
    if (modal.hidden) {
      return;
    }

    navigate(currentSlug, { pushHistory: false });
  }

  return { open, close, navigate, reloadCurrent };
}
