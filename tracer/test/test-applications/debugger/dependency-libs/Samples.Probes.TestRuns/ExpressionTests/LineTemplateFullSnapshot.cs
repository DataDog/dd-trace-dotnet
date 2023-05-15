using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    [LogLineProbeTestData(23, templateJson: Json, templateStr: "Result is: ", captureSnapshot: true)]
    internal class LineTemplateFullSnapshot : IRun
    {
        private const string Json = @"{
        ""ref"": ""arg""
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var result = Method(TimeSpan.FromSeconds(1).TotalSeconds);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        string Method(double arg)
        {
            var local = arg + GetInt(arg);
            Console.WriteLine(local);
            return $"Result is: {arg} + {local}";
        }

        int GetInt(double d)
        {
            return (int)(d + 1);
        }
    }
}
