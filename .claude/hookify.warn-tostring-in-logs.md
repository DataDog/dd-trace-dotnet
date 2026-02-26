---
name: warn-tostring-in-logs
enabled: true
event: file
conditions:
  - field: file_path
    operator: regex_match
    pattern: \.cs$
  - field: new_text
    operator: regex_match
    pattern: Log\.\w+.*\.ToString\(\)
action: warn
---

**Don't use `.ToString()` on numeric types in log calls!**

This allocates a string unnecessarily. Use generic log method overloads instead.

```csharp
// BAD - allocates a string
Log.Debug(ex, "Error (attempt {Attempt})", (attempt + 1).ToString());

// GOOD - uses generic method, no allocation
Log.Debug<int>(ex, "Error (attempt {Attempt})", attempt + 1);
```
