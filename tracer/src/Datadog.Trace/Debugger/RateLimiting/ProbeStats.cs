// <copyright file="ProbeStats.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Concurrent;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal class ProbeStats
    {
        private long _totalRequests;
        private long _accepted;
        private long _rejectedByKillSwitch;
        private long _rejectedByPrefilter;
        private long _rejectedByGlobalBudget;
        private long _rejectedByCircuit;
        private long _rejectedBySampler;
        private long _fullCaptures;
        private long _lightCaptures;
        private long _totalElapsedTicks;

        public long TotalRequests => _totalRequests;

        public long Accepted => _accepted;

        public long RejectedByKillSwitch => _rejectedByKillSwitch;

        public long RejectedByPrefilter => _rejectedByPrefilter;

        public long RejectedByGlobalBudget => _rejectedByGlobalBudget;

        public long RejectedByCircuit => _rejectedByCircuit;

        public long RejectedBySampler => _rejectedBySampler;

        public long FullCaptures => _fullCaptures;

        public long LightCaptures => _lightCaptures;

        public long TotalElapsedTicks => _totalElapsedTicks;

        public double AcceptanceRate => _totalRequests > 0 ? (_accepted / (double)_totalRequests) * 100.0 : 0.0;

        public double AverageElapsedTicks => _accepted > 0 ? _totalElapsedTicks / (double)_accepted : 0.0;

        public void RecordSample(bool accepted, CaptureBehaviour behaviour, long elapsedTicks)
        {
            System.Threading.Interlocked.Increment(ref _totalRequests);

            if (accepted)
            {
                System.Threading.Interlocked.Increment(ref _accepted);
                System.Threading.Interlocked.Add(ref _totalElapsedTicks, elapsedTicks);

                if (behaviour == CaptureBehaviour.Full)
                {
                    System.Threading.Interlocked.Increment(ref _fullCaptures);
                }
                else if (behaviour == CaptureBehaviour.Light)
                {
                    System.Threading.Interlocked.Increment(ref _lightCaptures);
                }
            }
        }

        public void RecordRejection(RejectionReason reason)
        {
            System.Threading.Interlocked.Increment(ref _totalRequests);

            switch (reason)
            {
                case RejectionReason.KillSwitch:
                    System.Threading.Interlocked.Increment(ref _rejectedByKillSwitch);
                    break;
                case RejectionReason.ThreadLocalPrefilter:
                    System.Threading.Interlocked.Increment(ref _rejectedByPrefilter);
                    break;
                case RejectionReason.GlobalBudgetExhausted:
                    System.Threading.Interlocked.Increment(ref _rejectedByGlobalBudget);
                    break;
                case RejectionReason.CircuitOpen:
                    System.Threading.Interlocked.Increment(ref _rejectedByCircuit);
                    break;
                case RejectionReason.AdaptiveSampler:
                    System.Threading.Interlocked.Increment(ref _rejectedBySampler);
                    break;
            }
        }
    }
}
