// <copyright file="HostingExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Data;
using System.Net.Sockets;

namespace Datadog.Aspire.Hosting.Extensions;

/// <summary>
/// Provides extension methods for configuring Datadog in the Aspire host.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Adds a Datadog container to the application model. The default image is "datadog/agent" and tag is "7".
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="apiKey">The API key for the Datadog Agent container.</param>
    /// <param name="port">The host port for the redis server.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RedisContainerResource}"/>.</returns>
    public static IResourceBuilder<DatadogContainerResource> AddDatadogAgentContainer(this IDistributedApplicationBuilder builder, string name, string? apiKey, int? port = null)
    {
        if (apiKey is null)
        {
            throw new ArgumentNullException(nameof(apiKey));
        }

        var datadog = new DatadogContainerResource(name);
        return builder.AddResource(datadog)
               .WithServiceBinding(hostPort: port, containerPort: 8126, name: "datadog-http", scheme: "http")
               .WithAnnotation(new ContainerImageAnnotation { Image = "datadog/agent", Tag = "7" })
               .WithEnvironment("DD_API_KEY", apiKey)
               .WithEnvironment("DD_HOSTNAME", "aspire-host");
    }

    /// <summary>
    /// Only sets up the .NET automatic instrumentation variables for the specified Resource.
    /// This does not creates a unique Datadog Agent.
    /// </summary>
    /// <typeparam name="TDestination">A type that represents the resource.</typeparam>
    /// <param name="builder">The host's application builder.</param>
    /// <param name="datadogBuilder">The Datadog resource builder.</param>
    /// <param name="service">The name of the application.</param>
    /// <param name="env">The env of the application.</param>
    /// <param name="version">The version of the application..</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TDestination}"/>.</returns>
    public static IResourceBuilder<TDestination> WithDatadogInstrumentation<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<DatadogContainerResource> datadogBuilder, string? service = null, string? env = null, string? version = null)
        where TDestination : IResourceWithEnvironment
    {
        return builder.WithReference(datadogBuilder.GetEndpoint("datadog-http"))
                      .WithEnvironment(context =>
        {
            if (service is not null)
            {
                context.EnvironmentVariables["DD_SERVICE"] = service;
            }

            if (env is not null)
            {
                context.EnvironmentVariables["DD_ENV"] = env;
            }

            if (version is not null)
            {
                context.EnvironmentVariables["DD_VERSION"] = version;
            }

            context.EnvironmentVariables["DD_TRACE_AGENT_URL"] = datadogBuilder.GetEndpoint("datadog-http").UriString;

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
