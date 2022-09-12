// <copyright file="AsyncGenericStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class AsyncGenericStruct : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await new NestedAsyncGenericStruct<Generic>().Method(new Generic { Message = "NestedAsyncGenericStruct" }, $".{nameof(RunAsync)}");
        }

        internal struct NestedAsyncGenericStruct<T>
            where T : IGeneric
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData(skip: true)]
            public async Task<string> Method<TGeneric>(TGeneric generic, string input)
                where TGeneric : IGeneric
            {
                var output = generic.Message + input + ".";
                await Task.Delay(20);
                return output + nameof(Method);
            }
        }
    }
}
