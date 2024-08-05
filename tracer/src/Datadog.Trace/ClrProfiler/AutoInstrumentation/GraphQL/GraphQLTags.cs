// <copyright file="GraphQLTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.SourceGenerators;
using Datadog.Trace.Internal.Tagging;

namespace Datadog.Trace.Internal.ClrProfiler.AutoInstrumentation.GraphQL
{
    internal partial class GraphQLTags : InstrumentationTags
    {
        public GraphQLTags()
        {
        }

        public GraphQLTags(string instrumentationName)
        {
            InstrumentationName = instrumentationName;
        }

        [Tag(Trace.Internal.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Server;

        [Tag(Trace.Internal.Tags.InstrumentationName)]
        public string InstrumentationName { get; }

        [Tag(Trace.Internal.Tags.GraphQLSource)]
        public string Source { get; set; }

        [Tag(Trace.Internal.Tags.GraphQLOperationName)]
        public string OperationName { get; set; }

        [Tag(Trace.Internal.Tags.GraphQLOperationType)]
        public string OperationType { get; set; }
    }
}
