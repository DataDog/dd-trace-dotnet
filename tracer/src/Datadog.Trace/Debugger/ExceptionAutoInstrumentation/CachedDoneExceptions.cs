// <copyright file="CachedDoneExceptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    /// <summary>
    /// Acts as a proxy to a static `CachedItems` object to make the Exception that are in done cases easily accesible, throughout the codebase
    /// without references detouring.
    /// </summary>
    internal static class CachedDoneExceptions
    {
        private static readonly CachedItems _cachedDoneExceptions = new CachedItems();

        internal static void Add(int item)
        {
            _cachedDoneExceptions.Add(item);
        }

        internal static bool Remove(int item)
        {
            return _cachedDoneExceptions.Remove(item);
        }

        internal static bool Contains(int item)
        {
            return _cachedDoneExceptions.Contains(item);
        }
    }
}
