using System;

namespace Datadog.Trace.Logging
{
    internal static partial class LogExtensions
    {
        public static bool ErrorExceptionForFilter(this ILog logger, string message, Exception exception, params object[] formatParams)
        {
            ErrorException(logger, message, exception, formatParams);
            return false;
        }
    }
}
