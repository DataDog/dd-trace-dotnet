# Azure App Service

Azure App Service is a platform for hosting web applications, REST APIs, and mobile backends.

## Setup

- Install the [Azure integration][azure-integration] for enriched metrics and resource metadata.
- Set up [Azure log forwarding][azure-logs] to collect Azure App Service resource and application logs.

## APM and Custom Metrics

- **Linux**: See Azure App Service documentation for code or container instrumentation.
- **Windows**: Use the Azure App Services Site Extension for automatic instrumentation of .NET applications.

## Capabilities

- Fully distributed APM tracing using automatic instrumentation
- Customized APM service and trace views with Azure App Service metrics and metadata
- Manual APM instrumentation to customize spans
- `Trace_ID` injection into application logs
- Custom metrics with DogStatsD
