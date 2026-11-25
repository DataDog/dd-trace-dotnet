// <copyright file="ActivityParent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity.DuckTypes
{
    /// <summary>
    /// The parent Activity fields we read, copied by value so reading <see cref="IActivity.Parent"/> doesn't allocate a proxy.
    /// </summary>
    [DuckCopy]
    internal struct ActivityParent
    {
        public DateTime StartTimeUtc;
    }
}
