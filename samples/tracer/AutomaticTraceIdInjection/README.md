# Automatic Trace ID injection
Follow the official documentation steps to set up [C# log collection](https://docs.datadoghq.com/logs/log_collection/csharp/) and [automatic trace ID injection](https://docs.datadoghq.com/tracing/connect_logs_and_traces/?tab=net), then run these samples to see the feature in action!

If there is a logging layout that you would like to see documented here, please feel free to reach out with an issue or contribution!

## Supported Logging Frameworks
### Log4Net
Layouts configured in the sample:
- JSON format: `SerializedLayout` (from the `log4net.Ext.Json` NuGet package)
- Raw format: `PatternLayout` (requires a custom Datadog Log Pipeline for processing)

### NLog
Layouts configured in the sample:
- JSON format: `JsonLayout`
- Raw format: Custom layout (requires a custom Datadog Log Pipeline for processing)

### Serilog
Layouts configured in the sample:
- JSON format: `JsonFormatter`
- JSON format: `CompactJsonFormatter` (from the `Serilog.Formatting.Compact` NuGet package)
- Raw format: output template (requires a custom Datadog Log Pipeline for processing)

### Microsoft.Extensions.Logging
Log injection for Microsoft.Extensions.Logging uses auto-instrumentation to inject logs. In this sample the [Datadog.Monitoring.Distribution](https://www.nuget.org/packages/Datadog.Monitoring.Distribution/) NuGet package is used to enable automatic instrumentation, but you can use any of the installation methods described in [the documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/).  

Layouts configured in the sample:
- JSON format (from the `NetEscapades.Extensions.Logging.RollingFile` NuGet package)