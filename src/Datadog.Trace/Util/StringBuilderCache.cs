using System;
using System.Text;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Provide a cached reusable instance of StringBuilder per thread.
    /// </summary>
    /// <remarks>
    /// Based on https://source.dot.net/#System.Private.CoreLib/StringBuilderCache.cs,a6dbe82674916ac0
    /// </remarks>
    internal static class StringBuilderCache
    {
        internal const int MaxBuilderSize = 360;

        [ThreadStatic]
        private static StringBuilder _cachedInstance;

        public static StringBuilder Acquire(int capacity)
        {
            if (capacity <= MaxBuilderSize)
            {
                StringBuilder sb = _cachedInstance;
                if (sb != null)
                {
                    // Avoid StringBuilder block fragmentation by getting a new StringBuilder
                    // when the requested size is larger than the current capacity
                    if (capacity <= sb.Capacity)
                    {
                        _cachedInstance = null;
                        sb.Clear();
                        return sb;
                    }
                }
            }

            return new StringBuilder(capacity);
        }

        public static string GetStringAndRelease(StringBuilder sb)
        {
            string result = sb.ToString();
            if (sb.Capacity <= MaxBuilderSize)
            {
                _cachedInstance = sb;
            }

            return result;
        }
    }
}
