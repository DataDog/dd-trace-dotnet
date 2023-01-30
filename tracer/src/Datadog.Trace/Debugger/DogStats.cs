// <copyright file="DogStats.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.Vendors.StatsdClient;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Debugger
{
    internal class DogStats
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DogStats));

        internal static IDogStatsd Create(string environment, string serviceVersion, ImmutableExporterSettings exporter, string serviceName)
        {
            try
            {
                var constantTags = new List<string>
                                   {
                                       "lang:.NET",
                                       $"lang_interpreter:{FrameworkDescription.Instance.Name}",
                                       $"lang_version:{FrameworkDescription.Instance.ProductVersion}",
                                       $"tracer_version:{TracerConstants.AssemblyVersion}",
                                       $"service:{NormalizerTraceProcessor.NormalizeService(serviceName)}",
                                       $"{Tags.RuntimeId}:{Tracer.RuntimeId}"
                                   };

                if (environment != null)
                {
                    constantTags.Add($"env:{environment}");
                }

                if (serviceVersion != null)
                {
                    constantTags.Add($"version:{serviceVersion}");
                }

                var statsd = new DogStatsdService();
                switch (exporter.MetricsTransport)
                {
                    case MetricsTransportType.NamedPipe:
                        // Environment variables for windows named pipes are not explicitly passed to statsd.
                        // They are retrieved within the vendored code, so there is nothing to pass.
                        // Passing anything through StatsdConfig may cause bugs when windows named pipes should be used.
                        Log.Information("Using windows named pipes for metrics transport.");
                        statsd.Configure(new StatsdConfig
                        {
                            ConstantTags = constantTags.ToArray()
                        });
                        break;
#if NETCOREAPP3_1_OR_GREATER
                    case MetricsTransportType.UDS:
                        Log.Information("Using unix domain sockets for metrics transport.");
                        statsd.Configure(new StatsdConfig
                        {
                            StatsdServerName = $"{ExporterSettings.UnixDomainSocketPrefix}{exporter.MetricsUnixDomainSocketPath}",
                            ConstantTags = constantTags.ToArray()
                        });
                        break;
#endif
                    case MetricsTransportType.UDP:
                    default:
                        statsd.Configure(new StatsdConfig
                        {
                            StatsdServerName = exporter.AgentUri.DnsSafeHost,
                            StatsdPort = exporter.DogStatsdPort,
                            ConstantTags = constantTags.ToArray()
                        });
                        break;
                }

                return statsd;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to instantiate StatsD client.");
                return new NoOpStatsd();
            }
        }
    }
}
