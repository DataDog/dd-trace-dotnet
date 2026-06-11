// <copyright file="FilterEvaluationHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Debugger.Expressions;

internal static class FilterEvaluationHelpers
{
    internal static BoundedCaptureCollectionResult<T> FilterForCapture<T>(IEnumerable<T> source, Func<T, bool> predicate, int maxCollectionSize, bool isDictionary)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (maxCollectionSize <= 0)
        {
            maxCollectionSize = 0;
        }

        var items = new List<T>(GetInitialCapacity(source, maxCollectionSize));
        var wasTruncated = false;
        foreach (var item in source)
        {
            if (!predicate(item))
            {
                continue;
            }

            if (items.Count >= maxCollectionSize)
            {
                wasTruncated = true;
                break;
            }

            items.Add(item);
        }

        return new BoundedCaptureCollectionResult<T>(items, wasTruncated, isDictionary);
    }

    private static int GetInitialCapacity<T>(IEnumerable<T> source, int maxCollectionSize)
    {
        if (maxCollectionSize <= 0)
        {
            return 0;
        }

        if (source is ICollection<T> collection)
        {
            return collection.Count < maxCollectionSize ? collection.Count : maxCollectionSize;
        }

        if (source is IReadOnlyCollection<T> readOnlyCollection)
        {
            return readOnlyCollection.Count < maxCollectionSize ? readOnlyCollection.Count : maxCollectionSize;
        }

        return maxCollectionSize;
    }
}
