// <copyright file="StartupHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

internal class StartupHook
{
    public static void Initialize()
    {
        // Intentional no-op:
        // Users may install dd-trace-dotnet by using the opentelemetry-operator,
        // which hardcodes environment variables needed for the OpenTelemetry .NET
        // auto-instrumentation installation, including DOTNET_STARTUP_HOOKS.
        // This assembly takes the place of the OpenTelemetry .NET auto-instrumentation
        // startup hook, because an otherwise missing startup hook causes the application
        // startup to fail.
    }
}
