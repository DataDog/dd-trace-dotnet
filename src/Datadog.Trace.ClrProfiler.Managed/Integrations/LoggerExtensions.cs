using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class LoggerExtensions
    {
        public static void ErrorRetrievingMethod(
            this IDatadogLogger logger,
            Exception exception,
            long moduleVersionPointer,
            int mdToken,
            int opCode,
            string instrumentedType,
            string methodName,
            string instanceType = null,
            string[] relevantArguments = null,
            [CallerLineNumber] int sourceLine = 0,
            [CallerFilePath] string sourceFile = "")
        {
            var instrumentedMethod = $"{instrumentedType}.{methodName}(...)";

            if (instanceType != null)
            {
                instrumentedMethod = $"{instrumentedMethod} on {instanceType}";
            }

            if (relevantArguments != null)
            {
                instrumentedMethod = $"{instrumentedMethod} with {string.Join(", ", relevantArguments)}";
            }

            var moduleVersionId = PointerHelpers.GetGuidFromNativePointer(moduleVersionPointer);
            logger.Error(
                exception,
                $"Error (MVID: {moduleVersionId}, mdToken: {mdToken}, opCode: {opCode}) could not retrieve: {instrumentedMethod}",
                sourceLine,
                sourceFile);

            var statsd = Tracer.Instance.Statsd;

            if (statsd != null)
            {
                string[] tags = { $"instrumented-method:{instrumentedMethod}" };
                statsd.Exception(exception, source: instrumentedType, message: "Error retrieving instrumented method", tags);
            }
        }
    }
}
