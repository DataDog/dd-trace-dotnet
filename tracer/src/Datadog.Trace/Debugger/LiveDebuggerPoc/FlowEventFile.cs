// <copyright file="FlowEventFile.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal readonly struct FlowEventFile
    {
        public FlowEventFile(FlowEvent[] events, FlowMethodMetadata[] methods)
            : this(events, methods, [], [], [], [], [])
        {
        }

        public FlowEventFile(
            FlowEvent[] events,
            FlowMethodMetadata[] methods,
            IReadOnlyList<string> strings,
            IReadOnlyList<string> types,
            FlowExceptionDetails[] exceptions,
            FlowCapturedValue[] values)
            : this(events, methods, strings, types, exceptions, values, [])
        {
        }

        public FlowEventFile(
            FlowEvent[] events,
            FlowMethodMetadata[] methods,
            IReadOnlyList<string> strings,
            IReadOnlyList<string> types,
            FlowExceptionDetails[] exceptions,
            FlowCapturedValue[] values,
            FlowOperationMetadata[] operations)
        {
            Events = events;
            Methods = methods;
            Strings = strings;
            Types = types;
            Exceptions = exceptions;
            Values = values;
            Operations = operations;
        }

        public FlowEvent[] Events { get; }

        public FlowMethodMetadata[] Methods { get; }

        public IReadOnlyList<string> Strings { get; }

        public IReadOnlyList<string> Types { get; }

        public FlowExceptionDetails[] Exceptions { get; }

        public FlowCapturedValue[] Values { get; }

        public FlowOperationMetadata[] Operations { get; }
    }
}
