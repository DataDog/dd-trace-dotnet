#!/usr/bin/env bash
# PreToolUse hook: block edits to generated and vendored files

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

if echo "$FILE_PATH" | grep -qE '(\.g\.cs$|[/\\]Vendors[/\\]|[/\\]Generated[/\\])'; then
  echo "Blocked: $FILE_PATH is a generated or vendored file — do not edit directly" >&2
  exit 2
fi
