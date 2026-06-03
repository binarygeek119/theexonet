/** Player-facing currency: Rax (icon from /images/currency.png). */
export const RAX_NAME = "Rax";
export const RAX_ICON_URL = "/images/currency.png";

export function formatRaxNumber(value, { decimals = 0 } = {}) {
  const n = Number(value);
  if (!Number.isFinite(n)) {
    return "—";
  }

  if (decimals > 0) {
    return n.toLocaleString(undefined, {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    });
  }

  return n.toLocaleString(undefined, { maximumFractionDigits: 0 });
}

export function raxIconHtml(className = "rax-icon") {
  return `<img class="${className}" src="${RAX_ICON_URL}" alt="" aria-hidden="true">`;
}

/** Symbol + amount, like $1,234. */
export function formatRaxHtml(value, { decimals = 0, showName = false } = {}) {
  const amount = formatRaxNumber(value, { decimals });
  const nameSuffix = showName ? `<span class="rax-name">${RAX_NAME}</span>` : "";
  return `<span class="rax-value">${raxIconHtml()}<span class="rax-amount">${amount}</span>${nameSuffix}</span>`;
}

export function formatRaxPlain(value, { decimals = 0 } = {}) {
  return `${formatRaxNumber(value, { decimals })} ${RAX_NAME}`;
}

export function formatRaxWithSymbol(value, { decimals = 0 } = {}) {
  return formatRaxHtml(value, { decimals });
}

export function setRaxHtml(element, value, options) {
  if (!element) {
    return;
  }

  element.innerHTML = formatRaxHtml(value, options);
}

export function formatRaxLabel(value, options) {
  return `${RAX_NAME}: ${formatRaxHtml(value, options)}`;
}

/** Map legacy API item type "Credits" to display name Rax. */
export function formatCurrencyItemType(itemType) {
  if ((itemType ?? "").trim().toLowerCase() === "credits") {
    return RAX_NAME;
  }

  return itemType ?? "";
}

export function formatRewardAmount(itemType, amount) {
  if ((itemType ?? "").trim().toLowerCase() === "credits") {
    return formatRaxPlain(amount);
  }

  return `${formatRaxNumber(amount)} ${itemType ?? ""}`.trim();
}
