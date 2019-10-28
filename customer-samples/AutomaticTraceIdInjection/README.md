# Automatic Trace ID injection

The .NET Tracer uses the LibLog library to automatically inject trace IDs into the application logs. It contains transparent built-in support for injecting into NLog, Log4Net, and Serilog.

After setting `DD_LOGS_INJECTION=true`, the layout of the application's logger must be modified to correctly log the `dd.trace_id` property and the `dd.span_id` property so that the logs and traces are correlated. The example applications in this repo demonstrate how to do that with popular layouts in each supported logging framework.

If there is a logging layout that you would like to see documented here, please feel free to reach out with an issue or contribution!

## Supported Logging Frameworks
### Log4Net
Layouts configured in the sample:
- `PatternLayout` (built-in, text-based layout that requires a conversion formatter)
- JSON `SerializedLayout` (from the `log4net.Ext.Json` NuGet package)

### NLog
Layouts configured in the sample:
- `SimpleLayout` (built-in, text-based layout that requires a conversion formatter)
- `JsonLayout` (built-in)

### Serilog
Layouts configured in the sample:
- output template (built-in, text-based layout that requires a conversion formatter)
- `JsonFormatter` (built-in)
- `CompactJsonFormatter` (from the `Serilog.Formatting.Compact` NuGet package)