function formatModerationDate(value) {
  if (!value) {
    return "—";
  }
  return new Date(value).toLocaleString();
}

export function initModerationFlow({ api, els, t, showScreen, setAuthMode, showLoginStatus, state }) {
  let ackResolver = null;

  function hideModerationModal() {
    if (els.moderationModal) {
      els.moderationModal.hidden = true;
    }
    if (ackResolver) {
      ackResolver();
      ackResolver = null;
    }
  }

  function showModerationModal({ title, message, meta, ackLabel, showAppeal = false }) {
    return new Promise((resolve) => {
      ackResolver = resolve;
      if (els.moderationTitle) {
        els.moderationTitle.textContent = title;
      }
      if (els.moderationMessage) {
        els.moderationMessage.textContent = message;
      }
      if (els.moderationMeta) {
        els.moderationMeta.textContent = meta ?? "";
        els.moderationMeta.hidden = !meta;
      }
      if (els.moderationAckBtn) {
        els.moderationAckBtn.textContent = ackLabel;
      }
      if (els.moderationAppealBtn) {
        els.moderationAppealBtn.hidden = !showAppeal;
      }
      if (els.moderationModal) {
        els.moderationModal.hidden = false;
      }
    });
  }

  function showBanModal(message, ban) {
    state.banMessage = message ?? ban?.reason ?? "";
    const reason = ban?.reason?.trim();
    const metaParts = [];
    if (reason) {
      metaParts.push(`${t("moderation.reason")}: ${reason}`);
    }
    if (ban?.expiresAt && !ban?.isPermanent) {
      metaParts.push(`${t("moderation.until")} ${formatModerationDate(ban.expiresAt)}`);
    }
    if (ban?.isPermanent) {
      metaParts.push(t("moderation.permanentBan"));
    }

    return showModerationModal({
      title: t("moderation.banTitle"),
      message: message || t("moderation.banDefault"),
      meta: metaParts.join(" · "),
      ackLabel: t("moderation.acknowledgeBan"),
      showAppeal: true,
    });
  }

  function showWarningModal(warning) {
    const reason = String(warning?.reason ?? "").trim() || t("moderation.noReason");
    const meta = `${t("moderation.issued")} ${formatModerationDate(warning?.createdAt)} · ${t("moderation.expires")} ${formatModerationDate(warning?.expiresAt)}`;
    return showModerationModal({
      title: t("moderation.warningTitle"),
      message: t("moderation.warningBody", { reason }),
      meta,
      ackLabel: t("moderation.acknowledgeWarning"),
      showAppeal: false,
    });
  }

  async function acknowledgePendingWarnings(warnings) {
    let queue = Array.isArray(warnings) ? [...warnings] : [];
    while (queue.length > 0) {
      const warning = queue[0];
      await showWarningModal(warning);
      hideModerationModal();
      const result = await api.acknowledgeWarning(warning.id);
      queue = result?.remainingWarnings ?? [];
    }
  }

  function handleModerationKick(error) {
    if (error?.code === "banned") {
      api.clearAuth();
      showScreen("login");
      setAuthMode("login");
      showBanModal(error.message, error.ban).then(() => {
        hideModerationModal();
      });
      return true;
    }

    if (error?.code === "warning_required") {
      api.clearAuth();
      showScreen("login");
      setAuthMode("login");
      showLoginStatus(t("moderation.warningRelogin"), "info");
      return true;
    }

    return false;
  }

  function attachModerationRequestHook() {
    const originalRequest = api.request.bind(api);
    api.request = async (path, options = {}) => {
      try {
        return await originalRequest(path, options);
      } catch (error) {
        if (options.auth !== false && api.token) {
          handleModerationKick(error);
        }
        throw error;
      }
    };
  }

  els.moderationAckBtn?.addEventListener("click", () => {
    hideModerationModal();
  });

  els.moderationAppealBtn?.addEventListener("click", () => {
    hideModerationModal();
    setAuthMode("ban-appeal");
    showLoginStatus(t("auth.hint.banAppeal"), "info");
  });

  attachModerationRequestHook();

  return {
    showBanModal,
    acknowledgePendingWarnings,
    handleModerationKick,
  };
}
