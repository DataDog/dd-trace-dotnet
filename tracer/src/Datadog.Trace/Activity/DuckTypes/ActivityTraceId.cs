// <copyright file="ActivityTraceId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// for reference https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Activity.cs#L1756

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity.DuckTypes
{
    [DuckCopy]
    internal struct ActivityTraceId
    {
        [DuckField(Name = "_hexString")]
        public string Id { get; }
    }
}
