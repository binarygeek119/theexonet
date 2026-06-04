function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function formatMessageDate(value) {
  if (!value) {
    return "—";
  }
  return new Date(value).toLocaleString();
}

function messageKindLabel(kind) {
  return kind === "staff" ? "Staff" : "Player";
}

export function initAdminMessagesHub({ api, els, setStatus, staffMessaging }) {
  let playerMessages = [];
  let selectedKey = null;
  let filter = "all";

  function staffMessages() {
    return staffMessaging.getStaffInbox?.() ?? [];
  }

  function mergedMessages() {
    const staff = staffMessages().map((message) => ({
      kind: "staff",
      id: message.id,
      createdAt: message.createdAt,
      isRead: message.isRead,
      fromLabel: message.fromUsername,
      preview: message.body,
      raw: message,
    }));

    const players = playerMessages.map((message) => ({
      kind: "player",
      id: message.id,
      createdAt: message.createdAt,
      isRead: message.isRead,
      fromLabel: message.playerUsername,
      preview: message.body,
      raw: message,
    }));

    let combined = [...staff, ...players];
    if (filter === "staff") {
      combined = staff;
    } else if (filter === "player") {
      combined = players;
    }

    return combined.sort(
      (left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime(),
    );
  }

  function messageKey(message) {
    return `${message.kind}:${message.id}`;
  }

  function renderFilterButtons() {
    els.filterButtons?.forEach((button) => {
      button.classList.toggle("active", button.dataset.messageFilter === filter);
    });
  }

  function renderInbox() {
    const messages = mergedMessages();
    if (!messages.length) {
      els.inbox.innerHTML = `<p class="admin-empty-note">No messages yet.</p>`;
      els.detail.innerHTML = `<p class="admin-empty-note">Select a message to read it.</p>`;
      return;
    }

    els.inbox.innerHTML = messages
      .map((message) => {
        const key = messageKey(message);
        return `
          <button
            type="button"
            class="staff-message-item ${message.isRead ? "read" : "unread"} ${key === selectedKey ? "active" : ""}"
            data-message-key="${escapeHtml(key)}">
            <span class="admin-message-item-head">
              <span class="admin-message-kind admin-message-kind-${message.kind}">${messageKindLabel(message.kind)}</span>
              <span class="staff-message-from">${escapeHtml(message.fromLabel)}</span>
            </span>
            <span class="staff-message-preview">${escapeHtml(message.preview.slice(0, 100))}${message.preview.length > 100 ? "…" : ""}</span>
            <span class="staff-message-date">${formatMessageDate(message.createdAt)}</span>
          </button>`;
      })
      .join("");
  }

  function renderDetail(message) {
    if (!message) {
      els.detail.innerHTML = `<p class="admin-empty-note">Select a message to read it.</p>`;
      return;
    }

    const kindLabel = messageKindLabel(message.kind);
    const direction =
      message.kind === "staff"
        ? `Staff message from ${escapeHtml(message.fromLabel)}`
        : `Player message from ${escapeHtml(message.fromLabel)}`;

    els.detail.innerHTML = `
      <header class="staff-message-detail-top">
        <div class="staff-message-detail-head">
          <div>
            <p class="admin-message-detail-kind">
              <span class="admin-message-kind admin-message-kind-${message.kind}">${kindLabel}</span>
            </p>
            <h3>${direction}</h3>
            <p class="staff-message-detail-meta">${formatMessageDate(message.createdAt)}</p>
          </div>
          <button type="button" class="btn ghost danger staff-message-delete-btn">Delete</button>
        </div>
      </header>
      <div class="staff-message-detail-body">${escapeHtml(message.preview)}</div>`;
  }

  function findMessage(key) {
    return mergedMessages().find((message) => messageKey(message) === key) ?? null;
  }

  function syncDisplay() {
    renderFilterButtons();
    renderInbox();
    renderDetail(findMessage(selectedKey));
  }

  async function selectMessage(key) {
    selectedKey = key;
    const message = findMessage(key);
    renderInbox();
    renderDetail(message);

    if (!message || message.isRead) {
      return;
    }

    try {
      if (message.kind === "staff") {
        const updated = await api.staffMarkMessageRead(message.id);
        staffMessaging.applyStaffMessageUpdate?.(updated);
      } else {
        const updated = await api.staffMarkPlayerInboxRead(message.id);
        playerMessages = playerMessages.map((item) => (item.id === message.id ? updated : item));
      }

      syncDisplay();
      await staffMessaging.refreshUnreadBadge();
    } catch (error) {
      setStatus(els.status, error.message, true);
    }
  }

  async function refresh(options = {}) {
    const { successMessage } = options;
    if (!successMessage) {
      setStatus(els.status, "Loading…");
    }
    await staffMessaging.reloadStaffInbox();
    const response = await api.staffPlayerInbox();
    playerMessages = response.messages ?? [];

    const messages = mergedMessages();
    if (!selectedKey || !findMessage(selectedKey)) {
      selectedKey = messages[0] ? messageKey(messages[0]) : null;
    }

    syncDisplay();

    const selected = findMessage(selectedKey);
    if (selected && !selected.isRead) {
      await selectMessage(selectedKey);
    }

    if (successMessage) {
      setStatus(els.status, successMessage, false);
    } else {
      updateStatusLine();
    }
    await staffMessaging.refreshUnreadBadge();
  }

  function updateStatusLine() {
    const staffCount = staffMessages().length;
    const playerCount = playerMessages.length;
    setStatus(
      els.status,
      `${staffCount + playerCount} message(s) (${staffCount} staff, ${playerCount} from players)`,
    );
  }

  async function deleteSelectedMessage() {
    const message = findMessage(selectedKey);
    if (!message) {
      return;
    }

    if (!window.confirm("Delete this message? This cannot be undone.")) {
      return;
    }

    setStatus(els.status, "Deleting…");
    try {
      let successMessage = "Message deleted.";
      if (message.kind === "staff") {
        const result = await api.staffDeleteMessage(message.id);
        successMessage = result.message ?? successMessage;
        await staffMessaging.reloadStaffInbox();
      } else {
        const result = await api.staffDeletePlayerInboxMessage(message.id);
        successMessage = result.message ?? successMessage;
        playerMessages = playerMessages.filter((item) => item.id !== message.id);
      }

      selectedKey = null;
      const messages = mergedMessages();
      selectedKey = messages[0] ? messageKey(messages[0]) : null;
      syncDisplay();
      setStatus(els.status, successMessage, false);
      await staffMessaging.refreshUnreadBadge();
    } catch (error) {
      setStatus(els.status, error.message, true);
    }
  }

  els.filterButtons?.forEach((button) => {
    button.addEventListener("click", () => {
      filter = button.dataset.messageFilter ?? "all";
      const messages = mergedMessages();
      if (selectedKey && !findMessage(selectedKey)) {
        selectedKey = messages[0] ? messageKey(messages[0]) : null;
      }
      syncDisplay();
    });
  });

  els.inbox.addEventListener("click", (event) => {
    const button = event.target.closest(".staff-message-item");
    if (!button?.dataset.messageKey) {
      return;
    }

    selectMessage(button.dataset.messageKey).catch((error) => setStatus(els.status, error.message, true));
  });

  els.detail?.addEventListener("click", (event) => {
    if (!event.target.closest(".staff-message-delete-btn")) {
      return;
    }

    deleteSelectedMessage().catch((error) => setStatus(els.status, error.message, true));
  });

  return { refresh, syncDisplay };
}
