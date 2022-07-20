// <copyright file="Computer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Samples.WindowsService01
{
    internal class Computer
    {
        private static readonly TimeSpan PauseBetweenWorkloads = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PauseBetweenStatusChecks = TimeSpan.FromSeconds(1);

        private DateTimeOffset _lastWorkloadTime = DateTimeOffset.MinValue;
        private bool _isActive;
        private bool _mustStop;

        public async Task Run()
        {
            _isActive = true;
            _mustStop = false;

            while (!_mustStop)
            {
                try
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;

                    if (_isActive && (now - _lastWorkloadTime) > PauseBetweenWorkloads)
                    {
                        _lastWorkloadTime = now;
                        await Task.Run(DoSomeWork);
                    }

                    await Task.Delay(PauseBetweenStatusChecks);
                }
                catch
                {
                }
            }
        }

        public void Pause()
        {
            _isActive = false;
        }

        public void Resume()
        {
            _isActive = true;
        }

        public void Stop()
        {
            _mustStop = true;
        }

        private static void DoSomeWork()
        {
            int val = 0;
            Random rnd = new Random();
            const int MaxIterations = 100000000;
            for (int i = 0; i < MaxIterations; i++)
            {
                int rn = rnd.Next(0, int.MaxValue);
                val ^= rn;
            }
        }
    }
}
