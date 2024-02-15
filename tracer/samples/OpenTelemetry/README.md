# OpenTelemetry

### About the sample:

This sample contains an example ASP.NET Core application with the OpenTelemetry SDK configured. The dockerfile downloads the Datadog .NET Tracer and configures automatic instrumentation (along with the Application Security Monitoring).

### How to use:

- Clone/download this repository
- Update the `docker-compose.yml` by replacing the value for `DD_API_KEY`
- Run `docker-compose up` & navigate to `http://localhost:8080`

### Resulting .NET APM traces within Datadog:
![trace_view](https://github.com/DataDog/dd-trace-dotnet/assets/13769665/8387f313-c2bb-4e3f-b9b0-15ddb6cf962f)