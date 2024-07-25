// <copyright file="DebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

namespace Datadog.Trace.Debugger
{
    internal class DebuggerSettings
    {
        public const string DebuggerMetricPrefix = "dynamic.instrumentation.metric.probe";
        public const int DefaultMaxDepthToSerialize = 3;
        public const int DefaultMaxSerializationTimeInMilliseconds = 200;
        public const int DefaultMaxNumberOfItemsInCollectionToCopy = 100;
        public const int DefaultMaxNumberOfFieldsToCopy = 20;
        public const int DefaultMaxStringLength = 1000;

        private const int DefaultUploadBatchSize = 100;
        public const int DefaultSymbolBatchSizeInBytes = 100000;
        private const int DefaultDiagnosticsIntervalSeconds = 60 * 60; // 1 hour
        private const int DefaultUploadFlushIntervalMilliseconds = 0;

        public DebuggerSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);

            Enabled = config.WithKeys(ConfigurationKeys.Debugger.Enabled).AsBool(false);
            SymbolDatabaseUploadEnabled = config.WithKeys(ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled).AsBool(false);

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

            SymbolDatabaseBatchSizeInBytes = config
                                         .WithKeys(ConfigurationKeys.Debugger.SymbolDatabaseBatchSizeInBytes)
                                         .AsInt32(DefaultSymbolBatchSizeInBytes, batchSize => batchSize > 0)
                                         .Value;

            var thirdPartyIncludes = config
                                  .WithKeys(ConfigurationKeys.Debugger.ThirdPartyDetectionIncludes)
                                  .AsString()?
                                  .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ??
                                   Enumerable.Empty<string>();

            ThirdPartyDetectionIncludes = thirdPartyIncludes.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

            var thirdPartyExcludes = config
                                    .WithKeys(ConfigurationKeys.Debugger.ThirdPartyDetectionExcludes)
                                    .AsString()?
                                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ??
                                     Enumerable.Empty<string>();

            ThirdPartyDetectionExcludes = thirdPartyExcludes.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

            var symDb3rdPartyIncludeLibraries = config
                                               .WithKeys(ConfigurationKeys.Debugger.SymDbThirdPartyDetectionIncludes)
                                               .AsString()?
                                               .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ??
                                                Enumerable.Empty<string>();

            SymDbThirdPartyDetectionIncludes = new HashSet<string>([..symDb3rdPartyIncludeLibraries, ..ThirdPartyDetectionIncludes]).ToImmutableHashSet();

            var symDb3rdPartyExcludeLibraries = config
                                               .WithKeys(ConfigurationKeys.Debugger.SymDbThirdPartyDetectionExcludes)
                                               .AsString()?
                                               .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ??
                                                Enumerable.Empty<string>();

            SymDbThirdPartyDetectionExcludes = new HashSet<string>([..symDb3rdPartyExcludeLibraries, ..ThirdPartyDetectionExcludes]).ToImmutableHashSet();

            DiagnosticsIntervalSeconds = config
                                        .WithKeys(ConfigurationKeys.Debugger.DiagnosticsInterval)
                                        .AsInt32(DefaultDiagnosticsIntervalSeconds, interval => interval > 0)
                                        .Value;

            UploadFlushIntervalMilliseconds = config
                                             .WithKeys(ConfigurationKeys.Debugger.UploadFlushInterval)
                                             .AsInt32(DefaultUploadFlushIntervalMilliseconds, flushInterval => flushInterval >= 0)
                                             .Value;

            var redactedIdentifiers = config
                                 .WithKeys(ConfigurationKeys.Debugger.RedactedIdentifiers)
                                 .AsString()?
                                 .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ??
                                  Enumerable.Empty<string>();

            RedactedIdentifiers = new HashSet<string>(redactedIdentifiers, StringComparer.OrdinalIgnoreCase);

            var redactedTypes = config
                                     .WithKeys(ConfigurationKeys.Debugger.RedactedTypes)
                                     .AsString()?
                                     .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ??
                                      Enumerable.Empty<string>();

            RedactedTypes = new HashSet<string>(redactedTypes, StringComparer.OrdinalIgnoreCase);
        }

        public bool Enabled { get; }

        public bool SymbolDatabaseUploadEnabled { get; }

        public int MaxSerializationTimeInMilliseconds { get; }

        public int MaximumDepthOfMembersToCopy { get; }

        public int UploadBatchSize { get; }

        public int SymbolDatabaseBatchSizeInBytes { get; }

        public ImmutableHashSet<string> ThirdPartyDetectionIncludes { get; }

        public ImmutableHashSet<string> ThirdPartyDetectionExcludes { get; }

        public ImmutableHashSet<string> SymDbThirdPartyDetectionIncludes { get; }

        public ImmutableHashSet<string> SymDbThirdPartyDetectionExcludes { get; }

        public int DiagnosticsIntervalSeconds { get; }

        public int UploadFlushIntervalMilliseconds { get; }

        public HashSet<string> RedactedIdentifiers { get; }

        public HashSet<string> RedactedTypes { get; }

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
