# Testing Guidelines

## Test Frameworks

- Frameworks: xUnit (managed), GoogleTest (native).
- Projects: `*.Tests.csproj` under `tracer/test`, native under `profiler/test`.
- Filters: `--filter "Category=Smoke"`, `--framework net6.0` as needed.
- Docker: Many integration tests require Docker; services in `docker-compose.yml`.
- Test style: Inline result variables in assertions. Prefer `SomeMethod().Should().Be(expected)` over storing intermediate `result` variables.

## Testing Patterns

### Abstraction for Testability

- Extract interfaces for environment/filesystem dependencies (e.g., `IEnvironmentVariableProvider`)
- Allows mocking in unit tests without affecting production performance
- Use struct implementations with generic constraints for zero-allocation production code
- Example: Managed loader tests use `MockEnvironmentVariableProvider` for isolation (see tracer/test/Datadog.Trace.Tests/ClrProfiler/Managed/Loader/)

## Verifying Instrumentation with Datadog API

The Datadog API allows querying spans to verify instrumentation is working correctly across all environments and platforms.

### Prerequisites

- API Key: Set as environment variable `DD_API_KEY` or use directly
- Application Key: Required for API access; set as `DD_APPLICATION_KEY` or use directly

### Search Spans Endpoint

- URL: `https://api.datadoghq.com/api/v2/spans/events/search`
- Method: `POST`
- Headers: `DD-API-KEY`, `DD-APPLICATION-KEY`, `Content-Type: application/json`

### Request Format

```json
{
  "data": {
    "attributes": {
      "filter": {
        "query": "env:your-env service:your-service",
        "from": "now-1h",
        "to": "now"
      },
      "sort": "-timestamp",
      "page": {
        "limit": 10
      }
    },
    "type": "search_request"
  }
}
```

### Query Syntax

- `env:your-env` - Filter by environment
- `host:your-hostname` - Filter by hostname
- `service:my-service` - Filter by service name
- `operation_name:azure_functions.invoke` - Filter by operation
- `resource_name:"GET /api/httptrigger"` - Filter by resource
- Combine with `AND` / `OR` operators

### Example with curl

```bash
curl -X POST "https://api.datadoghq.com/api/v2/spans/events/search" \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{"data": {"attributes": {"filter": {"query": "env:your-env service:your-service", "from": "now-24h", "to": "now"}, "sort": "-timestamp", "page": {"limit": 10}}, "type": "search_request"}}'
```

### Response Structure

- `data[]` - Array of span objects with attributes including:
  - `attributes.custom` - Custom tags (e.g., `aas.*`, `http.*`, `git.*`)
  - `attributes.operation_name` - Operation name
  - `attributes.resource_name` - Resource identifier
  - `attributes.service` - Service name
  - `attributes.env` - Environment
  - `attributes.start_timestamp` / `end_timestamp` - Timing
  - `attributes.duration` - Duration in nanoseconds
  - `attributes.trace_id` / `span_id` - Trace identifiers
- `meta.page.after` - Pagination token for next page
- `links.next` - Next page URL

### Common Use Cases

- Verify spans are being sent: Query recent time range with broad filters
- Debug missing spans: Check by service/operation/resource filters
- Validate tags: Inspect `attributes.custom` for expected tag values
- Check Azure Functions instrumentation: Filter by `origin:azurefunction` and `service:your-function-app-name`

## Verifying Logs with Datadog API

The Datadog API allows querying logs to verify application logging and diagnostics are working correctly.

### Prerequisites

- API Key: Set as environment variable `DD_API_KEY` or use directly
- Application Key: Required for API access; set as `DD_APPLICATION_KEY` or use directly

### Search Logs Endpoint

- URL: `https://api.datadoghq.com/api/v2/logs/events/search`
- Method: `POST`
- Headers: `DD-API-KEY`, `DD-APPLICATION-KEY`, `Content-Type: application/json`

### Request Format

```json
{
  "filter": {
    "query": "env:your-env service:your-service",
    "from": "now-1h",
    "to": "now"
  },
  "sort": "timestamp",
  "page": {
    "limit": 10
  }
}
```

### Query Syntax

- `env:your-env` - Filter by environment
- `service:my-service` - Filter by service name
- `host:your-hostname` - Filter by hostname
- `status:error` - Filter by log level (debug, info, warn, error)
- `"exact message"` - Search for exact text in log message
- Combine with `AND` / `OR` operators

### Example with curl

```bash
curl -X POST "https://api.datadoghq.com/api/v2/logs/events/search" \
  -H "DD-API-KEY: ${DD_API_KEY}" \
  -H "DD-APPLICATION-KEY: ${DD_APPLICATION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{"filter": {"query": "env:your-env \"your log message\"", "from": "now-24h", "to": "now"}, "sort": "timestamp", "page": {"limit": 10}}'
```

### Common Use Cases

- Verify logs are being sent: Query recent time range with broad filters
- Debug application issues: Search for error messages or specific log content
- Check diagnostic output: Validate startup/shutdown logs or configuration messages
