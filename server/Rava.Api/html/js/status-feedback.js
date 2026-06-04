const PENDING_SUFFIX = /(?:…|\.\.\.)$/;

function isNeutralStatusMessage(message) {
  const text = String(message).trim();
  if (!text) {
    return true;
  }

  if (/^(loading|loaded|testing mode)/i.test(text)) {
    return true;
  }

  if (/^\d+\s+/i.test(text) && /(shown|pending|message\(s\)|event\(s\)|reporter|loaded|appeal)/i.test(text)) {
    return true;
  }

  return false;
}

/**
 * @param {HTMLElement | null | undefined} el
 * @param {string} message
 * @param {boolean} [isError]
 */
export function setActionStatus(el, message, isError = false) {
  if (!el) {
    return;
  }

  el.textContent = message ?? "";
  el.style.color = "";
  el.classList.remove("success", "error", "info");

  if (!message) {
    return;
  }

  if (isError) {
    el.classList.add("error");
  } else if (PENDING_SUFFIX.test(String(message).trim())) {
    el.classList.add("info");
  } else if (!isNeutralStatusMessage(message)) {
    el.classList.add("success");
  }
}
