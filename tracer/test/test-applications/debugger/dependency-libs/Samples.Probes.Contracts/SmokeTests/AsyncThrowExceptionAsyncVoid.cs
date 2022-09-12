// <copyright file="AsyncThrowExceptionAsyncVoid.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class AsyncThrowExceptionAsyncVoid : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Task.Run(() => { VoidMethod(nameof(RunAsync)); });
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData(skip: true)]
        private async void VoidMethod(string caller)
        {
            await Task.Delay(20);
            throw new InvalidOperationException($"Exception from {caller}.{nameof(VoidMethod)}");
        }
    }
}
