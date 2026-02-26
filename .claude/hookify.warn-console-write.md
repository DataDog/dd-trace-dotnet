---
name: warn-console-write
enabled: true
event: file
conditions:
  - field: file_path
    operator: regex_match
    pattern: \.cs$
  - field: new_text
    operator: regex_match
    pattern: Console\.Write
action: warn
---

**Avoid `Console.Write`/`Console.WriteLine` in tracer code.**

This is a production library that runs in-process with customer applications. Writing to the console can interfere with application output.

Use the internal logging API instead:

```csharp
Log.Debug("message");
Log.Information("message");
Log.Warning("message");
Log.Error("message");
```
