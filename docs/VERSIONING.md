# Game versioning

RAVA uses **semantic versioning** in the form **V*MAJOR*.*MINOR*.*PATCH*** (for example **V1.0.0**).

| Part | When to increase | Examples |
|------|------------------|----------|
| **Major** (first number) | Major releases, breaking changes, large milestones | `V1.0.0` → `V2.0.0` |
| **Minor** (second number) | New features, gameplay additions, non-breaking API changes | `V1.0.0` → `V1.1.0` |
| **Patch** (third number) | Bug fixes, balance tweaks, small corrections | `V1.0.0` → `V1.0.1` |

Reset lower numbers when you bump a higher part (e.g. `V1.2.5` → `V2.0.0`, not `V2.2.5`).

**Project rule:** only bump `Major`, `Minor`, or `Patch` when you explicitly request it. Agents and contributors must not change the version as part of unrelated work.

## Where the version is defined

Single source of truth:

`server/Rava.Core/Constants/GameVersion.cs`

```csharp
public const int Major = 1;
public const int Minor = 0;
public const int Patch = 0;
```

`GameVersion.Display` (e.g. `V1.0.0`) is returned by:

- `GET /api/status` → `gameVersion`
- [Server Status dashboard](https://ravastatus.binarygeek119.duckdns.org/) — Game API card
- Game login screen — bottom-right version tag

After changing the constants, rebuild and deploy the API (and status site if bundled together).

## Release checklist

1. Update `Major`, `Minor`, or `Patch` in `GameVersion.cs`.
2. Note the change in your commit or release notes.
3. Deploy to production (GitHub Actions or manual rsync).
4. Confirm `curl -s https://ravaapi.binarygeek119.duckdns.org/api/status | grep gameVersion`.
