using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestRunner`1.RunAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assemblies = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        Type = "Xunit.Sdk.TestRunner`1",
        Method = "RunAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<Xunit.Sdk.RunSummary>",
        ParametersTypesNames = new string[0],
        MinimumVersion = "2.2.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    public static class XUnitTestRunnerRunAsyncIntegration
    {
        private const string IntegrationName = "XUnit";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                return CallTargetState.GetDefault();
            }

            TestRunnerStruct runnerInstance = instance.As<TestRunnerStruct>();

            // Skip test support
            if (runnerInstance.SkipReason != null)
            {
                XUnitIntegration.CreateScope(ref runnerInstance, instance.GetType());
            }

            return CallTargetState.GetDefault();
        }
    }
}
