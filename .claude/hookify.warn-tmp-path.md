---
name: warn-tmp-path
enabled: true
event: bash
pattern: /tmp/
action: warn
---

**`/tmp/` may not resolve correctly on Windows!**

On Windows with Git Bash, `/tmp/` maps to a path that other tools (Read, Edit) can't access via their Windows path resolution.

**Safe alternatives:**
- Use `$TMP` or `$TEMP` environment variable in bash
- Convert with `cygpath -w` before passing to non-bash tools
- For PowerShell, use `$env:TEMP` (resolves to `D:/lucas/temp/`)
