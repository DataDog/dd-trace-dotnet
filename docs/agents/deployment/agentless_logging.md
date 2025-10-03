# Agentless Logging

Agentless logging allows sending logs directly to Datadog without the Datadog Agent, useful in serverless environments.

## Prerequisites

- APM must be enabled with automatic instrumentation
- Supported logging frameworks: Serilog, NLog, Log4Net, Microsoft.Extensions.Logging (ILogger)

## Required Environment Variables

- `DD_API_KEY` — Datadog API key for sending logs
- `DD_SITE` — Datadog site (e.g., `datadoghq.com`, `datadoghq.eu`, `us3.datadoghq.com`)
- `DD_LOGS_INJECTION=true` — Enable connecting logs and traces (default: true from tracer v3.24.0+)
- `DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS` — Enable agentless logging for frameworks
  - Values: `Serilog`, `NLog`, `Log4Net`, `ILogger` (semicolon-separated for multiple)
  - Example: `Serilog;Log4Net;NLog`

## Optional Configuration

- `DD_LOGS_DIRECT_SUBMISSION_MINIMUM_LEVEL` — Filter logs by level before sending
  - Values: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Critical`
  - Default: `Information`
- `DD_LOGS_DIRECT_SUBMISSION_HOST` — Host machine name (auto-detected if not provided)
- `DD_LOGS_DIRECT_SUBMISSION_TAGS` — Tags to add to all logs (comma-separated)
  - Example: `layer:api, team:intake`
- `DD_LOGS_DIRECT_SUBMISSION_URL` — Custom log submission URL (uses `DD_SITE` by default)

## Important Notes

- Log4net/NLog require an appender/logger to be configured for agentless logging to work
- When using logging framework with `Microsoft.Extensions.Logging`, use the framework name (e.g., `Serilog` for Serilog.Extensions.Logging)
- Agentless logging is particularly useful for Azure Functions where the agent runs in a separate process
