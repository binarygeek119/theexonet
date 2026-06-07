import { getDummyFriendSummaries, isDummyPlayerId } from "./admin-testing-mode.js?v=20260529-testing-mode-server";

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
  /** @type {"peer" | "staff"} */
  let messageViewMode = "peer";

  function isStaffMessage(message) {
    return message.kind === "staff" || message.kind === "to-staff";
  }

  function getVisibleMessages() {
    return inboxMessages.filter((message) =>
      messageViewMode === "staff" ? isStaffMessage(message) : message.kind === "peer"
    );
  }

  function getInboxEl() {
    return messageViewMode === "staff" ? els.staffMessagesInbox : els.messagesInbox;
  }

  function getDetailEl() {
    return messageViewMode === "staff" ? els.staffMessagesDetail : els.messagesDetail;
  }

  function showPeerMode() {
    messageViewMode = "peer";
    if (els.peerPanel) {
      els.peerPanel.hidden = false;
    }
    if (els.staffPanel) {
      els.staffPanel.hidden = true;
    }
    selectFirstVisibleMessage();
  }

  function showStaffMode() {
    messageViewMode = "staff";
    if (els.peerPanel) {
      els.peerPanel.hidden = true;
    }
    if (els.staffPanel) {
      els.staffPanel.hidden = false;
    }
    selectFirstVisibleMessage();
    els.staffRecipient?.focus();
  }

  function selectFirstVisibleMessage() {
    const visible = getVisibleMessages();
    const firstUnread =
      visible.find((item) => !item.isRead && !item.isSentByMe) ?? visible[0] ?? null;

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
    updateStatusCount();
  }

  function updateStatusCount() {
    const visible = getVisibleMessages();
    const label = messageViewMode === "staff" ? "staff " : "";
    setStatus(els.messagesStatus, `${visible.length} ${label}message(s)`);
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
    const inboxEl = getInboxEl();
    const detailEl = getDetailEl();
    if (!inboxEl || !detailEl) {
      return;
    }

    const visible = getVisibleMessages();
    if (!visible.length) {
      const emptyLabel =
        messageViewMode === "staff" ? "No staff messages yet." : "No friend messages yet.";
      inboxEl.innerHTML = `<p class="friends-status">${emptyLabel}</p>`;
      detailEl.innerHTML = `<p class="friends-status">Select a message to read it.</p>`;
      return;
    }

    inboxEl.innerHTML = visible
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
    const detailEl = getDetailEl();
    if (!detailEl) {
      return;
    }

    if (!message) {
      detailEl.innerHTML = `<p class="friends-status">Select a message to read it.</p>`;
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

    detailEl.innerHTML = `
      <header class="staff-message-detail-top">
        <div class="staff-message-detail-head">
          <div>
            <h3>${escapeHtml(heading)}</h3>
            <p class="staff-message-detail-meta">${escapeHtml(meta)}</p>
          </div>
          <button type="button" class="btn ghost danger staff-message-delete-btn">Remove</button>
        </div>
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
      updateStatusCount();
      await refreshUnreadBadge();
    } catch (error) {
      setStatus(els.messagesStatus, error.message, true);
    }
  }

  async function loadMessages() {
    if (composePlayerId && isDummyPlayerId(composePlayerId)) {
      showPeerMode();
      setStatus(els.messagesStatus, "Testing profile");
      inboxMessages = [];
      const dummyFriends = getDummyFriendSummaries().filter(
        (friend) => friend.playerId === composePlayerId,
      );
      populateRecipients(dummyFriends);
      if (els.messageRecipient) {
        els.messageRecipient.value = composePlayerId;
      }
      renderInbox();
      getDetailEl().innerHTML =
        `<p class="friends-status">Test profile — messages are not saved. This conversation is for UI testing only.</p>`;
      updateStatusCount();
      return;
    }

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

    const visible = getVisibleMessages();
    const firstUnread =
      visible.find((item) => !item.isRead && !item.isSentByMe) ?? visible[0] ?? null;

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

    updateStatusCount();
    await refreshUnreadBadge();
  }

  async function deleteSelectedMessage() {
    if (!selectedMessageKind || !selectedMessageId) {
      return;
    }

    if (composePlayerId && isDummyPlayerId(composePlayerId)) {
      setStatus(els.messagesStatus, "Test profile — messages are not saved.");
      return;
    }

    const message = findMessage(selectedMessageKind, selectedMessageId);
    if (!message) {
      return;
    }

    if (!window.confirm("Remove this message from your inbox? The other person can still see it.")) {
      return;
    }

    setStatus(els.messagesStatus, "Removing...");
    try {
      if (message.kind === "staff") {
        await api.deletePlayerMessage(message.id);
      } else if (message.kind === "to-staff") {
        await api.deletePlayerStaffMessage(message.id);
      } else {
        await api.deletePeerMessage(message.id);
      }

      setStatus(els.messagesStatus, "Message removed.");
      await loadMessages();
    } catch (error) {
      setStatus(els.messagesStatus, error.message, true);
    }
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

    if (isDummyPlayerId(toPlayerId)) {
      els.messageBody.value = "";
      setStatus(els.messagesStatus, "Test profile — messages are not saved.");
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
      setStatus(els.messagesStatus, result.statusMessage ?? "Message sent to staff.");
      await loadMessages();
    } catch (error) {
      setStatus(els.messagesStatus, error.message, true);
    } finally {
      els.staffSendBtn.disabled = false;
    }
  }

  function openToPlayer(playerId) {
    showPeerMode();
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
      showStaffMode();
    });
  }

  if (els.peerToggleBtn) {
    els.peerToggleBtn.addEventListener("click", () => {
      showPeerMode();
    });
  }

  showPeerMode();

  function handleDetailClick(event) {
    if (event.target.closest(".staff-message-delete-btn")) {
      deleteSelectedMessage().catch((error) =>
        setStatus(els.messagesStatus, error.message, true)
      );
    }
  }

  els.messagesInbox?.addEventListener("click", handleInboxClick);
  els.staffMessagesInbox?.addEventListener("click", handleInboxClick);
  els.messagesDetail?.addEventListener("click", handleDetailClick);
  els.staffMessagesDetail?.addEventListener("click", handleDetailClick);

  function handleInboxClick(event) {
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
  }

  return { loadMessages, refreshUnreadBadge, openToPlayer, showPeerMode };
}
