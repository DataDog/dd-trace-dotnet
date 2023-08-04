// <copyright file="AsyncMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class AsyncMethod
    {
        private async Task<int> GetTime()
        {
            await Task.Delay(500);

            var result = await Task.Run(
                () =>
                {
                    var rand = new Random().Next();
                    return rand;
                }).ConfigureAwait(false);

            return result;
        }
    }
}
