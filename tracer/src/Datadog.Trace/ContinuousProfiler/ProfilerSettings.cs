// <copyright file="ProfilerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.ContinuousProfiler;

internal class ProfilerSettings
{
    public ProfilerSettings(IConfigurationSource config, IConfigurationTelemetry telemetry)
        : this(config, new EnvironmentConfigurationSource(), telemetry)
    {
    }

    // Internal for testing only
    internal ProfilerSettings(IConfigurationSource config, IConfigurationSource envConfig, IConfigurationTelemetry telemetry)
    {
        if (!IsProfilingSupported)
        {
            ProfilerState = ProfilerState.Disabled;
            telemetry.Record(ConfigurationKeys.ProfilingEnabled, false, ConfigurationOrigins.Calculated);
            return;
        }

        // If managed activation is enabled, we need to _just_ read from the environment variables,
        // as that's all that applies
        var envConfigBuilder = new ConfigurationBuilder(envConfig, telemetry);
        var managedActivationEnabled = envConfigBuilder
                                      .WithKeys(ConfigurationKeys.ProfilerManagedActivationEnabled)
                                      .AsBool(true);

        // If we're using managed activation, we use the "full" config source set.
        // Otherwise we only read from the environment variables, to "match" the behavior of the profiler
        var profilingConfig = managedActivationEnabled
                                  ? new ConfigurationBuilder(config, telemetry)
                                  : envConfigBuilder;

        // With SSI, beyond ContinuousProfiler.ConfigurationKeys.ProfilingEnabled (true or auto vs false),
        // the profiler could be enabled via ContinuousProfiler.ConfigurationKeys.SsiDeployed:
        //  - if it contains "profiler", the profiler is enabled after 30 seconds + at least 1 span
        //  - if not, the profiler needed to be loaded by the CLR but no profiling will be done, only telemetry metrics will be sent
        // So, for the Tracer, the profiler should be seen as enabled if ContinuousProfiler.ConfigurationKeys.SsiDeployed has a value
        // (even without "profiler") so that spans will be sent to the profiler.
        var profilingEnabled = profilingConfig
                              .WithKeys(ConfigurationKeys.ProfilingEnabled)
                               // We stick with strings here instead of using the `GetAs` method,
                               // so that telemetry continues to store true/false/auto, instead of the enum values.
                              .AsString(
                                   converter: x => x switch
                                   {
                                       "auto" => "auto",
                                       _ when x.ToBoolean() is { } boolean => boolean ? "true" : "false",
                                       _ => ParsingResult<string>.Failure(),
                                   },
                                   getDefaultValue: () =>
                                   {
                                       // If there's no explicit `DD_PROFILING_ENABLED` key,
                                       // we set the state based on the SSI value, only checking env vars (not the full stack)
                                       var isSsiDeployment = envConfigBuilder
                                                            .WithKeys(ConfigurationKeys.SsiDeployed)
                                                            .AsString();

                                       return isSsiDeployment switch
                                       {
                                           not null when isSsiDeployment.Contains("profiler") => "auto",
                                           _ => "false",
                                       };
                                   },
                                   validator: null);

        ProfilerState = profilingEnabled switch
        {
            "auto" => ProfilerState.Auto,
            "true" => ProfilerState.Enabled,
            _ => ProfilerState.Disabled,
        };
    }

    // Internal for testing only
    internal ProfilerSettings(ProfilerState state)
    {
        ProfilerState = state;
    }

    /// <summary>
    /// Gets a value indicating whether the profiler is supported on this platform at all.
    /// If it's not supported, we should not try to P/Invoke to it or do any context tracking.
    /// </summary>
    public static bool IsProfilingSupported
    {
        get
        {
            var fd = FrameworkDescription.Instance;
            return
                (fd.OSPlatform == OSPlatformName.Windows && fd.ProcessArchitecture is ProcessArchitecture.X64 or ProcessArchitecture.X86) ||
                (fd.OSPlatform == OSPlatformName.Linux && fd.ProcessArchitecture is ProcessArchitecture.X64);
        }
    }

    public ProfilerState ProfilerState { get; }

    public bool IsProfilerEnabled => ProfilerState != ProfilerState.Disabled;
}
