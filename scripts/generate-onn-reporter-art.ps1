# Legacy placeholder SVG generator. Reporter profiles now use AI JPEGs via
# Admin -> Offworld News -> Regenerate reporter portraits, or the API endpoint
# POST /api/admin/offworld-news/regenerate-reporter-portraits

$reporters = @(
    @("mira-solano", "MS", "#e85d4a", "#5ec8ff", "#0f1c33", "#1e3a6e"),
    @("jonah-kest", "JK", "#c9a227", "#8fd4ff", "#141820", "#2a3f5c"),
    @("priya-menon", "PM", "#4ec9b0", "#7ec8ff", "#0c1824", "#1a4a6e"),
    @("cassian-holt", "CH", "#9b7bff", "#ffd28a", "#121428", "#3a2a6e"),
    @("elena-varga", "EV", "#ff7eb6", "#9fd0ff", "#1a1020", "#4a2048"),
    @("marcus-whitaker", "MW", "#6ec4ff", "#ffe08a", "#0a1420", "#1c4a7a"),
    @("sable-nguyen", "SN", "#56d6c8", "#b8a0ff", "#081820", "#1a3d52"),
    @("theo-brassard", "TB", "#8fd56a", "#7ec8ff", "#101810", "#2a4a30"),
    @("ingrid-falk", "IF", "#ff9f6b", "#c8e8ff", "#181018", "#4a2838"),
    @("devon-ashcroft", "DA", "#7aa2ff", "#ffc48a", "#0c1020", "#283878"),
    @("lena-okonkwo", "LO", "#f4d35e", "#9fd0ff", "#18140c", "#4a4020"),
    @("rafael-cruz", "RC", "#ff6b6b", "#8fd4ff", "#200c0c", "#6e2020"),
    @("yumiko-ito", "YI", "#ff8ad8", "#7ec8ff", "#1a1028", "#4a2868"),
    @("anders-lindqvist", "AL", "#7ec8ff", "#ffe08a", "#081420", "#1a4870"),
    @("zara-pemberton", "ZP", "#b8ff6a", "#9fd0ff", "#101808", "#2a5030")
)

$root = (Join-Path $PSScriptRoot "..\server\Theexonet.Api\html\exonet\offworld-news\reporters")
New-Item -ItemType Directory -Force -Path $root | Out-Null

foreach ($r in $reporters) {
    $slug, $initials, $accent, $glow, $bg1, $bg2 = $r
    $folder = Join-Path $root $slug
    New-Item -ItemType Directory -Force -Path $folder | Out-Null

    $avatar = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160" role="img" aria-label="ONN reporter portrait">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="$bg1"/>
      <stop offset="100%" stop-color="$bg2"/>
    </linearGradient>
    <radialGradient id="glow" cx="50%" cy="35%" r="55%">
      <stop offset="0%" stop-color="$glow" stop-opacity="0.45"/>
      <stop offset="100%" stop-color="$glow" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <rect width="160" height="160" rx="80" fill="url(#bg)"/>
  <circle cx="80" cy="58" r="52" fill="url(#glow)"/>
  <circle cx="80" cy="62" r="34" fill="$accent" opacity="0.88"/>
  <rect x="48" y="96" width="64" height="44" rx="18" fill="$accent" opacity="0.55"/>
  <text x="80" y="72" text-anchor="middle" font-family="Georgia, serif" font-size="28" font-weight="700" fill="#f4f8ff">$initials</text>
  <circle cx="80" cy="80" r="78" fill="none" stroke="$glow" stroke-width="2" opacity="0.35"/>
</svg>
"@

    $background = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 200" role="img" aria-label="ONN reporter bureau backdrop" preserveAspectRatio="xMidYMid slice">
  <defs>
    <linearGradient id="sky" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="$bg1"/>
      <stop offset="55%" stop-color="$bg2"/>
      <stop offset="100%" stop-color="$accent" stop-opacity="0.55"/>
    </linearGradient>
  </defs>
  <rect width="800" height="200" fill="url(#sky)"/>
  <rect y="120" width="800" height="80" fill="$glow" opacity="0.12"/>
  <path d="M0 150 Q200 110 400 145 T800 135 L800 200 L0 200 Z" fill="$accent" opacity="0.18"/>
  <rect width="800" height="200" fill="#0a1420" opacity="0.18"/>
</svg>
"@

    Set-Content -Path (Join-Path $folder "avatar.svg") -Value $avatar -Encoding utf8
    Set-Content -Path (Join-Path $folder "background.svg") -Value $background -Encoding utf8
    Write-Output $slug
}
