using System.Runtime.CompilerServices;
using Samples.Probes;
using Samples.Probes.Shared;
using Samples.Probes.SmokeTests;

public class SimpleTypeNameInGlobalNamespaceTest : IRun
{
    public void Run()
    {
        MethodToInstrument(nameof(Run));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    [MethodProbeTestData("System.Void", new[] { "System.String" }, useFullTypeName: false)]
    public void MethodToInstrument(string callerName)
    {
        var arr = new[] { callerName, nameof(MethodToInstrument), nameof(SimpleTypeNameTest) };
        if (NoOp(arr).Length == arr.Length)
        {
            throw new IntentionalDebuggerException("Same length.");
        }
    }

    [MethodImpl(MethodImplOptions.NoOptimization)]
    string[] NoOp(string[] arr) => arr;
}
