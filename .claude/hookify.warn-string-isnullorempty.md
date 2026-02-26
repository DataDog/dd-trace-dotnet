---
name: warn-string-isnullorempty
enabled: true
event: file
conditions:
  - field: file_path
    operator: regex_match
    pattern: \.cs$
  - field: new_text
    operator: regex_match
    pattern: string\.IsNullOrEmpty\(
action: warn
---

**Use `StringUtil.IsNullOrEmpty()` instead of `string.IsNullOrEmpty()`**

Per coding standards in AGENTS.md, this codebase uses `StringUtil.IsNullOrEmpty()` for compatibility across all supported .NET runtimes.
