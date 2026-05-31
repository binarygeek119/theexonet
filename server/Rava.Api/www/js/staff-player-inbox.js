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

export function initStaffPlayerInbox({ api, els, setStatus, onUnreadChange }) {
  let inboxMessages = [];
  let selectedMessageId = null;

  function renderInbox() {
    if (!inboxMessages.length) {
      els.inbox.innerHTML = `<p class="admin-empty-note">No player messages yet.</p>`;
      els.detail.innerHTML = `<p class="admin-empty-note">Select a message to read it.</p>`;
      return;
    }

    els.inbox.innerHTML = inboxMessages
      .map(
        (message) => `
          <button
            type="button"
            class="staff-message-item ${message.isRead ? "read" : "unread"} ${message.id === selectedMessageId ? "active" : ""}"
            data-message-id="${message.id}">
            <span class="staff-message-from">${escapeHtml(message.playerUsername)}</span>
            <span class="staff-message-preview">${escapeHtml(message.body.slice(0, 100))}${message.body.length > 100 ? "…" : ""}</span>
            <span class="staff-message-date">${formatMessageDate(message.createdAt)}</span>
          </button>`
      )
      .join("");
  }

  function renderDetail(message) {
    if (!message) {
      els.detail.innerHTML = `<p class="admin-empty-note">Select a message to read it.</p>`;
      return;
    }

    els.detail.innerHTML = `
      <header class="staff-message-detail-top">
        <h3>From ${escapeHtml(message.playerUsername)}</h3>
        <p class="staff-message-detail-meta">${formatMessageDate(message.createdAt)}</p>
      </header>
      <div class="staff-message-detail-body">${escapeHtml(message.body)}</div>`;
  }

  async function selectMessage(messageId) {
    selectedMessageId = messageId;
    const message = inboxMessages.find((item) => item.id === messageId);
    renderInbox();
    renderDetail(message);

    if (!message || message.isRead) {
      return;
    }

    try {
      const updated = await api.staffMarkPlayerInboxRead(messageId);
      inboxMessages = inboxMessages.map((item) => (item.id === messageId ? updated : item));
      renderInbox();
      renderDetail(updated);
      if (onUnreadChange) {
        await onUnreadChange();
      }
    } catch (error) {
      setStatus(els.status, error.message, true);
    }
  }

  async function loadMessages() {
    setStatus(els.status, "Loading…");
    const response = await api.staffPlayerInbox();
    inboxMessages = response.messages ?? [];
    selectedMessageId = inboxMessages[0]?.id ?? null;
    renderInbox();
    renderDetail(inboxMessages.find((item) => item.id === selectedMessageId) ?? null);

    if (selectedMessageId) {
      const selected = inboxMessages.find((item) => item.id === selectedMessageId);
      if (selected && !selected.isRead) {
        await selectMessage(selectedMessageId);
      }
    }

    setStatus(els.status, `${inboxMessages.length} player message(s)`);
  }

  els.refreshBtn.addEventListener("click", () => {
    loadMessages().catch((error) => setStatus(els.status, error.message, true));
  });

  els.inbox.addEventListener("click", (event) => {
    const button = event.target.closest(".staff-message-item");
    if (!button) {
      return;
    }

    const messageId = button.dataset.messageId;
    if (messageId) {
      selectMessage(messageId).catch((error) => setStatus(els.status, error.message, true));
    }
  });

  return { loadMessages };
}
