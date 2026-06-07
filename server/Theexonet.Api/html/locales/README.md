# theexonet UI translations

English JSON files in `en/` are the **source** strings. Weblate is **off** until go-live (`weblate.yml.off`, `WEBLATE_LIVE` in `js/i18n.js`); then Weblate can open PRs with `es/`, `fr/`, etc.

## Components

| File | UI |
|------|-----|
| `game.json` | Main game (`index.html`, `app.js` player UI) |
| `admin.json` | Admin portal (`admin.html`) |
| `moderator.json` | Moderator portal (`moderator.html`) |
| `status.json` | Status dashboard (`Theexonet.Status/wwwroot`) |

Repository root [`weblate.yml.off`](../../../weblate.yml.off) defines Weblate components (rename to `weblate.yml` when enabling).

## Not translated here

- **Exonet / Offworld News** — `js/exonet.js`, generated editions, reporter AI copy
- Server validation messages and API errors (English for now)
- Player-generated profile text, chat, and news articles

## Keys

- Flat keys with dots: `"nav.profile": "Profile"`
- Placeholders: `"day.title": "Day {day} report"`

## HTML

```html
<button type="button" data-i18n="nav.profile">Profile</button>
<input data-i18n-placeholder="auth.usernamePlaceholder" />
```

## JavaScript

```javascript
import { t, initI18n, applyTranslations } from "./i18n.js";
await initI18n({ namespaces: ["game"] });
applyTranslations(document);
showStatus(t("auth.connecting"));
```

After `setLocale`, listen for `theexonet:localechange` or call `applyTranslations` again on dynamic DOM.
