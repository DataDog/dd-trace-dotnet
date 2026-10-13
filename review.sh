#!/usr/bin/env bash

set -euo pipefail

fail() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

[[ $# -eq 0 ]] || fail "this script does not accept arguments"

has_codex=false
has_claude=false
command -v codex >/dev/null 2>&1 && has_codex=true
command -v claude >/dev/null 2>&1 && has_claude=true

if [[ "$has_codex" == true && "$has_claude" == true ]]; then
    printf 'Choose a reviewer:\n  1) Codex\n  2) Claude Code\n'
    read -r -p "Selection [1-2]: " selection ||
        fail "could not read reviewer selection"
    case "$selection" in
        1) tool="codex" ;;
        2) tool="claude" ;;
        *) fail "invalid selection" ;;
    esac
elif [[ "$has_codex" == true ]]; then
    tool="codex"
elif [[ "$has_claude" == true ]]; then
    tool="claude"
else
    fail "install Codex CLI or Claude Code before running a review"
fi

if [[ "$tool" == "codex" ]]; then
    printf 'Running Codex review...\n'
    codex review
else
    printf 'Running Claude Code review...\n'
    claude --permission-mode plan -p "/code-review"
fi
