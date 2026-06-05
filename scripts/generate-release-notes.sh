#!/usr/bin/env bash
set -euo pipefail

MAX_COMMITS="${MAX_COMMITS:-200}"

PREV_TAG="$(git tag -l 'website-*' --sort=-creatordate | head -1 || true)"

if [[ -z "${PREV_TAG}" ]]; then
  RANGE=""
else
  RANGE="${PREV_TAG}..HEAD"
fi

mapfile -t COMMITS < <(
  if [[ -n "${RANGE}" ]]; then
    git log "${RANGE}" --no-merges --pretty=format:'%s|%h'
  else
    git log --no-merges --reverse --pretty=format:'%s|%h'
  fi
)

TOTAL="${#COMMITS[@]}"

if [[ "${TOTAL}" -eq 0 ]]; then
  echo "No commits since last release."
  exit 0
fi

SHOWN="${TOTAL}"
if [[ "${TOTAL}" -gt "${MAX_COMMITS}" ]]; then
  SHOWN="${MAX_COMMITS}"
fi

for ((i = 0; i < SHOWN; i++)); do
  SUBJECT="${COMMITS[$i]%%|*}"
  SHA="${COMMITS[$i]##*|}"
  echo "- ${SUBJECT} (\`${SHA}\`)"
done

REMAINING=$((TOTAL - SHOWN))
if [[ "${REMAINING}" -gt 0 ]]; then
  echo
  echo "_And ${REMAINING} more commit(s)._"
fi
