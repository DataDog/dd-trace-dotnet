// <copyright file="HostingExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.Extensions.Hosting;

namespace Datadog.Aspire.Hosting.Extensions;

/// <summary>
/// Provides extension methods for configuring Datadog in the Aspire host.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Configures OTLP exporters
    /// </summary>
    /// <typeparam name="TDestination">A type that represents the resource.</typeparam>
    /// <param name="builder">The host's application builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TDestination}"/>.</returns>
    public static IResourceBuilder<TDestination> WithDatadogInstrumentation<TDestination>(this IResourceBuilder<TDestination> builder)
        where TDestination : IResourceWithEnvironment
    {
        return builder.WithEnvironment(context =>
        {
            if (builder.Resource.TryGetLastAnnotation<IServiceMetadata>(out var projectMetadata))
            {
                // The ProjectPath points to the directory containing the application's csproj file
                // As a result, we have to build our own directory-walking to find the "datadog" folder
                // that was copied to the application output folder, aka bin/<BuildConfiguration>/<runtime>
                var workingDirectory = Path.GetDirectoryName(projectMetadata.ProjectPath)!;

                foreach (var buildConfigurationDirectory in Directory.EnumerateDirectories(Path.Combine(workingDirectory, "bin")))
                {
                    string dirName = new DirectoryInfo(buildConfigurationDirectory).Name;

                    if (dirName.Equals("Debug", StringComparison.OrdinalIgnoreCase)
                        || dirName.Equals("Release", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var runtimeDirectory in Directory.EnumerateDirectories(buildConfigurationDirectory))
                        {
                            var potentialDirectory = Path.Combine(runtimeDirectory, "datadog");
                            if (Path.Exists(potentialDirectory))
                            {
                                // TODO: Get this to work on Linux
                                // TODO: Get this to work in containers
                                context.EnvironmentVariables["CORECLR_ENABLE_PROFILING"] = "1";
                                context.EnvironmentVariables["CORECLR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
                                context.EnvironmentVariables["CORECLR_PROFILER_PATH"] = Path.Combine(potentialDirectory, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");

                                context.EnvironmentVariables["DD_DOTNET_TRACER_HOME"] = potentialDirectory;
                                context.EnvironmentVariables["DD_TRACE_OTEL_ENABLED"] = "true";
                            }
                        }
                    }
                }
            }
        });
    }
}
