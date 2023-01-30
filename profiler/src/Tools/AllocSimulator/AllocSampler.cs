// <copyright file="AllocSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class AllocSampler : ISampler
    {
        // the key is the type+key of the allocation
        private Dictionary<string, AllocInfo> _sampledAllocations;
        private long _totalAllocatedAmount;
        private long _totalSampledSize;

        public AllocSampler()
        {
            _sampledAllocations = new Dictionary<string, AllocInfo>();
            _totalAllocatedAmount = 0;
            _totalSampledSize = 0;
        }

        public void OnAllocationTick(string type, int key, long size, long allocationsAmount)
        {
            // TODO: simulate the sampler
            var dictKey = $"{type}+{key}";
            if (!_sampledAllocations.TryGetValue(dictKey, out var info))
            {
                info = new AllocInfo()
                {
                    Key = key,
                    Type = type,
                    Size = 0,
                    Count = 0
                };

                _sampledAllocations[dictKey] = info;
            }

            info.Size += size;
            info.Count += 1;

            // used to upscale
            _totalAllocatedAmount += allocationsAmount;
            _totalSampledSize += size;
        }

        public IEnumerable<AllocInfo> GetAllocs()
        {
            // TODO: implement the upscale
            foreach (var group in _sampledAllocations.Keys)
            {
                yield return _sampledAllocations[group];
            }
        }
    }
}
