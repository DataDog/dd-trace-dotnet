// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing
{
    internal static class Common
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Common));
        private static object _padLock = new object();
        private static Tracer _testTracer = null;

        static Common()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
        }

        internal static Tracer TestTracer
        {
            get
            {
                if (_testTracer is null)
                {
                    lock (_padLock)
                    {
                        if (_testTracer is null)
                        {
                            var settings = TracerSettings.FromDefaultSources();
                            settings.TraceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.

                            if (string.IsNullOrEmpty(settings.ServiceName))
                            {
                                // Extract repository name from the git url and use it as a default service name.
                                string repository = CIEnvironmentValues.Repository;
                                if (!string.IsNullOrEmpty(repository))
                                {
                                    Regex regex = new Regex(@"/([a-zA-Z0-9\\\-_.]*)$");
                                    Match match = regex.Match(repository);
                                    if (match.Success && match.Groups.Count > 1)
                                    {
                                        const string gitSuffix = ".git";
                                        string repoName = match.Groups[1].Value;
                                        if (repoName.EndsWith(gitSuffix))
                                        {
                                            settings.ServiceName = repoName.Substring(0, repoName.Length - gitSuffix.Length);
                                        }
                                        else
                                        {
                                            settings.ServiceName = repoName;
                                        }
                                    }
                                }
                            }

                            _testTracer = new Tracer(settings);
                            Tracer.Instance = _testTracer;
                        }
                    }
                }

                return _testTracer;
            }
        }

        internal static void FlushSpans(IntegrationInfo integrationInfo)
        {
            if (!TestTracer.Settings.IsIntegrationEnabled(integrationInfo))
            {
                return;
            }

            try
            {
                var flushThread = new Thread(() => InternalFlush().GetAwaiter().GetResult());
                flushThread.IsBackground = false;
                flushThread.Name = "FlushThread";
                flushThread.Start();
                flushThread.Join();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred when flushing spans.");
            }

            static async Task InternalFlush()
            {
                try
                {
                    // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                    // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                    // So the last spans in buffer aren't send to the agent.
                    Log.Debug("Integration flushing spans.");
                    await TestTracer.FlushAsync().ConfigureAwait(false);
                    Log.Debug("Integration flushed.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred when flushing spans.");
                }
            }
        }

        internal static string GetParametersValueData(object paramValue)
        {
            if (paramValue is null)
            {
                return "(null)";
            }
            else if (paramValue is Array pValueArray)
            {
                const int maxArrayLength = 50;
                int length = pValueArray.Length > maxArrayLength ? maxArrayLength : pValueArray.Length;

                string[] strValueArray = new string[length];
                for (var i = 0; i < length; i++)
                {
                    strValueArray[i] = GetParametersValueData(pValueArray.GetValue(i));
                }

                return "[" + string.Join(", ", strValueArray) + (pValueArray.Length > maxArrayLength ? ", ..." : string.Empty) + "]";
            }
            else if (paramValue is Delegate pValueDelegate)
            {
                return $"{paramValue}[{pValueDelegate.Target}|{pValueDelegate.Method}]";
            }

            return paramValue.ToString();
        }
    }
}
