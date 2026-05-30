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

function memberLabel(member) {
  const roles = [];
  if (member.isAdmin) {
    roles.push("admin");
  }
  if (member.isModerator) {
    roles.push("moderator");
  }
  return `${member.username} (${roles.join(", ")})`;
}

export function initStaffMessaging({ api, els, setStatus }) {
  let inboxMessages = [];
  let selectedMessageId = null;

  function renderInbox() {
    if (!inboxMessages.length) {
      els.messagesInbox.innerHTML = `<p class="admin-empty-note">No messages yet.</p>`;
      els.messagesDetail.innerHTML = `<p class="admin-empty-note">Select a message to read it.</p>`;
      return;
    }

    els.messagesInbox.innerHTML = inboxMessages
      .map(
        (message) => `
          <button
            type="button"
            class="staff-message-item ${message.isRead ? "read" : "unread"} ${message.id === selectedMessageId ? "active" : ""}"
            data-message-id="${message.id}">
            <span class="staff-message-from">${escapeHtml(message.fromUsername)}</span>
            <span class="staff-message-preview">${escapeHtml(message.body.slice(0, 100))}${message.body.length > 100 ? "…" : ""}</span>
            <span class="staff-message-date">${formatMessageDate(message.createdAt)}</span>
          </button>`
      )
      .join("");
  }

  function renderDetail(message) {
    if (!message) {
      els.messagesDetail.innerHTML = `<p class="admin-empty-note">Select a message to read it.</p>`;
      return;
    }

    els.messagesDetail.innerHTML = `
      <header class="staff-message-detail-top">
        <h3>From ${escapeHtml(message.fromUsername)}</h3>
        <p class="staff-message-detail-meta">${formatMessageDate(message.createdAt)}</p>
      </header>
      <div class="staff-message-detail-body">${escapeHtml(message.body)}</div>`;
  }

  function populateRecipients(members) {
    els.messagesRecipient.innerHTML = [
      `<option value="">Select recipient…</option>`,
      ...members.map(
        (member) =>
          `<option value="${escapeHtml(member.username)}">${escapeHtml(memberLabel(member))}</option>`
      ),
    ].join("");
  }

  async function refreshUnreadBadge() {
    if (!els.messagesNavBadge) {
      return;
    }

    try {
      const result = await api.staffUnreadCount();
      const count =
        Number(result.count ?? 0) + Number(result.playerMessageCount ?? 0);
      els.messagesNavBadge.hidden = count <= 0;
      els.messagesNavBadge.textContent = String(count);
    } catch {
      els.messagesNavBadge.hidden = true;
    }
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
      const updated = await api.staffMarkMessageRead(messageId);
      inboxMessages = inboxMessages.map((item) => (item.id === messageId ? updated : item));
      renderInbox();
      renderDetail(updated);
      await refreshUnreadBadge();
    } catch (error) {
      setStatus(els.messagesStatus, error.message, true);
    }
  }

  async function loadMessages() {
    setStatus(els.messagesStatus, "Loading…");
    const [membersResponse, messagesResponse] = await Promise.all([
      api.staffMembers(),
      api.staffMessages(),
    ]);

    inboxMessages = messagesResponse.messages ?? [];
    populateRecipients(membersResponse.members ?? []);
    selectedMessageId = inboxMessages[0]?.id ?? null;
    renderInbox();
    renderDetail(inboxMessages.find((item) => item.id === selectedMessageId) ?? null);

    if (selectedMessageId) {
      const selected = inboxMessages.find((item) => item.id === selectedMessageId);
      if (selected && !selected.isRead) {
        await selectMessage(selectedMessageId);
      }
    }

    setStatus(els.messagesStatus, `${inboxMessages.length} message(s)`);
    await refreshUnreadBadge();
  }

  async function sendMessage() {
    const toUsername = els.messagesRecipient.value;
    const body = els.messagesBody.value.trim();

    if (!toUsername) {
      setStatus(els.messagesStatus, "Select a recipient.", true);
      return;
    }

    if (!body) {
      setStatus(els.messagesStatus, "Enter a message.", true);
      return;
    }

    els.messagesSendBtn.disabled = true;
    setStatus(els.messagesStatus, "Sending…");
    try {
      const result = await api.staffSendMessage(toUsername, body);
      els.messagesBody.value = "";
      setStatus(els.messagesStatus, result.statusMessage ?? "Message sent.");
      await loadMessages();
    } catch (error) {
      setStatus(els.messagesStatus, error.message, true);
    } finally {
      els.messagesSendBtn.disabled = false;
    }
  }

  els.messagesSendBtn.addEventListener("click", () => {
    sendMessage().catch((error) => setStatus(els.messagesStatus, error.message, true));
  });

  els.messagesRefreshBtn.addEventListener("click", () => {
    loadMessages().catch((error) => setStatus(els.messagesStatus, error.message, true));
  });

  els.messagesInbox.addEventListener("click", (event) => {
    const button = event.target.closest(".staff-message-item");
    if (!button) {
      return;
    }

    const messageId = button.dataset.messageId;
    if (messageId) {
      selectMessage(messageId).catch((error) => setStatus(els.messagesStatus, error.message, true));
    }
  });

  return { loadMessages, refreshUnreadBadge };
}
