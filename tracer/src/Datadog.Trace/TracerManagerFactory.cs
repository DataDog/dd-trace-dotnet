// <copyright file="TracerManagerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace
{
    internal class TracerManagerFactory
    {
        private const string UnknownServiceName = "UnknownService";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerManagerFactory>();

        public static readonly TracerManagerFactory Instance = new();

        /// <summary>
        /// The primary factory method, called by <see cref="TracerManager"/>,
        /// providing the previous global <see cref="TracerManager"/> instance (may be null)
        /// </summary>
        internal TracerManager CreateTracerManager(ImmutableTracerSettings settings, TracerManager previous)
        {
            // TODO: If relevant settings have not changed, continue using existing statsd/agent writer/runtime metrics etc
            return CreateTracerManager(
                settings,
                agentWriter: null,
                sampler: null,
                scopeManager: previous?.ScopeManager, // no configuration, so can always use the same one
                statsd: null,
                runtimeMetrics: null,
                logSubmissionManager: previous?.DirectLogSubmission);
        }

        /// <summary>
        /// Internal for use in tests that create "standalone" <see cref="TracerManager"/> by
        /// <see cref="Tracer(TracerSettings, IAgentWriter, ISampler, IScopeManager, IDogStatsd)"/>
        /// </summary>
        internal TracerManager CreateTracerManager(
            ImmutableTracerSettings settings,
            IAgentWriter agentWriter,
            ISampler sampler,
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetrics,
            DirectLogSubmissionManager logSubmissionManager)
        {
            settings ??= ImmutableTracerSettings.FromDefaultSources();

            var defaultServiceName = settings.ServiceName ??
                                     GetApplicationName() ??
                                     UnknownServiceName;

            statsd = settings.TracerMetricsEnabled
                         ? (statsd ?? CreateDogStatsdClient(settings, defaultServiceName))
                         : null;
            sampler ??= GetSampler(settings);
            agentWriter ??= GetAgentWriter(settings, statsd, sampler);
            scopeManager ??= new AsyncLocalScopeManager();

            if (settings.RuntimeMetricsEnabled && !DistributedTracer.Instance.IsChildTracer)
            {
                runtimeMetrics ??= new RuntimeMetricsWriter(statsd ?? CreateDogStatsdClient(settings, defaultServiceName), TimeSpan.FromSeconds(10));
            }

            logSubmissionManager = DirectLogSubmissionManager.Create(
                logSubmissionManager,
                settings.LogSubmissionSettings,
                defaultServiceName,
                settings.Environment,
                settings.ServiceVersion);

            var tracerManager = CreateTracerManagerFrom(settings, agentWriter, sampler, scopeManager, statsd, runtimeMetrics, logSubmissionManager, defaultServiceName);
            return tracerManager;
        }

        /// <summary>
        ///  Can be overriden to create a different <see cref="TracerManager"/>, e.g. <see cref="Ci.CITracerManager"/>
        /// </summary>
        protected virtual TracerManager CreateTracerManagerFrom(
            ImmutableTracerSettings settings,
            IAgentWriter agentWriter,
            ISampler sampler,
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetrics,
            DirectLogSubmissionManager logSubmissionManager,
            string defaultServiceName)
            => new TracerManager(settings, agentWriter, sampler, scopeManager, statsd, runtimeMetrics, logSubmissionManager, defaultServiceName);

        protected virtual ISampler GetSampler(ImmutableTracerSettings settings)
        {
            var sampler = new RuleBasedSampler(new RateLimiter(settings.MaxTracesSubmittedPerSecond));

            if (!string.IsNullOrWhiteSpace(settings.CustomSamplingRules))
            {
                foreach (var rule in CustomSamplingRule.BuildFromConfigurationString(settings.CustomSamplingRules))
                {
                    sampler.RegisterRule(rule);
                }
            }

            if (settings.GlobalSamplingRate != null)
            {
                var globalRate = (float)settings.GlobalSamplingRate;

                if (globalRate < 0f || globalRate > 1f)
                {
                    Log.Warning("{ConfigurationKey} configuration of {ConfigurationValue} is out of range", ConfigurationKeys.GlobalSamplingRate, settings.GlobalSamplingRate);
                }
                else
                {
                    sampler.RegisterRule(new GlobalSamplingRule(globalRate));
                }
            }

            return sampler;
        }

        protected virtual IAgentWriter GetAgentWriter(ImmutableTracerSettings settings, IDogStatsd statsd, ISampler sampler)
        {
            var apiRequestFactory = TracesTransportStrategy.Get(settings.Exporter);
            var api = new Api(settings.Exporter.AgentUri, apiRequestFactory, statsd, rates => sampler.SetDefaultSampleRates(rates), settings.Exporter.PartialFlushEnabled);
            return new AgentWriter(api, statsd, maxBufferSize: settings.TraceBufferSize);
        }

        private static IDogStatsd CreateDogStatsdClient(ImmutableTracerSettings settings, string serviceName)
        {
            try
            {
                var constantTags = new List<string>
                                   {
                                       "lang:.NET",
                                       $"lang_interpreter:{FrameworkDescription.Instance.Name}",
                                       $"lang_version:{FrameworkDescription.Instance.ProductVersion}",
                                       $"tracer_version:{TracerConstants.AssemblyVersion}",
                                       $"service:{serviceName}",
                                       $"{Tags.RuntimeId}:{Tracer.RuntimeId}"
                                   };

                if (settings.Environment != null)
                {
                    constantTags.Add($"env:{settings.Environment}");
                }

                if (settings.ServiceVersion != null)
                {
                    constantTags.Add($"version:{settings.ServiceVersion}");
                }

                var statsd = new DogStatsdService();
                if (settings.Exporter.MetricsTransport == Vendors.StatsdClient.Transport.TransportType.NamedPipe)
                {
                    // Environment variables for windows named pipes are not explicitly passed to statsd.
                    // They are retrieved within the vendored code, so there is nothing to pass.
                    // Passing anything through StatsdConfig may cause bugs when windows named pipes should be used.
                    statsd.Configure(new StatsdConfig
                    {
                        ConstantTags = constantTags.ToArray()
                    });
                }
                else if (settings.Exporter.MetricsTransport == Vendors.StatsdClient.Transport.TransportType.UDS)
                {
                    statsd.Configure(new StatsdConfig
                    {
                        StatsdServerName = $"{ExporterSettings.UnixDomainSocketPrefix}{settings.Exporter.MetricsUnixDomainSocketPath}",
                        ConstantTags = constantTags.ToArray()
                    });
                }
                else if (settings.Exporter.MetricsTransport == Vendors.StatsdClient.Transport.TransportType.UDP)
                {
                    statsd.Configure(new StatsdConfig
                    {
                        StatsdServerName = settings.Exporter.AgentUri.DnsSafeHost,
                        StatsdPort = settings.Exporter.DogStatsdPort,
                        ConstantTags = constantTags.ToArray()
                    });
                }

                return statsd;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to instantiate StatsD client.");
                return new NoOpStatsd();
            }
        }

        /// <summary>
        /// Gets an "application name" for the executing application by looking at
        /// the hosted app name (.NET Framework on IIS only), assembly name, and process name.
        /// </summary>
        /// <returns>The default service name.</returns>
        private static string GetApplicationName()
        {
            try
            {
                if (AzureAppServices.Metadata.IsRelevant)
                {
                    return AzureAppServices.Metadata.SiteName;
                }

                try
                {
                    if (TryLoadAspNetSiteName(out var siteName))
                    {
                        return siteName;
                    }
                }
                catch (Exception ex)
                {
                    // Unable to call into System.Web.dll
                    Log.Error(ex, "Unable to get application name through ASP.NET settings");
                }

                return Assembly.GetEntryAssembly()?.GetName().Name ??
                       ProcessHelpers.GetCurrentProcessName();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating default service name.");
                return null;
            }
        }

        private static bool TryLoadAspNetSiteName(out string siteName)
        {
#if NETFRAMEWORK
            // System.Web.dll is only available on .NET Framework
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                // if this app is an ASP.NET application, return "SiteName/ApplicationVirtualPath".
                // note that ApplicationVirtualPath includes a leading slash.
                siteName = (System.Web.Hosting.HostingEnvironment.SiteName + System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath).TrimEnd('/');
                return true;
            }

#endif
            siteName = default;
            return false;
        }
    }
}
