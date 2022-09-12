// <copyright file="AsyncCallChain.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class AsyncCallChain : IAsyncRun
    {
        private int _chain;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            _chain++;
            await Async1(_chain);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<int> Async1(int chain)
        {
            chain++;
            var result = await Async2(chain);
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<int> Async2(int chain)
        {
            await Task.Delay(20);
            chain++;
            return chain;
        }
    }
}
