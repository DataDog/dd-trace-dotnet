using System;
using Datadog.Trace.ClrProfiler.Helpers;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class LoggerExtensions
    {
        public static void ErrorRetrievingMethod(
            this Vendors.Serilog.ILogger logger,
            Exception exception,
            long moduleVersionPointer,
            int mdToken,
            int opCode,
            string instrumentedType,
            string methodName,
            string instanceType = null,
            string[] relevantArguments = null)
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
                $"Error (MVID: {moduleVersionId}, mdToken: {mdToken}, opCode: {opCode}) could not retrieve: {instrumentedMethod}");

            var statsd = Tracer.Instance.Statsd;

            if (statsd != null)
            {
                string[] tags = { $"instrumented-method:{instrumentedMethod}" };
                var command = statsd.GetException(exception, source: instrumentedType, message: "Error retrieving instrumented method", tags);
                statsd.Send(command);
            }
        }
    }
}
