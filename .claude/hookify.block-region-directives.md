---
name: block-region-directives
enabled: true
event: file
conditions:
  - field: file_path
    operator: regex_match
    pattern: \.cs$
  - field: new_text
    operator: regex_match
    pattern: "#region\\b"
action: block
---

**Do not use `#region` directives in C# code.**

This is a strict coding standard for this repository. Remove the `#region` / `#endregion` and keep the code flat.
