#!/usr/bin/env bash
# Apply branch protection for main using a repository ruleset.
# Requires GitHub CLI: gh auth login (admin access to the repo).
#
# Usage:
#   bash scripts/configure-branch-protection.sh
#   bash scripts/configure-branch-protection.sh owner/repo
#
# To update an existing ruleset with the same name, delete it in
# Settings → Rules → Rulesets first, or adjust this script to PATCH by ID.
set -euo pipefail

REPO="${1:-$(gh repo view --json nameWithOwner -q .nameWithOwner)}"
RULESET_FILE=".github/rulesets/protect-main.json"

if [[ ! -f "${RULESET_FILE}" ]]; then
  echo "ERROR: missing ${RULESET_FILE}" >&2
  exit 1
fi

if ! command -v gh >/dev/null 2>&1; then
  echo "ERROR: GitHub CLI (gh) is required. Install from https://cli.github.com/" >&2
  exit 1
fi

echo "Applying ruleset to ${REPO} from ${RULESET_FILE}..."

EXISTING_ID="$(gh api "repos/${REPO}/rulesets" --jq '.[] | select(.name=="Protect main") | .id' 2>/dev/null | head -1 || true)"

if [[ -n "${EXISTING_ID}" ]]; then
  echo "Updating existing ruleset id=${EXISTING_ID}..."
  gh api --method PUT "repos/${REPO}/rulesets/${EXISTING_ID}" --input "${RULESET_FILE}"
else
  echo "Creating ruleset..."
  gh api --method POST "repos/${REPO}/rulesets" --input "${RULESET_FILE}"
fi

echo "Done. Verify at: https://github.com/${REPO}/settings/rules"
