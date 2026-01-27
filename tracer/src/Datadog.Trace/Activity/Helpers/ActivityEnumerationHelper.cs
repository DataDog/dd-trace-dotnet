// <copyright file="ActivityEnumerationHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Logging;
using TagObjectsEnumerator = Datadog.Trace.Activity.Helpers.AllocationFreeEnumerator<
    System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object?>>,
    System.Collections.Generic.KeyValuePair<string, object?>,
    Datadog.Trace.Activity.Helpers.OtelTagsEnumerationState>;
using TagsEnumerator = Datadog.Trace.Activity.Helpers.AllocationFreeEnumerator<
    System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string?>>,
    System.Collections.Generic.KeyValuePair<string, string?>,
    Datadog.Trace.Activity.Helpers.OtelTagsEnumerationState>;

namespace Datadog.Trace.Activity.Helpers;

internal static class ActivityEnumerationHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityEnumerationHelper));

    private static readonly bool IsDotNet10
#if NET10_0_OR_GREATER
        = true;
#else
        = FrameworkDescription.Instance.RuntimeVersion.Major >= 10;
#endif

    private static TagObjectsEnumerator.AllocationFreeForEachDelegate? _tagObjectsEnumerator;
    private static TagsEnumerator.AllocationFreeForEachDelegate? _tagsEnumerator;
    private static Type? _tagObjectsType;
    private static Type? _tagsType;

    /// <summary>
    /// Performs an allocation-free enumeration of the TagObjects field in the provided activity
    /// </summary>
    public static void EnumerateTagObjects<T>(T activity5, ref OtelTagsEnumerationState state, TagObjectsEnumerator.ForEachDelegate funcToRun)
        where T : IActivity5
    {
        if (IsDotNet10)
        {
            // .NET 10 doesn't allocate _anyway_, so we should just do naive enumeration
            foreach (var value in activity5.TagObjects)
            {
                funcToRun(ref state, value);
            }

            return;
        }

        var tagObjects = activity5.TagObjects;
        var runtimeType = tagObjects.GetType();

        // This is a safety check - we must always invoke this method with the same _runtime_ type
        // If we have a different type, we fallback to returning null instead and doing the allocating case
        var expected = Volatile.Read(ref _tagObjectsType);
        if (expected == runtimeType || expected is null)
        {
            var forEach = Volatile.Read(ref _tagObjectsEnumerator) ?? BuildDelegate(runtimeType);
            forEach(activity5.TagObjects, ref state, funcToRun);
            return;
        }

        // fallback case
        EnumerateWithAllocation(ref state, funcToRun, tagObjects, expected, runtimeType);

        static TagObjectsEnumerator.AllocationFreeForEachDelegate BuildDelegate(Type runtimeType)
        {
            var forEach = TagObjectsEnumerator.BuildAllocationFreeForEachDelegate(runtimeType);

            Volatile.Write(ref _tagObjectsType, runtimeType);
            return Interlocked.CompareExchange(ref _tagObjectsEnumerator, forEach, null) ?? forEach;
        }

        static void EnumerateWithAllocation(ref OtelTagsEnumerationState state, TagObjectsEnumerator.ForEachDelegate forEachDelegate, IEnumerable<KeyValuePair<string, object?>> values, Type expected, Type runtimeType)
        {
            Log.Error($"Invalid activity enumeration object was passed to {nameof(EnumerateTagObjects)}. Expected {{Expected}} but received {{Actual}}. Executing foreach loop using allocating fallback", expected, runtimeType);
            foreach (var value in values)
            {
                forEachDelegate(ref state, value);
            }
        }
    }

    public static void EnumerateTags<T>(T activity, ref OtelTagsEnumerationState state, TagsEnumerator.ForEachDelegate funcToRun)
        where T : IActivity
    {
        if (IsDotNet10)
        {
            // .NET 10 doesn't allocate _anyway_, so we should just do naive enumeration
            foreach (var value in activity.Tags)
            {
                funcToRun(ref state, value);
            }

            return;
        }

        var tags = activity.Tags;
        var runtimeType = tags.GetType();

        // This is a safety check - we must always invoke this method with the same _runtime_ type
        // If we have a different type, we fallback to returning null instead and doing the allocating case
        var expected = Volatile.Read(ref _tagsType);
        if (expected == runtimeType || expected is null)
        {
            var forEach = Volatile.Read(ref _tagsEnumerator) ?? BuildDelegate(runtimeType);
            forEach(activity.Tags, ref state, funcToRun);
            return;
        }

        // fallback case
        EnumerateWithAllocation(ref state, funcToRun, tags, expected, runtimeType);

        static TagsEnumerator.AllocationFreeForEachDelegate BuildDelegate(Type runtimeType)
        {
            var forEach = TagsEnumerator.BuildAllocationFreeForEachDelegate(runtimeType);

            Volatile.Write(ref _tagsType, runtimeType);
            return Interlocked.CompareExchange(ref _tagsEnumerator, forEach, null) ?? forEach;
        }

        static void EnumerateWithAllocation(ref OtelTagsEnumerationState state, TagsEnumerator.ForEachDelegate forEachDelegate, IEnumerable<KeyValuePair<string, string?>> values, Type expected, Type runtimeType)
        {
            Log.Error($"Invalid activity enumeration object was passed to {nameof(EnumerateTags)}. Expected {{Expected}} but received {{Actual}}. Executing foreach loop using allocating fallback", expected, runtimeType);
            foreach (var value in values)
            {
                forEachDelegate(ref state, value);
            }
        }
    }

    /// <summary>
    /// Checks if the <see cref="IActivity5.TagObjects"/> object is a zero-length array, to avoid unnecessary allocations
    /// from boxing the enumerator
    /// </summary>
    /// <returns>true if <see cref="IActivity5.TagObjects"/> may contain values, false if it definitely doesn't</returns>
    public static bool HasTagObjects<T>(this T activity5)
        where T : IActivity5
        => activity5.TagObjects is not null and not KeyValuePair<string, object?>[] { Length: 0 };

    /// <summary>
    /// Checks if the <see cref="IActivity5.TagObjects"/> object is a zero-length array, to avoid unnecessary allocations
    /// from boxing the enumerator
    /// </summary>
    /// <returns>true if <see cref="IActivity5.TagObjects"/> may contain values, false if it definitely doesn't</returns>
    public static bool HasTags<T>(this T activity5)
        where T : IActivity
        => activity5.Tags is not null and not KeyValuePair<string, string?>[] { Length: 0 };

    /// <summary>
    /// Checks if the <see cref="IActivity5.Events"/> object is a zero-length array, to avoid unnecessary allocations
    /// from boxing the enumerator
    /// </summary>
    /// <returns>true if <see cref="IActivity5.Events"/> may contain values, false if it definitely doesn't</returns>
    public static bool HasEvents<T>(this T activity5)
        where T : IActivity5
        => activity5.Events is not null and not Array { Length: 0 };

    /// <summary>
    /// Checks if the <see cref="IActivity5.Links"/> object is a zero-length array, to avoid unnecessary allocations
    /// from boxing the enumerator
    /// </summary>
    /// <returns>true if <see cref="IActivity5.Links"/> may contain values, false if it definitely doesn't</returns>
    public static bool HasLinks<T>(this T activity5)
        where T : IActivity5
        => activity5.Links is not null and not Array { Length: 0 };
}
