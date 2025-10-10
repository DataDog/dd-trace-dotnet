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

## Logs Search API

### Search for logs with specific criteria

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
