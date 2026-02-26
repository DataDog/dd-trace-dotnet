---
name: warn-sh-line-endings
enabled: true
event: file
conditions:
  - field: file_path
    operator: regex_match
    pattern: \.sh$
action: warn
---

**Editing a .sh file — use LF line endings, not CRLF.**

Shell scripts with CRLF line endings fail inside Docker containers.
Ensure this file uses LF (`\n`) only, even on Windows.
