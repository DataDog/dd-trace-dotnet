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
        string Id { get; }

        string RootId { get; }

        TimeSpan Duration { get; }

        string OperationName { get; }

        IActivity Parent { get; }

        DateTime StartTimeUtc { get; }

        IEnumerable<KeyValuePair<string, string>> Baggage { get; }

        IEnumerable<KeyValuePair<string, string>> Tags { get; }

        object AddBaggage(string key, string value);

        object AddTag(string key, string value);

        string GetBaggageItem(string key);

        object SetEndTime(DateTime endTimeUtc);

        object SetParentId(string parentId);

        object SetStartTime(DateTime startTimeUtc);
    }

    internal interface IActivity5 : IActivity
    {
        string DisplayName { get; }

        bool IsAllDataRequested { get; set; }

        ActivityKind Kind { get; }

        IEnumerable<KeyValuePair<string, object>> TagObjects { get; }

        object AddTag(string key, object value);
    }

    internal interface IActivity6 : IActivity5
    {
        string StatusDescription { get; }
    }
}
