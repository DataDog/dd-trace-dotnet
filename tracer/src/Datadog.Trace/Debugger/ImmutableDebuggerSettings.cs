// <copyright file="ImmutableDebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Debugger
{
    internal class ImmutableDebuggerSettings
    {
        public ImmutableDebuggerSettings(
            bool enabled,
            string apiKey,
            string runtimeId,
            string serviceVersion,
            string environment,
            int maxSerializationTimeInMilliseconds,
            int maximumDepthOfMembersToCopy,
            string snapshotsPath,
            int uploadBatchSize,
            int diagnosticsIntervalSeconds,
            int uploadFlushIntervalMilliseconds,
            TracesTransportType transportType,
            Uri agentUri)
        {
            Enabled = enabled;
            ApiKey = apiKey;
            RuntimeId = runtimeId;
            ServiceVersion = serviceVersion;
            Environment = environment;
            MaxSerializationTimeInMilliseconds = maxSerializationTimeInMilliseconds;
            MaximumDepthOfMembersOfMembersToCopy = maximumDepthOfMembersToCopy;
            SnapshotsPath = snapshotsPath;
            UploadBatchSize = uploadBatchSize;
            DiagnosticsIntervalSeconds = diagnosticsIntervalSeconds;
            UploadFlushIntervalMilliseconds = uploadFlushIntervalMilliseconds;
            TransportType = transportType;
            AgentUri = agentUri;
        }

        public bool Enabled { get; }

        public string ApiKey { get; }

        public string RuntimeId { get; }

        public string ServiceVersion { get; }

        public string SnapshotsPath { get; set; }

        public string Environment { get; }

        public int MaxSerializationTimeInMilliseconds { get; }

        public int MaximumDepthOfMembersOfMembersToCopy { get; }

        public int UploadBatchSize { get; }

        public int DiagnosticsIntervalSeconds { get; }

        public int UploadFlushIntervalMilliseconds { get; }

        public TracesTransportType TransportType { get; }

        public Uri AgentUri { get; }

        public static ImmutableDebuggerSettings Create(TracerSettings tracerSettings) =>
            Create(tracerSettings.DebuggerSettings);

        public static ImmutableDebuggerSettings Create(DebuggerSettings debuggerSettings) =>
            Create(
                debuggerSettings.Enabled,
                debuggerSettings.ApiKey,
                debuggerSettings.RuntimeId,
                debuggerSettings.ServiceName,
                debuggerSettings.ServiceVersion,
                debuggerSettings.Environment,
                debuggerSettings.MaxSerializationTimeInMilliseconds,
                debuggerSettings.MaximumDepthOfMembersToCopy,
                debuggerSettings.SnapshotsPath,
                debuggerSettings.UploadBatchSize,
                debuggerSettings.DiagnosticsIntervalSeconds,
                debuggerSettings.UploadFlushIntervalMilliseconds,
                debuggerSettings.TransportType,
                debuggerSettings.AgentUri);

        public static ImmutableDebuggerSettings Create(
            bool enabled,
            string apiKey,
            string runtimeId,
            string serviceName,
            string serviceVersion,
            string environment,
            int maxSerializationTimeInMilliseconds,
            int maximumDepthOfMembersOfMembersToCopy,
            string snapshotsPath,
            int uploadBatchSize,
            int diagnosticsIntervalSeconds,
            int uploadFlushIntervalMilliseconds,
            TracesTransportType transportType,
            Uri agentUri) =>
            new ImmutableDebuggerSettings(
                enabled,
                apiKey,
                runtimeId,
                serviceVersion,
                environment,
                maxSerializationTimeInMilliseconds,
                maximumDepthOfMembersOfMembersToCopy,
                snapshotsPath,
                uploadBatchSize,
                diagnosticsIntervalSeconds,
                uploadFlushIntervalMilliseconds,
                transportType,
                agentUri);
    }
}
