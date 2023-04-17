// <copyright file="FixedUpscaler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class FixedUpscaler : UpscalerBase
    {
        protected override void OnUpscale(AllocInfo sampled, ref AllocInfo upscaled)
        {
            upscaled.Size = (long)((sampled.Size * _totalAllocatedAmount) / _totalSampledSize);
            upscaled.Count = (int)((sampled.Count * _totalAllocatedAmount) / _totalSampledSize);
        }
    }
}
