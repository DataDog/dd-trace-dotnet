// <copyright file="PoissonUpscaler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class PoissonUpscaler : UpscalerBase
    {
        private float _meanPoisson;

        public PoissonUpscaler(int meanPoisson)
        {
            _meanPoisson = meanPoisson * 1024;
        }

        protected override void OnUpscale(AllocInfo sampled, ref AllocInfo upscaled)
        {
            var averageSize = (double)sampled.Size / (double)sampled.Count;
            var scale = 1 / (1 - Math.Exp(-averageSize / _meanPoisson));

            upscaled.Size = (long)((float)sampled.Size * scale);
            upscaled.Count = (int)((float)sampled.Count * scale);
        }
    }
}
