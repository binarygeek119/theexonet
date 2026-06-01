# RAVA UI translations (Weblate)

English JSON files in `en/` are the **source** strings. Weblate opens PRs with `es/`, `fr/`, etc.

## Components

| File | UI |
|------|-----|
| `game.json` | Main game (`index.html`, `app.js` player UI) |
| `admin.json` | Admin portal (`admin.html`) |
| `moderator.json` | Moderator portal (`moderator.html`) |
| `status.json` | Status dashboard (`Rava.Status/wwwroot`) |

Repository root [`weblate.yml`](../../../weblate.yml) defines Weblate components.

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

After `setLocale`, listen for `rava:localechange` or call `applyTranslations` again on dynamic DOM.
