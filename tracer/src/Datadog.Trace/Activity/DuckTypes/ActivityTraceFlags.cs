// <copyright file="ActivityTraceFlags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// TODO where is this enum in runtime github?

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity.DuckTypes
{
    [System.Flags] // TODO unsure if this is needed?
    internal enum ActivityTraceFlags
    {
        None = 0,
        Recorded = 1
    }
}
