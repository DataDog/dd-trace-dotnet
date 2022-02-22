// <copyright file="IActivity5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Activity.DuckTypes
{
    internal interface IActivity5 : IActivity
    {
        string DisplayName { get; }

        bool IsAllDataRequested { get; set; }

        ActivityKind Kind { get; }

        IEnumerable<KeyValuePair<string, object>> TagObjects { get; }

        ActivitySource Source { get; }

        object AddTag(string key, object value);
    }
}
