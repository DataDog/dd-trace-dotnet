using System;

namespace Datadog.Trace.Logging
{
    internal static partial class LibLogExtensions
    {
        public static bool ErrorExceptionForFilter(this IDatadogLogger logger, string message, Exception exception, params object[] formatParams)
        {
            logger.Error(exception, message, formatParams);
            return false;
        }
    }
}
