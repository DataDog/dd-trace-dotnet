// <copyright file="DebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Configuration;

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
        private const int DefaultDiagnosticsIntervalSeconds = 5;
        private const int DefaultUploadFlushIntervalMilliseconds = 0;

        public DebuggerSettings()
            : this(configurationSource: null)
        {
        }

        public DebuggerSettings(IConfigurationSource? configurationSource)
        {
            Enabled = configurationSource?.GetBool(ConfigurationKeys.Debugger.Enabled) ?? false;

            var maxDepth = configurationSource?.GetInt32(ConfigurationKeys.Debugger.MaxDepthToSerialize);
            MaximumDepthOfMembersToCopy =
                maxDepth is null or <= 0
                    ? DefaultMaxDepthToSerialize
                    : maxDepth.Value;

            var serializationTimeThreshold = configurationSource?.GetInt32(ConfigurationKeys.Debugger.MaxTimeToSerialize);
            MaxSerializationTimeInMilliseconds =
                serializationTimeThreshold is null or <= 0
                    ? DefaultMaxSerializationTimeInMilliseconds
                    : serializationTimeThreshold.Value;

            var batchSize = configurationSource?.GetInt32(ConfigurationKeys.Debugger.UploadBatchSize);
            UploadBatchSize =
                batchSize is null or <= 0
                    ? DefaultUploadBatchSize
                    : batchSize.Value;

            var interval = configurationSource?.GetInt32(ConfigurationKeys.Debugger.DiagnosticsInterval);
            DiagnosticsIntervalSeconds =
                interval is null or <= 0
                    ? DefaultDiagnosticsIntervalSeconds
                    : interval.Value;

            var flushInterval = configurationSource?.GetInt32(ConfigurationKeys.Debugger.UploadFlushInterval);
            UploadFlushIntervalMilliseconds =
                flushInterval is null or < 0
                    ? DefaultUploadFlushIntervalMilliseconds
                    : flushInterval.Value;
        }

        public bool Enabled { get; }

        public int MaxSerializationTimeInMilliseconds { get; }

        public int MaximumDepthOfMembersToCopy { get; }

        public int UploadBatchSize { get; }

        public int DiagnosticsIntervalSeconds { get; }

        public int UploadFlushIntervalMilliseconds { get; }

        public static DebuggerSettings FromSource(IConfigurationSource source)
        {
            return new DebuggerSettings(source);
        }

        public static DebuggerSettings FromDefaultSource()
        {
            return FromSource(GlobalConfigurationSource.Instance);
        }
    }
}
