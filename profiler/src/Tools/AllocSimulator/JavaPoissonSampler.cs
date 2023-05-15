// <copyright file="JavaPoissonSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    // from https://github.com/openjdk/jdk/blob/master/src/hotspot/share/runtime/threadHeapSampler.cpp
    public class JavaPoissonSampler : ISampler
    {
        private const ulong MeanSamplingSize = 512 * 1024;      // 512 KB is the mean of the distribution
        private const double MinusLog2 = -0.6931471805599453;   // = - ln(2)

        // for fast randomizer
        private const ulong PrngMult = 0x5DEECE66D;
        private const ulong PrngAdd = 0xB;
        private const ulong PrngModMask = (1 << 48) - 1;

        private ulong _totalAllocatedAmount;
        private ulong _threshold;  // number of bytes until the next sample
        private ulong _random;

        public JavaPoissonSampler()
        {
            _random = (ulong)Random.Shared.NextInt64(1, 281474976710656); // from 1 to 2^48
            _threshold = GetNextThreshold();
        }

        public string GetDescription()
        {
            throw new NotImplementedException("The current implementation is a port of Java but does not work");
        }

        public string GetName()
        {
            throw new NotImplementedException("The current implementation is a port of Java but does not work");
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

        // Generates a geometric variable with the specified mean (512K by default).
        // This is done by generating a random number between 0 and 1 and applying
        // the inverse cumulative distribution function for an exponential.
        // Specifically: Let m be the inverse of the sample interval, then
        // the probability distribution function is m*exp(-mx) so the CDF is
        // p = 1 - exp(-mx), so
        // q = 1 - p = exp(-mx)
        // log_e(q) = -mx
        // -log_e(q)/m = x
        // log_2(q) * (-log_e(2) * 1/m) = x
        // In the code, q is actually in the range 1 to 2**26, hence the -26 below
        private ulong GetNextThreshold()
        {
            _random = GetNextRandom(_random);

            // Take the top 26 bits as the random number
            // (This plus a 1<<58 sampling bound gives a max possible step of
            // 5194297183973780480 bytes.  In this case,
            // for sample_parameter = 1<<19, max possible step is
            // 9448372 bytes (24 bits).

            // 48 is the number of bits in prng
            // The uint32_t cast is to prevent a (hard-to-reproduce) NAN
            // under piii debug for some binaries.
            double q = (uint)(_random >> (48 - 26)) + 1.0;
            // Put the computed p-value through the CDF of a geometric.
            // For faster performance (save ~1/20th exec time), replace
            // min(0.0, FastLog2(q) - 26)  by  (Fastlog2(q) - 26.000705)
            // The value 26.000705 is used rather than 26 to compensate
            // for inaccuracies in FastLog2 which otherwise result in a
            // negative answer.
            double log_val = (Log2(q) - 26);
            if (log_val > 0.0)
            {
                log_val = 0.0;
            }

            double result = (log_val * (MinusLog2 * (MeanSamplingSize))) + 1;
            ulong interval = (ulong)result;
            _threshold = interval;
            return _threshold;
        }

        // Returns the next prng value.
        // pRNG is: aX+b mod c with a = 0x5DEECE66D, b =  0xB, c = 1<<48
        // This is the lrand64 generator.
        private ulong GetNextRandom(ulong random)
        {
            return ((PrngMult * random) + PrngAdd) & PrngModMask;
        }

        private double Log2(double d)
        {
            return Math.Log2(d);
        }
    }
}
