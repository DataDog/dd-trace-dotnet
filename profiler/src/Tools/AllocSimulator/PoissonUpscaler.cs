// <copyright file="PoissonUpscaler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class PoissonUpscaler : UpscalerBase
    {
        private const float MeanSamplingSize = 512 * 1024;      // 512 KB is the mean of the distribution for Java

        protected override void OnUpscale(AllocInfo sampled, ref AllocInfo upscaled)
        {
            var averageSize = (double)sampled.Size / (double)sampled.Count;
            var scale = 1 / (1 - Math.Exp(-averageSize / MeanSamplingSize));

            upscaled.Size = (long)((float)sampled.Size * scale);
            upscaled.Count = (int)((float)sampled.Count * scale);
        }
    }
}
