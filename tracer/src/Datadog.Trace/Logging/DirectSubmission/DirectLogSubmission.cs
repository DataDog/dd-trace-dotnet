// <copyright file="DirectLogSubmission.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;

namespace Datadog.Trace.Logging.DirectSubmission
{
    internal class DirectLogSubmission
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DirectLogSubmission>();
        private static DirectLogSubmission _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        public DirectLogSubmission()
            : this(DirectLogSubmissionSettings.CreateNullSettings(), new NullDatadogSink())
        {
        }

        private DirectLogSubmission(DirectLogSubmissionSettings settings, IDatadogSink sink)
        {
            Settings = settings;
            Sink = sink;
            Formatter = new LogFormatter(settings);

            // no need for shutdown tasks unless enabled
            if (settings.IsEnabled)
            {
                LifetimeManager.Instance.AddShutdownTask(RunShutdown);
            }
        }

        public static DirectLogSubmission Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock);
            }

            set
            {
                lock (_globalInstanceLock)
                {
                    _globalInstanceInitialized = true;
                    _instance = value;
                }
            }
        }

        public DirectLogSubmissionSettings Settings { get; }

        public IDatadogSink Sink { get; }

        public LogFormatter Formatter { get; }

        public static DirectLogSubmission Create(DirectLogSubmissionSettings settings)
        {
            if (!settings.IsEnabled)
            {
                return new DirectLogSubmission(settings, new NullDatadogSink());
            }

            // TODO: create batching options from config? Or should we not expose these options, and stick to the defaults?
            var batchingOptions = new BatchingSinkOptions();
            var apiFactory = LogsTransportStrategy.Get(settings);
            var logsApi = new LogsApi(settings.IntakeUrl, settings.ApiKey, apiFactory);

            return new DirectLogSubmission(settings, new DatadogSink(logsApi, new LogFormatter(settings), batchingOptions));
        }

        private void RunShutdown()
        {
            try
            {
                Logger.Debug("Running shutdown tasks for logs direct submission");
                Sink?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error flushing logs on shutdown");
            }
        }
    }
}
