// <copyright file="IActivity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity.DuckTypes
{
    internal interface IActivity : IDuckType
    {
        /// <summary>
        /// Gets an ID for the activity. If the Activity is using hierarchical IDs, the Id has a hierarchical structure:
        /// '|root-id.id1_id2.id3_' and is generated when Start() is called by appending suffix to Parent.Id
        /// or ParentId; Activity has no Id until it started, but as we only see started IDs, this will always be non null.
        /// </summary>
        /// <remarks>NOTE: this property allocates when using W3C activities, and so should generally not be called
        /// unless you know that you're not using W3C activities (because <see cref="IW3CActivity.SpanId"/> is null</remarks>
        string Id { get; }

        string? ParentId { get; }

        // The corresponding property on the Activity object is nullable,
        // but we are guaranteed that the value will not be null once the Activity is started.
        // Since we are only accessing the Activity after it has started, we can treat the value
        // as never having a null value.
        string RootId { get; }

        TimeSpan Duration { get; }

        string? OperationName { get; }

        IActivity? Parent { get; }

        DateTime StartTimeUtc { get; }

        IEnumerable<KeyValuePair<string, string?>> Baggage { get; }

        IEnumerable<KeyValuePair<string, string?>> Tags { get; }

        object AddBaggage(string key, string value);

        object AddTag(string key, string value);

        string GetBaggageItem(string key);

        object SetEndTime(DateTime endTimeUtc);

        object SetParentId(string parentId);

        object SetStartTime(DateTime startTimeUtc);
    }
}
