// <copyright file="AsyncGenericMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class AsyncGenericMethod : IAsyncRun
    {
        private const string ClassName = "AsyncWithGenericMethod";

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await Method(ClassName, $".{nameof(RunAsync)}");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData(skip: true)]
        public async Task<string> Method<T>(T obj, string input)
        {
            var output = obj + input + ".";
            await Task.Delay(20);
            return output + nameof(Method);
        }
    }
}
