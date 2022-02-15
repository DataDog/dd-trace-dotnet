// <copyright file="IActivity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity
{
    internal interface IActivity : IDuckType
    {
        string DisplayName { get; }

        TimeSpan Duration { get; }

        string OperationName { get; }

        IActivity Parent { get; }

        DateTime StartTimeUtc { get; }

        IEnumerable<KeyValuePair<string, string>> Baggage { get; }

        IEnumerable<KeyValuePair<string, string>> Tags { get; }

        IEnumerable<KeyValuePair<string, object>> TagObjects { get; }

        object AddBaggage(string key, string value);

        object AddTag(string key, string value);

        object AddTag(string key, object value);
    }
}
