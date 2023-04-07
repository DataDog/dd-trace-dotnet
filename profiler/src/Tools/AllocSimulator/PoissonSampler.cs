// <copyright file="PoissonSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class PoissonSampler : ISampler
    {
        private Random _randomizer = new Random(DateTime.Now.Millisecond);
        private ulong _totalAllocatedAmount;
        private ulong _threshold;  // number of bytes until the next sample
        private float _meanPoisson;

        public PoissonSampler(int meanPoisson)
        {
            _meanPoisson = meanPoisson * 1024;
            _threshold = GetNextThreshold();
        }

        public bool ShouldSample(long size)
        {
            _totalAllocatedAmount += (ulong)size;
            var shouldSample = _totalAllocatedAmount > _threshold;

            if (shouldSample)
            {
                _totalAllocatedAmount = 0;
                _threshold = GetNextThreshold();
            }

            return shouldSample;
        }

        public ulong GetNextThreshold()
        {
            var q = _randomizer.NextDouble();

            ulong next = (ulong)((-Math.Log(1 - q)) * _meanPoisson) + 1;
            return next;
        }
    }
}
