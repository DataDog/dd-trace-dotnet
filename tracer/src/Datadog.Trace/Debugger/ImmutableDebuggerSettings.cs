// <copyright file="ImmutableDebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger
{
    internal class ImmutableDebuggerSettings
    {
        public ImmutableDebuggerSettings(
            bool enabled,
            string? serviceVersion,
            string? environment,
            int maxSerializationTimeInMilliseconds,
            int maximumDepthOfMembersToCopy,
            Uri? snapshotUri,
            int uploadBatchSize,
            int diagnosticsIntervalSeconds,
            int uploadFlushIntervalMilliseconds,
            TracesTransportType transportType)
        {
            Enabled = enabled;
            ServiceVersion = serviceVersion;
            Environment = environment;
            MaxSerializationTimeInMilliseconds = maxSerializationTimeInMilliseconds;
            MaximumDepthOfMembersOfMembersToCopy = maximumDepthOfMembersToCopy;
            SnapshotUri = snapshotUri;
            UploadBatchSize = uploadBatchSize;
            DiagnosticsIntervalSeconds = diagnosticsIntervalSeconds;
            UploadFlushIntervalMilliseconds = uploadFlushIntervalMilliseconds;
            TransportType = transportType;
        }

        public bool Enabled { get; }

        public string? ServiceVersion { get; }

        public string? Environment { get; }

        public Uri? SnapshotUri { get; set; }

        public int MaxSerializationTimeInMilliseconds { get; }

        public int MaximumDepthOfMembersOfMembersToCopy { get; }

        public int UploadBatchSize { get; }

        public int DiagnosticsIntervalSeconds { get; }

        public int UploadFlushIntervalMilliseconds { get; }

        public TracesTransportType TransportType { get; }

        public static ImmutableDebuggerSettings Create(TracerSettings tracerSettings) =>
            Create(tracerSettings.DebuggerSettings);

        public static ImmutableDebuggerSettings Create(DebuggerSettings debuggerSettings) =>
            Create(
                debuggerSettings.Enabled,
                debuggerSettings.ServiceVersion,
                debuggerSettings.Environment,
                debuggerSettings.MaxSerializationTimeInMilliseconds,
                debuggerSettings.MaximumDepthOfMembersToCopy,
                debuggerSettings.SnapshotUri,
                debuggerSettings.UploadBatchSize,
                debuggerSettings.DiagnosticsIntervalSeconds,
                debuggerSettings.UploadFlushIntervalMilliseconds,
                debuggerSettings.TransportType);

        public static ImmutableDebuggerSettings Create(
            bool enabled,
            string? serviceVersion,
            string? environment,
            int maxSerializationTimeInMilliseconds,
            int maximumDepthOfMembersOfMembersToCopy,
            Uri? snapshotUri,
            int uploadBatchSize,
            int diagnosticsIntervalSeconds,
            int uploadFlushIntervalMilliseconds,
            TracesTransportType transportType) =>
            new ImmutableDebuggerSettings(
                enabled,
                serviceVersion,
                environment,
                maxSerializationTimeInMilliseconds,
                maximumDepthOfMembersOfMembersToCopy,
                snapshotUri,
                uploadBatchSize,
                diagnosticsIntervalSeconds,
                uploadFlushIntervalMilliseconds,
                transportType);
    }
}
