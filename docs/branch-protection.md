# Branch protection for `main`

Protect the default branch so merges and releases only happen when CI passes.

## What is enforced

| Rule | Purpose |
|------|---------|
| Block branch deletion | Prevents removing `main` |
| Block force pushes | Prevents rewriting published history |
| Required status checks | CI must pass before merge |
| Strict / up to date | PR branch must include latest `main` |

### Required checks (GitHub Actions)

These match job names in [`.github/workflows/build-website.yml`](../.github/workflows/build-website.yml) and [`.github/workflows/security.yml`](../.github/workflows/security.yml):

| Check name | Workflow |
|------------|----------|
| **Build and test** | RAVA CI |
| **NuGet vulnerability audit** | Security |
| **CodeQL** | Security |

Repository admins can bypass rules (configured in the ruleset) so direct pushes remain possible for maintainers; **pull requests** (including Dependabot) still need green checks.

### Path filters

RAVA CI and Security only run when certain paths change. If a PR touches only files outside those paths, checks may be **skipped**. In **Settings → Rules → Rulesets → Protect main**, enable **Do not require status checks for workflows that are skipped** if merges get stuck on skipped jobs.

## Apply automatically (recommended)

From a repo clone with [GitHub CLI](https://cli.github.com/) logged in as an admin:

```bash
gh auth login
bash scripts/configure-branch-protection.sh
```

The script creates or updates the **Protect main** ruleset from [`.github/rulesets/protect-main.json`](../.github/rulesets/protect-main.json).

## Apply manually in GitHub UI

1. Open **Settings → Rules → Rulesets → New ruleset → Import a ruleset**.
2. Choose [`.github/rulesets/protect-main.json`](../.github/rulesets/protect-main.json).
3. Review required checks (they must exist after at least one CI run on a PR).
4. Save and set enforcement to **Active**.

## Optional: require pull request reviews

The default ruleset does **not** require approving reviews (solo maintainer can still push to `main`). To require reviews before merge, add a **Pull request** rule in the ruleset or edit `protect-main.json` and re-run the configure script.

## Verify

1. Open a test PR against `main`.
2. Confirm **Build and test**, **NuGet vulnerability audit**, and **CodeQL** appear under required checks.
3. Merge should be blocked until all required checks pass.
