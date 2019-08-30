using System;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class LoggerExtensions
    {
        public static void ErrorRetrievingMethod(
            this ILog logger,
            Exception exception,
            long moduleVersionPointer,
            int mdToken,
            int opCode,
            string instrumentedType,
            string methodName,
            string instanceType = null)
        {
            var instrumentedMethod = $"{instrumentedType}.{methodName}(...)";
            if (instanceType != null)
            {
                instrumentedMethod = $"{instrumentedMethod} on {instanceType}";
            }

            var moduleVersionId = moduleVersionPointer.GetGuidFromNativePointer();
            logger.ErrorException(
                message: $"Error (MVID: {moduleVersionId}, mdToken: {mdToken}, opCode: {opCode}) could not retrieve: {instrumentedMethod}",
                exception: exception);
        }
    }
}
