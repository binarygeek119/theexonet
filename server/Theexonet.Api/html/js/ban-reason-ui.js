const CUSTOM_BAN_REASON_VALUE = "__custom__";

function escapeOptionText(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/"/g, "&quot;");
}

export function populateBanReasonPresets(selectEl, presets) {
  if (!selectEl) {
    return;
  }

  const options = ['<option value="">Select a reason…</option>'];
  for (const preset of presets ?? []) {
    const text = String(preset ?? "").trim();
    if (!text) {
      continue;
    }
    options.push(
      `<option value="${escapeOptionText(text)}">${escapeOptionText(text)}</option>`,
    );
  }
  options.push(`<option value="${CUSTOM_BAN_REASON_VALUE}">Custom reason…</option>`);
  selectEl.innerHTML = options.join("");
}

export function syncBanReasonCustomVisibility(presetSelect, customTextarea, customLabel) {
  const isCustom = presetSelect?.value === CUSTOM_BAN_REASON_VALUE;
  if (customTextarea) {
    customTextarea.hidden = !isCustom;
  }
  if (customLabel) {
    customLabel.hidden = !isCustom;
  }
}

export function getResolvedBanReason(presetSelect, customTextarea) {
  const preset = presetSelect?.value ?? "";
  if (!preset) {
    return "";
  }
  if (preset === CUSTOM_BAN_REASON_VALUE) {
    return customTextarea?.value.trim() ?? "";
  }
  return preset;
}

export function resetBanReasonForm(presetSelect, customTextarea, customLabel) {
  if (presetSelect) {
    presetSelect.value = "";
  }
  if (customTextarea) {
    customTextarea.value = "";
  }
  syncBanReasonCustomVisibility(presetSelect, customTextarea, customLabel);
}

export function wireBanReasonForm(presetSelect, customTextarea, customLabel) {
  if (!presetSelect || presetSelect.dataset.banReasonWired === "1") {
    syncBanReasonCustomVisibility(presetSelect, customTextarea, customLabel);
    return;
  }

  presetSelect.dataset.banReasonWired = "1";
  presetSelect.addEventListener("change", () => {
    syncBanReasonCustomVisibility(presetSelect, customTextarea, customLabel);
  });
  syncBanReasonCustomVisibility(presetSelect, customTextarea, customLabel);
}
