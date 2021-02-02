using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestRunner`1.RunAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyNames = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        TypeName = "Xunit.Sdk.TestRunner`1",
        MethodName = "RunAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<Xunit.Sdk.RunSummary>",
        MinimumVersion = "2.2.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    public static class XUnitTestRunnerRunAsyncIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.XUnit);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
            {
                TestRunnerStruct runnerInstance = instance.As<TestRunnerStruct>();

                // Skip test support
                if (runnerInstance.SkipReason != null)
                {
                    XUnitIntegration.CreateScope(ref runnerInstance, instance.GetType());
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
