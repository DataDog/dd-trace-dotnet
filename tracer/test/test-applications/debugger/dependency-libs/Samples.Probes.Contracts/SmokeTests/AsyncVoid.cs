// <copyright file="AsyncVoid.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class AsyncVoid : IAsyncRun
    {
        public async Task RunAsync()
        {
            try
            {
                await VoidTaskMethod();
            }
            catch (AccessViolationException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        private async Task VoidTaskMethod()
        {
            try
            {
                await Task.Delay(20);
                var methodName = nameof(VoidTaskMethod);
                await Task.Run(() => { VoidMethod(methodName); });
            }
            catch (AccessViolationException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        private async void VoidMethod(string caller)
        {
            await Task.Delay(20);
            Console.WriteLine($"{nameof(VoidMethod)} is called from {caller}");
        }
    }
}
