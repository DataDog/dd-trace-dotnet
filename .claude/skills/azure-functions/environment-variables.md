# Azure Functions Environment Variables

This guide documents the environment variables needed for Datadog instrumentation in Azure Functions (Linux).

## Required Variables

These variables are **essential** for the tracer to load and function:

```bash
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
CORECLR_PROFILER_PATH=/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so
DD_DOTNET_TRACER_HOME=/home/site/wwwroot/datadog
DD_API_KEY=<YOUR_API_KEY>
```

### Variable Explanations

- **`CORECLR_ENABLE_PROFILING`**: Enables the CLR profiling API (required for auto-instrumentation)
- **`CORECLR_PROFILER`**: GUID identifying the Datadog profiler
- **`CORECLR_PROFILER_PATH`**: Absolute path to the native profiler library
- **`DD_DOTNET_TRACER_HOME`**: Directory containing tracer managed assemblies
- **`DD_API_KEY`**: Your Datadog API key for submitting traces/metrics/logs

## Required for Agent Process

The tracer requires a background agent process to handle trace/metric/log submission. This is automatically started if:

```bash
DOTNET_STARTUP_HOOKS=/home/site/wwwroot/Datadog.Serverless.Compat.dll
```

**Alternative**: If your function code calls `Datadog.Serverless.CompatibilityLayer.Start()` during startup, the startup hook is not required.

## Recommended Variables

### Disable Unsupported Features

Azure Functions does not support these Datadog features. Explicitly disable them to avoid unnecessary overhead:

```bash
DD_APPSEC_ENABLED=false
DD_CIVISIBILITY_ENABLED=false
DD_REMOTE_CONFIGURATION_ENABLED=false
DD_AGENT_FEATURE_POLLING_ENABLED=false
```

### Environment and Service Identification

```bash
DD_ENV=<your_environment>         # e.g., "production", "staging"
DD_SERVICE=<your_service_name>    # Override default service name
DD_VERSION=<your_app_version>     # Application version for trace filtering
```

### Sampling Rules

Control which requests are sampled to reduce costs and noise:

```bash
DD_TRACE_SAMPLING_RULES=[{"resource": "GET /admin/*", "sample_rate": 0}, {"resource": "POST /admin/*", "sample_rate": 0}]
```

**Note**: Adjust rules based on your function routes and traffic patterns.

## Debugging Variables

Enable detailed logging when troubleshooting instrumentation issues:

### Tracer Debug Logging

```bash
DD_TRACE_DEBUG=true                                     # Enable tracer debug logs
DD_TRACE_LOG_SINKS=file,console-experimental           # Log to both file and console
```

**Log file locations:**
- Worker: `/home/LogFiles/Datadog/dotnet-tracer-worker-*.log`
- Host: `/home/LogFiles/Datadog/dotnet-tracer-host-*.log`

### Agent Debug Logging

```bash
DD_LOG_LEVEL=debug                                     # Set agent log level (error, warn, info, debug)
```

**Note**: `DD_TRACE_DEBUG` controls tracer logging, while `DD_LOG_LEVEL` controls the agent process logging. Set both for comprehensive debugging.

### Direct Log Submission

Send logs directly to Datadog (bypasses agent log collection):

```bash
DD_LOGS_DIRECT_SUBMISSION_AZURE_FUNCTIONS_HOST_ENABLED=true
DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS=ILogger
```

**Use cases:**
- Correlate logs with traces in Datadog UI
- Debug issues where traces appear but logs don't
- Verify ILogger integration is capturing function logs

## Windows-Specific Variables

For Windows-based Azure Functions, use these paths:

```bash
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
CORECLR_PROFILER_PATH_32=C:\home\site\wwwroot\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll
CORECLR_PROFILER_PATH_64=C:\home\site\wwwroot\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll
DD_DOTNET_TRACER_HOME=C:\home\site\wwwroot\datadog
DOTNET_STARTUP_HOOKS=C:\home\site\wwwroot\Datadog.Serverless.Compat.dll
```

**Key differences from Linux**:
- **Paths**: Windows uses `C:\home\site\wwwroot\...` instead of `/home/site/wwwroot/...`
- **Path separators**: Windows uses backslashes (`\`) instead of forward slashes (`/`)
- **Architecture**: Windows requires **separate 32-bit and 64-bit profiler paths** using `CORECLR_PROFILER_PATH_32` and `CORECLR_PROFILER_PATH_64` (not `CORECLR_PROFILER_PATH`)
  - 32-bit: `win-x86\Datadog.Trace.ClrProfiler.Native.dll`
  - 64-bit: `win-x64\Datadog.Trace.ClrProfiler.Native.dll`
- **Linux**: Uses single `CORECLR_PROFILER_PATH` variable pointing to `linux-x64/Datadog.Trace.ClrProfiler.Native.so`

**Note**: Azure Functions only supports .NET 6+ (no .NET Framework), so always use `CORECLR_*` prefix on both Windows and Linux.

## Advanced Configuration

### Performance Tuning

```bash
DD_TRACE_BUFFER_SIZE=4096                              # Trace buffer size (default: 1024)
DD_TRACE_AGENT_MAX_CONNECTIONS=10                      # Max concurrent connections to agent
DD_TRACE_PARTIAL_FLUSH_ENABLED=true                    # Flush traces before buffer is full
```

### Agent Configuration

```bash
DD_AGENT_HOST=localhost                                # Agent hostname (default: localhost)
DD_TRACE_AGENT_PORT=8126                               # Trace agent port (default: 8126)
DD_DOGSTATSD_PORT=8125                                 # DogStatsD port (default: 8125)
```

**Note**: In Azure Functions, the agent runs as a child process and uses the default localhost/ports.

### Integration-Specific

```bash
DD_TRACE_HTTP_CLIENT_ENABLED=true                      # Trace HttpClient calls
DD_TRACE_ASPNETCORE_ENABLED=true                       # Trace ASP.NET Core middleware
DD_TRACE_LOGS_INJECTION_ENABLED=true                   # Inject trace IDs into logs
```

## Verification Commands

After configuring environment variables, verify the setup:

### Using Azure CLI

```bash
# View all app settings
az functionapp config appsettings list \
  --name <app-name> \
  --resource-group <resource-group>

# Check specific setting
az functionapp config appsettings list \
  --name <app-name> \
  --resource-group <resource-group> \
  --query "[?name=='DD_TRACE_DEBUG'].value" -o tsv
```

### Using Azure Portal

1. Navigate to your Function App
2. Go to **Settings** → **Configuration**
3. Check **Application settings** tab
4. Verify all required variables are present

### From Logs

After triggering a function, check worker logs for configuration values:

```bash
# Download logs
az functionapp logs download \
  --name <app-name> \
  --resource-group <resource-group>

# Check configuration in logs
grep "DD_TRACE_DEBUG" worker.log
grep "TracerSettings" worker.log
```

## Common Issues

### Issue: Tracer not loading

**Check:**
- `CORECLR_ENABLE_PROFILING=1` is set
- `CORECLR_PROFILER_PATH` points to correct .so file
- Profiler DLL exists at specified path

**Verify in logs:**
```bash
grep "Assembly metadata" worker.log
```

### Issue: No traces appearing in Datadog

**Check:**
- `DD_API_KEY` is set correctly
- Agent process is running (check for "ServerlessAgent" in logs)
- No network issues (check for "Failed to send" in logs)

**Verify in logs:**
```bash
grep "ServerlessAgent" worker.log
grep "Failed to send" worker.log
```

### Issue: Debug logs not appearing

**Check:**
- `DD_TRACE_DEBUG=true` is set
- `DD_TRACE_LOG_SINKS` includes `file` or `console-experimental`
- Log files exist in `/home/LogFiles/Datadog/`

**Verify:**
```bash
ls -la /home/LogFiles/Datadog/
```

## Setting Variables via Azure CLI

### Set individual variable

```bash
az functionapp config appsettings set \
  --name <app-name> \
  --resource-group <resource-group> \
  --settings "DD_TRACE_DEBUG=true"
```

### Set multiple variables

```bash
az functionapp config appsettings set \
  --name <app-name> \
  --resource-group <resource-group> \
  --settings \
    "DD_TRACE_DEBUG=true" \
    "DD_TRACE_LOG_SINKS=file,console-experimental" \
    "DD_LOG_LEVEL=debug"

# Note: DD_TRACE_DEBUG is for tracer, DD_LOG_LEVEL is for agent
```

### Delete a variable

```bash
az functionapp config appsettings delete \
  --name <app-name> \
  --resource-group <resource-group> \
  --setting-names "DD_TRACE_DEBUG"
```

## Best Practices

1. **Keep debug logging off in production** - Only enable when actively troubleshooting
2. **Use sampling rules** - Reduce trace volume for high-traffic functions
3. **Set DD_ENV consistently** - Match your deployment environment
4. **Disable unused features** - Explicitly set AppSec/CI Visibility/RCM to false
5. **Monitor log file sizes** - Debug logging can generate large files quickly
6. **Use direct log submission selectively** - Only enable when needed for correlation
7. **Version your configuration** - Document env var changes alongside code deployments

## Reference

- [Datadog Azure Functions documentation](https://docs.datadoghq.com/serverless/azure_app_services/)
- [Tracer configuration reference](https://docs.datadoghq.com/tracing/trace_collection/library_config/dotnet-core/)
- [Azure Functions environment variables](https://learn.microsoft.com/en-us/azure/azure-functions/functions-app-settings)
