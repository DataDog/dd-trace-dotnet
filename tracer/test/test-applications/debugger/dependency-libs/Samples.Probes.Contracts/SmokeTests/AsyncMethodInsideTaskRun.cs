// <copyright file="AsyncMethodInsideTaskRun.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class AsyncMethodInsideTaskRun : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await RunInsideTask();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<string> RunInsideTask()
        {
            return await Task.Run(
                 async () =>
                 {
                     var local1 = $"{nameof(RunInsideTask)}: Start";
                     var res = await Method(local1.Substring(0, nameof(RunInsideTask).Length));
                     return res + ": Finished";
                 });
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<string> Method(string seed)
        {
            string result = seed + " ";
            await Task.Delay(20);
            for (int i = 0; i < 5; i++)
            {
                result += i;
            }

            return result;
        }
    }
}
