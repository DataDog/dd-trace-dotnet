// <copyright file="FixedSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class FixedSampler : ISampler
    {
        // the current .NET fixed threshold for AllocationTick is 100 KB
        private const long Threshold = 100 * 1024;

        private long _totalAllocatedAmount;

        public string GetDescription()
        {
            return $"Fixed 100 KB sampling";
        }

        public virtual string GetName()
        {
            return "Fixed";
        }

        public bool ShouldSample(long size)
        {
            _totalAllocatedAmount += size;
            var shouldSample = _totalAllocatedAmount > Threshold;

            if (shouldSample)
            {
                _totalAllocatedAmount = 0;
            }

            return shouldSample;
        }
    }
}
