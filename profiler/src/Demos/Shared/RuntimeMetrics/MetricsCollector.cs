// <copyright file="MetricsCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;

namespace Datadog.RuntimeMetrics
{
    // Based on the presence of an environment variable,
    // collect CLR metrics and save them into a json file
    public class MetricsCollector : IMetricsCollector
    {
        private const string ManagedMetricsPrefix = "Managed_";

        private bool _disposed;
        private string _metricsFilePath;
#if NETFRAMEWORK
        private FrameworkProvider _provider;
#else
        private RuntimeProvider _provider;
#endif
        public MetricsCollector()
        {
            _metricsFilePath = GetMetricsFilePath();
            if (string.IsNullOrEmpty(_metricsFilePath))
            {
                return;
            }

            if (_metricsFilePath != null)
            {
#if NETFRAMEWORK
                _provider = new FrameworkProvider();
#else
                _provider = new RuntimeProvider();
#endif
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_metricsFilePath == null)
            {
                return;
            }

            // save the metrics into a json file
            _provider.Stop();
            var writer = new JsonMetricsWriter(_metricsFilePath);
            var metrics = _provider.GetMetrics();
            writer.Write(metrics);
        }

        // Use the path given by DD_PROFILING_METRICS_FILEPATH as a suffix and prefix it with "Managed_"
        private static string GetMetricsFilePath()
        {
            var filePath = Environment.GetEnvironmentVariable("DD_PROFILING_METRICS_FILEPATH");
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            // check for local file (i.e. in the current directory)
            var folder = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(folder))
            {
                filePath = Path.Combine(Directory.GetCurrentDirectory(), ManagedMetricsPrefix + filePath);
            }
            else
            {
                // we don't create the path if it does not exist
                if (!Directory.Exists(folder))
                {
                    throw new InvalidOperationException($"Missing folder '{folder}' for metrics");
                }

                // add the prefix
                var filename = Path.GetFileName(filePath);
                filePath = Path.Combine(folder, ManagedMetricsPrefix + filename);
            }

            return filePath;
        }
    }
}
