// <copyright file="MessagePackFieldNames.DSM.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Agent.MessagePack
{
    /// <summary>
    /// MessagePack field names for Data Streams Monitoring serialization (DSM-specific part).
    /// These constants are marked with [MessagePackField] to generate pre-serialized byte arrays.
    /// </summary>
    internal static partial class MessagePackFieldNames
    {
        // Top-level payload fields (PascalCase per DSM protocol)
        // Using DSM suffix to avoid naming conflicts with span protocol fields (which use lowercase)
        [MessagePackField]
        public const string EnvDSM = "Env";

        [MessagePackField]
        public const string ServiceDSM = "Service";

        [MessagePackField]
        public const string Stats = "Stats";

        [MessagePackField]
        public const string Backlogs = "Backlogs";

        [MessagePackField]
        public const string TracerVersion = "TracerVersion";

        [MessagePackField]
        public const string Lang = "Lang";

        [MessagePackField]
        public const string ProductMask = "ProductMask";

        [MessagePackField]
        public const string ProcessTags = "ProcessTags";

        [MessagePackField]
        public const string IsInDefaultState = "IsInDefaultState";

        // Bucket fields
        [MessagePackField]
        public const string StartDSM = "Start";

        [MessagePackField]
        public const string DurationDSM = "Duration";

        // Stats point fields
        [MessagePackField]
        public const string Hash = "Hash";

        [MessagePackField]
        public const string ParentHash = "ParentHash";

        [MessagePackField]
        public const string TimestampType = "TimestampType";

        [MessagePackField]
        public const string PathwayLatency = "PathwayLatency";

        [MessagePackField]
        public const string EdgeLatency = "EdgeLatency";

        [MessagePackField]
        public const string PayloadSize = "PayloadSize";

        [MessagePackField]
        public const string EdgeTags = "EdgeTags";

        // Backlog fields
        [MessagePackField]
        public const string Tags = "Tags";

        [MessagePackField]
        public const string Value = "Value";

        // Timestamp type values
        [MessagePackField]
        public const string Current = "current";

        [MessagePackField]
        public const string Origin = "origin";
    }
}
