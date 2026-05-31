export const SOCIAL_PLATFORMS = [
  { key: "discord", label: "Discord", placeholder: "username or discord.gg/invite" },
  { key: "bluesky", label: "Bluesky", placeholder: "@handle.bsky.social" },
  { key: "twitter", label: "Twitter / X", placeholder: "@handle" },
  { key: "youtube", label: "YouTube", placeholder: "@channel or channel URL" },
  { key: "facebook", label: "Facebook", placeholder: "username or profile URL" },
];

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

export function resolveSocialLink(platform, value) {
  const raw = String(value ?? "").trim();
  if (!raw) {
    return null;
  }

  if (/^https?:\/\//i.test(raw)) {
    return { label: raw, href: raw };
  }

  switch (platform) {
    case "discord":
      if (/discord(?:app)?\.com|discord\.gg/i.test(raw)) {
        return { label: raw, href: raw.startsWith("http") ? raw : `https://${raw}` };
      }
      return { label: raw.startsWith("@") ? raw : `@${raw}`, href: null };
    case "bluesky": {
      const handle = raw.replace(/^@/, "");
      return { label: `@${handle}`, href: `https://bsky.app/profile/${handle}` };
    }
    case "twitter": {
      const handle = raw.replace(/^@/, "");
      return { label: `@${handle}`, href: `https://x.com/${handle}` };
    }
    case "youtube": {
      if (raw.includes("youtube.com") || raw.includes("youtu.be")) {
        return { label: raw, href: raw.startsWith("http") ? raw : `https://${raw}` };
      }
      const handle = raw.replace(/^@/, "");
      return { label: `@${handle}`, href: `https://youtube.com/@${handle}` };
    }
    case "facebook": {
      const handle = raw.replace(/^@/, "");
      return { label: handle, href: `https://facebook.com/${handle}` };
    }
    default:
      return { label: raw, href: null };
  }
}

export function renderSocialLinksHtml(profile) {
  const items = SOCIAL_PLATFORMS.map(({ key, label }) => {
    const link = resolveSocialLink(key, profile?.[key]);
    if (!link) {
      return null;
    }

    const content = link.href
      ? `<a href="${escapeHtml(link.href)}" target="_blank" rel="noopener noreferrer">${escapeHtml(link.label)}</a>`
      : `<span>${escapeHtml(link.label)}</span>`;

    return `<li><span class="profile-social-label">${escapeHtml(label)}</span>${content}</li>`;
  }).filter(Boolean);

  if (!items.length) {
    return `<p class="profile-social-empty">No social links yet.</p>`;
  }

  return `<ul class="profile-social-list">${items.join("")}</ul>`;
}

export function hasSocialLinks(profile) {
  return SOCIAL_PLATFORMS.some(({ key }) => String(profile?.[key] ?? "").trim());
}
