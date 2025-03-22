// <copyright file="SyncOverAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public class SyncOverAsync : ScenarioBase
    {
        private readonly bool _useResultProperty = false;

        public SyncOverAsync(int nbThreads, bool useResultProperty)
            : base(nbThreads)
        {
            _useResultProperty = useResultProperty;
        }

        public override void OnProcess()
        {
            Console.WriteLine("--> Before compute");
            int result;
            if (_useResultProperty)
            {
                result = Compute1().Result;
            }
            else
            {
                result = Compute1().GetAwaiter().GetResult();
            }

            Console.WriteLine($"<-- After compute = {result}");
        }

        private async Task<int> Compute1()
        {
            var result = await Compute2();
            return result;
        }

        private async Task<int> Compute2()
        {
            var result = await Compute3();
            return result;
        }

        private async Task<int> Compute3()
        {
            Console.WriteLine("in Compute3");
            await Task.Delay(1000);

            return 42;
        }
    }
}
