#!/usr/bin/env python3
"""Generate ONN reporter avatar and background SVG assets."""

from pathlib import Path

REPORTERS = [
    ("mira-solano", "MS", "#e85d4a", "#5ec8ff", "#0f1c33", "#1e3a6e"),
    ("jonah-kest", "JK", "#c9a227", "#8fd4ff", "#141820", "#2a3f5c"),
    ("priya-menon", "PM", "#4ec9b0", "#7ec8ff", "#0c1824", "#1a4a6e"),
    ("cassian-holt", "CH", "#9b7bff", "#ffd28a", "#121428", "#3a2a6e"),
    ("elena-varga", "EV", "#ff7eb6", "#9fd0ff", "#1a1020", "#4a2048"),
    ("marcus-whitaker", "MW", "#6ec4ff", "#ffe08a", "#0a1420", "#1c4a7a"),
    ("sable-nguyen", "SN", "#56d6c8", "#b8a0ff", "#081820", "#1a3d52"),
    ("theo-brassard", "TB", "#8fd56a", "#7ec8ff", "#101810", "#2a4a30"),
    ("ingrid-falk", "IF", "#ff9f6b", "#c8e8ff", "#181018", "#4a2838"),
    ("devon-ashcroft", "DA", "#7aa2ff", "#ffc48a", "#0c1020", "#283878"),
    ("lena-okonkwo", "LO", "#f4d35e", "#9fd0ff", "#18140c", "#4a4020"),
    ("rafael-cruz", "RC", "#ff6b6b", "#8fd4ff", "#200c0c", "#6e2020"),
    ("yumiko-ito", "YI", "#ff8ad8", "#7ec8ff", "#1a1028", "#4a2868"),
    ("anders-lindqvist", "AL", "#7ec8ff", "#ffe08a", "#081420", "#1a4870"),
    ("zara-pemberton", "ZP", "#b8ff6a", "#9fd0ff", "#101808", "#2a5030"),
]

ROOT = Path(__file__).resolve().parents[1] / "server" / "Theexonet.Api" / "html" / "exonet" / "offworld-news" / "reporters"


def avatar(slug: str, initials: str, accent: str, glow: str, bg1: str, bg2: str) -> str:
    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160" role="img" aria-label="ONN reporter portrait">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="{bg1}"/>
      <stop offset="100%" stop-color="{bg2}"/>
    </linearGradient>
    <radialGradient id="glow" cx="50%" cy="35%" r="55%">
      <stop offset="0%" stop-color="{glow}" stop-opacity="0.45"/>
      <stop offset="100%" stop-color="{glow}" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <rect width="160" height="160" rx="80" fill="url(#bg)"/>
  <circle cx="80" cy="58" r="52" fill="url(#glow)"/>
  <ellipse cx="80" cy="118" rx="46" ry="34" fill="{accent}" opacity="0.22"/>
  <circle cx="80" cy="62" r="34" fill="{accent}" opacity="0.88"/>
  <rect x="48" y="96" width="64" height="44" rx="18" fill="{accent}" opacity="0.55"/>
  <text x="80" y="72" text-anchor="middle" font-family="Georgia, serif" font-size="28" font-weight="700" fill="#f4f8ff">{initials}</text>
  <circle cx="80" cy="80" r="78" fill="none" stroke="{glow}" stroke-width="2" opacity="0.35"/>
</svg>
'''


def background(slug: str, accent: str, glow: str, bg1: str, bg2: str) -> str:
    stars = "\n".join(
        f'  <circle cx="{20 + (i * 47) % 760}" cy="{12 + (i * 29) % 120}" r="{1 + i % 2}" fill="#ffffff" opacity="{0.15 + (i % 5) * 0.08}"/>'
        for i in range(18)
    )
    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 200" role="img" aria-label="ONN reporter bureau backdrop" preserveAspectRatio="xMidYMid slice">
  <defs>
    <linearGradient id="sky" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="{bg1}"/>
      <stop offset="55%" stop-color="{bg2}"/>
      <stop offset="100%" stop-color="{accent}" stop-opacity="0.55"/>
    </linearGradient>
    <linearGradient id="band" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="{glow}" stop-opacity="0.05"/>
      <stop offset="100%" stop-color="{glow}" stop-opacity="0.28"/>
    </linearGradient>
  </defs>
  <rect width="800" height="200" fill="url(#sky)"/>
{stars}
  <rect y="120" width="800" height="80" fill="url(#band)"/>
  <path d="M0 150 Q200 110 400 145 T800 135 L800 200 L0 200 Z" fill="{accent}" opacity="0.18"/>
  <path d="M0 170 Q260 130 520 168 T800 158 L800 200 L0 200 Z" fill="{glow}" opacity="0.12"/>
  <rect width="800" height="200" fill="#0a1420" opacity="0.18"/>
</svg>
'''


def main() -> None:
    for slug, initials, accent, glow, bg1, bg2 in REPORTERS:
        folder = ROOT / slug
        folder.mkdir(parents=True, exist_ok=True)
        (folder / "avatar.svg").write_text(avatar(slug, initials, accent, glow, bg1, bg2), encoding="utf-8")
        (folder / "background.svg").write_text(background(slug, accent, glow, bg1, bg2), encoding="utf-8")
        print(slug)


if __name__ == "__main__":
    main()
