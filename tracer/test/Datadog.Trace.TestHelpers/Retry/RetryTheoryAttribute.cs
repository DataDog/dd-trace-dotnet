// <copyright file="RetryTheoryAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.dev/JoshKeegan/xRetry

using System;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers.Retry
{
    /// <summary>
    /// Attribute that is applied to a method to indicate that it is a theory that should be run
    /// by the test runner up to MaxRetries times, until it succeeds.
    /// </summary>
    [XunitTestCaseDiscoverer("Datadog.Trace.TestHelpers.Retry.RetryTheoryDiscoverer", "Datadog.Trace.TestHelpers")]
    [AttributeUsage(AttributeTargets.Method)]
    public class RetryTheoryAttribute : RetryFactAttribute
    {
        /// <inheritdoc/>
        public RetryTheoryAttribute(int maxRetries = 3, int delayBetweenRetriesMs = 0)
            : base(maxRetries, delayBetweenRetriesMs)
        {
        }
    }
}
