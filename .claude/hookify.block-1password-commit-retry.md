---
name: block-1password-commit-retry
enabled: true
event: bash
pattern: git\s+commit
action: warn
---

**1Password signing reminder.**

If this `git commit` fails with "1Password: agent returned an error", DO NOT retry.
The user is AFK and 1Password is awaiting authentication. Abort and inform the user.
