// <copyright file="HeadersCollectionTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.TestHelpers
{
    public static class HeadersCollectionTestHelpers
    {
        public static IEnumerable<object[]> GetInvalidIds()
        {
            yield return new object[] { null };
            yield return new object[] { string.Empty };
            yield return new object[] { "0" };
            yield return new object[] { "-1" };
            yield return new object[] { "id" };
        }

        public static IEnumerable<object[]> GetInvalidIntegerSamplingPriorities()
        {
            // these are valid integers, but not values defined in the sampling priority enum.
            // when we see this values in distributed tracing, we pass them along unchanged
            // to allow for forward compatibility (in case new valid enum values are added in the future).
            // keep these test values large to avoid conflicts when we may add new valid values like -2 or +3.
            yield return new object[] { "-1000" };
            yield return new object[] { "1000" };
        }

        public static IEnumerable<object[]> GetInvalidNonIntegerSamplingPriorities()
        {
            // these are not valid integers and will be ignored if found in distributed tracing
            yield return new object[] { "1.0" };
            yield return new object[] { "1,0" };
            yield return new object[] { "sampling.priority" };
        }
    }
}
