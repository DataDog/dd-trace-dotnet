# Query Datadog API

When traces or logs have reached Datadog (e.g., verifying spans look correct, correlating logs with a trace ID), use these scripts. Both require `DD_API_KEY` and `DD_APPLICATION_KEY` environment variables.

**Retrieve all spans for a trace ID**:
```powershell
# Table view (default)
./tracer/tools/Get-DatadogTrace.ps1 -TraceId "<trace-id>"

# Hierarchy view — shows span parent-child tree with process tags
./tracer/tools/Get-DatadogTrace.ps1 -TraceId "<trace-id>" -OutputFormat hierarchy

# Search further back in time (default: 2h)
./tracer/tools/Get-DatadogTrace.ps1 -TraceId "<trace-id>" -TimeRange "1d"
```

**Query logs from Datadog**:
```powershell
./tracer/tools/Get-DatadogLogs.ps1 -Query "service:<app-name>"
./tracer/tools/Get-DatadogLogs.ps1 -Query "service:<app-name> error" -TimeRange "2h" -Limit 100
```

See [scripts-reference.md](scripts-reference.md) for full parameter reference.
