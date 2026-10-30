using System.Reflection;

namespace Samples.Probes.TestRuns.SmokeTests
{
#if NETFRAMEWORK
    /// <summary>
    /// Loads `Samples.Probes.Unreferenced.External`, whose NGen image inlines `NGenInlinedTarget.Target` into
    /// `NGenInlinedTarget.Caller`, and calls `Caller` once.
    /// Refer to the test `NGenInlinerOnFirstCallAfterModuleLoadInNetFrameworkTest` for more information.
    /// </summary>
    public class NGenInlinerAfterModuleLoadTest : IRun
    {
        public void Run()
        {
            var assembly = Assembly.Load("Samples.Probes.Unreferenced.External");
            var caller = assembly.GetType("Samples.Probes.Unreferenced.External.NGenInlinedTarget").GetMethod("Caller");
            caller.Invoke(null, new object[] { 1 });
        }
    }
#endif
}
