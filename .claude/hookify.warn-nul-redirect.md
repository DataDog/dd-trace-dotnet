---
name: warn-nul-redirect
enabled: true
event: bash
pattern: [12]?>nul\b(?!\.)
action: block
---

**Windows `>nul` redirection detected!**

Redirecting to `nul` on Windows can create a literal file named "nul" that is extremely difficult to delete.

**Safe alternatives:**
- Don't suppress output at all — let errors show naturally
- Use full device path: `2>\\.\NUL`
- Use dedicated tools (Grep, Glob, Read) instead of piped bash commands

See: https://github.com/anthropics/claude-code/issues/4928
