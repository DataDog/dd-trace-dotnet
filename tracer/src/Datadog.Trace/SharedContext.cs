// <copyright file="SharedContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace
{
    internal static class SharedContext
    {
        private static IReadOnlyDictionary<string, string> _distributedTrace;

        public static object GetDistributedTrace()
        {
            return null;
        }

        public static void SetDistributedTrace(object value)
        {
        }

        // Implementations

        private static object GetDistributedTraceImpl()
        {
            return _distributedTrace;
        }

        private static void SetDistributedTraceImpl(object value)
        {
            _distributedTrace = (IReadOnlyDictionary<string, string>)value;
        }
    }
}
