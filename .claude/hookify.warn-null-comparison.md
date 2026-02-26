---
name: warn-null-comparison
enabled: true
event: file
conditions:
  - field: file_path
    operator: regex_match
    pattern: \.cs$
  - field: new_text
    operator: regex_match
    pattern: "[!=]=\\s*null"
action: warn
---

**Prefer `is null` / `is not null` over `== null` / `!= null`.**

Per coding standards, use modern C# pattern matching syntax:

```csharp
// BAD
if (value == null)
if (value != null)

// GOOD
if (value is null)
if (value is not null)
```
