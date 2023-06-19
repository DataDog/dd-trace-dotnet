using System;
using System.Text;
using Datadog.Trace.ClrProfiler;

namespace CallTargetNativeTest;

partial class Program
{
    static void InitCallSiteDebug()
    {
        string[] aspects = new string[]
        {
                @"[AspectClass(""CallTargetNativeTest"",[None],Propagation,[])] Datadog.Trace.Iast.Aspects.DebugAspects",
                @"  [AspectCtorReplace(""CallTargetNativeTest.Program+CallSiteTargets::.ctor(System.String,System.String)"","""",[0],[False],[None],Propagation,[])] AspectCtorReplace(System.String,System.String)",
                @"  [AspectMethodReplace(""CallTargetNativeTest.Program+CallSiteTargets::TargetMethodReplace(System.String,System.String)"","""",[0],[False],[None],Propagation,[])] AspectMethodReplace(System.String,System.String)",
                @"  [AspectMethodInsertBefore(""CallTargetNativeTest.Program+CallSiteTargets::TargetMethodInsertBefore_0(System.String,System.String)"","""",[0],[False],[None],Propagation,[])] AspectMethodInsertBefore_0(System.String)",
                @"  [AspectMethodInsertBefore(""CallTargetNativeTest.Program+CallSiteTargets::TargetMethodInsertBefore_1(System.String,System.String)"","""",[1],[False],[None],Propagation,[])] AspectMethodInsertBefore_1(System.String)",
                @"  [AspectMethodInsertAfter(""CallTargetNativeTest.Program+CallSiteTargets::TargetMethodInsertAfter(System.String,System.String)"","""",[0],[False],[None],Propagation,[])] AspectMethodInsertAfter(System.String)",
        };
        NativeMethods.RegisterIastAspects(aspects);
    }

    private static void CallSite()
    {
        InitCallSiteDebug();

        var tests = new CallSiteTests();
        Console.WriteLine($"CallSite Tests");
        string param1 = "concat1";
        string param2 = "CONCAT2";
        string espected = param1 + param2;
        CallSiteTests.RunMethod(() => tests.AspectCtorReplace(param1, param2, espected), $"[AspectCtorReplace]DebugAspects.AspectCtorReplace(string {param1}, string {param2})");
        CallSiteTests.RunMethod(() => tests.AspectMethodReplace(param1, param2, espected), $"[AspectMethodReplace]DebugAspects.AspectMethodReplace(string {param1}, string {param2})");
        CallSiteTests.RunMethod(() => tests.AspectMethodInsertBefore_0(param1, param2, espected), $"[AspectMethodInsertBefore]DebugAspects.AspectMethodInsertBefore_0(string {param2})");
        CallSiteTests.RunMethod(() => tests.AspectMethodInsertBefore_1(param1, param2, espected), $"[AspectMethodInsertBefore]DebugAspects.AspectMethodInsertBefore_1(string {param1})");
        CallSiteTests.RunMethod(() => tests.AspectMethodInsertAfter(param1, param2, espected), $"[AspectMethodInsertAfter]DebugAspects.AspectMethodInsertAfter(string {espected})");
    }

    public class CallSiteTests
    {
        internal static void RunMethod(Action action, string aspectMethod)
        {
            var cOut = Console.Out;
            Console.SetOut(sWriter);
            action();
            sWriter.Flush();
            var str = Encoding.UTF8.GetString(mStream.GetBuffer(), 0, (int)mStream.Length);
            mStream.SetLength(0);
            if (!string.IsNullOrEmpty(aspectMethod))
            {
                if (!str.Contains(aspectMethod))
                {
                    throw new Exception("Profiler didn't return a valid Debug Aspect Method execution");
                }
            }
            if (!string.IsNullOrEmpty(str))
            {
                cOut.Write("     " + string.Join("\n     ", str.Split('\n')));
            }
            Console.SetOut(cOut);
            Console.WriteLine();
        }

        public object AspectCtorReplace(string param1, string param2, string espected)
        {
            var result = new CallSiteTargets(param1, param2); //This new call should be probed with the instance
            System.Diagnostics.Debug.Assert(result.ToString() == espected);

            return result;
        }
        public string AspectMethodReplace(string param1, string param2, string espected)
        {
            var result = CallSiteTargets.TargetMethodReplace(param1, param2); // This call should be replaced by the Aspect
            System.Diagnostics.Debug.Assert(result == espected);

            return result;
        }
        public string AspectMethodInsertBefore_0(string param1, string param2, string espected)
        {
            var result = CallSiteTargets.TargetMethodInsertBefore_0(param1, param2); //This call should be probed in param2
            System.Diagnostics.Debug.Assert(result == espected);

            return result;
        }
        public string AspectMethodInsertBefore_1(string param1, string param2, string espected)
        {
            var result = CallSiteTargets.TargetMethodInsertBefore_1(param1, param2); //This call should be probed in param1
            System.Diagnostics.Debug.Assert(result == espected);

            return result;
        }
        public string AspectMethodInsertAfter(string param1, string param2, string espected)
        {
            var result = CallSiteTargets.TargetMethodInsertAfter(param1, param2); //This call should be probed in the result
            System.Diagnostics.Debug.Assert(result == espected);

            return result;
        }
    }

    public class CallSiteTargets
    {
        private string espected;

        public CallSiteTargets(string param1, string param2)
        {
            espected = param1 + param2;
        }

        public override string ToString()
        {
            return espected;
        }

        public static string TargetMethodReplace(string param1, string param2)
        {
            return string.Concat(param1, param2);
        }
        public static string TargetMethodInsertBefore_0(string param1, string param2)
        {
            return string.Concat(param1, param2);
        }
        public static string TargetMethodInsertBefore_1(string param1, string param2)
        {
            return string.Concat(param1, param2);
        }
        public static string TargetMethodInsertAfter(string param1, string param2)
        {
            return string.Concat(param1, param2);
        }

    }
}
