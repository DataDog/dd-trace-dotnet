// <copyright file="IActivity5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Activity.DuckTypes
{
    internal interface IActivity5 : IW3CActivity
    {
        string DisplayName { get; }

        bool IsAllDataRequested { get; set; }

        ActivityKind Kind { get; }

        IEnumerable<KeyValuePair<string, object>> TagObjects { get; }

        ActivitySource Source { get; }

        IEnumerable Events { get; }

        /// <summary>
        /// Gets the list of all <see cref="IActivityLink" /> objects attached to this Activity object.
        /// If there is no any <see cref="IActivityLink" /> object attached to the Activity object, Links will return empty list.
        /// </summary>
        IEnumerable Links { get; }

        object AddTag(string key, object value);
    }
}
