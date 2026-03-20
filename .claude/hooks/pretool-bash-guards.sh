#!/bin/bash
# PreToolUse hook: guards against common Bash mistakes.
# Input: JSON on stdin with .tool_input.command and .cwd
# Exit 0 = allow, Exit 2 = block (stderr shown to Claude)

input=$(cat)
command=$(echo "$input" | jq -r '.tool_input.command // empty')

[ -z "$command" ] && exit 0

is_windows=false
[ "$(uname -o 2>/dev/null)" = "Msys" ] && is_windows=true

# Rule: nul-redirect (BLOCK) — Windows only
# >nul and 2>nul create a literal file named "nul" that is nearly impossible to delete.
if $is_windows; then
  stripped=$(echo "$command" | sed '/<<.*EOF/,/^[[:space:]]*EOF/d' | sed "s/'[^']*'//g; s/\"[^\"]*\"//g")
  if echo "$stripped" | grep -qE '[12]?>+\s*nul\b' && ! echo "$stripped" | grep -qE '[12]?>+\s*\\\\\.\\NUL'; then
    # printf instead of echo: message contains backslashes that echo may interpret as escape sequences
    printf '%s\n' 'Do not use >nul or 2>nul on Windows. This creates a literal file named "nul" that is nearly impossible to delete. Safe alternatives: (1) omit the redirection entirely, (2) use 2>\\.\NUL for the full device path, (3) use dedicated tools (Grep, Glob, Read) instead of piped bash commands.' >&2
    exit 2
  fi
fi

# Rule: gh-api-leading-slash (BLOCK)
if echo "$command" | grep -qE 'gh\s+api\s+/'; then
  echo "Omit the leading / from gh api endpoint paths (wrong: gh api /repos/..., right: gh api repos/...)." >&2
  exit 2
fi

# Rule: redundant-cd (BLOCK)
# shellcheck disable=SC2016
if echo "$command" | grep -qE '(cd\s+"?[^;|& ]+\s*&&\s)|(git\s+-C\s+"?[^;|& ]+)'; then
  cwd=$(echo "$input" | jq -r '.cwd // "unknown"')
  target=""
  if [[ "$command" =~ cd[[:space:]]+([\"\']?)([^';''|''&'' '\"]+) ]]; then
    target="${BASH_REMATCH[2]}"
  elif [[ "$command" =~ git[[:space:]]+-C[[:space:]]+([\"\']?)([^';''|''&'' '\"]+) ]]; then
    target="${BASH_REMATCH[2]}"
  fi
  target="${target%\"}"
  target="${target%\'}"
  norm_cwd=$(cygpath -w "$cwd" 2>/dev/null || echo "$cwd")
  norm_target=$(cygpath -w "$target" 2>/dev/null || echo "$target")
  if [ "$norm_cwd" = "$norm_target" ]; then
    echo "Redundant \`cd\` detected. Current directory ($cwd) already matches target ($target). Don't use \`cd <path> && <command>\` or \`git -C <path> <command>\` when already in the target directory." >&2
    exit 2
  fi
fi

# Rule: tmp-path (BLOCK) — Windows only
# /tmp, $TMP, and $TEMP in Git Bash are virtual paths that don't map to the real Windows temp dir.
# shellcheck disable=SC2016
if $is_windows && \
   echo "$command" | grep -qE '(^|[[:space:];|&><"'"'"'])((/tmp($|[[:space:]/"'"'"']))|(\$(TMP|TEMP)\b)|(\$\{(TMP|TEMP)\}))' && \
   ! echo "$command" | grep -qE 'cygpath\s+(-w\s+)?(/tmp|\$TMP|\$TEMP|\$\{TMP\}|\$\{TEMP\})'; then
  tmp_win=$(cygpath -w "$TMP" 2>/dev/null || echo '%TMP%')
  echo "Do not use /tmp, \$TMP, or \$TEMP on Git Bash for Windows. These are virtual paths that don't map to the real Windows temp directory. Use the Windows temp path instead: $tmp_win (or run \`cygpath -w \$TMP\` to get it)." >&2
  exit 2
fi

# Rule: 1password-commit-retry (WARN)
if echo "$command" | grep -qE 'git\s+commit'; then
  jq -n '{
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      additionalContext: "If `git commit` fails with \"1Password: agent returned an error\", do NOT retry. Abort and inform the user."
    }
  }'
  exit 0
fi

exit 0
