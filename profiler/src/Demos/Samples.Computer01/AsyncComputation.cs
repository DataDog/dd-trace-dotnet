// <copyright file="AsyncComputation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class AsyncComputation : ScenarioBase
    {
        public AsyncComputation(int nbThreads)
            : base(nbThreads)
        {
        }

        public override void OnProcess()
        {
            Compute1().Wait();
        }

        private async Task Compute1()
        {
            ConsumeCPU();
            await Compute2();
            ConsumeCPUAfterCompute2();
        }

        private async Task Compute2()
        {
            ConsumeCPU();
            await Compute3();
            ConsumeCPUAfterCompute3();
        }

        private async Task Compute3()
        {
            await Task.Delay(1000);
            ConsumeCPUinCompute3();
            Console.WriteLine("Exit Compute3");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConsumeCPUinCompute3()
        {
            ConsumeCPU();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConsumeCPUAfterCompute2()
        {
            ConsumeCPU();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ConsumeCPUAfterCompute3()
        {
            ConsumeCPU();
        }

        private void ConsumeCPU()
        {
            for (int i = 0; i < 1000; i++)
            {
                for (int j = 0; j < 1000000; j++)
                {
                    Math.Sqrt((double)j);
                }
            }
        }
    }
}
