// <copyright file="Engine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace AllocSimulator
{
    public class Engine : IEngine
    {
        private const long Threshold = 100 * 1024;

        private IAllocProvider _provider;
        private ISampler _sampler;
        private long _cumulatedSize;
        private IDictionary<string, AllocInfo> _allocations;

        // TODO support a list of ISampler to compare non upscaled and upscaled for example
        public Engine(IAllocProvider provider, ISampler sampler)
        {
            _provider = provider;
            _sampler = sampler;
            _cumulatedSize = 0;
            _allocations = new Dictionary<string, AllocInfo>();
        }

        public IEnumerable<AllocInfo> GetAllocations()
        {
            foreach (var key in _allocations.Keys)
            {
                yield return _allocations[key];
            }
        }

        public void Run()
        {
            _cumulatedSize = 0;

            // Since GetAllocation() will return different allocations when random sets are defined,
            // it is needed to keep track of them so GetAllocations() will return the same sequence
            _allocations = new Dictionary<string, AllocInfo>();

            foreach (var alloc in _provider.GetAllocations())
            {
                for (int i = 0; i < alloc.Count; i++)
                {
                    var key = $"{alloc.Type}+{alloc.Key}";
                    if (!_allocations.TryGetValue(key, out var info))
                    {
                        info = new AllocInfo()
                        {
                            Key = alloc.Key,
                            Type = alloc.Type,
                            Count = 0,
                            Size = 0
                        };

                        _allocations[key] = info;
                    }

                    info.Size = info.Size + alloc.Size;
                    info.Count = info.Count + alloc.Count;

                    if (ShouldSample(alloc.Size))
                    {
                        _sampler.OnAllocationTick(alloc.Type, alloc.Key, alloc.Size, _cumulatedSize);
                        _cumulatedSize = 0;
                    }
                }
            }
        }

        private bool ShouldSample(long size)
        {
            _cumulatedSize += size;
            return (_cumulatedSize >= Threshold);
        }
    }
}

#pragma warning restore CS8602 // Dereference of a possibly null reference.
