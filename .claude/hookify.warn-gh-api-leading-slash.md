---
name: warn-gh-api-leading-slash
enabled: true
event: bash
pattern: gh\s+api\s+/
action: block
---

**`gh api` called with a leading slash.**

Omit the leading `/` from the endpoint path:
- Wrong: `gh api /repos/OWNER/REPO/pulls`
- Right: `gh api repos/OWNER/REPO/pulls`
