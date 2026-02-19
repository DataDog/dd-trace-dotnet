// <copyright file="MessagePackFieldNames.CIVisibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Agent.MessagePack
{
    /// <summary>
    /// MessagePack field names for CI Visibility serialization (CI-specific part).
    /// These constants are marked with [MessagePackField] to generate pre-serialized byte arrays.
    /// </summary>
    internal static partial class MessagePackFieldNames
    {
        [MessagePackField]
        public const string Content = "content";

        // CIEventMessagePackFormatter fields
        [MessagePackField]
        public const string Metadata = "metadata";

        [MessagePackField]
        public const string Asterisk = "*";

        [MessagePackField]
        public const string TestSessionName = "test_session.name";

        // CoveragePayloadMessagePackFormatter fields
        [MessagePackField]
        public const string Coverages = "coverages";

        // CI SpanMessagePackFormatter fields
        [MessagePackField]
        public const string ItrCorrelationId = "itr_correlation_id";

        // Span types (duplicated from SpanTypes.cs which is shared with Manual)
        [MessagePackField]
        public const string Test = "test";

        [MessagePackField]
        public const string TestSuite = "test_suite_end";

        [MessagePackField]
        public const string TestModule = "test_module_end";

        [MessagePackField]
        public const string TestSession = "test_session_end";
    }
}
