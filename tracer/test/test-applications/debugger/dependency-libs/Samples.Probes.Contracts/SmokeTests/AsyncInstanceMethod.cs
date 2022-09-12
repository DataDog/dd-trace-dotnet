// <copyright file="AsyncInstanceMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.Contracts.SmokeTests
{
    [LineProbeTestData(lineNumber: 26)]
    public class AsyncInstanceMethod : IAsyncRun
    {
        private const string ClassName = "AsyncInstanceMethod";

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await Method($"{ClassName}.{nameof(RunAsync)}");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<string> Method(string input)
        {
            var output = input + ".";
            await Task.Delay(20);
            return output + nameof(Method);
        }
    }
}
