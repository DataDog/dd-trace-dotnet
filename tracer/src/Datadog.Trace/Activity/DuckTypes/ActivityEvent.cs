// <copyright file="ActivityEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

// https://learn.microsoft.com/dotnet/api/system.diagnostics.activityevent

namespace Datadog.Trace.Activity.DuckTypes
{
    [DuckCopy]
    internal struct ActivityEvent
    {
        public string Name;

        public IEnumerable<KeyValuePair<string, object?>> Tags;

        public DateTimeOffset Timestamp;
    }
}
