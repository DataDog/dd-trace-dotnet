---
name: warn-interpolated-log
enabled: true
event: file
conditions:
  - field: file_path
    operator: regex_match
    pattern: \.cs$
  - field: new_text
    operator: regex_match
    pattern: "Log\\.\\w+\\(\\$\""
action: warn
---

**Don't use string interpolation in log calls!**

Interpolated strings like `Log.Debug($"value: {x}")` allocate a string even when the log level is disabled.

Use structured format strings instead:

```csharp
// BAD - always allocates
Log.Debug($"Processing {count} items");

// GOOD - no allocation when debug is off
Log.Debug("Processing {Count} items", count);
```
