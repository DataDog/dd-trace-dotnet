// <copyright file="ActivityContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// for reference: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/ActivityContext.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity.DuckTypes
{
    // TODO this is only available .NET 5+
    [DuckCopy]
    internal struct ActivityContext
    {
        public ActivityTraceId TraceId { get; }

        public ActivitySpanId SpanId { get; }

        public ActivityTraceFlags TraceFlags { get; }

        public string? TraceState { get; }

        public bool IsRemote { get; }
    }
}
