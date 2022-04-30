// <copyright file="DirectLogSubmissionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.Logging.DirectSubmission
{
    internal class DirectLogSubmissionManager
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DirectLogSubmissionManager>();

        private DirectLogSubmissionManager(ImmutableDirectLogSubmissionSettings settings, IDatadogSink sink, LogFormatter formatter)
        {
            Settings = settings;
            Sink = sink;
            Formatter = formatter;
        }

        public ImmutableDirectLogSubmissionSettings Settings { get; }

        public IDatadogSink Sink { get; }

        public LogFormatter Formatter { get; }

        public static DirectLogSubmissionManager Create(
            DirectLogSubmissionManager? previous,
            ImmutableDirectLogSubmissionSettings settings,
            string serviceName,
            string env,
            string serviceVersion)
        {
            var formatter = new LogFormatter(settings, serviceName, env, serviceVersion);
            if (previous is not null)
            {
                // Only the formatter uses settings that are configurable in code.
                // If that ever changes, need to update the log-shipping integrations that
                // currently cache the sink/settings instances
                return new DirectLogSubmissionManager(previous.Settings, previous.Sink, formatter);
            }

            if (!settings.IsEnabled)
            {
                return new DirectLogSubmissionManager(settings, new NullDatadogSink(), formatter);
            }

            var apiFactory = LogsTransportStrategy.Get(settings);
            var logsApi = new LogsApi(settings.ApiKey, apiFactory);

            return new DirectLogSubmissionManager(settings, new DatadogSink(logsApi, formatter, settings.BatchingOptions), formatter);
        }

        public async Task DisposeAsync()
        {
            try
            {
                Logger.Debug("Running shutdown tasks for logs direct submission");
                if (Sink is { } sink)
                {
                    await sink.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error flushing logs on shutdown");
            }
        }
    }
}
