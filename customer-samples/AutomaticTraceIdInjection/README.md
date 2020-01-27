# Automatic Trace ID injection
Follow the official documentation steps to set up [C# log collection](https://docs.datadoghq.com/logs/log_collection/csharp/) and [automatic trace ID injection](https://docs.datadoghq.com/tracing/connect_logs_and_traces/?tab=net), then run these samples to see the feature in action!

If there is a logging layout that you would like to see documented here, please feel free to reach out with an issue or contribution!

## Supported Logging Frameworks
### Log4Net
Layouts configured in the sample:
- JSON format: `SerializedLayout` (from the `log4net.Ext.Json` NuGet package)

### NLog
Layouts configured in the sample:
- JSON format: `JsonLayout` (built-in)

### Serilog
Layouts configured in the sample:
- Raw format: output template (built-in, requires a conversion formatter)
- JSON format: `JsonFormatter` (built-in)
- JSON format: `CompactJsonFormatter` (from the `Serilog.Formatting.Compact` NuGet package)