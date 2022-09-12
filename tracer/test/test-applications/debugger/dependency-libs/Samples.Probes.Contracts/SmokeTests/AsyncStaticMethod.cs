// <copyright file="AsyncStaticMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class AsyncStaticMethod : IAsyncRun
    {
        private const string ClassName = "AsyncStaticMethod";

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public static async Task<string> Method(string input)
        {
            var output = input + ".";
            await Task.Delay(20);
            return output + nameof(Method);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await Method($"{ClassName}.{nameof(RunAsync)}");
        }
    }
}
