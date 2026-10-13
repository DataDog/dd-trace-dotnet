#!/usr/bin/env bash

set -euo pipefail

fail() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

[[ $# -eq 0 ]] || fail "this script does not accept arguments"

tool="${REVIEWER_TOOL:-}"

if [[ -n "$tool" ]]; then
    case "$tool" in
        codex|claude) command -v "$tool" >/dev/null 2>&1 || fail "$tool was not found in PATH" ;;
        *) fail "REVIEWER_TOOL must be 'codex' or 'claude'" ;;
    esac
elif command -v codex >/dev/null 2>&1 && command -v claude >/dev/null 2>&1; then
    printf 'Choose a reviewer:\n  1) Codex\n  2) Claude Code\n'
    read -r -p "Selection [1-2]: " selection ||
        fail "could not read reviewer selection"
    case "$selection" in
        1) tool="codex" ;;
        2) tool="claude" ;;
        *) fail "invalid selection" ;;
    esac
elif command -v codex >/dev/null 2>&1; then
    tool="codex"
elif command -v claude >/dev/null 2>&1; then
    tool="claude"
else
    fail "install Codex CLI or Claude Code before running a review"
fi

if [[ "$tool" == "codex" ]]; then
    printf 'Running Codex review...\n'
    codex /review
else
    printf 'Running Claude Code review...\n'
    claude /code-review
fi
