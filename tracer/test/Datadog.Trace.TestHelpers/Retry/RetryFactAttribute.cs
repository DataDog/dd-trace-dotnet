// <copyright file="RetryFactAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.dev/JoshKeegan/xRetry

using System;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers.Retry
{
    /// <summary>
    /// Attribute that is applied to a method to indicate that it is a fact that should be run
    /// by the test runner up to MaxRetries times, until it succeeds.
    /// </summary>
    [XunitTestCaseDiscoverer("Datadog.Trace.TestHelpers.Retry.RetryFactDiscoverer", "Datadog.Trace.TestHelpers")]
    [AttributeUsage(AttributeTargets.Method)]
    public class RetryFactAttribute : FactAttribute
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="maxRetries">The number of times to run a test for until it succeeds</param>
        /// <param name="delayBetweenRetriesMs">The amount of time (in ms) to wait between each test run attempt</param>
        public RetryFactAttribute(int maxRetries = 3, int delayBetweenRetriesMs = 0)
        {
            if (maxRetries < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetries) + " must be >= 1");
            }

            if (delayBetweenRetriesMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayBetweenRetriesMs) + " must be >= 0");
            }

            MaxRetries = maxRetries;
            DelayBetweenRetriesMs = delayBetweenRetriesMs;
        }

        public int MaxRetries { get; }

        public int DelayBetweenRetriesMs { get; }
    }
}
