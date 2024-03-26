// <copyright file="IUpscaler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public interface IUpscaler
    {
        public void OnAllocationTick(string type, int key, long size, long allocationsAmount);

        public IEnumerable<AllocInfo> GetUpscaledAllocs();

        public IEnumerable<AllocInfo> GetSampledAllocs();

        public void Upscale(AllocInfo sampled, ref AllocInfo upscaled);
    }
}
