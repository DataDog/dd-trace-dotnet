using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static class IntegrationOptions<TIntegration, TTarget>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IntegrationOptions<TIntegration, TTarget>));

        private static volatile bool _disableIntegration = false;

        internal static bool IsIntegrationEnabled => !_disableIntegration;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DisableIntegration() => _disableIntegration = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogException(Exception exception, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
        {
            Log.Error(exception, exception?.Message, sourceLine, sourceFile);
            if (exception is DuckTypeException)
            {
                Log.Warning($"DuckTypeException has been detected, the integration <{typeof(TIntegration)}, {typeof(TTarget)}> will be disabled.");
                _disableIntegration = true;
            }
            else if (exception is CallTargetInvokerException)
            {
                Log.Warning($"CallTargetInvokerException has been detected, the integration <{typeof(TIntegration)}, {typeof(TTarget)}> will be disabled.");
                _disableIntegration = true;
            }
        }
    }
}
