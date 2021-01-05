using System;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestAssemblyRunner`1.RunTestCollectionAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assemblies = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        Type = "Xunit.Sdk.TestAssemblyRunner`1",
        Method = "RunTestCollectionAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<Xunit.Sdk.RunSummary>",
        ParametersTypesNames = new[] { "Xunit.Sdk.IMessageBus", "_", "_", "_" },
        MinimumVersion = "2.2.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    public static class XUnitTestAssemblyRunnerRunTestCollectionAsyncIntegration
    {
        private const string IntegrationName = "XUnit";

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            // We have to ensure the flush of the buffer after we finish the tests of an assembly.
            // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
            // So the last spans in buffer aren't send to the agent.
            // Other times we reach the 500 items of the buffer in a sec and the tracer start to drop spans.
            // In a test scenario we must keep all spans.

            SynchronizationContext currentContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                Tracer.Instance.FlushAsync().GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(currentContext);
            }

            return returnValue;
        }
    }
}
