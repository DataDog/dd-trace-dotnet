// <copyright file="UpscalerBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public abstract class UpscalerBase : IUpscaler
    {
#pragma warning disable SA1401 // Fields should be private
        protected long _totalAllocatedAmount;
        protected long _totalSampledSize;
#pragma warning restore SA1401 // Fields should be private

        // the key is the type+key of the allocation
        private Dictionary<string, AllocInfo> _sampledAllocations;

        public UpscalerBase()
        {
            _sampledAllocations = new Dictionary<string, AllocInfo>();
            _totalAllocatedAmount = 0;
            _totalSampledSize = 0;
        }

        public void OnAllocationTick(string type, int key, long size, long allocationsAmount)
        {
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

        public IEnumerable<AllocInfo> GetUpscaledAllocs()
        {
            foreach (var group in _sampledAllocations.Keys)
            {
                var sampled = _sampledAllocations[group];

                // let the child class implement the upscaling strategy
                var upscaled = new AllocInfo()
                {
                    Key = sampled.Key,
                    Type = sampled.Type,
                };
                OnUpscale(sampled, ref upscaled);

                yield return upscaled;
            }
        }

        public IEnumerable<AllocInfo> GetSampledAllocs()
        {
            foreach (var group in _sampledAllocations.Keys)
            {
                var sampled = _sampledAllocations[group];

                // let the child class implement the upscaling strategy
                var upscaled = new AllocInfo()
                {
                    Key = sampled.Key,
                    Type = sampled.Type,
                    Count = sampled.Count,
                    Size = sampled.Size
                };

                yield return upscaled;
            }
        }

        public void Upscale(AllocInfo sampled, ref AllocInfo upscaled)
        {
            OnUpscale(sampled, ref upscaled);
        }

        protected abstract void OnUpscale(AllocInfo sampled, ref AllocInfo upscaled);
    }
}
