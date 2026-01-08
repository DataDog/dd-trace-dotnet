// <copyright file="ActivityEnumerationHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Activity.DuckTypes;

namespace Datadog.Trace.Activity.Helpers;

internal static class ActivityEnumerationHelper
{
    private static AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, object?>>, KeyValuePair<string, object?>, OtelTagsEnumerationState>.AllocationFreeForEachDelegate? _tagObjectsEnumerator;
    private static AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, string?>>, KeyValuePair<string, string?>, OtelTagsEnumerationState>.AllocationFreeForEachDelegate? _tagsEnumerator;

    /// <summary>
    /// Returns an enumerator than can be used to iterate the provided <see cref="IActivity5.TagObjects"/>, without allocating.
    /// Before calling this API, call
    /// </summary>
    /// <param name="activity5">The activity to enumerate</param>
    /// <returns>An enumerator that can be used prior to enumerating</returns>
    public static AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, object?>>, KeyValuePair<string, object?>, OtelTagsEnumerationState>.AllocationFreeForEachDelegate GetTagObjectsEnumerator<T>(T activity5)
        where T : IActivity5
    {
        return Volatile.Read(ref _tagObjectsEnumerator) ?? BuildDelegate(activity5);

        static AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, object?>>, KeyValuePair<string, object?>, OtelTagsEnumerationState>.AllocationFreeForEachDelegate BuildDelegate(T activity5)
        {
            var forEach = AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, object?>>, KeyValuePair<string, object?>, OtelTagsEnumerationState>
               .BuildAllocationFreeForEachDelegate(activity5.TagObjects.GetType());

            return Interlocked.CompareExchange(ref _tagObjectsEnumerator, forEach, null) ?? forEach;
        }
    }

    public static AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, string?>>, KeyValuePair<string, string?>, OtelTagsEnumerationState>.AllocationFreeForEachDelegate GetTagsEnumerator<T>(T activity)
        where T : IActivity
    {
        return Volatile.Read(ref _tagsEnumerator) ?? BuildDelegate(activity);

        static AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, string?>>, KeyValuePair<string, string?>, OtelTagsEnumerationState>.AllocationFreeForEachDelegate BuildDelegate(T activity)
        {
            var forEach = AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, string?>>, KeyValuePair<string, string?>, OtelTagsEnumerationState>
               .BuildAllocationFreeForEachDelegate(activity.Tags.GetType());

            return Interlocked.CompareExchange(ref _tagsEnumerator, forEach, null) ?? forEach;
        }
    }

    /// <summary>
    /// Checks if the <see cref="IActivity5.TagObjects"/> object is a zero-length array, to avoid unnecessary allocations
    /// from boxing the enumerator
    /// </summary>
    /// <returns>true if <see cref="IActivity5.TagObjects"/> may contain values, false if it definitely doesn't</returns>
    public static bool HasTagObjects<T>(this T activity5)
        where T : IActivity5
        => activity5.TagObjects is not KeyValuePair<string, object?>[] { Length: 0 };

    /// <summary>
    /// Checks if the <see cref="IActivity5.TagObjects"/> object is a zero-length array, to avoid unnecessary allocations
    /// from boxing the enumerator
    /// </summary>
    /// <returns>true if <see cref="IActivity5.TagObjects"/> may contain values, false if it definitely doesn't</returns>
    public static bool HasTags<T>(this T activity5)
        where T : IActivity
        => activity5.Tags is not KeyValuePair<string, string?>[] { Length: 0 };
}
