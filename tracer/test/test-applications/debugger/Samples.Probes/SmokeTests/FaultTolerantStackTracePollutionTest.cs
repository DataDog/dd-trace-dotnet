using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    public class FaultTolerantStackTracePollutionTest : IRun
    {
        private string _output;

        public void Run()
        {
            try
            {
                Kickoff();
            }
            catch (Exception ex)
            {
                var source = ex.TargetSite;
                var exceptionString = ex.StackTrace;
                var toString = ex.ToString();
                Console.WriteLine($"{source.Name}:{exceptionString}:{toString}");
                A();
            }
        }

        void Kickoff()
        {
            A();
        }

        void A()
        {
            var currenTFrame = new StackFrame(0);
            var currentMethod = MethodBase.GetCurrentMethod();
            Instrumented_A();
        }

        void Instrumented_A()
        {
            var currenTFrame = new StackFrame(0);
            var currentMethod = MethodBase.GetCurrentMethod();
            B();
        }

        void B()
        {
            var currenTFrame = new StackFrame(0);
            var currentMethod = MethodBase.GetCurrentMethod();
            Instrumented_B();
        }

        void Instrumented_B()
        {
            var currenTFrame = new StackFrame(0);
            var currentMethod = MethodBase.GetCurrentMethod();
            Instrumented_BeforeBeforeSayWhat();
        }

        void Instrumented_BeforeBeforeSayWhat()
        {
            var currenTFrame = new StackFrame(0);
            var currentMethod = MethodBase.GetCurrentMethod();
            Instrumented_BeforeSayWhat();
        }

        void Instrumented_BeforeSayWhat()
        {
            var currenTFrame = new StackFrame(0);
            var currentMethod = MethodBase.GetCurrentMethod();
            C();
        }

        void C()
        {
            var currenTFrame = new StackFrame(0);
            var currentMethod = MethodBase.GetCurrentMethod();
            Instrumented_SayWhat();
        }

        void Instrumented_SayWhat()
        {
            var currenTFrame = new StackFrame(0);
            var currentMethod = MethodBase.GetCurrentMethod();
            D();
        }

        void D()
        {
            var stackTrace = new StackTrace(0).ToString();
            var currentFrame = new StackFrame(0);
            var up1 = new StackFrame(1);
            var up2 = new StackFrame(2);
            var up3 = new StackFrame(3);
            var up4 = new StackFrame(4);
            var up5 = new StackFrame(5);
            Instrumented_Thrower();
        }

        void Instrumented_Thrower()
        {
            throw new InvalidOperationException("WTF");
        }
    }
}
