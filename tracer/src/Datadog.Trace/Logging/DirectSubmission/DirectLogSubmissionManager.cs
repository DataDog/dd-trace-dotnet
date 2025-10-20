// <copyright file="DirectLogSubmissionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.Logging.DirectSubmission
{
    internal class DirectLogSubmissionManager
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DirectLogSubmissionManager>();

        private DirectLogSubmissionManager(DirectLogSubmissionSettings settings, IDirectSubmissionLogSink sink, LogFormatter formatter)
        {
            Settings = settings;
            Sink = sink;
            Formatter = formatter;
        }

        public DirectLogSubmissionSettings Settings { get; }

        public IDirectSubmissionLogSink Sink { get; }

        public LogFormatter Formatter { get; }

        public static DirectLogSubmissionManager Create(
            DirectLogSubmissionManager? previous,
            TracerSettings settings,
            DirectLogSubmissionSettings directLogSettings,
            ImmutableAzureAppServiceSettings? azureAppServiceSettings,
            string serviceName,
            string env,
            string serviceVersion,
            IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            var formatter = new LogFormatter(settings, directLogSettings, azureAppServiceSettings, serviceName, env, serviceVersion, gitMetadataTagsProvider);

#if NETCOREAPP3_1_OR_GREATER
            if (settings.OpenTelemetryLogsEnabled is true && settings.OtelLogsExporterEnabled is true)
            {
                return new DirectLogSubmissionManager(directLogSettings, new Sink.OtlpSubmissionLogSink(directLogSettings.CreateBatchingSinkOptions()), formatter);
            }
#endif

            if (previous is not null)
            {
                // Only the formatter uses settings that are configurable in code.
                // If that ever changes, need to update the log-shipping integrations that
                // currently cache the sink/settings instances
                return new DirectLogSubmissionManager(previous.Settings, previous.Sink, formatter);
            }

            if (!directLogSettings.IsEnabled)
            {
                return new DirectLogSubmissionManager(directLogSettings, new NullDirectSubmissionLogSink(), formatter);
            }

            var apiFactory = LogsTransportStrategy.Get(directLogSettings);
            var logsApi = new LogsApi(directLogSettings.ApiKey, apiFactory);

            return new DirectLogSubmissionManager(directLogSettings, new DirectSubmissionLogSink(logsApi, formatter, directLogSettings.CreateBatchingSinkOptions()), formatter);
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
