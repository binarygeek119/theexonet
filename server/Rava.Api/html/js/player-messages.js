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

function normalizeStaffMessage(message) {
  return {
    kind: "staff",
    id: message.id,
    label: message.fromStaffUsername,
    preview: message.body,
    body: message.body,
    createdAt: message.createdAt,
    isRead: message.isRead,
    isSentByMe: false,
    fromStaffUsername: message.fromStaffUsername,
  };
}

function normalizePeerMessage(message) {
  const label = message.isSentByMe
    ? `To ${message.toUsername}`
    : message.fromUsername;
  return {
    kind: "peer",
    id: message.id,
    label,
    preview: message.body,
    body: message.body,
    createdAt: message.createdAt,
    isRead: message.isRead,
    isSentByMe: message.isSentByMe,
    fromUsername: message.fromUsername,
    toUsername: message.toUsername,
    toPlayerId: message.toPlayerId,
    fromPlayerId: message.fromPlayerId,
    readAt: message.readAt,
  };
}

function normalizeToStaffMessage(message) {
  return {
    kind: "to-staff",
    id: message.id,
    label: `To ${message.toStaffUsername}`,
    preview: message.body,
    body: message.body,
    createdAt: message.createdAt,
    isRead: message.isRead,
    isSentByMe: true,
    toStaffUsername: message.toStaffUsername,
    readAt: message.readAt,
  };
}

function staffContactLabel(contact) {
  const roles = [];
  if (contact.isAdmin) {
    roles.push("admin");
  }
  if (contact.isModerator) {
    roles.push("moderator");
  }
  return `${contact.username} (${roles.join(", ")})`;
}

export function initPlayerMessaging({ api, els, setStatus }) {
  let inboxMessages = [];
  let selectedMessageId = null;
  let selectedMessageKind = null;
  let composePlayerId = null;

  function hideStaffCompose() {
    if (els.staffCompose) {
      els.staffCompose.hidden = true;
    }
    if (els.staffToggleBtn) {
      els.staffToggleBtn.hidden = false;
    }
  }

  function showStaffCompose() {
    if (els.staffCompose) {
      els.staffCompose.hidden = false;
    }
    if (els.staffToggleBtn) {
      els.staffToggleBtn.hidden = true;
    }
    els.staffRecipient?.focus();
  }

  function getMessageKey(message) {
    return `${message.kind}:${message.id}`;
  }

  function findMessage(kind, id) {
    return inboxMessages.find((item) => item.kind === kind && item.id === id) ?? null;
  }

  function getTypeLabel(message) {
    if (message.kind === "staff") {
      return "Staff";
    }
    if (message.kind === "to-staff") {
      return "Sent";
    }
    return message.isSentByMe ? "Sent" : "Friend";
  }

  function renderInbox() {
    if (!inboxMessages.length) {
      els.messagesInbox.innerHTML = `<p class="friends-status">No messages yet.</p>`;
      els.messagesDetail.innerHTML = `<p class="friends-status">Select a message to read it.</p>`;
      return;
    }

    els.messagesInbox.innerHTML = inboxMessages
      .map((message) => {
        const key = getMessageKey(message);
        const active = key === `${selectedMessageKind}:${selectedMessageId}`;
        return `
          <button
            type="button"
            class="staff-message-item ${message.isRead ? "read" : "unread"} ${active ? "active" : ""}"
            data-message-kind="${message.kind}"
            data-message-id="${message.id}">
            <span class="staff-message-from">${escapeHtml(message.label)}</span>
            <span class="staff-message-type">${escapeHtml(getTypeLabel(message))}</span>
            <span class="staff-message-preview">${escapeHtml(message.preview.slice(0, 100))}${message.preview.length > 100 ? "…" : ""}</span>
            <span class="staff-message-date">${formatMessageDate(message.createdAt)}</span>
          </button>`;
      })
      .join("");
  }

  function renderDetail(message) {
    if (!message) {
      els.messagesDetail.innerHTML = `<p class="friends-status">Select a message to read it.</p>`;
      return;
    }

    let heading;
    let meta = formatMessageDate(message.createdAt);

    if (message.kind === "staff") {
      heading = `From ${message.fromStaffUsername} (staff)`;
    } else if (message.kind === "to-staff") {
      heading = `To ${message.toStaffUsername} (staff)`;
      meta = message.readAt
        ? `${meta} · read ${formatMessageDate(message.readAt)}`
        : `${meta} · not read yet`;
    } else if (message.isSentByMe) {
      heading = `To ${message.toUsername}`;
      meta = message.readAt
        ? `${meta} · read ${formatMessageDate(message.readAt)}`
        : `${meta} · not read yet`;
    } else {
      heading = `From ${message.fromUsername}`;
    }

    els.messagesDetail.innerHTML = `
      <header class="staff-message-detail-top">
        <h3>${escapeHtml(heading)}</h3>
        <p class="staff-message-detail-meta">${escapeHtml(meta)}</p>
      </header>
      <div class="staff-message-detail-body">${escapeHtml(message.body)}</div>`;
  }

  function populateRecipients(friends) {
    const options = [
      `<option value="">Select friend…</option>`,
      ...(friends ?? []).map(
        (friend) =>
          `<option value="${friend.playerId}" ${friend.playerId === composePlayerId ? "selected" : ""}>${escapeHtml(friend.username)}</option>`
      ),
    ];
    els.messageRecipient.innerHTML = options.join("");
  }

  function populateStaffRecipients(contacts) {
    if (!els.staffRecipient) {
      return;
    }

    els.staffRecipient.innerHTML = [
      `<option value="">Select staff member…</option>`,
      ...(contacts ?? []).map(
        (contact) =>
          `<option value="${escapeHtml(contact.username)}">${escapeHtml(staffContactLabel(contact))}</option>`
      ),
    ].join("");
  }

  async function refreshUnreadBadge() {
    if (!els.messagesNavBadge) {
      return;
    }

    try {
      const result = await api.playerUnreadCount();
      const count = Number(result.count ?? 0);
      els.messagesNavBadge.hidden = count <= 0;
      els.messagesNavBadge.textContent = String(count);
    } catch {
      els.messagesNavBadge.hidden = true;
    }
  }

  async function selectMessage(kind, messageId) {
    selectedMessageKind = kind;
    selectedMessageId = messageId;
    const message = findMessage(kind, messageId);
    renderInbox();
    renderDetail(message);

    if (!message || message.isRead || message.isSentByMe) {
      return;
    }

    try {
      const updated =
        kind === "staff"
          ? normalizeStaffMessage(await api.playerMarkMessageRead(messageId))
          : normalizePeerMessage(await api.peerMarkMessageRead(messageId));

      inboxMessages = inboxMessages.map((item) =>
        item.kind === kind && item.id === messageId ? updated : item
      );
      renderInbox();
      renderDetail(updated);
      await refreshUnreadBadge();
    } catch (error) {
      setStatus(els.messagesStatus, error.message, true);
    }
  }

  async function loadMessages() {
    hideStaffCompose();
    setStatus(els.messagesStatus, "Loading...");
    const [staffResponse, peerResponse, toStaffResponse, friendsResponse, contactsResponse] =
      await Promise.all([
        api.playerMessages(),
        api.peerMessages(),
        api.playerStaffMessages(),
        api.getFriends(),
        api.playerStaffContacts(),
      ]);

    const staffMessages = (staffResponse.messages ?? []).map(normalizeStaffMessage);
    const peerMessages = (peerResponse.messages ?? []).map(normalizePeerMessage);
    const toStaffMessages = (toStaffResponse.messages ?? []).map(normalizeToStaffMessage);
    inboxMessages = [...staffMessages, ...peerMessages, ...toStaffMessages].sort(
      (a, b) => new Date(b.createdAt) - new Date(a.createdAt)
    );

    populateRecipients(friendsResponse.friends ?? []);
    populateStaffRecipients(contactsResponse.contacts ?? []);

    if (composePlayerId) {
      els.messageRecipient.value = composePlayerId;
    }

    const firstUnread =
      inboxMessages.find((item) => !item.isRead && !item.isSentByMe) ?? inboxMessages[0] ?? null;

    if (firstUnread) {
      selectedMessageKind = firstUnread.kind;
      selectedMessageId = firstUnread.id;
    } else {
      selectedMessageKind = null;
      selectedMessageId = null;
    }

    renderInbox();
    renderDetail(
      selectedMessageKind && selectedMessageId
        ? findMessage(selectedMessageKind, selectedMessageId)
        : null
    );

    if (selectedMessageKind && selectedMessageId) {
      const selected = findMessage(selectedMessageKind, selectedMessageId);
      if (selected && !selected.isRead && !selected.isSentByMe) {
        await selectMessage(selectedMessageKind, selectedMessageId);
      }
    }

    setStatus(els.messagesStatus, `${inboxMessages.length} message(s)`);
    await refreshUnreadBadge();
  }

  async function sendMessage() {
    const toPlayerId = els.messageRecipient.value;
    const body = els.messageBody.value.trim();

    if (!toPlayerId) {
      setStatus(els.messagesStatus, "Select a friend.", true);
      return;
    }

    if (!body) {
      setStatus(els.messagesStatus, "Enter a message.", true);
      return;
    }

    els.messageSendBtn.disabled = true;
    setStatus(els.messagesStatus, "Sending...");
    try {
      const result = await api.sendPeerMessage(toPlayerId, body);
      els.messageBody.value = "";
      composePlayerId = null;
      setStatus(els.messagesStatus, result.statusMessage ?? "Message sent.");
      await loadMessages();
    } catch (error) {
      setStatus(els.messagesStatus, error.message, true);
    } finally {
      els.messageSendBtn.disabled = false;
    }
  }

  async function sendStaffMessage() {
    if (!els.staffRecipient || !els.staffBody || !els.staffSendBtn) {
      return;
    }

    const toStaffUsername = els.staffRecipient.value;
    const body = els.staffBody.value.trim();

    if (!toStaffUsername) {
      setStatus(els.messagesStatus, "Select a staff member.", true);
      return;
    }

    if (!body) {
      setStatus(els.messagesStatus, "Enter a message.", true);
      return;
    }

    els.staffSendBtn.disabled = true;
    setStatus(els.messagesStatus, "Sending...");
    try {
      const result = await api.sendPlayerStaffMessage(toStaffUsername, body);
      els.staffBody.value = "";
      hideStaffCompose();
      setStatus(els.messagesStatus, result.statusMessage ?? "Message sent to staff.");
      await loadMessages();
    } catch (error) {
      setStatus(els.messagesStatus, error.message, true);
    } finally {
      els.staffSendBtn.disabled = false;
    }
  }

  function openToPlayer(playerId) {
    composePlayerId = playerId || null;
    if (composePlayerId && els.messageRecipient) {
      els.messageRecipient.value = composePlayerId;
    }
  }

  els.messageSendBtn.addEventListener("click", () => {
    sendMessage().catch((error) => setStatus(els.messagesStatus, error.message, true));
  });

  if (els.staffSendBtn) {
    els.staffSendBtn.addEventListener("click", () => {
      sendStaffMessage().catch((error) => setStatus(els.messagesStatus, error.message, true));
    });
  }

  if (els.staffToggleBtn) {
    els.staffToggleBtn.addEventListener("click", () => {
      showStaffCompose();
    });
  }

  if (els.staffCloseBtn) {
    els.staffCloseBtn.addEventListener("click", () => {
      hideStaffCompose();
    });
  }

  els.messagesInbox.addEventListener("click", (event) => {
    const button = event.target.closest(".staff-message-item");
    if (!button) {
      return;
    }

    const kind = button.dataset.messageKind;
    const messageId = button.dataset.messageId;
    if (kind && messageId) {
      selectMessage(kind, messageId).catch((error) =>
        setStatus(els.messagesStatus, error.message, true)
      );
    }
  });

  return { loadMessages, refreshUnreadBadge, openToPlayer };
}
