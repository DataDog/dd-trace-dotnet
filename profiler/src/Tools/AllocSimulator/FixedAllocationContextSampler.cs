// <copyright file="FixedAllocationContextSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class FixedAllocationContextSampler : PoissonSampler
    {
        private readonly long _allocContextSize;
        private long _bytesInContext;

        public FixedAllocationContextSampler(int meanPoisson, int allocContextSize)
            : base(meanPoisson)
        {
            _allocContextSize = allocContextSize;
            _bytesInContext = 0;
        }

        public override string GetDescription()
        {
            return $"Allocation context {_allocContextSize} + {base.GetDescription()}";
        }

        public override string GetName()
        {
            return "Alloc Context + Poisson";
        }

        public override bool OnShouldSample(long size)
        {
            // check if the allocation will fit in the AllocationContext
            if (_bytesInContext + size <= _allocContextSize)
            {
                _bytesInContext += size;
                return false;
            }

            // need to switch to a new allocation context
            _bytesInContext = 0;

            return base.OnShouldSample(size);
        }
    }
}
