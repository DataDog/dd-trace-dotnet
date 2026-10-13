#!/usr/bin/env bash

# Set REVIEWER_TOOL to "codex" or "claude" to skip the selection prompt.

set -euo pipefail

fail() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

[[ $# -eq 0 ]] || fail "this script does not accept arguments"

tool="${REVIEWER_TOOL:-}"
show_tip=false

if [[ -n "$tool" ]]; then
    case "$tool" in
        codex) command -v codex >/dev/null 2>&1 || fail "REVIEWER_TOOL selects 'codex', but Codex CLI was not found in PATH" ;;
        claude) command -v claude >/dev/null 2>&1 || fail "REVIEWER_TOOL selects 'claude', but Claude Code was not found in PATH" ;;
        *) fail "REVIEWER_TOOL must be 'codex' or 'claude'" ;;
    esac
    printf 'Using %s from REVIEWER_TOOL.\n' "$tool"
elif command -v codex >/dev/null 2>&1 && command -v claude >/dev/null 2>&1; then
    printf 'Choose a reviewer:\n  1) Codex\n  2) Claude Code\n'
    if ! read -r -p "Selection [1-2]: " selection && [[ -z "${selection:-}" ]]; then
        fail "could not read reviewer selection"
    fi
    case "$selection" in
        1) tool="codex" ;;
        2) tool="claude" ;;
        *) fail "invalid selection" ;;
    esac
    show_tip=true
elif command -v codex >/dev/null 2>&1; then
    tool="codex"
elif command -v claude >/dev/null 2>&1; then
    tool="claude"
else
    fail "install Codex CLI or Claude Code before running a review"
fi

status=0
if [[ "$tool" == "codex" ]]; then
    printf 'Running Codex review...\n'
    codex /review || status=$?
else
    printf 'Running Claude Code review...\n'
    claude /code-review || status=$?
fi

if [[ "$show_tip" == true ]]; then
    printf 'Tip: add export REVIEWER_TOOL=%s to your shell profile to skip this prompt.\n' "$tool"
fi

exit "$status"
