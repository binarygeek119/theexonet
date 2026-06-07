# Translating theexonet with Weblate

Player-facing UI strings live in JSON locale files and are loaded by [`server/Rava.Api/html/js/i18n.js`](../server/Rava.Api/html/js/i18n.js).

## Status: disabled (pre–go-live)

Weblate is **not** active yet. The UI stays **English-only** and language pickers are hidden (`WEBLATE_LIVE = false` in both `i18n.js` files). Component config is kept in [`weblate.yml.off`](../weblate.yml.off) so discovery does not run until you enable it.

### Go-live checklist

1. Rename [`weblate.yml.off`](../weblate.yml.off) → `weblate.yml` and connect the repo in Weblate.
2. Set `WEBLATE_LIVE = true` in `server/Rava.Api/html/js/i18n.js` and `server/Rava.Status/wwwroot/js/i18n.js`.
3. Merge translated `es/`, `fr/`, etc. locale folders and deploy HTML/wwwroot.
4. Pause or remove any test Weblate project that was pointed at a fork.

## Weblate project (when enabled)

1. Add this GitHub repository to Weblate.
2. Enable **weblate.yml** component discovery (or import the four components from `weblate.yml` manually).
3. Source language: **English (`en`)**.
4. Translators work in `game.json`, `admin.json`, `moderator.json`, and `status.json`; merged PRs add `es/`, `fr/`, etc.

See also [`server/Rava.Api/html/locales/README.md`](../server/Rava.Api/html/locales/README.md).

## In scope

| Component | Path |
|-----------|------|
| Game UI | `server/Rava.Api/html/locales/*/game.json` |
| Admin portal | `server/Rava.Api/html/locales/*/admin.json` |
| Moderator portal | `server/Rava.Api/html/locales/*/moderator.json` |
| Status dashboard | `server/Rava.Status/wwwroot/locales/*/status.json` |

## Out of scope (do not add to Weblate)

- **Offworld News / Exonet** — `exonet.js`, generated story HTML, reporter AI bios, edition templates under `offworld-news/`
- API / server validation error strings
- User-written profile text, peer messages, staff messages

Exonet stays English-only so AI-generated news is not mixed with translator workflows.

## Adding strings

1. Add the English key to the correct `en/*.json` file.
2. Mark HTML with `data-i18n="your.key"` (or `data-i18n-placeholder`, `data-i18n-title`).
3. In JavaScript modules (not `exonet.js`), use `t("your.key")` from `i18n.js`.
4. Use `{name}` placeholders in JSON and pass `{ name: "..." }` to `t()`.

## Local test

1. Copy a locale folder, e.g. `locales/es/game.json` (Weblate normally does this).
2. Open the game, choose **Language** on the login screen, pick Spanish.
3. Reload; static labels should follow the JSON file.

## Deploy

Locale files ship with `deploy-rava-html` / API wwwroot sync. No separate build step.

## Profile completion prompts

When new required profile columns ship (e.g. gender, `ProfileLocale`), `ProfileCompletionEvaluator` in `Rava.Core` lists missing fields on `PlayerProfileResponse.missingProfileFields`. Existing players see a blocking modal on login until they save. Add a new check in that class when another field becomes mandatory. Field ids: `gender`, `preferredPronouns`, `locale`.
