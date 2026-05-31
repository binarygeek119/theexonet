const TOKEN_KEY = "rava_token";
const MINE_KEY = "rava_mineId";
const USER_KEY = "rava_username";

export class RavaApi {
  constructor(baseUrl = "") {
    this.baseUrl = baseUrl.replace(/\/$/, "");
    this.token = localStorage.getItem(TOKEN_KEY);
    this.mineId = localStorage.getItem(MINE_KEY);
    this.username = localStorage.getItem(USER_KEY);
  }

  get isAuthenticated() {
    return Boolean(this.token && this.mineId);
  }

  reloadFromStorage() {
    this.token = localStorage.getItem(TOKEN_KEY);
    this.mineId = localStorage.getItem(MINE_KEY);
    this.username = localStorage.getItem(USER_KEY);
  }

  saveAuth(response) {
    this.token = response.token;
    this.username = response.username;
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, response.username);
    if (response.mineId) {
      this.mineId = response.mineId;
      localStorage.setItem(MINE_KEY, response.mineId);
    }
  }

  applySession(response) {
    if (response?.mineId) {
      this.mineId = response.mineId;
      localStorage.setItem(MINE_KEY, response.mineId);
    }
    if (response?.username) {
      this.username = response.username;
      localStorage.setItem(USER_KEY, response.username);
    }
  }

  async restoreMineIdIfNeeded() {
    if (this.mineId || !this.token) {
      return;
    }

    const session = await this.getSession();
    this.applySession(session);
  }

  clearAuth() {
    this.token = null;
    this.mineId = null;
    this.username = null;
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(MINE_KEY);
    localStorage.removeItem(USER_KEY);
  }

  async request(path, { method = "GET", body, auth = true } = {}) {
    const headers = { Accept: "application/json" };
    if (body !== undefined) {
      headers["Content-Type"] = "application/json";
    }
    if (auth && this.token) {
      headers.Authorization = `Bearer ${this.token}`;
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });

    if (!response.ok) {
      let message = formatHttpError(response);
      let code = null;
      try {
        const error = await response.json();
        if (error?.message) {
          message = error.message;
        }
        if (error?.code) {
          code = error.code;
        }
      } catch {
        // ignore parse errors
      }
      const err = new Error(message);
      err.code = code;
      err.status = response.status;
      throw err;
    }

    if (response.status === 204) {
      return null;
    }

    return response.json();
  }

  register(username, email, password, birthday) {
    return this.request("/api/auth/register", {
      method: "POST",
      body: { username, email, password, birthday },
      auth: false,
    });
  }

  login(username, password) {
    return this.request("/api/auth/login", {
      method: "POST",
      body: { username, password },
      auth: false,
    });
  }

  submitBanAppeal(username, password, message) {
    return this.request("/api/auth/ban-appeal", {
      method: "POST",
      body: { username, password, message },
      auth: false,
    });
  }

  getSession() {
    return this.request("/api/auth/session");
  }

  async getStatus() {
    const response = await fetch(`${this.baseUrl}/api/status`, {
      headers: { Accept: "application/json" },
    });

    if (!response.ok) {
      throw new Error(formatHttpError(response));
    }

    return response.json();
  }

  forgotPassword(email) {
    return this.request("/api/auth/forgot-password", {
      method: "POST",
      body: { email },
      auth: false,
    });
  }

  resetPassword(token, newPassword) {
    return this.request("/api/auth/reset-password", {
      method: "POST",
      body: { token, newPassword },
      auth: false,
    });
  }

  getMine() {
    return this.request(`/api/mines/${this.mineId}`);
  }

  assignWorker(workerId, zoneId) {
    return this.request(`/api/mines/${this.mineId}/assign-worker`, {
      method: "POST",
      body: { workerId, zoneId },
    });
  }

  unassignWorker(workerId) {
    return this.assignWorker(workerId, null);
  }

  buySupply(supplyType, quantity = 5) {
    return this.request(`/api/mines/${this.mineId}/buy-supply`, {
      method: "POST",
      body: { supplyType, quantity },
    });
  }

  sellOre(oreType, quantity, emergencyBuyback = false) {
    return this.request(`/api/mines/${this.mineId}/sell-ore`, {
      method: "POST",
      body: { oreType, quantity, emergencyBuyback },
    });
  }

  advanceDay() {
    return this.request("/api/game/advance-day", { method: "POST", body: {} });
  }

  getMarket() {
    return this.request("/api/market/today");
  }

  getTradeItems() {
    return this.request("/api/trade/items", { auth: false });
  }

  getFinances() {
    return this.request("/api/player/finances");
  }

  getProfile() {
    return this.request("/api/player/profile");
  }

  getProfileByUsername(username) {
    return this.request(`/api/player/profile/user/${encodeURIComponent(username)}`);
  }

  updateProfile(payload) {
    return this.request("/api/player/profile", {
      method: "PUT",
      body: payload,
    });
  }

  async uploadProfileAvatar(file) {
    const form = new FormData();
    form.append("file", file);

    const headers = { Accept: "application/json" };
    if (this.token) {
      headers.Authorization = `Bearer ${this.token}`;
    }

    const response = await fetch(`${this.baseUrl}/api/player/profile/avatar`, {
      method: "POST",
      headers,
      body: form,
    });

    if (!response.ok) {
      let message = formatHttpError(response);
      try {
        const error = await response.json();
        if (error?.message) {
          message = error.message;
        }
      } catch {
        // ignore parse errors
      }
      throw new Error(message);
    }

    return response.json();
  }

  getFriends() {
    return this.request("/api/player/friends");
  }

  addFriend(profileNumber) {
    return this.request("/api/player/friends", {
      method: "POST",
      body: { profileNumber },
    });
  }

  acceptFriend(friendshipId) {
    return this.request(`/api/player/friends/${friendshipId}/accept`, {
      method: "POST",
      body: {},
    });
  }

  removeFriend(friendshipId) {
    return this.request(`/api/player/friends/${friendshipId}`, {
      method: "DELETE",
    });
  }

  adminAccess() {
    return this.request("/api/admin/access");
  }

  adminDashboard() {
    return this.request("/api/admin/dashboard");
  }

  adminPlayers(search = "", limit = 50) {
    const params = new URLSearchParams();
    if (search) {
      params.set("search", search);
    }
    params.set("limit", String(limit));
    return this.request(`/api/admin/players?${params.toString()}`);
  }

  adminSetCredits(playerId, credits) {
    return this.request(`/api/admin/players/${playerId}/credits`, {
      method: "PUT",
      body: { credits },
    });
  }

  adminGameCreditsConfig() {
    return this.request("/api/admin/game-credits-config");
  }

  adminSaveGameCreditsConfig(signUp, birthdayBonus) {
    return this.request("/api/admin/game-credits-config", {
      method: "PUT",
      body: { signUp, birthdayBonus },
    });
  }

  adminSpecialEvents() {
    return this.request("/api/admin/special-events");
  }

  adminCreateSpecialEvent(payload) {
    return this.request("/api/admin/special-events", {
      method: "POST",
      body: payload,
    });
  }

  adminUpdateSpecialEvent(eventId, payload) {
    return this.request(`/api/admin/special-events/${eventId}`, {
      method: "PUT",
      body: payload,
    });
  }

  adminDeleteSpecialEvent(eventId) {
    return this.request(`/api/admin/special-events/${eventId}`, {
      method: "DELETE",
    });
  }

  adminPlayerProfile(playerId) {
    return this.request(`/api/admin/players/${playerId}/profile`);
  }

  adminFlagProfile(playerId, comment) {
    return this.request(`/api/admin/players/${playerId}/flag`, {
      method: "POST",
      body: { comment },
    });
  }

  adminBanLevels() {
    return this.request("/api/admin/ban-levels");
  }

  adminBanPlayer(playerId, banLevel, reason = "") {
    return this.request(`/api/admin/players/${playerId}/ban`, {
      method: "POST",
      body: { banLevel, reason },
    });
  }

  adminUnbanPlayer(playerId) {
    return this.request(`/api/admin/players/${playerId}/unban`, {
      method: "POST",
      body: {},
    });
  }

  adminWarnPlayer(playerId, reason) {
    return this.request(`/api/admin/players/${playerId}/warn`, {
      method: "POST",
      body: { reason },
    });
  }

  adminBanAppeals() {
    return this.request("/api/admin/ban-appeals");
  }

  adminMessageLog(search = "", channel = "", limit = 100) {
    const params = new URLSearchParams();
    if (search) {
      params.set("search", search);
    }
    if (channel) {
      params.set("channel", channel);
    }
    params.set("limit", String(limit));
    return this.request(`/api/admin/message-log?${params.toString()}`);
  }

  adminDismissBanAppeal(appealId) {
    return this.request(`/api/admin/ban-appeals/${appealId}/dismiss`, {
      method: "POST",
      body: {},
    });
  }

  moderatorAccess() {
    return this.request("/api/moderator/access");
  }

  moderatorDashboard() {
    return this.request("/api/moderator/dashboard");
  }

  moderatorPlayers(search = "", limit = 50) {
    const params = new URLSearchParams();
    if (search) {
      params.set("search", search);
    }
    params.set("limit", String(limit));
    return this.request(`/api/moderator/players?${params.toString()}`);
  }

  moderatorPlayerProfile(playerId) {
    return this.request(`/api/moderator/players/${playerId}/profile`);
  }

  moderatorFlagProfile(playerId, comment) {
    return this.request(`/api/moderator/players/${playerId}/flag`, {
      method: "POST",
      body: { comment },
    });
  }

  moderatorBanLevels() {
    return this.request("/api/moderator/ban-levels");
  }

  moderatorBanPlayer(playerId, banLevel, reason = "") {
    return this.request(`/api/moderator/players/${playerId}/ban`, {
      method: "POST",
      body: { banLevel, reason },
    });
  }

  moderatorUnbanPlayer(playerId) {
    return this.request(`/api/moderator/players/${playerId}/unban`, {
      method: "POST",
      body: {},
    });
  }

  moderatorWarnPlayer(playerId, reason) {
    return this.request(`/api/moderator/players/${playerId}/warn`, {
      method: "POST",
      body: { reason },
    });
  }

  moderatorBanAppeals() {
    return this.request("/api/moderator/ban-appeals");
  }

  moderatorDismissBanAppeal(appealId) {
    return this.request(`/api/moderator/ban-appeals/${appealId}/dismiss`, {
      method: "POST",
      body: {},
    });
  }

  staffMembers() {
    return this.request("/api/staff/members");
  }

  staffMessages() {
    return this.request("/api/staff/messages");
  }

  staffUnreadCount() {
    return this.request("/api/staff/messages/unread-count");
  }

  staffSendMessage(toUsername, body) {
    return this.request("/api/staff/messages", {
      method: "POST",
      body: { toUsername, body },
    });
  }

  staffMarkMessageRead(messageId) {
    return this.request(`/api/staff/messages/${messageId}/read`, {
      method: "POST",
      body: {},
    });
  }

  staffPlayerInbox() {
    return this.request("/api/staff/player-inbox");
  }

  staffMarkPlayerInboxRead(messageId) {
    return this.request(`/api/staff/player-inbox/${messageId}/read`, {
      method: "POST",
      body: {},
    });
  }

  staffPlayerMessages(playerId) {
    return this.request(`/api/staff/players/${playerId}/messages`);
  }

  staffSendPlayerMessage(playerId, body) {
    return this.request(`/api/staff/players/${playerId}/messages`, {
      method: "POST",
      body: { body },
    });
  }

  playerMessages() {
    return this.request("/api/player/messages");
  }

  playerUnreadCount() {
    return this.request("/api/player/messages/unread-count");
  }

  playerMarkMessageRead(messageId) {
    return this.request(`/api/player/messages/${messageId}/read`, {
      method: "POST",
      body: {},
    });
  }

  peerMessages() {
    return this.request("/api/player/peer-messages");
  }

  sendPeerMessage(toPlayerId, body) {
    return this.request("/api/player/peer-messages", {
      method: "POST",
      body: { toPlayerId, body },
    });
  }

  peerMarkMessageRead(messageId) {
    return this.request(`/api/player/peer-messages/${messageId}/read`, {
      method: "POST",
      body: {},
    });
  }

  playerStaffContacts() {
    return this.request("/api/player/staff-contacts");
  }

  playerStaffMessages() {
    return this.request("/api/player/staff-messages");
  }

  sendPlayerStaffMessage(toStaffUsername, body) {
    return this.request("/api/player/staff-messages", {
      method: "POST",
      body: { toStaffUsername, body },
    });
  }
}

function formatHttpError(response) {
  if (response.status === 401) {
    return "Invalid username or password, or session expired.";
  }

  if (response.status === 403) {
    return "Access denied. Your account is not configured as an admin.";
  }

  if (response.status === 405) {
    return "API route not available. Requests must go to the API host (ravaapi), not the game site. Hard-refresh the page or redeploy html/.";
  }

  if (response.status === 503) {
    return "Database unavailable. Check PostgreSQL connection settings on the server.";
  }

  if (response.status >= 500) {
    return "Server error. Check API logs and database connection.";
  }

  return response.statusText || "Request failed.";
}
