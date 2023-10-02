// <copyright file="DebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Debugger
{
    internal class DebuggerSettings
    {
        public const string DebuggerMetricPrefix = "dynamic.instrumentation.metric.probe";
        public const int DefaultMaxDepthToSerialize = 3;
        public const int DefaultMaxSerializationTimeInMilliseconds = 200;
        public const int DefaultMaxNumberOfItemsInCollectionToCopy = 100;
        public const int DefaultMaxNumberOfFieldsToCopy = 20;

        private const int DefaultUploadBatchSize = 100;
        private const int DefaultDiagnosticsIntervalSeconds = 60 * 60; // 1 hour
        private const int DefaultUploadFlushIntervalMilliseconds = 0;

        public DebuggerSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);

            Enabled = config.WithKeys(ConfigurationKeys.Debugger.Enabled).AsBool(false);

            MaximumDepthOfMembersToCopy = config
                                         .WithKeys(ConfigurationKeys.Debugger.MaxDepthToSerialize)
                                         .AsInt32(DefaultMaxDepthToSerialize, maxDepth => maxDepth > 0)
                                         .Value;

            MaxSerializationTimeInMilliseconds = config
                                                .WithKeys(ConfigurationKeys.Debugger.MaxTimeToSerialize)
                                                .AsInt32(
                                                     DefaultMaxSerializationTimeInMilliseconds,
                                                     serializationTimeThreshold => serializationTimeThreshold > 0)
                                                .Value;

            UploadBatchSize = config
                             .WithKeys(ConfigurationKeys.Debugger.UploadBatchSize)
                             .AsInt32(DefaultUploadBatchSize, batchSize => batchSize > 0)
                             .Value;

            DiagnosticsIntervalSeconds = config
                                        .WithKeys(ConfigurationKeys.Debugger.DiagnosticsInterval)
                                        .AsInt32(DefaultDiagnosticsIntervalSeconds, interval => interval > 0)
                                        .Value;

            UploadFlushIntervalMilliseconds = config
                                             .WithKeys(ConfigurationKeys.Debugger.UploadFlushInterval)
                                             .AsInt32(DefaultUploadFlushIntervalMilliseconds, flushInterval => flushInterval >= 0)
                                             .Value;
        }

        public bool Enabled { get; }

        public int MaxSerializationTimeInMilliseconds { get; }

        public int MaximumDepthOfMembersToCopy { get; }

        public int UploadBatchSize { get; }

        public int DiagnosticsIntervalSeconds { get; }

        public int UploadFlushIntervalMilliseconds { get; }

        public static DebuggerSettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry)
        {
            return new DebuggerSettings(source, telemetry);
        }

        public static DebuggerSettings FromDefaultSource()
        {
            return FromSource(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
        }
    }
}
