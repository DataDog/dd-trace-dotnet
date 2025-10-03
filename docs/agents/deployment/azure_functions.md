# Azure Functions

Azure Functions supports multiple hosting plans with different instrumentation approaches.

## Automatic Instrumentation Setup

### Windows (Premium / Elastic Premium / Dedicated / App Services hosting plans)

Use the Azure App Services Site Extension, not the NuGet package.

### Other scenarios (e.g., Linux Consumption, Container Apps, Flex Consumption)

1. **Install dependencies**: Add `Datadog.AzureFunctions` NuGet package
2. **Start the Datadog Serverless Compatibility Layer**:
   - **Isolated Worker model**: Add `Datadog.Serverless.CompatibilityLayer.Start();` to main entry point
   - **In-Container model**: Add `Microsoft.Azure.Functions.Extensions` package and create startup class:
     ```csharp
     using Datadog.Serverless;
     using Microsoft.Azure.Functions.Extensions.DependencyInjection;

     [assembly: FunctionsStartup(typeof(MyFunctionApp.Startup))]

     namespace MyFunctionApp
     {
        public class Startup : FunctionsStartup
        {
           public override void Configure(IFunctionsHostBuilder builder)
           {
                 Datadog.Serverless.CompatibilityLayer.Start();
           }
        }
     }
     ```

## Environment Variables

### Windows

```
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
CORECLR_PROFILER_PATH_64=C:\home\site\wwwroot\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll
CORECLR_PROFILER_PATH_32=C:\home\site\wwwroot\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll
DD_DOTNET_TRACER_HOME=C:\home\site\wwwroot\datadog
```

### Linux

```
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
CORECLR_PROFILER_PATH=/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so
DD_DOTNET_TRACER_HOME=/home/site/wwwroot/datadog
```

### Datadog Configuration

- `DD_API_KEY` — Datadog API key for sending data
- `DD_SITE` — Datadog site (e.g., `datadoghq.com`, `datadoghq.eu`, `us3.datadoghq.com`)
- `DD_AZURE_RESOURCE_GROUP` — Azure resource group (required for Flex Consumption plan)
- `DD_ENV` — Environment tag for Unified Service Tagging
- `DD_SERVICE` — Service name for Unified Service Tagging
- `DD_VERSION` — Version tag for Unified Service Tagging

## Development

- Integration details: See `docs/development/AzureFunctions.md` for in-process vs isolated worker model differences, instrumentation specifics, and ASP.NET Core integration.
- Tests: `BuildAndRunWindowsAzureFunctionsTests` Nuke target; samples under `tracer/test/test-applications/azure-functions/`.
- Dependencies: `Datadog.AzureFunctions` transitively references `Datadog.Serverless.Compat` ([datadog-serverless-compat-dotnet](https://github.com/DataDog/datadog-serverless-compat-dotnet)), which contains the Datadog Agent executable. The agent process is started either via `DOTNET_STARTUP_HOOKS` or by calling `Datadog.Serverless.CompatibilityLayer.Start()` explicitly during bootstrap in user code.

### Using local NuGet packages

Create `NuGet.config` with local source first for priority. Ensure transitive dependencies (e.g., `Datadog.Trace`) are also available locally or from nuget.org.

## Testing with Live Azure Functions

1. Create test function: `func init . --worker-runtime dotnet-isolated && func new --template "HTTP trigger" --name HttpTrigger`
2. Add `Datadog.AzureFunctions` package and call `Datadog.Serverless.CompatibilityLayer.Start()` in `Program.cs`
3. Publish: `func azure functionapp publish <function-app-name>`
4. Trigger the function: `curl https://<function-app-name>.azurewebsites.net/api/httptrigger`
5. Verify instrumentation:
   - **Spans**: Use the Datadog API to query spans (see Testing Guidelines > Verifying Instrumentation with Datadog API)
     - Filter by `env:your-env service:your-function-app-name origin:azurefunction`
     - Check for `operation_name:azure_functions.invoke`
   - **Logs**: Use the Datadog API to query logs (see Testing Guidelines > Verifying Logs with Datadog API)
     - Search by `env:your-env service:your-function-app-name "your log message"`

## Common Testing Scenarios

- Verify cold start instrumentation: Delete function app, republish, trigger, check first span
- Test different trigger types: HTTP, Queue, Timer, Blob, EventHub
- Validate custom tags: Check `attributes.custom.aas.*` tags in spans
- Verify Datadog Agent startup: Check logs for agent initialization messages
- Test isolated vs in-process: Compare instrumentation behavior between worker models
