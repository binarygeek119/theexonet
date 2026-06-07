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

export function initStaffPlayerMessaging({ api, els, getPlayerId, setStatus }) {
  function renderHistory(messages) {
    if (!messages.length) {
      els.profileMessageHistory.innerHTML = "";
      return;
    }

    els.profileMessageHistory.innerHTML = `
      <p class="admin-profile-flag-label">Sent messages</p>
      <ul class="admin-profile-message-list">
        ${messages
          .map(
            (message) => `
              <li class="${message.isRead ? "read" : "unread"}">
                <p class="admin-profile-flag-comment">${escapeHtml(message.body)}</p>
                <p class="admin-profile-flag-meta">
                  ${escapeHtml(message.fromStaffUsername)} · ${formatMessageDate(message.createdAt)}
                  ${message.isRead ? " · read" : " · unread"}
                </p>
              </li>`
          )
          .join("")}
      </ul>`;
  }

  function setMessageStatus(message, isError = false) {
    els.profileMessageStatus.textContent = message ?? "";
    els.profileMessageStatus.classList.toggle("error", Boolean(isError && message));
    els.profileMessageStatus.classList.toggle("success", Boolean(!isError && message));
  }

  async function loadHistory() {
    const playerId = getPlayerId();
    if (!playerId) {
      renderHistory([]);
      return;
    }

    try {
      const result = await api.staffPlayerMessages(playerId);
      renderHistory(result.messages ?? []);
    } catch (error) {
      setMessageStatus(error.message, true);
    }
  }

  function clearForm() {
    els.profileMessageInput.value = "";
    setMessageStatus("");
  }

  async function sendMessage() {
    const playerId = getPlayerId();
    if (!playerId) {
      return;
    }

    const body = els.profileMessageInput.value.trim();
    if (!body) {
      setMessageStatus("Enter a message.", true);
      return;
    }

    els.profileMessageBtn.disabled = true;
    setMessageStatus("Sending...");
    try {
      const result = await api.staffSendPlayerMessage(playerId, body);
      els.profileMessageInput.value = "";
      setMessageStatus(result.statusMessage ?? "Message sent.", false);
      await loadHistory();
    } catch (error) {
      setMessageStatus(error.message, true);
    } finally {
      els.profileMessageBtn.disabled = false;
    }
  }

  els.profileMessageBtn.addEventListener("click", () => {
    sendMessage().catch((error) => setMessageStatus(error.message, true));
  });

  return { loadHistory, clearForm };
}
