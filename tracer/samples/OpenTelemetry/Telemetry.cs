using System.Diagnostics;

namespace OpenTelemetry.AspNetCoreApplication;

public static class Telemetry
{
    public static readonly string ServiceName = "OpenTelemetry.AspNetCoreApplication";
    public static readonly string ServiceVersion = "1.0.0";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
}
