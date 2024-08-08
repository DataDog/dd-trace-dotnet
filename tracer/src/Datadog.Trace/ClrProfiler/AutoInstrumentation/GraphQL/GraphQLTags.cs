// <copyright file="GraphQLTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
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

        [Tag(Internal.Tags.SpanKind)]
        public override string SpanKind => InternalSpanKinds.Server;

        [Tag(Internal.Tags.InstrumentationName)]
        public string InstrumentationName { get; }

        [Tag(Internal.Tags.GraphQLSource)]
        public string Source { get; set; }

        [Tag(Internal.Tags.GraphQLOperationName)]
        public string OperationName { get; set; }

        [Tag(Internal.Tags.GraphQLOperationType)]
        public string OperationType { get; set; }
    }
}
