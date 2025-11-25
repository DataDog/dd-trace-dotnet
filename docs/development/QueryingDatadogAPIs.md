# Querying Datadog APIs for Debugging

When troubleshooting tracer issues, you can query spans and logs directly from the Datadog API to verify instrumentation behavior, span relationships, and context propagation.

## Spans Search API

### Search for spans with specific criteria

```bash
curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: <your-api-key>" \
  -H "DD-APPLICATION-KEY: <your-app-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "data": {
      "attributes": {
        "filter": {
          "query": "service:<service-name> env:<env-name>",
          "from": "now-15m",
          "to": "now"
        },
        "sort": "-timestamp",
        "page": {
          "limit": 20
        }
      },
      "type": "search_request"
    }
  }' | jq -r '.data[] | .attributes | "Name: \(.operation_name)\nResource: \(.resource_name)\nSpan ID: \(.span_id)\nParent ID: \(.parent_id)\nTrace ID: \(.trace_id)\n---"'
```

### Get all spans in a specific trace to verify parent-child relationships

```bash
curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
  -H "DD-API-KEY: <your-api-key>" \
  -H "DD-APPLICATION-KEY: <your-app-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "data": {
      "attributes": {
        "filter": {
          "query": "trace_id:<trace-id>",
          "from": "now-1h",
          "to": "now"
        },
        "sort": "-timestamp",
        "page": {
          "limit": 50
        }
      },
      "type": "search_request"
    }
  }' | jq -r '.data[] | .attributes | "Name: \(.operation_name)\nResource: \(.resource_name)\nSpan ID: \(.span_id)\nParent ID: \(.parent_id)\n---"'
```

## Important Notes

- The request body must be wrapped in `{"data": {"attributes": {...}, "type": "search_request"}}` structure
- API keys can be obtained from https://app.datadoghq.com/organization-settings/api-keys
- Full API documentation: https://docs.datadoghq.com/api/latest/spans/
- **Shell variable substitution issue**: If using environment variables like `${DD_API_KEY}` fails with "Unauthorized" errors, try using the key values directly in the curl command instead of shell variable substitution. Some shells may have issues with variable expansion in curl headers.

## Common Use Cases

- **Verifying span parenting**: Check that `parent_id` matches the expected `span_id`
- **Debugging distributed tracing context propagation**: Verify trace context flows correctly across process boundaries
- **Validating span tags and attributes**: Ensure custom tags and integration-specific attributes are set correctly
- **Investigating timing and duration issues**: Compare span durations and timestamps to identify bottlenecks

## Query Syntax

The `query` field supports the Datadog query syntax:

- `service:<service-name>` - Filter by service name
- `env:<environment>` - Filter by environment
- `resource_name:<pattern>` - Filter by resource name (supports wildcards: `*HttpTest*`)
- `trace_id:<trace-id>` - Get all spans in a specific trace
- `operation_name:<op-name>` - Filter by operation name
- Combine with boolean operators: `service:my-service AND env:prod`
- Exclude with `-`: `service:my-service -resource_name:*ping*`

## Response Structure

The Spans API returns data in the following structure:

```json
{
  "data": [
    {
      "id": "...",
      "type": "spans",
      "attributes": {
        "operation_name": "azure_functions.invoke",
        "resource_name": "GET /api/endpoint",
        "span_id": "4208934728885019856",
        "parent_id": "0",
        "trace_id": "690507fc00000000b882bcd2bdac6b9e",
        "start_timestamp": 1761937404333164200,
        "end_timestamp": 1761937404533785600,
        "status": "ok",
        "service": "service-name",
        "env": "environment",
        "tags": ["tag1:value1", "tag2:value2"],
        "custom": {
          "duration": 200621200,
          "aas": {
            "function": {
              "process": "host",
              "name": "HttpTest"
            }
          }
        }
      }
    }
  ]
}
```

Key fields:
- `attributes.span_id` and `attributes.parent_id` - Direct span relationship fields (not in `custom`)
- `attributes.operation_name` and `attributes.resource_name` - At root of `attributes`
- `attributes.custom.*` - Custom tags and metadata (including nested objects like `aas.function.process`)
- `attributes.tags[]` - Array of tag strings in `key:value` format

## Example: Debugging Span Parenting

When investigating span parenting issues (e.g., verifying that child spans are correctly linked to parent spans):

1. Trigger the operation you want to debug
2. Query for recent spans to get a trace ID:
   ```bash
   curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
     -H "DD-API-KEY: <key>" \
     -H "DD-APPLICATION-KEY: <app-key>" \
     -H "Content-Type: application/json" \
     -d '{
       "data": {
         "attributes": {
           "filter": {
             "query": "service:my-service resource_name:*MyFunction*",
             "from": "now-5m",
             "to": "now"
           },
           "sort": "-timestamp",
           "page": {"limit": 5}
         },
         "type": "search_request"
       }
     }' | jq -r '.data[0].attributes.trace_id'
   ```

3. Get all spans in that trace:
   ```bash
   curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
     -H "DD-API-KEY: <key>" \
     -H "DD-APPLICATION-KEY: <app-key>" \
     -H "Content-Type: application/json" \
     -d '{
       "data": {
         "attributes": {
           "filter": {
             "query": "trace_id:<trace-id-from-step-2>",
             "from": "now-1h",
             "to": "now"
           },
           "sort": "-timestamp",
           "page": {"limit": 50}
         },
         "type": "search_request"
       }
     }' | jq -r '.data[] | .attributes | "Name: \(.operation_name)\nResource: \(.resource_name)\nSpan ID: \(.span_id)\nParent ID: \(.parent_id)\nDuration: \(.custom.duration // 0)\n---"'
   ```

4. Verify the parent-child relationships by checking that each span's `parent_id` matches another span's `span_id` in the trace.

## PowerShell Helper Script

The repository includes a PowerShell script that simplifies querying traces from the Datadog API.

### Get-DatadogTrace.ps1

**Location**: `tracer/tools/Get-DatadogTrace.ps1`

This script retrieves all spans for a given trace ID and displays them in various formats.

**Prerequisites:**
- Set environment variables: `DD_API_KEY` and `DD_APPLICATION_KEY`
- API keys can be obtained from https://app.datadoghq.com/organization-settings/api-keys

**Basic usage:**
```powershell
.\tracer\tools\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e"
```

**Parameters:**
- `-TraceId` (required) - The 128-bit trace ID in hex format
- `-TimeRange` (optional) - How far back to search (default: "2h"). Examples: "15m", "1h", "2h", "1d"
- `-OutputFormat` (optional) - Output format: "table" (default), "json", or "hierarchy"

**Output formats:**

1. **Table** (default) - Formatted table with key span information:
   ```powershell
   .\tracer\tools\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e"
   ```
   Shows: Operation, Resource, Span ID, Parent ID, Process, Duration

2. **Hierarchy** - Tree view showing parent-child relationships:
   ```powershell
   .\tracer\tools\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e" -OutputFormat hierarchy
   ```
   Useful for visualizing span nesting and verifying proper parent-child relationships

3. **JSON** - Raw JSON output for further processing:
   ```powershell
   .\tracer\tools\Get-DatadogTrace.ps1 -TraceId "690507fc00000000b882bcd2bdac6b9e" -OutputFormat json
   ```

**Example workflow:**

1. Get a recent trace ID from your service:
   ```bash
   curl -s -X POST https://api.datadoghq.com/api/v2/spans/events/search \
     -H "DD-API-KEY: $DD_API_KEY" \
     -H "DD-APPLICATION-KEY: $DD_APPLICATION_KEY" \
     -H "Content-Type: application/json" \
     -d '{"data":{"attributes":{"filter":{"query":"service:my-service","from":"now-5m","to":"now"},"sort":"-timestamp","page":{"limit":1}},"type":"search_request"}}' \
     | jq -r '.data[0].attributes.trace_id'
   ```

2. Analyze the full trace with the PowerShell script:
   ```powershell
   .\tracer\tools\Get-DatadogTrace.ps1 -TraceId "<trace-id-from-step-1>" -OutputFormat hierarchy
   ```

**Note**: The script uses `Invoke-RestMethod` which properly handles authentication headers, avoiding the shell variable substitution issues that can occur with curl.

## Logs Search API

### Search for logs with specific criteria (curl)

```bash
curl -s -X POST https://api.datadoghq.com/api/v2/logs/events/search \
  -H "DD-API-KEY: <your-api-key>" \
  -H "DD-APPLICATION-KEY: <your-app-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "filter": {
      "query": "service:<service-name> env:<env-name>",
      "from": "now-15m",
      "to": "now"
    },
    "sort": "timestamp",
    "page": {
      "limit": 100
    }
  }' | jq -r '.data[] | .attributes | "\(.timestamp) [\(.status)] \(.message)"'
```

**Note:** Unlike the Spans API, the Logs API does not require the `{"data": {"attributes": {...}, "type": "search_request"}}` wrapper structure. The request body is directly the filter/sort/page configuration.

## PowerShell Helper Script - Logs

The repository includes a PowerShell script that simplifies querying logs from the Datadog API.

### Get-DatadogLogs.ps1

**Location**: `tracer/tools/Get-DatadogLogs.ps1`

This script retrieves logs matching a query and displays them in various formats.

**Prerequisites:**
- Set environment variables: `DD_API_KEY` and `DD_APPLICATION_KEY`
- API keys can be obtained from https://app.datadoghq.com/organization-settings/api-keys

**Basic usage:**
```powershell
.\tracer\tools\Get-DatadogLogs.ps1 -Query "service:my-service error"
```

**Parameters:**
- `-Query` (required) - Log query using Datadog query syntax (e.g., "service:my-service error")
- `-TimeRange` (optional) - How far back to search (default: "1h"). Examples: "15m", "1h", "2h", "1d"
- `-Limit` (optional) - Maximum number of log entries to return (default: 50, max: 1000)
- `-OutputFormat` (optional) - Output format: "table" (default), "json", or "raw"

**Output formats:**

1. **Table** (default) - Formatted table with key log information:
   ```powershell
   .\tracer\tools\Get-DatadogLogs.ps1 -Query "service:my-service error"
   ```
   Shows: Timestamp, Status, Service, Host, Message (truncated to 100 chars)

2. **JSON** - Raw JSON output for further processing:
   ```powershell
   .\tracer\tools\Get-DatadogLogs.ps1 -Query "service:my-service" -OutputFormat json
   ```

3. **Raw** - Simple timestamp and message format:
   ```powershell
   .\tracer\tools\Get-DatadogLogs.ps1 -Query "service:my-service" -OutputFormat raw
   ```

**Example workflows:**

1. Search for recent errors in a service:
   ```powershell
   .\tracer\tools\Get-DatadogLogs.ps1 -Query "service:my-service error" -TimeRange "1h" -Limit 20
   ```

2. Find tracer debug logs from Azure Functions:
   ```powershell
   .\tracer\tools\Get-DatadogLogs.ps1 -Query "service:lucasp-premium-linux-isolated AspNetCoreDiagnosticObserver" -TimeRange "30m"
   ```

3. Export logs as JSON for processing:
   ```powershell
   .\tracer\tools\Get-DatadogLogs.ps1 -Query "service:my-service DD-TRACE-DOTNET" -OutputFormat json | ConvertFrom-Json | ConvertTo-Json -Depth 10
   ```

**Note**: The script uses `Invoke-RestMethod` which properly handles authentication headers, avoiding the shell variable substitution issues that can occur with curl.

### Search for tracer debug logs

```bash
curl -s -X POST https://api.datadoghq.com/api/v2/logs/events/search \
  -H "DD-API-KEY: <your-api-key>" \
  -H "DD-APPLICATION-KEY: <your-app-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "filter": {
      "query": "env:<env-name> DD-TRACE-DOTNET",
      "from": "now-15m",
      "to": "now"
    },
    "sort": "timestamp",
    "page": {
      "limit": 100
    }
  }' | jq -r '.data[] | .attributes | "\(.timestamp) - \(.message[:150])"'
```

### Search for logs with specific attributes

You can filter by attributes using the `@` prefix:

```bash
curl -s -X POST https://api.datadoghq.com/api/v2/logs/events/search \
  -H "DD-API-KEY: <your-api-key>" \
  -H "DD-APPLICATION-KEY: <your-app-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "filter": {
      "query": "env:<env-name> @Category:Host.Function.Console",
      "from": "now-15m",
      "to": "now"
    },
    "sort": "timestamp",
    "page": {
      "limit": 50
    }
  }' | jq -r '.data[] | .attributes | "\(.timestamp) - \(.attributes.Category) - \(.message[:100])"'
```

### Use absolute timestamps for precise time ranges

When debugging specific function invocations, use absolute timestamps:

```bash
curl -s -X POST https://api.datadoghq.com/api/v2/logs/events/search \
  -H "DD-API-KEY: <your-api-key>" \
  -H "DD-APPLICATION-KEY: <your-app-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "filter": {
      "query": "env:<env-name> my-search-term",
      "from": "2025-10-08T21:01:40",
      "to": "2025-10-08T21:02:10"
    },
    "sort": "timestamp",
    "page": {
      "limit": 100
    }
  }' | jq -r '.data[] | .attributes | "\(.timestamp) - \(.message)"'
```

## Azure Functions Logging Configuration

### Enabling Direct Log Submission

For Azure Functions, tracer logs can be sent directly to Datadog via direct log submission:

**Worker Process Logs:**
- Set `DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS=ILogger` to enable direct log submission from the worker process
- Worker logs will appear with `service:<function-app-name>` and may include `Category:Host.Function.Console`

**Host Process Logs:**
- Set `DD_LOGS_DIRECT_SUBMISSION_AZURE_FUNCTIONS_HOST_ENABLED=true` to enable direct log submission from the Azure Functions host process
- Host process logs contain tracer diagnostics from the host (e.g., `GrpcMessageConversionExtensions.ToRpcHttp`)
- Requires tracer version 3.29.0 or later

**Debug Logging:**
- Set `DD_TRACE_DEBUG=true` to enable debug-level logging from the tracer
- Debug logs will include `DD-TRACE-DOTNET` prefix in the message
- Both `Log.Debug()` and `Log.Information()` calls will appear in Datadog

### Common Log Categories

Azure Functions logs may be tagged with different categories:

- `Host.Function.Console` - Console output from worker process functions
- `Microsoft.Azure.WebJobs.Script.WebHost.Middleware.*` - Host middleware logs
- `Function.<FunctionName>` - Function-specific execution logs
- `Microsoft.AspNetCore.*` - ASP.NET Core framework logs (when using ASP.NET Core integration)

## API Documentation

- **Spans API**: https://docs.datadoghq.com/api/latest/spans/
- **Logs API**: https://docs.datadoghq.com/api/latest/logs/
